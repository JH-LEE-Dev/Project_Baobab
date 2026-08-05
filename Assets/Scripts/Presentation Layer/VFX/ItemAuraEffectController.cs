using UnityEngine;
using NaughtyAttributes;

/// <summary>
/// 희귀 아이템 등장 방사형 회전 오오라(URP2D_ItemRadialAura) 이펙트를 제어하는 단발성(One-Shot) 버스트 컨트롤러 컴포넌트입니다.
/// Play 호출 시 외곽으로 갈수록 넓어지는 부채꼴(Fan Shape) 빛줄기들이 불규칙한 각도로 생성된 후, 각 광선마다 개별 속도로 회전하며 은은한 알파로 자연스럽게 소멸됩니다.
/// 인스펙터에서 블러 강도(Beam Blur: 선명도 vs 부드러움) 및 회전 속도를 직접 커스텀할 수 있습니다.
/// </summary>
public class ItemAuraEffectController : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Renderer targetRenderer;

    // 설정 파라미터
    [Header("단발성 버스트 애니메이션")]
    [SerializeField] private float burstDuration = 1.2f;
    [SerializeField] private float targetIntensity = 1.0f;
    [SerializeField] private Vector3 maxScale = Vector3.one;
    [SerializeField] private bool playOnAwake = false;

    [Header("회전 속도 제어 (-20.0 ~ 20.0 고속 스핀 지원)")]
    [SerializeField, Range(-20f, 20f)] private float rotationSpeed = 1.2f;
    [SerializeField, Range(0f, 1f)] private float speedVariation = 0.45f;

    [Header("부채꼴 광선 커스텀 오버라이드 (체크 시 머티리얼 값 재정의)")]
    [SerializeField] private bool overrideRaySettings = false;
    [SerializeField, Range(1f, 32f), ShowIf("overrideRaySettings")] private float rayCount = 6f;
    [SerializeField, Range(0f, 0.8f), ShowIf("overrideRaySettings")] private float angleJitter = 0.4f;
    [SerializeField, Range(0.01f, 1.0f), ShowIf("overrideRaySettings")] private float beamBlur = 0.22f;
    [SerializeField, Range(0.04f, 0.35f), ShowIf("overrideRaySettings")] private float minBeamWidth = 0.12f;
    [SerializeField, Range(0.15f, 0.8f), ShowIf("overrideRaySettings")] private float maxBeamWidth = 0.45f;

    [Header("디버그 및 테스트 GUI")]
    [SerializeField] private bool showOnScreenDebugGui = true;

    // 런타임 상태
    private MaterialPropertyBlock propertyBlock;
    private float elapsedTime = 0f;
    private bool isPlaying = false;

    // 셰이더 프로퍼티 ID 캐싱
    private static readonly int IntensityPropertyId = Shader.PropertyToID("_Intensity");
    private static readonly int BurstProgressPropertyId = Shader.PropertyToID("_BurstProgress");
    private static readonly int RotationSpeedPropertyId = Shader.PropertyToID("_RotationSpeed");
    private static readonly int SpeedVariationPropertyId = Shader.PropertyToID("_SpeedVariation");
    private static readonly int RayCountPropertyId = Shader.PropertyToID("_RayCount");
    private static readonly int AngleJitterPropertyId = Shader.PropertyToID("_AngleJitter");
    private static readonly int BeamBlurPropertyId = Shader.PropertyToID("_BeamBlur");
    private static readonly int BeamMinWidthPropertyId = Shader.PropertyToID("_BeamMinWidth");
    private static readonly int BeamMaxWidthPropertyId = Shader.PropertyToID("_BeamMaxWidth");
    private static readonly int CoreColorPropertyId = Shader.PropertyToID("_CoreColor");
    private static readonly int BeamColorPropertyId = Shader.PropertyToID("_BeamColor");

    // ========================================================================
    // 1. 퍼블릭 제어 및 초기화 메서드
    // ========================================================================

    /// <summary>
    /// 외부에서 렌더러를 주입하여 초기화합니다.
    /// </summary>
    public void Initialize(Renderer _targetRenderer)
    {
        targetRenderer = _targetRenderer;
        EnsurePropertyBlock();
        ApplyBurstProgress(0f);
        ApplyIntensity(0f);
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

        if (null != targetRenderer)
        {
            targetRenderer.enabled = true;
        }

        transform.localScale = Vector3.zero;
        ApplyIntensity(targetIntensity);
        ApplyRotationSettings(rotationSpeed, speedVariation);
        ApplyRayOverrides();
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
    /// 실시간으로 기준 회전 속도 및 광선별 속도 편차를 변경합니다.
    /// </summary>
    public void SetRotationSpeed(float _speed, float _speedVariation = 0.45f)
    {
        rotationSpeed = _speed;
        speedVariation = _speedVariation;
        ApplyRotationSettings(_speed, _speedVariation);
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
        EnsurePropertyBlock();

        if (null != targetRenderer)
        {
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(CoreColorPropertyId, _coreColor);
            propertyBlock.SetColor(BeamColorPropertyId, _beamColor);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
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

    private void ApplyRotationSettings(float _speed, float _variation)
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(RotationSpeedPropertyId, _speed);
        propertyBlock.SetFloat(SpeedVariationPropertyId, _variation);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyRayOverrides()
    {
        if (false == overrideRaySettings || null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(RayCountPropertyId, rayCount);
        propertyBlock.SetFloat(AngleJitterPropertyId, angleJitter);
        propertyBlock.SetFloat(BeamBlurPropertyId, beamBlur);
        propertyBlock.SetFloat(BeamMinWidthPropertyId, minBeamWidth);
        propertyBlock.SetFloat(BeamMaxWidthPropertyId, maxBeamWidth);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyBurstProgress(float _progress)
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(BurstProgressPropertyId, _progress);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void ApplyIntensity(float _intensity)
    {
        if (null == targetRenderer) return;

        EnsurePropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(IntensityPropertyId, _intensity);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private void UpdateBurst(float _deltaTime)
    {
        elapsedTime += _deltaTime;
        float progress = 0f < burstDuration ? Mathf.Clamp01(elapsedTime / burstDuration) : 1f;

        // 초반 빠른 스케일 팝업 (0.0~0.25 구간에서 빠르게 100% 도달)
        float scaleProgress = Mathf.Clamp01(progress / 0.25f);
        float scaleEase = 1f - Mathf.Pow(1f - scaleProgress, 3f);
        transform.localScale = Vector3.LerpUnclamped(Vector3.zero, maxScale, scaleEase);

        // 셰이더로 단발성 버스트 진행도(0.0~1.0) 전달 (생성 후 회전 개시 및 광선별 시차 소멸 연산 구동)
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
        if (false == playOnAwake)
        {
            ApplyIntensity(0f);
            ApplyBurstProgress(0f);
            if (null != targetRenderer)
            {
                targetRenderer.enabled = false;
            }
        }
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

    private void OnGUI()
    {
        if (false == showOnScreenDebugGui) return;

        GUILayout.BeginArea(new Rect(20, 20, 210, 120), GUI.skin.box);
        GUILayout.Label("✨ Item Aura (One-Shot)");

        if (true == GUILayout.Button("▶ Play One-Shot Burst", GUILayout.Height(35)))
        {
            Play();
        }

        if (true == GUILayout.Button("⏹ Stop Immediate", GUILayout.Height(25)))
        {
            Stop();
        }

        GUILayout.EndArea();
    }

    private void OnDestroy()
    {
        propertyBlock = null;
        targetRenderer = null;
    }
}
