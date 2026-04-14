using UnityEngine;

public class ArmComponent : PComponent, IArmComponent
{
    // 내부 의존성

    private Transform attackTransform;

    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float maxYOffset = 0.5f;

    // 캐싱된 해시값
    private WeaponMode currentWeaponMode = WeaponMode.None;

    public AxeComponent axeComponent { get; private set; }
    public RifleComponent rifleComponent { get; private set; }
    private Vector3 initialLocalPosition;

    public WeaponComponent currentWeapon { get; private set; }

    IAxeComponent IArmComponent.axeComponent => axeComponent;

    IRifleComponent IArmComponent.rifleComponent => rifleComponent;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        initialLocalPosition = transform.localPosition;

        axeComponent = GetComponentInChildren<AxeComponent>();
        rifleComponent = GetComponentInChildren<RifleComponent>();
        axeComponent.Initialize(ctx);
        rifleComponent.Initialize(ctx);
        axeComponent.SetEnable(false);
        rifleComponent.SetEnable(false);

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

    private void Update()
    {
        UpdateRotation();
        UpdateFacingDirection();
        UpdatePositionOffset();
        UpdateFlip();
    }

    private void BindEvents()
    {
        ctx.inputManager.inputReader.MoveTriggerEvent -= rifleComponent.CancelReady;
        ctx.inputManager.inputReader.MoveTriggerEvent += rifleComponent.CancelReady;

        ctx.inputManager.inputReader.MouseClickEvent -= LeftButtonClicked;
        ctx.inputManager.inputReader.MouseClickEvent += LeftButtonClicked;

        ctx.inputManager.inputReader.MouseReleaseEvent -= LeftButtonReleased;
        ctx.inputManager.inputReader.MouseReleaseEvent += LeftButtonReleased;

        ctx.inputManager.inputReader.ReloadButtonPressedEvent -= rifleComponent.Reload;
        ctx.inputManager.inputReader.ReloadButtonPressedEvent += rifleComponent.Reload;
    }

    private void ReleaseEvents()
    {
        ctx.inputManager.inputReader.MoveTriggerEvent -= rifleComponent.CancelReady;

        ctx.inputManager.inputReader.MouseClickEvent -= LeftButtonClicked;

        ctx.inputManager.inputReader.MouseReleaseEvent -= LeftButtonReleased;

        ctx.inputManager.inputReader.ReloadButtonPressedEvent -= rifleComponent.Reload;
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
    }

    private void UpdatePositionOffset()
    {
        if (attackTransform == null) return;

        Vector2 direction = (attackTransform.position - transform.position);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // 0~360도로 변환 (0: 우, 90: 상, 180: 좌, 270: 하)
        if (angle < 0) angle += 360f;

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

        // 타겟의 x 위치가 Arm의 x 위치보다 작으면 왼쪽(-1), 크면 오른쪽(1)
        Vector3 localScale = transform.localScale;
        localScale.x = (attackTransform.position.x < transform.position.x) ? -1f : 1f;
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

    public void ResetDurability()
    {
        axeComponent.ResetDurability();
        rifleComponent.ResetDurability();
        rifleComponent.ResetAmmo();
    }
}
