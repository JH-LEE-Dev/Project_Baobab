using UnityEngine;
using System;

/// <summary>
/// 옵션 UI 시스템 전체를 총괄하는 최상위 컨트롤러입니다.
/// 메인 메뉴나 ESC 메뉴에서 호출되어 옵션 창을 띄우고 닫는 역할을 합니다.
/// </summary>
public class UI_Option : MonoBehaviour
{
    // 외부 컴포넌트 참조
    [Header("Core System")]
    [SerializeField] private UI_OptionTabGroup tabGroup;
    [SerializeField] private GameObject optionPanelRoot;
    [SerializeField] private UI_OptionButton closeButton;

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
        public int languageIndex;
        public int resolutionIndex;
        public int windowModeIndex; // 0: Windowed, 1: Fullscreen
        public int fpsIndex;
        public int pauseOnUnfocusIndex; // 0: Off, 1: On

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

    // 상수 및 캐싱 데이터
    private readonly string[] languageOptions = { "한국어", "English", "日本語", "中文", "Русский" };
    private readonly string[] resolutionOptions = { "1280x720", "1600x900", "1920x1080", "2560x1440" };
    private readonly string[] windowModeOptions = { "창모드", "전체화면" };
    private readonly string[] fpsOptions = { "60", "75", "120", "144", "165", "240", "V Sync", "무제한" };
    private readonly string[] offOnOptions = { "Off", "On" };

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

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize()
    {
        if (true == isInitialized) return;

        LoadMockData();
        CacheDelegates();

        if (null == hideAction) hideAction = Hide;

        if (null != tabGroup)
        {
            tabGroup.Initialize();
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
        if (false == isInitialized)
        {
            Initialize();
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
        
        if (null != onCloseAction)
        {
            onCloseAction.Invoke();
            onCloseAction = null;
        }

        // 닫힐 때 설정값 저장(백엔드 전송) 로직 호출 가능
        SaveMockData();
    }

    // 초기화 관련 프라이빗 메서드
    private void LoadMockData()
    {
        currentOptions = new OptionDataModel
        {
            languageIndex = 0,
            resolutionIndex = 2,
            windowModeIndex = 0,
            fpsIndex = 7,
            pauseOnUnfocusIndex = 0,

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

    private void InitializeSelectors()
    {
        if (null != languageSelector) languageSelector.Initialize("언어", languageOptions[currentOptions.languageIndex], onLanguageLeft, onLanguageRight);
        if (null != resolutionSelector) resolutionSelector.Initialize("해상도", resolutionOptions[currentOptions.resolutionIndex], onResolutionLeft, onResolutionRight);
        if (null != windowModeSelector) windowModeSelector.Initialize("화면", windowModeOptions[currentOptions.windowModeIndex], onWindowModeLeft, onWindowModeRight);
        if (null != fpsSelector) fpsSelector.Initialize("FPS", fpsOptions[currentOptions.fpsIndex], onFpsLeft, onFpsRight);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.Initialize("비활성화 중 게임 일시정지", offOnOptions[currentOptions.pauseOnUnfocusIndex], onPauseLeft, onPauseRight);
    }

    private void InitializeSliders()
    {
        if (null != cameraShakeSlider) cameraShakeSlider.Initialize("카메라 흔들림", currentOptions.cameraShake, 0f, 100f, onCameraShakeChanged);
        if (null != crosshairBrightnessSlider) crosshairBrightnessSlider.Initialize("캐릭터 조준 인디케이터 밝기", currentOptions.crosshairBrightness, 0f, 100f, onCrosshairBrightnessChanged);
        if (null != chromaticAberrationSlider) chromaticAberrationSlider.Initialize("색수차 효과", currentOptions.chromaticAberration, 0f, 100f, onChromaticAberrationChanged);
        if (null != brightnessSlider) brightnessSlider.Initialize("화면 명도", currentOptions.brightness, 0f, 100f, onBrightnessChanged);
        if (null != saturationSlider) saturationSlider.Initialize("화면 채도", currentOptions.saturation, 0f, 100f, onSaturationChanged);
        
        if (null != masterVolumeSlider) masterVolumeSlider.Initialize("마스터 볼륨", currentOptions.masterVolume, 0f, 100f, onMasterVolumeChanged);
        if (null != bgmVolumeSlider) bgmVolumeSlider.Initialize("배경음악 볼륨", currentOptions.bgmVolume, 0f, 100f, onBgmVolumeChanged);
        if (null != sfxVolumeSlider) sfxVolumeSlider.Initialize("사운드 볼륨", currentOptions.sfxVolume, 0f, 100f, onSfxVolumeChanged);
    }

    // 로직 및 유틸리티
    private void ChangeSelectorIndex(ref int _currentIndex, int _arrayLength, int _delta, UI_OptionSelector _selector, string[] _options)
    {
        _currentIndex += _delta;

        if (_currentIndex < 0) _currentIndex = _arrayLength - 1;
        else if (_currentIndex >= _arrayLength) _currentIndex = 0;

        if (null != _selector)
        {
            _selector.UpdateValue(_options[_currentIndex]);
        }
    }

    private void ApplyWindowModeLogic()
    {
        // 1: 전체화면, 0: 창모드
        bool _isFullscreen = (1 == currentOptions.windowModeIndex);

        if (null != resolutionSelector)
        {
            resolutionSelector.SetInteractable(false == _isFullscreen);

            if (true == _isFullscreen)
            {
                // 전체화면일 때는 모니터 해상도로 강제 표기 (모의 로직)
                string _monitorRes = Screen.currentResolution.width + "x" + Screen.currentResolution.height;
                resolutionSelector.UpdateValue(_monitorRes);
            }
            else
            {
                // 창모드로 돌아오면 기존에 설정된 해상도 복구
                resolutionSelector.UpdateValue(resolutionOptions[currentOptions.resolutionIndex]);
            }
        }
    }

    // 명시적 델리게이트 바인딩 메서드들 (GC 할당 방지)
    private void OnLanguageLeft() { ChangeSelectorIndex(ref currentOptions.languageIndex, languageOptions.Length, -1, languageSelector, languageOptions); }
    private void OnLanguageRight() { ChangeSelectorIndex(ref currentOptions.languageIndex, languageOptions.Length, 1, languageSelector, languageOptions); }

    private void OnResolutionLeft() { ChangeSelectorIndex(ref currentOptions.resolutionIndex, resolutionOptions.Length, -1, resolutionSelector, resolutionOptions); }
    private void OnResolutionRight() { ChangeSelectorIndex(ref currentOptions.resolutionIndex, resolutionOptions.Length, 1, resolutionSelector, resolutionOptions); }

    private void OnWindowModeLeft() { ChangeSelectorIndex(ref currentOptions.windowModeIndex, windowModeOptions.Length, -1, windowModeSelector, windowModeOptions); ApplyWindowModeLogic(); }
    private void OnWindowModeRight() { ChangeSelectorIndex(ref currentOptions.windowModeIndex, windowModeOptions.Length, 1, windowModeSelector, windowModeOptions); ApplyWindowModeLogic(); }

    private void OnFpsLeft() { ChangeSelectorIndex(ref currentOptions.fpsIndex, fpsOptions.Length, -1, fpsSelector, fpsOptions); }
    private void OnFpsRight() { ChangeSelectorIndex(ref currentOptions.fpsIndex, fpsOptions.Length, 1, fpsSelector, fpsOptions); }

    private void OnPauseLeft() { ChangeSelectorIndex(ref currentOptions.pauseOnUnfocusIndex, offOnOptions.Length, -1, pauseOnUnfocusSelector, offOnOptions); }
    private void OnPauseRight() { ChangeSelectorIndex(ref currentOptions.pauseOnUnfocusIndex, offOnOptions.Length, 1, pauseOnUnfocusSelector, offOnOptions); }

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
