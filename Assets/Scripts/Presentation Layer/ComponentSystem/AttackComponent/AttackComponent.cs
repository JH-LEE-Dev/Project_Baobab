using System;
using System.Collections.Generic;
using UnityEngine;

public class AttackComponent : PComponent
{
    public event Action AttackSuccessEvent;
    public event Action<WeaponMode> WeaponModeChangedEvent;
    //외부 의존성
    private Camera mainCamera;

    //내부 의존성
    [Header("Attack Settings")]
    [SerializeField] private float maxAttackDistance = 0.15f; // 캐릭터로부터 공격 포인트가 떨어질 수 있는 최대 거리
    [SerializeField] private float attackRadius = 0.5f; // 공격 판정 반경
    [SerializeField] private LayerMask targetLayer; // 공격 대상 레이어 (도끼용)

    [Header("Aim Correction")]
    [SerializeField] private float aimCorrectionRadius = 1.0f; // 조준 보정 탐색 반경
    [SerializeField] private LayerMask aimCorrectionLayer; // 조준 보정 대상 레이어

    [SerializeField] private Transform attackPointTransform;
    private Transform characterTransform;

    //최적화를 위한 재사용 컬렉션
    private List<IStaticCollidable> collisionResults = new List<IStaticCollidable>(16);
    private List<IStaticCollidable> correctionResults = new List<IStaticCollidable>(16);

    private WeaponMode currentWeaponMode = WeaponMode.Axe;

    private bool bAttack = false;
    private Vector2 lastMouseScreenPos;
    public Vector3 mouseTransform { get; private set; }

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        characterTransform = transform.parent;
        mainCamera = Camera.main;

        // 물리 엔진용 콜라이더 비활성화
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

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

        // 2. 월드 카메라가 출력 중인 해상도 기준으로 좌표 리매핑
        float _targetWidth = (mainCamera.targetTexture != null) ? mainCamera.targetTexture.width : mainCamera.pixelWidth;
        float _targetHeight = (mainCamera.targetTexture != null) ? mainCamera.targetTexture.height : mainCamera.pixelHeight;

        Vector3 _convertedMousePos = new Vector3(
            _normalizedX * _targetWidth,
            _normalizedY * _targetHeight,
            -mainCamera.transform.position.z
        );

        // 3. 변환된 좌표를 사용하여 월드 좌표 계산
        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(_convertedMousePos);
        mouseWorldPos.z = 0;

        // 4. 조준 보정 (Aim Correction)
        if (CollisionSystem.Instance != null)
        {
            CollisionSystem.Instance.GetCollidablesInRadius(mouseWorldPos, aimCorrectionRadius, aimCorrectionLayer, correctionResults);
            if (correctionResults.Count > 0)
            {
                float minDistSqr = float.MaxValue;
                Vector2 bestPos = mouseWorldPos;
                bool found = false;

                for (int i = 0; i < correctionResults.Count; i++)
                {
                    Vector2 targetPos = correctionResults[i].Position + correctionResults[i].Offset;
                    float dSqr = (targetPos - (Vector2)mouseWorldPos).sqrMagnitude;
                    if (dSqr < minDistSqr) { minDistSqr = dSqr; bestPos = targetPos; found = true; }
                }
                if (found) mouseWorldPos = (Vector3)bestPos;
            }
        }

        mouseTransform = mouseWorldPos;

        // 5. 캐릭터에서 마우스 방향으로의 벡터 계산
        Vector3 characterPos = characterTransform.position;
        Vector3 direction = mouseWorldPos - characterPos;

        // 6. 일정 거리(Radius) 무조건 유지
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction = direction.normalized * maxAttackDistance;
        }
        else
        {
            Vector3 currentOffset = attackPointTransform.position - characterPos;
            direction = (currentOffset.sqrMagnitude > 0.0001f)
                ? currentOffset.normalized * maxAttackDistance
                : Vector3.right * maxAttackDistance;
        }

        // 7. 위치 업데이트
        attackPointTransform.position = mouseWorldPos;
    }

    public void Attack()
    {
        if (CollisionSystem.Instance == null) return;

        // 1. 공격 포인트(attackPointTransform) 기준으로 대상 탐지
        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, attackRadius, targetLayer, collisionResults);

        int hitCount = collisionResults.Count;
        if (hitCount <= 0) return;

        Vector3 characterPos = characterTransform.position;
        Vector3 attackDir = (attackPointTransform.position - characterPos).normalized;
        float cosThreshold = Mathf.Cos(45f * Mathf.Deg2Rad);

        IStaticCollidable nearestDamageable = null;
        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var target = collisionResults[i];
            Vector3 targetPos = target.Position + target.Offset;
            Vector3 targetDir = (targetPos - characterPos).normalized;

            float dot = Vector2.Dot(attackDir, targetDir);

            if (dot >= cosThreshold)
            {
                float distSqr = (targetPos - characterPos).sqrMagnitude;
                if (distSqr < minDistanceSqr)
                {
                    minDistanceSqr = distSqr;
                    nearestDamageable = target;
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

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(characterTransform.position, maxAttackDistance);

        if (attackPointTransform != null)
        {
            Vector3 characterPos = characterTransform.position;
            Vector3 attackDir = (attackPointTransform.position - characterPos).normalized;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPointTransform.position, attackRadius);

            Vector3 leftBoundary = Quaternion.Euler(0, 0, 45f) * attackDir * maxAttackDistance;
            Vector3 rightBoundary = Quaternion.Euler(0, 0, -45f) * attackDir * maxAttackDistance;

            Gizmos.DrawLine(characterPos, characterPos + leftBoundary);
            Gizmos.DrawLine(characterPos, characterPos + rightBoundary);
        }

        // 조준 보정 범위 시각화
        Gizmos.color = new Color(0, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(mouseTransform, aimCorrectionRadius);
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
        if (ctx == null || ctx.characterStat.bCanHunting == false || bAttack) return;     

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

        if (!bAttack)
        {
            UpdateAttackColliderPosition(lastMouseScreenPos);
        }
    }

    public void GoToAxeMode()
    {
        if (ctx == null || bAttack) return;
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
        if (ctx == null || ctx.characterStat.bCanHunting == false || bAttack) return;
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
