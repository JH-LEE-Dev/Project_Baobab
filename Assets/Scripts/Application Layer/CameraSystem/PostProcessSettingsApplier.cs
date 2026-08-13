using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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
/// 슬라이더는 모두 100%가 "원래 아트 설정 그대로"이고 0%로 갈수록 효과가 옅어지도록
/// 되어 있다(SettingsData.CreateDefault의 기본값이 전부 SLIDER_MAX인 것과 같은 맥락).
/// 명도·채도는 프로필에 이미 아트 디렉팅된 기준값(ColorAdjustments)이 있으므로, 그 값을
/// Awake 시점에 읽어 100% 지점으로 삼고 0%는 각각 최소 노출/무채색 쪽으로 보간한다.
/// </summary>
[RequireComponent(typeof(Volume))]
public class PostProcessSettingsApplier : MonoBehaviour
{
    [Tooltip("색수차 슬라이더 100%일 때의 ChromaticAberration intensity")]
    [SerializeField] private float chromaticAberrationMaxIntensity = 0.3f;

    [Tooltip("명도 슬라이더 0%일 때의 노출(EV). 100%는 프로필에 설정된 원래 노출값을 그대로 쓴다")]
    [SerializeField] private float minPostExposure = -2f;

    [Tooltip("채도 슬라이더 0%일 때의 saturation 값. 100%는 프로필에 설정된 원래 채도값을 그대로 쓴다")]
    [SerializeField] private float minSaturation = -100f;

    private Volume volume;
    private ChromaticAberration chromaticAberration;
    private ColorAdjustments colorAdjustments;

    private float baseExposure;
    private float baseSaturation;

    private void Awake()
    {
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

    private void Apply(SettingsData _data)
    {
        float _chromaticT = Mathf.Clamp01(_data.chromaticAberration / SettingsData.SLIDER_MAX);
        float _brightnessT = Mathf.Clamp01(_data.brightness / SettingsData.SLIDER_MAX);
        float _saturationT = Mathf.Clamp01(_data.saturation / SettingsData.SLIDER_MAX);

        if (null != chromaticAberration)
        {
            chromaticAberration.intensity.overrideState = true;
            chromaticAberration.intensity.value = chromaticAberrationMaxIntensity * _chromaticT;
        }

        if (null != colorAdjustments)
        {
            colorAdjustments.postExposure.overrideState = true;
            colorAdjustments.postExposure.value = Mathf.Lerp(minPostExposure, baseExposure, _brightnessT);

            colorAdjustments.saturation.overrideState = true;
            colorAdjustments.saturation.value = Mathf.Lerp(minSaturation, baseSaturation, _saturationT);
        }
    }
}
