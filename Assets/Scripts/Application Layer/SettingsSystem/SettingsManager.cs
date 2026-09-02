using System;
using UnityEngine;

/// <summary>
/// 환경설정 값의 소유·적용·저장을 총괄하는 싱글턴입니다.
/// UI(UI_Option)는 이 매니저의 값을 읽어 표시하고, 변경 요청만 위임합니다.
/// 씬에 배치할 필요 없이 최초 접근 시 자동으로 생성됩니다.
/// </summary>
public class SettingsManager : MonoBehaviour
{
    private static SettingsManager instance;

    public static SettingsManager Instance
    {
        get
        {
            if (null == instance)
            {
                GameObject _go = new GameObject("[SettingsManager]");
                instance = _go.AddComponent<SettingsManager>();
            }
            return instance;
        }
    }

    /// <summary>
    /// 인스턴스를 새로 생성하지 않고 존재 여부만 확인합니다.
    /// 씬 종료(OnDisable/OnDestroy) 중에는 Instance 게터를 호출하면 이미 파괴된 싱글턴이
    /// 그 시점에 새로 생성되어 정리되지 못한 채 남는 문제가 있어, 그런 경로에서는 이걸 써야 합니다.
    /// </summary>
    public static bool HasInstance => null != instance;

    /// <summary>언어가 실제로 변경되어 로컬라이징이 갱신된 뒤 발생합니다.</summary>
    public event Action<EOptionLanguage> OnLanguageChangedEvent;

    /// <summary>해상도 선택 가능 여부에 영향을 주는 창 모드가 바뀌었을 때 발생합니다.</summary>
    public event Action<EWindowMode> OnWindowModeChangedEvent;

    /// <summary>
    /// 볼륨 설정이 적용될 때 발생합니다. AudioManager 등이 구독해 자기 몫만 반영합니다.
    /// (SettingsManager가 하위 시스템을 직접 알지 않도록 의존성 방향을 뒤집기 위한 것)
    /// </summary>
    public event Action<SettingsData> OnAudioSettingsAppliedEvent;

    /// <summary>포스트프로세싱·카메라 등 그래픽 관련 설정이 적용될 때 발생합니다.</summary>
    public event Action<SettingsData> OnGraphicsSettingsAppliedEvent;

    /// <summary>입력 관련 설정(패드 아이콘 표기)을 하위 시스템이 가져가도록 알립니다. InputManager가 구독합니다.</summary>
    public event Action<SettingsData> OnInputSettingsAppliedEvent;

    /// <summary>
    /// 실제 화면 크기(_width, _height)가 정해질 때(부팅 시 포함) 발생합니다.
    /// OnGraphicsSettingsAppliedEvent와 달리 Bootstrap에서도 발생하므로,
    /// PixelPerfectCamera의 기준 해상도처럼 게임 시작부터 맞아 있어야 하는
    /// 화면 크기 의존 로직은 이 이벤트를 구독해야 합니다.
    /// </summary>
    public event Action<int, int> OnScreenTargetResolvedEvent;

    // 언어 이름 표기는 여기에 두지 않는다. OptionUI.json의 LanguageKorean~LanguageJapanese 항목에
    // 있고 UI_Option.GetLanguageText가 읽는다. (창모드·On/Off 표기와 같은 방식)
    // 코드에 문자열로 박아두면 로컬라이징 문자셋 생성기가 그 글자를 수집하지 못해,
    // 정적 아틀라스로 구운 CJK 폰트에서 언어 이름이 통째로 깨진다. 실제로 그런 상태였다.
    //
    // 언어를 추가하려면 다음을 함께 손봐야 한다:
    //   1) SettingsData.SUPPORTED_LANGUAGE_COUNT
    //   2) ApplyLanguageToLocalization의 Language 매핑 (매핑되지 않은 항목은 모두 EN이 된다)
    //   3) LocalizationManager가 읽는 로컬라이징 데이터 (OptionUI.json의 언어 이름 항목 포함)
    //   4) UI_Option.GetLanguageText의 분기
    //   5) LocalizationFontTable의 해당 언어 폰트
    //   6) LanguageAutoDetect의 매핑 두 곳 (빠뜨리면 그 언어권 유저가 첫 실행에 영어로 시작한다)

    // 표기 문자열은 SettingsData의 해상도 목록에서 파생해 1회만 생성한다.
    // (손으로 관리하면 크기와 표기가 어긋날 수 있고, 컴파일러가 잡아주지 못한다)
    private static readonly string[] resolutionLabels = BuildResolutionLabels();

    private static string[] BuildResolutionLabels()
    {
        string[] _labels = new string[SettingsData.ResolutionCount];
        for (int i = 0; i < _labels.Length; i++)
        {
            SettingsData.GetResolutionSize((EResolution)i, out int _width, out int _height);
            _labels[i] = _width + "x" + _height;
        }
        return _labels;
    }

    // 숫자로 표기되는 FPS는 이 배열 하나만 관리한다. (EFPS 선언 순서와 동일)
    private static readonly int[] fpsValues = { 60, 75, 120, 144, 165, 240 };

    // 표기 문자열은 값에서 파생해 1회만 생성한다. (매번 ToString하면 GC 할당이 발생)
    private static readonly string[] fpsNumberLabels = BuildFpsNumberLabels();

    private static string[] BuildFpsNumberLabels()
    {
        string[] _labels = new string[fpsValues.Length];
        for (int i = 0; i < fpsValues.Length; i++)
        {
            _labels[i] = fpsValues[i].ToString();
        }
        return _labels;
    }

    private SettingsData current = SettingsData.CreateDefault();
    private LocalizationManager locManager;
    private bool isLoaded = false;

    // 유저가 실제로 값을 바꿨을 때만 파일에 기록한다.
    // 이 플래그가 없으면 옵션 창을 한 번도 열지 않고 종료해도 기본값이 저장되어,
    // 다음 실행부터 Player Settings의 시작 해상도가 덮어써진다.
    private bool isDirty = false;

    // 화면(해상도·창모드·FPS) 항목이 바뀌었는지를 따로 추적한다.
    // isDirty는 "무언가 바뀜"일 뿐이라, 이걸 구분하지 않으면 볼륨만 조정하고 창을 닫아도
    // 해상도가 다시 적용되어 창 크기가 튄다.
    private bool isDisplayDirty = false;

    /// <summary>현재 설정값 스냅샷입니다. (구조체이므로 복사본이 반환됩니다)</summary>
    public SettingsData Current
    {
        get
        {
            EnsureLoaded();
            return current;
        }
    }

    // 초기화
    private void Awake()
    {
        if (null != instance && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 설정을 읽어 화면 관련 항목을 게임 시작 시 반영합니다.
    /// 언어·볼륨은 의존 시스템이 준비된 뒤 Bind에서 처리합니다.
    ///
    /// AfterSceneLoad를 쓰는 이유: BeforeSceneLoad 시점에는 씬이 존재하지 않아
    /// 그때 만든 GameObject의 DontDestroyOnLoad 보호가 보장되지 않습니다.
    /// (첫 씬 로드에서 파괴되면 설정이 조용히 매번 초기화됩니다)
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SettingsManager _mgr = Instance;
        _mgr.EnsureLoaded();

        // 저장된 설정이 없어도(첫 실행) SettingsData.CreateDefault()의 값(60fps/전체화면 등)을
        // 그대로 적용한다. Player Settings의 기본값보다 이 기본값을 우선한다.
        _mgr.ApplyDisplaySettings();
    }

    /// <summary>
    /// 설정 파일을 아직 읽지 않았다면 지금 읽습니다.
    /// Bootstrap(AfterSceneLoad)은 첫 씬의 Awake 이후에 실행되므로, Awake 체인에서
    /// 초기화되는 UI가 먼저 값을 조회하면 기본값을 읽어가는 문제가 생깁니다.
    /// 값에 접근하는 모든 경로에서 이 메서드를 먼저 호출해 로드 순서를 보장합니다.
    /// </summary>
    private void EnsureLoaded()
    {
        if (true == isLoaded) return;

        // Load 안에서 Current를 다시 타더라도 무한 재귀에 빠지지 않도록 먼저 세운다.
        isLoaded = true;
        Load();
    }

    /// <summary>
    /// LocalizationManager를 주입하고, 로드된 언어 설정을 실제로 반영합니다.
    /// 씬마다 UI가 새로 생성되므로 여러 번 호출될 수 있습니다.
    /// </summary>
    public void Bind(LocalizationManager _locManager)
    {
        // 이미 유효한 참조를 null로 덮어쓰지 않는다.
        // (컨텍스트가 불완전한 씬에서 Bind(null)이 불리면 이후 언어 변경이 조용히 먹통이 된다)
        if (null == _locManager) return;

        EnsureLoaded();
        locManager = _locManager;

        // 부팅 시점에는 LocalizationManager가 없어 미뤄둔 언어 적용을 여기서 수행한다.
        // 값이 바뀐 게 아니라 반영만 하는 것이므로 변경 이벤트는 발행하지 않는다.
        ApplyLanguageToLocalization();
    }

    // 값 변경 (UI의 좌/우 화살표에 대응)
    // 값을 바꾸는 모든 경로는 먼저 EnsureLoaded를 거친다.
    // 로드 전에 current를 수정하면 뒤늦은 Load가 그 변경을 파일 내용으로 덮어써 버린다.
    public EOptionLanguage CurrentLanguage
    {
        get
        {
            EnsureLoaded();
            return current.language;
        }
    }

    public void SetLanguage(EOptionLanguage _lang)
    {
        EnsureLoaded();
        if (current.language == _lang) return;

        current.language = _lang;
        isDirty = true;
        ApplyLanguage();
    }

    public void CycleLanguage(int _delta)
    {
        EnsureLoaded();

        EOptionLanguage _next = (EOptionLanguage)IterateEnum((int)current.language, SettingsData.SUPPORTED_LANGUAGE_COUNT, _delta);
        if (_next == current.language) return;

        current.language = _next;
        isDirty = true;
        ApplyLanguage();
    }

    /// <summary>
    /// 현재 모니터에서 실제로 적용되는 해상도입니다.
    /// UI 표시·선택기 순환·화면 적용이 모두 이 값을 기준으로 동작해야
    /// "보이는 값"과 "동작하는 값"이 어긋나지 않습니다.
    /// </summary>
    public EResolution EffectiveResolution
    {
        get
        {
            EnsureLoaded();
            DisplayUtil.GetMainDisplaySize(out int _maxWidth, out int _maxHeight);
            return SettingsData.ClampResolution(current.resolution, _maxWidth, _maxHeight);
        }
    }

    /// <summary>
    /// 현재 모니터에서 표시 가능한 해상도만 순환합니다.
    /// 시작점은 저장값이 아니라 실제 적용값(EffectiveResolution)이어야
    /// 유저가 화면에서 보고 있는 항목의 다음/이전으로 이동합니다.
    /// </summary>
    public void CycleResolution(int _delta)
    {
        EnsureLoaded();

        // 디스플레이 조회는 루프 밖에서 한 번만 수행한다.
        DisplayUtil.GetMainDisplaySize(out int _maxWidth, out int _maxHeight);

        // enum 인덱스가 아니라 표시 순서를 순환한다. 유저가 화면에서 보는 나열 순서와
        // 좌우 이동 순서가 같아야 하기 때문이다. (SettingsData.displayOrder)
        EResolution _effective = SettingsData.ClampResolution(current.resolution, _maxWidth, _maxHeight);
        int _order = SettingsData.GetDisplayOrderIndex(_effective);

        for (int i = 0; i < SettingsData.ResolutionCount; i++)
        {
            _order = IterateEnum(_order, SettingsData.ResolutionCount, _delta);
            EResolution _candidate = SettingsData.GetResolutionAtDisplayOrder(_order);

            // 강등되지 않는 값 == 이 모니터에서 표시 가능한 값
            if (_candidate == SettingsData.ClampResolution(_candidate, _maxWidth, _maxHeight))
            {
                current.resolution = _candidate;
                MarkDisplayDirty();
                return;
            }
        }

        // 여기에는 도달하지 않는다. 목록에서 가장 작은 해상도는 어떤 모니터에도 들어간다고
        // 가정하므로, 순환이 그 후보에 닿는 순간 위에서 반드시 return한다.
        // (ClampResolution의 하한 처리를 바꾸면 이 전제도 함께 검토해야 한다)
    }

    public void CycleWindowMode(int _delta)
    {
        EnsureLoaded();

        current.windowMode = (EWindowMode)IterateEnum((int)current.windowMode, SettingsData.WINDOW_MODE_COUNT, _delta);
        MarkDisplayDirty();
        OnWindowModeChangedEvent?.Invoke(current.windowMode);
    }

    public void CycleFps(int _delta)
    {
        EnsureLoaded();

        current.fps = (EFPS)IterateEnum((int)current.fps, SettingsData.FPS_COUNT, _delta);
        MarkDisplayDirty();
    }

    public void CyclePauseOnUnfocus(int _delta)
    {
        EnsureLoaded();

        current.pauseOnUnfocus = (EOnOff)IterateEnum((int)current.pauseOnUnfocus, SettingsData.ON_OFF_COUNT, _delta);
        MarkDisplayDirty();
    }

    /// <summary>
    /// 패드 아이콘 표기를 순환시킵니다. 화면 항목이 아니므로 MarkDisplayDirty가 아니라 isDirty만 세웁니다.
    /// (여기서 화면 dirty를 세우면 표기만 바꿔도 해상도가 다시 적용되어 창 크기가 튑니다)
    ///
    /// 선택 즉시 화면의 아이콘이 바뀌어야 유저가 무엇을 고르는지 알 수 있으므로,
    /// 볼륨 슬라이더와 같은 방식으로 여기서 곧바로 실시간 반영까지 합니다. (저장은 CommitChanges)
    /// </summary>
    public void CycleGamepadIconPreference(int _delta)
    {
        EnsureLoaded();

        EGamepadIconPreference _next = (EGamepadIconPreference)IterateEnum((int)current.gamepadIconPreference, SettingsData.GAMEPAD_ICON_PREFERENCE_COUNT, _delta);
        if (_next == current.gamepadIconPreference) return;

        current.gamepadIconPreference = _next;
        isDirty = true;

        ApplyInputSettingsLive();
    }

    /// <summary>
    /// 패드 진동 세기를 설정합니다. (0~100, 0 = 진동 끔)
    /// 다른 슬라이더와 달리 여기서 곧바로 실시간 반영까지 하므로, UI는 이 메서드만 부르면 됩니다.
    /// (조절하는 동안 실제로 패드가 울려야 세기를 가늠할 수 있기 때문입니다. 저장은 CommitChanges)
    /// </summary>
    public void SetHapticStrength(float _val)
    {
        EnsureLoaded();

        if (Mathf.Approximately(_val, current.hapticStrength)) return;

        current.hapticStrength = _val;
        isDirty = true;

        ApplyInputSettingsLive();
    }

    /// <summary>
    /// 특성 UI 가상 커서의 감도를 설정합니다. (0~100, 가운데 50이 기본 배율)
    ///
    /// 진동 세기와 마찬가지로 여기서 곧바로 실시간 반영까지 하므로 UI는 이 메서드만 부르면 됩니다.
    /// 감도는 숫자로는 가늠이 안 되고 직접 움직여 봐야 알 수 있는 값이라 실시간 반영이 특히 중요합니다.
    /// (저장은 CommitChanges)
    /// </summary>
    public void SetVirtualCursorSensitivity(float _val)
    {
        EnsureLoaded();

        if (Mathf.Approximately(_val, current.virtualCursorSensitivity)) return;

        current.virtualCursorSensitivity = _val;
        isDirty = true;

        ApplyInputSettingsLive();
    }

    /// <summary>패드 아이콘 표기를 직접 지정합니다. (순환이 아닌 목록형 UI용)</summary>
    public void SetGamepadIconPreference(EGamepadIconPreference _preference)
    {
        EnsureLoaded();

        if (_preference == current.gamepadIconPreference) return;

        current.gamepadIconPreference = _preference;
        isDirty = true;

        ApplyInputSettingsLive();
    }

    public void SetCameraShake(float _val) { EnsureLoaded(); current.cameraShake = _val; isDirty = true; }
    public void SetCrosshairBrightness(float _val) { EnsureLoaded(); current.crosshairBrightness = _val; isDirty = true; }
    public void SetChromaticAberration(float _val) { EnsureLoaded(); current.chromaticAberration = _val; isDirty = true; }
    public void SetBrightness(float _val) { EnsureLoaded(); current.brightness = _val; isDirty = true; }
    public void SetSaturation(float _val) { EnsureLoaded(); current.saturation = _val; isDirty = true; }

    public void SetMasterVolume(float _val) { EnsureLoaded(); current.masterVolume = _val; isDirty = true; }
    public void SetBgmVolume(float _val) { EnsureLoaded(); current.bgmVolume = _val; isDirty = true; }
    public void SetSfxVolume(float _val) { EnsureLoaded(); current.sfxVolume = _val; isDirty = true; }

    /// <summary>
    /// 볼륨을 조작하는 즉시 소리에 반영합니다. (슬라이더를 드래그하는 동안 실시간 피드백용)
    /// 저장은 하지 않으므로 CommitChanges와 별개이며, 창을 닫을 때 한 번 더 적용/저장됩니다.
    /// </summary>
    public void ApplyAudioSettingsLive()
    {
        EnsureLoaded();
        OnAudioSettingsAppliedEvent?.Invoke(current);
    }

    /// <summary>
    /// 색수차·명도·채도를 조작하는 즉시 화면에 반영합니다. (슬라이더를 드래그하는 동안 실시간 피드백용)
    /// 저장은 하지 않으므로 CommitChanges와 별개이며, 창을 닫을 때 한 번 더 적용/저장됩니다.
    /// </summary>
    public void ApplyGraphicsSettingsLive()
    {
        EnsureLoaded();
        OnGraphicsSettingsAppliedEvent?.Invoke(current);
    }

    /// <summary>
    /// 패드 아이콘 표기를 바꾸는 즉시 화면에 반영합니다. (옵션 창에서 선택하는 동안의 실시간 피드백용)
    /// 저장은 하지 않으므로 CommitChanges와 별개이며, 창을 닫을 때 한 번 더 적용/저장됩니다.
    /// </summary>
    public void ApplyInputSettingsLive()
    {
        EnsureLoaded();
        OnInputSettingsAppliedEvent?.Invoke(current);
    }

    private void MarkDisplayDirty()
    {
        isDirty = true;
        isDisplayDirty = true;
    }

    // 표기용 헬퍼 (로컬라이징이 불필요한 항목만 담당)
    public static string GetResolutionLabel(EResolution _res)
    {
        int _idx = (int)_res;
        if (_idx >= 0 && _idx < resolutionLabels.Length) return resolutionLabels[_idx];
        return "Unknown";
    }

    /// <summary>
    /// 숫자로 표기 가능한 FPS면 해당 문자열을, VSync/Unlimited처럼
    /// 로컬라이징이 필요한 항목이면 null을 반환합니다.
    /// </summary>
    public static string GetFpsNumberLabel(EFPS _fps)
    {
        int _idx = (int)_fps;
        if (_idx >= 0 && _idx < fpsNumberLabels.Length) return fpsNumberLabels[_idx];
        return null;
    }

    /// <summary>전체화면일 때 표기할 현재 모니터 해상도 문자열입니다.</summary>
    public static string GetMonitorResolutionLabel()
    {
        DisplayUtil.GetMainDisplaySize(out int _width, out int _height);
        return _width + "x" + _height;
    }

    // 실제 적용 및 저장
    /// <summary>
    /// 현재 설정값을 엔진에 실제로 반영합니다. (옵션 창을 닫을 때 호출)
    /// </summary>
    public void ApplySettings()
    {
        EnsureLoaded();
        ApplyDisplaySettings();

        // 하위 시스템이 자기 몫을 가져가도록 알린다. (구독자가 없으면 아무 일도 일어나지 않음)
        OnAudioSettingsAppliedEvent?.Invoke(current);
        OnGraphicsSettingsAppliedEvent?.Invoke(current);
        OnInputSettingsAppliedEvent?.Invoke(current);
    }

    /// <summary>
    /// 유저가 값을 바꾼 경우에만 적용하고 저장합니다. (옵션 창을 닫을 때 호출)
    /// 바꾼 게 없으면 아무 일도 하지 않으므로, 창을 열었다 닫기만 해서
    /// 화면 해상도가 임의로 바뀌는 일이 없습니다.
    /// </summary>
    public void CommitChanges()
    {
        if (false == isDirty) return;

        EnsureLoaded();

        // 화면 항목을 실제로 건드린 경우에만 해상도·프레임레이트를 다시 적용한다.
        // 볼륨만 조정했는데 창 크기가 바뀌는 일을 막기 위한 구분이다.
        if (true == isDisplayDirty)
        {
            ApplyDisplaySettings();
            isDisplayDirty = false;
        }

        OnAudioSettingsAppliedEvent?.Invoke(current);
        OnGraphicsSettingsAppliedEvent?.Invoke(current);

        Save();
    }

    /// <summary>
    /// 엔진 API만으로 적용 가능한 화면 관련 설정입니다.
    /// 다른 시스템에 의존하지 않으므로 부팅 시점에도 안전하게 호출할 수 있습니다.
    /// </summary>
    private void ApplyDisplaySettings()
    {
        ApplyScreen();
        ApplyFrameRate();

        // 백그라운드 일시정지 옵션. Off일 경우 백그라운드에서도 게임이 계속 실행됨
        Application.runInBackground = (EOnOff.Off == current.pauseOnUnfocus);
    }

    /// <summary>
    /// 지금 설정대로라면 실제 적용될(또는 이미 적용되어 있는) 화면 크기를 계산만 합니다.
    /// Screen.SetResolution을 호출하지 않으므로, 화면을 실제로 바꾸지 않고도
    /// "지금 화면이 어떤 크기일지" 미리 알아야 하는 곳(PixelPerfectCamera 기준 해상도
    /// 계산 등)에서 안전하게 쓸 수 있습니다. ApplyScreen과 로직을 공유합니다.
    /// </summary>
    public void GetCurrentScreenTarget(out int _width, out int _height)
    {
        EnsureLoaded();

        if (EWindowMode.Fullscreen == current.windowMode)
        {
            // 전체화면 시 현재 모니터 해상도로 덮어쓰기 (기획 의도에 따라 다를 수 있음)
            DisplayUtil.GetMainDisplaySize(out _width, out _height);
        }
        else
        {
            // 저장된 값은 그대로 두고, 표시 불가능한 경우에만 적용 시점에 낮춘다.
            SettingsData.GetResolutionSize(EffectiveResolution, out _width, out _height);
        }
    }

    private void ApplyScreen()
    {
        bool _isFullscreen = (EWindowMode.Fullscreen == current.windowMode);

        GetCurrentScreenTarget(out int _width, out int _height);

        if (_width <= 0 || _height <= 0)
        {
            // 해상도를 알아내지 못했더라도 전체화면 여부는 반영해야 한다.
            // (그냥 return하면 유저가 전체화면을 골라도 아무 반응이 없다)
            Screen.fullScreen = _isFullscreen;
            return;
        }

        Screen.SetResolution(_width, _height, _isFullscreen);
        OnScreenTargetResolvedEvent?.Invoke(_width, _height);
    }

    /// <summary>
    /// 목표 fps와 모니터 주사율을 "같다"고 볼 허용 오차(Hz)입니다.
    /// 모니터는 60을 59.94로, 165를 164.8로 보고하는 등 정수로 떨어지지 않습니다.
    /// </summary>
    private const float refreshRateTolerance = 1.5f;

    private FrameRateLimiter frameRateLimiter;

    /// <summary>
    /// 엔진 설정과 무관하게 상한을 보장하는 리미터입니다. 싱글턴 오브젝트에 붙여 두므로
    /// 씬 전환에도 유지되고, 이 매니저와 수명을 같이합니다.
    /// </summary>
    private FrameRateLimiter Limiter
    {
        get
        {
            if (null == frameRateLimiter)
            {
                // 프리팹으로 배치된 경우 이미 붙어 있을 수 있으므로 먼저 찾아본다.
                frameRateLimiter = GetComponent<FrameRateLimiter>();

                if (null == frameRateLimiter)
                {
                    frameRateLimiter = gameObject.AddComponent<FrameRateLimiter>();
                }
            }
            return frameRateLimiter;
        }
    }

    private void ApplyFrameRate()
    {
        int _target = GetFpsValue(current.fps);   // VSync/Unlimited는 -1
        float _refresh = GetMonitorRefreshRate(); // 알 수 없으면 0

        // 1. 유저가 VSync를 명시적으로 선택한 경우.
        //    이때의 상한은 곧 주사율이므로, 드라이버가 VSync를 무시하는 환경을 대비해
        //    리미터에도 주사율을 걸어 둔다. (주사율을 모르면 0이 되어 제한하지 않는다)
        if (EFPS.VSync == current.fps)
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1; // VSync에 동기화
            Limiter.SetLimit(Mathf.RoundToInt(_refresh));
            return;
        }

        // 2. 목표 fps가 모니터 주사율과 사실상 같으면 VSync로 처리한다.
        //    targetFrameRate 리미터는 스캔아웃과 무관한 타이머라 프레임 간격이 흔들리는데,
        //    이 프로젝트의 저해상도 RT + 픽셀 스냅(SubpixelSnapper) 파이프라인에서는
        //    그 지터가 Mathf.Round 경계를 넘나들며 픽셀 우글거림으로 증폭된다.
        //
        //    예전에 이 분기를 제거했던 이유는, vSyncCount가 0이 아니면 Unity가
        //    targetFrameRate를 아예 무시하는데 드라이버의 "수직 동기화: 끄기"가 VSync까지
        //    덮어쓰면 상한이 하나도 남지 않아 165 선택에도 200+ FPS로 치솟았기 때문이다.
        //    이제는 FrameRateLimiter가 드라이버와 무관하게 상한을 보장하므로 안전하다.
        //
        //    주사율과 정수비가 아닌 조합(144Hz 모니터에서 60 선택 등)은 원리상 균등 배분이
        //    불가능하므로 VSync를 쓰지 않고 기존대로 targetFrameRate로 제한한다.
        bool _matchesRefresh = _target > 0
                               && _refresh > 0f
                               && Mathf.Abs(_refresh - _target) <= refreshRateTolerance;

        QualitySettings.vSyncCount = _matchesRefresh ? 1 : 0;
        Application.targetFrameRate = _matchesRefresh ? -1 : _target;

        // 3. 어느 경로를 타든 최종 상한은 리미터가 보장한다. (Unlimited는 -1이라 제한 없음)
        Limiter.SetLimit(_target);
    }

    /// <summary>
    /// 숫자로 지정된 FPS 값을 반환합니다. VSync/Unlimited처럼 고정 수치가 없으면 -1입니다.
    /// (-1은 Application.targetFrameRate에서 "제한 없음"을 의미하므로 그대로 대입해도 안전합니다)
    /// </summary>
    public static int GetFpsValue(EFPS _fps)
    {
        int _idx = (int)_fps;
        if (_idx >= 0 && _idx < fpsValues.Length) return fpsValues[_idx];
        return -1;
    }

    /// <summary>현재 모니터의 주사율(Hz)입니다. 알 수 없으면 0입니다.</summary>
    public static float GetMonitorRefreshRate()
    {
        return DisplayUtil.GetMainDisplayRefreshRate();
    }

    /// <summary>언어는 예외적으로 선택 즉시 반영됩니다.</summary>
    private void ApplyLanguage()
    {
        ApplyLanguageToLocalization();
        OnLanguageChangedEvent?.Invoke(current.language);
    }

    private void ApplyLanguageToLocalization()
    {
        if (null == locManager) return;

        Language _langToSet = current.language switch
        {
            EOptionLanguage.Korean => Language.KR,
            EOptionLanguage.ChineseSimplified => Language.ZH_HANS,
            EOptionLanguage.ChineseTraditional => Language.ZH_HANT,
            EOptionLanguage.Japanese => Language.JA,
            _ => Language.EN
        };
        locManager.SetLanguage(_langToSet);
    }

    /// <summary>
    /// 저장된 설정을 읽어옵니다. 파일이 없거나 손상되었으면 기본값을 사용합니다.
    /// isLoaded와 짝을 이루어야 하므로 반드시 EnsureLoaded를 통해서만 호출합니다.
    /// (직접 부르면 상태가 어긋납니다)
    /// </summary>
    private void Load()
    {
        ESettingsLoadResult _result = SettingsRepository.TryLoad(out current);

        // 유저가 고른 언어가 아직 없으면(첫 실행이거나 파일을 폐기한 경우) 기본값인 한국어를 그대로
        // 쓰지 않고 환경에서 추론한다. 근거와 우선순위는 LanguageAutoDetect 주석 참고.
        // Loaded일 때는 파일에 유저의 선택이 들어 있으므로 절대 건드리지 않는다.
        if (ESettingsLoadResult.Loaded != _result)
        {
            current.language = LanguageAutoDetect.Resolve();
        }

        // 손상·변조된 값만 교정한다. 현재 모니터에 맞춘 해상도 보정은 적용 시점에만 수행하며
        // 저장값에는 반영하지 않는다. (작은 화면에 임시로 연결한 것만으로 설정이 사라지지 않도록)
        bool _corrected = current.Validate();

        // 다음 두 경우에는 정리된 값을 한 번 기록해 파일을 최신 상태로 만든다.
        //  - Discarded: 못 쓰는 파일(구버전·손상)이 남아 있어, 그대로 두면 매 실행 같은 경고가 반복된다.
        //  - Loaded + 교정 발생: 범위를 벗어난 값이 파일에 계속 남지 않도록 한다.
        isDirty = (ESettingsLoadResult.Discarded == _result)
               || (ESettingsLoadResult.Loaded == _result && true == _corrected);

        // 로드 직후의 화면 적용은 Bootstrap이 담당하므로 여기서는 세우지 않는다.
        isDisplayDirty = false;
    }

    /// <summary>
    /// 유저가 값을 바꾼 적이 있을 때만 파일에 기록합니다.
    /// _force는 값 변경 여부와 무관하게 기록해야 할 때만 사용합니다.
    /// </summary>
    public void Save(bool _force = false)
    {
        if (false == isDirty && false == _force) return;

        SettingsRepository.Save(current);
        isDirty = false;
    }

    // 유틸리티
    private static int IterateEnum(int _current, int _length, int _delta)
    {
        int _newVal = _current + _delta;
        if (_newVal < 0) return _length - 1;
        if (_newVal >= _length) return 0;
        return _newVal;
    }

    /// <summary>
    /// 옵션 창을 닫지 않고 게임을 종료해도 변경분이 남도록 보강합니다.
    /// 바꾼 게 없으면 Save()가 스스로 스킵하므로 빈 파일이 생기지 않습니다.
    /// </summary>
    private void OnApplicationQuit()
    {
        Save();
    }

    private void OnDestroy()
    {
        OnLanguageChangedEvent = null;
        OnWindowModeChangedEvent = null;
        OnAudioSettingsAppliedEvent = null;
        OnGraphicsSettingsAppliedEvent = null;
        OnScreenTargetResolvedEvent = null;

        if (instance == this)
        {
            instance = null;
        }
    }
}
