using System;
using System.Collections.Generic;
using UnityEngine;

public class AttackComponent : PComponent
{
    [SerializeField] private GameObject componentCenterPoint;

    public event Action AttackSuccessEvent;
    public event Action<WeaponMode> WeaponModeChangedEvent;
    //외부 의존성
    private Camera mainCamera;

    //내부 의존성
    [Header("Attack Settings")]
    [SerializeField] private float maxAttackDistance = 0.15f; // 캐릭터로부터 공격 포인트가 떨어질 수 있는 최대 거리
    [SerializeField] private float attackRadius = 0.5f; // 충돌 탐지 판정 반경
    [SerializeField] private float ellipseAttackRadius = 1.5f; // 타원 공격 판정 반경
    [SerializeField] private LayerMask targetLayer; // 공격 대상 레이어 (도끼용)
    [SerializeField] private float shockWaveSpawnOffset = 0.2f; // 충격파 생성 시 공격 지점으로부터의 오프셋

    [Header("Aim Correction")]
    [SerializeField] private float aimCorrectionRadius = 1.0f; // 조준 보정 탐색 반경
    [SerializeField] private LayerMask aimCorrectionLayer; // 조준 보정 대상 레이어

    [SerializeField] private Transform attackPointTransform;
    private Transform componentCenterTransform;

    //최적화를 위한 재사용 컬렉션
    private List<IStaticCollidable> collisionResults = new List<IStaticCollidable>(16);
    private List<IStaticCollidable> correctionResults = new List<IStaticCollidable>(16);

    private WeaponMode currentWeaponMode = WeaponMode.Axe;

    private bool bAttack = false;
    private bool bCanRotate = true;
    private Vector2 lastMouseScreenPos;
    public Vector3 mouseTransform { get; private set; }

    private float originalSpeed; // 무기 교체 전 원래 속도 캐싱용

    private bool bCanSwap = false;

    private float detectionTimer = 0f;
    private const float detectionInterval = 0.2f;
    private List<IStaticCollidable> detectionResults = new List<IStaticCollidable>(16);
    public IStaticCollidable nearestTarget { get; private set; }
    private IStaticCollidable lastNearestTarget;

    private AxeExtraAttackCreator axeExtraAttackCreator;

    [SerializeField] private GameObject ellipseRadiusIndicator;
    private Material ellipseIndicatorMat;
    private static readonly int EllipseRadiusID = Shader.PropertyToID("_EllipseRadius");
    private static readonly int AttackDirID = Shader.PropertyToID("_AttackDir");

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        if (componentCenterPoint != null) componentCenterTransform = componentCenterPoint.transform;
        mainCamera = Camera.main;

        axeExtraAttackCreator = GetComponent<AxeExtraAttackCreator>();
        if (axeExtraAttackCreator != null) axeExtraAttackCreator.Initialize(ctx);

        if (ellipseRadiusIndicator != null)
        {
            var renderer = ellipseRadiusIndicator.GetComponent<Renderer>();
            if (renderer != null) ellipseIndicatorMat = renderer.material;
        }

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
        lastMouseScreenPos = _mouseScreenPos;

        if (/*bAttack ||*/ componentCenterTransform == null)
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

        // 5. 중심점에서 마우스 방향으로의 벡터 계산
        Vector3 centerPos = transform.position;
        Vector3 direction = mouseWorldPos - centerPos;

        // 6. 일정 거리(Radius) 무조건 유지
        if (direction.sqrMagnitude > 0.0001f)
        {
            direction = direction.normalized * maxAttackDistance;
        }
        else
        {
            Vector3 currentOffset = attackPointTransform.position - centerPos;
            direction = (currentOffset.sqrMagnitude > 0.0001f)
                ? currentOffset.normalized * maxAttackDistance
                : Vector3.right * maxAttackDistance;
        }

        // 7. 위치 업데이트 (계산된 오프셋 적용)
        attackPointTransform.position = mouseWorldPos;
    }

    public void Attack()
    {
        if (CollisionSystem.Instance == null) return;

        float effectiveEllipseRadius = ellipseAttackRadius * ctx.characterStat.axeAttackRangeMultiplier;

        // 1단계: 타원 판정 범위를 모두 포함할 수 있도록 타원의 장반경(effectiveEllipseRadius)으로 1차 탐색
        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, effectiveEllipseRadius, targetLayer, collisionResults);

        int hitCount = collisionResults.Count;
        if (hitCount <= 0) return;

        Vector3 centerPos = transform.position;
        Vector3 attackDir = (attackPointTransform.position - centerPos).normalized;
        float cosThreshold = Mathf.Cos(45f * Mathf.Deg2Rad);
        float radiusSq = effectiveEllipseRadius * effectiveEllipseRadius;

        IStaticCollidable nearestDamageable = null;
        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var target = collisionResults[i];
            Vector3 targetPos = target.Position + target.Offset;

            // 2단계: 타원형 반지름(ellipseAttackRadius)으로 2차 필터링
            float isoDistSq = GetIsometricDistSq(targetPos, centerPos);
            if (isoDistSq > radiusSq) continue;

            Vector3 targetDir = (targetPos - centerPos).normalized;
            float dot = Vector2.Dot(attackDir, targetDir);

            if (dot >= cosThreshold)
            {
                if (isoDistSq < minDistanceSqr)
                {
                    minDistanceSqr = isoDistSq;
                    nearestDamageable = target;
                }
            }
        }

        if (nearestDamageable != null && nearestDamageable.bCanApplyDamage)
        {
            nearestDamageable.TakeDamage(ctx.characterStat.axeDamage);
            AttackSuccessEvent?.Invoke();

            // 나무 타격 시 확률적으로 충격파 생성
            if (nearestDamageable is TreeObj && axeExtraAttackCreator != null)
            {
                if (UnityEngine.Random.Range(0f, 100f) < ctx.characterStat.shockWaveChance)
                {
                    Vector3 hitPos = nearestDamageable.Position + nearestDamageable.Offset;
                    Vector3 direction = (hitPos - centerPos).normalized;
                    StartCoroutine(CreateShockWaveRoutine(hitPos, direction));
                }
            }
        }
    }

    private System.Collections.IEnumerator CreateShockWaveRoutine(Vector3 _position, Vector3 _direction)
    {
        yield return new WaitForSeconds(ctx.characterStat.shockWaveCreateDelay);

        if (axeExtraAttackCreator != null)
        {
            Vector3 spawnPos = _position + (_direction * shockWaveSpawnOffset);
            ShockWave sw = axeExtraAttackCreator.CreateShockWave(spawnPos);
            if (sw != null)
            {
                sw.transform.right = _direction;
            }
        }
    }

    private void UpdateIndicator()
    {
        if (ellipseRadiusIndicator == null || ellipseIndicatorMat == null) return;

        // 도끼 모드일 때 항상 표시
        bool bShow = (currentWeaponMode == WeaponMode.Axe);

        if (bShow)
        {
            Vector3 centerPos = transform.position;
            Vector3 attackDir = (attackPointTransform.position - centerPos).normalized;
            float effectiveEllipseRadius = ellipseAttackRadius * ctx.characterStat.axeAttackRangeMultiplier;
            
            // 셰이더 프로퍼티 업데이트
            ellipseIndicatorMat.SetFloat(EllipseRadiusID, effectiveEllipseRadius);
            ellipseIndicatorMat.SetVector(AttackDirID, (Vector2)attackDir);
            
            // 인디케이터 위치 및 스케일 업데이트
            ellipseRadiusIndicator.transform.position = centerPos;
            ellipseRadiusIndicator.transform.localScale = new Vector3((effectiveEllipseRadius + 0.5f) * 2f, effectiveEllipseRadius + 0.5f, 1f);
        }
    }

    private void Update()
    {
        UpdateIndicator();

        detectionTimer += Time.deltaTime;
        if (detectionTimer >= detectionInterval)
        {
            detectionTimer = 0f;
            DetectNearestTarget();

            if (lastNearestTarget != nearestTarget)
            {
                if (lastNearestTarget is TreeObj oldTree)
                {
                    oldTree.SetOutline(false);
                }

                if (nearestTarget is TreeObj newTree)
                {
                    newTree.SetOutline(true);
                }

                lastNearestTarget = nearestTarget;
            }
        }
    }

    private void DetectNearestTarget()
    {
        if (CollisionSystem.Instance == null) return;
        if (currentWeaponMode != WeaponMode.Axe)
        {
            nearestTarget = null;
            return;
        }

        float effectiveEllipseRadius = ellipseAttackRadius * ctx.characterStat.axeAttackRangeMultiplier;

        // 1단계: 타원 판정 범위를 모두 포함할 수 있도록 타원의 장반경(effectiveEllipseRadius)으로 1차 탐색
        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, effectiveEllipseRadius, targetLayer, detectionResults);

        int hitCount = detectionResults.Count;
        if (hitCount <= 0)
        {
            nearestTarget = null;
            return;
        }

        Vector3 centerPos = transform.position;
        Vector3 attackDir = (attackPointTransform.position - centerPos).normalized;
        float cosThreshold = Mathf.Cos(45f * Mathf.Deg2Rad);
        float radiusSq = effectiveEllipseRadius * effectiveEllipseRadius;

        IStaticCollidable nearest = null;
        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var target = detectionResults[i];
            Vector3 targetPos = target.Position + target.Offset;

            // 2단계: 타원형 반지름(ellipseAttackRadius)으로 2차 필터링
            float isoDistSq = GetIsometricDistSq(targetPos, centerPos);
    
            if (isoDistSq > radiusSq) continue;

            Vector3 targetDir = (targetPos - centerPos).normalized;
            float dot = Vector2.Dot(attackDir, targetDir);

            if (dot >= cosThreshold)
            {
                if (isoDistSq < minDistanceSqr)
                {
                    minDistanceSqr = isoDistSq;
                    nearest = target;
                }
            }
        }

        nearestTarget = nearest;
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }

    private void OnDrawGizmos()
    {
        Vector3 centerPos = transform.position;
        float effectiveSearchRadius = attackRadius * (ctx != null ? ctx.characterStat.axeAttackRangeMultiplier : 1f);
        float effectiveEllipseRadius = ellipseAttackRadius * (ctx != null ? ctx.characterStat.axeAttackRangeMultiplier : 1f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(centerPos, maxAttackDistance);

        if (attackPointTransform != null)
        {
            Vector3 attackDir = (attackPointTransform.position - centerPos).normalized;

            // 1차 탐색 범위 (노란색 원 - 디버그용으로 연하게 표시 가능)
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(centerPos, effectiveSearchRadius);

            // 2차 타원 판정 범위 (빨간색 타원)
            Gizmos.color = Color.red;
            DrawWireEllipse(centerPos, effectiveEllipseRadius, effectiveEllipseRadius * 0.5f);

            // 45도 경계선 시각화 (아이소매트릭 비율 적용)
            Vector3 leftDir = Quaternion.Euler(0, 0, 45f) * attackDir;
            Vector3 rightDir = Quaternion.Euler(0, 0, -45f) * attackDir;

            Vector3 leftBoundary = new Vector3(leftDir.x * effectiveEllipseRadius, leftDir.y * effectiveEllipseRadius * 0.5f, 0);
            Vector3 rightBoundary = new Vector3(rightDir.x * effectiveEllipseRadius, rightDir.y * effectiveEllipseRadius * 0.5f, 0);

            Gizmos.DrawLine(centerPos, centerPos + leftBoundary);
            Gizmos.DrawLine(centerPos, centerPos + rightBoundary);
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
        if (ctx == null || ctx.characterStat.bCanHunting == false || bCanRotate == false || bCanSwap == false) return;

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
        // 무기 교체 중이 아닐 때만 원래 속도를 저장합니다.
        // 이미 교체 중이라면 originalSpeed에 진짜 원래 속도가 저장되어 있습니다.
        if (!ctx.bWhileChangingWeapon)
        {
            originalSpeed = ctx.characterStat.originalSpeed;
        }

        ctx.characterStat.speed = originalSpeed * ctx.characterStat.speedDecreaseWhileAction;
        ctx.bWhileChangingWeapon = true;

        yield return new WaitForSeconds(ctx.characterStat.weaponChangeCoolTime);

        ctx.bWhileChangingWeapon = false;
        ctx.characterStat.speed = originalSpeed;
    }

    public void SetbAttack(bool _bAttack)
    {
        bAttack = _bAttack;

        if (!bAttack)
        {
            UpdateAttackColliderPosition(lastMouseScreenPos);
        }
    }

    public void SetbCanRotate(bool _bCanRotate)
    {
        bCanRotate = _bCanRotate;
    }

    public void GoToAxeMode()
    {
        if (ctx == null || bCanRotate == false || bCanSwap == false) return;
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
        if (ctx == null || ctx.characterStat.bCanHunting == false || bCanRotate == false || bCanSwap == false) return;
        if (currentWeaponMode == WeaponMode.Rifle) return;

        currentWeaponMode = WeaponMode.Rifle;
        WeaponModeChangedEvent?.Invoke(currentWeaponMode);

        if (ctx != null && ctx.characterStat != null)
        {
            StopCoroutine(nameof(WeaponChangeSpeedModifierRoutine));
            StartCoroutine(nameof(WeaponChangeSpeedModifierRoutine));
        }
    }

    public void SetbCanSwap(bool _boolean)
    {
        bCanSwap = _boolean;
    }

    public void ResetAttackComponent()
    {
        currentWeaponMode = WeaponMode.Axe;

        SetbCanSwap(false);
        SetbAttack(false);

        if (lastNearestTarget is TreeObj tree)
        {
            tree.SetOutline(false);
            lastNearestTarget = null;
            nearestTarget = null;
        }
    }

    public void Refresh()
    {

    }

    private float GetIsometricDistSq(Vector3 _p1, Vector3 _p2)
    {
        float _dx = _p1.x - _p2.x;
        float _dy = (_p1.y - _p2.y) * 2f;
        return _dx * _dx + _dy * _dy;
    }

    private void DrawWireEllipse(Vector3 _center, float _radiusX, float _radiusY)
    {
        int _segments = 32;
        float _angle = 0f;
        Vector3 _lastPoint = _center + new Vector3(Mathf.Cos(0) * _radiusX, Mathf.Sin(0) * _radiusY, 0);
        for (int i = 1; i <= _segments; i++)
        {
            _angle = i * 2 * Mathf.PI / _segments;
            Vector3 _nextPoint = _center + new Vector3(Mathf.Cos(_angle) * _radiusX, Mathf.Sin(_angle) * _radiusY, 0);
            Gizmos.DrawLine(_lastPoint, _nextPoint);
            _lastPoint = _nextPoint;
        }
    }

    public void SetEnable(bool _boolean)
    {
        ellipseRadiusIndicator.gameObject.SetActive(_boolean);
    }
}
