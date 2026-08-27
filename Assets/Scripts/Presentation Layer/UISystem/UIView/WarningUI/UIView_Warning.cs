using System;
using System.Collections.Generic;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIView_Warning : UIView
{
    private const int WarningLocalizationJsonId = 4;
    private const int MainTextEntryId = 1;
    private const int SubTextEntryId = 2;
    private const float HiddenBGWidth = 0f;
    private const float WarningBGTargetAlpha = 0.95f;

    public event Action DeActivateWarningUIEvent;

    [Header("UI References")]
    [SerializeField] private RectTransform warningBG;
    [SerializeField] private RectTransform mainText;
    [SerializeField] private RectTransform subText;
    [SerializeField] private RectTransform buttonRoot;
    [SerializeField] private Button okButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private RectTransform okButtonTouchArea;
    [SerializeField] private RectTransform cancelButtonTouchArea;
    [SerializeField] private UISelectionCursor selectionCursorPrefab;
    [SerializeField] private RectTransform selectionCursorParent;
    [SerializeField] private Vector2 selectionCursorSize = new Vector2(40f, 40f);

    [Header("Position Pivots")]
    [SerializeField] private RectTransform mainTextPivot;
    [SerializeField] private RectTransform subTextPivot;
    [SerializeField] private RectTransform buttonPivot;
    [SerializeField] private RectTransform soloTextPivot;

    [Header("Production Settings")]
    [SerializeField] private float bgOpenDuration = 0.25f;
    [SerializeField] private float bgTargetWidth = 700f;
    [SerializeField] private float bgPieceOpenDelay = 0.04f;
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
    private CanvasGroup[] closeFadeCanvasGroups = Array.Empty<CanvasGroup>();
    private TMP_Text mainTMPText;
    private TMP_Text subTMPText;
    private WarningBGPiece[] warningBGPieces = Array.Empty<WarningBGPiece>();
    private UISelectionCursor selectionCursorInstance;
    private UIHoverSelectionTarget okHoverTarget;
    private UIHoverSelectionTarget cancelHoverTarget;
    private Button okTouchAreaButton;
    private Button cancelTouchAreaButton;
    private RectTransform okButtonVisual;
    private RectTransform cancelButtonVisual;
    private LocalizationManager localizationManager;
    private InputManager inputManager;
    private Action cachedOnUICancel;
    private Action<EInputDeviceType> cachedOnInputDeviceChanged;
    private bool isClosing;
    private bool playSoundsForCurrentPresentation;

    private sealed class WarningBGPiece
    {
        public RectTransform rectTransform;
        public Graphic graphic;
        public float targetWidth;
        public float targetAlpha;
        public float delay;
    }

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        localizationManager = _ctx?.localizationManager;
        inputManager = _ctx?.inputManager;
        cachedOnUICancel ??= OnUICancelPressed;
        cachedOnInputDeviceChanged ??= OnInputDeviceChanged;

        if (localizationManager != null)
            localizationManager.OnLanguageChanged += RefreshLocalizedTexts;

        if (null != inputManager?.inputReader)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
            inputManager.inputReader.InputDeviceChangedEvent += cachedOnInputDeviceChanged;
        }

        CacheCanvasGroups();
        CacheTextReferences();
        CacheButtonTouchAreas();
        ConfigureButtonNavigation();
        RefreshLocalizedTexts();
        InitializeButtonHoverTargets();
        BindButtonEvents();
    }

    public void ShowWarning()
    {
        playSoundsForCurrentPresentation = true;

        if (null != inputManager?.inputReader && null != cachedOnUICancel)
        {
            inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
            inputManager.inputReader.UICancelEvent += cachedOnUICancel;
        }

        Show();
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

        // 설치 시점(GameplayUIInstaller)에도 UIManager.Open()이 Show()를 부르지만, 그때의 Hide는
        // 닫기 연출을 거치는 비동기라 OnHide가 몇 프레임 뒤에 온다. 그 사이 오디오가 먹먹해지는 걸
        // 막기 위해 실제 경고창으로 열린 경우(ShowWarning)에만 오디오를 건드린다.
        if (playSoundsForCurrentPresentation)
        {
            Sound.PlayUI(SoundID.ResultUIOpen);
            Sound.RequestAudioDuck();
        }

        PlayOpenProduction();
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);

        // OnShow에서 요청했을 때만 짝을 맞춰 해제한다(playSoundsForCurrentPresentation은 아래에서
        // 꺼지므로 반드시 그 전에 확인해야 한다).
        if (playSoundsForCurrentPresentation)
            Sound.ReleaseAudioDuck();

        if (null != inputManager?.inputReader && null != cachedOnUICancel)
            inputManager.inputReader.UICancelEvent -= cachedOnUICancel;

        DeActivateWarningUI();
        bApproved = false;
        playSoundsForCurrentPresentation = false;
    }

    public override void OnDestroy()
    {
        KillProductionSequences();
        UnbindButtonEvents();

        if (localizationManager != null)
            localizationManager.OnLanguageChanged -= RefreshLocalizedTexts;

        if (null != inputManager?.inputReader && null != cachedOnInputDeviceChanged)
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;

        if (null != inputManager?.inputReader && null != cachedOnUICancel)
            inputManager.inputReader.UICancelEvent -= cachedOnUICancel;

        DeActivateWarningUIEvent = null;
        cachedOnUICancel = null;
        cachedOnInputDeviceChanged = null;
        inputManager = null;
        base.OnDestroy();
    }

    public void OnOKButtonClicked()
    {
        if (true == isClosing)
            return;

        HideSelectionCursorImmediately();
        bApproved = true;

        if (playSoundsForCurrentPresentation)
            Sound.PlayUI(SoundID.MainClick);

        Hide();
    }

    public void OnCancelButtonClicked()
    {
        if (true == isClosing)
            return;

        HideSelectionCursorImmediately();
        bApproved = false;

        if (playSoundsForCurrentPresentation)
            Sound.PlayUI(SoundID.MainClick);

        Hide();
    }

    private void CacheCanvasGroups()
    {
        if (warningBG == null)
            warningBG = FindChildRecursive(transform, "BGRoot") as RectTransform;

        warningBGCanvasGroup = GetOrAddCanvasGroup(warningBG);
        mainTextCanvasGroup = GetOrAddCanvasGroup(mainText);
        subTextCanvasGroup = GetOrAddCanvasGroup(subText);
        buttonRootCanvasGroup = GetOrAddCanvasGroup(buttonRoot);
        closeFadeCanvasGroups = new[] { warningBGCanvasGroup, mainTextCanvasGroup, subTextCanvasGroup, buttonRootCanvasGroup };
        CacheWarningBGPieces();
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
        AssignRandomWarningBGDelays();
        float bgProductionDuration = GetWarningBGProductionDuration();
        float subTextStartTime = bgProductionDuration + contentOpenInterval;
        float buttonStartTime = bgProductionDuration + (contentOpenInterval * 2f);

        PrepareOpenState(true, mainTextPivot);
        SetButtonInputEnabled(false);

        openSequence = DOTween.Sequence().SetUpdate(true);

        if (warningBGPieces.Length > 0)
        {
            SetCanvasGroupAlpha(warningBGCanvasGroup, 1f);
            InsertWarningBGOpenTweens();
        }
        else if (warningBG != null)
        {
            openSequence.Join(DOTween.To(GetWarningBGWidth, SetWarningBGWidth, bgTargetWidth, bgOpenDuration).SetEase(Ease.OutCubic));

            if (warningBGCanvasGroup != null)
                openSequence.Join(warningBGCanvasGroup.DOFade(1f, bgOpenDuration).SetEase(productionEase));
        }

        InsertContentOpenTween(mainText, mainTextCanvasGroup, mainTextPivot, bgProductionDuration);
        InsertContentOpenTween(subText, subTextCanvasGroup, subTextPivot, subTextStartTime);

        InsertContentOpenTween(buttonRoot, buttonRootCanvasGroup, buttonPivot, buttonStartTime);

        if (playSoundsForCurrentPresentation)
        {
            openSequence.InsertCallback(bgProductionDuration, () => Sound.PlayUI(SoundID.MainMenuDot02));
            openSequence.InsertCallback(subTextStartTime, () => Sound.PlayUI(SoundID.MainMenuDot03));
            openSequence.InsertCallback(buttonStartTime, () => Sound.PlayUI(SoundID.MainMenuDot04));
        }

        openSequence.InsertCallback(buttonStartTime, EnableButtons);
    }

    private void CacheTargetSize()
    {
        if (warningBGPieces.Length > 0 && bgTargetWidth <= 0f)
            bgTargetWidth = warningBGPieces[0].targetWidth;

        if (warningBG != null && bgTargetWidth <= 0f)
            bgTargetWidth = warningBG.rect.width;
    }

    private void PrepareOpenState(bool showSubText, RectTransform mainTargetPivot)
    {
        if (warningBGPieces.Length > 0)
            SetWarningBGPiecesHidden();
        else if (warningBG != null)
            SetWarningBGWidth(HiddenBGWidth);

        SetSubTextActive(showSubText);
        SetCanvasGroupAlpha(warningBGCanvasGroup, 0f);
        SetCanvasGroupAlpha(mainTextCanvasGroup, 0f);
        SetCanvasGroupAlpha(subTextCanvasGroup, 0f);
        SetCanvasGroupAlpha(buttonRootCanvasGroup, 0f);

        SetContentToHiddenPosition(mainText, mainTargetPivot);

        if (showSubText)
            SetContentToHiddenPosition(subText, subTextPivot);

        SetContentToHiddenPosition(buttonRoot, buttonPivot);

        SetCanvasGroupRaycast(warningBGCanvasGroup, true);
        SetCanvasGroupRaycast(mainTextCanvasGroup, false);
        SetCanvasGroupRaycast(subTextCanvasGroup, false);
        SetButtonInputEnabled(false);
    }

    private void SetContentToHiddenPosition(RectTransform target, RectTransform pivot)
    {
        if (target == null || pivot == null)
            return;

        target.localPosition = GetHiddenPosition(pivot);
    }

    private void SetSubTextActive(bool active)
    {
        if (subText != null && subText.gameObject.activeSelf != active)
            subText.gameObject.SetActive(active);
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

    private void CacheWarningBGPieces()
    {
        warningBGPieces = Array.Empty<WarningBGPiece>();

        if (warningBG == null)
            return;

        RectTransform[] rectTransforms = warningBG.GetComponentsInChildren<RectTransform>(true);
        List<WarningBGPiece> pieces = new List<WarningBGPiece>();

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform == warningBG || rectTransform.name.StartsWith("BG_", StringComparison.Ordinal) == false)
                continue;

            Graphic graphic = rectTransform.GetComponent<Graphic>();
            if (graphic == null)
                continue;

            pieces.Add(new WarningBGPiece
            {
                rectTransform = rectTransform,
                graphic = graphic,
                targetWidth = rectTransform.rect.width,
                targetAlpha = WarningBGTargetAlpha
            });
        }

        pieces.Sort((left, right) => string.CompareOrdinal(left.rectTransform.name, right.rectTransform.name));

        warningBGPieces = pieces.ToArray();
    }

    private float GetWarningBGProductionDuration()
    {
        if (warningBGPieces.Length <= 0)
            return bgOpenDuration;

        float lastDelay = 0f;
        for (int i = 0; i < warningBGPieces.Length; i++)
            lastDelay = Mathf.Max(lastDelay, warningBGPieces[i].delay);

        return bgOpenDuration + lastDelay;
    }

    private void InsertWarningBGOpenTweens()
    {
        if (warningBGPieces.Length > 0)
        {
            openSequence.Insert(0f, DOTween.To(
                () => GetWarningBGPiecesAlpha(),
                SetWarningBGPiecesAlpha,
                1f,
                bgOpenDuration).SetEase(Ease.Linear));
        }

        for (int i = 0; i < warningBGPieces.Length; i++)
        {
            WarningBGPiece piece = warningBGPieces[i];
            float targetWidth = bgTargetWidth > 0f ? bgTargetWidth : piece.targetWidth;

            openSequence.Insert(piece.delay, DOTween.To(
                () => GetWarningBGPieceWidth(piece),
                width => SetWarningBGPieceWidth(piece, width),
                targetWidth,
                bgOpenDuration).SetEase(Ease.OutCubic));
        }
    }

    private void AssignRandomWarningBGDelays()
    {
        if (warningBGPieces.Length <= 0)
            return;

        float[] delays = new float[warningBGPieces.Length];
        for (int i = 0; i < delays.Length; i++)
            delays[i] = i * bgPieceOpenDelay;

        for (int i = delays.Length - 1; i > 0; i--)
        {
            int randomIndex = UnityEngine.Random.Range(0, i + 1);
            float temp = delays[i];
            delays[i] = delays[randomIndex];
            delays[randomIndex] = temp;
        }

        for (int i = 0; i < warningBGPieces.Length; i++)
            warningBGPieces[i].delay = delays[i];
    }

    private void SetWarningBGPiecesHidden()
    {
        for (int i = 0; i < warningBGPieces.Length; i++)
        {
            SetWarningBGPieceWidth(warningBGPieces[i], HiddenBGWidth);
            SetGraphicAlpha(warningBGPieces[i].graphic, 0f);
        }
    }

    private float GetWarningBGPieceWidth(WarningBGPiece piece)
    {
        return piece?.rectTransform != null ? piece.rectTransform.rect.width : 0f;
    }

    private void SetWarningBGPieceWidth(WarningBGPiece piece, float width)
    {
        if (piece?.rectTransform == null)
            return;

        piece.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
    }

    private float GetWarningBGPiecesAlpha()
    {
        if (warningBGPieces.Length <= 0 || warningBGPieces[0].graphic == null)
            return 0f;

        float targetAlpha = Mathf.Max(warningBGPieces[0].targetAlpha, 0.0001f);
        return Mathf.Clamp01(warningBGPieces[0].graphic.color.a / targetAlpha);
    }

    private void SetWarningBGPiecesAlpha(float ratio)
    {
        for (int i = 0; i < warningBGPieces.Length; i++)
            SetGraphicAlpha(warningBGPieces[i].graphic, warningBGPieces[i].targetAlpha * ratio);
    }

    private void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
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

        if (playSoundsForCurrentPresentation)
            Sound.PlayUI(SoundID.ResultUIClose);

        ClearButtonSelection();
        HideSelectionCursorImmediately();
        SetButtonInputEnabled(false);
        SetCanvasGroupRaycast(warningBGCanvasGroup, true);

        closeSequence = DOTween.Sequence().SetUpdate(true);

        for (int i = 0; i < closeFadeCanvasGroups.Length; i++)
        {
            CanvasGroup canvasGroup = closeFadeCanvasGroups[i];
            if (canvasGroup != null)
                closeSequence.Join(canvasGroup.DOFade(0f, closeDuration).SetEase(productionEase));
        }

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
        SetButtonInputEnabled(true);

        if (true == IsGamepadMode())
            SelectDefaultButton();
        else
            RefreshButtonHoverTargets();
    }

    private void SetButtonInputEnabled(bool enabled)
    {
        SetCanvasGroupRaycast(buttonRootCanvasGroup, enabled);
        SetButtonsInteractable(enabled);
        SetHoverTargetsEnabled(enabled);

        if (enabled == false)
            HideSelectionCursorImmediately();
    }

    private void SetButtonsInteractable(bool enabled)
    {
        if (okTouchAreaButton != null)
            okTouchAreaButton.interactable = enabled;

        if (cancelTouchAreaButton != null)
            cancelTouchAreaButton.interactable = enabled;

        if (okButton != null)
            okButton.interactable = enabled;

        if (cancelButton != null)
            cancelButton.interactable = enabled;
    }

    private void SetHoverTargetsEnabled(bool enabled)
    {
        if (okHoverTarget != null)
            okHoverTarget.enabled = enabled;

        if (cancelHoverTarget != null)
            cancelHoverTarget.enabled = enabled;
    }

    private void RefreshButtonHoverTargets()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        Vector2 screenPosition = mouse.position.ReadValue();
        Camera eventCamera = GetUICamera();

        if (okHoverTarget != null)
            okHoverTarget.RefreshHover(screenPosition, eventCamera);

        if (cancelHoverTarget != null)
            cancelHoverTarget.RefreshHover(screenPosition, eventCamera);
    }

    private Camera GetUICamera()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
    }

    private void CacheButtonTouchAreas()
    {
        if (okButtonTouchArea == null)
            okButtonTouchArea = FindChildRecursive(transform, "Button_OK_TouchArea") as RectTransform;

        if (cancelButtonTouchArea == null)
            cancelButtonTouchArea = FindChildRecursive(transform, "Button_Cancel_TouchArea") as RectTransform;

        okButtonVisual = GetButtonVisual(okButton, "Button_OK");
        cancelButtonVisual = GetButtonVisual(cancelButton, "Button_Cancel");
        okTouchAreaButton = EnsureTouchAreaButton(okButtonTouchArea);
        cancelTouchAreaButton = EnsureTouchAreaButton(cancelButtonTouchArea);

        SetButtonVisualRaycastTarget(okButtonVisual, false);
        SetButtonVisualRaycastTarget(cancelButtonVisual, false);
    }

    private RectTransform GetButtonVisual(Button button, string visualName)
    {
        if (button != null)
            return button.transform as RectTransform;

        RectTransform visual = FindChildRecursive(transform, visualName) as RectTransform;
        Button visualButton = visual != null ? visual.GetComponent<Button>() : null;

        if (visualName == "Button_OK")
            okButton = visualButton;
        else if (visualName == "Button_Cancel")
            cancelButton = visualButton;

        return visual;
    }

    private Button EnsureTouchAreaButton(RectTransform touchArea)
    {
        if (touchArea == null)
            return null;

        Image touchImage = touchArea.GetComponent<Image>();
        if (touchImage != null)
            touchImage.raycastTarget = true;

        Button button = touchArea.GetComponent<Button>();
        if (button == null)
            button = touchArea.gameObject.AddComponent<Button>();

        button.targetGraphic = touchImage;
        button.transition = Selectable.Transition.None;
        return button;
    }

    private void SetButtonVisualRaycastTarget(RectTransform visual, bool enabled)
    {
        if (visual == null)
            return;

        Graphic[] graphics = visual.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = enabled;
    }

    private void InitializeButtonHoverTargets()
    {
        EnsureSelectionCursorInstance();

        okHoverTarget = InitializeHoverTarget(okButtonTouchArea, okButtonVisual);
        cancelHoverTarget = InitializeHoverTarget(cancelButtonTouchArea, cancelButtonVisual);
    }

    private UIHoverSelectionTarget InitializeHoverTarget(RectTransform touchArea, RectTransform visual)
    {
        if (touchArea == null)
            return null;

        UIHoverSelectionTarget hoverTarget = touchArea.GetComponent<UIHoverSelectionTarget>();
        if (hoverTarget == null)
            hoverTarget = touchArea.gameObject.AddComponent<UIHoverSelectionTarget>();

        ObjectMotionPlayer motionPlayer = visual != null ? visual.GetComponentInChildren<ObjectMotionPlayer>(true) : null;
        hoverTarget.Initialize(selectionCursorInstance, touchArea, motionPlayer, IsGamepadMode);
        return hoverTarget;
    }

    private bool IsGamepadMode()
    {
        return null != inputManager && true == inputManager.IsGamepadMode;
    }

    private void ConfigureButtonNavigation()
    {
        ConfigureHorizontalNavigation(okTouchAreaButton, cancelTouchAreaButton);
        ConfigureHorizontalNavigation(cancelTouchAreaButton, okTouchAreaButton);
    }

    private static void ConfigureHorizontalNavigation(Selectable source, Selectable other)
    {
        if (null == source)
            return;

        Selectable horizontalTarget = null != other ? other : source;
        source.navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = horizontalTarget,
            selectOnRight = horizontalTarget,
            selectOnUp = source,
            selectOnDown = source
        };
    }

    private void SelectDefaultButton()
    {
        if (false == IsVisible || true == isClosing || null == okTouchAreaButton ||
            false == okTouchAreaButton.interactable || false == okTouchAreaButton.gameObject.activeInHierarchy)
            return;

        if (null != EventSystem.current)
        {
            if (EventSystem.current.currentSelectedGameObject == okTouchAreaButton.gameObject)
                okHoverTarget?.ForceSelect();
            else
                EventSystem.current.SetSelectedGameObject(okTouchAreaButton.gameObject);
        }
        else
        {
            okHoverTarget?.ForceSelect();
        }
    }

    private void OnInputDeviceChanged(EInputDeviceType device)
    {
        if (false == IsVisible || true == isClosing || false == gameObject.activeInHierarchy)
            return;

        if (EInputDeviceType.Gamepad == device)
        {
            SelectDefaultButton();
            return;
        }

        if (EInputDeviceType.KeyboardMouse == device)
        {
            ClearButtonSelection();
            RefreshButtonHoverTargets();
        }
    }

    private void OnUICancelPressed()
    {
        if (false == IsVisible || true == isClosing || false == gameObject.activeInHierarchy)
            return;

        OnCancelButtonClicked();
    }

    private void ClearButtonSelection()
    {
        if (null == EventSystem.current)
            return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if ((null != okTouchAreaButton && selected == okTouchAreaButton.gameObject) ||
            (null != cancelTouchAreaButton && selected == cancelTouchAreaButton.gameObject))
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void EnsureSelectionCursorInstance()
    {
        if (selectionCursorInstance != null || selectionCursorPrefab == null)
            return;

        RectTransform parent = selectionCursorParent != null ? selectionCursorParent : transform as RectTransform;
        if (parent == null)
            return;

        selectionCursorInstance = Instantiate(selectionCursorPrefab, parent);
        selectionCursorInstance.Initialize(selectionCursorSize);
    }

    private void HideSelectionCursorImmediately()
    {
        if (selectionCursorInstance != null)
            selectionCursorInstance.HideImmediately();

        if (okHoverTarget != null)
            okHoverTarget.HideCursorImmediately();

        if (cancelHoverTarget != null)
            cancelHoverTarget.HideCursorImmediately();
    }

    private void BindButtonEvents()
    {
        if (okTouchAreaButton != null)
            okTouchAreaButton.onClick.AddListener(OnOKButtonClicked);

        if (cancelTouchAreaButton != null)
            cancelTouchAreaButton.onClick.AddListener(OnCancelButtonClicked);

        if (okHoverTarget != null)
            okHoverTarget.PointerEnteredEvent += OnWarningButtonHovered;

        if (cancelHoverTarget != null)
            cancelHoverTarget.PointerEnteredEvent += OnWarningButtonHovered;
    }

    private void UnbindButtonEvents()
    {
        if (okTouchAreaButton != null)
            okTouchAreaButton.onClick.RemoveListener(OnOKButtonClicked);

        if (cancelTouchAreaButton != null)
            cancelTouchAreaButton.onClick.RemoveListener(OnCancelButtonClicked);

        if (okHoverTarget != null)
            okHoverTarget.PointerEnteredEvent -= OnWarningButtonHovered;

        if (cancelHoverTarget != null)
            cancelHoverTarget.PointerEnteredEvent -= OnWarningButtonHovered;
    }

    private void OnWarningButtonHovered()
    {
        if (playSoundsForCurrentPresentation)
            Sound.PlayUI(SoundID.ResultUIHover);
    }

    private void DeActivateWarningUI()
    {
        DeActivateWarningUIEvent?.Invoke();
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
                return child;

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
                return result;
        }

        return null;
    }
}
