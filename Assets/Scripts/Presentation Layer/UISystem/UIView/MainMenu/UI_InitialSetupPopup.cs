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
public class UI_InitialSetupPopup : MonoBehaviour, IUIDepthCloseable
{
    private const int MAIN_MENU_JSON_ID = 8;

    // 언어 버튼 격자 버퍼의 초기 용량입니다. 지원 언어 수보다 넉넉히 잡아두면 언어가 늘어도
    // 리스트 내부 배열이 다시 할당되지 않습니다. (넘어가도 동작에는 문제가 없습니다)
    private const int MAX_LANGUAGE_BUTTONS = 16;

    // 오브젝트 이름으로 언어를 가려내지 못한 버튼에 물릴 언어입니다. 어떤 버튼도 눌리지 않는
    // 상태만은 피해야 하므로(첫 실행 팝업은 반드시 하나를 골라야 넘어간다) 원문 언어로 둡니다.
    private const EOptionLanguage FALLBACK_BUTTON_LANGUAGE = EOptionLanguage.Korean;

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
    private UIDepthController depthController;

    // 내부 상태
    private Action onCompletedCallback;
    private Action<EInputDeviceType> cachedOnDeviceChanged;
    private Sequence panelTransitionTween;
    private bool isConsentPhase = false;
    private bool isInputAllowed = true;

    // 닫기 연출이 도는 동안 확인 버튼이 다시 눌리는 것을 막는다. blocksRaycasts는 마우스만
    // 막고 게임패드 Submit은 그대로 통과하므로, 플래그와 interactable을 함께 써야 한다.
    private bool isClosing = false;
    private bool isTransitioning = false;
    private bool isInternalToggleUpdating = false;
    private bool suppressNextConsentSelectAudio = false;
    private Toggle hoveredConsentToggle = null;
    private UI_PanelSelectButton lastFocusedLanguageButton;
    private Selectable lastFocusedConsentSelectable;
    private Vector2 originalWindowPos = Vector2.zero;

    // 언어 버튼 격자 배선에 쓰는 재사용 버퍼입니다. Initialize에서 한 번만 쓰이지만,
    // 멤버로 두어 호출 때마다 배열이 새로 생기지 않게 합니다.
    private readonly List<UI_PanelSelectButton> gridOrderedButtons = new List<UI_PanelSelectButton>(MAX_LANGUAGE_BUTTONS);
    private readonly List<int> gridRowStarts = new List<int>(MAX_LANGUAGE_BUTTONS);

    /// <summary>
    /// 버튼 오브젝트 이름을 언어로 옮기는 표입니다.
    ///
    /// 프리팹의 버튼이 어느 언어인지 코드가 알아보는 유일한 단서가 오브젝트 이름이므로,
    /// 언어를 늘릴 때는 여기에 한 줄을 넣고 프리팹에 그 이름을 포함하는 버튼을 만들어
    /// languageButtons 배열에 넣으면 됩니다. 배선(격자 이동)은 버튼의 화면 위치에서
    /// 자동으로 계산되므로 따로 손볼 곳이 없습니다.
    ///
    /// 위에서부터 순서대로 검사하므로, 다른 항목의 이름을 부분 문자열로 포함하는 항목
    /// ("ChineseTrad"는 "Chinese"를 포함)은 반드시 더 위에 두어야 합니다.
    /// </summary>
    private static readonly LanguageButtonBinding[] languageButtonBindings = new LanguageButtonBinding[]
    {
        // "KoreanTrad"는 예전 프리팹에서 쓰던 이름입니다. 지금은 쓰이지 않지만, 남아 있는
        // 버튼이 조용히 한국어로 떨어지는 사고를 막기 위해 별칭으로 남겨둡니다.
        new LanguageButtonBinding(EOptionLanguage.ChineseTraditional, LocKeys.OptionUI.languageChineseTraditional, "繁體中文", "ChineseTrad", "KoreanTrad"),
        new LanguageButtonBinding(EOptionLanguage.ChineseSimplified, LocKeys.OptionUI.languageChineseSimplified, "简体中文", "ChineseSim", "Chinese"),
        new LanguageButtonBinding(EOptionLanguage.Japanese, LocKeys.OptionUI.languageJapanese, "日本語", "Japan"),
        new LanguageButtonBinding(EOptionLanguage.English, LocKeys.OptionUI.languageEnglish, "English", "English"),
        new LanguageButtonBinding(EOptionLanguage.German, LocKeys.OptionUI.languageGerman, "Deutsch", "German", "Deutsch"),
        new LanguageButtonBinding(EOptionLanguage.French, LocKeys.OptionUI.languageFrench, "Français", "French", "Francais"),
        new LanguageButtonBinding(EOptionLanguage.Portuguese, LocKeys.OptionUI.languagePortuguese, "Português", "Portug"),
        new LanguageButtonBinding(EOptionLanguage.Spanish, LocKeys.OptionUI.languageSpanish, "Español", "Spanish", "Espanol"),
        new LanguageButtonBinding(EOptionLanguage.Russian, LocKeys.OptionUI.languageRussian, "Русский", "Russia"),
        new LanguageButtonBinding(EOptionLanguage.Korean, LocKeys.OptionUI.languageKorean, "한국어", "Korean")
    };

    private readonly struct LanguageButtonBinding
    {
        public readonly EOptionLanguage Language;

        /// <summary>표시 이름을 읽어올 로컬라이징 키입니다.</summary>
        public readonly int LocKey;

        /// <summary>로컬라이징 데이터가 아직 로드되지 않았을 때 쓰는 표기입니다.</summary>
        public readonly string FallbackName;

        /// <summary>버튼 오브젝트 이름에 이 중 하나가 들어 있으면 이 언어로 봅니다.</summary>
        public readonly string[] NameTokens;

        public LanguageButtonBinding(EOptionLanguage _language, int _locKey, string _fallbackName, params string[] _nameTokens)
        {
            Language = _language;
            LocKey = _locKey;
            FallbackName = _fallbackName;
            NameTokens = _nameTokens;
        }
    }

    public bool IsActive => gameObject.activeInHierarchy && (null == rootCanvasGroup || 0f < rootCanvasGroup.alpha);

    public void Hide()
    {
        // 초기 언어 설정 및 약관 동의는 게임 진입 전 필수 완료 단계이므로 취소 키(ESC / 패드 B)로 닫힐 수 없습니다.
        // 아무런 동작을 하지 않고 입력을 소비하여 하위 뷰로 관통되는 것을 완벽히 방어합니다.
        return;
    }

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

    public void Initialize(InputManager _inputManager, LocalizationManager _locManager, ICursorBoxUI _cursorBoxUI, UIDepthController _depthController = null)
    {
        EnsureRootCanvasGroup();
        inputManager = _inputManager;
        localizationManager = _locManager;
        cursorBoxUI = _cursorBoxUI;
        depthController = _depthController;
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
        // 1. 언어 버튼 2D 격자 네비게이션 직결
        SetupLanguageGridNavigation();

        // 2. Consent 패널 상하 네비게이션 연결 (Toggle <-> DisagreeToggle <-> ConfirmButton)
        UpdateConsentNavigations();
    }

    /// <summary>
    /// 언어 버튼들을 화면에 놓인 대로 격자로 읽어 상하좌우 이동을 직접 배선합니다.
    ///
    /// 언어마다 버튼을 손으로 이어 붙이던 것을 위치 기반으로 바꾼 이유는, 언어가 늘 때마다
    /// 배선을 다시 짜야 했고 한 곳만 빠뜨려도 패드로 닿지 못하는 버튼이 생기기 때문입니다.
    /// 이제 프리팹에 버튼을 어떻게 배치하든(3+2든 5+5든) 보이는 대로 이동합니다.
    ///
    /// 이동 규칙은 기존 배선과 같습니다.
    ///  - 좌우: 읽는 순서대로 전체를 한 바퀴 돕니다. (줄 끝에서 다음 줄 첫 버튼으로 넘어감)
    ///  - 상하: 위/아래 줄의 같은 칸으로 갑니다. 그 줄이 더 짧으면 마지막 칸으로 붙고,
    ///          위/아래에 줄이 없으면 제자리에 머무릅니다. (목록 밖으로 포커스가 빠지지 않게)
    /// </summary>
    private void SetupLanguageGridNavigation()
    {
        BuildLanguageGrid();

        int _count = gridOrderedButtons.Count;
        if (0 == _count || 0 == gridRowStarts.Count) return;

        int _rowCount = gridRowStarts.Count;

        for (int i = 0; i < _count; i++)
        {
            UI_PanelSelectButton _btn = gridOrderedButtons[i];

            int _row = FindRowIndex(i);
            int _column = i - gridRowStarts[_row];

            _btn.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = gridOrderedButtons[(i - 1 + _count) % _count],
                selectOnRight = gridOrderedButtons[(i + 1) % _count],
                selectOnUp = (0 == _row) ? _btn : GetButtonInRow(_row - 1, _column, _rowCount, _count),
                selectOnDown = (_rowCount - 1 == _row) ? _btn : GetButtonInRow(_row + 1, _column, _rowCount, _count)
            };
        }
    }

    /// <summary>
    /// 언어 버튼을 화면에 보이는 순서(위에서 아래로, 왼쪽에서 오른쪽으로)로 정렬하고
    /// 각 줄이 시작되는 인덱스를 기록합니다.
    ///
    /// 인스펙터 배열 순서가 아니라 실제 위치를 기준으로 삼는 이유는, 배열에 넣은 순서와
    /// 화면 배치가 어긋나 있어도 패드 이동이 보이는 대로 동작해야 하기 때문입니다.
    /// </summary>
    private void BuildLanguageGrid()
    {
        gridOrderedButtons.Clear();
        gridRowStarts.Clear();

        if (null == languageButtons) return;

        for (int i = 0; i < languageButtons.Length; i++)
        {
            UI_PanelSelectButton _btn = languageButtons[i];
            if (null == _btn) continue;

            gridOrderedButtons.Add(_btn);
        }

        int _count = gridOrderedButtons.Count;
        if (0 == _count) return;

        // 버튼 배치는 GridLayoutGroup이 정한다. 레이아웃 갱신은 프레임 끝에 몰아서 도므로,
        // 여기서 그대로 위치를 읽으면 아직 반영되지 않은 좌표를 보고 줄을 잘못 나눌 수 있다.
        // 버튼을 새로 추가한 직후가 특히 그렇다. 한 번 강제로 계산시켜 놓고 읽는다.
        RectTransform _layoutRoot = gridOrderedButtons[0].transform.parent as RectTransform;
        if (null != _layoutRoot && true == _layoutRoot.gameObject.activeInHierarchy)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_layoutRoot);
        }

        // 같은 줄로 볼 Y 오차입니다. 버튼 높이의 절반을 쓰면 줄 간격이 아무리 좁아도
        // 두 줄이 한 줄로 뭉치지 않고, 같은 줄이 미세하게 어긋나 있어도 갈라지지 않습니다.
        float _rowTolerance = GetRowTolerance(gridOrderedButtons[0]);

        // 삽입 정렬로 직접 정렬합니다. List.Sort는 비교자가 엄밀한 순서 관계여야 하는데,
        // 오차를 허용하는 "같은 줄" 판정은 그 조건을 만족하지 않아 결과가 뒤틀릴 수 있습니다.
        // (버튼은 많아야 십여 개라 비용도 문제가 되지 않습니다)
        for (int i = 1; i < _count; i++)
        {
            UI_PanelSelectButton _current = gridOrderedButtons[i];
            Vector3 _currentPos = _current.transform.position;

            int j = i - 1;
            while (j >= 0 && true == ComesAfter(gridOrderedButtons[j].transform.position, _currentPos, _rowTolerance))
            {
                gridOrderedButtons[j + 1] = gridOrderedButtons[j];
                j--;
            }
            gridOrderedButtons[j + 1] = _current;
        }

        gridRowStarts.Add(0);
        float _rowY = gridOrderedButtons[0].transform.position.y;

        for (int i = 1; i < _count; i++)
        {
            float _y = gridOrderedButtons[i].transform.position.y;
            if (Mathf.Abs(_y - _rowY) <= _rowTolerance) continue;

            gridRowStarts.Add(i);
            _rowY = _y;
        }
    }

    /// <summary>_a가 읽는 순서에서 _b보다 뒤에 오는지 여부입니다. (위 → 아래, 왼쪽 → 오른쪽)</summary>
    private static bool ComesAfter(Vector3 _a, Vector3 _b, float _rowTolerance)
    {
        if (Mathf.Abs(_a.y - _b.y) > _rowTolerance) return _a.y < _b.y;
        return _a.x > _b.x;
    }

    private static float GetRowTolerance(UI_PanelSelectButton _button)
    {
        const float DEFAULT_TOLERANCE = 1f;

        RectTransform _rect = _button.transform as RectTransform;
        if (null == _rect) return DEFAULT_TOLERANCE;

        float _height = _rect.rect.height * Mathf.Abs(_rect.lossyScale.y);
        return (_height > 0f) ? (_height * 0.5f) : DEFAULT_TOLERANCE;
    }

    private int FindRowIndex(int _buttonIndex)
    {
        for (int i = gridRowStarts.Count - 1; i >= 0; i--)
        {
            if (_buttonIndex >= gridRowStarts[i]) return i;
        }
        return 0;
    }

    /// <summary>_row번째 줄의 _column번째 버튼입니다. 그 줄이 더 짧으면 마지막 칸으로 붙습니다.</summary>
    private UI_PanelSelectButton GetButtonInRow(int _row, int _column, int _rowCount, int _buttonCount)
    {
        int _start = gridRowStarts[_row];
        int _end = (_row + 1 < _rowCount) ? gridRowStarts[_row + 1] : _buttonCount;
        int _index = Mathf.Min(_start + _column, _end - 1);

        return gridOrderedButtons[_index];
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

            ResolveLanguageBinding(_btn.gameObject.name, out EOptionLanguage _lang, out string _name);

            _btn.Initialize(inputManager, cursorBoxUI, null);
            _btn.SetBoundLanguage(_lang, _name);
            _btn.OnClickedEvent -= HandleLanguageButtonClicked;
            _btn.OnClickedEvent += HandleLanguageButtonClicked;
        }
    }

    /// <summary>
    /// 버튼 오브젝트 이름으로 어느 언어의 버튼인지 가려냅니다.
    ///
    /// 표기는 로컬라이징 데이터에서 읽습니다. 언어 이름은 어느 언어로 봐도 같은 값이
    /// 나오도록 OptionUI.json의 모든 열에 자기 표기가 들어 있고, 코드에 박아두면 폰트
    /// 문자셋 생성기가 그 글자를 수집하지 못해 CJK 폰트에서 통째로 깨지기 때문입니다.
    /// (UI_Option.GetLanguageText와 같은 방식)
    /// </summary>
    private void ResolveLanguageBinding(string _buttonName, out EOptionLanguage _language, out string _displayName)
    {
        for (int i = 0; i < languageButtonBindings.Length; i++)
        {
            LanguageButtonBinding _binding = languageButtonBindings[i];
            if (false == MatchesAnyToken(_buttonName, _binding.NameTokens)) continue;

            _language = _binding.Language;
            _displayName = GetLocalizedName(_binding);
            return;
        }

        // 어느 이름에도 걸리지 않았다. 예전에는 조용히 한국어가 되었는데, 그러면 새 언어
        // 버튼을 추가하고 이름만 어긋났을 때 "한국어 버튼이 두 개"인 화면이 원인 없이 나온다.
        // 동작은 그대로 두고(선택 자체는 가능해야 하므로) 경고만 남긴다.
        Debug.LogWarning("[UI_InitialSetupPopup] '" + _buttonName + "' 버튼의 언어를 알 수 없어 " +
            FALLBACK_BUTTON_LANGUAGE + "로 둡니다. languageButtonBindings의 이름 조각 중 하나를 " +
            "오브젝트 이름에 포함시키세요.", this);

        _language = FALLBACK_BUTTON_LANGUAGE;
        _displayName = GetLocalizedName(FALLBACK_BUTTON_LANGUAGE);
    }

    /// <summary>
    /// 해당 언어의 표기입니다.
    ///
    /// 표에서 언어로 직접 찾습니다. 예전에는 "표의 마지막 항목이 곧 한국어"라고 보고 끝에서
    /// 꺼냈는데, 바로 위 표의 주석이 "언어를 늘릴 때는 여기에 한 줄을 넣으라"고 안내하는 터라
    /// 그 말대로 끝에 추가하는 순간 버튼에 엉뚱한 언어 이름이 찍히게 됩니다.
    /// (고른 언어와 표기가 어긋나는 셈이라, 유저는 Italiano를 눌렀는데 한국어가 켜집니다)
    /// </summary>
    private string GetLocalizedName(EOptionLanguage _language)
    {
        for (int i = 0; i < languageButtonBindings.Length; i++)
        {
            if (languageButtonBindings[i].Language != _language) continue;
            return GetLocalizedName(languageButtonBindings[i]);
        }

        // 표에 없는 언어. 표기는 투박해지지만 어느 언어인지는 드러나고 버튼도 계속 눌립니다.
        return _language.ToString();
    }

    private static bool MatchesAnyToken(string _buttonName, string[] _tokens)
    {
        if (true == string.IsNullOrEmpty(_buttonName) || null == _tokens) return false;

        for (int i = 0; i < _tokens.Length; i++)
        {
            if (true == _buttonName.Contains(_tokens[i])) return true;
        }
        return false;
    }

    private string GetLocalizedName(in LanguageButtonBinding _binding)
    {
        if (null == localizationManager) return _binding.FallbackName;

        string _text = localizationManager.GetText(_binding.LocKey);
        return string.IsNullOrEmpty(_text) ? _binding.FallbackName : _text;
    }

    private void InitConsentPanel()
    {
        isInternalToggleUpdating = true;
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
        isInternalToggleUpdating = false;

        if (null != confirmButton)
        {
            confirmButton.Initialize(inputManager, cursorBoxUI, HandleConfirmButtonClicked, SoundID.None);
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
        else
        {
            _trigger.triggers.Clear();
        }

        AddTriggerEntry(_trigger, EventTriggerType.PointerEnter, (_eventData) =>
        {
            bool _isNewHover = (hoveredConsentToggle != _toggle);
            hoveredConsentToggle = _toggle;
            if (null != inputManager && true == inputManager.IsGamepadMode) return;
            if (true == _isNewHover)
            {
                Sound.PlayUI(SoundID.ResultUIHover);
                ShowConsentToggleCursor(_toggle, _label);
            }
        });

        AddTriggerEntry(_trigger, EventTriggerType.PointerExit, (_eventData) =>
        {
            if (_eventData is PointerEventData _ped && null != _ped.pointerCurrentRaycast.gameObject)
            {
                GameObject _nextGo = _ped.pointerCurrentRaycast.gameObject;
                if (_nextGo == _toggle.gameObject || (null != _label && _nextGo == _label.gameObject) || _nextGo.transform.IsChildOf(_toggle.transform))
                {
                    return;
                }
            }

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
                _toggle.isOn = !_toggle.isOn;
            });
        }
        else
        {
            AddTriggerEntry(_trigger, EventTriggerType.Select, (_eventData) =>
            {
                if (null != inputManager && false == inputManager.IsGamepadMode) return;
                if (false == suppressNextConsentSelectAudio)
                {
                    Sound.PlayUI(SoundID.ResultUIHover);
                }
                suppressNextConsentSelectAudio = false;
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
        if (true == isInternalToggleUpdating) return;

        Sound.PlayUI(SoundID.OptionClick);
        if (true == _isOn)
        {
            if (null != consentDisagreeToggle && true == consentDisagreeToggle.isOn)
            {
                isInternalToggleUpdating = true;
                consentDisagreeToggle.isOn = false;
                isInternalToggleUpdating = false;
            }
        }
        UpdateConfirmButtonState();
    }

    private void HandleConsentDisagreeToggleValueChanged(bool _isOn)
    {
        if (true == isInternalToggleUpdating) return;

        Sound.PlayUI(SoundID.OptionClick);
        if (true == _isOn)
        {
            if (null != consentToggle && true == consentToggle.isOn)
            {
                isInternalToggleUpdating = true;
                consentToggle.isOn = false;
                isInternalToggleUpdating = false;
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

    public void Show(Action _onCompleted, bool _allowInput = true)
    {
        onCompletedCallback = _onCompleted;
        isConsentPhase = false;
        isInputAllowed = _allowInput;
        isClosing = false;
        isTransitioning = false;
        suppressNextConsentSelectAudio = false;
        gameObject.SetActive(true);
        depthController?.RegisterView(this);

        if (true == _allowInput)
        {
            Sound.PlayUI(SoundID.ResultUIOpen);
            SetInputInteractable(true);
        }
        else
        {
            SetInputInteractable(false);
        }

        if (null != inputManager)
        {
            inputManager.SetInputMode(EInputMode.UI);
            if (null != inputManager.inputReader && null != cachedOnDeviceChanged)
            {
                inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
                inputManager.inputReader.InputDeviceChangedEvent += cachedOnDeviceChanged;
            }
        }

        // 1단계 언어 패널 먼저 활성화
        if (null != languagePanel)
        {
            languagePanel.gameObject.SetActive(true);
            languagePanel.alpha = 1f;
            languagePanel.interactable = _allowInput;
            languagePanel.blocksRaycasts = _allowInput;
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

    public void ActivateInput()
    {
        if (true == isInputAllowed) return;

        isInputAllowed = true;
        Sound.PlayUI(SoundID.ResultUIOpen);
        SetInputInteractable(true);

        if (null != languagePanel && false == isConsentPhase)
        {
            languagePanel.interactable = true;
            languagePanel.blocksRaycasts = true;
        }
        else if (null != consentPanel && true == isConsentPhase)
        {
            consentPanel.interactable = true;
            consentPanel.blocksRaycasts = true;
        }

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            if (false == isConsentPhase)
            {
                UI_PanelSelectButton.SuppressSelectAudio = true;
                FocusLanguageButton(lastFocusedLanguageButton ?? GetKoreanLanguageButton() ?? GetFirstLanguageButton());
                UI_PanelSelectButton.SuppressSelectAudio = false;
            }
            else
            {
                FocusConsentItem(lastFocusedConsentSelectable ?? (Selectable)consentToggle);
            }
        }
        else if (null != EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void SetInputInteractable(bool _interactable)
    {
        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.interactable = _interactable;
            rootCanvasGroup.blocksRaycasts = _interactable;
        }
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
        if (false == isInputAllowed)
        {
            ActivateInput();
            return;
        }

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
            if (null != confirmButton && null != EventSystem.current && EventSystem.current.currentSelectedGameObject == _target.gameObject)
            {
                confirmButton.ForceHover();
            }
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
        // 동의 패널로 넘어가는 연출이 도는 동안에는 무시한다. 이 구간에서 언어 버튼은 아직
        // 살아 있어서 게임패드 Submit이 그대로 들어오고, 그때마다 전환 시퀀스가 Kill되고
        // 처음부터 다시 재생되어 화면이 넘어가지 않는다.
        //
        // isConsentPhase까지 한 줄에 모아 둔다. isTransitioning을 세운 뒤에 따로 검사하면
        // 그 경로로 빠져나갈 때 플래그가 true로 남고, 되돌리는 곳이 Show와
        // HandleConsentPanelShown뿐이라 복구되지 않는다.
        if (null == _btn
            || true == isTransitioning
            || true == isClosing
            || true == isConsentPhase) return;

        isTransitioning = true;

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

        if (null != languagePanel)
        {
            // blocksRaycasts는 마우스 클릭만 막는다. 게임패드 Submit은 Selectable의
            // IsInteractable()만 보므로 interactable까지 꺼야 실제로 입력이 차단된다.
            languagePanel.interactable = false;
            languagePanel.blocksRaycasts = false;
        }

        Sequence _seq = DOTween.Sequence();

        if (null != languagePanel)
        {
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

        isInternalToggleUpdating = true;
        if (null != consentToggle) consentToggle.isOn = false;
        if (null != consentDisagreeToggle) consentDisagreeToggle.isOn = false;
        isInternalToggleUpdating = false;

        UpdateConfirmButtonState(true);

        SnapConsentTogglesPixelPerfect();

        Sound.PlayUI(SoundID.ResultUIOpen);
    }

    private void HandleConsentPanelShown()
    {
        isTransitioning = false;
        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            // 패널이 열리며 자동으로 잡는 첫 포커스에서는 hover음을 내지 않는다.
            // SetSelectedGameObject가 Select 이벤트를 동기로 발생시키므로 이 구간만 감싸면 된다.
            // 미리 세워 두면 Select 트리거가 마우스 모드에서 플래그를 소비하기 전에 반환해
            // true로 남고, 나중에 패드로 바꿨을 때 첫 hover음이 대신 사라진다.
            // (ActivateInput의 UI_PanelSelectButton.SuppressSelectAudio와 같은 방식)
            suppressNextConsentSelectAudio = true;
            FocusConsentItem(lastFocusedConsentSelectable ?? (Selectable)consentToggle);
            suppressNextConsentSelectAudio = false;
        }
    }

    private void HandleConfirmButtonClicked()
    {
        // 닫기 연출 중 재입력 무시. 그냥 두면 확인음이 겹쳐 울리고 닫기 시퀀스가 매번
        // 다시 시작되어 팝업이 사라지지 않는다.
        if (true == isClosing) return;

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
        if (true == isClosing) return;
        isClosing = true;
        depthController?.UnregisterView(this);

        KillTransition();

        // 확인 버튼은 연출이 끝나는 HandleCloseCompleted에서야 비활성화된다. 그때까지
        // 게임패드 Submit이 들어오지 않도록 interactable도 함께 내린다.
        SetInputInteractable(false);
        if (null != confirmButton)
        {
            confirmButton.SetInteractable(false);
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
        isClosing = false;
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
        if (false == IsActive || false == isInputAllowed) return;

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
