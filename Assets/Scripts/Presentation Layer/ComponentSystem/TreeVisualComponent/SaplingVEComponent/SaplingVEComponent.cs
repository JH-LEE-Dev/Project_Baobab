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

    public void AnimateSaplingVE(bool _bIsSapling)
    {
        if (saplingTransform == null) return;

        saplingTransform.DOKill();

        if (_bIsSapling == false)
            saplingTransform.localScale = new Vector3(startScale.x, startScale.y, 1f);
        else
            saplingTransform.localScale = new Vector3(0, 0, 1f);

        // 더욱 극적인 스프링 효과를 위해 Amplitude(진동 폭)를 크게 높이고 Period(진동 주기)를 조절
        // 1.0초 동안 매우 탄력 있게 튕기며 목표 스케일로 수렴
        saplingTransform.DOScale(new Vector3(targetScale.x, targetScale.y, 1f), 1.0f)
            .SetEase(Ease.OutElastic, 1.7f, 0.3f);
    }
}
