using DG.Tweening;
using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public struct AxeKeyframe
{
    public Transform target;       // 키프레임의 위치와 회전 (Transform)
    [Range(0.01f, 1f)]
    public float durationRatio;    // 이 구간이 차지하는 시간 비중 (합계가 1이 되도록 설정)
    public Ease ease;              // 이 구간의 보간 곡선
}

public class AxeComponent : WeaponComponent
{
    // 외부 의존성
    [Header("Keyframe Settings")]
    [SerializeField] private List<AxeKeyframe> swingKeyframes = new List<AxeKeyframe>();
    [SerializeField] private float totalSwingDuration = 0.3f;

    [Header("Return Settings")]
    [SerializeField] private float returnDuration = 0.2f;
    [SerializeField] private Ease returnEase = Ease.OutQuad;

    // 내부 의존성
    private bool bAttacked = false;
    private Sequence attackSeq;
    
    private Vector3 initialLocalPos;
    private Quaternion initialLocalRot;

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
        if (bAttacked) return;

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
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        int dirIndex = Mathf.RoundToInt(angle / 45f) % 8;
        anim.SetFloat(facingDirHash, dirIndex);

        spriteRenderer.sortingOrder = (angle > 0 && angle < 180) ? -1 : 1;
    }

    public override void LeftButtonClicked()
    {
        if (bAttacked || swingKeyframes == null || swingKeyframes.Count == 0) return;

        bAttacked = true;

        // 1. 초기 상태 기록
        initialLocalPos = transform.localPosition;
        initialLocalRot = transform.localRotation;

        if (null != attackSeq && attackSeq.IsActive())
            attackSeq.Kill();

        attackSeq = DOTween.Sequence();

        // 2. 키프레임 루프를 돌며 시퀀스 구성
        for (int i = 0; i < swingKeyframes.Count; i++)
        {
            AxeKeyframe kf = swingKeyframes[i];
            if (kf.target == null) continue;

            float stepTime = totalSwingDuration * kf.durationRatio;
            
            // 로컬 좌표 및 회전 변환
            Vector3 targetPos = transform.parent.InverseTransformPoint(kf.target.position);
            Quaternion targetRot = Quaternion.Inverse(transform.parent.rotation) * kf.target.rotation;

            // 이동과 회전을 동시에 실행 (Append + Join)
            attackSeq.Append(transform.DOLocalMove(targetPos, stepTime).SetEase(kf.ease));
            attackSeq.Join(transform.DOLocalRotateQuaternion(targetRot, stepTime).SetEase(kf.ease));
        }

        // 3. 모든 키프레임 완료 후 복귀 단계로 전환
        attackSeq.OnComplete(OnSwingFinished);
    }

    private void OnSwingFinished()
    {
        if (null != attackSeq && attackSeq.IsActive())
            attackSeq.Kill();

        attackSeq = DOTween.Sequence();

        // 초기 위치로 부드럽게 복귀
        attackSeq.Append(transform.DOLocalMove(initialLocalPos, returnDuration).SetEase(returnEase));
        attackSeq.Join(transform.DOLocalRotateQuaternion(initialLocalRot, returnDuration).SetEase(returnEase));

        attackSeq.OnComplete(OnReturnFinished);
    }

    private void OnReturnFinished()
    {
        bAttacked = false;
    }

    private void OnDestroy()
    {
        if (null != attackSeq && attackSeq.IsActive())
            attackSeq.Kill();
    }

    private void OnDrawGizmosSelected()
    {
        if (swingKeyframes == null || swingKeyframes.Count == 0) return;

        Gizmos.color = Color.yellow;
        Vector3 lastPos = transform.position;
        
        for (int i = 0; i < swingKeyframes.Count; i++)
        {
            if (swingKeyframes[i].target == null) continue;
            
            Vector3 currentPos = swingKeyframes[i].target.position;
            Gizmos.DrawLine(lastPos, currentPos);
            Gizmos.DrawSphere(currentPos, 0.03f);
            
            // 방향 표시 (빨간색 선으로 도끼날이 향할 방향 시각화)
            Gizmos.color = Color.red;
            Gizmos.DrawRay(currentPos, swingKeyframes[i].target.up * 0.1f);
            Gizmos.color = Color.yellow;
            
            lastPos = currentPos;
        }
    }
}
