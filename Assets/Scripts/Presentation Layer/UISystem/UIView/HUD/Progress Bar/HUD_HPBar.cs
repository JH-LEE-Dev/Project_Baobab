using System;
using PresentationLayer.ObjectSystem;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// HUD에서 캐릭터의 상태를 추적하며 표시하는 HP 바입니다.
/// ObjectMotionPlayer를 통해 등장 및 퇴장 애니메이션을 처리하며 가시성을 제어합니다.
/// </summary>
public class HUD_HPBar : HUD_ProgressBar
{
    // //외부 의존성
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    // //내부 의존성
    private object owner;
    private GameObject targetObj;
    private float yOffset;
    private float showDuration;
    private float displayTimer;
    private Action<HUD_HPBar> onFinishCallback;
    private bool isHiding;
    private RectTransform rect;
    private UnityAction onHideCompleteAction;

    public object Owner => owner;

    // //퍼블릭 초기화 및 제어 메서드

    public override void Initialize()
    {
        base.Initialize();

        onHideCompleteAction = HandleHideComplete;

        if (null == motionPlayer)
            motionPlayer = GetComponent<ObjectMotionPlayer>();

        if (null != motionPlayer)
            motionPlayer.Initialize();

        rect = GetComponent<RectTransform>();
    }

    public void SetOwner(object _owner)
    {
        owner = _owner;
    }

    public void Setup(GameObject _target, float _yOffset, float _duration)
    {
        targetObj = _target;
        yOffset = _yOffset;
        showDuration = _duration;
        displayTimer = showDuration;
        
        UpdatePosition();

        if (true == isHiding || false == gameObject.activeSelf)
        {
            isHiding = false;
            gameObject.SetActive(true);
            
            if (null != motionPlayer)
                motionPlayer.Play("Show", bReset: true);
        }
    }

    public void TriggerActive(Action<HUD_HPBar> _onFinish)
    {
        onFinishCallback = _onFinish;
        displayTimer = showDuration;
    }

    public void OnHide()
    {
        if (true == isHiding) 
            return;

        isHiding = true;
        
        if (null != motionPlayer)
            motionPlayer.PlayBackward("Show", _onComplete: onHideCompleteAction, bReset: true);
        else
            HandleHideComplete();
    }

    private void HandleHideComplete()
    {
        if (null != onFinishCallback)
            onFinishCallback.Invoke(this);
    }

    public void OnDespawn()
    {
        owner = null;
        targetObj = null;
        onFinishCallback = null;
        isHiding = false;
        gameObject.SetActive(false);
    }

    // //유니티 이벤트 함수

    private void Update()
    {
        if (null == targetObj || true == isHiding)
            return;

        displayTimer -= Time.deltaTime;
        
        if (0.0f >= displayTimer)
            OnHide();
    }

    private void LateUpdate()
    {
        UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (null == targetObj || null == rect)
            return;

        Vector3 _pos = targetObj.transform.position;
        _pos.y += yOffset;
        
        rect.position = _pos;
    }
}
