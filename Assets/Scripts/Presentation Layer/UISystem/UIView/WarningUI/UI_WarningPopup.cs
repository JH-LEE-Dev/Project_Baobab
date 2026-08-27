using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// 범용 경고/확인 팝업 UI 스크립트입니다.
/// 다양한 시스템 텍스트 출력과 콜백 실행을 담당하며 DOTween을 사용한 슬라이드 및 페이드 연출이 포함되어 있습니다.
/// </summary>
public class UI_WarningPopup : MonoBehaviour, IUIDepthCloseable
{
    [Header("UI References")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private UI_WarningPopupButton confirmButton;
    [SerializeField] private UI_WarningPopupButton cancelButton;
    
    [Header("Animation Settings")]
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private RectTransform popupWindowRoot;
    [SerializeField] private float animationDuration = 0.25f;
    [SerializeField] private float slideOffset = 50f;
    [SerializeField] private Ease openEase = Ease.OutCubic;
    [SerializeField] private Ease closeEase = Ease.InCubic;

    [Header("Dim Background Settings")]
    [SerializeField] private CanvasGroup dimCanvasGroup;
    [SerializeField, Range(0f, 1f)] private float dimTargetAlpha = 0.8f;
    [SerializeField] private float dimAnimationDuration = 0.25f;

    private ICursorBoxUI cursorBoxUI;
    private UIDepthController depthController;
    private InputManager inputManager;
    private Action onConfirmAction;
    private Action onCancelAction;
    private Action cachedOnUICancel;
    private Action<EInputDeviceType> cachedOnInputDeviceChanged;
    private GameObject previousSelectedGameObject;
    private SoundID openSoundId = SoundID.None;
    private SoundID closeSoundId = SoundID.None;
    private SoundID hoverSoundId = SoundID.None;
    private bool hasPlayedCloseSound;
    private bool isInitialized = false;
    private bool isClosing = false;

    private Sequence productionSequence;
    private Vector2 originalRootAnchoredPosition;

    public bool IsActive => gameObject.activeSelf;
    public InputManager InputManager => inputManager;

    /// <summary>ESC로 뎁스 스택에서 닫힐 때 호출됩니다. 확인(Confirm)이 아닌 취소(Cancel)로 처리해
    /// 파괴적인 동작이 실수로 실행되지 않게 합니다.</summary>
    public void Hide()
    {
        if (true == isClosing)
        {
            return;
        }

        OnCancelButtonClicked();
    }

    private void Awake()
    {
        if (null != popupWindowRoot)
        {
            originalRootAnchoredPosition = popupWindowRoot.anchoredPosition;
        }

        if (null == cachedOnUICancel)
        {
            cachedOnUICancel = OnCancelButtonClicked;
        }

        if (null == cachedOnInputDeviceChanged)
        {
            cachedOnInputDeviceChanged = OnInputDeviceChanged;
        }
    }

    private void OnDestroy()
    {
        isInitialized = false;
        isClosing = false;
        depthController?.UnregisterView(this);
        KillSequence();
        HideCursor();

        if (null != inputManager && null != inputManager.inputReader)
        {
            if (null != cachedOnUICancel)
            {
                inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
            }
            if (null != cachedOnInputDeviceChanged)
            {
                inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
            }
        }

        onConfirmAction = null;
        onCancelAction = null;
        cachedOnUICancel = null;
        cachedOnInputDeviceChanged = null;
        previousSelectedGameObject = null;
        cursorBoxUI = null;
        depthController = null;
        inputManager = null;
    }

    public void Initialize(UIViewContext _ctx)
    {
        if (true == isInitialized)
        {
            return;
        }

        if (null == cachedOnUICancel)
        {
            cachedOnUICancel = OnCancelButtonClicked;
        }

        if (null == cachedOnInputDeviceChanged)
        {
            cachedOnInputDeviceChanged = OnInputDeviceChanged;
        }

        if (null != _ctx)
        {
            cursorBoxUI = _ctx.cursorBoxUI;
            depthController = _ctx.depthController;
            inputManager = _ctx.inputManager;
            isInitialized = true;

            if (null != inputManager && null != inputManager.inputReader && null != cachedOnInputDeviceChanged)
            {
                inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
                inputManager.inputReader.InputDeviceChangedEvent += cachedOnInputDeviceChanged;
            }
        }
    }

    public void SetCursorBoxUI(ICursorBoxUI _cursorBoxUI)
    {
        cursorBoxUI = _cursorBoxUI;
    }

    /// <summary>
    /// 경고 팝업을 띄우고 콜백을 등록합니다.
    /// </summary>
    public void ShowWarning(
        string _message,
        Action _onConfirm,
        Action _onCancel = null,
        SoundID _openSoundId = SoundID.None,
        SoundID _closeSoundId = SoundID.None,
        SoundID _hoverSoundId = SoundID.None)
    {
        previousSelectedGameObject = EventSystem.current?.currentSelectedGameObject;

        if (null != inputManager && null != inputManager.inputReader)
        {
            if (null != cachedOnUICancel)
            {
                inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
                inputManager.inputReader.UICancelEvent += cachedOnUICancel;
            }
            if (null != cachedOnInputDeviceChanged)
            {
                inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
                inputManager.inputReader.InputDeviceChangedEvent += cachedOnInputDeviceChanged;
            }
        }

        if (null != messageText)
            messageText.text = _message;

        onConfirmAction = _onConfirm;
        onCancelAction = _onCancel;
        openSoundId = _openSoundId;
        closeSoundId = _closeSoundId;
        hoverSoundId = _hoverSoundId;
        hasPlayedCloseSound = false;
        isClosing = false;

        if (null != confirmButton)
            confirmButton.Initialize(OnConfirmButtonClicked, PlayHoverSound, this);
            
        if (null != cancelButton)
        {
            cancelButton.gameObject.SetActive(null != _onCancel);
            cancelButton.Initialize(OnCancelButtonClicked, PlayHoverSound, this);
        }

        if (null != confirmButton && null != cancelButton && true == cancelButton.gameObject.activeSelf)
        {
            Navigation _confirmNav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = cancelButton,
                selectOnLeft = cancelButton,
                selectOnUp = confirmButton,
                selectOnDown = confirmButton
            };
            Navigation _cancelNav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = confirmButton,
                selectOnRight = confirmButton,
                selectOnUp = cancelButton,
                selectOnDown = cancelButton
            };
            confirmButton.navigation = _confirmNav;
            cancelButton.navigation = _cancelNav;
        }
        else if (null != confirmButton)
        {
            Navigation _confirmNav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = confirmButton,
                selectOnLeft = confirmButton,
                selectOnUp = confirmButton,
                selectOnDown = confirmButton
            };
            confirmButton.navigation = _confirmNav;
        }

        depthController?.RegisterView(this);

        PlayConfiguredSound(openSoundId);
        PlayOpenProduction();

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            if (null != confirmButton && true == confirmButton.gameObject.activeInHierarchy)
            {
                if (null != EventSystem.current)
                {
                    if (EventSystem.current.currentSelectedGameObject == confirmButton.gameObject)
                    {
                        confirmButton.SimulateSelect();
                    }
                    else
                    {
                        EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
                    }
                }
                else
                {
                    confirmButton.SimulateSelect();
                }
            }
        }
    }

    #region Button Event Handling

    public void OnButtonHovered(UI_WarningPopupButton _button)
    {
        if (null == cursorBoxUI || null == _button)
            return;

        RectTransform targetRect = _button.GetCursorTargetRect();
        if (null == targetRect)
            return;

        Vector2 size = _button.GetCursorSize();
        Vector2 offset = _button.GetCursorOffset();

        cursorBoxUI.Show(targetRect, size, offset, CursorMotionSettings.Default);
    }

    public void OnButtonUnhovered(UI_WarningPopupButton _button)
    {
        if (null == cursorBoxUI || null == _button)
            return;

        // 키마 모드일 때만 마우스가 버튼을 벗어났을 때 커서박스를 숨긴다.
        if (null != inputManager && false == inputManager.IsGamepadMode)
        {
            RectTransform targetRect = _button.GetCursorTargetRect();
            if (null != targetRect)
            {
                cursorBoxUI.Hide(targetRect);
            }
            else
            {
                cursorBoxUI.Hide();
            }
        }
    }

    public void OnButtonClicked(UI_WarningPopupButton _button)
    {
        HideCursor();
    }

    private void HideCursor()
    {
        if (null != cursorBoxUI)
        {
            cursorBoxUI.HideImmediately();
        }
    }

    private bool IsMouseOverButton(UI_WarningPopupButton _button)
    {
        if (null == _button || false == _button.gameObject.activeInHierarchy) return false;
        if (true == _button.IsPointerHovered) return true;

        RectTransform _rect = _button.transform as RectTransform;
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

        Canvas _canvas = _button.GetComponentInParent<Canvas>();
        Camera _cam = (null != _canvas && RenderMode.ScreenSpaceOverlay != _canvas.renderMode) ? _canvas.worldCamera : null;

        return RectTransformUtility.RectangleContainsScreenPoint(_rect, _mousePos, _cam);
    }

    private void OnInputDeviceChanged(EInputDeviceType _device)
    {
        if (false == gameObject.activeInHierarchy || false == IsActive) return;

        if (EInputDeviceType.Gamepad == _device)
        {
            Cursor.visible = false;

            if (null != confirmButton) confirmButton.ResetHoverState();
            if (null != cancelButton) cancelButton.ResetHoverState();

            GameObject _selected = EventSystem.current?.currentSelectedGameObject;
            bool _isOurButton = (null != _selected &&
                                 true == _selected.activeInHierarchy &&
                                 ((null != confirmButton && _selected == confirmButton.gameObject) ||
                                  (null != cancelButton && _selected == cancelButton.gameObject)));

            if (false == _isOurButton)
            {
                if (null != confirmButton && true == confirmButton.gameObject.activeInHierarchy)
                {
                    if (null != EventSystem.current)
                    {
                        EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
                    }
                    else
                    {
                        confirmButton.SimulateSelect();
                    }
                }
            }
            else
            {
                UI_WarningPopupButton _btn = _selected.GetComponent<UI_WarningPopupButton>();
                if (null != _btn)
                {
                    _btn.SimulateSelect();
                }
            }
        }
        else if (EInputDeviceType.KeyboardMouse == _device)
        {
            Cursor.visible = true;

            if (true == IsMouseOverButton(confirmButton))
            {
                OnButtonHovered(confirmButton);
            }
            else if (true == IsMouseOverButton(cancelButton))
            {
                OnButtonHovered(cancelButton);
            }
            else
            {
                HideCursor();
            }
        }
    }


    #endregion

    public void HideImmediately()
    {
        depthController?.UnregisterView(this);
        KillSequence();
        HideCursor();
        onConfirmAction = null;
        onCancelAction = null;

        if (null != popupCanvasGroup)
        {
            popupCanvasGroup.interactable = false;
            popupCanvasGroup.blocksRaycasts = false;
            popupCanvasGroup.alpha = 0f;
        }

        if (null != dimCanvasGroup)
        {
            dimCanvasGroup.alpha = 0f;
        }

        gameObject.SetActive(false);
        RestorePreviousFocus();
    }

    private void RestorePreviousFocus()
    {
        if (null != inputManager && null != inputManager.inputReader && null != cachedOnUICancel)
        {
            inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
        }

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            if (null != previousSelectedGameObject && true == previousSelectedGameObject.activeInHierarchy)
            {
                if (null != EventSystem.current)
                {
                    EventSystem.current.SetSelectedGameObject(previousSelectedGameObject);
                }

                UI_OptionButton _ob = previousSelectedGameObject.GetComponent<UI_OptionButton>();
                if (null != _ob)
                {
                    _ob.ShowCursor();
                }
                else
                {
                    UI_OptionSelector _os = previousSelectedGameObject.GetComponent<UI_OptionSelector>();
                    if (null != _os)
                    {
                        _os.ShowCursor();
                        _os.ApplyFocusVisual(true);
                    }
                    else
                    {
                        UI_OptionSlider _osl = previousSelectedGameObject.GetComponent<UI_OptionSlider>();
                        if (null != _osl)
                        {
                            _osl.ShowCursor();
                            _osl.ApplyFocusVisual(true);
                        }
                        else
                        {
                            UI_OptionTabButton _otb = previousSelectedGameObject.GetComponent<UI_OptionTabButton>();
                            if (null != _otb)
                            {
                                _otb.ShowCursor();
                            }
                        }
                    }
                }
            }
        }
        else
        {
            if (null != EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
        previousSelectedGameObject = null;
    }

    private void OnConfirmButtonClicked()
    {
        if (true == isClosing)
        {
            return;
        }
        isClosing = true;

        HideCursor();

        Action _confirm = onConfirmAction;
        onConfirmAction = null;
        onCancelAction = null;

        if (null != _confirm)
        {
            _confirm.Invoke();
        }

        PlayCloseProduction();
    }

    private void OnCancelButtonClicked()
    {
        if (true == isClosing)
        {
            return;
        }
        isClosing = true;

        HideCursor();

        Action _cancel = onCancelAction;
        onConfirmAction = null;
        onCancelAction = null;

        if (null != _cancel)
        {
            _cancel.Invoke();
        }

        PlayCloseProduction();
    }

    private void PlayOpenProduction()
    {
        gameObject.SetActive(true);
        KillSequence();

        if (null == popupCanvasGroup || null == popupWindowRoot)
            return;

        popupCanvasGroup.interactable = true;
        popupCanvasGroup.blocksRaycasts = true;

        // 초기 상태 세팅 (투명, 아래로 내려간 상태)
        popupCanvasGroup.alpha = 0f;
        popupWindowRoot.anchoredPosition = originalRootAnchoredPosition + new Vector2(0f, -slideOffset);

        if (null != dimCanvasGroup)
        {
            dimCanvasGroup.alpha = 0f;
        }

        productionSequence = DOTween.Sequence().SetUpdate(true);
        
        productionSequence.Join(popupCanvasGroup.DOFade(1f, animationDuration).SetEase(openEase));
        productionSequence.Join(popupWindowRoot.DOAnchorPosY(originalRootAnchoredPosition.y, animationDuration).SetEase(openEase));

        if (null != dimCanvasGroup)
        {
            productionSequence.Join(dimCanvasGroup.DOFade(dimTargetAlpha, dimAnimationDuration));
        }

        productionSequence.OnComplete(() =>
        {
            if (null != inputManager && true == inputManager.IsGamepadMode)
            {
                if (null != confirmButton && true == confirmButton.gameObject.activeInHierarchy)
                {
                    if (null != EventSystem.current && EventSystem.current.currentSelectedGameObject != confirmButton.gameObject)
                    {
                        EventSystem.current.SetSelectedGameObject(confirmButton.gameObject);
                    }
                }
            }
        });
    }

    private void PlayCloseProduction()
    {
        depthController?.UnregisterView(this);

        if (null != inputManager && null != inputManager.inputReader)
        {
            if (null != cachedOnUICancel)
            {
                inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
            }
            if (null != cachedOnInputDeviceChanged)
            {
                inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
            }
        }

        if (false == hasPlayedCloseSound)
        {
            hasPlayedCloseSound = true;
            PlayConfiguredSound(closeSoundId);
        }

        KillSequence();
        HideCursor();

        if (null == popupCanvasGroup || null == popupWindowRoot)
        {
            isClosing = false;
            gameObject.SetActive(false);
            return;
        }

        popupCanvasGroup.interactable = false;
        popupCanvasGroup.blocksRaycasts = false;

        productionSequence = DOTween.Sequence().SetUpdate(true);
        
        float targetY = originalRootAnchoredPosition.y - slideOffset;
        
        productionSequence.Join(popupCanvasGroup.DOFade(0f, animationDuration).SetEase(closeEase));
        productionSequence.Join(popupWindowRoot.DOAnchorPosY(targetY, animationDuration).SetEase(closeEase));
        
        if (null != dimCanvasGroup)
        {
            productionSequence.Join(dimCanvasGroup.DOFade(0f, dimAnimationDuration));
        }

        productionSequence.OnComplete(OnCloseProductionComplete);
    }

    private void OnCloseProductionComplete()
    {
        isClosing = false;
        gameObject.SetActive(false);
        RestorePreviousFocus();
    }

    private void KillSequence()
    {
        if (null != productionSequence)
        {
            productionSequence.Kill();
            productionSequence = null;
        }
    }

    private void PlayHoverSound()
    {
        PlayConfiguredSound(hoverSoundId);
    }

    private static void PlayConfiguredSound(SoundID _soundId)
    {
        if (SoundID.None != _soundId)
        {
            Sound.PlayUI(_soundId);
        }
    }
}
