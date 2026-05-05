using UnityEngine;
using UnityEngine.Rendering.Universal;

[ExecuteAlways]
public class LightingController : MonoBehaviour, IShadowDataProvider
{
    //외부 의존성
    private ITimeDataProvider timeDataProvider;

    [Header("Point Lights")]
    [SerializeField] private SpritePointLight2D characterPointLightPrefab;

    //내부 의존성
    [Header("Shadow Settings")]
    [SerializeField] private float dayCycleSpeed;
    [SerializeField] private float minHeightScale;
    [SerializeField] private float maxHeightScale;
    [SerializeField] private Material shadowMaterial;
    [SerializeField] private Material buildingShadowMaterial;
    [SerializeField] private Material characterShadowMaterial;
    [SerializeField] private Color shadowColor = new Color(0, 0, 0, 0.5f);
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("Light Settings")]
    [SerializeField] private GlobalSpriteDirectionalLight globalLightPrefab;
    [SerializeField] private Gradient lightColorGradient; // 시간에 따른 빛의 색상
    [SerializeField] private AnimationCurve lightIntensityCurve; // 시간에 따른 빛의 강도

    // 중앙 계산 결과 저장을 위한 프로퍼티
    public Quaternion CurrentShadowRotation => _currentShadowRotation;
    public float CurrentShadowAngle => _currentShadowAngle;
    public float CurrentShadowScaleY => _currentShadowScaleY;
    public bool IsShadowActive => _isShadowActive; // 추가

    private Quaternion _currentShadowRotation;
    private float _currentShadowAngle;
    private float _currentShadowScaleY;
    private bool _isShadowActive; // 추가

    float IShadowDataProvider.currentTimePercent => timeDataProvider?.currentTimePercent ?? 0f;
    float IShadowDataProvider.dayCycleSpeed => dayCycleSpeed;
    float IShadowDataProvider.minHeightScale => minHeightScale;
    float IShadowDataProvider.maxHeightScale => maxHeightScale;

    private GlobalSpriteDirectionalLight globalLight;

    private SpritePointLight2D characterPointLight;
    private ICharacter character;

    public void Initialize(ITimeDataProvider _timeDataProvider)
    {
        timeDataProvider = _timeDataProvider;

        if (globalLightPrefab != null)
        {
            globalLight = Instantiate(globalLightPrefab, transform);
            globalLight.Initialize();
            globalLight.SetCurrentWeather(WeatherType.Normal);
        }

        BindEvents();
    }

    private void BindEvents()
    {

    }

    public void DI(ICharacter _character)
    {
        character = _character;

        if (characterPointLightPrefab != null)
        {
            characterPointLight = Instantiate(characterPointLightPrefab, character.GetTransform());
            characterPointLight.Enable();
        }
    }

    public void EnablePointLights()
    {

    }

    private void Update()
    {
        if (timeDataProvider == null)
            return;

        float timePercent = timeDataProvider.currentTimePercent;

        UpdateShadows(timePercent);
        UpdateLights(timePercent);
    }

    private void FixedUpdate()
    {

    }

    private void UpdateShadows(float _timePercent)
    {
        // 1. 중앙화된 그림자 연산 (모든 Shadow 객체가 공유)
        // 아침 6시(0.25) -> 서쪽(90도), 정오(0.5) -> 남쪽(180도), 저녁 6시(0.75) -> 동쪽(270도)
        // Mathf.Repeat을 사용하여 0~360 범위를 부드럽게 순환시킴
        _currentShadowAngle = Mathf.Repeat(-26 + (_timePercent - 0.25f) * 360f, 360f);
        _currentShadowRotation = Quaternion.Euler(0, 0, _currentShadowAngle);

        // 그림자 길이 연산: Mathf.Abs 대신 Sin^2을 사용하여 0 지점에서 부드러운 감가속 구현
        float timeAngle = _timePercent * Mathf.PI * 2f;
        float sinValue = Mathf.Sin(timeAngle);
        float heightFactor = sinValue * sinValue;
        _currentShadowScaleY = Mathf.Lerp(minHeightScale, maxHeightScale, heightFactor);

        // 2. 머티리얼 알파 페이드 로직
        // 알파 페이드 로직 (0.20 ~ 0.30 일출 페이드 인, 0.70 ~ 0.80 일몰 페이드 아웃)
        float fadeIn = Mathf.InverseLerp(0.20f, 0.30f, _timePercent);
        float fadeOut = 1f - Mathf.InverseLerp(0.70f, 0.80f, _timePercent);
        float finalAlphaMultiplier = fadeIn * fadeOut;

        // 그림자 활성화 여부 (알파가 0보다 크면 활성)
        _isShadowActive = finalAlphaMultiplier > 0.1f;

        Color targetColor = shadowColor;
        targetColor.a *= finalAlphaMultiplier;

        if (shadowMaterial != null)
        {
            shadowMaterial.SetColor(BaseColorId, targetColor);
        }

        if (buildingShadowMaterial != null)
        {
            buildingShadowMaterial.SetColor(BaseColorId, targetColor);
        }

        if (characterShadowMaterial != null)
        {
            characterShadowMaterial.SetColor(BaseColorId, targetColor);
        }
    }

    private void UpdateLights(float _timePercent)
    {
        if (globalLight == null) return;

        globalLight.SetCurrentTimePercent(_timePercent);
    }

    public void WeatherChanged(WeatherType _weatherType)
    {
        globalLight.SetCurrentWeather(_weatherType);
    }
}
