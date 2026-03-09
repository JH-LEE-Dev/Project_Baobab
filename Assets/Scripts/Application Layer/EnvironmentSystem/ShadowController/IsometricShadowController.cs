using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class IsometricShadowController : MonoBehaviour, IShadowDataProvider
{
    //외부 의존성
    private ITimeDataProvider timeDataProvider;

    //내부 의존성
    [Header("Shadow Settings")]
    [SerializeField] private float dayCycleSpeed;
    [SerializeField] private float minHeightScale;
    [SerializeField] private float maxHeightScale;
    [SerializeField] private Material _shadowMaterial;
    [SerializeField] private Color _shadowColor = new Color(0, 0, 0, 0.5f);
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("Light Settings")]
    [SerializeField] private Light2D globalLightPrefab;
    [SerializeField] private Gradient lightColorGradient; // 시간에 따른 빛의 색상
    [SerializeField] private AnimationCurve lightIntensityCurve; // 시간에 따른 빛의 강도

    float IShadowDataProvider.currentTimePercent => timeDataProvider?.currentTimePercent ?? 0f;
    float IShadowDataProvider.dayCycleSpeed => dayCycleSpeed;
    float IShadowDataProvider.minHeightScale => minHeightScale;
    float IShadowDataProvider.maxHeightScale => maxHeightScale;

    private Light2D globalLight;

    public void Initialize(ITimeDataProvider _timeDataProvider)
    {
        timeDataProvider = _timeDataProvider;
        
        if(globalLightPrefab != null)
        {
            globalLight = Instantiate(globalLightPrefab,transform);
        }
    }

    private void Update()
    {
        if (timeDataProvider == null) 
        return;

        float timePercent = timeDataProvider.currentTimePercent;

        UpdateShadows(timePercent);
        UpdateLights(timePercent);
    }

    private void UpdateShadows(float _timePercent)
    {
        if (_shadowMaterial == null) return;

        // 알파 페이드 로직 (0.20 ~ 0.30 일출 페이드 인, 0.70 ~ 0.80 일몰 페이드 아웃)
        float fadeIn = Mathf.InverseLerp(0.20f, 0.30f, _timePercent);
        float fadeOut = 1f - Mathf.InverseLerp(0.70f, 0.80f, _timePercent);
        float finalAlphaMultiplier = fadeIn * fadeOut;
        
        Color targetColor = _shadowColor;
        targetColor.a *= finalAlphaMultiplier;

        _shadowMaterial.SetColor(BaseColorId, targetColor);
    }

    private void UpdateLights(float _timePercent)
    {
        if (globalLight == null) return;

        // Gradient와 AnimationCurve에서 현재 시간에 해당하는 값 추출
        globalLight.color = lightColorGradient.Evaluate(_timePercent);
        globalLight.intensity = lightIntensityCurve.Evaluate(_timePercent);
    }
}
