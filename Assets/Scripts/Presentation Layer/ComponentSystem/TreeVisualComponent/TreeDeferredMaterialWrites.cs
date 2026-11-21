using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 나무 그림에 넣는 값 가운데 그리는 데만 쓰이는 것(열기 차오름, 점멸)을 화면에 걸릴 때만 써넣는다.
///
/// TreeVisualComponent가 값을 기억만 하고 여기 등록해 두면, 카메라마다 그리기 직전(컬링보다 앞)에 그 카메라 절두체에 걸릴 수 있는 나무만
/// 실제 렌더러에 써넣는다. 걸리지 않는 나무는 등록된 채 남아 다음 프레임에 다시 본다. 판정은 나무 뿌리 위치에서 넉넉한 고정 범위만큼 뻗은
/// 상자(TreeVisualComponent.IsDeferredTargetInFrustum)를 각 변에 PixelVfxCulling.MarginUnits만큼 더 넓혀서 하므로(픽셀 퍼펙트 보정으로
/// 카메라가 조금 달라져도 덮는다) 그려지는 렌더러는 반드시 가장 나중 값을 갖는다. 씬 뷰·보조 카메라도 같은 경로로 처리되고,
/// 나무 레이어를 그리지 않는 카메라(UI 카메라)는 건너뛴다.
/// </summary>
public static class TreeDeferredMaterialWrites
{
    private static readonly List<TreeVisualComponent> pending = new List<TreeVisualComponent>(256);
    private static readonly Plane[] frustumPlanes = new Plane[6];
    // 평면 6개의 (nx, ny, nz, |nx|, |ny|, |nz|, d)
    private static readonly float[] planeData = new float[6 * 7];
    // 그리기 묶음(한 번에 이어서 그리는 카메라들) 번호. 같은 묶음 안에서는 게임 코드가 돌지 않으므로 나무 위치를 한 번만 읽는다.
    private static int contextSerial;
    // 지금까지 등록된 나무들의 렌더러 레이어 비트 합. 이 레이어를 하나도 그리지 않는 카메라는 목록을 돌지 않는다.
    private static int targetLayerMask;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        pending.Clear();
        contextSerial = 0;
        targetLayerMask = 0;
#if UNITY_EDITOR
        editorCheckCursor = 0;
        bEditorReachReported = false;
#endif
        RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
        RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    internal static void Register(TreeVisualComponent _visual)
    {
        _visual.deferredWriteIndex = pending.Count;
        pending.Add(_visual);
        targetLayerMask |= _visual.DeferredLayerMask;
    }

    internal static void Unregister(TreeVisualComponent _visual)
    {
        int index = _visual.deferredWriteIndex;
        _visual.deferredWriteIndex = -1;
        if (index < 0 || index >= pending.Count || false == ReferenceEquals(pending[index], _visual)) return;

        RemoveAt(index);
    }

    private static void RemoveAt(int _index)
    {
        int last = pending.Count - 1;
        if (_index != last)
        {
            TreeVisualComponent moved = pending[last];
            pending[_index] = moved;
            if (moved is object) moved.deferredWriteIndex = _index;
        }
        pending.RemoveAt(last);
    }

    /// <summary>미뤄 둔 값을 모두 지금 써넣는다. 나무 렌더러의 블록을 밖에서 읽어 가기 전에 부른다.</summary>
    public static void FlushAll()
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            TreeVisualComponent visual = pending[i];
            if (visual == null)
            {
                RemoveAt(i);
                continue;
            }

            visual.FlushDeferredMaterial();
        }
    }

    private static void OnBeginContextRendering(ScriptableRenderContext _context, List<Camera> _cameras)
    {
        contextSerial++;
    }

    private static void OnBeginCameraRendering(ScriptableRenderContext _context, Camera _camera)
    {
        if (0 == pending.Count || null == _camera) return;

        int cameraMask = _camera.cullingMask;
        if (0 == (cameraMask & targetLayerMask)) return;

#if UNITY_EDITOR
        EditorCheckOne();
#endif

        GeometryUtility.CalculateFrustumPlanes(_camera, frustumPlanes);
        for (int i = 0; i < 6; i++)
        {
            Vector3 n = frustumPlanes[i].normal;
            int o = i * 7;
            planeData[o] = n.x;
            planeData[o + 1] = n.y;
            planeData[o + 2] = n.z;
            planeData[o + 3] = Mathf.Abs(n.x);
            planeData[o + 4] = Mathf.Abs(n.y);
            planeData[o + 5] = Mathf.Abs(n.z);
            planeData[o + 6] = frustumPlanes[i].distance;
        }

        int serial = contextSerial;
        float margin = PresentationLayer.VFX.PixelVfxCulling.MarginUnits;

        // 뒤에서부터 본다: 써넣은 나무는 목록에서 빠지며 맨 뒤 항목이 그 자리로 오는데, 그 항목은 이미 본 것이다.
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            TreeVisualComponent visual = pending[i];
            if (visual == null)
            {
                RemoveAt(i);
                continue;
            }

            // 미룬 값이 바로 쓰는 경로에 덮여 없어졌으면 목록에서만 뺀다.
            if (false == visual.HasDeferredMaterial)
            {
                Unregister(visual);
                continue;
            }

            // 이 카메라가 나무 렌더러의 레이어를 그리지 않으면 볼 것이 없다.
            if (0 == (cameraMask & visual.DeferredLayerMask)) continue;

            if (visual.IsDeferredTargetInFrustum(planeData, margin, serial)) visual.FlushDeferredMaterial();
        }
    }

#if UNITY_EDITOR
    // 에디터 전용 점검: 카메라가 그릴 때마다 등록된 나무 하나씩 돌아가며, 실제 렌더러 경계가 판정에 쓰는 고정 범위 안에 있는지 본다.
    // 새 나무 그림이나 배율이 범위를 넘으면 한 번 오류를 남긴다(빌드에는 들어가지 않는다).
    private static int editorCheckCursor;
    private static bool bEditorReachReported;

    private static void EditorCheckOne()
    {
        if (bEditorReachReported) return;

        if (editorCheckCursor >= pending.Count) editorCheckCursor = 0;
        TreeVisualComponent visual = pending[editorCheckCursor++];
        if (visual == null) return;

        if (false == visual.EditorCheckDeferredReach(out string detail))
        {
            bEditorReachReported = true;
            Debug.LogError("[TreeDeferredMaterialWrites] 나무 그림이 화면 밖 판정 범위를 넘었습니다. TreeVisualComponent의 DeferredReach 값을 키워야 합니다: " + detail, visual);
        }
    }
#endif
}
