using System;
using UnityEngine;

public class AttackComponent : PComponent
{
    //외부 의존성
    private Camera mainCamera;

    //내부 의존성
    [Header("Attack Settings")]
    [SerializeField] private float attackDamage = 10f; // 공격 데미지
    [SerializeField] private float maxAttackDistance = 2.0f; // 캐릭터로부터 공격 콜라이더가 떨어질 수 있는 최대 거리
    [SerializeField] private LayerMask targetLayer; // 공격 대상 레이어 (Tree 등)

    private Collider2D attackCollider;
    private Transform characterTransform;

    //최적화를 위한 재사용 컬렉션
    private Collider2D[] results = new Collider2D[10];
    private ContactFilter2D contactFilter;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        attackCollider = GetComponent<Collider2D>();
        characterTransform = transform.parent; 
        mainCamera = Camera.main;

        // 트리 레이어 등 특정 레이어만 필터링하도록 설정
        contactFilter.SetLayerMask(targetLayer);
        contactFilter.useLayerMask = true;
        contactFilter.useTriggers = true;

        BindEvents();
    }

    private void BindEvents()
    {
        if (ctx == null || ctx.inputManager == null)
            return;

        ctx.inputManager.inputReader.MouseMoveEvent -= MouseMove;
        ctx.inputManager.inputReader.MouseMoveEvent += MouseMove;

        ctx.inputManager.inputReader.MouseClickEvent -= Attack;
        ctx.inputManager.inputReader.MouseClickEvent += Attack;
    }

    private void ReleaseEvents()
    {
        if (ctx == null || ctx.inputManager == null)
            return;

        ctx.inputManager.inputReader.MouseMoveEvent -= MouseMove;
        ctx.inputManager.inputReader.MouseClickEvent -= Attack;
    }

    private void MouseMove(Vector2 _mouseScreenPos)
    {
        if (characterTransform == null || attackCollider == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        // 1. 마우스 화면 좌표를 월드 좌표로 변환
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(new Vector3(_mouseScreenPos.x, _mouseScreenPos.y, -mainCamera.transform.position.z));
        mouseWorldPos.z = 0;

        // 2. 캐릭터에서 마우스 방향으로의 벡터 계산
        Vector3 characterPos = characterTransform.position;
        Vector3 direction = mouseWorldPos - characterPos;

        // 3. 거리 제한 (ClampMagnitude)
        if (direction.magnitude > maxAttackDistance)
        {
            direction = direction.normalized * maxAttackDistance;
        }

        // 4. 콜라이더 위치 업데이트
        attackCollider.transform.position = characterPos + direction;
    }

    private void Attack()
    {
        if (attackCollider == null) return;

        // GC Alloc 없이 충돌체 탐지
        int hitCount = attackCollider.Overlap(contactFilter, results);

        for (int i = 0; i < hitCount; i++)
        {
            // IDamageable 인터페이스가 있는지 확인 후 데미지 처리
            if (results[i].TryGetComponent(out IDamageable damageable))
            {
                damageable.TakeDamage(attackDamage);
            }
        }
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }

    private void OnDrawGizmos()
    {
        // 1. 최대 사거리 표시 (노란색 원)
        Gizmos.color = Color.yellow;
        Transform parentTransform = characterTransform != null ? characterTransform : transform.parent;
        if (parentTransform != null)
        {
            Gizmos.DrawWireSphere(parentTransform.position, maxAttackDistance);
        }

        // 2. 현재 공격 콜라이더 위치 표시 (빨간색 원)
        if (attackCollider != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackCollider.transform.position, 0.3f);

            if (parentTransform != null)
            {
                Gizmos.DrawLine(parentTransform.position, attackCollider.transform.position);
            }
        }
    }
}
