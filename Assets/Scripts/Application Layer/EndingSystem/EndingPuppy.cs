using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 엔딩 소풍 장면의 강아지. 본편 마을 강아지(Puppy)의 그림·애니메이션 순서·속도를 그대로 쓰고,
/// 행동은 본편 FSM 중 소풍 자리에서 할 수 있는 것만 골라 짧게 돈다.
///
/// 돗자리·캐릭터·바구니가 한 장의 그림(Chara_Ending)이라 강아지가 돗자리 뒤쪽에 올라서면 앞뒤가 틀어진다.
/// 그래서 강아지는 돗자리를 감싸는 아이소메트릭 타원 위로만 다니고, 잠깐 뛰어갔다 오기는 앞쪽 바깥으로만 나간다.
/// 게임플레이 로직(입력·길찾기·상호작용)은 없다.
/// </summary>
public class EndingPuppy : MonoBehaviour
{
    private enum EBehavior
    {
        None,
        LookAtOwner,    // 본편 1: 가만히 주인을 바라본다
        Daydream,       // 본편 2: 다른 곳을 보며 멍때린다
        CircleMat,      // 본편 3·4: 돗자리 둘레를 돈다
        Fetch,          // 본편 5: 잠깐 뛰어갔다 돌아온다
        GoToOwner,      // 본편 6: 주인 쪽으로 다가간다
        Sit,            // 본편 7: 앉아서 주인을 바라본다
        Sniff,          // 본편 8: 땅 냄새를 맡는다
    }

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
    }

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

    // 본편 Puppy.AnimFrames / AnimLoop와 같은 값(밥 먹기·잠 제외).
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
    };

    private static readonly bool[] AnimLoop = { true, true, false, false, true, false, true, false };

    // 행동이 끝났을 때 넘어갈 후보. 본편 NextOnComplete에서 밥·잠·섬 일주를 소풍 자리에 맞게 바꿨다.
    // 소풍 장면이 9초 남짓이라 멈춰 있는 행동 다음에는 움직이는 행동만, 움직인 다음에는 멈춰 있는 행동만 고른다.
    private static readonly EBehavior[][] NextOnComplete =
    {
        new EBehavior[0],
        new[] { EBehavior.CircleMat, EBehavior.GoToOwner, EBehavior.Fetch },                       // LookAtOwner
        new[] { EBehavior.CircleMat, EBehavior.GoToOwner },                                         // Daydream
        new[] { EBehavior.LookAtOwner, EBehavior.Sit, EBehavior.Sniff, EBehavior.Daydream },       // CircleMat
        new[] { EBehavior.LookAtOwner, EBehavior.Sit },                                             // Fetch
        new[] { EBehavior.LookAtOwner, EBehavior.Sit, EBehavior.Sniff },                            // GoToOwner
        new[] { EBehavior.CircleMat, EBehavior.GoToOwner, EBehavior.Fetch },                       // Sit
        new[] { EBehavior.CircleMat, EBehavior.GoToOwner, EBehavior.Fetch },                       // Sniff
    };

    [Header("Visual")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField, Tooltip("짖을 때 콩 튀어오르는 연출(본편 PuppyPopEffect.PlayHop). 몸통의 부모(Visual)에 붙는다.")]
    private PuppyPopEffect popEffect;

    [Header("Dust (본편과 같은 VFX_PuppyDust)")]
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private float dustInterval = 0.2f;
    [SerializeField] private float dustOffsetDistance = 0.08f;

    [Header("Sprites (본편 Puppy와 같은 시트)")]
    [SerializeField] private Sprite[] southSprites = new Sprite[15];
    [SerializeField] private Sprite[] southEastSprites = new Sprite[15];
    [SerializeField] private Sprite[] eastSprites = new Sprite[15];
    [SerializeField] private Sprite[] northEastSprites = new Sprite[15];
    [SerializeField] private Sprite[] northSprites = new Sprite[15];

    [Header("Animation FPS (본편 값)")]
    [SerializeField] private float runFps = 10f;
    [SerializeField] private float transitionFps = 12f;
    [SerializeField] private float daydreamWagFps = 5f;
    [SerializeField] private float wagFpsFar = 3f;
    [SerializeField] private float wagFpsNear = 14f;
    [SerializeField] private float wagFarDistance = 3f;
    [SerializeField] private float wagNearDistance = 0.5f;

    [Header("Movement")]
    [SerializeField] private float runSpeed = 3.6f;
    [SerializeField, Tooltip("주인(캐릭터가 앉은 자리).")] private Transform owner;
    [SerializeField, Tooltip("돗자리 가운데. 강아지가 도는 타원의 중심.")] private Transform matCenter;
    [SerializeField, Tooltip("돗자리를 감싸는 타원의 가로/세로 반지름. 돗자리(가로 1, 세로 0.5 마름모)보다 넉넉해야 한다.")]
    private Vector2 matRadius = new Vector2(1.45f, 0.725f);
    [SerializeField, Tooltip("처음 자리(타원 위 각도, 도). 0이 오른쪽, 270이 앞.")]
    private float startAngle = 235f;

    [Header("Behavior Timing (초, 범위 안 랜덤) - 소풍 장면이 짧아 본편보다 짧게")]
    [SerializeField] private Vector2 lookAtOwnerDuration = new Vector2(1.2f, 2f);
    [SerializeField] private Vector2 daydreamDuration = new Vector2(1f, 1.6f);
    [SerializeField] private Vector2 sitDuration = new Vector2(1.5f, 2.5f);
    [SerializeField] private Vector2 sniffDuration = new Vector2(0.8f, 1.4f);
    [SerializeField, Tooltip("돗자리 둘레를 도는 바퀴 수.")] private Vector2 circleLaps = new Vector2(0.5f, 1f);
    [SerializeField, Tooltip("앞쪽 바깥으로 뛰어나가는 거리.")] private Vector2 fetchDistance = new Vector2(0.8f, 1.4f);
    [SerializeField, Tooltip("돗자리 가운데에서 이 거리 밖으로는 뛰어나가지 않는다. 엔딩 숲은 picnicMatClearRadius(1.6) 밖부터 바위 등 장식을 깐다.")]
    private float fetchMaxRadius = 1.5f;
    [SerializeField, Tooltip("남은 거리가 이보다 짧으면 뛰어나가기를 고르지 않는다.")] private float fetchMinDistance = 0.4f;
    [SerializeField, Tooltip("뛰어나간 자리에서 주인을 바라보며 짖는 시간. 본편 fetchBarkDuration과 같은 값.")]
    private float fetchPauseDuration = 1f;
    [SerializeField, Tooltip("뛰어나간 자리에 도착한 뒤 짖는 시점(초). 본편 fetchBarkTimes와 같은 값.")]
    private float[] fetchBarkTimes = { 0.15f, 0.55f };

    private EBehavior behavior = EBehavior.None;
    private EBehavior pendingBehavior = EBehavior.None;
    private bool bExiting;
    private int phase;
    private float behaviorTimer;
    private readonly System.Collections.Generic.List<EBehavior> candidates = new System.Collections.Generic.List<EBehavior>(8);

    // 타원 위 위치(라디안)와 남은 이동 각도
    private float angle;
    private float angleRemaining;
    private float angleDirection = 1f;

    // 뛰어갔다 오기
    private Vector2 fetchOrigin;
    private Vector2 fetchPoint;
    private int fetchBarkCount;

    private EAnim currentAnim = EAnim.StandIdle;
    private int animFrame;
    private float animTimer;
    private float animFps = 1f;
    private bool bAnimFinished;
    private EFacing facing = EFacing.SW;
    private float dustTimer;

    private Vector2 Position => transform.position;
    private Vector2 Center => null != matCenter ? (Vector2)matCenter.position : Position;
    private Vector2 OwnerPosition => null != owner ? (Vector2)owner.position : Center;

    private void Awake()
    {
        if (null != popEffect) popEffect.Initialize(new[] { bodyRenderer });
    }

    private void OnEnable()
    {
        behavior = EBehavior.None;
        pendingBehavior = EBehavior.None;
        bExiting = false;
        dustTimer = 0f;

        angle = startAngle * Mathf.Deg2Rad;
        MoveTo(EllipsePoint(angle));
        FaceTowards(OwnerPosition - Position);

        // 화면이 밝아지는 동안 바로 움직이도록 둘레 돌기부터 시작한다.
        // 씬을 불러올 때(엔딩 디렉터가 소풍 자리를 끄기 전)와 검은 화면 중에도 불리므로 첫 행동은 소리를 내지 않는다.
        EnterBehavior(EBehavior.CircleMat, false);
        ApplySprite();
    }

    private void Update()
    {
        if (EBehavior.None == behavior) return;

        float _dt = Time.deltaTime;
        Vector2 _before = Position;
        UpdateBehavior(_dt);
        UpdateAnim(_dt);
        UpdateDust(_before, _dt);
    }

    private void LateUpdate()
    {
        ApplySprite();
        if (null != sortingGroup) sortingGroup.sortingOrder = -Mathf.RoundToInt(transform.position.y * 100f);
    }

    // ───────────────────────────── FSM ─────────────────────────────

    private void CompleteBehavior()
    {
        RequestBehavior(PickRandom(NextOnComplete[(int)behavior]));
    }

    // 본편 RequestBehavior와 같은 규칙: 앉기·냄새 맡기에서 넘어갈 때는 끝 애니메이션을 먼저 재생한다.
    private void RequestBehavior(EBehavior _next)
    {
        EAnim _endAnim;
        if (true == TryGetEndAnim(out _endAnim))
        {
            bExiting = true;
            pendingBehavior = _next;
            PlayAnim(_endAnim, transitionFps, true);
            return;
        }

        EnterBehavior(_next, true);
    }

    private bool TryGetEndAnim(out EAnim _endAnim)
    {
        switch (behavior)
        {
            case EBehavior.Sit: _endAnim = EAnim.SitEnd; return true;
            case EBehavior.Sniff: _endAnim = EAnim.BowEnd; return true;
        }

        _endAnim = EAnim.StandIdle;
        return false;
    }

    // 소리는 본편 EnterBehavior와 같은 시점: 바라보기·앉기는 낑낑, 둘레 돌기(본편 3·4)는 짖기.
    private void EnterBehavior(EBehavior _behavior, bool _bSound)
    {
        behavior = _behavior;
        pendingBehavior = EBehavior.None;
        bExiting = false;
        phase = 0;

        switch (_behavior)
        {
            case EBehavior.LookAtOwner:
                behaviorTimer = RandomRange(lookAtOwnerDuration);
                PlayAnim(EAnim.StandIdle, GetWagFps(), true);
                if (true == _bSound) Sound.Play(SoundID.PuppyWhining, transform.position);
                break;

            case EBehavior.Daydream:
                behaviorTimer = RandomRange(daydreamDuration);
                PlayAnim(EAnim.StandIdle, daydreamWagFps, true);
                FaceAwayFromOwner();
                break;

            case EBehavior.CircleMat:
                angleDirection = 0 == Random.Range(0, 2) ? 1f : -1f;
                angleRemaining = RandomRange(circleLaps) * Mathf.PI * 2f;
                // 다음 행동(멈춰 있기)이 돗자리·인물에 가려지지 않도록 앞쪽 절반에서 멈춘다.
                while (Mathf.Sin(angle + angleRemaining * angleDirection) > -0.25f) angleRemaining += 0.1f;
                PlayAnim(EAnim.Run, runFps, true);
                if (true == _bSound) Sound.Play(SoundID.PuppyBark, transform.position);
                break;

            case EBehavior.GoToOwner:
                SetArcTarget(OwnerNearestAngle());
                PlayAnim(EAnim.Run, runFps, true);
                break;

            case EBehavior.Fetch:
                fetchOrigin = Position;
                fetchPoint = fetchOrigin + OutwardDirection(angle) * Mathf.Min(RandomRange(fetchDistance), FetchRoom());
                PlayAnim(EAnim.Run, runFps, true);
                break;

            case EBehavior.Sit:
                PlayAnim(EAnim.SitStart, transitionFps, true);
                FaceTowards(OwnerPosition - Position);
                if (true == _bSound) Sound.Play(SoundID.PuppyWhining, transform.position);
                break;

            case EBehavior.Sniff:
                PlayAnim(EAnim.BowStart, transitionFps, true);
                break;
        }
    }

    private void UpdateBehavior(float _dt)
    {
        if (true == bExiting)
        {
            if (true == bAnimFinished) EnterBehavior(pendingBehavior, true);
            return;
        }

        switch (behavior)
        {
            case EBehavior.LookAtOwner:
                PlayAnim(EAnim.StandIdle, GetWagFps());
                FaceTowards(OwnerPosition - Position);
                TickTimer(_dt);
                break;

            case EBehavior.Daydream:
                TickTimer(_dt);
                break;

            case EBehavior.CircleMat:
            case EBehavior.GoToOwner:
                if (true == StepArc(_dt)) CompleteBehavior();
                break;

            case EBehavior.Fetch:
                UpdateFetch(_dt);
                break;

            case EBehavior.Sit:
                if (0 == phase)
                {
                    if (false == bAnimFinished) break;
                    phase = 1;
                    behaviorTimer = RandomRange(sitDuration);
                    PlayAnim(EAnim.SitIdle, GetWagFps(), true);
                    break;
                }

                PlayAnim(EAnim.SitIdle, GetWagFps());
                FaceTowards(OwnerPosition - Position);
                TickTimer(_dt);
                break;

            case EBehavior.Sniff:
                if (0 == phase)
                {
                    if (false == bAnimFinished) break;
                    phase = 1;
                    behaviorTimer = RandomRange(sniffDuration);
                    PlayAnim(EAnim.Bow, transitionFps, true);
                    break;
                }

                TickTimer(_dt);
                break;
        }
    }

    private void TickTimer(float _dt)
    {
        behaviorTimer -= _dt;
        if (behaviorTimer <= 0f) CompleteBehavior();
    }

    // 0: 뛰어나가기 → 1: 주인을 바라보며 본편처럼 "왕왕" 두 번 → 2: 원래 자리로 돌아오기
    private void UpdateFetch(float _dt)
    {
        switch (phase)
        {
            case 0:
                if (false == RunTowards(fetchPoint, _dt)) return;
                phase = 1;
                behaviorTimer = 0f;
                fetchBarkCount = 0;
                PlayAnim(EAnim.StandIdle, GetWagFps(), true);
                FaceTowards(OwnerPosition - Position);
                return;

            case 1:
                PlayAnim(EAnim.StandIdle, GetWagFps());
                FaceTowards(OwnerPosition - Position);
                behaviorTimer += _dt;
                if (null != fetchBarkTimes && fetchBarkCount < fetchBarkTimes.Length && behaviorTimer >= fetchBarkTimes[fetchBarkCount])
                {
                    fetchBarkCount++;
                    Sound.Play(SoundID.PuppyBark, transform.position);
                    if (null != popEffect) popEffect.PlayHop();
                }

                if (behaviorTimer < fetchPauseDuration) return;
                phase = 2;
                PlayAnim(EAnim.Run, runFps, true);
                return;

            default:
                if (false == RunTowards(fetchOrigin, _dt)) return;
                CompleteBehavior();
                return;
        }
    }

    private EBehavior PickRandom(EBehavior[] _from)
    {
        candidates.Clear();
        for (int i = 0; i < _from.Length; i++)
        {
            if (true == IsAvailable(_from[i])) candidates.Add(_from[i]);
        }

        if (0 == candidates.Count) return EBehavior.LookAtOwner;
        return candidates[Random.Range(0, candidates.Count)];
    }

    private bool IsAvailable(EBehavior _behavior)
    {
        switch (_behavior)
        {
            // 뒤쪽은 차·나무가 있고 돗자리에 가려지므로 앞쪽 절반에서만 뛰어나간다.
            case EBehavior.Fetch: return Mathf.Sin(angle) < -0.3f && FetchRoom() >= fetchMinDistance;
            // 이미 주인 옆이면 다가가지 않는다.
            case EBehavior.GoToOwner: return Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, OwnerNearestAngle() * Mathf.Rad2Deg)) > 30f;
        }

        return true;
    }

    // ───────────────────────────── 이동 ─────────────────────────────

    private Vector2 EllipsePoint(float _angle)
    {
        return Center + new Vector2(Mathf.Cos(_angle) * matRadius.x, Mathf.Sin(_angle) * matRadius.y);
    }

    // 타원 바깥쪽 방향(아이소메트릭 비율 그대로).
    private Vector2 OutwardDirection(float _angle)
    {
        return new Vector2(Mathf.Cos(_angle) * matRadius.x, Mathf.Sin(_angle) * matRadius.y).normalized;
    }

    // 지금 자리에서 바깥쪽으로 fetchMaxRadius 원에 닿을 때까지의 거리.
    private float FetchRoom()
    {
        Vector2 _origin = EllipsePoint(angle) - Center;
        Vector2 _dir = OutwardDirection(angle);
        float _b = Vector2.Dot(_origin, _dir);
        float _c = _origin.sqrMagnitude - fetchMaxRadius * fetchMaxRadius;
        float _disc = _b * _b - _c;
        if (_disc <= 0f) return 0f;
        return Mathf.Max(0f, -_b + Mathf.Sqrt(_disc));
    }

    private float OwnerNearestAngle()
    {
        const int Samples = 72;
        Vector2 _owner = OwnerPosition;
        float _best = angle;
        float _bestDistance = float.MaxValue;
        for (int i = 0; i < Samples; i++)
        {
            float _a = i * Mathf.PI * 2f / Samples;
            float _distance = (EllipsePoint(_a) - _owner).sqrMagnitude;
            if (_distance < _bestDistance)
            {
                _bestDistance = _distance;
                _best = _a;
            }
        }

        return _best;
    }

    // 짧은 쪽으로 타원을 따라 _target 각도까지 간다.
    private void SetArcTarget(float _target)
    {
        float _delta = Mathf.DeltaAngle(angle * Mathf.Rad2Deg, _target * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        angleDirection = _delta >= 0f ? 1f : -1f;
        angleRemaining = Mathf.Abs(_delta);
    }

    // 타원을 따라 runSpeed로 한 걸음. 남은 각도를 다 갔으면 true.
    private bool StepArc(float _dt)
    {
        if (angleRemaining <= 0f) return true;

        float _speedPerRadian = Mathf.Sqrt(Mathf.Pow(Mathf.Sin(angle) * matRadius.x, 2f) + Mathf.Pow(Mathf.Cos(angle) * matRadius.y, 2f));
        float _step = Mathf.Min(angleRemaining, runSpeed * _dt / Mathf.Max(0.0001f, _speedPerRadian));

        Vector2 _before = Position;
        angle = Mathf.Repeat(angle + _step * angleDirection, Mathf.PI * 2f);
        angleRemaining -= _step;
        MoveTo(EllipsePoint(angle));
        FaceTowards(Position - _before);

        return angleRemaining <= 0f;
    }

    // 목표를 향해 곧장 한 걸음. 도착했으면 true.
    private bool RunTowards(Vector2 _target, float _dt)
    {
        Vector2 _delta = _target - Position;
        float _distance = _delta.magnitude;
        float _step = runSpeed * _dt;
        if (_distance <= _step)
        {
            MoveTo(_target);
            return true;
        }

        FaceTowards(_delta);
        MoveTo(Position + _delta / _distance * _step);
        return false;
    }

    private void MoveTo(Vector2 _position)
    {
        transform.position = new Vector3(_position.x, _position.y, transform.position.z);
    }

    // ───────────────────────────── 방향 ─────────────────────────────

    private void FaceTowards(Vector2 _direction)
    {
        if (_direction.sqrMagnitude < 0.000001f) return;

        float _angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        int _index = Mathf.RoundToInt(_angle / 45f);
        facing = (EFacing)(((_index % FacingCount) + FacingCount) % FacingCount);
    }

    // 본편과 같이 주인 쪽(과 그 양옆 45도)을 뺀 나머지 방향 중 하나를 본다.
    private void FaceAwayFromOwner()
    {
        FaceTowards(OwnerPosition - Position);
        int _offset = Random.Range(2, FacingCount - 1);
        facing = (EFacing)(((int)facing + _offset) % FacingCount);
    }

    // ───────────────────────────── 애니메이션 ─────────────────────────────

    private float GetWagFps()
    {
        float _distance = (OwnerPosition - Position).magnitude;
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

    // 본편 ApplySprite와 같이 서쪽 3방향은 몸통 Transform 가로 스케일로 뒤집는다.
    private void ApplySprite()
    {
        if (null == bodyRenderer) return;

        bool _bFlip;
        Sprite[] _sheet = GetDirectionSheet(out _bFlip);
        int _frame = AnimFrames[(int)currentAnim][animFrame];
        if (null == _sheet || _frame >= _sheet.Length) return;

        bodyRenderer.sprite = _sheet[_frame];

        Transform _bodyTransform = bodyRenderer.transform;
        float _scaleX = true == _bFlip ? -1f : 1f;
        if (_bodyTransform.localScale.x != _scaleX)
            _bodyTransform.localScale = new Vector3(_scaleX, 1f, 1f);
    }

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

    // 본편 UpdateDust와 같은 방식: 달리는 동안 일정 간격으로 발밑 뒤쪽에 작은 먼지.
    private void UpdateDust(Vector2 _before, float _dt)
    {
        Vector2 _moveDelta = Position - _before;
        if (EAnim.Run != currentAnim || _moveDelta.sqrMagnitude < 0.0000001f || null == vfxComponent)
        {
            dustTimer = 0f;
            return;
        }

        dustTimer += _dt;
        if (dustTimer < dustInterval) return;

        dustTimer -= dustInterval;

        Vector2 _spawn = Position - _moveDelta.normalized * dustOffsetDistance;
        ParticleSystem _effect = vfxComponent.Play("Dust", _spawn, Quaternion.identity);
        if (null != _effect && null != sortingGroup)
            vfxComponent.SetSortingSettings(_effect, bodyRenderer.sortingLayerID, sortingGroup.sortingOrder);
    }

    private static float RandomRange(Vector2 _range)
    {
        return Random.Range(_range.x, _range.y);
    }
}
