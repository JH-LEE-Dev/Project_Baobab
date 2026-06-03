using UnityEngine;
using UnityEngine.UI;
using System;


public class UIView_MainMenu : UIView
{
    public event Action NewGameButtonClickedEvent;
    public event Action LoadGameButtonClickedEvent;
    public event Action ExitButtonClickedEvent;


    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject uiPrefab; //생성할 uiPrefab인데 임의로 추가/제거해서 사용하면 됨.
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button exitButton;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (uiPrefab != null)
            Instantiate(uiPrefab, uiRoot);

        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGameStartButton);

        if (loadGameButton != null)
            loadGameButton.onClick.AddListener(OnLoadGameButtonClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitButtonClicked);
    }

    public override void OnDestroy()
    {
        NewGameButtonClickedEvent = null;
        LoadGameButtonClickedEvent = null;
        ExitButtonClickedEvent = null;
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
    }

    public void OnNewGameStartButton()
    {
        NewGameButtonClickedEvent?.Invoke();
    }

    public void OnLoadGameButtonClicked()
    {
        LoadGameButtonClickedEvent?.Invoke();
    }

    public void OnExitButtonClicked()
    {
        ExitButtonClickedEvent?.Invoke();
    }
}
