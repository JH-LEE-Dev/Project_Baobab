using System;
using UnityEngine;

public class TreeObj : MonoBehaviour, IDamageable, ITreeObj, IStaticCollidable
{
    public event Action<TreeObj> TreeDeadEvent;
    public event Action<TreeObj> TreeGetHitEvent;

    [SerializeField] private Shadow topShadowObject;
    [SerializeField] private Shadow bottomShadowObject;
    [SerializeField] private TreeVisualComponent treeVisualComponent;
    [SerializeField] private float collisionRadius = 0.29f;
    [SerializeField] private Vector2 collisionOffset = Vector2.zero; // 충돌 오프셋 필드 추가

    private IEnvironmentProvider environmentProvider;
    private IShadowDataProvider shadowDataProvider;
    private EHealthComponent healthComponent;
    private SaplingVEComponent saplingVEComponent;
    private Transform cachedTransform;

    public TreeData treeData { get; private set; }
    public IHealthComponent health => healthComponent;

    public bool bDead = false;
    bool ITreeObj.bDead => bDead;

    // IStaticCollidable 구현 - 캐싱된 트랜스폼 사용
    public Vector2 Position => (Vector2)cachedTransform.position;
    public Vector2 Offset => collisionOffset;
    public float Radius => collisionRadius;
    public int Layer => gameObject.layer;
    public int EntityIndex { get; set; } = -1;

    [SerializeField] private float alphaDownRadius = 0.5f;
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

    private CustomSortable customSortable;

    //For Shadow
    float shadowAngle;
    float shadowScaleY;
    bool isShadowActive;

    private void Awake()
    {
        cachedTransform = transform;
    }

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        shadowDataProvider = _environmentProvider.shadowDataProvider;
        cachedTransform = transform;

        healthComponent = GetComponent<EHealthComponent>();
        healthComponent.Initialize();

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

        // 에디터 커스텀 모드일 경우 나무의 실질 종류 속성(TreeType)을 에디터 설정값으로 연동
        if (treeVisualComponent != null && treeVisualComponent.bUseCustomColor)
        {
            var modifiedData = treeData;
            modifiedData.type = treeVisualComponent.customTreeType;
            treeData = modifiedData;
        }

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
        }
    }

    public void ResetTree()
    {
        bDead = false;
        healthComponent.Reset();
        bIsSapling = false;
        growTime = 0f;
        lastDisableTime = 0f;
        bWaterNearBy = false;
        bTreeShadowSet = false;

        if (treeVisualComponent != null)
        {
            SetOutline(false);
            treeVisualComponent.ResetVisualState();
        }
    }

    public void TakeDamage(float _damage)
    {
        healthComponent.DecreaseHealth(_damage);

        if (treeVisualComponent != null)
        {
            treeVisualComponent.PlayHitFeedback();
        }

        TreeGetHitEvent?.Invoke(this);

        if (bDead)
            TreeDeadEvent?.Invoke(this);
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

            treeVisualComponent.CacheSwayBasePose();
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
            treeVisualComponent.ActivateOnWaterObject();

            treeVisualComponent.ApplyVisual(treeData);
        }

        if (saplingVEComponent != null)
            saplingVEComponent.AnimateSaplingVE(false);
    }

    public Color GetColor()
    {
        return treeVisualComponent.GetBottomColor();
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
    }

    private void ReleaseEvents()
    {
        if (healthComponent == null)
        {
            return;
        }

        healthComponent.EnemyIsDeadEvent -= TreeIsDead;

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
    }

    public TreeType GetTreeType()
    {
        if (treeVisualComponent != null && treeVisualComponent.bUseCustomColor)
        {
            return treeVisualComponent.customTreeType;
        }
        return treeData.type;
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
}
