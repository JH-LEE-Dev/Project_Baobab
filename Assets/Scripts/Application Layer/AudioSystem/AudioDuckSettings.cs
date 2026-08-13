using UnityEngine;

[CreateAssetMenu(fileName = "AudioDuckSettings", menuName = "Game/Audio Duck Settings")]
public class AudioDuckSettings : ScriptableObject
{
    [Header("Cutoff Frequency (Hz)")]
    [Tooltip("평소 상태의 Cutoff. Lowpass가 사실상 걸리지 않는 최대값.")]
    public float openCutoffHz = 22000f;

    [Tooltip("대상 UI가 떠 있을 때 적용되는 Cutoff. 낮을수록 더 먹먹해진다.")]
    public float duckedCutoffHz = 1200f;

    [Tooltip("Cutoff를 전환하는 데 걸리는 시간(초). unscaledTime 기준.")]
    public float cutoffTransitionDuration = 0.25f;

    [Header("Duck Volume")]
    [Tooltip("덕킹 중 게임플레이 사운드(SFX/Ambience)에 곱할 볼륨 배율. 0.5 = 소리 크기 절반(-6dB). " +
             "먹먹하게 만드는 것만으로는 부족할 때 함께 작아지도록 한다. BGM은 이 감쇠를 받지 않는다.")]
    [Range(0f, 1f)]
    public float duckedVolumeScale = 0.5f;

    [Header("Pause Mute (dB)")]
    [Tooltip("평소 게임플레이(SFX/Ambience) 그룹 볼륨. 0 = 원본 그대로.")]
    public float normalVolumeDb = 0f;

    [Tooltip("일시정지 중 게임플레이 그룹 볼륨. -80 = 완전 무음.")]
    public float pausedVolumeDb = -80f;

    [Tooltip("일시정지 음소거를 전환하는 데 걸리는 시간(초). 짧게 둬야 딸깍 소리가 안 난다.")]
    public float pauseFadeDuration = 0.1f;

    [Header("Fatigue Tension Cutoff (Hz)")]
    [Tooltip("긴박감 연출 시작 임계값. 캐릭터 피로도(스태미나) 비율이 이 값 아래로 떨어지면 " +
             "Cutoff 보간이 시작된다 (0~1, 0.2 = 20%). Duck Cutoff와 별개로 이 값이 더 먹먹하면 그쪽이 우선한다.")]
    [Range(0f, 1f)]
    public float tensionStartRatio = 0.2f;

    [Tooltip("이 비율 이하로 내려가면 Max Cutoff(가장 먹먹한 상태)로 고정된다 (0~1, 0.1 = 10%).")]
    [Range(0f, 1f)]
    public float tensionMaxRatio = 0.1f;

    [Tooltip("피로도가 tensionMaxRatio 이하일 때 적용되는 Max Cutoff. 낮을수록 더 먹먹해진다.")]
    public float tensionMaxCutoffHz = 500f;

    [Header("Volume Slider Mapping")]
    [Tooltip("마스터 슬라이더가 이 값일 때 0dB(원본 그대로)가 된다.")]
    public float masterZeroDbValue = 100f;

    [Tooltip("BGM/효과음 슬라이더가 이 값일 때 0dB(원본 그대로)가 된다. 사운드를 50% 기준으로 " +
             "밸런싱했으므로 50이며, 그래서 기본값 50에서 지금 들리는 것과 똑같이 재생된다.")]
    public float mixZeroDbValue = 50f;

    [Tooltip("슬라이더를 0까지 내렸을 때 적용할 dB. -80이면 완전 무음.")]
    public float minVolumeDb = -80f;

    [Tooltip("슬라이더를 최대로 올렸을 때 허용할 최대 dB. 과도한 증폭으로 클리핑되는 걸 막는다.")]
    public float maxVolumeDb = 6f;
}
