using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 같은 GameObject의 CanvasScaler 배율을 PixelPerfectCamera와 동일한 정수 배율로 고정합니다.
///
/// 기본 설정(ScaleWithScreenSize + Match: Height)의 배율은 화면세로/360이라 실수입니다.
/// 16:9 해상도에서는 우연히 정수가 되지만(1080/360=3), 그 밖의 화면비·해상도에서는
/// 4.44배 같은 값이 나와 원본 1px이 화면에서 4px과 5px로 들쭉날쭉 찍힙니다.
/// 게임 아트가 Point 필터 픽셀아트라 테두리·폰트 굵기가 눈에 띄게 불균일해집니다.
///
/// 하는 일은 결국 그 화면세로/360을 반올림해 정수로 만드는 것뿐입니다. 카메라 쪽은
/// CinemachinePixelPerfect가 이미 같은 값을 반올림해 쓰고 있어서(SettingsData.GetPixelScale
/// 주석 참고), 이렇게 맞추면 월드 픽셀과 UI 픽셀의 크기가 항상 정확히 일치합니다.
/// 16:9 정수배 해상도에서는 원래 값이 이미 정수라 아무것도 달라지지 않습니다.
///
/// 카메라 줌 연출(CameraMoveController.ZoomCamera)은 따라가지 않습니다. 줌은 월드만
/// 확대하는 연출이고 UI까지 같이 커지면 안 되기 때문입니다. 화면 크기만 보는 이 방식은
/// 그 요구를 자연히 만족합니다.
///
/// 배율이 낮아지는 만큼 캔버스의 논리 크기(화면크기/배율)는 커집니다. 즉 UI가 화면에서
/// 조금 작아지고 배치 여백이 늘어나는데, 이는 카메라가 여분 시야를 노출하는 것과 같은
/// 성질의 변화입니다. 화면비와 무관하게 UI 여백까지 고정해야 한다면 캔버스 자체를
/// 레터박스로 잘라내는 별도 작업이 필요합니다.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class PixelPerfectCanvasScaleApplier : MonoBehaviour
{
    private CanvasScaler canvasScaler;
    private Canvas canvas;

    // 마지막으로 반영한 화면 크기. 변화가 없으면 아무 일도 하지 않기 위한 것이다.
    private int appliedWidth;
    private int appliedHeight;

    private void Awake()
    {
        canvasScaler = GetComponent<CanvasScaler>();
        canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        // 첫 프레임부터 맞아 있어야 하므로 Update를 기다리지 않고 즉시 반영한다.
        Apply(Screen.width, Screen.height);
    }

    /// <summary>
    /// SettingsManager의 OnScreenTargetResolvedEvent를 구독하지 않고 실제 화면 크기를
    /// 직접 감시합니다. Screen.SetResolution은 호출 즉시 반영되지 않아 이벤트 시점의
    /// Screen 값이 아직 예전 값일 수 있고, 창 드래그 리사이즈나 모니터 교체처럼
    /// 그 이벤트를 아예 거치지 않는 경로도 있기 때문입니다.
    /// 값이 그대로면 int 비교 두 번으로 끝나므로 매 프레임 확인해도 부담이 없습니다.
    /// </summary>
    private void Update()
    {
        if (Screen.width == appliedWidth && Screen.height == appliedHeight) return;

        Apply(Screen.width, Screen.height);
    }

    private void Apply(int _screenWidth, int _screenHeight)
    {
        // 월드 스페이스 캔버스는 CanvasScaler가 uiScaleMode를 무시하고 물리 크기로만
        // 동작하므로 건드리지 않는다. (WorldCanvas 프리팹에는 이 컴포넌트를 붙이지 않는다)
        if (null != canvas && RenderMode.WorldSpace == canvas.renderMode) return;

        // 중첩 캔버스의 CanvasScaler는 유니티가 무시하고 루트 캔버스 배율을 그대로 쓴다.
        // 여기서 값을 써봐야 효과가 없고 오해만 부르므로 건너뛴다.
        if (null != canvas && false == canvas.isRootCanvas) return;

        // 반영에 성공한 뒤에만 기록한다. 위 조건에 걸린 상태로 기록해버리면, 조건이 풀린 뒤에도
        // 화면 크기가 다시 바뀌기 전까지 영영 반영되지 않는다.
        appliedWidth = _screenWidth;
        appliedHeight = _screenHeight;

        int _scale = SettingsData.GetPixelScale(_screenHeight);

        // ScaleWithScreenSize로는 정수 배율을 표현할 수 없어(화면세로/기준세로가 실수)
        // ConstantPixelSize로 바꾸고 배율을 직접 지정한다.
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasScaler.scaleFactor = _scale;
    }
}
