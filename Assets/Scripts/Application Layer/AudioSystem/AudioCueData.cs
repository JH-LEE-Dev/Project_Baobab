using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAudioCue", menuName = "Game/Audio Cue")]
public class AudioCueData : ScriptableObject
{
    [Header("Clips")]
    public List<AudioClip> clips = new List<AudioClip>();

    [Header("Randomization")]
    [Range(0.1f, 3f)] public float minPitch = 0.95f;
    [Range(0.1f, 3f)] public float maxPitch = 1.05f;

    [Range(0.1f, 2f)] public float minVolumeModifier = 0.9f;
    [Range(0.1f, 2f)] public float maxVolumeModifier = 1.1f;

    private int lastIndex = -1;

    public AudioClip GetRandomClip()
    {
        if (clips == null || clips.Count == 0) return null;
        if (clips.Count == 1) return clips[0];

        int index = Random.Range(0, clips.Count);
        
        // 동일한 클립이 연속 재생되지 않도록 방지
        if (index == lastIndex)
        {
            index = (index + 1) % clips.Count;
        }
        
        lastIndex = index;
        return clips[index];
    }

    public float GetRandomPitch() => Random.Range(minPitch, maxPitch);
    public float GetRandomVolumeModifier() => Random.Range(minVolumeModifier, maxVolumeModifier);
}
