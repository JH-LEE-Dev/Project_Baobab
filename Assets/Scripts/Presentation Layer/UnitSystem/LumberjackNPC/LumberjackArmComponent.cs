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

    // SwingAxe()는 chopInterval마다 반복 호출되는 핫 패스이므로, 매번 새 람다 클로저를 만드는 대신
    // 콜백은 필드에 저장하고 완료 델리게이트는 한 번만 캐싱해 재사용한다 (GC 할당 제거).
    private System.Action pendingImpactCallback;
    private System.Action cachedOnSwingComplete;
    private System.Action cachedOnReturnComplete;

    private void Awake()
    {
        initialLocalPosition = transform.localPosition;
        cachedOnSwingComplete = OnSwingComplete;
        cachedOnReturnComplete = OnReturnComplete;
    }

    public void Initialize()
    {
        // Awake에서 initialLocalPosition을 캐싱하여 Update가 먼저 실행되어 좌표가 망가지는 현상 방지

        // 오브젝트 풀에서 재사용될 때, 이전 생애에 스윙 도중 상태가 남아있으면
        // bIsAttacking이 true로 고정되어 SwingAxe()가 영원히 무시되는 문제를 방지
        bIsAttacking = false;
        if (axeAnimation != null)
        {
            axeAnimation.ResetPose();
        }
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

    private Vector3 GetFakeTargetPosition()
    {
        // 부모(몸체) 중심에서 타겟 방향으로 일정 거리만큼 떨어진 가상의 목표점 계산
        return transform.parent.position + (Vector3)targetDirection * 2f;
    }

    public void UpdateSortingOrder()
    {
        if (axeSpriteRenderer == null) return;

        Vector2 direction = (GetFakeTargetPosition() - transform.position);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        // 위쪽 방향(0도~180도)을 바라볼 때는 무기가 몸 뒤에 가려져야 함 (-1)
        int sortingOffset = (angle > 0f && angle < 180f) ? -1 : 1;
        axeSpriteRenderer.sortingOrder += sortingOffset;
    }

    private void UpdateRotation()
    {
        Vector2 dirToTarget = GetFakeTargetPosition() - transform.parent.position;
        if (dirToTarget.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(dirToTarget.y, dirToTarget.x) * Mathf.Rad2Deg + 90f;
            Quaternion targetRotation = Quaternion.Euler(0, 0, angle);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);
        }
    }

    private void UpdatePositionOffset()
    {
        Vector2 direction = GetFakeTargetPosition() - transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

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
        Vector3 fakePos = GetFakeTargetPosition();
        Vector3 localScale = transform.localScale;
        localScale.x = (fakePos.x < transform.position.x) ? -1f : 1f;
        transform.localScale = localScale;
    }

    /// <summary>
    /// 공격 애니메이션 재생
    /// </summary>
    public void SwingAxe(System.Action _onImpactCallback)
    {
        if (bIsAttacking || axeAnimation == null) return;

        bIsAttacking = true;
        pendingImpactCallback = _onImpactCallback;

        axeAnimation.PlaySwing(cachedOnSwingComplete);
    }

    private void OnSwingComplete()
    {
        // 타격 시점
        pendingImpactCallback?.Invoke();
        axeAnimation.PlayReturn(cachedOnReturnComplete);
    }

    private void OnReturnComplete()
    {
        // 타격 종료
        bIsAttacking = false;
    }
}
