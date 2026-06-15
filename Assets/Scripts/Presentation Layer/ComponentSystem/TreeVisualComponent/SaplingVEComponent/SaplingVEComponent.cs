using UnityEngine;
using DG.Tweening;

public class SaplingVEComponent : MonoBehaviour
{
    private Transform saplingTransform;

    [SerializeField] private Vector2 startScale;
    [SerializeField] private Vector2 targetScale;

    private Vector3 startScaleVector;
    private Vector3 zeroScaleVector;
    private Vector3 targetScaleVector;

    public void Initialize(Transform _saplingTransform)
    {
        saplingTransform = _saplingTransform;
        startScaleVector = new Vector3(startScale.x, startScale.y, 1f);
        zeroScaleVector = new Vector3(0f, 0f, 1f);
        targetScaleVector = new Vector3(targetScale.x, targetScale.y, 1f);
    }

    public void AnimateSaplingVE(bool _bIsSapling)
    {
        if (saplingTransform == null) return;

        saplingTransform.DOKill();

        if (_bIsSapling == false)
            saplingTransform.localScale = startScaleVector;
        else
            saplingTransform.localScale = zeroScaleVector;

        // 더욱 극적인 스프링 효과를 위해 Amplitude(진동 폭)를 크게 높이고 Period(진동 주기)를 조절
        // 1.0초 동안 매우 탄력 있게 튕기며 목표 스케일로 수렴
        saplingTransform.DOScale(targetScaleVector, 1.0f)
            .SetEase(Ease.OutElastic, 1.7f, 0.3f);
    }
}
