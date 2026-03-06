using UnityEngine;

[ExecuteAlways]
public class IsometricShadowController : MonoBehaviour, IShadowDataProvider
{
    //외부 의존성
    private ITimeDataProvider timeDataProvider;

    //내부 의존성
    [SerializeField] private float dayCycleSpeed;
    [SerializeField] private float minHeightScale;
    [SerializeField] private float maxHeightScale;
    [SerializeField] private Material _shadowMaterial;
    [SerializeField] private Color _shadowColor = new Color(0, 0, 0, 0.5f);
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    float IShadowDataProvider.currentTimePercent => timeDataProvider?.currentTimePercent ?? 0f;
    float IShadowDataProvider.dayCycleSpeed => dayCycleSpeed;
    float IShadowDataProvider.minHeightScale => minHeightScale;
    float IShadowDataProvider.maxHeightScale => maxHeightScale;

    public void Initialize(ITimeDataProvider _timeDataProvider)
    {
        timeDataProvider = _timeDataProvider;
    }

    private void Update()
    {
        if (_shadowMaterial == null || timeDataProvider == null)
        {
            return;
        }

        float timePercent = timeDataProvider.currentTimePercent;

        // 알파 페이드 로직 (0.25=일출, 0.75=일몰)
        // 0.20 ~ 0.30 구간: 일출 페이드 인
        // 0.70 ~ 0.80 구간: 일몰 페이드 아웃
        float fadeIn = Mathf.InverseLerp(0.20f, 0.30f, timePercent);
        float fadeOut = 1f - Mathf.InverseLerp(0.70f, 0.80f, timePercent);
        
        // 최종 알파 결정
        float finalAlphaMultiplier = fadeIn * fadeOut;
        
        Color targetColor = _shadowColor;
        targetColor.a *= finalAlphaMultiplier;

        // 재질의 전역 색상 업데이트
        _shadowMaterial.SetColor(BaseColorId, targetColor);
    }
}
