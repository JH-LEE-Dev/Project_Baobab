using UnityEngine;

public class RifleComponent : WeaponComponent
{
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    public override void SetFacingDir(Transform _attackTransform)
    {
        // 타겟 방향 벡터 계산
        Vector2 direction = (_attackTransform.position - transform.position);

        if (direction.sqrMagnitude < 0.01f) return;

        // 4방향 인덱스 계산 (0: 우, 1: 상, 2: 좌, 3: 하)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        int dirIndex = Mathf.RoundToInt(angle / 90f) % 4;

        // Left(2)일 때는 Right(0)로 변경
        if (dirIndex == 2)
        {
            dirIndex = 0;
        }

        anim.SetFloat(facingDirHash, dirIndex);
    }
}
