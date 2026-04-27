using UnityEngine;
using UnityEngine.UI;
using System;

public class UIView_ESC : UIView
{
    public event Action SaveGameButtonClickedEvent;
    public event Action GoToMainMenuButtonClickedEvent;
    public event Action ExitButtonClickedEvent;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject uiPrefab; //생성할 uiPrefab인데 임의로 추가/제거해서 사용하면 됨.
    [SerializeField] private Button saveGameButton;
    [SerializeField] private Button goToMainMenuButton;
    [SerializeField] private Button exitButton;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (uiPrefab != null)
            Instantiate(uiPrefab, uiRoot);

        if (saveGameButton != null)
            saveGameButton.onClick.AddListener(OnSaveGameButton);

        if (goToMainMenuButton != null)
            goToMainMenuButton.onClick.AddListener(OnGoToMainMenuButtonClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitButtonClicked);
    }

    public override void OnDestroy()
    {
        SaveGameButtonClickedEvent = null;
        GoToMainMenuButtonClickedEvent = null;
        ExitButtonClickedEvent = null;
    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    public void OnSaveGameButton()
    {
        SaveGameButtonClickedEvent?.Invoke();
    }

    public void OnGoToMainMenuButtonClicked()
    {
        GoToMainMenuButtonClickedEvent?.Invoke();
    }

    public void OnExitButtonClicked()
    {
        ExitButtonClickedEvent?.Invoke();
    }
}
