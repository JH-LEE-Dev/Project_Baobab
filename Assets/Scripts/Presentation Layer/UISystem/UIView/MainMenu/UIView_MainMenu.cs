using UnityEngine;
using UnityEngine.UI;
using System;


public class UIView_MainMenu : UIView
{
    public event Action PlayButtonClickedEvent;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject uiPrefab;
    [SerializeField] private Button startButton;


    protected override void Awake()
    {
        base.Awake();

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

    public void RenderUI()
    {

    }

    public void OnGameStartButton()
    {
        PlayButtonClickedEvent?.Invoke();
    }
}
