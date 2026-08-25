/// <summary>
/// 유저가 직접 리바인딩할 수 있는 액션입니다. Move는 2DVector 컴포지트라
/// 방향별(up/down/left/right)로 쪼개서 노출합니다.
/// ESC/Mouse(포인터 이동)는 시스템 예약·포인터 입력이라 여기 포함하지 않습니다.
///
/// 모든 항목이 두 장치 모두에 있는 것은 아닙니다. VirtualCursor는 패드에만 존재합니다.
/// 그래서 화면에 목록을 그릴 때는 반드시 장치별 목록(GetRebindableActions(device))을 쓰세요.
/// 전체 목록을 그대로 돌면 그 장치에 없는 항목이 빈 칸으로 끼어듭니다.
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

    /// <summary>
    /// 마을 가상 커서 토글. **패드에만 존재합니다.** (기본값 R3)
    ///
    /// 키보드/마우스에는 대응 항목이 없습니다. 마우스가 곧 커서라서 켜고 끌 대상이 없기 때문입니다.
    /// 목록 맨 뒤에 둔 이유: 이 enum의 순서가 키 설정 화면의 행 순서라, 중간에 끼우면
    /// 기존 항목들의 위치가 통째로 밀립니다.
    /// </summary>
    VirtualCursor,
}

/// <summary>
/// 패드 기본 배치입니다. (참고용 문서. 실제 값은 InputActionSystem.inputactions에 있습니다)
///
/// | 액션        | 패드                  |
/// |------------|----------------------|
/// | Move       | leftStick            |
/// | Aim        | rightStick           |
/// | Attack     | rightTrigger (RT/R2) |
/// | Interaction| buttonSouth (A/×)    |
/// | Inventory  | buttonNorth (Y/△)    |
/// | PotionKey  | buttonWest (X/□)     |
/// | ESC(메뉴)   | start                |
/// | 가상 커서    | rightStickPress (R3) |
///
/// buttonEast(B/○)는 비워 둡니다. 리바인딩 취소이자 "뒤로가기"라는 보편 관례라
/// 다른 기능에 할당하면 유저가 리바인딩 대기 상태에서 빠져나오지 못합니다.
/// </summary>
public static class GamepadDefaultBindings
{
    /// <summary>
    /// 스틱에 묶인 액션은 패드에서 리바인딩 대상이 아닙니다.
    ///
    /// 이유: 패드의 이동/조준은 "왼쪽 스틱 / 오른쪽 스틱"이라는 덩어리 하나이지, 방향별로
    /// 나뉘지 않습니다. 키보드처럼 MoveUp/Down/Left/Right를 따로 바꾸게 하면 넷 다 같은
    /// 스틱 바인딩을 가리켜서, 하나를 바꾸면 나머지 셋이 함께 바뀌는 것처럼 보입니다.
    /// (표시는 그대로 됩니다. 편집만 막습니다)
    /// </summary>
    public static bool IsRebindableOnGamepad(ERebindableAction _action)
    {
        switch (_action)
        {
            case ERebindableAction.MoveUp:
            case ERebindableAction.MoveDown:
            case ERebindableAction.MoveLeft:
            case ERebindableAction.MoveRight:
                return false;

            default:
                return true;
        }
    }
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
