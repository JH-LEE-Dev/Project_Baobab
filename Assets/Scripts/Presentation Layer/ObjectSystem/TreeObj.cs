using System;
using System.Collections;
using UnityEngine;

public class TreeObj : MonoBehaviour, IDamageable, ITreeObj, IStaticCollidable, IShadowCaster
{
    public event Action<TreeObj> TreeDeadEvent;
    public event Action<TreeObj> TreeGetHitEvent;
    public event Action<TreeObj> TreeShieldBrokenEvent;
    public event Action<TreeObj> TreeShieldRecoveringEvent;
    public event Action<TreeObj> TreeHeatEmitEvent;
    // 열기 카운트다운이 시작된 순간 발생. float는 방출까지 남은 시간(초)으로, 예고 인디케이터가 이 값을 쓴다.
    public event Action<TreeObj, float> TreeHeatCountdownStartedEvent;
    // 과열 강화된 ShockWave에 맞았을 때 발생. 실제 폭발 이펙트 생성은 InDungeonObjectManager가
    // 이 이벤트를 구독해서 처리한다(포자막 폭발과 동일한 신호 흐름).
    public event Action<TreeObj> TreeOverheatExplosionEvent;
    // 보석 단계로 변한 순간 발생. VFX 생성은 InDungeonObjectManager가 이 이벤트를 받아 처리한다
    // (나무가 풀로 반환되어도 연출이 끊기지 않도록 나무 바깥에서 재생한다).
    public event Action<TreeObj> TreeGemTransformedEvent;
    // HP가 0이 되어 "그냥 죽을지 / 보석 단계로 회생할지"를 가르기 직전에 발생.
    // 구독자가 이 시점에 PromoteGrade로 등급을 올리면 그 나무는 곧바로 보석 단계로 회생한다
    // (데모 황금 나무 보장이 이 틈을 쓴다). 회생 여부만 바꾸므로 드랍/킬 집계는 그대로다.
    public event Action<TreeObj> TreeAboutToDieEvent;

    [SerializeField] private Shadow topShadowObject;
    [SerializeField] private Shadow bottomShadowObject;
    [SerializeField] private TreeVisualComponent _treeVisualComponent;
    public TreeVisualComponent treeVisualComponent => _treeVisualComponent;
    [SerializeField] private float collisionRadius = 0.29f;
    [SerializeField] private Vector2 collisionOffset = Vector2.zero; // 충돌 오프셋 필드 추가

    private IEnvironmentProvider environmentProvider;
    private IShadowDataProvider shadowDataProvider;
    private ISporeShieldStatProvider shieldStatProvider;
    private EHealthComponent healthComponent;
    private SaplingVEComponent saplingVEComponent;
    private Transform cachedTransform;

    // 성장 연출 정점 콜백. 나무는 풀에서 반복 사용되므로 델리게이트를 매번 새로 만들지 않고 캐싱한다.
    private Action cachedGrowUpFlashAction;

    // StarrootForest 별 표식 - Stage3TreeGenerationStrategySO가 스폰 시 부여
    public bool bStarMarked { get; private set; } = false;
    public int StarGroupId { get; private set; } = -1;

    public void SetStarMarked(bool _boolean)
    {
        bStarMarked = _boolean;
        if (treeVisualComponent != null)
        {
            treeVisualComponent.SetConstellationMarkActive(_boolean);
        }
    }

    public void SetStarGroupId(int _groupId)
    {
        StarGroupId = _groupId;
    }

    [Header("Gem Visual")]
    [Tooltip("켜면 이 나무를 보석(결정) 재질로 렌더링한다. 에디터에서 체크하는 즉시 씬 뷰에 반영된다.")]
    [SerializeField] private bool bGemVisual = false;

    // 보석 비주얼의 단일 기준값. 풀에서 재사용될 때 ResetTree가 이 값을 다시 적용한다.
    public bool bIsGem => bGemVisual;

    public void SetGemVisual(bool _boolean)
    {
        bGemVisual = _boolean;
        RefreshGemVisual();
    }

    /// <summary>
    /// 현재 설정으로 보석 비주얼을 다시 적용한다.
    /// 색 데이터를 인스펙터에서 바꿨을 때 이미 스폰된 나무에도 반영하기 위해 외부에서 호출한다.
    /// </summary>
    public void RefreshGemVisual()
    {
        if (treeVisualComponent != null)
        {
            // 보석 색은 나무 등급이 결정한다. treeData는 ApplyData 진입 시점에 이미 갱신되어 있다.
            treeVisualComponent.ApplyGemVisual(bGemVisual, treeData.grade);
        }
    }

    public TreeData treeData { get; private set; }

    /// <summary>
    /// 이미 스폰된 나무의 등급을 올린다. 체력/스탯/비주얼은 건드리지 않으므로,
    /// 실제로 달라지는 것은 회생 가능한 최대 보석 단계(GetMaxGemStage)와 드랍 배율뿐이다.
    /// 등급을 내리는 용도는 아니라서 현재보다 낮거나 같은 등급은 무시한다.
    /// </summary>
    public void PromoteGrade(TreeGrade _grade)
    {
        if (_grade <= treeData.grade) return;

        treeData = new TreeData(treeData.type, _grade, treeData.treeVisualData, treeData.treeStatData);
    }

    public IHealthComponent health => healthComponent;
    IBaseHealthComponent IDamageable.health => healthComponent;

    public bool bDead = false;
    bool ITreeObj.bDead => bDead;

    // 나무 등급별 셰이더 회생 단계 (0: Normal, 1: Gold, 2: Diamond, 3: Rainbow).
    // ResetTree에서 0으로 초기화되며, 등급이 허용하는 최대 단계까지는 HP가 0이 되어도 죽지 않고
    // 회생하며 다음 단계로 전환된다. 최대 단계에서 다시 HP가 0이 되면 그때 실제로 죽는다.
    private int currentGemStage = 0;

    // 현재 보석 단계 (0 = 일반, 1 = 황금, 2 = 다이아, 3 = 무지개).
    public int gemStage => currentGemStage;

    // 지금 이 나무가 보석(황금/다이아/무지개)으로 변해 있는 상태인지.
    // 전용 타격음/사망음 분기에 쓴다. 인스펙터 토글인 bIsGem(bGemVisual)과는 별개다.
    public bool bIsGemStage => currentGemStage > 0;

    [Header("Gem Stage Health")]
    [Tooltip("보석 단계로 회생할 때 체력을 일반 상태 최대 체력의 몇 배로 되살릴지. 순서대로 1단계(황금)/2단계(다이아)/3단계(프리즘).")]
    [SerializeField] private float[] gemStageHealthMultipliers = { 2f, 3f, 3.5f };

    // 해당 보석 단계의 체력 배율. 배열이 비어 있거나 단계가 범위를 벗어나면 1배(기존 동작)로 돌아간다.
    private float GetGemStageHealthMultiplier(int _stage)
    {
        if (gemStageHealthMultipliers == null) return 1f;

        int index = _stage - 1;
        if (index < 0 || index >= gemStageHealthMultipliers.Length) return 1f;

        return gemStageHealthMultipliers[index];
    }

    // 여러 NPC가 같은 나무를 동시에 타겟팅하지 못하도록 하는 예약 플래그
    public bool bReserved { get; set; } = false;

    // IStaticCollidable 구현 - 캐싱된 트랜스폼 사용
    public Vector2 Position => (Vector2)cachedTransform.position;
    public Vector2 Offset => collisionOffset;
    public float Radius => collisionRadius;
    public int Layer => gameObject.layer;
    public int EntityIndex { get; set; } = -1;

    [SerializeField] private float alphaDownRadius = 0.6f;
    [SerializeField] private Vector2 adColliderOffset = new Vector2(0f, 0.9f);

    public float AlphaDownRadius => alphaDownRadius;
    public Vector2 AdColliderOffset => adColliderOffset;

    [SerializeField] private float topShadowRadius = 0.2f;
    [SerializeField] private Vector2 topShadowOffset = new Vector2(0f, 0.7f);

    public Shadow TopShadowObject => topShadowObject;
    public float TopShadowRadius => topShadowRadius;
    public Vector2 TopShadowOffset => topShadowOffset;

    // 나무는 태양 각도에서 유도된 전역 장축 배율을 그대로 사용한다.
    public float ShadowLengthScaleOverride => 0f;

    public bool bCanApplyDamage => !bIsSapling;

    public bool bIsSapling = false;
    private float growTime = 0f;
    private float lastDisableTime = 0f;

    // 관리용 인덱스
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;

    // 이 오브젝트가 현재 풀 안에 들어가 있는지. 이중 반납을 O(1)로 차단하기 위한 플래그로,
    // 풀의 actionOnGet/actionOnRelease에서만 갱신한다. (자세한 배경은 PoolSettings 참조)
    public bool IsPooled { get; set; } = false;

    private bool bWaterNearBy = false;
    private bool bTreeShadowSet = false;

    // 열기 발산 - 피격 시에만 3~5초 랜덤 타이머가 시작된다. 타이머가 도는 중에 다시 피격되어도
    // 기존 타이머는 그대로 유지되고, 발산이 끝나면 다시 피격 시 재발동 가능한 상태로 돌아간다.
    public Vector3Int CellPos { get; private set; }
    private float heatDamageAmount = 0f;
    private bool bHeatCounting = false;
    private Coroutine heatCoroutine;
    // 카운트다운을 시작하거나 끊을(ResetTree) 때마다 1씩 오른다. 예고 인디케이터는 자기가 붙은 카운트다운의
    // 값을 기억해 두었다가 달라지면 사라진다(나무가 풀에서 재사용되어 새 카운트다운을 시작한 경우 포함).
    public int HeatSequence { get; private set; } = 0;

    // 과열 버프 중 도끼 평타에 맞았을 때의 지속 피해. 이 나무 자신이 코루틴을 들고 있어야,
    // 나무가 죽어 풀에서 재사용되어도(ResetTree) 엉뚱한 새 나무에 데미지가 잘못 들어가지 않는다.
    private Coroutine overheatDotCoroutine;
    // 화상 틱 대기 객체. 틱 간격은 호출부 상수(0.5초)라 모든 나무가 하나를 공유한다(간격이 바뀌면 재생성).
    private static WaitForSeconds cachedDotTickWait;
    private static float cachedDotTickWaitInterval = -1f;

    // 과열 버프 중 드론 전이에 맞았을 때의 지속 피해 (평타와 중첩 가능)
    private Coroutine droneOverheatDotCoroutine;

    // 화상(위 두 지속 피해 중 하나라도 돌고 있는 상태)을 보여 주는 푸른 여우불 루프 이펙트. 나무 프리팹의 비활성 자식이고 이 나무가 켜고 끈다.
    [SerializeField] private PresentationLayer.VFX.VFX_TreeBurn burnVfx;

    private void RefreshBurnVfx()
    {
        if (burnVfx == null) return;

        burnVfx.SetBurning(overheatDotCoroutine != null || droneOverheatDotCoroutine != null);
    }

    public void ApplyOverheatDot(float _damagePerTick, int _tickCount, float _tickInterval)
    {
        // 이 타격이 막타였다면 TakeDamage 안에서 이미 죽어 풀로 반환되어 비활성화된 뒤이므로,
        // 그 상태에서 StartCoroutine을 시도하면 안 된다. 묘목은 어떤 상호작용도 받지 않는다.
        //
        // bDead만으로는 걸러지지 않는다. 풀 반환(OnReleaseTree -> ResetTree)이 bDead를 false로 되돌린 뒤라
        // 죽은 나무도 살아 있는 것처럼 보이므로, 풀 상태(IsPooled)와 활성 여부로 함께 판단한다.
        // bDead는 풀을 거치지 않아 죽은 채로 활성 상태에 남은 나무를 거르기 위해 그대로 둔다.
        if (bDead || IsPooled || !gameObject.activeInHierarchy || !bCanApplyDamage) return;

        if (overheatDotCoroutine != null)
        {
            StopCoroutine(overheatDotCoroutine); // 같은 나무 재타격 시 리셋
        }
        overheatDotCoroutine = StartCoroutine(OverheatDotRoutine(_damagePerTick, _tickCount, _tickInterval, false));
        RefreshBurnVfx();
    }

    public void ApplyDroneOverheatDot(float _damagePerTick, int _tickCount, float _tickInterval)
    {
        // ApplyOverheatDot과 같은 이유로 bDead에 더해 풀 상태/활성 여부로도 거른다
        if (bDead || IsPooled || !gameObject.activeInHierarchy || !bCanApplyDamage) return;

        if (droneOverheatDotCoroutine != null)
        {
            StopCoroutine(droneOverheatDotCoroutine); // 같은 나무 드론 재타격 시 리셋
        }
        droneOverheatDotCoroutine = StartCoroutine(OverheatDotRoutine(_damagePerTick, _tickCount, _tickInterval, true));
        RefreshBurnVfx();
    }

    // 화상 틱이 타격음/피격 이펙트를 낼 수 있는 최소 간격(초). 모든 나무가 이 간격 하나를 같이 쓴다.
    // 과열 충격파는 한 번에 여러 그루에 화상을 걸어 틱이 짧은 시간에 몰린다. 타격음(Tree_Hit 0.44초, 동시 재생 상한 6)과
    // 피격 이펙트 풀은 상한에 걸리면 가장 오래된 것을 빼앗아 재생하므로, 틱이 몰리면 실제 도끼 타격의 소리/이펙트가 끊긴다.
    // 0.15초 간격이면 Tree_Hit 한 번이 울리는 동안 화상 틱은 최대 3개라 상한 6 중 절반은 항상 실제 타격 몫으로 남는다.
    // 프레임 단위가 아니라 시간 단위라 프레임레이트와 무관하다. 한 그루만 탈 때는 틱 간격(0.5초)이 더 길어 매 틱 그대로 울린다.
    // 간격 안에 들어온 틱은 소리/이펙트만 생략한다(데미지, 체력바, 피격 플래시, 화상 루프 이펙트는 그대로).
    private const float DotTickFeedbackMinInterval = 0.15f;
    private static float lastDotTickFeedbackTime = float.NegativeInfinity;

    // 이번 피격에서 피격 이펙트를 생략할지. 간격에 걸린 화상 틱의 TakeDamageInternal 동안에만 켜지며,
    // InDungeonObjectManager.OnTreeHit가 TreeGetHitEvent를 받을 때 읽는다.
    public bool bSkipHitVfx { get; private set; } = false;

    private static bool TryConsumeDotTickFeedback()
    {
        float now = Time.time;

        // now < last는 플레이 모드를 다시 시작해 Time.time이 0부터 다시 흐르는데 static 값이 남은 경우다
        // (도메인 리로드를 끈 에디터 설정). 이전 세션 값 때문에 피드백이 막히지 않도록 그대로 허용한다.
        if (now >= lastDotTickFeedbackTime && now - lastDotTickFeedbackTime < DotTickFeedbackMinInterval) return false;

        lastDotTickFeedbackTime = now;
        return true;
    }

    private IEnumerator OverheatDotRoutine(float _damagePerTick, int _tickCount, float _tickInterval, bool _isDrone)
    {
        if (cachedDotTickWait == null || cachedDotTickWaitInterval != _tickInterval)
        {
            cachedDotTickWait = new WaitForSeconds(_tickInterval);
            cachedDotTickWaitInterval = _tickInterval;
        }
        WaitForSeconds tickWait = cachedDotTickWait;

        for (int i = 0; i < _tickCount; i++)
        {
            yield return tickWait;
            if (!bCanApplyDamage) break;

            bool bPlayFeedback = TryConsumeDotTickFeedback();
            bSkipHitVfx = !bPlayFeedback;
            // 드론 레이저가 건 지속 피해는 도끼로 맞은 게 아니므로 도끼 타격음을 내지 않는다
            TakeDamageInternal(_damagePerTick, false, false, !_isDrone && bPlayFeedback);
            bSkipHitVfx = false;
        }
        if (_isDrone)
        {
            droneOverheatDotCoroutine = null;
        }
        else
        {
            overheatDotCoroutine = null;
        }

        RefreshBurnVfx(); // 두 지속 피해가 모두 끝났다면 화상 이펙트가 꺼지는 연출을 시작한다
    }

    public void SetCellPos(Vector3Int _cellPos)
    {
        CellPos = _cellPos;
    }

    public float HeatDamageAmount => heatDamageAmount;

    public void SetHeatDamageAmount(float _amount)
    {
        heatDamageAmount = _amount;
    }

    private CustomSortable customSortable;

    //For Shadow
    float shadowAngle;
    float shadowScaleY;
    bool isShadowActive;

    private void Awake()
    {
        cachedTransform = transform;
    }

    public void Initialize(IEnvironmentProvider _environmentProvider, ISporeShieldStatProvider _shieldStatProvider = null)
    {
        environmentProvider = _environmentProvider;
        shadowDataProvider = _environmentProvider.shadowDataProvider;
        shieldStatProvider = _shieldStatProvider;
        cachedTransform = transform;

        healthComponent = GetComponent<EHealthComponent>();
        healthComponent.Initialize(_shieldStatProvider);

        cachedGrowUpFlashAction = PlayGrowUpFlash;

        saplingVEComponent = GetComponentInChildren<SaplingVEComponent>();
        if (saplingVEComponent != null)
        {
            saplingVEComponent.Initialize(treeVisualComponent.transform);
        }

        InitializeShadow(topShadowObject);
        InitializeShadow(bottomShadowObject);

        customSortable = GetComponent<CustomSortable>();

        if (treeVisualComponent != null)
        {
            treeVisualComponent.Initialize(topShadowObject.transform, customSortable);
        }

        BindEvents();
    }

    private void OnEnable()
    {
        // 정적 객체(나무)로 등록
        CollisionSystem.Instance?.Register(this, true);

        if (bIsSapling && lastDisableTime > 0f)
        {
            growTime -= (Time.time - lastDisableTime);
            lastDisableTime = 0f;

            if (growTime <= 0f)
            {
                GrowUp();
            }
        }
    }

    private void OnDisable()
    {
        CollisionSystem.Instance?.Unregister(this);

        if (bIsSapling)
        {
            lastDisableTime = Time.time;
        }
    }


    public void ApplyData(TreeData _treeData)
    {
        treeData = _treeData;

        ResetTree();

        healthComponent.Setup(treeData.type, treeData.treeStatData.hp, treeData.treeStatData.sp, treeData.treeStatData.spRegen, treeData.treeStatData.regenStrategy);

        if (treeVisualComponent != null)
        {
            treeVisualComponent.ApplyVisual(treeData);
        }

        shadowAngle = shadowDataProvider.CurrentShadowAngle;
        shadowScaleY = shadowDataProvider.CurrentShadowScaleY;
        isShadowActive = shadowDataProvider.IsShadowActive;
    }

    public void SetIsSapling(bool _bIsSapling, float _growTime)
    {
        bIsSapling = _bIsSapling;
        growTime = _growTime;

        if (bIsSapling && !gameObject.activeInHierarchy)
        {
            lastDisableTime = Time.time;
        }

        if (bIsSapling && treeVisualComponent != null)
        {
            treeVisualComponent.DeActivateOnWaterObject();
            treeVisualComponent.ApplySaplingVisual(treeData);
            saplingVEComponent.AnimateSaplingVE(true);
            Sound.Play(SoundID.TreeSmallGrow, cachedTransform.position);
        }
    }

    [HideInInspector] public bool bLastHitByPlayer = true;

    public void ResetTree()
    {
        bDead = false;
        currentGemStage = 0;
        bReserved = false;
        bLastHitByPlayer = true;
        bSkipHitVfx = false;
        SetStarMarked(false);
        SetStarGroupId(-1);
        healthComponent.Reset();
        bIsSapling = false;
        growTime = 0f;
        lastDisableTime = 0f;
        bWaterNearBy = false;
        bTreeShadowSet = false;

        // 카운트다운 도중 사망/재사용되는 경우를 포함해 항상 정리한다 (ResetTree는 스폰 시/사망 시 모두 호출됨).
        if (heatCoroutine != null)
        {
            StopCoroutine(heatCoroutine);
            heatCoroutine = null;
        }
        bHeatCounting = false;
        HeatSequence++;

        if (overheatDotCoroutine != null)
        {
            StopCoroutine(overheatDotCoroutine);
            overheatDotCoroutine = null;
        }

        if (droneOverheatDotCoroutine != null)
        {
            StopCoroutine(droneOverheatDotCoroutine);
            droneOverheatDotCoroutine = null;
        }

        // 지속 피해를 끊은 만큼 화상 이펙트도 연출 없이 즉시 끈다(풀 재사용 시 새 나무에 남지 않게)
        if (burnVfx != null) burnVfx.StopImmediate();

        if (treeVisualComponent != null)
        {
            SetOutline(false);
            treeVisualComponent.ResetVisualState();
            // 인스펙터에서 켜 둔 보석 비주얼이 풀 재사용 후에도 유지되도록 마지막에 다시 적용한다.
            // 나중에 스폰별로 보석 여부를 굴린다면, ResetTree가 끝난 뒤에 SetGemVisual을 호출하면 된다.
            treeVisualComponent.ApplyGemVisual(bGemVisual, treeData.grade);
        }
    }

    public void TakeDamage(float _damage)
    {
        TakeDamageInternal(_damage, true, false);
    }

    /// <summary>
    /// 타격/파괴 진동(TreeImpact/TreeDestroy)을 호출부가 직접 관리하고 싶을 때 쓴다. 부메랑처럼 자동으로
    /// 계속 들어오는 공격이 도끼용 묵직한 진동을 매번 울리지 않고 자기 전용 진동을 쓰기 위한 용도다.
    /// (사운드/이펙트는 TakeDamage와 동일하게 그대로 재생된다)
    /// 치명타 여부(_bCritical)를 넘기면 피격 플래시가 흰색 대신 빨간색으로 번쩍인다.
    /// </summary>
    public void TakeDamage(float _damage, bool _bPlayHaptic, bool _bCritical = false)
    {
        TakeDamageInternal(_damage, _bPlayHaptic, _bCritical);
    }

    /// <summary>
    /// 드론 레이저 타격. 레이저는 발사와 동시에 맞으므로 드론이 발사음을 직접 내고, 여기서는 도끼 타격음
    /// (Tree_Hit/Pitch_Hit 등)을 내지 않는다. 진동/이펙트는 TakeDamage와 동일하다.
    /// </summary>
    public void TakeDamageFromDrone(float _damage)
    {
        TakeDamageInternal(_damage, true, false, false);
    }

    private void TakeDamageInternal(float _damage, bool _bPlayHaptic, bool _bCritical, bool _bPlayHitSound = true)
    {
        if (!bCanApplyDamage) return;

        // 죽음 판정 직전 상태를 기억해, 이미 죽은 나무가 정리되기 전 다시 타격당해도
        // TreeDeadEvent가 중복 발생하지 않도록 false->true 전이 시점에만 이벤트를 발생시킨다.
        bool wasAlreadyDead = bDead;

        // 이 타격이 보석 전환을 일으키면 DecreaseHealth 안에서 currentGemStage가 먼저 올라간다.
        // 타격음은 "맞는 순간 보석이었는지"로 갈라져야 하므로 전환 전 상태를 미리 기억해 둔다.
        bool wasGemBeforeHit = bIsGemStage;
        int gemStageBeforeHit = currentGemStage;

        // 포자막 타격음도 "맞는 순간 포자막이 있었는지"로 갈라진다. 이 타격으로 포자막이 깨지면
        // DecreaseHealth 안에서 SP가 0이 되므로 역시 미리 기억해 둔다.
        bool hadShieldBeforeHit = healthComponent.GetCurrentSP() > 0f;

        // 별표식 베기 - 별 표식을 가진 나무에게 배율 적용
        if (bStarMarked)
        {
            _damage *= Mathf.Max(0f, shieldStatProvider?.StarMarkDamageMultiplier ?? 1f);
        }

        healthComponent.DecreaseHealth(_damage);

        if (treeVisualComponent != null)
        {
            treeVisualComponent.PlayHitFeedback();
            treeVisualComponent.PlayHitFlash(_bCritical);
        }

        if (_bPlayHitSound)
        {
            PlayHitSound(wasGemBeforeHit, gemStageBeforeHit, hadShieldBeforeHit);
        }

        // 진동은 플레이어가 때린 경우에만 낸다. 벌목 NPC가 베는 것까지 울리면 아무것도 안 하고
        // 서 있어도 패드가 계속 떤다. (bLastHitByPlayer는 LumberjackNPC가 때리기 직전에 false로
        // 내려두고, 이 함수 끝에서 다시 true로 돌아간다)
        //
        // 도끼 평타와 쇼크웨이브 모두 이 함수를 지나므로 EHapticEvent.TreeImpact 하나로 묶인다.
        // 한 번 휘두른 결과로 여러 그루가 맞아도 Rumble이 한 번의 사건으로 묶어 한 번만 울린다.
        if (_bPlayHaptic && bLastHitByPlayer)
        {
            Rumble.Play(EHapticEvent.TreeImpact);
        }

        TreeGetHitEvent?.Invoke(this);

        // 이 타격으로 죽었는지는 반드시 여기서 확정해 둔다. 아래 TreeDeadEvent를 타면 풀 반환
        // (OnTreeDead -> TryReleaseTree -> OnReleaseTree -> ResetTree)이 bDead를 false로
        // 되돌려버리므로, 이벤트가 끝난 뒤에 bDead를 다시 읽으면 "죽지 않았다"로 보인다.
        bool bDiedThisHit = (false == wasAlreadyDead && true == bDead);

        if (bDiedThisHit)
        {
            // TreeDeadEvent를 타면 나무가 풀로 반환되며 ResetTree()가 bLastHitByPlayer를 true로
            // 되돌리므로, 판정은 반드시 이벤트를 쏘기 전에 끝내야 한다.
            if (_bPlayHaptic && bLastHitByPlayer)
            {
                Rumble.Play(EHapticEvent.TreeDestroy);
            }

            TreeDeadEvent?.Invoke(this);
        }

        // 이 히트로 죽었다면 위에서 이미 풀로 반환되어 gameObject가 비활성이므로,
        // 여기서 StartCoroutine을 시도하면 안 된다.
        //
        // 예전엔 이 판단을 TryStartHeatTimer() 안의 bDead 가드에 맡겼는데, 그 시점엔 ResetTree()가
        // bDead와 bHeatCounting을 둘 다 false로 되돌린 뒤라 가드 세 개 중 둘이 이미 무너져 있었다.
        // 마지막 하나인 heatDamageAmount는 ResetTree가 건드리지 않아 그 맵의 값이 그대로 남으므로,
        // 열기 수치가 0인 맵(Stage1~3)에서만 우연히 조기 반환에 걸려 조용했을 뿐이다.
        // 열기가 켜진 맵에서는 벌목할 때마다 비활성 오브젝트에 코루틴을 걸려 시도하게 된다.
        if (false == bDiedThisHit)
        {
            TryStartHeatTimer();
        }

        bLastHitByPlayer = true;
    }

    // 도끼 타격음. 나무가 많이 닳을수록(HP가 낮을수록) Tree_Hit은 피치가 1.0 -> 1.3으로,
    // Pitch_Hit은 피치가 1.0 -> 1.6, 볼륨도 함께 1.0 -> 1.4로 올라가 타격감이 누적되는 느낌을 준다.
    //
    // 보석 나무는 Pitch_Hit 자리에 전용 사운드(Pitch_Hit_Mine)를 대신 재생하며,
    // 보석 단계에 따라 피치 범위가 1.0~2.0 구간으로 나뉘어 누적 상승한다 (1: 황금 1~1.33, 2: 다이아 1.33~1.66, 3: 프리즘 1.66~2.0).
    // 피치/볼륨 계산과 함께 울리는 Tree_Hit은 일반 나무와 동일하게 유지한다.
    //
    // 포자막이 있는 상태로 맞으면 위 나무 타격음(Tree_Hit, Pitch_Hit/Pitch_Hit_Mine)은 피치 로직을 그대로 둔 채
    // 볼륨만 0.7배로 줄이고, 그 위에 Spore_Hit을 얹는다. Spore_Hit은 체력 대신 포자막이 깎인 비율로
    // Pitch_Hit과 같은 곡선(1.0 -> 1.6)을 따라 올라가며, 이 타격으로 포자막이 깨졌으면 최고 피치로 친다.
    private const float ShieldedTreeHitVolumeMul = 0.7f;

    private void PlayHitSound(bool _bGemTree, int _gemStage, bool _bHadShield)
    {
        float maxHealth = health.GetMaxHealth();
        // 이번 타격으로 보석 단계가 전환되어 체력이 즉시 풀피로 회복되었거나 완전히 죽은 경우,
        // 해당 타격은 체력이 0에 도달한 최종 타격이므로 damageRatio를 1.0f(최고 피치)로 설정한다.
        bool isTransformedOrDead = (_gemStage != currentGemStage || true == bDead);
        float damageRatio = (true == isTransformedOrDead)
            ? 1.0f
            : (maxHealth > 0f ? Mathf.Clamp01(1f - health.GetCurrentHealth() / maxHealth) : 0f);

        float treeHitPitch = Mathf.Lerp(1.0f, 1.3f, damageRatio);
        float treeHitVolumeMul = _bHadShield ? ShieldedTreeHitVolumeMul : 1f;
        float pitchHitVolume = Mathf.Lerp(1.0f, 1.4f, damageRatio) * treeHitVolumeMul;

        Sound.Play(SoundID.TreeHit, cachedTransform.position, treeHitVolumeMul, true, treeHitPitch);

        if (_bHadShield)
        {
            float maxSP = healthComponent.GetMaxSP();
            float currentSP = healthComponent.GetCurrentSP();
            float shieldDamageRatio = (currentSP <= 0f || maxSP <= 0f) ? 1.0f : Mathf.Clamp01(1f - currentSP / maxSP);

            Sound.Play(SoundID.SporeHit, cachedTransform.position, 1f, true, Mathf.Lerp(1.0f, 1.6f, shieldDamageRatio));
        }

        if (true == _bGemTree)
        {
            float gemPitch;
            switch (_gemStage)
            {
                case 1: // 황금 (Gold / Fascinating)
                    gemPitch = Mathf.Lerp(1.0f, 1.33f, damageRatio);
                    break;
                case 2: // 다이아 (Diamond / Advanced)
                    gemPitch = Mathf.Lerp(1.33f, 1.66f, damageRatio);
                    break;
                case 3: // 프리즘 (Rainbow / Perfect)
                    gemPitch = Mathf.Lerp(1.66f, 2.0f, damageRatio);
                    break;
                default:
                    gemPitch = Mathf.Lerp(1.0f, 2.0f, damageRatio);
                    break;
            }

            Sound.Play(SoundID.PitchHitMine, cachedTransform.position, pitchHitVolume, true, gemPitch);
        }
        else
        {
            float pitchHitPitch = Mathf.Lerp(1.0f, 1.6f, damageRatio);
            Sound.Play(SoundID.PitchHit, cachedTransform.position, pitchHitVolume, true, pitchHitPitch);
        }
    }

    private void TryStartHeatTimer()
    {
        if (bDead || bHeatCounting || heatDamageAmount <= 0f) return;

        bHeatCounting = true;
        HeatSequence++;
        heatCoroutine = StartCoroutine(HeatEmitRoutine());
    }

    private IEnumerator HeatEmitRoutine()
    {
        float delay = UnityEngine.Random.Range(3f, 5f);
        TreeHeatCountdownStartedEvent?.Invoke(this, delay);

        yield return new WaitForSeconds(delay);

        TreeHeatEmitEvent?.Invoke(this);

        bHeatCounting = false;
        heatCoroutine = null;
    }

    public bool ManualUpdate()
    {
        if (bIsSapling)
        {
            growTime -= Time.deltaTime;
            if (growTime <= 0f)
            {
                GrowUp();
            }
        }

        if (bTreeShadowSet == false)
        {
            if (topShadowObject != null) topShadowObject.ManualUpdate(shadowAngle, shadowScaleY, isShadowActive);
            if (bottomShadowObject != null) bottomShadowObject.ManualUpdate(shadowAngle, shadowScaleY, isShadowActive);

            bTreeShadowSet = true;
        }

        // 묘목 상태이거나 그림자 설정이 아직 끝나지 않았다면 계속 Update가 필요함
        return bIsSapling || !bTreeShadowSet;
    }

    private void GrowUp()
    {
        bIsSapling = false;

        if (treeVisualComponent != null)
        {
            // 기존 로직(bWaterNearBy 값에 관계 없이 결국 항상 ActivateOnWaterObject를 수행)의 비주얼 동작을 동일하게 유지하며
            // 불필요한 이중 조건 연산과 중복 활성/비활성 호출 부하만 제거합니다.
            if (bWaterNearBy == true)
                treeVisualComponent.ActivateOnWaterObject();

            treeVisualComponent.ApplyVisual(treeData);
        }

        if (saplingVEComponent != null)
        {
            saplingVEComponent.AnimateSaplingVE(false, cachedGrowUpFlashAction);
            Sound.Play(SoundID.TreeBigGrow, cachedTransform.position);
        }
    }

    // 성장 스케일 연출이 최대에 도달하는 순간 피격과 동일한 하얀 플래시를 한 번 터뜨린다.
    private void PlayGrowUpFlash()
    {
        if (treeVisualComponent != null)
        {
            treeVisualComponent.PlayGrowUpFlash();
        }
    }

    public Color GetColor()
    {
        return Color.white;
    }

    private void InitializeShadow(Shadow shadow)
    {
        if (shadow != null)
        {
            shadow.Initialize();
        }
    }

    private void BindEvents()
    {
        if (healthComponent == null)
        {
            return;
        }

        healthComponent.EnemyIsDeadEvent -= TreeIsDead;
        healthComponent.EnemyIsDeadEvent += TreeIsDead;

        healthComponent.ShieldBrokenEvent -= treeVisualComponent.ShieldBroken;
        healthComponent.ShieldBrokenEvent += treeVisualComponent.ShieldBroken;

        healthComponent.ShieldRegenedEvent -= treeVisualComponent.ShieldRegened;
        healthComponent.ShieldRegenedEvent += treeVisualComponent.ShieldRegened;

        healthComponent.ShieldBrokenEvent -= OnShieldBroken;
        healthComponent.ShieldBrokenEvent += OnShieldBroken;

        healthComponent.ShieldRecoveringEvent -= OnShieldRecovering;
        healthComponent.ShieldRecoveringEvent += OnShieldRecovering;
    }

    private void OnShieldBroken()
    {
        // 파괴 VFX(InDungeonObjectManager.OnTreeShieldBroken)와 마찬가지로 포자막 폭발 스킬/맵 여부와 무관하게 항상 재생한다.
        Sound.Play(SoundID.SporeShieldBreak, cachedTransform.position);

        TreeShieldBrokenEvent?.Invoke(this);
    }

    // 과열 강화된 ShockWave가 이 나무를 때렸을 때 ShockWave가 호출한다. 폭발 이펙트 생성은
    // InDungeonObjectManager가 TreeOverheatExplosionEvent를 받아 처리한다(포자막 폭발과 동일한 방식).
    public void RaiseOverheatExplosion()
    {
        if (!bCanApplyDamage) return; // 묘목은 어떤 상호작용도 받지 않는다

        TreeOverheatExplosionEvent?.Invoke(this);
    }

    private void OnShieldRecovering()
    {
        TreeShieldRecoveringEvent?.Invoke(this);
    }

    private void ReleaseEvents()
    {
        if (healthComponent == null)
        {
            return;
        }

        healthComponent.EnemyIsDeadEvent -= TreeIsDead;
        healthComponent.ShieldBrokenEvent -= OnShieldBroken;
        healthComponent.ShieldRecoveringEvent -= OnShieldRecovering;

        if (treeVisualComponent != null)
        {
            healthComponent.ShieldBrokenEvent -= treeVisualComponent.ShieldBroken;
            healthComponent.ShieldRegenedEvent -= treeVisualComponent.ShieldRegened;
        }
    }

    private void OnDestroy()
    {
        ReleaseEvents();
        CollisionSystem.Instance?.Unregister(this);
    }

    private void TreeIsDead()
    {
        // 회생 단계를 계산하기 전에 알린다. 구독자가 등급을 올려주면 아래 GetMaxGemStage()가
        // 그 등급을 바로 반영한다.
        TreeAboutToDieEvent?.Invoke(this);

        int maxGemStage = GetMaxGemStage();
        if (currentGemStage < maxGemStage)
        {
            currentGemStage++;
            healthComponent.ReviveFullHealth(GetGemStageHealthMultiplier(currentGemStage));

            if (treeVisualComponent != null)
            {
                treeVisualComponent.ApplyGemVisual(true, GemStageToVirtualGrade(currentGemStage));
            }

            // 보석 나무로 변하는 순간의 전용 연출음/이펙트.
            Sound.Play(SoundID.TreeTransformation, cachedTransform.position);
            TreeGemTransformedEvent?.Invoke(this);
            return;
        }

        bDead = true;
        if (burnVfx != null) burnVfx.StopImmediate(); // 죽은 나무에는 화상 이펙트가 남지 않는다
    }

    // 이 나무의 등급이 회생으로 도달할 수 있는 최대 셰이더 단계.
    private int GetMaxGemStage()
    {
        return Mathf.Clamp((int)treeData.grade - (int)TreeGrade.Normal, 0, 3);
    }

    // 셰이더 단계를 ApplyGemVisual이 받는 등급 값으로 변환한다.
    // (TreeGemColorDataBase의 등급->보석종류 매핑을 그대로 재사용: Fascinating=Gold, Advanced=Diamond, Perfect=Rainbow)
    private TreeGrade GemStageToVirtualGrade(int _stage)
    {
        switch (_stage)
        {
            case 1: return TreeGrade.Fascinating;
            case 2: return TreeGrade.Advanced;
            case 3: return TreeGrade.Perfect;
            default: return TreeGrade.None;
        }
    }

    public Transform GetTransform()
    {
        return cachedTransform;
    }

    public void KnockBack(Vector2 _knockBackDir, float _knockBackForce)
    {

    }

    public void SetAlpha(float _alpha)
    {
        if (treeVisualComponent != null)
        {
            treeVisualComponent.SetAlpha(_alpha);
        }
    }

    public void FadeAlpha(float _targetAlpha, float _duration)
    {
        if (treeVisualComponent != null)
        {
            treeVisualComponent.FadeAlpha(_targetAlpha, _duration);
        }
    }

    public void SetOnWaterObjectState(bool _isWaterNearby)
    {
        if (treeVisualComponent == null)
            return;

        bWaterNearBy = _isWaterNearby;

        if (bIsSapling == false)
        {
            if (bWaterNearBy == true)
                treeVisualComponent.ActivateOnWaterObject();
            else
                treeVisualComponent.DeActivateOnWaterObject();
        }
    }

    public void SetOutline(bool _boolean)
    {
        if (treeVisualComponent != null)
        {
            treeVisualComponent.SetOutline(_boolean);
        }
    }

    public void SetSortOrder()
    {
        customSortable.ManualLateUpdate();
        treeVisualComponent.UpdateOnWaterSortingOrder();
        treeVisualComponent.UpdateSortingOrder();
    }

    public TreeType GetTreeType()
    {
        return treeData.type;
    }

    public TreeType GetCustomTreeType()
    {
        return treeVisualComponent.customTreeType;
    }

    public bool BTreeShadowSet
    {
        get => bTreeShadowSet;
        set => bTreeShadowSet = value;
    }

    public void DisableOutline()
    {
        treeVisualComponent.DisableOutline();
    }

    public void EnableOutline()
    {
        treeVisualComponent.EnableOutline();
    }

#if UNITY_EDITOR
    // 인스펙터에서 Gem Visual 체크박스를 토글하면 플레이 중이 아니어도 씬 뷰에 즉시 반영한다.
    private void OnValidate()
    {
        if (treeVisualComponent == null) return;

        treeVisualComponent.ApplyGemVisual(bGemVisual);

        // ApplyGemVisual은 교체 전 원본 머티리얼을 TreeVisualComponent에 기록해 두는데,
        // OnValidate에서의 변경은 명시적으로 dirty 처리하지 않으면 씬에 저장되지 않는다.
        // 저장이 안 되면 다음 재컴파일 후 원본을 몰라 체크를 해제해도 되돌릴 수 없다.
        UnityEditor.EditorUtility.SetDirty(treeVisualComponent);
    }

    [ContextMenu("Update All Trees In Scene")]
    public void UpdateAllTreesInScene()
    {
        TreeObj[] trees = FindObjectsByType<TreeObj>(FindObjectsInactive.Exclude);
        int updatedCount = 0;
        foreach (var tree in trees)
        {
            if (tree.treeVisualComponent != null)
            {
                tree.treeVisualComponent.RefreshVisualPreview();
                UnityEditor.EditorUtility.SetDirty(tree.treeVisualComponent);
                updatedCount++;
            }
        }
        Debug.Log($"Updated {updatedCount} trees in the scene based on CustomType.");
    }
#endif
}
