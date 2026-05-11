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
}

[System.Serializable]
public struct MixerMapping
{
    public MixerID id;
    public UnityEngine.Audio.AudioMixer mixer;
}
