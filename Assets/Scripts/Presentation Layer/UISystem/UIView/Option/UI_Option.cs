using UnityEngine;
using System;
using TMPro;

/// <summary>
/// 옵션 UI 시스템 전체를 총괄하는 최상위 컨트롤러입니다.
/// 메인 메뉴나 ESC 메뉴에서 호출되어 옵션 창을 띄우고 닫는 역할을 합니다.
/// 설정값의 소유·적용·저장은 SettingsManager가 담당하며, 이 클래스는 표시와 입력 전달만 합니다.
/// </summary>
public class UI_Option : MonoBehaviour
{
    public event Action<EOptionLanguage> OnLanguageOptionChangedEvent;

    // 외부 컴포넌트 참조
    [Header("Core System")]
    [SerializeField] private UI_OptionTabGroup tabGroup;
    [SerializeField] private GameObject optionPanelRoot;
    [SerializeField] private UI_OptionButton closeButton;

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
    private LocalizationManager locManager;
    private SettingsManager settings;
    private InputManager inputManager;
    private System.Collections.Generic.List<UI_OptionKeyBindRow> keyBindRows 
        = new System.Collections.Generic.List<UI_OptionKeyBindRow>();

    // 캐싱 델리게이트
    private Action<ERebindableAction> cachedOnRowRebindRequested;
    private Action<ERebindableAction> cachedOnRowResetRequested;
    private Action cachedOnResetAllClicked;
    private Action cachedRefreshKeyBindRows;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UIViewContext _ctx)
    {
        if (true == isInitialized) return;

        if (null != _ctx)
        {
            locManager = _ctx.localizationManager;
            inputManager = _ctx.inputManager;
        }

        settings = SettingsManager.Instance;
        settings.Bind(locManager);

        CacheDelegates();

        settings.OnLanguageChangedEvent -= onSettingsLanguageChanged;
        settings.OnLanguageChangedEvent += onSettingsLanguageChanged;
        settings.OnWindowModeChangedEvent -= onSettingsWindowModeChanged;
        settings.OnWindowModeChangedEvent += onSettingsWindowModeChanged;

        if (null == hideAction) hideAction = Hide;

        if (null != tabGroup)
        {
            tabGroup.Initialize(BuildTabTexts());
        }

        if (null != closeButton)
        {
            closeButton.Initialize(hideAction);
        }

        CacheControlDelegates();

        InitializeSelectors();
        InitializeSliders();
        InitializeControlTab();

        RefreshResolutionSelector();

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
    }

    public void Hide()
    {
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

        // 닫기 버튼을 누를 때 실제 설정 반영 및 저장.
        // 바꾼 값이 없으면 CommitChanges가 스스로 스킵하므로,
        // 창을 열었다 닫기만 해도 해상도가 바뀌는 일은 없다.
        if (null != settings)
        {
            settings.CommitChanges();
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

            // 충돌 없으면 저장, 있으면 변경 취소
            if (false == inputManager.HasAnyConflict())
            {
                inputManager.CommitEditSession();
            }
            else
            {
                inputManager.DiscardEditSession();
            }
        }
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
        cachedOnRowRebindRequested = OnRowRebindRequested;
        cachedOnRowResetRequested = OnRowResetRequested;
        cachedOnResetAllClicked = OnResetAllClicked;
        cachedRefreshKeyBindRows = RefreshKeyBindRows;
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
            for (int i = 0; i < _fields.Length; i++)
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

        string[] _tabTexts = new string[tabLocalizeKeys.Length];
        for (int i = 0; i < tabLocalizeKeys.Length; i++)
        {
            _tabTexts[i] = GetTextFromKeyString(tabLocalizeKeys[i], "Tab");
        }
        return _tabTexts;
    }

    private void InitializeSelectors()
    {
        SettingsData _data = settings.Current;

        if (null != languageSelector) languageSelector.Initialize(GetText(LocKeys.OptionUI.language, "언어"), SettingsManager.GetLanguageLabel(_data.language), onLanguageLeft, onLanguageRight);
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
        for (int i = 0; i < keyBindRows.Count; i++)
        {
            if (null != keyBindRows[i]) Destroy(keyBindRows[i].gameObject);
        }
        keyBindRows.Clear();

        // 리바인딩 가능한 액션 목록으로 행 동적 생성
        System.Collections.Generic.IReadOnlyList<ERebindableAction> _actions = inputManager.GetRebindableActions();
        for (int i = 0; i < _actions.Count; i++)
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

        if (null != OnLanguageOptionChangedEvent) OnLanguageOptionChangedEvent.Invoke(_lang);
    }

    private void RefreshControlTabLabels()
    {
        if (null == inputManager) return;

        System.Collections.Generic.IReadOnlyList<ERebindableAction> _actions = inputManager.GetRebindableActions();
        for (int i = 0; i < keyBindRows.Count && i < _actions.Count; i++)
        {
            if (null == keyBindRows[i]) continue;
            keyBindRows[i].RefreshLabel(GetActionLabel(_actions[i]));
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
    private void OnLanguageLeft() { settings.CycleLanguage(-1); }
    private void OnLanguageRight() { settings.CycleLanguage(1); }

    // 표기 규칙(전체화면 강제 표기, 표시 불가 해상도 강등)이 한 곳에만 있도록 갱신을 위임한다.
    private void OnResolutionLeft()
    {
        settings.CycleResolution(-1);
        RefreshResolutionSelector();
    }

    private void OnResolutionRight()
    {
        settings.CycleResolution(1);
        RefreshResolutionSelector();
    }

    private void OnWindowModeLeft() { settings.CycleWindowMode(-1); }
    private void OnWindowModeRight() { settings.CycleWindowMode(1); }

    private void OnFpsLeft()
    {
        settings.CycleFps(-1);
        if (null != fpsSelector) fpsSelector.UpdateValue(GetFpsText(settings.Current.fps));
    }

    private void OnFpsRight()
    {
        settings.CycleFps(1);
        if (null != fpsSelector) fpsSelector.UpdateValue(GetFpsText(settings.Current.fps));
    }

    private void OnPauseLeft()
    {
        settings.CyclePauseOnUnfocus(-1);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.UpdateValue(GetOnOffText(settings.Current.pauseOnUnfocus));
    }

    private void OnPauseRight()
    {
        settings.CyclePauseOnUnfocus(1);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.UpdateValue(GetOnOffText(settings.Current.pauseOnUnfocus));
    }

    private void OnCameraShakeChanged(float _val) { settings.SetCameraShake(_val); }
    private void OnCrosshairBrightnessChanged(float _val) { settings.SetCrosshairBrightness(_val); }
    private void OnChromaticAberrationChanged(float _val) { settings.SetChromaticAberration(_val); }
    private void OnBrightnessChanged(float _val) { settings.SetBrightness(_val); }
    private void OnSaturationChanged(float _val) { settings.SetSaturation(_val); }

    private void OnMasterVolumeChanged(float _val) { settings.SetMasterVolume(_val); }
    private void OnBgmVolumeChanged(float _val) { settings.SetBgmVolume(_val); }
    private void OnSfxVolumeChanged(float _val) { settings.SetSfxVolume(_val); }

    private void RefreshKeyBindRows()
    {
        if (null == inputManager) return;

        System.Collections.Generic.IReadOnlyList<ERebindableAction> _actions = inputManager.GetRebindableActions();
        for (int i = 0; i < keyBindRows.Count && i < _actions.Count; i++)
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

        // Duplicate 경고 (선택적 - 행 색상으로 이미 표시됨)
        // RefreshKeyBindRows는 KeyBindingsChangedEvent가 자동 호출하므로 별도 처리 불필요
    }

    private void OnRowResetRequested(ERebindableAction _action)
    {
        if (null == inputManager) return;
        inputManager.ResetBinding(_action);
    }

    private void OnResetAllClicked()
    {
        if (null == inputManager) return;
        inputManager.ResetAllBindings();
    }

    // 유니티 이벤트 함수
    private void OnDestroy()
    {
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

        cachedOnRowRebindRequested = null;
        cachedOnRowResetRequested = null;
        cachedOnResetAllClicked = null;
        cachedRefreshKeyBindRows = null;
    }
}
