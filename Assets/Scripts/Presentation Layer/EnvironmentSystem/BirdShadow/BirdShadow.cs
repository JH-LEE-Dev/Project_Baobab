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

        // 무리 간 15~20초 간격을 두고 순차적으로 날기 시작하도록 누적 딜레이 계산 (첫 무리도 최소 15~20초 대기)
        float _accumulatedDelay = 0f;
        for (int _i = 0; _i <= flockIndex; _i++)
        {
            _accumulatedDelay += GetPseudoRandom((_i + 1) * 1234, 15f, 20f);
        }
        delayTime = _accumulatedDelay;

        // 최초 딜레이 이후부터는 대기 시간 없이 비행(lifeTime)만 순환
        cycleDuration = lifeTime;

        timeOffset = -Time.time;
    }

    private struct FlightState
    {
        public int cycleIndex;
        public bool isWaiting;
        public float elapsedFlightTime;
    }

    public Vector3 GetFlightDirection()
    {
        float _totalTime = Time.time + timeOffset;
        FlightState _state = GetFlightState(_totalTime);
        return isoDirections[(flockIndex + _state.cycleIndex) % 4];
    }

    public override Vector3 GetCurrentPosition()
    {
        if (birdIndexInFlock > 0)
        {
            return transform.position;
        }

        float _totalTime = Time.time + timeOffset;
        FlightState _state = GetFlightState(_totalTime);

        int _dirIndex = (flockIndex + _state.cycleIndex) % 4;
        Vector3 _dir = isoDirections[_dirIndex];
        Vector3 _perp = new Vector3(-_dir.y, _dir.x, 0f);
        int _cycleSeed = flockIndex * 10000 + _state.cycleIndex;
        float _offsetDist = GetPseudoRandom(_cycleSeed + 2, -spawnRadius * 0.15f, spawnRadius * 0.15f);
        Vector3 _startPos = mapCenter - _dir * spawnRadius + _perp * _offsetDist;

        if (_state.isWaiting)
        {
            return _startPos;
        }

        float _speed = GetPseudoRandom(_cycleSeed + 3, minSpeed, maxSpeed);
        return _startPos + _dir * (_speed * _state.elapsedFlightTime);
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

    private FlightState GetFlightState(float _totalTime)
    {
        FlightState _state;
        int _k = 0;
        float _accumulatedTime = 0f;
        float _currentCycleDelay = 0f;

        while (true)
        {
            if (_k == 0)
            {
                _currentCycleDelay = delayTime;
            }
            else
            {
                _currentCycleDelay = GetPseudoRandom(flockIndex * 10000 + _k, 15f, 20f);
            }

            float _waitEnd = _accumulatedTime + _currentCycleDelay;
            float _flightEnd = _waitEnd + lifeTime;

            if (_totalTime < _waitEnd)
            {
                _state.cycleIndex = _k;
                _state.isWaiting = true;
                _state.elapsedFlightTime = 0f;
                return _state;
            }
            else if (_totalTime < _flightEnd)
            {
                _state.cycleIndex = _k;
                _state.isWaiting = false;
                _state.elapsedFlightTime = _totalTime - _waitEnd;
                return _state;
            }

            _accumulatedTime = _flightEnd;
            _k++;
        }
    }

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

        float _totalTime = Time.time + timeOffset;
        FlightState _state = GetFlightState(_totalTime);

        // 대기 중이거나 비활성화 시 렌더러 숨김
        if (false == bActivated || _state.isWaiting)
        {
            if (sr != null && sr.enabled)
            {
                sr.enabled = false;
            }
            return;
        }

        if (sr != null && !sr.enabled)
        {
            sr.enabled = true;
        }

        int _dirIndex = (flockIndex + _state.cycleIndex) % 4;

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
