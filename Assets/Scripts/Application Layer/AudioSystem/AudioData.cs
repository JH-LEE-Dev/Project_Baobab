using UnityEngine;
using UnityEngine.Audio;

[System.Serializable]
public class AudioData
{
    public string id;
    public AudioClip clip;
    public AudioMixerGroup mixerGroup;
    public float defaultVolume = 1f;
    public bool is3D = true;
}