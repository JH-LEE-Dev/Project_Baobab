using UnityEngine;

public static class Sound
{
    public static void Play(SoundID id, Vector3 position, float volume = 1f, bool is3D = true)
    {
        if (AudioManager.Instance == null)
            return;

        AudioEvent e = new AudioEvent(id, position, volume, is3D);
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
}
