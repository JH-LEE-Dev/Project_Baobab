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

    [Header("Gem Tree Sprites")]
    [SerializeField] private Sprite normalHpBarSprite;
    [SerializeField] private Sprite goldHpBarSprite;
    [SerializeField] private Sprite diamondHpBarSprite;
    [SerializeField] private Sprite rainbowHpBarSprite;
    [SerializeField] private Image hpFillImage;

    [Header("Gem Tree Ghost Sprites")]
    [SerializeField] private Sprite normalGhostBarSprite;
    [SerializeField] private Sprite goldGhostBarSprite;
    [SerializeField] private Sprite diamondGhostBarSprite;
    [SerializeField] private Sprite rainbowGhostBarSprite;
    [SerializeField] private Image hpGhostFillImage;

    [Header("Revival Animation Settings")]
    [SerializeField] private float revivalDuration = 0.35f;
    [SerializeField] private Ease revivalEase = Ease.OutCubic;
    [SerializeField] private float revivalShakeDuration = 1.0f;
    [SerializeField] private float revivalShakeStrength = 0.18f;

    // 내부 의존성
    private object owner;
    private GameObject targetObj;
    private float yOffset;
    private float showDuration;
    // 숨김 타이머: 맞을 때마다 트윈을 지우고 다시 만드는 대신 만료 시각만 갱신하고 LateUpdate에서 비교한다.
    private float hideAtTime;
    private bool bHideTimerActive;
    private Action<HUD_ShieldHPBar> onFinishCallback;
    private bool isHiding;
    private RectTransform rect;
    private CanvasGroup rootCanvasGroup;
    private UnityAction onHideCompleteAction;
    private Tween hpGhostTween;
    private Tween shieldGhostTween;
    private Tween shieldRecoveryTween;
    // Slider.DOValue 대신 쓰는 슬라이더별 델리게이트 캐시(SliderValueTweener 참조)
    private SliderValueTweener ghostValueTweener;
    private SliderValueTweener shieldGhostValueTweener;
    private SliderValueTweener shieldValueTweener;

    // 고스트/포자막 바의 보이기·숨기기는 오브젝트 SetActive가 아니라 그 아래 그래픽의 enabled로 한다.
    // Slider는 OnEnable/OnDisable마다 참조 갱신·비주얼 갱신·레이아웃 재구성을 하므로, 과열 충격파로 수백 개 바가
    // 한 프레임에 켜지고 꺼지면 그 비용이 그대로 쌓였다. 그래픽 enabled 토글은 그 그래픽의 메쉬만 넣고 뺀다.
    private Graphic[] ghostGraphics;
    private Graphic[] shieldGraphics;
    private Graphic[] shieldGhostGraphics;
    
    private float currentHpValue = 0.0f;
    private float currentShieldValue = 0.0f;
    private bool useShield = false;
    private bool isShieldFadedOut = false;
    private Tween shieldFadeTween;

    private bool isSpecialRecovering = false;
    private Tween specialRecoveryTween;
    private Tween specialShakeTween;
    private Vector3 shakeOffset = Vector3.zero;
    private int currentGemStage = 0;
    private int lastRevivalFrame = -1;

    private TweenCallback<float> cachedSpecialRecoveryUpdate;
    private TweenCallback cachedSpecialRecoveryComplete;
    private TweenCallback<float> cachedSpecialShakeUpdate;
    private TweenCallback cachedSpecialShakeComplete;

    public object Owner => owner;
    public bool IsSpecialRecovering => isSpecialRecovering;
    public int LastRevivalFrame => lastRevivalFrame;


    // 퍼블릭 초기화 및 제어 메서드

    public override void Initialize()
    {
        base.Initialize();

        onHideCompleteAction = HandleHideComplete;
        cachedSpecialRecoveryUpdate = HandleSpecialRecoveryUpdate;
        cachedSpecialRecoveryComplete = HandleSpecialRecoveryComplete;
        cachedSpecialShakeUpdate = HandleSpecialShakeUpdate;
        cachedSpecialShakeComplete = HandleSpecialShakeComplete;

        if (null != motionPlayer)
            motionPlayer.Initialize();

        // 세 슬라이더의 오브젝트는 여기서 한 번 켜 두고 이후로는 끄지 않는다. 보이기/숨기기는 그래픽 enabled로만 한다.
        ghostGraphics = CollectGraphics(ghostSlider);
        shieldGraphics = CollectGraphics(shieldSlider);
        shieldGhostGraphics = CollectGraphics(shieldGhostSlider);

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
            SetGraphicsEnabled(shieldGraphics, false);
        }

        if (null != shieldGhostSlider)
        {
            shieldGhostSlider.minValue = 0.0f;
            shieldGhostSlider.maxValue = 1.0f;
            shieldGhostSlider.value = 0.0f;
            SetGraphicsEnabled(shieldGhostGraphics, false);
        }

        if (null != shieldCanvasGroup)
            shieldCanvasGroup.alpha = 0.0f;

        isShieldFadedOut = true;

        rect = GetComponent<RectTransform>();
        rootCanvasGroup = GetComponent<CanvasGroup>();

        if (null == hpFillImage && null != progressSlider && null != progressSlider.fillRect)
            hpFillImage = progressSlider.fillRect.GetComponent<Image>();

        if (null == hpGhostFillImage && null != ghostSlider && null != ghostSlider.fillRect)
            hpGhostFillImage = ghostSlider.fillRect.GetComponent<Image>();
    }

    public void SetOwner(object _owner, float _initialHpRatio = 1.0f, float _initialShieldRatio = 0.0f, bool _useShield = false)
    {
        owner = _owner;
        useShield = _useShield;

        currentHpValue = _initialHpRatio;
        currentShieldValue = _initialShieldRatio;

        if (null != specialRecoveryTween && true == specialRecoveryTween.IsActive())
        {
            specialRecoveryTween.Kill();
            specialRecoveryTween = null;
        }

        if (null != specialShakeTween && true == specialShakeTween.IsActive())
        {
            specialShakeTween.Kill();
            specialShakeTween = null;
        }

        shakeOffset = Vector3.zero;
        isSpecialRecovering = false;

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

        SetGraphicsEnabled(shieldGraphics, _useShield);
        SetGraphicsEnabled(shieldGhostGraphics, _useShield);

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

    public void SetGradeByGemStage(int _gemStage)
    {
        currentGemStage = _gemStage;

        if (null == hpFillImage && null != progressSlider && null != progressSlider.fillRect)
            hpFillImage = progressSlider.fillRect.GetComponent<Image>();

        if (null == hpGhostFillImage && null != ghostSlider && null != ghostSlider.fillRect)
            hpGhostFillImage = ghostSlider.fillRect.GetComponent<Image>();

        switch (_gemStage)
        {
            case 1:
                if (null != hpFillImage && null != goldHpBarSprite)
                    hpFillImage.sprite = goldHpBarSprite;
                if (null != hpGhostFillImage && null != goldGhostBarSprite)
                    hpGhostFillImage.sprite = goldGhostBarSprite;
                break;
            case 2:
                if (null != hpFillImage && null != diamondHpBarSprite)
                    hpFillImage.sprite = diamondHpBarSprite;
                if (null != hpGhostFillImage && null != diamondGhostBarSprite)
                    hpGhostFillImage.sprite = diamondGhostBarSprite;
                break;
            case 3:
                if (null != hpFillImage && null != rainbowHpBarSprite)
                    hpFillImage.sprite = rainbowHpBarSprite;
                if (null != hpGhostFillImage && null != rainbowGhostBarSprite)
                    hpGhostFillImage.sprite = rainbowGhostBarSprite;
                break;
            default:
                if (null != hpFillImage && null != normalHpBarSprite)
                    hpFillImage.sprite = normalHpBarSprite;
                if (null != hpGhostFillImage && null != normalGhostBarSprite)
                    hpGhostFillImage.sprite = normalGhostBarSprite;
                break;
        }
    }

    public void PlayTreeRevivalPresentation(int _gemStage, float _targetHpRatio = 1.0f)
    {
        lastRevivalFrame = Time.frameCount;
        SetGradeByGemStage(_gemStage);

        if (null != specialRecoveryTween && true == specialRecoveryTween.IsActive())
        {
            specialRecoveryTween.Kill();
            specialRecoveryTween = null;
        }

        if (null != hpGhostTween && true == hpGhostTween.IsActive())
        {
            hpGhostTween.Kill();
            hpGhostTween = null;
        }

        currentHpValue = 0.0f;
        if (null != progressSlider)
            progressSlider.value = 0.0f;

        if (null != ghostSlider)
            ghostSlider.value = 0.0f;

        isSpecialRecovering = true;

        if (null != rootCanvasGroup)
            rootCanvasGroup.alpha = 1.0f;

        RestartHideTimer();

        float _target = Mathf.Clamp01(_targetHpRatio);
        specialRecoveryTween = DOVirtual.Float(0.0f, _target, revivalDuration, cachedSpecialRecoveryUpdate)
            .SetEase(revivalEase)
            .SetLink(gameObject)
            .OnComplete(cachedSpecialRecoveryComplete);

        if (null != specialShakeTween && true == specialShakeTween.IsActive())
        {
            specialShakeTween.Kill();
            specialShakeTween = null;
        }

        shakeOffset = Vector3.zero;
        specialShakeTween = DOVirtual.Float(0.0f, 1.0f, revivalShakeDuration, cachedSpecialShakeUpdate)
            .SetEase(Ease.Linear)
            .SetLink(gameObject)
            .OnComplete(cachedSpecialShakeComplete);
    }

    private void HandleSpecialRecoveryUpdate(float _val)
    {
        currentHpValue = _val;
        if (null != progressSlider)
            progressSlider.value = _val;
        if (null != ghostSlider)
            ghostSlider.value = _val;
    }

    private void HandleSpecialRecoveryComplete()
    {
        isSpecialRecovering = false;
        specialRecoveryTween = null;
    }

    private void HandleSpecialShakeUpdate(float _t)
    {
        float _decay = Mathf.Pow(1.0f - _t, 2.5f);
        float _waveY = (Mathf.Sin(_t * Mathf.PI * 36.0f) + Mathf.Sin(_t * Mathf.PI * 72.0f) * 0.4f) * _decay * revivalShakeStrength;
        float _waveX = (Mathf.Cos(_t * Mathf.PI * 30.0f) + Mathf.Sin(_t * Mathf.PI * 60.0f) * 0.4f) * _decay * (revivalShakeStrength * 0.8f);
        shakeOffset.x = _waveX;
        shakeOffset.y = _waveY;
    }

    private void HandleSpecialShakeComplete()
    {
        shakeOffset = Vector3.zero;
        specialShakeTween = null;
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

            SetGraphicsEnabled(ghostGraphics, true);
            SetGraphicsEnabled(shieldGhostGraphics, useShield);

            if (null != motionPlayer && false == isSpecialRecovering)
                motionPlayer.Play("Show", bReset: true);
        }
    }

    public void UpdateValues(float _hpRatio, float _shieldRatio)
    {
        if (Time.frameCount == lastRevivalFrame)
            return;

        if (true == isSpecialRecovering)
        {
            if (_hpRatio < currentHpValue)
            {
                if (null != specialRecoveryTween && true == specialRecoveryTween.IsActive())
                {
                    specialRecoveryTween.Kill();
                    specialRecoveryTween = null;
                }
                isSpecialRecovering = false;
            }
            else
            {
                return;
            }
        }

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

                hpGhostTween = SliderValueTweener.DOValue(ref ghostValueTweener, ghostSlider, _hpRatio, ghostFollowDuration)
                    .SetDelay(_nextHpDelay)
                    .SetEase(Ease.OutQuad)
                    .SetLink(gameObject);
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

                    shieldGhostTween = SliderValueTweener.DOValue(ref shieldGhostValueTweener, shieldGhostSlider, _shieldRatio, ghostFollowDuration)
                        .SetDelay(_nextShieldDelay)
                        .SetEase(Ease.OutQuad)
                        .SetLink(gameObject);
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

                    shieldRecoveryTween = SliderValueTweener.DOValue(ref shieldValueTweener, shieldSlider, _shieldRatio, ghostFollowDuration)
                        .SetDelay(_nextShieldDelay)
                        .SetEase(Ease.OutQuad)
                        .SetLink(gameObject);
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
                            .SetEase(Ease.OutQuad)
                            .SetLink(gameObject);
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
                            .SetEase(Ease.OutQuad)
                            .SetLink(gameObject);
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
        bHideTimerActive = 0.0f < showDuration;
        hideAtTime = Time.time + showDuration;
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
            SetGraphicsEnabled(ghostGraphics, false);
        }

        if (null != shieldGhostTween && true == shieldGhostTween.IsActive())
        {
            shieldGhostTween.Kill();
            shieldGhostTween = null;
        }
        
        if (null != shieldGhostSlider)
        {
            shieldGhostSlider.value = currentShieldValue;
            SetGraphicsEnabled(shieldGhostGraphics, false);
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

    // 슬라이더 오브젝트를 켠 채로 두고(이후 SetActive로 끄지 않는다) 그 아래 그래픽 목록을 모은다.
    private static Graphic[] CollectGraphics(Slider _slider)
    {
        if (null == _slider)
            return Array.Empty<Graphic>();

        if (false == _slider.gameObject.activeSelf)
            _slider.gameObject.SetActive(true);

        return _slider.GetComponentsInChildren<Graphic>(true);
    }

    private static void SetGraphicsEnabled(Graphic[] _graphics, bool _bEnabled)
    {
        if (null == _graphics)
            return;

        for (int _i = 0; _i < _graphics.Length; _i++)
        {
            Graphic _graphic = _graphics[_i];
            if (null != _graphic && _graphic.enabled != _bEnabled)
                _graphic.enabled = _bEnabled;
        }
    }

    private void HandleHideComplete()
    {
        if (null != onFinishCallback)
            onFinishCallback.Invoke(this);
    }

    public void OnDespawn()
    {
        if (null != specialRecoveryTween && true == specialRecoveryTween.IsActive())
        {
            specialRecoveryTween.Kill();
            specialRecoveryTween = null;
        }

        if (null != specialShakeTween && true == specialShakeTween.IsActive())
        {
            specialShakeTween.Kill();
            specialShakeTween = null;
        }

        shakeOffset = Vector3.zero;
        isSpecialRecovering = false;
        currentGemStage = 0;

        if (null != hpFillImage && null != normalHpBarSprite)
            hpFillImage.sprite = normalHpBarSprite;

        if (null != hpGhostFillImage && null != normalGhostBarSprite)
            hpGhostFillImage.sprite = normalGhostBarSprite;

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

        bHideTimerActive = false;

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

        if (true == bHideTimerActive && Time.time >= hideAtTime)
        {
            bHideTimerActive = false;
            OnHide(-1f);
        }
    }

    private void UpdatePosition()
    {
        if (null == targetObj || null == rect)
            return;

        Vector3 _pos = targetObj.transform.position;
        _pos.y += yOffset;
        _pos += shakeOffset;

        // 이미 그 자리면 대입하지 않는다(대입만으로도 트랜스폼/캔버스가 더럽혀진다). 캔버스가 카메라를 따라 움직여
        // 현재 월드 위치가 달라졌다면 값이 달라서 그대로 다시 맞춘다.
        if (_pos != rect.position)
            rect.position = _pos;
    }
}
