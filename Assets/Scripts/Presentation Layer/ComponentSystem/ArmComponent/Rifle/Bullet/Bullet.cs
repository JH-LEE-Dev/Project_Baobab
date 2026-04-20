using System;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public event Action<Bullet> ReturnToPoolEvent;

    [SerializeField] private float damage = 1f;
    [SerializeField] private float speed = 10f;
    [SerializeField] private float lifeTime = 3f;
    [SerializeField] private float bulletRadius = 0.2f;
    [SerializeField] private LayerMask targetLayer;

    private float timer;

    public void Initialize()
    {
        // 커스텀 충돌 시스템을 사용하므로 기존 콜라이더 컴포넌트 비활성화 권장
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }

    public void SetDamage(float _damage)
    {
        damage = _damage;
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

        // 커스텀 충돌 시스템 사용 (레이어 마스크 추가)
        if (CollisionSystem.Instance != null && 
            CollisionSystem.Instance.CheckCollision(startPos, endPos, bulletRadius, targetLayer, out IStaticCollidable hitObject))
        {
            // 충돌 지점으로 이동
            transform.position = hitObject.Position;

            // 데미지 처리
            hitObject.TakeDamage(damage);

            ReturnToPoolEvent?.Invoke(this);
            return;
        }

        // 충돌이 없으면 이동 적용
        transform.position = endPos;

        // 일정 시간 후 자동 반환
        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            ReturnToPoolEvent?.Invoke(this);
        }
    }
}

