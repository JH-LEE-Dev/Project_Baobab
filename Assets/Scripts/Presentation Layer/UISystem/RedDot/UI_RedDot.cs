using UnityEngine;
using DG.Tweening;

public class UI_RedDot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform targetVisual;

    [Header("Animation Settings")]
    [SerializeField] private float appearDuration = 0.4f;
    [SerializeField] private float idleDuration = 3f;
    [SerializeField] private float shakeDuration = 0.4f;
    [SerializeField] private Vector3 shakePosStrength = new Vector3(3f, 3f, 0f);
    [SerializeField] private Vector3 shakeRotStrength = new Vector3(0f, 0f, 5f);
    [SerializeField] private int shakeVibrato = 50;

    private Sequence dotweenSeq;

    /// <summary>
    /// 레드닷 알림을 활성화하고 애니메이션을 시작합니다.
    /// (예: 새로운 아이템 획득 시 호출)
    /// </summary>
    public void Activate()
    {
        gameObject.SetActive(true);
        PlayAnimation();
    }

    /// <summary>
    /// 레드닷 알림을 비활성화(대기 모드)하고 애니메이션을 정지합니다.
    /// (예: 유저가 해당 UI를 확인/클릭했을 때 호출)
    /// </summary>
    public void Deactivate()
    {
        KillAnimation();
        gameObject.SetActive(false);
    }

    private void PlayAnimation()
    {
        if (targetVisual == null)
        {
            Debug.LogWarning("[UI_RedDot] 타겟 비주얼이 할당되지 않았습니다. 인스펙터에서 타겟 이미지를 바인딩해주세요.", this);
            return;
        }

        KillAnimation();

        // 초기화
        targetVisual.localScale = Vector3.zero;
        targetVisual.localPosition = Vector3.zero;
        targetVisual.localEulerAngles = Vector3.zero;

        dotweenSeq = DOTween.Sequence();
        
        // 1. 등장 연출 (뽁 하고 커짐)
        dotweenSeq.Append(targetVisual.DOScale(1f, appearDuration).SetEase(Ease.OutBack));
        
        // 2. 무한 반복될 대기 -> 미세 진동(핸드폰 진동) 시퀀스
        Sequence loopSeq = DOTween.Sequence();
        loopSeq.AppendInterval(idleDuration);
        
        loopSeq.Append(targetVisual.DOShakePosition(shakeDuration, shakePosStrength, shakeVibrato, 90f, snapping: false, fadeOut: true));
        loopSeq.Join(targetVisual.DOShakeRotation(shakeDuration, shakeRotStrength, shakeVibrato, 90f, fadeOut: true));
        
        loopSeq.SetLoops(-1);

        // 메인 시퀀스에 루프 시퀀스를 연결
        dotweenSeq.Append(loopSeq);
    }

    private void KillAnimation()
    {
        if (dotweenSeq != null)
        {
            dotweenSeq.Kill();
            dotweenSeq = null;
        }
        
        // 트랜스폼 롤백
        if (targetVisual != null)
        {
            targetVisual.localScale = Vector3.one;
            targetVisual.localPosition = Vector3.zero;
            targetVisual.localEulerAngles = Vector3.zero;
        }
    }

    private void OnDisable()
    {
        KillAnimation();
    }

    private void OnDestroy()
    {
        KillAnimation();
    }
}
