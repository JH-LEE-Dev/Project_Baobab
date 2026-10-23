// 로그라이크 모드 코어 타입 모음.
//
// 이 폴더(Baobab.Roguelike 어셈블리)는 본편 게임 코드(Assembly-CSharp)를 참조하지 않는다.
// 카드·제시·압박 판정·웨이브·기록처럼 "규칙"만 담고, 실제 게임 오브젝트를 움직이는 일은
// RoguelikeSystem 폴더의 연동 코드가 맡는다. 그래서 규칙은 EditMode 테스트로 단독 검증할 수 있다.
//
// [직렬화 주의] 아래 enum은 ScriptableObject/기록 파일에 정수로 저장된다. 값을 끼워 넣거나 순서를
// 바꾸면 이미 만든 카드 에셋과 기록이 다른 의미로 읽힌다. 새 항목은 반드시 맨 뒤에 추가한다.

/// <summary>카드 종류. 기획서 v2.4 5-1 "카드 종류와 태그".</summary>
public enum ECardKind
{
    Active = 0,
    Passive = 1,
    /// <summary>계약(v2.3까지의 루비). 버프와 디메리트를 함께 가지며 레벨이 없다. 붉은 루비색 프레임으로 그린다.</summary>
    Ruby = 2,
    Prism = 3,
}

/// <summary>
/// 카드 UI가 실제로 그리는 프레임. 기획서 v2.4 5-3 "카드 분류와 레어".
/// v2.4에서 등급(브론즈·실버·골드)이 없어져 일반 카드는 모두 Bronze(일반 프레임)로 그리고, Silver·Gold는 더 이상 나오지 않는다.
/// 값은 UI의 프레임 배열 순서이므로 번호를 바꾸지 않는다.
/// </summary>
public enum ECardDisplayGrade
{
    Bronze = 0,
    Silver = 1,
    Gold = 2,
    Emerald = 3,
    Prism = 4,
    Ruby = 5,
}

/// <summary>
/// 액티브 카드의 내부 태그. 카드 이름과 무관하게 시스템은 이 태그로만 스킬을 찾는다(기획서 3-3 "태그 규칙").
/// None은 "특정 스킬이 아닌 전역 효과"를 뜻하는 데도 쓴다.
/// </summary>
public enum ERoguelikeSkill
{
    None = 0,
    Offroad = 1,
    Spin = 2,
    Shockwave = 3,
    Drone = 4,
    Boomerang = 5,
    Stella = 6,
    SporeBomb = 7,
}

/// <summary>조합 카드(프리즘) 종류. 기획서 v2.5 6-2 "조합(프리즘) 7종".</summary>
public enum ERoguelikeCombo
{
    None = 0,
    SpinShockwave = 1,
    BoomerangShockwave = 2,
    SpinBoomerang = 3,
    // 4는 폐지된 스텔라 + 포자 수류탄(StellaSpore) 자리다. v2.5에서 삭제됐다. 다시 쓰지 않는다.
    StellaDrone = 5,
    /// <summary>포자 수류탄 + 드론(포자 레이저): 드론이 공격하는 모든 나무에서 포자 수류탄 대폭발 + 오염.</summary>
    SporeDrone = 6,
    /// <summary>포자 수류탄 + 오프로드(방구차): 오프로드가 지나가는 경로에 포자 수류탄 대폭발 + 오염.</summary>
    SporeOffroad = 7,
    /// <summary>포자 수류탄 + 충격파(포자 충격파): 초록 충격파에 맞은 모든 나무에서 포자 수류탄 대폭발 + 오염.</summary>
    SporeShockwave = 8,
}

/// <summary>
/// 카드 한 레벨이 주는 효과의 종류. CardEffectEntry.skill이 None이면 전역, 아니면 그 스킬에만 적용한다.
/// 수치의 단위는 항목마다 주석에 적었다. 같은 항목은 보유 카드 전체에서 단순 합산된다.
/// </summary>
public enum ERunEffect
{
    /// <summary>스킬 획득. skill 필수, value는 쓰지 않는다(0보다 크면 획득).</summary>
    UnlockSkill = 0,
    /// <summary>피해량 증가(%). skill=None이면 도끼와 모든 스킬.</summary>
    DamagePercent = 1,
    /// <summary>공격 속도(%). skill=None이면 도끼와 모든 스킬의 발동 주기.</summary>
    AttackSpeedPercent = 2,
    /// <summary>범위(%).</summary>
    RangePercent = 3,
    /// <summary>지속 시간(%).</summary>
    DurationPercent = 4,
    /// <summary>도트 피해(%).</summary>
    DotDamagePercent = 5,
    /// <summary>동시 발동 수(개수 가산). 드론 수, 부메랑 수, 별 최대 개수 등.</summary>
    CountAdd = 6,
    /// <summary>발동 확률(%p 가산). 충격파 발동 확률, 별 생성 확률 등.</summary>
    ChancePercent = 7,
    /// <summary>타격 횟수 기반 스킬의 필요 타격 수 가감(정수). 회전베기 n번 타격 등. 음수면 더 자주 발동.</summary>
    TriggerHitCountAdd = 8,
    /// <summary>연쇄 횟수(개수 가산). 드론 체인 라이트닝 등.</summary>
    ChainCountAdd = 9,
    /// <summary>재사용 대기 감소(%).</summary>
    CooldownReductionPercent = 10,
    // 11은 폐지된 다중 타격(MultiHit) 자리다. 도끼는 항상 단일 타격이고(기획서 3-1) 다중 타격 카드도 없다.
    // 옛 에셋에 남은 11이 다른 효과로 읽히지 않도록 이 번호는 다시 쓰지 않는다.
    /// <summary>이동 속도(%). skill=None이면 플레이어, skill=Offroad면 오프로드 돌진 속도(오프로드 IV).</summary>
    MoveSpeedPercent = 12,
    /// <summary>웨이브 클리어마다 스태미너 회복(절대값).</summary>
    StaminaRecoverPerWave = 13,
    /// <summary>최대 스태미너(절대값 가산). 현재 스태미너도 같이 오른다.</summary>
    MaxStaminaAdd = 14,
    /// <summary>최대 스태미너(%). 유리 대포 같은 감소는 음수.</summary>
    MaxStaminaPercent = 15,
    /// <summary>임계치 초과 후 첫 피해까지의 유예 시간(초 가산).</summary>
    GraceTimeAdd = 16,
    /// <summary>반복 피해 간격(초 가산). v2.7에서 피해 쿨다운 증가 카드가 삭제되고 피해 간격 = 유예 시간이 되어 지금은 쓰지 않는다(GraceTimeAdd가 둘 다 올린다).</summary>
    DamageCooldownAdd = 17,
    // 18은 폐지된 골드 획득 증가(GoldGainPercent) 자리다. v2.4에서 골드가 없어졌다. 다시 쓰지 않는다.
    /// <summary>임계치 T(정수 가산). 인내의 시간은 음수.</summary>
    ThresholdAdd = 19,
    // 20 · 21 · 22는 폐지된 카드 가격(CardPricePercent) · 리롤 금지(RerollDisabled) · 리롤 무료(RerollFree) 자리다
    // (v2.4 골드 제거로 바겐 세일! · VIP 회원권 삭제). 다시 쓰지 않는다.
    /// <summary>나무 생성 주기(%). 계약 디메리트용. 양수면 더 자주 생긴다.</summary>
    TreeSpawnRatePercent = 23,
    /// <summary>스텔라 별 표식 유지 시간(초 가산). 레벨 IV는 6초 → 9초.</summary>
    StarMarkDurationAdd = 24,
    // 25는 폐지된 회전베기 연속 회전(SpinRotationAdd) 자리다. v2.4에서 상시 회전베기와 겹쳐 삭제됐다. 다시 쓰지 않는다.
    /// <summary>충격파 한 번에 나가는 수(개수 가산). 충격파 VII는 +1(2중 발생).</summary>
    ShockwaveRepeatAdd = 26,
    /// <summary>다른 액티브 스킬도 별을 칠 수 있다. 0보다 크면 켜진다(스텔라 III).</summary>
    StarHitByActives = 27,
    /// <summary>별을 칠 때 폭발 횟수(개수 가산). 스텔라 VII는 +1(2회 연속).</summary>
    StarBlastRepeatAdd = 28,
    /// <summary>오염된 나무가 대폭발에 다시 맞을 때 지속 피해가 중첩되는 최대 수(개수 가산, 기본 1). 포자폭탄 V +1(최대 2), VII +1(최대 3).</summary>
    SporeStackAdd = 29,
    /// <summary>필드의 나무 수가 임계치 이상인 동안만 붙는 피해량(%). 공격력 증가 MAX(+20%).</summary>
    DamagePercentAboveThreshold = 30,
    /// <summary>지속형 스킬(포자 오염)로 나무를 처치하면 그 유지 시간을 늘린다(초 가산). 유지 시간 증가 MAX(+0.3초).</summary>
    DurationExtendOnDotKill = 31,
    /// <summary>캐릭터가 이동하는 동안만 붙는 공격 속도(%). 이동속도 증가 MAX(+10%).</summary>
    AttackSpeedPercentWhileMoving = 32,
    // 33은 폐지된 웨이브 클리어 골드(GoldPerWaveClear) 자리다. 다시 쓰지 않는다.
    /// <summary>도끼 공격이 연속으로 더 타격하는 횟수(개수 가산). 계약 다다익선(+1, 연속 2회 타격).</summary>
    AxeExtraStrikeAdd = 34,
    /// <summary>별 표식 나무가 받는 피해 증가율(%p 가산, 기본 +30%는 튜닝 값). 스텔라 V는 +15(+45%).</summary>
    StarMarkDamageTakenAdd = 35,
    /// <summary>지속 피해 틱 주기(초 가산, 음수면 짧아진다). 포자 수류탄 오염 틱 1.5초 → II 1.2 → IV 0.8 → VI 0.5초(−0.3 · −0.4 · −0.3, 기획서 v2.8).</summary>
    DotTickIntervalAdd = 36,
    /// <summary>지속 피해 틱 주기 감소(%). 틱 주기 × (1 − 값/100), 하한은 튜닝 값(0.2초). 지속 피해 속도 증가(MAX 누적 40).</summary>
    DotTickIntervalReducePercent = 37,
    /// <summary>오염 중첩이 최대인 나무에만 더 붙는 틱 주기 감소(%). DotTickIntervalReducePercent에 더한다. 지속 피해 속도 증가 MAX(+10).</summary>
    DotTickIntervalReduceAtMaxStackPercent = 38,
    /// <summary>충격파가 나무를 맞히지 않은 헛스윙에도 발동 판정을 한다. 0보다 크면 켜진다(충격파 MAX, 기획서 v2.6 6-1).</summary>
    ShockwaveFireOnWhiff = 39,
    /// <summary>대쉬 보유 개수(개수 가산, 충전식). 이동속도 증가 III +1, V(MAX) +1(최대 2개, 기획서 v2.7 6-3).</summary>
    DashChargeAdd = 40,
    /// <summary>도끼 한 번에 맞히는 나무 수(개수 가산, 기본 1). 범위 증가 II · III · IV · V 각 +1(MAX 5그루, 기획서 v2.6 부록 A-5).</summary>
    AxeTargetCountAdd = 41,
    /// <summary>
    /// 충격파 그루당 피해에 따로 곱하는 배율(%, 1 + 합/100). 레벨별 피해를 기준 곡선에 맞추는 데 쓴다(기획서 v2.8 A-4: 10 / 19 / 38 / 40 / 75 / 110 / 115).
    /// 피해%(DamagePercent)와 더하지 않고 곱하므로 공격력 증가 패시브가 붙어도 레벨별 비율이 유지된다.
    /// </summary>
    ShockwaveDamageScalePercent = 42,

    // 새 효과는 이 위가 아니라 여기(맨 뒤)에 추가한다.
    Count,
}

/// <summary>런 진행 상태. 기획서 7장 "런 상태 흐름"의 표와 1:1로 대응한다.</summary>
public enum ERunPhase
{
    None = 0,
    Lobby = 1,
    RunStarting = 2,
    CardSelect = 3,
    TierTransition = 4,
    WaveCombat = 5,
    WaveClear = 6,
    RunClear = 7,
    GameOver = 8,
    Result = 9,
    ReturningToLobby = 10,
}

/// <summary>임계치 피해 판정 상태. 기획서 3-2 "피해 판정 흐름".</summary>
public enum EPressureState
{
    /// <summary>나무 수가 T 미만. 피해 없음.</summary>
    Safe = 0,
    /// <summary>T 이상이 되어 유예 타이머가 흐르는 중.</summary>
    Grace = 1,
    /// <summary>첫 피해 이후 T 이상 유지. 쿨다운마다 반복 피해.</summary>
    Damaging = 2,
}

/// <summary>런이 끝난 방식.</summary>
public enum ERunEndReason
{
    GameOver = 0,
    Abandoned = 1,
}
