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

    // 마우스 휠로 언어를 넘길 때 한 칸 사이의 최소 간격(초)입니다. 터치패드처럼 한 번 쓸어도 휠 값이
    // 여러 프레임에 잘게 들어오는 장치에서 언어가 순식간에 몇 칸씩 넘어가는 것을 막습니다.
    // 한 칸마다 로컬라이징 전환·전체 텍스트 폰트 교체·설정 파일 저장이 돌므로, 패드 좌우를 길게 누를 때의
    // 반복 간격(InputSystemUIInputModule.moveRepeatRate = 0.1)보다 자주 돌지 않게 같은 값으로 둡니다.
    private const float LANGUAGE_SCROLL_INTERVAL = 0.03f;

    /// <summary>
    /// 언어 이름 라벨 하나와 그 언어의 짝입니다. 라벨은 각자 그 언어를 표시할 폰트(갈무리/FusionPixel/Lorem)와
    /// LocalizedFontTracker를 그대로 들고 있어서, 폰트를 코드로 바꿔 끼우지 않고 켜고 끄기만 합니다.
    /// (FontLocalizer가 텍스트 갱신 때마다 현재 앱 언어 기준으로 폰트를 다시 맞추므로, 폰트를 코드로 바꾸면 덮어씌워집니다)
    /// </summary>
    [Serializable]
    private struct LanguageLabelEntry
    {
        public EOptionLanguage language;
        public TextMeshProUGUI label;
    }

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
    [SerializeField] private UI_OptionSelector languageSelector;        // < 현재 언어 > 선택기 (좌우 입력/화살표 버튼으로 언어 순환)
    [SerializeField] private UI_PopupButton languageConfirmButton;      // 선택한 언어로 확정하는 체크 버튼
    [SerializeField] private LanguageLabelEntry[] languageLabels;       // 언어별 이름 라벨(선택기 중앙에 겹쳐 두고 현재 언어 것만 켠다)
    [SerializeField] private TextMeshProUGUI languageTitleText;         // 지구본 아이콘 옆 제목("언어"). 앱 언어가 바뀔 때마다 그 언어 문구로 갱신된다
    [SerializeField] private TextMeshProUGUI languageSubtitleText;      // 이름 아래의 영어 언어명(Korean, Japanese ...). 영어는 비워서 자리만 유지한다
    [SerializeField] private Image[] languageDots;                      // 이름 아래의 페이지 점. 언어 목록과 같은 순서이고 현재 언어의 점만 밝다
    [SerializeField] private Color languageDotActiveColor = Color.white;
    [SerializeField] private Color languageDotInactiveColor = new Color(0.35f, 0.36f, 0.45f, 1.0f);
    [SerializeField] private RectTransform languageDotCursor;           // 현재 언어의 점 위에서 미끄러지는 5x5 커서 (레이아웃 제외)
    [SerializeField] private Color languageFlashColor = new Color(1.0f, 0.835f, 0.31f, 1.0f); // 이름이 바뀔 때 번쩍이는 색 (#FFD54F)
    [SerializeField] private float languageSlideDistance = 12f;         // 이름이 들어오는 거리(px). 정수 스냅으로 움직여 픽셀이 뭉개지지 않는다

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
    private Action cachedOnLanguagePrev;
    private Action cachedOnLanguageNext;
    private Action cachedOnLanguageConfirm;
    private int languageIndex = 0;
    private Vector2[] languageLabelBasePositions;
    private Color[] languageLabelBaseColors;
    private Color languageTitleBaseColor = Color.white;
    private Color languageSubtitleBaseColor = Color.white;
    private Sequence languageFeedbackTween;
    private Selectable lastFocusedLanguageSelectable;
    private Selectable lastFocusedConsentSelectable;
    private Vector2 originalWindowPos = Vector2.zero;
    private float nextLanguageScrollTime = 0f;

    /// <summary>
    /// 언어 선택기가 순환하는 언어 목록입니다. 목록 순서가 곧 좌우 이동 순서이고, 끝에서 처음으로 이어집니다.
    ///
    /// 언어를 늘릴 때는 여기에 한 줄을 넣고, 프리팹의 선택기 프레임 안에 그 언어의 이름 라벨을
    /// (그 언어를 표시할 폰트로) 만들어 languageLabels에 연결하면 됩니다.
    /// </summary>
    private static readonly LanguageBinding[] languageBindings = new LanguageBinding[]
    {
        new LanguageBinding(EOptionLanguage.Korean, LocKeys.OptionUI.languageKorean, "한국어", "Korean"),
        new LanguageBinding(EOptionLanguage.English, LocKeys.OptionUI.languageEnglish, "English", ""),
        new LanguageBinding(EOptionLanguage.Japanese, LocKeys.OptionUI.languageJapanese, "日本語", "Japanese"),
        new LanguageBinding(EOptionLanguage.ChineseSimplified, LocKeys.OptionUI.languageChineseSimplified, "简体中文", "Chinese (Simplified)"),
        new LanguageBinding(EOptionLanguage.ChineseTraditional, LocKeys.OptionUI.languageChineseTraditional, "繁體中文", "Chinese (Traditional)"),
        new LanguageBinding(EOptionLanguage.German, LocKeys.OptionUI.languageGerman, "Deutsch", "German"),
        new LanguageBinding(EOptionLanguage.French, LocKeys.OptionUI.languageFrench, "Français", "French"),
        new LanguageBinding(EOptionLanguage.Portuguese, LocKeys.OptionUI.languagePortuguese, "Português", "Portuguese"),
        new LanguageBinding(EOptionLanguage.Spanish, LocKeys.OptionUI.languageSpanish, "Español (España)", "Spanish (Spain)"),
        new LanguageBinding(EOptionLanguage.SpanishLatAm, LocKeys.OptionUI.languageSpanishLatAm, "Español (Latinoamérica)", "Spanish (Latin America)"),
        new LanguageBinding(EOptionLanguage.Russian, LocKeys.OptionUI.languageRussian, "Русский", "Russian"),
        new LanguageBinding(EOptionLanguage.Polish, LocKeys.OptionUI.languagePolish, "Polski", "Polish"),
        new LanguageBinding(EOptionLanguage.Turkish, LocKeys.OptionUI.languageTurkish, "Türkçe", "Turkish"),
        new LanguageBinding(EOptionLanguage.Italian, LocKeys.OptionUI.languageItalian, "Italiano", "Italian"),
        new LanguageBinding(EOptionLanguage.Ukrainian, LocKeys.OptionUI.languageUkrainian, "Українська", "Ukrainian"),
        new LanguageBinding(EOptionLanguage.Czech, LocKeys.OptionUI.languageCzech, "Čeština", "Czech"),
        new LanguageBinding(EOptionLanguage.Indonesian, LocKeys.OptionUI.languageIndonesian, "Bahasa Indonesia", "Indonesian"),
        new LanguageBinding(EOptionLanguage.Vietnamese, LocKeys.OptionUI.languageVietnamese, "Tiếng Việt", "Vietnamese")
    };

    private readonly struct LanguageBinding
    {
        public readonly EOptionLanguage Language;

        /// <summary>표시 이름을 읽어올 로컬라이징 키입니다.</summary>
        public readonly int LocKey;

        /// <summary>로컬라이징 데이터가 아직 로드되지 않았을 때 쓰는 표기입니다.</summary>
        public readonly string FallbackName;

        /// <summary>이름 아래에 작게 보여주는 영어 언어명입니다. 비어 있으면 부제를 비웁니다. (영문뿐이라 어느 폰트에서도 깨지지 않습니다)</summary>
        public readonly string EnglishName;

        public LanguageBinding(EOptionLanguage _language, int _locKey, string _fallbackName, string _englishName)
        {
            Language = _language;
            LocKey = _locKey;
            FallbackName = _fallbackName;
            EnglishName = _englishName;
        }
    }

    public bool IsActive => gameObject.activeInHierarchy && (null == rootCanvasGroup || 0f < rootCanvasGroup.alpha);

    public void Hide()
    {
        // 초기 언어 설정 및 약관 동의는 게임 진입 전 필수 완료 단계이므로 취소 키(ESC / 패드 B)로 닫힐 수 없습니다.
        // 약관 단계에서는 한 단계 뒤인 언어 선택으로 되돌아가고, 언어 단계에서는 아무것도 하지 않고
        // 입력만 소비하여 하위 뷰로 관통되는 것을 막습니다.
        //
        // 연타로 같은 프레임에 두 번 들어와도 isTransitioning이 두 번째 호출을 걸러준다.
        if (false == isConsentPhase
            || true == isTransitioning
            || true == isClosing
            || false == isInputAllowed) return;

        TransitionToLanguagePanel();
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
        cachedOnLanguagePrev = HandleLanguagePrevClicked;
        cachedOnLanguageNext = HandleLanguageNextClicked;
        cachedOnLanguageConfirm = HandleLanguageConfirmClicked;

        if (null != rootCanvasGroup)
        {
            rootCanvasGroup.alpha = 0f;
            rootCanvasGroup.interactable = false;
            rootCanvasGroup.blocksRaycasts = false;
        }

        InitLanguageSelector();
        InitConsentPanel();
        SetupSpatialNavigations();

        gameObject.SetActive(false);
    }

    private void SetupSpatialNavigations()
    {
        // 1. 언어 단계: 선택기 행 <-> 체크 버튼 상하 연결 (좌우는 선택기가 직접 언어를 바꾼다)
        SetupLanguageNavigation();

        // 2. Consent 패널 상하 네비게이션 연결 (Toggle <-> DisagreeToggle <-> ConfirmButton)
        UpdateConsentNavigations();
    }

    private void SetupLanguageNavigation()
    {
        if (null != languageSelector)
        {
            languageSelector.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = null,
                selectOnDown = languageConfirmButton,
                selectOnLeft = null,
                selectOnRight = null
            };
        }

        if (null != languageConfirmButton)
        {
            languageConfirmButton.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = languageSelector,
                selectOnDown = null,
                selectOnLeft = null,
                selectOnRight = null
            };
        }
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

    private void InitLanguageSelector()
    {
        // 라벨에 표기를 채운다. 언어 이름은 어느 언어로 봐도 같은 값이 나오도록 OptionUI.json의 모든 열에 들어 있고,
        // 코드에 박아두면 폰트 문자셋 생성기가 그 글자를 수집하지 못해 CJK 폰트에서 통째로 깨진다. (UI_Option.GetLanguageText와 같은 방식)
        if (null != languageLabels)
        {
            for (int i = 0; i < languageLabels.Length; i++)
            {
                if (null == languageLabels[i].label) continue;

                languageLabels[i].label.text = GetLocalizedName(languageLabels[i].language);
            }
        }

        if (null != languageSelector)
        {
            languageSelector.Initialize(string.Empty, string.Empty, cachedOnLanguagePrev, cachedOnLanguageNext);
            languageSelector.SetCursorBoxUI(cursorBoxUI, inputManager);
        }

        CacheLanguageFeedbackBase();

        if (null != languageConfirmButton)
        {
            languageConfirmButton.Initialize(inputManager, cursorBoxUI, cachedOnLanguageConfirm);
        }
    }

    /// <summary>
    /// 해당 언어의 표기입니다. 표에서 언어로 직접 찾으므로 표 순서가 바뀌어도 어긋나지 않습니다.
    /// </summary>
    private string GetLocalizedName(EOptionLanguage _language)
    {
        for (int i = 0; i < languageBindings.Length; i++)
        {
            if (languageBindings[i].Language != _language) continue;
            return GetLocalizedName(in languageBindings[i]);
        }

        // 표에 없는 언어. 표기는 투박해지지만 어느 언어인지는 드러난다.
        return _language.ToString();
    }

    private string GetLocalizedName(in LanguageBinding _binding)
    {
        if (null == localizationManager) return _binding.FallbackName;

        string _text = localizationManager.GetText(_binding.LocKey);
        return string.IsNullOrEmpty(_text) ? _binding.FallbackName : _text;
    }

    /// <summary>
    /// 처음 보여줄 언어의 목록 위치입니다. 부팅 때 시스템 언어로 이미 적용된 현재 언어를 그대로 보여주므로,
    /// 대부분의 유저는 체크만 누르면 됩니다. 목록에 없는 언어는 첫 항목(한국어)으로 둡니다.
    /// </summary>
    private int ResolveInitialLanguageIndex()
    {
        EOptionLanguage _current = SettingsManager.Instance.CurrentLanguage;
        for (int i = 0; i < languageBindings.Length; i++)
        {
            if (languageBindings[i].Language == _current) return i;
        }

        return 0;
    }

    /// <summary>현재 언어의 이름 라벨만 켜고 나머지는 끕니다. 영어 부제와 페이지 점도 함께 갱신합니다.</summary>
    private void UpdateLanguageDisplay()
    {
        EOptionLanguage _current = languageBindings[languageIndex].Language;

        if (null != languageLabels)
        {
            for (int i = 0; i < languageLabels.Length; i++)
            {
                TextMeshProUGUI _label = languageLabels[i].label;
                if (null == _label) continue;

                bool _isCurrent = (languageLabels[i].language == _current);
                if (_label.gameObject.activeSelf != _isCurrent)
                {
                    _label.gameObject.SetActive(_isCurrent);
                }
            }
        }

        // 부제는 비활성화하지 않고 문구만 비운다. 켜고 끄면 레이아웃 높이가 언어마다 달라진다.
        if (null != languageSubtitleText)
        {
            languageSubtitleText.text = languageBindings[languageIndex].EnglishName;
        }

        if (null != languageDots)
        {
            for (int i = 0; i < languageDots.Length; i++)
            {
                if (null == languageDots[i]) continue;

                languageDots[i].color = (i == languageIndex) ? languageDotActiveColor : languageDotInactiveColor;
            }
        }
    }

    /// <summary>연출이 되돌아갈 기준 위치/색을 한 번만 저장합니다.</summary>
    private void CacheLanguageFeedbackBase()
    {
        if (null != languageLabels)
        {
            languageLabelBasePositions = new Vector2[languageLabels.Length];
            languageLabelBaseColors = new Color[languageLabels.Length];
            for (int i = 0; i < languageLabels.Length; i++)
            {
                if (null == languageLabels[i].label) continue;

                languageLabelBasePositions[i] = languageLabels[i].label.rectTransform.anchoredPosition;
                languageLabelBaseColors[i] = languageLabels[i].label.color;
            }
        }

        if (null != languageTitleText) languageTitleBaseColor = languageTitleText.color;
        if (null != languageSubtitleText) languageSubtitleBaseColor = languageSubtitleText.color;
    }

    /// <summary>진행 중인 연출을 멈추고 모든 요소를 기준 위치/색으로 되돌립니다. 연타해도 최종 모습이 항상 같도록 하는 안전장치입니다.</summary>
    private void ResetLanguageFeedback()
    {
        if (null != languageFeedbackTween && true == languageFeedbackTween.IsActive())
        {
            languageFeedbackTween.Kill();
        }

        languageFeedbackTween = null;

        if (null != languageDotCursor) languageDotCursor.DOKill();

        if (null != languageLabels && null != languageLabelBasePositions)
        {
            for (int i = 0; i < languageLabels.Length; i++)
            {
                TextMeshProUGUI _label = languageLabels[i].label;
                if (null == _label) continue;

                _label.rectTransform.DOKill();
                _label.DOKill();
                _label.rectTransform.anchoredPosition = languageLabelBasePositions[i];
                _label.color = languageLabelBaseColors[i];
            }
        }

        if (null != languageTitleText)
        {
            languageTitleText.DOKill();
            languageTitleText.color = languageTitleBaseColor;
        }

        if (null != languageSubtitleText)
        {
            languageSubtitleText.DOKill();
            languageSubtitleText.color = languageSubtitleBaseColor;
        }
    }

    private bool HasLanguageDotCursorTarget()
    {
        return null != languageDotCursor && null != languageDots
            && languageIndex < languageDots.Length && null != languageDots[languageIndex];
    }

    /// <summary>
    /// 점 커서를 현재 언어의 점에 바로 붙입니다. (연출 없이)
    /// 커서는 활성 점의 자식으로 로컬 (0, 0)에 놓이므로 레이아웃이 아직 안정되기 전이어도 점과 항상 겹칩니다.
    /// 점 중심이 .5 픽셀이라 5x5 커서도 같은 중심에 놓여야 가장자리가 정수 픽셀에 떨어진다.
    /// </summary>
    private void SnapLanguageDotCursor()
    {
        if (false == HasLanguageDotCursorTarget()) return;

        languageDotCursor.DOKill();
        languageDotCursor.SetParent(languageDots[languageIndex].rectTransform, false);
        languageDotCursor.anchoredPosition = Vector2.zero;
    }

    /// <summary>
    /// 언어를 넘길 때의 피드백입니다. 이름은 누른 방향에서 정수 픽셀로 미끄러져 들어오며 금색에서 흰색으로 돌아오고,
    /// 점 커서는 새 점으로 미끄러집니다. (화살표 밀기는 UI_OptionSelector가 담당합니다)
    /// 텍스트에는 스케일과 알파 페이드를 쓰지 않습니다. (픽셀 폰트가 뭉개지고, 빠르게 넘길 때 화면이 깜빡임)
    /// </summary>
    private void PlayLanguageSwitchFeedback(int _direction)
    {
        ResetLanguageFeedback();

        float _dir = (0 < _direction) ? 1f : -1f;
        float _slide = Mathf.Round(languageSlideDistance);
        Sequence _seq = DOTween.Sequence();

        // 이름: 누른 방향에서 슬라이드 인 + 금색 -> 흰색 (알파는 항상 유지)
        EOptionLanguage _current = languageBindings[languageIndex].Language;
        for (int i = 0; i < languageLabels.Length; i++)
        {
            TextMeshProUGUI _label = languageLabels[i].label;
            if (null == _label || _current != languageLabels[i].language) continue;

            Vector2 _base = languageLabelBasePositions[i];
            Color _from = languageFlashColor;
            _from.a = languageLabelBaseColors[i].a;

            _label.rectTransform.anchoredPosition = new Vector2(_base.x + _dir * _slide, _base.y);
            _label.color = _from;
            _seq.Join(_label.rectTransform.DOAnchorPosX(_base.x, 0.14f, true).SetEase(Ease.OutBack));
            _seq.Join(_label.DOColor(languageLabelBaseColors[i], 0.22f).SetEase(Ease.OutQuad));
        }

        // 점 커서: 새 점의 자식으로 옮기되 월드 위치를 유지해 이전 위치에서 출발하고, 로컬 (0, 0)으로 미끄러진다
        if (true == HasLanguageDotCursorTarget())
        {
            languageDotCursor.SetParent(languageDots[languageIndex].rectTransform, true);
            _seq.Join(languageDotCursor.DOAnchorPos(Vector2.zero, 0.12f).SetEase(Ease.OutBack));
        }

        languageFeedbackTween = _seq;
    }

    private void CycleLanguage(int _delta)
    {
        int _count = languageBindings.Length;
        languageIndex = (languageIndex + _delta + _count) % _count;
        UpdateLanguageDisplay();

        // 옵션 화면의 언어 선택과 같이, 넘길 때마다 앱 언어를 바로 바꾼다. 제목 문구와 폰트(FontLocalizer)는
        // 기존 로컬라이징 시스템이 갱신하므로 선택한 언어로 실시간 표시된다.
        // (같은 언어면 SetLanguage가 아무것도 하지 않고, 바뀌면 설정에 바로 저장한다)
        SettingsManager.Instance.SetLanguage(languageBindings[languageIndex].Language);
        RefreshLocalizedTexts();

        PlayLanguageSwitchFeedback(_delta);
    }

    // 동의 패널로 넘어가는 연출이 도는 동안, 닫히는 동안, 이미 동의 단계인 동안에는 언어 입력을 무시한다.
    // 이 구간에서 선택기와 체크 버튼은 아직 살아 있어서 게임패드 입력이 그대로 들어오고, 그때마다
    // 전환 시퀀스가 Kill되고 처음부터 다시 재생되어 화면이 넘어가지 않는다.
    private bool IsLanguageInputBlocked()
    {
        return true == isTransitioning || true == isClosing || true == isConsentPhase;
    }

    private void HandleLanguagePrevClicked()
    {
        if (true == IsLanguageInputBlocked()) return;

        CycleLanguage(-1);
    }

    private void HandleLanguageNextClicked()
    {
        if (true == IsLanguageInputBlocked()) return;

        CycleLanguage(1);
    }

    private void Update()
    {
        HandleLanguageScroll();
    }

    /// <summary>
    /// 언어 단계에서 마우스 휠로 언어를 넘깁니다. 휠을 위로 굴리면 이전(왼쪽), 아래로 굴리면 다음(오른쪽) 언어입니다.
    /// 팝업이 화면을 덮는 모달이라 커서 위치와 상관없이 받습니다.
    /// </summary>
    private void HandleLanguageScroll()
    {
        if (false == isInputAllowed || true == IsLanguageInputBlocked()) return;

        // 언어 패널은 되돌아오는 페이드인이 끝날 때까지 interactable이 꺼져 있다. (HandleLanguagePanelReturned)
        if (null == languagePanel || false == languagePanel.interactable) return;

        Mouse _mouse = Mouse.current;
        if (null == _mouse) return;

        float _scrollY = _mouse.scroll.ReadValue().y;
        if (0f == _scrollY) return;

        if (Time.unscaledTime < nextLanguageScrollTime) return;
        nextLanguageScrollTime = Time.unscaledTime + LANGUAGE_SCROLL_INTERVAL;

        StepLanguageByScroll((0f < _scrollY) ? -1 : 1);
    }

    /// <summary>
    /// 선택기의 화살표 버튼을 누른 것과 똑같이 처리해, 클릭음·화살표 밀기·이름 슬라이드 연출이 키보드 좌우 입력과 같게 나오게 합니다.
    /// (UI_OptionSelector.OnMove와 같은 경로)
    /// </summary>
    private void StepLanguageByScroll(int _direction)
    {
        UI_OptionButton _arrow = null;
        if (null != languageSelector)
        {
            _arrow = (0 > _direction) ? languageSelector.LeftArrowButton : languageSelector.RightArrowButton;
        }

        if (null != _arrow && true == _arrow.IsInteractable)
        {
            _arrow.OnPointerClick(null);
            return;
        }

        // 화살표 버튼이 없는 프리팹이면 선택기 콜백을 직접 부른다. (UI_OptionSelector.OnMove의 대체 경로와 같다)
        Sound.PlayUI(SoundID.OptionClick);
        if (0 > _direction)
        {
            HandleLanguagePrevClicked();
        }
        else
        {
            HandleLanguageNextClicked();
        }
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
            // 클릭음은 기본값(OptionClick)을 쓴다. 안내판의 확인 버튼과 같게 클릭음 + 닫기음(Close의 ResultUIClose) 순으로 울린다.
            // 닫기가 시작되면 SetInteractable(false)로 버튼이 막히므로 재입력으로 확인음이 겹쳐 울리지 않는다.
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

        // 처음 보여줄 언어를 정한다. 선택은 체크 버튼을 눌러야 적용된다.
        languageIndex = ResolveInitialLanguageIndex();
        UpdateLanguageDisplay();
        ResetLanguageFeedback();
        SnapLanguageDotCursor();
        lastFocusedLanguageSelectable = languageSelector;

        if (null != languageConfirmButton)
        {
            languageConfirmButton.SetInteractable(true, true);
        }

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
                FocusLanguageSelectable(GetDefaultLanguageSelectable());
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

    private void HandleShowCompleted()
    {
        if (false == isInputAllowed)
        {
            ActivateInput();
            return;
        }

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            FocusLanguageSelectable(GetDefaultLanguageSelectable());
        }
        else if (null != EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    /// <summary>패드 포커스를 둘 언어 단계의 기본 대상입니다. 마지막으로 포커스했던 것이 살아 있으면 그것, 아니면 선택기 행입니다.</summary>
    private Selectable GetDefaultLanguageSelectable()
    {
        if (null != lastFocusedLanguageSelectable && true == lastFocusedLanguageSelectable.gameObject.activeInHierarchy)
        {
            return lastFocusedLanguageSelectable;
        }

        return languageSelector;
    }

    private void FocusLanguageSelectable(Selectable _target)
    {
        if (null == _target) return;
        lastFocusedLanguageSelectable = _target;

        if (null == EventSystem.current) return;

        if (EventSystem.current.currentSelectedGameObject != _target.gameObject)
        {
            EventSystem.current.SetSelectedGameObject(_target.gameObject);
            return;
        }

        // 이미 선택된 대상이면 OnSelect가 다시 오지 않으므로 커서와 포커스 표시를 직접 다시 띄운다.
        if (_target == languageSelector)
        {
            languageSelector.ShowCursor();
            languageSelector.ApplyFocusVisual(true);
        }
        else if (_target == languageConfirmButton)
        {
            languageConfirmButton.ForceHover();
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


    private void HandleLanguageConfirmClicked()
    {
        if (true == IsLanguageInputBlocked()) return;

        // 메인 메뉴 버튼/안내판의 확인과 같이, 버튼의 클릭음(OptionClick)에 메인 메뉴 클릭음(MainClick)을 함께 울린다.
        Sound.PlayUI(SoundID.MainClick);

        isTransitioning = true;
        ResetLanguageFeedback();

        // 1. 선택한 언어 확정. 넘기는 동안 이미 적용되어 있으므로 보통은 아무 일도 하지 않는다.
        EOptionLanguage _selected = languageBindings[languageIndex].Language;
        SettingsManager.Instance.SetLanguage(_selected);

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
            suppressNextConsentSelectAudio = true;
            FocusConsentItem(lastFocusedConsentSelectable ?? (Selectable)consentToggle);
            suppressNextConsentSelectAudio = false;
        }
    }

    /// <summary>
    /// 약관 동의 패널에서 취소 키(ESC / 패드 B)를 눌렀을 때 언어 선택 패널로 되돌아갑니다.
    /// TransitionToConsentPanel의 역순 연출이며, 선택기는 방금 고른 언어를 그대로 보여줍니다.
    /// </summary>
    private void TransitionToLanguagePanel()
    {
        KillTransition();
        isTransitioning = true;

        // 페이드아웃 동안 약관 쪽 입력을 끊는다. 확인 버튼은 ConsentPanel의 자식이 아니라 형제라
        // 패널의 CanvasGroup이 막아주지 않으므로 따로 내리고(내부에서 ForceUnhover까지 처리),
        // 선택도 비워서 게임패드 Submit이 토글에 들어가지 않게 한다.
        if (null != consentPanel)
        {
            consentPanel.interactable = false;
            consentPanel.blocksRaycasts = false;
        }
        if (null != confirmButton)
        {
            confirmButton.SetInteractable(false);
        }
        if (null != EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
        hoveredConsentToggle = null;
        ForceUnhoverAll();

        Sound.PlayUI(SoundID.ResultUIClose);

        Sequence _seq = DOTween.Sequence();

        if (null != consentPanel)
        {
            _seq.Append(consentPanel.DOFade(0f, fadeDuration * 0.7f).SetEase(Ease.InQuad));
        }

        _seq.AppendCallback(SetupLanguagePanelOnReturn);

        if (null != languagePanel)
        {
            _seq.Append(languagePanel.DOFade(1f, fadeDuration * 0.7f).SetEase(Ease.OutQuad));
        }

        _seq.OnComplete(HandleLanguagePanelReturned);
        _seq.SetTarget(this);
        panelTransitionTween = _seq;
    }

    private void SetupLanguagePanelOnReturn()
    {
        // 약관 단계 표시를 여기서 내린다. 페이드아웃 도중에 내리면 그 사이 언어 입력 차단
        // (IsLanguageInputBlocked)과 장치 전환 처리가 아직 꺼져 있는 언어 패널을 대상으로 동작한다.
        isConsentPhase = false;

        if (null != consentPanel) consentPanel.gameObject.SetActive(false);
        if (null != confirmButton) confirmButton.gameObject.SetActive(false);

        if (null != languagePanel)
        {
            // 입력은 페이드인이 끝난 HandleLanguagePanelReturned에서 연다. 여기서 열면 그 사이 클릭이
            // isTransitioning에 막혀 클릭음만 나고 아무 일도 일어나지 않는다.
            languagePanel.gameObject.SetActive(true);
            languagePanel.alpha = 0f;
            languagePanel.interactable = false;
            languagePanel.blocksRaycasts = false;
        }

        // 패널이 꺼져 있던 동안 멈춘 연출이 있을 수 있으니 이름/점 커서를 현재 언어 기준으로 다시 맞춘다.
        UpdateLanguageDisplay();
        ResetLanguageFeedback();
        SnapLanguageDotCursor();

        // 되돌아온 목적은 언어를 다시 고르는 것이므로 패드 포커스는 체크 버튼이 아니라 선택기에서 시작한다.
        lastFocusedLanguageSelectable = languageSelector;

        // 다시 약관으로 넘어가면 토글이 모두 꺼진 상태로 초기화되므로(SetupConsentPanelOnTransition),
        // 지난번 포커스가 비활성 확인 버튼에 남아 있으면 첫 포커스가 그리로 가 버린다.
        lastFocusedConsentSelectable = null;
    }

    private void HandleLanguagePanelReturned()
    {
        isTransitioning = false;
        if (null != languagePanel)
        {
            languagePanel.interactable = true;
            languagePanel.blocksRaycasts = true;
        }

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            FocusLanguageSelectable(GetDefaultLanguageSelectable());
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

        // 메인 메뉴 안내판의 확인(UI_MainMenu.ExecuteNewGame)과 같이, 버튼의 클릭음(OptionClick)과 닫기음(Close의 ResultUIClose)에
        // 메인 메뉴 클릭음(MainClick)을 함께 울린다. 확정되지 않는 경우(선택 없음/닫는 중)에는 위에서 이미 반환한다.
        Sound.PlayUI(SoundID.MainClick);

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

        if (null != languageTitleText)
        {
            string _title = localizationManager.GetText(LocKeys.OptionUI.language);
            if (false == string.IsNullOrEmpty(_title)) languageTitleText.text = _title;
        }

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
            // 패널 전환 중에는 포커스를 잡지 않는다. 전환 완료 콜백(HandleConsentPanelShown /
            // HandleLanguagePanelReturned)이 그 시점의 장치를 보고 포커스를 잡아준다.
            // 특히 마우스 모드에서 패드 B로 되돌아갈 때는 취소 콜백(Input System 업데이트)이
            // 장치 전환 폴링(InputManager.Update)보다 먼저 돌아서, 여기서 막지 않으면 사라지는
            // 중인 약관 토글에 포커스·커서·hover음이 들어간다.
            if (true == isTransitioning) return;

            if (false == isConsentPhase)
            {
                // 마우스가 올라가 있던 대상으로 포커스를 옮긴다. 없으면 마지막 포커스(처음엔 선택기 행)다.
                Selectable _targetLanguage;
                if (null != languageConfirmButton && true == languageConfirmButton.IsMouseOver())
                {
                    _targetLanguage = languageConfirmButton;
                }
                else if (null != languageSelector && true == languageSelector.IsMouseOver())
                {
                    _targetLanguage = languageSelector;
                }
                else
                {
                    _targetLanguage = GetDefaultLanguageSelectable();
                }

                ForceUnhoverAll();
                FocusLanguageSelectable(_targetLanguage);
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
            // 마우스로 바꾸면 선택이 지워지므로, 패드로 돌아올 때 같은 자리에서 시작하도록 지금 포커스를 기억해 둔다.
            if (false == isConsentPhase && null != EventSystem.current && null != EventSystem.current.currentSelectedGameObject)
            {
                GameObject _selectedGo = EventSystem.current.currentSelectedGameObject;
                if (_selectedGo == languageSelector.gameObject || _selectedGo == languageConfirmButton.gameObject)
                {
                    lastFocusedLanguageSelectable = _selectedGo.GetComponent<Selectable>();
                }
            }

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

    private void ForceUnhoverAll()
    {
        if (null != languageSelector)
        {
            languageSelector.HideCursor();
            languageSelector.ApplyFocusVisual(false);

            if (null != languageSelector.LeftArrowButton) languageSelector.LeftArrowButton.ForceUnhover();
            if (null != languageSelector.RightArrowButton) languageSelector.RightArrowButton.ForceUnhover();
        }

        if (null != languageConfirmButton)
        {
            languageConfirmButton.ForceUnhover();
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
        ResetLanguageFeedback();
        onCompletedCallback = null;
        if (null != inputManager && null != inputManager.inputReader && null != cachedOnDeviceChanged)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
        }
    }
}
