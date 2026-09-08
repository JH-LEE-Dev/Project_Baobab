using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 세이브 확인 시스템(SaveManager)과 확인 화면(UIView_SaveCheck)을 잇습니다.
///
/// 이 클래스가 있는 이유는 UIView가 SaveManager를 직접 알지 못하게 하기 위해서입니다.
/// UI 쪽은 버튼이 눌렸다는 사실만 이벤트로 올려보내고, 그것을 무슨 동작으로 옮길지는 전부 여기서
/// 정합니다. 덕분에 UI 작업 중에 세이브 로직이 바뀔 일이 없습니다.
///
/// [왜 다른 UI처럼 Installer/UIManager를 타지 않는가]
/// 이 화면은 메인 메뉴가 만들어지기 *전에* 떠 있어야 합니다. 세이브 상태가 확정되기 전에 메뉴가
/// 뜨면 유저가 새로하기를 눌러 멀쩡한 세이브를 지울 수 있고, 그것을 막는 게 이 화면의 목적이기
/// 때문입니다. 그 시점에는 MainMenuUIInstaller도 UIViewContext도 아직 없으므로, BootStrap에 붙어
/// 직접 뷰를 띄웁니다. 그래서 UIViewContext 없이 동작하도록 UIView_SaveCheck를 만들어 두었습니다.
/// </summary>
public class SaveCheckCoordinator : MonoBehaviour
{
    /// <summary>
    /// 캔버스가 스프라이트 1픽셀을 UI 단위 몇으로 볼지입니다. Assets/Prefabs/UI/Canvas 아래
    /// 캔버스 프리팹 세 개가 모두 쓰는 값이자 PixelPerfectCamera의 Assets PPU와 같은 값입니다.
    /// 여기만 유니티 기본값(100)으로 남으면 이 화면의 스프라이트만 3분의 1 크기로 그려집니다.
    /// </summary>
    private const float REFERENCE_PIXELS_PER_UNIT = 32f;

    [SerializeField, Tooltip("세이브 확인/실패 화면 프리팹. 비워두면 이 화면 없이 게임이 진행됩니다(로그 경고).")]
    private UIView_SaveCheck saveCheckViewPrefab;

    private ISaveCheckSystem saveCheckSystem;
    private LocalizationManager localizationManager;
    private UIView_SaveCheck view;

    /// <summary>
    /// 유저에게 물어볼 화면을 띄울 수 있는지입니다.
    /// false면 실패 상태에서 물어볼 방법이 없으므로, BootStrap은 기다리지 않고 진행해야 합니다.
    /// (기다리면 아무것도 없는 화면에서 영원히 멈춥니다)
    ///
    /// 지금 인스턴스가 살아 있는지가 아니라 "필요하면 만들 수 있는지"를 답합니다. 결론이 난 뒤에는
    /// 인스턴스를 파괴하지만(아래 PushState 참고) 물어볼 능력을 잃은 것은 아니기 때문입니다.
    /// </summary>
    public bool IsPresenting => null != saveCheckViewPrefab;

    public void Initialize(ISaveCheckSystem _saveCheckSystem, LocalizationManager _localizationManager = null)
    {
        saveCheckSystem = _saveCheckSystem;

        // 화면을 다시 만들 때 또 필요하므로 들고 있는다. (메인 메뉴로 복귀할 때마다 확인이 다시 돈다)
        localizationManager = null != _localizationManager
            ? _localizationManager
            : GetComponentInChildren<LocalizationManager>();

        if (null == saveCheckViewPrefab)
        {
            Debug.LogWarning("[SaveCheckCoordinator] No save check view prefab is assigned. " +
                "The player will not be told when a save file cannot be read.");
            return;
        }

        PushState();
    }

    /// <summary>
    /// 화면이 필요한데 없으면 만듭니다.
    ///
    /// 부팅 때 한 번 만들고 끝내지 않는 이유는, 결론이 난 뒤 인스턴스를 파괴하기 때문입니다.
    /// 그런데 확인은 부팅 때만 도는 것이 아니라 타운/던전에서 메인 메뉴로 돌아올 때마다 다시 돕니다
    /// (BootStrap.OnSceneLoaded). 만들어 두기만 하면 두 번째부터는 화면 없이 조용히 실패합니다.
    /// </summary>
    private bool EnsureView()
    {
        if (null != view) return true;
        if (null == saveCheckViewPrefab) return false;

        view = Instantiate(saveCheckViewPrefab);
        DontDestroyOnLoad(view.gameObject);

        EnsurePixelPerfectCanvas(view);

        view.InitializeDependencies(GetComponent<InputManager>(), localizationManager);

        view.RetryRequestedEvent += OnRetryRequested;
        view.AbandonConfirmedEvent += OnAbandonConfirmed;

        return true;
    }

    /// <summary>
    /// 화면을 치웁니다. 결론이 난 뒤에도 들고 있으면 이 화면의 커서 박스가 메인 메뉴 것과 겹칩니다.
    /// </summary>
    private void DestroyViewIfAny()
    {
        if (null == view) return;

        view.RetryRequestedEvent -= OnRetryRequested;
        view.AbandonConfirmedEvent -= OnAbandonConfirmed;

        Destroy(view.gameObject);
        view = null;
    }

    /// <summary>
    /// 이 화면의 캔버스를 프로젝트의 픽셀 퍼펙트 규칙에 맞춥니다.
    ///
    /// 다른 UI는 전부 uiManager.Open으로 열려 Assets/Prefabs/UI/Canvas 아래 캔버스 프리팹
    /// 세 개 중 하나의 자식으로 들어가고, 거기 붙어 있는 PixelPerfectCanvasScaleApplier가
    /// 배율을 정수로 고정해 줍니다. 이 화면만 Installer를 타지 않고 자기 캔버스째로 뜨기
    /// 때문에 그 혜택을 받지 못합니다.
    ///
    /// 기본 CanvasScaler(ScaleWithScreenSize + Match Height)의 배율은 화면세로/360이라
    /// 실수입니다. 16:9 프리셋에서는 우연히 정수가 되어 멀쩡해 보이지만(1080/360=3),
    /// 16:10 프리셋 전부와 1366x768 같은 노트북 해상도에서는 2.13배 같은 값이 나와
    /// 원본 1px이 화면에서 2px과 3px로 들쭉날쭉 찍힙니다. 월드와 나머지 UI는 반올림한
    /// 정수 배율로 그려지므로, 이 화면만 배율이 어긋나게 됩니다.
    ///
    /// 프리팹에 이미 붙어 있으면 아무것도 하지 않습니다. 그런데도 코드에서 한 번 더 확인하는
    /// 것은, 이 화면이 Installer를 타지 않는 유일한 UI라 빠뜨려도 잡아줄 곳이 없기 때문입니다.
    /// </summary>
    private static void EnsurePixelPerfectCanvas(UIView_SaveCheck _view)
    {
        // 프리팹 루트에 있는 것이 정석이지만(Docs/SaveCheckUI.md 3절), 한 겹 안에 두었어도
        // 찾아낸 뒤 rootCanvas로 거슬러 올라간다. 배율은 루트 캔버스만 정하기 때문이다.
        Canvas _canvas = _view.GetComponentInChildren<Canvas>(true);

        if (null == _canvas)
        {
            Debug.LogWarning("[SaveCheckCoordinator] The save check view prefab has no Canvas, so it will not be drawn. " +
                "See Docs/SaveCheckUI.md section 3.");
            return;
        }

        _canvas = _canvas.rootCanvas;
        _canvas.pixelPerfect = true;

        // 스케일러를 먼저 확보하고 REFERENCE_PIXELS_PER_UNIT(32)을 강제한다.
        // PPU가 100 등 다른 값으로 남으면 9-슬라이스 스프라이트의 테두리가 3.125배로 뻥튀기되어
        // 커서 박스의 테두리가 찌그러지고 강제로 늘어난 것처럼 왜곡된다.
        if (false == _canvas.TryGetComponent(out CanvasScaler _canvasScaler))
        {
            _canvasScaler = _canvas.gameObject.AddComponent<CanvasScaler>();
        }
        else if (false == Mathf.Approximately(_canvasScaler.referencePixelsPerUnit, REFERENCE_PIXELS_PER_UNIT))
        {
            // 값은 아래에서 강제하지만, 어긋났다는 사실은 알린다. 조용히 덮어쓰면 프리팹에서 보이는
            // 모습과 실행 결과가 계속 다른 채로 남고, 작업자는 자기 설정이 무시된 줄 모른다.
            Debug.LogWarning("[SaveCheckCoordinator] The save check canvas prefab uses Reference Pixels Per Unit " +
                $"{_canvasScaler.referencePixelsPerUnit}, but the rest of the game's UI uses {REFERENCE_PIXELS_PER_UNIT}. " +
                "Overriding it so sprites match the other screens; fix the prefab to remove this warning.");
        }

        _canvasScaler.referencePixelsPerUnit = REFERENCE_PIXELS_PER_UNIT;
        _canvasScaler.referenceResolution =
            new Vector2(SettingsData.PIXEL_PERFECT_REF_WIDTH, SettingsData.CAMERA_VIEW_HEIGHT);
        _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        _canvasScaler.matchWidthOrHeight = 1f;

        // 활성 오브젝트에 붙이므로 이 자리에서 곧바로 OnEnable이 돌아 첫 프레임부터 배율이 맞는다.
        if (false == _canvas.TryGetComponent<PixelPerfectCanvasScaleApplier>(out _))
        {
            _canvas.gameObject.AddComponent<PixelPerfectCanvasScaleApplier>();
        }
    }
    private void PushState()
    {
        if (null == saveCheckSystem) return;

        ESaveCheckState _state = saveCheckSystem.SaveCheckState;

        // 확인 중이거나 실패했을 때만 화면이 필요하다. 결론이 났으면(Ready) 또는 아직 시작 전이면
        // 들고 있을 이유가 없다. 남겨두면 이 화면의 커서 박스가 메인 메뉴 것과 겹친다.
        if (ESaveCheckState.Checking != _state && ESaveCheckState.Failed != _state)
        {
            DestroyViewIfAny();
            return;
        }

        if (false == EnsureView()) return;

        view.ApplyState(_state, saveCheckSystem.SaveCheckElapsedSeconds);
    }

    private void OnRetryRequested()
    {
        saveCheckSystem?.RetrySaveAvailabilityCheck();
    }

    private void OnAbandonConfirmed()
    {
        // 뷰가 확인 팝업까지 거친 뒤에만 올려보낸다. 여기서 한 번 더 묻지 않는다.
        saveCheckSystem?.AbandonUnreadableSaveAndStartFresh();
    }

    private void OnQuitRequested()
    {
        // MainMenuUIInstaller.ExitGame과 같은 처리. 에디터에서는 플레이 모드를 끈다.
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // // Unity 생명주기 메서드 (SW_Rules 준수)
    // 상태는 코루틴이 아니라 매 프레임 밀어 넣는다. 확인이 도는 동안에도, 실패 화면에서 유저가
    // 다시 시도를 눌러 상태가 되돌아갈 때도 같은 경로로 반영되어 흐름이 하나로 유지된다.
    private void Update()
    {
        PushState();
    }

    private void OnDestroy()
    {
        if (null == view) return;

        view.RetryRequestedEvent -= OnRetryRequested;
        view.AbandonConfirmedEvent -= OnAbandonConfirmed;
    }
}
