using UnityEngine;

public class ArmComponent : PComponent, IArmComponent
{
    // 내부 의존성

    private Transform attackTransform;
    private Vector3 mouseTransform;

    [SerializeField] private float smoothSpeed = 10f;

    [SerializeField] private float maxYOffset = 0.5f;

    // 조준이 거의 수직일 때 팔이 좌우로 깜빡이는 것을 막는 완충 거리(월드 단위).
    // 패드 조준 반경(1.25) 기준으로 수직에서 약 2도에 해당한다.
    private const float FlipDeadband = 0.05f;

    // 캐싱된 해시값
    private WeaponMode currentWeaponMode = WeaponMode.None;

    public AxeComponent axeComponent { get; private set; }
    public RifleComponent rifleComponent { get; private set; }
    private Vector3 initialLocalPosition;
    // 마지막으로 적용한 Y 오프셋/좌우 부호. 값이 같으면 Transform 세터(계층 dirty)를 건너뛴다.
    private float appliedYOffset = float.NaN;
    private float appliedFlipSign = 0f;
    private Transform parentTransform; // transform.parent(네이티브 호출) 1회 캐싱 - 계층은 런타임에 바뀌지 않는다

    public WeaponComponent currentWeapon { get; private set; }

    // 넉백 등으로 조준 회전/방향 전환을 일시적으로 막아야 할 때 사용
    private bool bRotationLocked = false;
    public void SetRotationLocked(bool _locked)
    {
        bRotationLocked = _locked;
    }

    // 회전 베기 연출 중에는 팔이 마우스를 따라가지 않고 캐릭터 스프라이트와 함께 한 바퀴 돈다.
    // 넉백 잠금과 서로 풀어버리지 않도록 별도 플래그로 둔다.
    // 연출이 끝나면 UpdateRotation의 Lerp가 그대로 마우스 방향으로 보간해 준다.
    private bool bWhirlwindSpinning = false;
    private float whirlwindSpinStartZ = 0f;

    public void BeginWhirlwindSpin()
    {
        bWhirlwindSpinning = true;
        whirlwindSpinStartZ = transform.eulerAngles.z;
    }

    // _degrees: 시작 각도로부터 누적 회전량 (반시계 +)
    public void ApplyWhirlwindSpin(float _degrees)
    {
        if (false == bWhirlwindSpinning || bRotationLocked) return;

        float z = whirlwindSpinStartZ + _degrees;
        transform.rotation = Quaternion.Euler(0f, 0f, z);

        // 팔 회전값은 아래(0,-1)를 0도로 삼으므로(UpdateRotation 참고) 조준 각도로 되돌려 쓴다.
        // 좌우 반전(localScale.x)은 시작할 때 그대로 두어야 한 방향으로 매끄럽게 돈다.
        float aimAngle = z - 90f;
        ApplyPositionOffset(aimAngle);
        if (axeComponent != null && ReferenceEquals(currentWeapon, axeComponent))
        {
            axeComponent.SetSortingByAngle(aimAngle);
        }
    }

    public void EndWhirlwindSpin()
    {
        bWhirlwindSpinning = false;
    }

    // 넉백 등으로 연출을 중간에 끊을 때 - 회전 시작 전 자세로 되돌린다.
    // 넉백 잠금이 걸리기 전에 불려야 복원된 자세가 넉백 동안 그대로 유지된다.
    public void CancelWhirlwindSpin()
    {
        if (false == bWhirlwindSpinning) return;

        transform.rotation = Quaternion.Euler(0f, 0f, whirlwindSpinStartZ);

        float aimAngle = whirlwindSpinStartZ - 90f;
        ApplyPositionOffset(aimAngle);
        if (axeComponent != null && ReferenceEquals(currentWeapon, axeComponent))
        {
            axeComponent.SetSortingByAngle(aimAngle);
        }

        bWhirlwindSpinning = false;
    }

    // 대시처럼 조준점이 아니라 정해진 방향을 봐야 할 때 쓴다. 몸이 대시 방향을 보는 동안 팔도 같은 쪽을 보게 한다.
    // 켜져 있는 동안 회전·좌우 반전·높이 보정·도끼 앞뒤 정렬을 조준점 대신 이 방향(월드 기준)으로 맞춘다.
    // 넉백 잠금·회전 베기는 지금처럼 이보다 우선한다(Update가 먼저 거른다).
    private bool bAimOverride = false;
    private Vector2 aimOverrideDirection = Vector2.down;

    // 방향 덮어쓰기 중 좌우 반전 완충값. 단위 벡터의 x라 FlipDeadband(월드 거리)를 그대로 쓸 수 없다.
    private const float AimOverrideFlipDeadband = 0.01f;

    /// <summary>
    /// 팔이 조준점 대신 _worldDirection을 보게 한다. 몸이 보간 없이 바로 돌아가므로 팔도 바로 맞춘다
    /// (보간하면 대시처럼 짧은 동작에서는 몸과 팔이 끝까지 따로 논다).
    /// </summary>
    public void SetAimOverride(Vector2 _worldDirection)
    {
        if (_worldDirection.sqrMagnitude < 0.0001f) return;

        bAimOverride = true;
        aimOverrideDirection = _worldDirection.normalized;

        if (bRotationLocked || bWhirlwindSpinning) return;

        transform.rotation = Quaternion.Euler(0f, 0f, GetArmRotationZ(aimOverrideDirection));
        UpdateFacingDirection();
        UpdatePositionOffset();
        UpdateFlip();
    }

    /// <summary>방향 덮어쓰기를 푼다. 이후 UpdateRotation의 보간으로 조준 방향으로 부드럽게 돌아간다.</summary>
    public void ClearAimOverride()
    {
        bAimOverride = false;
    }

    // 팔 회전값은 아래(0,-1)를 0도로 삼는다(UpdateRotation과 같은 기준).
    private static float GetArmRotationZ(Vector2 _direction)
    {
        return Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg + 90f;
    }

    IAxeComponent IArmComponent.axeComponent => axeComponent;

    IRifleComponent IArmComponent.rifleComponent => rifleComponent;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        initialLocalPosition = transform.localPosition;

        axeComponent = GetComponentInChildren<AxeComponent>();
        //rifleComponent = GetComponentInChildren<RifleComponent>();
        axeComponent.Initialize(ctx);
        //rifleComponent.Initialize(ctx);
        axeComponent.SetEnable(false);
        //rifleComponent.SetEnable(false);

        currentWeapon = axeComponent;

        BindEvents();
    }

    public void OnDestroy()
    {
        ReleaseEvents();
    }

    public void SetActivate(bool _boolean)
    {
        currentWeapon.SetEnable(_boolean);
    }

    public void SetAttackTransform(Transform _transform)
    {
        attackTransform = _transform;
    }
    public void SetMouseTransform(Vector3 _transform)
    {
        mouseTransform = _transform;
    }


    private void Update()
    {
        if (bRotationLocked || bWhirlwindSpinning || Time.timeScale == 0f) return;

        UpdateRotation();
        UpdateFacingDirection();
        UpdatePositionOffset();
        UpdateFlip();
    }

    private void BindEvents()
    {
        ctx.inputManager.inputReader.MouseClickEvent -= LeftButtonClicked;
        ctx.inputManager.inputReader.MouseClickEvent += LeftButtonClicked;

        ctx.inputManager.inputReader.MouseReleaseEvent -= LeftButtonReleased;
        ctx.inputManager.inputReader.MouseReleaseEvent += LeftButtonReleased;
    }

    private void ReleaseEvents()
    {
        ctx.inputManager.inputReader.MouseClickEvent -= LeftButtonClicked;

        ctx.inputManager.inputReader.MouseReleaseEvent -= LeftButtonReleased;
    }

    private void UpdateRotation()
    {
        if (bAimOverride)
        {
            Quaternion overrideRotation = Quaternion.Euler(0, 0, GetArmRotationZ(aimOverrideDirection));
            transform.rotation = Quaternion.Lerp(transform.rotation, overrideRotation, Time.deltaTime * smoothSpeed);
            return;
        }

        if (attackTransform == null) return;

        // 타겟을 바라보는 방향 계산
        if (parentTransform == null) parentTransform = transform.parent;
        Vector2 dirToTarget = (Vector2)attackTransform.position - (Vector2)parentTransform.position;

        if (dirToTarget.sqrMagnitude > 0.001f)
        {
            // Down(0, -1) 방향을 0도로 기준 삼기 위해 90도 오프셋 추가
            float angle = Mathf.Atan2(dirToTarget.y, dirToTarget.x) * Mathf.Rad2Deg + 90f;
            Quaternion targetRotation = Quaternion.Euler(0, 0, angle);

            // 회전 스무딩 적용
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
        }
    }

    private void UpdateFacingDirection()
    {
        if (bAimOverride)
        {
            // 도끼 앞뒤 정렬(위를 볼 때 몸 뒤로)도 덮어쓴 방향 기준으로 맞춘다(AxeComponent.SetFacingDir와 같은 각도 기준).
            if (axeComponent != null && ReferenceEquals(currentWeapon, axeComponent))
            {
                axeComponent.SetSortingByAngle(Mathf.Atan2(aimOverrideDirection.y, aimOverrideDirection.x) * Mathf.Rad2Deg);
            }

            if (currentWeapon != null) currentWeapon.SetMouseTransform(mouseTransform);
            return;
        }

        if (attackTransform == null || currentWeapon == null) return;

        currentWeapon.SetFacingDir(attackTransform);
        currentWeapon.SetMouseTransform(mouseTransform);
    }

    private void UpdatePositionOffset()
    {
        if (bAimOverride)
        {
            ApplyPositionOffset(Mathf.Atan2(aimOverrideDirection.y, aimOverrideDirection.x) * Mathf.Rad2Deg);
            return;
        }

        if (attackTransform == null) return;

        Vector2 direction = (attackTransform.position - transform.position);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        ApplyPositionOffset(angle);
    }

    private void ApplyPositionOffset(float angle)
    {
        // 0~360도로 변환 (0: 우, 90: 상, 180: 좌, 270: 하)
        angle = Mathf.Repeat(angle, 360f);

        // 0~180도(상단 반원) 범위일 때만 Sin 곡선을 따라 오프셋 적용
        float targetOffset = 0f;
        if (angle >= 0f && angle <= 180f)
        {
            // Mathf.Sin은 라디안 값을 사용하므로 Deg2Rad 변환
            float offsetMultiplier = Mathf.Sin(angle * Mathf.Deg2Rad);
            targetOffset = offsetMultiplier * maxYOffset;
        }

        if (targetOffset != appliedYOffset)
        {
            appliedYOffset = targetOffset;
            transform.localPosition = initialLocalPosition + Vector3.down * targetOffset;
        }
    }

    private void UpdateFlip()
    {
        float _distanceX;

        if (bAimOverride)
        {
            _distanceX = aimOverrideDirection.x;

            // 정확히 위·아래로 대시할 때는 방금까지의 방향을 유지한다(아래 조준과 같은 이유).
            if (Mathf.Abs(_distanceX) < AimOverrideFlipDeadband) return;
        }
        else
        {
            if (attackTransform == null) return;

            _distanceX = attackTransform.position.x - transform.position.x;

            // 조준이 거의 수직일 때는 방금까지의 방향을 유지한다. x 부호만 보고 뒤집으면, 스틱을 위로
            // 곧게 밀었을 때 x가 0 근처에서 미세하게 흔들리며 팔이 좌우로 깜빡인다.
            // (8방향 조준일 때는 x가 정확히 0이라 드러나지 않던 문제다)
            if (Mathf.Abs(_distanceX) < FlipDeadband) return;
        }

        // 타겟의 x 위치가 Arm의 x 위치보다 작으면 왼쪽(-1), 크면 오른쪽(1)
        float sign = (_distanceX < 0f) ? -1f : 1f;
        if (sign == appliedFlipSign) return;
        appliedFlipSign = sign;

        Vector3 localScale = transform.localScale;
        localScale.x = sign;
        transform.localScale = localScale;
    }

    private void LeftButtonClicked()
    {
        currentWeapon.LeftButtonClicked();
    }

    private void LeftButtonReleased()
    {
        currentWeapon.LeftButtonReleased();
    }

    public void WeaponModeChanged(WeaponMode _weaponMode)
    {
        currentWeaponMode = _weaponMode;

        if (currentWeaponMode == WeaponMode.Axe)
        {
            currentWeapon = axeComponent;
            rifleComponent.SetEnable(false);
            currentWeapon.SetEnable(true);
        }
        else if (currentWeaponMode == WeaponMode.Rifle)
        {
            currentWeapon = rifleComponent;
            axeComponent.SetEnable(false);
            currentWeapon.SetEnable(true);
        }
    }

    public void ResetWeaponStatus()
    {
        axeComponent.SetEnable(false);
        //rifleComponent.SetEnable(false);
        currentWeaponMode = WeaponMode.Axe;
        currentWeapon = axeComponent;
        axeComponent.ResetDurability();
        //rifleComponent.ResetDurability();
        //rifleComponent.ResetAmmo();
    }

    public void Refresh()
    {
        axeComponent.Refresh();
        //rifleComponent.Refresh();
    }

    public void SortArmCompOrder()
    {
        axeComponent.SortingOrder();
    }

    public void SetbCanAttack(bool _boolean)
    {
        axeComponent.SetbCanAttack(_boolean);
    }

    public void ResetRotation()
    {
        transform.localRotation = Quaternion.identity;
    }
}
