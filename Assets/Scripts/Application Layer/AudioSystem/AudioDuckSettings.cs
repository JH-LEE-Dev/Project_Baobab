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

    [Tooltip("팝업 UI가 열려 덕킹이 걸리면 피로도 Cutoff를 완전히 열어(=UI 덕킹만 남겨) 두 효과가 " +
             "겹쳐 과하게 먹먹해지는 것을 막는데, 그 전환에 걸리는 시간(초). unscaledTime 기준.\n" +
             "즉시 열어버리면 UI를 여는 순간 소리가 확 밝아졌다가 덕킹이 걸리며 다시 어두워지는 " +
             "'와우' 아티팩트가 생기므로 서서히 넘긴다. 덕킹 램프(cutoffTransitionDuration)보다 느려야 " +
             "덕킹이 먼저 자리를 잡아 중간에 밝아지지 않는다. 0.6이면 아주 짧게(약 0.1초) 목표보다 " +
             "반 옥타브쯤 밝아지는 정도이고, 1.1 이상이면 그 잔여 오버슈트도 완전히 사라지지만 UI를 " +
             "닫은 뒤 피로도 먹먹함이 돌아오는 것도 그만큼 느려진다.")]
    public float tensionSuppressionDuration = 0.6f;

    [Header("Fatigue Tension Cutoff - BGM")]
    [Tooltip("피로도가 tensionMaxRatio 이하일 때 BGM에 적용되는 Max Cutoff. 낮을수록 더 먹먹해진다. " +
             "실측 결과 이 게임 BGM 6곡 전부 에너지의 90% 이상이 500~1000Hz 아래에 몰려있다.")]
    public float bgmTensionMaxCutoffHz = 500f;

    [Tooltip("피로도 비율(t)을 BGM Cutoff 로그 보간에 넣기 전에 적용하는 ease-out 지수(1 = 보정 없음). " +
             "BGM은 에너지의 90% 이상이 500~1000Hz 아래에 몰려있어서, 22000Hz에서 출발하는 스윕은 " +
             "대부분(약 2000Hz까지)이 아무 소리도 안 걸리는 구간이라 초반엔 안 들리다가 막판에 갑자기 " +
             "확 먹먹해지는 것처럼 느껴진다. 이 값을 1보다 크게 주면 안 들리는 고음역 구간을 피로도 " +
             "초반에 빠르게 통과시키고, 실제로 음악이 걸리는 저역 구간에 나머지 피로도 구간을 더 많이 " +
             "배분해 체감상 고르게 어두워진다. BGM 6곡 모두 비슷한 대역이라 공통값 하나로 충분하다. " +
             "3은 너무 앞쪽에 몰려 13%에서 이미 최대치에 닿고 13~10% 구간이 낭비되므로 2로 둔다.")]
    [Min(1f)]
    public float bgmTensionCurveExponent = 2f;

    [Header("Fatigue Tension Cutoff - SFX/Ambience")]
    [Tooltip("피로도가 tensionMaxRatio 이하일 때 SFX/Ambience(Gameplay 그룹)에 적용되는 Max Cutoff. " +
             "낮을수록 더 먹먹해진다.")]
    public float sfxTensionMaxCutoffHz = 500f;

    [Tooltip("피로도 비율(t)을 SFX/Ambience Cutoff 로그 보간에 넣기 전에 적용하는 ease-out 지수 " +
             "(1 = 보정 없음, 왜곡 없는 순수 로그 보간). BGM과 달리 SFX는 종류마다 에너지가 몰린 대역이 " +
             "제각각이다(예: 동전 줍기 효과음은 90% 지점이 18761Hz까지 올라가는 반면 도끼 파괴음은 " +
             "86Hz 아래에 몰려있다) - 그래서 BGM처럼 '고음역은 어차피 무음'이라는 전제로 커브를 " +
             "휘게 하면 오히려 밝은 SFX에서 어색해진다. 다만 1(순수 로그 보간)로 두면 BGM보다 한참 " +
             "밝게 남아 음악만 물속에 잠긴 것처럼 들리므로, 간격을 좁히되 왜곡은 최소인 1.5로 둔다.")]
    [Min(1f)]
    public float sfxTensionCurveExponent = 1.5f;

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
