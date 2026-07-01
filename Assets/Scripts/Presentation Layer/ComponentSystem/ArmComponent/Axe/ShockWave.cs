using System;
using System.Collections.Generic;
using UnityEngine;

public class ShockWave : MonoBehaviour
{
    public event Action<ShockWave> ReturnToPoolEvent;

    [Header("Basic Settings")]
    [SerializeField] private float lifeTime = 0.5f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float scaleFactor = 2f; // 스피드 보정을 위해 유지

    private float timer;
    private Vector3 startPosition;
    private float damage;
    private Vector3 moveDirection = Vector3.right;
    private bool bIsEnforced = false;
    private float maxEffectiveDistance = 0f;

    [Header("Sector Ring Settings")]
    public float minDist = 0f;
    public float maxDist = 2f;
    public float angle = 55f;
    public float findRange = 2.5f;

    // 초기 설정값 캐싱용
    private Vector3 initialScale;
    private float initialMinDist;
    private float initialMaxDist;
    private float initialFindRange;
    public Quaternion InitialRotation { get; private set; }

    // 최적화: 판정 주기 및 수학 연산용
    private float lastDamageCheckTime;
    private const float DAMAGE_CHECK_INTERVAL = 0.04f; // 약 25FPS 판정 (나무는 정적이므로 충분)
    private List<IStaticCollidable> targetsInRange = new List<IStaticCollidable>(128);
    private HashSet<IStaticCollidable> hitTargets = new HashSet<IStaticCollidable>();
    
    // 빠른 확장 시 충돌 누락(터널링) 방지용 (Sweep 처리)
    private float lastMinDist;

    // 비주얼 프로퍼티
    private Transform visualOrigin;
    public Transform VisualOrigin => visualOrigin;
    public float EffectiveExpandSpeed => moveSpeed * (1f + initialMaxDist * scaleFactor);

    public void Initialize()
    {
        angle = 45f; // 각도를 45도로 고정
        // 인스펙터에서 설정된 초기값들을 저장
        initialScale = transform.localScale;
        initialMinDist = minDist;
        initialMaxDist = maxDist;
        initialFindRange = findRange;
        InitialRotation = transform.rotation;
    }

    public void SetValue(float _damage, float _speed, float _duration)
    {
        damage = _damage;
        moveSpeed = _speed;
        lifeTime = _duration;

        float effectiveExpandSpeed = EffectiveExpandSpeed;
        maxEffectiveDistance = initialMaxDist + (effectiveExpandSpeed * lifeTime);
    }

    public void SetEnforced(bool _isEnforced)
    {
        bIsEnforced = _isEnforced;
    }

    public void SetDirection(Vector3 _dir)
    {
        moveDirection = _dir.normalized;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, moveDirection) * InitialRotation;
    }

    public void Reset()
    {
        angle = 45f; // 각도 고정 유지
        timer = 0f;
        lastDamageCheckTime = 0f;
        startPosition = transform.position;
        targetsInRange.Clear();
        hitTargets.Clear();
        moveDirection = Vector3.right;
        bIsEnforced = false;

        // 리셋 시 스케일과 범위를 초기 상태로 복구
        transform.localScale = initialScale;
        transform.rotation = InitialRotation;
        minDist = initialMinDist;
        maxDist = initialMaxDist;
        findRange = initialFindRange;
        lastMinDist = initialMinDist;
    }

    private void ApplyShockWaveDamage()
    {
        if (CollisionSystem.Instance == null) return;

        // 현재 확장된 findRange를 사용하여 검색
        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, findRange, targetLayer.value, targetsInRange);

        Vector3 centerPos = transform.position;
        Vector3 isoForward = Vector3.right;
        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            isoForward = new Vector3(moveDirection.x, moveDirection.y * 2f, 0f).normalized;
        }

        // AttackComponent.cs와 완전히 동일한 코사인 임계값 계산
        float cosThreshold = Mathf.Cos(angle * Mathf.Deg2Rad);

        // 스윕(Sweep) 로직: 이전 주기의 안쪽 반지름(lastMinDist)부터 현재 주기의 바깥쪽 반지름(maxDist)까지 한 번에 검사하여 터널링 방지
        float sweepMinDistSqr = lastMinDist * lastMinDist;
        float maxDistSqr = maxDist * maxDist;

        for (int i = 0; i < targetsInRange.Count; i++)
        {
            var target = targetsInRange[i];

            // 1. 이미 맞은 대상인지 먼저 체크 (O(1))
            if (hitTargets.Contains(target)) continue;
            
            // 2. 타입 체크 (레이어 필터링이 잘 되어 있다면 무시 가능하지만 안전을 위해 유지)
            if (!(target is TreeObj treeObj)) continue;

            Vector3 targetPos = treeObj.Position + treeObj.Offset;
            float isoDistSq = GetIsometricDistSq(targetPos, centerPos);

            // 3. 거리 범위 체크 (타원 적용 및 스윕)
            if (isoDistSq >= sweepMinDistSqr && isoDistSq <= maxDistSqr)
            {
                // AttackComponent.cs와 완전히 동일한 타원 판정 로직
                Vector3 targetOffset = targetPos - centerPos;
                Vector3 targetDir = Vector3.right;
                if (targetOffset.sqrMagnitude > 0.0001f)
                {
                    targetDir = new Vector3(targetOffset.x, targetOffset.y * 2f, 0f).normalized;
                }

                float dot = Vector3.Dot(isoForward, targetDir);

                if (dot >= cosThreshold)
                {
                    float finalDamage = damage;
                    if (bIsEnforced && maxEffectiveDistance > 0f)
                    {
                        float isoDistFromStartSq = GetIsometricDistSq(targetPos, startPosition);
                        float isoDistFromStart = Mathf.Sqrt(isoDistFromStartSq);
                        float t = Mathf.Clamp01(isoDistFromStart / maxEffectiveDistance);
                        float damageMultiplier = Mathf.Lerp(1.5f, 1.0f, t);
                        finalDamage *= damageMultiplier;
                    }

                    treeObj.TakeDamage(finalDamage);
                    hitTargets.Add(target);
                }
            }
        }
    }

    private float GetIsometricDistSq(Vector3 _p1, Vector3 _p2)
    {
        float _dx = _p1.x - _p2.x;
        float _dy = (_p1.y - _p2.y) * 2f;
        return _dx * _dx + _dy * _dy;
    }

    private void Update()
    {
        bool bIsFinished = false;
        timer += Time.deltaTime;

        if (timer >= lifeTime)
        {
            timer = lifeTime; // 최대 사거리 초과 방지
            bIsFinished = true;
        }

        // 1. 이동 거리에 따른 충돌 범위 확장
        // 기존에 날아가던 총합 속도(중심 이동 속도 + 스케일로 인한 범위 증가 속도)를 계산
        float effectiveExpandSpeed = EffectiveExpandSpeed;
        float expandDistance = effectiveExpandSpeed * timer;

        // 판정 수치들은 차이를 유지하기 위해 곱연산 대신 합연산 적용
        minDist = initialMinDist + expandDistance;
        maxDist = initialMaxDist + expandDistance;
        findRange = initialFindRange + expandDistance;

        // 2. 판정 주기 조절 (끝부분 누락을 방지하기 위해 종료 시에도 강제 판정)
        if (bIsFinished || timer >= lastDamageCheckTime + DAMAGE_CHECK_INTERVAL)
        {
            lastDamageCheckTime = timer;
            ApplyShockWaveDamage();
            lastMinDist = minDist; // 스윕 시작점 갱신
        }

        if (bIsFinished)
        {
            ReturnToPoolEvent?.Invoke(this);
        }
    }

    private void OnDrawGizmos()
    {
        Vector3 centerPos = transform.position;
        Vector3 dir = moveDirection.normalized;

        // 에디터가 실행 중이 아닐 때는 moveDirection이 기본값일 수 있으므로 transform.right 사용
        if (dir.sqrMagnitude < 0.0001f)
        {
            dir = transform.right;
        }

        Vector3 isoDir = new Vector3(dir.x, dir.y * 2f, 0f).normalized;

        // AttackComponent와 동일하게 angle 자체가 반각을 의미함
        Vector3 leftDir = Quaternion.Euler(0, 0, angle) * isoDir;
        Vector3 rightDir = Quaternion.Euler(0, 0, -angle) * isoDir;

        Vector3 ApplyIsometricScale(Vector3 v) => new Vector3(v.x, v.y * 0.5f, v.z);

        // 1. 전체 탐색 범위 (findRange) - 하늘색 타원
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
        DrawWireEllipse(centerPos, findRange, findRange * 0.5f);

        // 2. 실제 타격 판정 부채꼴 범위 (Sector Ring) - 빨간색
        Gizmos.color = Color.red;

        Vector3 leftMin = ApplyIsometricScale(leftDir * minDist);
        Vector3 leftMax = ApplyIsometricScale(leftDir * maxDist);
        Vector3 rightMin = ApplyIsometricScale(rightDir * minDist);
        Vector3 rightMax = ApplyIsometricScale(rightDir * maxDist);

        // 좌우 경계선
        Gizmos.DrawLine(centerPos + leftMin, centerPos + leftMax);
        Gizmos.DrawLine(centerPos + rightMin, centerPos + rightMax);

        // 최소/최대 거리 원호(Arc) 그리기
        int segments = 16;
        Vector3 prevMinPoint = centerPos + leftMin;
        Vector3 prevMaxPoint = centerPos + leftMax;

        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            float currAngle = Mathf.Lerp(angle, -angle, t);
            Vector3 currDir = Quaternion.Euler(0, 0, currAngle) * isoDir;

            Vector3 currMinPoint = centerPos + ApplyIsometricScale(currDir * minDist);
            Vector3 currMaxPoint = centerPos + ApplyIsometricScale(currDir * maxDist);

            Gizmos.DrawLine(prevMinPoint, currMinPoint);
            Gizmos.DrawLine(prevMaxPoint, currMaxPoint);

            prevMinPoint = currMinPoint;
            prevMaxPoint = currMaxPoint;
        }
    }

    private void DrawWireEllipse(Vector3 _center, float _radiusX, float _radiusY)
    {
        int _segments = 32;
        float _angle = 0f;
        Vector3 _lastPoint = _center + new Vector3(Mathf.Cos(0) * _radiusX, Mathf.Sin(0) * _radiusY, 0);
        for (int i = 1; i <= _segments; i++)
        {
            _angle = i * 2 * Mathf.PI / _segments;
            Vector3 _nextPoint = _center + new Vector3(Mathf.Cos(_angle) * _radiusX, Mathf.Sin(_angle) * _radiusY, 0);
            Gizmos.DrawLine(_lastPoint, _nextPoint);
            _lastPoint = _nextPoint;
        }
    }

    public void SetVisualOrigin(Transform _transform)
    {
        visualOrigin = _transform;
    }
}
