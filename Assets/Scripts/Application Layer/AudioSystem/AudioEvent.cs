using UnityEngine;

public struct AudioEvent
{
    public SoundID soundId;
    public Vector3 position;
    public float volume;
    public bool is3D;
    // 0f 미만이면 오버라이드 없음(AudioData/Cue 기본 피치 사용)
    public float pitchOverride;

    public AudioEvent(SoundID soundId, Vector3 position, float volume = 1f, bool is3D = true, float pitchOverride = -1f)
    {
        this.soundId = soundId;
        this.position = position;
        this.volume = volume;
        this.is3D = is3D;
        this.pitchOverride = pitchOverride;
    }
}