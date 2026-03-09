using UnityEngine;
using System;


public class TreeObj : MonoBehaviour, IDamageable
{
    //이벤트
    public event Action<TreeObj> TreeDeadEvent;

    //외부 의존성

    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;
    private IEnvironmentProvider environmentProvider;

    private TreeInitData treeData;

    private EHealthComponent healthComponent;


    public void Initialize(IEnvironmentProvider _environmentProvider, TreeInitData _initData)
    {
        EComponentCtx ctx = new EComponentCtx();

        environmentProvider = _environmentProvider;
        treeData = _initData;

        healthComponent = GetComponentInChildren<EHealthComponent>();
        healthComponent.Initialize();

        shadowObject.Initialize(environmentProvider.shadowDataProvider);

        BindEvents();
    }

    public void ResetTree()
    {
        healthComponent.Reset();
    }

    public void TakeDamage(float _damage)
    {
        healthComponent.DecreaseHealth(_damage);
    }

    private void Update()
    {
        if (shadowObject != null)
        {
            shadowObject.GetComponent<SpriteRenderer>().sprite = animatorObject.GetComponentInChildren<SpriteRenderer>().sprite;
        }
    }

    private void BindEvents()
    {
        if (healthComponent == null)
            return;

        healthComponent.EnemyIsDeadEvent -= TreeIsDeadEvent;
        healthComponent.EnemyIsDeadEvent += TreeIsDeadEvent;
    }

    private void ReleaseEvents()
    {
        if (healthComponent == null)
            return;

        healthComponent.EnemyIsDeadEvent -= TreeIsDeadEvent;
    }

    private void Oestroy()
    {
        ReleaseEvents();
    }

    private void TreeIsDeadEvent()
    {
        TreeDeadEvent?.Invoke(this);
    }
}
