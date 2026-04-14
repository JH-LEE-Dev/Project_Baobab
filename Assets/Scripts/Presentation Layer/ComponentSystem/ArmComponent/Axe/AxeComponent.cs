using DG.Tweening;
using UnityEngine;

public class AxeComponent : WeaponComponent
{
    // 외부 의존성
    [Header("Swing Path Points (World)")]
    [SerializeField] private Transform controlPoint; // 궤적 곡률 제어점
    [SerializeField] private Transform targetPoint;  // 도착 위치 (타격 지점)

    [Header("Swing Settings")]
    [SerializeField] private float swingDuration = 0.2f;
    [SerializeField] private float endRotationZ = 90f;
    [SerializeField] private Ease swingEase = Ease.InCubic;

    [Header("Return Settings")]
    [SerializeField] private float returnDuration = 0.15f;
    [SerializeField] private Ease returnEase = Ease.OutQuad;

    // 내부 의존성
    private bool bAttacked = false;
    private Tween pathTween;
    private Tween rotateTween;
    
    // 복귀를 위한 초기 상태 저장
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;

    // 베지어 경로 캐싱 (GC 방지용 고정 배열)
    // DOTween Bezier Segment: [Control1, Control2, End]
    private readonly Vector3[] bezierPath = new Vector3[3];
    private readonly int facingDirHash = Animator.StringToHash("facingDir");

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
        if (bAttacked || null == controlPoint || null == targetPoint) return;

        bAttacked = true;

        // 1. 초기 상태 저장
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;

        // 2. 좌표 변환 (월드 -> 부모 기준 로컬)
        // InverseTransformPoint는 부모의 스케일/플립 상태가 이미 반영된 좌표를 반환합니다.
        Vector3 localControl = transform.parent.InverseTransformPoint(controlPoint.position);
        Vector3 localTarget = transform.parent.InverseTransformPoint(targetPoint.position);

        // 베지어 경로 설정 [Control1, Control2, End]
        bezierPath[0] = localControl;
        bezierPath[1] = localControl;
        bezierPath[2] = localTarget;

        KillTweens();

        // 3. 휘두르기 시작 (현재 로컬 위치에서 목표 로컬 지점까지)
        pathTween = transform.DOLocalPath(bezierPath, swingDuration, PathType.CubicBezier)
            .SetEase(swingEase)
            .OnComplete(OnSwingComplete);

        rotateTween = transform.DOLocalRotate(new Vector3(0, 0, endRotationZ), swingDuration, RotateMode.LocalAxisAdd)
            .SetEase(swingEase);
    }

    private void OnSwingComplete()
    {
        // 4. 휘두르기 완료 후 원래 위치로 복귀 시작
        pathTween = transform.DOLocalMove(initialLocalPos, returnDuration)
            .SetEase(returnEase)
            .OnComplete(OnReturnComplete);

        rotateTween = transform.DOLocalRotateQuaternion(initialLocalRot, returnDuration)
            .SetEase(returnEase);
    }

    private void OnReturnComplete()
    {
        // 5. 복귀 완료 후 다음 공격 가능 상태로 전환
        bAttacked = false;
    }

    private void KillTweens()
    {
        if (null != pathTween && pathTween.IsActive()) pathTween.Kill();
        if (null != rotateTween && rotateTween.IsActive()) rotateTween.Kill();
    }

    private void OnDestroy()
    {
        KillTweens();
    }

    private void OnDrawGizmosSelected()
    {
        if (controlPoint == null || targetPoint == null) return;

        Gizmos.color = Color.cyan;
        Vector3 startPos = transform.position;
        Vector3 previousPoint = startPos;
        const int segments = 20;

        for (int i = 1; i <= segments; i++)
        {
            float t = i / (float)segments;
            float invT = 1f - t;
            Vector3 currentPoint = invT * invT * startPos + 
                                   2f * invT * t * controlPoint.position + 
                                   t * t * targetPoint.position;

            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(targetPoint.position, 0.05f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(controlPoint.position, 0.03f);
    }
}
