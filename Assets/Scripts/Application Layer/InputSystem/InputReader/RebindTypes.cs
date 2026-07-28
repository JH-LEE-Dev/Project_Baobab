/// <summary>
/// 유저가 직접 리바인딩할 수 있는 액션입니다. Move는 2DVector 컴포지트라
/// 방향별(up/down/left/right)로 쪼개서 노출합니다.
/// ESC/Mouse(포인터 이동)는 시스템 예약·포인터 입력이라 여기 포함하지 않습니다.
///
/// SwitchMode/AxeMode/RifleMode/Reload/AimCorrection은 .inputactions 에셋에는 액션이 남아 있지만
/// InputReader.Initialize에서 구독하지 않는 죽은 바인딩(예전 무기 전환 시스템의 잔재)이라 제외했습니다.
/// 아무 효과도 없는 키를 리바인딩 대상으로 보여주면 혼란만 주기 때문입니다.
/// </summary>
public enum ERebindableAction
{
    MoveUp,
    MoveDown,
    MoveLeft,
    MoveRight,
    Inventory,
    Interaction,

    /// <summary>공격. 실제 액션은 Click(&lt;Mouse&gt;/leftButton)이며 기본값은 마우스 좌클릭입니다.</summary>
    Attack,

    PotionKey,
}

/// <summary>
/// 리바인딩 시도 결과입니다. Duplicate여도 입력한 키는 그대로 적용됩니다.
/// (편집 세션 동안은 중복 상태를 허용하고 화면에 표시만 하다가, 저장 시점에만 막습니다)
/// ESC는 인터랙티브 리바인딩의 취소 키로 예약되어 있어 애초에 새 바인딩으로 입력될 수 없으므로
/// 별도의 "예약 키" 결과는 두지 않습니다.
/// </summary>
public enum ERebindResult
{
    Success,
    Canceled,

    /// <summary>적용은 됐지만 다른 액션과 키가 겹칩니다. UI가 경고 표시만 하면 됩니다.</summary>
    Duplicate,
}
