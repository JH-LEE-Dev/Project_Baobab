using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class AudioData
{
    public SoundID id;
    public MixerID mixerId;

    [Header("Simple Sound")]
    public AudioClip clip;

    [Header("Complex Sound Cue (Optional)")]
    public AudioCueData cueData;

    [Header("Settings")]
    public AudioMixerGroup mixerGroup;
    public float defaultVolume = 1f;
    public bool is3D = true;
    public bool loop = false;

    [Header("Polyphony (동시 재생 제어)")]
    [Tooltip("이 사운드가 동시에 재생될 수 있는 최대 개수. 0이면 제한 없음(기존과 동일 동작).\n" +
        "초과 시 새 소리는 이 사운드 중 가장 먼저 재생을 시작한 것을 이어받아 재생한다(다른 종류의 소리는 건드리지 않음).")]
    public int maxConcurrentVoices = 0;

    [Tooltip("동시에 겹쳐 재생될수록 개별 볼륨을 줄이는 정도. 0=끔(기존과 동일), 1=최대.\n" +
        "겹친 개수가 n일 때 볼륨에 1/sqrt(1+n) 감쇠를 이 값만큼(0~1) 섞어 적용한다.\n" +
        "인크리멘탈 특성상 여러 발음원이 동시에 같은 사운드를 재생해 합산 음량이 과도해지는 사운드(GetItem, TreeHit, CoinGet, ConvayerPut 등)에 사용.")]
    [Range(0f, 1f)] public float polyphonyAttenuationStrength = 0f;

    [Header("Priority (실보이스 경쟁 우선순위)")]
    [Tooltip("AudioSource.priority. 낮을수록 중요(0=최우선, 256=최하).\n" +
        "Unity는 실보이스(프로젝트 설정 Max Real Voices)가 모자라면 priority가 높은(덜 중요한) 소리부터, 같으면 들리는 볼륨이 작은 소리부터 가상화(무음)한다.\n" +
        "UI 조작음·사망음·루프처럼 반드시 들려야 하는 소리는 낮게(32~64), 발소리처럼 빠져도 되는 소리는 높게(192) 둔다. BGM은 코드에서 0으로 고정된다.")]
    [Range(0, 256)] public int priority = 128;
}

[System.Serializable]
public struct MixerMapping
{
    public MixerID id;
    public UnityEngine.Audio.AudioMixerGroup group;
}
