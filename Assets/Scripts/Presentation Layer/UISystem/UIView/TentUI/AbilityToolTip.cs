using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityToolTip : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform rootRectTransform;
    [SerializeField] private RectTransform backgroundRectTransform;
    [SerializeField] private TMP_Text titleAndLevelText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text costText;
    [SerializeField] private Image costIcon;
    [SerializeField] private Sprite coinCostIcon;
    [SerializeField] private Sprite carrotCostIcon;

    [Header("Motion Settings")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float showDuration = 0.7f;
    [SerializeField] private float hideDuration = 0.18f;
    [SerializeField] private float showStartOffsetY = -12f;
    [SerializeField] private float hideEndOffsetY = -10f;
    [SerializeField] private float showStartAngle = 12f;
    [SerializeField] private float showAngleDamping = 0.62f;
    [SerializeField] private int showSwingCount = 2;
    [SerializeField] private Ease showMoveEase = Ease.OutQuad;
    [SerializeField] private Ease showRotationEase = Ease.OutSine;
    [SerializeField] private Ease hideEase = Ease.InQuad;

    private Tween motionTween;
    private Vector2 baseAnchoredPosition;

    public RectTransform RootRectTransform => rootRectTransform;
    public TMP_Text TitleAndLevelText => titleAndLevelText;
    public TMP_Text DescriptionText => descriptionText;
    public TMP_Text CostText => costText;

    public void SetContent(string _titleAndLevel, string _description, string _cost)
    {
        if (titleAndLevelText != null)
            titleAndLevelText.text = _titleAndLevel;

        if (descriptionText != null)
            descriptionText.text = _description;

        SetPlainCost(_cost);
    }

    public void SetContent(string _titleAndLevel, string _description, string _cost, MoneyType _moneyType)
    {
        if (titleAndLevelText != null)
            titleAndLevelText.text = _titleAndLevel;

        if (descriptionText != null)
            descriptionText.text = _description;

        SetCurrencyCost(_cost, _moneyType);
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

        if (backgroundRectTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRectTransform);
    }

    public void PlayShowMotion()
    {
        Show();
        StopMotion();

        if (rootRectTransform == null)
            return;

        EnsureCanvasGroup();
        canvasGroup.alpha = 0f;
        rootRectTransform.anchoredPosition = baseAnchoredPosition + Vector2.up * showStartOffsetY;
        rootRectTransform.localEulerAngles = Vector3.zero;

        Sequence sequence = DOTween.Sequence();
        sequence.Join(canvasGroup.DOFade(1f, showDuration).SetEase(showMoveEase));
        sequence.Join(rootRectTransform.DOAnchorPos(baseAnchoredPosition, showDuration).SetEase(showMoveEase));
        sequence.Join(BuildShowRotationTween());
        sequence.OnComplete(RestoreVisibleState);
        motionTween = sequence;
    }

    public void PlayHideMotion()
    {
        if (gameObject.activeSelf == false)
            return;

        StopMotion();

        if (rootRectTransform == null)
        {
            HideImmediately();
            return;
        }

        EnsureCanvasGroup();
        rootRectTransform.localEulerAngles = Vector3.zero;

        Sequence sequence = DOTween.Sequence();
        sequence.Join(canvasGroup.DOFade(0f, hideDuration).SetEase(hideEase));
        sequence.Join(rootRectTransform.DOAnchorPos(baseAnchoredPosition + Vector2.up * hideEndOffsetY, hideDuration).SetEase(hideEase));
        sequence.OnComplete(CompleteHideMotion);
        motionTween = sequence;
    }

    public void HideImmediately()
    {
        StopMotion();

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (rootRectTransform != null)
        {
            rootRectTransform.anchoredPosition = baseAnchoredPosition;
            rootRectTransform.localEulerAngles = Vector3.zero;
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

    private Tween BuildShowRotationTween()
    {
        Sequence sequence = DOTween.Sequence();
        float angle = Mathf.Abs(showStartAngle);
        int swingCount = Mathf.Max(showSwingCount, 1);
        float rotationDuration = showDuration;
        float swingDuration = rotationDuration / (swingCount + 1);

        for (int i = 0; i < swingCount; i++)
        {
            float direction = i % 2 == 0 ? -1f : 1f;
            Vector3 targetRotation = Vector3.forward * angle * direction;
            sequence.Append(rootRectTransform.DOLocalRotate(targetRotation, swingDuration, RotateMode.Fast).SetEase(showRotationEase));
            angle *= Mathf.Clamp01(showAngleDamping);
        }

        sequence.Append(rootRectTransform.DOLocalRotate(Vector3.zero, swingDuration, RotateMode.Fast).SetEase(showRotationEase));
        return sequence;
    }

    private void RestoreVisibleState()
    {
        if (gameObject.activeSelf == false)
            return;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (rootRectTransform != null)
        {
            rootRectTransform.anchoredPosition = baseAnchoredPosition;
            rootRectTransform.localEulerAngles = Vector3.zero;
        }
    }

    private void CompleteHideMotion()
    {
        motionTween = null;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (rootRectTransform != null)
        {
            rootRectTransform.anchoredPosition = baseAnchoredPosition;
            rootRectTransform.localEulerAngles = Vector3.zero;
        }

        gameObject.SetActive(false);
    }

    private void StopMotion()
    {
        if (motionTween != null && motionTween.IsActive())
            motionTween.Kill(false);

        motionTween = null;
    }

    private void OnDestroy()
    {
        StopMotion();
    }
}
