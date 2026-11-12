using System.Collections.Generic;
using PresentationLayer.VFX;
using UnityEngine;

/// <summary>
/// 제재소 "특등품 평가"(가격 2배) 연출. 가공 라인 하나에 하나씩 붙는다(LogProcessLine.SetupTopgradePresentation).
///
/// 흐름 - 뿅(커터) → 톡(착지) → 쏙(평가기로) → 쾅(평가기):
/// 1) 커터에서 나온 원목이 특등품으로 미리 판정되면(LogEvaluator.PreRollTopgrade) 같은 모양의 "쌍둥이 원목"이
///    원목에서 튀어 올라 한 바퀴 돌고 원목 등 위에 착지한다.
/// 2) 원목에 업힌 채 벨트를 따라간다.
/// 3) 원목이 평가기로 미끄러져 들어갈 때 같이 뛰어든다.
/// 4) 평가기가 크게 눌렸다 튀고, 효과음·카메라·진동이 터진다. 짧은 간격으로 연달아 터지면 차임 음이 한 칸씩 올라간다.
///
/// 순수 연출이다. 돈·판정·벨트·세이브에는 아무것도 하지 않는다. 쌍둥이는 벨트 원목과 같은 풀의 LogItem을 빌려
/// 그리기만 하고, 벨트 목록·충돌·저장 어디에도 들어가지 않는다(상태 None이라 충돌 등록도 되지 않는다).
///
/// 벨트 속도·길이와 무관하게 맞물리도록, 쌍둥이의 모든 움직임은 "원목 기준 상대 위치"로 계산한다.
/// 원목이 어디로 얼마나 빨리 움직이든(컨베이어 특성, 가공 스택 가속, 벨트 증설로 통째로 밀림) 쌍둥이는 늘 원목 위에 있다.
/// 출고벨트가 아주 짧아서(1~3칸, 기본 속도로 0.2~1.3초) 튀어 오르는 시간은 남은 벨트 시간에 맞춰 줄인다.
/// 남은 시간보다 길면 착지 전에 원목이 들어가 버리는데, 그때는 공중에서 바로 따라 뛰어든다.
///
/// 이펙트(섬광·금가루·×2 글자)는 VFX_TopgradeAssessment가 그리고, 이 컴포넌트는 언제 어디서 터질지만 알려 준다.
///
/// 마을에 있을 때만 보여준다. 던전에 가 있는 동안에는 판정만 진행되고(돈은 그대로 2배), 마을로 돌아오면
/// 벨트 위에 남아 있는 특등품 원목에 쌍둥이를 다시 얹는다.
/// </summary>
public class TopgradeAssessmentPresenter : MonoBehaviour
{
    [Header("쌍둥이 원목 - 튀어 오르기")]
    [Tooltip("튀어 오르는 시간의 상한(초). 벨트가 길고 느릴 때 이만큼 쓴다.")]
    [SerializeField] private float popMaxDuration = 0.36f;
    [Tooltip("튀어 오르는 시간의 하한(초). 이보다 짧으면 튀었는지 읽히지 않는다. 벨트가 이보다 짧으면 착지 전에 뛰어든다.")]
    [SerializeField] private float popMinDuration = 0.14f;
    [Tooltip("원목이 평가기로 들어가기 시작하기 이만큼(초) 전에는 착지하도록 튀는 시간을 줄인다.")]
    [SerializeField] private float landBeforeExitMargin = 0.04f;
    [Tooltip("가장 길게 튈 때의 정점 높이(유닛).")]
    [SerializeField] private float popMaxApex = 0.6f;
    [Tooltip("가장 짧게 튈 때의 정점 높이(유닛).")]
    [SerializeField] private float popMinApex = 0.3f;
    [Tooltip("튀는 동안 도는 바퀴 수(시계 방향).")]
    [SerializeField] private float popSpinTurns = 1f;
    [Tooltip("튀어 오를 때 시작 크기(배율). 원목에서 쑥 빠져나오듯 작게 시작해 커진다.")]
    [SerializeField] private float popStartScale = 0.4f;
    [Tooltip("튀어 나오는 출발점을 원목에서 커터 쪽으로 이 비율만큼 옮긴다(0 = 원목 자리, 1 = 커터 자리).")]
    [SerializeField, Range(0f, 1f)] private float popFromCutterRatio = 0.25f;

    [Header("쌍둥이 원목 - 업혀 가기")]
    [Tooltip("원목 등 위에 얹히는 높이(유닛, 원목 중심 기준).")]
    [SerializeField] private float rideHeight = 0.19f;
    [Tooltip("쌍둥이 크기(배율).")]
    [SerializeField] private float twinScale = 1f;
    [Tooltip("착지 순간 눌림(가로 배율). 세로는 그만큼 줄어든다.")]
    [SerializeField] private float landSquash = 0.28f;
    [Tooltip("착지 눌림이 풀리는 시간(초).")]
    [SerializeField] private float landSquashDuration = 0.18f;
    [Tooltip("업혀 가는 동안 까딱거리는 높이(유닛).")]
    [SerializeField] private float rideBobAmplitude = 0.02f;
    [Tooltip("까딱거리는 빠르기(초당 횟수).")]
    [SerializeField] private float rideBobFrequency = 2.5f;
    [Tooltip("원목 그림의 절반 높이(유닛). 눌릴 때 바닥이 뜨지 않도록 위치를 보정하는 데 쓴다.")]
    [SerializeField] private float twinHalfHeight = 0.16f;

    [Header("쌍둥이 원목 - 뛰어들기")]
    [Tooltip("평가기로 뛰어들 때 살짝 튀는 높이(유닛).")]
    [SerializeField] private float diveHop = 0.08f;
    [Tooltip("원목보다 이만큼(초) 늦게 다 들어간다. 평가기 '쾅'과 거의 같은 순간이다.")]
    [SerializeField] private float diveLag = 0.04f;
    [Tooltip("원목이 갑자기 사라졌을 때(세이브 로드·라인 철거 등) 쌍둥이가 오그라들며 사라지는 시간(초).")]
    [SerializeField] private float vanishDuration = 0.12f;
    [Tooltip("마을로 돌아와 다시 얹을 때 커지는 시간(초).")]
    [SerializeField] private float reattachDuration = 0.15f;

    [Header("이펙트")]
    [Tooltip("업혀 가는 동안 반짝임이 피는 간격(초) 최소·최대.")]
    [SerializeField] private float rideSparkleIntervalMin = 0.14f;
    [SerializeField] private float rideSparkleIntervalMax = 0.24f;
    [Tooltip("반짝임이 생기는 범위(쌍둥이 중심 기준 반폭·반높이, 유닛).")]
    [SerializeField] private Vector2 rideSparkleExtents = new Vector2(0.32f, 0.12f);
    // 평가기 스프라이트(64x64 칸)는 아래쪽 32px만 그림이고 기준점이 바닥에서 4.8px 위라, 칸 경계(bounds)가 아니라
    // 기준점에서의 거리로 자리를 잡는다. 몸통 가운데 ≈ +0.35, 윗면 ≈ +0.85 유닛.
    [Tooltip("섬광·충격파·금화 분수가 터지는 자리(평가기 기준점에서, 유닛). 평가기 몸통 가운데.")]
    [SerializeField] private Vector2 stampImpactOffset = new Vector2(0f, 0.35f);
    [Tooltip("×2 글자 바닥 가운데 자리(평가기 기준점에서, 유닛). 평가기 윗면 바로 위.")]
    [SerializeField] private Vector2 stampLabelOffset = new Vector2(0f, 0.92f);

    [Header("평가기 쾅")]
    [SerializeField] private float stampBounceDuration = 0.45f;
    [SerializeField] private float stampBounceAmplitude = 0.28f;
    [Tooltip("콤보 한 단계마다 더해지는 반동 진폭.")]
    [SerializeField] private float stampBounceAmplitudePerCombo = 0.015f;
    [SerializeField] private float stampBounceAmplitudeMax = 0.36f;
    [SerializeField] private float stampBounceDecay = 3.2f;
    [SerializeField] private float stampBounceHalfCycles = 3.5f;
    [Tooltip("이 시간(초) 안에 같은 평가기에서 다시 터지면 콤보가 이어진다.")]
    [SerializeField] private float comboWindow = 1.2f;

    [Header("카메라 · 진동 (평가기가 화면에 보일 때만)")]
    [SerializeField] private float cameraKickDistance = 0.06f;
    [SerializeField] private float cameraKickSeconds = 0.2f;
    [Tooltip("라인 여러 개가 겹쳐 터져도 카메라는 이 간격(초)에 한 번만 찍는다.")]
    [SerializeField] private float cameraKickMinInterval = 0.25f;
    [Tooltip("화면 가장자리 여유(뷰포트 비율). 이만큼 밖에 있어도 보인다고 친다.")]
    [SerializeField] private float onScreenMargin = 0.05f;

    [Header("효과음")]
    [SerializeField] private SoundID revealSound = SoundID.NiceItem;
    [SerializeField] private float revealVolume = 1f;
    [SerializeField] private SoundID landSound = SoundID.ConvayerPut;
    [SerializeField] private float landVolume = 0.6f;
    [SerializeField] private float landPitch = 1.35f;
    [SerializeField] private SoundID stampSound = SoundID.BoxLandingEx;
    [SerializeField] private float stampVolume = 1f;
    [SerializeField] private SoundID chimeSound = SoundID.SFXLevelUpChime;
    [SerializeField] private float chimeVolume = 1f;
    [Tooltip("콤보 단계별 차임 음높이(반음). 단계가 끝까지 가면 마지막 값을 유지한다.")]
    [SerializeField] private float[] chimeComboSemitones = { 0f, 2f, 4f, 5f, 7f, 9f, 11f, 12f };

    // 원목 정렬 기준보다 이만큼 앞(지면 Y가 낮게)으로 그려 늘 원목 앞에 보이게 한다.
    // CustomSortable 정밀도가 100이라 0.04면 4칸 앞이다. 쌍둥이 아웃라인 스텐실(-1칸)까지 원목 본체보다 앞에 온다.
    private const float SortAheadOfMain = 0.04f;

    private enum ETwinState
    {
        Pop,
        Ride,
        Dive,
        Vanish,
    }

    private sealed class Twin
    {
        public LogItem main;
        public int ticket;
        public CustomSortable mainSortable;
        public LogItem body;
        public Transform bodyVisual;

        public ETwinState state;
        public float time;
        public float duration;

        // 원목 기준 상대 위치(튀어 오르기 출발점·뛰어들기 출발점)
        public Vector3 popFromOffset;
        public float popApex;
        public Vector3 diveFromOffset;
        public float diveFromScale;
        public float diveFromAngle;

        // 원목이 사라진 뒤에도 쌍둥이를 마저 그리기 위한 마지막 원목 상태
        public Vector3 lastMainPos;
        public float lastMainGroundY;

        public float landTime;      // 착지 후 경과(눌림 연출)
        public float bobPhase;
        public float scaleIn;       // 다시 얹을 때 커지는 진행(1이면 다 큼)
        public float sparkleTimer;  // 다음 반짝임까지 남은 시간
    }

    private LogItemPoolingManager pool;
    private LogCutter cutter;
    private LogInBelt outBelt;
    private LogEvaluator evaluator;
    private MapType mapType;

    private readonly List<Twin> twins = new List<Twin>(4);
    private readonly Stack<Twin> twinPool = new Stack<Twin>(4);
    private readonly List<LogItem> scratchItems = new List<LogItem>(8);

    private int comboCount = 0;
    private float lastStampTime = float.NegativeInfinity;

    // 라인 여러 개가 같은 순간에 터져도 카메라는 한 번만 찍는다(라인마다 컴포넌트가 따로라 정적으로 공유).
    private static float lastCameraKickTime = float.NegativeInfinity;

    private Camera cachedCamera;
    private VFX_TopgradeAssessment vfx;

    public void Setup(LogItemPoolingManager _pool, LogCutter _cutter, LogInBelt _outBelt, LogEvaluator _evaluator)
    {
        Unbind();

        pool = _pool;
        cutter = _cutter;
        outBelt = _outBelt;
        evaluator = _evaluator;

        if (outBelt != null) outBelt.LogExitStartedEvent += OnLogExitStarted;
        if (evaluator != null) evaluator.TopgradeAssessedEvent += OnTopgradeAssessed;

        // 이펙트는 라인 자식으로 하나만 둔다(라인과 함께 꺼지고 사라진다). 셰이더가 없으면 null이고 이펙트 없이 진행한다.
        if (vfx == null) vfx = GetComponentInChildren<VFX_TopgradeAssessment>(true);
        if (vfx == null) vfx = VFX_TopgradeAssessment.Create(transform);
    }

    private void Unbind()
    {
        if (outBelt != null) outBelt.LogExitStartedEvent -= OnLogExitStarted;
        if (evaluator != null) evaluator.TopgradeAssessedEvent -= OnTopgradeAssessed;
    }

    private void OnDestroy()
    {
        Unbind();
        ClearAllTwins();
    }

    // 라인이 꺼지면(철거·세이브 로드로 라인 수가 줄어듦) 원목들도 더 이상 움직이지 않는다. 쌍둥이를 남겨 두면
    // 허공에 멈춰 보이므로 즉시 거둔다. 콤보도 끊는다.
    private void OnDisable()
    {
        ClearAllTwins();
        comboCount = 0;
        lastStampTime = float.NegativeInfinity;
    }

    // 다시 켜지면 벨트에 남아 있던 특등품 원목에 쌍둥이를 다시 얹는다(맵 복귀와 같은 처리).
    // 처음 붙을 때는 Setup 전이라 IsPresenting이 거짓이어서 아무것도 하지 않는다.
    private void OnEnable()
    {
        ReattachTwins();
    }

    public void SetMapType(MapType _mapType)
    {
        MapType prev = mapType;
        mapType = _mapType;

        if (mapType != MapType.Town)
        {
            ClearAllTwins();
            if (vfx != null) vfx.ClearAll();
            comboCount = 0;
            lastStampTime = float.NegativeInfinity;
            return;
        }

        if (prev != MapType.Town) ReattachTwins();
    }

    private bool IsPresenting => mapType == MapType.Town && isActiveAndEnabled && pool != null;

    // ---------------------------------------------------------------- 1) 커터에서 튀어나오기

    public void OnLogCut(LogItem _main)
    {
        if (!IsPresenting || _main == null) return;
        if (_main.TopgradePreRoll != ETopgradePreRoll.Hit) return;

        Twin twin = SpawnTwin(_main);
        if (twin == null) return;

        // 남은 벨트 시간에 맞춰 튀는 시간을 정한다(착지 후 조금이라도 업혀 가도록 여유를 둔다).
        float popDuration = popMaxDuration;
        if (outBelt != null && outBelt.TryEstimateSecondsToExit(_main, out float secondsToExit))
        {
            popDuration = Mathf.Clamp(secondsToExit - landBeforeExitMargin, popMinDuration, popMaxDuration);
        }

        float apexT = Mathf.InverseLerp(popMinDuration, popMaxDuration, popDuration);

        twin.state = ETwinState.Pop;
        twin.time = 0f;
        twin.duration = popDuration;
        twin.popApex = Mathf.Lerp(popMinApex, popMaxApex, apexT);

        Vector3 fromCutter = Vector3.zero;
        if (cutter != null)
        {
            fromCutter = cutter.transform.position - _main.transform.position;
            fromCutter.z = 0f;
        }
        twin.popFromOffset = fromCutter * popFromCutterRatio;

        ApplyTwin(twin, 0f);
        if (vfx != null) vfx.PlayReveal(twin.body.transform.position);

        Sound.Play(revealSound, _main.transform.position, revealVolume, pitchOverride: Random.Range(0.97f, 1.04f));
    }

    // 마을로 돌아왔을 때 벨트 위에 남은 특등품 원목에 쌍둥이를 다시 얹는다(튀어 오르는 연출·소리 없이 커지기만).
    private void ReattachTwins()
    {
        if (!IsPresenting || outBelt == null) return;

        outBelt.CollectActiveItems(scratchItems);
        for (int i = 0; i < scratchItems.Count; i++)
        {
            LogItem main = scratchItems[i];
            if (main == null || main.TopgradePreRoll != ETopgradePreRoll.Hit) continue;
            if (FindTwin(main) != null) continue;

            Twin twin = SpawnTwin(main);
            if (twin == null) continue;

            twin.state = ETwinState.Ride;
            twin.time = 0f;
            twin.landTime = landSquashDuration; // 눌림 없이
            twin.scaleIn = 0f;
            ApplyTwin(twin, 0f);
        }
        scratchItems.Clear();
    }

    private Twin SpawnTwin(LogItem _main)
    {
        LogItem body = pool.GetLogItem(_main.treeType, _main.logState);
        if (body == null) return null;

        // 원목과 똑같이 생긴 제재목. 보석 등급이면 벨트 원목처럼 반짝임도 붙는다.
        body.SetTimberSprite();
        body.PlayGemShiny();

        Twin twin = twinPool.Count > 0 ? twinPool.Pop() : new Twin();
        twin.main = _main;
        twin.ticket = _main.TopgradeTicket;
        twin.mainSortable = _main.GetComponent<CustomSortable>();
        twin.body = body;
        twin.bodyVisual = body.spriteRenderer != null ? body.spriteRenderer.transform : body.transform;
        twin.time = 0f;
        twin.duration = 0f;
        twin.landTime = 0f;
        twin.bobPhase = Random.Range(0f, Mathf.PI * 2f);
        twin.scaleIn = 1f;
        twin.sparkleTimer = 0f;
        twin.lastMainPos = _main.transform.position;
        twin.lastMainGroundY = twin.mainSortable != null ? twin.mainSortable.GroundPosition.y : twin.lastMainPos.y;

        twins.Add(twin);
        return twin;
    }

    private Twin FindTwin(LogItem _main)
    {
        for (int i = 0; i < twins.Count; i++)
        {
            if (twins[i].main == _main && twins[i].ticket == _main.TopgradeTicket) return twins[i];
        }
        return null;
    }

    // ---------------------------------------------------------------- 3) 평가기로 뛰어들기

    private void OnLogExitStarted(LogItem _main, Vector3 _exitTarget, float _exitDuration)
    {
        if (_main == null) return;

        Twin twin = FindTwin(_main);
        if (twin == null || twin.state == ETwinState.Dive || twin.state == ETwinState.Vanish) return;

        // 지금 그려진 자리에서 그대로 뛰어든다(튀어 오르는 도중이어도 이어진다).
        twin.diveFromOffset = twin.body.transform.position - _main.transform.position;
        twin.diveFromOffset.z = 0f;
        // 튀어 오르기 도중(커지며 살짝 넘친 순간)에 끊겨도 뛰어들기 예비 동작이 겹쳐 너무 커지지 않게 원래 크기로 묶는다.
        twin.diveFromScale = Mathf.Min(twin.body.transform.localScale.x, twinScale);
        twin.diveFromAngle = twin.bodyVisual.localEulerAngles.z;
        twin.state = ETwinState.Dive;
        twin.time = 0f;
        twin.duration = Mathf.Max(0.02f, _exitDuration + diveLag);
    }

    // ---------------------------------------------------------------- 4) 평가기 쾅

    private void OnTopgradeAssessed(long _basePrice, long _finalPrice)
    {
        if (mapType != MapType.Town || !isActiveAndEnabled) return;

        float now = Time.time;
        comboCount = (now - lastStampTime <= comboWindow) ? comboCount + 1 : 1;
        lastStampTime = now;

        LogStorage storage = evaluator != null ? evaluator.Storage : null;
        Vector3 stampPos = storage != null ? storage.transform.position : transform.position;

        if (vfx != null)
        {
            // 섬광·분수는 평가기 몸통 가운데, ×2는 평가기 윗면 바로 위(기준점은 반동과 무관하게 고정이다).
            vfx.PlayStamp(stampPos + (Vector3)stampImpactOffset, stampPos + (Vector3)stampLabelOffset, comboCount);
        }

        if (storage != null)
        {
            float amplitude = Mathf.Min(stampBounceAmplitudeMax, stampBounceAmplitude + stampBounceAmplitudePerCombo * (comboCount - 1));
            storage.TriggerTopgradeBounce(stampBounceDuration, amplitude, stampBounceDecay, stampBounceHalfCycles);
        }

        Sound.Play(stampSound, stampPos, stampVolume, pitchOverride: Random.Range(0.96f, 1.04f));
        Sound.Play(chimeSound, stampPos, chimeVolume, pitchOverride: GetChimePitch(comboCount));

        if (!IsOnScreen(stampPos)) return;

        Rumble.Play(EHapticEvent.TopgradeAssessment);

        // now < last: 정적 값이 이전 플레이 세션(도메인 리로드 없이 재시작)의 시각을 들고 있는 경우. 막지 않고 새로 센다.
        if (now - lastCameraKickTime >= cameraKickMinInterval || now < lastCameraKickTime)
        {
            lastCameraKickTime = now;
            // 흔들림(perlin)은 걸지 않고 아래로 한 번 찍기만 한다 - 돌고 있던 다른 흔들림을 끊지 않는다.
            CameraMoveController.Instance?.ShakeCameraKick(Vector3.down, cameraKickDistance, cameraKickSeconds, 0f, 0f);
        }
    }

    private float GetChimePitch(int _combo)
    {
        if (chimeComboSemitones == null || chimeComboSemitones.Length == 0) return 1f;

        int step = Mathf.Clamp(_combo - 1, 0, chimeComboSemitones.Length - 1);
        return Mathf.Pow(2f, chimeComboSemitones[step] / 12f);
    }

    private bool IsOnScreen(Vector3 _worldPos)
    {
        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
        if (cachedCamera == null) return false;

        Vector3 vp = cachedCamera.WorldToViewportPoint(_worldPos);
        return vp.x >= -onScreenMargin && vp.x <= 1f + onScreenMargin
            && vp.y >= -onScreenMargin && vp.y <= 1f + onScreenMargin;
    }

    // ---------------------------------------------------------------- 매 프레임

    // 벨트(Update)가 원목을 옮기고 정렬까지 끝낸 뒤에 따라간다.
    private void LateUpdate()
    {
        if (twins.Count == 0) return;

        float dt = Time.deltaTime;

        for (int i = twins.Count - 1; i >= 0; i--)
        {
            Twin twin = twins[i];

            if (twin.body == null || twin.body.IsPooled || !twin.body.gameObject.activeInHierarchy)
            {
                // 다른 경로가 쌍둥이를 거둬 갔다(풀 정리 등). 우리 쪽 기록만 지운다.
                RemoveTwinAt(i, false);
                continue;
            }

            bool bMainValid = IsMainValid(twin);
            if (bMainValid)
            {
                twin.lastMainPos = twin.main.transform.position;
                if (twin.mainSortable != null) twin.lastMainGroundY = twin.mainSortable.GroundPosition.y;
            }
            else if (twin.state == ETwinState.Pop || twin.state == ETwinState.Ride)
            {
                // 원목이 뛰어들기 없이 사라졌다(세이브 로드·벨트 증설로 통째로 밀리며 즉시 평가 등). 그 자리에서 오그라든다.
                BeginVanish(twin);
            }

            twin.time += dt;

            bool bFinished = ApplyTwin(twin, dt);
            if (bFinished) RemoveTwinAt(i, true);
        }
    }

    private static bool IsMainValid(Twin _twin)
    {
        LogItem main = _twin.main;
        return main != null && !main.IsPooled && main.gameObject.activeInHierarchy && main.TopgradeTicket == _twin.ticket;
    }

    private void BeginVanish(Twin _twin)
    {
        _twin.diveFromOffset = _twin.body.transform.position - _twin.lastMainPos;
        _twin.diveFromOffset.z = 0f;
        _twin.diveFromScale = _twin.body.transform.localScale.x;
        _twin.diveFromAngle = _twin.bodyVisual.localEulerAngles.z;
        _twin.state = ETwinState.Vanish;
        _twin.time = 0f;
        _twin.duration = vanishDuration;
    }

    /// <summary>쌍둥이를 지금 상태대로 그린다. 연출이 끝났으면 true.</summary>
    private bool ApplyTwin(Twin _twin, float _dt)
    {
        Vector3 anchor = _twin.lastMainPos;
        Vector3 offset;
        float scaleX;
        float scaleY;
        float angle;
        bool bFinished = false;
        bool bLanded = false;
        bool bSparkle = false;

        switch (_twin.state)
        {
            case ETwinState.Pop:
            {
                float t = Mathf.Clamp01(_twin.time / Mathf.Max(0.0001f, _twin.duration));
                Vector3 ride = new Vector3(0f, rideHeight, 0f);

                // 수평은 빠르게 출발해 감속(EaseOutQuad), 수직은 포물선. 끝에서 정확히 등 위에 닿는다.
                float horizontal = t * (2f - t);
                offset = Vector3.LerpUnclamped(_twin.popFromOffset, ride, horizontal);
                offset.y += _twin.popApex * 4f * t * (1f - t);

                // 원목에서 쑥 빠져나오듯 작게 시작해 앞 40% 동안 살짝 넘치게 커진다(EaseOutBack).
                float grow = Mathf.Clamp01(t / 0.4f);
                float s = Mathf.LerpUnclamped(popStartScale, 1f, EaseOutBack(grow)) * twinScale;
                scaleX = s;
                scaleY = s;

                angle = -360f * popSpinTurns * (t * (2f - t));

                if (t >= 1f)
                {
                    _twin.state = ETwinState.Ride;
                    _twin.time = 0f;
                    _twin.landTime = 0f;
                    _twin.sparkleTimer = Random.Range(0.05f, 0.1f);
                    bLanded = true;
                    Sound.Play(landSound, anchor, landVolume, pitchOverride: landPitch * Random.Range(0.97f, 1.03f));
                }
                break;
            }

            case ETwinState.Ride:
            {
                _twin.landTime += _dt;
                _twin.sparkleTimer -= _dt;
                if (_twin.sparkleTimer <= 0f)
                {
                    _twin.sparkleTimer = Random.Range(rideSparkleIntervalMin, rideSparkleIntervalMax);
                    bSparkle = _twin.scaleIn >= 1f;
                }
                _twin.scaleIn = reattachDuration > 0f ? Mathf.Min(1f, _twin.scaleIn + _dt / reattachDuration) : 1f;

                // 착지 눌림: 가로로 퍼지고 세로로 눌렸다가 탄력 있게 돌아온다.
                float lt = Mathf.Clamp01(_twin.landTime / Mathf.Max(0.0001f, landSquashDuration));
                float squash = landSquash * (1f - lt) * Mathf.Cos(lt * Mathf.PI * 1.5f);

                float bob = Mathf.Sin(_twin.bobPhase + _twin.time * rideBobFrequency * Mathf.PI * 2f) * rideBobAmplitude * lt;

                float s = twinScale * EaseOutBack(_twin.scaleIn);
                scaleX = s * (1f + squash);
                scaleY = s * (1f - squash);

                // 눌린 만큼 바닥(원목 등)이 뜨지 않도록 내려 준다.
                offset = new Vector3(0f, rideHeight + bob - (1f - scaleY / Mathf.Max(0.0001f, s)) * twinHalfHeight * s, 0f);
                angle = 0f;
                break;
            }

            case ETwinState.Dive:
            {
                float t = Mathf.Clamp01(_twin.time / Mathf.Max(0.0001f, _twin.duration));

                // 원목 쪽으로 빨려 들어가며(EaseInQuad) 살짝 튀고, 크기는 원목 퇴출과 같은 InBack 느낌으로 줄어든다.
                float pull = t * t;
                offset = Vector3.LerpUnclamped(_twin.diveFromOffset, Vector3.zero, pull);
                offset.y += diveHop * 4f * t * (1f - t);

                float s = Mathf.LerpUnclamped(_twin.diveFromScale, 0f, EaseInBack(t));
                scaleX = s;
                scaleY = s;
                angle = Mathf.LerpAngle(_twin.diveFromAngle, 0f, t);

                bFinished = t >= 1f;
                break;
            }

            default: // Vanish
            {
                float t = Mathf.Clamp01(_twin.time / Mathf.Max(0.0001f, _twin.duration));
                offset = _twin.diveFromOffset;
                float s = Mathf.Lerp(_twin.diveFromScale, 0f, t * t);
                scaleX = s;
                scaleY = s;
                angle = _twin.diveFromAngle;
                bFinished = t >= 1f;
                break;
            }
        }

        Transform root = _twin.body.transform;
        root.position = anchor + offset;
        root.localScale = new Vector3(Mathf.Max(0f, scaleX), Mathf.Max(0f, scaleY), 1f);
        _twin.bodyVisual.localRotation = Quaternion.Euler(0f, 0f, angle);

        // 늘 원목 바로 앞에 그린다: 쌍둥이의 정렬 지면을 원목 지면보다 조금 낮게 맞춘다.
        float twinAnchorY = _twin.bodyVisual.position.y;
        _twin.body.SetHeight(twinAnchorY - (_twin.lastMainGroundY - SortAheadOfMain));
        _twin.body.UpdateSortingOrder();

        if (vfx != null)
        {
            if (bLanded) vfx.PlayLand(root.position);
            if (bSparkle) vfx.EmitRideSparkle(root.position, rideSparkleExtents.x, rideSparkleExtents.y);
        }

        return bFinished;
    }

    private void RemoveTwinAt(int _index, bool _bReturnBody)
    {
        Twin twin = twins[_index];
        twins.RemoveAt(_index);

        if (_bReturnBody && twin.body != null)
        {
            ReturnBody(twin);
        }

        twin.main = null;
        twin.mainSortable = null;
        twin.body = null;
        twin.bodyVisual = null;
        twinPool.Push(twin);
    }

    private void ReturnBody(Twin _twin)
    {
        // 회전은 ResetItem이 되돌리지만, 다음 사용 전까지 남겨 두지 않도록 여기서도 바로 풀어 둔다.
        if (_twin.bodyVisual != null) _twin.bodyVisual.localRotation = Quaternion.identity;
        _twin.body.transform.localScale = Vector3.one;

        if (pool != null) pool.ReturnLogItem(_twin.body);
    }

    private void ClearAllTwins()
    {
        for (int i = twins.Count - 1; i >= 0; i--)
        {
            Twin twin = twins[i];
            bool bAlive = twin.body != null && !twin.body.IsPooled;
            RemoveTwinAt(i, bAlive);
        }
    }

    private static float EaseOutBack(float _t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = _t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private static float EaseInBack(float _t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return c3 * _t * _t * _t - c1 * _t * _t;
    }
}
