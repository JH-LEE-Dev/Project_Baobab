using UnityEngine;

public class ArmComponent : PComponent
{
    // 내부 의존성
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    private Transform attackTransform;

    public ArmAnimValueHandler armAnimValueHandler { get; private set; }

    [SerializeField] private float smoothSpeed = 10f;

    // 캐싱된 해시값
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private readonly int bAttackHash = Animator.StringToHash("bAttack");
    private WeaponMode currentWeaponMode = WeaponMode.None;


    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        anim = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        spriteRenderer.enabled = false;

        armAnimValueHandler = new ArmAnimValueHandler();
        armAnimValueHandler.Initialize(anim);

        BindEvents();
    }

    public void OnDestroy()
    {
        ReleaseEvents();
    }

    public void SetActivate(bool _boolean)
    {
        spriteRenderer.enabled = _boolean;
    }

    public void SetAttackTransform(Transform _transform)
    {
        attackTransform = _transform;
    }

    private void Update()
    {
        UpdateRotation();
        UpdateFacingDirection();
    }

    private void BindEvents()
    {
        ctx.inputManager.inputReader.MouseClickEvent -= StartAttack;
        ctx.inputManager.inputReader.MouseClickEvent += StartAttack;
    }

    private void ReleaseEvents()
    {
        ctx.inputManager.inputReader.MouseClickEvent -= StartAttack;
    }

    private void UpdateRotation()
    {
        if (attackTransform == null || !spriteRenderer.enabled) return;

        // 타겟을 바라보는 방향 계산
        Vector2 dirToTarget = (Vector2)attackTransform.position - (Vector2)transform.position;
        
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
        if (attackTransform == null || !spriteRenderer.enabled) return;

        // Arm 위치에서 attackTransform까지의 방향 벡터 계산
        Vector2 direction = (attackTransform.position - transform.position);

        if (direction.sqrMagnitude < 0.01f) return;

        // 8방향 인덱스 계산 (0: 우, 1: 우상, 2: 상, 3: 좌상, 4: 좌, 5: 좌하, 6: 하, 7: 우하)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        int dirIndex = Mathf.RoundToInt(angle / 45f) % 8;
        anim.SetFloat(facingDirHash, dirIndex);
    }

    private void StartAttack()
    {
        anim.SetBool(bAttackHash, true);
    }

    public void WeaponModeChanged(WeaponMode _weaponMode)
    {
        currentWeaponMode = _weaponMode;
    }
}
