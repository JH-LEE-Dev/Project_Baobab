using UnityEngine;
using System;
using DG.Tweening;


public class TreeObj : MonoBehaviour, IDamageable
{
    //이벤트
    public event Action<TreeObj> TreeDeadEvent;

    //외부 의존성

    [SerializeField] private Shadow shadowObject;
    [SerializeField] private GameObject animatorObject;
    private IEnvironmentProvider environmentProvider;
    private SpriteRenderer sr;

    private TreeData treeData;

    private EHealthComponent healthComponent;


    public void Initialize(IEnvironmentProvider _environmentProvider, TreeData _initData)
    {
        EComponentCtx ctx = new EComponentCtx();

        environmentProvider = _environmentProvider;
        treeData = _initData;

        healthComponent = GetComponentInChildren<EHealthComponent>();
        healthComponent.Initialize();
        sr = animatorObject.GetComponent<SpriteRenderer>();

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

        if (sr != null)
        {
            sr.transform.DOKill();
            sr.transform.localPosition = Vector3.zero;
            sr.transform.DOPunchPosition(new Vector3(0.1f, 0, 0), 0.2f, 15, 1);
        }
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
