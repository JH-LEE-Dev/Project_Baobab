using System;
using System.Collections.Generic;
using UnityEngine;

public class AttackComponent : PComponent
{
    [SerializeField] private GameObject componentCenterPoint;

    public event Action AttackSuccessEvent;
    // ShockWaveMastery로 허공에 충격파만 나갔을 때(실제 타격 없음) - 내구도만 감소시키고 콤보는 쌓지 않기 위해 별도 이벤트로 분리
    public event Action ShockWaveMissEvent;
    public event Action<WeaponMode> WeaponModeChangedEvent;
    //외부 의존성
    private Camera mainCamera;

    //내부 의존성
    [Header("Attack Settings")]
    [SerializeField] private float maxAttackDistance = 0.15f; // 캐릭터로부터 공격 포인트가 떨어질 수 있는 최대 거리
    [SerializeField] private float attackRadius = 0.5f; // 충돌 탐지 판정 반경
    [SerializeField] private float ellipseAttackRadius = 1.5f; // 타원 공격 판정 반경
    [SerializeField] private float attackAngle = 55f; // 공격 범위 반각 (중심선으로부터 좌우 각도)
    [SerializeField] private LayerMask targetLayer; // 공격 대상 레이어 (도끼용)
    [SerializeField] private float shockWaveSpawnOffset = 0.35f; // 충격파 생성 시 공격 지점으로부터의 오프셋

    // 과열 버프 - 도끼 평타로 맞은 나무에게 부여하는 지속 피해 수치
    private const float OverheatDotDamagePerTick = 10000f;
    private const int OverheatDotTickCount = 6;
    private const float OverheatDotTickInterval = 0.5f;

    [Header("Aim Correction")]
    [SerializeField] private float aimCorrectionRadius = 1.0f; // 조준 보정 탐색 반경
    [SerializeField] private LayerMask aimCorrectionLayer; // 조준 보정 대상 레이어

    [SerializeField] private Transform attackPointTransform;
    private Transform componentCenterTransform;

    //최적화를 위한 재사용 컬렉션
    private List<IStaticCollidable> collisionResults = new List<IStaticCollidable>(16);
    private List<IStaticCollidable> correctionResults = new List<IStaticCollidable>(16);
    private List<IStaticCollidable> multiAttackResults = new List<IStaticCollidable>(16);
    private List<TreeObj> currentlyDetectedTrees = new List<TreeObj>(16);
    private List<TreeObj> previouslyDetectedTrees = new List<TreeObj>(16);

    private WeaponMode currentWeaponMode = WeaponMode.Axe;

    private bool bAttack = false;
    private bool bCanRotate = true;
    private Vector2 lastMouseScreenPos;
    public Vector3 mouseTransform { get; private set; }

    private bool bCanSwap = false;

    private float detectionTimer = 0f;
    private const float detectionInterval = 0.2f;
    private List<IStaticCollidable> detectionResults = new List<IStaticCollidable>(16);

    // 이중 버퍼용 리스트
    private List<IStaticCollidable> detectionResultsA = new List<IStaticCollidable>(16);
    public IStaticCollidable nearestTarget { get; private set; }

    private AxeExtraAttackCreator axeExtraAttackCreator;

    [SerializeField] private GameObject ellipseRadiusIndicator;
    private Material ellipseIndicatorMat;
    private static readonly int EllipseRadiusID = Shader.PropertyToID("_EllipseRadius");
    private static readonly int AttackDirID = Shader.PropertyToID("_AttackDir");

    [Header("Whirlwind VFX")]
    [SerializeField] private Sprite[] whirlwindFrames; // Whirlwind 스프라이트 시트의 프레임들 (인스펙터에서 직접 연결)

    private bool bCursorEnable = false;
    private bool bCanAttack = false;
    private int successfulAttackCount = 0;

    // 넉백 등으로 공격 범위 인디케이터가 조준 방향을 따라 도는 것을 일시적으로 막아야 할 때 사용
    private bool bIndicatorRotationLocked = false;
    public void SetRotationLocked(bool _locked)
    {
        bIndicatorRotationLocked = _locked;
    }

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        if (componentCenterPoint != null) componentCenterTransform = componentCenterPoint.transform;
        mainCamera = CameraFinder.Instance.PPMainCamera;

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
        if (bCursorEnable == false || Time.timeScale == 0f)
            return;

        if (mainCamera == null)
            mainCamera = CameraFinder.Instance.PPMainCamera;

        if (mainCamera == null)
            return;

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

        Vector3 centerPos = transform.position;
        Vector3 direction = mouseWorldPos - centerPos;

        // 최소 거리 제한 (0.1 이하로 떨어지지 않도록 설정)
        float distance = direction.magnitude;
        if (distance < 0.1f)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                mouseWorldPos = centerPos + direction.normalized * 0.1f;
            }
            else
            {
                mouseWorldPos = centerPos + Vector3.right * 0.1f;
            }
            direction = mouseWorldPos - centerPos;
        }

        mouseTransform = mouseWorldPos;

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
        if (CollisionSystem.Instance == null || bCanAttack == false) return;

        Vector3 centerPos = transform.position;

        bool bShockWaveTriggered = false;
        if (ctx.characterStat.bShockWaveMastery && axeExtraAttackCreator != null)
        {
            if (UnityEngine.Random.Range(0f, 100f) < ctx.characterStat.shockWaveChance)
            {
                Vector3 direction = (mouseTransform - centerPos).normalized;
                StartCoroutine(CreateShockWaveRoutine(centerPos, direction));
                bShockWaveTriggered = true;
            }
        }

        float effectiveEllipseRadius = ellipseAttackRadius * ctx.characterStat.axeAttackRangeMultiplier;

        // 1단계: 타원 판정 범위를 모두 포함할 수 있도록 타원의 장반경(effectiveEllipseRadius)으로 1차 탐색
        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, effectiveEllipseRadius, targetLayer, collisionResults);

        int hitCount = collisionResults.Count;
        if (hitCount <= 0)
        {
            // 허공을 공격했더라도 마스터리로 충격파가 발생했다면 나무를 타격했을 때와 동일하게 도끼 내구도 감소 (콤보는 미적용)
            if (bShockWaveTriggered) ShockWaveMissEvent?.Invoke();
            return;
        }

        Vector3 attackDirVec = attackPointTransform.position - centerPos;
        Vector3 isoAttackDir = Vector3.right;
        if (attackDirVec.sqrMagnitude > 0.0001f)
        {
            isoAttackDir = new Vector3(attackDirVec.x, attackDirVec.y * 2f, 0f).normalized;
        }

        float cosThreshold = Mathf.Cos(attackAngle * Mathf.Deg2Rad);
        float radiusSq = effectiveEllipseRadius * effectiveEllipseRadius;

        bool bMultiAttack = ctx.characterStat.bMultiAttack;
        bool bIsWhirlwindStrike = ctx.characterStat.bWhirlWind && (successfulAttackCount % 3 == 2);
        multiAttackResults.Clear();

        if (bIsWhirlwindStrike)
        {
            WhirlwindVFX.Spawn(centerPos, effectiveEllipseRadius, whirlwindFrames);
        }

        IStaticCollidable nearestDamageable = null;
        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var target = collisionResults[i];
            Vector3 targetPos = target.Position + target.Offset;

            // 2단계: 타원형 반지름(ellipseAttackRadius)으로 2차 필터링
            float isoDistSq = GetIsometricDistSq(targetPos, centerPos);
            if (isoDistSq > radiusSq) continue;

            Vector3 targetOffset = targetPos - centerPos;
            Vector3 targetDir = Vector3.right;
            if (targetOffset.sqrMagnitude > 0.0001f)
            {
                targetDir = new Vector3(targetOffset.x, targetOffset.y * 2f, 0f).normalized;
            }

            float dot = Vector2.Dot(isoAttackDir, targetDir);

            bool bHitByAngle = bIsWhirlwindStrike || (dot >= cosThreshold);

            if (bHitByAngle)
            {
                if (bIsWhirlwindStrike)
                {
                    multiAttackResults.Add(target);
                }
                else if (bMultiAttack && target is TreeObj)
                {
                    multiAttackResults.Add(target);
                }
                else
                {
                    if (isoDistSq < minDistanceSqr)
                    {
                        minDistanceSqr = isoDistSq;
                        nearestDamageable = target;
                    }
                }
            }
        }

        if ((bMultiAttack || bIsWhirlwindStrike) && multiAttackResults.Count > 0)
        {
            CameraMoveController.Instance?.ShakeCamera(2f, 0.15f);
            bool bAnyHit = false;

            for (int i = 0; i < multiAttackResults.Count; i++)
            {
                var target = multiAttackResults[i];
                if (target is IDamageable damageable && damageable.bCanApplyDamage)
                {
                    ProcessAxeHit(damageable, centerPos);
                    bAnyHit = true;
                }
            }

            if (bAnyHit)
            {
                successfulAttackCount++;
                AttackSuccessEvent?.Invoke();
            }
            else if (bShockWaveTriggered)
            {
                // 허공을 공격했더라도 마스터리로 충격파가 발생했다면 나무를 타격했을 때와 동일하게 도끼 내구도 감소 (콤보는 미적용)
                ShockWaveMissEvent?.Invoke();
            }
        }
        else if (nearestDamageable != null && nearestDamageable is IDamageable damageable && damageable.bCanApplyDamage)
        {
            CameraMoveController.Instance?.ShakeCamera(2f, 0.15f);
            ProcessAxeHit(damageable, centerPos);
            successfulAttackCount++;
            AttackSuccessEvent?.Invoke();
        }
        else if (bShockWaveTriggered)
        {
            // 허공을 공격했더라도 마스터리로 충격파가 발생했다면 나무를 타격했을 때와 동일하게 도끼 내구도 감소 (콤보는 미적용)
            ShockWaveMissEvent?.Invoke();
        }
    }

    private void ProcessAxeHit(IDamageable damageable, Vector3 centerPos)
    {
        float damage = ctx.characterStat.axeDamage;
        if (damageable.health != null)
        {
            float currentHp = damageable.health.GetCurrentHealth();
            float maxHp = damageable.health.GetMaxHealth();

            if (damageable is TreeObj && maxHp > 0f && (currentHp / maxHp) <= ctx.characterStat.finalAttackHealthPercent)
            {
                damage = currentHp;
            }
            else
            {
                // N배 추가 데미지 효과는 서로 곱연산되지 않고, 원본 데미지 기준으로 각각 가산된다.
                float extraDamage = 0f;

                if (!damageable.health.bIsFirstDamage)
                {
                    extraDamage += damage * ctx.characterStat.helloDamageMul;
                }

                if (maxHp > 0f && (currentHp / maxHp) <= 0.5f)
                {
                    extraDamage += damage * ctx.characterStat.weakPointDamageMul;
                }

                damage += extraDamage;
            }
        }

        if (UnityEngine.Random.value < ctx.characterStat.criticalChance)
        {
            damage *= ctx.characterStat.ciriticalDamageMul;
        }

        damageable.TakeDamage(damage);

        // 과열 버프 활성 중 도끼 평타로 나무를 맞히면 지속 피해 부여 (충격파/드론/부메랑 제외, 도끼 평타만)
        if (ctx.overheatComponent != null && ctx.overheatComponent.IsActive && damageable is TreeObj overheatTarget)
        {
            overheatTarget.ApplyOverheatDot(OverheatDotDamagePerTick, OverheatDotTickCount, OverheatDotTickInterval);
        }

        // 나무 타격 시 (마스터리가 없을 때만) 확률적으로 충격파 생성
        if (!ctx.characterStat.bShockWaveMastery && damageable is TreeObj && axeExtraAttackCreator != null)
        {
            if (UnityEngine.Random.Range(0f, 100f) < ctx.characterStat.shockWaveChance)
            {
                Vector3 direction = (mouseTransform - centerPos).normalized;
                StartCoroutine(CreateShockWaveRoutine(centerPos, direction));
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
                sw.SetDirection(_direction);
                axeExtraAttackCreator.PlayShockWaveVisual(sw);
            }
        }
    }

    private void UpdateIndicator()
    {
        if (bIndicatorRotationLocked) return;
        if (ellipseRadiusIndicator == null || ellipseIndicatorMat == null) return;

        // 도끼 모드일 때 항상 표시
        bool bShow = (currentWeaponMode == WeaponMode.Axe);

        if (bShow)
        {
            Vector3 centerPos = transform.position;

            Vector3 attackDirVec = attackPointTransform.position - centerPos;
            Vector3 isoAttackDir = Vector3.right;
            if (attackDirVec.sqrMagnitude > 0.0001f)
            {
                isoAttackDir = new Vector3(attackDirVec.x, attackDirVec.y * 2f, 0f).normalized;
            }

            float effectiveEllipseRadius = ellipseAttackRadius * ctx.characterStat.axeAttackRangeMultiplier;

            // 셰이더 프로퍼티 업데이트
            ellipseIndicatorMat.SetFloat(EllipseRadiusID, effectiveEllipseRadius);
            ellipseIndicatorMat.SetVector(AttackDirID, (Vector2)isoAttackDir);

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
        }
    }

    private void DetectNearestTarget()
    {
        if (CollisionSystem.Instance == null) return;
        if (currentWeaponMode != WeaponMode.Axe)
        {
            nearestTarget = null;
            ClearDetectedTreeOutlines();
            return;
        }

        float effectiveEllipseRadius = ellipseAttackRadius * ctx.characterStat.axeAttackRangeMultiplier;

        // 1단계: 타원 판정 범위를 모두 포함할 수 있도록 타원의 장반경(effectiveEllipseRadius)으로 1차 탐색
        CollisionSystem.Instance.GetCollidablesInRadius(transform.position, effectiveEllipseRadius, targetLayer, detectionResults);

        int hitCount = detectionResults.Count;
        if (hitCount <= 0)
        {
            nearestTarget = null;
            ClearDetectedTreeOutlines();
            return;
        }

        Vector3 centerPos = transform.position;

        Vector3 attackDirVec = attackPointTransform.position - centerPos;
        Vector3 isoAttackDir = Vector3.right;
        if (attackDirVec.sqrMagnitude > 0.0001f)
        {
            isoAttackDir = new Vector3(attackDirVec.x, attackDirVec.y * 2f, 0f).normalized;
        }

        float cosThreshold = Mathf.Cos(attackAngle * Mathf.Deg2Rad);
        float radiusSq = effectiveEllipseRadius * effectiveEllipseRadius;

        bool bMultiAttack = ctx.characterStat.bMultiAttack;
        currentlyDetectedTrees.Clear();

        IStaticCollidable nearest = null;
        float minDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            var target = detectionResults[i];
            Vector3 targetPos = target.Position + target.Offset;

            // 2단계: 타원형 반지름(ellipseAttackRadius)으로 2차 필터링
            float isoDistSq = GetIsometricDistSq(targetPos, centerPos);

            if (isoDistSq > radiusSq) continue;

            Vector3 targetOffset = targetPos - centerPos;
            Vector3 targetDir = Vector3.right;
            if (targetOffset.sqrMagnitude > 0.0001f)
            {
                targetDir = new Vector3(targetOffset.x, targetOffset.y * 2f, 0f).normalized;
            }

            float dot = Vector2.Dot(isoAttackDir, targetDir);

            if (dot >= cosThreshold)
            {
                if (bMultiAttack && target is TreeObj tree)
                {
                    currentlyDetectedTrees.Add(tree);
                }
                else
                {
                    if (isoDistSq < minDistanceSqr)
                    {
                        minDistanceSqr = isoDistSq;
                        nearest = target;
                    }
                }
            }
        }

        nearestTarget = nearest;

        // 광역 공격 모드가 아니고 가장 가까운 대상이 나무라면 감지 목록에 추가
        if (!bMultiAttack && nearest is TreeObj nearestTree)
        {
            currentlyDetectedTrees.Add(nearestTree);
        }

        // 기존 감지 대상 중 제외된 대상들의 아웃라인 끄기
        for (int i = 0; i < previouslyDetectedTrees.Count; i++)
        {
            var oldTree = previouslyDetectedTrees[i];
            if (!currentlyDetectedTrees.Contains(oldTree))
            {
                oldTree.SetOutline(false);
            }
        }

        // 새로 감지된 대상들의 아웃라인 켜기
        for (int i = 0; i < currentlyDetectedTrees.Count; i++)
        {
            var newTree = currentlyDetectedTrees[i];
            if (!previouslyDetectedTrees.Contains(newTree))
            {
                newTree.SetOutline(true);
            }
        }

        // 리스트 스왑 (GC 할당 방지)
        var temp = previouslyDetectedTrees;
        previouslyDetectedTrees = currentlyDetectedTrees;
        currentlyDetectedTrees = temp;
        currentlyDetectedTrees.Clear();
    }

    private void ClearDetectedTreeOutlines()
    {
        for (int i = 0; i < previouslyDetectedTrees.Count; i++)
        {
            previouslyDetectedTrees[i].SetOutline(false);
        }
        previouslyDetectedTrees.Clear();
        currentlyDetectedTrees.Clear();
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
            Vector3 attackDirVec = attackPointTransform.position - centerPos;
            Vector3 isoAttackDir = Vector3.right;
            if (attackDirVec.sqrMagnitude > 0.0001f)
            {
                isoAttackDir = new Vector3(attackDirVec.x, attackDirVec.y * 2f, 0f).normalized;
            }

            // 1차 탐색 범위 (노란색 원 - 디버그용으로 연하게 표시 가능)
            Gizmos.color = new Color(1, 1, 0, 0.3f);
            Gizmos.DrawWireSphere(centerPos, effectiveSearchRadius);

            // 2차 타원 판정 범위 (빨간색 타원)
            Gizmos.color = Color.red;
            DrawWireEllipse(centerPos, effectiveEllipseRadius, effectiveEllipseRadius * 0.5f);

            // 설정된 각도 경계선 시각화 (아이소매트릭 비율 적용)
            Vector3 leftDir = Quaternion.Euler(0, 0, attackAngle) * isoAttackDir;
            Vector3 rightDir = Quaternion.Euler(0, 0, -attackAngle) * isoAttackDir;

            Vector3 leftBoundary = new Vector3(leftDir.x * effectiveEllipseRadius, leftDir.y * 0.5f * effectiveEllipseRadius, 0);
            Vector3 rightBoundary = new Vector3(rightDir.x * effectiveEllipseRadius, rightDir.y * 0.5f * effectiveEllipseRadius, 0);

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

        ApplyWeaponChangeSpeedModifier();
    }

    private System.Collections.IEnumerator WeaponChangeSpeedModifierRoutine()
    {
        if (!ctx.bWhileChangingWeapon)
        {
            ctx.characterStat.AddActionState();
            ctx.bWhileChangingWeapon = true;
        }

        yield return new WaitForSeconds(ctx.characterStat.weaponChangeCoolTime);

        ctx.bWhileChangingWeapon = false;
        ctx.characterStat.RemoveActionState();
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

        ApplyWeaponChangeSpeedModifier();
    }

    public void GoToRifleMode()
    {
        if (ctx == null || ctx.characterStat.bCanHunting == false || bCanRotate == false || bCanSwap == false) return;
        if (currentWeaponMode == WeaponMode.Rifle) return;

        currentWeaponMode = WeaponMode.Rifle;
        WeaponModeChangedEvent?.Invoke(currentWeaponMode);

        ApplyWeaponChangeSpeedModifier();
    }

    private void ApplyWeaponChangeSpeedModifier()
    {
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

        ClearDetectedTreeOutlines();
        nearestTarget = null;
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

    public void SetCursorEnable(bool _boolean)
    {
        bCursorEnable = _boolean;
    }

    public void SetbCanAttack(bool _boolean)
    {
        bCanAttack = _boolean;
    }

    public void ResetAttackTransform()
    {
        if (componentCenterTransform != null && attackPointTransform != null)
        {
            Vector3 targetPos = componentCenterTransform.position;
            targetPos.y -= 2f;
            attackPointTransform.position = targetPos;
        }
    }
}
