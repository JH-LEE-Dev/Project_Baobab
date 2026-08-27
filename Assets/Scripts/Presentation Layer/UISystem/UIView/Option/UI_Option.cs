using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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
    [SerializeField] private UI_OptionSelector gamepadIconPreferenceSelector;

    [SerializeField] private UI_OptionSlider cameraShakeSlider;
    [SerializeField] private UI_OptionSlider crosshairBrightnessSlider;
    [SerializeField] private UI_OptionSlider hapticStrengthSlider;
    [SerializeField] private UI_OptionSlider virtualCursorSensitivitySlider;
    [SerializeField] private UI_OptionSlider chromaticAberrationSlider;
    [SerializeField] private UI_OptionSlider brightnessSlider;
    [SerializeField] private UI_OptionSlider saturationSlider;

    [Header("Sound Options")]
    [SerializeField] private UI_OptionSlider masterVolumeSlider;
    [SerializeField] private UI_OptionSlider bgmVolumeSlider;
    [SerializeField] private UI_OptionSlider sfxVolumeSlider;

    [SerializeField, Tooltip("효과음 슬라이더를 조작할 때 들려줄 미리듣기 사운드. UI 그룹으로 우회 " +
        "재생되어(PlayUI bypassDucking) 일시정지 음소거 중에도 예외적으로 들린다. UiVolume이 효과음 " +
        "슬라이더 값과 동기화되어 있어(AudioManager.ApplyVolumeSettings) 조절한 볼륨은 그대로 반영된다.")]
    private SoundID sfxVolumePreviewSound = SoundID.OptionSFXBarTick;

    // 슬라이더는 소수점 단위로 값이 들어오지만, 미리듣기는 화면에 표시되는 정수(%) 눈금이
    // 바뀔 때만 울려야 "매 틱마다 재생"이라는 의도와 맞는다. 그 위에 0.03초 쿨타임을 안전장치로
    // 둬서, 아주 짧은 시간에 여러 눈금을 오가더라도 소리가 겹쳐 울리지 않게 한다.
    private const float SfxPreviewInterval = 0.03f;
    private float lastSfxPreviewTime = float.NegativeInfinity;
    private int lastSfxPreviewTick = int.MinValue;

    private const float HapticPreviewInterval = 0.05f;
    private float lastHapticPreviewTime = float.NegativeInfinity;
    private int lastHapticPreviewTick = int.MinValue;

    [Header("Control Options")]
    [SerializeField] private Transform keyBindRowContainer;           // 키보드/마우스 행 부모 Transform (KeyMo_Contents)
    [SerializeField] private Transform gamepadKeyBindRowContainer;    // 게임패드 행 부모 Transform (Pad_Contents)
    [SerializeField] private UI_OptionKeyBindRow keyBindRowPrefab;    // 키보드/마우스 행 프리팹
    [SerializeField] private UI_OptionGamepadKeyBindRow gamepadKeyBindRowPrefab; // 게임패드 행 프리팹
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
    private Action onGamepadIconPreferenceLeft;
    private Action onGamepadIconPreferenceRight;

    private Action<float> onCameraShakeChanged;
    private Action<float> onCrosshairBrightnessChanged;
    private Action<float> onHapticStrengthChanged;
    private Action<float> onVirtualCursorSensitivityChanged;
    private Action<float> onChromaticAberrationChanged;
    private Action<float> onBrightnessChanged;
    private Action<float> onSaturationChanged;
    private Action<float> onMasterVolumeChanged;
    private Action<float> onBgmVolumeChanged;
    private Action<float> onSfxVolumeChanged;

    private Action<bool> cachedOnGamepadConnectionChanged;
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
    private System.Collections.Generic.List<UI_OptionGamepadKeyBindRow> gamepadKeyBindRows 
        = new System.Collections.Generic.List<UI_OptionGamepadKeyBindRow>();
    private Action<ERebindableAction> cachedOnGamepadRowRebindRequested;
    private string[] cachedTabTexts;

    private readonly struct ApplyTargetSettingsSnapshot
    {
        public readonly EWindowMode windowMode;
        public readonly EResolution resolution;
        public readonly EFPS fps;
        public readonly EOnOff pauseOnUnfocus;
        public readonly float cameraShake;
        public readonly float crosshairBrightness;
        public readonly float hapticStrength;
        public readonly float virtualCursorSensitivity;
        public readonly EGamepadIconPreference gamepadIconPreference;

        public ApplyTargetSettingsSnapshot(in SettingsData _data)
        {
            windowMode = _data.windowMode;
            resolution = _data.resolution;
            fps = _data.fps;
            pauseOnUnfocus = _data.pauseOnUnfocus;
            cameraShake = _data.cameraShake;
            crosshairBrightness = _data.crosshairBrightness;
            hapticStrength = _data.hapticStrength;
            virtualCursorSensitivity = _data.virtualCursorSensitivity;
            gamepadIconPreference = _data.gamepadIconPreference;
        }

        public bool Equals(in SettingsData _current)
        {
            bool _windowModeMatches = (windowMode == _current.windowMode);
            bool _resolutionMatches = (EWindowMode.Fullscreen == windowMode && EWindowMode.Fullscreen == _current.windowMode)
                || (resolution == _current.resolution);

            return true == _windowModeMatches
                && true == _resolutionMatches
                && fps == _current.fps
                && pauseOnUnfocus == _current.pauseOnUnfocus
                && Mathf.Approximately(cameraShake, _current.cameraShake)
                && Mathf.Approximately(crosshairBrightness, _current.crosshairBrightness)
                && Mathf.Approximately(hapticStrength, _current.hapticStrength)
                && Mathf.Approximately(virtualCursorSensitivity, _current.virtualCursorSensitivity)
                && gamepadIconPreference == _current.gamepadIconPreference;
        }
    }

    private ApplyTargetSettingsSnapshot savedSnapshot;

    // 캐싱 델리게이트
    private Action cachedOnApplyClicked;
    private Action cachedConfirmDiscardAndClose;
    private Action cachedCancelDiscardAndClose;
    private Action<ERebindableAction> cachedOnRowRebindRequested;
    private Action cachedOnResetAllClicked;
    private Action cachedRefreshKeyBindRows;
    private Action cachedExecuteResetAll;
    private Action cachedCancelResetAll;
    private Action<int> cachedOnTabShift;
    private Action cachedOnUICancel;
    private Action<EInputDeviceType> cachedOnInputDeviceChanged;
    private Action<EGamepadIconSet> cachedOnGamepadIconSetChanged;

    private Coroutine rebindCoroutine;
    private ICursorBoxUI cursorBoxUI;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UIViewContext _ctx)
    {
        if (true == isInitialized) return;

        if (null != _ctx)
        {
            locManager = _ctx.localizationManager;
            inputManager = _ctx.inputManager;
            depthController = _ctx.depthController;
            cursorBoxUI = _ctx.cursorBoxUI;
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
            tabGroup.SetCursorBoxUI(cursorBoxUI, inputManager);
            tabGroup.OnTabChanged -= OnTabGroupChanged;
            tabGroup.OnTabChanged += OnTabGroupChanged;
        }

        if (null != applyButton)
        {
            applyButton.Initialize(cachedOnApplyClicked, SoundID.MainButtonHover, SoundID.MainClick);
            applyButton.SetCursorBoxUI(cursorBoxUI, inputManager);
        }

        if (null != closeButton)
        {
            closeButton.Initialize(hideAction, SoundID.MainButtonHover, SoundID.MainClick);
            closeButton.SetCursorBoxUI(cursorBoxUI, inputManager);
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

        // 옵션 창은 ESC 메뉴(일시정지)에서 열리는데, 그 상태에서는 덕킹(로우패스)으로 소리가 먹먹하다.
        // BGM 슬라이더를 원래 음색 기준으로 판단할 수 있도록 창이 열려 있는 동안만 덕킹을 푼다.
        // 일시정지 음소거(SFX/Ambience) 자체는 ESC와 동일하게 유지되며, 효과음 미리듣기만
        // PlaySfxVolumePreview에서 UI 그룹으로 우회 재생해 예외적으로 들리게 한다.
        Sound.SetAudioPreviewMode(true);

        if (null != optionPanelRoot)
        {
            optionPanelRoot.SetActive(true);
        }

        RefreshGamepadOptionsVisibility();
        RefreshControlTabVisibility();

        if (null != inputManager)
        {
            inputManager.BeginEditSession();
            inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshKeyBindRows;
            inputManager.inputReader.KeyBindingsChangedEvent += cachedRefreshKeyBindRows;
            inputManager.inputReader.GamepadConnectionChangedEvent -= cachedOnGamepadConnectionChanged;
            inputManager.inputReader.GamepadConnectionChangedEvent += cachedOnGamepadConnectionChanged;
            inputManager.inputReader.UITabShiftEvent -= cachedOnTabShift;
            inputManager.inputReader.UITabShiftEvent += cachedOnTabShift;
            inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
            inputManager.inputReader.UICancelEvent += cachedOnUICancel;
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
            inputManager.inputReader.InputDeviceChangedEvent += cachedOnInputDeviceChanged;
            inputManager.inputReader.GamepadIconSetChangedEvent -= cachedOnGamepadIconSetChanged;
            inputManager.inputReader.GamepadIconSetChangedEvent += cachedOnGamepadIconSetChanged;
            RefreshKeyBindRows();
        }

        SetupOptionNavigation();

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            SelectDefaultFocusElement();
        }

        UpdateApplyButtonState();
    }

    public void Hide()
    {
        // 키 바인딩 오버레이 또는 리바인딩 코루틴/오퍼레이션이 활성화되어 있으면 오버레이만 닫고 조기 반환 (옵션 패널 유지)
        if ((null != rebindOverlay && true == rebindOverlay.activeSelf)
            || null != rebindCoroutine
            || (null != inputManager && true == inputManager.IsRebinding))
        {
            CancelRebindOverlay();
            return;
        }

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
        cursorBoxUI?.HideImmediately();

        if (null != warningPopup && true == warningPopup.IsActive)
        {
            warningPopup.Hide();
        }

        RestoreSnapshot(savedSnapshot);

        // 창을 닫으면 원래의 덕킹 상태로 되돌린다(ESC 메뉴로 복귀하는 경우 등).
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
            inputManager.inputReader.GamepadConnectionChangedEvent -= cachedOnGamepadConnectionChanged;
            inputManager.inputReader.UITabShiftEvent -= cachedOnTabShift;
            inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
            inputManager.inputReader.GamepadIconSetChangedEvent -= cachedOnGamepadIconSetChanged;

            // 리바인딩 진행 중이면 취소
            if (null != rebindCoroutine)
            {
                StopCoroutine(rebindCoroutine);
                rebindCoroutine = null;
            }
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

        onGamepadIconPreferenceLeft = OnGamepadIconPreferenceLeft;
        onGamepadIconPreferenceRight = OnGamepadIconPreferenceRight;

        onCameraShakeChanged = OnCameraShakeChanged;
        onCrosshairBrightnessChanged = OnCrosshairBrightnessChanged;
        onHapticStrengthChanged = OnHapticStrengthChanged;
        onVirtualCursorSensitivityChanged = OnVirtualCursorSensitivityChanged;
        onChromaticAberrationChanged = OnChromaticAberrationChanged;
        onBrightnessChanged = OnBrightnessChanged;
        onSaturationChanged = OnSaturationChanged;

        onMasterVolumeChanged = OnMasterVolumeChanged;
        onBgmVolumeChanged = OnBgmVolumeChanged;
        onSfxVolumeChanged = OnSfxVolumeChanged;

        cachedOnGamepadConnectionChanged = OnGamepadConnectionChanged;
        onSettingsLanguageChanged = HandleLanguageChanged;
        onSettingsWindowModeChanged = HandleWindowModeChanged;
    }

    private void CacheControlDelegates()
    {
        cachedOnApplyClicked = OnApplyClicked;
        cachedConfirmDiscardAndClose = OnDiscardAndCloseConfirmed;
        cachedCancelDiscardAndClose = OnDiscardAndCloseCancelled;

        cachedOnRowRebindRequested = OnRowRebindRequested;
        cachedOnGamepadRowRebindRequested = OnGamepadRowRebindRequested;
        cachedOnResetAllClicked = OnResetAllClicked;
        cachedRefreshKeyBindRows = RefreshKeyBindRows;
        
        cachedExecuteResetAll = ExecuteResetAllBindings;
        cachedCancelResetAll = CancelResetAllBindings;

        cachedOnTabShift = HandleTabShift;
        cachedOnUICancel = HandleUICancel;
        cachedOnInputDeviceChanged = OnInputDeviceChanged;
        cachedOnGamepadIconSetChanged = OnGamepadIconSetChanged;
    }

    private void OnGamepadIconSetChanged(EGamepadIconSet _iconSet)
    {
        RefreshKeyBindRows();
    }

    private void OnInputDeviceChanged(EInputDeviceType _device)
    {
        RefreshGamepadOptionsVisibility();
        RefreshControlTabVisibility(EInputDeviceType.Gamepad == _device);
        SetupOptionNavigation();

        if (null != warningPopup && true == warningPopup.IsActive)
        {
            return;
        }

        if (EInputDeviceType.KeyboardMouse == _device)
        {
            cursorBoxUI?.HideImmediately();
            ResetAllRowsFocusVisuals();
        }
        else if (EInputDeviceType.Gamepad == _device)
        {
            GameObject _selected = EventSystem.current?.currentSelectedGameObject;
            bool _hasValidSelection = (null != _selected && true == _selected.activeInHierarchy && true == _selected.transform.IsChildOf(transform));

            if (false == _hasValidSelection)
            {
                SelectDefaultFocusElement();
            }
            else
            {
                UI_OptionSelector _selector = _selected.GetComponent<UI_OptionSelector>();
                if (null != _selector)
                {
                    _selector.ShowCursor();
                    _selector.ApplyFocusVisual(true);
                }
                else
                {
                    UI_OptionSlider _slider = _selected.GetComponent<UI_OptionSlider>();
                    if (null != _slider)
                    {
                        _slider.ShowCursor();
                        _slider.ApplyFocusVisual(true);
                    }
                    else
                    {
                        UI_OptionGamepadKeyBindRow _padRow = _selected.GetComponent<UI_OptionGamepadKeyBindRow>();
                        if (null != _padRow)
                        {
                            _padRow.ShowCursor();
                            _padRow.ApplyFocusVisual(true);
                        }
                        else
                        {
                            UI_OptionButton _btn = _selected.GetComponent<UI_OptionButton>();
                            if (null != _btn)
                            {
                                _btn.ShowCursor();
                            }
                            else
                            {
                                UI_OptionTabButton _tab = _selected.GetComponent<UI_OptionTabButton>();
                                if (null != _tab)
                                {
                                    _tab.ShowCursor();
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void ResetAllRowsFocusVisuals()
    {
        if (null != languageSelector) languageSelector.ApplyFocusVisual(false);
        if (null != resolutionSelector) resolutionSelector.ApplyFocusVisual(false);
        if (null != windowModeSelector) windowModeSelector.ApplyFocusVisual(false);
        if (null != fpsSelector) fpsSelector.ApplyFocusVisual(false);
        if (null != pauseOnUnfocusSelector) pauseOnUnfocusSelector.ApplyFocusVisual(false);
        if (null != gamepadIconPreferenceSelector) gamepadIconPreferenceSelector.ApplyFocusVisual(false);

        if (null != cameraShakeSlider) cameraShakeSlider.ApplyFocusVisual(false);
        if (null != crosshairBrightnessSlider) crosshairBrightnessSlider.ApplyFocusVisual(false);
        if (null != hapticStrengthSlider) hapticStrengthSlider.ApplyFocusVisual(false);
        if (null != virtualCursorSensitivitySlider) virtualCursorSensitivitySlider.ApplyFocusVisual(false);
        if (null != chromaticAberrationSlider) chromaticAberrationSlider.ApplyFocusVisual(false);
        if (null != brightnessSlider) brightnessSlider.ApplyFocusVisual(false);
        if (null != saturationSlider) saturationSlider.ApplyFocusVisual(false);

        if (null != masterVolumeSlider) masterVolumeSlider.ApplyFocusVisual(false);
        if (null != bgmVolumeSlider) bgmVolumeSlider.ApplyFocusVisual(false);
        if (null != sfxVolumeSlider) sfxVolumeSlider.ApplyFocusVisual(false);

        if (null != keyBindRows)
        {
            for (int i = 0; keyBindRows.Count > i; i++)
            {
                if (null != keyBindRows[i])
                {
                    keyBindRows[i].ApplyFocusVisual(false);
                }
            }
        }

        if (null != gamepadKeyBindRows)
        {
            for (int i = 0; gamepadKeyBindRows.Count > i; i++)
            {
                if (null != gamepadKeyBindRows[i])
                {
                    gamepadKeyBindRows[i].ApplyFocusVisual(false);
                }
            }
        }
    }

    private void SelectDefaultFocusElement()
    {
        if (null == tabGroup) return;

        int _curTab = tabGroup.CurrentTabIndex;
        Selectable _target = tabGroup.GetTabButton(_curTab);

        if (null != _target && true == _target.gameObject.activeInHierarchy)
        {
            if (null != EventSystem.current)
            {
                EventSystem.current.firstSelectedGameObject = _target.gameObject;
                EventSystem.current.SetSelectedGameObject(_target.gameObject);
            }
        }
    }


    private void HandleTabShift(int _direction)
    {
        if (false == gameObject.activeInHierarchy || false == IsActive) return;
        if (null != warningPopup && true == warningPopup.IsActive) return;
        if (null != tabGroup)
        {
            tabGroup.ShiftTab(_direction);
        }
    }

    private void HandleUICancel()
    {
        if (false == gameObject.activeInHierarchy || false == IsActive) return;

        // 키 바인딩 오버레이 또는 리바인딩 진행 중이면 오버레이만 닫고 조기 반환
        if ((null != rebindOverlay && true == rebindOverlay.activeSelf)
            || null != rebindCoroutine
            || (null != inputManager && true == inputManager.IsRebinding))
        {
            CancelRebindOverlay();
            return;
        }

        // 경고 팝업이 열려 있으면 팝업 닫기 처리
        if (null != warningPopup && true == warningPopup.IsActive)
        {
            warningPopup.Hide();
            return;
        }

        // 패드 모드이고 현재 포커스가 탭 버튼이 아닌 하위 옵션에 위치해 있다면 ➔ 상단 탭 버튼으로 포커스 복귀
        if (null != inputManager && true == inputManager.IsGamepadMode && null != EventSystem.current && null != tabGroup)
        {
            GameObject _selected = EventSystem.current.currentSelectedGameObject;
            UI_OptionTabButton _currentTabBtn = tabGroup.GetTabButton(tabGroup.CurrentTabIndex);
            if (null != _selected && (null == _currentTabBtn || _selected != _currentTabBtn.gameObject))
            {
                if (null != _currentTabBtn && true == _currentTabBtn.gameObject.activeInHierarchy)
                {
                    EventSystem.current.SetSelectedGameObject(_currentTabBtn.gameObject);
                    Sound.PlayUI(SoundID.MainButtonHover);
                    return;
                }
            }
        }

        Hide();
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

        if (null != languageSelector)
        {
            languageSelector.Initialize(GetText(LocKeys.OptionUI.language, "언어"), GetLanguageText(_data.language), onLanguageLeft, onLanguageRight);
            languageSelector.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != resolutionSelector)
        {
            resolutionSelector.Initialize(GetText(LocKeys.OptionUI.resolution, "해상도"), SettingsManager.GetResolutionLabel(settings.EffectiveResolution), onResolutionLeft, onResolutionRight);
            resolutionSelector.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != windowModeSelector)
        {
            windowModeSelector.Initialize(GetText(LocKeys.OptionUI.windowMode, "화면"), GetWindowModeText(_data.windowMode), onWindowModeLeft, onWindowModeRight);
            windowModeSelector.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != fpsSelector)
        {
            fpsSelector.Initialize(GetText(LocKeys.OptionUI.fPS, "FPS"), GetFpsText(_data.fps), onFpsLeft, onFpsRight);
            fpsSelector.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != pauseOnUnfocusSelector)
        {
            pauseOnUnfocusSelector.Initialize(GetText(LocKeys.OptionUI.pauseOnUnfocus, "비활성화 중 게임 일시정지"), GetOnOffText(_data.pauseOnUnfocus), onPauseLeft, onPauseRight);
            pauseOnUnfocusSelector.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != gamepadIconPreferenceSelector)
        {
            gamepadIconPreferenceSelector.Initialize(GetText(LocKeys.OptionUI.gamepadIconPreference, "게임패드 버튼 표기"), GetGamepadIconPreferenceText(_data.gamepadIconPreference), onGamepadIconPreferenceLeft, onGamepadIconPreferenceRight);
            gamepadIconPreferenceSelector.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
    }

    private void InitializeSliders()
    {
        SettingsData _data = settings.Current;

        if (null != cameraShakeSlider)
        {
            cameraShakeSlider.Initialize(GetText(LocKeys.OptionUI.cameraShake, "카메라 흔들림"), _data.cameraShake, 0f, 100f, onCameraShakeChanged);
            cameraShakeSlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != crosshairBrightnessSlider)
        {
            crosshairBrightnessSlider.Initialize(GetText(LocKeys.OptionUI.crosshairBrightness, "캐릭터 조준 인디케이터 밝기"), _data.crosshairBrightness, 0f, 100f, onCrosshairBrightnessChanged);
            crosshairBrightnessSlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != hapticStrengthSlider)
        {
            hapticStrengthSlider.Initialize(GetText(LocKeys.OptionUI.hapticStrength, "컨트롤러 진동"), _data.hapticStrength, 0f, 100f, onHapticStrengthChanged);
            hapticStrengthSlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != virtualCursorSensitivitySlider)
        {
            virtualCursorSensitivitySlider.Initialize(GetText(LocKeys.OptionUI.virtualCursorSensitivity, "가상 커서 감도"), _data.virtualCursorSensitivity, 0f, 100f, onVirtualCursorSensitivityChanged);
            virtualCursorSensitivitySlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != chromaticAberrationSlider)
        {
            chromaticAberrationSlider.Initialize(GetText(LocKeys.OptionUI.chromaticAberration, "색수차 효과"), _data.chromaticAberration, 0f, 100f, onChromaticAberrationChanged);
            chromaticAberrationSlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != brightnessSlider)
        {
            brightnessSlider.Initialize(GetText(LocKeys.OptionUI.screenBrightness, "화면 명도"), _data.brightness, 0f, 100f, onBrightnessChanged);
            brightnessSlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != saturationSlider)
        {
            saturationSlider.Initialize(GetText(LocKeys.OptionUI.screenSaturation, "화면 채도"), _data.saturation, 0f, 100f, onSaturationChanged);
            saturationSlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }

        if (null != masterVolumeSlider)
        {
            masterVolumeSlider.Initialize(GetText(LocKeys.OptionUI.masterVolume, "마스터 볼륨"), _data.masterVolume, 0f, 100f, onMasterVolumeChanged);
            masterVolumeSlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != bgmVolumeSlider)
        {
            bgmVolumeSlider.Initialize(GetText(LocKeys.OptionUI.bGMVolume, "배경음악 볼륨"), _data.bgmVolume, 0f, 100f, onBgmVolumeChanged);
            bgmVolumeSlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != sfxVolumeSlider)
        {
            sfxVolumeSlider.Initialize(GetText(LocKeys.OptionUI.sFXVolume, "사운드 볼륨"), _data.sfxVolume, 0f, 100f, onSfxVolumeChanged);
            sfxVolumeSlider.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
    }

    private void InitializeControlTab()
    {
        if (null == inputManager) return;

        System.Collections.Generic.IReadOnlyList<ERebindableAction> _actions = inputManager.GetRebindableActions();

        // 1) 키보드/마우스 행 초기화
        if (null != keyBindRowContainer && null != keyBindRowPrefab)
        {
            for (int i = 0; keyBindRows.Count > i; i++)
            {
                if (null != keyBindRows[i]) Destroy(keyBindRows[i].gameObject);
            }
            keyBindRows.Clear();

            UI_CustomScroll _customScroll = keyBindRowContainer.GetComponentInParent<UI_CustomScroll>();

            for (int i = 0; _actions.Count > i; i++)
            {
                ERebindableAction _action = _actions[i];
                UI_OptionKeyBindRow _row = Instantiate(keyBindRowPrefab, keyBindRowContainer);

                string _label = GetActionLabel(_action);
                string _bindingPath = inputManager.GetBindingPath(_action, EInputDeviceType.KeyboardMouse);
                string _displayString = inputManager.GetBindingDisplayString(_action, EInputDeviceType.KeyboardMouse);
                bool _isConflict = inputManager.IsConflicting(_action, EInputDeviceType.KeyboardMouse);

                _row.Initialize(_action, _label, _bindingPath, _displayString, _isConflict,
                                keyIconDatabase, cachedOnRowRebindRequested);
                _row.SetCursorBoxUI(cursorBoxUI, inputManager);
                _row.SetCustomScroll(_customScroll);
                keyBindRows.Add(_row);
            }
        }

        // 2) 게임패드 행 초기화
        if (null != gamepadKeyBindRowContainer && null != gamepadKeyBindRowPrefab)
        {
            for (int i = 0; gamepadKeyBindRows.Count > i; i++)
            {
                if (null != gamepadKeyBindRows[i]) Destroy(gamepadKeyBindRows[i].gameObject);
            }
            gamepadKeyBindRows.Clear();

            UI_CustomScroll _customScroll = gamepadKeyBindRowContainer.GetComponentInParent<UI_CustomScroll>();

            for (int i = 0; _actions.Count > i; i++)
            {
                ERebindableAction _action = _actions[i];
                UI_OptionGamepadKeyBindRow _padRow = Instantiate(gamepadKeyBindRowPrefab, gamepadKeyBindRowContainer);

                string _label = GetActionLabel(_action);
                string _bindingPath = inputManager.GetBindingPath(_action, EInputDeviceType.Gamepad);
                string _displayString = inputManager.GetBindingDisplayString(_action, EInputDeviceType.Gamepad);
                bool _isConflict = inputManager.IsConflicting(_action, EInputDeviceType.Gamepad);
                bool _isRebindable = GamepadDefaultBindings.IsRebindableOnGamepad(_action);

                EGamepadIconSet _iconSet = (null != inputManager) ? inputManager.CurrentGamepadIconSet : EGamepadIconSet.Xbox;
                _padRow.Initialize(_action, _label, _bindingPath, _displayString, _isConflict, _isRebindable,
                                   keyIconDatabase, cachedOnGamepadRowRebindRequested, _iconSet);
                _padRow.SetCursorBoxUI(cursorBoxUI, inputManager);
                _padRow.SetCustomScroll(_customScroll);
                gamepadKeyBindRows.Add(_padRow);
            }
        }

        // 전체 초기화 버튼
        if (null != resetAllBindingsButton)
        {
            resetAllBindingsButton.Initialize(cachedOnResetAllClicked);
            resetAllBindingsButton.SetCursorBoxUI(cursorBoxUI, inputManager);
        }

        // 오버레이 숨김
        if (null != rebindOverlay) rebindOverlay.SetActive(false);

        RefreshControlTabVisibility();
        SetupOptionNavigation();
    }

    private void RefreshControlTabVisibility(bool? _isGamepadOverride = null)
    {
        bool _isGamepad = _isGamepadOverride ?? (null != inputManager && true == inputManager.IsGamepadMode);

        if (null == keyBindRowContainer && null != tabGroup)
        {
            GameObject _controlPanel = tabGroup.GetTabPanel(3);
            if (null != _controlPanel)
            {
                Transform _km = _controlPanel.transform.Find("ViewRect/KeyMo_Contents");
                if (null != _km) keyBindRowContainer = _km;
            }
        }

        if (null == gamepadKeyBindRowContainer && null != tabGroup)
        {
            GameObject _controlPanel = tabGroup.GetTabPanel(3);
            if (null != _controlPanel)
            {
                Transform _pad = _controlPanel.transform.Find("ViewRect/Pad_Contents");
                if (null != _pad) gamepadKeyBindRowContainer = _pad;
            }
        }

        if (null != keyBindRowContainer)
        {
            keyBindRowContainer.gameObject.SetActive(false == _isGamepad);
        }

        if (null != gamepadKeyBindRowContainer)
        {
            gamepadKeyBindRowContainer.gameObject.SetActive(true == _isGamepad);
        }

        Transform _activeContainer = (true == _isGamepad) ? gamepadKeyBindRowContainer : keyBindRowContainer;
        if (null != _activeContainer)
        {
            UI_CustomScroll _customScroll = _activeContainer.GetComponentInParent<UI_CustomScroll>();
            if (null != _customScroll)
            {
                _customScroll.SetContent(_activeContainer as RectTransform);
            }
        }
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

    private string GetGamepadIconSetShortName(EGamepadIconSet _iconSet)
    {
        switch (_iconSet)
        {
            case EGamepadIconSet.Xbox: return "Xbox";
            case EGamepadIconSet.PlayStation: return "PS";
            case EGamepadIconSet.Generic: return GetText(LocKeys.OptionUI.gamepadIconGeneric, "Generic");
        }
        return _iconSet.ToString();
    }

    private string GetGamepadIconPreferenceText(EGamepadIconPreference _pref)
    {
        switch (_pref)
        {
            case EGamepadIconPreference.Auto:
                string _autoText = GetText(LocKeys.OptionUI.gamepadIconAuto, "자동");
                if (null != inputManager)
                {
                    return $"{_autoText} ({GetGamepadIconSetShortName(inputManager.DetectedGamepadIconSet)})";
                }
                return _autoText;
            case EGamepadIconPreference.Xbox: return "Xbox";
            case EGamepadIconPreference.PlayStation: return "PS";
            case EGamepadIconPreference.Generic: return GetText(LocKeys.OptionUI.gamepadIconGeneric, "Generic");
        }
        return _pref.ToString();
    }

    private void RefreshGamepadOptionsVisibility()
    {
        bool _isConnected = (null != inputManager && true == inputManager.IsGamepadConnected);

        if (null != gamepadIconPreferenceSelector)
        {
            gamepadIconPreferenceSelector.gameObject.SetActive(_isConnected);
        }

        if (null != hapticStrengthSlider)
        {
            hapticStrengthSlider.gameObject.SetActive(_isConnected);
        }

        if (null != virtualCursorSensitivitySlider)
        {
            virtualCursorSensitivitySlider.gameObject.SetActive(_isConnected);
        }

        SetupOptionNavigation();
    }

    private struct OptionRowNav
    {
        public Selectable left;
        public Selectable right;

        public OptionRowNav(Selectable _single)
        {
            left = _single;
            right = _single;
        }

        public OptionRowNav(Selectable _left, Selectable _right)
        {
            left = _left;
            right = _right;
        }

        public bool IsValid => null != left || null != right;
    }

    private void SetupOptionNavigation()
    {
        if (null == tabGroup) return;

        for (int t = 0; tabGroup.TabCount > t; t++)
        {
            SetupTabPanelNav(t);
        }

        SetupBottomButtonsNavigation();
    }

    private void SetupBottomButtonsNavigation()
    {
        if (null == tabGroup) return;

        int _curTab = tabGroup.CurrentTabIndex;
        List<OptionRowNav> _validRows = GetTabValidRows(_curTab);

        Selectable _elementAboveBottom = null;
        if (0 < _validRows.Count)
        {
            _elementAboveBottom = _validRows[_validRows.Count - 1].left ?? _validRows[_validRows.Count - 1].right;
        }
        else
        {
            _elementAboveBottom = tabGroup.GetTabButton(_curTab);
        }

        bool _isApplyValid = (null != applyButton && true == applyButton.gameObject.activeSelf && true == applyButton.IsInteractable);
        bool _isResetAllValid = (null != resetAllBindingsButton && true == resetAllBindingsButton.gameObject.activeSelf && true == resetAllBindingsButton.IsInteractable);
        bool _isCloseValid = (null != closeButton && true == closeButton.gameObject.activeSelf && true == closeButton.IsInteractable);

        Selectable _rightBottomButton = true == _isApplyValid ? (Selectable)applyButton : (true == _isResetAllValid ? (Selectable)resetAllBindingsButton : null);

        if (null != _rightBottomButton && true == _isCloseValid)
        {
            Selectable _aboveTarget = (_elementAboveBottom != _rightBottomButton)
                ? _elementAboveBottom
                : (1 < _validRows.Count ? (_validRows[_validRows.Count - 2].left ?? _validRows[_validRows.Count - 2].right) : tabGroup.GetTabButton(_curTab));

            Navigation _rightNav = new Navigation();
            _rightNav.mode = Navigation.Mode.Explicit;
            _rightNav.selectOnUp = _aboveTarget;
            _rightNav.selectOnDown = closeButton;
            _rightNav.selectOnLeft = closeButton;
            _rightNav.selectOnRight = _rightBottomButton;
            _rightBottomButton.navigation = _rightNav;

            Navigation _closeNav = new Navigation();
            _closeNav.mode = Navigation.Mode.Explicit;
            _closeNav.selectOnUp = _aboveTarget;
            _closeNav.selectOnDown = null;
            _closeNav.selectOnLeft = closeButton;
            _closeNav.selectOnRight = _rightBottomButton;
            closeButton.navigation = _closeNav;
        }
        else if (true == _isCloseValid)
        {
            Navigation _closeNav = new Navigation();
            _closeNav.mode = Navigation.Mode.Explicit;
            _closeNav.selectOnUp = _elementAboveBottom;
            _closeNav.selectOnDown = null;
            _closeNav.selectOnLeft = closeButton;
            _closeNav.selectOnRight = closeButton;
            closeButton.navigation = _closeNav;
        }
    }

    private void OnTabGroupChanged(int _index)
    {
        RefreshControlTabVisibility();
        SetupOptionNavigation();

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            GameObject _selected = EventSystem.current?.currentSelectedGameObject;
            bool _hasValidSelection = (null != _selected && true == _selected.activeInHierarchy && true == _selected.transform.IsChildOf(transform));
            if (false == _hasValidSelection)
            {
                SelectDefaultFocusElement();
            }
        }
    }

    private RectTransform GetContentRoot(GameObject _tabPanel)
    {
        if (null == _tabPanel) return null;

        Transform _gpContents = _tabPanel.transform.Find("ViewRect/GP_Contents");
        if (null != _gpContents) return _gpContents as RectTransform;

        Transform _contents = _tabPanel.transform.Find("ViewRect/Contents");
        if (null != _contents) return _contents as RectTransform;

        VerticalLayoutGroup _vlg = _tabPanel.GetComponentInChildren<VerticalLayoutGroup>(true);
        if (null != _vlg) return _vlg.transform as RectTransform;

        return _tabPanel.transform as RectTransform;
    }

    private List<OptionRowNav> GetTabValidRows(int _tabIndex)
    {
        List<OptionRowNav> _validRows = new List<OptionRowNav>();
        if (null == tabGroup) return _validRows;

        // 조작 탭(3)의 경우 키보드마우스 / 게임패드 행 목록을 분기하여 반환
        if (3 == _tabIndex)
        {
            if (null != inputManager && true == inputManager.IsGamepadMode)
            {
                for (int i = 0; gamepadKeyBindRows.Count > i; i++)
                {
                    UI_OptionGamepadKeyBindRow _padRow = gamepadKeyBindRows[i];
                    if (null != _padRow && true == _padRow.gameObject.activeSelf && true == _padRow.IsInteractable)
                    {
                        _validRows.Add(new OptionRowNav(_padRow));
                    }
                }
            }
            else
            {
                for (int i = 0; keyBindRows.Count > i; i++)
                {
                    UI_OptionKeyBindRow _keyRow = keyBindRows[i];
                    if (null == _keyRow || false == _keyRow.gameObject.activeSelf) continue;

                    if (true == _keyRow.IsInteractable)
                    {
                        _validRows.Add(new OptionRowNav(_keyRow));
                    }
                }
            }

            if (null != resetAllBindingsButton && true == resetAllBindingsButton.gameObject.activeSelf && true == resetAllBindingsButton.IsInteractable)
            {
                _validRows.Add(new OptionRowNav(resetAllBindingsButton));
            }

            return _validRows;
        }

        GameObject _panel = tabGroup.GetTabPanel(_tabIndex);
        if (null == _panel) return _validRows;

        RectTransform _contentRoot = GetContentRoot(_panel);
        if (null != _contentRoot)
        {
            for (int i = 0; _contentRoot.childCount > i; i++)
            {
                Transform _child = _contentRoot.GetChild(i);
                if (false == _child.gameObject.activeSelf) continue;

                // 1) UI_OptionSelector
                UI_OptionSelector _selector = _child.GetComponent<UI_OptionSelector>();
                if (null != _selector)
                {
                    if (true == _selector.IsInteractable)
                    {
                        _validRows.Add(new OptionRowNav(_selector));
                    }
                    continue;
                }

                // 2) UI_OptionSlider
                UI_OptionSlider _slider = _child.GetComponent<UI_OptionSlider>();
                if (null != _slider)
                {
                    if (true == _slider.IsInteractable)
                    {
                        _validRows.Add(new OptionRowNav(_slider));
                    }
                    continue;
                }

                // 3) UI_OptionKeyBindRow
                UI_OptionKeyBindRow _keyRow = _child.GetComponent<UI_OptionKeyBindRow>();
                if (null != _keyRow)
                {
                    if (true == _keyRow.IsInteractable)
                    {
                        _validRows.Add(new OptionRowNav(_keyRow));
                    }
                    continue;
                }

                // 4) UI_OptionGamepadKeyBindRow
                UI_OptionGamepadKeyBindRow _padRow = _child.GetComponent<UI_OptionGamepadKeyBindRow>();
                if (null != _padRow)
                {
                    if (true == _padRow.IsInteractable)
                    {
                        _validRows.Add(new OptionRowNav(_padRow));
                    }
                    continue;
                }

                // 5) UI_OptionButton
                UI_OptionButton _btn = _child.GetComponent<UI_OptionButton>();
                if (null != _btn)
                {
                    if (true == _btn.IsInteractable)
                    {
                        _validRows.Add(new OptionRowNav(_btn));
                    }
                    continue;
                }

                // 6) 기타 Selectable
                Selectable _sel = _child.GetComponent<Selectable>();
                if (null != _sel && true == _sel.interactable)
                {
                    _validRows.Add(new OptionRowNav(_sel));
                }
            }
        }

        return _validRows;
    }

    private void SetupTabPanelNav(int _tabIndex)
    {
        if (null == tabGroup) return;
        UI_OptionTabButton _tabBtn = tabGroup.GetTabButton(_tabIndex);
        if (null == _tabBtn) return;

        List<OptionRowNav> _validRows = GetTabValidRows(_tabIndex);
        ApplyRowsExplicitNavigation(_tabBtn, _validRows);
    }

    private void ApplyRowsExplicitNavigation(UI_OptionTabButton _tabBtn, List<OptionRowNav> _validRows)
    {
        if (null == _tabBtn) return;

        bool _isApplyValid = (null != applyButton && true == applyButton.gameObject.activeSelf && true == applyButton.IsInteractable);
        bool _isCloseValid = (null != closeButton && true == closeButton.gameObject.activeSelf && true == closeButton.IsInteractable);
        Selectable _bottomEntry = true == _isApplyValid ? (Selectable)applyButton : (true == _isCloseValid ? (Selectable)closeButton : null);

        if (null != _validRows && 0 < _validRows.Count)
        {
            Selectable _firstElement = _validRows[0].left ?? _validRows[0].right;
            Navigation _tabNav = _tabBtn.navigation;
            _tabNav.selectOnDown = _firstElement;
            _tabBtn.navigation = _tabNav;

            for (int i = 0; _validRows.Count > i; i++)
            {
                OptionRowNav _cur = _validRows[i];
                Selectable _prevLeft = (0 == i) ? (Selectable)_tabBtn : (_validRows[i - 1].left ?? _validRows[i - 1].right);
                Selectable _prevRight = (0 == i) ? (Selectable)_tabBtn : (_validRows[i - 1].right ?? _validRows[i - 1].left);
                Selectable _nextLeft = (_validRows.Count - 1 == i) ? _bottomEntry : (_validRows[i + 1].left ?? _validRows[i + 1].right);
                Selectable _nextRight = (_validRows.Count - 1 == i) ? _bottomEntry : (_validRows[i + 1].right ?? _validRows[i + 1].left);

                if (null != _cur.left && null != _cur.right && _cur.left != _cur.right)
                {
                    Navigation _leftNav = new Navigation();
                    _leftNav.mode = Navigation.Mode.Explicit;
                    _leftNav.selectOnLeft = _cur.left;
                    _leftNav.selectOnRight = _cur.right;
                    _leftNav.selectOnUp = _prevLeft;
                    _leftNav.selectOnDown = _nextLeft;
                    _cur.left.navigation = _leftNav;

                    Navigation _rightNav = new Navigation();
                    _rightNav.mode = Navigation.Mode.Explicit;
                    _rightNav.selectOnLeft = _cur.left;
                    _rightNav.selectOnRight = _cur.right;
                    _rightNav.selectOnUp = _prevRight;
                    _rightNav.selectOnDown = _nextRight;
                    _cur.right.navigation = _rightNav;
                }
                else
                {
                    Selectable _single = _cur.left ?? _cur.right;
                    if (null != _single)
                    {
                        Navigation _singleNav = new Navigation();
                        _singleNav.mode = Navigation.Mode.Explicit;

                        if (_single == resetAllBindingsButton || _single == applyButton)
                        {
                            _singleNav.selectOnLeft = true == _isCloseValid ? closeButton : _single;
                            _singleNav.selectOnRight = _single;
                        }
                        else
                        {
                            _singleNav.selectOnLeft = _single;
                            _singleNav.selectOnRight = _single;
                        }

                        _singleNav.selectOnUp = _prevLeft;
                        _singleNav.selectOnDown = _nextLeft;
                        _single.navigation = _singleNav;
                    }
                }
            }
        }
        else
        {
            Navigation _tabNav = _tabBtn.navigation;
            _tabNav.selectOnDown = _bottomEntry;
            _tabBtn.navigation = _tabNav;
        }
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

        for (int i = 0; gamepadKeyBindRows.Count > i && _actions.Count > i; i++)
        {
            if (null == gamepadKeyBindRows[i]) continue;
            gamepadKeyBindRows[i].RefreshLabel(GetActionLabel(_actions[i]));
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

        SetupOptionNavigation();
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

    private void OnGamepadIconPreferenceLeft()
    {
        settings.CycleGamepadIconPreference(-1);
        if (null != gamepadIconPreferenceSelector)
        {
            gamepadIconPreferenceSelector.UpdateValue(GetGamepadIconPreferenceText(settings.Current.gamepadIconPreference));
        }
        RefreshKeyBindRows();
        UpdateApplyButtonState();
    }

    private void OnGamepadIconPreferenceRight()
    {
        settings.CycleGamepadIconPreference(1);
        if (null != gamepadIconPreferenceSelector)
        {
            gamepadIconPreferenceSelector.UpdateValue(GetGamepadIconPreferenceText(settings.Current.gamepadIconPreference));
        }
        RefreshKeyBindRows();
        UpdateApplyButtonState();
    }

    private void OnHapticStrengthChanged(float _val)
    {
        settings.SetHapticStrength(_val);
        PlayHapticPreview(_val);
        UpdateApplyButtonState();
    }

    private void OnVirtualCursorSensitivityChanged(float _val)
    {
        settings.SetVirtualCursorSensitivity(_val);
        UpdateApplyButtonState();
    }

    private void OnGamepadConnectionChanged(bool _isConnected)
    {
        RefreshGamepadOptionsVisibility();
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
    // 몇 %인지 귀로 알 수 없다. 그래서 화면에 표시되는 정수(%) 눈금이 바뀔 때마다 짧게 재생해
    // 유저가 바로 체감하게 한다. 옵션 창이 열려 있는 동안에도 ESC와 동일하게 일시정지 음소거가
    // 유지되므로(SetAudioPreviewMode 참고), 이 미리듣기만은 예외로 들려야 해서 UI 그룹으로 우회
    // 재생한다(bypassDucking: true) - 그 외 게임플레이 SFX/Ambience는 계속 음소거된 채로 남는다.
    private void PlaySfxVolumePreview(float _val)
    {
        int _tick = Mathf.RoundToInt(_val);
        if (_tick == lastSfxPreviewTick) return;

        float _now = Time.unscaledTime;
        if (SfxPreviewInterval > _now - lastSfxPreviewTime) return;

        lastSfxPreviewTick = _tick;
        lastSfxPreviewTime = _now;
        Sound.PlayUI(sfxVolumePreviewSound, bypassDucking: true);
    }

    private void PlayHapticPreview(float _val)
    {
        if (null == inputManager || false == inputManager.IsGamepadConnected) return;

        int _tick = Mathf.RoundToInt(_val);
        if (_tick == lastHapticPreviewTick) return;

        float _now = Time.unscaledTime;
        if (HapticPreviewInterval > _now - lastHapticPreviewTime) return;

        lastHapticPreviewTick = _tick;
        lastHapticPreviewTime = _now;

        if (0f < _val)
        {
            inputManager.Haptics.Play(0.8f, 0.4f, 0.12f);
        }
        else
        {
            inputManager.Haptics.Stop();
        }
    }

    private void RefreshKeyBindRows()
    {
        if (null == inputManager) return;

        System.Collections.Generic.IReadOnlyList<ERebindableAction> _actions = inputManager.GetRebindableActions();

        // 1) 키보드/마우스 행 갱신
        for (int i = 0; keyBindRows.Count > i && _actions.Count > i; i++)
        {
            if (null == keyBindRows[i]) continue;

            string _bindingPath = inputManager.GetBindingPath(_actions[i], EInputDeviceType.KeyboardMouse);
            string _displayString = inputManager.GetBindingDisplayString(_actions[i], EInputDeviceType.KeyboardMouse);
            bool _isConflict = inputManager.IsConflicting(_actions[i], EInputDeviceType.KeyboardMouse);
            keyBindRows[i].Refresh(_bindingPath, _displayString, _isConflict);
        }

        // 2) 게임패드 행 갱신
        for (int i = 0; gamepadKeyBindRows.Count > i && _actions.Count > i; i++)
        {
            if (null == gamepadKeyBindRows[i]) continue;

            string _bindingPath = inputManager.GetBindingPath(_actions[i], EInputDeviceType.Gamepad);
            string _displayString = inputManager.GetBindingDisplayString(_actions[i], EInputDeviceType.Gamepad);
            bool _isConflict = inputManager.IsConflicting(_actions[i], EInputDeviceType.Gamepad);
            bool _isRebindable = GamepadDefaultBindings.IsRebindableOnGamepad(_actions[i]);
            EGamepadIconSet _iconSet = (null != inputManager) ? inputManager.CurrentGamepadIconSet : EGamepadIconSet.Xbox;
            gamepadKeyBindRows[i].Refresh(_bindingPath, _displayString, _isConflict, _isRebindable, _iconSet);
        }

        SetupOptionNavigation();
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

        inputManager.StartRebind(_action, EInputDeviceType.KeyboardMouse, OnRebindFinished);
    }

    private void OnGamepadRowRebindRequested(ERebindableAction _action)
    {
        if (null == inputManager || true == inputManager.IsRebinding) return;
        if (false == GamepadDefaultBindings.IsRebindableOnGamepad(_action)) return;

        // 오버레이 표시
        if (null != rebindOverlay) rebindOverlay.SetActive(true);
        if (null != rebindOverlayText)
        {
            rebindOverlayText.text = GetText(LocKeys.OptionUI.pressGamepadPrompt, "변경할 패드 버튼을 입력하세요.");
        }

        if (null != rebindCoroutine)
        {
            StopCoroutine(rebindCoroutine);
        }
        rebindCoroutine = StartCoroutine(CoStartGamepadRebind(_action));
    }

    private IEnumerator CoStartGamepadRebind(ERebindableAction _action)
    {
        // 1) 행 선택을 위해 누른 Submit 버튼(A 버튼 / South)이 완전히 떼어질 때까지 대기
        while (null != UnityEngine.InputSystem.Gamepad.current && true == UnityEngine.InputSystem.Gamepad.current.buttonSouth.isPressed)
        {
            yield return null;
        }

        // 2) 바운스 방지용 추가 2프레임 대기 (Input System 내부 버퍼 잔여 이벤트 플러시)
        yield return null;
        yield return null;

        if (null != inputManager)
        {
            inputManager.StartRebind(_action, EInputDeviceType.Gamepad, OnRebindFinished);
        }
    }

    private void OnRebindFinished(ERebindResult _result, ERebindableAction? _conflict)
    {
        if (null != rebindCoroutine)
        {
            StopCoroutine(rebindCoroutine);
            rebindCoroutine = null;
        }

        // 오버레이 숨김
        if (null != rebindOverlay) rebindOverlay.SetActive(false);

        if (null != inputManager && false == inputManager.HasAnyConflict())
        {
            inputManager.CommitEditSession();
        }
    }

    private void CancelRebindOverlay()
    {
        if (null != rebindCoroutine)
        {
            StopCoroutine(rebindCoroutine);
            rebindCoroutine = null;
        }

        if (null != inputManager && true == inputManager.IsRebinding)
        {
            inputManager.CancelRebind();
        }

        if (null != rebindOverlay)
        {
            rebindOverlay.SetActive(false);
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
        bool _wasFocusedOnApply = (null != EventSystem.current && 
            (EventSystem.current.currentSelectedGameObject == applyButton.gameObject ||
             null == EventSystem.current.currentSelectedGameObject));

        applyButton.SetInteractable(_dirty);
        SetupOptionNavigation();

        if (false == _dirty && true == _wasFocusedOnApply)
        {
            if (null != closeButton && true == closeButton.gameObject.activeInHierarchy && true == closeButton.IsInteractable)
            {
                if (null != EventSystem.current)
                {
                    EventSystem.current.SetSelectedGameObject(closeButton.gameObject);
                }
                if (null != inputManager && true == inputManager.IsGamepadMode)
                {
                    closeButton.ShowCursor();
                }
            }
            else
            {
                SelectDefaultFocusElement();
            }
        }
    }

    private void RestoreSnapshot(in ApplyTargetSettingsSnapshot _snapshot)
    {
        if (null == settings) return;

        settings.SetCameraShake(_snapshot.cameraShake);
        settings.SetCrosshairBrightness(_snapshot.crosshairBrightness);
        settings.SetHapticStrength(_snapshot.hapticStrength);
        settings.SetVirtualCursorSensitivity(_snapshot.virtualCursorSensitivity);
        settings.SetGamepadIconPreference(_snapshot.gamepadIconPreference);
        if (null != gamepadIconPreferenceSelector)
        {
            gamepadIconPreferenceSelector.UpdateValue(GetGamepadIconPreferenceText(_snapshot.gamepadIconPreference));
        }

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

        if (null != tabGroup)
        {
            tabGroup.OnTabChanged -= OnTabGroupChanged;
        }

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
        onGamepadIconPreferenceLeft = null; onGamepadIconPreferenceRight = null;

        onCameraShakeChanged = null;
        onCrosshairBrightnessChanged = null;
        onHapticStrengthChanged = null;
        onVirtualCursorSensitivityChanged = null;
        onChromaticAberrationChanged = null;
        onBrightnessChanged = null;
        onSaturationChanged = null;
        onMasterVolumeChanged = null;
        onBgmVolumeChanged = null;
        onSfxVolumeChanged = null;

        cachedOnGamepadConnectionChanged = null;
        onSettingsLanguageChanged = null;
        onSettingsWindowModeChanged = null;

        if (null != inputManager)
        {
            inputManager.inputReader.KeyBindingsChangedEvent -= cachedRefreshKeyBindRows;
            inputManager.inputReader.GamepadConnectionChangedEvent -= cachedOnGamepadConnectionChanged;
            inputManager.inputReader.UITabShiftEvent -= cachedOnTabShift;
            inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
            inputManager.inputReader.GamepadIconSetChangedEvent -= cachedOnGamepadIconSetChanged;
        }

        cachedOnApplyClicked = null;
        cachedConfirmDiscardAndClose = null;
        cachedCancelDiscardAndClose = null;

        cachedOnRowRebindRequested = null;
        cachedOnGamepadRowRebindRequested = null;
        cachedOnResetAllClicked = null;
        cachedRefreshKeyBindRows = null;
        cachedExecuteResetAll = null;
        cachedCancelResetAll = null;
        cachedOnTabShift = null;
        cachedOnUICancel = null;
        cachedOnInputDeviceChanged = null;

        keyBindRows.Clear();
        gamepadKeyBindRows.Clear();

        if (null != rebindCoroutine)
        {
            StopCoroutine(rebindCoroutine);
            rebindCoroutine = null;
        }

        cursorBoxUI?.HideImmediately();
        cursorBoxUI = null;
    }
}
