using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using DG.Tweening.Core;
using Coffee.UIEffects;

/// <summary>
/// 메인 메뉴 전용 버튼 스크립트입니다.
/// GC 발생 방지를 위해 람다 및 클로저 사용을 배제하고 델리게이트를 캐싱하여 사용합니다.
/// </summary>
public class UI_MainMenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
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
    [SerializeField] private float disappearStaggerInterval = 0.08f; // 여러 버튼이 순차적으로 사라지는 간격 (다다닥)
    [SerializeField] private float disappearSuckDuration = 0.3f; 
    [SerializeField] private float disappearDotShrinkDuration = 0.15f; 
    [SerializeField] private Ease disappearSuckEase = Ease.InCubic;
    [SerializeField] private Ease disappearShrinkEase = Ease.InBack;

    [Header("Disabled Settings")]
    [SerializeField] private bool isInteractable = true; 
    [ColorUsage(true, true)] [SerializeField] private Color disabledDotColor = Color.gray;
    [SerializeField] private Color disabledTextColor = Color.gray;

    // 내부 상태
    private Action onClickAction;
    private Vector2 textOriginalPos;
    private Vector3 dotOriginalRot;
    private TextMeshProUGUI targetTextComponent;
    private UnityEngine.UI.Graphic dotGraphicComponent;
    private Color originalDotColor = Color.white;
    private Color originalTextColor = Color.white;
    
    private bool isClicked = false;
    private bool isDisappearing = false;
    private bool isMaintained = false;
    private bool isAppearing = false;
    private bool isHovered = false;

    // 델리게이트 캐싱 (GC Alloc 방지)
    private TweenCallback onAppearCompleteCallback;
    private TweenCallback onDisappearCompleteCallback;
    private TweenCallback onClickPunchCompleteCallback;
    private TweenCallback playAppearTextMotionCallback;
    private TweenCallback invokeOnClickActionCallback;
    private TweenCallback playDisappearImmediateCallback;

    private DOGetter<Color> getDotShadowColor;
    private DOSetter<Color> setDotShadowColor;

    private DOGetter<Color> getTextShadowColor;
    private DOSetter<Color> setTextShadowColor;

    private void Awake()
    {
        if (null != textTarget)
        {
            textOriginalPos = textTarget.anchoredPosition;
            targetTextComponent = textTarget.GetComponent<TextMeshProUGUI>();
            if (null != targetTextComponent) originalTextColor = targetTextComponent.color;
        }

        if (null != dotTarget)
        {
            dotOriginalRot = dotTarget.localEulerAngles;
            dotGraphicComponent = dotTarget.GetComponentInChildren<UnityEngine.UI.Graphic>(); // 자식까지 탐색하거나 Graphic(Image, Text 등)으로 포괄 탐색
            if (null != dotGraphicComponent) originalDotColor = dotGraphicComponent.color;
        }

        // 델리게이트 인스턴스 사전 생성 및 캐싱 (람다/클로저 제거)
        onAppearCompleteCallback = OnAppearComplete;
        onDisappearCompleteCallback = OnDisappearComplete;
        onClickPunchCompleteCallback = OnClickPunchComplete;
        playAppearTextMotionCallback = PlayAppearTextMotion;
        invokeOnClickActionCallback = InvokeOnClickAction;
        playDisappearImmediateCallback = PlayDisappearImmediate;

        getDotShadowColor = GetDotShadowColor;
        setDotShadowColor = SetDotShadowColor;

        getTextShadowColor = GetTextShadowColor;
        setTextShadowColor = SetTextShadowColor;
    }

    private void OnEnable()
    {
        ResetAndPlayAppear();
    }

    /// <summary>
    /// 외부에서 강제로 버튼을 초기화하고 처음 등장하는 다다닥 모션을 다시 재생할 때 호출합니다.
    /// </summary>
    public void ResetAndPlayAppear()
    {
        // 꺼져있다면 켜주기만 해도 OnEnable이 불리면서 알아서 연출이 시작됨
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            return;
        }

        SetInteractable(isInteractable); // 초기 컬러 갱신

        float _delay = appearManualDelay;
        if (true == autoStaggerBySiblingIndex)
        {
            _delay = transform.GetSiblingIndex() * appearStaggerInterval;
        }
        PlayAppearMotion(_delay);
    }

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(Action _onClickCallback)
    {
        onClickAction = _onClickCallback;

        if (null == buttonImage)
        {
            buttonImage = GetComponent<Image>();
        }
    }

    public void SetInteractable(bool _isInteractable)
    {
        isInteractable = _isInteractable;

        if (null != targetTextComponent)
        {
            targetTextComponent.color = isInteractable ? originalTextColor : disabledTextColor;
        }

        if (null != dotGraphicComponent)
        {
            dotGraphicComponent.color = isInteractable ? originalDotColor : disabledDotColor;
        }
    }

    /// <summary>
    /// 로컬라이징 등 외부에서 텍스트를 변경할 때 사용합니다.
    /// </summary>
    public void SetText(string _text)
    {
        if (null != targetTextComponent)
        {
            targetTextComponent.text = _text;
        }
    }

    public void Release()
    {
        onClickAction = null;
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

            dotTarget.DOScale(Vector3.one, appearDotDuration).SetEase(Ease.OutBack).SetDelay(_delay);
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
    
    public void OnPointerEnter(PointerEventData _eventData)
    {
        isHovered = true;
        if (true == isClicked || true == isDisappearing || true == isAppearing || true == isMaintained) return;

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

    public void OnPointerExit(PointerEventData _eventData)
    {
        isHovered = false;
        if (true == isClicked || true == isDisappearing || true == isAppearing || true == isMaintained) return;

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

        Color targetShadowColor = isInteractable ? clickShadowColor : disabledClickShadowColor;

        TweenShadow(dotUIEffect, getDotShadowColor, setDotShadowColor, targetShadowColor, clickShadowGlow, clickDuration, clickShadowEase);
        TweenShadow(textUIEffect, getTextShadowColor, setTextShadowColor, targetShadowColor, clickShadowGlow, clickDuration, clickShadowEase);

        if (false == isInteractable)
        {
            // 비활성 상태면 기능 호출 및 숨김 연출 없이 펀치 모션만 재생
            transform.DOKill();
            transform.localScale = Vector3.one;
            Sequence _disabledClickSeq = DOTween.Sequence();
            _disabledClickSeq.Join(transform.DOPunchScale(clickPunchScale, clickDuration, clickVibrato, clickElasticity));
            _disabledClickSeq.InsertCallback(clickDuration, onClickPunchCompleteCallback);
            return;
        }

        float _maxDelay = clickDuration;
        bool _hasDisappearTargets = false;

        float _currentDisappearDelay = 0f;

        // 부모 하위에 있는 활성화된 모든 버튼을 찾음 (GetComponentsInChildren은 자신의 자식도 찾지만, 보통 형제 노드 탐색에 유용함)
        // 여기서는 부모 객체의 자식들을 탐색하여 형제 버튼들을 수집
        UI_MainMenuButton[] _siblingButtons = null;
        if (null != transform.parent)
        {
            _siblingButtons = transform.parent.GetComponentsInChildren<UI_MainMenuButton>(false);
            
            // GC 할당을 피하기 위해 간단한 삽입 정렬(Insertion Sort)을 사용하여 Sibling Index 기준으로 정렬
            if (null != _siblingButtons && _siblingButtons.Length > 1)
            {
                for (int i = 1; i < _siblingButtons.Length; i++)
                {
                    UI_MainMenuButton _key = _siblingButtons[i];
                    int _j = i - 1;
                    while (_j >= 0 && _siblingButtons[_j].transform.GetSiblingIndex() > _key.transform.GetSiblingIndex())
                    {
                        _siblingButtons[_j + 1] = _siblingButtons[_j];
                        _j = _j - 1;
                    }
                    _siblingButtons[_j + 1] = _key;
                }
            }

            // 정렬된 순서대로 사라지는 모션 재생
            if (null != _siblingButtons)
            {
                for (int i = 0; i < _siblingButtons.Length; i++)
                {
                    UI_MainMenuButton _btn = _siblingButtons[i];
                    if (null != _btn && _btn != this)
                    {
                        _btn.PlayDisappearMotion(_currentDisappearDelay);
                        _hasDisappearTargets = true;
                        _currentDisappearDelay += disappearStaggerInterval;
                    }
                }
            }
        }

        if (true == _hasDisappearTargets)
        {
            // 자신(this)을 가장 마지막에 사라지도록 추가
            DOVirtual.DelayedCall(_currentDisappearDelay, playDisappearImmediateCallback);

            float _disappearTime = _currentDisappearDelay + disappearSuckDuration + disappearDotShrinkDuration;
            if (_disappearTime > _maxDelay)
            {
                _maxDelay = _disappearTime;
            }
        }

        transform.DOKill();
        transform.localScale = Vector3.one; // 펀치 모션 시작 전 스케일 정규화
        
        Sequence _clickSeq = DOTween.Sequence();
        
        _clickSeq.Join(transform.DOPunchScale(clickPunchScale, clickDuration, clickVibrato, clickElasticity));
        
        if (true == _hasDisappearTargets)
        {
            DOVirtual.DelayedCall(_maxDelay, invokeOnClickActionCallback);
        }
        else
        {
            _clickSeq.InsertCallback(clickDuration, onClickPunchCompleteCallback);
            _clickSeq.InsertCallback(_maxDelay, invokeOnClickActionCallback);
        }
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
        
        if (_delay > 0f)
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
    private void OnAppearComplete() { isAppearing = false; }
    private void OnDisappearComplete() { gameObject.SetActive(false); }
    private void OnClickPunchComplete()
    {
        if (false == isInteractable)
        {
            isClicked = false;
            RestoreHoverOrExit();
            return;
        }

        if (true == isToggledButton)
        {
            PlayMaintainMotion();
        }
        else
        {
            isClicked = false;
            RestoreHoverOrExit();
        }
    }

    private void RestoreHoverOrExit()
    {
        if (isHovered)
        {
            TweenShadow(dotUIEffect, getDotShadowColor, setDotShadowColor, hoverShadowColor, hoverShadowGlow, hoverDuration, hoverShadowEase);
            TweenShadow(textUIEffect, getTextShadowColor, setTextShadowColor, hoverShadowColor, hoverShadowGlow, hoverDuration, hoverShadowEase);
        }
        else
        {
            OnPointerExit(null);
        }
    }

    private void InvokeOnClickAction()
    {
        if (null == this) return;
        if (null != onClickAction)
        {
            onClickAction.Invoke();
        }
    }

    private void PlayDisappearImmediate()
    {
        if (null == this) return;
        PlayDisappearMotion(0f);
    }

    private Color GetDotShadowColor() => null != dotUIEffect ? dotUIEffect.shadowColor : Color.white;
    private void SetDotShadowColor(Color _c) { if (null != dotUIEffect) dotUIEffect.shadowColor = _c; }

    private Color GetTextShadowColor() => null != textUIEffect ? textUIEffect.shadowColor : Color.white;
    private void SetTextShadowColor(Color _c) { if (null != textUIEffect) textUIEffect.shadowColor = _c; }

    private void OnDestroy()
    {
        Release();
    }
}
