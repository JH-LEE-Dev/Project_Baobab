using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class UI_AnimationController : MonoBehaviour
{
    [SerializeField] private bool loop = false;

    [SerializeField] private List<UI_AnimationBase> animations;

    private Sequence seq;
    private TweenCallback cachedCompletedCallback;
    private TweenCallback outCallback;

    public void Initialize()
    {
        cachedCompletedCallback = Complete;

        foreach (UI_AnimationBase target in animations)
        {
            target.Initialize();
        }
    }

    public void Play(TweenCallback _finishCallback)
    {
        CheckTweening(seq);
        seq = DOTween.Sequence();

        outCallback = _finishCallback;

        foreach (UI_AnimationBase target in animations)
        {
            if (Mathf.Epsilon < target.waitForSec)
                seq.AppendInterval(target.waitForSec);

            if (!target.joinAnimation)
                seq.Append(target.PlayEnter());
            else
                seq.Join(target.PlayEnter());
        }

        if (loop)
            seq.SetLoops(-1);

        seq.OnComplete(cachedCompletedCallback);
    }

    private void Complete()
    {
        CheckTweening(seq);

        foreach (UI_AnimationBase target in animations)
        {
            target.ResetState();
        }

        outCallback.Invoke();
    }

    private void CheckTweening(Sequence _seq)
    {
        if (null != _seq && true == _seq.active)
            _seq.Kill();
    }
}
