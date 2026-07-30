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

    // 마지막으로 재생된 클립의 인덱스는 이 애셋을 사용하는 쪽(AudioManager)에서 호출별로 관리한다.
    // ScriptableObject 필드에 재생 상태를 저장하면 동일 Cue를 여러 발음원이 동시에 재생할 때
    // 서로의 회피 로직을 오염시키므로, 상태를 외부에서 주입받는 형태로 변경했다.
    public AudioClip GetRandomClip(ref int lastIndex)
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
