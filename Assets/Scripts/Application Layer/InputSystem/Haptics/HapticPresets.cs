/// <summary>
/// 이벤트별 진동 파형과 재발동 규칙을 모아 둔 표입니다. 세기를 손보고 싶으면 여기만 고치면 됩니다.
///
/// 세기 감각 기준 (실제로 나가는 값은 여기에 유저 설정 배율이 한 번 더 곱해집니다):
/// - 0.1~0.25 : 있는 줄 겨우 아는 정도 (아이템이 박히는 톡톡한 느낌)
/// - 0.3~0.5  : 약~중 (탑승, 착지)
/// - 0.6~0.9  : 타격감 (사망, 시동이 걸리는 순간)
///
/// 두 모터의 역할이 다릅니다. low(굵은 모터)는 묵직하고 둔한 느낌, high(가는 모터)는 가볍고
/// 또렷한 느낌입니다. "가볍게"는 high 위주로, "묵직하게"는 low 위주로 씁니다.
/// </summary>
public static class HapticPresets
{
    // 나무 타격 - 묵직하고 확실한 도끼 타격감.
    private static readonly HapticPattern treeImpact = new HapticPattern(
        new HapticStep(0.60f, 0.50f, 0.11f));

    // 나무 파괴 - 강렬한 파괴 임팩트.
    private static readonly HapticPattern treeDestroy = new HapticPattern(
        new HapticStep(0.85f, 0.60f, 0.16f));

    // 차량 시동 - 강력한 크랭킹 두 번 → 엔진 점화 폭발 → 안정적인 공회전.
    private static readonly HapticPattern vehicleIgnition = new HapticPattern(
        new HapticStep(0.70f, 0.40f, 0.09f),
        new HapticStep(0f, 0f, 0.06f),
        new HapticStep(0.70f, 0.40f, 0.09f),
        new HapticStep(0f, 0f, 0.06f),
        new HapticStep(0.90f, 0.60f, 0.13f),
        new HapticStep(0.40f, 0.25f, 0.25f),
        new HapticStep(0.25f, 0.12f, 0.35f));

    // 스킬 찍기 - 손끝에 확실하게 닿는 경쾌한 딸깍 손맛.
    private static readonly HapticPattern skillPoint = new HapticPattern(
        new HapticStep(0.30f, 0.65f, 0.09f));

    // 프레스티지 레벨업 - 단계별로 강렬하게 치고 올라가는 화려한 레벨업 연출.
    private static readonly HapticPattern prestigeLevelUp = new HapticPattern(
        new HapticStep(0.35f, 0.50f, 0.07f),
        new HapticStep(0f, 0f, 0.05f),
        new HapticStep(0.50f, 0.70f, 0.07f),
        new HapticStep(0f, 0f, 0.05f),
        new HapticStep(0.70f, 0.85f, 0.09f),
        new HapticStep(0f, 0f, 0.06f),
        new HapticStep(0.90f, 0.45f, 0.22f));

    // 원목/코인이 박힘 - 손바닥에 묵직하게 감기는 찰진 획득 손맛.
    private static readonly HapticPattern itemImpact = new HapticPattern(
        new HapticStep(0.20f, 0.55f, 0.07f));

    // 차량 탑승 - 쿵 안착하는 묵직한 하중감.
    private static readonly HapticPattern vehicleBoard = new HapticPattern(
        new HapticStep(0.70f, 0.45f, 0.14f));

    // 상자가 차 위에 착지 - 육중한 원목 컨테이너의 착지 충격.
    private static readonly HapticPattern containerLanding = new HapticPattern(
        new HapticStep(0.70f, 0.45f, 0.13f));

    // 차량에서 하차 - 안정적인 바닥 착지감.
    private static readonly HapticPattern vehicleDismount = new HapticPattern(
        new HapticStep(0.60f, 0.40f, 0.12f));

    // 스태미너 소진 사망 - 묵직한 충격과 잦아드는 긴 탈력감.
    private static readonly HapticPattern staminaDeath = new HapticPattern(
        new HapticStep(0.85f, 0.50f, 0.12f),
        new HapticStep(0.45f, 0.25f, 0.20f));

    // 고급 원목 생성 - "쨍" 하고 터지는 당첨 쾌감.
    private static readonly HapticPattern rareLogSpawn = new HapticPattern(
        new HapticStep(0.75f, 0.70f, 0.10f),
        new HapticStep(0.35f, 0.45f, 0.14f));

    // 원목 유실 - 바닥에 흘렸음을 확실히 인지할 수 있는 가벼운 틱.
    private static readonly HapticPattern itemDropped = new HapticPattern(
        new HapticStep(0.15f, 0.50f, 0.07f));

    private static readonly HapticPattern[] patterns = BuildPatterns();

    /// <summary>
    /// 이벤트별 "묶음 간격"(초)입니다. 마지막 요청으로부터 이 시간 안에 들어온 같은 이벤트는
    /// 같은 한 번의 사건으로 보고 새로 울리지 않습니다.
    ///
    /// 나무 타격이 0이 아닌 이유가 핵심입니다. 쇼크웨이브는 0.04초마다 판정을 돌며 퍼져 나가기
    /// 때문에, 한 번 휘두른 결과가 여러 프레임에 걸쳐 수십 그루에 나눠 들어옵니다. 프레임 단위로만
    /// 막으면 그 내내 진동이 재시작되어 "드르륵" 끌리는 소리가 됩니다. 판정 주기보다 넉넉한
    /// 0.11초로 묶으면 도끼 평타 + 뒤따르는 쇼크웨이브 전체가 한 번의 진동으로 정리됩니다.
    /// (도끼 쿨타임은 이보다 길어서, 다음 휘두르기가 여기에 잡아먹히지 않습니다)
    ///
    /// 반대로 아이템이 박히는 진동은 하나하나 톡톡 느껴져야 하므로 0입니다.
    /// </summary>
    private static readonly float[] burstIntervals = BuildBurstIntervals();

    /// <summary>이벤트에 해당하는 파형입니다. 표에 없으면 null입니다.</summary>
    public static HapticPattern GetPattern(EHapticEvent _event)
    {
        int _index = (int)_event;

        if (_index < 0 || _index >= patterns.Length) return null;

        return patterns[_index];
    }

    /// <summary>이벤트를 하나의 사건으로 묶는 시간(초)입니다. 0이면 요청할 때마다 울립니다.</summary>
    public static float GetBurstInterval(EHapticEvent _event)
    {
        int _index = (int)_event;

        if (_index < 0 || _index >= burstIntervals.Length) return 0f;

        return burstIntervals[_index];
    }

    private static HapticPattern[] BuildPatterns()
    {
        HapticPattern[] _table = new HapticPattern[(int)EHapticEvent.Count];

        _table[(int)EHapticEvent.TreeImpact] = treeImpact;
        _table[(int)EHapticEvent.TreeDestroy] = treeDestroy;
        _table[(int)EHapticEvent.VehicleIgnition] = vehicleIgnition;
        _table[(int)EHapticEvent.SkillPoint] = skillPoint;
        _table[(int)EHapticEvent.PrestigeLevelUp] = prestigeLevelUp;
        _table[(int)EHapticEvent.ItemImpact] = itemImpact;
        _table[(int)EHapticEvent.VehicleBoard] = vehicleBoard;
        _table[(int)EHapticEvent.ContainerLanding] = containerLanding;
        _table[(int)EHapticEvent.VehicleDismount] = vehicleDismount;
        _table[(int)EHapticEvent.StaminaDeath] = staminaDeath;
        _table[(int)EHapticEvent.RareLogSpawn] = rareLogSpawn;
        _table[(int)EHapticEvent.ItemDropped] = itemDropped;

        return _table;
    }

    private static float[] BuildBurstIntervals()
    {
        float[] _table = new float[(int)EHapticEvent.Count];

        // 한 번 휘두른 결과(평타 + 쇼크웨이브 + 연쇄 폭발)를 한 번으로 묶되, 고속 평타 연타가 씹히지 않도록 맞춘다.
        _table[(int)EHapticEvent.TreeImpact] = 0.11f;
        _table[(int)EHapticEvent.TreeDestroy] = 0.16f;

        // 연출 중 중복 호출 방지용 최소 간격. 사람이 두 번 겪는 일이 아니라 코드가 두 번 부르는 경우만 막는다.
        _table[(int)EHapticEvent.VehicleIgnition] = 0.5f;
        _table[(int)EHapticEvent.VehicleBoard] = 0.5f;
        _table[(int)EHapticEvent.ContainerLanding] = 0.5f;
        _table[(int)EHapticEvent.VehicleDismount] = 0.5f;
        _table[(int)EHapticEvent.StaminaDeath] = 0.5f;

        // 원목 묶음(4~7개)이 한 번에 튀어나와도 생성 진동은 한 번만 울린다.
        _table[(int)EHapticEvent.RareLogSpawn] = 0.2f;

        // 나머지(스킬, 프레스티지, 아이템 박힘/유실)는 하나하나 느껴져야 하므로 묶지 않는다.

        return _table;
    }
}
