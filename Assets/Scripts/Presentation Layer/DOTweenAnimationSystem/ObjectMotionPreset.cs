using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

[CreateAssetMenu(fileName = "New Sequence Preset", menuName = "ScriptableObjects/Motion Sequence Preset")]
public class ObjectMotionPreset : ScriptableObject
{
    // 외부 의존성
    public List<ObjectMotionData> motions = new List<ObjectMotionData>();

    public Sequence GenerateSequence(RectTransform _targetRect, CanvasGroup _targetCanvasGroup, ObjectMotionPlayer _player)
    {
        Sequence seq = DOTween.Sequence();

        for (int i = 0; i < motions.Count; i++)
        {
            ObjectMotionData motion = motions[i];
            Tween tween = CreateTween(motion, _targetRect, _targetCanvasGroup);
            if (tween == null) 
                continue;

            // 콜백 연결
            if (!string.IsNullOrEmpty(motion.startEventKey))
                tween.OnStart(() => _player.InvokeEvent(motion.startEventKey, motion.startParam));

            if (!string.IsNullOrEmpty(motion.completeEventKey))
                tween.OnComplete(() => _player.InvokeEvent(motion.completeEventKey, motion.completeParam));

            tween.SetDelay(motion.delay).SetEase(motion.ease);

            if (ObjectMotionData.SequenceType.Append == motion.sequenceType)
                seq.Append(tween);
            else
                seq.Join(tween);
        }

        return seq;
    }

    private Tween CreateTween(ObjectMotionData _data, RectTransform _rect, CanvasGroup _cg)
    {
        switch (_data.motionType)
        {
            case ObjectMotionData.MotionType.MoveAnchored:
                return _rect.DOAnchorPos(_data.targetVector, _data.duration).SetRelative(_data.isRelative);
            case ObjectMotionData.MotionType.MovePosition:
                return _rect.DOMove(_data.targetVector, _data.duration).SetRelative(_data.isRelative);
            case ObjectMotionData.MotionType.Scale:
                return _rect.DOScale(_data.targetVector, _data.duration).SetRelative(_data.isRelative);
            case ObjectMotionData.MotionType.Fade:
                return _cg != null ? _cg.DOFade(_data.targetFloat, _data.duration).SetRelative(_data.isRelative) : null;
            case ObjectMotionData.MotionType.Rotate:
                return _rect.DORotate(_data.targetVector, _data.duration).SetRelative(_data.isRelative);
            default:
                return null;
        }
    }
}