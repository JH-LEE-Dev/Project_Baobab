// ──────────────────────────────────────────────────────────────────────────
// [사용 안 함 — 기획 결정]  총(소총 / 리코셰) 계열
//
// 이 계열은 쓰지 않기로 결정된 기능입니다. 배선이 빠진 것이 아니라 "안 쓰기로 한 것"이므로,
// 동작하지 않는다고 해서 버그로 보고 되살리거나 배선을 복구하지 마십시오.
//
// 현재 상태: SkillDataBase_Full에 소총·리코셰 커맨드 8종 항목이 없어 WeaponMode 전환이 열리지 않습니다.
//            공격은 항상 도끼 모드로 고정됩니다.
//
// 코드를 지우지 않고 주석만 남긴 이유: WeaponMode가 AttackComponent(15곳) · ArmComponent(6곳) 같은
//   "지금 돌아가는 공격 경로"에 박혀 있어, 삭제가 곧 전투 경로 리팩터가 됩니다.
// 정리하려면 에디터에서 컴파일이 도는 상태로 독립 커밋으로 진행하십시오.
// ──────────────────────────────────────────────────────────────────────────
using DG.Tweening;
using UnityEngine;
using System;

public class RifleAnimation : MonoBehaviour
{
    // 외부 의존성
    [Header("Recoil Settings")]
    [SerializeField] private float recoilDistance = 0.1f;
    [SerializeField] private float recoilDuration = 0.05f;
    [SerializeField] private float returnDuration = 0.15f;
    [SerializeField] private Ease recoilEase = Ease.OutQuad;
    [SerializeField] private Ease returnEase = Ease.InQuad;

    [Header("Shake Settings")]
    [SerializeField] private float shakeStrength = 0.05f;
    [SerializeField] private int shakeVibrato = 10;
    [SerializeField] private float shakeRandomness = 90f;

    // 내부 의존성
    private Sequence recoilSequence;
    private Vector3 initialLocalPos;
    private Transform targetTransform;
    private Action onCompleteCallback;

    public void SetTarget(Transform _target)
    {
        targetTransform = _target;
    }

    public void PlayRecoil(Action _onComplete)
    {
        if (null == targetTransform || null == transform.parent) return;

        // 1. 초기 상태 및 콜백 저장
        initialLocalPos = transform.localPosition;
        onCompleteCallback = _onComplete;

        // 2. 반동 방향 계산 (현재 위치 - 타겟 위치 = 타겟의 반대 방향)
        Vector3 worldRecoilDir = (transform.position - targetTransform.position).normalized;
        
        // 3. 월드 방향을 부모 기준의 로컬 방향으로 변환하여 거리 곱함
        Vector3 localRecoilOffset = transform.parent.InverseTransformDirection(worldRecoilDir) * recoilDistance;

        KillTweens();

        recoilSequence = DOTween.Sequence();
        
        // 4. 반동(뒤로 밀림)과 셰이킹(진동)을 동시에 실행 (Join 사용)
        recoilSequence.Append(transform.DOLocalMove(initialLocalPos + localRecoilOffset, recoilDuration)
            .SetEase(recoilEase));
            
        // 미세한 떨림 추가 (동시 실행)
        recoilSequence.Join(transform.DOShakePosition(recoilDuration, shakeStrength, shakeVibrato, shakeRandomness, false, false).SetLink(gameObject));
            
        // 5. 원래 위치로 복귀
        recoilSequence.Append(transform.DOLocalMove(initialLocalPos, returnDuration)
            .SetEase(returnEase));

        // 6. 완료 시 기명 메서드 호출
        recoilSequence.OnComplete(NotifyComplete);
        recoilSequence.SetLink(gameObject);
    }

    private void NotifyComplete()
    {
        onCompleteCallback?.Invoke();
        onCompleteCallback = null;
    }

    public void KillTweens()
    {
        if (recoilSequence != null && recoilSequence.IsActive())
        {
            recoilSequence.Kill();
            // Kill 시 위치 원복 보장 (셰이크 도중 중단 시 대비)
            transform.localPosition = initialLocalPos;
        }
    }

    private void OnDestroy()
    {
        KillTweens();
    }
}
