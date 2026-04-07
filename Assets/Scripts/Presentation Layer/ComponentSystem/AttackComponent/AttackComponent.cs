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
    }

    private void ReleaseEvents()
    {
        if (ctx == null || ctx.inputManager == null)
            return;

        ctx.inputManager.inputReader.MouseMoveEvent -= MouseMove;
    }

    private void MouseMove(Vector2 _mouseScreenPos)
    {
        if (characterTransform == null || attackCollider == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        // 1. 현재 모니터 화면 좌표를 0~1 비율(정규화)로 변환
        float _normalizedX = _mouseScreenPos.x / Screen.width;
        float _normalizedY = _mouseScreenPos.y / Screen.height;

        // 2. 월드 카메라가 출력 중인 RenderTexture(또는 실제 픽셀) 해상도 기준으로 좌표 리매핑
        // mainCamera.targetTexture가 설정되어 있다면 해당 텍스처 해상도를 사용합니다.
        float _targetWidth = (mainCamera.targetTexture != null) ? mainCamera.targetTexture.width : mainCamera.pixelWidth;
        float _targetHeight = (mainCamera.targetTexture != null) ? mainCamera.targetTexture.height : mainCamera.pixelHeight;

        Vector3 _convertedMousePos = new Vector3(
            _normalizedX * _targetWidth,
            _normalizedY * _targetHeight,
            -mainCamera.transform.position.z // 카메라와 월드(Z=0) 사이의 거리
        );

        // 3. 변환된 좌표를 사용하여 월드 좌표 계산
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(_convertedMousePos);
        mouseWorldPos.z = 0;

        // 4. 캐릭터에서 마우스 방향으로의 벡터 계산
        Vector3 characterPos = characterTransform.position;
        Vector3 direction = mouseWorldPos - characterPos;

        // 5. 거리 제한 (ClampMagnitude)
        if (direction.magnitude > maxAttackDistance)
        {
            direction = direction.normalized * maxAttackDistance;
        }

        // 6. 콜라이더 위치 업데이트
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
        if (attackCollider == null)
            attackCollider = GetComponent<Collider2D>();

        if (attackCollider == null) return;

        // 1. 공격 콜라이더 범위 시각화 (빨간색)
        Gizmos.color = Color.red;
        if (attackCollider is CircleCollider2D circle)
        {
            Gizmos.DrawWireSphere(attackCollider.transform.position + (Vector3)circle.offset, circle.radius);
        }
        else
        {
            Gizmos.DrawWireSphere(attackCollider.bounds.center, 0.5f);
        }

        // 2. 최대 공격 가능 사거리 시각화 (노란색)
        if (characterTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(characterTransform.position, maxAttackDistance);
        }
    }

    public Transform GetAttackPointTransform()
    {
        return attackCollider.transform;
    }
}
