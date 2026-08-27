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
///
/// 짧은 진동이 약하게 느껴질 때, 세기 숫자만 올리는 것은 생각보다 효과가 적습니다. 패드의 모터는
/// 추를 돌려서 떠는 물리 장치라 지정한 세기가 즉시 나오지 않습니다. 멈춰 있던 추가 제 속도로
/// 도는 데만 굵은 모터는 50~80ms, 가는 모터도 20~40ms가 걸립니다. 0.1초짜리 진동은 그 가속
/// 구간에서 대부분이 끝나버려서, 0.6으로 적어도 손에는 0.3쯤으로 옵니다.
///
/// 그래서 짧은 파형은 앞에 짧고 센 구간(킥)을 두고 곧바로 낮추는 편이 훨씬 세게 느껴집니다.
/// 추를 단번에 띄워 놓고 힘을 빼는 식이라, 전체 길이는 그대로인데 첫 순간의 타격감이 살아납니다.
/// 세기를 낮추고 싶을 때도 킥 구간을 없애지 말고 킥과 꼬리를 같은 비율로 낮추세요.
///
/// 그리고 "약하다"고 느껴질 때 실제로 가장 잘 듣는 레버는 세기가 아니라 **지속시간**입니다.
/// 세기는 1.0이 천장이라 금방 한계에 부딪히지만, 체감 강도는 "얼마나 셌나"보다 "얼마나 오래
/// 떨었나"에 훨씬 크게 좌우됩니다. 위에서 말한 가속 구간 때문에, 0.1초짜리는 세기를 뭘로 적든
/// 전부 "톡" 이상이 되지 않습니다. 타격감을 원하면 0.2초, 묵직함을 원하면 0.3초 이상이 필요합니다.
/// 옵션의 진동 미리듣기가 세게 느껴지는 것도 세기 때문이 아니라, 슬라이더를 잡고 있는 동안
/// 0.05초마다 계속 재생되어 모터가 멈추지 않고 도는 덕분입니다.
///
/// 그 성질 때문에, **연달아 터지는 이벤트는 한 발짜리와 같은 기준으로 잡으면 안 됩니다.**
/// 상자 입출고는 0.075초, 상점 코인은 개수가 많으면 0.03초 간격까지 좁아집니다. 그 간격에서는
/// 앞의 진동이 멎기 전에 다음 것이 들어와 세기가 계속 쌓이므로, 한 발씩 보면 약한 파형이라도
/// 실제로는 아주 센 연속 진동이 됩니다. 연속 이벤트는 두 가지로 다스립니다.
/// 1) 굵은 모터를 쓰지 않는다. 굵은 모터는 멎는 데도 오래 걸려 쌓임이 가장 심하다.
/// 2) 묶음 간격을 줘서 지나치게 촘촘한 것은 솎아낸다. (아래 BuildBurstIntervals 참고)
/// </summary>
public static class HapticPresets
{
    // 나무 타격 - 묵직하고 확실한 도끼 타격감.
    // 총 0.2초. 0.1초대에서는 모터가 제 속도로 돌기도 전에 끝나 아무리 세게 적어도 "톡"에 그친다.
    // 도끼 쿨타임(최소 0.05초까지 줄지만 통상 0.3초 이상)보다는 짧아, 연타해도 서로 겹쳐 뭉개지지 않는다.
    private static readonly HapticPattern treeImpact = new HapticPattern(
        new HapticStep(1.0f, 0.85f, 0.055f),
        new HapticStep(0.70f, 0.50f, 0.15f));

    // 나무 파괴 - 강렬한 파괴 임팩트. 총 0.36초로, 타격(0.2초)보다 확실히 길어야 "쓰러졌다"로 읽힌다.
    // 세기는 이미 천장(1.0)이라 여기서 더 세게 만드는 방법은 길이를 늘리는 것뿐이다.
    // 최대치로 때린 뒤 두 단계에 걸쳐 잦아들며 넘어가는 여운을 남긴다.
    private static readonly HapticPattern treeDestroy = new HapticPattern(
        new HapticStep(1.0f, 1.0f, 0.09f),
        new HapticStep(0.85f, 0.60f, 0.16f),
        new HapticStep(0.50f, 0.28f, 0.11f));

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
    // 0.12초로 짧게 유지하되, 첫 순간을 최대치로 때려 "눌렸다"는 감각을 확실히 만든다.
    private static readonly HapticPattern skillPoint = new HapticPattern(
        new HapticStep(0.60f, 1.0f, 0.045f),
        new HapticStep(0.35f, 0.50f, 0.075f));

    // 프레스티지 레벨업 - 단계별로 강렬하게 치고 올라가는 화려한 레벨업 연출.
    private static readonly HapticPattern prestigeLevelUp = new HapticPattern(
        new HapticStep(0.35f, 0.50f, 0.07f),
        new HapticStep(0f, 0f, 0.05f),
        new HapticStep(0.50f, 0.70f, 0.07f),
        new HapticStep(0f, 0f, 0.05f),
        new HapticStep(0.70f, 0.85f, 0.09f),
        new HapticStep(0f, 0f, 0.06f),
        new HapticStep(0.90f, 0.45f, 0.22f));

    // 필드 원목 습득 - 톡 하고 손에 감기는 획득감.
    // 처음엔 "1~3틱"(0.04~0.07초) 요청대로 짧게 잡았는데, 그 길이로는 모터가 반응조차 못 해
    // 손에 아무것도 오지 않았다. 0.105초로 늘려 확실히 잡히게 한다.
    // 다만 나무 한 그루가 원목 4~7개를 뿌리고 그것들이 몰려서 흡수되므로, 굵은 모터는 거들기만
    // 하는 수준으로 낮춘다. 굵은 모터를 세게 쓰면 몰리는 구간에서 통째로 쌓여 묵직해진다.
    private static readonly HapticPattern itemPickup = new HapticPattern(
        new HapticStep(0.25f, 0.90f, 0.040f),
        new HapticStep(0.10f, 0.40f, 0.065f));

    // 상자↔캐릭터 원목 이동, 상점 코인 - 연달아 쏟아지는 획득.
    // 간격이 0.075초(상자), 코인은 개수가 많으면 0.03초까지 좁아진다. 한 발씩은 이만큼 약해야
    // 이어졌을 때 "짤랑짤랑"이 되고, 조금만 세도 통째로 붙어 센 연속 진동이 된다.
    // 굵은 모터를 아예 쓰지 않는 것이 핵심이다(멎는 데 오래 걸려 쌓임이 가장 심하다).
    private static readonly HapticPattern itemStream = new HapticPattern(
        new HapticStep(0f, 0.50f, 0.025f),
        new HapticStep(0f, 0.20f, 0.035f));

    // 차량 탑승 - 쿵 안착하는 묵직한 하중감.
    private static readonly HapticPattern vehicleBoard = new HapticPattern(
        new HapticStep(0.70f, 0.45f, 0.14f));

    // 상자가 차 위에 착지 - 약하게. 요청 자체가 "약하게"인데 탑승(약~중)과 같은 값이라 더 셌다.
    // 킥으로 착지의 순간만 또렷하게 잡고 몸통은 확실히 낮춰, 탑승보다 한 단계 가볍게 만든다.
    private static readonly HapticPattern containerLanding = new HapticPattern(
        new HapticStep(0.55f, 0.40f, 0.045f),
        new HapticStep(0.28f, 0.16f, 0.085f));

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

    // 원목 유실 - 전체에서 가장 약한 틱. 0.08초 간격으로 최대 15개가 연달아 나가므로,
    // 굵은 모터를 쓰면 그 15번이 통째로 쌓여 "잃었다"가 아니라 "터졌다"가 된다.
    // 획득(itemStream)보다도 한 단계 약하게 두어야 잃는 느낌이 산다.
    private static readonly HapticPattern itemDropped = new HapticPattern(
        new HapticStep(0f, 0.38f, 0.022f),
        new HapticStep(0f, 0.15f, 0.030f));

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
        _table[(int)EHapticEvent.ItemPickup] = itemPickup;
        _table[(int)EHapticEvent.ItemStream] = itemStream;
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

        // 상점 코인은 개수가 많으면 간격이 0.03초까지 좁아진다. 그대로 두면 전부 이어붙어
        // 한 덩어리의 센 진동이 되므로, 지나치게 촘촘한 것만 솎아내 0.05초 간격을 보장한다.
        // (상자 입출고는 0.075초라 이 값에 걸리지 않고 하나하나 그대로 울린다)
        _table[(int)EHapticEvent.ItemStream] = 0.05f;

        // 나머지(스킬, 프레스티지, 필드 습득, 원목 유실)는 하나하나 느껴져야 하므로 묶지 않는다.

        return _table;
    }
}
