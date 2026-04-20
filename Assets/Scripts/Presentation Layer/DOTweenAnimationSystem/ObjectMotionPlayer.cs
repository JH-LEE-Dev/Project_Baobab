using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

[Serializable]
public class StringEvent : UnityEvent<string> { }

public class ObjectMotionPlayer : MonoBehaviour
{
    [Serializable]
    public struct MotionEvent
    {
        public string eventKey;
        public UnityEvent simpleAction;  // 인수 없는 단순 실행용
        public StringEvent stringAction; // string 인수를 사용하는 실행용
    }

    // 외부 의존성
    [SerializeField] public ObjectMotionPreset motionPreset;
    [SerializeField] public List<MotionEvent> eventLibrary = new List<MotionEvent>();

    // 내부 의존성
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Sequence currentSequence;

    public void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    public void PlayMotion()
    {
        if (motionPreset == null)
            return;

        currentSequence?.Kill();
        
        currentSequence = motionPreset.GenerateSequence(rectTransform, canvasGroup, this);
        currentSequence.Play();
    }

    public void InvokeEvent(string _key, string _param)
    {
        if (string.IsNullOrEmpty(_key)) 
            return;

        for (int i = 0; i < eventLibrary.Count; i++)
        {
            if (_key == eventLibrary[i].eventKey)
            {
                if (eventLibrary[i].simpleAction != null)
                {
                    eventLibrary[i].simpleAction.Invoke();
                }

                if (eventLibrary[i].stringAction != null)
                {
                    eventLibrary[i].stringAction.Invoke(_param);
                }
                break;
            }
        }
    }

    private void OnDestroy()
    {
        currentSequence?.Kill();
    }
}