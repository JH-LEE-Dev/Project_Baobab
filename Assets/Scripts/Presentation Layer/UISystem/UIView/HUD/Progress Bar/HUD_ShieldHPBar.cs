using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

/// <summary>
/// HUD에서 캐릭터의 상태를 추적하며 표시하는 쉴드 기능이 결합된 HP 바입니다.
/// </summary>
public class HUD_ShieldHPBar : HUD_ProgressBar
{
    // 외부 의존성
    [SerializeField] private Slider ghostSlider;
    [SerializeField] private Slider shieldSlider;
    [SerializeField] private Slider shieldGhostSlider;
    [SerializeField] private CanvasGroup shieldCanvasGroup;
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    [Header("Ghost Bar Settings")]
    [SerializeField] private float ghostFollowDuration = 0.5f;
    [SerializeField] private float ghostDelay = 0.5f;
    [SerializeField] private bool useAccumulatedGhost = true;
    [SerializeField] private bool resetDelayOnHit = false;
    [SerializeField] private float shieldFadeDuration = 0.3f;

    // 내부 의존성
    private object owner;
    private GameObject targetObj;
    private float yOffset;
    private float showDuration;
    private Tween hideDelayTween;
    private Action<HUD_ShieldHPBar> onFinishCallback;
    private bool isHiding;
    private RectTransform rect;
    private UnityAction onHideCompleteAction;
    private Tween hpGhostTween;
    private Tween shieldGhostTween;
    
    private float currentHpValue = 0.0f;
    private float currentShieldValue = 0.0f;
    private bool useShield = false;
    private bool isShieldFadedOut = false;
    private Tween shieldFadeTween;

    public object Owner => owner;


    // 퍼블릭 초기화 및 제어 메서드

    public override void Initialize()
    {
        base.Initialize();

        onHideCompleteAction = HandleHideComplete;

        if (null != motionPlayer)
            motionPlayer.Initialize();

        if (null != ghostSlider)
        {
            ghostSlider.minValue = 0.0f;
            ghostSlider.maxValue = 1.0f;
            ghostSlider.value = 1.0f;
        }

        if (null != shieldSlider)
        {
            shieldSlider.minValue = 0.0f;
            shieldSlider.maxValue = 1.0f;
            shieldSlider.value = 0.0f;
            shieldSlider.gameObject.SetActive(false);
        }

        if (null != shieldGhostSlider)
        {
            shieldGhostSlider.minValue = 0.0f;
            shieldGhostSlider.maxValue = 1.0f;
            shieldGhostSlider.value = 0.0f;
            shieldGhostSlider.gameObject.SetActive(false);
        }

        if (null != shieldCanvasGroup)
            shieldCanvasGroup.alpha = 0.0f;

        isShieldFadedOut = true;

        rect = GetComponent<RectTransform>();
    }

    public void SetOwner(object _owner, float _initialHpRatio = 1.0f, float _initialShieldRatio = 0.0f, bool _useShield = false)
    {
        owner = _owner;
        useShield = _useShield;

        currentHpValue = _initialHpRatio;
        currentShieldValue = _initialShieldRatio;

        if (null != progressSlider)
            progressSlider.value = _initialHpRatio;

        if (null != ghostSlider)
            ghostSlider.value = _initialHpRatio;

        if (null != shieldFadeTween && true == shieldFadeTween.IsActive())
        {
            shieldFadeTween.Kill();
            shieldFadeTween = null;
        }

        if (null != shieldSlider)
            shieldSlider.gameObject.SetActive(_useShield);

        if (null != shieldGhostSlider)
            shieldGhostSlider.gameObject.SetActive(_useShield);

        if (true == _useShield)
        {
            if (null != shieldSlider)
                shieldSlider.value = _initialShieldRatio;

            if (null != shieldGhostSlider)
                shieldGhostSlider.value = _initialShieldRatio;

            if (0.0f < _initialShieldRatio)
            {
                if (null != shieldCanvasGroup)
                    shieldCanvasGroup.alpha = 1.0f;
                isShieldFadedOut = false;
            }
            else
            {
                if (null != shieldCanvasGroup)
                    shieldCanvasGroup.alpha = 0.0f;
                isShieldFadedOut = true;
            }
        }
        else
        {
            if (null != shieldCanvasGroup)
                shieldCanvasGroup.alpha = 0.0f;
            isShieldFadedOut = true;
        }
    }

    public void Setup(GameObject _target, float _yOffset, float _duration)
    {
        targetObj = _target;
        yOffset = _yOffset;
        showDuration = _duration;
        RestartHideTimer();

        UpdatePosition();

        if (true == isHiding || false == gameObject.activeSelf)
        {
            isHiding = false;
            gameObject.SetActive(true);

            if (null != motionPlayer)
                motionPlayer.Play("Show", bReset: true);
        }
    }

    public void UpdateValues(float _hpRatio, float _shieldRatio)
    {
        float _prevHp = currentHpValue;
        float _prevShield = currentShieldValue;

        currentHpValue = _hpRatio;
        currentShieldValue = _shieldRatio;

        if (null != progressSlider)
            progressSlider.value = _hpRatio;

        if (null != ghostSlider)
        {
            if (_hpRatio > _prevHp)
            {
                if (null != hpGhostTween && true == hpGhostTween.IsActive())
                {
                    hpGhostTween.Kill();
                    hpGhostTween = null;
                }
                ghostSlider.value = _hpRatio;
            }
            else if (_hpRatio < _prevHp)
            {
                float _nextHpDelay = ghostDelay;
                if (null != hpGhostTween && true == hpGhostTween.IsActive())
                {
                    if (true == useAccumulatedGhost && false == resetDelayOnHit)
                    {
                        if (0.0f < hpGhostTween.Elapsed(false))
                            _nextHpDelay = 0.0f;
                    }
                    hpGhostTween.Kill();
                    hpGhostTween = null;
                }

                hpGhostTween = ghostSlider.DOValue(_hpRatio, ghostFollowDuration)
                    .SetDelay(_nextHpDelay)
                    .SetEase(Ease.OutQuad);
            }
        }

        if (true == useShield)
        {
            if (null != shieldSlider)
                shieldSlider.value = _shieldRatio;

            if (null != shieldGhostSlider)
            {
                if (_shieldRatio > _prevShield)
                {
                    if (null != shieldGhostTween && true == shieldGhostTween.IsActive())
                    {
                        shieldGhostTween.Kill();
                        shieldGhostTween = null;
                    }
                    shieldGhostSlider.value = _shieldRatio;
                }
                else if (_shieldRatio < _prevShield)
                {
                    float _nextShieldDelay = ghostDelay;
                    if (null != shieldGhostTween && true == shieldGhostTween.IsActive())
                    {
                        if (true == useAccumulatedGhost && false == resetDelayOnHit)
                        {
                            if (0.0f < shieldGhostTween.Elapsed(false))
                                _nextShieldDelay = 0.0f;
                        }
                        shieldGhostTween.Kill();
                        shieldGhostTween = null;
                    }

                    shieldGhostTween = shieldGhostSlider.DOValue(_shieldRatio, ghostFollowDuration)
                        .SetDelay(_nextShieldDelay)
                        .SetEase(Ease.OutQuad);
                }
            }

            if (0.0f >= _shieldRatio)
            {
                if (false == isShieldFadedOut)
                {
                    isShieldFadedOut = true;
                    if (null != shieldFadeTween && true == shieldFadeTween.IsActive())
                    {
                        shieldFadeTween.Kill();
                        shieldFadeTween = null;
                    }

                    if (null != shieldCanvasGroup)
                    {
                        shieldFadeTween = shieldCanvasGroup.DOFade(0.0f, shieldFadeDuration)
                            .SetEase(Ease.OutQuad);
                    }
                }
            }
            else
            {
                if (true == isShieldFadedOut)
                {
                    isShieldFadedOut = false;
                    if (null != shieldFadeTween && true == shieldFadeTween.IsActive())
                    {
                        shieldFadeTween.Kill();
                        shieldFadeTween = null;
                    }

                    if (null != shieldCanvasGroup)
                    {
                        shieldFadeTween = shieldCanvasGroup.DOFade(1.0f, shieldFadeDuration)
                            .SetEase(Ease.OutQuad);
                    }
                }
            }
        }
    }

    public void TriggerActive(Action<HUD_ShieldHPBar> _onFinish)
    {
        onFinishCallback = _onFinish;
        RestartHideTimer();
    }

    private void RestartHideTimer()
    {
        if (null != hideDelayTween && true == hideDelayTween.IsActive())
            hideDelayTween.Kill();

        if (0.0f < showDuration)
            hideDelayTween = DOVirtual.DelayedCall(showDuration, () => OnHide(-1f), false);
    }

    public void OnHide(float _forceDuration = -1f, bool _bSkip = false)
    {
        if (true == isHiding)
            return;

        isHiding = true;

        if (null != motionPlayer)
        {
            MotionPlaySettings _newPlaySettings = MotionPlaySettings.Default;
            _newPlaySettings.onComplete = onHideCompleteAction;
            _newPlaySettings.bReset = true;
            _newPlaySettings.skip = _bSkip;

            if (0.0f < _forceDuration)
                _newPlaySettings.forceDelayBackward = _forceDuration;

            motionPlayer.PlayBackward("Show", _newPlaySettings);
        }
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
        if (null != hpGhostTween && true == hpGhostTween.IsActive())
        {
            hpGhostTween.Kill();
            hpGhostTween = null;
        }

        if (null != shieldGhostTween && true == shieldGhostTween.IsActive())
        {
            shieldGhostTween.Kill();
            shieldGhostTween = null;
        }

        if (null != shieldFadeTween && true == shieldFadeTween.IsActive())
        {
            shieldFadeTween.Kill();
            shieldFadeTween = null;
        }

        if (null != hideDelayTween && true == hideDelayTween.IsActive())
        {
            hideDelayTween.Kill();
        }

        if (null != shieldCanvasGroup)
            shieldCanvasGroup.alpha = 0.0f;

        isShieldFadedOut = true;

        owner = null;
        targetObj = null;
        onFinishCallback = null;
        isHiding = false;
        gameObject.SetActive(false);
    }


    // 유니티 이벤트 함수

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
