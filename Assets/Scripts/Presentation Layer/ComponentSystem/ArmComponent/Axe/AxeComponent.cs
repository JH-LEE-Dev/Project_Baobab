using UnityEngine;

public class AxeComponent : WeaponComponent
{
    // 내부 의존성
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private float currentTargetAngle; 

    private readonly int facingDirHash = Animator.StringToHash("facingDir");

    /// <summary>
    /// 무기 초기화 및 초기 상태 캐싱
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        initialLocalPosition = transform.localPosition;
        initialLocalRotation = transform.localRotation;
    }
    
    /// <summary>
    /// 타겟 방향에 따른 애니메이터 및 렌더링 정렬 설정
    /// </summary>
    public override void SetFacingDir(Transform _attackTransform)
    {
        if (transform.parent == null || transform.parent.parent == null) return;

        // 마우스 방향 계산
        Vector2 direction = (_attackTransform.position - transform.parent.parent.position);
        if (direction.sqrMagnitude < 0.01f) return;

        currentTargetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (currentTargetAngle < 0) currentTargetAngle += 360;

        // 애니메이터 방향 인덱스 업데이트 (8방향)
        if (anim != null)
        {
            anim.SetFloat(facingDirHash, Mathf.RoundToInt(currentTargetAngle / 45f) % 8);
        }

        // Isometric Depth 처리를 위한 정렬 순서 조절
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = (currentTargetAngle > 0 && currentTargetAngle < 180) ? -1 : 1;
        }
    }

    public override void LeftButtonClicked()
    {

    }
}
