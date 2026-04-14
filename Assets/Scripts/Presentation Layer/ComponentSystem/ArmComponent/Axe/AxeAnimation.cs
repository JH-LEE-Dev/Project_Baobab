using DG.Tweening;
using UnityEngine;
using System;

public class AxeAnimation : MonoBehaviour
{
    // 외부 의존성
    [Header("Swing Path Points (World)")]
    [SerializeField] private Transform controlPoint; // 궤적 곡률 제어점
    [SerializeField] private Transform targetPoint;  // 도착 위치 (타격 지점)

    [Header("Swing Settings")]
    [SerializeField] private float swingDuration = 0.05f;
    [SerializeField] private float endRotationZ = -90f;
    [SerializeField] private Ease swingEase = Ease.InCubic;

    [Header("Return Settings")]
    [SerializeField] private float returnDuration = 0.15f;
    [SerializeField] private Ease returnEase = Ease.OutQuad;

    // 내부 의존성
    private Tween pathTween;
    private Tween rotateTween;
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;
    private readonly Vector3[] bezierPath = new Vector3[3];

    public void PlaySwing(Action _onComplete)
    {
        if (null == controlPoint || null == targetPoint) return;

        // 1. 초기 상태 저장
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;

        // 2. 좌표 변환 (월드 -> 부모 기준 로컬)
        Vector3 localControl = transform.parent.InverseTransformPoint(controlPoint.position);
        Vector3 localTarget = transform.parent.InverseTransformPoint(targetPoint.position);

        // 베지어 경로 설정 [Control1, Control2, End]
        bezierPath[0] = localControl;
        bezierPath[1] = localControl;
        bezierPath[2] = localTarget;

        KillTweens();

        // 3. 휘두르기 시작
        pathTween = transform.DOLocalPath(bezierPath, swingDuration, PathType.CubicBezier)
            .SetEase(swingEase)
            .OnComplete(() => _onComplete?.Invoke());

        rotateTween = transform.DOLocalRotate(new Vector3(0, 0, endRotationZ), swingDuration, RotateMode.LocalAxisAdd)
            .SetEase(swingEase);
    }

    public void PlayReturn(Action _onComplete)
    {
        pathTween = transform.DOLocalMove(initialLocalPos, returnDuration)
            .SetEase(returnEase)
            .OnComplete(() => _onComplete?.Invoke());

        rotateTween = transform.DOLocalRotateQuaternion(initialLocalRot, returnDuration)
            .SetEase(returnEase);
    }

    public void KillTweens()
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
