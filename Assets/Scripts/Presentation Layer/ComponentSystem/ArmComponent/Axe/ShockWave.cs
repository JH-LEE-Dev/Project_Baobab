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

    private List<IStaticCollidable> targetsInRange = new List<IStaticCollidable>(16);
    private HashSet<IStaticCollidable> hitTargets = new HashSet<IStaticCollidable>();

    public void Initialize()
    {
        // 인스펙터에서 설정된 초기값들을 저장
        initialScale = transform.localScale;
        initialMinDist = minDist;
        initialMaxDist = maxDist;
        initialFindRange = findRange;
    }

    public void SetValue(float _damage, float _duration)
    {
        damage = _damage;
        lifeTime = _duration;
    }

    public void Reset()
    {
        timer = 0f;
        targetsInRange.Clear();
        hitTargets.Clear();
        
        // 리셋 시 스케일과 범위를 초기 상태로 복구
        transform.localScale = initialScale;
        minDist = initialMinDist;
        maxDist = initialMaxDist;
        findRange = initialFindRange;
    }

    private void ApplyShockWaveDamage()
    {
        if (CollisionSystem.Instance == null) return;

        // 현재 확장된 findRange를 사용하여 검색
        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, findRange, targetLayer.value, targetsInRange);

        Vector2 forward = transform.right;
        float halfAngle = angle * 0.5f;
        float minDistSqr = minDist * minDist;
        float maxDistSqr = maxDist * maxDist;

        for (int i = 0; i < targetsInRange.Count; i++)
        {
            var target = targetsInRange[i];
            
            if (!(target is TreeObj) || hitTargets.Contains(target)) continue;

            Vector2 targetPos = target.Position + target.Offset;
            Vector2 dirToTarget = targetPos - (Vector2)transform.position;
            float distSqr = dirToTarget.sqrMagnitude;

            // 현재 확장된 minDist ~ maxDist 범위 체크
            if (distSqr >= minDistSqr && distSqr <= maxDistSqr)
            {
                if (Vector2.Angle(forward, dirToTarget) <= halfAngle)
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
        float progress = Mathf.Clamp01(timer / lifeTime);

        // 1. 지정된 방향(transform.right)으로 이동
        transform.position += transform.right * (moveSpeed * Time.deltaTime);

        // 2. 시간에 따른 스케일 및 충돌 범위 확장
        float currentScaleMultiplier = Mathf.Lerp(1f, scaleFactor, progress);
        transform.localScale = initialScale * currentScaleMultiplier;
        
        // 판정 수치들도 동일한 비율로 확장
        minDist = initialMinDist * currentScaleMultiplier;
        maxDist = initialMaxDist * currentScaleMultiplier;
        findRange = initialFindRange * currentScaleMultiplier;
        
        // 3. 매 프레임 판정 수행
        ApplyShockWaveDamage();

        if (timer >= lifeTime)
        {
            ReturnToPoolEvent?.Invoke(this);
        }
    }
}
