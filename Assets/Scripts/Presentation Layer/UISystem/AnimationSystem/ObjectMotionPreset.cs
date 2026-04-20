using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(fileName = "New UI Sequence Preset", menuName = "UI/Motion Sequence Preset")]
public class ObjectMotionPreset : ScriptableObject
{
    public List<ObjectMotionData> motions = new List<ObjectMotionData>();

    public Sequence GenerateSequence(RectTransform targetRect, CanvasGroup targetCanvasGroup, ObjectMotionPlayer player)
    {
        Sequence seq = DOTween.Sequence();

        foreach (var motion in motions)
        {
            Tween tween = CreateTween(motion, targetRect, targetCanvasGroup);
            if (tween == null) 
                continue;

            // 콜백 연결
            if (!string.IsNullOrEmpty(motion.startEventKey))
                tween.OnStart(() => player.InvokeEvent(motion.startEventKey, motion.startParam));

            if (!string.IsNullOrEmpty(motion.completeEventKey))
                tween.OnComplete(() => player.InvokeEvent(motion.completeEventKey, motion.completeParam));

            tween.SetDelay(motion.delay).SetEase(motion.ease);

            if (ObjectMotionData.SequenceType.Append == motion.sequenceType)
                seq.Append(tween);
            else
                seq.Join(tween);
        }

        return seq;
    }

    private Tween CreateTween(ObjectMotionData data, RectTransform rect, CanvasGroup cg)
    {
        switch (data.motionType)
        {
            case ObjectMotionData.MotionType.MoveAnchored:
                return rect.DOAnchorPos(data.targetVector, data.duration).SetRelative(data.isRelative);
            case ObjectMotionData.MotionType.Scale:
                return rect.DOScale(data.targetVector, data.duration).SetRelative(data.isRelative);
            case ObjectMotionData.MotionType.Fade:
                return cg != null ? cg.DOFade(data.targetFloat, data.duration).SetRelative(data.isRelative) : null;
            case ObjectMotionData.MotionType.Rotate:
                return rect.DORotate(data.targetVector, data.duration).SetRelative(data.isRelative);
            default:
                return null;
        }
    }
}
