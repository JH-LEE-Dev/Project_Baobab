using UnityEngine;

public class AxeComponent : WeaponComponent
{
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    
    public override void SetFacingDir(Transform _attackTransform)
    {
        // Arm 위치에서 attackTransform까지의 방향 벡터 계산
        Vector2 direction = (_attackTransform.position - transform.parent.parent.position);

        if (direction.sqrMagnitude < 0.01f) return;

        // 8방향 인덱스 계산 (0: 우, 1: 우상, 2: 상, 3: 좌상, 4: 좌, 5: 좌하, 6: 하, 7: 우하)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        int dirIndex = Mathf.RoundToInt(angle / 45f) % 8;
        anim.SetFloat(facingDirHash, dirIndex);

        // 정확히 위쪽 반원(0~180도)일 때는 뒤쪽(-1), 나머지는 앞쪽(1)으로 정렬
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
    
    }
}
