using UnityEngine;

/// <summary>
/// 이 RectTransform을 카메라가 실제로 그리는 영역(잘리지 않은 부분)에 정확히 맞춥니다.
/// UI 레이어 루트 프리팹(Assets/Prefabs/UI/UIRoot의 CameraOverlayRoot, OverlayRoot)에 붙어 있어서,
/// 그 아래에 놓이는 모든 UIView가 자동으로 잘린 띠 안쪽에 배치됩니다.
///
/// [왜 필요한가]
/// 카메라에 연결된 캔버스(Screen Space - Camera)는 카메라가 잘리면 함께 좁아지지만, 화면 전체를 쓰는
/// 캔버스(Overlay, 그리고 카메라 없이 쓰는 Screen Space - Camera)는 잘리지 않습니다. 메인 메뉴와
/// 게임플레이의 Overlay Canvas가 그렇습니다. 그래서 화면 가장자리에 붙인 UI가 검은 띠 위에 놓였습니다.
/// (2560x1080에서 메인 메뉴 버튼은 왼쪽 띠, Discord·버전 표시는 오른쪽 띠)
///
/// [어떻게]
/// 루트 캔버스가 그려지는 영역과 카메라 영역의 차이만큼 안쪽으로 들입니다. 카메라에 연결된 캔버스는
/// 두 영역이 같아서 여백이 0이 되므로, 어느 캔버스 아래에 놓여도 같은 컴포넌트로 맞습니다.
/// 자르지 않는 화면에서는 카메라 영역이 화면 전체라 언제나 여백 0, 즉 기존과 똑같습니다.
///
/// 앵커와 오프셋을 이 컴포넌트가 소유합니다. 설치 코드(SetAnchorToCanvas)가 생성 직후 오프셋을
/// 0으로 되돌려도 같은 프레임의 LateUpdate에서 다시 맞추므로 화면에는 드러나지 않습니다.
///
/// 배치만 옮깁니다. 루트보다 넓게 그린 아트(씬 전환 구름 등)는 여전히 띠로 넘칠 수 있는데,
/// 그것은 CroppedAreaGuard가 덮습니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class CroppedAreaFitter : MonoBehaviour
{
    private RectTransform rectTransform;
    private Canvas rootCanvas;

    private void Awake()
    {
        rectTransform = (RectTransform)transform;
    }

    // 다른 캔버스 아래로 옮겨지면 기준 캔버스가 바뀌므로 다시 찾는다.
    private void OnTransformParentChanged()
    {
        rootCanvas = null;
    }

    private void OnCanvasHierarchyChanged()
    {
        rootCanvas = null;
    }

    /// <summary>
    /// 매 프레임 확인합니다. 화면 크기 외에도 설치 코드의 오프셋 초기화, 카메라 연결 시점처럼
    /// 이벤트로 잡기 어려운 변화가 있어서입니다. 값이 이미 맞으면 비교만 하고 끝납니다.
    /// </summary>
    private void LateUpdate()
    {
        if (null == rootCanvas)
        {
            Canvas _canvas = GetComponentInParent<Canvas>();
            rootCanvas = (null != _canvas) ? _canvas.rootCanvas : null;
        }

        if (null == rootCanvas) return;

        // 월드 캔버스는 화면 영역과 무관하다.
        if (RenderMode.WorldSpace == rootCanvas.renderMode) return;

        float _scaleFactor = rootCanvas.scaleFactor;
        if (_scaleFactor <= 0f) return;

        if (Screen.width <= 0 || Screen.height <= 0) return;

        // 카메라에 연결된 캔버스는 그 카메라 영역에 그려진다. 카메라가 없으면 유니티가 Overlay처럼
        // 화면 전체에 그리므로 화면 전체를 기준으로 삼는다.
        Camera _canvasCamera = (RenderMode.ScreenSpaceCamera == rootCanvas.renderMode) ? rootCanvas.worldCamera : null;
        Rect _canvasPixelRect = (null != _canvasCamera)
            ? _canvasCamera.pixelRect
            : new Rect(0f, 0f, Screen.width, Screen.height);

        RectInt _view = SettingsData.GetCameraViewPixelRect(Screen.width, Screen.height);

        // 캔버스가 카메라 영역보다 넓은 만큼만 들인다. 좁은 쪽(음수)은 0으로 둔다.
        Vector2 _offsetMin = new Vector2(
            Mathf.Max(0f, _view.xMin - _canvasPixelRect.xMin) / _scaleFactor,
            Mathf.Max(0f, _view.yMin - _canvasPixelRect.yMin) / _scaleFactor);

        Vector2 _offsetMax = new Vector2(
            -Mathf.Max(0f, _canvasPixelRect.xMax - _view.xMax) / _scaleFactor,
            -Mathf.Max(0f, _canvasPixelRect.yMax - _view.yMax) / _scaleFactor);

        if (rectTransform.anchorMin != Vector2.zero) rectTransform.anchorMin = Vector2.zero;
        if (rectTransform.anchorMax != Vector2.one) rectTransform.anchorMax = Vector2.one;
        if (rectTransform.offsetMin != _offsetMin) rectTransform.offsetMin = _offsetMin;
        if (rectTransform.offsetMax != _offsetMax) rectTransform.offsetMax = _offsetMax;
    }
}
