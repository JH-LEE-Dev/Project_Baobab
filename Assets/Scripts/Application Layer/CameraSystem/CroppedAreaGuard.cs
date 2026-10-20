using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// 카메라가 좌우를 잘라낸(Pillarbox) 화면에서, 잘려 나간 띠에 아무것도 보이지 않게 합니다.
///
/// [왜 필요한가]
/// UltraWideCropApplier가 카메라를 자르면 화면 양옆에 띠가 생깁니다. URP는 원래 이 띠를 검게
/// 지워주지만, 그 처리는 Pixel Perfect Camera가 붙은 Base 카메라가 화면에 직접 출력할 때만
/// 동작합니다. (Renderer2DRendergraph.IsPixelPerfectCameraEnabled: "Pixel Perfect Camera doesn't
/// support camera stacking") 이 게임은 PP Main Camera 위에 PP UI Camera를 스택으로 얹어서 그 조건에서
/// 빠지고, 그 결과 띠를 지우는 카메라가 하나도 없습니다. 씬 전환 연출 구름처럼 한 번 띠에 그려진
/// 것이 지워지지 않고 계속 남아 화면이 깨진 것처럼 보였습니다. (2560x1080 데모 리뷰 피드백)
///
/// 또 화면 전체를 쓰는 오버레이 캔버스는 카메라를 따라 잘리지 않아서 띠 위에도 그려집니다.
///
/// [하는 일] 자르는 화면에서만 켜지고, 그 외에는 아무 비용도 들지 않습니다.
///   1. 매 프레임 가장 먼저 화면 전체를 검게 지우는 카메라 - 지난 프레임의 잔상 제거
///   2. 모든 오버레이 UI보다 위에서 띠를 검게 덮는 캔버스 - 넓게 그린 UI가 띠로 넘치는 것 차단
///
/// UI 배치를 띠 안쪽으로 옮기는 것은 CroppedAreaFitter가 합니다. 이 클래스의 2번은 배치와 무관하게
/// "띠에는 아무것도 보이지 않는다"를 보장하는 마지막 안전장치입니다. 띠를 덮는 이미지는 클릭도
/// 막으므로, 띠 쪽에 남은 UI가 보이지 않는 채로 눌리는 일도 없습니다.
///
/// 씬마다 카메라가 새로 만들어지고 사라져도, 씬 전환 중 카메라가 하나도 없는 순간에도 동작해야 해서
/// 씬 카메라에 붙이지 않고 DontDestroyOnLoad 오브젝트로 혼자 존재합니다. 잘린 영역은 카메라를
/// 읽지 않고 SettingsData.GetCameraViewPixelRect로 계산합니다. 생성은 BootStrap이 합니다(Create 참고).
/// </summary>
public class CroppedAreaGuard : MonoBehaviour
{
    /// <summary>
    /// 씬 카메라(PP Main Camera, depth -1)보다 먼저 그려져야 합니다. URP는 Base 카메라를 depth
    /// 오름차순으로 그리고, 오버레이 UI는 마지막 Base 카메라가 그리므로 기존 UI 순서도 그대로입니다.
    /// </summary>
    private const float CLEAR_CAMERA_DEPTH = -100f;

    /// <summary>오버레이 캔버스 정렬 순서의 최댓값. 어떤 오버레이 UI보다도 위에 그려집니다.</summary>
    private const int MASK_SORTING_ORDER = short.MaxValue;

    private static CroppedAreaGuard instance;

    private Camera clearCamera;
    private Canvas maskCanvas;
    private RectTransform leftBar;
    private RectTransform rightBar;

    // 마지막으로 반영한 화면 크기. 변화가 없으면 아무 일도 하지 않기 위한 것이다.
    private int appliedWidth;
    private int appliedHeight;

    // 도메인 리로드를 끈 채 플레이 모드에 들어가도 지난 세션의 파괴된 인스턴스를 붙잡지 않도록 비운다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    /// <summary>
    /// BootStrap.Awake가 부릅니다. 이미 있으면 아무것도 하지 않습니다.
    ///
    /// 자동 생성(RuntimeInitializeOnLoadMethod)을 쓰지 않는 이유: 이 가드는 화면 크기만 보고
    /// 띠를 덮으므로, 카메라를 자르지 않는 씬(AbilityToolScene 같은 툴·테스트 씬)에서 켜지면
    /// 실제로 그려진 화면 가장자리를 가립니다. 카메라를 자르는 씬은 모두 BootStrap을 거쳐
    /// 들어오므로 BootStrap이 만들게 해서 범위를 실제 게임 흐름으로 한정합니다.
    /// </summary>
    public static void Create()
    {
        if (null != instance) return;

        GameObject _go = new GameObject(nameof(CroppedAreaGuard));
        DontDestroyOnLoad(_go);
        _go.AddComponent<CroppedAreaGuard>();
    }

    private void Awake()
    {
        if (null != instance && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        CreateClearCamera();
        CreateMaskCanvas();

        // 첫 프레임부터 맞아 있어야 하므로 LateUpdate를 기다리지 않는다.
        Apply();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// <summary>
    /// UltraWideCropApplier와 같은 이유로 해상도 변경 이벤트 대신 실제 화면 크기를 감시합니다.
    /// 값이 그대로면 int 비교 두 번으로 끝납니다.
    /// </summary>
    private void LateUpdate()
    {
        if (Screen.width == appliedWidth && Screen.height == appliedHeight) return;

        Apply();
    }

    private void Apply()
    {
        int _screenWidth = Screen.width;
        int _screenHeight = Screen.height;

        // 화면 크기를 아직 신뢰할 수 없는 프레임에서는 캐시하지 않고 다음 프레임에 다시 시도한다.
        if (_screenWidth <= 0 || _screenHeight <= 0) return;

        appliedWidth = _screenWidth;
        appliedHeight = _screenHeight;

        bool _bCropped = SettingsData.IsPillarboxRequired(_screenWidth, _screenHeight);

        clearCamera.enabled = _bCropped;

        // Canvas.enabled만 끄면 그리기는 멈춰도 레이캐스트 경로가 남을 수 있어 오브젝트째 끈다.
        if (maskCanvas.gameObject.activeSelf != _bCropped)
        {
            maskCanvas.gameObject.SetActive(_bCropped);
        }

        if (false == _bCropped) return;

        RectInt _view = SettingsData.GetCameraViewPixelRect(_screenWidth, _screenHeight);

        // 마스크 캔버스에는 CanvasScaler가 없어 1단위 = 1픽셀이다. 띠 폭을 픽셀 그대로 넣는다.
        leftBar.sizeDelta = new Vector2(_view.xMin, 0f);
        rightBar.sizeDelta = new Vector2(_screenWidth - _view.xMax, 0f);
    }

    private void CreateClearCamera()
    {
        GameObject _go = new GameObject("CroppedAreaClearCamera");
        _go.transform.SetParent(transform, false);

        clearCamera = _go.AddComponent<Camera>();
        clearCamera.clearFlags = CameraClearFlags.SolidColor;
        clearCamera.backgroundColor = Color.black;
        clearCamera.cullingMask = 0;    // 지우기만 하고 아무것도 그리지 않는다
        clearCamera.depth = CLEAR_CAMERA_DEPTH;
        clearCamera.orthographic = true;
        clearCamera.useOcclusionCulling = false;
        clearCamera.allowHDR = false;
        clearCamera.allowMSAA = false;
        clearCamera.enabled = false;

        UniversalAdditionalCameraData _cameraData = clearCamera.GetUniversalAdditionalCameraData();
        _cameraData.renderType = CameraRenderType.Base;
        _cameraData.renderPostProcessing = false;
        _cameraData.renderShadows = false;
        _cameraData.requiresColorOption = CameraOverrideOption.Off;
        _cameraData.requiresDepthOption = CameraOverrideOption.Off;
        _cameraData.antialiasing = AntialiasingMode.None;
    }

    private void CreateMaskCanvas()
    {
        GameObject _go = new GameObject("CroppedAreaMaskCanvas");
        _go.transform.SetParent(transform, false);

        maskCanvas = _go.AddComponent<Canvas>();
        maskCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        maskCanvas.sortingOrder = MASK_SORTING_ORDER;
        _go.SetActive(false);

        // 띠 위에서 아래쪽 UI가 클릭되지 않게 레이캐스트를 받는다.
        _go.AddComponent<GraphicRaycaster>();

        leftBar = CreateBar("LeftBar", 0f);
        rightBar = CreateBar("RightBar", 1f);
    }

    /// <summary>
    /// 화면 왼쪽(_anchorX = 0) 또는 오른쪽(1) 가장자리에 붙어 세로로 꽉 차는 검은 막대를 만듭니다.
    /// 폭은 Apply에서 정합니다.
    /// </summary>
    private RectTransform CreateBar(string _name, float _anchorX)
    {
        GameObject _go = new GameObject(_name, typeof(RectTransform));
        _go.transform.SetParent(maskCanvas.transform, false);

        RectTransform _rect = (RectTransform)_go.transform;
        _rect.anchorMin = new Vector2(_anchorX, 0f);
        _rect.anchorMax = new Vector2(_anchorX, 1f);
        _rect.pivot = new Vector2(_anchorX, 0.5f);
        _rect.anchoredPosition = Vector2.zero;
        _rect.sizeDelta = Vector2.zero;

        Image _image = _go.AddComponent<Image>();
        _image.color = Color.black;
        _image.raycastTarget = true;

        return _rect;
    }
}
