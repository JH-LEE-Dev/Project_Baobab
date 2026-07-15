using UnityEngine;
using System;

public enum EOptionLanguage { Korean, English, Japanese, Chinese, Russian }
public enum EWindowMode { Windowed, Fullscreen }
public enum EOnOff { Off, On }
public enum EResolution { Res1280x720, Res1600x900, Res1920x1080, Res2560x1440 }
public enum EFPS { FPS60, FPS75, FPS120, FPS144, FPS165, FPS240, VSync, Unlimited }

/// <summary>
/// 옵션 UI 시스템 전체를 총괄하는 최상위 컨트롤러입니다.
/// 메인 메뉴나 ESC 메뉴에서 호출되어 옵션 창을 띄우고 닫는 역할을 합니다.
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

    // 내부 데이터 모델 (백엔드 연동 전까지 UI 상태를 임시 보관)
    private struct OptionDataModel
    {
        public EOptionLanguage language;
        public EResolution resolution;
        public EWindowMode windowMode;
        public EFPS fps;
        public EOnOff pauseOnUnfocus;

        public float cameraShake;
        public float crosshairBrightness;
        public float chromaticAberration;
        public float brightness;
        public float saturation;

        public float masterVolume;
        public float bgmVolume;
        public float sfxVolume;
    }

    private OptionDataModel currentOptions;

    // 언어 및 단순 숫자 데이터 캐싱 (로컬라이징 제외)
    private readonly string[] languageOptions = { "한국어", "English", "日本語", "中文", "Русский" };
    private readonly string[] resolutionOptions = { "1280x720", "1600x900", "1920x1080", "2560x1440" };
    private readonly string[] fpsNumberOptions = { "60", "75", "120", "144", "165", "240" };

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

    private bool isInitialized = false;
    private LocalizationManager locManager;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UIViewContext _ctx)
    {
        if (true == isInitialized) return;
        
        if (null != _ctx)
        {
            locManager = _ctx.localizationManager;
        }

        LoadMockData();
        CacheDelegates();

        if (null == hideAction) hideAction = Hide;

        if (null != tabGroup)
        {
            string[] _tabTexts = null;
            if (null != tabLocalizeKeys && tabLocalizeKeys.Length > 0)
            {
                _tabTexts = new string[tabLocalizeKeys.Length];
                for (int i = 0; i < tabLocalizeKeys.Length; i++)
                {
                    _tabTexts[i] = GetTextFromKeyString(tabLocalizeKeys[i], "Tab");
                }
            }
            tabGroup.Initialize(_tabTexts);
        }

        if (null != closeButton)
        {
            closeButton.Initialize(hideAction);
        }

        InitializeSelectors();
        InitializeSliders();

        ApplyWindowModeLogic();

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

        // 닫기 버튼을 누를 때 실제 설정 반영 및 백엔드 전송
        ApplySettings();
        SaveMockData();
    }

    // 초기화 관련 프라이빗 메서드
    private void LoadMockData()
    {
        currentOptions = new OptionDataModel
        {
            language = EOptionLanguage.Korean,
            resolution = EResolution.Res1920x1080,
            windowMode = EWindowMode.Windowed,
            fps = EFPS.Unlimited,
            pauseOnUnfocus = EOnOff.Off,

            cameraShake = 100f,
            crosshairBrightness = 100f,
            chromaticAberration = 100f,
            brightness = 100f,
            saturation = 100f,

            masterVolume = 100f,
            bgmVolume = 100f,
            sfxVolume = 100f
        };
    }

    private void SaveMockData()
    {
        // TODO: SaveSystem이나 Backend와 연동
        Debug.Log("Option Data Saved (Mock)");
    }

    private void ApplySettings()
    {
        // 1. 화면 해상도 및 창 모드 적용
        bool _isFullscreen = (EWindowMode.Fullscreen == currentOptions.windowMode);
        
        int _width = 1920;
        int _height = 1080;
        switch (currentOptions.resolution)
        {
            case EResolution.Res1280x720: _width = 1280; _height = 720; break;
            case EResolution.Res1600x900: _width = 1600; _height = 900; break;
            case EResolution.Res1920x1080: _width = 1920; _height = 1080; break;
            case EResolution.Res2560x1440: _width = 2560; _height = 1440; break;
        }

        if (true == _isFullscreen)
        {
            // 전체화면 시 현재 모니터 해상도로 덮어쓰기 (기획 의도에 따라 다를 수 있음)
            _width = Screen.currentResolution.width;
            _height = Screen.currentResolution.height;
        }
        
        Screen.SetResolution(_width, _height, _isFullscreen);

        // 언어 설정은 예외적으로 즉각 반영되므로 여기서는 생략합니다.

        // 3. FPS 제한 및 수직동기화(VSync) 적용
        if (EFPS.VSync == currentOptions.fps)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1; // VSync에 동기화
        }
        else
        {
            QualitySettings.vSyncCount = 0; // VSync 해제
            switch (currentOptions.fps)
            {
                case EFPS.FPS60: Application.targetFrameRate = 60; break;
                case EFPS.FPS75: Application.targetFrameRate = 75; break;
                case EFPS.FPS120: Application.targetFrameRate = 120; break;
                case EFPS.FPS144: Application.targetFrameRate = 144; break;
                case EFPS.FPS165: Application.targetFrameRate = 165; break;
                case EFPS.FPS240: Application.targetFrameRate = 240; break;
                case EFPS.Unlimited: Application.targetFrameRate = -1; break;
            }
        }

        // 4. 백그라운드 일시정지 옵션
        // Off일 경우 백그라운드에서도 게임이 계속 실행됨
        Application.runInBackground = (EOnOff.Off == currentOptions.pauseOnUnfocus);

        // 5. 볼륨 및 추가 그래픽 설정 (추후 연동 필요)
        // TODO: AudioMixer / PostProcessing 등 실제 적용 로직 추가 필요
    }

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

    private void InitializeSelectors()
    {
        if (null != languageSelector) languageSelector.Initialize(GetText(LocKeys.OptionUI.language, "언어"), GetLanguageText(currentOptions.language), onLanguageLeft, onLanguageRight);
        if (null != resolutionSelector) resolutionSelector.Initialize(GetText(LocKeys.OptionUI.resolution, "해상도"), GetResolutionText(currentOptions.resolution), onResolutionLeft, onResolutionRight);
        if (null != windowModeSelector) windowModeSelector.Initialize(GetText(LocKeys.OptionUI.windowMode, "화면"), GetWindowModeText(currentOptions.windowMode), onWindowModeLeft, onWindowModeRight);
        if (null != fpsSelector) fpsSelector.Initialize(GetText(LocKeys.OptionUI.fPS, "FPS"), GetFpsText(currentOptions.fps), onFpsLeft, onFpsRight);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.Initialize(GetText(LocKeys.OptionUI.pauseOnUnfocus, "비활성화 중 게임 일시정지"), GetOnOffText(currentOptions.pauseOnUnfocus), onPauseLeft, onPauseRight);
    }

    private void InitializeSliders()
    {
        if (null != cameraShakeSlider) cameraShakeSlider.Initialize(GetText(LocKeys.OptionUI.cameraShake, "카메라 흔들림"), currentOptions.cameraShake, 0f, 100f, onCameraShakeChanged);
        if (null != crosshairBrightnessSlider) crosshairBrightnessSlider.Initialize(GetText(LocKeys.OptionUI.crosshairBrightness, "캐릭터 조준 인디케이터 밝기"), currentOptions.crosshairBrightness, 0f, 100f, onCrosshairBrightnessChanged);
        if (null != chromaticAberrationSlider) chromaticAberrationSlider.Initialize(GetText(LocKeys.OptionUI.chromaticAberration, "색수차 효과"), currentOptions.chromaticAberration, 0f, 100f, onChromaticAberrationChanged);
        if (null != brightnessSlider) brightnessSlider.Initialize(GetText(LocKeys.OptionUI.screenBrightness, "화면 명도"), currentOptions.brightness, 0f, 100f, onBrightnessChanged);
        if (null != saturationSlider) saturationSlider.Initialize(GetText(LocKeys.OptionUI.screenSaturation, "화면 채도"), currentOptions.saturation, 0f, 100f, onSaturationChanged);
        
        if (null != masterVolumeSlider) masterVolumeSlider.Initialize(GetText(LocKeys.OptionUI.masterVolume, "마스터 볼륨"), currentOptions.masterVolume, 0f, 100f, onMasterVolumeChanged);
        if (null != bgmVolumeSlider) bgmVolumeSlider.Initialize(GetText(LocKeys.OptionUI.bGMVolume, "배경음악 볼륨"), currentOptions.bgmVolume, 0f, 100f, onBgmVolumeChanged);
        if (null != sfxVolumeSlider) sfxVolumeSlider.Initialize(GetText(LocKeys.OptionUI.sFXVolume, "사운드 볼륨"), currentOptions.sfxVolume, 0f, 100f, onSfxVolumeChanged);
    }

    // Enum 변환기
    private string GetLanguageText(EOptionLanguage _lang)
    {
        int _idx = (int)_lang;
        if (_idx >= 0 && _idx < languageOptions.Length) return languageOptions[_idx];
        return "Unknown";
    }

    private string GetResolutionText(EResolution _res)
    {
        int _idx = (int)_res;
        if (_idx >= 0 && _idx < resolutionOptions.Length) return resolutionOptions[_idx];
        return "Unknown";
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
        int _idx = (int)_fps;
        if (_idx < fpsNumberOptions.Length)
        {
            return fpsNumberOptions[_idx];
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

    // 로직 및 유틸리티
    private int IterateEnum(int _current, int _length, int _delta)
    {
        int _newVal = _current + _delta;
        if (_newVal < 0) return _length - 1;
        if (_newVal >= _length) return 0;
        return _newVal;
    }

    private void ApplyWindowModeLogic()
    {
        bool _isFullscreen = (EWindowMode.Fullscreen == currentOptions.windowMode);

        if (null != resolutionSelector)
        {
            resolutionSelector.SetInteractable(false == _isFullscreen);

            if (true == _isFullscreen)
            {
                // 전체화면일 때는 모니터 해상도로 강제 표기
                string _monitorRes = Screen.currentResolution.width + "x" + Screen.currentResolution.height;
                resolutionSelector.UpdateValue(_monitorRes);
            }
            else
            {
                // 창모드로 돌아오면 기존에 설정된 해상도 복구
                resolutionSelector.UpdateValue(GetResolutionText(currentOptions.resolution));
            }
        }
    }

    private void ApplyLanguageAndRefreshUI()
    {
        if (null != locManager)
        {
            Language _langToSet = (EOptionLanguage.Korean == currentOptions.language) ? Language.KR : Language.EN;
            locManager.SetLanguage(_langToSet);
        }

        // 언어 텍스트 재로드 후 UI 갱신
        InitializeSelectors();
        InitializeSliders();

        if (null != tabGroup && null != tabLocalizeKeys)
        {
            string[] _tabTexts = new string[tabLocalizeKeys.Length];
            for (int i = 0; i < tabLocalizeKeys.Length; i++)
            {
                _tabTexts[i] = GetTextFromKeyString(tabLocalizeKeys[i], "Tab");
            }
            tabGroup.RefreshTabTexts(_tabTexts);
        }

        OnLanguageOptionChangedEvent?.Invoke(currentOptions.language);
    }

    // 명시적 델리게이트 바인딩 메서드들 (GC 할당 방지)
    private void OnLanguageLeft() 
    { 
        currentOptions.language = (EOptionLanguage)IterateEnum((int)currentOptions.language, 2, -1);
        ApplyLanguageAndRefreshUI();
    }
    
    private void OnLanguageRight() 
    { 
        currentOptions.language = (EOptionLanguage)IterateEnum((int)currentOptions.language, 2, 1);
        ApplyLanguageAndRefreshUI();
    }

    private void OnResolutionLeft() 
    { 
        currentOptions.resolution = (EResolution)IterateEnum((int)currentOptions.resolution, 4, -1);
        if (null != resolutionSelector) resolutionSelector.UpdateValue(GetResolutionText(currentOptions.resolution));
    }
    
    private void OnResolutionRight() 
    { 
        currentOptions.resolution = (EResolution)IterateEnum((int)currentOptions.resolution, 4, 1);
        if (null != resolutionSelector) resolutionSelector.UpdateValue(GetResolutionText(currentOptions.resolution));
    }

    private void OnWindowModeLeft() 
    { 
        currentOptions.windowMode = (EWindowMode)IterateEnum((int)currentOptions.windowMode, 2, -1);
        if (null != windowModeSelector) windowModeSelector.UpdateValue(GetWindowModeText(currentOptions.windowMode));
        ApplyWindowModeLogic();
    }
    
    private void OnWindowModeRight() 
    { 
        currentOptions.windowMode = (EWindowMode)IterateEnum((int)currentOptions.windowMode, 2, 1);
        if (null != windowModeSelector) windowModeSelector.UpdateValue(GetWindowModeText(currentOptions.windowMode));
        ApplyWindowModeLogic();
    }

    private void OnFpsLeft() 
    { 
        currentOptions.fps = (EFPS)IterateEnum((int)currentOptions.fps, 8, -1);
        if (null != fpsSelector) fpsSelector.UpdateValue(GetFpsText(currentOptions.fps));
    }
    
    private void OnFpsRight() 
    { 
        currentOptions.fps = (EFPS)IterateEnum((int)currentOptions.fps, 8, 1);
        if (null != fpsSelector) fpsSelector.UpdateValue(GetFpsText(currentOptions.fps));
    }

    private void OnPauseLeft() 
    { 
        currentOptions.pauseOnUnfocus = (EOnOff)IterateEnum((int)currentOptions.pauseOnUnfocus, 2, -1);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.UpdateValue(GetOnOffText(currentOptions.pauseOnUnfocus));
    }
    
    private void OnPauseRight() 
    { 
        currentOptions.pauseOnUnfocus = (EOnOff)IterateEnum((int)currentOptions.pauseOnUnfocus, 2, 1);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.UpdateValue(GetOnOffText(currentOptions.pauseOnUnfocus));
    }

    private void OnCameraShakeChanged(float _val) { currentOptions.cameraShake = _val; }
    private void OnCrosshairBrightnessChanged(float _val) { currentOptions.crosshairBrightness = _val; }
    private void OnChromaticAberrationChanged(float _val) { currentOptions.chromaticAberration = _val; }
    private void OnBrightnessChanged(float _val) { currentOptions.brightness = _val; }
    private void OnSaturationChanged(float _val) { currentOptions.saturation = _val; }
    
    private void OnMasterVolumeChanged(float _val) { currentOptions.masterVolume = _val; }
    private void OnBgmVolumeChanged(float _val) { currentOptions.bgmVolume = _val; }
    private void OnSfxVolumeChanged(float _val) { currentOptions.sfxVolume = _val; }

    // 유니티 이벤트 함수
    private void OnDestroy()
    {
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
    }
}
