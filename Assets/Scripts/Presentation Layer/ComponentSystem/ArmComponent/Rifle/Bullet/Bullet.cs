using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public event Action<Bullet> ReturnToPoolEvent;
    public event Action<Bullet, Vector2, Vector2, int,IStaticCollidable> RicochetEvent;

    [SerializeField] private float damage = 1f;
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private float bulletRadius = 0.2f;
    [SerializeField] private LayerMask targetLayer;

    private float penetrationChance = 0f;
    private int ricochetCnt = 0;
    private float ricochetAngle = 90f;
    private float ricochetDist = 0.5f;


    private float knockBackForce = 3f;
    private float timer;

    private IStaticCollidable ignoreTarget;

    public void Initialize()
    {
        // 커스텀 충돌 시스템을 사용하므로 기존 콜라이더 컴포넌트 비활성화 권장
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    public void SetValue(StatComponent _characterStat)
    {
        ricochetCnt = _characterStat.ricochetCnt;
        penetrationChance = _characterStat.gunPenetrationChance;
        damage = _characterStat.rifleDamage;
        ricochetAngle = _characterStat.ricochetAngle;
        ricochetDist = _characterStat.ricochetDist;
    }

    // 도비탄 생성 시 사용할 전용 초기화 메서드
    public void SetRicochetValue(float _damage, int _remainRicochetCnt, float _angle, float _dist, float _penChance, IStaticCollidable _ignoreTarget)
    {
        damage = _damage;
        ricochetCnt = _remainRicochetCnt;
        ricochetAngle = _angle;
        ricochetDist = _dist;
        penetrationChance = _penChance;
        ignoreTarget = _ignoreTarget;
    }

    public void Reset()
    {
        timer = 0f;
    }

    private void Update()
    {
        Vector2 startPos = transform.position;
        Vector2 direction = transform.right;
        float moveDistance = speed * Time.deltaTime;
        Vector2 endPos = startPos + (direction * moveDistance);

        // 커스텀 충돌 시스템 사용 (레이어 마스크 추가 및 ignoreTarget 무시)
        if (CollisionSystem.Instance != null &&
            CollisionSystem.Instance.CheckCollision(startPos, endPos, bulletRadius, (int)targetLayer, out IStaticCollidable hitObject) &&
            hitObject != ignoreTarget)
        {
            // 데미지 처리 (TreeObj인 경우 데미지를 주지 않음)
            if (!(hitObject is TreeObj))
            {
                hitObject.TakeDamage(damage);

                if (hitObject is IDamageable damageable)
                {
                    damageable.KnockBack(direction, knockBackForce);
                }

                // 관통 여부 관계 없이 Animal에 맞았을 때 도비탄 이벤트 발생
                if (hitObject is Animal && ricochetCnt > 0)
                {
                    RicochetEvent?.Invoke(this, hitObject.Position, direction, ricochetCnt,hitObject);
                }
            }

            // 관통 로직: 확률에 당첨된 경우 (당첨될 때마다 확률 반감)
            if (UnityEngine.Random.value < penetrationChance)
            {
                penetrationChance *= 0.5f; // 관통 확률 절반으로 감소
                // 관통했으므로 멈추지 않고 이번 프레임의 목표 지점까지 이동하여 계속 진행
                transform.position = endPos;
            }
            else
            {
                // 관통하지 못한 경우 충돌 지점으로 이동 후 풀로 반환
                transform.position = hitObject.Position;
                ReturnToPoolEvent?.Invoke(this);
                return;
            }
        }
        else
        {
            // 충돌이 없거나 ignoreTarget인 경우 이동 적용
            transform.position = endPos;
        }

        // 일정 시간 후 자동 반환
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            ReturnToPoolEvent?.Invoke(this);
        }
    }

    // 도비탄 로직에서 참조할 정보들
    public float GetDamage() => damage;
    public float GetRicochetAngle() => ricochetAngle;
    public float GetRicochetDist() => ricochetDist;
    public float GetPenetrationChance() => penetrationChance;
    public int GetTargetLayer() => targetLayer.value;
}

