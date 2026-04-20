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
        public StringEvent action;
    }

    public ObjectMotionPreset motionPreset;
    public List<MotionEvent> eventLibrary = new List<MotionEvent>();

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
        currentSequence?.Kill();
  
        currentSequence = motionPreset.GenerateSequence(rectTransform, canvasGroup, this);
        currentSequence.Play();
    }

    public void InvokeEvent(string key, string param)
    {
        if (string.IsNullOrEmpty(key)) 
            return;
        
        var entry = eventLibrary.Find(x => x.eventKey == key);

        if (null != entry.action)
            entry.action.Invoke(param); // 리스너 함수에 인자 전달
    }
}
