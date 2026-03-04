using UnityEngine;

public struct AudioEvent
{
    public string soundId;
    public Vector3 position;
    public float volume;
    public bool is3D;

    public AudioEvent(string soundId, Vector3 position, float volume = 1f, bool is3D = true)
    {
        this.soundId = soundId;
        this.position = position;
        this.volume = volume;
        this.is3D = is3D;
    }
}