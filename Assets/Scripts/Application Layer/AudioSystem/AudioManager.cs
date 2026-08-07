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

    [Header("Debug Settings")]
    [SerializeField] private bool enableDebugSound = false;
    [SerializeField] private SoundID debugSoundId;

    private Queue<AudioEvent> eventQueue = new Queue<AudioEvent>(100);
    private List<AudioSource> sourcePool = new List<AudioSource>();
    private float[] sourceStartTime;
    // 슬롯이 재생 중인 사운드의 DB 기준 볼륨(defaultVolume × 큐 랜덤 보정). SetTrackedVolume이
    // Play(volume)과 동일하게 "0~1 배율"을 받도록 하기 위한 기준값이다.
    private float[] sourceBaseVolume;
    // 위 기준값에 호출부가 지정한 배율까지 곱한 "의도한 볼륨". 실제 src.volume은 여기에
    // 연출용 전역 계수(production3DVolumeFactor)를 한 번 더 곱해서 정해진다(ApplySourceVolume 참고).
    private float[] sourceTargetVolume;
    private int[] sourcePlayId;
    // 슬롯이 지금 재생 중인 사운드의 ID. 같은 SoundID가 동시에 몇 개나 겹쳐 재생 중인지 세어
    // 폴리포니 제한/감쇠(maxConcurrentVoices, polyphonyAttenuationStrength)를 적용하는 데 쓴다.
    private SoundID[] sourceSoundID;
    // 재생 시작 시점에 계산된 폴리포니 감쇠 계수(0~1, 미적용 시 1). SetTrackedVolume이 볼륨을
    // 다시 계산할 때 이 계수를 빼먹으면 감쇠가 통째로 풀려 소리가 원래 크기로 튄다.
    private float[] sourcePolyphonyAttenuation;
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

    // Bootstrap 오브젝트가 MainMenuScene에 들어 있어서, 메인 메뉴로 돌아올 때마다 그 하위의
    // AudioManager까지 통째로 중복 생성된다. 중복 인스턴스는 Awake()에서 Instance 검사에 걸려
    // CreatePool()을 거치지 않고 빠져나가므로 sourceStartTime/sourceBaseVolume 등 내부 배열이
    // 전부 null인 상태다. 그런데도 여기서 sceneLoaded에 등록해 버리면(파괴는 프레임 끝이라
    // OnEnable은 그대로 실행된다) 준비되지 않은 인스턴스의 OnSceneLoaded가 함께 호출된다.
    // 지금은 eventQueue/sourcePool이 필드 초기화라 빈 컬렉션이어서 우연히 넘어가지만,
    // OnSceneLoaded가 배열을 건드리는 순간 메인 메뉴로 돌아올 때마다 NRE가 난다.
    // sceneLoaded 핸들러에서 터진 예외는 뒤에 등록된 BootStrap.OnSceneLoaded까지 막을 수 있어
    // 그대로 "메인 메뉴가 뜨지 않는" 증상이 되므로, 실제 인스턴스만 등록하도록 막는다.
    private void OnEnable()
    {
        if (Instance != this) return;

        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (Instance != this) return;

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

                // 여기서 끊은 소리를 가리키던 기존 핸들은 전부 무효화한다. playId를 그대로 두면
                // IsHandleValid()가 계속 true를 반환해서, 루프 사운드를 유지해야 하는 발음체가
                // "핸들이 살아있으니 아직 재생 중"이라고 오판하고 영영 다시 재생하지 않게 된다.
                // (playIdCounter는 단조 증가라 예전 핸들과 값이 겹치지 않는다.)
                playIdCounter++;
                sourcePlayId[i] = playIdCounter;
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
            if (src != null && src.spatialBlend > 0f && src.isPlaying)
            {
                ApplySourceVolume(i);
            }
        }
    }

    // src.volume에 쓰는 창구를 여기 하나로 모은다. 예전에는 재생 시작, 연출 계수 페이드, 파워다운
    // 페이드아웃이 각자 src.volume에 직접 써서 서로를 덮어썼다(특히 연출 계수 페이드가 매 프레임
    // 원래 볼륨으로 되돌려버려 파워다운 페이드아웃이 들리지 않았다).
    // 이제 각자는 sourceTargetVolume만 갱신하고, 실제 반영은 항상 이 함수를 거친다.
    private void ApplySourceVolume(int index)
    {
        if (sourceTargetVolume == null || index < 0 || index >= sourcePool.Count) return;

        AudioSource src = sourcePool[index];
        if (src == null) return;

        // 연출용 전역 계수는 3D 사운드에만 적용한다(UI/2D 사운드는 카메라 연출과 무관해야 한다).
        float factor = src.spatialBlend > 0f ? production3DVolumeFactor : 1f;
        src.volume = sourceTargetVolume[index] * factor;
    }

    private void CreatePool()
    {
        sourcePool.Capacity = poolSize;
        sourceStartTime = new float[poolSize];
        sourceBaseVolume = new float[poolSize];
        sourceTargetVolume = new float[poolSize];
        sourcePlayId = new int[poolSize];
        sourceSoundID = new SoundID[poolSize];
        sourcePolyphonyAttenuation = new float[poolSize];

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
        float baseFar = diagonal * farDistanceBuffer;
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

        if (enableDebugSound && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.backquoteKey.wasPressedThisFrame)
        {
            if (Camera.main != null && UnityEngine.InputSystem.Mouse.current != null)
            {
                Vector3 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                mousePos.z = Mathf.Abs(Camera.main.transform.position.z - ListenerZ);
                Vector3 worldPos = Camera.main.ScreenToWorldPoint(mousePos);
                EnqueueEvent(new AudioEvent(debugSoundId, worldPos));
            }
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
        int index = handle.sourceIndex;
        AudioSource src = sourcePool[index];
        float startPitch = src.pitch;
        // src.volume이 아니라 "의도한 볼륨"에서 시작한다. src.volume에는 연출용 전역 계수가 이미
        // 곱해져 있어서, 그걸 기준으로 잡으면 페이드 도중 계수가 올라갈 때 최종 볼륨이 어긋난다.
        float startTargetVolume = sourceTargetVolume[index];

        float timer = 0f;
        while (timer < duration)
        {
            // 대기 중 슬롯이 다른 사운드에 강탈되면(재사용) 더 이상 손대지 않고 중단한다.
            if (!IsHandleValid(handle)) yield break;

            timer += Time.deltaTime;
            float t = timer / duration;
            src.pitch = Mathf.Lerp(startPitch, minPitch, t);
            sourceTargetVolume[index] = Mathf.Lerp(startTargetVolume, 0f, t);
            ApplySourceVolume(index);
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

    // 핸들이 가리키는 소스가 지금 실제로 재생 중인지 확인한다. 핸들이 유효해도 클립 재생이 이미
    // 끝났거나(원샷) 외부에서 정지된 경우를 걸러내야 하는 쪽 - 특히 루프를 계속 유지해야 하는
    // 발음체 - 에서 이 값을 확인해 필요하면 다시 재생을 트리거한다.
    public bool IsTrackedPlaying(AudioHandle handle)
    {
        if (!IsHandleValid(handle)) return false;
        return sourcePool[handle.sourceIndex].isPlaying;
    }

    // 트랙 중인 사운드의 볼륨 배율을 즉시(보간 없이) 설정한다. 컨베이어 벨트처럼 이미 매 프레임
    // 자체 로직(가감속 등)으로 값을 계산해 미는 경우, 별도 코루틴 없이 그 값을 그대로 반영할 때 쓴다.
    //
    // _volumeScale은 Play(id, pos, volume)의 volume과 같은 의미의 0~1 배율이며, DB의 defaultVolume에
    // 곱해진다. 여기서 곧장 src.volume에 쓰면 (1) DB 볼륨 설정이 무시되고 (2) 카메라 연출용 전역
    // 덕킹(production3DVolumeFactor)까지 무시된다 - 씬 전환 직후 계수가 0인데도 벨트 루프처럼 매
    // 프레임 갱신되는 소리가 그걸 무시하고 원래 볼륨으로 튀어나오는 문제가 실제로 있었다.
    public void SetTrackedVolume(AudioHandle handle, float _volumeScale)
    {
        if (!IsHandleValid(handle)) return;

        int index = handle.sourceIndex;
        // 재생 시작 시 적용된 폴리포니 감쇠를 그대로 유지한다. 이걸 빼면 매 프레임 볼륨을 미는
        // 발음체(벨트 루프 등)에서 감쇠가 첫 갱신 순간 풀려 소리가 원래 크기로 튀어오른다.
        sourceTargetVolume[index] = sourceBaseVolume[index] * _volumeScale * sourcePolyphonyAttenuation[index];
        ApplySourceVolume(index);
    }

    public void SetTrackedPitch(AudioHandle handle, float pitch)
    {
        if (!IsHandleValid(handle)) return;
        sourcePool[handle.sourceIndex].pitch = pitch;
    }

    private bool IsHandleValid(AudioHandle handle)
    {
        if (!handle.IsValid || handle.sourceIndex >= sourcePool.Count) return false;
        // 슬롯이 이미 다른 사운드에 재사용되었다면(강탈) 이 핸들은 더 이상 유효하지 않다.
        return sourcePlayId[handle.sourceIndex] == handle.playId;
    }

    private AudioHandle PlayOnAvailableSource(AudioData data, Vector3 position, float volume, bool is3D, float pitchOverride = -1f)
    {
        AudioClip clip;
        float pitch;
        // baseVolume: 호출부 배율을 빼고 DB/큐만으로 결정되는 볼륨. 이후 SetTrackedVolume이
        // "0~1 배율"을 받아 이 값에 곱할 수 있도록 슬롯에 따로 보관한다.
        float baseVolume;

        if (data.cueData != null)
        {
            clip = PickClipFromCue(data.cueData);
            pitch = data.cueData.GetRandomPitch();
            baseVolume = data.defaultVolume * data.cueData.GetRandomVolumeModifier();
        }
        else
        {
            clip = data.clip;
            pitch = 1f;
            baseVolume = data.defaultVolume;
        }

        float finalVolume = baseVolume * volume;

        if (pitchOverride >= 0f) pitch = pitchOverride;

        if (clip == null) return AudioHandle.Invalid;

        // 해상도가 바뀌어 화면 범위가 달라졌다면 재생 직전에 감지해서 갱신한다.
        // (아래 폴리포니 계산이 최신 가청 거리(cachedFarDistance)를 기준으로 세도록 먼저 호출한다.)
        EnsureDistanceRolloffUpToDate();

        // 같은 SoundID가 지금 몇 개나 겹쳐 재생 중인지 센다 (인크리멘탈 특성상 여러 발음원이
        // 동시에 같은 사운드를 울릴 때 합산 음량이 과도해지는 문제를 사운드별로 억제하기 위함).
        int activeVoices = CountActiveVoicesAndFindOldest(data.id, out int oldestSameSoundIndex);

        float polyphonyAttenuation = 1f;
        if (data.polyphonyAttenuationStrength > 0f)
        {
            // 비상관 음원 n개가 겹치면 체감 진폭은 대략 sqrt(n)로 늘어난다는 근거로,
            // 그 반대인 1/sqrt(1+n)를 감쇠 계수로 삼는다. strength(0~1)로 사운드별 적용 정도를 조절.
            float countFactor = 1f / Mathf.Sqrt(1f + activeVoices);
            polyphonyAttenuation = Mathf.Lerp(1f, countFactor, data.polyphonyAttenuationStrength);
            finalVolume *= polyphonyAttenuation;
        }

        int index;
        if (data.maxConcurrentVoices > 0 && activeVoices >= data.maxConcurrentVoices && oldestSameSoundIndex >= 0)
        {
            // 이 사운드만의 한도를 넘었다면, 전체 풀에서 아무 슬롯이나 강탈하는 대신 같은 SoundID 중
            // 가장 먼저 시작된 것을 이어받는다 - 관계없는 루프 사운드가 엉뚱하게 끊기는 일을 막는다.
            index = oldestSameSoundIndex;
        }
        else
        {
            index = GetAvailableSourceIndex();
        }
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

        bool is3DSound = data.is3D && is3D;
        float initialVolume = finalVolume;

        sourceBaseVolume[index] = baseVolume;
        sourceTargetVolume[index] = finalVolume;
        sourcePolyphonyAttenuation[index] = polyphonyAttenuation;

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
        sourceSoundID[index] = data.id;

        AudioHandle handle = new AudioHandle(index, playIdCounter);

        // 3D 사운드는 1프레임 뒤 오디오 스레드 3D 스파티얼라이저가 동기화되면 의도한 볼륨으로 원복해,
        // 이후 이동이나 카메라 변화에 따른 동적 거리 감쇠가 Unity 엔진 표준 방식으로 계속 적용되게 한다.
        if (is3DSound)
        {
            StartCoroutine(SyncVolumeNextFrameRoutine(handle));
        }

        return handle;
    }

    private System.Collections.IEnumerator SyncVolumeNextFrameRoutine(AudioHandle handle)
    {
        yield return null;
        if (IsHandleValid(handle))
        {
            ApplySourceVolume(handle.sourceIndex);
        }
    }

    private AudioClip PickClipFromCue(AudioCueData cue)
    {
        int lastIndex = cueLastIndexCache.TryGetValue(cue, out int idx) ? idx : -1;
        AudioClip clip = cue.GetRandomClip(ref lastIndex);
        cueLastIndexCache[cue] = lastIndex;
        return clip;
    }

    // 지정한 SoundID가 지금 몇 개의 슬롯에서 "실제로 들리게" 재생 중인지 세고, 그중 가장 먼저
    // 재생을 시작한 슬롯의 인덱스를 함께 돌려준다(폴리포니 한도 초과 시 이어받을 대상).
    // 풀 크기(최대 50)만 순회하므로 사운드 재생마다 호출해도 비용이 미미하다.
    private int CountActiveVoicesAndFindOldest(SoundID id, out int oldestIndex)
    {
        oldestIndex = -1;
        float oldestTime = float.MaxValue;
        int count = 0;

        Camera cam = Camera.main;
        Vector3 listenerPos = cam != null ? cam.transform.position : Vector3.zero;

        int poolCount = sourcePool.Count;
        for (int i = 0; i < poolCount; i++)
        {
            if (sourceSoundID[i] != id) continue;

            AudioSource src = sourcePool[i];
            if (!src.isPlaying) continue;

            // 호출부가 볼륨 0으로 재생한 소리는 세지 않는다. 던전에 있는 동안에도 마을의 제재소
            // 라인은 배경에서 계속 돌아가며 무음(볼륨 0)으로 사운드를 재생하는데(각 발음체의
            // GetSoundVolume() 참고), 마을 오브젝트는 DontDestroyOnLoad라 던전 카메라와 월드
            // 좌표상 가까울 수 있어 아래 거리 필터로도 걸러지지 않는다. 이걸 세면 던전에서 실제로
            // 들려야 할 소리가 들리지도 않는 마을 소리 때문에 감쇠되거나 슬롯을 빼앗긴다.
            // (production3DVolumeFactor에 의한 일시적 덕킹은 sourceTargetVolume에 반영되지 않으므로
            //  여기서 걸러지지 않는다 - 덕킹이 풀리는 순간 여러 소리가 한꺼번에 터지지 않도록 의도한 것이다.)
            if (sourceTargetVolume[i] <= 0.0001f) continue;

            // 가청 범위 밖(거리 감쇠로 이미 볼륨 0)에서 나는 3D 사운드는 세지 않는다. 이걸 세면
            // 맵 저편에서 NPC들이 내는, 플레이어에게 들리지도 않는 소리 때문에 정작 화면 안의
            // 소리가 깎이거나 잘려나간다 - 발음원이 맵 전체에 흩어지는 이 게임에서는 치명적이다.
            if (src.spatialBlend > 0f && cam != null &&
                Vector3.Distance(src.transform.position, listenerPos) >= cachedFarDistance)
                continue;

            count++;
            if (sourceStartTime[i] < oldestTime)
            {
                oldestTime = sourceStartTime[i];
                oldestIndex = i;
            }
        }

        return count;
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
        // 풀의 원샷 소스들과 priority(128)가 같으면 실보이스 한도(32) 경쟁에서 BGM이 밀려 뮤트될 수 있다.
        // 0(최우선)으로 고정해 BGM은 항상 실보이스에서 배제되지 않도록 한다.
        bgmSource.priority = 0;
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
