using UnityEngine;

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

    public void Initialize(ISaveCheckSystem _saveCheckSystem)
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

            view.RetryRequestedEvent += OnRetryRequested;
            view.AbandonConfirmedEvent += OnAbandonConfirmed;
            view.QuitRequestedEvent += OnQuitRequested;
        }

        PushState();
    }

    private void OnDestroy()
    {
        if (null == view) return;

        view.RetryRequestedEvent -= OnRetryRequested;
        view.AbandonConfirmedEvent -= OnAbandonConfirmed;
        view.QuitRequestedEvent -= OnQuitRequested;
    }

    // 상태는 코루틴이 아니라 매 프레임 밀어 넣는다. 확인이 도는 동안에도, 실패 화면에서 유저가
    // 다시 시도를 눌러 상태가 되돌아갈 때도 같은 경로로 반영되어 흐름이 하나로 유지된다.
    private void Update()
    {
        PushState();
    }

    private void PushState()
    {
        if (null == view || null == saveCheckSystem) return;

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
}
