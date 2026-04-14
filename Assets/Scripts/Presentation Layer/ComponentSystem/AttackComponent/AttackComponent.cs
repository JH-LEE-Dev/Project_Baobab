using System;
using UnityEngine;

public class AttackComponent : PComponent
{
    public event Action AttackSuccessEvent;
    public event Action<WeaponMode> WeaponModeChangedEvent;
    //외부 의존성
    private Camera mainCamera;

    //내부 의존성
    [Header("Attack Settings")]
    [SerializeField] private float attackDamage = 10f; // 공격 데미지
    [SerializeField] private float maxAttackDistance = 2.0f; // 캐릭터로부터 공격 콜라이더가 떨어질 수 있는 최대 거리
    [SerializeField] private LayerMask targetLayer; // 공격 대상 레이어 (Tree 등)
    [SerializeField] private bool bAttackOnlyNearest = true; // 가장 가까운 대상 하나만 공격할지 여부

    private Collider2D attackCollider;
    private Transform characterTransform;

    //최적화를 위한 재사용 컬렉션
    private Collider2D[] results = new Collider2D[10];
    private ContactFilter2D contactFilter;

    private WeaponMode currentWeaponMode = WeaponMode.Axe;

    private bool bAttack = false;

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

        ctx.inputManager.inputReader.SwitchModeKeyPressedEvent -= SwitchWeaponMode;
        ctx.inputManager.inputReader.SwitchModeKeyPressedEvent += SwitchWeaponMode;
    }

    private void ReleaseEvents()
    {
        if (ctx == null || ctx.inputManager == null)
            return;

        ctx.inputManager.inputReader.MouseMoveEvent -= MouseMove;
        ctx.inputManager.inputReader.SwitchModeKeyPressedEvent -= SwitchWeaponMode;
    }

    private void MouseMove(Vector2 _mouseScreenPos)
    {
        if (bAttack || characterTransform == null || attackCollider == null)
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

        // 5. 일정 거리(Radius) 무조건 유지
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction = direction.normalized * maxAttackDistance;
        }
        else
        {
            // 마우스가 캐릭터와 겹칠 경우 기존 오프셋 방향 유지 혹은 기본값 처리
            Vector3 currentOffset = attackCollider.transform.position - characterPos;
            direction = (currentOffset.sqrMagnitude > 0.0001f)
                ? currentOffset.normalized * maxAttackDistance
                : Vector3.right * maxAttackDistance;
        }

        // 6. 콜라이더 위치 업데이트
        attackCollider.transform.position = characterPos + direction;
    }

    public void Attack()
    {
        if (attackCollider == null) return;

        // GC Alloc 없이 충돌체 탐지
        int hitCount = attackCollider.Overlap(contactFilter, results);
        if (hitCount <= 0) return;

        if (bAttackOnlyNearest)
        {
            IDamageable nearestDamageable = null;
            float minDistanceSqr = float.MaxValue;
            Vector3 attackPos = attackCollider.transform.position;

            for (int i = 0; i < hitCount; i++)
            {
                if (results[i].TryGetComponent(out IDamageable damageable))
                {
                    // 거리의 제곱을 비교하여 가장 가까운 대상 탐색 (성능 최적화)
                    float distSqr = (results[i].transform.position - attackPos).sqrMagnitude;
                    if (distSqr < minDistanceSqr)
                    {
                        minDistanceSqr = distSqr;
                        nearestDamageable = damageable;
                    }
                }
            }

            if (nearestDamageable != null)
            {
                nearestDamageable.TakeDamage(attackDamage);
                AttackSuccessEvent?.Invoke();
            }
        }
        else
        {
            for (int i = 0; i < hitCount; i++)
            {
                // IDamageable 인터페이스가 있는지 확인 후 데미지 처리
                if (results[i].TryGetComponent(out IDamageable damageable))
                {
                    damageable.TakeDamage(attackDamage);
                    AttackSuccessEvent?.Invoke();
                }
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

    public void SetWeaponMode(WeaponMode _weaponMode)
    {
        currentWeaponMode = _weaponMode;
    }

    public void SwitchWeaponMode()
    {
        if (currentWeaponMode == WeaponMode.Axe)
            currentWeaponMode = WeaponMode.Rifle;
        else
            currentWeaponMode = WeaponMode.Axe;

        WeaponModeChangedEvent?.Invoke(currentWeaponMode);

        if (ctx != null && ctx.characterStat != null)
        {
            StopCoroutine(nameof(WeaponChangeSpeedModifierRoutine));
            StartCoroutine(nameof(WeaponChangeSpeedModifierRoutine));
        }
    }

    private System.Collections.IEnumerator WeaponChangeSpeedModifierRoutine()
    {
        float _originalSpeed = ctx.characterStat.speed;
        ctx.characterStat.speed = 0.5f;
        ctx.bWhileChangingWeapon = true;

        yield return new WaitForSeconds(ctx.characterStat.weaponChangeCoolTime);

        ctx.bWhileChangingWeapon = false;
        ctx.characterStat.speed = _originalSpeed;
    }

    public void SetbAttack(bool _bAttack)
    {
        bAttack = _bAttack;
    }
}
