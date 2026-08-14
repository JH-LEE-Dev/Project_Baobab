using UnityEngine;
using UnityEngine.UI;
using NaughtyAttributes;

/// <summary>
/// [UI 전용] 희귀 아이템 등장 방사형 회전 오오라(URP2D_ItemRadialAura) 이펙트를 제어하는
/// 단발성(One-Shot) 버스트 컨트롤러 컴포넌트의 UI Canvas 버전입니다.
/// 기존 <see cref="ItemAuraEffectController"/>와 동일한 셰이더 파라미터를 사용하며,
/// Renderer/MaterialPropertyBlock 대신 <see cref="UnityEngine.UI.Image"/>의 인스턴스 머티리얼을 직접 제어합니다.
/// </summary>
public class UI_ItemAuraEffectController : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Image targetImage;

    // 설정 파라미터
    [Header("단발성 버스트 애니메이션")]
    [SerializeField] private float burstDuration = 1.2f;
    [SerializeField] private float targetIntensity = 1.0f;
    [SerializeField] private Vector3 maxScale = new Vector3(5f, 5f, 1f);
    [SerializeField] private bool playOnAwake = false;

    [Header("픽셀 아트 / 픽셀 퍼펙트 설정")]
    [SerializeField, Tooltip("체크 시 픽셀 그리드에 맞춰 스냅 및 컬러 밴딩 적용 (해제 시 부드러운 아날로그 스타일)")]
    private bool enablePixelStyle = true;
    [SerializeField, Range(8f, 128f), ShowIf("enablePixelStyle"), Tooltip("게임 픽셀 해상도 (기본 32 PPU)")]
    private float pixelResolution = 32f;
    [SerializeField, Range(1f, 16f), ShowIf("enablePixelStyle"), Tooltip("컬러 밴딩 단계 (4: 레트로 도트 4단계 밴딩)")]
    private float colorBandingSteps = 4f;

    [Header("블룸 및 HDR 발광 강도 (Bloom Intensity)")]
    [SerializeField, Range(0.5f, 10f), Tooltip("URP Post Processing Bloom 발광 증폭 배율")]
    private float bloomIntensity = 1.5f;

    [Header("회전 속도 제어 (-20.0 ~ 20.0 고속 스핀 지원)")]
    [SerializeField, Range(-20f, 20f)] private float rotationSpeed = 0.6f;
    [SerializeField, Range(0f, 1f)] private float speedVariation = 0.2f;

    [Header("부채꼴 광선 커스텀 오버라이드 (체크 시 머티리얼 값 재정의)")]
    [SerializeField] private bool overrideRaySettings = true;
    [SerializeField, Range(1f, 32f), ShowIf("overrideRaySettings")] private float rayCount = 8f;
    [SerializeField, Range(0f, 0.8f), ShowIf("overrideRaySettings")] private float angleJitter = 0.45f;
    [SerializeField, Range(0.01f, 1.0f), ShowIf("overrideRaySettings")] private float beamBlur = 0.22f;
    [SerializeField, Range(0.04f, 0.35f), ShowIf("overrideRaySettings")] private float minBeamWidth = 0.04f;
    [SerializeField, Range(0.15f, 0.8f), ShowIf("overrideRaySettings")] private float maxBeamWidth = 0.4f;

    [Header("디버그 및 테스트 GUI")]
    [SerializeField] private bool showOnScreenDebugGui = false;

    // 런타임 상태
    private Material instanceMaterial;
    private float elapsedTime = 0f;
    private bool isPlaying = false;

    /// <summary>
    /// 인스펙터에 세팅된 단발성 버스트의 재생 시간(수명)입니다.
    /// </summary>
    public float BurstDuration => burstDuration;

    // 셰이더 프로퍼티 ID 캐싱은 ItemAuraShaderHelper로 위임됨

    // ========================================================================
    // 1. 퍼블릭 제어 및 초기화 메서드
    // ========================================================================

    /// <summary>
    /// 외부에서 Image를 주입하여 초기화합니다.
    /// </summary>
    public void Initialize(Image _targetImage)
    {
        targetImage = _targetImage;
        EnsureInstanceMaterial();
        transform.localScale = maxScale;
        ApplyBurstProgress(0f);
        ApplyIntensity(0f);
        ApplyAllSettings();
    }

    /// <summary>
    /// 단발성(One-Shot) 오오라 버스트 이펙트를 재생합니다.
    /// </summary>
    [Button("▶ Play One-Shot Aura (단발 재생)")]
    public void Play()
    {
        EnsureInstanceMaterial();
        isPlaying = true;
        elapsedTime = 0f;

        transform.localScale = maxScale;

        if (null != targetImage)
            targetImage.enabled = true;

        ApplyIntensity(targetIntensity);
        ApplyAllSettings();
        ApplyBurstProgress(0f);
    }

    /// <summary>
    /// 오오라 이펙트를 즉시 정지합니다.
    /// </summary>
    [Button("⏹ Stop Immediate (즉시 정지)")]
    public void Stop()
    {
        isPlaying = false;
        elapsedTime = 0f;
        ApplyIntensity(0f);
        ApplyBurstProgress(0f);

        if (null != targetImage)
            targetImage.enabled = false;
    }

    /// <summary>
    /// 실시간으로 픽셀 퍼펙트 모드 및 해상도/밴딩을 설정합니다.
    /// </summary>
    public void SetPixelStyle(bool _enable, float _resolution = 32f, float _banding = 4f)
    {
        enablePixelStyle = _enable;
        pixelResolution = _resolution;
        colorBandingSteps = _banding;
        ApplyPixelSettings();
    }

    /// <summary>
    /// 실시간으로 블룸 발광 증폭 배율을 설정합니다.
    /// </summary>
    public void SetBloomIntensity(float _intensity)
    {
        bloomIntensity = Mathf.Max(0.1f, _intensity);
        ApplyBloomSettings();
    }

    /// <summary>
    /// 실시간으로 기준 회전 속도 및 광선별 속도 편차를 변경합니다.
    /// </summary>
    public void SetRotationSpeed(float _speed, float _speedVariation = 0.45f)
    {
        rotationSpeed = _speed;
        speedVariation = _speedVariation;
        ApplyRotationSettings(rotationSpeed, speedVariation);
    }

    /// <summary>
    /// 실시간으로 블러 강도를 변경합니다 (0.01: 초선명 ~ 1.0: 몽환적 안개).
    /// </summary>
    public void SetBeamBlur(float _blur)
    {
        beamBlur = _blur;
        overrideRaySettings = true;
        ApplyRayOverrides();
    }

    /// <summary>
    /// 실시간으로 정확한 광선 개수(1~32), 지터 및 굵기를 설정합니다.
    /// </summary>
    public void SetRayParameters(float _rayCount, float _angleJitter, float _minWidth, float _maxWidth, float _blur = 0.22f)
    {
        rayCount = _rayCount;
        angleJitter = _angleJitter;
        minBeamWidth = _minWidth;
        maxBeamWidth = _maxWidth;
        beamBlur = _blur;
        overrideRaySettings = true;
        ApplyRayOverrides();
    }

    /// <summary>
    /// 실시간으로 오오라의 코어 및 빔 색상을 변경합니다.
    /// </summary>
    public void SetColors(Color _coreColor, Color _beamColor)
    {
        EnsureInstanceMaterial();
        if (null == instanceMaterial)
            return;

        instanceMaterial.SetColor(ItemAuraShaderHelper.CoreColorPropertyId, _coreColor);
        instanceMaterial.SetColor(ItemAuraShaderHelper.BeamColorPropertyId, _beamColor);
    }

    // ========================================================================
    // 2. 프라이빗 내부 헬퍼 메서드
    // ========================================================================

    /// <summary>
    /// Image.material 접근 시 Unity가 자동으로 인스턴스 머티리얼을 생성합니다.
    /// 이 메서드는 그 참조를 캐싱하여 반복 호출 비용을 줄입니다.
    /// </summary>
    private void EnsureInstanceMaterial()
    {
        if (null == targetImage)
            targetImage = GetComponent<Image>();

        if (null == targetImage || null == targetImage.material)
            return;

        if (null == instanceMaterial)
        {
#if UNITY_EDITOR
            if (false == Application.isPlaying)
            {
                instanceMaterial = targetImage.material;
                return;
            }
#endif
            // UGUI의 Image.material은 자동 인스턴스화되지 않으므로 런타임에서 직접 복제하여 사용합니다.
            instanceMaterial = new Material(targetImage.material);
            targetImage.material = instanceMaterial;
        }
    }

    private void ApplyAllSettings()
    {
        if (null == instanceMaterial)
            return;

        ItemAuraShaderHelper.ApplyPixelSettings(instanceMaterial, enablePixelStyle, pixelResolution, colorBandingSteps);
        instanceMaterial.SetFloat(ItemAuraShaderHelper.BloomMultiplierPropertyId, bloomIntensity);
        instanceMaterial.SetFloat(ItemAuraShaderHelper.RotationSpeedPropertyId, rotationSpeed);
        instanceMaterial.SetFloat(ItemAuraShaderHelper.SpeedVariationPropertyId, speedVariation);

        if (true == overrideRaySettings)
        {
            ItemAuraShaderHelper.ApplyRayOverrides(instanceMaterial, rayCount, angleJitter, beamBlur, minBeamWidth, maxBeamWidth);
        }
        ForceUIRenderUpdate();
    }

    private void ApplyPixelSettings()
    {
        if (null == instanceMaterial)
            return;

        ItemAuraShaderHelper.ApplyPixelSettings(instanceMaterial, enablePixelStyle, pixelResolution, colorBandingSteps);
        ForceUIRenderUpdate();
    }

    private void ApplyBloomSettings()
    {
        if (null == instanceMaterial)
            return;

        instanceMaterial.SetFloat(ItemAuraShaderHelper.BloomMultiplierPropertyId, bloomIntensity);
        ForceUIRenderUpdate();
    }

    private void ApplyRotationSettings(float _speed, float _variation)
    {
        if (null == instanceMaterial)
            return;

        instanceMaterial.SetFloat(ItemAuraShaderHelper.RotationSpeedPropertyId, _speed);
        instanceMaterial.SetFloat(ItemAuraShaderHelper.SpeedVariationPropertyId, _variation);
        ForceUIRenderUpdate();
    }

    private void ApplyRayOverrides()
    {
        if (false == overrideRaySettings || null == instanceMaterial)
            return;

        ItemAuraShaderHelper.ApplyRayOverrides(instanceMaterial, rayCount, angleJitter, beamBlur, minBeamWidth, maxBeamWidth);
        ForceUIRenderUpdate();
    }

    private void ApplyBurstProgress(float _progress)
    {
        if (null == instanceMaterial)
            return;

        instanceMaterial.SetFloat(ItemAuraShaderHelper.BurstProgressPropertyId, _progress);
        ForceUIRenderUpdate();
    }

    private void ApplyIntensity(float _intensity)
    {
        if (null == instanceMaterial)
            return;

        instanceMaterial.SetFloat(ItemAuraShaderHelper.IntensityPropertyId, _intensity);
        ForceUIRenderUpdate();
    }

    /// <summary>
    /// UGUI Canvas 배칭 최적화를 우회하고 즉시 렌더링 갱신을 강제합니다.
    /// 머티리얼 속성만 변할 경우 화면이 안 그려지는 버그를 해결합니다.
    /// </summary>
    private void ForceUIRenderUpdate()
    {
        if (null != targetImage)
        {
            targetImage.SetMaterialDirty();
            targetImage.SetVerticesDirty();
        }
    }

    private void UpdateBurst(float _deltaTime)
    {
        elapsedTime += _deltaTime;
        float _progress = 0f < burstDuration ? Mathf.Clamp01(elapsedTime / burstDuration) : 1f;

        ApplyBurstProgress(_progress);

        if (1f <= _progress)
        {
            isPlaying = false;
            ApplyIntensity(0f);

            if (null != targetImage)
                targetImage.enabled = false;
        }
    }

    // ========================================================================
    // 3. 유니티 라이프사이클 이벤트 함수
    // ========================================================================

    private void Awake()
    {
        EnsureInstanceMaterial();
        transform.localScale = maxScale;
        ApplyAllSettings();

        if (false == playOnAwake)
        {
            ApplyIntensity(0f);
            ApplyBurstProgress(0f);

            if (null != targetImage)
                targetImage.enabled = false;
        }
    }

    private void Start()
    {
        if (true == playOnAwake)
            Play();
    }

    private void Update()
    {
        if (false == isPlaying)
            return;

        UpdateBurst(Time.deltaTime);
    }

    private void OnValidate()
    {
        EnsureInstanceMaterial();
        transform.localScale = maxScale;
        ApplyAllSettings();
    }

    private void OnGUI()
    {
        if (false == showOnScreenDebugGui)
            return;

        GUILayout.BeginArea(new Rect(20, 20, 240, 170), GUI.skin.box);
        GUILayout.Label("✨ UI Item Aura (One-Shot)");
        GUILayout.Label($"Pixel Style: {(enablePixelStyle ? "ON (32 PPU)" : "OFF (Smooth)")}");
        GUILayout.Label($"Bloom Mult: {bloomIntensity:F1}");

        if (true == GUILayout.Button("▶ Play One-Shot Burst", GUILayout.Height(30)))
            Play();

        if (true == GUILayout.Button("⏹ Stop Immediate", GUILayout.Height(20)))
            Stop();

        if (true == GUILayout.Button($"Toggle Pixel ({(!enablePixelStyle ? "Enable" : "Disable")})", GUILayout.Height(20)))
        {
            enablePixelStyle = !enablePixelStyle;
            ApplyPixelSettings();
        }

        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        if (null != instanceMaterial)
        {
#if UNITY_EDITOR
            if (false == Application.isPlaying)
            {
                // 에디터 상태에서는 원본 에셋이므로 파괴하지 않고 참조만 해제합니다.
                instanceMaterial = null;
                targetImage = null;
                return;
            }
#endif
            // GC 발생을 줄이기 위해 DestroyImmediate 사용 (사용자 규칙)
            DestroyImmediate(instanceMaterial);
            instanceMaterial = null;
        }

        targetImage = null;
    }
}
