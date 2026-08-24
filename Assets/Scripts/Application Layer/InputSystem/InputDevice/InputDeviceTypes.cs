/// <summary>
/// 유저가 "현재 조작에 쓰고 있는" 입력 장치 분류입니다.
/// 물리적 연결 여부가 아니라 마지막으로 실제 입력이 들어온 쪽을 뜻합니다.
/// (키보드와 패드가 동시에 꽂혀 있어도 둘 다 항상 입력은 받으며, 이 값은 표시/연출 판단용입니다)
/// </summary>
public enum EInputDeviceType
{
    KeyboardMouse = 0,
    Gamepad = 1,
}

/// <summary>
/// 패드 버튼 아이콘을 어느 벤더 표기로 그릴지입니다.
///
/// 주의: 이 값이 환경설정에 저장되기 시작하면(SettingsData) 정수값이 그대로 직렬화되므로,
/// 기존 항목의 순서를 바꾸거나 중간에 삽입하면 안 됩니다. 새 항목은 항상 맨 뒤에 추가하세요.
/// Generic이 0인 이유는 판별 실패 시의 안전한 기본값이기 때문입니다.
/// </summary>
public enum EGamepadIconSet
{
    /// <summary>벤더를 특정하지 못한 패드. 중립 표기(도형/번호)로 그립니다.</summary>
    Generic = 0,

    /// <summary>A / B / X / Y</summary>
    Xbox = 1,

    /// <summary>✕ / ○ / □ / △</summary>
    PlayStation = 2,

    /// <summary>B / A / Y / X (닌텐도는 Xbox와 좌우가 반대입니다)</summary>
    Nintendo = 3,
}
