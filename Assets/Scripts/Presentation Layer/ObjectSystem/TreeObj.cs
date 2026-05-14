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
    public Vector2 Position => cachedTransform != null ? (Vector2)cachedTransform.position : (Vector2)transform.position;
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

    // 관리용 인덱스
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;

    private bool bWaterNearBy = false;

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

        if (treeVisualComponent != null)
        {
            treeVisualComponent.Initialize();
        }

        InitializeShadow(topShadowObject);
        InitializeShadow(bottomShadowObject);

        BindEvents();
    }

    private void OnEnable()
    {
        // 정적 객체(나무)로 등록
        CollisionSystem.Instance?.Register(this, true);
    }

    private void OnDisable()
    {
        CollisionSystem.Instance?.Unregister(this);
    }


    public void ApplyData(TreeData _treeData)
    {
        treeData = _treeData;
        ResetTree();

        healthComponent.Setup(treeData.treeStatData.hp);

        if (treeVisualComponent != null)
        {
            treeVisualComponent.ApplyVisual(treeData);
        }
    }

    public void SetIsSapling(bool _bIsSapling, float _growTime)
    {
        bIsSapling = _bIsSapling;
        growTime = _growTime;

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
        bWaterNearBy = false;

        if (treeVisualComponent != null)
        {
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

    public void ManualUpdate()
    {
        // 캐싱된 shadowDataProvider 사용
        float shadowAngle = shadowDataProvider.CurrentShadowAngle;
        float shadowScaleY = shadowDataProvider.CurrentShadowScaleY;
        bool isShadowActive = shadowDataProvider.IsShadowActive;

        if (topShadowObject != null) topShadowObject.ManualUpdate(shadowAngle, shadowScaleY, isShadowActive);
        if (bottomShadowObject != null) bottomShadowObject.ManualUpdate(shadowAngle, shadowScaleY, isShadowActive);

        if (bIsSapling)
        {
            growTime -= Time.deltaTime;
            if (growTime <= 0f)
            {
                bIsSapling = false;

                if (treeVisualComponent != null)
                {
                    if (bWaterNearBy == true)
                        treeVisualComponent.ActivateOnWaterObject();
                    else
                        treeVisualComponent.DeActivateOnWaterObject();

                    // 기존 로직의 결과(항상 마지막에 Activate 호출)를 유지하면서 중복만 제거
                    if (bWaterNearBy == false)
                        treeVisualComponent.ActivateOnWaterObject();

                    treeVisualComponent.ApplyVisual(treeData);
                }

                if (saplingVEComponent != null)
                    saplingVEComponent.AnimateSaplingVE(false);
            }
        }
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
    }

    private void ReleaseEvents()
    {
        if (healthComponent == null)
        {
            return;
        }

        healthComponent.EnemyIsDeadEvent -= TreeIsDead;
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
}
