using System;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using Coffee.UIEffects;

/// <summary>
/// ESC 메뉴 전용 커스텀 버튼 컴포넌트입니다.
/// 등장 시 X 확장/Y 압축 상태에서 원복되며,
/// 호버 및 클릭 시 Y 스케일이 살짝 찌부되었다가 뽀잉 원복되는 찰진 모션을 제공합니다.
/// 퇴장 시에는 역순으로 찌부되며 페이드아웃됩니다.
/// </summary>
public class UI_EscapeMenuButton : Selectable,
    IPointerClickHandler,
    ISubmitHandler
{
    [Header("UI Component References")]
    [SerializeField] private Image raycastImage;
    [SerializeField] private RectTransform motionTarget;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private UIEffect uiEffect;

    [Header("Appear Motion Settings (X확장 + Y압축에서 원복)")]
    [SerializeField] private Vector3 appearStartScale = new Vector3(1.25f, 0.2f, 1f);
    [SerializeField] private float appearDuration = 0.2f;
    [SerializeField] private Ease appearEase = Ease.OutBack;

    [Header("Disappear Motion Settings (되감기 역모션)")]
    [SerializeField] private float disappearDuration = 0.15f;
    [SerializeField] private Ease disappearEase = Ease.InBack;

    [Header("Hover & Click Squash Motion Settings (Y찌부 뽀잉)")]
    [SerializeField, Tooltip("호버/클릭 시 찌부되는 Y 스케일 비율 (예: 0.85)")]
    private float squashYScale = 0.85f;
    [SerializeField, Tooltip("찌부 후 뽀잉 원복까지의 전체 시간")]
    private float squashDuration = 0.22f;
    [SerializeField, Range(0.1f, 0.9f), Tooltip("눌리는 시간 비율")]
    private float squashPressRatio = 0.4f;
    [SerializeField] private Ease squashPressEase = Ease.OutQuad;
    [SerializeField] private Ease squashBounceEase = Ease.OutBack;

    [Header("UIEffect HDR Shadow Colors")]
    [ColorUsage(true, true)] [SerializeField] private Color normalShadowColor = Color.black;
    [ColorUsage(true, true)] [SerializeField] private Color hoverShadowColor = new Color(1.5f, 1.5f, 1.5f, 1f);
    [ColorUsage(true, true)] [SerializeField] private Color unhoverShadowColor = Color.black;
    [ColorUsage(true, true)] [SerializeField] private Color clickShadowColor = new Color(2f, 1.5f, 0.5f, 1f);
    [SerializeField] private float shadowTweenDuration = 0.15f;
    [SerializeField] private Ease shadowEase = Ease.OutQuad;

    private Action onClickAction;
    private InputManager inputManager;
    private bool isInteractable = true;
    private bool isHovered = false;
    private bool isPointerHovered = false;
    private bool isAppearing = false;
    private bool isClickLocked = false;

    public bool IsPointerHovered => isPointerHovered;
    public new bool IsInteractable => isInteractable && interactable;
    public bool IsClickLocked => isClickLocked;

    private Vector3 originalScale = Vector3.one;

    private RectTransform cachedRectTransform;
    private CanvasGroup canvasGroup;
    private Canvas cachedCanvas;

    private DOGetter<Color> getShadowColorDelegate;
    private DOSetter<Color> setShadowColorDelegate;
    private TweenCallback onAppearMotionStartCallback;
    private TweenCallback onAppearCompleteCallback;
    private Action onDisappearCallback;
    private TweenCallback onDisappearCompleteCallback;

    public RectTransform RectTransform
    {
        get
        {
            if (null == cachedRectTransform)
                cachedRectTransform = GetComponent<RectTransform>();
            return cachedRectTransform;
        }
    }

    /// <summary>
    /// EventSystem이 없을 때의 폴백 판정에 쓰는 영역입니다. EventSystem이 실제로 포인터 이벤트를 발생시키는
    /// 레이캐스트 이미지와 같은 영역이어야 합니다. 루트(레이아웃 슬롯)는 레이캐스트 이미지보다 넓어서, 루트로
    /// 판정하면 OnPointerEnter 없이 호버가 켜지고 OnPointerExit가 오지 않아 호버가 해제되지 않습니다.
    /// </summary>
    private RectTransform HitRectTransform => null != raycastImage ? raycastImage.rectTransform : RectTransform;

    // 수동 호버 판정용 레이캐스트 버퍼. 매 호출마다 할당하지 않도록 재사용한다.
    private static readonly List<RaycastResult> sharedRaycastResults = new List<RaycastResult>(16);
    private PointerEventData cachedPointerEventData;

    public CanvasGroup CanvasGroup
    {
        get
        {
            if (null == canvasGroup)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (null == canvasGroup)
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
            return canvasGroup;
        }
    }

    public float AppearDuration => appearDuration;
    public float DisappearDuration => disappearDuration;

    protected override void Awake()
    {
        base.Awake();
        transition = Transition.None;

        if (null == cachedRectTransform)
            cachedRectTransform = GetComponent<RectTransform>();

        if (null == motionTarget)
            motionTarget = cachedRectTransform;

        originalScale = motionTarget.localScale;

        if (null == raycastImage)
            raycastImage = GetComponent<Image>();

        if (null == buttonText)
            buttonText = GetComponentInChildren<TMP_Text>();

        if (null == uiEffect)
            uiEffect = GetComponentInChildren<UIEffect>();

        if (null == canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (null == canvasGroup)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        getShadowColorDelegate = GetShadowColor;
        setShadowColorDelegate = SetShadowColor;
        onAppearMotionStartCallback = OnAppearMotionStart;
        onAppearCompleteCallback = OnAppearAnimationComplete;
        onDisappearCompleteCallback = OnDisappearAnimationComplete;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        isHovered = false;
        isPointerHovered = false;
        isAppearing = false;
        isClickLocked = false;

        KillTweens();

        if (null != motionTarget)
        {
            motionTarget.localScale = originalScale;
        }

        if (null != CanvasGroup)
        {
            CanvasGroup.alpha = 1f;
        }

        if (null != uiEffect)
        {
            uiEffect.shadowColor = normalShadowColor;
        }
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        KillTweens();
        onClickAction = null;
        getShadowColorDelegate = null;
        setShadowColorDelegate = null;
        onAppearMotionStartCallback = null;
        onAppearCompleteCallback = null;
        onDisappearCompleteCallback = null;
        onDisappearCallback = null;
    }

    public void Initialize(Action _onClick, InputManager _inputManager = null)
    {
        onClickAction = _onClick;
        inputManager = _inputManager;
    }

    public void SetText(string _text)
    {
        if (null != buttonText)
        {
            buttonText.text = _text;
        }
    }

    public void SetInteractable(bool _isInteractable)
    {
        isInteractable = _isInteractable;

        if (false == isInteractable)
        {
            KillTweens();
            isHovered = false;

            if (null != motionTarget)
            {
                motionTarget.localScale = originalScale;
            }

            if (null != uiEffect)
            {
                Color _c = normalShadowColor;
                _c.a = 0.5f;
                uiEffect.shadowColor = _c;
            }

            if (null != CanvasGroup)
            {
                CanvasGroup.alpha = 0.5f;
            }
        }
        else
        {
            if (null != CanvasGroup)
            {
                CanvasGroup.alpha = 1f;
            }

            if (null != uiEffect)
            {
                uiEffect.shadowColor = normalShadowColor;
            }

            if (false == isAppearing)
            {
                CheckCursorHover();
            }
        }
    }

    public void SetClickLocked(bool _locked)
    {
        isClickLocked = _locked;
    }

    public void PrepareAppearState()
    {
        KillTweens();
        isInteractable = false;
        isAppearing = true;
        isClickLocked = true;
        isHovered = false;
        isPointerHovered = false;

        if (null != CanvasGroup)
        {
            CanvasGroup.alpha = 0f;
        }

        if (null != motionTarget)
        {
            motionTarget.localScale = Vector3.zero;
        }

        if (null != uiEffect)
        {
            uiEffect.shadowColor = normalShadowColor;
        }
    }

    public void PlayAppearAnimation()
    {
        PlayAppearAnimation(0f);
    }

    public void PlayAppearAnimation(float _delay)
    {
        KillTweens();
        isInteractable = false;
        isAppearing = true;
        isClickLocked = true;
        isHovered = false;

        if (null == motionTarget)
        {
            isAppearing = false;
            isInteractable = true;
            return;
        }

        if (null != CanvasGroup)
        {
            CanvasGroup.alpha = 0f;
        }
        motionTarget.localScale = Vector3.zero;

        Sequence _appearSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        if (_delay > 0f)
        {
            _appearSeq.AppendInterval(_delay);
        }

        _appearSeq.AppendCallback(onAppearMotionStartCallback);
        _appearSeq.Append(motionTarget.DOScale(originalScale, appearDuration).SetEase(appearEase));
        _appearSeq.OnComplete(onAppearCompleteCallback);
    }

    private void OnAppearMotionStart()
    {
        if (null != CanvasGroup)
        {
            CanvasGroup.alpha = 1f;
        }

        if (null != motionTarget)
        {
            motionTarget.localScale = new Vector3(
                originalScale.x * appearStartScale.x,
                originalScale.y * appearStartScale.y,
                originalScale.z * appearStartScale.z);
        }
    }

    private void OnAppearAnimationComplete()
    {
        isAppearing = false;
        isInteractable = true;
        CheckCursorHover();
    }

    public void PlayDisappearAnimation()
    {
        PlayDisappearAnimation(0f, null);
    }

    /// <summary>
    /// 등장 연출의 역과정으로 찌부 축소되며 페이드아웃되는 퇴장 애니메이션입니다.
    /// </summary>
    public void PlayDisappearAnimation(float _delay, Action _onComplete)
    {
        KillTweens();
        isInteractable = false;
        isAppearing = false;
        isClickLocked = false;
        isHovered = false;

        if (null == motionTarget)
        {
            if (null != CanvasGroup) CanvasGroup.alpha = 0f;
            if (null != _onComplete) _onComplete.Invoke();
            return;
        }

        Vector3 _squashTargetScale = new Vector3(
            originalScale.x * appearStartScale.x,
            originalScale.y * appearStartScale.y,
            originalScale.z * appearStartScale.z);

        onDisappearCallback = _onComplete;

        Sequence _disappearSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        if (_delay > 0f)
        {
            _disappearSeq.AppendInterval(_delay);
        }

        _disappearSeq.Append(motionTarget.DOScale(_squashTargetScale, disappearDuration).SetEase(disappearEase));
        if (null != CanvasGroup)
        {
            _disappearSeq.Join(CanvasGroup.DOFade(0f, disappearDuration * 0.8f).SetEase(Ease.InQuad));
        }

        if (null != onDisappearCompleteCallback)
        {
            _disappearSeq.OnComplete(onDisappearCompleteCallback);
        }
    }

    private void OnDisappearAnimationComplete()
    {
        if (null != motionTarget)
            motionTarget.localScale = Vector3.zero;

        if (null != CanvasGroup)
            CanvasGroup.alpha = 0f;

        if (null != onDisappearCallback)
        {
            Action _cb = onDisappearCallback;
            onDisappearCallback = null;
            _cb.Invoke();
        }
    }

    /// <summary>
    /// 마우스 커서가 "EventSystem 기준으로" 이 버튼 위에 있는지 판정합니다.
    ///
    /// 단순히 자기 레이캐스트 사각형에 커서 좌표가 들어오는지만 보면 안 된다. 버튼 슬롯(25px)보다 레이캐스트
    /// 이미지가 큰 폰트(유럽어 Lorem_Optimum은 30px)에서는 인접 버튼의 레이캐스트 영역이 서로 겹치는데,
    /// 겹친 띠에서 EventSystem은 위에 그려지는 한 버튼에만 OnPointerEnter/Exit를 보낸다. 그 띠에서 사각형
    /// 포함 검사로 호버를 켠 다른 버튼은 OnPointerExit를 영영 받지 못해 호버가 풀리지 않는다.
    /// 따라서 EventSystem이 실제로 최상위로 히트하는 오브젝트가 이 버튼 아래에 있을 때만 true를 돌려,
    /// 수동 판정과 EventSystem의 포인터 Enter/Exit가 항상 같은 버튼을 가리키게 한다.
    /// </summary>
    private bool IsCursorOverThisButton()
    {
        if (null == Mouse.current) return false;
        Vector2 _mousePos = Mouse.current.position.ReadValue();

        EventSystem _eventSystem = EventSystem.current;
        if (null != _eventSystem)
        {
            if (null == cachedPointerEventData)
            {
                cachedPointerEventData = new PointerEventData(_eventSystem);
            }

            cachedPointerEventData.Reset();
            cachedPointerEventData.position = _mousePos;

            sharedRaycastResults.Clear();
            _eventSystem.RaycastAll(cachedPointerEventData, sharedRaycastResults);

            // RaycastAll은 정렬된 결과를 돌려주므로 "gameObject가 살아 있는 첫 항목"이 실제로 포인터 이벤트를
            // 받는 최상위 오브젝트다. 입력 모듈(BaseInputModule.FindFirstRaycast)과 같은 규칙으로, 같은 프레임에
            // 파괴된 오브젝트가 맨 앞에 남아 있어도 건너뛴다.
            GameObject _topHit = null;
            for (int i = 0; sharedRaycastResults.Count > i; i++)
            {
                if (null != sharedRaycastResults[i].gameObject)
                {
                    _topHit = sharedRaycastResults[i].gameObject;
                    break;
                }
            }
            sharedRaycastResults.Clear();

            if (null == _topHit) return false;
            return _topHit.transform.IsChildOf(transform);
        }

        // EventSystem이 없으면 종전처럼 레이캐스트 이미지 사각형으로 폴백한다.
        RectTransform _hitRect = HitRectTransform;
        if (null == _hitRect) return false;

        if (null == cachedCanvas)
            cachedCanvas = GetComponentInParent<Canvas>();

        Camera _cam = (null != cachedCanvas && RenderMode.ScreenSpaceOverlay != cachedCanvas.renderMode)
            ? cachedCanvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(_hitRect, _mousePos, _cam);
    }

    /// <summary>
    /// 마우스 커서가 이미 버튼 영역에 놓여져 있는지 수동 검사하여 호버 애니메이션을 즉시 트리거합니다.
    /// </summary>
    public void CheckCursorHover()
    {
        if (false == isInteractable || false == gameObject.activeInHierarchy || true == isAppearing)
            return;

        if (null != inputManager && true == inputManager.IsGamepadMode)
            return;

        bool _contains = true == isPointerHovered || IsCursorOverThisButton();

        if (true == _contains)
        {
            isPointerHovered = true;
            if (false == isHovered)
            {
                isHovered = true;
                PlayHoverAnimation();
            }
        }
        else
        {
            isPointerHovered = false;
            if (true == isHovered)
            {
                isHovered = false;
                PlayUnhoverAnimation();
            }
        }
    }

    // isPointerHovered는 "EventSystem이 이 버튼을 포인터 아래로 보고 있는가"의 기록이다. 입력 모드와 무관하게
    // Enter/Exit 그대로 따라가야 한다. 게임패드 모드에서 Exit를 무시해 true로 남겨두면, 나중에 마우스 모드로
    // 돌아올 때 IsMouseOver()가 그 묵은 값으로 커서가 없는 버튼에 호버를 켜고, EventSystem은 이미 떠난 버튼이라
    // Exit를 다시 보내지 않아 호버가 풀리지 않는다.
    public override void OnPointerEnter(PointerEventData _eventData)
    {
        base.OnPointerEnter(_eventData);
        isPointerHovered = true;

        if (null != inputManager && true == inputManager.IsGamepadMode) return;
        if (false == isInteractable || true == isAppearing) return;

        isHovered = true;
        PlayHoverAnimation();
    }

    public override void OnPointerExit(PointerEventData _eventData)
    {
        base.OnPointerExit(_eventData);
        isPointerHovered = false;

        if (null != inputManager && true == inputManager.IsGamepadMode) return;
        if (false == isInteractable || true == isAppearing) return;

        isHovered = false;
        PlayUnhoverAnimation();
    }

    public bool IsMouseOver()
    {
        if (false == isInteractable || false == gameObject.activeInHierarchy || true == isAppearing) return false;
        if (null != inputManager && true == inputManager.IsGamepadMode) return false;
        if (true == isPointerHovered) return true;

        return IsCursorOverThisButton();
    }

    public void ForceHover()
    {
        if (false == isInteractable || true == isAppearing) return;
        isHovered = true;
        PlayHoverAnimation();
    }

    public void ForceUnhover()
    {
        isHovered = false;
        isPointerHovered = false;
        if (false == isInteractable || true == isAppearing) return;
        PlayUnhoverAnimation();
    }

    public override void OnSelect(BaseEventData _eventData)
    {
        base.OnSelect(_eventData);
        if (null != inputManager && false == inputManager.IsGamepadMode) return;

        if (false == isInteractable || true == isAppearing) return;

        isHovered = true;
        PlayHoverAnimation();
    }

    public override void OnDeselect(BaseEventData _eventData)
    {
        base.OnDeselect(_eventData);
        if (null != inputManager && false == inputManager.IsGamepadMode) return;

        if (false == isInteractable || true == isAppearing) return;

        isHovered = false;
        PlayUnhoverAnimation();
    }

    public void OnSubmit(BaseEventData _eventData)
    {
        OnPointerClick(null);
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (false == isInteractable || true == isAppearing || true == isClickLocked) return;

        isClickLocked = true;

        PlayClickAnimation();

        if (null != onClickAction)
        {
            onClickAction.Invoke();
        }
    }

    private void PlayHoverAnimation()
    {
        Sound.PlayUI(SoundID.MainButtonHover);
        PlaySquashBoingMotion();
        TweenShadowColor(hoverShadowColor, shadowTweenDuration, shadowEase);
    }

    private void PlayUnhoverAnimation()
    {
        if (null != motionTarget)
        {
            motionTarget.DOKill();
            motionTarget.DOScale(originalScale, 0.1f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        TweenShadowColor(unhoverShadowColor, shadowTweenDuration, shadowEase);
    }

    private void PlayClickAnimation()
    {
        Sound.PlayUI(SoundID.MainClick);
        PlaySquashBoingMotion();
        TweenShadowColor(clickShadowColor, shadowTweenDuration * 0.5f, Ease.OutQuad);
    }

    /// <summary>
    /// Y 스케일이 살짝 찌부되었다가 뽀잉하며 1로 원복되는 탄성 모션을 재생합니다.
    /// </summary>
    private void PlaySquashBoingMotion()
    {
        if (null == motionTarget) return;

        motionTarget.DOKill();

        float _pressTime = squashDuration * squashPressRatio;
        float _bounceTime = squashDuration * (1f - squashPressRatio);

        Sequence _squashSeq = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        _squashSeq.Append(motionTarget.DOScaleY(originalScale.y * squashYScale, _pressTime).SetEase(squashPressEase));
        _squashSeq.Append(motionTarget.DOScaleY(originalScale.y, _bounceTime).SetEase(squashBounceEase));
    }

    private void TweenShadowColor(Color _targetColor, float _duration, Ease _ease)
    {
        if (null == uiEffect) return;

        DOTween.Kill(uiEffect);
        DOTween.To(getShadowColorDelegate, setShadowColorDelegate, _targetColor, _duration)
            .SetEase(_ease)
            .SetUpdate(true)
            .SetTarget(uiEffect);
    }

    private Color GetShadowColor()
    {
        return null != uiEffect ? uiEffect.shadowColor : Color.black;
    }

    private void SetShadowColor(Color _color)
    {
        if (null != uiEffect)
        {
            uiEffect.shadowColor = _color;
        }
    }

    private void KillTweens()
    {
        if (null != motionTarget) motionTarget.DOKill();
        if (null != uiEffect) DOTween.Kill(uiEffect);
    }
}
