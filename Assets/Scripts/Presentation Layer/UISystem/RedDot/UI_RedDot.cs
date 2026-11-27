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
    [SerializeField] private Vector3 shakeRotStrength = new Vector3(0f, 0f, 15f);
    [SerializeField] private int shakeVibrato = 40;

    private Sequence dotweenSeq;
    // targetVisual.DOScale 대신 쓰는 델리게이트 캐시(TransformScaleTweener 참조)
    private TransformScaleTweener scaleTweener;
    private TweenCallback cachedOnAppearComplete;

    private void Awake()
    {
        cachedOnAppearComplete = StartIdleLoopSequence;
    }

    /// <summary>
    /// 레드닷 알림을 활성화하고 애니메이션을 시작합니다.
    /// (예: 새로운 아이템 획득 시 호출)
    /// </summary>
    public void Activate()
    {
        bool wasActive = gameObject.activeSelf;
        gameObject.SetActive(true);
        PlayAnimation(wasActive);
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

    private void PlayAnimation(bool wasActive)
    {
        if (null == targetVisual)
        {
            Debug.LogWarning("[UI_RedDot] 타겟 비주얼이 할당되지 않았습니다. 인스펙터에서 타겟 이미지를 바인딩해주세요.", this);
            return;
        }

        KillAnimation();

        // 초기화
        targetVisual.localPosition = Vector3.zero;
        targetVisual.localEulerAngles = Vector3.zero;

        // 등장·뽀잉 연출의 시퀀스와 크기 트윈은 재활용(SetRecyclable)한다. 원목을 주울 때마다 새로 만들던 시퀀스·트윈과
        // DOScale의 클로저·델리게이트가 쓰레기로 쌓였다. 만드는 연출은 예전과 같다(DOScale(1f, 시간) 본문과 같은 트윈).
        // 재활용 트윈은 죽은 뒤 다른 곳의 트윈으로 다시 쓰이므로 id(this)를 달아 두고, 끊을 때 아직 이 점의 것인지 확인한다(KillAnimation).
        dotweenSeq = DOTween.Sequence().SetLink(gameObject).SetId(this).SetRecyclable(true);

        if (true == wasActive)
        {
            // 이미 켜진 상태에서 추가 신호: 뽀잉 연출
            targetVisual.localScale = new Vector3(0.7f, 0.7f, 0.7f);
            dotweenSeq.Append(TransformScaleTweener.DOScale(ref scaleTweener, targetVisual, Vector3.one, 0.2f).SetRecyclable(true).SetEase(Ease.OutBack));
        }
        else
        {
            // 최초 등장 연출 (뽁 하고 커짐)
            targetVisual.localScale = Vector3.zero;
            dotweenSeq.Append(TransformScaleTweener.DOScale(ref scaleTweener, targetVisual, Vector3.one, appearDuration).SetRecyclable(true).SetEase(Ease.OutBack));
        }
        
        // DOTween은 시퀀스 내부에 무한 반복(-1) 요소를 Append하는 것을 허용하지 않습니다.
        // 따라서 첫 등장 연출이 끝난 직후(OnComplete)에 무한 반복 시퀀스로 덮어씌워 재생합니다.
        dotweenSeq.OnComplete(cachedOnAppearComplete);
    }

    private void StartIdleLoopSequence()
    {
        if (null == targetVisual) return;

        dotweenSeq = DOTween.Sequence().SetLink(gameObject).SetId(this);
        dotweenSeq.AppendInterval(idleDuration);
        dotweenSeq.Append(targetVisual.DOShakeRotation(shakeDuration, shakeRotStrength, shakeVibrato, 90f, fadeOut: true));
        dotweenSeq.SetLoops(-1);
    }

    private void KillAnimation()
    {
        if (null != dotweenSeq)
        {
            // 이미 죽어 다른 곳에서 다시 쓰이는 트윈이면 건드리지 않는다(죽은 트윈의 Kill은 원래도 아무 일도 하지 않았다).
            if (true == dotweenSeq.IsActive() && true == ReferenceEquals(dotweenSeq.id, this))
                dotweenSeq.Kill();
            dotweenSeq = null;
        }
        
        // 트랜스폼 롤백
        if (null != targetVisual)
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
