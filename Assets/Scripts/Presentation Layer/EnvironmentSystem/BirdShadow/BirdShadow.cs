using UnityEngine;

public class BirdShadow : EnvironmentObj
{
    //외부 의존성
    // (없음)

    //내부 의존성
    [SerializeField] private SpriteRenderer sr;


    [Header("Shadow Sprites")]
    [SerializeField] private Sprite ruSprite;
    [SerializeField] private Sprite rDSprite;
    [SerializeField] private Sprite lUSprite;
    [SerializeField] private Sprite lDSprite;

    private static readonly Vector3[] isoDirections = new Vector3[]
    {
        new Vector3(1f, 0.5f, 0f).normalized,   // 우상 (26.565도)
        new Vector3(-1f, 0.5f, 0f).normalized,  // 좌상 (153.435도)
        new Vector3(-1f, -0.5f, 0f).normalized, // 좌하 (-153.435도)
        new Vector3(1f, -0.5f, 0f).normalized   // 우하 (-26.565도)
    };

    private int flockIndex;
    private int birdIndexInFlock;
    private Vector3 mapCenter;
    private float spawnRadius;
    private float minSpeed;
    private float maxSpeed;
    private float timeOffset;
    private Vector3 flockOffset;
    private float lifeTime;
    private float delayTime;
    private float cycleDuration;
    private BirdShadow leaderBird;

    // 퍼블릭 초기화 및 제어 메서드

    public override void Initialize()
    {
        base.Initialize();
        if (null == sr)
        {
            sr = GetComponentInChildren<SpriteRenderer>();
        }
    }

    public void SetLeader(BirdShadow _leaderBird)
    {
        leaderBird = _leaderBird;
    }

    public void SetupBird(int _flockIndex, int _birdIndexInFlock, Vector3 _mapCenter, float _spawnRadius, float _minSpeed, float _maxSpeed, float _minDelay, float _maxDelay, Vector3 _flockOffset)
    {
        flockIndex = _flockIndex;
        birdIndexInFlock = _birdIndexInFlock;
        mapCenter = _mapCenter;
        spawnRadius = _spawnRadius;
        minSpeed = _minSpeed;
        maxSpeed = _maxSpeed;
        flockOffset = _flockOffset;

        float _avgSpeed = (minSpeed + maxSpeed) * 0.5f;
        lifeTime = (spawnRadius * 2.2f) / _avgSpeed;
        delayTime = GetPseudoRandom(flockIndex * 789, _minDelay, _maxDelay);
        cycleDuration = lifeTime + delayTime;

        float _initialProgress = GetPseudoRandom(flockIndex * 123, 0f, cycleDuration);
        timeOffset = -Time.time + _initialProgress;
    }

    public Vector3 GetFlightDirection()
    {
        if (cycleDuration <= 0f)
            return Vector3.up;

        float _totalTime = Time.time + timeOffset;
        int _cycleIndex = Mathf.FloorToInt(_totalTime / cycleDuration);
        return isoDirections[(flockIndex + _cycleIndex) % 4];
    }

    public override Vector3 GetCurrentPosition()
    {
        if (birdIndexInFlock > 0)
        {
            return transform.position;
        }

        if (cycleDuration <= 0f)
            return transform.position;

        float _totalTime = Time.time + timeOffset;
        int _cycleIndex = Mathf.FloorToInt(_totalTime / cycleDuration);
        float _elapsed = _totalTime - (_cycleIndex * cycleDuration);

        // 대기 시간(딜레이) 동안에는 이동 경과 시간을 0f으로 고정하여 대기 지점 유지
        float _flightElapsed = Mathf.Max(0f, _elapsed - delayTime);
        int _cycleSeed = flockIndex * 10000 + _cycleIndex;

        // 1. 아이소메트릭 변 방향 중 하나 선택 (순환 방식으로 번갈아가며 생성)
        int _dirIndex = (flockIndex + _cycleIndex) % 4;
        Vector3 _dir = isoDirections[_dirIndex];

        // 2. 방향에 수직인 벡터 및 오프셋 계산 (경로 다양성 확보)
        Vector3 _perp = new Vector3(-_dir.y, _dir.x, 0f);
        // 수직 편차 오프셋 범위를 축소하여 새 무리가 마름모 맵의 중앙 플레이 영역을 관통하도록 유도
        float _offsetDist = GetPseudoRandom(_cycleSeed + 2, -spawnRadius * 0.15f, spawnRadius * 0.15f);

        // 3. 출발지 설정 (선택된 진행 방향의 정반대편 외곽 가장자리)
        Vector3 _startPos = mapCenter - _dir * spawnRadius + _perp * _offsetDist;

        float _speed = GetPseudoRandom(_cycleSeed + 3, minSpeed, maxSpeed);

        return _startPos + _dir * (_speed * _flightElapsed);
    }

    public override void Show()
    {
        bActivated = true;
        if (sr != null)
        {
            sr.enabled = true;
        }
        UpdatePositionAndRotation();
    }

    public override void Hide()
    {
        bActivated = false;
        if (sr != null)
        {
            sr.enabled = false;
        }
    }

    public override void ResetObj()
    {
        base.ResetObj();
        leaderBird = null;
    }

    // 내부 메서드

    private float GetPseudoRandom(int _seed, float _min, float _max)
    {
        uint _state = (uint)_seed;
        _state = _state * 1664525u + 1013904223u;
        double _normalized = (double)_state / (double)uint.MaxValue;
        return _min + (float)(_normalized * (_max - _min));
    }

    private void UpdatePositionAndRotation()
    {
        if (birdIndexInFlock > 0)
        {
            if (null != leaderBird)
            {
                if (sr != null)
                {
                    sr.enabled = leaderBird.sr != null && leaderBird.sr.enabled && bActivated;
                    sr.sprite = leaderBird.sr != null ? leaderBird.sr.sprite : null;
                }

                Vector3 _dir = leaderBird.GetFlightDirection();
                Vector3 _perp = new Vector3(-_dir.y, _dir.x, 0f);
                float _side = (birdIndexInFlock % 2 == 1) ? -1f : 1f;
                int _depth = (birdIndexInFlock + 1) / 2;
                float _stepSide = 0.5f;
                float _stepBack = 0.6f;

                transform.localPosition = -_dir * (_depth * _stepBack) + _perp * (_side * _depth * _stepSide);
            }
            else
            {
                if (sr != null)
                {
                    sr.enabled = false;
                }
            }
            return;
        }

        Vector3 _currentPos = GetCurrentPosition();
        transform.position = _currentPos;

        if (cycleDuration <= 0f)
            return;

        float _totalTime = Time.time + timeOffset;
        int _cycleIndex = Mathf.FloorToInt(_totalTime / cycleDuration);
        float _elapsed = _totalTime - (_cycleIndex * cycleDuration);

        // 컬링 비활성화 상태이거나 대기 상태(딜레이)일 경우 비주얼 렌더러를 끄고 스프라이트 갱신을 생략함
        if (false == bActivated || _elapsed < delayTime)
        {
            if (sr != null && sr.enabled)
            {
                sr.enabled = false;
            }
            return;
        }

        // 비행 중일 경우 렌더러를 활성화함
        if (sr != null && !sr.enabled)
        {
            sr.enabled = true;
        }

        // GetCurrentPosition과 완벽히 동일한 순환 공식으로 방향 유도
        int _dirIndex = (flockIndex + _cycleIndex) % 4;

        if (sr != null)
        {
            switch (_dirIndex)
            {
                case 0:
                    sr.sprite = ruSprite;
                    break;
                case 1:
                    sr.sprite = lUSprite;
                    break;
                case 2:
                    sr.sprite = lDSprite;
                    break;
                case 3:
                    sr.sprite = rDSprite;
                    break;
            }
        }
    }

    // 유니티 이벤트 함수

    private void Update()
    {
        UpdatePositionAndRotation();
    }
}
