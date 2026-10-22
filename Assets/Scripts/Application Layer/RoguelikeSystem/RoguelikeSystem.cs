using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크 모드의 오케스트레이터. 기획서 7장 "런 상태 흐름"을 그대로 구현한다.
///
///   로비 → 런 시작 → [카드 선택 → (티어 전환) → 웨이브 전투 → 웨이브 클리어] × 30 → 런 클리어
///   → 언리밋(카드 없이 웨이브 반복) → 게임오버/포기 → 결과 기록 → 결과 화면 → 로비
///
/// GameInstaller가 로그라이크 세션일 때만 만든다(본편 세션에서는 존재하지 않는다).
/// 본편 시스템(캐릭터·나무 매니저·타일맵·HUD)은 그대로 재사용하되, 던전 원정 흐름(InDungeonSystem)은 타지 않는다.
///
/// [역할] 이펙트(파티클·셰이더·잔상 등 그림 효과)와 UI만 각 담당이 입히고, 그 밖의 시스템·움직임·손맛(가감속·반동·카메라 흔들림·진동)·
/// 효과음은 이 시스템 쪽 코드가 맡는다.
/// [이펙트] 시스템이 기다려야 하는 연출은 RoguelikeProductionGate(IRoguelikeProductionHandler)로, 기다릴 필요 없는 연출은
/// RoguelikeSystemSignals의 상태 알림 시그널로 이펙트 담당이 입힌다. 이펙트가 하나도 없어도 루프는 끝까지 돈다.
/// [UI] RoguelikeUICoordinator가 로그라이크 UIView와 연결한다.
///
/// 흐름 코루틴은 RoguelikeSystem.Flow.cs에 있다.
/// </summary>
public partial class RoguelikeSystem : MonoBehaviour, IRoguelikeRunInfo
{
    // 메인메뉴 커튼을 다시 내린다(로비 → 메인메뉴). GameInstaller가 BootStrap까지 그대로 올려 보낸다(본편 던전 이탈과 같은 경로).
    // 진입 때의 커튼 걷기는 메인메뉴가 암전 아래에서 직접 한다(UIView_MainMenu.RoguelikeEntryRoutine).
    public event Action GoToMainMenuCurtainRevealEvent;

    [Serializable]
    public struct TierFieldData
    {
        [Tooltip("이 티어의 지형(타일·환경 오브젝트·파티클). 기존 스테이지 리소스를 쓴다(기획서 9장).")]
        public MapType mapType;
        [Tooltip("나무 종류 분포(DensityData)를 고르는 숲. 같은 MapType의 하위 숲이어야 한다.")]
        public ForestType forestType;
        [Tooltip("티어 안 초기 · 중간 · 후반 구간의 나무 종류(기획서 7-1 비주얼 진화). 밸런스 웨이브 표의 treeKind가 이 순번을 가리킨다. 비우면 숲의 분포를 그대로 쓴다.")]
        public List<TreeType> treeTypes;

        public TreeType GetTreeType(int _kind)
        {
            if (null == treeTypes || _kind < 0 || _kind >= treeTypes.Count) return TreeType.None;
            return treeTypes[_kind];
        }
    }

    /// <summary>
    /// 섬 둘레 물의 구름. 본편 분포(맵 전체 약 196×95에 700개, 0.06~0.075/초, 불투명도 0.1)로는 섬 둘레의 좁은 물에
    /// 두세 개만 걸치고 거의 멈춘 듯 보여, 섬 한 화면 둘레에만 촘촘히 깔고 더 빠르고 진하게 쓴다.
    /// </summary>
    [Serializable]
    private sealed class FieldCloudSettings
    {
        [Tooltip("섬 한 화면 둘레(좌우로는 구름 반 폭씩 더)에 까는 구름 수.")]
        public int count = 35;
        [Tooltip("구름이 흐르는 속도 범위(월드 단위/초). 본편은 0.06~0.075.")]
        public float minSpeed = 0.07f;
        public float maxSpeed = 0.1f;
        [Tooltip("물에 비친 구름의 불투명도(0~1). 본편 재질 값은 0.1.")]
        [Range(0f, 1f)] public float alpha = 0.12f;
        [Tooltip("기준 화면(또는 그보다 넓은 실제 화면) 바깥으로 구름을 더 까는 여유(월드). 해상도가 바뀌어도 가장자리가 비지 않게 한다.")]
        public float viewMargin = 1f;
        [Tooltip("진입·티어 전환에서 파도가 지나간 구름이 서서히 드러나고(새 구름) 지워지는(직전 구름) 시간(초).")]
        public float fadeSeconds = 1f;
    }

    [Header("데이터")]
    [SerializeField] private RoguelikeBalanceSO balance;
    [SerializeField] private CardDatabaseSO cardDatabase;
    [Tooltip("웨이브 나무 생성 전략 원본. 런타임에는 사본을 쓴다.")]
    [SerializeField] private RoguelikeWaveTreeStrategySO waveStrategyTemplate;

    [Header("필드")]
    [Tooltip("티어 순서대로의 지형. 로비는 첫 칸을 쓴다. 모자라면 마지막 칸을 쓴다.")]
    [SerializeField] private List<TierFieldData> tierFields = new List<TierFieldData>();
    [Tooltip("섬 크기를 맞추는 기준 화면의 월드 크기. 1920×1080에서 보이는 영역(직교 크기 5.625 → 20 × 11.25)이다.\n" +
        "섬은 이 값에서 한 번 계산한 '절대 크기'라 다른 해상도에서도 크기가 변하지 않는다.")]
    [SerializeField] private Vector2 islandReferenceViewSize = new Vector2(20f, 11.25f);
    [Tooltip("기준 화면 가장자리(좌우·상하)에 보일 물 타일 수. 기획 1~3칸. 섬 반지름 = 기준 화면 절반 - 이 칸 수 × 타일 크기.")]
    [SerializeField] private float islandEdgeWaterTiles = 1f;
    [Tooltip("시작 지점 주변의 나무 금지 반경(칸). 본편 값 15는 섬보다 커서 로그라이크에서는 작게 덮어쓴다.")]
    [SerializeField] private float centerSafeZoneRadius = 2f;
    [Tooltip("바닥 타일 파도(기획서 4-1 진입 3단계, 3-4 지형 전환 공용) 수치.")]
    [SerializeField] private RoguelikeTileWaveSettings tileWaveSettings = new RoguelikeTileWaveSettings();
    [Tooltip("섬 둘레 물에 비치는 구름 수치. 본편 던전 구름 값은 바꾸지 않는다.")]
    [SerializeField] private FieldCloudSettings fieldCloudSettings = new FieldCloudSettings();
    [Tooltip("섬 둘레 물에 띄우는 장식(맵 종류별 본편 물 장식) 수치.")]
    [SerializeField] private RoguelikeWaterDecoSettings waterDecoSettings = new RoguelikeWaterDecoSettings();
    [Tooltip("결과 화면 → 로비 진입 1~2단계(디스토션 → 암전)와 암전이 걷히는 페이드인 수치. 메인메뉴에서 들어올 때의 디스토션은 UIView_MainMenu가 같은 수치 형식으로 따로 갖는다.")]
    [SerializeField] private RoguelikeScreenTransitionSettings screenTransitionSettings = new RoguelikeScreenTransitionSettings();

    [Header("플레이어 낙하(기획서 4-1 6단계)")]
    [Tooltip("시작 칸 위 이 높이(월드)에서 떨어진다. 기준 화면 세로 절반(5.6)보다 크면 화면 위쪽 밖에서 들어온다.")]
    [SerializeField] private float characterDropHeight = 7f;
    [Tooltip("떨어지는 시간(초). 처음엔 느리고 점점 빨라진다(중력처럼).")]
    [SerializeField] private float characterDropSeconds = 0.45f;

    [Header("로비")]
    [SerializeField] private RoguelikeLobbyInteractable startRunObjectPrefab;
    [SerializeField] private RoguelikeLobbyInteractable recordsObjectPrefab;
    [SerializeField] private Vector2 startRunObjectOffset = new Vector2(2.5f, 1f);
    [SerializeField] private Vector2 recordsObjectOffset = new Vector2(-2.5f, 1f);
    [Tooltip("로비 오브젝트를 올릴 충돌 레이어. 도끼가 맞히는 레이어(나무 레이어)여야 한다.")]
    [SerializeField] private LayerMask lobbyObjectLayer;

    [Header("신규 스킬·조합")]
    [SerializeField] private RoguelikeSkillController skillController;

    [Header("카드 효과")]
    [Tooltip("공통 패시브 '공격속도 증가'가 도끼에도 붙는지. 기획서 6-3은 붙는다.")]
    [SerializeField] private bool bAttackSpeedAffectsAxe = true;

    [Header("웨이브 클리어 묘목 연출(기획서 3-1 \"달성 즉시\")")]
    [Tooltip("남은 나무가 묘목으로 돌아가 톡 튀었다가 작아지며 사라지는 시간(초). 나무 회수는 이 연출이 끝난 뒤에 한다.")]
    [SerializeField] private float waveClearSaplingVanishSeconds = 0.6f;
    [Tooltip("묘목으로 돌아갈 때 쓸 나무 종류별 묘목 위쪽 그림(기획서 v2.7 3-1 \"원래 묘목 그대로\"). 공용 나무 데이터(Tree Visual Data Base)의 묘목 그림이 " +
        "다른 종류 것으로 들어 있는 종류만 넣는다(예: 균사 나무 → FluffyMyceliumTree_Baby_Top). 본편 데이터는 바꾸지 않는다.")]
    [SerializeField] private List<SaplingSpriteOverride> saplingTopOverrides = new List<SaplingSpriteOverride>();

    [Serializable]
    private struct SaplingSpriteOverride
    {
        public TreeType treeType;
        public Sprite saplingTop;
    }

    [Header("런 클리어 연출(기획서 3-5 - 30웨이브 마지막 나무 줌인 + 슬로우 부서짐 → 줌아웃)")]
    [Tooltip("마지막 나무로 확대하는 배율(몇 배 확대).")]
    [SerializeField] private float runClearZoomMultiplier = 2f;
    [Tooltip("마지막 나무로 빠르게 다가가는 시간(실제 초).")]
    [SerializeField] private float runClearZoomInSeconds = 0.25f;
    [Tooltip("슬로우 동안의 시간 배율.")]
    [Range(0.05f, 1f)] [SerializeField] private float runClearSlowTimeScale = 0.25f;
    [Tooltip("확대한 채 슬로우로 부서지는 모습을 보여 주는 시간(실제 초).")]
    [SerializeField] private float runClearSlowSeconds = 1.2f;
    [Tooltip("섬 전체로 돌아가는 시간(실제 초).")]
    [SerializeField] private float runClearZoomOutSeconds = 0.6f;

    [Header("연출 핸들러가 없을 때 기다리는 시간(초)")]
    [SerializeField] private float fallbackLobbyEntrySeconds = 0.3f;
    [SerializeField] private float fallbackTierTransitionSeconds = 0.5f;
    [SerializeField] private float fallbackWaveClearSeconds = 0.6f;
    [SerializeField] private float fallbackRunClearSeconds = 1f;
    [SerializeField] private float fallbackResultToLobbySeconds = 0.2f;
    [SerializeField] private float fallbackLobbyToRunSeconds = 0f;
    [Tooltip("연출 핸들러가 완료를 알리지 않을 때 강제로 진행하기까지의 시간.")]
    [SerializeField] private float maxProductionWaitSeconds = 15f;
    [Tooltip("카드 UI가 구매 연출 완료를 알리지 않을 때 강제로 진행하기까지의 시간.")]
    [SerializeField] private float maxCardPresentationWaitSeconds = 10f;
    [Tooltip("노출 가능한 카드가 0장일 때 안내 연출이 완료를 알리지 않으면 이 시간(초) 뒤 웨이브를 시작한다.")]
    [SerializeField] private float noCardsNoticeFallbackSeconds = 1.5f;
    [Tooltip("캐릭터가 쓰러진 뒤 결과 화면까지 기다리는 상한(본편은 1.5초 뒤 알림이 온다).")]
    [SerializeField] private float maxCharacterDownWaitSeconds = 5f;
    [Tooltip("메인메뉴 이탈 상승 연출(본편과 같은 카메라 상승)이 완료를 알리지 않을 때 강제로 메인메뉴로 넘기기까지의 시간.")]
    [SerializeField] private float exitToMainMenuSeconds = 1f;

    // // 외부 의존성
    private SignalHub signalHub;
    private InputManager inputManager;
    private EnvironmentSystem environmentSystem;
    private InDungeonObjectManager inDungeonObjectManager;
    private UnitLogicManager unitLogicManager;
    private TownSystem townSystem;
    // 씬 전환 연출(메인메뉴 ↔ 로그라이크). 본편 메인메뉴 ↔ 마을과 같은 카메라 하강·상승을 쓴다.
    private SkyCameraProductionManager skyCameraProductionManager;
    private GameplayUIInstaller gameplayUIInstaller;
    private Character character;

    // // 내부 의존성 (규칙)
    private readonly RunBuildState build = new RunBuildState();
    private readonly RunModifiers modifiers = new RunModifiers();
    private readonly CardShop shop = new CardShop();
    private readonly StaminaPressureLogic pressure = new StaminaPressureLogic();
    private readonly RoguelikeTreeRules treeRules = new RoguelikeTreeRules();
    private readonly RunEffectApplier effectApplier = new RunEffectApplier();
    private readonly RoguelikeProductionGate productionGate = new RoguelikeProductionGate();
    private readonly RoguelikeUICoordinator uiCoordinator = new RoguelikeUICoordinator();
    private readonly RoguelikeTileWave tileWave = new RoguelikeTileWave();
    private readonly RoguelikeWaterDeco waterDeco = new RoguelikeWaterDeco();
    private readonly RoguelikeCloudFade cloudFade = new RoguelikeCloudFade();
    private Coroutine tileWaveCoroutine;
    private RoguelikeWaveTreeStrategySO waveStrategy;
    private RoguelikeRecordData records;

    // 카메라가 따라갈 섬 중앙의 고정 지점(캐릭터 대신).
    private Transform cameraAnchor;

    // 진입 파도 동안 캐릭터를 비켜 두는 자리(시작 칸 기준). 기준 화면 세로 절반(5.6)보다 멀어 화면 밖이고 그리드 안이다.
    private static readonly Vector3 characterHiddenOffset = new Vector3(0f, 15f, 0f);

    // 지금 틀고 있는 지역 BGM. 같은 곡을 다시 PlayBGM하면 처음부터 다시 시작하므로 바뀔 때만 튼다.
    private bool bFieldBgmPlaying;
    private SoundID currentFieldBgm;

    private RoguelikeLobbyInteractable startRunObject;
    private RoguelikeLobbyInteractable recordsObject;

    private readonly List<int> discardedOfferIndices = new List<int>(4);
    private readonly List<IRoguelikeProductionHandler> handlerBuffer = new List<IRoguelikeProductionHandler>(8);

    private Action<TreeObj, Vector3> treeKilledHandler;
    private Action<ERoguelikeLobbyAction> lobbyActionHandler;

    // // 런 상태
    private ERunPhase phase = ERunPhase.None;
    private WaveSpec currentWave;
    private int waveKills;
    // 다음에 진행할 통산 웨이브 번호(1부터). RunRoutine이 웨이브를 마칠 때마다 올린다.
    private int runWaveNumber;
    // 지금 필드에 깔린 지형의 티어(로비는 0). 웨이브의 티어와 다르면 지형 전환을 한다.
    private int currentFieldTierIndex;
    private bool bRunActive;
    private bool bRunEnding;
    private bool bCleared;
    private float playTimeSeconds;
    private float clearTimeSeconds;
    private int treesFelled;
    private int lastClearedWave;
    private int lastClearedUnlimitWave;
    // 허탕 이동 측정(기획서 v2.3 C-2): 이 판의 웨이브 전투 시간 합과 그중 나무 없이 이동한 시간.
    private float combatSecondsForIdle;
    private float idleMoveSeconds;
    private Vector3 finalTreePosition;
    // 런 클리어 슬로우가 걸려 있다(흐름이 끊기면 시간 배율을 되돌린다).
    private bool bRunClearSlowMo;
    // 웨이브 클리어 묘목 연출이 끝나는 게임 시간. 나무 회수는 이 뒤에 한다.
    private float saplingVanishEndTime;
    private bool bExitingToMainMenu;

    // 로비로 들어가는 동안(필드 재생성·입장 연출) ESC를 잠그는 소유자 이름. 이 구간의 메인메뉴 요청은 받을 곳이 없다 -
    // 런도 아니고 로비도 아니라 무시되는데, ESC 쪽은 이미 메뉴를 닫고 이동 잠금을 풀어 버린다.
    private const string LobbyEntryESCLockOwner = "RoguelikeLobbyEntry";
    private const string RunClearESCLockOwner = "RoguelikeRunClear";

    // 로그라이크 세션 내내 본편 가방 키(인벤토리 팝업·원목 교체)를 막는 소유자 이름. 로그라이크에는 가방이 없고,
    // 열리면 카드 선택·결과 화면 위에 본편 가방이 겹쳐 뜬다. 잠금 집합은 앱 수명이라 Release에서 반드시 푼다.
    private const string SessionInventoryLockOwner = "RoguelikeSession";
    // 로비 진입 암전이 HUD 내림 모션을 기다리는 상한(초, 검은 화면 아래로 들어온 때부터). 모션이 멈춰 버려도 진입이 막히지 않게 한다.
    private const float EntryHudSettleLimitSeconds = 2f;

    private RunRecordEntry lastRunRecord;
    private int lastClearRank = -1;
    private int lastUnlimitRank = -1;

    // 다른 곳에서 알려 주기를 기다리는 플래그(시그널로 켜진다).
    private bool bCardPresentationDone;
    private bool bResultConfirmed;
    private bool bCharacterDownCompleted;

    private Coroutine flowCoroutine;

    // // IRoguelikeRunInfo
    public ERunPhase Phase => phase;
    public WaveSpec CurrentWave => currentWave;
    public int WaveKills => waveKills;
    public int FieldTreeCount => null != inDungeonObjectManager ? inDungeonObjectManager.ActiveTrees.Count : 0;
    public int Threshold => pressure.Threshold;
    public int SpawnCap => CurrentSpawnCap;
    /// <summary>지금 스폰 상한(필드 최대 나무 수). 임계치와 무관한 고정값이다(기획서 v2.6 3-1, 80그루). 계약으로 임계치가 줄어도 그대로다.</summary>
    private int CurrentSpawnCap => null != balance ? balance.SpawnCap : 0;
    public EPressureState PressureState => pressure.State;
    public float PressureGraceRatio => pressure.GraceRatio;
    public float PressureCooldownRatio => pressure.CooldownRatio;
    public bool PressureHitLethal => ERunPhase.WaveCombat == phase && null != character && character.pHealthComponent.GetCurrentStamina() <= currentWave.staminaDamage;
    public bool IsCardSelectionOpen => shop.IsOpen;
    public bool IsFirstPick => shop.IsFirstPick;
    public IReadOnlyList<CardOffer> CurrentOffers => shop.Offers;
    public bool CanReroll => shop.CanReroll;
    public IReadOnlyList<int> LastDiscardedOfferIndices => discardedOfferIndices;
    public RunBuildState Build => build;
    public CardDatabaseSO CardDatabase => cardDatabase;
    public RoguelikeRecordData Records => records;
    public RunRecordEntry LastRunRecord => lastRunRecord;
    public int LastClearRank => lastClearRank;
    public int LastUnlimitRank => lastUnlimitRank;

    // // 디버그·연출 담당용 접근
    public RoguelikeProductionGate ProductionGate => productionGate;
    /// <summary>바닥 타일 파도. 이펙트 담당이 TileRevealedEvent로 칸 단위 장식을 붙인다.</summary>
    public RoguelikeTileWave TileWave => tileWave;
    public RunModifiers Modifiers => modifiers;
    public float PlayTimeSeconds => playTimeSeconds;
    /// <summary>이 판의 전투 시간 중 나무 없이 이동한 시간 비율(0~1, 측정용).</summary>
    public float IdleMoveRatio => combatSecondsForIdle > 0f ? idleMoveSeconds / combatSecondsForIdle : 0f;

    // // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(SignalHub _signalHub, InputManager _inputManager, EnvironmentSystem _environmentSystem,
        InDungeonObjectManager _inDungeonObjectManager, UnitLogicManager _unitLogicManager, TownSystem _townSystem,
        GameplayUIInstaller _gameplayUIInstaller, SkyCameraProductionManager _skyCameraProductionManager)
    {
        signalHub = _signalHub;
        inputManager = _inputManager;
        environmentSystem = _environmentSystem;
        inDungeonObjectManager = _inDungeonObjectManager;
        unitLogicManager = _unitLogicManager;
        townSystem = _townSystem;
        gameplayUIInstaller = _gameplayUIInstaller;
        skyCameraProductionManager = _skyCameraProductionManager;

        if (null == balance || null == cardDatabase || null == waveStrategyTemplate)
        {
            Debug.LogError("[RoguelikeSystem] balance / cardDatabase / waveStrategyTemplate 중 비어 있는 것이 있습니다. 프리팹을 확인하십시오.");
        }

        records = RoguelikeRecordRepository.Load();

        // 정적 상태라 이전 세션(에디터 재생 포함)의 값이 남아 있을 수 있다. 뷰를 열기(OpenViews) 전에 비운다.
        RoguelikeModalInput.Reset();

        inputManager.SetInventoryKeyLock(SessionInventoryLockOwner, true);

        shop.Initialize(cardDatabase, balance, build, modifiers);

        // 런 시작 때 끼울 전투 기준값(부록 A-1·A-4: 도끼 10, 충격파 12, 부메랑 틱 4, 드론 레이저 7). 본편 캐릭터 프리팹은 그대로 둔다.
        RoguelikeSkillTuningSO tuning = null != skillController ? skillController.Tuning : null;
        effectApplier.Initialize(bAttackSpeedAffectsAxe, null != tuning ? tuning.spinRequiredSwings : 3);
        if (null != tuning)
        {
            effectApplier.SetRunBaseCombatStats(new StatComponent.CombatBaseStats
            {
                axeDamage = tuning.axeDamage,
                axeAttackCoolTime = tuning.axeAttackCoolTime,
                shockWaveDamage = tuning.shockwaveDamage,
                boomerangDamage = tuning.boomerangTickDamage,
                boomerangDamageInterval = tuning.boomerangTickInterval,
                boomerangCooldown = tuning.boomerangCooldown,
                boomerangMajorAxisRatio = tuning.boomerangRangeRatio,
                droneDamage = tuning.droneLaserDamage,
                droneDamageInterval = tuning.droneLaserInterval,
            });
            effectApplier.SetRepeatDelays(tuning.shockwaveExtraDelay, tuning.axeExtraStrikeDelay, tuning.shockwaveExtraDamageRatio);
            effectApplier.SetSporeShockwaveColor(tuning.sporeShockwaveColor);
            effectApplier.SetDashRechargeSeconds(tuning.dashRechargeSeconds);
        }

        effectApplier.SetMoveSpeedCap(balance.maxMoveSpeedPercent);
        productionGate.Initialize(GetFallbackSeconds, maxProductionWaitSeconds);
        tileWave.Initialize(environmentSystem, tileWaveSettings);
        waterDeco.Initialize(environmentSystem, waterDecoSettings, transform);
        cloudFade.Initialize(environmentSystem, transform);

        // 이펙트 담당이 이 프리팹 아래에 붙여 둔 연출 핸들러를 모은다.
        GetComponentsInChildren(true, handlerBuffer);
        for (int i = 0; i < handlerBuffer.Count; i++) productionGate.Register(handlerBuffer[i]);
        handlerBuffer.Clear();

        if (null != waveStrategyTemplate)
        {
            waveStrategy = Instantiate(waveStrategyTemplate);
        }

        inDungeonObjectManager.SetRunTreeRules(treeRules);

        if (null != skillController)
        {
            skillController.Initialize(signalHub, modifiers);
            skillController.SetVfxSources(inDungeonObjectManager);
        }

        CreateLobbyObjects();
        OpenViews();

        treeKilledHandler = OnTreeKilled;
        lobbyActionHandler = OnLobbyAction;

        BindEvents();
        SubscribeSignals();
    }

    /// <summary>씬 진입 직후(GameInstaller.SetupGameInstaller). 로비로 들어간다.</summary>
    public void StartSession()
    {
        townSystem.PrepareForRoguelike();

        TierFieldData lobbyField = GetTierField(0);
        signalHub.Publish(new DecalreDungeonTypeSignal(lobbyField.mapType, lobbyField.forestType));
        signalHub.Publish(new PopupUIDownSignal());

        // 메인메뉴가 디스토션 → 암전을 건 채 씬이 넘어온다(기획서 4-1 1~2단계). 암전 아래에서 섬을 깔고
        // EnterLobbyRoutine이 암전을 걷은 뒤 바닥 타일 파도 → 배경 조립 → 낙하를 잇는다.

        StartFlow(EnterLobbyRoutine(false));
    }

    public void Release()
    {
        StopAllCoroutines();
        flowCoroutine = null;
        tileWaveCoroutine = null;
        tileWave.FinishImmediately();
        productionGate.CancelAll();
        RoguelikeScreenTransition.ClearIfCovering();
        EndRunClearCinematic();

        // InputManager는 BootStrap 소속이라 메인메뉴까지 살아 있다. 로비 입장 도중 해제돼도 잠금이 남지 않게 푼다.
        inputManager?.SetESCKeyLock(LobbyEntryESCLockOwner, false);
        inputManager?.SetInventoryKeyLock(SessionInventoryLockOwner, false);

        UnSubscribeSignals();
        ReleaseEvents();
        uiCoordinator.Release();
        RoguelikeModalInput.Reset();

        effectApplier.RemoveAll();
        RunCombatOverrides.ResetAll();

        if (null != inDungeonObjectManager)
        {
            inDungeonObjectManager.SetRunTreeRules(null);
        }

        if (null != skillController) skillController.Release();
        RunCombatHooks.ClearAll();

        if (null != waveStrategy)
        {
            Destroy(waveStrategy);
            waveStrategy = null;
        }

        if (null != startRunObject) Destroy(startRunObject.gameObject);
        if (null != recordsObject) Destroy(recordsObject.gameObject);
    }

    /// <summary>캐릭터 생성(CharacterSpawnedSignal) 전에 만들어지므로 그 시그널로 받는다.</summary>
    private void CharacterSpawned(CharacterSpawnedSignal _signal)
    {
        character = _signal.character;
        effectApplier.SetCharacter(character);
        skillController?.SetCharacter(character);
    }

    // // 시그널·이벤트

    private void SubscribeSignals()
    {
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<CharacterStaminaIsEmptySignal>(CharacterStaminaIsEmpty);
        signalHub.Subscribe<RoguelikeCharacterDownCompletedSignal>(CharacterDownCompleted);
        signalHub.Subscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
        signalHub.Subscribe<RoguelikeCardBuyRequestedSignal>(CardBuyRequested);
        signalHub.Subscribe<RoguelikeCardRerollRequestedSignal>(CardRerollRequested);
        signalHub.Subscribe<RoguelikeCardSelectionPresentationDoneSignal>(CardSelectionPresentationDone);
        signalHub.Subscribe<RoguelikeResultConfirmedSignal>(ResultConfirmed);
        signalHub.Subscribe<RoguelikeLobbyActionSignal>(LobbyActionRequested);
    }

    private void UnSubscribeSignals()
    {
        if (null == signalHub) return;

        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<CharacterStaminaIsEmptySignal>(CharacterStaminaIsEmpty);
        signalHub.UnSubscribe<RoguelikeCharacterDownCompletedSignal>(CharacterDownCompleted);
        signalHub.UnSubscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
        signalHub.UnSubscribe<RoguelikeCardBuyRequestedSignal>(CardBuyRequested);
        signalHub.UnSubscribe<RoguelikeCardRerollRequestedSignal>(CardRerollRequested);
        signalHub.UnSubscribe<RoguelikeCardSelectionPresentationDoneSignal>(CardSelectionPresentationDone);
        signalHub.UnSubscribe<RoguelikeResultConfirmedSignal>(ResultConfirmed);
        signalHub.UnSubscribe<RoguelikeLobbyActionSignal>(LobbyActionRequested);
    }

    private void BindEvents()
    {
        if (null != waveStrategy)
        {
            waveStrategy.TreeKilledEvent -= treeKilledHandler;
            waveStrategy.TreeKilledEvent += treeKilledHandler;
        }

        if (null != startRunObject)
        {
            startRunObject.ActionTriggeredEvent -= lobbyActionHandler;
            startRunObject.ActionTriggeredEvent += lobbyActionHandler;
        }

        if (null != recordsObject)
        {
            recordsObject.ActionTriggeredEvent -= lobbyActionHandler;
            recordsObject.ActionTriggeredEvent += lobbyActionHandler;
        }
    }

    private void ReleaseEvents()
    {
        if (null != waveStrategy) waveStrategy.TreeKilledEvent -= treeKilledHandler;
        if (null != startRunObject) startRunObject.ActionTriggeredEvent -= lobbyActionHandler;
        if (null != recordsObject) recordsObject.ActionTriggeredEvent -= lobbyActionHandler;
    }

    private void CharacterDownCompleted(RoguelikeCharacterDownCompletedSignal _signal) => bCharacterDownCompleted = true;
    private void CardSelectionPresentationDone(RoguelikeCardSelectionPresentationDoneSignal _signal) => bCardPresentationDone = true;
    private void ResultConfirmed(RoguelikeResultConfirmedSignal _signal) => bResultConfirmed = true;
    private void LobbyActionRequested(RoguelikeLobbyActionSignal _signal) => OnLobbyAction(_signal.action);

    // // 카드 선택 요청

    private void CardBuyRequested(RoguelikeCardBuyRequestedSignal _signal)
    {
        if (ERunPhase.CardSelect != phase) return;

        ECardShopResult result = shop.TryBuy(_signal.offerIndex, out CardPurchaseResult purchase, discardedOfferIndices);
        if (ECardShopResult.Success != result)
        {
            signalHub.Publish(new RoguelikeCardShopRejectedSignal(result, _signal.offerIndex));
            return;
        }

        effectApplier.Apply(modifiers);
        signalHub.Publish(new RoguelikeModifiersChangedSignal());
        signalHub.Publish(new RoguelikeCardPurchasedSignal(purchase));
    }

    private void CardRerollRequested(RoguelikeCardRerollRequestedSignal _signal)
    {
        if (ERunPhase.CardSelect != phase) return;

        ECardShopResult result = shop.TryReroll();
        if (ECardShopResult.Success != result)
        {
            signalHub.Publish(new RoguelikeCardShopRejectedSignal(result, -1));
            return;
        }

        signalHub.Publish(new RoguelikeOffersChangedSignal());
    }

    // // 처치

    private void OnTreeKilled(TreeObj _tree, Vector3 _position)
    {
        if (false == bRunActive || ERunPhase.WaveCombat != phase) return;

        treesFelled++;

        // 스텔라 별 생성·포자 폭발 연쇄는 처치마다 판정한다(_tree는 디버그 클리어에서 null일 수 있다).
        if (null != _tree) skillController?.OnTreeKilled(_tree, _position);

        waveKills++;
        signalHub.Publish(new RoguelikeWaveProgressSignal(waveKills, currentWave.killTarget));

        if (waveKills < currentWave.killTarget) return;

        // 목표 달성 즉시: 스폰을 멈추고 남은 나무를 묘목으로 돌린다(이 순간부터 피해가 없다). 기획서 3-1 "달성 즉시".
        // 30웨이브는 마지막 나무 줌인·슬로우 → 줌아웃 뒤에 나무를 정리한다(기획서 3-5 "나무 정리") - WaveClearRoutine이 돌린다.
        waveStrategy?.StopSpawning();
        if (IsFinalWave(in currentWave))
        {
            finalTreePosition = _position;
            signalHub.Publish(new RoguelikeFinalTreeKilledSignal(_position));
        }
        else
        {
            RevertFieldTreesToSapling();
        }

        SetPhase(ERunPhase.WaveClear);
    }

    // 묘목 그림 보정 조회(웨이브 클리어마다 델리게이트를 새로 만들지 않도록 한 번만 만든다).
    private Func<TreeType, Sprite> saplingTopOverrideHandler;

    /// <summary>나무 종류의 보정 묘목 그림. 보정이 없으면 null(공용 데이터 그대로).</summary>
    private Sprite GetSaplingTopOverride(TreeType _treeType)
    {
        for (int i = 0; i < saplingTopOverrides.Count; i++)
        {
            if (saplingTopOverrides[i].treeType == _treeType) return saplingTopOverrides[i].saplingTop;
        }

        return null;
    }

    /// <summary>남은 나무가 묘목으로 돌아가 사라진다(기획서 3-1). 나무 회수는 연출이 끝난 뒤 WaveClearRoutine이 한다.</summary>
    private void RevertFieldTreesToSapling()
    {
        float seconds = Mathf.Max(0f, waveClearSaplingVanishSeconds);
        if (null == saplingTopOverrideHandler) saplingTopOverrideHandler = GetSaplingTopOverride;
        inDungeonObjectManager.RevertAllTreesToSaplingForRun(seconds, saplingTopOverrideHandler);
        saplingVanishEndTime = Time.time + seconds;

        // 본편 묘목 등장음. 나무마다 내면 수십 개가 겹치므로 섬 중앙에서 한 번만 낸다.
        if (inDungeonObjectManager.ActiveTrees.Count > 0 && null != cameraAnchor) Sound.Play(SoundID.TreeSmallGrow, cameraAnchor.position);
    }

    /// <summary>런 클리어 슬로우를 풀고 ESC 잠금을 되돌린다(연출이 끝났거나 흐름이 끊겼을 때).</summary>
    private void EndRunClearCinematic()
    {
        if (bRunClearSlowMo)
        {
            Time.timeScale = 1f;
            bRunClearSlowMo = false;
        }

        inputManager?.SetESCKeyLock(RunClearESCLockOwner, false);
    }

    // // 게임오버·포기·메인메뉴

    private void CharacterStaminaIsEmpty(CharacterStaminaIsEmptySignal _signal)
    {
        if (false == bRunActive || bRunEnding) return;

        bRunEnding = true;
        bCharacterDownCompleted = false;

        // 쓰러짐은 캐릭터가 직접 이동·ESC를 잠근다. 그 사이 열려 있던 모달(보유 카드)이 닫혀도 이동이 풀리지 않게
        // "조작 잠금" 상태만 기록한다(입력은 건드리지 않는다).
        RoguelikeModalInput.SetGameplayControl(false, null);
        StopRunActivity();
        SetPhase(ERunPhase.GameOver);
        StartFlow(GameOverRoutine());
    }

    private void GoToMainMenuRequested(GoToMainMenuRequestedSignal _signal)
    {
        if (bExitingToMainMenu) return;

        if (ERunPhase.Lobby == phase)
        {
            StartFlow(ExitToMainMenuRoutine());
            return;
        }

        // 런 도중 ESC → 포기(기획서 3-7 "런 중도 나가기"). 결과 화면을 거쳐 로비로 간다.
        if (bRunActive && false == bRunEnding)
        {
            bRunEnding = true;
            StopRunActivity();
            DisableControl();
            StartFlow(FinishRunRoutine(ERunEndReason.Abandoned));
        }
    }

    /// <summary>진행 중인 흐름·스폰·연출 대기를 모두 끊는다.</summary>
    private void StopRunActivity()
    {
        StopFlow();
        StopTileWave();
        productionGate.CancelAll();
        EndRunClearCinematic();
        waveStrategy?.StopSpawning();
        skillController?.EndCombat();
        pressure.Reset();

        if (shop.IsOpen)
        {
            shop.Close();
            signalHub.Publish(new RoguelikeCardSelectionSkippedSignal());
        }
    }

    // // 로비

    private void OnLobbyAction(ERoguelikeLobbyAction _action)
    {
        if (ERunPhase.Lobby != phase) return;

        switch (_action)
        {
            case ERoguelikeLobbyAction.StartRun:
                StartFlow(RunRoutine());
                break;
            case ERoguelikeLobbyAction.OpenRecords:
                signalHub.Publish(new RoguelikeRecordsRequestedSignal());
                break;
        }
    }

    private void CreateLobbyObjects()
    {
        int layer = GetFirstLayer(lobbyObjectLayer);

        if (null != startRunObjectPrefab)
        {
            startRunObject = Instantiate(startRunObjectPrefab, transform);
            startRunObject.Initialize(layer);
            startRunObject.Remove();
        }

        if (null != recordsObjectPrefab)
        {
            recordsObject = Instantiate(recordsObjectPrefab, transform);
            recordsObject.Initialize(layer);
            recordsObject.Remove();
        }
    }

    private void PlaceLobbyObjects(Vector3 _center)
    {
        startRunObject?.Place(_center + (Vector3)startRunObjectOffset);
        recordsObject?.Place(_center + (Vector3)recordsObjectOffset);
    }

    private void RemoveLobbyObjects()
    {
        startRunObject?.Remove();
        recordsObject?.Remove();
    }

    private static int GetFirstLayer(LayerMask _mask)
    {
        int value = _mask.value;
        for (int i = 0; i < 32; i++)
        {
            if (0 != (value & (1 << i))) return i;
        }

        return 0;
    }

    // // UI

    private void OpenViews()
    {
        UIView_RoguelikeHUD hudUI = gameplayUIInstaller.OpenAdditionalView<UIView_RoguelikeHUD>();
        hudUI?.SetCombatVisible(false);

        UIView_RoguelikeCardSelect cardSelectUI = gameplayUIInstaller.OpenAdditionalView<UIView_RoguelikeCardSelect>();
        cardSelectUI?.Hide();

        UIView_RoguelikeOwnedCards ownedCardsUI = gameplayUIInstaller.OpenAdditionalView<UIView_RoguelikeOwnedCards>();
        ownedCardsUI?.Hide();

        UIView_RoguelikeResult resultUI = gameplayUIInstaller.OpenAdditionalView<UIView_RoguelikeResult>();
        resultUI?.Hide();

        UIView_RoguelikeRecords recordsUI = gameplayUIInstaller.OpenAdditionalView<UIView_RoguelikeRecords>();
        recordsUI?.Hide();

        uiCoordinator.Initialize(signalHub, this, hudUI, cardSelectUI, ownedCardsUI, resultUI, recordsUI, gameplayUIInstaller.GetOpenedView<UIView_ESC>(),
            gameplayUIInstaller.GetOpenedView<UIView_Popup>(), gameplayUIInstaller.GetOpenedView<UIView_HUD>());
    }

    // // 공통

    private void SetPhase(ERunPhase _phase)
    {
        if (phase == _phase) return;

        ERunPhase prev = phase;
        phase = _phase;

        // 카드 선택은 시간이 흐르지 않는 단계라 대시 쿨타임도 멈춘다(timeScale은 그대로라 직접 막는다).
        character?.SetDashRechargePaused(ERunPhase.CardSelect == _phase);

        signalHub.Publish(new RoguelikePhaseChangedSignal(_phase, prev));
    }

    private TierFieldData GetTierField(int _tierIndex)
    {
        if (null == tierFields || 0 == tierFields.Count)
        {
            return new TierFieldData { mapType = MapType.WideGreenForest, forestType = ForestType.WideGreenForest_1 };
        }

        return tierFields[Mathf.Clamp(_tierIndex, 0, tierFields.Count - 1)];
    }

    private bool IsFinalWave(in WaveSpec _spec)
    {
        return false == _spec.bUnlimit && null != balance && _spec.waveNumber == balance.clearWave;
    }

    private float GetFallbackSeconds(ERoguelikeProduction _type)
    {
        switch (_type)
        {
            case ERoguelikeProduction.LobbyEntry: return fallbackLobbyEntrySeconds;
            case ERoguelikeProduction.TierTransition: return fallbackTierTransitionSeconds;
            case ERoguelikeProduction.WaveClear: return fallbackWaveClearSeconds;
            case ERoguelikeProduction.RunClear: return fallbackRunClearSeconds;
            case ERoguelikeProduction.ResultToLobby: return fallbackResultToLobbySeconds;
            case ERoguelikeProduction.LobbyToRun: return fallbackLobbyToRunSeconds;
            default: return 0f;
        }
    }

    private void StartFlow(IEnumerator _routine)
    {
        StopFlow();
        flowCoroutine = StartCoroutine(_routine);
    }

    private void StopFlow()
    {
        if (null == flowCoroutine) return;

        StopCoroutine(flowCoroutine);
        flowCoroutine = null;
    }

    // 에디터 전용 디버그 키 처리(RoguelikeSystem.Debug.cs). 빌드에서는 구현이 없어 호출째 빠진다.
    partial void DebugTick();

    // // 유니티 이벤트 함수

    private void Update()
    {
        // 플레이 시간: 카드 선택(시간이 흐르지 않는 단계)·결과·로비는 세지 않는다. ESC 일시정지는 timeScale 0이라 자연히 멈춘다.
        if (bRunActive && ERunPhase.CardSelect != phase && ERunPhase.Result != phase)
        {
            playTimeSeconds += Time.deltaTime;
        }

        uiCoordinator.Tick();
        waterDeco.Tick(tileWave, Time.deltaTime);
        cloudFade.Tick(tileWave, Time.deltaTime);

        DebugTick();
    }
}
