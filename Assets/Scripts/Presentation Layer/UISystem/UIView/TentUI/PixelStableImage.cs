using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// 픽셀 퍼펙트 캔버스에서 움직이는 Image의 메시 재생성 비용을 줄인 Image. 화면에 그려지는 결과는 Image와 같다.
//
// 픽셀 퍼펙트 캔버스는 그래픽이 움직인 프레임에 크기 변경 알림(OnRectTransformDimensionsChange)을 보내고(그 프레임의 캔버스 갱신 뒤에 온다),
// 그래픽은 다음 프레임의 캔버스 갱신에서 그때의 위치로 메시를 다시 만든다. 이 흐름은 그대로 두고 다시 만드는 순간에만 손을 댄다.
//
// 1) 화면이 정수 픽셀만큼만 움직였으면 메시를 그대로 둔다.
//    단순(Simple) Image 메시는 픽셀 보정 사각형(GetPixelAdjustedRect)과 스프라이트·색 같은 자기 설정으로만 정해지는데,
//    화면이 정수 픽셀만큼만 움직이면(마우스 드래그) 보정 사각형이 부동소수 오차(실측 0.001픽셀 미만)만큼만 달라진다.
//    다시 만들 차례에 지금 올라가 있는 메시의 보정 사각형과 지금 값이 화면에서 SameSnapTolerancePixels 안이고,
//    그 사이 정점 변경 요청이 이동 알림에서만 왔으면 다시 만들어도 같은 픽셀에 그려지므로 그대로 둔다.
//    비교 기준은 직전 프레임이 아니라 메시를 만든 순간이라 오차가 쌓이지 않는다.
//    색·스프라이트 등 다른 변경은 SetVerticesDirty를 직접 부르므로 원래대로 다시 만든다.
//    줌처럼 전부 다시 만들어야 하는 프레임에서는 판정이 연달아 "다시 만들기"로 나오므로, 그 프레임의 나머지는 판정하지 않는다.
//
// 2) 보정 사각형을 같은 자리에 겹친 그림끼리 나눠 쓴다.
//    보정 사각형을 구하는 호출은 깊은 계층에서 비싸다(원래 재생성 비용의 절반쯤). 보정 사각형은 자기 로컬 사각형과
//    월드 변환(자기 로컬 위치·회전·크기 + 부모 사슬)과 캔버스로 정해지므로, 같은 캔버스 갱신 안에서 직전 그래픽과
//    부모·캔버스가 같고 로컬 사각형·위치·회전·크기가 비트까지 같으면 직전에 구한 값이 곧 자기 값이다.
//    메시를 다시 만들 때도 이 값을 쓰려고, 단순 스프라이트의 사각형 메시는 Image.GenerateSimpleSprite와 같은 식으로 여기서 만든다
//    (비율 유지·스프라이트 메시·다른 Image 종류·스프라이트 없음은 원래 Image가 만든다).
public class PixelStableImage : Image
{
    // 래스터라이저의 서브픽셀 격자(보통 1/256픽셀) 반 칸(약 0.002픽셀)보다 작게 잡는다.
    private const float SameSnapTolerancePixels = 0.001f;

    // 한 프레임에 그대로 둔 그래픽 없이 이만큼 "다시 만들기" 판정이 나오면 전부 다시 만드는 프레임으로 본다.
    // 노드 하나가 모션으로 혼자 움직일 때(그림 최대 7장)는 넘지 않게 잡는다.
    // 잘못 봐도(실은 그대로 둘 수 있었어도) 원래 Image처럼 다시 만들 뿐이라 화면은 같다.
    private const int RejectsBeforeSkippingCheck = 8;

    // 바로 앞 프레임도 전부 다시 만드는 프레임이었으면(줌이 이어지는 중) 이만큼만 판정하고 판단한다.
    private const int RejectsBeforeSkippingCheckWhenSustained = 2;

    private static readonly List<Component> meshModifierBuffer = new List<Component>(4);

    // 이번 프레임에 그대로 둔 횟수·다시 만들기로 판정한 횟수(같이 움직인 그래픽은 판정이 대개 같다)
    private static int judgedFrame = -1;
    private static int keptThisFrame;
    private static int rejectedThisFrame;
    private static bool wasAllRebuildPreviousFrame;

    // 직전 그래픽의 픽셀 보정 사각형과 그때의 조건(같은 자리에 겹친 그림들이 나눠 쓴다)
    private static int canvasUpdateSerial;
    private static int sharedSerial = -1;
    private static Transform sharedParent;
    private static Canvas sharedCanvas;
    private static Rect sharedRect;
    private static Vector3 sharedLocalPosition;
    private static Quaternion sharedLocalRotation;
    private static Vector3 sharedLocalScale;
    private static Rect sharedAdjustedRect;
    private static bool hasSharedPixelsPerUnit;
    private static float sharedPixelsPerUnit;

    // 지금 CanvasRenderer에 올라가 있는 메시의 보정 사각형(메시를 만들 때 쓴 값)
    private bool hasBuiltSnapshot;
    private Vector2 builtMin;
    private Vector2 builtMax;

    // 켜진 메시 수정 컴포넌트가 있는지(위치에 따라 정점을 바꿀 수 있으므로 있으면 늘 다시 만든다)
    private bool isMeshModifierStateKnown;
    private bool hasActiveMeshModifier;

    // 이동 알림을 처리하는 중인지, 마지막으로 메시를 만든 뒤 이동 알림이 아닌 정점 변경 요청이 있었는지
    private bool isHandlingMoveNotification;
    private bool hasOtherVertexChange;

    // UpdateGeometry 안에서 메시를 만드는 중인지, 그때 쓴 보정 사각형
    private bool isUpdatingGeometry;
    private bool hasCapturedBounds;
    private Vector2 capturedMin;
    private Vector2 capturedMax;

    static PixelStableImage()
    {
        // 캔버스 갱신 한 번 안에서는 그래픽 재생성 사이에 위치가 바뀌지 않는다. 갱신이 새로 시작되면 나눠 쓰던 값을 버린다.
        Canvas.preWillRenderCanvases += OnCanvasUpdateStarting;
    }

    public override void SetVerticesDirty()
    {
        if (false == isHandlingMoveNotification)
        {
            hasOtherVertexChange = true;
            // 메시 수정 컴포넌트는 켜지고 꺼질 때 SetVerticesDirty를 부른다(UGUI BaseMeshEffect·UIEffect 모두). 다음에 만들 때 다시 확인한다.
            isMeshModifierStateKnown = false;
        }

        base.SetVerticesDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        isHandlingMoveNotification = true;
        try
        {
            base.OnRectTransformDimensionsChange();
        }
        finally
        {
            isHandlingMoveNotification = false;
        }
    }

    // 그래픽 재생성 대기열은 그래픽마다 PreRender·LatePreRender 두 단계를 부르는데, Graphic은 PreRender에서만 일을 한다.
    public override void Rebuild(CanvasUpdate _update)
    {
        if (CanvasUpdate.PreRender != _update)
            return;

        base.Rebuild(_update);
    }

    protected override void UpdateGeometry()
    {
        BeginFrameIfNeeded();

        if (false == hasOtherVertexChange && hasBuiltSnapshot && isMeshModifierStateKnown && false == hasActiveMeshModifier &&
            false == IsAllRebuildFrame() && IsSameSnapAsBuiltMesh())
        {
            return;
        }

        hasCapturedBounds = false;
        isUpdatingGeometry = true;
        try
        {
            base.UpdateGeometry();
        }
        finally
        {
            isUpdatingGeometry = false;
        }

        if (false == isMeshModifierStateKnown)
        {
            hasActiveMeshModifier = HasActiveMeshModifier();
            isMeshModifierStateKnown = true;
        }

        hasOtherVertexChange = false;
        hasBuiltSnapshot = hasCapturedBounds;
        builtMin = capturedMin;
        builtMax = capturedMax;
    }

    protected override void OnPopulateMesh(VertexHelper _toFill)
    {
        if (isUpdatingGeometry && TryPopulateSimpleSprite(_toFill))
            return;

        base.OnPopulateMesh(_toFill);
    }

    protected override void OnDisable()
    {
        // 꺼지면 CanvasRenderer 메시가 지워지므로, 다시 켜질 때는 원래대로 한 번 만든다.
        hasBuiltSnapshot = false;
        isMeshModifierStateKnown = false;
        base.OnDisable();
    }

    // Image.OnPopulateMesh가 단순 스프라이트에서 타는 경로(GenerateSimpleSprite + GetDrawingDimensions)와 같은 식·같은 순서로 만든다.
    // 다른 점은 픽셀 보정 사각형을 나눠 쓰는 것 하나다. 비율 유지는 사각형을 한 번 더 바꾸므로 원래 Image에 맡긴다.
    private bool TryPopulateSimpleSprite(VertexHelper _toFill)
    {
        if (Type.Simple != type || useSpriteMesh || preserveAspect)
            return false;

        Sprite activeSprite = overrideSprite;
        if (null == activeSprite)
            return false;

        Vector4 padding = DataUtility.GetPadding(activeSprite);
        Vector2 size = new Vector2(activeSprite.rect.width, activeSprite.rect.height);

        Rect r = GetSharedPixelAdjustedRect();

        int spriteW = Mathf.RoundToInt(size.x);
        int spriteH = Mathf.RoundToInt(size.y);

        Vector4 v = new Vector4(
            padding.x / spriteW,
            padding.y / spriteH,
            (spriteW - padding.z) / spriteW,
            (spriteH - padding.w) / spriteH);

        v = new Vector4(
            r.x + r.width * v.x,
            r.y + r.height * v.y,
            r.x + r.width * v.z,
            r.y + r.height * v.w);

        Vector4 uv = DataUtility.GetOuterUV(activeSprite);

        Color color32 = color;
        _toFill.Clear();
        _toFill.AddVert(new Vector3(v.x, v.y), color32, new Vector2(uv.x, uv.y));
        _toFill.AddVert(new Vector3(v.x, v.w), color32, new Vector2(uv.x, uv.w));
        _toFill.AddVert(new Vector3(v.z, v.w), color32, new Vector2(uv.z, uv.w));
        _toFill.AddVert(new Vector3(v.z, v.y), color32, new Vector2(uv.z, uv.y));

        _toFill.AddTriangle(0, 1, 2);
        _toFill.AddTriangle(2, 3, 0);

        // 여백 없는 스프라이트는 네 정점이 보정 사각형의 네 꼭짓점이라 화면 픽셀 경계에 놓인다. 이때만 다음에 메시를 그대로 둘 수 있다.
        if (0f == padding.x && 0f == padding.y && 0f == padding.z && 0f == padding.w)
        {
            capturedMin = new Vector2(r.xMin, r.yMin);
            capturedMax = new Vector2(r.xMax, r.yMax);
            hasCapturedBounds = true;
        }

        return true;
    }

    private static void BeginFrameIfNeeded()
    {
        int frame = Time.frameCount;
        if (frame == judgedFrame)
            return;

        wasAllRebuildPreviousFrame = frame == judgedFrame + 1 && IsAllRebuildFrame();
        judgedFrame = frame;
        keptThisFrame = 0;
        rejectedThisFrame = 0;
    }

    private static int GetRejectsBeforeSkippingCheck()
    {
        return wasAllRebuildPreviousFrame ? RejectsBeforeSkippingCheckWhenSustained : RejectsBeforeSkippingCheck;
    }

    private static bool IsAllRebuildFrame()
    {
        return 0 == keptThisFrame && GetRejectsBeforeSkippingCheck() <= rejectedThisFrame;
    }

    // 지금 픽셀 보정 사각형이 올라가 있는 메시의 것과 화면에서 SameSnapTolerancePixels 안인지
    private bool IsSameSnapAsBuiltMesh()
    {
        Rect adjustedRect = GetSharedPixelAdjustedRect();
        float difference = Mathf.Abs(adjustedRect.xMin - builtMin.x);
        difference = Mathf.Max(difference, Mathf.Abs(adjustedRect.yMin - builtMin.y));
        difference = Mathf.Max(difference, Mathf.Abs(adjustedRect.xMax - builtMax.x));
        difference = Mathf.Max(difference, Mathf.Abs(adjustedRect.yMax - builtMax.y));

        // 로컬 단위 차이를 화면 픽셀로 바꿔 비교한다(확대·축소 배율에 따라 로컬 단위당 픽셀 수가 달라진다).
        // 배율이 0이라 NaN이 나오면 비교가 거짓이 되어 다시 만든다.
        bool isSame = 0f == difference || difference * GetSharedPixelsPerUnit() <= SameSnapTolerancePixels;

        if (isSame)
            keptThisFrame++;
        else
            rejectedThisFrame++;

        return isSame;
    }

    private static void OnCanvasUpdateStarting()
    {
        canvasUpdateSerial++;
    }

    private Rect GetSharedPixelAdjustedRect()
    {
        RectTransform ownTransform = rectTransform;
        Transform parent = ownTransform.parent;
        Canvas ownCanvas = canvas;
        Rect rect = ownTransform.rect;
        Vector3 localPosition = ownTransform.localPosition;
        Quaternion localRotation = ownTransform.localRotation;
        Vector3 localScale = ownTransform.localScale;

        if (sharedSerial == canvasUpdateSerial &&
            ReferenceEquals(parent, sharedParent) && ReferenceEquals(ownCanvas, sharedCanvas) &&
            rect.x == sharedRect.x && rect.y == sharedRect.y && rect.width == sharedRect.width && rect.height == sharedRect.height &&
            localPosition.x == sharedLocalPosition.x && localPosition.y == sharedLocalPosition.y && localPosition.z == sharedLocalPosition.z &&
            localRotation.x == sharedLocalRotation.x && localRotation.y == sharedLocalRotation.y &&
            localRotation.z == sharedLocalRotation.z && localRotation.w == sharedLocalRotation.w &&
            localScale.x == sharedLocalScale.x && localScale.y == sharedLocalScale.y && localScale.z == sharedLocalScale.z
            )
        {
            return sharedAdjustedRect;
        }

        sharedSerial = canvasUpdateSerial;
        sharedParent = parent;
        sharedCanvas = ownCanvas;
        sharedRect = rect;
        sharedLocalPosition = localPosition;
        sharedLocalRotation = localRotation;
        sharedLocalScale = localScale;
        sharedAdjustedRect = GetPixelAdjustedRect();
        hasSharedPixelsPerUnit = false;
        return sharedAdjustedRect;
    }

    // GetSharedPixelAdjustedRect 바로 뒤에 부른다(나눠 쓰는 값이 지금 이 그래픽 것이다). 로컬 단위당 화면 픽셀 수도 같은 조건에서 같다.
    private float GetSharedPixelsPerUnit()
    {
        if (hasSharedPixelsPerUnit)
            return sharedPixelsPerUnit;

        float pixelsPerUnit = float.NaN;
        if (null != sharedCanvas)
        {
            Canvas rootCanvas = sharedCanvas.rootCanvas;
            Vector3 ownScale = rectTransform.lossyScale;
            Vector3 rootScale = rootCanvas.transform.lossyScale;
            pixelsPerUnit = rootCanvas.scaleFactor *
                            Mathf.Max(Mathf.Abs(ownScale.x / rootScale.x), Mathf.Abs(ownScale.y / rootScale.y));
        }

        sharedPixelsPerUnit = pixelsPerUnit;
        hasSharedPixelsPerUnit = true;
        return pixelsPerUnit;
    }

    // 꺼진 메시 수정 컴포넌트는 정점을 건드리지 않는다(UGUI BaseMeshEffect·UIEffect 모두 꺼져 있으면 바로 반환).
    private bool HasActiveMeshModifier()
    {
        GetComponents(typeof(IMeshModifier), meshModifierBuffer);
        bool hasActive = false;
        for (int i = 0; i < meshModifierBuffer.Count; i++)
        {
            Behaviour behaviour = meshModifierBuffer[i] as Behaviour;
            if (null == behaviour || behaviour.isActiveAndEnabled)
            {
                hasActive = true;
                break;
            }
        }

        meshModifierBuffer.Clear();
        return hasActive;
    }

}
