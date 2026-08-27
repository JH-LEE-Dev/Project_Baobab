using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using DG.Tweening.Core;
using Coffee.UIEffects;
using UnityEngine.InputSystem;

/// <summary>
/// 메인 메뉴 전용 버튼 스크립트입니다.
/// GC 발생 방지를 위해 람다 및 클로저 사용을 배제하고 델리게이트를 캐싱하여 사용합니다.
/// </summary>
public class UI_MainMenuButton : Selectable,
    IPointerClickHandler,
    ISubmitHandler
{
    // 외부 의존성
    [Header("Targets")]
    [SerializeField] private Image buttonImage; 
    [SerializeField] private RectTransform dotTarget;  
    [SerializeField] private RectTransform textTarget; 

    [Header("UIEffect Targets (그림자 색상 제어용)")]
    [SerializeField] private UIEffect dotUIEffect;
    [SerializeField] private UIEffect textUIEffect;

    [Header("Appear Settings (처음 등장)")]
    [SerializeField] private bool autoStaggerBySiblingIndex = true; // Sibling Index를 기반으로 자동 순차 연출
    [SerializeField] private float appearStaggerInterval = 0.08f; // 순차 등장 간격
    [SerializeField] private float appearManualDelay = 0f; // 수동 딜레이 (위 옵션이 false일 때 사용)
    [SerializeField] private float appearDotDuration = 0.4f;
    [SerializeField] private float appearTextDuration = 0.3f;
    [SerializeField] private Vector3 appearDotRotation = new Vector3(0f, 0f, 180f);
    [SerializeField] private Ease appearEase = Ease.OutBack;
    [SerializeField] private Ease appearRotEase = Ease.OutCubic;

    [Header("Hover Settings (뽀잉하는 찰진 느낌)")]
    [SerializeField] private float hoverDuration = 0.35f;
    [SerializeField] private Vector3 hoverDotRotation = new Vector3(0f, 0f, 90f); 
    [SerializeField] private Vector2 textHoverStartScale = new Vector2(1.1f, 1.1f); 
    [SerializeField] private float textHoverMoveX = 15f; 
    [ColorUsage(true, true)] [SerializeField] private Color hoverShadowColor = Color.white;
    [SerializeField] private bool hoverShadowGlow = true;
    [SerializeField] private Ease hoverEase = Ease.OutBack;
    [SerializeField] private Ease hoverShadowEase = Ease.OutQuad;

    [Header("UnHover Settings (무심하게 툭 떨어지는 느낌)")]
    [SerializeField] private float unhoverDuration = 0.2f;
    [ColorUsage(true, true)] [SerializeField] private Color unhoverShadowColor = Color.black;
    [SerializeField] private bool unhoverShadowGlow = false;
    [SerializeField] private Ease unhoverEase = Ease.OutQuad;

    [Header("Click Settings (뽀잉 모션)")]
    [SerializeField] private Vector3 clickPunchScale = new Vector3(0.2f, -0.2f, 0f); // 클릭 시 찌그러지는 스케일 정도
    [SerializeField] private float clickDuration = 0.35f; // 뽀잉거리는 전체 시간
    [SerializeField] private int clickVibrato = 6; // 흔들리는 횟수 (탄성)
    [SerializeField, Range(0f, 1f)] private float clickElasticity = 0.6f; // 늘어나는 정도
    [ColorUsage(true, true)] [SerializeField] private Color clickShadowColor = Color.yellow;
    [ColorUsage(true, true)] [SerializeField] private Color disabledClickShadowColor = Color.red; // 비활성화 상태 클릭 시 색상
    [SerializeField] private bool clickShadowGlow = true;
    [SerializeField] private Ease clickShadowEase = Ease.OutQuad;

    [Header("Maintain (Toggled) Settings")]
    [SerializeField] private bool isToggledButton = false; 
    [SerializeField] private float maintainRotationDuration = 1.5f; 
    [SerializeField] private float maintainPulseDuration = 0.5f; 
    [ColorUsage(true, true)] [SerializeField] private Color maintainPulseColorA = Color.white;
    [ColorUsage(true, true)] [SerializeField] private Color maintainPulseColorB = Color.red;
    [SerializeField] private Ease maintainRotationEase = Ease.Linear;
    [SerializeField] private Ease maintainPulseEase = Ease.InOutSine;

    [Header("Disappear Settings")]
    [SerializeField] private bool autoDisappearOnClick = true; // 클릭 시 자동으로 주변 버튼을 숨길지 여부
    [SerializeField] private float disappearStaggerInterval = 0.08f; // 여러 버튼이 순차적으로 사라지는 간격 (다다닥)
    [SerializeField] private float disappearSuckDuration = 0.3f; 
    [SerializeField] private float disappearDotShrinkDuration = 0.15f; 
    [SerializeField] private Ease disappearSuckEase = Ease.InCubic;
    [SerializeField] private Ease disappearShrinkEase = Ease.InBack;

    // 내부 상태
    private Action onClickAction;
    private Action onPressedAction;
    private Action manualDisappearCallback;
    private InputManager inputManager;
    private Vector2 textOriginalPos;
    private Vector3 dotOriginalRot;
    private TextMeshProUGUI targetTextComponent;
    private TextMeshProUGUI targetDotTextComponent;
    private UnityEngine.UI.Graphic dotGraphicComponent;

    // 캐싱된 UI 컴포넌트
    private RectTransform cachedRectTransform;
    private Canvas cachedCanvas;
    private static List<RaycastResult> raycastResults = new List<RaycastResult>();
    private PointerEventData cachedPointerData;
    private UI_MainMenuButton[] _siblingButtons;
    private bool _siblingsCached = false;
    
    private bool isClicked = false;
    private bool isDisappearing = false;
    private bool isMaintained = false;
    private bool isAppearing = false;
    private bool isHovered = false;
    private bool isPointerHovered = false;

    public bool IsPointerHovered => isPointerHovered;

    // 델리게이트 캐싱 (GC Alloc 방지)
    private TweenCallback onAppearCompleteCallback;
    private TweenCallback onDisappearCompleteCallback;
    private TweenCallback onClickPunchCompleteCallback;
    private TweenCallback playAppearTextMotionCallback;
    private TweenCallback playAppearDotSoundCallback;
    private TweenCallback playAppearButtonSoundCallback;
    private TweenCallback invokeOnClickActionCallback;
    private TweenCallback playDisappearImmediateCallback;
    private TweenCallback invokeManualDisappearCallback;

    private DOGetter<Color> getDotShadowColor;
    private DOSetter<Color> setDotShadowColor;

    private DOGetter<Color> getTextShadowColor;
    private DOSetter<Color> setTextShadowColor;

    private int appearSoundIndex = -1;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;

        EnsureTargetComponents();

        // 델리게이트 인스턴스 사전 생성 및 캐싱 (람다/클로저 제거)
        onAppearCompleteCallback = OnAppearComplete;
        onDisappearCompleteCallback = OnDisappearComplete;
        onClickPunchCompleteCallback = OnClickPunchComplete;
        playAppearTextMotionCallback = PlayAppearTextMotion;
        playAppearDotSoundCallback = PlayAppearDotSound;
        playAppearButtonSoundCallback = PlayAppearButtonSound;
        invokeOnClickActionCallback = InvokeOnClickAction;
        playDisappearImmediateCallback = PlayDisappearImmediate;
        invokeManualDisappearCallback = InvokeManualDisappearAction;

        getDotShadowColor = GetDotShadowColor;
        setDotShadowColor = SetDotShadowColor;

        getTextShadowColor = GetTextShadowColor;
        setTextShadowColor = SetTextShadowColor;

        cachedRectTransform = GetComponent<RectTransform>();
        cachedCanvas = GetComponentInParent<Canvas>();
        if (null != EventSystem.current)
        {
            cachedPointerData = new PointerEventData(EventSystem.current);
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (false == Application.isPlaying) return;
        ResetAndPlayAppearInternal();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (false == Application.isPlaying) return;
        isHovered = false;
        isPointerHovered = false;
        isClicked = false;
        isDisappearing = false;
        isMaintained = false;
        isAppearing = false;
        appearSoundIndex = -1;

        KillAllTweens();
        
        if (null != dotTarget) dotTarget.localEulerAngles = dotOriginalRot;
        if (null != textTarget)
        {
            textTarget.localScale = Vector3.one;
            textTarget.anchoredPosition = textOriginalPos;
        }
    }

    /// <summary>
    /// 외부에서 강제로 버튼을 초기화하고 처음 등장하는 다다닥 모션을 다시 재생할 때 호출합니다.
    /// </summary>
    public void ResetAndPlayAppear(int _appearSoundIndex = -1)
    {
        appearSoundIndex = _appearSoundIndex;

        // 꺼져있다면 켜주기만 해도 OnEnable이 불리면서 알아서 연출이 시작됨
        if (false == gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            return;
        }

        ResetAndPlayAppearInternal();
    }

    private void ResetAndPlayAppearInternal()
    {
        float _delay = appearManualDelay;
        if (true == autoStaggerBySiblingIndex)
        {
            _delay = transform.GetSiblingIndex() * appearStaggerInterval;
        }
        PlayAppearMotion(_delay);
    }

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(Action _onClickCallback, Action _onPressedCallback = null, InputManager _inputManager = null)
    {
        onClickAction = _onClickCallback;
        onPressedAction = _onPressedCallback;
        inputManager = _inputManager;

        EnsureTargetComponents();
    }

    private void EnsureTargetComponents()
    {
        if (null == buttonImage)
        {
            buttonImage = GetComponent<Image>();
        }

        if (null != textTarget && null == targetTextComponent)
        {
            textOriginalPos = textTarget.anchoredPosition;
            if (null != dotTarget && textOriginalPos.x <= dotTarget.anchoredPosition.x)
            {
                textOriginalPos = new Vector2(dotTarget.anchoredPosition.x + 10f, textOriginalPos.y);
            }
            targetTextComponent = textTarget.GetComponent<TextMeshProUGUI>();
        }

        if (null != dotTarget)
        {
            dotOriginalRot = dotTarget.localEulerAngles;
            if (null == dotGraphicComponent) dotGraphicComponent = dotTarget.GetComponentInChildren<UnityEngine.UI.Graphic>();
            if (null == targetDotTextComponent) targetDotTextComponent = dotTarget.GetComponentInChildren<TextMeshProUGUI>();
        }
    }

    /// <summary>
    /// 로컬라이징 등 외부에서 텍스트를 변경할 때 사용합니다.
    /// </summary>
    public void SetText(string _text)
    {
        EnsureTargetComponents();

        if (null != targetTextComponent)
        {
            targetTextComponent.text = _text;
        }
    }

    /// <summary>
    /// 로컬라이징 등 외부에서 도트 텍스트(특수기호)를 변경할 때 사용합니다.
    /// </summary>
    public void SetDotText(string _dotText)
    {
        EnsureTargetComponents();

        if (null != targetDotTextComponent)
        {
            targetDotTextComponent.text = _dotText;
        }
    }

    public void Release()
    {
        onClickAction = null;
        onPressedAction = null;
        _siblingButtons = null;
        _siblingsCached = false;
        KillAllTweens();
    }

    private void KillAllTweens()
    {
        transform.DOKill();
        if (null != dotTarget) dotTarget.DOKill();
        if (null != textTarget) textTarget.DOKill();
        if (null != dotUIEffect) DOTween.Kill(dotUIEffect);
        if (null != textUIEffect) DOTween.Kill(textUIEffect);
    }

    public void PlayAppearMotion(float _delay = 0f)
    {
        if (false == Application.isPlaying) return;
        isAppearing = true;
        isClicked = false;
        isDisappearing = false;
        isMaintained = false;

        KillAllTweens();
        
        transform.localScale = Vector3.one;

        if (null != dotUIEffect)
        {
            dotUIEffect.shadowColor = unhoverShadowColor;
            dotUIEffect.shadowColorGlow = unhoverShadowGlow;
        }
        if (null != textUIEffect)
        {
            textUIEffect.shadowColor = unhoverShadowColor;
            textUIEffect.shadowColorGlow = unhoverShadowGlow;
        }

        if (null != textTarget)
        {
            textTarget.localScale = Vector3.zero;
        }

        if (null != dotTarget)
        {
            dotTarget.localScale = Vector3.zero;
            dotTarget.localEulerAngles = dotOriginalRot - appearDotRotation;

            dotTarget.DOScale(Vector3.one, appearDotDuration).SetEase(Ease.OutBack).SetDelay(_delay)
                     .OnStart(playAppearDotSoundCallback);
            dotTarget.DOLocalRotate(dotOriginalRot, appearDotDuration, RotateMode.FastBeyond360).SetEase(appearRotEase).SetDelay(_delay)
                     .OnComplete(playAppearTextMotionCallback);
            
            if (null != textTarget)
            {
                textTarget.anchoredPosition = dotTarget.anchoredPosition;
            }
        }
        else if (null != textTarget)
        {
            textTarget.DOScale(Vector3.one, appearTextDuration).SetEase(Ease.OutBack).SetDelay(_delay);
            textTarget.DOAnchorPos(textOriginalPos, appearTextDuration).SetEase(Ease.OutBack).SetDelay(_delay)
                      .OnStart(playAppearButtonSoundCallback)
                      .OnComplete(onAppearCompleteCallback);
        }
        else
        {
            isAppearing = false;
        }
    }

    private void PlayAppearTextMotion()
    {
        if (null != textTarget)
        {
            PlayAppearButtonSound();
            textTarget.DOScale(Vector3.one, appearTextDuration).SetEase(appearEase);
            textTarget.DOAnchorPos(textOriginalPos, appearTextDuration).SetEase(appearEase)
                      .OnComplete(onAppearCompleteCallback);
        }
        else
        {
            isAppearing = false;
        }
    }

    // 유니티 이벤트 시스템
    
    private void PlayAppearDotSound()
    {
        switch (appearSoundIndex)
        {
            case 0: Sound.PlayUI(SoundID.MainMenuDot00); break;
            case 1: Sound.PlayUI(SoundID.MainMenuDot01); break;
            case 2: Sound.PlayUI(SoundID.MainMenuDot02); break;
            case 3: Sound.PlayUI(SoundID.MainMenuDot03); break;
            case 4: Sound.PlayUI(SoundID.MainMenuDot04); break;
        }
    }

    private void PlayAppearButtonSound()
    {
        if (0 == appearSoundIndex)
        {
            Sound.PlayUI(SoundID.MainMenuButtonAppearStart00);
            Sound.PlayUI(SoundID.MainMenuButtonAppearStart01);
        }

        switch (appearSoundIndex)
        {
            case 0: Sound.PlayUI(SoundID.MainMenuButton00); break;
            case 1: Sound.PlayUI(SoundID.MainMenuButton01); break;
            case 2: Sound.PlayUI(SoundID.MainMenuButton02); break;
            case 3: Sound.PlayUI(SoundID.MainMenuButton03); break;
            case 4: Sound.PlayUI(SoundID.MainMenuButton04); break;
        }
    }

    public override void OnPointerEnter(PointerEventData _eventData)
    {
        base.OnPointerEnter(_eventData);
        isHovered = true;
        isPointerHovered = true;
        if (true == isClicked || true == isDisappearing || true == isAppearing || true == isMaintained) return;

        Sound.PlayUI(SoundID.MainButtonHover);
        PlayHoverMotion();
    }

    public override void OnPointerExit(PointerEventData _eventData)
    {
        base.OnPointerExit(_eventData);
        isHovered = false;
        isPointerHovered = false;
        if (true == isClicked || true == isDisappearing || true == isAppearing || true == isMaintained) return;

        PlayUnhoverMotion();
    }

    public bool IsMouseOver()
    {
        if (false == gameObject.activeInHierarchy) return false;
        if (true == isPointerHovered) return true;

        RectTransform _rect = transform as RectTransform;
        if (null == _rect) return false;

        Vector2 _mousePos = Vector2.zero;
        if (null != Mouse.current)
        {
            _mousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            _mousePos = Input.mousePosition;
        }

        Canvas _canvas = GetComponentInParent<Canvas>();
        Camera _cam = (null != _canvas && RenderMode.ScreenSpaceOverlay != _canvas.renderMode) ? _canvas.worldCamera : null;

        return RectTransformUtility.RectangleContainsScreenPoint(_rect, _mousePos, _cam);
    }

    public void ForceHover()
    {
        isHovered = true;
        if (true == isClicked || true == isDisappearing || true == isAppearing || true == isMaintained) return;
        PlayHoverMotion();
    }

    public void ForceUnhover()
    {
        isHovered = false;
        isPointerHovered = false;
        if (true == isClicked || true == isDisappearing || true == isAppearing || true == isMaintained) return;
        PlayUnhoverMotion();
    }

    public override void OnSelect(BaseEventData _eventData)
    {
        base.OnSelect(_eventData);
        if (null != inputManager && false == inputManager.IsGamepadMode) return;

        isHovered = true;
        if (true == isClicked || true == isDisappearing || true == isAppearing || true == isMaintained) return;

        Sound.PlayUI(SoundID.MainButtonHover);
        PlayHoverMotion();
    }

    public override void OnDeselect(BaseEventData _eventData)
    {
        base.OnDeselect(_eventData);
        if (null != inputManager && false == inputManager.IsGamepadMode) return;

        isHovered = false;
        if (true == isClicked || true == isDisappearing || true == isAppearing || true == isMaintained) return;

        PlayUnhoverMotion();
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        OnPointerClick(null);
    }

    private void PlayHoverMotion()
    {
        if (null != dotTarget)
        {
            dotTarget.DOKill();
            dotTarget.DOLocalRotate(dotOriginalRot + hoverDotRotation, hoverDuration).SetEase(hoverEase);
        }

        if (null != textTarget)
        {
            textTarget.DOKill();
            textTarget.localScale = new Vector3(textHoverStartScale.x, textHoverStartScale.y, 1f);
            textTarget.DOScale(Vector3.one, hoverDuration).SetEase(hoverEase);
            textTarget.DOAnchorPos(new Vector2(textOriginalPos.x + textHoverMoveX, textOriginalPos.y), hoverDuration).SetEase(hoverEase);
        }

        TweenShadow(dotUIEffect, getDotShadowColor, setDotShadowColor, hoverShadowColor, hoverShadowGlow, hoverDuration, hoverShadowEase);
        TweenShadow(textUIEffect, getTextShadowColor, setTextShadowColor, hoverShadowColor, hoverShadowGlow, hoverDuration, hoverShadowEase);
    }

    private void PlayUnhoverMotion()
    {
        if (null != dotTarget)
        {
            dotTarget.DOKill();
            dotTarget.DOLocalRotate(dotOriginalRot, unhoverDuration).SetEase(unhoverEase);
        }

        if (null != textTarget)
        {
            textTarget.DOKill();
            textTarget.DOScale(Vector3.one, unhoverDuration).SetEase(unhoverEase);
            textTarget.DOAnchorPos(textOriginalPos, unhoverDuration).SetEase(unhoverEase);
        }

        TweenShadow(dotUIEffect, getDotShadowColor, setDotShadowColor, unhoverShadowColor, unhoverShadowGlow, unhoverDuration, unhoverEase);
        TweenShadow(textUIEffect, getTextShadowColor, setTextShadowColor, unhoverShadowColor, unhoverShadowGlow, unhoverDuration, unhoverEase);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (true == isClicked || true == isDisappearing || true == isAppearing) return;
        
        isClicked = true;

        PlayClickShadowMotion();

        Sound.PlayUI(SoundID.MainClick);
        onPressedAction?.Invoke();

        HandleClickSequence();
    }

    private void PlayClickShadowMotion()
    {
        TweenShadow(dotUIEffect, getDotShadowColor, setDotShadowColor, clickShadowColor, clickShadowGlow, clickDuration, clickShadowEase);
        TweenShadow(textUIEffect, getTextShadowColor, setTextShadowColor, clickShadowColor, clickShadowGlow, clickDuration, clickShadowEase);
    }

    private void HandleClickSequence()
    {
        float _maxDelay = clickDuration;
        bool _hasDisappearTargets = false;
        float _currentDisappearDelay = 0f;

        if (true == autoDisappearOnClick)
        {
            if (null != transform.parent)
            {
                if (false == _siblingsCached)
                {
                    _siblingButtons = transform.parent.GetComponentsInChildren<UI_MainMenuButton>(false);
                    _siblingsCached = true;
                }
                
                SortSiblingButtonsByIndex();

                if (null != _siblingButtons)
                {
                    for (int i = 0; i < _siblingButtons.Length; i++)
                    {
                        UI_MainMenuButton _btn = _siblingButtons[i];
                        if (null != _btn && _btn != this)
                        {
                            if (false == _hasDisappearTargets)
                            {
                                Sound.PlayUI(SoundID.MainGameSelect);
                            }

                            _btn.PlayDisappearMotion(_currentDisappearDelay);
                            _hasDisappearTargets = true;
                            _currentDisappearDelay += disappearStaggerInterval;
                        }
                    }
                }
            }

            if (true == _hasDisappearTargets)
            {
                DOVirtual.DelayedCall(_currentDisappearDelay, playDisappearImmediateCallback);

                float _disappearTime = _currentDisappearDelay + disappearSuckDuration + disappearDotShrinkDuration;
                if (_maxDelay < _disappearTime)
                {
                    _maxDelay = _disappearTime;
                }
            }
        }

        transform.DOKill();
        transform.localScale = Vector3.one; 
        
        Sequence _clickSeq = DOTween.Sequence();
        
        _clickSeq.Join(transform.DOPunchScale(clickPunchScale, clickDuration, clickVibrato, clickElasticity));
        
        if (true == autoDisappearOnClick && true == _hasDisappearTargets)
        {
            DOVirtual.DelayedCall(_maxDelay, invokeOnClickActionCallback);
        }
        else
        {
            _clickSeq.InsertCallback(clickDuration, onClickPunchCompleteCallback);
            _clickSeq.InsertCallback(clickDuration, invokeOnClickActionCallback);
        }
    }

    private void SortSiblingButtonsByIndex()
    {
        if (null == _siblingButtons || 1 >= _siblingButtons.Length) return;

        for (int i = 1; i < _siblingButtons.Length; i++)
        {
            UI_MainMenuButton _key = _siblingButtons[i];
            int _j = i - 1;
            while (0 <= _j && _key.transform.GetSiblingIndex() < _siblingButtons[_j].transform.GetSiblingIndex())
            {
                _siblingButtons[_j + 1] = _siblingButtons[_j];
                _j = _j - 1;
            }
            _siblingButtons[_j + 1] = _key;
        }
    }

    public void PlayDisappearSequenceManually(Action _onCompleteCallback)
    {
        manualDisappearCallback = _onCompleteCallback;
        
        float _maxDelay = 0f;
        bool _hasDisappearTargets = false;
        float _currentDisappearDelay = 0f;

        if (null != transform.parent)
        {
            if (false == _siblingsCached)
            {
                _siblingButtons = transform.parent.GetComponentsInChildren<UI_MainMenuButton>(false);
                _siblingsCached = true;
            }
            
            SortSiblingButtonsByIndex();

            if (null != _siblingButtons)
            {
                for (int i = 0; i < _siblingButtons.Length; i++)
                {
                    UI_MainMenuButton _btn = _siblingButtons[i];
                    if (null != _btn && _btn != this)
                    {
                        if (false == _hasDisappearTargets)
                        {
                            Sound.PlayUI(SoundID.MainGameSelect);
                        }

                        _btn.PlayDisappearMotion(_currentDisappearDelay);
                        _hasDisappearTargets = true;
                        _currentDisappearDelay += disappearStaggerInterval;
                    }
                }
            }
        }

        if (true == _hasDisappearTargets)
        {
            DOVirtual.DelayedCall(_currentDisappearDelay, playDisappearImmediateCallback);

            float _disappearTime = _currentDisappearDelay + disappearSuckDuration + disappearDotShrinkDuration;
            if (_maxDelay < _disappearTime)
            {
                _maxDelay = _disappearTime;
            }
        }
        else
        {
            // 타겟이 하나도 없으면 자신만 사라짐
            Sound.PlayUI(SoundID.MainGameSelect);
            PlayDisappearImmediate();
            _maxDelay = disappearSuckDuration + disappearDotShrinkDuration;
        }

        DOVirtual.DelayedCall(_maxDelay, invokeManualDisappearCallback);
    }

    private void PlayMaintainMotion()
    {
        isMaintained = true;

        if (null != dotTarget)
        {
            dotTarget.DOKill();
            dotTarget.DOLocalRotate(new Vector3(0f, 0f, 360f), maintainRotationDuration, RotateMode.FastBeyond360)
                     .SetEase(maintainRotationEase)
                     .SetLoops(-1, LoopType.Incremental)
                     .SetRelative();
        }

        TweenShadowPulse(dotUIEffect, getDotShadowColor, setDotShadowColor);
        TweenShadowPulse(textUIEffect, getTextShadowColor, setTextShadowColor);
    }

    public void ReleaseMaintainState()
    {
        if (false == isMaintained) return;
        
        isMaintained = false;
        isClicked = false;
        
        KillAllTweens();
        
        if (null != dotTarget) dotTarget.localEulerAngles = dotOriginalRot;
        if (null != textTarget) textTarget.localScale = Vector3.one;
        
        EvaluatePointerState();
    }

    public void PlayDisappearMotion(float _delay = 0f)
    {
        if (true == isDisappearing) return;
        
        isDisappearing = true;
        isMaintained = false;
        isAppearing = false;
        
        KillAllTweens();

        transform.localScale = Vector3.one;
        if (null != dotTarget) dotTarget.localScale = Vector3.one;
        if (null != textTarget) textTarget.localScale = Vector3.one;

        Sequence _disappearSeq = DOTween.Sequence();
        
        if (0f < _delay)
        {
            _disappearSeq.AppendInterval(_delay);
        }

        if (null != textTarget && null != dotTarget)
        {
            _disappearSeq.Append(textTarget.DOAnchorPos(dotTarget.anchoredPosition, disappearSuckDuration).SetEase(disappearSuckEase));
            _disappearSeq.Join(textTarget.DOScale(Vector3.zero, disappearSuckDuration).SetEase(disappearSuckEase));
            _disappearSeq.Join(dotTarget.DOLocalRotate(dotOriginalRot + new Vector3(0f, 0f, 720f), disappearSuckDuration, RotateMode.FastBeyond360).SetEase(disappearSuckEase));
            _disappearSeq.Append(dotTarget.DOScale(Vector3.zero, disappearDotShrinkDuration).SetEase(disappearShrinkEase));
        }

        _disappearSeq.OnComplete(onDisappearCompleteCallback);
    }

    private void TweenShadow(UIEffect _effect, DOGetter<Color> _getColor, DOSetter<Color> _setColor, Color _targetColor, bool _glow, float _duration, Ease _ease)
    {
        if (null == _effect) return;
        
        DOTween.Kill(_effect); 
        _effect.shadowColorGlow = _glow;
        
        DOTween.To(_getColor, _setColor, _targetColor, _duration)
               .SetEase(_ease)
               .SetTarget(_effect);
    }

    private void TweenShadowPulse(UIEffect _effect, DOGetter<Color> _getColor, DOSetter<Color> _setColor)
    {
        if (null == _effect) return;
        
        DOTween.Kill(_effect);
        _setColor(maintainPulseColorA);

        DOTween.To(_getColor, _setColor, maintainPulseColorB, maintainPulseDuration)
               .SetEase(maintainPulseEase)
               .SetLoops(-1, LoopType.Yoyo)
               .SetTarget(_effect);
    }

    // 캐싱용 메서드 모음
    private void OnAppearComplete() 
    { 
        isAppearing = false; 
        EvaluatePointerState();
    }
    private void OnDisappearComplete() { gameObject.SetActive(false); }
    private void OnClickPunchComplete()
    {
        if (true == isToggledButton)
        {
            PlayMaintainMotion();
        }
        else
        {
            isClicked = false;
            EvaluatePointerState();
        }
    }

    private void EvaluatePointerState()
    {
        if (false == gameObject.activeInHierarchy) return;

        bool _isOver = isHovered;

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            _isOver = (null != EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject);
        }

        isHovered = _isOver;

        if (true == isClicked || true == isDisappearing || true == isAppearing || true == isMaintained) return;

        if (true == _isOver)
        {
            PlayHoverMotion();
        }
        else
        {
            PlayUnhoverMotion();
        }
    }

    private void InvokeOnClickAction()
    {
        if (null != onClickAction) onClickAction();
    }

    private void InvokeManualDisappearAction()
    {
        if (null != manualDisappearCallback)
        {
            manualDisappearCallback();
            manualDisappearCallback = null;
        }
    }

    private void PlayDisappearImmediate()
    {
        PlayDisappearMotion(0f);
    }

    private Color GetDotShadowColor() => null != dotUIEffect ? dotUIEffect.shadowColor : Color.white;
    private void SetDotShadowColor(Color _c) { if (null != dotUIEffect) dotUIEffect.shadowColor = _c; }

    private Color GetTextShadowColor() => null != textUIEffect ? textUIEffect.shadowColor : Color.white;
    private void SetTextShadowColor(Color _c) { if (null != textUIEffect) textUIEffect.shadowColor = _c; }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Release();
    }
}
