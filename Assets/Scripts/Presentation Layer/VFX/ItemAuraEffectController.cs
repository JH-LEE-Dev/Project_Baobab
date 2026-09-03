using UnityEngine;
using NaughtyAttributes;

/// <summary>
/// 희귀 아이템 등장 방사형 회전 오오라(URP2D_ItemRadialAura) 이펙트를 제어하는 단발성(One-Shot) 버스트 컨트롤러 컴포넌트입니다.
/// Play 호출 시 외곽으로 갈수록 넓어지는 부채꼴(Fan Shape) 빛줄기들이 불규칙한 각도로 생성된 후, 각 광선마다 개별 속도로 회전하며 은은한 알파로 자연스럽게 소멸됩니다.
/// 인스펙터에서 픽셀 퍼펙트(32 PPU 도트 스타일 vs 부드러운 아날로그), 블룸 발광 강도, 블러 및 회전 속도를 직접 커스텀할 수 있습니다.
/// </summary>
public class ItemAuraEffectController : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Renderer targetRenderer;

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
    [SerializeField, Range(0f, 10f), Tooltip("URP Post Processing Bloom 발광 증폭 배율")]
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

    [Header("색상 커스텀 오버라이드 (체크 시 머티리얼 색상 재정의)")]
    [SerializeField] private bool overrideColors = false;
    [SerializeField, ColorUsage(true, true), ShowIf("overrideColors")] private Color coreColor = new Color(3.5f, 3.2f, 2.0f, 1.0f);
    [SerializeField, ColorUsage(true, true), ShowIf("overrideColors")] private Color beamColor = new Color(2.5f, 1.8f, 0.3f, 1.0f);
    [SerializeField, ColorUsage(true, true), ShowIf("overrideColors")] private Color outerColor = new Color(1.2f, 0.5f, 0.05f, 1.0f);

    [Header("프리즘 / 무지개 모드 (광선별 무지개 색상 분광 연출)")]
    [SerializeField, Tooltip("체크 시 광선마다 고유한 무지개/프리즘 색상 분광 연출 적용")]
    private bool enablePrismMode = false;
    [SerializeField, Range(0f, 2f), ShowIf("enablePrismMode"), Tooltip("프리즘 채도/선명도")]
    private float prismSaturation = 1.0f;
    [SerializeField, Range(0f, 5f), ShowIf("enablePrismMode"), Tooltip("프리즘 색상 회전/시프트 속도 (0: 고정 무지개, >0: 회전 무지개)")]
    private float prismSpeed = 0.5f;
    [SerializeField, Range(0f, 1f), ShowIf("enablePrismMode"), Tooltip("프리즘 시작 색상 오프셋")]
    private float prismHueOffset = 0.0f;

    [Header("랜덤 시작 각도 및 인스턴스 불규칙성 (다중 인스턴스 동시 재생 시 다양성 부여)")]
    [SerializeField, Tooltip("재생 시 고유 랜덤 시드 및 시작 각도 할당")] private bool randomizeOnPlay = true;
    [SerializeField, Tooltip("재생 시 회전 방향을 랜덤으로 결정 (시계 / 반시계)")] private bool randomizeRotationDirection = true;

    [Header("디버그 및 테스트 GUI")]
    [SerializeField] private bool showOnScreenDebugGui = false;

    // 런타임 상태
    private MaterialPropertyBlock propertyBlock;
    private float elapsedTime = 0f;
    private bool isPlaying = false;
    private float currentRandomSeed = 0f;
    private float currentStartAngle = 0f;
    private float currentRotationSpeed = 0.6f;

    /// <summary>
    /// 인스펙터에 세팅된 단발성 버스트의 재생 시간(수명)입니다.
    /// </summary>
    public float BurstDuration => burstDuration;

    // 셰이더 프로퍼티 ID 캐싱은 ItemAuraShaderHelper로 위임됨

    // ========================================================================
    // 1. 퍼블릭 제어 및 초기화 메서드
    // ========================================================================

    /// <summary>
    /// 외부에서 렌더러를 주입하여 초기화합니다.
    /// </summary>
    public void Initialize(Renderer _targetRenderer)
    {
        targetRenderer = _targetRenderer;
        currentRotationSpeed = rotationSpeed;
        EnsurePropertyBlock();
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
        EnsurePropertyBlock();
        isPlaying = true;
        elapsedTime = 0f;

        transform.localScale = maxScale;

        if (true == randomizeOnPlay)
        {
            currentRandomSeed = Random.Range(0.0f, 1000.0f);
            currentStartAngle = Random.Range(0.0f, 360.0f);
            if (true == randomizeRotationDirection)
            {
                currentRotationSpeed = rotationSpeed * (Random.value > 0.5f ? 1.0f : -1.0f);
            }
            else
            {
                currentRotationSpeed = rotationSpeed;
            }
        }
        else
        {
            currentRandomSeed = 0f;
            currentStartAngle = 0f;
            currentRotationSpeed = rotationSpeed;
        }

        if (null != targetRenderer)
        {
            targetRenderer.enabled = true;
        }

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

        if (null != targetRenderer)
        {
            targetRenderer.enabled = false;
        }
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
        bloomIntensity = Mathf.Max(0f, _intensity);
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
    /// 실시간으로 렌더러의 소팅 오더를 변경합니다 (소팅 레이어는 그대로 유지).
    /// </summary>
    public void SetSortingOrder(int _order)
    {
        EnsurePropertyBlock();
        if (null != targetRenderer)
        {
            targetRenderer.sortingOrder = _order;
        }
    }

    /// <summary>
    /// 정렬 레이어를 지정합니다.
    /// 프리팹 기본값은 "Default" 레이어인데, 이 프로젝트는 Default를 정렬 레이어 목록 맨 뒤에만
    /// 두고 있어 그대로 두면 다른 오브젝트에 전부 가려집니다. 붙는 대상과 같은 레이어로 맞춰야 합니다.
    /// </summary>
    public void SetSortingLayer(int _sortingLayerId)
    {
        if (null != targetRenderer)
        {
            targetRenderer.sortingLayerID = _sortingLayerId;
        }
    }

    /// <summary>
    /// 실시간으로 오오라의 코어, 빔, 외곽 글로우 색상을 변경합니다.
    /// </summary>
    public void SetColors(Color _coreColor, Color _beamColor, Color _outerColor)
    {
        overrideColors = true;
        coreColor = _coreColor;
        beamColor = _beamColor;
        outerColor = _outerColor;
        ApplyColorSettings();
    }

    /// <summary>
    /// 실시간으로 오오라의 코어 및 빔 색상을 변경합니다 (기존 아우터 컬러 유지).
    /// </summary>
    public void SetColors(Color _coreColor, Color _beamColor)
    {
        overrideColors = true;
        coreColor = _coreColor;
        beamColor = _beamColor;
        ApplyColorSettings();
    }

    /// <summary>
    /// 실시간으로 광선별 프리즘(무지개) 분광 모드를 활성화 또는 비활성화합니다.
    /// </summary>
    public void SetPrismMode(bool _enable, float _saturation = 1.0f, float _speed = 0.5f, float _hueOffset = 0.0f)
    {
        enablePrismMode = _enable;
        prismSaturation = _saturation;
        prismSpeed = _speed;
        prismHueOffset = _hueOffset;
        ApplyPrismSettings();
    }

    // ========================================================================
    // 2. 프라이빗 내부 헬퍼 메서드
    // ========================================================================

    private void EnsurePropertyBlock()
    {
        if (null == propertyBlock)
        {
            propertyBlock = new MaterialPropertyBlock();
        }

        if (null == targetRenderer)
        {
            targetRenderer = GetComponent<Renderer>();
        }
    }

    private void ApplyAllSettings()
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);

        ItemAuraShaderHelper.ApplyPixelSettings(propertyBlock, enablePixelStyle, pixelResolution, colorBandingSteps);
        ItemAuraShaderHelper.ApplyRandomness(propertyBlock, currentRandomSeed, currentStartAngle);
        propertyBlock.SetFloat(ItemAuraShaderHelper.BloomMultiplierPropertyId, bloomIntensity);
        propertyBlock.SetFloat(ItemAuraShaderHelper.RotationSpeedPropertyId, currentRotationSpeed);
        propertyBlock.SetFloat(ItemAuraShaderHelper.SpeedVariationPropertyId, speedVariation);

        if (true == overrideRaySettings)
        {
            ItemAuraShaderHelper.ApplyRayOverrides(propertyBlock, rayCount, angleJitter, beamBlur, minBeamWidth, maxBeamWidth);
        }

        if (true == overrideColors)
        {
            ItemAuraShaderHelper.ApplyColorSettings(propertyBlock, coreColor, beamColor, outerColor);
        }

        ItemAuraShaderHelper.ApplyPrismSettings(propertyBlock, enablePrismMode, prismSaturation, prismSpeed, prismHueOffset);

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyColorSettings()
    {
        if (false == overrideColors || null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        ItemAuraShaderHelper.ApplyColorSettings(propertyBlock, coreColor, beamColor, outerColor);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyPrismSettings()
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        ItemAuraShaderHelper.ApplyPrismSettings(propertyBlock, enablePrismMode, prismSaturation, prismSpeed, prismHueOffset);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyPixelSettings()
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        ItemAuraShaderHelper.ApplyPixelSettings(propertyBlock, enablePixelStyle, pixelResolution, colorBandingSteps);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyBloomSettings()
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(ItemAuraShaderHelper.BloomMultiplierPropertyId, bloomIntensity);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyRotationSettings(float _speed, float _variation)
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(ItemAuraShaderHelper.RotationSpeedPropertyId, _speed);
        propertyBlock.SetFloat(ItemAuraShaderHelper.SpeedVariationPropertyId, _variation);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyRayOverrides()
    {
        if (false == overrideRaySettings || null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        ItemAuraShaderHelper.ApplyRayOverrides(propertyBlock, rayCount, angleJitter, beamBlur, minBeamWidth, maxBeamWidth);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyBurstProgress(float _progress)
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(ItemAuraShaderHelper.BurstProgressPropertyId, _progress);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyIntensity(float _intensity)
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(ItemAuraShaderHelper.IntensityPropertyId, _intensity);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateBurst(float _deltaTime)
    {
        elapsedTime += _deltaTime;
        float progress = 0f < burstDuration ? Mathf.Clamp01(elapsedTime / burstDuration) : 1f;

        // 셰이더 내부에서 32 PPU 픽셀 그리드를 타고 순차적으로 중심에서 외곽으로 확장 및 부드러운 소멸 연산 구동
        ApplyBurstProgress(progress);

        if (1f <= progress)
        {
            isPlaying = false;
            ApplyIntensity(0f);
            if (null != targetRenderer)
            {
                targetRenderer.enabled = false;
            }
        }
    }

    // ========================================================================
    // 3. 유니티 라이프사이클 이벤트 함수
    // ========================================================================

    private void Awake()
    {
        EnsurePropertyBlock();
        transform.localScale = maxScale;
        ApplyAllSettings();

        if (false == playOnAwake)
        {
            Stop();
        }
    }

    private void OnEnable()
    {
        EnsurePropertyBlock();
        transform.localScale = maxScale;
        ApplyAllSettings();

        if (true == playOnAwake)
        {
            Play();
        }
        else
        {
            Stop();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private void Start()
    {
        if (true == playOnAwake)
        {
            Play();
        }
    }

    private void Update()
    {
        if (false == isPlaying) return;

        UpdateBurst(Time.deltaTime);
    }

    private void OnValidate()
    {
        EnsurePropertyBlock();
        transform.localScale = maxScale;
        ApplyAllSettings();
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (false == showOnScreenDebugGui) return;

        GUILayout.BeginArea(new Rect(20, 20, 240, 170), GUI.skin.box);
        GUILayout.Label("✨ Item Aura (One-Shot)");
        GUILayout.Label($"Pixel Style: {(enablePixelStyle ? "ON (32 PPU)" : "OFF (Smooth)")}");
        GUILayout.Label($"Bloom Mult: {bloomIntensity:F1}");

        if (true == GUILayout.Button("▶ Play One-Shot Burst", GUILayout.Height(30)))
        {
            Play();
        }

        if (true == GUILayout.Button("⏹ Stop Immediate", GUILayout.Height(20)))
        {
            Stop();
        }

        if (true == GUILayout.Button($"Toggle Pixel ({(!enablePixelStyle ? "Enable" : "Disable")})", GUILayout.Height(20)))
        {
            enablePixelStyle = !enablePixelStyle;
            ApplyPixelSettings();
        }

        GUILayout.EndArea();
    }
#endif

    private void OnDestroy()
    {
        propertyBlock = null;
        targetRenderer = null;
    }
}
