using UnityEngine;

public static class Sound
{
    public static void Play(SoundID id, Vector3 position, float volume = 1f, bool is3D = true, float pitchOverride = -1f)
    {
        if (AudioManager.Instance == null)
            return;

        AudioEvent e = new AudioEvent(id, position, volume, is3D, pitchOverride);
        AudioManager.Instance.EnqueueEvent(e);
    }

    public static void PlayUI(SoundID id, float volume = 1f)
    {
        if (AudioManager.Instance == null)
            return;

        AudioEvent e = new AudioEvent(id, Vector3.zero, volume, false);
        AudioManager.Instance.EnqueueEvent(e);
    }

    public static void PlayBGM(SoundID id, float volume = 1f)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayBGM(id, volume);
    }

    // 지정한 시간에 걸쳐 볼륨을 낮추며 BGM을 정지한다.
    public static void FadeOutBGM(float duration)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.FadeOutBGM(duration);
    }

    // 루프 사운드 등, 재생 이후에도 정지/위치 갱신이 필요한 경우 핸들을 통해 제어한다.
    public static AudioHandle PlayTracked(SoundID id, Vector3 position, float volume = 1f, bool is3D = true)
    {
        if (AudioManager.Instance == null)
            return AudioHandle.Invalid;

        return AudioManager.Instance.PlayTracked(id, position, volume, is3D);
    }

    public static void StopTracked(AudioHandle handle)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.StopTracked(handle);
    }

    // 정지음이 없는 루프 사운드를 피치/볼륨을 서서히 낮추며(전원 꺼지듯) 정지한다.
    public static void StopTrackedWithPowerDown(AudioHandle handle, float duration = 0.4f, float minPitch = 0.1f)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.StopTrackedWithPowerDown(handle, duration, minPitch);
    }

    public static void UpdateTrackedPosition(AudioHandle handle, Vector3 position)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.UpdateTrackedPosition(handle, position);
    }
}
