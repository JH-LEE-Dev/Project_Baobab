using UnityEngine;

public class AxeComponent : WeaponComponent
{
    // 내부 의존성
    private AxeAnimation axeAnimation;
    private bool bAttacked = false;
    private readonly int facingDirHash = Animator.StringToHash("facingDir");

    public override void Initialize()
    {
        base.Initialize();
        
        // 내부 컴포넌트 참조 구성
        axeAnimation = GetComponent<AxeAnimation>();
    }

    public override void SetFacingDir(Transform _attackTransform)
    {
        if (bAttacked) return;

        // Arm 위치에서 attackTransform까지의 방향 벡터 계산
        Vector2 direction = (_attackTransform.position - transform.parent.parent.position);

        if (direction.sqrMagnitude < 0.01f) return;

        // 8방향 인덱스 계산
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        int dirIndex = Mathf.RoundToInt(angle / 45f) % 8;
        anim.SetFloat(facingDirHash, dirIndex);

        // 정렬 레이어 처리
        spriteRenderer.sortingOrder = (angle > 0 && angle < 180) ? -1 : 1;
    }

    public override void LeftButtonClicked()
    {
        if (bAttacked || null == axeAnimation) return;

        bAttacked = true;

        // 1. 휘두르기 시작
        axeAnimation.PlaySwing(OnSwingComplete);
    }

    private void OnSwingComplete()
    {
        // 2. 휘두르기 완료 후 원래 위치로 복귀 시작
        axeAnimation.PlayReturn(OnReturnComplete);
    }

    private void OnReturnComplete()
    {
        // 3. 복귀 완료 후 다음 공격 가능 상태로 전환
        bAttacked = false;
    }
}
