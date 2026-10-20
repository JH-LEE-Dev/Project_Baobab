using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크에만 있는 액티브 스킬과 조합 효과. 기획서 3-3 "액티브 카드 7종"·"조합 카드(프리즘) 5종".
///
///   본편 스킬 재사용(RunEffectApplier가 스탯으로 켬): 회전베기, 충격파, 부메랑, 드론
///   여기서 구현하는 신규 스킬: 오프로드, 스텔라, 포자폭탄
///   여기서 구현하는 조합: 회전베기+충격파(360도 충격파), 부메랑+충격파, 회전베기+부메랑, 스텔라+포자폭탄, 스텔라+드론
///
/// 판정·피해와 움직임·손맛(오프로드 예열·가속·충돌 반동, 카메라 흔들림, 진동, 효과음)은 여기서 맡는다.
/// 먼지·잔상 같은 이펙트는 이펙트 담당이 RoguelikeSkillVfxSignal(및 별 프리팹)로 입힌다.
/// 웨이브 전투 중에만 돈다(BeginCombat ~ EndCombat). 웨이브가 끝나면 별·연막·표식을 모두 치운다.
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
        public bool bDot;
        public float nextDotTime;
    }

    private struct SporeZone
    {
        public int id;
        public Vector3 position;
        public float radius;
        public float endTime;
        public float tickDamage;
    }

    /// <summary>돌진 중인 오프로드 한 대. 차마다 맞힌 나무를 따로 세어, 여러 대가 같은 나무를 각각 칠 수 있다.</summary>
    private sealed class OffroadCar
    {
        public RoguelikeOffroadVisual visual;
        public Vector3 from;
        public Vector3 islandExit;
        public Vector2 direction;
        public float length;
        public float traveled;
        public float speed;
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
        public AudioHandle engine = AudioHandle.Invalid;
        // 맞힌 나무 → 맞힌 순간의 재사용 번호. 쓰러져 다른 칸의 새 나무로 다시 쓰인 객체는 번호가 달라 다시 칠 수 있다.
        public readonly Dictionary<TreeObj, int> hitTrees = new Dictionary<TreeObj, int>();

        public Vector3 Position => from + (Vector3)(direction * traveled);
    }

    [SerializeField] private RoguelikeSkillTuningSO tuning;

    public RoguelikeSkillTuningSO Tuning => tuning;

    private InDungeonObjectManager objectManager;
    private bool bMarkDamageBonusApplied;
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
    private readonly List<SporeZone> sporeZones = new List<SporeZone>(8);
    private readonly List<OffroadCar> offroadCars = new List<OffroadCar>(4);
    private readonly Stack<OffroadCar> offroadCarPool = new Stack<OffroadCar>(4);
    private readonly Stack<RoguelikeOffroadVisual> offroadVisualPool = new Stack<RoguelikeOffroadVisual>(4);
    private float nextOffroadHitShakeTime;

    private readonly List<Vector3> pendingBoomerangHits = new List<Vector3>(16);
    private readonly List<Vector3> pendingSporeExplosions = new List<Vector3>(16);
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
    private System.Action<IReadOnlyList<Vector3>> droneLaserHandler;

    // 스텔라 V "별을 칠 때 폭발이 2회 연속": 첫 폭발 뒤 이어질 폭발(위치, 시각, 별 번호).
    private struct RepeatBlast
    {
        public Vector3 position;
        public float time;
        public int starId;
    }

    private readonly List<RepeatBlast> pendingRepeatBlasts = new List<RepeatBlast>(8);

    // 포자 연막 틱. 연막끼리 겹친 나무가 받는 지속 피해 중첩 수를 세기 위해 모든 연막이 같은 틱에 판정한다(포자폭탄 V).
    private float nextSporeTickTime;
    private int nextSporeZoneId = 1;
    private readonly Dictionary<TreeObj, int> sporeOverlapCounts = new Dictionary<TreeObj, int>(64);
    private readonly Dictionary<TreeObj, float> sporeTickDamages = new Dictionary<TreeObj, float>(64);
    // 나무마다 그 나무를 덮은 연막 번호의 비트 묶음(유지 시간 증가 MAX가 처치한 연막만 늘리기 위해).
    private readonly Dictionary<TreeObj, int> sporeZoneMasks = new Dictionary<TreeObj, int>(64);
    private readonly List<TreeObj> sporeTargets = new List<TreeObj>(64);

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
    }

    public void Release()
    {
        RunCombatHooks.AxeSwungEvent -= axeSwungHandler;
        RunCombatHooks.BoomerangHitTreeEvent -= boomerangHitHandler;
        RunCombatHooks.DroneLaserEvent -= droneLaserHandler;
        EndCombat();
        SetMarkDamageBonusApplied(false);
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

        SetMarkDamageBonusApplied(false);
        objectManager = _objectManager;
        SetMarkDamageBonusApplied(true);
    }

    /// <summary>
    /// 별 표식 나무의 받는 피해 +30%(기획서 6-1). 표식 나무가 받는 모든 피해(도끼·다른 액티브·지속 피해)는
    /// TreeObj의 별표식 배율 한 곳을 지나므로, 그 배율(본편 "별표식 베기" 특성이 쓰는 누산 값, 기본 1)에 세션 동안만 더한다.
    /// 로그라이크는 본편 능력 트리를 불러오지 않아 이 값은 세션 내내 1 + 보너스이고, Release에서 뺀다.
    /// </summary>
    private void SetMarkDamageBonusApplied(bool _bApply)
    {
        if (null == objectManager || null == tuning || bMarkDamageBonusApplied == _bApply) return;

        appliedMarkDamageBonus = _bApply ? tuning.stellaMarkDamageTakenBonus : appliedMarkDamageBonus;
        objectManager.IncreaseStarMarkDamage(_bApply ? appliedMarkDamageBonus : -appliedMarkDamageBonus);
        bMarkDamageBonusApplied = _bApply;
    }

    /// <summary>섬(타원)의 중심과 반지름(월드). 오프로드가 섬 가장자리에서 반대편 가장자리로 돌진하는 데 쓴다.</summary>
    public void SetField(Vector3 _center, float _radiusX, float _radiusY)
    {
        fieldCenter = _center;
        fieldRadiusX = Mathf.Max(1f, _radiusX);
        fieldRadiusY = Mathf.Max(1f, _radiusY);
    }

    public void BeginCombat()
    {
        bActive = true;
        float now = Time.time;
        nextOffroadTime = now + Interval(tuning.offroadInterval, ERoguelikeSkill.Offroad);
        nextSporeTime = now + Interval(tuning.sporeInterval, ERoguelikeSkill.SporeBomb) * 0.5f;
        nextBeamTime = now + tuning.stellaDroneBeamInterval;
        nextLonePulseTime = now + tuning.stellaDroneLoneStarInterval;
        nextSporeTickTime = now;
        pendingRepeatBlasts.Clear();
        pendingAxeSwings = 0;
        pendingBoomerangHits.Clear();
        pendingSporeExplosions.Clear();
    }

    /// <summary>
    /// 웨이브 종료(_bWaveEnd) 또는 런 종료. 별·연막·표식·돌진을 모두 치운다.
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
        sporeZones.Clear();
        for (int i = offroadCars.Count - 1; i >= 0; i--) ReleaseOffroadCar(i, true);

        pendingStarHits.Clear();
        pendingRepeatBlasts.Clear();
        pendingBoomerangHits.Clear();
        pendingSporeExplosions.Clear();
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

        // 표식·도트 목록에서 뺀다. 이 나무는 곧 풀로 돌아가 다른 나무로 재사용된다.
        for (int i = markedTrees.Count - 1; i >= 0; i--)
        {
            if (markedTrees[i].tree != _tree) continue;

            if (markedTrees[i].bDot && modifiers.IsComboActive(ERoguelikeCombo.StellaSpore))
            {
                pendingSporeExplosions.Add(_position);
            }

            markedTrees.RemoveAt(i);
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
        float radius = tuning.spinBoomerangRadius * stat.axeAttackRangeMultiplier;
        float damage = stat.axeDamage * tuning.spinBoomerangDamageMultiplier;

        DamageTreesInRadius(center, radius, damage, false, false);
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

        Publish(ERoguelikeSkillVfx.StarSpawned, _position, _position, 0f, 0f, star.StarId);

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
        return star;
    }

    /// <summary>
    /// 스텔라 III: 드론 레이저(총구 → 주 대상 → 연쇄 대상)가 지나간 경로 위의 별도 맞은 것으로 친다.
    /// 본편 드론 판정 도중에 불리므로 기록만 하고 터뜨리기는 Update(UpdateStars)에서 한다.
    /// </summary>
    private void OnDroneLaser(IReadOnlyList<Vector3> _points)
    {
        if (false == bActive || null == _points || _points.Count < 2 || 0 == activeStars.Count) return;
        if (false == RunEffectApplier.IsStarHitByActives(modifiers)) return;

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

            // 스텔라 V: 같은 자리에서 폭발이 이어서 더 일어난다(별은 첫 폭발과 함께 사라진다).
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

    /// <summary>별의 단발 공격. 주변 나무에 피해를 주고 별 표식을 남긴다(스텔라+포자폭탄이면 표식에 도트가 붙는다).</summary>
    private void StarBlast(Vector3 _center, float _baseRadius, float _baseDamage)
    {
        float radius = _baseRadius * RangeMul(ERoguelikeSkill.Stella);
        float damage = _baseDamage * DamageMul(ERoguelikeSkill.Stella);
        bool bDot = modifiers.IsComboActive(ERoguelikeCombo.StellaSpore);

        DamageTreesInRadius(_center, radius, damage, true, bDot);
    }

    private void DespawnStar(int _index, bool _bRebuildLinks)
    {
        RoguelikeStar star = activeStars[_index];
        activeStars.RemoveAt(_index);
        star.Remove();
        starPool.Push(star);

        if (_bRebuildLinks) RebuildStarLinks();
    }

    // // 표식·도트

    private void MarkTree(TreeObj _tree, bool _bDot)
    {
        float duration = tuning.stellaMarkDuration + modifiers.GetForSkill(ERunEffect.StarMarkDurationAdd, ERoguelikeSkill.Stella);
        float expire = Time.time + Mathf.Max(0f, duration) * DurationMul(ERoguelikeSkill.Stella);

        for (int i = 0; i < markedTrees.Count; i++)
        {
            if (markedTrees[i].tree != _tree) continue;

            MarkedTree existing = markedTrees[i];
            existing.expireTime = expire;
            existing.bDot |= _bDot;
            markedTrees[i] = existing;
            return;
        }

        _tree.SetStarMarked(true);
        markedTrees.Add(new MarkedTree
        {
            tree = _tree,
            expireTime = expire,
            bDot = _bDot,
            nextDotTime = Time.time + tuning.stellaSporeDotInterval,
        });
    }

    private void UpdateMarks(float _now)
    {
        float dotDamage = tuning.stellaSporeDotDamage * DotMul(ERoguelikeSkill.Stella);

        TreeTileWriteBatch.Begin();
        try
        {
            for (int i = markedTrees.Count - 1; i >= 0; i--)
            {
                if (i >= markedTrees.Count) continue;

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
                    continue;
                }

                if (mark.bDot && _now >= mark.nextDotTime)
                {
                    mark.nextDotTime = _now + tuning.stellaSporeDotInterval;
                    markedTrees[i] = mark;

                    // 이 타격으로 죽으면 OnTreeKilled가 목록에서 빼고 포자 폭발을 예약한다.
                    tree.TakeDamageWithoutHitSound(dotDamage);

                    // 유지 시간 증가 MAX: 지속형 스킬(별 표식 지속 피해)로 처치하면 남은 표식들의 유지 시간이 늘어난다.
                    if (false == IsHittable(tree)) ExtendMarksOnDotKill();
                }
            }
        }
        finally
        {
            TreeTileWriteBatch.End();
        }

        // 스텔라 + 포자폭탄: 도트 중인 나무가 무엇으로 죽었든 포자 폭발. 연쇄는 프레임당 상한까지만.
        int explosions = Mathf.Min(pendingSporeExplosions.Count, Mathf.Max(1, tuning.stellaSporeMaxExplosionsPerFrame));
        for (int i = 0; i < explosions; i++)
        {
            Vector3 pos = pendingSporeExplosions[i];
            float radius = tuning.stellaSporeExplosionRadius * RangeMul(ERoguelikeSkill.SporeBomb);
            DamageTreesInRadius(pos, radius, tuning.stellaSporeExplosionDamage * DamageMul(ERoguelikeSkill.SporeBomb), false, false);
            Publish(ERoguelikeSkillVfx.SporeExplosion, pos, pos, radius, 0f, 0);
        }

        if (explosions > 0) pendingSporeExplosions.RemoveRange(0, explosions);
    }

    // // 포자폭탄

    private void UpdateSpore(float _now)
    {
        if (modifiers.IsSkillActive(ERoguelikeSkill.SporeBomb) && _now >= nextSporeTime)
        {
            nextSporeTime = _now + Interval(tuning.sporeInterval, ERoguelikeSkill.SporeBomb);

            int bombs = 1 + CountAdd(ERoguelikeSkill.SporeBomb);
            for (int i = 0; i < bombs; i++) ThrowSporeBomb(_now);
        }

        for (int i = sporeZones.Count - 1; i >= 0; i--)
        {
            if (_now >= sporeZones[i].endTime) sporeZones.RemoveAt(i);
        }

        if (0 == sporeZones.Count || _now < nextSporeTickTime) return;

        nextSporeTickTime = _now + tuning.sporeTickInterval;
        TickSporeZones();
    }

    /// <summary>
    /// 모든 연막이 같은 틱에 판정한다. 연막이 겹친 나무는 겹친 수만큼 지속 피해가 중첩되며, 중첩은 SporeStackMax까지다
    /// (기본 1 = 겹쳐도 한 번, 포자폭탄 V = 최대 3). 기획서 6-1 포자폭탄 V "연막이 겹치면 지속 피해 중첩".
    /// </summary>
    private void TickSporeZones()
    {
        if (null == CollisionSystem.Instance) return;

        int stackMax = SporeStackMax();
        bool bStarHit = RunEffectApplier.IsStarHitByActives(modifiers);
        float extendOnKill = modifiers.Get(ERunEffect.DurationExtendOnDotKill);

        sporeOverlapCounts.Clear();
        sporeTickDamages.Clear();
        sporeZoneMasks.Clear();
        sporeTargets.Clear();

        for (int z = 0; z < sporeZones.Count; z++)
        {
            SporeZone zone = sporeZones[z];
            CollisionSystem.Instance.GetCollidablesInRadius(zone.position, zone.radius, treeLayer.value, scanBuffer);

            float radiusSq = zone.radius * zone.radius;
            for (int i = 0; i < scanBuffer.Count; i++)
            {
                if (((Vector2)zone.position - scanBuffer[i].Position).sqrMagnitude > radiusSq) continue;

                if (scanBuffer[i] is TreeObj tree)
                {
                    if (false == IsHittable(tree)) continue;

                    int zoneBit = z < 32 ? 1 << z : 0;

                    if (sporeOverlapCounts.TryGetValue(tree, out int count))
                    {
                        sporeOverlapCounts[tree] = count + 1;
                        sporeZoneMasks[tree] |= zoneBit;
                        if (zone.tickDamage > sporeTickDamages[tree]) sporeTickDamages[tree] = zone.tickDamage;
                    }
                    else
                    {
                        sporeOverlapCounts[tree] = 1;
                        sporeZoneMasks[tree] = zoneBit;
                        sporeTickDamages[tree] = zone.tickDamage;
                        sporeTargets.Add(tree);
                    }
                }
                else if (bStarHit && scanBuffer[i] is RoguelikeStar star && star.bCanBeHitBySkill)
                {
                    // 스텔라 III: 연막도 별을 친다.
                    OnStarHit(star);
                }
            }
        }

        TreeTileWriteBatch.Begin();
        try
        {
            for (int i = 0; i < sporeTargets.Count; i++)
            {
                TreeObj tree = sporeTargets[i];
                if (false == IsHittable(tree)) continue;

                int stacks = Mathf.Min(sporeOverlapCounts[tree], stackMax);
                tree.TakeDamage(sporeTickDamages[tree] * stacks, false, false);

                // 유지 시간 증가 MAX: 연막으로 처치하면 그 나무를 덮고 있던 연막의 유지 시간이 늘어난다.
                if (extendOnKill > 0f && false == IsHittable(tree)) ExtendSporeZonesCovering(sporeZoneMasks[tree], extendOnKill);
            }
        }
        finally
        {
            TreeTileWriteBatch.End();
        }

        sporeTargets.Clear();
    }

    /// <summary>_mask에 표시된 연막들의 끝 시각을 _seconds만큼 늦춘다(유지 시간 증가 MAX).</summary>
    private void ExtendSporeZonesCovering(int _mask, float _seconds)
    {
        for (int z = 0; z < sporeZones.Count && z < 32; z++)
        {
            if (0 == (_mask & (1 << z))) continue;

            SporeZone zone = sporeZones[z];
            zone.endTime += _seconds;
            sporeZones[z] = zone;

            // 연막 그림도 같은 만큼 더 피어오르게 한다(판정만 늘고 그림이 먼저 꺼지면 보이지 않는 연막이 피해를 준다).
            Publish(ERoguelikeSkillVfx.SporeZoneExtended, zone.position, zone.position, zone.radius, _seconds, zone.id);
        }
    }

    /// <summary>별 표식 지속 피해로 나무를 처치하면 남아 있는 표식들의 유지 시간을 늘린다(유지 시간 증가 MAX).</summary>
    private void ExtendMarksOnDotKill()
    {
        float seconds = modifiers.Get(ERunEffect.DurationExtendOnDotKill);
        if (seconds <= 0f) return;

        for (int i = 0; i < markedTrees.Count; i++)
        {
            MarkedTree mark = markedTrees[i];
            mark.expireTime += seconds;
            markedTrees[i] = mark;
        }
    }

    private int SporeStackMax() => Mathf.Max(1, 1 + Mathf.RoundToInt(modifiers.GetForSkill(ERunEffect.SporeStackAdd, ERoguelikeSkill.SporeBomb)));

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
        float duration = tuning.sporeDuration * DurationMul(ERoguelikeSkill.SporeBomb);

        int zoneId = nextSporeZoneId++;
        sporeZones.Add(new SporeZone
        {
            id = zoneId,
            position = target,
            radius = radius,
            endTime = _now + duration,
            tickDamage = tuning.sporeTickDamage * DotMul(ERoguelikeSkill.SporeBomb),
        });

        // 모든 연막은 같은 틱 주기로 판정한다. 이미 돌고 있는 주기를 당기면 기존 연막이 간격보다 일찍 한 번 더 피해를 주므로,
        // 다른 연막이 없을 때(주기가 멈춰 있을 때)만 새 연막이 바로 첫 틱을 받게 한다. 그 밖에는 다음 틱(최대 틱 간격 뒤)에 합류한다.
        if (1 == sporeZones.Count) nextSporeTickTime = _now;

        Publish(ERoguelikeSkillVfx.SporeZoneStarted, target, character.transform.position, radius, duration, zoneId);
    }

    // // 오프로드
    //
    // 섬 가장자리 위에 플래시되며 소환(예열) → 확 튀어 나가 가속 → 나무를 들이받을 때마다 멈칫하며 속도를 잃고 다시 붙는다 →
    // 감속 없이 반대편 섬 끝까지 달리고, 끝에 닿기 직전 아주 짧게 하얗게 된 뒤 사라진다(물 위로 나가지 않는다).
    // 진행 방향은 차량 그림에 맞는 아래쪽 대각선 두 방향뿐이다(OffroadLanes).
    // 여러 대(III 2대, V 3대)는 서로 다른 차선에서 시간차를 두고 출발한다.

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
                car.speed = Mathf.MoveTowards(car.speed, tuning.offroadSpeed, tuning.offroadAcceleration * dt);
                // 하얗게 된 뒤에는 끝에서 멈추지 않도록 그대로 달린다. 하얀 시간이 정해져 있어 끝을 넘는 거리는 한 프레임 이동량 이하다(섬 안쪽 들임 거리 안).
                car.traveled = car.bExiting ? car.traveled + car.speed * dt : Mathf.Min(car.length, car.traveled + car.speed * dt);
            }

            Vector3 pos = car.Position;
            car.visual?.SetPose(pos, _now >= car.hitStopUntil ? car.speed : 0f);
            if (car.bEngine) Sound.UpdateTrackedPosition(car.engine, pos);

            int hits = DamageTreesAlongSegment(car.lastPosition, pos, car.radius, car.damage, car.hitTrees);
            if (bStarHit) HitStarsAlongSegment(car.lastPosition, pos, car.radius);
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

    /// <summary>한 번에 _count대를 내보낸다. 차선은 겹치지 않게 섬 폭을 나눠 고르고, 방향(좌·우)은 차마다 무작위다.</summary>
    private void StartOffroadVolley(float _now, int _count)
    {
        int count = Mathf.Max(1, _count);
        bool bFirstRight = Random.value < 0.5f;

        for (int i = 0; i < count; i++)
        {
            // 두 대 이상이면 방향을 번갈아 엇갈리게 해 화면이 한쪽으로 쏠리지 않게 한다.
            bool bRight = 0 == i % 2 ? bFirstRight : false == bFirstRight;
            float delay = tuning.offroadVolleyStagger * i;
            StartOffroad(_now, delay, bRight, i, count);
        }
    }

    private void StartOffroad(float _now, float _delay, bool _bRight, int _laneIndex, int _laneCount)
    {
        Vector2 dir = OffroadLanes.DownDiagonal(_bRight);
        float halfWidth = OffroadLanes.HalfWidth(fieldRadiusX, fieldRadiusY, dir);
        float offset = OffroadLanes.LaneOffset(_laneIndex, _laneCount, halfWidth, tuning.offroadLaneSpread, Random.value);

        if (false == OffroadLanes.TryGetLane(fieldCenter, fieldRadiusX, fieldRadiusY, dir, offset, out Vector2 entry, out Vector2 exit)) return;

        // 섬 테두리 타일 위에서 소환되도록 경계선에서 진행 방향으로 들여온다(차선이 짧으면 차선 길이의 1/4까지만).
        float inset = Mathf.Min(Mathf.Max(0f, tuning.offroadEdgeInset), Vector2.Distance(entry, exit) * 0.25f);
        entry += dir * inset;

        OffroadCar car = 0 < offroadCarPool.Count ? offroadCarPool.Pop() : new OffroadCar();
        car.from = entry;
        car.islandExit = exit;
        car.direction = dir;
        // 반대편도 같은 만큼 안쪽에서 멈춘다(물 위로 나가지 않는다).
        car.length = Mathf.Max(0.5f, Vector2.Distance(entry, exit) - inset);
        car.traveled = 0f;
        car.speed = 0f;
        car.dashStartTime = _now + tuning.offroadWarmup + _delay;
        car.bDashStarted = false;
        car.hitStopUntil = 0f;
        car.bExiting = false;
        car.exitEndTime = 0f;
        car.exitStartFrame = 0;
        car.lastPosition = entry;
        car.damage = tuning.offroadDamage * DamageMul(ERoguelikeSkill.Offroad);
        car.radius = tuning.offroadHitRadius * RangeMul(ERoguelikeSkill.Offroad);
        car.bEngine = 0 == _laneIndex;
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

        float estimate = _car.length / Mathf.Max(0.1f, tuning.offroadSpeed);
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
            DamageTreesInRadius(centroid, radius, tuning.stellaDroneNovaDamage * DamageMul(ERoguelikeSkill.Stella), false, false);
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
            DamageTreesInRadius(pos, radius, tuning.stellaBlastDamage * DamageMul(ERoguelikeSkill.Stella), true, false);
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

    private void DamageTreesInRadius(Vector3 _center, float _radius, float _damage, bool _bMark, bool _bDot, bool _bBatch = true)
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
                tree.TakeDamage(_damage, false, false);
                if (_bMark && IsHittable(tree)) MarkTree(tree, _bDot);
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
    }
}
