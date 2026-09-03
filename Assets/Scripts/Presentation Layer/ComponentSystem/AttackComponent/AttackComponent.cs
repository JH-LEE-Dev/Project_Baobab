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
    // 공격 범위 안에 나무가 하나도 없다가 처음 감지되었을 때/감지되어 있다가 전부 사라졌을 때만 발생(매 프레임, 감지 대상 교체 시엔 발생하지 않음)
    public event Action TreeDetectedEvent;
    public event Action TreeDetectionClearedEvent;
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

    [SerializeField] private Transform attackPointTransform;
    private Transform componentCenterTransform;

    //최적화를 위한 재사용 컬렉션
    private List<IStaticCollidable> collisionResults = new List<IStaticCollidable>(16);
    private List<IStaticCollidable> multiAttackResults = new List<IStaticCollidable>(16);
    private List<TreeObj> currentlyDetectedTrees = new List<TreeObj>(16);
    private List<TreeObj> previouslyDetectedTrees = new List<TreeObj>(16);

    private WeaponMode currentWeaponMode = WeaponMode.Axe;

    [Header("Gamepad Aim")]
    [SerializeField, Tooltip("패드 조준 시 캐릭터로부터 조준점을 띄울 거리. 게임플레이에는 영향이 없고 " +
        "기즈모·디버깅에서만 보인다. 조준점을 읽는 코드가 전부 '방향'만 쓰기 때문이다.")]
    private float gamepadAimRadius = 1.25f;

    // 스틱 중립 근처의 노이즈를 조준으로 치지 않기 위한 하한. 장치 전환 문턱값(0.5)보다 낮은데,
    // 여기서는 "패드를 쓰는 중"이 이미 확정된 상태라 더 미세한 조준까지 받아야 하기 때문이다.
    private const float GamepadAimDeadzoneSqr = 0.04f; // 0.2^2

    // 조준 스틱에서 손을 뗀 뒤 이 시간이 지나면 조준 오버라이드를 풀고 기본 동작(이동방향 조준)으로 돌아간다.
    // 실시간 기준(unscaled)인 이유는 슬로우모션·히트스톱 때문에 체감 대기 시간이 늘어나면 안 되기 때문이다.
    private const float GamepadAimHoldDuration = 1f;

    private Vector2 aimStickDirection = Vector2.zero;

    // 마지막으로 조준 스틱이 유효하게 기울어져 있던 시각(unscaled). 만료 판정에만 쓴다.
    private float lastAimStickInputTime = float.NegativeInfinity;

    private bool bAttack = false;
    private bool bCanRotate = true;
    private Vector2 lastMouseScreenPos;
    public Vector3 mouseTransform { get; private set; }

    private bool bCanSwap = false;

    private float detectionTimer = 0f;
    private const float detectionInterval = 0.2f;
    private List<IStaticCollidable> detectionResults = new List<IStaticCollidable>(16);
    private bool bTreesDetected = false;

    // 이중 버퍼용 리스트
    private List<IStaticCollidable> detectionResultsA = new List<IStaticCollidable>(16);
    public IStaticCollidable nearestTarget { get; private set; }

    private AxeExtraAttackCreator axeExtraAttackCreator;

    [SerializeField] private GameObject ellipseRadiusIndicator;
    private Material ellipseIndicatorMat;
    private static readonly int EllipseRadiusID = Shader.PropertyToID("_EllipseRadius");
    private static readonly int AttackDirID = Shader.PropertyToID("_AttackDir");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

    private const float IndicatorFadeInDuration = 1f; // 인디케이터 활성화 시 알파가 0에서 복원되는 데 걸리는 시간
    private Coroutine indicatorFadeCoroutine;
    private float indicatorFullAlpha = 1f; // Initialize에서 1회만 캐싱되는 인디케이터의 원래 알파값(페이드인의 목표값)

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
            if (renderer != null)
            {
                ellipseIndicatorMat = renderer.material;
                // 이후 페이드인의 목표값으로 쓸 "원래" 알파값. 페이드 도중 SetEnable(false)로 끊기면
                // 머티리얼에 중간값(예: 0.4)이 남는데, 이걸 그대로 목표로 삼으면 재입장할 때마다
                // 인디케이터가 갈수록 흐려지므로 반드시 최초 1회 값을 고정해 둬야 한다.
                indicatorFullAlpha = ellipseIndicatorMat.GetColor(BaseColorID).a;
            }
        }

        BindEvents();
    }

    private void BindEvents()
    {
        if (ctx == null || ctx.inputManager == null)
            return;

        ctx.inputManager.inputReader.MouseMoveEvent -= MouseMove;
        ctx.inputManager.inputReader.MouseMoveEvent += MouseMove;

        ctx.inputManager.inputReader.AimEvent -= AimStickMoved;
        ctx.inputManager.inputReader.AimEvent += AimStickMoved;
    }

    private void ReleaseEvents()
    {
        if (ctx == null || ctx.inputManager == null)
            return;

        ctx.inputManager.inputReader.MouseMoveEvent -= MouseMove;
        ctx.inputManager.inputReader.AimEvent -= AimStickMoved;
    }

    /// <summary>
    /// 패드 조준 스틱 입력입니다. 방향만 기억하고, 실제 적용은 매 프레임 UpdateGamepadAim에서 합니다.
    /// </summary>
    private void AimStickMoved(Vector2 _stick)
    {
        AccumulateAimStickInput(_stick);
    }

    /// <summary>
    /// 조준 스틱 값을 받아 "조준 오버라이드" 상태를 갱신합니다. 이벤트(AimStickMoved)와
    /// 매 프레임 폴링(UpdateGamepadAim) 양쪽에서 같은 판정을 쓰기 위한 공용 진입점입니다.
    ///
    /// 스틱을 중립으로 놓아도 방향을 즉시 지우지 않는 것이 중요합니다. 지우면 손을 떼는 순간
    /// 캐릭터가 조준을 잃고 엉뚱한 방향으로 홱 돌아가 버립니다. 대신 마지막으로 겨눈 방향을
    /// 유지해 두고, GamepadAimHoldDuration이 지난 뒤에 UpdateGamepadAim에서 풀어 줍니다.
    /// </summary>
    private void AccumulateAimStickInput(Vector2 _stick)
    {
        if (_stick.sqrMagnitude < GamepadAimDeadzoneSqr)
            return;

        aimStickDirection = _stick.normalized;
        lastAimStickInputTime = Time.unscaledTime;
    }

    /// <summary>
    /// 패드 조준을 매 프레임 다시 계산합니다.
    ///
    /// 매 프레임이어야 하는 이유가 마우스와 다릅니다. 마우스 조준점은 월드에 고정이지만,
    /// 패드 조준점은 "캐릭터로부터 일정 거리"라는 상대 좌표라서 캐릭터가 움직이면 같이 따라와야
    /// 합니다. 게다가 스틱을 기울인 채 가만히 있으면 입력 이벤트가 오지 않으므로,
    /// 이벤트에만 의존하면 조준점이 뒤처집니다.
    /// </summary>
    private void UpdateGamepadAim()
    {
        if (null == ctx || null == ctx.inputManager) return;

        // 마우스를 쓰는 동안에는 패드 조준이 끼어들지 않아야 한다.
        if (EInputDeviceType.Gamepad != ctx.inputManager.CurrentDevice) return;

        // 스틱을 기울인 채 가만히 있으면 입력 이벤트가 오지 않으므로, 지금 기울어져 있는지는
        // 직접 읽어서 확인한다. 이게 없으면 "조준을 유지하는 중"이 입력 없음으로 오해되어
        // 1초 뒤에 조준이 이동방향으로 풀려 버린다.
        AccumulateAimStickInput(ctx.inputManager.inputReader.ReadAimStick());

        // 조준 스틱 입력이 끊긴 지 일정 시간이 지나면 오버라이드를 풀고 기본 패드 조작으로 되돌린다.
        if (Vector2.zero != aimStickDirection &&
            GamepadAimHoldDuration <= Time.unscaledTime - lastAimStickInputTime)
        {
            aimStickDirection = Vector2.zero;
        }

        Vector2 targetAimDir = aimStickDirection;

        // 우측 조준 스틱을 쓰지 않는 상태이고 이동 입력이 있으면, 이동 방향을 조준 방향의 기본값으로 쓴다.
        if (Vector2.zero == targetAimDir && Vector2.zero != ctx.moveInput)
        {
            targetAimDir = ctx.moveInput.normalized;
        }

        if (Vector2.zero == targetAimDir) return;

        Vector3 _aimWorldPos = transform.position + (Vector3)(targetAimDir * gamepadAimRadius);
        ApplyAimWorldPosition(_aimWorldPos);
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

        // 마우스 화면 좌표를 그대로 넘긴다. ScreenToWorldPoint가 카메라의 pixelRect를 이미
        // 반영하므로, 화면 크기로 정규화한 뒤 pixelWidth를 다시 곱하는 리매핑을 하면 안 된다.
        //
        // 예전에는 그 리매핑을 했는데, 카메라가 화면 일부만 그리는 경우(UltraWideCropApplier가
        // Pillarbox를 켠 상태)에 pixelRect의 시작점을 두 번 빼게 되어 조준이 어긋났다.
        // 3440x1440이면 화면 오른쪽 끝을 가리켜도 시야의 83% 지점으로 계산된다.
        //
        // 크롭이 없는 해상도(Screen.width == pixelWidth)에서는 예전 계산과 결과가 같다.
        Vector3 _convertedMousePos = new Vector3(
            _mouseScreenPos.x,
            _mouseScreenPos.y,
            -mainCamera.transform.position.z
        );

        Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(_convertedMousePos);
        mouseWorldPos.z = 0;

        ApplyAimWorldPosition(mouseWorldPos);
    }

    /// <summary>
    /// 조준 지점을 캐릭터 기준의 방향으로 바꿔 실제로 적용합니다.
    /// 마우스(화면 좌표 → 월드 좌표)와 패드(스틱 방향 → 월드 좌표)가 이 지점에서 합류합니다.
    ///
    /// 조준 보정(에임 어시스트)은 쓰지 않기로 확정되어 제거했습니다. 마우스든 패드든
    /// 유저가 가리킨 방향을 그대로 따릅니다.
    /// </summary>
    private void ApplyAimWorldPosition(Vector3 _aimWorldPos)
    {
        // 각 진입점이 스스로를 지키도록 여기서도 검사한다. (패드 경로는 카메라를 거치지 않고 바로 들어온다)
        if (bCursorEnable == false || Time.timeScale == 0f)
            return;

        Vector3 mouseWorldPos = _aimWorldPos;
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
        }

        mouseTransform = mouseWorldPos;

        // 6. 위치 업데이트
        //
        // 예전에는 여기에 "캐릭터로부터 maxAttackDistance만큼 떨어진 지점으로 고정"하는 계산이
        // 있었지만, 그 결과가 담긴 지역 변수를 한 번도 쓰지 않고 아래 줄이 mouseWorldPos를 그대로
        // 대입해 버려서 실제로는 아무 효과가 없었다. 혼동을 없애려고 그 계산을 걷어냈다.
        //
        // 지워도 안전한 이유: attackPointTransform의 위치를 읽는 살아있는 코드(Attack,
        // UpdateIndicator, DetectNearestTarget, Character.UpdateFacingByAttackPoint,
        // Character.UpdateDroneFormation, ArmComponent.UpdateRotation/UpdatePositionOffset,
        // AxeComponent.SetFacingDir)는 전부 centerPos로부터의 "방향"만 정규화해서 쓰고
        // 거리는 읽지 않는다. 즉 그 계산을 되살리든 말든 게임 동작은 동일하다.
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
        UpdateGamepadAim();
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

        SetTreesDetected(previouslyDetectedTrees.Count > 0);
    }

    private void ClearDetectedTreeOutlines()
    {
        for (int i = 0; i < previouslyDetectedTrees.Count; i++)
        {
            previouslyDetectedTrees[i].SetOutline(false);
        }
        previouslyDetectedTrees.Clear();
        currentlyDetectedTrees.Clear();

        SetTreesDetected(false);
    }

    // 감지 상태가 실제로 바뀔 때(없음→있음, 있음→없음)만 이벤트를 발생시킨다. 감지된 나무가
    // 다른 나무로 바뀌거나 매 탐지 주기마다 호출되는 것은 상태 전환이 아니므로 무시한다.
    private void SetTreesDetected(bool _detected)
    {
        if (bTreesDetected == _detected)
            return;

        bTreesDetected = _detected;

        if (_detected)
            TreeDetectedEvent?.Invoke();
        else
            TreeDetectionClearedEvent?.Invoke();
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

        if (false == bAttack)
        {
            // 키보드/마우스 모드일 때만 마우스 위치로 조준점을 갱신하고, 패드 모드일 때는 마우스 좌표로 덮어쓰지 않는다.
            if (null != ctx && null != ctx.inputManager && EInputDeviceType.Gamepad != ctx.inputManager.CurrentDevice)
            {
                UpdateAttackColliderPosition(lastMouseScreenPos);
            }
        }
    }

    public void SetbCanRotate(bool _bCanRotate)
    {
        bCanRotate = _bCanRotate;
    }

    public void GoToAxeMode()
    {
        if (null == ctx || false == bCanRotate || false == bCanSwap) return;
        if (WeaponMode.Axe == currentWeaponMode) return;

        currentWeaponMode = WeaponMode.Axe;
        WeaponModeChangedEvent?.Invoke(currentWeaponMode);

        ApplyWeaponChangeSpeedModifier();
    }

    public void GoToRifleMode()
    {
        if (null == ctx || false == ctx.characterStat.bCanHunting || false == bCanRotate || false == bCanSwap) return;
        if (WeaponMode.Rifle == currentWeaponMode) return;

        currentWeaponMode = WeaponMode.Rifle;
        WeaponModeChangedEvent?.Invoke(currentWeaponMode);

        ApplyWeaponChangeSpeedModifier();
    }

    private void ApplyWeaponChangeSpeedModifier()
    {
        if (null != ctx && null != ctx.characterStat)
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

        if (null != indicatorFadeCoroutine)
        {
            StopCoroutine(indicatorFadeCoroutine);
            indicatorFadeCoroutine = null;
        }

        if (null == ellipseIndicatorMat) return;

        if (_boolean)
        {
            indicatorFadeCoroutine = StartCoroutine(IndicatorFadeInRoutine());
        }
        else
        {
            // 페이드 도중 꺼졌다면(예: 던전→마을 이동) 머티리얼에 중간 알파값이 남아있을 수 있으므로,
            // 다음 SetEnable(true)이 항상 0에서 시작할 수 있도록 즉시 원래 알파로 되돌려 둔다.
            Color color = ellipseIndicatorMat.GetColor(BaseColorID);
            color.a = indicatorFullAlpha;
            ellipseIndicatorMat.SetColor(BaseColorID, color);
        }
    }

    private System.Collections.IEnumerator IndicatorFadeInRoutine()
    {
        Color color = ellipseIndicatorMat.GetColor(BaseColorID);
        color.a = 0f;
        ellipseIndicatorMat.SetColor(BaseColorID, color);

        float elapsed = 0f;
        while (elapsed < IndicatorFadeInDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(0f, indicatorFullAlpha, elapsed / IndicatorFadeInDuration);
            ellipseIndicatorMat.SetColor(BaseColorID, color);
            yield return null;
        }

        color.a = indicatorFullAlpha;
        ellipseIndicatorMat.SetColor(BaseColorID, color);
        indicatorFadeCoroutine = null;
    }

    public bool IsCursorEnabled => bCursorEnable;

    public void SetCursorEnable(bool _boolean)
    {
        bool _wasEnabled = bCursorEnable;
        bCursorEnable = _boolean;

        // 조준은 마우스가 물리적으로 움직일 때(MouseMove)만 갱신되므로, 켜는 순간 한 번 직접
        // 계산해주지 않으면 다음 마우스 이동이 있을 때까지 이전 방향을 그대로 보고 있게 된다.
        // lastMouseScreenPos는 bCursorEnable이 false인 동안에도 계속 캐싱되므로(MouseMove 참고)
        // 여기서 곧바로 현재 커서 위치 기준으로 맞출 수 있다. 단, 패드 모드일 때는 마우스 위치로 덮어쓰지 않는다.
        if (_boolean && false == _wasEnabled && Vector2.zero != lastMouseScreenPos)
        {
            if (null != ctx && null != ctx.inputManager && EInputDeviceType.Gamepad != ctx.inputManager.CurrentDevice)
            {
                UpdateAttackColliderPosition(lastMouseScreenPos);
            }
        }
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
