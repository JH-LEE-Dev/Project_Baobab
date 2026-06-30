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
    private Vector3 moveDirection = Vector3.right;

    [Header("Sector Ring Settings")]
    public float minDist = 0f;
    public float maxDist = 2f;
    public float angle = 90f;
    public float findRange = 2.5f;

    private Vector3 initialScale;
    private float initialMinDist;
    private float initialMaxDist;
    private float initialFindRange;
    public Quaternion InitialRotation { get; private set; }

    private float lastDamageCheckTime;
    private const float DAMAGE_CHECK_INTERVAL = 0.04f;
    private float cosHalfAngle;
    private float cosHalfAngleSqr;

    private List<IStaticCollidable> targetsInRange = new List<IStaticCollidable>(128);
    private HashSet<IStaticCollidable> hitTargets = new HashSet<IStaticCollidable>();

    public void Initialize()
    {
        initialScale = transform.localScale;
        initialMinDist = minDist;
        initialMaxDist = maxDist;
        initialFindRange = findRange;
        InitialRotation = transform.rotation;

        float halfRad = angle * 0.5f * Mathf.Deg2Rad;
        cosHalfAngle = Mathf.Cos(halfRad);
        cosHalfAngleSqr = cosHalfAngle * cosHalfAngle;
    }

    public void SetValue(float _damage, float _speed, float _duration)
    {
        damage = _damage;
        moveSpeed = _speed;
        lifeTime = _duration;
    }

    public void SetDirection(Vector3 _dir)
    {
        moveDirection = _dir.normalized;
        transform.rotation = Quaternion.FromToRotation(Vector3.right, moveDirection) * InitialRotation;
    }

    public void Reset()
    {
        timer = 0f;
        lastDamageCheckTime = 0f;
        startPosition = transform.position;
        targetsInRange.Clear();
        hitTargets.Clear();
        moveDirection = Vector3.right;

        transform.localScale = initialScale;
        transform.rotation = InitialRotation;
        minDist = initialMinDist;
        maxDist = initialMaxDist;
        findRange = initialFindRange;

        float halfRad = angle * 0.5f * Mathf.Deg2Rad;
        cosHalfAngle = Mathf.Cos(halfRad);
        cosHalfAngleSqr = cosHalfAngle * cosHalfAngle;
    }

    private void ApplyShockWaveDamage()
    {
        if (CollisionSystem.Instance == null) return;

        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, findRange, targetLayer.value, targetsInRange);

        Vector2 forward = moveDirection;
        float minDistSqr = minDist * minDist;
        float maxDistSqr = maxDist * maxDist;

        for (int i = 0; i < targetsInRange.Count; i++)
        {
            var target = targetsInRange[i];

            if (hitTargets.Contains(target)) continue;
            if (!(target is TreeObj)) continue;

            Vector2 targetPos = target.Position + target.Offset;
            Vector2 dirToTarget = targetPos - (Vector2)transform.position;
            float distSqr = dirToTarget.sqrMagnitude;

            if (distSqr >= minDistSqr && distSqr <= maxDistSqr)
            {
                float dot = Vector2.Dot(forward, dirToTarget);
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

        transform.position += moveDirection * (moveSpeed * Time.deltaTime);

        float distanceTraveled = moveSpeed * timer;
        float currentScaleMultiplier = 1f + (distanceTraveled * scaleFactor);

        transform.localScale = initialScale * currentScaleMultiplier;

        minDist = initialMinDist * currentScaleMultiplier;
        maxDist = initialMaxDist * currentScaleMultiplier;
        findRange = initialFindRange * currentScaleMultiplier;

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
