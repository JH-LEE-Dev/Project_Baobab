using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Database")]
    [SerializeField] private AudioDatabase database;

    [Header("Pool Settings")]
    [SerializeField] private int poolSize = 50;

    private Queue<AudioEvent> eventQueue = new Queue<AudioEvent>();
    private List<AudioSource> sourcePool = new List<AudioSource>();

    private AudioSource bgmSource;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(transform.parent.gameObject);
        CreatePool();
        CreateBGMSource();
    }

    private void CreatePool()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var obj = new GameObject("AudioSource_" + i);
            obj.transform.parent = transform;

            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;      // default: 3D

            sourcePool.Add(source);
        }
    }

    public void EnqueueEvent(AudioEvent audioEvent)
    {
        eventQueue.Enqueue(audioEvent);
    }

    private void Update()
    {
        while (eventQueue.Count > 0)
        {
            var e = eventQueue.Dequeue();
            PlayInternal(e);
        }
    }

    private void PlayInternal(AudioEvent e)
    {
        AudioData data = database.Get(e.soundId);
        if (data == null)
        {
            Debug.LogWarning($"Audio ID '{e.soundId}' not found in database.");
            return;
        }

        AudioSource src = GetAvailableSource();
        if (src == null) return;

        src.transform.position = e.position;

        src.clip = data.clip;
        src.outputAudioMixerGroup = data.mixerGroup;

        src.volume = data.defaultVolume * e.volume;
        src.spatialBlend = data.is3D ? 1f : 0f;

        src.Play();
    }

    private AudioSource GetAvailableSource()
    {
        foreach (var src in sourcePool)
        {
            if (!src.isPlaying)
                return src;
        }

        return sourcePool[0];
    }
    private void CreateBGMSource()
    {
        var obj = new GameObject("BGMSource");
        obj.transform.parent = transform;

        bgmSource = obj.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;               // �⺻ Loop
        bgmSource.spatialBlend = 0f;         // BGM�� 2D
    }
    public void PlayBGM(string bgmId, float volume = 1f)
    {
        AudioData data = database.Get(bgmId);
        if (data == null)
        {
            Debug.LogWarning($"BGM ID '{bgmId}' not found in database.");
            return;
        }

        bgmSource.clip = data.clip;
        bgmSource.outputAudioMixerGroup = data.mixerGroup;
        bgmSource.volume = data.defaultVolume * volume;
        bgmSource.loop = true;  // �׻� loop
        bgmSource.spatialBlend = 0f;

        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource.isPlaying)
            bgmSource.Stop();
    }

    public void PauseBGM()
    {
        if (bgmSource.isPlaying)
            bgmSource.Pause();
    }

    public void ResumeBGM()
    {
        if (!bgmSource.isPlaying)
            bgmSource.UnPause();
    }
}


