using UnityEngine;
using UnityEngine.Animations;

public class RifleComponent : WeaponComponent
{
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private readonly int bReadyHash = Animator.StringToHash("bReady");

    private bool bReady = false;

    public override void SetFacingDir(Transform _attackTransform)
    {
        // 타겟 방향 벡터 계산
        Vector2 direction = (_attackTransform.position - transform.parent.parent.position);

        if (direction.sqrMagnitude < 0.01f) return;

        // 4방향 판정 (0: 우/좌, 1: 상, 3: 하)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        int dirIndex;
        if (angle >= 65f && angle <= 115f) // 상: 90도 기준 +-25도 (총 50도)
        {
            dirIndex = 1;
        }
        else if (angle >= 245f && angle <= 295f) // 하: 270도 기준 +-25도 (총 50도)
        {
            dirIndex = 3;
        }
        else
        {
            // 그 외 범위(좌/우)는 0으로 설정
            // (Left 판정 범위인 115~245도 구간도 포함하며, 이미 부모에서 Flip 처리가 되므로 0을 사용)
            dirIndex = 0;
        }

        anim.SetFloat(facingDirHash, dirIndex);

        // 위쪽 반원(0~180도)일 때는 뒤쪽(-1), 나머지는 앞쪽(1)으로 정렬
        if (angle > 0 && angle < 180)
        {
            spriteRenderer.sortingOrder = -1;
        }
        else
        {
            spriteRenderer.sortingOrder = 1;
        }
    }

    public override void LeftButtonClicked()
    {
        bReady = !bReady;

        anim.SetBool(bReadyHash, bReady);
    }
}
