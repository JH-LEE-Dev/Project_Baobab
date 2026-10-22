using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크에만 있는 액티브 스킬과 조합 효과. 기획서 v2.5 6-1 "액티브 7종"·6-2 "조합(프리즘) 7종".
///
///   본편 스킬 재사용(RunEffectApplier가 스탯으로 켬): 회전베기, 충격파, 부메랑, 드론
///   여기서 구현하는 신규 스킬: 오프로드, 스텔라, 포자폭탄
///   여기서 구현하는 조합: 회전베기+충격파(360도 충격파), 부메랑+충격파, 회전베기+부메랑, 스텔라+드론,
///                       포자 수류탄+드론(포자 레이저) · +오프로드(방구차) · +충격파(포자 충격파)
///
/// 판정·피해와 움직임·손맛(오프로드 예열·가속·충돌 반동, 카메라 흔들림, 진동, 효과음)은 여기서 맡는다.
/// 먼지·잔상 같은 이펙트는 이펙트 담당이 RoguelikeSkillVfxSignal(및 별 프리팹)로 입힌다.
/// 웨이브 전투 중에만 돈다(BeginCombat ~ EndCombat). 웨이브가 끝나면 별·오염·표식을 모두 치운다.
///
/// 카드 수치 적용 규칙(기획서 3-3 공통 패시브):
///   피해 = 기본 × (1 + 피해%(전역+스킬)/100), 도트 = 기본 × (1 + 피해%/100) × (1 + 도트%/100)
///   범위·지속 = 기본 × (1 + %/100), 주기 = 기본 ÷ (1 + 공격속도%/100) × (1 - 재사용 대기 감소%/100)
///   발동 수 = 기본 + 발동 수(전역+스킬)
/// 기획서 미정 항목(공격력과 도트의 중복 적용, 공격속도의 적용 범위)은 위처럼 "모두 적용"으로 두었다.
/// </summary>
public class RoguelikeSkillController : MonoBehaviour
{
    private struct MarkedTree
    {
        public TreeObj tree;
        public float expireTime;
    }

    /// <summary>포자 대폭발에 맞아 오염된 나무(기획서 v2.5 6-1 포자 수류탄). 오염이 유지되는 동안 틱마다 지속 피해를 받는다.</summary>
    private struct Contamination
    {
        public TreeObj tree;
        // 오염된 순간의 재사용 번호. 쓰러져 다른 자리의 새 나무로 다시 쓰인 객체는 번호가 달라 오염이 이어지지 않는다.
        public int reuseCount;
        public float expireTime;
        public int stacks;
        // 다음 틱 시각. 틱 주기가 나무마다 다를 수 있어(지속 피해 속도 증가 MAX: 중첩 최대인 나무만 더 빠름) 나무마다 따로 잰다.
        public float nextTickTime;
    }

    /// <summary>포자 조합 3종의 대폭발 한 번(같은 자리 재발동 제한에 쓴다).</summary>
    private struct SporeComboBlast
    {
        public Vector3 position;
        public float time;
    }

    /// <summary>
    /// 던진 포자 폭탄. 떨어지면(landTime) 대폭발 연출이 시작되고, blastTime에 범위 안 나무를 오염시킨다.
    /// 포자 조합 3종의 대폭발도 던지지 않고 그 자리에 바로 떨어진 폭탄으로 여기에 들어온다.
    /// </summary>
    private struct SporeBomb
    {
        public Vector3 position;
        public float radius;
        public float landTime;
        public float blastTime;
        public bool bLanded;
    }

    /// <summary>돌진 중인 오프로드 한 대. 차마다 맞힌 나무를 따로 세어, 여러 대가 같은 나무를 각각 칠 수 있다.</summary>
    private sealed class OffroadCar
    {
        public RoguelikeOffroadVisual visual;
        // 차선(진행 방향 · 수직 오프셋). 같은 차선에 다른 차를 겹쳐 소환하지 않는 데 쓴다.
        public bool bRight;
        public float laneOffset;
        public Vector3 from;
        public Vector3 islandExit;
        public Vector2 direction;
        public float length;
        public float traveled;
        public float speed;
        public float maxSpeed;
        public float dashStartTime;
        public bool bDashStarted;
        public float hitStopUntil;
        public bool bExiting;
        public float exitEndTime;
        public int exitStartFrame;
        public Vector3 lastPosition;
        public float damage;
        public float radius;
        public bool bEngine;
        // 방구차(포자 수류탄 + 오프로드): 마지막 대폭발 뒤 달린 거리.
        public float sporeTrail;
        public AudioHandle engine = AudioHandle.Invalid;
        // 맞힌 나무 → 맞힌 순간의 재사용 번호. 쓰러져 다른 칸의 새 나무로 다시 쓰인 객체는 번호가 달라 다시 칠 수 있다.
        public readonly Dictionary<TreeObj, int> hitTrees = new Dictionary<TreeObj, int>();

        public Vector3 Position => from + (Vector3)(direction * traveled);
    }

    /// <summary>오프로드 소환 후보 차선 하나. 진입점은 섬 테두리에서 진행 방향으로 들여온 소환 위치다.</summary>
    private struct OffroadLane
    {
        public bool bRight;
        public float offset;
        public Vector2 entry;
        public Vector2 exit;
        public float inset;
    }

    [SerializeField] private RoguelikeSkillTuningSO tuning;

    public RoguelikeSkillTuningSO Tuning => tuning;

    private InDungeonObjectManager objectManager;
    // 별 표식 나무의 받는 피해 증가율 중 이 세션이 본편 누산 값에 더해 둔 양(0.3 = +30%).
    private float appliedMarkDamageBonus;
    [SerializeField] private RoguelikeStar starPrefab;
    [Tooltip("오프로드 외형(선택). 비어 있으면 판정만 움직인다.")]
    [SerializeField] private GameObject offroadVisualPrefab;
    [SerializeField] private LayerMask treeLayer;
    [Tooltip("본편 연출을 재생할 때 코드에서 꺼낼 수 없는 에셋(회전베기 프레임, 포자 폭탄 그림).")]
    [SerializeField] private RoguelikeSkillVfxAssets vfxAssets = new RoguelikeSkillVfxAssets();

    private readonly RoguelikeSkillVfxPlayer vfxPlayer = new RoguelikeSkillVfxPlayer();

    // // 외부 의존성
    private SignalHub signalHub;
    private RunModifiers modifiers;
    private Character character;
    private AxeExtraAttackCreator axeExtraAttackCreator;

    // // 상태
    private bool bActive;
    private Vector3 fieldCenter;
    private float fieldRadiusX = 8f;
    private float fieldRadiusY = 4.5f;

    private readonly List<IStaticCollidable> scanBuffer = new List<IStaticCollidable>(128);
    // 부메랑+충격파: 이번 프레임에 이미 충격파를 낸 자리(가까운 자리는 하나로 합친다).
    private readonly List<Vector3> boomerangShockwaveFired = new List<Vector3>(8);
    private readonly List<TreeObj> hitBuffer = new List<TreeObj>(64);

    private readonly List<RoguelikeStar> activeStars = new List<RoguelikeStar>(16);
    private readonly Stack<RoguelikeStar> starPool = new Stack<RoguelikeStar>(16);
    private readonly List<RoguelikeStar> pendingStarHits = new List<RoguelikeStar>(4);
    private int nextStarId = 1;

    private readonly List<MarkedTree> markedTrees = new List<MarkedTree>(128);
    private readonly List<Contamination> contaminations = new List<Contamination>(64);
    private readonly List<SporeBomb> sporeBombs = new List<SporeBomb>(8);
    private readonly List<OffroadCar> offroadCars = new List<OffroadCar>(4);
    private readonly Stack<OffroadCar> offroadCarPool = new Stack<OffroadCar>(4);
    private readonly Stack<RoguelikeOffroadVisual> offroadVisualPool = new Stack<RoguelikeOffroadVisual>(4);
    private readonly List<OffroadLane> offroadLaneCandidates = new List<OffroadLane>(32);
    private readonly List<OffroadLane> offroadLanePicks = new List<OffroadLane>(8);
    private float nextOffroadHitShakeTime;

    private readonly List<Vector3> pendingBoomerangHits = new List<Vector3>(16);
    // 포자 조합 3종이 예약한 대폭발 자리. 본편 판정 도중에 쌓이므로 Update(UpdateSporeComboExplosions)에서 일으킨다.
    private readonly List<Vector3> pendingSporeExplosions = new List<Vector3>(32);
    private readonly List<SporeComboBlast> recentSporeExplosions = new List<SporeComboBlast>(64);
    private int pendingAxeSwings;
    private int spinShockwaveSwingCount;

    // 스텔라+드론 이음선(별 인덱스 쌍). 별이 바뀔 때마다 최소 신장 트리로 다시 잇는다 - 더 가까운 새 별이 생기면
    // 기존 긴 이음선이 끊기고 새 별로 이어지는 규칙(기획서 "이음선 재구성")이 그대로 성립한다.
    private readonly List<Vector2Int> starLinks = new List<Vector2Int>(16);
    // 빔을 쏘는 동안 쓰는 이음선 사본. 빔에 나무가 죽으면 그 자리에 별이 생기며 starLinks가 다시 만들어지므로(TrySpawnStar) 원본을 돌면 안 된다.
    private readonly List<Vector2Int> beamLinkBuffer = new List<Vector2Int>(16);
    private readonly List<bool> mstInTree = new List<bool>(16);
    private readonly List<float> mstBestDist = new List<float>(16);
    private readonly List<int> mstBestFrom = new List<int>(16);

    private float nextOffroadTime;
    private float nextSporeTime;
    private float nextBeamTime;
    private float nextLonePulseTime;
    private float nextBoomerangShockwaveTime;
    private float nextSpinBoomerangTime;

    private System.Action<Vector3> axeSwungHandler;
    private System.Action<Vector3> boomerangHitHandler;
    private System.Action<RoguelikeStar> starHitHandler;
    private System.Action<IReadOnlyList<Vector3>, IReadOnlyList<Vector3>> droneLaserHandler;
    private System.Action<Vector3> shockWaveHitHandler;

    // 스텔라 MAX "별을 칠 때 폭발이 2회 연속": 첫 폭발 뒤 이어질 폭발(위치, 시각, 별 번호).
    private struct RepeatBlast
    {
        public Vector3 position;
        public float time;
        public int starId;
    }

    private readonly List<RepeatBlast> pendingRepeatBlasts = new List<RepeatBlast>(8);

    // 이번 웨이브의 별 폭발 1회 피해 상한(나무 최대 HP 대비). 0이면 상한 없음(4티어 미만).
    private float starBlastHpCapRatio;

    /// <summary>스텔라+드론 이음선. 이펙트 담당이 선을 그릴 때 읽는다(StarLinksChanged 시그널 뒤).</summary>
    public IReadOnlyList<Vector2Int> StarLinks => starLinks;
    public IReadOnlyList<RoguelikeStar> ActiveStars => activeStars;

    public void Initialize(SignalHub _signalHub, RunModifiers _modifiers)
    {
        signalHub = _signalHub;
        modifiers = _modifiers;

        axeSwungHandler = OnAxeSwung;
        boomerangHitHandler = OnBoomerangHitTree;
        starHitHandler = OnStarHit;

        RunCombatHooks.AxeSwungEvent -= axeSwungHandler;
        RunCombatHooks.AxeSwungEvent += axeSwungHandler;
        RunCombatHooks.BoomerangHitTreeEvent -= boomerangHitHandler;
        RunCombatHooks.BoomerangHitTreeEvent += boomerangHitHandler;

        droneLaserHandler = OnDroneLaser;
        RunCombatHooks.DroneLaserEvent -= droneLaserHandler;
        RunCombatHooks.DroneLaserEvent += droneLaserHandler;

        shockWaveHitHandler = OnShockWaveHitTree;
        RunCombatHooks.ShockWaveHitTreeEvent -= shockWaveHitHandler;
        RunCombatHooks.ShockWaveHitTreeEvent += shockWaveHitHandler;
    }

    public void Release()
    {
        RunCombatHooks.AxeSwungEvent -= axeSwungHandler;
        RunCombatHooks.BoomerangHitTreeEvent -= boomerangHitHandler;
        RunCombatHooks.DroneLaserEvent -= droneLaserHandler;
        RunCombatHooks.ShockWaveHitTreeEvent -= shockWaveHitHandler;
        EndCombat();
        SetMarkDamageBonus(0f);
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
        axeExtraAttackCreator = null != _character ? _character.GetComponentInChildren<AxeExtraAttackCreator>(true) : null;
    }

    /// <summary>본편 연출(별 표식·별똥별 폭발·포자 폭발·별자리 레이저·노바)을 꺼낼 곳. 던전 오브젝트 매니저 아래에 있다.</summary>
    public void SetVfxSources(InDungeonObjectManager _objectManager)
    {
        vfxPlayer.Initialize(_objectManager, vfxAssets, this);

        SetMarkDamageBonus(0f);
        objectManager = _objectManager;
        SyncMarkDamageBonus();
    }

    /// <summary>
    /// 별 표식 나무의 받는 피해 증가(기획서 v2.4 6-1: 기본 +30%, 스텔라 V부터 +45%). 표식 나무가 받는 모든 피해(도끼·다른 액티브·지속 피해)는
    /// TreeObj의 별표식 배율 한 곳을 지나므로, 그 배율(본편 "별표식 베기" 특성이 쓰는 누산 값, 기본 1)에 세션 동안만 더한다.
    /// 로그라이크는 본편 능력 트리를 불러오지 않아 이 값은 세션 내내 1 + 보너스이고, Release에서 뺀다.
    /// 카드는 웨이브 사이에만 바뀌므로 웨이브 전투가 시작될 때(BeginCombat) 지금 빌드 값으로 맞춘다.
    /// </summary>
    private void SyncMarkDamageBonus()
    {
        if (null == tuning || null == modifiers) return;
        SetMarkDamageBonus(tuning.stellaMarkDamageTakenBonus + modifiers.GetForSkill(ERunEffect.StarMarkDamageTakenAdd, ERoguelikeSkill.Stella) / 100f);
    }

    private void SetMarkDamageBonus(float _bonus)
    {
        if (null == objectManager) return;

        float delta = _bonus - appliedMarkDamageBonus;
        if (Mathf.Approximately(delta, 0f)) return;

        objectManager.IncreaseStarMarkDamage(delta);
        appliedMarkDamageBonus = _bonus;
    }

    /// <summary>섬(타원)의 중심과 반지름(월드). 오프로드가 섬 가장자리에서 반대편 가장자리로 돌진하는 데 쓴다.</summary>
    public void SetField(Vector3 _center, float _radiusX, float _radiusY)
    {
        fieldCenter = _center;
        fieldRadiusX = Mathf.Max(1f, _radiusX);
        fieldRadiusY = Mathf.Max(1f, _radiusY);
    }

    /// <summary>웨이브 전투 시작. _tierIndex(0부터)는 별 폭발 피해 상한(4티어부터)을 거는 데 쓴다.</summary>
    public void BeginCombat(int _tierIndex)
    {
        bActive = true;
        starBlastHpCapRatio = _tierIndex >= tuning.stellaBlastCapFromTier ? Mathf.Max(0f, tuning.stellaBlastMaxHpRatio) : 0f;
        float now = Time.time;
        nextOffroadTime = now + Interval(tuning.offroadInterval, ERoguelikeSkill.Offroad);
        nextSporeTime = now + Interval(tuning.sporeInterval, ERoguelikeSkill.SporeBomb) * 0.5f;
        nextBeamTime = now + tuning.stellaDroneBeamInterval;
        nextLonePulseTime = now + tuning.stellaDroneLoneStarInterval;
        SyncMarkDamageBonus();
        pendingRepeatBlasts.Clear();
        pendingAxeSwings = 0;
        pendingBoomerangHits.Clear();
        pendingSporeExplosions.Clear();
        recentSporeExplosions.Clear();
    }

    /// <summary>
    /// 웨이브 종료(_bWaveEnd) 또는 런 종료. 별·오염·표식·돌진을 모두 치운다.
    /// 단, 웨이브 종료에서 스텔라+드론이 켜져 있으면 별과 이음선은 남긴다 - 기획서 "별들은 노바가 발동될 때만 모두 소멸한다",
    /// 홀로 남은 별도 "사라지지도 않는다". 다음 웨이브에서 이어서 모은다. 런이 끝나면(_bWaveEnd = false) 전부 치운다.
    /// </summary>
    public void EndCombat(bool _bWaveEnd = false)
    {
        bActive = false;

        bool bKeepStars = _bWaveEnd && modifiers.IsComboActive(ERoguelikeCombo.StellaDrone);
        if (false == bKeepStars)
        {
            for (int i = activeStars.Count - 1; i >= 0; i--) DespawnStar(i, false);
            starLinks.Clear();
        }

        for (int i = 0; i < markedTrees.Count; i++)
        {
            TreeObj tree = markedTrees[i].tree;
            if (null != tree && false == tree.IsPooled) tree.SetStarMarked(false);
        }

        markedTrees.Clear();
        ClearContaminations();
        sporeBombs.Clear();
        for (int i = offroadCars.Count - 1; i >= 0; i--) ReleaseOffroadCar(i, true);

        pendingStarHits.Clear();
        pendingRepeatBlasts.Clear();
        pendingBoomerangHits.Clear();
        pendingSporeExplosions.Clear();
        recentSporeExplosions.Clear();
        pendingAxeSwings = 0;
        spinShockwaveSwingCount = 0;
        vfxPlayer.StopCombatEffects();
        if (false == bKeepStars) vfxPlayer.Clear();
    }

    /// <summary>
    /// 본편 나무 정리(웨이브 클리어·티어 전환의 ClearTreesForRun)가 땅 별·점선 그림까지 거둔 뒤 부른다.
    /// 웨이브를 넘어 남아 있는 별(스텔라+드론)이 있으면 그 그림을 다시 그린다.
    /// </summary>
    public void RestoreKeptStarVisuals()
    {
        if (0 == activeStars.Count) return;
        vfxPlayer.RestoreStars(activeStars, starLinks);
    }

    /// <summary>
    /// 지형을 새로 깐 뒤(티어 전환) 남겨 둔 별이 새 지형의 육지 위에 있는지 확인하고, 아닌 별은 조용히 거둔다(충돌 등록도 함께 풀린다).
    /// 섬은 티어마다 같은 모양이라 보통은 아무 일도 없다. 그림은 이미 나무 정리가 거뒀으므로 소멸 연출은 내지 않는다.
    /// </summary>
    public void DropKeptStarsOffLand(System.Func<Vector3, bool> _isLand)
    {
        if (null == _isLand || 0 == activeStars.Count) return;

        bool bRemoved = false;
        for (int i = activeStars.Count - 1; i >= 0; i--)
        {
            if (_isLand(activeStars[i].transform.position)) continue;
            DespawnStar(i, false);
            bRemoved = true;
        }

        if (bRemoved) RebuildStarLinks();
    }

    /// <summary>웨이브 전투 중 나무가 쓰러졌다(RoguelikeSystem이 알려 준다).</summary>
    public void OnTreeKilled(TreeObj _tree, Vector3 _position)
    {
        if (false == bActive) return;

        // 표식 목록에서 뺀다. 이 나무는 곧 풀로 돌아가 다른 나무로 재사용된다(오염은 재사용 번호로 걸러진다).
        for (int i = markedTrees.Count - 1; i >= 0; i--)
        {
            if (markedTrees[i].tree == _tree) markedTrees.RemoveAt(i);
        }

        TrySpawnStar(_position);
    }

    // // 유니티

    private void Update()
    {
        if (false == bActive || null == tuning || null == character || character.bDead) return;

        float now = Time.time;

        UpdateCombos(now);
        UpdateStars(now);
        UpdateMarks(now);
        UpdateSpore(now);
        UpdateOffroad(now);
        UpdateStellaDrone(now);
        UpdateSporeComboExplosions(now);

        // 드론은 본편과 달리 도끼질을 하지 않아도 발사 주기마다 자동으로 쏜다(기획서 v2.5 6-1). 발사 · 체인 판정은 본편 드론 그대로다.
        if (modifiers.IsSkillActive(ERoguelikeSkill.Drone)) character.KeepDronesActiveForRun();
    }

    // // 조합: 회전베기+충격파 / 부메랑+충격파 / 회전베기+부메랑

    private void OnAxeSwung(Vector3 _center)
    {
        if (bActive) pendingAxeSwings++;
    }

    private void OnBoomerangHitTree(Vector3 _position)
    {
        if (bActive && pendingBoomerangHits.Count < 64) pendingBoomerangHits.Add(_position);
    }

    private void UpdateCombos(float _now)
    {
        // 회전베기 + 충격파: 회전베기·충격파 기본 능력은 꺼지고, n번 휘두를 때마다 360도 충격파.
        if (modifiers.IsComboActive(ERoguelikeCombo.SpinShockwave))
        {
            int interval = Mathf.Max(1, tuning.spinShockwaveSwingInterval + Mathf.RoundToInt(modifiers.GetForSkill(ERunEffect.TriggerHitCountAdd, ERoguelikeSkill.Spin)));
            for (int i = 0; i < pendingAxeSwings; i++)
            {
                spinShockwaveSwingCount++;
                if (spinShockwaveSwingCount < interval) continue;

                spinShockwaveSwingCount = 0;
                Fire360ShockWave(character.transform.position);
            }
        }
        pendingAxeSwings = 0;

        if (pendingBoomerangHits.Count > 0)
        {
            bool bBoomerangShockwave = modifiers.IsComboActive(ERoguelikeCombo.BoomerangShockwave);
            bool bSpinBoomerang = modifiers.IsComboActive(ERoguelikeCombo.SpinBoomerang);
            bool bSpinStruckThisFrame = false;
            float mergeSqr = Mathf.Max(0f, tuning.boomerangShockwaveMergeRadius);
            mergeSqr *= mergeSqr;
            boomerangShockwaveFired.Clear();

            for (int i = 0; i < pendingBoomerangHits.Count; i++)
            {
                Vector3 hit = pendingBoomerangHits[i];

                // 부메랑 + 충격파: 맞은 나무 주변으로 충격파가 추가로 터진다(부메랑·충격파 기본 능력은 유지).
                // 같은 프레임에 거의 같은 자리(붙어 선 나무를 한 번에 꿰뚫은 경우)면 하나로 합친다 - 같은 자리 충격파가 겹쳐 쌓이지 않게.
                if (bBoomerangShockwave && _now >= nextBoomerangShockwaveTime && false == IsNearAny(boomerangShockwaveFired, hit, mergeSqr))
                {
                    nextBoomerangShockwaveTime = _now + tuning.boomerangShockwaveCooldown;
                    boomerangShockwaveFired.Add(hit);
                    Fire360ShockWave(hit);
                }

                // 회전베기 + 부메랑: 부메랑 타격 1회당 회전베기 1회(회전베기의 도끼 n타 발동은 꺼짐).
                // 회전베기는 캐릭터 중심이라 한 프레임의 여러 타격은 같은 공격이 겹칠 뿐이다 - 프레임당 한 번만 친다.
                if (bSpinBoomerang && false == bSpinStruckThisFrame && _now >= nextSpinBoomerangTime)
                {
                    nextSpinBoomerangTime = _now + tuning.spinBoomerangCooldown;
                    bSpinStruckThisFrame = true;
                    SpinStrikeAroundCharacter();
                }
            }

            pendingBoomerangHits.Clear();
        }
    }

    private static bool IsNearAny(List<Vector3> _points, Vector3 _position, float _sqrDistance)
    {
        for (int i = 0; i < _points.Count; i++)
        {
            if ((_points[i] - _position).sqrMagnitude <= _sqrDistance) return true;
        }

        return false;
    }

    private void Fire360ShockWave(Vector3 _position)
    {
        if (null == axeExtraAttackCreator) return;

        ShockWave sw = axeExtraAttackCreator.CreateShockWave(_position);
        if (null == sw) return;

        sw.SetDirection(Vector3.right);
        // 부채꼴 반각 180도 = 전 범위. 다음 생성 때 Reset이 기본 각도로 되돌린다.
        sw.angle = 180f;
        axeExtraAttackCreator.PlayShockWaveVisual(sw);

        Publish(ERoguelikeSkillVfx.Shockwave360, _position, _position, 0f, sw.LifeTime, 0);
    }

    private void SpinStrikeAroundCharacter()
    {
        StatComponent stat = character.statComponent;
        Vector3 center = character.transform.position;
        // 회전베기 카드의 범위·회전 피해 증가는 회전베기 타격에만 붙는다(RunCombatOverrides). 이 회전베기도 같이 받는다.
        float radius = tuning.spinBoomerangRadius * stat.axeAttackRangeMultiplier * RunCombatOverrides.WhirlwindRangeMultiplier;
        float damage = stat.axeDamage * tuning.spinBoomerangDamageMultiplier * RunCombatOverrides.WhirlwindDamageMultiplier;

        DamageTreesInRadius(center, radius, damage, false);
        if (RunEffectApplier.IsStarHitByActives(modifiers)) HitStarsInRadius(center, radius);
        Publish(ERoguelikeSkillVfx.SpinBoomerangStrike, center, center, radius, 0f, 0);
    }

    /// <summary>스텔라 III: 반경 안의 별을 친다(회전베기+부메랑의 회전베기 등 여기서 판정하는 공격).</summary>
    private void HitStarsInRadius(Vector3 _center, float _radius)
    {
        for (int i = 0; i < activeStars.Count; i++)
        {
            RoguelikeStar star = activeStars[i];
            if (false == star.bCanBeHitBySkill) continue;

            float reach = _radius + star.Radius;
            if (((Vector2)_center - (star.Position + star.Offset)).sqrMagnitude <= reach * reach) OnStarHit(star);
        }
    }

    // // 스텔라

    private void TrySpawnStar(Vector3 _position)
    {
        if (null == starPrefab || false == modifiers.IsSkillUnlocked(ERoguelikeSkill.Stella)) return;

        int maxStars = MaxStars();
        if (activeStars.Count >= maxStars) return;

        float chance = tuning.stellaBaseChance + modifiers.GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Stella);
        if (Random.value * 100f >= chance) return;

        bool bStellaDrone = modifiers.IsComboActive(ERoguelikeCombo.StellaDrone);

        RoguelikeStar star = starPool.Count > 0 ? starPool.Pop() : CreateStar();
        star.Place(nextStarId++, _position, GetFirstLayer(treeLayer), false == bStellaDrone);
        // 본편 땅 별(별 표식 나무에서 나오는 별)을 그릴 수 있으면 로그라이크 별 자체 그림은 숨긴다. 판정은 그대로다.
        star.SetVisualVisible(false == vfxPlayer.bUsesMainStarVisual);
        star.ExpireTime = Time.time + tuning.stellaStarLifetime * DurationMul(ERoguelikeSkill.Stella);
        activeStars.Add(star);

        // 범위 링(폭발 반경)과 유지시간 게이지(남은 수명)에 쓸 값. 스텔라+드론의 별은 쳐도 터지지 않고 노바 때까지
        // 사라지지 않으므로 둘 다 0으로 보낸다(링·게이지를 붙이지 않는다).
        float blastRadius = bStellaDrone ? 0f : tuning.stellaBlastRadius * RangeMul(ERoguelikeSkill.Stella);
        float lifetime = bStellaDrone ? 0f : star.ExpireTime - Time.time;
        Publish(ERoguelikeSkillVfx.StarSpawned, _position, _position, blastRadius, lifetime, star.StarId);

        if (bStellaDrone) RebuildStarLinks();
    }

    private int MaxStars()
    {
        int max = tuning.stellaBaseMaxStars + Mathf.RoundToInt(modifiers.GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Stella));

        // 스텔라+드론은 노바 조건(별 9개)까지 모여야 하므로 상한을 그 수 이상으로 연다.
        if (modifiers.IsComboActive(ERoguelikeCombo.StellaDrone)) max = Mathf.Max(max, tuning.stellaDroneNovaStars);

        return Mathf.Max(1, max);
    }

    private RoguelikeStar CreateStar()
    {
        RoguelikeStar star = Instantiate(starPrefab, transform);
        star.HitEvent += starHitHandler;
        star.HighlightChangedEvent += OnStarHighlightChanged;
        return star;
    }

    /// <summary>별이 도끼 범위에 들어오거나 빠졌다. 나무 아웃라인처럼 강조를 그릴 자리를 이펙트 쪽에 알린다(그림 중심 기준).</summary>
    private void OnStarHighlightChanged(RoguelikeStar _star, bool _bHighlighted)
    {
        Vector3 pos = _star.transform.position;
        Publish(_bHighlighted ? ERoguelikeSkillVfx.StarHighlightOn : ERoguelikeSkillVfx.StarHighlightOff, pos, pos, _star.Radius, 0f, _star.StarId);
    }

    /// <summary>
    /// 드론 레이저(총구 → 주 대상 → 연쇄 대상)가 나갔다. _points는 레이저 지점(나무 꼭대기), _hitGroundPositions는 맞힌 나무의 바닥 위치다.
    ///   포자 수류탄 + 드론(포자 레이저): 레이저가 맞힌 나무(주 대상 + 체인 대상) 모두의 바닥 자리에서 포자 수류탄 대폭발을 예약한다.
    ///   스텔라 III: 레이저가 지나간 경로 위의 별도 맞은 것으로 친다.
    /// 본편 드론 판정 도중에 불리므로 기록만 하고 터뜨리기는 Update(UpdateSporeComboExplosions · UpdateStars)에서 한다.
    /// </summary>
    private void OnDroneLaser(IReadOnlyList<Vector3> _points, IReadOnlyList<Vector3> _hitGroundPositions)
    {
        if (false == bActive) return;

        if (null != _hitGroundPositions && modifiers.IsComboActive(ERoguelikeCombo.SporeDrone))
        {
            for (int i = 0; i < _hitGroundPositions.Count; i++) QueueSporeExplosion(_hitGroundPositions[i]);
        }

        if (null == _points || _points.Count < 2) return;

        if (0 == activeStars.Count || false == RunEffectApplier.IsStarHitByActives(modifiers)) return;

        for (int s = 0; s < activeStars.Count; s++)
        {
            RoguelikeStar star = activeStars[s];
            if (false == star.bCanBeHitBySkill) continue;

            Vector2 starPos = star.Position + star.Offset;
            float radiusSq = star.Radius * star.Radius;

            for (int i = 1; i < _points.Count; i++)
            {
                if (DistancePointToSegmentSq(starPos, _points[i - 1], _points[i]) > radiusSq) continue;

                OnStarHit(star);
                break;
            }
        }

        // 스텔라 III: 드론은 레이저 사거리 안의 별을 나무보다 먼저 노린다(기획서 v2.6 6-1, 초안). 본편 드론 타겟팅(나무 전용)은 건드리지 않고,
        // 레이저가 나갈 때마다 총구에서 사거리 안 가장 가까운 별 하나를 함께 맞힌다(별까지 레이저를 하나 더 그린다).
        RoguelikeStar target = FindNearestStarForDrone(_points[0]);
        if (null == target) return;

        OnStarHit(target);
        Publish(ERoguelikeSkillVfx.DroneStarShot, _points[0], target.transform.position, 0f, 0f, target.StarId);
    }

    /// <summary>총구 _origin에서 드론 사거리 안(등각 거리) 가장 가까운, 아직 맞지 않은 별. 없으면 null.</summary>
    private RoguelikeStar FindNearestStarForDrone(Vector3 _origin)
    {
        float range = null != character ? character.statComponent.droneAttackRange : 0f;
        float bestSq = range * range;
        RoguelikeStar best = null;

        for (int i = 0; i < activeStars.Count; i++)
        {
            RoguelikeStar star = activeStars[i];
            if (false == star.bCanBeHitBySkill || pendingStarHits.Contains(star)) continue;

            Vector2 offset = (star.Position + star.Offset) - (Vector2)_origin;
            float isoSq = offset.x * offset.x + offset.y * offset.y * 4f;
            if (isoSq > bestSq) continue;

            bestSq = isoSq;
            best = star;
        }

        return best;
    }

    private void OnStarHit(RoguelikeStar _star)
    {
        // 도끼 판정 루프 안에서 불린다. 바로 터뜨리지 않고 다음 Update에서 처리한다.
        if (false == pendingStarHits.Contains(_star)) pendingStarHits.Add(_star);
    }

    private void UpdateStars(float _now)
    {
        for (int i = 0; i < pendingStarHits.Count; i++)
        {
            int idx = activeStars.IndexOf(pendingStarHits[i]);
            if (idx < 0) continue;

            RoguelikeStar star = activeStars[idx];
            StarBlast(star.transform.position, tuning.stellaBlastRadius, tuning.stellaBlastDamage);
            Publish(ERoguelikeSkillVfx.StarBlast, star.transform.position, star.transform.position, tuning.stellaBlastRadius * RangeMul(ERoguelikeSkill.Stella), 0f, star.StarId);

            // 스텔라 MAX: 같은 자리에서 폭발이 이어서 더 일어난다(별은 첫 폭발과 함께 사라진다).
            int repeats = Mathf.Max(0, Mathf.RoundToInt(modifiers.GetForSkill(ERunEffect.StarBlastRepeatAdd, ERoguelikeSkill.Stella)));
            for (int r = 1; r <= repeats; r++)
            {
                pendingRepeatBlasts.Add(new RepeatBlast
                {
                    position = star.transform.position,
                    time = _now + tuning.stellaBlastRepeatDelay * r,
                    starId = star.StarId,
                });
            }

            DespawnStar(idx, false);
        }
        pendingStarHits.Clear();

        for (int i = pendingRepeatBlasts.Count - 1; i >= 0; i--)
        {
            RepeatBlast blast = pendingRepeatBlasts[i];
            if (_now < blast.time) continue;

            pendingRepeatBlasts.RemoveAt(i);
            StarBlast(blast.position, tuning.stellaBlastRadius, tuning.stellaBlastDamage);
            // 별은 첫 폭발 때 사라졌으므로 StarBlast(땅 별 사라짐 포함)가 아니라 폭발만 다시 내는 신호를 쓴다.
            Publish(ERoguelikeSkillVfx.StarBlastRepeat, blast.position, blast.position, tuning.stellaBlastRadius * RangeMul(ERoguelikeSkill.Stella), 0f, blast.starId);
        }

        // 스텔라+드론의 별은 노바 때만 사라진다.
        if (modifiers.IsComboActive(ERoguelikeCombo.StellaDrone)) return;

        for (int i = activeStars.Count - 1; i >= 0; i--)
        {
            if (_now < activeStars[i].ExpireTime) continue;

            Publish(ERoguelikeSkillVfx.StarExpired, activeStars[i].transform.position, activeStars[i].transform.position, 0f, 0f, activeStars[i].StarId);
            DespawnStar(i, false);
        }
    }

    /// <summary>별의 단발 공격. 주변 나무에 피해를 주고 별 표식을 남긴다.</summary>
    private void StarBlast(Vector3 _center, float _baseRadius, float _baseDamage)
    {
        float radius = _baseRadius * RangeMul(ERoguelikeSkill.Stella);
        float damage = _baseDamage * DamageMul(ERoguelikeSkill.Stella);

        DamageTreesInRadius(_center, radius, damage, true, true, starBlastHpCapRatio);
    }

    private void DespawnStar(int _index, bool _bRebuildLinks)
    {
        RoguelikeStar star = activeStars[_index];
        activeStars.RemoveAt(_index);
        star.Remove();
        starPool.Push(star);

        if (_bRebuildLinks) RebuildStarLinks();
    }

    // // 표식

    private void MarkTree(TreeObj _tree)
    {
        float duration = tuning.stellaMarkDuration + modifiers.GetForSkill(ERunEffect.StarMarkDurationAdd, ERoguelikeSkill.Stella);
        float expire = Time.time + Mathf.Max(0f, duration) * DurationMul(ERoguelikeSkill.Stella);

        for (int i = 0; i < markedTrees.Count; i++)
        {
            if (markedTrees[i].tree != _tree) continue;

            MarkedTree existing = markedTrees[i];
            existing.expireTime = expire;
            markedTrees[i] = existing;
            return;
        }

        _tree.SetStarMarked(true);
        markedTrees.Add(new MarkedTree
        {
            tree = _tree,
            expireTime = expire,
        });
    }

    private void UpdateMarks(float _now)
    {
        for (int i = markedTrees.Count - 1; i >= 0; i--)
        {
            MarkedTree mark = markedTrees[i];
            TreeObj tree = mark.tree;

            if (null == tree || tree.IsPooled || tree.bDead || false == tree.bCanApplyDamage)
            {
                markedTrees.RemoveAt(i);
                continue;
            }

            if (_now >= mark.expireTime)
            {
                tree.SetStarMarked(false);
                markedTrees.RemoveAt(i);
            }
        }
    }

    // // 포자폭탄
    //
    // 기획서 v2.5 6-1: 자동으로 특정 위치에 포자 폭탄을 던진다. 폭탄이 터지면 본편의 포자 대폭발이 일어나고, 폭발 범위에 맞은
    // 나무들이 오염되어 오염이 유지되는 동안 지속 피해를 받는다(연막은 남지 않는다). 오염된 나무가 대폭발에 다시 맞으면
    // 오염 유지 시간이 새로 시작되고, 포자폭탄 V부터는 지속 피해가 중첩된다(V 최대 2, MAX 최대 3).
    // 틱 주기는 2.0초로 극단적으로 느리게 시작해 II 1.5 · IV 1.0 · VI 0.5초로 짧아지고(DotTickIntervalAdd), 공통 패시브
    // "지속 피해 속도 증가"가 다시 줄인다(하한 dotTickIntervalMin). 틱 피해는 4로 고정이다(레벨이 올리지 않는다).

    private void UpdateSpore(float _now)
    {
        if (modifiers.IsSkillActive(ERoguelikeSkill.SporeBomb) && _now >= nextSporeTime)
        {
            nextSporeTime = _now + Interval(tuning.sporeInterval, ERoguelikeSkill.SporeBomb);

            int bombs = 1 + CountAdd(ERoguelikeSkill.SporeBomb);
            for (int i = 0; i < bombs; i++) ThrowSporeBomb(_now);
        }

        for (int i = sporeBombs.Count - 1; i >= 0; i--)
        {
            SporeBomb bomb = sporeBombs[i];

            // 떨어지는 순간 본편 대폭발 연출이 시작된다(응집한 뒤 터진다). 판정은 터지는 순간(blastTime)에 한다.
            if (false == bomb.bLanded && _now >= bomb.landTime)
            {
                bomb.bLanded = true;
                sporeBombs[i] = bomb;
                Publish(ERoguelikeSkillVfx.SporeBigExplosion, bomb.position, bomb.position, bomb.radius, 0f, SporeTier());
            }

            if (_now < bomb.blastTime) continue;

            sporeBombs.RemoveAt(i);
            SporeBigExplosion(bomb.position, bomb.radius);
        }

        UpdateContaminations(_now);
    }

    /// <summary>
    /// 오염 지속 피해 틱과 만료. 오염된 나무는 자기 틱 주기마다 (틱 피해 × 중첩 수)를 받는다.
    /// 틱을 만료보다 먼저 처리해, 만료 시각 이전에 잡혀 있던 틱(유지 4초 · 주기 2초면 4초 시점의 두 번째 틱)이 만료와 같은 프레임이어도 들어가게 한다.
    /// 다음 틱 시각은 "이전 틱 시각 + 주기"로 쌓는다 - "지금 + 주기"로 잡으면 프레임마다 늦어져 만료 시각의 틱이 늘 밀려난다.
    /// </summary>
    private void UpdateContaminations(float _now)
    {
        if (0 == contaminations.Count) return;

        float tickDamage = tuning.sporeTickDamage * DotMul(ERoguelikeSkill.SporeBomb);
        float extendOnKill = modifiers.Get(ERunEffect.DurationExtendOnDotKill);
        int stackMax = SporeStackMax();
        int kills = 0;

        TreeTileWriteBatch.Begin();
        try
        {
            for (int i = contaminations.Count - 1; i >= 0; i--)
            {
                if (i >= contaminations.Count) continue;

                Contamination c = contaminations[i];

                // 프레임이 길게 끊겨 틱 여러 개가 밀렸으면 만료 시각 안의 몫만 이어서 준다.
                while (IsContaminationAlive(in c) && c.nextTickTime <= _now && c.nextTickTime <= c.expireTime)
                {
                    c.nextTickTime += SporeTickInterval(c.stacks >= stackMax);
                    contaminations[i] = c;

                    // 이 피해로 쓰러지면 OnTreeKilled 이후 아래 만료 정리에서 목록에서 빠진다(재사용 번호가 바뀐다).
                    c.tree.TakeDamage(tickDamage * c.stacks, false, false);
                    if (false == IsHittable(c.tree))
                    {
                        kills++;
                        break;
                    }
                }
            }
        }
        finally
        {
            TreeTileWriteBatch.End();
        }

        // 만료 · 쓰러진 오염을 거둔다. 틱 뒤에 거두므로 만료 시각에 걸린 마지막 틱은 이미 들어갔다.
        for (int i = contaminations.Count - 1; i >= 0; i--)
        {
            Contamination c = contaminations[i];
            if (IsContaminationAlive(in c) && _now < c.expireTime) continue;

            RemoveContaminationAt(i, true);
        }

        // 유지 시간 증가 MAX: 지속형 스킬(오염)로 나무를 처치하면 남아 있는 오염의 유지 시간이 늘어난다. 처치 하나마다 늘린다(별 표식과 같다).
        if (kills > 0 && extendOnKill > 0f) ExtendContaminations(extendOnKill * kills);
    }

    private bool IsContaminationAlive(in Contamination _c)
    {
        return IsHittable(_c.tree) && _c.tree.ReuseCount == _c.reuseCount;
    }

    /// <summary>본편 포자 대폭발과 같은 등각 타원 범위의 나무를 오염시킨다(피해가 정해져 있으면 먼저 준다).</summary>
    private void SporeBigExplosion(Vector3 _center, float _radius)
    {
        if (null == CollisionSystem.Instance || _radius <= 0f) return;

        CollisionSystem.Instance.GetCollidablesInRadius(_center, _radius, treeLayer.value, scanBuffer);

        bool bStarHit = RunEffectApplier.IsStarHitByActives(modifiers);
        float radiusSq = _radius * _radius;
        hitBuffer.Clear();

        for (int i = 0; i < scanBuffer.Count; i++)
        {
            Vector2 offset = scanBuffer[i].Position - (Vector2)_center;
            // 등각 타원 보정(본편 포자 대폭발과 같은 공식): 세로 거리를 두 배로 본다.
            if (offset.x * offset.x + offset.y * offset.y * 4f > radiusSq) continue;

            if (scanBuffer[i] is TreeObj tree)
            {
                if (IsHittable(tree)) hitBuffer.Add(tree);
            }
            else if (bStarHit && scanBuffer[i] is RoguelikeStar star && star.bCanBeHitBySkill)
            {
                // 스텔라 III: 대폭발도 별을 친다.
                OnStarHit(star);
            }
        }

        float damage = tuning.sporeExplosionDamage * DamageMul(ERoguelikeSkill.SporeBomb);
        float duration = tuning.sporeDuration * DurationMul(ERoguelikeSkill.SporeBomb);
        int stackMax = SporeStackMax();

        TreeTileWriteBatch.Begin();
        try
        {
            for (int i = 0; i < hitBuffer.Count; i++)
            {
                TreeObj tree = hitBuffer[i];
                if (false == IsHittable(tree)) continue;

                if (damage > 0f)
                {
                    tree.TakeDamage(damage, false, false);
                    if (false == IsHittable(tree)) continue;
                }

                Contaminate(tree, duration, stackMax);
            }
        }
        finally
        {
            TreeTileWriteBatch.End();
        }
    }

    /// <summary>나무를 오염시킨다. 이미 오염된 나무면 유지 시간을 새로 시작하고 중첩을 하나 올린다(상한까지).</summary>
    private void Contaminate(TreeObj _tree, float _duration, int _stackMax)
    {
        float expire = Time.time + Mathf.Max(0f, _duration);

        for (int i = 0; i < contaminations.Count; i++)
        {
            Contamination c = contaminations[i];
            if (c.tree != _tree || c.reuseCount != _tree.ReuseCount) continue;

            c.expireTime = expire;
            c.stacks = Mathf.Min(c.stacks + 1, _stackMax);
            contaminations[i] = c;
            vfxPlayer.SetContaminated(_tree, c.stacks);
            return;
        }

        // 처음 오염된 나무는 한 틱 주기 뒤에 첫 피해를 받는다. 다시 맞은 나무(위)는 돌던 틱 주기를 그대로 이어 간다.
        contaminations.Add(new Contamination
        {
            tree = _tree,
            reuseCount = _tree.ReuseCount,
            expireTime = expire,
            stacks = 1,
            nextTickTime = Time.time + SporeTickInterval(1 >= _stackMax),
        });
        vfxPlayer.SetContaminated(_tree, 1);
    }

    private void ExtendContaminations(float _seconds)
    {
        for (int i = 0; i < contaminations.Count; i++)
        {
            Contamination c = contaminations[i];
            c.expireTime += _seconds;
            contaminations[i] = c;
        }
    }

    /// <summary>오염 하나를 거둔다. _bRestoreMain이면 본편 포자 대폭발 증폭 표시가 남은 나무는 그 표시로 되돌린다(전투 중 만료).</summary>
    private void RemoveContaminationAt(int _index, bool _bRestoreMain)
    {
        Contamination c = contaminations[_index];
        contaminations.RemoveAt(_index);

        // 쓰러져 풀로 돌아간(재사용 번호가 바뀐) 나무의 표시는 본편 초기화(ResetTree)가 이미 지웠다.
        if (null != c.tree && false == c.tree.IsPooled && c.tree.ReuseCount == c.reuseCount) vfxPlayer.ClearContaminated(c.tree, _bRestoreMain);
    }

    /// <summary>
    /// 웨이브·런·세션이 끝나 오염을 모두 거둔다. 필드의 나무도 곧 정리되므로 본편 증폭 표시는 되돌리지 않는다
    /// (되돌리면 세션 해제 중에 디버프 표시 관리자를 새로 만들 수 있다).
    /// </summary>
    private void ClearContaminations()
    {
        for (int i = contaminations.Count - 1; i >= 0; i--) RemoveContaminationAt(i, false);
        contaminations.Clear();
    }

    /// <summary>포자폭탄 레벨 단계(1~3). 본편 대폭발 연출의 단계(I~III)로 쓴다: 중첩 상한을 따라 I~IV 1단계, V~VI 2단계, MAX 3단계.</summary>
    private int SporeTier()
    {
        int stack = SporeStackMax();
        if (stack >= 3) return 3;
        if (stack >= 2) return 2;
        return 1;
    }

    private int SporeStackMax() => Mathf.Max(1, 1 + Mathf.RoundToInt(modifiers.GetForSkill(ERunEffect.SporeStackAdd, ERoguelikeSkill.SporeBomb)));

    /// <summary>
    /// 오염 틱 주기(초). 기본 2.0초 + 포자 수류탄 레벨의 단축(II · IV · VI 각 −0.5초)에 공통 패시브 "지속 피해 속도 증가"의 감소율을 곱한다.
    /// _bAtMaxStack이면(중첩이 지금 상한에 닿은 나무) 그 패시브 MAX의 추가 감소가 더해진다. 하한은 dotTickIntervalMin(0.2초).
    /// </summary>
    private float SporeTickInterval(bool _bAtMaxStack)
    {
        float interval = tuning.sporeTickInterval + modifiers.GetForSkill(ERunEffect.DotTickIntervalAdd, ERoguelikeSkill.SporeBomb);
        float reduce = modifiers.GetForSkill(ERunEffect.DotTickIntervalReducePercent, ERoguelikeSkill.SporeBomb);
        if (_bAtMaxStack) reduce += modifiers.GetForSkill(ERunEffect.DotTickIntervalReduceAtMaxStackPercent, ERoguelikeSkill.SporeBomb);

        interval *= 1f - Mathf.Clamp(reduce, 0f, 95f) / 100f;
        return Mathf.Max(Mathf.Max(0.05f, tuning.dotTickIntervalMin), interval);
    }

    private void ThrowSporeBomb(float _now)
    {
        // 캐릭터 주변 나무 하나를 노린다. 없으면 이번 투하는 건너뛴다.
        CollisionSystem.Instance?.GetCollidablesInRadius(character.transform.position, tuning.sporeTargetRange, treeLayer.value, scanBuffer);

        int candidates = 0;
        for (int i = 0; i < scanBuffer.Count; i++)
        {
            if (scanBuffer[i] is TreeObj tree && IsHittable(tree)) candidates++;
        }

        if (0 == candidates) return;

        int pick = Random.Range(0, candidates);
        Vector3 target = character.transform.position;
        for (int i = 0; i < scanBuffer.Count; i++)
        {
            if (false == (scanBuffer[i] is TreeObj tree) || false == IsHittable(tree)) continue;
            if (0 == pick--)
            {
                target = tree.Position;
                break;
            }
        }

        float radius = tuning.sporeRadius * RangeMul(ERoguelikeSkill.SporeBomb);
        float flight = Mathf.Max(0f, vfxAssets.sporeBombThrowSeconds);

        sporeBombs.Add(new SporeBomb
        {
            position = target,
            radius = radius,
            landTime = _now + flight,
            blastTime = _now + flight + Mathf.Max(0f, tuning.sporeExplosionDelay),
            bLanded = false,
        });

        Publish(ERoguelikeSkillVfx.SporeBombThrown, target, character.transform.position, radius, flight, 0);
    }

    // // 오프로드
    //
    // 섬 가장자리 위에 플래시되며 소환(예열) → 확 튀어 나가 가속 → 나무를 들이받을 때마다 멈칫하며 속도를 잃고 다시 붙는다 →
    // 감속 없이 반대편 섬 끝까지 달리고, 끝에 닿기 직전 아주 짧게 하얗게 된 뒤 사라진다(물 위로 나가지 않는다).
    // 진행 방향은 차량 그림에 맞는 아래쪽 대각선 두 방향뿐이다(OffroadLanes).
    // 여러 대(III 2대, V 4대, MAX 6대)는 서로 다른 차선에서 시간차를 두고 출발한다.

    private void UpdateOffroad(float _now)
    {
        if (modifiers.IsSkillActive(ERoguelikeSkill.Offroad) && _now >= nextOffroadTime)
        {
            nextOffroadTime = _now + Interval(tuning.offroadInterval, ERoguelikeSkill.Offroad);
            StartOffroadVolley(_now, 1 + CountAdd(ERoguelikeSkill.Offroad));
        }

        if (0 == offroadCars.Count) return;

        float dt = Time.deltaTime;
        bool bStarHit = RunEffectApplier.IsStarHitByActives(modifiers);
        bool bSporeTrail = modifiers.IsComboActive(ERoguelikeCombo.SporeOffroad);

        for (int i = offroadCars.Count - 1; i >= 0; i--)
        {
            OffroadCar car = offroadCars[i];

            if (_now < car.dashStartTime)
            {
                car.visual?.SetPose(car.from, 0f);
                continue;
            }

            if (false == car.bDashStarted) LaunchOffroad(car);

            // 들이받은 직후에는 잠깐 멈칫한다(히트스톱). 멈춘 동안에도 엔진음 위치와 차체 떨림은 이어진다.
            // 감속은 하지 않는다. 섬 끝까지 그대로 달리고, 끝에 닿기 직전 구간에서 하얗게 된 뒤 사라진다(BeginOffroadExit).
            if (_now >= car.hitStopUntil)
            {
                car.speed = Mathf.MoveTowards(car.speed, car.maxSpeed, tuning.offroadAcceleration * dt);
                // 하얗게 된 뒤에는 끝에서 멈추지 않도록 그대로 달린다. 하얀 시간이 정해져 있어 끝을 넘는 거리는 한 프레임 이동량 이하다(섬 안쪽 들임 거리 안).
                car.traveled = car.bExiting ? car.traveled + car.speed * dt : Mathf.Min(car.length, car.traveled + car.speed * dt);
            }

            Vector3 pos = car.Position;
            car.visual?.SetPose(pos, _now >= car.hitStopUntil ? car.speed : 0f);
            if (car.bEngine) Sound.UpdateTrackedPosition(car.engine, pos);

            int hits = DamageTreesAlongSegment(car.lastPosition, pos, car.radius, car.damage, car.hitTrees);
            if (bStarHit) HitStarsAlongSegment(car.lastPosition, pos, car.radius);
            if (bSporeTrail) TrailSporeExplosions(car, pos);
            car.lastPosition = pos;

            if (hits > 0) OnOffroadHit(car, pos, hits, _now);

            // 지금 속도로 offroadExitSeconds 안에 섬 끝에 닿는 지점부터 하얗게 된다. 달리는 채로 하얀 상태가 offroadExitSeconds 동안 유지되다 사라진다.
            if (false == car.bExiting && car.length - car.traveled <= car.speed * Mathf.Max(0.01f, tuning.offroadExitSeconds))
            {
                BeginOffroadExit(car, pos, _now);
            }

            // 하얀 시간은 프레임 수가 아니라 시간(offroadExitSeconds)으로 재서 어떤 프레임 속도에서도 같게 보인다.
            // 프레임 경계에 맞춰 가장 가까운 프레임에서 거두도록 반 프레임을 당겨 비교한다(60fps에서 0.05초 = 정확히 3프레임).
            // 프레임이 아주 느려도 하얗게 된 모습이 적어도 한 프레임은 그려진 뒤에 거둔다.
            if (car.bExiting && Time.frameCount > car.exitStartFrame && _now >= car.exitEndTime - dt * 0.5f) ReleaseOffroadCar(i, false);
        }
    }

    /// <summary>
    /// 섬 끝에 닿기 직전이다. 엔진이 꺼지고, 차가 아주 짧게 하얗게 된 채 달리다 사라진다.
    /// 차는 멈추지 않고 그대로 달린다(감속 없음). offroadExitSeconds가 지나면 거둔다.
    /// </summary>
    private void BeginOffroadExit(OffroadCar _car, Vector3 _position, float _now)
    {
        float seconds = Mathf.Max(0.01f, tuning.offroadExitSeconds);

        _car.bExiting = true;
        _car.exitEndTime = _now + seconds;
        _car.exitStartFrame = Time.frameCount;
        _car.visual?.BeginExit();

        if (_car.engine.IsValid)
        {
            // 하얀 시간이 매우 짧아 엔진음은 따로 0.2초 이상에 걸쳐 잦아들게 한다(바로 끊기면 툭 끊긴다).
            Sound.StopTrackedWithPowerDown(_car.engine, Mathf.Max(0.2f, seconds), 0.5f);
            _car.engine = AudioHandle.Invalid;
        }

        Publish(ERoguelikeSkillVfx.OffroadExit, _position, _position, _car.radius, seconds, 0);
    }

#if UNITY_EDITOR
    /// <summary>
    /// [에디터 전용 디버그] 오프로드를 지금 바로 한 번 내보낸다(카드 보유·소환 주기와 무관). _count가 0 이하면 지금 빌드의 대수(1 + 발동 수)를 쓴다.
    /// 피해·돌진 폭은 지금 빌드 값을 그대로 쓴다. 웨이브 전투 중에만 차가 움직이므로 그때만 된다.
    /// </summary>
    public bool DebugFireOffroad(int _count)
    {
        if (false == bActive || null == tuning || null == character) return false;

        int count = _count > 0 ? _count : 1 + CountAdd(ERoguelikeSkill.Offroad);
        StartOffroadVolley(Time.time, count);
        return true;
    }
#endif

    /// <summary>
    /// 한 번에 _count대를 내보낸다(기획서 v2.5 6-1 오프로드 "소환 위치").
    ///   - 돌진 경로 위에 나무가 1그루 이상 있는 차선만 후보다.
    ///   - 이번에 함께 나가는 차끼리, 그리고 아직 돌진 중인 차와 위치가 겹치는 차선에는 소환하지 않는다.
    ///   - 후보가 대수보다 적으면 있는 만큼만 나간다.
    /// 후보는 두 방향(좌·우 아래 대각선)마다 섬 폭을 차선 폭으로 나눈 칸이고, 그중 무작위로 고른다.
    /// </summary>
    private void StartOffroadVolley(float _now, int _count)
    {
        int count = Mathf.Max(1, _count);
        float radius = tuning.offroadHitRadius * RangeMul(ERoguelikeSkill.Offroad);

        CollectOffroadLaneCandidates(radius);

        // 무작위 순서로 훑으며 겹치지 않는 차선을 고른다.
        offroadLanePicks.Clear();
        for (int i = offroadLaneCandidates.Count - 1; i >= 0 && offroadLanePicks.Count < count; i--)
        {
            int swap = Random.Range(0, i + 1);
            OffroadLane lane = offroadLaneCandidates[swap];
            offroadLaneCandidates[swap] = offroadLaneCandidates[i];
            offroadLaneCandidates[i] = lane;

            if (IsOffroadLaneTaken(in lane, radius)) continue;
            offroadLanePicks.Add(lane);
        }

        for (int i = 0; i < offroadLanePicks.Count; i++)
        {
            StartOffroad(_now, tuning.offroadVolleyStagger * i, offroadLanePicks[i], 0 == i);
        }
    }

    /// <summary>
    /// 돌진 경로 위에 나무가 있는 차선을 모두 모은다. 차선 칸은 소환 위치 최소 간격(돌진 판정 반지름)으로 나누고, 이웃 칸끼리도
    /// 그 간격 이상 떨어지게 흔든다 - 나무가 고루 있으면 MAX 6대가 늘 다 나간다. 돌진 판정 폭(반지름 × 2)은 이웃 차선과 일부 겹칠 수 있다
    /// (기획서의 "위치 중복 금지"는 같은 자리 소환을 막는 규칙이다).
    /// </summary>
    private void CollectOffroadLaneCandidates(float _radius)
    {
        offroadLaneCandidates.Clear();

        for (int side = 0; side < 2; side++)
        {
            bool bRight = 1 == side;
            Vector2 dir = OffroadLanes.DownDiagonal(bRight);
            float halfWidth = OffroadLanes.HalfWidth(fieldRadiusX, fieldRadiusY, dir);
            int slots = OffroadLanes.SlotCount(halfWidth, tuning.offroadLaneSpread, _radius);

            for (int slot = 0; slot < slots; slot++)
            {
                float offset = OffroadLanes.SlotOffset(slot, slots, halfWidth, tuning.offroadLaneSpread, _radius, Random.value);
                if (false == OffroadLanes.TryGetLane(fieldCenter, fieldRadiusX, fieldRadiusY, dir, offset, out Vector2 entry, out Vector2 exit)) continue;

                // 섬 테두리 타일 위에서 소환되도록 경계선에서 진행 방향으로 들여온다(차선이 짧으면 차선 길이의 1/4까지만).
                // 반대편도 같은 만큼 안쪽에서 멈추므로 판정 경로는 양끝에서 inset만큼 짧다.
                float inset = Mathf.Min(Mathf.Max(0f, tuning.offroadEdgeInset), Vector2.Distance(entry, exit) * 0.25f);
                Vector2 start = entry + dir * inset;
                Vector2 end = exit - dir * inset;
                if (false == HasTreeAlongSegment(start, end, _radius)) continue;

                offroadLaneCandidates.Add(new OffroadLane { bRight = bRight, offset = offset, entry = start, exit = exit, inset = inset });
            }
        }
    }

    /// <summary>
    /// 이 차선이 이번에 고른 차선이나 아직 달리는 차와 겹치는지. 최소 간격은 돌진 판정 반지름이다(CollectOffroadLaneCandidates와 같은 값).
    /// 같은 방향이면 차선 간격이 최소 간격보다 좁을 때, 반대 방향이면 소환 위치가 다른 차(이번에 고른 차는 소환 위치, 달리는 차는 지금 위치)와
    /// 최소 간격 안일 때 겹친다고 본다.
    /// </summary>
    private bool IsOffroadLaneTaken(in OffroadLane _lane, float _radius)
    {
        float gap = _radius;
        float gapSq = gap * gap;

        for (int i = 0; i < offroadLanePicks.Count; i++)
        {
            OffroadLane other = offroadLanePicks[i];
            if (other.bRight == _lane.bRight ? Mathf.Abs(other.offset - _lane.offset) < gap : (other.entry - _lane.entry).sqrMagnitude < gapSq) return true;
        }

        for (int i = 0; i < offroadCars.Count; i++)
        {
            OffroadCar car = offroadCars[i];
            if (car.bRight == _lane.bRight ? Mathf.Abs(car.laneOffset - _lane.offset) < gap : ((Vector2)car.Position - _lane.entry).sqrMagnitude < gapSq) return true;
        }

        return false;
    }

    private bool HasTreeAlongSegment(Vector2 _from, Vector2 _to, float _width)
    {
        if (null == CollisionSystem.Instance) return false;

        Vector2 center = (_from + _to) * 0.5f;
        float scanRadius = Vector2.Distance(_from, _to) * 0.5f + _width;
        CollisionSystem.Instance.GetCollidablesInRadius(center, scanRadius, treeLayer.value, scanBuffer);

        float widthSq = _width * _width;
        for (int i = 0; i < scanBuffer.Count; i++)
        {
            if (scanBuffer[i] is TreeObj tree && IsHittable(tree) && DistancePointToSegmentSq(tree.Position, _from, _to) <= widthSq) return true;
        }

        return false;
    }

    private void StartOffroad(float _now, float _delay, in OffroadLane _lane, bool _bEngine)
    {
        Vector2 dir = OffroadLanes.DownDiagonal(_lane.bRight);
        Vector2 entry = _lane.entry;
        Vector2 exit = _lane.exit;
        float inset = _lane.inset;

        OffroadCar car = 0 < offroadCarPool.Count ? offroadCarPool.Pop() : new OffroadCar();
        car.bRight = _lane.bRight;
        car.laneOffset = _lane.offset;
        car.from = entry;
        car.islandExit = exit;
        car.direction = dir;
        // 반대편도 같은 만큼 안쪽에서 멈춘다(물 위로 나가지 않는다).
        car.length = Mathf.Max(0.5f, Vector2.Distance(entry, exit) - inset);
        car.traveled = 0f;
        car.speed = 0f;
        // 오프로드 IV "돌진 속도 증가"(MoveSpeedPercent, 오프로드 전용).
        car.maxSpeed = tuning.offroadSpeed * Mathf.Max(0.1f, 1f + modifiers.Get(ERunEffect.MoveSpeedPercent, ERoguelikeSkill.Offroad) / 100f);
        car.dashStartTime = _now + tuning.offroadWarmup + _delay;
        car.bDashStarted = false;
        car.hitStopUntil = 0f;
        car.bExiting = false;
        car.exitEndTime = 0f;
        car.exitStartFrame = 0;
        car.lastPosition = entry;
        car.damage = tuning.offroadDamage * DamageMul(ERoguelikeSkill.Offroad);
        car.radius = tuning.offroadHitRadius * RangeMul(ERoguelikeSkill.Offroad);
        car.bEngine = _bEngine;
        car.sporeTrail = 0f;
        car.engine = AudioHandle.Invalid;
        car.hitTrees.Clear();
        car.visual = RentOffroadVisual();

        offroadCars.Add(car);

        // 예열은 출발 시각에 맞춰 끝난다. 뒤차는 앞차보다 늦게 예열을 시작한다.
        float warmup = car.dashStartTime - _now;
        if (null != car.visual) car.visual.BeginWarmup(entry, exit, warmup, car.bEngine);

        Publish(ERoguelikeSkillVfx.OffroadWarmup, entry, exit, car.radius, warmup, 0);
    }

    private void LaunchOffroad(OffroadCar _car)
    {
        _car.bDashStarted = true;
        _car.speed = tuning.offroadLaunchSpeed;
        _car.visual?.BeginDash();

        CameraMoveController.Instance?.ShakeCamera(tuning.offroadLaunchShake.x, tuning.offroadLaunchShake.y);

        // 엔진음: 한 번에 나가는 차량 중 첫 차만 낸다(여러 대가 겹치면 시끄럽다). 출발과 함께 회전수가 오른다.
        if (_car.bEngine)
        {
            _car.engine = Sound.PlayTrackedWithPowerUp(SoundID.OffroadNonEditLoop, _car.from, tuning.offroadEngineVolume, true, 0.25f, 0.6f, 1.25f);
        }

        float estimate = _car.length / Mathf.Max(0.1f, _car.maxSpeed);
        Publish(ERoguelikeSkillVfx.OffroadDash, _car.from, _car.from + (Vector3)(_car.direction * _car.length), _car.radius, estimate, 0);
    }

    /// <summary>나무를 들이받았다. 멈칫(히트스톱)·감속·차체 반동·카메라 흔들림·진동을 준다.</summary>
    private void OnOffroadHit(OffroadCar _car, Vector3 _position, int _hits, float _now)
    {
        float stop = tuning.offroadHitStop + tuning.offroadHitStopPerExtraTree * (_hits - 1);
        _car.hitStopUntil = _now + Mathf.Min(stop, tuning.offroadHitStopMax);
        _car.speed *= 1f - Mathf.Clamp01(tuning.offroadHitSpeedLoss);
        _car.visual?.Bump(_hits);

        if (_now >= nextOffroadHitShakeTime)
        {
            nextOffroadHitShakeTime = _now + tuning.offroadHitShakeInterval;
            CameraMoveController.Instance?.ShakeCamera(tuning.offroadHitShake.x, tuning.offroadHitShake.y);
        }

        Rumble.Play(EHapticEvent.TreeImpact);
        Publish(ERoguelikeSkillVfx.OffroadHit, _position, _position, _car.radius, 0f, _hits);
    }

    /// <summary>스텔라 III: 오프로드가 지나간 길 위의 별도 친다.</summary>
    private void HitStarsAlongSegment(Vector3 _from, Vector3 _to, float _width)
    {
        for (int i = 0; i < activeStars.Count; i++)
        {
            RoguelikeStar star = activeStars[i];
            if (false == star.bCanBeHitBySkill) continue;

            float reach = _width + star.Radius;
            if (DistancePointToSegmentSq(star.Position + star.Offset, _from, _to) <= reach * reach) OnStarHit(star);
        }
    }

    private RoguelikeOffroadVisual RentOffroadVisual()
    {
        if (null == offroadVisualPrefab) return null;

        RoguelikeOffroadVisual visual = 0 < offroadVisualPool.Count ? offroadVisualPool.Pop() : null;
        if (null == visual)
        {
            GameObject go = Instantiate(offroadVisualPrefab, transform);
            visual = go.GetComponent<RoguelikeOffroadVisual>();
            if (null == visual) visual = go.AddComponent<RoguelikeOffroadVisual>();
        }

        visual.gameObject.SetActive(true);
        return visual;
    }

    /// <summary>차량 한 대를 거둔다. _bImmediate(웨이브 종료 등)면 엔진음을 바로 끊고, 아니면 회전수가 내려가며 꺼진다.</summary>
    private void ReleaseOffroadCar(int _index, bool _bImmediate)
    {
        OffroadCar car = offroadCars[_index];
        offroadCars.RemoveAt(_index);

        if (car.engine.IsValid)
        {
            if (_bImmediate) Sound.StopTracked(car.engine);
            else Sound.StopTrackedWithPowerDown(car.engine, 0.35f, 0.5f);
        }

        if (null != car.visual)
        {
            car.visual.Hide();
            offroadVisualPool.Push(car.visual);
        }

        car.visual = null;
        car.engine = AudioHandle.Invalid;
        car.hitTrees.Clear();
        offroadCarPool.Push(car);
    }

    // // 조합: 포자 수류탄 + 드론 / 오프로드 / 충격파 (기획서 v2.6 6-2)
    //
    // 세 조합 모두 포자 수류탄의 던지기만 꺼지고(조합 카드의 suppressedSkills) 포자 수류탄의 대폭발(폭발 피해 포함) + 오염은 그대로 쓴다.
    // 포자 레이저는 드론이 맞힌 모든 나무, 방구차는 오프로드가 지나가는 경로(일정 거리마다), 포자 충격파는 초록 충격파에 맞은 모든 나무에서
    // 대폭발이 일어난다. 범위 · 오염 유지 · 틱 주기 · 중첩 상한은 포자 수류탄 카드 레벨과 오염 관련 패시브를 그대로 따른다.
    // 맞히는 나무 수만큼 대폭발이 폭증할 수 있으므로(부록 C-2 "연쇄 폭발") 같은 자리 재발동 제한과 프레임당 발동 수 상한을 둔다.
    // 대폭발 자체는 다른 대폭발을 부르지 않는다(연쇄 없음).

    /// <summary>포자 충격파: 충격파가 나무를 맞혔다(본편 충격파 판정 도중이라 예약만 한다).</summary>
    private void OnShockWaveHitTree(Vector3 _position)
    {
        if (bActive && modifiers.IsComboActive(ERoguelikeCombo.SporeShockwave)) QueueSporeExplosion(_position);
    }

    /// <summary>방구차: 오프로드가 _position까지 달린 거리만큼 쌓다가 간격마다 그 자리에 포자 수류탄 대폭발을 예약한다.</summary>
    private void TrailSporeExplosions(OffroadCar _car, Vector3 _position)
    {
        float spacing = Mathf.Max(0.1f, tuning.sporeOffroadSpacing);
        _car.sporeTrail += Vector2.Distance(_car.lastPosition, _position);

        while (_car.sporeTrail >= spacing)
        {
            _car.sporeTrail -= spacing;
            QueueSporeExplosion(_position);
        }
    }

    private void QueueSporeExplosion(Vector3 _position)
    {
        if (pendingSporeExplosions.Count < Mathf.Max(1, tuning.sporeComboMaxPending)) pendingSporeExplosions.Add(_position);
    }

    /// <summary>
    /// 예약된 자리에 포자 수류탄의 대폭발을 프레임당 상한까지 일으킨다. 최근에 같은 자리(병합 반경 안)에서 일어났으면 건너뛴다.
    /// 던지지 않고 그 자리에 바로 떨어진 폭탄으로 넣는다 - 착지 연출 · 폭발 판정 지연 · 오염은 포자 수류탄과 같은 경로(UpdateSpore)를 탄다.
    /// </summary>
    private void UpdateSporeComboExplosions(float _now)
    {
        float cooldown = Mathf.Max(0f, tuning.sporeComboRepeatCooldown);
        for (int i = recentSporeExplosions.Count - 1; i >= 0; i--)
        {
            if (_now - recentSporeExplosions[i].time >= cooldown) recentSporeExplosions.RemoveAt(i);
        }

        if (0 == pendingSporeExplosions.Count) return;

        int budget = Mathf.Max(1, tuning.sporeComboMaxExplosionsPerFrame);
        float radius = tuning.sporeRadius * RangeMul(ERoguelikeSkill.SporeBomb);
        float delay = Mathf.Max(0f, tuning.sporeExplosionDelay);
        float mergeSq = Mathf.Max(0f, tuning.sporeComboMergeRadius);
        mergeSq *= mergeSq;

        int consumed = 0;
        for (; consumed < pendingSporeExplosions.Count && budget > 0; consumed++)
        {
            Vector3 pos = pendingSporeExplosions[consumed];
            if (IsRecentSporeExplosion(pos, mergeSq)) continue;

            budget--;
            recentSporeExplosions.Add(new SporeComboBlast { position = pos, time = _now });

            sporeBombs.Add(new SporeBomb
            {
                position = pos,
                radius = radius,
                landTime = _now,
                blastTime = _now + delay,
                bLanded = false,
            });
        }

        pendingSporeExplosions.RemoveRange(0, consumed);
    }

    private bool IsRecentSporeExplosion(Vector3 _position, float _mergeSq)
    {
        for (int i = 0; i < recentSporeExplosions.Count; i++)
        {
            if (((Vector2)recentSporeExplosions[i].position - (Vector2)_position).sqrMagnitude <= _mergeSq) return true;
        }

        return false;
    }

    // // 조합: 스텔라 + 드론

    private void UpdateStellaDrone(float _now)
    {
        if (false == modifiers.IsComboActive(ERoguelikeCombo.StellaDrone))
        {
            if (starLinks.Count > 0) starLinks.Clear();
            return;
        }

        // 조합을 산 순간 이미 떠 있던 별도 더 이상 공격받지 않는다.
        for (int i = 0; i < activeStars.Count; i++)
        {
            if (activeStars[i].bCanApplyDamage) activeStars[i].SetAttackable(false);
        }

        if (activeStars.Count >= 2 && 0 == starLinks.Count) RebuildStarLinks();

        // 별이 노바 수만큼 모이면 노바. 모든 별이 사라지고 처음부터 다시 모은다.
        if (activeStars.Count >= tuning.stellaDroneNovaStars)
        {
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < activeStars.Count; i++) centroid += activeStars[i].transform.position;
            centroid /= activeStars.Count;

            float radius = tuning.stellaDroneNovaRadius * RangeMul(ERoguelikeSkill.Stella);
            DamageTreesInRadius(centroid, radius, tuning.stellaDroneNovaDamage * DamageMul(ERoguelikeSkill.Stella), false);
            Publish(ERoguelikeSkillVfx.StarNova, centroid, centroid, radius, 0f, 0);

            // 노바 때 별이 모두 소멸한다(기획서). 별마다 소멸 연출 지점을 알린다.
            for (int i = activeStars.Count - 1; i >= 0; i--)
            {
                Vector3 starPos = activeStars[i].transform.position;
                Publish(ERoguelikeSkillVfx.StarExpired, starPos, starPos, 0f, 0f, activeStars[i].StarId);
                DespawnStar(i, false);
            }

            RebuildStarLinks();
            return;
        }

        // 홀로 남은 별: 공격받지도 사라지지도 않고 주변에 표식 공격을 계속한다.
        if (1 == activeStars.Count && _now >= nextLonePulseTime)
        {
            nextLonePulseTime = _now + Interval(tuning.stellaDroneLoneStarInterval, ERoguelikeSkill.Drone);
            Vector3 pos = activeStars[0].transform.position;
            float radius = tuning.stellaDroneLoneStarRadius * RangeMul(ERoguelikeSkill.Stella);
            DamageTreesInRadius(pos, radius, tuning.stellaBlastDamage * DamageMul(ERoguelikeSkill.Stella), true, true, starBlastHpCapRatio);
            Publish(ERoguelikeSkillVfx.LoneStarPulse, pos, pos, radius, 0f, activeStars[0].StarId);
        }

        // 이음선 빔.
        if (starLinks.Count > 0 && _now >= nextBeamTime)
        {
            nextBeamTime = _now + Interval(tuning.stellaDroneBeamInterval, ERoguelikeSkill.Drone);
            float width = tuning.stellaDroneBeamWidth * RangeMul(ERoguelikeSkill.Drone);
            float damage = tuning.stellaDroneBeamDamage * DamageMul(ERoguelikeSkill.Drone);

            // 이번 빔은 쏘기 직전의 이음선으로 끝까지 쏜다. 도중에 생긴 별은 다음 빔부터 이어진다.
            // (별은 이 사이 늘기만 하므로 사본의 인덱스는 activeStars에서 그대로 유효하다)
            beamLinkBuffer.Clear();
            beamLinkBuffer.AddRange(starLinks);

            for (int i = 0; i < beamLinkBuffer.Count; i++)
            {
                Vector3 a = activeStars[beamLinkBuffer[i].x].transform.position;
                Vector3 b = activeStars[beamLinkBuffer[i].y].transform.position;
                DamageTreesAlongSegment(a, b, width, damage, null);
                Publish(ERoguelikeSkillVfx.StarLinkBeam, a, b, width, 0f, 0);
            }
        }
    }

    /// <summary>현재 별들로 최소 신장 트리를 만든다(프림). 별은 많아야 노바 수(9) 근처라 매번 다시 계산해도 싸다.</summary>
    private void RebuildStarLinks()
    {
        starLinks.Clear();

        int n = activeStars.Count;
        if (n >= 2)
        {
            mstInTree.Clear();
            mstBestDist.Clear();
            mstBestFrom.Clear();
            for (int i = 0; i < n; i++)
            {
                mstInTree.Add(false);
                mstBestDist.Add(float.MaxValue);
                mstBestFrom.Add(-1);
            }

            mstBestDist[0] = 0f;
            for (int step = 0; step < n; step++)
            {
                int u = -1;
                for (int i = 0; i < n; i++)
                {
                    if (mstInTree[i]) continue;
                    if (u < 0 || mstBestDist[i] < mstBestDist[u]) u = i;
                }

                mstInTree[u] = true;
                if (mstBestFrom[u] >= 0) starLinks.Add(new Vector2Int(mstBestFrom[u], u));

                Vector3 pu = activeStars[u].transform.position;
                for (int v = 0; v < n; v++)
                {
                    if (mstInTree[v]) continue;
                    float d = (activeStars[v].transform.position - pu).sqrMagnitude;
                    if (d < mstBestDist[v])
                    {
                        mstBestDist[v] = d;
                        mstBestFrom[v] = u;
                    }
                }
            }
        }

        Publish(ERoguelikeSkillVfx.StarLinksChanged, fieldCenter, fieldCenter, 0f, 0f, starLinks.Count);
    }

    // // 피해 공통

    /// <summary>
    /// 반경 안 나무에 피해를 준다. _hpCapRatio가 0보다 크면 한 그루가 이 한 번에 받는 최종 피해(별 표식 · 포자 증폭 배율 포함)를
    /// 그 나무 최대 HP × _hpCapRatio로 자른다(기획서 v2.5 A-4 스텔라 4티어 폭발 상한).
    /// </summary>
    private void DamageTreesInRadius(Vector3 _center, float _radius, float _damage, bool _bMark, bool _bBatch = true, float _hpCapRatio = 0f)
    {
        if (null == CollisionSystem.Instance || _radius <= 0f) return;

        CollisionSystem.Instance.GetCollidablesInRadius(_center, _radius, treeLayer.value, scanBuffer);

        hitBuffer.Clear();
        float radiusSq = _radius * _radius;
        for (int i = 0; i < scanBuffer.Count; i++)
        {
            if (false == (scanBuffer[i] is TreeObj tree) || false == IsHittable(tree)) continue;
            if (((Vector2)_center - tree.Position).sqrMagnitude > radiusSq) continue;
            hitBuffer.Add(tree);
        }

        if (_bBatch) TreeTileWriteBatch.Begin();
        try
        {
            for (int i = 0; i < hitBuffer.Count; i++)
            {
                TreeObj tree = hitBuffer[i];
                if (false == IsHittable(tree)) continue;

                // 별은 주변을 공격한 "뒤에" 표식을 남긴다(기획서 6-1). 표식의 받는 피해 +30%는 이 폭발이 아니라 이후 피해부터 붙는다.
                // 이 피해로 쓰러진 나무는 풀로 돌아가므로 표식을 남기지 않는다.
                tree.TakeDamage(_hpCapRatio > 0f ? CapDamageToMaxHp(tree, _damage, _hpCapRatio) : _damage, false, false);
                if (_bMark && IsHittable(tree)) MarkTree(tree);
            }
        }
        finally
        {
            if (_bBatch) TreeTileWriteBatch.End();
        }
    }

    /// <summary>선분 위의 나무에 피해를 주고 맞힌 나무 수를 돌려준다.</summary>
    private int DamageTreesAlongSegment(Vector3 _from, Vector3 _to, float _width, float _damage, Dictionary<TreeObj, int> _alreadyHit)
    {
        if (null == CollisionSystem.Instance) return 0;

        Vector2 a = _from;
        Vector2 b = _to;
        Vector2 center = (a + b) * 0.5f;
        float scanRadius = Vector2.Distance(a, b) * 0.5f + _width;

        CollisionSystem.Instance.GetCollidablesInRadius(center, scanRadius, treeLayer.value, scanBuffer);

        hitBuffer.Clear();
        float widthSq = _width * _width;
        for (int i = 0; i < scanBuffer.Count; i++)
        {
            if (false == (scanBuffer[i] is TreeObj tree) || false == IsHittable(tree)) continue;
            if (null != _alreadyHit && _alreadyHit.TryGetValue(tree, out int hitReuse) && hitReuse == tree.ReuseCount) continue;
            if (DistancePointToSegmentSq(tree.Position, a, b) > widthSq) continue;
            hitBuffer.Add(tree);
        }

        int hits = 0;

        TreeTileWriteBatch.Begin();
        try
        {
            for (int i = 0; i < hitBuffer.Count; i++)
            {
                TreeObj tree = hitBuffer[i];
                if (false == IsHittable(tree)) continue;

                if (null != _alreadyHit) _alreadyHit[tree] = tree.ReuseCount;
                tree.TakeDamage(_damage, false, false);
                hits++;
            }
        }
        finally
        {
            TreeTileWriteBatch.End();
        }

        return hits;
    }

    /// <summary>
    /// 나무가 받는 최종 피해가 최대 HP × _ratio를 넘지 않게 넘길 피해를 줄인다. 나무는 TakeDamage 안에서 별 표식 배율과
    /// 포자 대폭발 증폭을 곱하므로 그 배율로 나눠 넘긴다.
    /// </summary>
    private float CapDamageToMaxHp(TreeObj _tree, float _damage, float _ratio)
    {
        float maxHp = null != _tree.health ? _tree.health.GetMaxHealth() : 0f;
        if (maxHp <= 0f) return _damage;

        float scale = 1f + Mathf.Max(0f, _tree.SporeDamageAmplify);
        if (_tree.bStarMarked && null != objectManager) scale *= Mathf.Max(0f, objectManager.StarMarkDamageMultiplier);
        if (scale <= 0f) return _damage;

        return Mathf.Min(_damage, maxHp * _ratio / scale);
    }

    private static bool IsHittable(TreeObj _tree)
    {
        return null != _tree && false == _tree.bDead && false == _tree.IsPooled && _tree.bCanApplyDamage;
    }

    private static float DistancePointToSegmentSq(Vector2 _p, Vector2 _a, Vector2 _b)
    {
        Vector2 ab = _b - _a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return (_p - _a).sqrMagnitude;

        float t = Mathf.Clamp01(Vector2.Dot(_p - _a, ab) / lenSq);
        return (_p - (_a + ab * t)).sqrMagnitude;
    }

    // // 수치

    // 피해%·공격속도%에는 전투 중 조건부 보너스(공격력 MAX 임계치 초과, 이동속도 MAX 이동 중)가 더해진다(RunModifiers.SetConditional).
    private float DamageMul(ERoguelikeSkill _skill) => Mathf.Max(0f, 1f + (modifiers.GetForSkill(ERunEffect.DamagePercent, _skill) + modifiers.ConditionalDamagePercent) / 100f);
    private float RangeMul(ERoguelikeSkill _skill) => Mathf.Max(0.1f, 1f + modifiers.GetForSkill(ERunEffect.RangePercent, _skill) / 100f);
    private float DurationMul(ERoguelikeSkill _skill) => Mathf.Max(0.1f, 1f + modifiers.GetForSkill(ERunEffect.DurationPercent, _skill) / 100f);
    private float DotMul(ERoguelikeSkill _skill) => DamageMul(_skill) * Mathf.Max(0f, 1f + modifiers.GetForSkill(ERunEffect.DotDamagePercent, _skill) / 100f);
    private int CountAdd(ERoguelikeSkill _skill) => Mathf.Max(0, Mathf.RoundToInt(modifiers.GetForSkill(ERunEffect.CountAdd, _skill)));

    private float Interval(float _baseInterval, ERoguelikeSkill _skill)
    {
        float speed = Mathf.Max(0.1f, 1f + (modifiers.GetForSkill(ERunEffect.AttackSpeedPercent, _skill) + modifiers.ConditionalAttackSpeedPercent) / 100f);
        float reduction = Mathf.Clamp(modifiers.GetForSkill(ERunEffect.CooldownReductionPercent, _skill), 0f, 90f) / 100f;
        return Mathf.Max(0.05f, _baseInterval / speed * (1f - reduction));
    }

    private void Publish(ERoguelikeSkillVfx _type, Vector3 _position, Vector3 _position2, float _radius, float _duration, int _id)
    {
        RoguelikeSkillVfxSignal signal = new RoguelikeSkillVfxSignal(_type, _position, _position2, _radius, _duration, _id);
        vfxPlayer.Play(in signal, activeStars, starLinks);
        signalHub?.Publish(signal);
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

    private void OnDestroy()
    {
        RunCombatHooks.AxeSwungEvent -= axeSwungHandler;
        RunCombatHooks.BoomerangHitTreeEvent -= boomerangHitHandler;
        RunCombatHooks.DroneLaserEvent -= droneLaserHandler;
        RunCombatHooks.ShockWaveHitTreeEvent -= shockWaveHitHandler;
    }
}
