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
        if (attackTransform == null) return;

        // 타겟을 바라보는 방향 계산
        Vector2 dirToTarget = (Vector2)attackTransform.position - (Vector2)transform.parent.position;

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
        if (attackTransform == null || currentWeapon == null) return;

        currentWeapon.SetFacingDir(attackTransform);
        currentWeapon.SetMouseTransform(mouseTransform);
    }

    private void UpdatePositionOffset()
    {
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
        if (angle >= 0f && angle <= 180f)
        {
            // Mathf.Sin은 라디안 값을 사용하므로 Deg2Rad 변환
            float offsetMultiplier = Mathf.Sin(angle * Mathf.Deg2Rad);
            float offset = offsetMultiplier * maxYOffset;
            transform.localPosition = initialLocalPosition + Vector3.down * offset;
        }
        else
        {
            transform.localPosition = initialLocalPosition;
        }
    }

    private void UpdateFlip()
    {
        if (attackTransform == null) return;

        float _distanceX = attackTransform.position.x - transform.position.x;

        // 조준이 거의 수직일 때는 방금까지의 방향을 유지한다. x 부호만 보고 뒤집으면, 스틱을 위로
        // 곧게 밀었을 때 x가 0 근처에서 미세하게 흔들리며 팔이 좌우로 깜빡인다.
        // (8방향 조준일 때는 x가 정확히 0이라 드러나지 않던 문제다)
        if (Mathf.Abs(_distanceX) < FlipDeadband) return;

        // 타겟의 x 위치가 Arm의 x 위치보다 작으면 왼쪽(-1), 크면 오른쪽(1)
        Vector3 localScale = transform.localScale;
        localScale.x = (_distanceX < 0f) ? -1f : 1f;
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
