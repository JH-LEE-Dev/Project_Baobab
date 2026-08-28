using UnityEngine;

/// <summary>
/// 입력 장치 자동 전환의 민감도 설정입니다.
///
/// 이 값들이 왜 필요한지가 이 시스템의 핵심입니다. "아무 입력이나 들어오면 전환"으로 만들면
/// 아날로그 스틱의 드리프트(가만히 둬도 0이 아닌 값이 계속 들어옴)와 마우스의 미세 떨림 때문에
/// 아이콘이 키보드/패드 사이에서 계속 깜빡입니다. 그래서 "의도적인 조작"으로 볼 수 있는
/// 최소 크기를 넘겨야만 전환하도록 문턱값을 둡니다.
///
/// InputManager에 에셋을 지정하지 않으면 여기 적힌 기본값으로 동작합니다.
/// </summary>
[CreateAssetMenu(fileName = "InputDeviceSettings", menuName = "Game/Input Device Settings")]
public class InputDeviceSettings : ScriptableObject
{
    [Header("Gamepad")]
    [Tooltip("스틱을 '의도적으로 기울였다'고 인정할 최소 크기(0~1). 드리프트는 보통 0.1 미만이라 " +
             "0.5면 충분히 안전하면서도 살짝 기울인 조작을 놓치지 않는다.")]
    [Range(0.1f, 0.95f)]
    public float stickActuationThreshold = 0.5f;

    [Tooltip("트리거를 '당겼다'고 인정할 최소 깊이(0~1). 트리거는 아날로그라 버튼 눌림 판정 대신 " +
             "깊이로 본다. 일부 패드는 미사용 상태에서도 0.05 안팎의 값을 흘린다.")]
    [Range(0.05f, 0.95f)]
    public float triggerActuationThreshold = 0.3f;

    [Header("Keyboard & Mouse")]
    [Tooltip("마우스를 '움직였다'고 인정할 누적 이동 거리(픽셀). 책상 진동이나 센서 지터로 " +
             "1~2px가 수시로 들어오므로, 한 번의 델타가 아니라 누적치로 판정한다.")]
    [Min(1f)]
    public float mouseTravelThresholdPixels = 12f;

    [Tooltip("마우스가 이 시간(초) 동안 멈춰 있으면 누적 이동 거리를 0으로 되돌린다. " +
             "이게 없으면 몇 분에 걸친 미세 지터가 조금씩 쌓여서 결국 문턱값을 넘어버린다.")]
    [Min(0.05f)]
    public float mouseTravelResetSeconds = 0.4f;

    [Header("Switching")]
    [Tooltip("한 번 전환한 뒤 다음 전환까지의 최소 간격(초). 양손으로 키보드와 패드를 번갈아 " +
             "건드릴 때 아이콘이 매 프레임 튀는 것을 막는 히스테리시스다. " +
             "패드 연결 해제로 인한 강제 전환은 이 간격을 무시한다.")]
    [Min(0f)]
    public float switchCooldownSeconds = 0.3f;

    /// <summary>
    /// 에셋이 지정되지 않았을 때 쓸 기본 설정 인스턴스를 만듭니다.
    /// (필드 기본값이 곧 기본 설정이므로 별도 값 세팅은 필요 없습니다)
    /// </summary>
    public static InputDeviceSettings CreateDefault()
    {
        InputDeviceSettings _settings = CreateInstance<InputDeviceSettings>();
        _settings.name = "InputDeviceSettings (Runtime Default)";
        return _settings;
    }
}
