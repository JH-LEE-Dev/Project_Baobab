using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// 픽셀 퍼펙트 캔버스에서 부모가 화면 정수 픽셀만큼만 움직였을 때 메시를 다시 만들지 않는 Image.
//
// 픽셀 퍼펙트 캔버스는 그래픽이 움직인 프레임에 크기 변경 알림(OnRectTransformDimensionsChange)을 보내고(그 프레임의 캔버스 갱신 뒤에 온다),
// 그래픽은 다음 프레임의 캔버스 갱신에서 그때의 위치로 메시를 다시 만든다. 이 흐름은 그대로 두고 다시 만드는 순간에만 판단한다.
// 단순(Simple) Image 메시는 픽셀 보정 사각형(GetPixelAdjustedRect)과 스프라이트·색 같은 자기 설정으로만 정해지는데,
// 화면이 정수 픽셀만큼만 움직이면(마우스 드래그) 보정 사각형이 부동소수 오차(실측 0.001픽셀 미만)만큼만 달라진다.
// 그래서 다시 만들 차례에 지금 올라가 있는 메시의 보정 사각형과 지금 값이 화면에서 SameSnapTolerancePixels 안이고,
// 그 사이 정점 변경 요청이 이동 알림에서만 왔으면 다시 만들어도 같은 픽셀에 그려지므로 메시를 그대로 둔다.
// 비교 기준은 직전 프레임이 아니라 메시를 만든 순간이라 오차가 쌓이지 않는다.
// 색·스프라이트 등 다른 변경은 SetVerticesDirty를 직접 부르므로 원래대로 다시 만든다.
//
// 보정 사각형을 구하는 호출은 깊은 계층에서 비싸다(원래 재생성 비용의 절반쯤). 그래서
// - 메시를 만들 때는 따로 구하지 않고 방금 만든 정점에서 읽는다(여백 없는 단순 스프라이트는 네 정점이 곧 보정 사각형이다. 그 밖의 경우는 원래 Image와 똑같이 동작한다).
// - 줌처럼 전부 다시 만들어야 하는 프레임에서는 판정이 연달아 "다시 만들기"로 나오므로, 그 프레임의 나머지는 판정도 정점 읽기도 없이 원래대로 만든다.
//   (그 프레임에 먼저 처리된 몇 개만 계속 판정해, 정수 픽셀 이동으로 돌아오면 그 프레임에 나머지가 정점을 읽고 다음 프레임부터 메시를 그대로 둔다.)
public class PixelStableImage : Image
{
    // 래스터라이저의 서브픽셀 격자(보통 1/256픽셀) 반 칸(약 0.002픽셀)보다 작게 잡는다.
    private const float SameSnapTolerancePixels = 0.001f;

    // 한 프레임에 그대로 둔 그래픽 없이 이만큼 "다시 만들기" 판정이 나오면 전부 다시 만드는 프레임으로 본다.
    // 노드 하나가 모션으로 혼자 움직일 때(그림 최대 7장)는 넘지 않게 잡는다. 그런 프레임에서도 이만큼은 정점을 읽어 다음 프레임에 판정한다.
    // 잘못 봐도(실은 그대로 둘 수 있었어도) 원래 Image처럼 다시 만들 뿐이라 화면은 같다.
    private const int RejectsBeforeSkippingCheck = 8;

    // 바로 앞 프레임도 전부 다시 만드는 프레임이었으면(줌이 이어지는 중) 이만큼만 판정하고 판단한다.
    private const int RejectsBeforeSkippingCheckWhenSustained = 2;

    private static readonly List<Component> meshModifierBuffer = new List<Component>(4);

    // 이번 프레임에 그대로 둔 횟수·다시 만들기로 판정한 횟수(같이 움직인 그래픽은 판정이 대개 같다)
    private static int judgedFrame = -1;
    private static int keptThisFrame;
    private static int rejectedThisFrame;
    private static int capturedThisFrame;
    private static bool wasAllRebuildPreviousFrame;

    // 지금 CanvasRenderer에 올라가 있는 메시의 보정 사각형(메시를 만들 때 정점에서 읽은 값)
    private bool hasBuiltSnapshot;
    private Vector2 builtMin;
    private Vector2 builtMax;

    // 켜진 메시 수정 컴포넌트가 있는지(위치에 따라 정점을 바꿀 수 있으므로 있으면 늘 다시 만든다)
    private bool isMeshModifierStateKnown;
    private bool hasActiveMeshModifier;

    // 이동 알림을 처리하는 중인지, 마지막으로 메시를 만든 뒤 이동 알림이 아닌 정점 변경 요청이 있었는지
    private bool isHandlingMoveNotification;
    private bool hasOtherVertexChange;

    // 메시를 만드는 중 정점에서 읽은 보정 사각형
    private bool isBuildingMesh;
    private bool hasCapturedBounds;
    private Vector2 capturedMin;
    private Vector2 capturedMax;

    // 여백 확인을 스프라이트가 바뀔 때만 다시 한다.
    private Sprite paddingCheckedSprite;
    private bool isPaddingCheckedSpriteUnpadded;

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

    protected override void UpdateGeometry()
    {
        BeginFrameIfNeeded();

        if (false == hasOtherVertexChange && hasBuiltSnapshot && isMeshModifierStateKnown && false == hasActiveMeshModifier &&
            false == IsAllRebuildFrame() && IsSameSnapAsBuiltMesh())
        {
            return;
        }

        hasCapturedBounds = false;
        isBuildingMesh = false == IsAllRebuildFrame() || capturedThisFrame < GetRejectsBeforeSkippingCheck();
        try
        {
            base.UpdateGeometry();
        }
        finally
        {
            isBuildingMesh = false;
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
        base.OnPopulateMesh(_toFill);

        if (isBuildingMesh)
            CaptureAdjustedRectFromVertices(_toFill);
    }

    protected override void OnDisable()
    {
        // 꺼지면 CanvasRenderer 메시가 지워지므로, 다시 켜질 때는 원래대로 한 번 만든다.
        hasBuiltSnapshot = false;
        isMeshModifierStateKnown = false;
        base.OnDisable();
    }

    // 여백 없는 단순 스프라이트(스프라이트 없음 포함)는 네 정점이 픽셀 보정 사각형의 네 꼭짓점이다.
    // 그 밖의 경우(비율 유지·스프라이트 메시·여백 있는 스프라이트·다른 Image 종류)는 읽지 않고, 그러면 메시를 그대로 두는 일도 없다.
    private void CaptureAdjustedRectFromVertices(VertexHelper _vertices)
    {
        if (Type.Simple != type || useSpriteMesh || preserveAspect || 4 != _vertices.currentVertCount)
            return;

        Sprite activeSprite = overrideSprite;
        if (false == ReferenceEquals(activeSprite, paddingCheckedSprite))
        {
            paddingCheckedSprite = activeSprite;
            isPaddingCheckedSpriteUnpadded = null == activeSprite || Vector4.zero == DataUtility.GetPadding(activeSprite);
        }

        if (false == isPaddingCheckedSpriteUnpadded)
            return;

        UIVertex vertex = new UIVertex();
        _vertices.PopulateUIVertex(ref vertex, 0);
        Vector2 min = vertex.position;
        Vector2 max = vertex.position;
        for (int i = 1; i < 4; i++)
        {
            _vertices.PopulateUIVertex(ref vertex, i);
            if (vertex.position.x < min.x) min.x = vertex.position.x;
            if (vertex.position.y < min.y) min.y = vertex.position.y;
            if (vertex.position.x > max.x) max.x = vertex.position.x;
            if (vertex.position.y > max.y) max.y = vertex.position.y;
        }

        capturedMin = min;
        capturedMax = max;
        hasCapturedBounds = true;
        capturedThisFrame++;
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
        capturedThisFrame = 0;
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
        Rect adjustedRect = GetPixelAdjustedRect();
        float difference = Mathf.Abs(adjustedRect.xMin - builtMin.x);
        difference = Mathf.Max(difference, Mathf.Abs(adjustedRect.yMin - builtMin.y));
        difference = Mathf.Max(difference, Mathf.Abs(adjustedRect.xMax - builtMax.x));
        difference = Mathf.Max(difference, Mathf.Abs(adjustedRect.yMax - builtMax.y));

        bool isSame = 0f == difference;
        if (false == isSame)
        {
            // 로컬 단위 차이를 화면 픽셀로 바꿔 비교한다(확대·축소 배율에 따라 로컬 단위당 픽셀 수가 달라진다).
            // 배율이 0이라 NaN이 나오면 비교가 거짓이 되어 다시 만든다.
            Canvas ownCanvas = canvas;
            if (null != ownCanvas)
            {
                Canvas rootCanvas = ownCanvas.rootCanvas;
                Vector3 ownScale = rectTransform.lossyScale;
                Vector3 rootScale = rootCanvas.transform.lossyScale;
                float pixelsPerUnit = rootCanvas.scaleFactor *
                                      Mathf.Max(Mathf.Abs(ownScale.x / rootScale.x), Mathf.Abs(ownScale.y / rootScale.y));
                isSame = difference * pixelsPerUnit <= SameSnapTolerancePixels;
            }
        }

        if (isSame)
            keptThisFrame++;
        else
            rejectedThisFrame++;

        return isSame;
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
