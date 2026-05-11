using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AudioDatabase", menuName = "Game/Audio Database")]
public class AudioDatabase : ScriptableObject, ISerializationCallbackReceiver
{
    public List<AudioData> sounds;
    public List<MixerMapping> mixers;

    private Dictionary<SoundID, AudioData> soundCache;

    public AudioData Get(SoundID id)
    {
        if (soundCache != null && soundCache.TryGetValue(id, out AudioData data))
        {
            return data;
        }

        // 캐시가 없는 경우(에디터 등) 선형 탐색
        for (int i = 0; i < sounds.Count; i++)
        {
            if (sounds[i].id == id) return sounds[i];
        }
        return null;
    }

    public void OnBeforeSerialize() { }

    public void OnAfterDeserialize()
    {
        // 역직렬화 후 딕셔너리 캐시 구축 (O(1) 접근 보장)
        if (sounds == null) return;
        
        soundCache = new Dictionary<SoundID, AudioData>(sounds.Count);
        for (int i = 0; i < sounds.Count; i++)
        {
            var data = sounds[i];
            if (data != null && data.id != SoundID.None)
            {
                soundCache[data.id] = data;
            }
        }
    }
}
