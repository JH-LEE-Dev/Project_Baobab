using UnityEngine;

public class ArmComponent : PComponent
{
    // 내부 의존성
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    private Transform attackTransform;

    public ArmAnimValueHandler armAnimValueHandler { get; private set; }

    // 캐싱된 해시값
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private readonly int bAttackHash = Animator.StringToHash("bAttack");

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

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
}
