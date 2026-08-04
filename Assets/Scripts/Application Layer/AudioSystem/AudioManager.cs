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

    [Header("3D Sound Distance Settings")]
    // 해상도 설정(16:9/16:10 등)에 따라 PixelPerfectCamera의 orthographicSize가 실제로 달라지므로,
    // 고정값 대신 재생 시점마다 Camera.main의 현재 값을 읽어 매번 다시 계산한다 (EnsureDistanceRolloffUpToDate 참고).
    // 화면 대각선(가장 먼 화면 안 지점) 기준으로 이 배율만큼 여유를 둔 지점부터 완전 무음 처리한다.
    [SerializeField] private float farDistanceBuffer = 1.05f;
    // Camera.main을 찾을 수 없을 때만 쓰이는 대비용 기본값 (16:9, orthographicSize 5.625 기준).
    private const float FallbackOrthographicSize = 5.625f;
    private const float FallbackAspect = 16f / 9f;

    private float cachedNearDistance = -1f;
    private float cachedFarDistance = -1f;

    [Header("BGM Settings")]
    [SerializeField] private float bgmFadeDuration = 1f;

    private Queue<AudioEvent> eventQueue = new Queue<AudioEvent>(100);
    private List<AudioSource> sourcePool = new List<AudioSource>();
    private float[] sourceStartTime;
    private float[] sourceTargetVolume;
    private int[] sourcePlayId;
    private int playIdCounter;
    private Dictionary<AudioCueData, int> cueLastIndexCache = new Dictionary<AudioCueData, int>();

    private float production3DVolumeFactor = 1f;
    private Coroutine productionVolumeCoroutine;

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

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        StopAll3DSounds();
        SetProduction3DVolumeFactor(0f);
    }

    /// <summary>
    /// 씬 전환 시 또는 필요 시 현재 재생 중인 모든 3D 사운드를 즉시 정지하고 대기 큐를 비운다.
    /// </summary>
    public void StopAll3DSounds()
    {
        eventQueue.Clear();
        int count = sourcePool.Count;
        for (int i = 0; i < count; i++)
        {
            AudioSource src = sourcePool[i];
            if (src != null && src.spatialBlend > 0f)
            {
                src.Stop();
            }
        }
    }

    /// <summary>
    /// 카메라 연출 등 전역 연출용 3D 볼륨 계수를 즉시 설정한다 (0f~1f).
    /// </summary>
    public void SetProduction3DVolumeFactor(float factor)
    {
        if (productionVolumeCoroutine != null)
        {
            StopCoroutine(productionVolumeCoroutine);
            productionVolumeCoroutine = null;
        }

        production3DVolumeFactor = Mathf.Clamp01(factor);
        ApplyProductionVolumeToActiveSources();
    }

    /// <summary>
    /// 카메라 연출 등 전역 연출용 3D 볼륨 계수를 지정한 시간에 걸쳐 서서히 페이드한다.
    /// </summary>
    public void RampProduction3DVolume(float targetFactor, float duration)
    {
        if (productionVolumeCoroutine != null)
        {
            StopCoroutine(productionVolumeCoroutine);
            productionVolumeCoroutine = null;
        }

        productionVolumeCoroutine = StartCoroutine(RampProductionVolumeRoutine(targetFactor, duration));
    }

    private System.Collections.IEnumerator RampProductionVolumeRoutine(float targetFactor, float duration)
    {
        float startFactor = production3DVolumeFactor;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            production3DVolumeFactor = Mathf.Lerp(startFactor, targetFactor, duration > 0f ? timer / duration : 1f);
            ApplyProductionVolumeToActiveSources();
            yield return null;
        }

        production3DVolumeFactor = targetFactor;
        ApplyProductionVolumeToActiveSources();
        productionVolumeCoroutine = null;
    }

    private void ApplyProductionVolumeToActiveSources()
    {
        int count = sourcePool.Count;
        for (int i = 0; i < count; i++)
        {
            AudioSource src = sourcePool[i];
            if (src != null && src.spatialBlend > 0f && src.isPlaying && sourceTargetVolume != null)
            {
                src.volume = sourceTargetVolume[i] * production3DVolumeFactor;
            }
        }
    }

    private void CreatePool()
    {
        sourcePool.Capacity = poolSize;
        sourceStartTime = new float[poolSize];
        sourceTargetVolume = new float[poolSize];
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

        // 최초 생성 시점 기준으로 한 번 적용해둔다 (실제 재생 시점에 다시 최신값으로 갱신됨).
        EnsureDistanceRolloffUpToDate();
    }

    private AnimationCurve cachedRolloffCurve;

    // 해상도/화면비가 바뀌면 Camera.main의 orthographicSize/aspect가 실제로 달라지므로,
    // 사운드 재생 시점마다 현재 값을 다시 계산해서 이전과 다를 때만(=해상도가 바뀐 경우에만)
    // 커브를 새로 만들어 전체 풀에 반영한다. 값이 그대로면 아무 것도 하지 않아 비용이 거의 없다.
    private void EnsureDistanceRolloffUpToDate()
    {
        Camera cam = Camera.main;
        float halfHeight = cam != null ? cam.orthographicSize : FallbackOrthographicSize;
        float halfWidth = cam != null ? halfHeight * cam.aspect : halfHeight * FallbackAspect;

        float near = Mathf.Min(halfWidth, halfHeight);
        float diagonal = Mathf.Sqrt(halfWidth * halfWidth + halfHeight * halfHeight);

        // 1번(0~near) 및 2번(near~cliffStart) 구간의 위치와 길이는 기존 그대로 100% 고정
        float baseFar = diagonal * 1.05f;
        float cliffStart = near + (baseFar - near) * 0.75f;

        // 마지막 3번 구간(cliffStart~far)의 길이만 늘려 완만하게 만들기 위해 전체 최대 거리(far)를 확장
        float far = diagonal * 1.4f;

        if (Mathf.Approximately(near, cachedNearDistance) && Mathf.Approximately(far, cachedFarDistance))
            return;

        cachedNearDistance = near;
        cachedFarDistance = far;

        cachedRolloffCurve = new AnimationCurve(
            new Keyframe(0f, 1f, 0f, 0f),
            new Keyframe(near, 1f, 0f, 0f),
            new Keyframe(cliffStart, 0.9f, 0f, 0f),
            new Keyframe(far, 0f, 0f, 0f)
        );

        for (int i = 0; i < sourcePool.Count; i++)
        {
            AudioSource source = sourcePool[i];
            source.rolloffMode = AudioRolloffMode.Custom;
            source.minDistance = near;
            source.maxDistance = far;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, cachedRolloffCurve);
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
    public AudioHandle PlayTracked(SoundID id, Vector3 position, float volume = 1f, bool is3D = true, float pitchOverride = -1f)
    {
        AudioData data = database != null ? database.Get(id) : null;
        if (data == null)
        {
            Debug.LogWarning($"Audio ID '{id}' not found in database.");
            return AudioHandle.Invalid;
        }

        return PlayOnAvailableSource(data, position, volume, is3D, pitchOverride);
    }

    public void StopTracked(AudioHandle handle)
    {
        if (!IsHandleValid(handle)) return;
        sourcePool[handle.sourceIndex].Stop();
    }

    // 다른 트랙 사운드로 전환할 때 현재 피치를 이어받기 위해(예: 파워업 도중 끊겨도 그 자리에서
    // 자연스럽게 이어지도록) 트랙 중인 소스의 현재 피치를 조회한다. 핸들이 유효하지 않으면 1(정상 피치)을 반환한다.
    public float GetTrackedPitch(AudioHandle handle)
    {
        if (!IsHandleValid(handle)) return 1f;
        return sourcePool[handle.sourceIndex].pitch;
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

    // 별도의 "예열음"이 없는 루프 사운드용: 낮은 피치로 재생을 시작해 목표 피치까지 서서히
    // 올리며(전원이 들어오듯) 재생한다. targetPitch를 호출부에서 넘겨받아, 도달하는 정상 피치
    // 자체를 다르게 줄 수 있다(예: 장비 속도 비율만큼 높여서 시작).
    public AudioHandle PlayTrackedWithPowerUp(SoundID id, Vector3 position, float volume = 1f, bool is3D = true, float duration = 0.4f, float minPitch = 0.1f, float targetPitch = 1f)
    {
        AudioData data = database != null ? database.Get(id) : null;
        if (data == null)
        {
            Debug.LogWarning($"Audio ID '{id}' not found in database.");
            return AudioHandle.Invalid;
        }

        AudioHandle handle = PlayOnAvailableSource(data, position, volume, is3D, minPitch);
        if (handle.IsValid)
            StartCoroutine(PowerUpRoutine(handle, duration, minPitch, targetPitch));

        return handle;
    }

    private System.Collections.IEnumerator PowerUpRoutine(AudioHandle handle, float duration, float minPitch, float targetPitch)
    {
        AudioSource src = sourcePool[handle.sourceIndex];

        float timer = 0f;
        while (timer < duration)
        {
            if (!IsHandleValid(handle)) yield break;

            timer += Time.deltaTime;
            src.pitch = Mathf.Lerp(minPitch, targetPitch, timer / duration);
            yield return null;
        }

        if (IsHandleValid(handle))
        {
            src.pitch = targetPitch;
        }
    }

    public void UpdateTrackedPosition(AudioHandle handle, Vector3 position)
    {
        if (!IsHandleValid(handle)) return;
        sourcePool[handle.sourceIndex].transform.position = FlattenToListenerZ(position);
    }

    // 이미 재생 중인 트랙 사운드의 피치를, 현재 피치를 시작점으로 삼아 목표 피치까지 서서히 바꾼다.
    // (PowerUpRoutine과 달리 재생 시작 시점이 아니라 임의의 시점에 호출해 이어서 적용할 수 있다.)
    public void RampTrackedPitch(AudioHandle handle, float targetPitch, float duration)
    {
        if (!IsHandleValid(handle)) return;
        StartCoroutine(RampPitchRoutine(handle, targetPitch, duration));
    }

    private System.Collections.IEnumerator RampPitchRoutine(AudioHandle handle, float targetPitch, float duration)
    {
        AudioSource src = sourcePool[handle.sourceIndex];
        float startPitch = src.pitch;

        float timer = 0f;
        while (timer < duration)
        {
            if (!IsHandleValid(handle)) yield break;

            timer += Time.deltaTime;
            src.pitch = Mathf.Lerp(startPitch, targetPitch, duration > 0f ? timer / duration : 1f);
            yield return null;
        }

        if (IsHandleValid(handle))
        {
            src.pitch = targetPitch;
        }
    }

    // 사운드 ID에 연결된 클립의 길이(초)를 조회한다. 클립 내 특정 지점(예: 20% 지점)에
    // 맞춰 다른 연출을 동기화해야 할 때 사용한다. AudioCueData(복수 클립) 사운드에는 사용하지 않는다.
    public float GetClipLength(SoundID id)
    {
        AudioData data = database != null ? database.Get(id) : null;
        if (data == null || data.clip == null) return 0f;
        return data.clip.length;
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

        // 해상도가 바뀌어 화면 범위가 달라졌다면 재생 직전에 감지해서 갱신한다.
        EnsureDistanceRolloffUpToDate();

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

        bool is3DSound = data.is3D && is3D;
        float initialVolume = finalVolume;

        sourceTargetVolume[index] = finalVolume;

        // 3D 사운드이고 카메라/커브 정보가 유효하다면, 재생 시작 시점의 거리를 사전 계산하여
        // 오디오 스레드가 첫 프레임(약 20ms)에 3D 거리 감쇠를 반영하지 못해 발생하는 팝 현상(풀 볼륨 출력)을 방지한다.
        if (is3DSound && Camera.main != null && cachedRolloffCurve != null)
        {
            Vector3 srcPos = FlattenToListenerZ(position);
            Vector3 listenerPos = Camera.main.transform.position;
            float dist = Vector3.Distance(srcPos, listenerPos);
            float initialAttenuation = cachedRolloffCurve.Evaluate(dist);
            initialVolume = finalVolume * initialAttenuation;
        }

        if (is3DSound)
        {
            initialVolume *= production3DVolumeFactor;
        }

        src.clip = clip;
        src.pitch = pitch;
        src.volume = initialVolume;
        src.outputAudioMixerGroup = data.mixerGroup;
        // 사운드 데이터의 is3D를 기준으로 하되, 호출부에서 2D로 강제하는 것은 허용한다(예: PlayUI).
        src.spatialBlend = is3DSound ? 1f : 0f;

        src.Play();

        playIdCounter++;
        sourceStartTime[index] = Time.time;
        sourcePlayId[index] = playIdCounter;

        AudioHandle handle = new AudioHandle(index, playIdCounter);

        // 3D 사운드는 1프레임 뒤 오디오 스레드 3D 스파티얼라이저가 동기화되면 기본 볼륨(finalVolume * productionFactor)으로 원복해,
        // 이후 이동이나 카메라 변화에 따른 동적 거리 감쇠가 Unity 엔진 표준 방식으로 계속 적용되게 한다.
        if (is3DSound)
        {
            StartCoroutine(SyncVolumeNextFrameRoutine(handle, finalVolume));
        }

        return handle;
    }

    private System.Collections.IEnumerator SyncVolumeNextFrameRoutine(AudioHandle handle, float targetVolume)
    {
        yield return null;
        if (IsHandleValid(handle))
        {
            sourcePool[handle.sourceIndex].volume = sourceTargetVolume[handle.sourceIndex] * production3DVolumeFactor;
        }
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
