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

    //내부 의존성 (캐시)
    private IEnvironmentProvider environmentProvider;
    private SpriteRenderer sr;                // 나무 본체 SpriteRenderer
    private SpriteRenderer shadowSr;          // 그림자 SpriteRenderer
    private SpriteRenderer animatorSr;        // 애니메이터 SpriteRenderer (그림자 동기화용)
    private EHealthComponent healthComponent;
    public TreeData treeData { get; private set; }

    public void Initialize(IEnvironmentProvider _environmentProvider, TreeData _initData)
    {
        environmentProvider = _environmentProvider;
        treeData = _initData;

        healthComponent = GetComponentInChildren<EHealthComponent>();
        healthComponent.Initialize();

        // 컴포넌트 캐싱 (Update에서 호출하지 않도록 미리 할당)
        sr = animatorObject.GetComponent<SpriteRenderer>();
        animatorSr = animatorObject.GetComponentInChildren<SpriteRenderer>();

        if (shadowObject != null)
        {
            shadowSr = shadowObject.GetComponent<SpriteRenderer>();
            shadowObject.Initialize();
        }

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

    public void ManualUpdate()
    {
        // 최적화: 스프라이트가 변경되었을 때만 할당 (포인트 4)
        if (shadowSr != null && animatorSr != null)
        {
            if (shadowSr.sprite != animatorSr.sprite)
            {
                shadowSr.sprite = animatorSr.sprite;
            }
        }

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

    private void OnDestroy()
    {
        ReleaseEvents();
    }

    private void TreeIsDeadEvent()
    {
        TreeDeadEvent?.Invoke(this);
    }
}
