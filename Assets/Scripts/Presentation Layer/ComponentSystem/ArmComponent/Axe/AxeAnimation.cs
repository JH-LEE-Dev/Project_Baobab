using DG.Tweening;
using UnityEngine;
using System;

public class AxeAnimation : MonoBehaviour
{
    // 외부 의존성
    [Header("Swing Settings")]
    [SerializeField] private float swingDuration = 0.05f;
    [SerializeField] private float endRotationZ = -90f;
    [SerializeField] private Ease swingEase = Ease.InCubic;

    [Header("Return Settings")]
    [SerializeField] private float returnDuration = 0.15f;
    [SerializeField] private Ease returnEase = Ease.OutQuad;

    // 내부 의존성
    // 스윙/복귀 트윈을 따로 들고 있는다. 재활용(SetRecyclable) 트윈은 죽은 뒤 다른 트윈으로 재사용되므로
    // OnKill에서 반드시 참조를 비워야 하는데, 스윙 완료 콜백 안에서 복귀 트윈이 먼저 만들어지기 때문에
    // 필드를 하나로 쓰면 스윙의 OnKill이 복귀 트윈 참조를 지워버린다.
    private Tween swingTween;
    private Tween returnTween;
    private Quaternion initialLocalRot;
    private Quaternion restLocalRot;
    private Action onSwingComplete;
    private Action onReturnComplete;

    // NotifySwingComplete/NotifyReturnComplete는 인스턴스 메서드라서 메서드 그룹을 델리게이트로
    // 넘길 때마다(OnComplete(NotifySwingComplete) 등) 매번 새 델리게이트가 할당된다.
    // 도끼 스윙마다 반복 호출되므로 한 번만 캐싱해서 재사용한다. (OnComplete은 Action이 아닌
    // DOTween의 TweenCallback 델리게이트 타입을 요구한다)
    private TweenCallback cachedNotifySwingComplete;
    private TweenCallback cachedNotifyReturnComplete;
    private TweenCallback cachedOnSwingKilled;
    private TweenCallback cachedOnReturnKilled;

    private void Awake()
    {
        restLocalRot = transform.localRotation;
        cachedNotifySwingComplete = NotifySwingComplete;
        cachedNotifyReturnComplete = NotifyReturnComplete;
        cachedOnSwingKilled = OnSwingKilled;
        cachedOnReturnKilled = OnReturnKilled;
    }

    public void PlaySwing(Action _onComplete)
    {
        // 1. 초기 회전 상태 저장
        initialLocalRot = transform.localRotation;
        onSwingComplete = _onComplete;

        KillTweens();

        // 2. 휘두르기 회전 시작
        // 스윙마다 새 Tweener를 할당하지 않도록 재활용한다. OnKill(완료 후 자동 Kill 포함)에서 참조를 비운다.
        swingTween = transform.DOLocalRotate(new Vector3(0, 0, endRotationZ), swingDuration, RotateMode.LocalAxisAdd)
            .SetEase(swingEase)
            .SetRecyclable(true)
            .OnKill(cachedOnSwingKilled)
            .OnComplete(cachedNotifySwingComplete);
    }

    public void PlayReturn(Action _onComplete)
    {
        onReturnComplete = _onComplete;

        // 3. 원래 회전으로 복귀 시작
        returnTween = transform.DOLocalRotateQuaternion(initialLocalRot, returnDuration)
            .SetEase(returnEase)
            .SetRecyclable(true)
            .OnKill(cachedOnReturnKilled)
            .OnComplete(cachedNotifyReturnComplete);
    }

    private void NotifySwingComplete()
    {
        onSwingComplete?.Invoke();
        onSwingComplete = null;
    }

    private void NotifyReturnComplete()
    {
        onReturnComplete?.Invoke();
        onReturnComplete = null;
    }

    // 주의: OnKill은 "어느 트윈이 죽었는지"를 넘겨주지 않는다. DOTween은 업데이트 루프 안(콜백 중)에서 Kill된 트윈의
    // OnKill을 루프가 끝난 뒤에 호출하므로, 같은 콜백 체인 안에서 같은 종류의 새 트윈을 만들면 뒤늦은 OnKill이 새 참조를
    // 지울 수 있다. 현재 PlaySwing은 입력/쿨다운 코루틴 꼬리에서만, PlayReturn은 스윙 완료 콜백에서만 불려 그런 경로가 없다.
    // 호출 시점을 바꿀 때는 이 전제를 함께 확인할 것.
    private void OnSwingKilled()
    {
        swingTween = null;
    }

    private void OnReturnKilled()
    {
        returnTween = null;
    }

    public void KillTweens()
    {
        if (null != swingTween && swingTween.IsActive())
            swingTween.Kill();

        if (null != returnTween && returnTween.IsActive())
            returnTween.Kill();
    }

    /// <summary>
    /// 진행 중인 트윈을 정리하고 최초(휴식) 회전 상태로 되돌립니다. (오브젝트 풀 재사용 시 사용)
    /// </summary>
    public void ResetPose()
    {
        KillTweens();
        onSwingComplete = null;
        onReturnComplete = null;
        transform.localRotation = restLocalRot;
    }

    private void OnDestroy()
    {
        KillTweens();
    }
}
