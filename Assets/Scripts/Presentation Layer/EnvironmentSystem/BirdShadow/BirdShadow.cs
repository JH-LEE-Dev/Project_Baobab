using UnityEngine;

public class BirdShadow : EnvironmentObj
{
    //외부 의존성
    // (없음)

    //내부 의존성
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float rotationOffset = 0f;

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

    // 퍼블릭 초기화 및 제어 메서드

    public override void Initialize()
    {
        base.Initialize();
        if (null == sr)
        {
            sr = GetComponentInChildren<SpriteRenderer>();
        }
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

    public override Vector3 GetCurrentPosition()
    {
        if (cycleDuration <= 0f)
            return transform.position;

        float _totalTime = Time.time + timeOffset;
        int _cycleIndex = Mathf.FloorToInt(_totalTime / cycleDuration);
        float _elapsed = _totalTime - (_cycleIndex * cycleDuration);

        // 대기 시간(딜레이) 구간
        if (_elapsed < delayTime)
        {
            // 카메라에 걸리지 않도록 매우 먼 위치를 반환
            return mapCenter + Vector3.down * 9999f;
        }

        float _flightElapsed = _elapsed - delayTime;
        int _cycleSeed = flockIndex * 10000 + _cycleIndex;

        float _startAngle = GetPseudoRandom(_cycleSeed + 1, 0f, Mathf.PI * 2f);
        Vector3 _startPos = mapCenter + new Vector3(Mathf.Cos(_startAngle), Mathf.Sin(_startAngle), 0f) * spawnRadius;

        float _targetAngle = GetPseudoRandom(_cycleSeed + 2, 0f, Mathf.PI * 2f);
        float _targetDist = GetPseudoRandom(_cycleSeed + 3, 0f, spawnRadius * 0.3f);
        Vector3 _targetPos = mapCenter + new Vector3(Mathf.Cos(_targetAngle), Mathf.Sin(_targetAngle), 0f) * _targetDist;

        Vector3 _dir = (_targetPos - _startPos).normalized;
        float _speed = GetPseudoRandom(_cycleSeed + 4, minSpeed, maxSpeed);

        return _startPos + _dir * (_speed * _flightElapsed) + flockOffset;
    }

    public override void Show()
    {
        UpdatePositionAndRotation();
        base.Show();
    }

    public override void ResetObj()
    {
        base.ResetObj();
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
        Vector3 _currentPos = GetCurrentPosition();
        transform.position = _currentPos;

        if (cycleDuration <= 0f)
            return;

        float _totalTime = Time.time + timeOffset;
        int _cycleIndex = Mathf.FloorToInt(_totalTime / cycleDuration);
        float _elapsed = _totalTime - (_cycleIndex * cycleDuration);

        // 대기 상태(딜레이)일 경우 회전 갱신을 생략함
        if (_elapsed < delayTime)
            return;

        int _cycleSeed = flockIndex * 10000 + _cycleIndex;

        float _startAngle = GetPseudoRandom(_cycleSeed + 1, 0f, Mathf.PI * 2f);
        Vector3 _startPos = mapCenter + new Vector3(Mathf.Cos(_startAngle), Mathf.Sin(_startAngle), 0f) * spawnRadius;

        float _targetAngle = GetPseudoRandom(_cycleSeed + 2, 0f, Mathf.PI * 2f);
        float _targetDist = GetPseudoRandom(_cycleSeed + 3, 0f, spawnRadius * 0.3f);
        Vector3 _targetPos = mapCenter + new Vector3(Mathf.Cos(_targetAngle), Mathf.Sin(_targetAngle), 0f) * _targetDist;

        Vector3 _dir = (_targetPos - _startPos).normalized;

        float _angleDeg = Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg + rotationOffset;
        transform.rotation = Quaternion.Euler(0f, 0f, _angleDeg);
    }

    // 유니티 이벤트 함수

    private void Update()
    {
        if (false == bActivated)
            return;

        UpdatePositionAndRotation();
    }
}
