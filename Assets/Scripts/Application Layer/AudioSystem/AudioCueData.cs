using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewAudioCue", menuName = "Game/Audio Cue")]
public class AudioCueData : ScriptableObject
{
    [Header("Clips")]
    public List<AudioClip> clips = new List<AudioClip>();

    // clips와 같은 순서의 클립별 음량 배율. 인스펙터(AudioCueDataEditor)에서 각 클립 옆에 함께 그린다.
    // 항목이 없거나 개수가 모자라면 1(원래 음량)로 본다 - 기존 큐 애셋은 손대지 않아도 그대로 들린다.
    [HideInInspector] public List<float> clipVolumes = new List<float>();

    // 켜면 무작위로 하나를 고르지 않고 clips 전부를 한꺼번에 겹쳐 재생한다(원샷 전용, 루프 사운드에서는 무시).
    // 피치·음량 랜덤 값은 한 번 뽑아 모든 클립에 같이 쓰고, 클립별 음량(clipVolumes)은 각각 곱한다.
    public bool playAllClips = false;

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
        return GetRandomClip(ref lastIndex, out _);
    }

    // _clipVolume: 고른 클립의 클립별 음량 배율(clipVolumes). 설정이 없으면 1.
    public AudioClip GetRandomClip(ref int lastIndex, out float _clipVolume)
    {
        _clipVolume = 1f;
        if (clips == null || clips.Count == 0) return null;
        if (clips.Count == 1)
        {
            _clipVolume = GetClipVolume(0);
            return clips[0];
        }

        int index = Random.Range(0, clips.Count);

        // 동일한 클립이 연속 재생되지 않도록 방지
        if (index == lastIndex)
        {
            index = (index + 1) % clips.Count;
        }

        lastIndex = index;
        _clipVolume = GetClipVolume(index);
        return clips[index];
    }

    public float GetClipVolume(int _index)
    {
        if (clipVolumes == null || _index < 0 || _index >= clipVolumes.Count) return 1f;
        return clipVolumes[_index];
    }

    public float GetRandomPitch() => Random.Range(minPitch, maxPitch);
    public float GetRandomVolumeModifier() => Random.Range(minVolumeModifier, maxVolumeModifier);
}
