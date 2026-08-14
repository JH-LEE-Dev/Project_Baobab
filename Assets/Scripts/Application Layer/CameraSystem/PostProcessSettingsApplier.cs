using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

/// <summary>
/// 같은 GameObject의 Volume에 옵션 슬라이더(색수차·명도·채도) 값을 반영합니다.
///
/// 카메라는 씬마다 파괴·재생성되므로, 프로필 원본(sharedProfile)을 직접 고치면 에디터에서
/// 값이 영구히 남을 위험이 있다. 그래서 Volume.profile(최초 접근 시 컴포넌트 단위로 복제본을
/// 만들어주는 프로퍼티)을 통해서만 건드린다.
///
/// 주의: VolumeProfile은 ScriptableObject라 Instantiate(sharedProfile)로 직접 복제하면
/// 프로필 오브젝트만 새로 생기고 그 안의 ColorAdjustments 등 컴포넌트는 원본과 같은 참조를
/// 그대로 공유한다(Unity가 하위 컴포넌트까지 깊은 복사를 해주지 않음). 그 상태로 값을 바꾸면
/// 에디터에서는 Play 모드를 꺼도 되돌아가지 않아 원본 프로필 에셋이 영구히 오염된다.
/// Volume.profile 게터는 각 컴포넌트를 개별적으로 Instantiate해 진짜 복제본을 만들어주므로
/// 반드시 이쪽을 써야 한다.
///
/// 명도·채도 슬라이더는 100%가 "원래 아트 설정 그대로"이고 0%로 갈수록 효과가 옅어지도록
/// 상시 적용된다(SettingsData.CreateDefault의 기본값이 전부 SLIDER_MAX인 것과 같은 맥락).
/// 프로필에 이미 아트 디렉팅된 기준값(ColorAdjustments)이 있으므로, 그 값을 Awake 시점에
/// 읽어 100% 지점으로 삼고 0%는 각각 최소 노출/무채색 쪽으로 보간한다.
///
/// 색수차는 성격이 달라서 상시 적용하지 않는다. 화면에 항상 걸려있는 효과가 아니라
/// 피로도(스태미나) 경고 연출 전용이며 두 부분으로 나뉜다:
///   1) UpdateLowStaminaChromaticAberration(비율) - 피로도가 임계 비율(기본 20%) 밑으로 내려간
///      만큼 세기가 연속적으로(비율의 함수로) 올라간다. 매 프레임 현재 비율을 그대로 넘기면 되며,
///      임계 비율 경계에서 값이 튀지 않도록 설계되어 있다(그 경계에서 목표값 자체가 정확히 0).
///   2) PlayDeathChromaticAberrationPulse() - 사망 순간 목표값과 무관하게 한 번 강하게 튀었다가
///      다시 0으로 원복하는 별도 연출이며, 재생 중에는 1)의 매 프레임 갱신을 무시한다.
/// 슬라이더 값은 두 경우 모두 "세기"만 조절한다(0%면 아무것도 보이지 않는다).
/// </summary>
[RequireComponent(typeof(Volume))]
public class PostProcessSettingsApplier : MonoBehaviour
{
    /// <summary>
    /// 씬에 하나만 존재하는 카메라(Global Post Process Volume)에 붙어 씬마다 새로 만들어지므로,
    /// CameraMoveController와 같은 방식으로 Character 등 외부에서
    /// Instance?.UpdateLowStaminaChromaticAberration(...) 형태로 접근한다.
    /// </summary>
    public static PostProcessSettingsApplier Instance { get; private set; }

    [Tooltip("피로도 비율이 이 값 이하로 내려가면 색수차가 나타나기 시작한다(0.2 = 20%). 이 값 자체에서는 세기가 정확히 0이라 경계에서 값이 튀지 않는다")]
    [SerializeField, Range(0.01f, 1f)] private float lowStaminaThresholdRatio = 0.2f;

    [Tooltip("피로도가 0%(완전 고갈)일 때의 색수차 세기(강조 세기 100% 기준). 임계 비율에서 0, 0%에서 이 값까지 비율에 비례해 연속적으로 올라간다")]
    [SerializeField] private float lowStaminaMaxIntensity = 0.3f;

    [Tooltip("색수차 세기가 목표치를 따라가는 속도(초당 intensity 변화량). 클수록 즉각적이고 작을수록 느긋하게 뒤따라간다 - 스태미나가 한 프레임에 크게 깎이는 경우(피격 등) 값이 순간 점프하지 않도록 완충하는 용도")]
    [SerializeField] private float lowStaminaSmoothSpeed = 1.5f;

    [Tooltip("사망 순간 재생되는 펄스의 최고조 intensity(강조 세기 100% 기준). 저피로도 최대 세기보다 확실히 강하게 잡아야 '꽤 강한' 임팩트가 산다")]
    [SerializeField] private float deathPulsePeakIntensity = 0.7f;

    [Tooltip("사망 펄스가 최고조까지 올라가는 시간(초) - 짧고 강하게")]
    [SerializeField] private float deathPulseFadeInDuration = 0.12f;

    [Tooltip("사망 펄스가 최고조에서 0으로 원복되는 시간(초)")]
    [SerializeField] private float deathPulseFadeOutDuration = 0.5f;

    [Tooltip("명도 슬라이더 0%일 때의 노출(EV). 100%는 프로필에 설정된 원래 노출값을 그대로 쓴다")]
    [SerializeField] private float minPostExposure = -2f;

    [Tooltip("채도 슬라이더 0%일 때의 saturation 값. 100%는 프로필에 설정된 원래 채도값을 그대로 쓴다")]
    [SerializeField] private float minSaturation = -100f;

    private Volume volume;
    private ChromaticAberration chromaticAberration;
    private ColorAdjustments colorAdjustments;

    private float baseExposure;
    private float baseSaturation;

    // 색수차 옵션 슬라이더 값(0~1). 최고조 세기를 스케일하는 용도로만 쓰인다.
    private float chromaticAberrationStrength;

    // 사망 펄스 재생 중에는 매 프레임 저피로도 갱신이 그 연출을 덮어쓰지 않도록 막는다.
    private bool isDeathPulsePlaying;
    private Tween chromaticAberrationTween;

    private void Awake()
    {
        Instance = this;

        volume = GetComponent<Volume>();

        // volume.profile은 최초 접근 시 sharedProfile의 각 컴포넌트를 개별 복제해 진짜 런타임
        // 전용 인스턴스를 만들어준다(위 클래스 주석 참고). Instantiate(volume.sharedProfile)을
        // 직접 쓰면 안 된다.
        VolumeProfile _runtimeProfile = volume.profile;

        // 프로필에 색수차 컴포넌트가 아직 없을 수 있으므로(아트 기본 프로필에는 미포함) 없으면 추가한다.
        if (false == _runtimeProfile.TryGet(out chromaticAberration))
        {
            chromaticAberration = _runtimeProfile.Add<ChromaticAberration>(true);
        }

        _runtimeProfile.TryGet(out colorAdjustments);

        baseExposure = (null != colorAdjustments) ? colorAdjustments.postExposure.value : 0f;
        baseSaturation = (null != colorAdjustments) ? colorAdjustments.saturation.value : 0f;
    }

    private void OnEnable()
    {
        // SettingsManager는 최초 접근 시 자동 생성되는 싱글턴이라 null 체크가 필요 없다.
        SettingsManager.Instance.OnGraphicsSettingsAppliedEvent -= Apply;
        SettingsManager.Instance.OnGraphicsSettingsAppliedEvent += Apply;

        // 이벤트는 옵션 창을 닫을 때만 발행되므로, 씬 진입 시점에는 저장된 값을 직접 한 번 반영한다.
        Apply(SettingsManager.Instance.Current);
    }

    private void OnDisable()
    {
        if (false == SettingsManager.HasInstance) return;
        SettingsManager.Instance.OnGraphicsSettingsAppliedEvent -= Apply;
    }

    private void OnDestroy()
    {
        chromaticAberrationTween?.Kill();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 피로도 비율(0~1)에 맞춰 색수차 세기를 매 프레임 갱신합니다. 임계 비율(기본 20%) 이상이면
    /// 목표 세기가 정확히 0이고, 그 아래로 내려갈수록 0%(완전 고갈)까지 비율에 비례해 연속적으로
    /// 올라갑니다 - 경계 자체에서 목표값이 0이므로 문턱을 넘나들어도 값이 튀지 않습니다.
    /// 실제 값은 목표치를 향해 lowStaminaSmoothSpeed로 부드럽게 뒤따라가며(순간적인 스태미나
    /// 급감에도 점프하지 않도록), 사망 펄스가 재생 중일 때는 그 연출을 방해하지 않도록 아무것도
    /// 하지 않습니다.
    /// </summary>
    public void UpdateLowStaminaChromaticAberration(float _staminaRatio)
    {
        if (null == chromaticAberration || isDeathPulsePlaying) return;

        float _t = Mathf.Clamp01((lowStaminaThresholdRatio - _staminaRatio) / lowStaminaThresholdRatio);
        float _target = lowStaminaMaxIntensity * chromaticAberrationStrength * _t;

        chromaticAberration.intensity.overrideState = true;
        chromaticAberration.intensity.value = Mathf.MoveTowards(chromaticAberration.intensity.value, _target, lowStaminaSmoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 사망 순간 색수차를 짧고 강하게 튀웠다가(0→최고조) 다시 0으로 원복하는 연출입니다.
    /// 재생 중에는 UpdateLowStaminaChromaticAberration의 매 프레임 갱신을 무시시켜 서로
    /// 충돌하지 않게 합니다. 색수차 옵션이 0%면(효과를 원치 않는 유저) 재생하지 않습니다.
    /// </summary>
    public void PlayDeathChromaticAberrationPulse()
    {
        if (null == chromaticAberration || chromaticAberrationStrength <= 0f) return;

        isDeathPulsePlaying = true;
        float _peak = deathPulsePeakIntensity * chromaticAberrationStrength;

        chromaticAberrationTween?.Kill();
        chromaticAberration.intensity.overrideState = true;

        chromaticAberrationTween = DOTween.Sequence()
            .Append(DOTween.To(() => chromaticAberration.intensity.value, _v => chromaticAberration.intensity.value = _v, _peak, deathPulseFadeInDuration).SetEase(Ease.OutQuad))
            .Append(DOTween.To(() => chromaticAberration.intensity.value, _v => chromaticAberration.intensity.value = _v, 0f, deathPulseFadeOutDuration).SetEase(Ease.InQuad))
            .OnComplete(() => isDeathPulsePlaying = false);
    }

    private void Apply(SettingsData _data)
    {
        chromaticAberrationStrength = Mathf.Clamp01(_data.chromaticAberration / SettingsData.SLIDER_MAX);

        float _brightnessT = Mathf.Clamp01(_data.brightness / SettingsData.SLIDER_MAX);
        float _saturationT = Mathf.Clamp01(_data.saturation / SettingsData.SLIDER_MAX);

        if (null != colorAdjustments)
        {
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = Mathf.Lerp(minPostExposure, baseExposure, _brightnessT);

            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = Mathf.Lerp(minSaturation, baseSaturation, _saturationT);
        }
    }
}
