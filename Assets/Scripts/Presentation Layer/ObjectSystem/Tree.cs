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

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

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
