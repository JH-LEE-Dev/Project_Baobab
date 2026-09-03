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
    [SerializeField] private UI_PopupButton confirmButton;

    // 외부 의존성
    private InputManager inputManager;
    private LocalizationManager localizationManager;
    private ICursorBoxUI cursorBoxUI;

    // 내부 상태
    private Action onCompletedCallback;
    private Action<EInputDeviceType> cachedOnDeviceChanged;
    private Sequence panelTransitionTween;
    private bool isConsentPhase = false;
    private bool isConsentToggleHovered = false;
    private UI_PanelSelectButton lastFocusedLanguageButton;
    private Selectable lastFocusedConsentSelectable;
    private Vector2 originalWindowPos = Vector2.zero;
    private bool isToggleCursorShowing = false;

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

        // 2. Consent 패널 상하 네비게이션 연결 (Toggle <-> ConfirmButton)
        if (null != consentToggle && null != confirmButton)
        {
            consentToggle.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = null,
                selectOnDown = confirmButton,
                selectOnLeft = null,
                selectOnRight = null
            };

            confirmButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = consentToggle,
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
            // opt-in이어야 한다. 체크를 미리 켜두면 유저가 아무것도 하지 않고 확인만 눌러도
            // 동의한 것이 되는데, 분석·텔레메트리 동의는 그렇게 받을 수 없다(GDPR).
            consentToggle.isOn = false;
            consentToggle.onValueChanged.RemoveListener(HandleConsentToggleValueChanged);
            consentToggle.onValueChanged.AddListener(HandleConsentToggleValueChanged);

            BindConsentToggleHoverTriggers(consentToggle.gameObject);
        }

        if (null != consentToggleLabel)
        {
            consentToggleLabel.raycastTarget = true;
            BindConsentToggleHoverTriggers(consentToggleLabel.gameObject, true);
        }

        if (null != confirmButton)
        {
            confirmButton.Initialize(inputManager, cursorBoxUI, HandleConfirmButtonClicked);
        }
    }

    private void BindConsentToggleHoverTriggers(GameObject _targetGo, bool _isLabel = false)
    {
        if (null == _targetGo) return;

        EventTrigger _trigger = _targetGo.GetComponent<EventTrigger>();
        if (null == _trigger)
        {
            _trigger = _targetGo.AddComponent<EventTrigger>();
        }

        AddTriggerEntry(_trigger, EventTriggerType.PointerEnter, HandleConsentTogglePointerEnter);
        AddTriggerEntry(_trigger, EventTriggerType.PointerExit, HandleConsentTogglePointerExit);

        if (true == _isLabel)
        {
            AddTriggerEntry(_trigger, EventTriggerType.PointerClick, HandleConsentToggleLabelClicked);
        }
        else
        {
            AddTriggerEntry(_trigger, EventTriggerType.Select, HandleConsentToggleSelected);
            AddTriggerEntry(_trigger, EventTriggerType.Deselect, HandleConsentToggleDeselected);
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

    private void HandleConsentToggleLabelClicked(BaseEventData _eventData)
    {
        if (null != inputManager && true == inputManager.IsGamepadMode) return;
        if (null != consentToggle)
        {
            consentToggle.isOn = !consentToggle.isOn;
        }
    }

    private void HandleConsentTogglePointerEnter(BaseEventData _eventData)
    {
        isConsentToggleHovered = true;
        if (null != inputManager && true == inputManager.IsGamepadMode) return;
        ShowConsentToggleCursor();
    }

    private void HandleConsentTogglePointerExit(BaseEventData _eventData)
    {
        isConsentToggleHovered = false;
        if (null != inputManager && true == inputManager.IsGamepadMode) return;
        HideConsentToggleCursor();
    }

    private void HandleConsentToggleSelected(BaseEventData _eventData)
    {
        if (null != inputManager && false == inputManager.IsGamepadMode) return;
        ShowConsentToggleCursor();
    }

    private void HandleConsentToggleDeselected(BaseEventData _eventData)
    {
        if (null != inputManager && false == inputManager.IsGamepadMode) return;
        HideConsentToggleCursor();
    }

    private void HandleConsentToggleValueChanged(bool _isOn)
    {
        Sound.PlayUI(SoundID.OptionClick);
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
        ApplyDefaultLanguageSelection();

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
    /// 지금 적용되어 있는 언어의 버튼을 미리 선택된 상태로 표시합니다.
    ///
    /// 첫 실행에는 저장된 설정 파일이 없으므로 SettingsManager가 이미 LanguageAutoDetect로
    /// 언어를 정해둔 상태이고(Steam 지정 언어 -> OS 언어 -> 영어), 이 팝업의 안내 문구도
    /// 그 언어로 나옵니다. 그런데 버튼은 전부 선택 해제된 채로 떠서, 화면에 보이는 문구의
    /// 언어와 선택기 상태가 서로 다른 말을 하고 있었습니다. 유저 입장에서는 "지금 무슨
    /// 언어인지"와 "그냥 넘어가면 무엇이 되는지"를 알 수 없습니다.
    ///
    /// 여기서 자동 판별 결과를 그대로 비추면 그 불일치가 사라지고, 대부분의 유저는 이미
    /// 맞는 언어가 골라져 있는 것을 확인만 하면 됩니다.
    ///
    /// 판별 결과를 다시 계산하지 않고 SettingsManager의 현재 언어를 읽는 것이 중요합니다.
    /// 그래야 화면에 실제로 적용된 언어와 선택 표시가 어긋날 수 없습니다.
    /// (LanguageAutoDetect.Resolve를 여기서 또 부르면, 예컨대 지원 언어 판정이 한쪽에서만
    ///  걸렸을 때 "영어로 보이는데 일본어가 선택된" 상태가 만들어질 수 있습니다)
    /// </summary>
    private void ApplyDefaultLanguageSelection()
    {
        if (null == languageButtons) return;

        EOptionLanguage _current = SettingsManager.Instance.CurrentLanguage;

        for (int i = 0; i < languageButtons.Length; i++)
        {
            UI_PanelSelectButton _btn = languageButtons[i];
            if (null == _btn) continue;

            bool _isCurrent = (_btn.BoundLanguage == _current);

            _btn.SetSelected(_isCurrent);
            _btn.ForceUnhover();

            // 패드로 열었을 때 첫 포커스가 현재 언어에 놓이도록 기억해둔다.
            // (HandleShowCompleted가 이 값을 쓴다. 없으면 목록의 첫 버튼 = 한국어로 떨어진다)
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
            FocusLanguageButton(lastFocusedLanguageButton ?? GetFirstLanguageButton());
        }
        else if (null != EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
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
            ShowConsentToggleCursor();
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

    private void ShowConsentToggleCursor()
    {
        if (true == isToggleCursorShowing) return;
        if (null == cursorBoxUI || null == consentToggle) return;

        RectTransform _rect = consentToggle.GetComponent<RectTransform>();
        if (null == _rect) return;

        isToggleCursorShowing = true;
        float _textWidth = (null != consentToggleLabel && consentToggleLabel.preferredWidth > 0f)
            ? consentToggleLabel.preferredWidth
            : 200f;
        float _totalWidth = 16f + 8f + _textWidth;
        Vector2 _size = new Vector2(_totalWidth + 12f, 28f);

        cursorBoxUI.Show(_rect, _size, Vector2.zero, CursorMotionSettings.RowSubtle);
    }

    private void HideConsentToggleCursor()
    {
        if (false == isToggleCursorShowing) return;
        isToggleCursorShowing = false;

        if (null == cursorBoxUI || null == consentToggle) return;

        RectTransform _rect = consentToggle.GetComponent<RectTransform>();
        if (null != _rect)
        {
            cursorBoxUI.Hide(_rect);
        }
    }


    private void HandleLanguageButtonClicked(UI_PanelSelectButton _btn)
    {
        if (null == _btn) return;

        // 1. 선택한 언어 적용
        EOptionLanguage _selected = _btn.BoundLanguage;
        SettingsManager.Instance.SetLanguage(_selected);

        // 선택 표시를 방금 누른 버튼으로 옮긴다. 곧바로 동의 패널로 넘어가긴 하지만, 전환
        // 연출이 끝나기 전까지는 이전 언어가 선택된 것처럼 보이고, 뒤로 돌아오는 경로가
        // 생기면 그대로 어긋난 채 남는다.
        ApplyDefaultLanguageSelection();

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

        SnapConsentTogglePixelPerfect();
    }

    private void HandleConsentPanelShown()
    {
        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            FocusConsentItem(lastFocusedConsentSelectable ?? (Selectable)confirmButton ?? (Selectable)consentToggle);
        }
    }

    private void HandleConfirmButtonClicked()
    {
        // 체크되지 않은 상태로 확인을 누르는 것은 유효한 "거부"다. 확인 버튼은 체크와
        // 무관하게 항상 눌리며, 어느 쪽이든 여기서 답이 확정된다.
        bool _isGranted = (null != consentToggle && true == consentToggle.isOn);

        // 여기서 저장하지 않으면 이 선택은 어디에도 남지 않는다. 예전에는 구독자가 하나도 없는
        // 이벤트를 발행하고 끝나서, 팝업은 있는데 아무 효력이 없는 상태였다.
        // SetDataConsent는 곧바로 파일에 기록하고 DataConsentGate가 SDK에 반영하므로,
        // 이 한 줄이 "동의 UI"를 "동의"로 만든다.
        SettingsManager.Instance.SetDataConsent(
            true == _isGranted ? EDataConsent.Granted : EDataConsent.Declined);

        // 팝업 페이드아웃 및 닫기
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

        SnapConsentTogglePixelPerfect();
    }

    private void SnapConsentTogglePixelPerfect()
    {
        if (null == consentToggle || null == consentToggleLabel) return;

        consentToggleLabel.raycastTarget = true;

        RectTransform _toggleRect = consentToggle.GetComponent<RectTransform>();
        RectTransform _labelRect = consentToggleLabel.rectTransform;
        if (null == _toggleRect || null == _labelRect) return;

        // 1. TMP 텍스트 정수 너비 및 높이 계산
        int _textWidth = Mathf.CeilToInt(consentToggleLabel.preferredWidth);
        int _textHeight = Mathf.CeilToInt(consentToggleLabel.preferredHeight);
        if (_textHeight <= 0) _textHeight = 16;

        // 2. 체크박스(Background) 규격
        int _boxSize = 16;
        int _spacing = 8;

        // 3. 총 너비 계산 및 짝수 스냅 (중앙 정렬 시 .5px 방지)
        int _totalWidth = _boxSize + _spacing + _textWidth;
        if (0 != (_totalWidth % 2))
        {
            _totalWidth += 1;
        }

        // 4. Toggle 부모 앵커 및 크기 정수 스냅 (중앙 앵커 보장)
        _toggleRect.anchorMin = new Vector2(0.5f, 0.5f);
        _toggleRect.anchorMax = new Vector2(0.5f, 0.5f);
        _toggleRect.pivot = new Vector2(0.5f, 0.5f);
        _toggleRect.sizeDelta = new Vector2(_totalWidth, Mathf.Max(_boxSize, _textHeight));
        _toggleRect.anchoredPosition = new Vector2(0f, Mathf.Round(_toggleRect.anchoredPosition.y));

        // 5. 체크박스 (Background) 중앙 앵커 보장 및 왼쪽 배치
        Transform _bgTransform = consentToggle.transform.Find("Background");
        if (null != _bgTransform)
        {
            RectTransform _bgRect = _bgTransform.GetComponent<RectTransform>();
            if (null != _bgRect)
            {
                _bgRect.anchorMin = new Vector2(0.5f, 0.5f);
                _bgRect.anchorMax = new Vector2(0.5f, 0.5f);
                _bgRect.pivot = new Vector2(0.5f, 0.5f);
                _bgRect.sizeDelta = new Vector2(_boxSize, _boxSize);
                int _bgPosX = -(_totalWidth / 2) + (_boxSize / 2);
                _bgRect.anchoredPosition = new Vector2(_bgPosX, 0f);
            }
        }

        // 6. 텍스트 (ToggleTMP) 중앙 앵커 보장 및 오른쪽 배치
        _labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _labelRect.pivot = new Vector2(0.5f, 0.5f);
        _labelRect.sizeDelta = new Vector2(_textWidth, _textHeight);
        int _labelPosX = -(_totalWidth / 2) + _boxSize + _spacing + (_textWidth / 2);
        _labelRect.anchoredPosition = new Vector2(_labelPosX, 0f);
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
                    _targetBtn = lastFocusedLanguageButton ?? GetFirstLanguageButton();
                }

                ForceUnhoverAll();
                FocusLanguageButton(_targetBtn);
            }
            else
            {
                Selectable _target = lastFocusedConsentSelectable ?? (Selectable)confirmButton ?? (Selectable)consentToggle;
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
