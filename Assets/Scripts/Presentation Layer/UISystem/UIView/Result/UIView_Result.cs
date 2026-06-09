using UnityEngine;
using UnityEngine.UI;
using System;

public class UIView_Result : UIView
{
    public event Action GoHomeButtonClickedEvent;
    public event Action RetryButtonClickedEvent;

    [Header("UI References")]
    [SerializeField] private Button goHomeButton;
    [SerializeField] private Button retryButton;

    #region Public Override Methods

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (goHomeButton != null)
            goHomeButton.onClick.AddListener(OnGoHomeButtonClicked);

        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryButtonClicked);

        // 비활성화
        SetResultContentsActive(false);
    }

    public override void SetupUI()
    {
        base.SetupUI();
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    public override void Release()
    {
        base.Release();
    }

    public void OnGoHomeButtonClicked()
    {
        GoHomeButtonClickedEvent?.Invoke();
    }

    public void OnRetryButtonClicked()
    {
        RetryButtonClickedEvent?.Invoke();
    }

    public void OpenResultUI()
    {
        // 활성화
        SetResultContentsActive(true);
    }

    private void SetResultContentsActive(bool active)
    {
        foreach (Transform child in transform)
            child.gameObject.SetActive(active);
    }

    #endregion

    #region Protected Override Methods

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    #endregion

    #region Unity Event Functions

    public override void OnDestroy()
    {
        base.OnDestroy();

        GoHomeButtonClickedEvent = null;
        RetryButtonClickedEvent = null;
    }

    #endregion
}
