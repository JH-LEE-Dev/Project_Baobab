using UnityEngine;
using System;

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

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UIViewContext _ctx)
    {
        if (true == isInitialized) return;

        if (null != _ctx)
        {
            locManager = _ctx.localizationManager;
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

        InitializeSelectors();
        InitializeSliders();

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

    private string GetText(int _compositeKey, string _fallback)
    {
        if (null != locManager)
        {
            string _res = locManager.GetText(_compositeKey);
            if (false == string.IsNullOrEmpty(_res)) return _res;
        }
        return _fallback;
    }

    private string GetTextFromKeyString(string _keyName, string _fallback)
    {
        if (string.IsNullOrEmpty(_keyName)) return _fallback;

        try
        {
            var _fieldInfo = typeof(LocKeys.OptionUI).GetField(_keyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (null != _fieldInfo)
            {
                int _compositeKey = (int)_fieldInfo.GetValue(null);
                return GetText(_compositeKey, _fallback);
            }
        }
        catch (System.Exception _ex)
        {
            Debug.LogWarning($"[UI_Option] Failed to find key '{_keyName}' in LocKeys.OptionUI: {_ex.Message}");
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

        if (null != tabGroup)
        {
            string[] _tabTexts = BuildTabTexts();
            if (null != _tabTexts) tabGroup.RefreshTabTexts(_tabTexts);
        }

        OnLanguageOptionChangedEvent?.Invoke(_lang);
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
    }
}
