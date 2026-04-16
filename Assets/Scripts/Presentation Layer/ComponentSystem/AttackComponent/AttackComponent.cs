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
    [SerializeField] private float maxAttackDistance = 2.0f; // 캐릭터로부터 공격 포인트가 떨어질 수 있는 최대 거리
    [SerializeField] private LayerMask targetLayer; // 공격 대상 레이어 (Tree 등)

    [SerializeField] private CircleCollider2D attackRadiusCollider;
    [SerializeField] private Transform attackPointTransform;
    private Transform characterTransform;

    //최적화를 위한 재사용 컬렉션
    private Collider2D[] results = new Collider2D[10];
    private ContactFilter2D contactFilter;

    private WeaponMode currentWeaponMode = WeaponMode.Axe;

    private bool bAttack = false;
    private Vector2 lastMouseScreenPos;
    public Vector3 mouseTransform { get; private set; }

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        if (attackRadiusCollider == null)
            attackRadiusCollider = GetComponent<CircleCollider2D>();
            
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
        lastMouseScreenPos = _mouseScreenPos;

        if (bAttack || characterTransform == null)
            return;

        UpdateAttackColliderPosition(_mouseScreenPos);
    }

    private void UpdateAttackColliderPosition(Vector2 _mouseScreenPos)
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        // 1. 현재 모니터 화면 좌표를 0~1 비율(정규화)로 변환
        float _normalizedX = _mouseScreenPos.x / Screen.width;
        float _normalizedY = _mouseScreenPos.y / Screen.height;

        // 2. 월드 카메라가 출력 중인 RenderTexture(또는 실제 픽셀) 해상도 기준으로 좌표 리매핑
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

        mouseTransform = mouseWorldPos;

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
            Vector3 currentOffset = attackPointTransform.position - characterPos;
            direction = (currentOffset.sqrMagnitude > 0.0001f)
                ? currentOffset.normalized * maxAttackDistance
                : Vector3.right * maxAttackDistance;
        }

        // 6. 위치 업데이트
        attackPointTransform.position = characterPos + direction;
    }

    public void Attack()
    {
        if (attackRadiusCollider == null) return;

        // 1. 반경 내 모든 대상 탐지
        int hitCount = attackRadiusCollider.Overlap(contactFilter, results);
        if (hitCount <= 0) return;

        Vector3 characterPos = characterTransform.position;
        // 캐릭터가 조준하고 있는 방향 (공격 포인트 방향)
        Vector3 attackDir = (attackPointTransform.position - characterPos).normalized;
        // 양쪽 45도 = 총 90도 범위 (내적 결과가 cos(45도)인 약 0.707 이상)
        float cosThreshold = Mathf.Cos(45f * Mathf.Deg2Rad);

        IDamageable nearestDamageable = null;
        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Vector3 targetPos = results[i].transform.position;
            Vector3 targetDir = (targetPos - characterPos).normalized;

            // 2. 방향 판정
            float dot = Vector2.Dot(attackDir, targetDir);

            if (dot >= cosThreshold)
            {
                if (results[i].TryGetComponent(out IDamageable damageable))
                {
                    // 3. 거리 판정 (가장 가까운 대상 선택)
                    float distSqr = (targetPos - characterPos).sqrMagnitude;
                    if (distSqr < minDistanceSqr)
                    {
                        minDistanceSqr = distSqr;
                        nearestDamageable = damageable;
                    }
                }
            }
        }

        if (nearestDamageable != null)
        {
            nearestDamageable.TakeDamage(ctx.characterStat.axeDamage);
            AttackSuccessEvent?.Invoke();
        }
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }

    private void OnDrawGizmos()
    {
        if (characterTransform == null) return;

        // 1. 공격 가능 사거리 시각화 (노란색)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(characterTransform.position, maxAttackDistance);

        // 2. 공격 포인트 및 범위 시각화 (빨간색)
        if (attackPointTransform != null)
        {
            Vector3 characterPos = characterTransform.position;
            Vector3 attackDir = (attackPointTransform.position - characterPos).normalized;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPointTransform.position, 0.2f);

            // 공격 가능 범위(부채꼴) 라인 표시
            Vector3 leftBoundary = Quaternion.Euler(0, 0, 45f) * attackDir * maxAttackDistance;
            Vector3 rightBoundary = Quaternion.Euler(0, 0, -45f) * attackDir * maxAttackDistance;

            Gizmos.DrawLine(characterPos, characterPos + leftBoundary);
            Gizmos.DrawLine(characterPos, characterPos + rightBoundary);
        }
    }

    public Transform GetAttackPointTransform()
    {
        return attackPointTransform;
    }

    public void SetWeaponMode(WeaponMode _weaponMode)
    {
        currentWeaponMode = _weaponMode;
    }

    public void SwitchWeaponMode()
    {
        if (ctx != null && ctx.bWhileChangingWeapon || ctx.characterStat.bCanHunting == false) return;

        WeaponMode targetMode = (currentWeaponMode == WeaponMode.Axe) ? WeaponMode.Rifle : WeaponMode.Axe;

        currentWeaponMode = targetMode;
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

        // 공격이 끝나는 시점에 즉시 위치 업데이트
        if (!bAttack)
        {
            UpdateAttackColliderPosition(lastMouseScreenPos);
        }
    }

    public void GoToAxeMode()
    {
        if (ctx != null && ctx.bWhileChangingWeapon) return;
        if (currentWeaponMode == WeaponMode.Axe) return;

        currentWeaponMode = WeaponMode.Axe;
        WeaponModeChangedEvent?.Invoke(currentWeaponMode);

        if (ctx != null && ctx.characterStat != null)
        {
            StopCoroutine(nameof(WeaponChangeSpeedModifierRoutine));
            StartCoroutine(nameof(WeaponChangeSpeedModifierRoutine));
        }
    }

    public void GoToRifleMode()
    {
        if (ctx != null && ctx.bWhileChangingWeapon || ctx.characterStat.bCanHunting == false) return;
        if (currentWeaponMode == WeaponMode.Rifle) return;

        currentWeaponMode = WeaponMode.Rifle;
        WeaponModeChangedEvent?.Invoke(currentWeaponMode);

        if (ctx != null && ctx.characterStat != null)
        {
            StopCoroutine(nameof(WeaponChangeSpeedModifierRoutine));
            StartCoroutine(nameof(WeaponChangeSpeedModifierRoutine));
        }
    }
}
