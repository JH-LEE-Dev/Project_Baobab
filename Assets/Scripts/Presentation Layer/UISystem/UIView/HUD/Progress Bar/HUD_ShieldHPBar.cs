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
    private Tween shieldRecoveryTween;
    
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

        if (null != shieldRecoveryTween && true == shieldRecoveryTween.IsActive())
        {
            shieldRecoveryTween.Kill();
            shieldRecoveryTween = null;
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

            if (null != ghostSlider)
                ghostSlider.gameObject.SetActive(true);
                
            if (null != shieldGhostSlider && true == useShield)
                shieldGhostSlider.gameObject.SetActive(true);

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
            if (_shieldRatio < _prevShield) // 쉴드 감소 (데미지)
            {
                // 실제 쉴드 바는 즉시 깎임
                if (null != shieldSlider)
                {
                    if (null != shieldRecoveryTween && true == shieldRecoveryTween.IsActive())
                    {
                        shieldRecoveryTween.Kill();
                        shieldRecoveryTween = null;
                    }
                    shieldSlider.value = _shieldRatio;
                }

                // 고스트 바는 딜레이 후 천천히 깎임
                if (null != shieldGhostSlider)
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
            else if (_shieldRatio > _prevShield) // 쉴드 회복
            {
                // 고스트 바는 즉시 목표치로 증가 (회복 예정량 시각화)
                if (null != shieldGhostSlider)
                {
                    if (null != shieldGhostTween && true == shieldGhostTween.IsActive())
                    {
                        shieldGhostTween.Kill();
                        shieldGhostTween = null;
                    }
                    shieldGhostSlider.value = _shieldRatio;
                }

                // 실제 쉴드 바는 딜레이 후 천천히 증가 (역방향 고스트 연출)
                if (null != shieldSlider)
                {
                    float _nextShieldDelay = ghostDelay;
                    if (null != shieldRecoveryTween && true == shieldRecoveryTween.IsActive())
                    {
                        if (true == useAccumulatedGhost && false == resetDelayOnHit)
                        {
                            if (0.0f < shieldRecoveryTween.Elapsed(false))
                                _nextShieldDelay = 0.0f;
                        }
                        shieldRecoveryTween.Kill();
                        shieldRecoveryTween = null;
                    }

                    shieldRecoveryTween = shieldSlider.DOValue(_shieldRatio, ghostFollowDuration)
                        .SetDelay(_nextShieldDelay)
                        .SetEase(Ease.OutQuad);
                }
            }
            else // 변화 없음
            {
                if (null != shieldSlider)
                    shieldSlider.value = _shieldRatio;

                if (null != shieldGhostSlider)
                    shieldGhostSlider.value = _shieldRatio;
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

        // 페이드아웃(은닉) 시작 시 고스트 바가 천천히 줄어드는 연출을 강제로 멈추고 고스트 바 오브젝트를 즉시 숨김
        if (null != hpGhostTween && true == hpGhostTween.IsActive())
        {
            hpGhostTween.Kill();
            hpGhostTween = null;
        }
        
        if (null != ghostSlider)
        {
            ghostSlider.value = currentHpValue;
            ghostSlider.gameObject.SetActive(false);
        }

        if (null != shieldGhostTween && true == shieldGhostTween.IsActive())
        {
            shieldGhostTween.Kill();
            shieldGhostTween = null;
        }
        
        if (null != shieldGhostSlider)
        {
            shieldGhostSlider.value = currentShieldValue;
            shieldGhostSlider.gameObject.SetActive(false);
        }

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
