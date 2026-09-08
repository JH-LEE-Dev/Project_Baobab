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
    private UIView_SaveCheck view;

    /// <summary>
    /// 화면을 띄울 준비가 됐는지입니다.
    /// false면 실패 상태에서 유저에게 물어볼 방법이 없으므로, BootStrap은 기다리지 않고 진행해야 합니다.
    /// (기다리면 아무것도 없는 화면에서 영원히 멈춥니다)
    /// </summary>
    public bool IsPresenting => null != view;

    public void Initialize(ISaveCheckSystem _saveCheckSystem, LocalizationManager _localizationManager = null)
    {
        saveCheckSystem = _saveCheckSystem;

        if (null == saveCheckViewPrefab)
        {
            Debug.LogWarning("[SaveCheckCoordinator] No save check view prefab is assigned. " +
                "The player will not be told when a save file cannot be read.");
            return;
        }

        if (null == view)
        {
            view = Instantiate(saveCheckViewPrefab);
            DontDestroyOnLoad(view.gameObject);

            EnsurePixelPerfectCanvas(view);

            InputManager _inputManager = GetComponent<InputManager>();
            if (null == _localizationManager)
            {
                _localizationManager = GetComponentInChildren<LocalizationManager>();
            }

            view.InitializeDependencies(_inputManager, _localizationManager);

            view.RetryRequestedEvent += OnRetryRequested;
            view.AbandonConfirmedEvent += OnAbandonConfirmed;
        }

        PushState();
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

        // 스케일러를 먼저 확보한다. 아래 Applier의 RequireComponent가 대신 붙이게 두면
        // 유니티 기본값이 들어가 REFERENCE_PIXELS_PER_UNIT을 지정할 기회를 놓친다.
        if (false == _canvas.TryGetComponent(out CanvasScaler _canvasScaler))
        {
            _canvasScaler = _canvas.gameObject.AddComponent<CanvasScaler>();
            _canvasScaler.referencePixelsPerUnit = REFERENCE_PIXELS_PER_UNIT;

            // Applier가 ConstantPixelSize로 바꾸고 나면 런타임 동작에는 쓰이지 않지만,
            // 인스펙터에서 이 캔버스의 기준을 읽을 수 있도록 다른 캔버스와 같은 값을 넣어둔다.
            _canvasScaler.referenceResolution =
                new Vector2(SettingsData.PIXEL_PERFECT_REF_WIDTH, SettingsData.CAMERA_VIEW_HEIGHT);
        }
        else if (false == Mathf.Approximately(_canvasScaler.referencePixelsPerUnit, REFERENCE_PIXELS_PER_UNIT))
        {
            // 배율과 달리 이 값은 UI가 의도해서 넣었을 수 있으므로 덮어쓰지 않는다.
            // 다만 어긋난 채로 두면 이 화면의 스프라이트만 크기가 달라지므로 조용히 넘기지도 않는다.
            Debug.LogWarning("[SaveCheckCoordinator] The save check canvas uses Reference Pixels Per Unit " +
                $"{_canvasScaler.referencePixelsPerUnit}, but the rest of the game's UI uses {REFERENCE_PIXELS_PER_UNIT}. " +
                "Sprites on this screen will be drawn at a different size.");
        }

        // 활성 오브젝트에 붙이므로 이 자리에서 곧바로 OnEnable이 돌아 첫 프레임부터 배율이 맞는다.
        if (false == _canvas.TryGetComponent<PixelPerfectCanvasScaleApplier>(out _))
        {
            _canvas.gameObject.AddComponent<PixelPerfectCanvasScaleApplier>();
        }
    }
    private void PushState()
    {
        if (null == view || null == saveCheckSystem) return;

        if (ESaveCheckState.Ready == saveCheckSystem.SaveCheckState)
        {
            view.RetryRequestedEvent -= OnRetryRequested;
            view.AbandonConfirmedEvent -= OnAbandonConfirmed;
            Destroy(view.gameObject);
            view = null;
            return;
        }

        view.ApplyState(saveCheckSystem.SaveCheckState, saveCheckSystem.SaveCheckElapsedSeconds);
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
