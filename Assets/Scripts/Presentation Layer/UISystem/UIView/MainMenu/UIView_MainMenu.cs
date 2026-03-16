using UnityEngine;
using UnityEngine.UI;
using System;


public class UIView_MainMenu : UIView
{
    public event Action PlayButtonClickedEvent;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject uiPrefab; //생성할 uiPrefab인데 임의로 추가/제거해서 사용하면 됨.
    [SerializeField] private Button startButton;


    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (uiPrefab != null)
            Instantiate(uiPrefab, uiRoot);

        if (startButton != null)
            startButton.onClick.AddListener(OnGameStartButton);
    }

    public override void OnDestroy()
    {
        PlayButtonClickedEvent = null;
    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    public void OnGameStartButton()
    {
        PlayButtonClickedEvent?.Invoke();
    }
}
