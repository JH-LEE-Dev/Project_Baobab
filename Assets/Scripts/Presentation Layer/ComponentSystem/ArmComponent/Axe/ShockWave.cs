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
    [SerializeField] private float scaleFactor = 2f;

    private float timer;
    private Vector3 startPosition;
    private float damage;

    [Header("Sector Ring Settings")]
    public float minDist = 0f;
    public float maxDist = 2f;
    public float angle = 90f;
    public float findRange = 2.5f;

    // 초기 설정값 캐싱용
    private Vector3 initialScale;
    private float initialMinDist;
    private float initialMaxDist;
    private float initialFindRange;

    // 최적화: 판정 주기 및 수학 연산용
    private float lastDamageCheckTime;
    private const float DAMAGE_CHECK_INTERVAL = 0.04f; // 약 25FPS 판정 (나무는 정적이므로 충분)
    private float cosHalfAngle; 
    private float cosHalfAngleSqr;

    private List<IStaticCollidable> targetsInRange = new List<IStaticCollidable>(128);
    private HashSet<IStaticCollidable> hitTargets = new HashSet<IStaticCollidable>();

    public void Initialize()
    {
        // 인스펙터에서 설정된 초기값들을 저장
        initialScale = transform.localScale;
        initialMinDist = minDist;
        initialMaxDist = maxDist;
        initialFindRange = findRange;

        // 부채꼴 판정용 코사인 값 및 제곱값 미리 계산 (Acos, Sqrt 제거용)
        float _halfRad = angle * 0.5f * Mathf.Deg2Rad;
        cosHalfAngle = Mathf.Cos(_halfRad);
        cosHalfAngleSqr = cosHalfAngle * cosHalfAngle;
    }

    public void SetValue(float _damage, float _speed, float _duration)
    {
        damage = _damage;
        moveSpeed = _speed;
        lifeTime = _duration;
    }

    public void Reset()
    {
        timer = 0f;
        lastDamageCheckTime = 0f;
        startPosition = transform.position;
        targetsInRange.Clear();
        hitTargets.Clear();

        // 리셋 시 스케일과 범위를 초기 상태로 복구
        transform.localScale = initialScale;
        minDist = initialMinDist;
        maxDist = initialMaxDist;
        findRange = initialFindRange;
        
        float _halfRad = angle * 0.5f * Mathf.Deg2Rad;
        cosHalfAngle = Mathf.Cos(_halfRad);
        cosHalfAngleSqr = cosHalfAngle * cosHalfAngle;
    }

    private void ApplyShockWaveDamage()
    {
        if (CollisionSystem.Instance == null) return;

        // 현재 확장된 findRange를 사용하여 검색
        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, findRange, targetLayer.value, targetsInRange);

        Vector2 forward = transform.right;
        float minDistSqr = minDist * minDist;
        float maxDistSqr = maxDist * maxDist;

        for (int i = 0; i < targetsInRange.Count; i++)
        {
            var target = targetsInRange[i];

            // 1. 이미 맞은 대상인지 먼저 체크 (O(1))
            if (hitTargets.Contains(target)) continue;
            
            // 2. 타입 체크 (레이어 필터링이 잘 되어 있다면 무시 가능하지만 안전을 위해 유지)
            if (!(target is TreeObj)) continue;

            Vector2 targetPos = target.Position + target.Offset;
            Vector2 dirToTarget = targetPos - (Vector2)transform.position;
            float distSqr = dirToTarget.sqrMagnitude;

            // 3. 거리 범위 체크
            if (distSqr >= minDistSqr && distSqr <= maxDistSqr)
            {
                // 4. 내적을 이용한 부채꼴 판정 (Sqrt 연산 없이 제곱 비교)
                float dot = Vector2.Dot(forward, dirToTarget);
                // 방향이 앞쪽이고(dot > 0), 각도 조건 만족 시 (cos^2 * dist^2 <= dot^2)
                if (dot > 0 && (dot * dot) >= (cosHalfAngleSqr * distSqr))
                {
                    target.TakeDamage(damage);
                    hitTargets.Add(target);
                }
            }
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // 1. 지정된 방향(transform.right)으로 이동
        transform.position += transform.right * (moveSpeed * Time.deltaTime);

        // 2. 이동 거리에 따른 스케일 및 충돌 범위 확장
        // 최적화: Vector3.Distance(sqrt) 대신 단순 시간*속도로 계산
        float distanceTraveled = moveSpeed * timer;
        float currentScaleMultiplier = 1f + (distanceTraveled * scaleFactor);

        transform.localScale = initialScale * currentScaleMultiplier;

        // 판정 수치들도 동일한 비율로 확장
        minDist = initialMinDist * currentScaleMultiplier;
        maxDist = initialMaxDist * currentScaleMultiplier;
        findRange = initialFindRange * currentScaleMultiplier;

        // 3. 판정 주기 조절 (매 프레임 실행하지 않음)
        if (timer >= lastDamageCheckTime + DAMAGE_CHECK_INTERVAL)
        {
            lastDamageCheckTime = timer;
            ApplyShockWaveDamage();
        }

        if (timer >= lifeTime)
        {
            ReturnToPoolEvent?.Invoke(this);
        }
    }
}
