using UnityEngine;
using DG.Tweening;

public class SaplingVEComponent : MonoBehaviour
{
    private Transform saplingTransform;

    [SerializeField] private Vector2 startScale;
    [SerializeField] private Vector2 targetScale;

    public void Initialize(Transform _saplingTransform)
    {
        saplingTransform = _saplingTransform;
    }

    public void AnimateSaplingVE()
    {
        if (saplingTransform == null) return;

        if (saplingTransform != null)
        {
            saplingTransform.localScale = new Vector3(startScale.x, startScale.y, 1f);
        }

        saplingTransform.DOKill();
        saplingTransform.localScale = new Vector3(startScale.x, startScale.y, 1f);

        // 빠르지만 부드럽게 키운 후에 spring damper 처럼 스케일이 진동하면서 targetscale로 수렴
        // Ease.OutElastic을 사용하여 스프링 효과 구현
        saplingTransform.DOScale(new Vector3(targetScale.x, targetScale.y, 1f), 0.75f)
            .SetEase(Ease.OutElastic, 1.0f, 0.5f);
    }
}
