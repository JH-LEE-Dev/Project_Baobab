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

    [Header("BGM Settings")]
    [SerializeField] private float bgmFadeDuration = 1f;

    private Queue<AudioEvent> eventQueue = new Queue<AudioEvent>(100);
    private List<AudioSource> sourcePool = new List<AudioSource>();
    private Dictionary<SoundID, AudioData> audioCache;

    private AudioSource bgmSource;
    private Coroutine bgmFadeCoroutine;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (transform.parent != null)
            DontDestroyOnLoad(transform.parent.gameObject);
        else
            DontDestroyOnLoad(gameObject);

        CreatePool();
        CreateBGMSource();
        InitializeCache();
    }

    private void InitializeCache()
    {
        if (database == null || database.sounds == null) return;

        // 초기 용량을 설정하여 런타임 확장을 방지 (Maximize Stack/Heap efficiency)
        int count = database.sounds.Count;
        audioCache = new Dictionary<SoundID, AudioData>(count);

        for (int i = 0; i < count; i++)
        {
            var data = database.sounds[i];
            if (data.id == SoundID.None) continue;
            
            if (!audioCache.ContainsKey(data.id))
            {
                audioCache.Add(data.id, data);
            }
            else
            {
                Debug.LogWarning($"[AudioManager] Duplicate SoundID found in database: {data.id}");
            }
        }
    }

    private void CreatePool()
    {
        sourcePool.Capacity = poolSize;
        for (int i = 0; i < poolSize; i++)
        {
            var obj = new GameObject("AudioSource_" + i);
            obj.transform.parent = transform;

            AudioSource source = obj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 1f;

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
        if (audioCache == null || !audioCache.TryGetValue(e.soundId, out AudioData data))
        {
            Debug.LogWarning($"Audio ID '{e.soundId}' not found in cache.");
            return;
        }

        AudioSource src = GetAvailableSource();
        if (src == null) return;

        // 3D 위치 설정
        src.transform.position = e.position;

        // AudioSource 상태 완전 초기화 (풀링 부작용 방지)
        src.Stop();
        src.loop = false;
        src.mute = false;
        src.bypassEffects = false;
        src.bypassListenerEffects = false;
        src.bypassReverbZones = false;
        src.priority = 128;

        // 데이터 및 이벤트 파라미터 적용
        if (data.cueData != null)
        {
            src.clip = data.cueData.GetRandomClip();
            src.pitch = data.cueData.GetRandomPitch();
            src.volume = data.defaultVolume * e.volume * data.cueData.GetRandomVolumeModifier();
        }
        else
        {
            src.clip = data.clip;
            src.pitch = 1f;
            src.volume = data.defaultVolume * e.volume;
        }

        src.outputAudioMixerGroup = data.mixerGroup;
        // AudioEvent에서 전달된 is3D 설정을 우선 적용
        src.spatialBlend = e.is3D ? 1f : 0f;

        src.Play();
    }

    private AudioSource GetAvailableSource()
    {
        int count = sourcePool.Count;
        // foreach 대신 for 루프를 사용하여 가비지 발생 차단
        for (int i = 0; i < count; i++)
        {
            if (!sourcePool[i].isPlaying)
                return sourcePool[i];
        }

        // 모든 소스가 사용 중일 경우 첫 번째 소스(가장 오래된 것일 가능성이 높음)를 재사용
        return sourcePool[0];
    }

    private void CreateBGMSource()
    {
        var obj = new GameObject("BGMSource");
        obj.transform.parent = transform;

        bgmSource = obj.AddComponent<AudioSource>();
        bgmSource.playOnAwake = false;
        bgmSource.loop = true;
        bgmSource.spatialBlend = 0f;
    }

    public void PlayBGM(SoundID bgmId, float volume = 1f)
    {
        if (audioCache == null || !audioCache.TryGetValue(bgmId, out AudioData data))
        {
            Debug.LogWarning($"BGM ID '{bgmId}' not found in cache.");
            return;
        }

        if (bgmFadeCoroutine != null)
            StopCoroutine(bgmFadeCoroutine);

        bgmFadeCoroutine = StartCoroutine(FadeBGMInternal(data, volume));
    }

    private System.Collections.IEnumerator FadeBGMInternal(AudioData data, float targetVolume)
    {
        float startVolume = bgmSource.volume;
        
        // 1. 기존 BGM 페이드 아웃
        if (bgmSource.isPlaying && startVolume > 0)
        {
            float timer = 0f;
            while (timer < bgmFadeDuration)
            {
                timer += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / bgmFadeDuration);
                yield return null;
            }
        }

        // 2. 새로운 BGM 설정
        if (data.cueData != null)
        {
            bgmSource.clip = data.cueData.GetRandomClip();
            bgmSource.pitch = data.cueData.GetRandomPitch();
        }
        else
        {
            bgmSource.clip = data.clip;
            bgmSource.pitch = 1f;
        }

        bgmSource.outputAudioMixerGroup = data.mixerGroup;
        bgmSource.Play();

        // 3. 페이드 인
        float finalTargetVolume = data.defaultVolume * targetVolume;
        if (data.cueData != null) finalTargetVolume *= data.cueData.GetRandomVolumeModifier();

        float fadeInTimer = 0f;
        while (fadeInTimer < bgmFadeDuration)
        {
            fadeInTimer += Time.deltaTime;
            bgmSource.volume = Mathf.Lerp(0f, finalTargetVolume, fadeInTimer / bgmFadeDuration);
            yield return null;
        }

        bgmSource.volume = finalTargetVolume;
        bgmFadeCoroutine = null;
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
