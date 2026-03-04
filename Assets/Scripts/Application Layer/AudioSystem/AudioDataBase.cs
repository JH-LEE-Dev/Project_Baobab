using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioDatabase", menuName = "Game/Audio Database")]
public class AudioDatabase : ScriptableObject
{
    public List<AudioData> sounds;

    public AudioData Get(string id)
    {
        return sounds.Find(x => x.id == id);
    }
}
