using System;
using UnityEngine.UI;
using DG.Tweening;
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
    [SerializeField] private Slider ghostSlider;

    [Header("Ghost Bar Settings")]
    [SerializeField] private float ghostFollowDuration = 0.5f;
    [SerializeField] private float ghostDelay = 0.5f;
    [SerializeField] private bool useAccumulatedGhost = true;

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
    private Tween ghostTween;

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

        if (null != ghostSlider)
        {
            ghostSlider.minValue = 0.0f;
            ghostSlider.maxValue = 1.0f;
            ghostSlider.value = 1.0f;
        }

        rect = GetComponent<RectTransform>();
    }

    public void SetOwner(object _owner, float _initialRatio = 1.0f)
    {
        owner = _owner;

        currentValue = _initialRatio;
        
        if (null != progressSlider)
            progressSlider.value = _initialRatio;
            
        if (null != ghostSlider)
            ghostSlider.value = _initialRatio;
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

    public new void UpdateValue(float _ratio)
    {
        float _prevValue = currentValue;
        base.UpdateValue(_ratio);

        if (null == ghostSlider)
            return;

        // 회복 시 즉시 동기화
        if (_ratio > _prevValue)
        {
            if (null != ghostTween && true == ghostTween.IsActive())
                ghostTween.Kill();
            ghostSlider.value = _ratio;
            return;
        }

        if (true == useAccumulatedGhost)
        {
            float _nextDelay = ghostDelay;

            if (null != ghostTween && true == ghostTween.IsActive())
            {
                // 이미 움직이기 시작했다면(지연 시간이 끝났다면) 지연 없이 즉시 타겟 갱신 ("쭉" 닳게)
                // 아직 지연 중이라면 지연 시간을 초기화하여 축적 유지
                if (ghostTween.Elapsed(false) > 0.0f)
                    _nextDelay = 0.0f;

                ghostTween.Kill();
            }

            ghostTween = ghostSlider.DOValue(_ratio, ghostFollowDuration)
                .SetDelay(_nextDelay)
                .SetEase(Ease.OutQuad);
        }
        else
        {
            // 기존 로직: 매 피격 시 지연 시간 초기화 (뚝뚝 끊김)
            if (null != ghostTween && true == ghostTween.IsActive())
                ghostTween.Kill();

            ghostTween = ghostSlider.DOValue(_ratio, ghostFollowDuration)
                .SetDelay(ghostDelay)
                .SetEase(Ease.OutQuad);
        }
    }

    public void TriggerActive(Action<HUD_HPBar> _onFinish)
    {
        onFinishCallback = _onFinish;
        displayTimer = showDuration;
    }

    public void OnHide(float _forceDuration = -1f)
    {
        if (true == isHiding) 
            return;

        isHiding = true;

        if (null != motionPlayer)
        {
            MotionPlaySettings _newPlaySettings = MotionPlaySettings.Default;
            _newPlaySettings.onComplete = onHideCompleteAction;
            _newPlaySettings.bReset = true;

            if (0.0f < _forceDuration)
                _newPlaySettings.forceDelayBackward = _forceDuration;

            motionPlayer.PlayBackward("Show", _newPlaySettings);
        }
        else
        {
            HandleHideComplete();
        }
    }

    private void HandleHideComplete()
    {
        if (null != onFinishCallback)
            onFinishCallback.Invoke(this);
    }

    public void OnDespawn()
    {
        if (null != ghostTween && true == ghostTween.IsActive())
            ghostTween.Kill();

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
