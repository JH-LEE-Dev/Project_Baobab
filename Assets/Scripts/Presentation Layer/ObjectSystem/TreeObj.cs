using System;
using UnityEngine;

public class TreeObj : MonoBehaviour, IDamageable, ITreeObj, IStaticCollidable
{
    public event Action<TreeObj> TreeDeadEvent;
    public event Action<TreeObj> TreeGetHitEvent;

    [SerializeField] private Shadow shadowObject;
    [SerializeField] private Shadow bottomShadowObject;
    [SerializeField] private TreeVisualComponent treeVisualComponent;
    [SerializeField] private float collisionRadius = 0.29f;
    [SerializeField] private Vector2 collisionOffset = Vector2.zero; // 충돌 오프셋 필드 추가


    private IEnvironmentProvider environmentProvider;
    private EHealthComponent healthComponent;
    private Collider2D treeCollider;

    public TreeData treeData { get; private set; }
    public IHealthComponent health => healthComponent;

    private bool bDead = false;
    bool ITreeObj.bDead => bDead;

    // IStaticCollidable 구현
    public Vector2 Position => transform.position;
    public Vector2 Offset => collisionOffset;
    public float Radius => collisionRadius;
    public int Layer => gameObject.layer;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        healthComponent = GetComponent<EHealthComponent>();
        healthComponent.Initialize();

        treeCollider = GetComponent<Collider2D>();
        if (treeCollider != null) treeCollider.enabled = false; // 물리 엔진에서 제외

        if (treeVisualComponent != null)
        {
            treeVisualComponent.Initialize();
        }

        InitializeShadow(shadowObject);
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
        CollisionSystem.Instance?.Unregister(this, true);
    }


    public void ApplyData(TreeData _treeData)
    {
        treeData = _treeData;
        ResetTree();

        if (treeVisualComponent != null)
        {
            treeVisualComponent.ApplyVisual(treeData);
        }
    }

    public void ResetTree()
    {
        bDead = false;
        healthComponent.Reset();

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
        UpdateShadow(shadowObject);
        UpdateShadow(bottomShadowObject);
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

    private void UpdateShadow(Shadow shadow)
    {
        if (shadow == null)
        {
            return;
        }

        shadow.ManualUpdate(
            environmentProvider.shadowDataProvider.CurrentShadowRotation,
            environmentProvider.shadowDataProvider.CurrentShadowScaleY,
            environmentProvider.shadowDataProvider.IsShadowActive
        );
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
        return transform;
    }
}
