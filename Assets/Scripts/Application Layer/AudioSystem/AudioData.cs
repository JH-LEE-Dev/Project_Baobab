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
}

[System.Serializable]
public struct MixerMapping
{
    public MixerID id;
    public UnityEngine.Audio.AudioMixerGroup group;
}
