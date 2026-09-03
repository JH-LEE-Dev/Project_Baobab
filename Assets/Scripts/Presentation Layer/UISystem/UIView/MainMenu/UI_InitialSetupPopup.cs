using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 게임 최초 실행 시 스플래시 직후 언어 설정 및 데이터 수집 약관 동의를 진행하는 팝업 컨트롤러입니다.
/// 1단계: 언어 선택 패널 -> 2단계: 약관 동의 패널 순으로 진행됩니다.
/// </summary>
public class UI_InitialSetupPopup : MonoBehaviour
{
    private const int MAIN_MENU_JSON_ID = 8;

    [Header("Root & Background")]
    [SerializeField] private CanvasGroup rootCanvasGroup;
    [SerializeField] private RectTransform windowRoot;
    [SerializeField] private Image backgroundDimmer;
    [SerializeField] [Range(0f, 1f)] private float dimmerTargetAlpha = 0.75f;
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private float slideOffset = 50f;
    [SerializeField] private Ease openEase = Ease.OutCubic;
    [SerializeField] private Ease closeEase = Ease.InCubic;

    [Header("1. Language Panel")]
    [SerializeField] private CanvasGroup languagePanel;
    [SerializeField] private UI_PanelSelectButton[] languageButtons;

    [Header("2. Consent Panel")]
    [SerializeField] private CanvasGroup consentPanel;
    [SerializeField] private TextMeshProUGUI consentTitleText;
    [SerializeField] private TextMeshProUGUI consentDescText;
    [SerializeField] private Toggle consentToggle;
    [SerializeField] private TextMeshProUGUI consentToggleLabel;
    [SerializeField] private Toggle consentDisagreeToggle;
    [SerializeField] private TextMeshProUGUI consentDisagreeToggleLabel;
    [SerializeField] private UI_PopupButton confirmButton;

    [Header("3. Consent Visual Colors")]
    [SerializeField] private Color consentNormalTextColor = Color.white;
    [SerializeField] private Color consentSelectedTextColor = new Color(1.0f, 0.835f, 0.31f, 1.0f); // #FFD54F (골드 옐로우)

    // 외부 의존성
    private InputManager inputManager;
    private LocalizationManager localizationManager;
    private ICursorBoxUI cursorBoxUI;

    // 내부 상태
    private Action onCompletedCallback;
    private Action<EInputDeviceType> cachedOnDeviceChanged;
    private Sequence panelTransitionTween;
    private bool isConsentPhase = false;
    private Toggle hoveredConsentToggle = null;
    private UI_PanelSelectButton lastFocusedLanguageButton;
    private Selectable lastFocusedConsentSelectable;
    private Vector2 originalWindowPos = Vector2.zero;

    private readonly EOptionLanguage[] supportedLanguages = new EOptionLanguage[]
    {
        EOptionLanguage.Korean,
        EOptionLanguage.English,
        EOptionLanguage.Japanese,
        EOptionLanguage.ChineseSimplified,
        EOptionLanguage.ChineseTraditional
    };

    private readonly string[] languageDisplayNames = new string[]
    {
        "한국어",
        "English",
        "日本語",
        "简体中文",
        "繁體中文"
    };

    public bool IsActive => gameObject.activeInHierarchy && (null == rootCanvasGroup || 0f < rootCanvasGroup.alpha);

    private void Awake()
    {
        EnsureRootCanvasGroup();
        if (null != windowRoot)
        {
            originalWindowPos = windowRoot.anchoredPosition;
        }
    }

    private void EnsureRootCanvasGroup()
    {
        if (null == rootCanvasGroup)
        {
            rootCanvasGroup = GetComponent<CanvasGroup>();
            if (null == rootCanvasGroup)
            {
                rootCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    public void Initialize(InputManager _inputManager, LocalizationManager _locManager, ICursorBoxUI _cursorBoxUI)
    {
        EnsureRootCanvasGroup();
        inputManager = _inputManager;
        localizationManager = _locManager;
        cursorBoxUI = _cursorBoxUI;
        cachedOnDeviceChanged = OnDeviceChanged;

        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        InitLanguageButtons();
        InitConsentPanel();
        SetupSpatialNavigations();

        gameObject.SetActive(false);
    }

    private void SetupSpatialNavigations()
    {
        // 1. 5개 언어 버튼 2D 그리드 네비게이션 직결
        // 상단 행: Korean(좌) <-> English(중) <-> Japanese(우)
        // 하단 행: ChineseSimplified(좌) <-> ChineseTraditional(우)
        UI_PanelSelectButton _btnKr = null;
        UI_PanelSelectButton _btnEn = null;
        UI_PanelSelectButton _btnJp = null;
        UI_PanelSelectButton _btnSim = null;
        UI_PanelSelectButton _btnTrad = null;

        if (null != languageButtons)
        {
            for (int i = 0; i < languageButtons.Length; i++)
            {
                UI_PanelSelectButton _b = languageButtons[i];
                if (null == _b) continue;

                switch (_b.BoundLanguage)
                {
                    case EOptionLanguage.Korean: _btnKr = _b; break;
                    case EOptionLanguage.English: _btnEn = _b; break;
                    case EOptionLanguage.Japanese: _btnJp = _b; break;
                    case EOptionLanguage.ChineseSimplified: _btnSim = _b; break;
                    case EOptionLanguage.ChineseTraditional: _btnTrad = _b; break;
                }
            }
        }

        if (null != _btnKr && null != _btnEn && null != _btnJp && null != _btnSim && null != _btnTrad)
        {
            _btnKr.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = _btnKr,
                selectOnDown = _btnSim,
                selectOnLeft = _btnTrad,
                selectOnRight = _btnEn
            };

            _btnEn.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = _btnEn,
                selectOnDown = _btnTrad,
                selectOnLeft = _btnKr,
                selectOnRight = _btnJp
            };

            _btnJp.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = _btnJp,
                selectOnDown = _btnTrad,
                selectOnLeft = _btnEn,
                selectOnRight = _btnSim
            };

            _btnSim.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = _btnKr,
                selectOnDown = _btnSim,
                selectOnLeft = _btnJp,
                selectOnRight = _btnTrad
            };

            _btnTrad.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = _btnEn,
                selectOnDown = _btnTrad,
                selectOnLeft = _btnSim,
                selectOnRight = _btnKr
            };
        }

        // 2. Consent 패널 상하 네비게이션 연결 (Toggle <-> DisagreeToggle <-> ConfirmButton)
        UpdateConsentNavigations();
    }

    private void UpdateConsentNavigations()
    {
        if (null != consentToggle)
        {
            consentToggle.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = null,
                selectOnDown = consentDisagreeToggle ?? (Selectable)confirmButton,
                selectOnLeft = null,
                selectOnRight = null
            };
        }

        if (null != consentDisagreeToggle)
        {
            consentDisagreeToggle.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = consentToggle,
                selectOnDown = (null != confirmButton && true == confirmButton.interactable) ? (Selectable)confirmButton : null,
                selectOnLeft = null,
                selectOnRight = null
            };
        }

        if (null != confirmButton)
        {
            confirmButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = consentDisagreeToggle ?? (Selectable)consentToggle,
                selectOnDown = null,
                selectOnLeft = null,
                selectOnRight = null
            };
        }
    }

    private void InitLanguageButtons()
    {
        if (null == languageButtons) return;

        for (int i = 0; i < languageButtons.Length; i++)
        {
            UI_PanelSelectButton _btn = languageButtons[i];
            if (null == _btn) continue;

            EOptionLanguage _lang = EOptionLanguage.Korean;
            string _name = "한국어";

            string _btnName = _btn.gameObject.name;
            if (_btnName.Contains("KoreanTrad") || _btnName.Contains("ChineseTrad"))
            {
                _lang = EOptionLanguage.ChineseTraditional;
                _name = "繁體中文";
            }
            else if (_btnName.Contains("ChineseSim") || _btnName.Contains("Chinese"))
            {
                _lang = EOptionLanguage.ChineseSimplified;
                _name = "简体中文";
            }
            else if (_btnName.Contains("Japan") || _btnName.Contains("Japanese"))
            {
                _lang = EOptionLanguage.Japanese;
                _name = "日本語";
            }
            else if (_btnName.Contains("English"))
            {
                _lang = EOptionLanguage.English;
                _name = "English";
            }
            else
            {
                _lang = EOptionLanguage.Korean;
                _name = "한국어";
            }

            _btn.Initialize(inputManager, cursorBoxUI, null);
            _btn.SetBoundLanguage(_lang, _name);
            _btn.OnClickedEvent -= HandleLanguageButtonClicked;
            _btn.OnClickedEvent += HandleLanguageButtonClicked;
        }
    }

    private void InitConsentPanel()
    {
        if (null != consentToggle)
        {
            consentToggle.isOn = false;
            consentToggle.onValueChanged.RemoveListener(HandleConsentToggleValueChanged);
            consentToggle.onValueChanged.AddListener(HandleConsentToggleValueChanged);
            BindConsentToggleTriggers(consentToggle, consentToggleLabel);
        }

        if (null != consentDisagreeToggle)
        {
            consentDisagreeToggle.isOn = false;
            consentDisagreeToggle.onValueChanged.RemoveListener(HandleConsentDisagreeToggleValueChanged);
            consentDisagreeToggle.onValueChanged.AddListener(HandleConsentDisagreeToggleValueChanged);
            BindConsentToggleTriggers(consentDisagreeToggle, consentDisagreeToggleLabel);
        }

        if (null != confirmButton)
        {
            confirmButton.Initialize(inputManager, cursorBoxUI, HandleConfirmButtonClicked);
        }

        UpdateConfirmButtonState(true);
    }

    private void BindConsentToggleTriggers(Toggle _toggle, TextMeshProUGUI _label)
    {
        if (null == _toggle) return;

        BindSingleToggleEvents(_toggle.gameObject, _toggle, _label, false);

        if (null != _label)
        {
            _label.raycastTarget = true;
            BindSingleToggleEvents(_label.gameObject, _toggle, _label, true);
        }
    }

    private void BindSingleToggleEvents(GameObject _targetGo, Toggle _toggle, TextMeshProUGUI _label, bool _isLabel)
    {
        if (null == _targetGo) return;

        EventTrigger _trigger = _targetGo.GetComponent<EventTrigger>();
        if (null == _trigger)
        {
            _trigger = _targetGo.AddComponent<EventTrigger>();
        }

        AddTriggerEntry(_trigger, EventTriggerType.PointerEnter, (_eventData) =>
        {
            hoveredConsentToggle = _toggle;
            if (null != inputManager && true == inputManager.IsGamepadMode) return;
            ShowConsentToggleCursor(_toggle, _label);
        });

        AddTriggerEntry(_trigger, EventTriggerType.PointerExit, (_eventData) =>
        {
            if (hoveredConsentToggle == _toggle)
            {
                hoveredConsentToggle = null;
            }
            if (null != inputManager && true == inputManager.IsGamepadMode) return;
            HideConsentToggleCursor(_toggle);
        });

        if (true == _isLabel)
        {
            AddTriggerEntry(_trigger, EventTriggerType.PointerClick, (_eventData) =>
            {
                if (null != inputManager && true == inputManager.IsGamepadMode) return;
                _toggle.isOn = true;
            });
        }
        else
        {
            AddTriggerEntry(_trigger, EventTriggerType.Select, (_eventData) =>
            {
                if (null != inputManager && false == inputManager.IsGamepadMode) return;
                ShowConsentToggleCursor(_toggle, _label);
            });

            AddTriggerEntry(_trigger, EventTriggerType.Deselect, (_eventData) =>
            {
                if (null != inputManager && false == inputManager.IsGamepadMode) return;
                HideConsentToggleCursor(_toggle);
            });
        }
    }

    private void AddTriggerEntry(EventTrigger _trigger, EventTriggerType _type, UnityEngine.Events.UnityAction<BaseEventData> _callback)
    {
        if (null == _trigger || null == _callback) return;

        EventTrigger.Entry _entry = new EventTrigger.Entry();
        _entry.eventID = _type;
        _entry.callback.AddListener(_callback);
        _trigger.triggers.Add(_entry);
    }

    private void HandleConsentToggleValueChanged(bool _isOn)
    {
        if (true == _isOn)
        {
            Sound.PlayUI(SoundID.OptionClick);
            if (null != consentDisagreeToggle && true == consentDisagreeToggle.isOn)
            {
                consentDisagreeToggle.isOn = false;
            }
        }
        UpdateConfirmButtonState();
    }

    private void HandleConsentDisagreeToggleValueChanged(bool _isOn)
    {
        if (true == _isOn)
        {
            Sound.PlayUI(SoundID.OptionClick);
            if (null != consentToggle && true == consentToggle.isOn)
            {
                consentToggle.isOn = false;
            }
        }
        UpdateConfirmButtonState();
    }

    private void UpdateConfirmButtonState(bool _instant = false)
    {
        bool _hasSelection = (null != consentToggle && true == consentToggle.isOn)
            || (null != consentDisagreeToggle && true == consentDisagreeToggle.isOn);

        if (null != confirmButton)
        {
            confirmButton.SetInteractable(_hasSelection, _instant);
        }

        UpdateConsentToggleTextColors();
        UpdateConsentNavigations();
    }

    private void UpdateConsentToggleTextColors()
    {
        if (null != consentToggleLabel && null != consentToggle)
        {
            consentToggleLabel.color = (true == consentToggle.isOn)
                ? consentSelectedTextColor
                : consentNormalTextColor;
        }

        if (null != consentDisagreeToggleLabel && null != consentDisagreeToggle)
        {
            consentDisagreeToggleLabel.color = (true == consentDisagreeToggle.isOn)
                ? consentSelectedTextColor
                : consentNormalTextColor;
        }
    }

    public void Show(Action _onCompleted)
    {
        onCompletedCallback = _onCompleted;
        isConsentPhase = false;
        gameObject.SetActive(true);
        Sound.PlayUI(SoundID.ResultUIOpen);

        if (null != inputManager)
        {
            inputManager.SetInputMode(EInputMode.UI);
            if (null != inputManager.inputReader && null != cachedOnDeviceChanged)
            {
                inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
                inputManager.inputReader.InputDeviceChangedEvent += cachedOnDeviceChanged;
            }
        }

        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.interactable = true;
            rootCanvasGroup.blocksRaycasts = true;
        }

        // 1단계 언어 패널 먼저 활성화
        if (null != languagePanel)
        {
            languagePanel.gameObject.SetActive(true);
            languagePanel.alpha = 1f;
            languagePanel.interactable = true;
            languagePanel.blocksRaycasts = true;
        }

        if (null != consentPanel)
        {
            consentPanel.gameObject.SetActive(false);
            consentPanel.alpha = 0f;
            consentPanel.blocksRaycasts = false;
        }

        if (null != confirmButton)
        {
            confirmButton.gameObject.SetActive(false);
        }

        SetupSpatialNavigations();
        RefreshLocalizedTexts();
        ClearLanguageSelection();

        // 루트 페이드인 및 슬라이드 연출
        KillTransition();
        Sequence _seq = DOTween.Sequence();
        if (null != rootCanvasGroup)
        {
            _seq.Join(rootCanvasGroup.DOFade(1f, fadeDuration).SetEase(openEase));
        }
        if (null != windowRoot)
        {
            windowRoot.DOKill();
            windowRoot.anchoredPosition = new Vector2(originalWindowPos.x, originalWindowPos.y - slideOffset);
            _seq.Join(windowRoot.DOAnchorPosY(originalWindowPos.y, fadeDuration).SetEase(openEase));
        }
        if (null != backgroundDimmer)
        {
            backgroundDimmer.DOKill();
            backgroundDimmer.gameObject.SetActive(true);
            Color _dimColor = backgroundDimmer.color;
            _dimColor.a = 0f;
            backgroundDimmer.color = _dimColor;
            _seq.Join(backgroundDimmer.DOFade(dimmerTargetAlpha, fadeDuration).SetEase(openEase));
        }
        _seq.OnComplete(HandleShowCompleted);
        _seq.SetTarget(this);
        panelTransitionTween = _seq;
    }

    /// <summary>
    /// 처음 언어설정 진입 시에는 어떤 언어도 미리 선택되어 있지 않아야 합니다.
    /// 모든 언어 버튼을 미선택(회색) 상태로 초기화하고, 게임패드 첫 포커스 대상만 한국어로 지정합니다.
    /// </summary>
    private void ClearLanguageSelection()
    {
        if (null == languageButtons) return;

        for (int i = 0; i < languageButtons.Length; i++)
        {
            UI_PanelSelectButton _btn = languageButtons[i];
            if (null == _btn) continue;

            _btn.SetSelected(false);
            _btn.ForceUnhover();
        }

        // 게임패드로 열었을 때 첫 포커스 대상은 한국어 버튼
        lastFocusedLanguageButton = GetKoreanLanguageButton() ?? GetFirstLanguageButton();
    }

    /// <summary>
    /// 유저가 언어 버튼을 클릭했을 때 선택 상태 비주얼을 반영합니다.
    /// </summary>
    private void ApplyLanguageSelection(EOptionLanguage _selected)
    {
        if (null == languageButtons) return;

        for (int i = 0; i < languageButtons.Length; i++)
        {
            UI_PanelSelectButton _btn = languageButtons[i];
            if (null == _btn) continue;

            bool _isCurrent = (_btn.BoundLanguage == _selected);
            _btn.SetSelected(_isCurrent);
            _btn.ForceUnhover();

            if (true == _isCurrent)
            {
                lastFocusedLanguageButton = _btn;
            }
        }
    }

    private void HandleShowCompleted()
    {
        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            FocusLanguageButton(lastFocusedLanguageButton ?? GetKoreanLanguageButton() ?? GetFirstLanguageButton());
        }
        else if (null != EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private UI_PanelSelectButton GetKoreanLanguageButton()
    {
        if (null == languageButtons || 0 == languageButtons.Length) return null;
        for (int i = 0; i < languageButtons.Length; i++)
        {
            UI_PanelSelectButton _btn = languageButtons[i];
            if (null != _btn && _btn.BoundLanguage == EOptionLanguage.Korean && true == _btn.gameObject.activeInHierarchy)
            {
                return _btn;
            }
        }
        return GetFirstLanguageButton();
    }

    private UI_PanelSelectButton GetFirstLanguageButton()
    {
        if (null == languageButtons || 0 == languageButtons.Length) return null;
        for (int i = 0; i < languageButtons.Length; i++)
        {
            if (null != languageButtons[i] && true == languageButtons[i].gameObject.activeInHierarchy)
            {
                return languageButtons[i];
            }
        }
        return null;
    }

    private void FocusLanguageButton(UI_PanelSelectButton _target)
    {
        if (null == _target) return;
        lastFocusedLanguageButton = _target;

        if (null != EventSystem.current)
        {
            if (EventSystem.current.currentSelectedGameObject == _target.gameObject)
            {
                _target.ForceHover();
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(_target.gameObject);
            }
        }
    }

    private void FocusConsentItem(Selectable _target)
    {
        if (null == _target) return;
        lastFocusedConsentSelectable = _target;

        if (_target == consentToggle)
        {
            if (null != confirmButton) confirmButton.ForceUnhover();
            HideConsentToggleCursor(consentDisagreeToggle);
            ShowConsentToggleCursor(consentToggle, consentToggleLabel);
        }
        else if (_target == consentDisagreeToggle)
        {
            if (null != confirmButton) confirmButton.ForceUnhover();
            HideConsentToggleCursor(consentToggle);
            ShowConsentToggleCursor(consentDisagreeToggle, consentDisagreeToggleLabel);
        }
        else if (_target == confirmButton)
        {
            HideConsentToggleCursor();
            if (null != confirmButton) confirmButton.ForceHover();
        }

        if (null != EventSystem.current)
        {
            if (EventSystem.current.currentSelectedGameObject != _target.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(_target.gameObject);
            }
        }
    }

    private void ShowConsentToggleCursor(Toggle _toggle, TextMeshProUGUI _label)
    {
        if (null == cursorBoxUI || null == _toggle) return;

        RectTransform _rect = _toggle.GetComponent<RectTransform>();
        if (null == _rect) return;

        float _width = (_rect.rect.width > 0f) ? _rect.rect.width : 160f;
        Vector2 _size = new Vector2(_width + 12f, 28f);

        cursorBoxUI.Show(_rect, _size, Vector2.zero, CursorMotionSettings.RowSubtle);
    }

    private void HideConsentToggleCursor(Toggle _toggle = null)
    {
        if (null == cursorBoxUI) return;

        if (null != _toggle)
        {
            RectTransform _rect = _toggle.GetComponent<RectTransform>();
            if (null != _rect)
            {
                cursorBoxUI.Hide(_rect);
                return;
            }
        }

        if (null != consentToggle)
        {
            RectTransform _rect1 = consentToggle.GetComponent<RectTransform>();
            if (null != _rect1) cursorBoxUI.Hide(_rect1);
        }
        if (null != consentDisagreeToggle)
        {
            RectTransform _rect2 = consentDisagreeToggle.GetComponent<RectTransform>();
            if (null != _rect2) cursorBoxUI.Hide(_rect2);
        }
    }


    private void HandleLanguageButtonClicked(UI_PanelSelectButton _btn)
    {
        if (null == _btn) return;

        // 1. 선택한 언어 적용
        EOptionLanguage _selected = _btn.BoundLanguage;
        SettingsManager.Instance.SetLanguage(_selected);

        // 선택 표시를 방금 누른 버튼으로 옮긴다.
        ApplyLanguageSelection(_selected);

        RefreshLocalizedTexts();

        // 2. 언어 패널 -> 약관 동의 패널 전환
        TransitionToConsentPanel();
    }

    private void TransitionToConsentPanel()
    {
        KillTransition();
        isConsentPhase = true;

        Sequence _seq = DOTween.Sequence();

        if (null != languagePanel)
        {
            languagePanel.blocksRaycasts = false;
            _seq.Append(languagePanel.DOFade(0f, fadeDuration * 0.7f).SetEase(Ease.InQuad));
        }

        _seq.AppendCallback(SetupConsentPanelOnTransition);

        if (null != consentPanel)
        {
            _seq.Append(consentPanel.DOFade(1f, fadeDuration * 0.7f).SetEase(Ease.OutQuad));
        }

        _seq.OnComplete(HandleConsentPanelShown);
        _seq.SetTarget(this);
        panelTransitionTween = _seq;
    }

    private void SetupConsentPanelOnTransition()
    {
        if (null != languagePanel) languagePanel.gameObject.SetActive(false);
        if (null != consentPanel)
        {
            consentPanel.gameObject.SetActive(true);
            consentPanel.alpha = 0f;
            consentPanel.interactable = true;
            consentPanel.blocksRaycasts = true;
        }
        if (null != confirmButton)
        {
            confirmButton.gameObject.SetActive(true);
        }

        if (null != consentToggle) consentToggle.isOn = false;
        if (null != consentDisagreeToggle) consentDisagreeToggle.isOn = false;
        UpdateConfirmButtonState(true);

        SnapConsentTogglesPixelPerfect();
    }

    private void HandleConsentPanelShown()
    {
        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            FocusConsentItem(lastFocusedConsentSelectable ?? (Selectable)consentToggle);
        }
    }

    private void HandleConfirmButtonClicked()
    {
        if (null != consentToggle && true == consentToggle.isOn)
        {
            SettingsManager.Instance.SetDataConsent(EDataConsent.Granted);
        }
        else if (null != consentDisagreeToggle && true == consentDisagreeToggle.isOn)
        {
            SettingsManager.Instance.SetDataConsent(EDataConsent.Declined);
        }
        else
        {
            return;
        }

        Close();
    }

    public void Close()
    {
        KillTransition();

        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.blocksRaycasts = false;
        }

        if (null != cursorBoxUI)
        {
            cursorBoxUI.Hide();
        }

        Sound.PlayUI(SoundID.ResultUIClose);

        Sequence _seq = DOTween.Sequence();
        if (null != rootCanvasGroup)
        {
            _seq.Join(rootCanvasGroup.DOFade(0f, fadeDuration).SetEase(closeEase));
        }
        if (null != windowRoot)
        {
            windowRoot.DOKill();
            float _targetY = originalWindowPos.y - slideOffset;
            _seq.Join(windowRoot.DOAnchorPosY(_targetY, fadeDuration).SetEase(closeEase));
        }
        if (null != backgroundDimmer)
        {
            _seq.Join(backgroundDimmer.DOFade(0f, fadeDuration).SetEase(closeEase));
        }

        _seq.OnComplete(HandleCloseCompleted);
        _seq.SetTarget(this);
        panelTransitionTween = _seq;
    }

    private void HandleCloseCompleted()
    {
        if (null != confirmButton)
        {
            confirmButton.gameObject.SetActive(false);
        }

        if (null != backgroundDimmer)
        {
            backgroundDimmer.gameObject.SetActive(false);
        }

        if (null != inputManager)
        {
            if (null != inputManager.inputReader && null != cachedOnDeviceChanged)
            {
                inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
            }
            inputManager.SetInputMode(EInputMode.Gameplay);
        }

        isConsentPhase = false;
        gameObject.SetActive(false);

        Action _cb = onCompletedCallback;
        onCompletedCallback = null;
        if (null != _cb)
        {
            _cb.Invoke();
        }
    }

    private void RefreshLocalizedTexts()
    {
        if (null == localizationManager) return;

        if (null != consentTitleText)
        {
            string _txt = localizationManager.GetText(MAIN_MENU_JSON_ID, 101);
            if (false == string.IsNullOrEmpty(_txt)) consentTitleText.text = _txt;
        }

        if (null != consentDescText)
        {
            string _txt = localizationManager.GetText(MAIN_MENU_JSON_ID, 102);
            if (false == string.IsNullOrEmpty(_txt)) consentDescText.text = _txt;
        }

        if (null != consentToggleLabel)
        {
            string _txt = localizationManager.GetText(MAIN_MENU_JSON_ID, 103);
            if (false == string.IsNullOrEmpty(_txt)) consentToggleLabel.text = _txt;
        }

        if (null != consentDisagreeToggleLabel)
        {
            string _txt = localizationManager.GetText(MAIN_MENU_JSON_ID, 105);
            if (false == string.IsNullOrEmpty(_txt)) consentDisagreeToggleLabel.text = _txt;
        }

        SnapConsentTogglesPixelPerfect();
    }

    private void SnapConsentTogglesPixelPerfect()
    {
        if (null == consentToggle || null == consentDisagreeToggle) return;

        // 1. 두 토글의 텍스트 레이블 raycastTarget 보장 및 preferredWidth 계산
        int _textWidthAgree = 0;
        int _textHeightAgree = 16;
        if (null != consentToggleLabel)
        {
            consentToggleLabel.raycastTarget = true;
            _textWidthAgree = Mathf.CeilToInt(consentToggleLabel.preferredWidth);
            _textHeightAgree = Mathf.Max(16, Mathf.CeilToInt(consentToggleLabel.preferredHeight));
        }

        int _textWidthDisagree = 0;
        int _textHeightDisagree = 16;
        if (null != consentDisagreeToggleLabel)
        {
            consentDisagreeToggleLabel.raycastTarget = true;
            _textWidthDisagree = Mathf.CeilToInt(consentDisagreeToggleLabel.preferredWidth);
            _textHeightDisagree = Mathf.Max(16, Mathf.CeilToInt(consentDisagreeToggleLabel.preferredHeight));
        }

        // 2. 체크박스(Background) 규격
        int _boxSize = 16;
        int _spacing = 8;

        // 3. 두 항목 중 더 긴 항목 기준으로 공통 너비 계산 (짝수 스냅으로 .5px 방지)
        int _maxTextWidth = Mathf.Max(_textWidthAgree, _textWidthDisagree);
        int _commonTotalWidth = _boxSize + _spacing + _maxTextWidth;
        if (0 != (_commonTotalWidth % 2))
        {
            _commonTotalWidth += 1;
        }

        int _commonHeight = Mathf.Max(_boxSize, Mathf.Max(_textHeightAgree, _textHeightDisagree));

        // 4. 두 토글에 동일한 공통 너비 및 왼쪽 정렬 기준점 적용 -> 두 체크박스의 X위치 일치 및 전체 중앙 정렬
        SnapSingleToggleWithCommonWidth(consentToggle, consentToggleLabel, _textWidthAgree, _textHeightAgree, _commonTotalWidth, _commonHeight, _boxSize, _spacing);
        SnapSingleToggleWithCommonWidth(consentDisagreeToggle, consentDisagreeToggleLabel, _textWidthDisagree, _textHeightDisagree, _commonTotalWidth, _commonHeight, _boxSize, _spacing);

        UpdateConsentToggleTextColors();
    }

    private void SnapSingleToggleWithCommonWidth(Toggle _toggle, TextMeshProUGUI _label, int _textWidth, int _textHeight, int _commonTotalWidth, int _commonHeight, int _boxSize, int _spacing)
    {
        if (null == _toggle) return;

        RectTransform _toggleRect = _toggle.GetComponent<RectTransform>();
        if (null == _toggleRect) return;

        // Toggle 부모 앵커 및 크기 스냅 (두 토글 모두 동일한 너비)
        _toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
        _toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        _toggleRect.pivot = new Vector2(0.5f, 0.5f);
        _toggleRect.sizeDelta = new Vector2(_commonTotalWidth, _commonHeight);
        _toggleRect.anchoredPosition = new Vector2(0f, Mathf.Round(_toggleRect.anchoredPosition.y));

        // 원형 체크박스 (Background): 공통 너비의 가장 왼쪽 시작점에 배치 -> 두 토글의 X좌표가 정확히 일치!
        Transform _bgTransform = _toggle.transform.Find("Background");
        if (null != _bgTransform)
        {
            RectTransform _bgRect = _bgTransform.GetComponent<RectTransform>();
            if (null != _bgRect)
            {
                _bgRect.anchorMin = new Vector2(0.5f, 0.5f);
                _bgRect.anchorMax = new Vector2(0.5f, 0.5f);
                _bgRect.pivot = new Vector2(0.5f, 0.5f);
                _bgRect.sizeDelta = new Vector2(_boxSize, _boxSize);
                int _bgPosX = -(_commonTotalWidth / 2) + (_boxSize / 2);
                _bgRect.anchoredPosition = new Vector2(_bgPosX, 0f);
            }
        }

        // 텍스트 (ToggleTMP): 체크박스 오른쪽 8px에서 시작하도록 배치
        if (null != _label)
        {
            RectTransform _labelRect = _label.rectTransform;
            if (null != _labelRect)
            {
                _labelRect.anchorMin = new Vector2(0.5f, 0.5f);
                _labelRect.anchorMax = new Vector2(0.5f, 0.5f);
                _labelRect.pivot = new Vector2(0.5f, 0.5f);
                _labelRect.sizeDelta = new Vector2(_textWidth, _textHeight);
                int _labelPosX = -(_commonTotalWidth / 2) + _boxSize + _spacing + (_textWidth / 2);
                _labelRect.anchoredPosition = new Vector2(_labelPosX, 0f);
            }
        }
    }

    private void OnDeviceChanged(EInputDeviceType _device)
    {
        if (false == IsActive) return;

        if (EInputDeviceType.Gamepad == _device)
        {
            if (false == isConsentPhase)
            {
                UI_PanelSelectButton _hoveredBtn = null;
                if (null != languageButtons)
                {
                    for (int i = 0; i < languageButtons.Length; i++)
                    {
                        UI_PanelSelectButton _btn = languageButtons[i];
                        if (null != _btn && true == _btn.gameObject.activeInHierarchy && true == _btn.IsMouseOver())
                        {
                            _hoveredBtn = _btn;
                            break;
                        }
                    }
                }

                UI_PanelSelectButton _targetBtn = _hoveredBtn;
                if (null != _hoveredBtn)
                {
                    MoveDirection _dir = GetTriggeringMoveDirection();
                    if (MoveDirection.Down == _dir && null != _hoveredBtn.navigation.selectOnDown && _hoveredBtn.navigation.selectOnDown is UI_PanelSelectButton _downBtn && true == _downBtn.gameObject.activeInHierarchy)
                    {
                        _targetBtn = _downBtn;
                    }
                    else if (MoveDirection.Up == _dir && null != _hoveredBtn.navigation.selectOnUp && _hoveredBtn.navigation.selectOnUp is UI_PanelSelectButton _upBtn && true == _upBtn.gameObject.activeInHierarchy)
                    {
                        _targetBtn = _upBtn;
                    }
                    else if (MoveDirection.Left == _dir && null != _hoveredBtn.navigation.selectOnLeft && _hoveredBtn.navigation.selectOnLeft is UI_PanelSelectButton _leftBtn && true == _leftBtn.gameObject.activeInHierarchy)
                    {
                        _targetBtn = _leftBtn;
                    }
                    else if (MoveDirection.Right == _dir && null != _hoveredBtn.navigation.selectOnRight && _hoveredBtn.navigation.selectOnRight is UI_PanelSelectButton _rightBtn && true == _rightBtn.gameObject.activeInHierarchy)
                    {
                        _targetBtn = _rightBtn;
                    }
                }
                else
                {
                    _targetBtn = lastFocusedLanguageButton ?? GetKoreanLanguageButton() ?? GetFirstLanguageButton();
                }

                ForceUnhoverAll();
                FocusLanguageButton(_targetBtn);
            }
            else
            {
                Selectable _target = (null != hoveredConsentToggle)
                    ? (Selectable)hoveredConsentToggle
                    : (lastFocusedConsentSelectable ?? (Selectable)consentToggle);
                ForceUnhoverAll();
                FocusConsentItem(_target);
            }
        }
        else if (EInputDeviceType.KeyboardMouse == _device)
        {
            if (null != EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            ForceUnhoverAll();
            if (null != cursorBoxUI)
            {
                cursorBoxUI.Hide();
            }
        }
    }

    private MoveDirection GetTriggeringMoveDirection()
    {
        Gamepad _pad = Gamepad.current;
        if (null == _pad) return MoveDirection.None;

        if (true == _pad.dpad.down.isPressed || _pad.leftStick.y.ReadValue() < -0.5f) return MoveDirection.Down;
        if (true == _pad.dpad.up.isPressed || _pad.leftStick.y.ReadValue() > 0.5f) return MoveDirection.Up;
        if (true == _pad.dpad.left.isPressed || _pad.leftStick.x.ReadValue() < -0.5f) return MoveDirection.Left;
        if (true == _pad.dpad.right.isPressed || _pad.leftStick.x.ReadValue() > 0.5f) return MoveDirection.Right;

        return MoveDirection.None;
    }

    private void ForceUnhoverAll()
    {
        if (null != languageButtons)
        {
            for (int i = 0; i < languageButtons.Length; i++)
            {
                if (null != languageButtons[i]) languageButtons[i].ForceUnhover();
            }
        }

        if (null != confirmButton)
        {
            confirmButton.ForceUnhover();
        }

        HideConsentToggleCursor();
    }

    private void KillTransition()
    {
        if (null != panelTransitionTween && true == panelTransitionTween.IsActive())
        {
            panelTransitionTween.Kill();
            panelTransitionTween = null;
        }
    }

    private void OnDisable()
    {
        if (null != inputManager && null != inputManager.inputReader && null != cachedOnDeviceChanged)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
        }
    }

    private void OnDestroy()
    {
        KillTransition();
        onCompletedCallback = null;
        if (null != inputManager && null != inputManager.inputReader && null != cachedOnDeviceChanged)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
        }
    }
}
