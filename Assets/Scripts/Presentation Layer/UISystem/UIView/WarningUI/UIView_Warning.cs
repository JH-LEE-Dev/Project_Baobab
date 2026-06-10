using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIView_Warning : UIView
{
    private const int WarningLocalizationJsonId = 4;
    private const int MainTextEntryId = 1;
    private const int SubTextEntryId = 2;

    public event Action DeActivateWarningUIEvent;

    [Header("UI References")]
    [SerializeField] private RectTransform warningBG;
    [SerializeField] private RectTransform mainText;
    [SerializeField] private RectTransform subText;
    [SerializeField] private RectTransform buttonRoot;
    [SerializeField] private Button okButton;
    [SerializeField] private Button cancelButton;

    [Header("Position Pivots")]
    [SerializeField] private RectTransform mainTextPivot;
    [SerializeField] private RectTransform subTextPivot;
    [SerializeField] private RectTransform buttonPivot;
    [SerializeField] private RectTransform soloTextPivot;

    [Header("Production Settings")]
    [SerializeField] private float bgOpenDuration = 0.25f;
    [SerializeField] private float bgTargetWidth = 700f;
    [SerializeField] private float contentOpenDuration = 0.25f;
    [SerializeField] private float contentOpenInterval = 0.15f;
    [SerializeField] private float contentOpenYOffset = -20f;
    [SerializeField] private float closeDuration = 0.2f;
    [SerializeField] private Ease productionEase = Ease.OutCubic;

    public bool bApproved = false;

    private Sequence openSequence;
    private Sequence closeSequence;
    private CanvasGroup warningBGCanvasGroup;
    private CanvasGroup mainTextCanvasGroup;
    private CanvasGroup subTextCanvasGroup;
    private CanvasGroup buttonRootCanvasGroup;
    private TMP_Text mainTMPText;
    private TMP_Text subTMPText;
    private LocalizationManager localizationManager;
    private bool isClosing;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        localizationManager = _ctx?.localizationManager;
        if (localizationManager != null)
            localizationManager.OnLanguageChanged += RefreshLocalizedTexts;

        CacheCanvasGroups();
        CacheTextReferences();
        RefreshLocalizedTexts();
        BindButtonEvents();
    }

    public override void Hide()
    {
        if (IsVisible == false || isClosing)
            return;

        PlayCloseProduction();
    }

    protected override void OnShow()
    {
        base.OnShow();
        bApproved = false;
        gameObject.SetActive(true);
        PlayOpenProduction();
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
        KillProductionSequences();
        UnbindButtonEvents();

        if (localizationManager != null)
            localizationManager.OnLanguageChanged -= RefreshLocalizedTexts;

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

    private void CacheCanvasGroups()
    {
        warningBGCanvasGroup = GetOrAddCanvasGroup(warningBG);
        mainTextCanvasGroup = GetOrAddCanvasGroup(mainText);
        subTextCanvasGroup = GetOrAddCanvasGroup(subText);
        buttonRootCanvasGroup = GetOrAddCanvasGroup(buttonRoot);
    }

    private void CacheTextReferences()
    {
        if (mainTMPText == null && mainText != null)
            mainTMPText = mainText.GetComponent<TMP_Text>();

        if (subTMPText == null && subText != null)
            subTMPText = subText.GetComponent<TMP_Text>();
    }

    private void RefreshLocalizedTexts()
    {
        SetLocalizedText(mainTMPText, MainTextEntryId);
        SetLocalizedText(subTMPText, SubTextEntryId);
    }

    private void SetLocalizedText(TMP_Text text, int entryId)
    {
        if (text == null || localizationManager == null)
            return;

        string localizedText = localizationManager.GetText(WarningLocalizationJsonId, entryId);
        if (string.IsNullOrEmpty(localizedText))
            return;

        text.text = localizedText;
    }

    private void PlayOpenProduction()
    {
        KillProductionSequences();
        CacheTargetSize();
        PrepareOpenState();
        SetButtonsInteractable(false);

        openSequence = DOTween.Sequence().SetUpdate(true);

        if (warningBG != null)
            openSequence.Join(DOTween.To(GetWarningBGWidth, SetWarningBGWidth, bgTargetWidth, bgOpenDuration).SetEase(productionEase));

        if (warningBGCanvasGroup != null)
            openSequence.Join(warningBGCanvasGroup.DOFade(1f, bgOpenDuration).SetEase(productionEase));

        InsertContentOpenTween(mainText, mainTextCanvasGroup, mainTextPivot, bgOpenDuration);
        InsertContentOpenTween(subText, subTextCanvasGroup, subTextPivot, bgOpenDuration + contentOpenInterval);
        InsertContentOpenTween(buttonRoot, buttonRootCanvasGroup, buttonPivot, bgOpenDuration + (contentOpenInterval * 2f));

        float inputEnableTime = bgOpenDuration + (contentOpenInterval * 2f) + contentOpenDuration;
        openSequence.InsertCallback(inputEnableTime, EnableButtons);
    }

    private void CacheTargetSize()
    {
        if (warningBG != null && bgTargetWidth <= 0f)
            bgTargetWidth = warningBG.rect.width;
    }

    private void PrepareOpenState()
    {
        if (warningBG != null)
            SetWarningBGWidth(1f);

        SetCanvasGroupAlpha(warningBGCanvasGroup, 0f);
        SetCanvasGroupAlpha(mainTextCanvasGroup, 0f);
        SetCanvasGroupAlpha(subTextCanvasGroup, 0f);
        SetCanvasGroupAlpha(buttonRootCanvasGroup, 0f);

        SetContentToHiddenPosition(mainText, mainTextPivot);
        SetContentToHiddenPosition(subText, subTextPivot);
        SetContentToHiddenPosition(buttonRoot, buttonPivot);

        SetCanvasGroupRaycast(warningBGCanvasGroup, true);
        SetCanvasGroupRaycast(mainTextCanvasGroup, false);
        SetCanvasGroupRaycast(subTextCanvasGroup, false);
        SetCanvasGroupRaycast(buttonRootCanvasGroup, false);
    }

    private void SetContentToHiddenPosition(RectTransform target, RectTransform pivot)
    {
        if (target == null || pivot == null)
            return;

        target.localPosition = GetHiddenPosition(pivot);
    }

    private void InsertContentOpenTween(RectTransform target, CanvasGroup canvasGroup, RectTransform pivot, float startTime)
    {
        if (target != null && pivot != null)
        {
            target.localPosition = GetHiddenPosition(pivot);
            openSequence.Insert(startTime, target.DOLocalMove(pivot.localPosition, contentOpenDuration)
                .SetEase(productionEase));
        }

        if (canvasGroup != null)
        {
            openSequence.Insert(startTime, canvasGroup.DOFade(1f, contentOpenDuration)
                .SetEase(productionEase));
        }
    }

    private Vector3 GetHiddenPosition(RectTransform pivot)
    {
        return pivot.localPosition + new Vector3(0f, contentOpenYOffset, 0f);
    }

    private float GetWarningBGWidth()
    {
        return warningBG != null ? warningBG.rect.width : 0f;
    }

    private void SetWarningBGWidth(float width)
    {
        if (warningBG == null)
            return;

        warningBG.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private void PlayCloseProduction()
    {
        KillProductionSequences();
        isClosing = true;
        SetButtonsInteractable(false);
        SetCanvasGroupRaycast(warningBGCanvasGroup, true);
        SetCanvasGroupRaycast(buttonRootCanvasGroup, false);

        closeSequence = DOTween.Sequence().SetUpdate(true);

        if (warningBGCanvasGroup != null)
            closeSequence.Join(warningBGCanvasGroup.DOFade(0f, closeDuration).SetEase(productionEase));

        closeSequence.OnComplete(OnCloseProductionComplete);
    }

    private void OnCloseProductionComplete()
    {
        closeSequence = null;
        isClosing = false;
        base.Hide();
    }

    private void KillProductionSequences()
    {
        if (openSequence != null)
        {
            openSequence.Kill();
            openSequence = null;
        }

        if (closeSequence != null)
        {
            closeSequence.Kill();
            closeSequence = null;
        }

        isClosing = false;
    }

    private CanvasGroup GetOrAddCanvasGroup(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return null;

        CanvasGroup canvasGroup = rectTransform.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = rectTransform.gameObject.AddComponent<CanvasGroup>();

        return canvasGroup;
    }

    private void SetCanvasGroupAlpha(CanvasGroup canvasGroup, float alpha)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = alpha;
    }

    private void SetCanvasGroupRaycast(CanvasGroup canvasGroup, bool enabled)
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = enabled;
        canvasGroup.blocksRaycasts = enabled;
    }

    private void EnableButtons()
    {
        SetCanvasGroupRaycast(warningBGCanvasGroup, true);
        SetCanvasGroupRaycast(mainTextCanvasGroup, false);
        SetCanvasGroupRaycast(subTextCanvasGroup, false);
        SetCanvasGroupRaycast(buttonRootCanvasGroup, true);
        SetButtonsInteractable(true);
    }

    private void SetButtonsInteractable(bool enabled)
    {
        if (okButton != null)
            okButton.interactable = enabled;

        if (cancelButton != null)
            cancelButton.interactable = enabled;
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
