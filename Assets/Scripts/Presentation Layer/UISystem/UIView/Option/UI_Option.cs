using UnityEngine;
using System;
using TMPro;

/// <summary>
/// 옵션 UI 시스템 전체를 총괄하는 최상위 컨트롤러입니다.
/// 메인 메뉴나 ESC 메뉴에서 호출되어 옵션 창을 띄우고 닫는 역할을 합니다.
/// 설정값의 소유·적용·저장은 SettingsManager가 담당하며, 이 클래스는 표시와 입력 전달만 합니다.
/// </summary>
public class UI_Option : MonoBehaviour, IUIDepthCloseable
{
    public event Action<EOptionLanguage> OnLanguageOptionChangedEvent;

    // 외부 컴포넌트 참조
    [Header("Core System")]
    [SerializeField] private UI_OptionTabGroup tabGroup;
    [SerializeField] private GameObject optionPanelRoot;
    [SerializeField] private UI_OptionButton applyButton;
    [SerializeField] private UI_OptionButton closeButton;
    [SerializeField] private UI_WarningPopup warningPopup;

    [Header("Tab Localization Keys")]
    [SerializeField, Tooltip("탭 이름으로 쓸 LocKeys.OptionUI 변수명 (예: tabGameplay)")]
    private string[] tabLocalizeKeys = { "tabGameplay", "tabSound", "tabGraphic", "tabControl" };

    [Header("Gameplay Options")]
    [SerializeField] private UI_OptionSelector languageSelector;
    [SerializeField] private UI_OptionSelector resolutionSelector;
    [SerializeField] private UI_OptionSelector windowModeSelector;
    [SerializeField] private UI_OptionSelector fpsSelector;
    [SerializeField] private UI_OptionSelector pauseOnUnfocusSelector;

    [SerializeField] private UI_OptionSlider cameraShakeSlider;
    [SerializeField] private UI_OptionSlider crosshairBrightnessSlider;
    [SerializeField] private UI_OptionSlider chromaticAberrationSlider;
    [SerializeField] private UI_OptionSlider brightnessSlider;
    [SerializeField] private UI_OptionSlider saturationSlider;

    [Header("Sound Options")]
    [SerializeField] private UI_OptionSlider masterVolumeSlider;
    [SerializeField] private UI_OptionSlider bgmVolumeSlider;
    [SerializeField] private UI_OptionSlider sfxVolumeSlider;

    [SerializeField, Tooltip("효과음 슬라이더를 조작할 때 들려줄 미리듣기 사운드. 반드시 SFX 믹서 " +
        "그룹을 타는 사운드여야 조절한 볼륨이 그대로 반영되어 들린다(UI 그룹 사운드는 영향을 받지 않는다).")]
    private SoundID sfxVolumePreviewSound = SoundID.OptionSFXBarTick;

    // 슬라이더는 소수점 단위로 값이 들어오지만, 미리듣기는 화면에 표시되는 정수(%) 눈금이
    // 바뀔 때만 울려야 "매 틱마다 재생"이라는 의도와 맞는다. 그 위에 0.03초 쿨타임을 안전장치로
    // 둬서, 아주 짧은 시간에 여러 눈금을 오가더라도 소리가 겹쳐 울리지 않게 한다.
    private const float SfxPreviewInterval = 0.03f;
    private float lastSfxPreviewTime = float.NegativeInfinity;
    private int lastSfxPreviewTick = int.MinValue;

    [Header("Control Options")]
    [SerializeField] private Transform keyBindRowContainer;           // 행들이 배치될 부모 Transform (ScrollView Content 등)
    [SerializeField] private UI_OptionKeyBindRow keyBindRowPrefab;    // 행 프리팹
    [SerializeField] private UI_OptionButton resetAllBindingsButton;  // "전체 초기화" 버튼
    [SerializeField] private GameObject rebindOverlay;                // "키를 입력하세요" 오버레이
    [SerializeField] private TextMeshProUGUI rebindOverlayText;       // 오버레이 안내 텍스트
    [SerializeField] private KeyIconDatabase keyIconDatabase;         // 키 아이콘 매핑 DB

    private Action onCloseAction;
    private Action hideAction;

    // 델리게이트 캐싱 (클로저 할당 원천 차단)
    private Action onLanguageLeft;
    private Action onLanguageRight;
    private Action onResolutionLeft;
    private Action onResolutionRight;
    private Action onWindowModeLeft;
    private Action onWindowModeRight;
    private Action onFpsLeft;
    private Action onFpsRight;
    private Action onPauseLeft;
    private Action onPauseRight;

    private Action<float> onCameraShakeChanged;
    private Action<float> onCrosshairBrightnessChanged;
    private Action<float> onChromaticAberrationChanged;
    private Action<float> onBrightnessChanged;
    private Action<float> onSaturationChanged;
    private Action<float> onMasterVolumeChanged;
    private Action<float> onBgmVolumeChanged;
    private Action<float> onSfxVolumeChanged;

    private Action<EOptionLanguage> onSettingsLanguageChanged;
    private Action<EWindowMode> onSettingsWindowModeChanged;

    private bool isInitialized = false;
    private bool isResetAllConfirmationOpen = false;
    private LocalizationManager locManager;
    private SettingsManager settings;
    private InputManager inputManager;
    private UIDepthController depthController;

    public bool IsActive => gameObject.activeSelf;
    private System.Collections.Generic.List<UI_OptionKeyBindRow> keyBindRows 
        = new System.Collections.Generic.List<UI_OptionKeyBindRow>();
    private string[] cachedTabTexts;

    private readonly struct ApplyTargetSettingsSnapshot
    {
        public readonly EWindowMode windowMode;
        public readonly EResolution resolution;
        public readonly EFPS fps;
        public readonly EOnOff pauseOnUnfocus;
        public readonly float cameraShake;
        public readonly float crosshairBrightness;

        public ApplyTargetSettingsSnapshot(in SettingsData _data)
        {
            windowMode = _data.windowMode;
            resolution = _data.resolution;
            fps = _data.fps;
            pauseOnUnfocus = _data.pauseOnUnfocus;
            cameraShake = _data.cameraShake;
            crosshairBrightness = _data.crosshairBrightness;
        }

        public bool Equals(in SettingsData _current)
        {
            return windowMode == _current.windowMode
                && resolution == _current.resolution
                && fps == _current.fps
                && pauseOnUnfocus == _current.pauseOnUnfocus
                && Mathf.Approximately(cameraShake, _current.cameraShake)
                && Mathf.Approximately(crosshairBrightness, _current.crosshairBrightness);
        }
    }

    private ApplyTargetSettingsSnapshot savedSnapshot;

    // 캐싱 델리게이트
    private Action cachedOnApplyClicked;
    private Action cachedConfirmDiscardAndClose;
    private Action cachedCancelDiscardAndClose;
    private Action<ERebindableAction> cachedOnRowRebindRequested;
    private Action<ERebindableAction> cachedOnRowResetRequested;
    private Action cachedOnResetAllClicked;
    private Action cachedRefreshKeyBindRows;
    private Action cachedExecuteResetAll;
    private Action cachedCancelResetAll;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UIViewContext _ctx)
    {
        if (true == isInitialized) return;

        if (null != _ctx)
        {
            locManager = _ctx.localizationManager;
            inputManager = _ctx.inputManager;
            depthController = _ctx.depthController;
        }

        settings = SettingsManager.Instance;
        settings.Bind(locManager);

        CacheDelegates();
        CacheControlDelegates();

        settings.OnLanguageChangedEvent -= onSettingsLanguageChanged;
        settings.OnLanguageChangedEvent += onSettingsLanguageChanged;
        settings.OnWindowModeChangedEvent -= onSettingsWindowModeChanged;
        settings.OnWindowModeChangedEvent += onSettingsWindowModeChanged;

        if (null == hideAction) hideAction = Hide;

        if (null != tabGroup)
        {
            tabGroup.Initialize(BuildTabTexts());
        }

        if (null != applyButton)
        {
            applyButton.Initialize(cachedOnApplyClicked, SoundID.MainButtonHover, SoundID.MainClick);
        }

        if (null != closeButton)
        {
            closeButton.Initialize(hideAction, SoundID.MainButtonHover, SoundID.MainClick);
        }

        if (null != warningPopup)
        {
            warningPopup.Initialize(_ctx);
        }

        InitializeSelectors();
        InitializeSliders();
        InitializeControlTab();

        RefreshResolutionSelector();
        RefreshButtonLabels();

        isInitialized = true;
    }

    public void Show(Action _onCloseCallback = null)
    {
        gameObject.SetActive(true);

        if (false == isInitialized)
        {
            Debug.LogError("UI_Option is not initialized properly (Missing Context).");
            return;
        }

        onCloseAction = _onCloseCallback;

        if (null != settings)
        {
            savedSnapshot = new ApplyTargetSettingsSnapshot(settings.Current);
        }

        depthController?.RegisterView(this);

        // 옵션 창은 ESC 메뉴(일시정지)에서 열리는데, 그 상태에서는 게임플레이 사운드가 음소거라
        // 효과음 볼륨을 조절해도 아무것도 들리지 않는다. 창이 열려 있는 동안만 덕킹/음소거를 풀어
        // 조절 중인 소리를 실제로 들을 수 있게 한다.
        Sound.SetAudioPreviewMode(true);

        if (null != optionPanelRoot)
        {
            optionPanelRoot.SetActive(true);
        }

        if (null != inputManager)
        {
            inputManager.BeginEditSession();
            inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshKeyBindRows;
            inputManager.inputReader.KeyBindingsChangedEvent += cachedRefreshKeyBindRows;
            RefreshKeyBindRows();
        }

        UpdateApplyButtonState();
    }

    public void Hide()
    {
        // Option이 ESC가 아닌 다른 경로(escUI.Hide() 등)로 강제로 닫힐 때, 그 안에 중첩된 경고
        // 팝업(warningPopup)이 아직 열려 있으면 뎁스 스택에 좀비로 남는다. 팝업부터 확실히 닫아 정리한다.
        if (null != warningPopup && true == warningPopup.IsActive)
        {
            warningPopup.Hide();
            return;
        }

        if (true == IsDirty())
        {
            if (null != warningPopup && null != locManager)
            {
                string _warningMsg = locManager.GetText(LocKeys.OptionUI.unsavedChangesWarning);
                if (true == string.IsNullOrEmpty(_warningMsg))
                {
                    _warningMsg = "변경된 설정을 저장하지 않고 나가시겠습니까?";
                }

                warningPopup.ShowWarning(
                    _warningMsg,
                    cachedConfirmDiscardAndClose,
                    cachedCancelDiscardAndClose,
                    SoundID.ResultUIOpen,
                    SoundID.ResultUIClose,
                    SoundID.ResultUIHover);
            }
            else
            {
                OnDiscardAndCloseConfirmed();
            }
        }
        else
        {
            ForceHide();
        }
    }

    private void ForceHide()
    {
        depthController?.UnregisterView(this);

        if (null != warningPopup && true == warningPopup.IsActive)
        {
            warningPopup.Hide();
        }

        // 창을 닫으면 원래의 덕킹/일시정지 음소거 상태로 되돌린다(ESC 메뉴로 복귀하는 경우 등).
        Sound.SetAudioPreviewMode(false);

        if (null != optionPanelRoot)
        {
            optionPanelRoot.SetActive(false);
        }

        gameObject.SetActive(false);

        if (null != onCloseAction)
        {
            onCloseAction.Invoke();
            onCloseAction = null;
        }

        if (null != inputManager)
        {
            inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshKeyBindRows;

            // 리바인딩 진행 중이면 취소
            if (true == inputManager.IsRebinding)
            {
                inputManager.CancelRebind();
            }
            if (null != rebindOverlay) rebindOverlay.SetActive(false);
        }
    }

    private void OnDiscardAndCloseConfirmed()
    {
        Sound.PlayUI(SoundID.ResultUIClose);

        RestoreSnapshot(savedSnapshot);

        InitializeSelectors();
        InitializeSliders();
        RefreshResolutionSelector();

        ForceHide();
    }

    private void OnDiscardAndCloseCancelled()
    {
        Sound.PlayUI(SoundID.ResultUIClose);
    }

    private void OnApplyClicked()
    {
        if (null != settings)
        {
            settings.CommitChanges();
            savedSnapshot = new ApplyTargetSettingsSnapshot(settings.Current);
        }

        UpdateApplyButtonState();
    }

    // 초기화 관련 프라이빗 메서드
    private void CacheDelegates()
    {
        onLanguageLeft = OnLanguageLeft;
        onLanguageRight = OnLanguageRight;

        onResolutionLeft = OnResolutionLeft;
        onResolutionRight = OnResolutionRight;

        onWindowModeLeft = OnWindowModeLeft;
        onWindowModeRight = OnWindowModeRight;

        onFpsLeft = OnFpsLeft;
        onFpsRight = OnFpsRight;

        onPauseLeft = OnPauseLeft;
        onPauseRight = OnPauseRight;

        onCameraShakeChanged = OnCameraShakeChanged;
        onCrosshairBrightnessChanged = OnCrosshairBrightnessChanged;
        onChromaticAberrationChanged = OnChromaticAberrationChanged;
        onBrightnessChanged = OnBrightnessChanged;
        onSaturationChanged = OnSaturationChanged;

        onMasterVolumeChanged = OnMasterVolumeChanged;
        onBgmVolumeChanged = OnBgmVolumeChanged;
        onSfxVolumeChanged = OnSfxVolumeChanged;

        onSettingsLanguageChanged = HandleLanguageChanged;
        onSettingsWindowModeChanged = HandleWindowModeChanged;
    }

    private void CacheControlDelegates()
    {
        cachedOnApplyClicked = OnApplyClicked;
        cachedConfirmDiscardAndClose = OnDiscardAndCloseConfirmed;
        cachedCancelDiscardAndClose = OnDiscardAndCloseCancelled;

        cachedOnRowRebindRequested = OnRowRebindRequested;
        cachedOnRowResetRequested = OnRowResetRequested;
        cachedOnResetAllClicked = OnResetAllClicked;
        cachedRefreshKeyBindRows = RefreshKeyBindRows;
        
        cachedExecuteResetAll = ExecuteResetAllBindings;
        cachedCancelResetAll = CancelResetAllBindings;
    }

    private string GetText(int _compositeKey, string _fallback)
    {
        if (null != locManager)
        {
            string _res = locManager.GetText(_compositeKey);
            if (false == string.IsNullOrEmpty(_res)) return _res;
        }
        return _fallback;
    }

    private static System.Collections.Generic.Dictionary<string, int> locKeyCache;

    private string GetTextFromKeyString(string _keyName, string _fallback)
    {
        if (true == string.IsNullOrEmpty(_keyName)) return _fallback;

        if (null == locKeyCache)
        {
            locKeyCache = new System.Collections.Generic.Dictionary<string, int>();
            System.Reflection.FieldInfo[] _fields = typeof(LocKeys.OptionUI).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            for (int i = 0; _fields.Length > i; i++)
            {
                locKeyCache[_fields[i].Name] = (int)_fields[i].GetValue(null);
            }
        }

        int _compositeKey;
        if (true == locKeyCache.TryGetValue(_keyName, out _compositeKey))
        {
            return GetText(_compositeKey, _fallback);
        }

        return _fallback;
    }

    private string[] BuildTabTexts()
    {
        if (null == tabLocalizeKeys || 0 == tabLocalizeKeys.Length) return null;

        if (null == cachedTabTexts || cachedTabTexts.Length != tabLocalizeKeys.Length) cachedTabTexts = new string[tabLocalizeKeys.Length];
        for (int i = 0; tabLocalizeKeys.Length > i; i++)
        {
            cachedTabTexts[i] = GetTextFromKeyString(tabLocalizeKeys[i], "Tab");
        }
        return cachedTabTexts;
    }

    private void InitializeSelectors()
    {
        SettingsData _data = settings.Current;

        if (null != languageSelector) languageSelector.Initialize(GetText(LocKeys.OptionUI.language, "언어"), GetLanguageText(_data.language), onLanguageLeft, onLanguageRight);
        // 저장 원본이 아니라 실제 적용값으로 초기 표기한다. (선택기 순환 기준과 맞추기 위함)
        if (null != resolutionSelector) resolutionSelector.Initialize(GetText(LocKeys.OptionUI.resolution, "해상도"), SettingsManager.GetResolutionLabel(settings.EffectiveResolution), onResolutionLeft, onResolutionRight);
        if (null != windowModeSelector) windowModeSelector.Initialize(GetText(LocKeys.OptionUI.windowMode, "화면"), GetWindowModeText(_data.windowMode), onWindowModeLeft, onWindowModeRight);
        if (null != fpsSelector) fpsSelector.Initialize(GetText(LocKeys.OptionUI.fPS, "FPS"), GetFpsText(_data.fps), onFpsLeft, onFpsRight);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.Initialize(GetText(LocKeys.OptionUI.pauseOnUnfocus, "비활성화 중 게임 일시정지"), GetOnOffText(_data.pauseOnUnfocus), onPauseLeft, onPauseRight);
    }

    private void InitializeSliders()
    {
        SettingsData _data = settings.Current;

        if (null != cameraShakeSlider) cameraShakeSlider.Initialize(GetText(LocKeys.OptionUI.cameraShake, "카메라 흔들림"), _data.cameraShake, 0f, 100f, onCameraShakeChanged);
        if (null != crosshairBrightnessSlider) crosshairBrightnessSlider.Initialize(GetText(LocKeys.OptionUI.crosshairBrightness, "캐릭터 조준 인디케이터 밝기"), _data.crosshairBrightness, 0f, 100f, onCrosshairBrightnessChanged);
        if (null != chromaticAberrationSlider) chromaticAberrationSlider.Initialize(GetText(LocKeys.OptionUI.chromaticAberration, "색수차 효과"), _data.chromaticAberration, 0f, 100f, onChromaticAberrationChanged);
        if (null != brightnessSlider) brightnessSlider.Initialize(GetText(LocKeys.OptionUI.screenBrightness, "화면 명도"), _data.brightness, 0f, 100f, onBrightnessChanged);
        if (null != saturationSlider) saturationSlider.Initialize(GetText(LocKeys.OptionUI.screenSaturation, "화면 채도"), _data.saturation, 0f, 100f, onSaturationChanged);

        if (null != masterVolumeSlider) masterVolumeSlider.Initialize(GetText(LocKeys.OptionUI.masterVolume, "마스터 볼륨"), _data.masterVolume, 0f, 100f, onMasterVolumeChanged);
        if (null != bgmVolumeSlider) bgmVolumeSlider.Initialize(GetText(LocKeys.OptionUI.bGMVolume, "배경음악 볼륨"), _data.bgmVolume, 0f, 100f, onBgmVolumeChanged);
        if (null != sfxVolumeSlider) sfxVolumeSlider.Initialize(GetText(LocKeys.OptionUI.sFXVolume, "사운드 볼륨"), _data.sfxVolume, 0f, 100f, onSfxVolumeChanged);
    }

    private void InitializeControlTab()
    {
        if (null == inputManager || null == keyBindRowPrefab || null == keyBindRowContainer) return;

        // 기존 행 정리
        for (int i = 0; keyBindRows.Count > i; i++)
        {
            if (null != keyBindRows[i]) Destroy(keyBindRows[i].gameObject);
        }
        keyBindRows.Clear();

        // 리바인딩 가능한 액션 목록으로 행 동적 생성
        System.Collections.Generic.IReadOnlyList<ERebindableAction> _actions = inputManager.GetRebindableActions();
        for (int i = 0; _actions.Count > i; i++)
        {
            ERebindableAction _action = _actions[i];
            UI_OptionKeyBindRow _row = Instantiate(keyBindRowPrefab, keyBindRowContainer);

            string _label = GetActionLabel(_action);
            string _bindingPath = inputManager.GetBindingPath(_action);
            string _displayString = inputManager.GetBindingDisplayString(_action);
            bool _isConflict = inputManager.IsConflicting(_action);

            _row.Initialize(_action, _label, _bindingPath, _displayString, _isConflict,
                            keyIconDatabase, cachedOnRowRebindRequested, cachedOnRowResetRequested);
            keyBindRows.Add(_row);
        }

        // 전체 초기화 버튼
        if (null != resetAllBindingsButton)
        {
            resetAllBindingsButton.Initialize(cachedOnResetAllClicked);
        }

        // 오버레이 숨김
        if (null != rebindOverlay) rebindOverlay.SetActive(false);
    }

    private string GetActionLabel(ERebindableAction _action)
    {
        switch (_action)
        {
            case ERebindableAction.MoveUp:        return GetText(LocKeys.OptionUI.moveUp, "위로 이동");
            case ERebindableAction.MoveDown:      return GetText(LocKeys.OptionUI.moveDown, "아래로 이동");
            case ERebindableAction.MoveLeft:      return GetText(LocKeys.OptionUI.moveLeft, "왼쪽 이동");
            case ERebindableAction.MoveRight:     return GetText(LocKeys.OptionUI.moveRight, "오른쪽 이동");
            case ERebindableAction.Inventory:     return GetText(LocKeys.OptionUI.inventory, "인벤토리");
            case ERebindableAction.Interaction:   return GetText(LocKeys.OptionUI.interaction, "상호작용");
            case ERebindableAction.Attack:        return GetText(LocKeys.OptionUI.attack, "공격");
            case ERebindableAction.PotionKey:     return GetText(LocKeys.OptionUI.potionKey, "물약 사용");
            default:                              return _action.ToString();
        }
    }

    // 로컬라이징이 필요한 Enum 표기 변환기
    /// <summary>
    /// 언어 이름은 각 언어 자체 표기(한국어/English/日本語 …)를 씁니다. 어느 언어로 UI가 떠 있든
    /// 자기 언어를 찾을 수 있어야 하기 때문이며, 그래서 로컬라이징 데이터의 모든 열에 같은 값이
    /// 들어 있습니다. 값을 바꾸고 싶으면 OptionUI.json만 고치면 됩니다.
    /// 코드에 문자열을 박지 않는 이유는 그래야 폰트 문자셋 생성기가 이 글자들을 수집하기 때문입니다.
    /// </summary>
    private string GetLanguageText(EOptionLanguage _lang)
    {
        switch (_lang)
        {
            case EOptionLanguage.Korean: return GetText(LocKeys.OptionUI.languageKorean, "한국어");
            case EOptionLanguage.English: return GetText(LocKeys.OptionUI.languageEnglish, "English");
            case EOptionLanguage.ChineseSimplified: return GetText(LocKeys.OptionUI.languageChineseSimplified, "简体中文");
            case EOptionLanguage.ChineseTraditional: return GetText(LocKeys.OptionUI.languageChineseTraditional, "繁體中文");
            case EOptionLanguage.Japanese: return GetText(LocKeys.OptionUI.languageJapanese, "日本語");
        }
        return _lang.ToString();
    }

    private string GetWindowModeText(EWindowMode _mode)
    {
        switch (_mode)
        {
            case EWindowMode.Windowed: return GetText(LocKeys.OptionUI.windowed, "Windowed");
            case EWindowMode.Fullscreen: return GetText(LocKeys.OptionUI.fullscreen, "Fullscreen");
        }
        return _mode.ToString();
    }

    private string GetOnOffText(EOnOff _state)
    {
        switch (_state)
        {
            case EOnOff.On: return GetText(LocKeys.OptionUI.on, "On");
            case EOnOff.Off: return GetText(LocKeys.OptionUI.off, "Off");
        }
        return _state.ToString();
    }

    private string GetFpsText(EFPS _fps)
    {
        string _numberLabel = SettingsManager.GetFpsNumberLabel(_fps);
        if (false == string.IsNullOrEmpty(_numberLabel))
        {
            return _numberLabel;
        }

        if (EFPS.VSync == _fps)
        {
            return GetText(LocKeys.OptionUI.vSync, "V Sync");
        }
        else if (EFPS.Unlimited == _fps)
        {
            return GetText(LocKeys.OptionUI.unlimited, "무제한");
        }

        return _fps.ToString();
    }

    // SettingsManager 이벤트 반영
    private void HandleLanguageChanged(EOptionLanguage _lang)
    {
        // 언어 텍스트 재로드 후 UI 갱신
        InitializeSelectors();
        InitializeSliders();
        RefreshControlTabLabels();

        if (null != tabGroup)
        {
            string[] _tabTexts = BuildTabTexts();
            if (null != _tabTexts) tabGroup.RefreshTabTexts(_tabTexts);
        }

        RefreshButtonLabels();

        if (null != OnLanguageOptionChangedEvent) OnLanguageOptionChangedEvent.Invoke(_lang);
    }

    private void RefreshControlTabLabels()
    {
        if (null == inputManager) return;

        System.Collections.Generic.IReadOnlyList<ERebindableAction> _actions = inputManager.GetRebindableActions();
        for (int i = 0; keyBindRows.Count > i && _actions.Count > i; i++)
        {
            if (null == keyBindRows[i]) continue;
            keyBindRows[i].RefreshLabel(GetActionLabel(_actions[i]));
        }
    }

    private void RefreshButtonLabels()
    {
        if (null != resetAllBindingsButton)
        {
            string _txt = string.Empty;
            if (null != locManager) _txt = locManager.GetText(LocKeys.OptionUI.resetToDefault);
            if (true == string.IsNullOrEmpty(_txt)) _txt = "기본 값으로 초기화";
            resetAllBindingsButton.SetText(_txt);
        }

        if (null != applyButton)
        {
            string _txt = string.Empty;
            if (null != locManager) _txt = locManager.GetText(LocKeys.OptionUI.apply);
            if (true == string.IsNullOrEmpty(_txt)) _txt = "적용";
            applyButton.SetText(_txt);
        }

        if (null != closeButton)
        {
            string _txt = string.Empty;
            if (null != locManager) _txt = locManager.GetText(LocKeys.OptionUI.close);
            if (true == string.IsNullOrEmpty(_txt)) _txt = "닫기";
            closeButton.SetText(_txt);
        }
    }

    private void HandleWindowModeChanged(EWindowMode _mode)
    {
        if (null != windowModeSelector) windowModeSelector.UpdateValue(GetWindowModeText(_mode));
        RefreshResolutionSelector();
    }

    /// <summary>전체화면 여부에 따라 해상도 셀렉터의 조작 가능 여부와 표기를 갱신합니다.</summary>
    private void RefreshResolutionSelector()
    {
        if (null == resolutionSelector) return;

        SettingsData _data = settings.Current;
        bool _isFullscreen = (EWindowMode.Fullscreen == _data.windowMode);

        resolutionSelector.SetInteractable(false == _isFullscreen);

        if (true == _isFullscreen)
        {
            // 전체화면일 때는 모니터 해상도로 강제 표기
            resolutionSelector.UpdateValue(SettingsManager.GetMonitorResolutionLabel());
        }
        else
        {
            // 실제로 적용되는 값을 보여준다. 선택기 순환도 같은 값을 기준으로 돌므로
            // 화면에 보이는 항목과 화살표 조작 결과가 일치한다.
            // 저장된 값 자체는 그대로 남아, 큰 모니터로 돌아가면 원래 설정이 복원된다.
            resolutionSelector.UpdateValue(SettingsManager.GetResolutionLabel(settings.EffectiveResolution));
        }
    }

    // 명시적 델리게이트 바인딩 메서드들 (GC 할당 방지)
    private void OnLanguageLeft() { settings.CycleLanguage(-1); settings.CommitChanges(); }
    private void OnLanguageRight() { settings.CycleLanguage(1); settings.CommitChanges(); }

    // 표기 규칙(전체화면 강제 표기, 표시 불가 해상도 강등)이 한 곳에만 있도록 갱신을 위임한다.
    private void OnResolutionLeft()
    {
        settings.CycleResolution(-1);
        RefreshResolutionSelector();
        UpdateApplyButtonState();
    }

    private void OnResolutionRight()
    {
        settings.CycleResolution(1);
        RefreshResolutionSelector();
        UpdateApplyButtonState();
    }

    private void OnWindowModeLeft() { settings.CycleWindowMode(-1); UpdateApplyButtonState(); }
    private void OnWindowModeRight() { settings.CycleWindowMode(1); UpdateApplyButtonState(); }

    private void OnFpsLeft()
    {
        settings.CycleFps(-1);
        if (null != fpsSelector) fpsSelector.UpdateValue(GetFpsText(settings.Current.fps));
        UpdateApplyButtonState();
    }

    private void OnFpsRight()
    {
        settings.CycleFps(1);
        if (null != fpsSelector) fpsSelector.UpdateValue(GetFpsText(settings.Current.fps));
        UpdateApplyButtonState();
    }

    private void OnPauseLeft()
    {
        settings.CyclePauseOnUnfocus(-1);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.UpdateValue(GetOnOffText(settings.Current.pauseOnUnfocus));
        UpdateApplyButtonState();
    }

    private void OnPauseRight()
    {
        settings.CyclePauseOnUnfocus(1);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.UpdateValue(GetOnOffText(settings.Current.pauseOnUnfocus));
        UpdateApplyButtonState();
    }

    private void OnCameraShakeChanged(float _val) { settings.SetCameraShake(_val); UpdateApplyButtonState(); }
    private void OnCrosshairBrightnessChanged(float _val) { settings.SetCrosshairBrightness(_val); UpdateApplyButtonState(); }

    // 화면 효과는 조작 즉시 실시간 반영 및 자동 저장된다.
    private void OnChromaticAberrationChanged(float _val)
    {
        settings.SetChromaticAberration(_val);
        settings.ApplyGraphicsSettingsLive();
        settings.Save();
    }

    private void OnBrightnessChanged(float _val)
    {
        settings.SetBrightness(_val);
        settings.ApplyGraphicsSettingsLive();
        settings.Save();
    }

    private void OnSaturationChanged(float _val)
    {
        settings.SetSaturation(_val);
        settings.ApplyGraphicsSettingsLive();
        settings.Save();
    }

    // 볼륨은 조작 즉시 실시간 반영 및 자동 저장된다.
    private void OnMasterVolumeChanged(float _val)
    {
        settings.SetMasterVolume(_val);
        settings.ApplyAudioSettingsLive();
        settings.Save();
    }

    private void OnBgmVolumeChanged(float _val)
    {
        settings.SetBgmVolume(_val);
        settings.ApplyAudioSettingsLive();
        settings.Save();
    }

    private void OnSfxVolumeChanged(float _val)
    {
        settings.SetSfxVolume(_val);
        settings.ApplyAudioSettingsLive();
        PlaySfxVolumePreview(_val);
        settings.Save();
    }

    // 효과음은 BGM과 달리 조작하는 동안 계속 울리는 소리가 없어서, 슬라이더를 움직여도 지금
    // 몇 %인지 귀로 알 수 없다. 그래서 화면에 표시되는 정수(%) 눈금이 바뀔 때마다 효과음 그룹을
    // 타는 소리를 짧게 재생해 유저가 바로 체감하게 한다.
    private void PlaySfxVolumePreview(float _val)
    {
        int _tick = Mathf.RoundToInt(_val);
        if (_tick == lastSfxPreviewTick) return;

        float _now = Time.unscaledTime;
        if (SfxPreviewInterval > _now - lastSfxPreviewTime) return;

        lastSfxPreviewTick = _tick;
        lastSfxPreviewTime = _now;
        Sound.PlayUI(sfxVolumePreviewSound);
    }

    private void RefreshKeyBindRows()
    {
        if (null == inputManager) return;

        System.Collections.Generic.IReadOnlyList<ERebindableAction> _actions = inputManager.GetRebindableActions();
        for (int i = 0; keyBindRows.Count > i && _actions.Count > i; i++)
        {
            if (null == keyBindRows[i]) continue;

            string _bindingPath = inputManager.GetBindingPath(_actions[i]);
            string _displayString = inputManager.GetBindingDisplayString(_actions[i]);
            bool _isConflict = inputManager.IsConflicting(_actions[i]);
            keyBindRows[i].Refresh(_bindingPath, _displayString, _isConflict);
        }
    }

    private void OnRowRebindRequested(ERebindableAction _action)
    {
        if (null == inputManager || true == inputManager.IsRebinding) return;

        // 오버레이 표시
        if (null != rebindOverlay) rebindOverlay.SetActive(true);
        if (null != rebindOverlayText)
        {
            rebindOverlayText.text = GetText(LocKeys.OptionUI.pressKeyPrompt, "변경할 키를 입력하세요.\n(ESC: 취소)");
        }

        inputManager.StartRebind(_action, OnRebindFinished);
    }

    private void OnRebindFinished(ERebindResult _result, ERebindableAction? _conflict)
    {
        // 오버레이 숨김
        if (null != rebindOverlay) rebindOverlay.SetActive(false);

        if (null != inputManager && false == inputManager.HasAnyConflict())
        {
            inputManager.CommitEditSession();
        }
    }

    private void OnRowResetRequested(ERebindableAction _action)
    {
        if (null == inputManager) return;
        inputManager.ResetBinding(_action);
        if (false == inputManager.HasAnyConflict())
        {
            inputManager.CommitEditSession();
        }
    }

    private void OnResetAllClicked()
    {
        if (null != warningPopup && null != locManager)
        {
            isResetAllConfirmationOpen = true;
            string _warningMsg = locManager.GetText(LocKeys.OptionUI.resetAllWarning);
            warningPopup.ShowWarning(
                _warningMsg,
                cachedExecuteResetAll,
                cachedCancelResetAll,
                SoundID.ResultUIOpen,
                SoundID.ResultUIClose,
                SoundID.ResultUIHover);
        }
        else
        {
            ExecuteResetAllBindings(); // 팝업이 없거나 로컬매니저가 없으면 바로 강제실행
        }
    }

    private void ExecuteResetAllBindings()
    {
        PlayResetAllConfirmationClickSound();

        if (null != inputManager)
        {
            inputManager.ResetAllBindings();
            if (false == inputManager.HasAnyConflict())
            {
                inputManager.CommitEditSession();
            }
        }
    }

    private void CancelResetAllBindings()
    {
        PlayResetAllConfirmationClickSound();
        // 취소 시 특별한 동작 없음
    }

    private void PlayResetAllConfirmationClickSound()
    {
        if (false == isResetAllConfirmationOpen)
            return;

        isResetAllConfirmationOpen = false;
        Sound.PlayUI(SoundID.MainClick);
    }

    private bool IsDirty()
    {
        if (null == settings) return false;
        return false == savedSnapshot.Equals(settings.Current);
    }

    private void UpdateApplyButtonState()
    {
        if (null == applyButton) return;
        bool _dirty = IsDirty();
        applyButton.SetInteractable(_dirty);
    }

    private void RestoreSnapshot(in ApplyTargetSettingsSnapshot _snapshot)
    {
        if (null == settings) return;

        settings.SetCameraShake(_snapshot.cameraShake);
        settings.SetCrosshairBrightness(_snapshot.crosshairBrightness);

        while (settings.Current.windowMode != _snapshot.windowMode)
        {
            settings.CycleWindowMode(1);
        }

        while (settings.Current.resolution != _snapshot.resolution)
        {
            settings.CycleResolution(1);
        }

        while (settings.Current.fps != _snapshot.fps)
        {
            settings.CycleFps(1);
        }

        while (settings.Current.pauseOnUnfocus != _snapshot.pauseOnUnfocus)
        {
            settings.CyclePauseOnUnfocus(1);
        }

        settings.ApplySettings();
        Application.runInBackground = (EOnOff.Off == _snapshot.pauseOnUnfocus);
    }

    // 유니티 이벤트 함수
    private void OnDestroy()
    {
        depthController?.UnregisterView(this);

        if (null != settings)
        {
            settings.OnLanguageChangedEvent -= onSettingsLanguageChanged;
            settings.OnWindowModeChangedEvent -= onSettingsWindowModeChanged;
            settings = null;
        }

        OnLanguageOptionChangedEvent = null;

        onLanguageLeft = null; onLanguageRight = null;
        onResolutionLeft = null; onResolutionRight = null;
        onWindowModeLeft = null; onWindowModeRight = null;
        onFpsLeft = null; onFpsRight = null;
        onPauseLeft = null; onPauseRight = null;

        onCameraShakeChanged = null;
        onCrosshairBrightnessChanged = null;
        onChromaticAberrationChanged = null;
        onBrightnessChanged = null;
        onSaturationChanged = null;
        onMasterVolumeChanged = null;
        onBgmVolumeChanged = null;
        onSfxVolumeChanged = null;

        onSettingsLanguageChanged = null;
        onSettingsWindowModeChanged = null;

        if (null != inputManager)
        {
            inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshKeyBindRows;
        }

        cachedOnApplyClicked = null;
        cachedConfirmDiscardAndClose = null;
        cachedCancelDiscardAndClose = null;

        cachedOnRowRebindRequested = null;
        cachedOnRowResetRequested = null;
        cachedOnResetAllClicked = null;
        cachedRefreshKeyBindRows = null;
        cachedExecuteResetAll = null;
        cachedCancelResetAll = null;
    }
}
