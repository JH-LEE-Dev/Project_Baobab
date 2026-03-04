using UnityEngine;

public static class Sound
{
    public static void Play(string id, Vector3 position, float volume = 1f, bool is3D = true)
    {
        if (AudioManager.Instance == null)
            return;

        AudioEvent e = new AudioEvent(id, position, volume, is3D);
        AudioManager.Instance.EnqueueEvent(e);
    }

    public static void PlayUI(string id, float volume = 1f)
    {
        if (AudioManager.Instance == null)
            return;

        AudioEvent e = new AudioEvent(id, Vector3.zero, volume, false);
        AudioManager.Instance.EnqueueEvent(e);
    }

    public static void PlayBGM(string id, float volume = 1f)
    {
        if (AudioManager.Instance == null)
            return;

        AudioManager.Instance.PlayBGM(id, volume);
    }
}
