using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

public class AbilityToolTip : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform rootRectTransform;
    [SerializeField] private RectTransform backgroundRectTransform;
    [SerializeField] private TMP_Text titleAndLevelText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text valueText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image costIcon;
    [SerializeField] private Sprite coinCostIcon;
    [SerializeField] private Sprite carrotCostIcon;

    [Header("Motion References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    [SerializeField] private string showMotionTag = "ToolTipShow";
    [SerializeField] private string hideMotionTag = "ToolTipHide";
    [SerializeField] private string clickMotionTag = "ToolTipClick";
    [SerializeField] private string idleMotionTag = "ToolTipIdle";

    [Header("Idle Motion")]
    [SerializeField] private float idleStepDuration = 0.2f;
    [SerializeField] private float idleOffsetY = 1f;

    private MotionEntry showMotionEntry;
    private MotionEntry hideMotionEntry;
    private MotionEntry clickMotionEntry;
    private MotionEntry idleMotionEntry;
    private Sequence idleFallbackSequence;
    private Vector2 baseAnchoredPosition;
    private Vector2 baseMotionAnchoredPosition;
    private Vector3 baseLocalScale = Vector3.one;
    private bool hasCachedBaseLocalScale;
    private bool hasCachedMotionAnchoredPosition;
    private int motionVersion;
    private int showMotionVersion;
    private int clickMotionVersion;
    private int hideMotionVersion;

    public RectTransform RootRectTransform => rootRectTransform;
    public TMP_Text TitleAndLevelText => titleAndLevelText;
    public TMP_Text DescriptionText => descriptionText;
    public TMP_Text ValueText => valueText;
    public TMP_Text CostText => costText;

    public void SetContent(string _titleAndLevel, string _description, string _cost)
    {
        SetContent(_titleAndLevel, _description, string.Empty, _cost, MoneyType.None, false);
    }

    public void SetContent(string _titleAndLevel, string _description, string _value, string _cost)
    {
        SetContent(_titleAndLevel, _description, _value, _cost, MoneyType.None, false);
    }

    public void SetContent(string _titleAndLevel, string _description, string _cost, MoneyType _moneyType)
    {
        SetContent(_titleAndLevel, _description, string.Empty, _cost, _moneyType, true);
    }

    public void SetContent(string _titleAndLevel, string _description, string _value, string _cost, MoneyType _moneyType)
    {
        SetContent(_titleAndLevel, _description, _value, _cost, _moneyType, true);
    }

    private void SetContent(string _titleAndLevel, string _description, string _value, string _cost, MoneyType _moneyType, bool _useCurrencyIcon)
    {
        if (titleAndLevelText != null)
            titleAndLevelText.text = _titleAndLevel;

        if (descriptionText != null)
            descriptionText.text = _description;

        SetValue(_value);
        if (_useCurrencyIcon)
            SetCurrencyCost(_cost, _moneyType);
        else
            SetPlainCost(_cost);
    }

    private void SetValue(string _value)
    {
        if (valueText == null)
            return;

        valueText.text = _value ?? string.Empty;
        valueText.gameObject.SetActive(string.IsNullOrEmpty(valueText.text) == false);
    }

    private void SetPlainCost(string _cost)
    {
        if (costText != null)
            costText.text = _cost;

        SetCostIcon(null);
    }

    private void SetCurrencyCost(string _cost, MoneyType _moneyType)
    {
        if (costText != null)
            costText.text = _cost;

        SetCostIcon(GetCurrencyIcon(_moneyType));
    }

    private Sprite GetCurrencyIcon(MoneyType _moneyType)
    {
        switch (_moneyType)
        {
            case MoneyType.Coin:
                return coinCostIcon;
            case MoneyType.Carrot:
                return carrotCostIcon;
            default:
                return null;
        }
    }

    private void SetCostIcon(Sprite _sprite)
    {
        if (costIcon == null)
            return;

        bool hasIcon = _sprite != null;
        costIcon.gameObject.SetActive(hasIcon);
        costIcon.sprite = _sprite;
    }

    public RectTransform GetRoot()
    {
        return rootRectTransform;
    }

    public Vector2 GetSize()
    {
        RectTransform target = backgroundRectTransform != null ? backgroundRectTransform : rootRectTransform;
        if (target == null)
            return Vector2.zero;

        LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        return target.rect.size;
    }

    public void SetAnchoredPosition(Vector2 _anchoredPosition)
    {
        baseAnchoredPosition = _anchoredPosition;

        if (rootRectTransform == null)
            return;

        rootRectTransform.anchoredPosition = _anchoredPosition;
    }

    public void Show()
    {
        gameObject.SetActive(true);
        EnsureCanvasGroup();
        CacheMotionPlayer();

        if (backgroundRectTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRectTransform);
    }

    public void PlayShowMotion()
    {
        Show();

        if (PlayShowObjectMotion() == false)
            PlayIdleFromVisibleState();
    }

    public void PlayHideMotion()
    {
        if (gameObject.activeSelf == false)
            return;

        ++motionVersion;
        if (PlayHideObjectMotion())
            return;

        HideImmediately();
    }

    public void PlayClickMotion()
    {
        Show();

        if (PlayClickObjectMotion() == false)
            PlayIdleFromVisibleState();
    }

    public void HideImmediately()
    {
        ++motionVersion;
        StopObjectMotions();
        CacheBaseMotionState();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (rootRectTransform != null)
            rootRectTransform.anchoredPosition = baseAnchoredPosition;

        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionRectTransform != null)
        {
            motionRectTransform.anchoredPosition = baseMotionAnchoredPosition;
            motionRectTransform.localEulerAngles = Vector3.zero;
            motionRectTransform.localScale = baseLocalScale;
        }

        gameObject.SetActive(false);
    }

    public void Hide()
    {
        PlayHideMotion();
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup != null)
            return;

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void CacheMotionPlayer()
    {
        if (motionPlayer != null)
            return;

        motionPlayer = GetComponentInChildren<ObjectMotionPlayer>(true);
    }

    private RectTransform GetMotionRectTransform()
    {
        return backgroundRectTransform != null ? backgroundRectTransform : rootRectTransform;
    }

    private void RestoreVisibleState()
    {
        RestoreVisibleState(motionVersion);
    }

    private void RestoreVisibleState(int _version)
    {
        if (gameObject.activeSelf == false)
            return;

        if (_version != motionVersion)
            return;

        CacheBaseMotionState();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionRectTransform != null)
        {
            motionRectTransform.anchoredPosition = baseMotionAnchoredPosition;
            motionRectTransform.localEulerAngles = Vector3.zero;
            motionRectTransform.localScale = baseLocalScale;
        }
    }

    private void CompleteShowMotion()
    {
        CompleteShowMotion(showMotionVersion);
    }

    private void CompleteShowMotion(int _version)
    {
        RestoreVisibleState(_version);
        PlayIdleMotion(_version);
    }

    private void CompleteClickMotion()
    {
        CompleteClickMotion(clickMotionVersion);
    }

    private void CompleteClickMotion(int _version)
    {
        RestoreVisibleState(_version);
        PlayIdleMotion(_version);
    }

    private void CompleteHideMotion()
    {
        CompleteHideMotion(hideMotionVersion);
    }

    private void CompleteHideMotion(int _version)
    {
        CacheBaseMotionState();

        if (_version != motionVersion)
            return;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionRectTransform != null)
        {
            motionRectTransform.anchoredPosition = baseMotionAnchoredPosition;
            motionRectTransform.localEulerAngles = Vector3.zero;
            motionRectTransform.localScale = baseLocalScale;
        }

        gameObject.SetActive(false);
    }

    private bool PlayShowObjectMotion()
    {
        CacheMotionPlayer();

        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionPlayer == null || string.IsNullOrEmpty(showMotionTag) || motionRectTransform == null)
            return false;

        EnsureCanvasGroup();
        CacheBaseMotionState();

        int currentVersion = ++motionVersion;
        StopEntryMotion(hideMotionEntry);
        StopEntryMotion(clickMotionEntry);
        StopIdleMotion();
        StopEntryMotion(showMotionEntry);
        motionRectTransform.anchoredPosition = baseMotionAnchoredPosition;
        motionRectTransform.localEulerAngles = Vector3.zero;
        motionRectTransform.localScale = baseLocalScale;
        showMotionVersion = currentVersion;
        showMotionEntry = motionPlayer.Play(showMotionTag, _onComplete: CompleteShowMotion, bReset: false);

        if (showMotionEntry == null || showMotionEntry.motionInstance == null)
            PlayIdleMotion(currentVersion);

        return showMotionEntry != null && showMotionEntry.motionInstance != null;
    }

    private bool PlayHideObjectMotion()
    {
        CacheMotionPlayer();

        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionPlayer == null || string.IsNullOrEmpty(hideMotionTag) || motionRectTransform == null)
            return false;

        EnsureCanvasGroup();
        CacheBaseMotionState();
        canvasGroup.alpha = 1f;

        StopEntryMotion(showMotionEntry);
        StopEntryMotion(clickMotionEntry);
        StopIdleMotion();
        StopEntryMotion(hideMotionEntry);
        motionRectTransform.anchoredPosition = baseMotionAnchoredPosition;
        motionRectTransform.localEulerAngles = Vector3.zero;
        motionRectTransform.localScale = baseLocalScale;
        hideMotionVersion = motionVersion;
        hideMotionEntry = motionPlayer.Play(hideMotionTag, _onComplete: CompleteHideMotion, bReset: false);
        return hideMotionEntry != null && hideMotionEntry.motionInstance != null;
    }

    private bool PlayClickObjectMotion()
    {
        CacheMotionPlayer();

        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionPlayer == null || string.IsNullOrEmpty(clickMotionTag) || motionRectTransform == null)
            return false;

        EnsureCanvasGroup();
        CacheBaseMotionState();
        canvasGroup.alpha = 1f;

        int currentVersion = ++motionVersion;
        StopEntryMotion(showMotionEntry);
        StopEntryMotion(hideMotionEntry);
        StopIdleMotion();
        StopEntryMotion(clickMotionEntry);
        motionRectTransform.anchoredPosition = baseMotionAnchoredPosition;
        motionRectTransform.localEulerAngles = Vector3.zero;
        motionRectTransform.localScale = baseLocalScale;
        clickMotionVersion = currentVersion;
        clickMotionEntry = motionPlayer.Play(clickMotionTag, _onComplete: CompleteClickMotion, bReset: false);

        if (clickMotionEntry == null || clickMotionEntry.motionInstance == null)
            PlayIdleMotion(currentVersion);

        return clickMotionEntry != null && clickMotionEntry.motionInstance != null;
    }

    private void PlayIdleFromVisibleState()
    {
        int currentVersion = ++motionVersion;
        StopObjectMotions();
        CacheBaseMotionState();
        RestoreVisibleState(currentVersion);
        PlayIdleMotion(currentVersion);
    }

    private void PlayIdleMotion(int _version)
    {
        if (_version != motionVersion || gameObject.activeSelf == false)
            return;

        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionRectTransform == null)
            return;

        StopEntryMotion(showMotionEntry);
        StopEntryMotion(clickMotionEntry);
        StopIdleMotion();
        motionRectTransform.anchoredPosition = SnapPixel(baseMotionAnchoredPosition);
        motionRectTransform.localEulerAngles = Vector3.zero;
        motionRectTransform.localScale = baseLocalScale;

        if (motionPlayer != null && string.IsNullOrEmpty(idleMotionTag) == false)
        {
            idleMotionEntry = motionPlayer.Play(idleMotionTag, bReset: false);
            if (idleMotionEntry != null && idleMotionEntry.motionInstance != null)
                return;
        }

        PlayFallbackIdleMotion();
    }

    private void PlayFallbackIdleMotion()
    {
        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionRectTransform == null)
            return;

        idleFallbackSequence = DOTween.Sequence();
        idleFallbackSequence.AppendCallback(SetIdlePlusOffset);
        idleFallbackSequence.AppendInterval(GetIdleStepDuration());
        idleFallbackSequence.AppendCallback(SetIdleBaseOffset);
        idleFallbackSequence.AppendInterval(GetIdleStepDuration());
        idleFallbackSequence.AppendCallback(SetIdleMinusOffset);
        idleFallbackSequence.AppendInterval(GetIdleStepDuration());
        idleFallbackSequence.AppendCallback(SetIdleBaseOffset);
        idleFallbackSequence.AppendInterval(GetIdleStepDuration());
        idleFallbackSequence.SetLoops(-1, LoopType.Restart);
    }

    private void StopIdleMotion()
    {
        StopEntryMotion(idleMotionEntry);
        idleMotionEntry = null;

        if (idleFallbackSequence != null)
        {
            idleFallbackSequence.Kill(false);
            idleFallbackSequence = null;
        }
    }

    private void SetIdlePlusOffset()
    {
        SetIdleOffset(idleOffsetY);
    }

    private void SetIdleBaseOffset()
    {
        SetIdleOffset(0f);
    }

    private void SetIdleMinusOffset()
    {
        SetIdleOffset(-idleOffsetY);
    }

    private void SetIdleOffset(float _offsetY)
    {
        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionRectTransform == null || gameObject.activeSelf == false)
            return;

        Vector2 targetPosition = baseMotionAnchoredPosition + Vector2.up * _offsetY;
        motionRectTransform.anchoredPosition = SnapPixel(targetPosition);
    }

    private void StopEntryMotion(MotionEntry _entry)
    {
        if (motionPlayer == null || _entry == null || _entry.motionInstance == null)
            return;

        motionPlayer.SettingEntryMotion(_entry, true, false);
    }

    private void StopObjectMotions()
    {
        StopEntryMotion(showMotionEntry);
        StopEntryMotion(hideMotionEntry);
        StopEntryMotion(clickMotionEntry);
        StopIdleMotion();
    }

    private void CacheBaseMotionState()
    {
        RectTransform motionRectTransform = GetMotionRectTransform();
        if (motionRectTransform == null)
            return;

        if (hasCachedMotionAnchoredPosition == false)
        {
            baseMotionAnchoredPosition = motionRectTransform.anchoredPosition;
            hasCachedMotionAnchoredPosition = true;
        }

        if (hasCachedBaseLocalScale)
            return;

        baseLocalScale = motionRectTransform.localScale;
        hasCachedBaseLocalScale = true;
    }

    private void OnDestroy()
    {
        StopObjectMotions();
    }

    private float GetIdleStepDuration()
    {
        return Mathf.Max(idleStepDuration, 0.0001f);
    }

    private Vector2 SnapPixel(Vector2 _position)
    {
        return new Vector2(Mathf.Round(_position.x), Mathf.Round(_position.y));
    }
}
