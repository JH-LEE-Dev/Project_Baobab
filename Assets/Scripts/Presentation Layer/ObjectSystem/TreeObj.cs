using System;
using UnityEngine;

public class TreeObj : MonoBehaviour, IDamageable, ITreeObj
{
    public event Action<TreeObj> TreeDeadEvent;
    public event Action<TreeObj> TreeGetHitEvent;

    [SerializeField] private Shadow shadowObject;
    [SerializeField] private TreeVisualComponent treeVisualComponent;

    private IEnvironmentProvider environmentProvider;
    private EHealthComponent healthComponent;

    public TreeData treeData { get; private set; }
    public IHealthComponent health => healthComponent;

    private bool bDead = false;
    bool ITreeObj.bDead => bDead;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        healthComponent = GetComponentInChildren<EHealthComponent>();
        healthComponent.Initialize();

        if (treeVisualComponent != null)
        {
            treeVisualComponent.Initialize();
        }

        if (shadowObject != null)
        {
            shadowObject.Initialize();
        }

        BindEvents();
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
        if (shadowObject != null)
        {
            shadowObject.ManualUpdate(
                environmentProvider.shadowDataProvider.CurrentShadowRotation,
                environmentProvider.shadowDataProvider.CurrentShadowScaleY,
                environmentProvider.shadowDataProvider.IsShadowActive
            );
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
