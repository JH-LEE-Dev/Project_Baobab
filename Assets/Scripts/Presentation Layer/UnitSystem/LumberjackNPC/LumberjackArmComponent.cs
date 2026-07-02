using UnityEngine;

public class LumberjackArmComponent : MonoBehaviour
{
    [SerializeField] private float smoothSpeed = 15f;
    [SerializeField] private float maxYOffset = 0.5f;

    [Header("Axe Visual & Animation")]
    [SerializeField] private AxeAnimation axeAnimation;
    [SerializeField] private SpriteRenderer axeSpriteRenderer;

    private Vector3 initialLocalPosition;
    private Vector2 targetDirection = Vector2.down;
    private bool bIsAttacking = false;

    public void Initialize()
    {
        initialLocalPosition = transform.localPosition;
    }

    /// <summary>
    /// 타겟 방향을 설정하여 팔이 8방향 중 알맞은 곳을 가리키게 합니다.
    /// </summary>
    public void SetTargetDirection(Vector2 _direction)
    {
        if (_direction.sqrMagnitude < 0.01f) return;

        // 8방향 스냅(Snap)을 주려면 각도를 계산하여 가장 가까운 45도 배수로 변환합니다.
        float angle = Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;
        
        float snappedAngle = Mathf.Round(angle / 45f) * 45f;
        
        // 방향 벡터 업데이트 (스냅된 각도로)
        float rad = snappedAngle * Mathf.Deg2Rad;
        targetDirection = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
    }

    private void Update()
    {
        UpdateRotation();
        UpdatePositionOffset();
        UpdateFlip();
    }

    private void UpdateRotation()
    {
        // Down(0, -1) 방향을 0도로 기준 삼기 위해 90도 오프셋 추가
        float angle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg + 90f;
        Quaternion targetRotation = Quaternion.Euler(0, 0, angle);

        // 회전 스무딩 적용
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
    }

    private void UpdatePositionOffset()
    {
        float angle = Mathf.Atan2(targetDirection.y, targetDirection.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        // 0~180도(상단 반원) 범위일 때만 Sin 곡선을 따라 오프셋 적용
        if (angle >= 0f && angle <= 180f)
        {
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
        // 타겟의 x 방향이 왼쪽이면 -1, 오른쪽이면 1
        Vector3 localScale = transform.localScale;
        localScale.x = (targetDirection.x < 0) ? -1f : 1f;
        transform.localScale = localScale;
    }

    /// <summary>
    /// 공격 애니메이션 재생
    /// </summary>
    public void SwingAxe(System.Action _onImpactCallback)
    {
        if (bIsAttacking || axeAnimation == null) return;

        bIsAttacking = true;

        axeAnimation.PlaySwing(() =>
        {
            // 타격 시점
            _onImpactCallback?.Invoke();
            
            axeAnimation.PlayReturn(() =>
            {
                // 타격 종료
                bIsAttacking = false;
            });
        });
    }

    public void EnableAxe(bool _enable)
    {
        gameObject.SetActive(_enable);
    }
}
