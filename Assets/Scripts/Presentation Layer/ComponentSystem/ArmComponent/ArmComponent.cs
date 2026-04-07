using UnityEngine;

public class ArmComponent : PComponent
{
    // 내부 의존성
    private Animator anim;
    private SpriteRenderer spriteRenderer;

    private Transform attackTransform;

    public ArmAnimValueHandler armAnimValueHandler { get; private set; }

    [SerializeField] private Transform revolutionCenter;
    [SerializeField] private float orbitRadius = 0.1f;

    // 캐싱된 해시값
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private readonly int bAttackHash = Animator.StringToHash("bAttack");

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
        UpdateOrbitPosition();
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

    private void UpdateOrbitPosition()
    {
        if (revolutionCenter == null || attackTransform == null || !spriteRenderer.enabled) return;

        // 1. 위치 결정: 중심점에서 타겟 방향으로 공전 궤도 위에 배치
        Vector2 centerToTarget = (Vector2)attackTransform.position - (Vector2)revolutionCenter.position;
        if (centerToTarget.sqrMagnitude < 0.001f) return;
        
        Vector2 orbitDir = centerToTarget.normalized;
        transform.position = (Vector2)revolutionCenter.position + orbitDir * orbitRadius;

        // 2. 회전 결정: "팔의 현재 위치"에서 "타겟"을 바라보도록 회전
        Vector2 armToTarget = (Vector2)attackTransform.position - (Vector2)transform.position;
        
        // 타겟이 팔의 위치와 겹칠 경우(궤도 안쪽 클릭 등) 공전 방향을 기본으로 사용
        if (armToTarget.sqrMagnitude < 0.0001f) armToTarget = orbitDir;

        // Down(0, -1) 방향을 0도로 기준 삼기 위해 90도 오프셋 추가
        float angle = Mathf.Atan2(armToTarget.y, armToTarget.x) * Mathf.Rad2Deg + 90f;
        transform.rotation = Quaternion.Euler(0, 0, angle);
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
