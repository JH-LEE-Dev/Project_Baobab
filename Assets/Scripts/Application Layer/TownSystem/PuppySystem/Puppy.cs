using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 강아지 행동. 숫자는 기획 시트(★강쥐)의 FSM 번호와 같다.
/// </summary>
public enum PuppyBehavior
{
    None = 0,
    LookAtOwner = 1,        // 가만히 주인을 바라본다 (StandIdle, 가까울수록 꼬리를 빨리 흔듦)
    Daydream = 2,           // 주인 말고 다른 곳을 보며 멍때린다 (StandIdle, 꼬리는 적당히)
    CircleOwner = 3,        // 주인 주변을 빙빙 돈다 (Run)
    RunAroundIsland = 4,    // 섬을 시계/반시계 방향으로 한 바퀴 돈다 (Run)
    FetchAndReturn = 5,     // 잠깐 어디 찍고 주인에게 돌아온다 (Run)
    FollowOwner = 6,        // 무지성으로 주인을 따라간다 (Run)
    SitLookAtOwner = 7,     // 앉아서 주인을 바라본다 (SitStart → SitIdle → SitEnd)
    Sniff = 8,              // 땅 냄새를 맡는다 (BowStart → Bow → BowEnd)
    Eat = 9,                // 밥그릇으로 달려가 밥을 먹는다 (Run → BowStart → Eating → BowEnd)
    Sleep = 10,             // 잔다 (SleepStart → Sleep → SleepEnd)
}

/// <summary>
/// 마을 강아지. 기획 시트(★강쥐)의 FSM을 그대로 옮긴 것이다.
///
/// 상태 전이는 세 갈래다.
///   - 행동이 끝났을 때(시간 경과·도착 등)   : NextOnComplete 표에서 무작위
///   - 주인이 상호작용했을 때                 : NextOnInteract 표에서 무작위
///   - 주인이 밥그릇을 채웠을 때              : 밥 먹기(9). 밥 먹는 중과 자는 중은 제외
/// 밥 먹기(9)는 밥그릇이 비어 있지 않을 때만 후보가 된다.
///
/// 앉기·냄새 맡기·밥 먹기·자기처럼 시작/끝 애니메이션이 있는 행동에서 다른 행동으로 넘어갈 때는
/// 끝 애니메이션(SitEnd/BowEnd/SleepEnd)을 먼저 재생하고 넘어간다.
///
/// 길찾기는 운반 NPC와 같은 PathFindComponent(마을 타일맵 기준)를 쓰고, 플레이어와는 물리적으로
/// 충돌하지 않는다(콜라이더 없음). 상호작용 범위 판정은 PuppyManager가 거리로 한다.
/// </summary>
public class Puppy : MonoBehaviour
{
    private enum EAnim
    {
        StandIdle,
        Run,
        SitStart,
        SitEnd,
        SitIdle,
        BowStart,
        Bow,
        BowEnd,
        Eating,
        SleepStart,
        SleepEnd,
        Sleep,
    }

    // 화면 기준 8방향. 동쪽에서 반시계 방향으로 45도씩이라 인덱스 계산으로 방향을 돌릴 수 있다.
    private enum EFacing
    {
        E = 0,
        NE = 1,
        N = 2,
        NW = 3,
        W = 4,
        SW = 5,
        S = 6,
        SE = 7,
    }

    private const int FacingCount = 8;

    // 프레임 번호는 시트의 스프라이트 정보 그대로다. SleepStart 이후는 Puppy_Sleep 시트를 쓴다.
    private static readonly int[][] AnimFrames =
    {
        new[] { 0, 1 },             // StandIdle (Loop)
        new[] { 2, 3 },             // Run (Loop)
        new[] { 4, 5, 6, 7 },       // SitStart
        new[] { 7, 6, 5, 4 },       // SitEnd
        new[] { 7, 8 },             // SitIdle (Loop)
        new[] { 9, 10, 11, 12 },    // BowStart
        new[] { 12 },               // Bow
        new[] { 12, 11, 10, 9 },    // BowEnd
        new[] { 13, 14 },           // Eating (Loop)
        new[] { 0, 1, 2, 3 },       // SleepStart
        new[] { 3, 2, 1, 0 },       // SleepEnd
        new[] { 4, 5 },             // Sleep (Loop)
    };

    private static readonly bool[] AnimLoop =
    {
        true, true, false, false, true, false, true, false, true, false, false, true,
    };

    // 행동이 끝났을 때 넘어갈 후보. 인덱스 = (int)PuppyBehavior.
    private static readonly PuppyBehavior[][] NextOnComplete =
    {
        new PuppyBehavior[0],
        new[] { PuppyBehavior.RunAroundIsland, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner, PuppyBehavior.Sniff, PuppyBehavior.Eat },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.RunAroundIsland, PuppyBehavior.SitLookAtOwner, PuppyBehavior.Sniff, PuppyBehavior.Eat },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.RunAroundIsland, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner, PuppyBehavior.Eat },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.Daydream, PuppyBehavior.RunAroundIsland, PuppyBehavior.SitLookAtOwner, PuppyBehavior.Sniff, PuppyBehavior.Eat },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.Daydream, PuppyBehavior.RunAroundIsland, PuppyBehavior.SitLookAtOwner, PuppyBehavior.Sniff, PuppyBehavior.Eat },
        new[] { PuppyBehavior.RunAroundIsland, PuppyBehavior.FollowOwner, PuppyBehavior.Sniff, PuppyBehavior.Eat },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.Daydream, PuppyBehavior.RunAroundIsland, PuppyBehavior.SitLookAtOwner, PuppyBehavior.Eat },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.Daydream, PuppyBehavior.RunAroundIsland, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner, PuppyBehavior.Sniff, PuppyBehavior.Sleep },
        new PuppyBehavior[0],
    };

    // 주인이 상호작용했을 때 넘어갈 후보. 인덱스 = (int)PuppyBehavior.
    private static readonly PuppyBehavior[][] NextOnInteract =
    {
        new PuppyBehavior[0],
        new[] { PuppyBehavior.CircleOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.CircleOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.CircleOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.CircleOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.CircleOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.CircleOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner },
        new[] { PuppyBehavior.LookAtOwner, PuppyBehavior.CircleOwner, PuppyBehavior.FetchAndReturn, PuppyBehavior.FollowOwner, PuppyBehavior.SitLookAtOwner },
    };

    // 마을에 도착할 때 처음 고를 수 있는 행동(시트의 ◆).
    private static readonly PuppyBehavior[] InitialBehaviors =
    {
        PuppyBehavior.LookAtOwner, PuppyBehavior.Daydream, PuppyBehavior.RunAroundIsland, PuppyBehavior.SitLookAtOwner,
        PuppyBehavior.Sniff, PuppyBehavior.Eat, PuppyBehavior.Sleep,
    };

    //내부 의존성
    [Header("Visual")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private GameObject outlineObject;
    [SerializeField] private PuppyPopEffect popEffect;

    [Header("Sprites (Puppy 0~14 / Puppy_Sleep 0~5)")]
    [SerializeField] private Sprite[] southSprites = new Sprite[15];
    [SerializeField] private Sprite[] southEastSprites = new Sprite[15];
    [SerializeField] private Sprite[] eastSprites = new Sprite[15];
    [SerializeField] private Sprite[] northEastSprites = new Sprite[15];
    [SerializeField] private Sprite[] northSprites = new Sprite[15];
    [SerializeField, Tooltip("동쪽(오른쪽)을 보고 있는 그림. 서쪽은 좌우 반전한다.")]
    private Sprite[] sleepSprites = new Sprite[6];

    [Header("Animation FPS")]
    [SerializeField] private float runFps = 10f;
    [SerializeField, Tooltip("Start/End 애니메이션 재생 속도.")] private float transitionFps = 12f;
    [SerializeField] private float eatingFps = 6f;
    [SerializeField] private float sleepFps = 2f;
    [SerializeField, Tooltip("멍때릴 때(2)의 꼬리 흔드는 속도.")] private float daydreamWagFps = 5f;
    [SerializeField, Tooltip("주인이 멀리 있을 때 꼬리 흔드는 속도.")] private float wagFpsFar = 3f;
    [SerializeField, Tooltip("주인이 바로 옆에 있을 때 꼬리 흔드는 속도.")] private float wagFpsNear = 14f;
    [SerializeField] private float wagFarDistance = 6f;
    [SerializeField] private float wagNearDistance = 1f;

    [Header("Movement")]
    [SerializeField] private float runSpeed = 3.6f;
    [SerializeField, Tooltip("주인이 이 거리보다 멀면 runSpeed에 catchUpMultiplier를 곱해 따라잡는다.")]
    private float catchUpDistance = 3.5f;
    [SerializeField] private float catchUpMultiplier = 1.4f;
    [SerializeField] private float repathInterval = 0.3f;
    [SerializeField, Tooltip("주인을 따라가다 멈추는 거리.")] private float followStopDistance = 0.9f;
    [SerializeField, Tooltip("멈춰 있다가 다시 따라가기 시작하는 거리.")] private float followResumeDistance = 1.5f;
    [SerializeField, Tooltip("코어루프 기물 영역 밖으로 걸어 나가는 데 쓸 최대 시간. 못 나가면 그 자리에서 행동을 시작한다.")]
    private float escapeMaxDuration = 4f;

    [Header("Behavior Timing (초, 범위 안 랜덤)")]
    [SerializeField] private Vector2 lookAtOwnerDuration = new Vector2(6f, 10f);
    [SerializeField] private Vector2 daydreamDuration = new Vector2(3f, 4f);
    [SerializeField] private Vector2 circleOwnerDuration = new Vector2(5f, 7f);
    [SerializeField] private Vector2 followOwnerDuration = new Vector2(6f, 8f);
    [SerializeField] private Vector2 sitDuration = new Vector2(5f, 8f);
    [SerializeField] private Vector2 sniffDuration = new Vector2(1f, 2f);
    [SerializeField] private float eatDuration = 5f;

    [Header("Circle Owner (3)")]
    [SerializeField, Tooltip("주인 주변을 도는 반지름(가로 기준, 세로는 아이소메트릭 비율로 절반).")]
    private float circleRadius = 1.1f;
    [SerializeField, Tooltip("주인에게 다가가기만 하다 이 시간이 지나면(원에 붙지 못함) 그만둔다.")]
    private float circleApproachMaxDuration = 6f;

    [Header("Run Around Island (4)")]
    [SerializeField] private Vector2 islandLaps = new Vector2(0.7f, 1.3f);
    [SerializeField, Tooltip("길이 막혀 바퀴 수를 못 채우더라도 이 시간이 지나면 끝낸다.")]
    private float islandMaxDuration = 45f;

    [Header("Fetch And Return (5)")]
    [SerializeField] private Vector2 fetchDistance = new Vector2(3f, 6f);
    [SerializeField, Tooltip("찍은 곳에서 잠깐 멈춰 있는 시간.")] private float fetchPause = 0.4f;
    [SerializeField] private float fetchMaxDuration = 20f;

    [Header("Eat (9)")]
    [SerializeField, Tooltip("밥그릇 중심에서 강아지가 서는 자리까지의 거리(가로/세로).")]
    private Vector2 eatOffset = new Vector2(0.32f, 0.2f);
    [SerializeField] private float eatApproachMaxDuration = 15f;

    //외부 의존성
    private PuppyManager manager;

    private PathFindComponent pathFind;
    private readonly List<Vector3> path = new List<Vector3>(128);
    private int pathIndex = 0;
    private float repathTimer = 0f;
    private float chaseBlockedTimer = 0f;

    private SpriteRenderer[] outlineRenderers;

    // FSM
    private PuppyBehavior behavior = PuppyBehavior.None;
    private PuppyBehavior pendingBehavior = PuppyBehavior.None;
    private bool bExiting = false;
    private int phase = 0;
    private float behaviorTimer = 0f;
    private float phaseTimer = 0f;
    private readonly List<PuppyBehavior> candidates = new List<PuppyBehavior>(8);

    // 3: 원 위의 현재 각도/방향
    private bool bCircling = false;
    private float circleAngle = 0f;
    private float circleDirection = 1f;
    private float circleBlockedTimer = 0f;

    // 4: 섬 일주
    private int islandWaypointIndex = 0;
    private float islandDirection = 1f;
    private float islandTargetAngle = 0f;
    private float islandAccumAngle = 0f;
    private float islandLastAngle = 0f;

    // 6: 따라가는 중인지(멈춰 있는지)
    private bool bFollowMoving = true;
    private Vector2 followStopOwnerPosition;

    // 멈춰 있는 행동을 시작하기 전에 코어루프 기물 영역 밖으로 걸어 나가는 중인지
    private bool bEscapingZone = false;
    private Vector2 escapePoint;
    private float escapeTimer = 0f;

    // 9: 밥그릇 앞 자리
    private Vector2 eatPoint;
    private float eatStuckTimer = 0f;

    // 애니메이션
    private EAnim currentAnim = EAnim.StandIdle;
    private int animFrame = 0;
    private float animTimer = 0f;
    private float animFps = 1f;
    private bool bAnimFinished = false;
    private EFacing facing = EFacing.S;

    public PuppyBehavior CurrentBehavior => behavior;

    /// <summary>밥그릇 앞에서 고개를 숙이고 먹는 중인지. 이 동안에는 밥그릇 상호작용이 막힌다.</summary>
    public bool IsEating => PuppyBehavior.Eat == behavior && 1 <= phase;

    public void Initialize(PuppyManager _manager)
    {
        manager = _manager;
        pathFind = GetComponent<PathFindComponent>();
        outlineRenderers = outlineObject.GetComponentsInChildren<SpriteRenderer>(true);
        popEffect.Initialize(new[] { bodyRenderer });
        SetOutline(false);
    }

    /// <summary>마을에 도착할 때마다 호출된다. 자리를 잡고 처음 행동(◆)을 무작위로 고른다.</summary>
    public void EnterTown(Vector2 _spawnPosition, ITilemapDataProvider _tilemapDataProvider)
    {
        // 마을 Grid는 매번 새로 만들어지므로 길찾기 크기도 다시 맞춘다.
        if (null != _tilemapDataProvider)
            pathFind.Initialize(_tilemapDataProvider, null);

        MoveTo(_spawnPosition);
        facing = (EFacing)Random.Range(0, FacingCount);

        behavior = PuppyBehavior.None;
        EnterBehavior(PickRandom(InitialBehaviors), true);
    }

    public void ExitTown()
    {
        behavior = PuppyBehavior.None;
        pendingBehavior = PuppyBehavior.None;
        bExiting = false;
        bEscapingZone = false;
        path.Clear();
        pathIndex = 0;
        popEffect.Stop();
        SetOutline(false);
    }

    /// <summary>주인이 상호작용했다. 흰색 + 뽀잉 연출 후 상황에 맞는 행동으로 바꾼다.</summary>
    public void Interact()
    {
        popEffect.Play();

        if (PuppyBehavior.None == behavior) return;

        RequestBehavior(PickRandom(NextOnInteract[(int)behavior]));
    }

    /// <summary>주인이 밥그릇을 채웠다. 밥 먹는 중이나 자는 중이 아니면 밥을 먹으러 간다.</summary>
    public void NotifyBowlFilled()
    {
        if (PuppyBehavior.None == behavior || PuppyBehavior.Eat == behavior || PuppyBehavior.Sleep == behavior) return;

        RequestBehavior(PuppyBehavior.Eat);
    }

    public void SetOutline(bool _bActive)
    {
        if (outlineObject.activeSelf != _bActive)
            outlineObject.SetActive(_bActive);
    }

    // ───────────────────────────── FSM ─────────────────────────────

    private void RequestBehavior(PuppyBehavior _next)
    {
        // 끝 애니메이션이 도는 중이면 다음 행동만 바꿔둔다.
        if (true == bExiting)
        {
            pendingBehavior = _next;
            return;
        }

        // 앉아 있다가 다시 앉기가 뽑히면 일어났다 앉지 않고 앉은 채로 시간만 새로 잡는다.
        if (PuppyBehavior.SitLookAtOwner == _next && PuppyBehavior.SitLookAtOwner == behavior && 1 == phase)
        {
            behaviorTimer = RandomRange(sitDuration);
            return;
        }

        if (true == TryGetEndAnim(out EAnim _endAnim))
        {
            bExiting = true;
            pendingBehavior = _next;
            PlayAnim(_endAnim, transitionFps, true);
            return;
        }

        EnterBehavior(_next, false);
    }

    // 지금 행동을 끝낼 때 재생해야 할 끝 애니메이션.
    private bool TryGetEndAnim(out EAnim _endAnim)
    {
        // 영역 밖으로 걸어 나가는 중이면 아직 앉거나 엎드리지 않았으므로 끝 애니메이션이 없다.
        if (true == bEscapingZone)
        {
            _endAnim = EAnim.StandIdle;
            return false;
        }

        switch (behavior)
        {
            case PuppyBehavior.SitLookAtOwner:
                _endAnim = EAnim.SitEnd;
                return true;
            case PuppyBehavior.Sniff:
                _endAnim = EAnim.BowEnd;
                return true;
            case PuppyBehavior.Eat:
                _endAnim = EAnim.BowEnd;
                return 1 <= phase;
            case PuppyBehavior.Sleep:
                _endAnim = EAnim.SleepEnd;
                return true;
        }

        _endAnim = EAnim.StandIdle;
        return false;
    }

    private void CompleteBehavior()
    {
        RequestBehavior(PickRandom(NextOnComplete[(int)behavior]));
    }

    // _bSkipIntro: 마을에 도착했을 때 이미 앉아/엎드려/자고 있던 것처럼 시작 애니메이션을 건너뛴다.
    // _bSkipZoneCheck: 영역 밖으로 걸어 나간 뒤 다시 들어올 때(또는 끝내 못 나갔을 때) 같은 검사를 반복하지 않는다.
    private void EnterBehavior(PuppyBehavior _behavior, bool _bSkipIntro, bool _bSkipZoneCheck = false)
    {
        behavior = _behavior;
        pendingBehavior = PuppyBehavior.None;
        bExiting = false;
        bEscapingZone = false;
        phase = 0;
        phaseTimer = 0f;
        path.Clear();
        pathIndex = 0;
        repathTimer = 0f;
        chaseBlockedTimer = 0f;

        // 기획 규칙 4: 코어루프 기물 상호작용 영역 안에서는 웬만하면 머물지 않는다. 멈춰 있는 행동(바라보기·멍때리기·
        // 앉기·냄새 맡기·자기)을 영역 안에서 시작하게 됐다면(주인 주변을 돌다 끝난 자리 등) 먼저 밖으로 걸어 나간다.
        if (false == _bSkipZoneCheck && true == IsStationary(_behavior) && true == manager.TryGetAvoidZoneExit(Position, out Vector2 _exit))
        {
            bEscapingZone = true;
            escapePoint = _exit;
            escapeTimer = escapeMaxDuration;
            BuildPath(_exit, true);
            PlayAnim(EAnim.Run, runFps, true);
            return;
        }

        switch (_behavior)
        {
            case PuppyBehavior.LookAtOwner:
                behaviorTimer = RandomRange(lookAtOwnerDuration);
                PlayAnim(EAnim.StandIdle, GetWagFps(), true);
                Sound.Play(SoundID.PuppyWhining, transform.position);
                break;

            case PuppyBehavior.Daydream:
                behaviorTimer = RandomRange(daydreamDuration);
                PlayAnim(EAnim.StandIdle, daydreamWagFps, true);
                FaceAwayFromOwner();
                break;

            case PuppyBehavior.CircleOwner:
                behaviorTimer = RandomRange(circleOwnerDuration);
                bCircling = false;
                circleBlockedTimer = 0f;
                circleDirection = 0 == Random.Range(0, 2) ? 1f : -1f;
                PlayAnim(EAnim.Run, runFps, true);
                Sound.Play(SoundID.PuppyBark, transform.position);
                break;

            case PuppyBehavior.RunAroundIsland:
                EnterRunAroundIsland();
                PlayAnim(EAnim.Run, runFps, true);
                Sound.Play(SoundID.PuppyBark, transform.position);
                break;

            case PuppyBehavior.FetchAndReturn:
                EnterFetchAndReturn();
                PlayAnim(EAnim.Run, runFps, true);
                break;

            case PuppyBehavior.FollowOwner:
                behaviorTimer = RandomRange(followOwnerDuration);
                bFollowMoving = true;
                PlayAnim(EAnim.Run, runFps, true);
                break;

            case PuppyBehavior.SitLookAtOwner:
                Sound.Play(SoundID.PuppyWhining, transform.position);
                if (true == _bSkipIntro) EnterSitIdle();
                else PlayAnim(EAnim.SitStart, transitionFps, true);
                break;

            case PuppyBehavior.Sniff:
                if (true == _bSkipIntro) EnterSniffHold();
                else PlayAnim(EAnim.BowStart, transitionFps, true);
                break;

            case PuppyBehavior.Eat:
                EnterEat();
                break;

            case PuppyBehavior.Sleep:
                if (true == _bSkipIntro) EnterSleepLoop();
                else PlayAnim(EAnim.SleepStart, transitionFps, true);
                break;
        }
    }

    private void UpdateBehavior(float _dt)
    {
        if (true == bExiting)
        {
            if (true == bAnimFinished)
                EnterBehavior(pendingBehavior, false);
            return;
        }

        if (true == bEscapingZone)
        {
            UpdateEscapeZone(_dt);
            return;
        }

        switch (behavior)
        {
            case PuppyBehavior.LookAtOwner: UpdateLookAtOwner(_dt); break;
            case PuppyBehavior.Daydream: UpdateDaydream(_dt); break;
            case PuppyBehavior.CircleOwner: UpdateCircleOwner(_dt); break;
            case PuppyBehavior.RunAroundIsland: UpdateRunAroundIsland(_dt); break;
            case PuppyBehavior.FetchAndReturn: UpdateFetchAndReturn(_dt); break;
            case PuppyBehavior.FollowOwner: UpdateFollowOwner(_dt); break;
            case PuppyBehavior.SitLookAtOwner: UpdateSitLookAtOwner(_dt); break;
            case PuppyBehavior.Sniff: UpdateSniff(_dt); break;
            case PuppyBehavior.Eat: UpdateEat(_dt); break;
            case PuppyBehavior.Sleep: UpdateSleep(); break;
        }
    }

    // 영역 밖 자리까지 달려간 뒤 원래 하려던 행동을 시작한다. 길이 막혀 시간 안에 못 나가면 그 자리에서 시작한다.
    private void UpdateEscapeZone(float _dt)
    {
        PlayAnim(EAnim.Run, runFps);

        bool _bArrived = pathIndex < path.Count
            ? false
            : SteerTowards(escapePoint, runSpeed, _dt);

        if (pathIndex < path.Count)
            StepPath(runSpeed, _dt);

        escapeTimer -= _dt;
        if (true == _bArrived || escapeTimer <= 0f)
            EnterBehavior(behavior, false, true);
    }

    private static bool IsStationary(PuppyBehavior _behavior)
    {
        switch (_behavior)
        {
            case PuppyBehavior.LookAtOwner:
            case PuppyBehavior.Daydream:
            case PuppyBehavior.SitLookAtOwner:
            case PuppyBehavior.Sniff:
            case PuppyBehavior.Sleep:
                return true;
        }

        return false;
    }

    // 1. 가만히 주인을 바라본다
    private void UpdateLookAtOwner(float _dt)
    {
        PlayAnim(EAnim.StandIdle, GetWagFps());
        FaceOwner();

        behaviorTimer -= _dt;
        if (behaviorTimer <= 0f) CompleteBehavior();
    }

    // 2. 멍때린다
    private void UpdateDaydream(float _dt)
    {
        behaviorTimer -= _dt;
        if (behaviorTimer <= 0f) CompleteBehavior();
    }

    // 3. 주인 주변을 빙빙 돈다. 주인이 움직여도 계속 따라붙어 돈다.
    private void UpdateCircleOwner(float _dt)
    {
        if (false == manager.HasOwner)
        {
            CompleteBehavior();
            return;
        }

        // 기획: "주인 주변을 5~7초간 계속 돌고 난 뒤" - 도는 시간(behaviorTimer)은 원을 그리는 동안만 흐른다.
        // 다가가는 시간은 따로 재서, 주인이 상호작용 영역 안이나 갈 수 없는 곳에 있어 끝내 원에 붙지 못하면
        // 그만두게 한다(행동이 영영 끝나지 않는 것 방지).
        if (false == bCircling)
        {
            phaseTimer += _dt;
            if (phaseTimer >= circleApproachMaxDuration)
            {
                CompleteBehavior();
                return;
            }
        }

        PlayAnim(EAnim.Run, runFps);

        // 주인이 코어루프 기물 상호작용 영역 안에 있으면 주인 대신 영역 밖 기다리는 자리를 중심으로 돈다(기획 규칙 4).
        // 주인을 중심으로 그대로 돌면 원 대부분이 영역 안에 걸려 강아지가 영역을 드나들게 된다.
        Vector2 _owner = manager.GetOwnerGoal(Position);
        Vector2 _rel = Position - _owner;

        // 아이소메트릭 바닥의 원은 화면에서 세로가 절반인 타원이라, 세로를 두 배로 펴서 거리를 잰다.
        float _groundDistance = new Vector2(_rel.x, _rel.y * 2f).magnitude;

        // 원에서 많이 벗어나면(주인이 달려가 버림) 다시 쫓아간다.
        if (true == bCircling && _groundDistance > circleRadius + 3f)
            bCircling = false;

        if (false == bCircling)
        {
            // 너무 멀면 원을 그리기 전에 먼저 주인에게 달려간다. 주인이 영역 안이라 영역 밖 자리에서 멈췄을 때도
            // (ChaseOwner가 도착을 알림) 그 자리에서 원을 그리기 시작한다 - 원 위의 자리도 영역 밖으로 밀려난다.
            if (_groundDistance > circleRadius + 1.5f && false == ChaseOwner(_dt, circleRadius))
                return;

            bCircling = true;
            phaseTimer = 0f;
            circleAngle = Mathf.Atan2(_rel.y * 2f, _rel.x);

            // 다가가며 쓰던 길이 남아 있으면 원 대신 그 길을 따라가 버리므로 비운다.
            path.Clear();
            pathIndex = 0;
        }

        circleAngle += circleDirection * (runSpeed / circleRadius) * _dt;
        Vector2 _target = _owner + new Vector2(Mathf.Cos(circleAngle), Mathf.Sin(circleAngle) * 0.5f) * circleRadius;
        _target = manager.PushOutOfAvoidZones(_target);

        // 도는 길이 나무·물에 막혀 한 걸음도 못 나가면 제자리에서 달리기만 하게 되므로, 잠깐 막혔다 싶으면
        // 원 위의 자리까지 길찾기로 돌아간다. 그 자리까지도 길이 없으면 반대 방향으로 돌아선다.
        Vector2 _before = Position;
        if (pathIndex < path.Count)
            StepPath(runSpeed * 1.25f, _dt);
        else
            SteerTowards(_target, runSpeed * 1.25f, _dt);

        circleBlockedTimer = (Position - _before).sqrMagnitude < 0.000001f ? circleBlockedTimer + _dt : 0f;
        if (circleBlockedTimer >= 0.3f)
        {
            circleBlockedTimer = 0f;
            if (false == BuildPath(_target, true) || 0 == path.Count)
                circleDirection = -circleDirection;
        }

        behaviorTimer -= _dt;
        if (behaviorTimer <= 0f) CompleteBehavior();
    }

    // 4. 섬 한 바퀴
    private void EnterRunAroundIsland()
    {
        IReadOnlyList<Vector2> _waypoints = manager.IslandWaypoints;

        behaviorTimer = islandMaxDuration;
        islandDirection = 0 == Random.Range(0, 2) ? 1f : -1f;
        islandTargetAngle = RandomRange(islandLaps) * Mathf.PI * 2f;
        islandAccumAngle = 0f;
        islandLastAngle = GetIslandAngle(Position);

        // 지금 자리에서 가장 가까운 웨이포인트부터 출발한다.
        float _bestSqr = float.MaxValue;
        islandWaypointIndex = 0;
        for (int i = 0; i < _waypoints.Count; i++)
        {
            float _sqr = (_waypoints[i] - Position).sqrMagnitude;
            if (_sqr < _bestSqr)
            {
                _bestSqr = _sqr;
                islandWaypointIndex = i;
            }
        }

        if (false == BuildPathToIslandWaypoint())
            behaviorTimer = 0f;
    }

    private void UpdateRunAroundIsland(float _dt)
    {
        PlayAnim(EAnim.Run, runFps);

        if (true == StepPath(runSpeed, _dt))
        {
            AdvanceIslandWaypoint();
            if (false == BuildPathToIslandWaypoint())
            {
                CompleteBehavior();
                return;
            }
        }

        // 섬 중심을 기준으로 돈 각도를 누적한다. 길이 돌아가느라 잠깐 반대로 가면 그만큼 깎인다.
        float _angle = GetIslandAngle(Position);
        islandAccumAngle += Mathf.DeltaAngle(islandLastAngle * Mathf.Rad2Deg, _angle * Mathf.Rad2Deg) * Mathf.Deg2Rad * islandDirection;
        islandLastAngle = _angle;

        behaviorTimer -= _dt;
        if (islandAccumAngle >= islandTargetAngle || behaviorTimer <= 0f)
            CompleteBehavior();
    }

    private void AdvanceIslandWaypoint()
    {
        int _count = manager.IslandWaypoints.Count;
        islandWaypointIndex = (islandWaypointIndex + (islandDirection > 0f ? 1 : -1) + _count) % _count;
    }

    // 다음 웨이포인트까지 길을 찾는다. 막혀 있으면 그다음 웨이포인트로 넘어가며 한 바퀴까지 시도한다.
    private bool BuildPathToIslandWaypoint()
    {
        IReadOnlyList<Vector2> _waypoints = manager.IslandWaypoints;
        for (int i = 0; i < _waypoints.Count; i++)
        {
            if (true == BuildPath(_waypoints[islandWaypointIndex], false) && 0 < path.Count)
                return true;

            AdvanceIslandWaypoint();
        }

        return false;
    }

    private float GetIslandAngle(Vector2 _position)
    {
        Vector2 _rel = _position - manager.IslandCenter;
        return Mathf.Atan2(_rel.y * 2f, _rel.x);
    }

    // 5. 잠깐 어디 찍고 주인에게 돌아온다
    private void EnterFetchAndReturn()
    {
        behaviorTimer = fetchMaxDuration;

        Vector2 _origin = manager.HasOwner ? manager.OwnerPosition : Position;
        if (true == manager.TryGetRandomPoint(_origin, fetchDistance.x, fetchDistance.y, out Vector2 _point) && true == BuildPath(_point, false))
            phase = 0;
        else
            phase = 2;
    }

    private void UpdateFetchAndReturn(float _dt)
    {
        behaviorTimer -= _dt;
        if (behaviorTimer <= 0f)
        {
            CompleteBehavior();
            return;
        }

        switch (phase)
        {
            case 0:
                PlayAnim(EAnim.Run, runFps);
                if (true == StepPath(runSpeed, _dt))
                {
                    phase = 1;
                    phaseTimer = fetchPause;
                }
                break;

            case 1:
                // 기획: StandIdle은 주인과 가까울수록 꼬리를 더 빠르게 흔든다(멍때리기(2)만 "적당히").
                PlayAnim(EAnim.StandIdle, GetWagFps());
                phaseTimer -= _dt;
                if (phaseTimer <= 0f)
                {
                    phase = 2;
                    repathTimer = 0f;
                    path.Clear();
                    pathIndex = 0;
                }
                break;

            case 2:
                if (false == manager.HasOwner)
                {
                    CompleteBehavior();
                    return;
                }

                PlayAnim(EAnim.Run, runFps);
                if (true == ChaseOwner(_dt, followStopDistance))
                {
                    FaceOwner();
                    CompleteBehavior();
                }
                break;
        }
    }

    // 6. 주인을 따라간다
    private void UpdateFollowOwner(float _dt)
    {
        if (false == manager.HasOwner)
        {
            CompleteBehavior();
            return;
        }

        float _distance = (manager.GetOwnerGoal(Position) - Position).magnitude;

        if (true == bFollowMoving)
        {
            PlayAnim(EAnim.Run, runFps);
            if (true == ChaseOwner(_dt, followStopDistance))
            {
                bFollowMoving = false;
                followStopOwnerPosition = manager.OwnerPosition;
            }
        }
        else
        {
            PlayAnim(EAnim.StandIdle, GetWagFps());
            FaceOwner();

            // 길이 막혀 멀리서 멈춘 경우엔 주인이 움직이기 전까지 다시 뛰지 않는다(뛰다 서다를 반복하지 않게).
            bool _bOwnerMoved = (manager.OwnerPosition - followStopOwnerPosition).sqrMagnitude > 0.25f;
            if (_distance > followResumeDistance && true == _bOwnerMoved)
            {
                bFollowMoving = true;
                repathTimer = 0f;
            }
        }

        behaviorTimer -= _dt;
        if (behaviorTimer <= 0f) CompleteBehavior();
    }

    // 7. 앉아서 주인을 바라본다
    private void UpdateSitLookAtOwner(float _dt)
    {
        if (0 == phase)
        {
            if (true == bAnimFinished) EnterSitIdle();
            return;
        }

        PlayAnim(EAnim.SitIdle, GetWagFps());
        FaceOwner();

        behaviorTimer -= _dt;
        if (behaviorTimer <= 0f) CompleteBehavior();
    }

    private void EnterSitIdle()
    {
        phase = 1;
        behaviorTimer = RandomRange(sitDuration);
        PlayAnim(EAnim.SitIdle, GetWagFps(), true);
    }

    // 8. 땅 냄새 맡기
    private void UpdateSniff(float _dt)
    {
        if (0 == phase)
        {
            if (true == bAnimFinished) EnterSniffHold();
            return;
        }

        behaviorTimer -= _dt;
        if (behaviorTimer <= 0f) CompleteBehavior();
    }

    private void EnterSniffHold()
    {
        phase = 1;
        behaviorTimer = RandomRange(sniffDuration);
        PlayAnim(EAnim.Bow, transitionFps, true);
    }

    // 9. 밥 먹기
    private void EnterEat()
    {
        phase = 0;
        behaviorTimer = eatApproachMaxDuration;
        eatStuckTimer = 0f;
        PlayAnim(EAnim.Run, runFps, true);

        PuppyBowl _bowl = manager.Bowl;
        if (null == _bowl)
        {
            behaviorTimer = 0f;
            return;
        }

        eatPoint = manager.GetEatPoint(Position, eatOffset);
        BuildPath(eatPoint, true);
    }

    private void UpdateEat(float _dt)
    {
        PuppyBowl _bowl = manager.Bowl;

        switch (phase)
        {
            case 0:
                behaviorTimer -= _dt;
                if (null == _bowl || behaviorTimer <= 0f)
                {
                    CompleteBehavior();
                    return;
                }

                PlayAnim(EAnim.Run, runFps);

                if (pathIndex < path.Count)
                {
                    StepPath(runSpeed, _dt);
                    return;
                }

                // 길 끝(칸 중심)에서 밥그릇 앞 자리까지는 직접 다가간다. 바로 앞에서 막히면 그 자리에서 먹고,
                // 멀리서 막혔다면(길을 못 찾은 경우) 포기하고 다른 행동으로 넘어간다.
                Vector2 _before = Position;
                bool _bArrived = SteerTowards(eatPoint, runSpeed, _dt);
                eatStuckTimer = (Position - _before).sqrMagnitude < 0.000001f ? eatStuckTimer + _dt : 0f;

                if (false == _bArrived && 0.3f <= eatStuckTimer && (eatPoint - Position).sqrMagnitude > 0.36f)
                {
                    CompleteBehavior();
                    return;
                }

                if (true == _bArrived || 0.3f <= eatStuckTimer)
                {
                    phase = 1;
                    FaceTowards((Vector2)_bowl.transform.position - Position);
                    PlayAnim(EAnim.BowStart, transitionFps, true);
                }
                break;

            case 1:
                if (true == bAnimFinished)
                {
                    phase = 2;
                    behaviorTimer = eatDuration;
                    PlayAnim(EAnim.Eating, eatingFps, true);
                }
                break;

            case 2:
                behaviorTimer -= _dt;
                if (behaviorTimer <= 0f)
                {
                    // 5초간 먹고 밥그릇 단계를 하나 낮춘 직후 다음 행동으로 넘어간다.
                    _bowl?.Consume();
                    CompleteBehavior();
                }
                break;
        }
    }

    // 10. 잔다. 주인이 상호작용해줄 때까지 계속 잔다.
    private void UpdateSleep()
    {
        if (0 == phase && true == bAnimFinished)
            EnterSleepLoop();
    }

    private void EnterSleepLoop()
    {
        phase = 1;
        PlayAnim(EAnim.Sleep, sleepFps, true);
    }

    private PuppyBehavior PickRandom(PuppyBehavior[] _from)
    {
        candidates.Clear();
        for (int i = 0; i < _from.Length; i++)
        {
            if (true == IsAvailable(_from[i]))
                candidates.Add(_from[i]);
        }

        if (0 == candidates.Count)
            return PuppyBehavior.LookAtOwner;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private bool IsAvailable(PuppyBehavior _behavior)
    {
        switch (_behavior)
        {
            case PuppyBehavior.Eat:
                return null != manager.Bowl && true == manager.Bowl.HasFood;
            case PuppyBehavior.RunAroundIsland:
                return 4 <= manager.IslandWaypoints.Count;
            case PuppyBehavior.CircleOwner:
            case PuppyBehavior.FetchAndReturn:
            case PuppyBehavior.FollowOwner:
                return manager.HasOwner;
        }

        return true;
    }

    // ───────────────────────────── 이동 ─────────────────────────────

    private Vector2 Position => transform.position;

    private void MoveTo(Vector2 _position)
    {
        transform.position = new Vector3(_position.x, _position.y, transform.position.z);
    }

    // _bNear: 목표 칸이 막혀 있으면 그 주변의 걸을 수 있는 칸까지 찾는다.
    // PathFindComponent.FindPathNear는 안쪽 탐색 횟수가 500으로 고정돼 있어 조금만 멀어도 실패하므로(운반 NPC와
    // 공유하는 코드라 손대지 않는다), 가까운 걸을 수 있는 칸은 여기서 직접 찾고 넉넉한 횟수로 길을 찾는다.
    private bool BuildPath(Vector2 _goal, bool _bNear)
    {
        const int PathIterations = 3000;
        const int NearSearchRing = 3;

        pathIndex = 0;

        bool _bFound = pathFind.FindPath(Position, _goal, path, PathIterations);
        if (false == _bFound && true == _bNear && true == manager.TryGetNearestWalkablePoint(_goal, NearSearchRing, out Vector2 _nearGoal))
            _bFound = pathFind.FindPath(Position, _nearGoal, path, PathIterations);

        if (false == _bFound)
            path.Clear();

        return _bFound;
    }

    // 길을 따라 한 걸음. 길 끝에 도착했으면 true.
    private bool StepPath(float _speed, float _dt)
    {
        if (pathIndex >= path.Count) return true;

        Vector2 _position = Position;
        Vector2 _delta = (Vector2)path[pathIndex] - _position;
        float _step = _speed * _dt;

        if (_delta.magnitude <= _step)
        {
            MoveTo(path[pathIndex]);
            pathIndex++;
            return pathIndex >= path.Count;
        }

        MoveTo(_position + _delta.normalized * _step);
        FaceTowards(_delta);
        return false;
    }

    // 목표를 향해 곧장 한 걸음. 다음 자리가 걸을 수 없는 칸이면 가로/세로로 미끄러지고, 그래도 막히면 멈춘다.
    // 도착했으면 true.
    private bool SteerTowards(Vector2 _target, float _speed, float _dt)
    {
        Vector2 _position = Position;
        Vector2 _delta = _target - _position;
        float _distance = _delta.magnitude;
        if (_distance <= 0.02f) return true;

        float _step = Mathf.Min(_distance, _speed * _dt);
        Vector2 _next = _position + _delta / _distance * _step;

        // 이미 걸을 수 없는 칸에 서 있다면(스폰 직후 등) 빠져나올 수 있도록 막지 않는다.
        if (false == manager.IsWalkablePoint(_next) && true == manager.IsWalkablePoint(_position))
        {
            Vector2 _slideX = new Vector2(_next.x, _position.y);
            Vector2 _slideY = new Vector2(_position.x, _next.y);

            if (true == manager.IsWalkablePoint(_slideX)) _next = _slideX;
            else if (true == manager.IsWalkablePoint(_slideY)) _next = _slideY;
            else return false;
        }

        FaceTowards(_next - _position);
        MoveTo(_next);
        return _distance <= _step + 0.02f;
    }

    // 주인(상호작용 영역 안이면 그 바깥)에게 달려간다. _stopDistance 안에 들어오면 true.
    private bool ChaseOwner(float _dt, float _stopDistance)
    {
        Vector2 _goal = manager.GetOwnerGoal(Position);
        float _distance = (_goal - Position).magnitude;
        if (_distance <= _stopDistance) return true;

        float _speed = _distance > catchUpDistance ? runSpeed * catchUpMultiplier : runSpeed;

        // 길이 끝났더라도 간격을 지켜서만 다시 찾는다. 주인에게 닿는 길이 없을 때(물 건너편 등) 매 프레임
        // 넉넉한 횟수의 A*를 돌지 않도록, 그 사이에는 곧장 다가가기만 한다.
        repathTimer -= _dt;
        if (repathTimer <= 0f)
        {
            repathTimer = repathInterval;
            BuildPath(_goal, true);
        }

        Vector2 _before = Position;
        if (pathIndex < path.Count)
            StepPath(_speed, _dt);
        else
            SteerTowards(_goal, _speed, _dt);

        // 주인에게 닿는 길이 없어(물 건너편 등) 한 걸음도 못 나가는 채로 잠깐 지나면, 제자리에서 달리기만 하지 않도록
        // 갈 수 있는 데까지 온 것으로 보고 그 자리에서 기다린다.
        chaseBlockedTimer = (Position - _before).sqrMagnitude < 0.000001f ? chaseBlockedTimer + _dt : 0f;
        if (chaseBlockedTimer >= 0.3f)
        {
            chaseBlockedTimer = 0f;
            FaceOwner();
            return true;
        }

        return false;
    }

    // ───────────────────────────── 방향 ─────────────────────────────

    private void FaceTowards(Vector2 _direction)
    {
        if (_direction.sqrMagnitude < 0.000001f) return;

        float _angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        int _index = Mathf.RoundToInt(_angle / 45f);
        facing = (EFacing)(((_index % FacingCount) + FacingCount) % FacingCount);
    }

    private void FaceOwner()
    {
        if (true == manager.HasOwner)
            FaceTowards(manager.OwnerPosition - Position);
    }

    // 주인 쪽(과 그 양옆 45도)을 뺀 나머지 방향 중 하나를 본다.
    private void FaceAwayFromOwner()
    {
        if (false == manager.HasOwner)
        {
            facing = (EFacing)Random.Range(0, FacingCount);
            return;
        }

        FaceOwner();
        int _offset = Random.Range(2, FacingCount - 1);
        facing = (EFacing)(((int)facing + _offset) % FacingCount);
    }

    // ───────────────────────────── 애니메이션 ─────────────────────────────

    private float GetWagFps()
    {
        if (false == manager.HasOwner) return wagFpsFar;

        float _distance = (manager.OwnerPosition - Position).magnitude;
        return Mathf.Lerp(wagFpsFar, wagFpsNear, Mathf.InverseLerp(wagFarDistance, wagNearDistance, _distance));
    }

    private void PlayAnim(EAnim _anim, float _fps, bool _bRestart = false)
    {
        animFps = _fps;

        if (false == _bRestart && currentAnim == _anim) return;

        currentAnim = _anim;
        animFrame = 0;
        animTimer = 0f;
        bAnimFinished = false;
    }

    private void UpdateAnim(float _dt)
    {
        int[] _frames = AnimFrames[(int)currentAnim];
        bool _bLoop = AnimLoop[(int)currentAnim];

        animTimer += _dt;
        float _frameDuration = 1f / Mathf.Max(0.01f, animFps);

        while (animTimer >= _frameDuration)
        {
            animTimer -= _frameDuration;

            if (animFrame < _frames.Length - 1)
            {
                animFrame++;
            }
            else if (true == _bLoop)
            {
                animFrame = 0;
            }
            else
            {
                bAnimFinished = true;
                animTimer = 0f;
                break;
            }
        }
    }

    private void ApplySprite()
    {
        int _frame = AnimFrames[(int)currentAnim][animFrame];
        bool _bFlip;
        Sprite _sprite;

        if (currentAnim >= EAnim.SleepStart)
        {
            _bFlip = facing == EFacing.W || facing == EFacing.NW || facing == EFacing.SW;
            _sprite = sleepSprites[_frame];
        }
        else
        {
            _sprite = GetDirectionSheet(out _bFlip)[_frame];
        }

        bodyRenderer.sprite = _sprite;

        // 좌우 반전은 SpriteRenderer.flipX가 아니라 몸통 Transform의 가로 스케일로 한다. flipX는 셰이더가
        // 정점에서 직접 뒤집어 줘야 하는데(UnityFlipSprite), 아웃라인 셰이더(OutlineWriter/OutlineShader)는
        // 그 처리가 없어 테두리만 반전되지 않은 모양으로 그려졌다. 스케일로 뒤집으면 자식인 아웃라인도 함께 뒤집힌다.
        Transform _bodyTransform = bodyRenderer.transform;
        float _scaleX = true == _bFlip ? -1f : 1f;
        if (_bodyTransform.localScale.x != _scaleX)
            _bodyTransform.localScale = new Vector3(_scaleX, 1f, 1f);

        if (true == outlineObject.activeSelf)
        {
            for (int i = 0; i < outlineRenderers.Length; i++)
            {
                outlineRenderers[i].sprite = _sprite;
            }
        }
    }

    // 5방향 그림(N/NE/E/SE/S)에서 서쪽 3방향은 좌우 반전해 8방향을 만든다.
    private Sprite[] GetDirectionSheet(out bool _bFlip)
    {
        _bFlip = false;
        switch (facing)
        {
            case EFacing.E: return eastSprites;
            case EFacing.NE: return northEastSprites;
            case EFacing.N: return northSprites;
            case EFacing.NW: _bFlip = true; return northEastSprites;
            case EFacing.W: _bFlip = true; return eastSprites;
            case EFacing.SW: _bFlip = true; return southEastSprites;
            case EFacing.SE: return southEastSprites;
            default: return southSprites;
        }
    }

    private static float RandomRange(Vector2 _range)
    {
        return Random.Range(_range.x, _range.y);
    }

    private void Update()
    {
        if (null == manager || PuppyBehavior.None == behavior) return;

        float _dt = Time.deltaTime;
        UpdateBehavior(_dt);
        UpdateAnim(_dt);
    }

    private void LateUpdate()
    {
        ApplySprite();
        sortingGroup.sortingOrder = -Mathf.RoundToInt(transform.position.y * 100f);
    }
}
