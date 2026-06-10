using System;
using UnityEngine;
using UnityEngine.UI;

public class UIView_Warning : UIView
{
    public event Action DeActivateWarningUIEvent;

    [Header("UI References")]
    [SerializeField] private Button okButton;
    [SerializeField] private Button cancelButton;

    public bool bApproved = false;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        CacheUIReferences();
        BindButtonEvents();
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

    protected override void OnShow()
    {
        base.OnShow();
        bApproved = false;
        gameObject.SetActive(true);
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
        DeActivateWarningUI();
        bApproved = false;
    }

    public override void OnDestroy()
    {
        UnbindButtonEvents();
        DeActivateWarningUIEvent = null;
        base.OnDestroy();
    }

    public void OnOKButtonClicked()
    {
        bApproved = true;
        Hide();
    }

    public void OnCancelButtonClicked()
    {
        bApproved = false;
        Hide();
    }

    private void CacheUIReferences()
    {
        if (okButton == null)
        {
            Transform okButtonTransform = transform.Find("WarningBG/ButtonRoot/Button_OK");
            if (okButtonTransform != null)
                okButton = okButtonTransform.GetComponent<Button>();
        }

        if (cancelButton == null)
        {
            Transform cancelButtonTransform = transform.Find("WarningBG/ButtonRoot/Button_Cancel");
            if (cancelButtonTransform != null)
                cancelButton = cancelButtonTransform.GetComponent<Button>();
        }
    }

    private void BindButtonEvents()
    {
        if (okButton != null)
            okButton.onClick.AddListener(OnOKButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(OnCancelButtonClicked);
    }

    private void UnbindButtonEvents()
    {
        if (okButton != null)
            okButton.onClick.RemoveListener(OnOKButtonClicked);

        if (cancelButton != null)
            cancelButton.onClick.RemoveListener(OnCancelButtonClicked);
    }

    private void DeActivateWarningUI()
    {
        DeActivateWarningUIEvent?.Invoke();
    }
}
