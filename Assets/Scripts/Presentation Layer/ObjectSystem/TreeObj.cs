using System;
using System.Collections;
using UnityEngine;

public class TreeObj : MonoBehaviour, IDamageable, ITreeObj, IStaticCollidable
{
    public event Action<TreeObj> TreeDeadEvent;
    public event Action<TreeObj> TreeGetHitEvent;
    public event Action<TreeObj> TreeShieldBrokenEvent;
    public event Action<TreeObj> TreeShieldRecoveringEvent;
    public event Action<TreeObj> TreeHeatEmitEvent;
    // 과열 강화된 ShockWave에 맞았을 때 발생. 실제 폭발 이펙트 생성은 InDungeonObjectManager가
    // 이 이벤트를 구독해서 처리한다(포자막 폭발과 동일한 신호 흐름).
    public event Action<TreeObj> TreeOverheatExplosionEvent;

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
        if (treeVisualComponent != null)
        {
            treeVisualComponent.ApplyGemVisual(_boolean);
        }
    }

    public TreeData treeData { get; private set; }
    public IHealthComponent health => healthComponent;
    IBaseHealthComponent IDamageable.health => healthComponent;

    public bool bDead = false;
    bool ITreeObj.bDead => bDead;

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

    public bool bCanApplyDamage => !bIsSapling;

    public bool bIsSapling = false;
    private float growTime = 0f;
    private float lastDisableTime = 0f;

    // 관리용 인덱스
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;

    private bool bWaterNearBy = false;
    private bool bTreeShadowSet = false;

    // 열기 발산 - 피격 시에만 3~5초 랜덤 타이머가 시작된다. 타이머가 도는 중에 다시 피격되어도
    // 기존 타이머는 그대로 유지되고, 발산이 끝나면 다시 피격 시 재발동 가능한 상태로 돌아간다.
    public Vector3Int CellPos { get; private set; }
    private float heatDamageAmount = 0f;
    private bool bHeatCounting = false;
    private Coroutine heatCoroutine;

    // 과열 버프 중 도끼 평타에 맞았을 때의 지속 피해. 이 나무 자신이 코루틴을 들고 있어야,
    // 나무가 죽어 풀에서 재사용되어도(ResetTree) 엉뚱한 새 나무에 데미지가 잘못 들어가지 않는다.
    private Coroutine overheatDotCoroutine;

    // 과열 버프 중 드론 전이에 맞았을 때의 지속 피해 (평타와 중첩 가능)
    private Coroutine droneOverheatDotCoroutine;

    public void ApplyOverheatDot(float _damagePerTick, int _tickCount, float _tickInterval)
    {
        // 이 타격 자체가 치명타였다면 TakeDamage 안에서 이미 죽어 풀로 반환되어 비활성화된 뒤이므로,
        // 그 상태에서 StartCoroutine을 시도하면 안 된다.
        if (bDead) return;

        if (overheatDotCoroutine != null)
        {
            StopCoroutine(overheatDotCoroutine); // 같은 나무 재타격 시 리셋
        }
        overheatDotCoroutine = StartCoroutine(OverheatDotRoutine(_damagePerTick, _tickCount, _tickInterval, false));
    }

    public void ApplyDroneOverheatDot(float _damagePerTick, int _tickCount, float _tickInterval)
    {
        if (bDead) return;

        if (droneOverheatDotCoroutine != null)
        {
            StopCoroutine(droneOverheatDotCoroutine); // 같은 나무 드론 재타격 시 리셋
        }
        droneOverheatDotCoroutine = StartCoroutine(OverheatDotRoutine(_damagePerTick, _tickCount, _tickInterval, true));
    }

    private IEnumerator OverheatDotRoutine(float _damagePerTick, int _tickCount, float _tickInterval, bool _isDrone)
    {
        for (int i = 0; i < _tickCount; i++)
        {
            yield return new WaitForSeconds(_tickInterval);
            if (!bCanApplyDamage) break;
            TakeDamage(_damagePerTick);
        }
        if (_isDrone)
        {
            droneOverheatDotCoroutine = null;
        }
        else
        {
            overheatDotCoroutine = null;
        }
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
        bReserved = false;
        bLastHitByPlayer = true;
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

        if (treeVisualComponent != null)
        {
            SetOutline(false);
            treeVisualComponent.ResetVisualState();
            // 인스펙터에서 켜 둔 보석 비주얼이 풀 재사용 후에도 유지되도록 마지막에 다시 적용한다.
            // 나중에 스폰별로 보석 여부를 굴린다면, ResetTree가 끝난 뒤에 SetGemVisual을 호출하면 된다.
            treeVisualComponent.ApplyGemVisual(bGemVisual);
        }
    }

    public void TakeDamage(float _damage)
    {
        if (!bCanApplyDamage) return;

        // 죽음 판정 직전 상태를 기억해, 이미 죽은 나무가 정리되기 전 다시 타격당해도
        // TreeDeadEvent가 중복 발생하지 않도록 false->true 전이 시점에만 이벤트를 발생시킨다.
        bool wasAlreadyDead = bDead;

        // 별표식 베기 - 별 표식을 가진 나무에게 배율 적용
        if (bStarMarked)
        {
            _damage *= Mathf.Max(0f, shieldStatProvider?.StarMarkDamageMultiplier ?? 1f);
        }

        healthComponent.DecreaseHealth(_damage);

        if (treeVisualComponent != null)
        {
            treeVisualComponent.PlayHitFeedback();
            treeVisualComponent.PlayHitFlash();
        }

        PlayHitSound();

        TreeGetHitEvent?.Invoke(this);

        if (!wasAlreadyDead && bDead)
        {
            TreeDeadEvent?.Invoke(this);
        }

        // bDead 가드 필수: 이 히트로 나무가 죽었다면 TreeDeadEvent 처리 과정에서 이미 풀로 반환되어
        // gameObject.SetActive(false) 상태이므로, 그 뒤에 StartCoroutine을 시도하면 안 된다.
        TryStartHeatTimer();

        bLastHitByPlayer = true;
    }

    // 도끼 타격음. 나무가 많이 닳을수록(HP가 낮을수록) Tree_Hit은 피치가 1.0 -> 1.3으로,
    // Pitch_Hit은 피치가 1.0 -> 1.6, 볼륨도 함께 1.0 -> 1.4로 올라가 타격감이 누적되는 느낌을 준다.
    private void PlayHitSound()
    {
        float maxHealth = health.GetMaxHealth();
        float damageRatio = maxHealth > 0f ? Mathf.Clamp01(1f - health.GetCurrentHealth() / maxHealth) : 0f;
        float treeHitPitch = Mathf.Lerp(1.0f, 1.3f, damageRatio);
        float pitchHitPitch = Mathf.Lerp(1.0f, 1.6f, damageRatio);
        float pitchHitVolume = Mathf.Lerp(1.0f, 1.4f, damageRatio);

        Sound.Play(SoundID.TreeHit, cachedTransform.position, 1f, true, treeHitPitch);
        Sound.Play(SoundID.PitchHit, cachedTransform.position, pitchHitVolume, true, pitchHitPitch);
    }

    private void TryStartHeatTimer()
    {
        if (bDead || bHeatCounting || heatDamageAmount <= 0f) return;

        bHeatCounting = true;
        heatCoroutine = StartCoroutine(HeatEmitRoutine());
    }

    private IEnumerator HeatEmitRoutine()
    {
        yield return new WaitForSeconds(UnityEngine.Random.Range(3f, 5f));

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
        TreeShieldBrokenEvent?.Invoke(this);
    }

    // 과열 강화된 ShockWave가 이 나무를 때렸을 때 ShockWave가 호출한다. 폭발 이펙트 생성은
    // InDungeonObjectManager가 TreeOverheatExplosionEvent를 받아 처리한다(포자막 폭발과 동일한 방식).
    public void RaiseOverheatExplosion()
    {
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
        bDead = true;
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
