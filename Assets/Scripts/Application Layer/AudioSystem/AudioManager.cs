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
    private float[] sourceStartTime;
    private int[] sourcePlayId;
    private int playIdCounter;
    private Dictionary<AudioCueData, int> cueLastIndexCache = new Dictionary<AudioCueData, int>();

    private AudioSource bgmSource;
    private Coroutine bgmFadeCoroutine;

    // 2D 게임이므로 카메라(리스너)의 Z축 거리가 3D 사운드 감쇠/패닝에 영향을 주면 안 된다.
    // 발음원의 Z를 카메라의 고정 Z값으로 맞춰서 거리 계산이 X/Y(화면상 좌우/상하)만으로 이뤄지게 한다.
    private const float ListenerZ = -8.34f;

    private Vector3 FlattenToListenerZ(Vector3 worldPosition)
    {
        return new Vector3(worldPosition.x, worldPosition.y, ListenerZ);
    }

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
    }

    private void CreatePool()
    {
        sourcePool.Capacity = poolSize;
        sourceStartTime = new float[poolSize];
        sourcePlayId = new int[poolSize];
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
        AudioData data = database != null ? database.Get(e.soundId) : null;
        if (data == null)
        {
            Debug.LogWarning($"Audio ID '{e.soundId}' not found in database.");
            return;
        }

        if (!PlayOnAvailableSource(data, e.position, e.volume, e.is3D, e.pitchOverride).IsValid)
            Debug.LogWarning($"[AudioManager] Sound '{e.soundId}' could not be played (missing clip, or no source available).");
    }

    // 큐를 거치지 않고 즉시 재생하며, 이후 Stop/위치 갱신이 가능하도록 핸들을 반환한다.
    // 루프 사운드처럼 재생 이후에도 개별적으로 제어해야 하는 경우에 사용한다.
    public AudioHandle PlayTracked(SoundID id, Vector3 position, float volume = 1f, bool is3D = true)
    {
        AudioData data = database != null ? database.Get(id) : null;
        if (data == null)
        {
            Debug.LogWarning($"Audio ID '{id}' not found in database.");
            return AudioHandle.Invalid;
        }

        return PlayOnAvailableSource(data, position, volume, is3D, -1f);
    }

    public void StopTracked(AudioHandle handle)
    {
        if (!IsHandleValid(handle)) return;
        sourcePool[handle.sourceIndex].Stop();
    }

    // 별도의 "정지음"이 없는 루프 사운드용: 피치를 극한으로 낮추고 볼륨을 0으로 줄이며
    // 기계가 서서히 전원이 꺼지듯 페이드아웃한 뒤 정지한다.
    public void StopTrackedWithPowerDown(AudioHandle handle, float duration = 0.4f, float minPitch = 0.1f)
    {
        if (!IsHandleValid(handle)) return;
        StartCoroutine(PowerDownRoutine(handle, duration, minPitch));
    }

    private System.Collections.IEnumerator PowerDownRoutine(AudioHandle handle, float duration, float minPitch)
    {
        AudioSource src = sourcePool[handle.sourceIndex];
        float startPitch = src.pitch;
        float startVolume = src.volume;

        float timer = 0f;
        while (timer < duration)
        {
            // 대기 중 슬롯이 다른 사운드에 강탈되면(재사용) 더 이상 손대지 않고 중단한다.
            if (!IsHandleValid(handle)) yield break;

            timer += Time.deltaTime;
            float t = timer / duration;
            src.pitch = Mathf.Lerp(startPitch, minPitch, t);
            src.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }

        if (IsHandleValid(handle))
        {
            src.Stop();
        }
    }

    public void UpdateTrackedPosition(AudioHandle handle, Vector3 position)
    {
        if (!IsHandleValid(handle)) return;
        sourcePool[handle.sourceIndex].transform.position = FlattenToListenerZ(position);
    }

    private bool IsHandleValid(AudioHandle handle)
    {
        if (!handle.IsValid || handle.sourceIndex >= sourcePool.Count) return false;
        // 슬롯이 이미 다른 사운드에 재사용되었다면(강탈) 이 핸들은 더 이상 유효하지 않다.
        return sourcePlayId[handle.sourceIndex] == handle.playId;
    }

    private AudioHandle PlayOnAvailableSource(AudioData data, Vector3 position, float volume, bool is3D, float pitchOverride)
    {
        AudioClip clip;
        float pitch;
        float finalVolume;

        if (data.cueData != null)
        {
            clip = PickClipFromCue(data.cueData);
            pitch = data.cueData.GetRandomPitch();
            finalVolume = data.defaultVolume * volume * data.cueData.GetRandomVolumeModifier();
        }
        else
        {
            clip = data.clip;
            pitch = 1f;
            finalVolume = data.defaultVolume * volume;
        }

        if (pitchOverride >= 0f) pitch = pitchOverride;

        if (clip == null) return AudioHandle.Invalid;

        int index = GetAvailableSourceIndex();
        if (index < 0) return AudioHandle.Invalid;

        AudioSource src = sourcePool[index];

        // 3D 위치 설정 (Z는 리스너 기준으로 맞춰 2D 게임에서 카메라 거리로 인한 왜곡을 없앤다)
        src.transform.position = FlattenToListenerZ(position);

        // AudioSource 상태 완전 초기화 (풀링 부작용 방지)
        src.Stop();
        src.loop = data.loop;
        src.mute = false;
        src.bypassEffects = false;
        src.bypassListenerEffects = false;
        src.bypassReverbZones = false;
        src.priority = 128;

        src.clip = clip;
        src.pitch = pitch;
        src.volume = finalVolume;
        src.outputAudioMixerGroup = data.mixerGroup;
        // 사운드 데이터의 is3D를 기준으로 하되, 호출부에서 2D로 강제하는 것은 허용한다(예: PlayUI).
        src.spatialBlend = (data.is3D && is3D) ? 1f : 0f;

        src.Play();

        playIdCounter++;
        sourceStartTime[index] = Time.time;
        sourcePlayId[index] = playIdCounter;

        return new AudioHandle(index, playIdCounter);
    }

    private AudioClip PickClipFromCue(AudioCueData cue)
    {
        int lastIndex = cueLastIndexCache.TryGetValue(cue, out int idx) ? idx : -1;
        AudioClip clip = cue.GetRandomClip(ref lastIndex);
        cueLastIndexCache[cue] = lastIndex;
        return clip;
    }

    private int GetAvailableSourceIndex()
    {
        int count = sourcePool.Count;
        if (count == 0) return -1;

        // foreach 대신 for 루프를 사용하여 가비지 발생 차단
        for (int i = 0; i < count; i++)
        {
            if (!sourcePool[i].isPlaying)
                return i;
        }

        // 모든 소스가 사용 중일 경우, 재생을 가장 먼저 시작해 가장 먼저 끝날 가능성이 높은 소스를 강탈
        int oldestIndex = 0;
        float oldestTime = sourceStartTime[0];
        for (int i = 1; i < count; i++)
        {
            if (sourceStartTime[i] < oldestTime)
            {
                oldestTime = sourceStartTime[i];
                oldestIndex = i;
            }
        }
        return oldestIndex;
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
        AudioData data = database != null ? database.Get(bgmId) : null;
        if (data == null)
        {
            Debug.LogWarning($"BGM ID '{bgmId}' not found in database.");
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
            bgmSource.clip = PickClipFromCue(data.cueData);
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

    // 지정한 시간에 걸쳐 볼륨을 0까지 낮춘 뒤 정지한다 (예: 던전->타운 복귀 시 카메라가 하늘로
    // 올라가는 연출 시간 안에 반드시 꺼지도록 그 시간과 맞춰 호출).
    public void FadeOutBGM(float duration)
    {
        if (bgmFadeCoroutine != null)
            StopCoroutine(bgmFadeCoroutine);

        bgmFadeCoroutine = StartCoroutine(FadeOutBGMInternal(duration));
    }

    private System.Collections.IEnumerator FadeOutBGMInternal(float duration)
    {
        float startVolume = bgmSource.volume;

        if (bgmSource.isPlaying && startVolume > 0f && duration > 0f)
        {
            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / duration);
                yield return null;
            }
        }

        bgmSource.volume = 0f;
        bgmSource.Stop();
        bgmFadeCoroutine = null;
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
