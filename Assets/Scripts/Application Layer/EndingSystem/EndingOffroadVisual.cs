using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 엔딩 크레딧 동안 오솔길을 달리는 오프로드 차량. 본편 차량의 차체·내부·바퀴·그림자 그림과 바퀴 애니메이터를 쓴다
/// (RoguelikeOffroadVisual과 같은 구성에 소풍 바구니를 실은 짐칸 그림을 더했다).
///
/// 위치와 속도는 EndingDirector가 정하고, 이 컴포넌트는 차체의 손맛만 맡는다.
///   주행: 본편 TravelSequence처럼 차체에 매 프레임 잔떨림(shakeIntensity)만 주고, 바퀴가 속도에 맞춰 돈다.
///         (위아래 출렁임·턱 밟기 스쿼시는 차가 떠 보이고 시동 임팩트처럼 보여 넣지 않는다.)
///         뒷바퀴 쪽에서 작은 흙먼지(본편 발걸음 먼지 Dust)가 퐁퐁 나온다.
///   출발(Launch): 크레딧이 끝나고 화면 밖으로 가속할 때. 앞으로 늘어났다 돌아오며 본편 시동 이펙트(StartUp)가 터지고,
///         그 뒤로는 본편 주행처럼 출발 이펙트(Go)가 일정 간격으로 계속 난다.
///         이미 달리던 차라 크랭킹이 든 본편 시동 소리(OffroadNonEdit) 대신 공회전 루프를 틀고 본편 주행 피치까지 올린다.
///   주차(Park): 소풍 장면. 바퀴와 흔들림이 멈춘다.
/// 움직임은 차체(Flip)에만 주고 그림자는 그대로 두어 차가 땅에 붙어 보이게 한다.
/// 정렬은 본편 CustomSortable과 같은 규칙(-y × 100)으로 매 프레임 맞춘다.
/// </summary>
public class EndingOffroadVisual : MonoBehaviour
{
    [SerializeField] private Transform flipRoot;
    [SerializeField] private Animator wheelAnimator;
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private VFXComponent vfxComponent;
    [Tooltip("바퀴 애니메이션 속도 = 차량 속도 × 이 값(본편은 0.2).")]
    [SerializeField] private float wheelSpeedPerUnit = 0.2f;
    [Tooltip("오솔길을 달리는 동안(퇴장 가속 전)만 바퀴 회전에 곱하는 값. 퇴장 가속 때는 곱하지 않는다.")]
    [SerializeField] private float pathWheelSpeedMultiplier = 1.4f;

    [Header("소풍 바구니 (리소스가 생기면 넣는다. 비어 있으면 본편 상자 그림)")]
    [SerializeField] private SpriteRenderer carriedContainerRenderer;
    [SerializeField] private Sprite picnicBasketCarriedSprite;
    [Tooltip("바구니를 내려놓은 뒤 보이는 빈 짐칸(본편 BoxUnCarry). 없으면 짐칸 그림만 숨긴다.")]
    [SerializeField] private SpriteRenderer emptyRackRenderer;

    [Header("주행 떨림")]
    [Tooltip("달리는 동안 매 프레임 잔떨림(월드). 본편 OffRoadVehicle.prefab의 shakeIntensity와 같은 값.")]
    [SerializeField] private float driveShake = 0.008f;

    [Header("흙먼지 (본편 발걸음 먼지)")]
    [SerializeField] private string dustTag = "Dust";
    [SerializeField] private Transform[] dustPoints;
    [SerializeField] private float dustIntervalMin = 0.18f;
    [SerializeField] private float dustIntervalMax = 0.42f;

    [Header("출발")]
    [SerializeField] private Transform startUpEffectPoint;
    [SerializeField] private Transform goEffectPoint;
    [Tooltip("출발 뒤 Go 이펙트를 다시 내는 간격(초). 본편 OffroadVehicleObj.goEffectInterval과 같은 값.")]
    [SerializeField] private float goEffectInterval = 0.2f;
    [SerializeField] private float launchStretch = 0.22f;
    [SerializeField] private float launchStretchSeconds = 0.2f;
    [Tooltip("출발 엔진 소리를 이 피치까지 올린다(본편 OffroadVehicleObj.engineRunPitchTarget과 같은 값).")]
    [SerializeField] private float launchEnginePitch = 1.4f;
    [SerializeField] private float launchEngineRampSeconds = 1f;
    [Tooltip("공회전 루프의 기본 볼륨(0.06)이 본편 시동 소리(0.09)보다 작아 그만큼 키운다.")]
    [SerializeField] private float launchEngineVolume = 1.5f;

    private enum EState
    {
        Drive,
        Park,
    }

    private EState state = EState.Drive;
    private Vector3 basePosition;
    private float speed;
    private float launchTime = -100f;
    private bool bLaunched;
    private float nextGoEffectTime;
    private AudioHandle engineHandle = AudioHandle.Invalid;
    private float nextDustTime;
    private int dustIndex;
    private ParticleSystem.MinMaxGradient effectColor = new ParticleSystem.MinMaxGradient(Color.white);
    private SpriteRenderer[] bodyRenderers;

    /// <summary>차체 그림(그림자 제외)이 차지하는 월드 영역. 앞쪽 나무가 차를 가리는지 볼 때 쓴다.</summary>
    public Bounds BodyBounds
    {
        get
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool bAny = false;
            if (null == bodyRenderers) return bounds;

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                SpriteRenderer sr = bodyRenderers[i];
                if (null == sr || false == sr.enabled || null == sr.sprite || false == sr.gameObject.activeInHierarchy) continue;

                if (bAny) bounds.Encapsulate(sr.bounds);
                else bounds = sr.bounds;
                bAny = true;
            }

            return bounds;
        }
    }

    private void Awake()
    {
        if (null != flipRoot) bodyRenderers = flipRoot.GetComponentsInChildren<SpriteRenderer>(true);

        if (null != carriedContainerRenderer && null != picnicBasketCarriedSprite)
        {
            carriedContainerRenderer.sprite = picnicBasketCarriedSprite;
        }

        basePosition = transform.position;
        nextDustTime = Time.time + Random.Range(dustIntervalMin, dustIntervalMax);
    }

    /// <summary>EndingDirector가 정한 이번 프레임의 위치(차체 기준점)와 속도. 손맛 오프셋은 이 위치 위에 얹는다.</summary>
    public void SetPose(Vector3 _position, float _speed)
    {
        basePosition = _position;
        speed = Mathf.Max(0f, _speed);
        state = EState.Drive;
        ApplyPose();
    }

    /// <summary>화면 밖으로 가속하기 시작한다. 본편 출발과 같은 시동·출발 이펙트가 난다.</summary>
    public void Launch()
    {
        launchTime = Time.time;
        bLaunched = true;
        nextGoEffectTime = Time.time + goEffectInterval;
        PlayEffect("StartUp", startUpEffectPoint);
        PlayEffect("Go", goEffectPoint);

        StopEngine(0f);
        engineHandle = Sound.PlayTracked(SoundID.OffroadNonEditLoop, basePosition, launchEngineVolume);
        Sound.RampTrackedPitch(engineHandle, launchEnginePitch, launchEngineRampSeconds);
    }

    /// <summary>출발 엔진 소리를 끈다. 화면이 검게 덮이는 시간에 맞춰 줄인다.</summary>
    public void StopEngine(float _fadeSeconds)
    {
        if (false == engineHandle.IsValid) return;

        if (_fadeSeconds > 0f) Sound.StopTrackedWithFadeOut(engineHandle, _fadeSeconds);
        else Sound.StopTracked(engineHandle);
        engineHandle = AudioHandle.Invalid;
    }

    /// <summary>짐칸에 바구니를 실었는지. 소풍 장면에서는 바구니를 돗자리 옆에 내려놓는다.</summary>
    public void SetCarrying(bool _bCarrying)
    {
        if (null != carriedContainerRenderer) carriedContainerRenderer.enabled = _bCarrying;
        if (null != emptyRackRenderer) emptyRackRenderer.enabled = false == _bCarrying;
    }

    /// <summary>소풍 장면에 세워 둔다. 바퀴·흔들림·먼지가 멈춘다.</summary>
    public void Park(Vector3 _position)
    {
        state = EState.Park;
        bLaunched = false;
        StopEngine(0f);
        basePosition = _position;
        speed = 0f;
        ApplyPose();
    }

    private void Update()
    {
        if (engineHandle.IsValid) Sound.UpdateTrackedPosition(engineHandle, basePosition);

        if (EState.Drive != state || speed <= 0.01f) return;

        float now = Time.time;
        if (now >= nextDustTime)
        {
            nextDustTime = now + Random.Range(dustIntervalMin, dustIntervalMax);
            SpawnDust();
        }

        // 본편 TravelSequence처럼 달리는 동안 Go 이펙트를 일정 간격으로 낸다.
        if (bLaunched && now >= nextGoEffectTime)
        {
            nextGoEffectTime = now + Mathf.Max(0.02f, goEffectInterval);
            PlayEffect("Go", goEffectPoint);
        }
    }

    private void LateUpdate()
    {
        ApplyPose();
    }

    private void ApplyPose()
    {
        float now = Time.time;
        Vector2 scale = Vector2.one;
        Vector3 bodyOffset = Vector3.zero;

        if (EState.Drive == state)
        {
            // 본편 TravelSequence의 ApplyShake와 같은 매 프레임 잔떨림. 달리지 않으면(속도 0) 떨지 않는다.
            if (speed > 0.01f)
            {
                bodyOffset += new Vector3(Random.Range(-driveShake, driveShake), Random.Range(-driveShake, driveShake), 0f);
            }

            // 출발: 앞으로 늘어났다가 스프링처럼 돌아온다.
            float launch = Mathf.Clamp01((now - launchTime) / Mathf.Max(0.01f, launchStretchSeconds));
            if (launch < 1f)
            {
                float k = Mathf.Sin(launch * Mathf.PI) * (1f - launch * 0.5f);
                scale += new Vector2(launchStretch, -launchStretch * 0.6f) * k;
            }
        }

        if (null != wheelAnimator)
        {
            float wheelScale = bLaunched ? 1f : pathWheelSpeedMultiplier;
            wheelAnimator.speed = speed * wheelSpeedPerUnit * wheelScale;
        }

        transform.position = basePosition;

        if (null != flipRoot)
        {
            flipRoot.localScale = new Vector3(Mathf.Max(0.1f, scale.x), Mathf.Max(0.1f, scale.y), 1f);
            flipRoot.localPosition = bodyOffset;
        }

        if (null != sortingGroup) sortingGroup.sortingOrder = -Mathf.RoundToInt(basePosition.y * 100f);
    }

    private void SpawnDust()
    {
        if (null == dustPoints || 0 == dustPoints.Length) return;

        dustIndex = (dustIndex + 1) % dustPoints.Length;
        PlayEffect(dustTag, dustPoints[dustIndex]);
    }

    // 본편 OffroadPlayEffect와 같은 설정(흰색 시작색, Objects 레이어, 차체 정렬 순서).
    private void PlayEffect(string _tag, Transform _point)
    {
        if (null == vfxComponent || null == _point) return;

        VFXPlaySettings settings = new VFXPlaySettings(_tag, _point.position, _point.rotation, effectColor);
        settings.OverrideSorting = true;
        settings.SortingLayerName = "Objects";
        settings.SortingOrder = -Mathf.RoundToInt(_point.position.y * 100f);
        vfxComponent.Play(settings);
    }

    private void OnDestroy()
    {
        StopEngine(0f);
    }
}
