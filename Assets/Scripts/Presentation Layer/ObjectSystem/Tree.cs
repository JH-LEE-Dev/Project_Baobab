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

    public void Initialize(IEnvironmentProvider _environmentProvider,TreeInitData _initData)
    {
        environmentProvider = _environmentProvider;
        treeData = _initData;

        shadowObject.Initialize(environmentProvider.shadowDataProvider);
    }

    public void TakeDamage(float _damage)
    {
        TreeDeadEvent?.Invoke(this);
    }

    private void Update()
    {
        if(shadowObject != null)
        {
            shadowObject.GetComponent<SpriteRenderer>().sprite = animatorObject.GetComponentInChildren<SpriteRenderer>().sprite;
        }
    }
}
