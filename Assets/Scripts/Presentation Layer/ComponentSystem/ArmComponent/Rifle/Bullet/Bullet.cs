using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public event Action<Bullet> ReturnToPoolEvent;

    [SerializeField] private float damage = 1f;
    [SerializeField] private LayerMask targetLayer;
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifeTime = 3f;

    [SerializeField] private CircleCollider2D col;

    private float timer;

    public void Initialize()
    {
        if (col == null)
            col = GetComponent<CircleCollider2D>();
    }

    public void Reset()
    {
        timer = 0f;
    }

    private void Update()
    {
        Vector2 startPos = transform.position;
        Vector2 direction = transform.right;
        float distance = speed * Time.deltaTime;

        // 콜라이더의 반지름을 사용하여 CircleCast 수행
        RaycastHit2D hit = Physics2D.CircleCast(startPos, col.radius, direction, distance, targetLayer);

        if (hit.collider != null)
        {
            // 충돌 지점으로 위치 보정 (콜라이더 중심점 위치인 centroid 사용)
            transform.position = hit.centroid;

            // IDamageable 인터페이스가 있는지 확인 후 데미지 처리
            if (hit.collider.TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(damage);
            }

            ReturnToPoolEvent?.Invoke(this);
            return;
        }

        // 충돌이 없으면 이동 적용
        transform.position = startPos + (direction * distance);

        // 일정 시간 후 자동 반환
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            ReturnToPoolEvent?.Invoke(this);
        }
    }
}
