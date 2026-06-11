using System.Collections;
using System.Collections.Generic;
using PresentationLayer.UISystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD_Message : MonoBehaviour
{
    private const float HiddenWidth = 1f;

    private class BgPiece
    {
        public RectTransform rectTransform;
        public Graphic graphic;
        public float targetWidth;
        public float targetAlpha;
        public float delay;
    }

    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;
    [SerializeField] private TMPInlineStyleAnimator titleAnimator;
    [SerializeField] private TMPInlineStyleAnimator subAnimator;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Animation")]
    [SerializeField] private float showDelay = 1f;
    [SerializeField] private float bgExpandDuration = 0.25f;
    [SerializeField] private float titleRevealDelay = 0.125f;
    [SerializeField] private float holdDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 0.8f;

    private LocalizationManager localizationManager;
    private ForestType forestType = ForestType.None;
    private DungeonState dungeonState = DungeonState.None;
    private Coroutine animationCoroutine;
    private Graphic[] graphicTargets;
    private BgPiece[] bgPieces;

    public void Initialize(LocalizationManager _localizationManager)
    {
        localizationManager = _localizationManager;
        CacheReferences();
        RefreshTexts();

        if (null != localizationManager)
        {
            localizationManager.OnLanguageChanged -= RefreshTexts;
            localizationManager.OnLanguageChanged += RefreshTexts;
        }

        Hide();
    }

    public void Release()
    {
        if (null != localizationManager)
            localizationManager.OnLanguageChanged -= RefreshTexts;
    }

    public void SetMessage(ForestType _forestType, DungeonState _dungeonState)
    {
        forestType = _forestType;
        dungeonState = _dungeonState;

        RefreshTexts();
    }

    public void Play()
    {
        RefreshTexts();
        gameObject.SetActive(true);

        if (null != animationCoroutine)
            StopCoroutine(animationCoroutine);

        animationCoroutine = StartCoroutine(PlayAnimation());
    }

    public void Hide()
    {
        if (null != animationCoroutine)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        SetupHiddenState();
        gameObject.SetActive(false);
    }

    private IEnumerator PlayAnimation()
    {
        SetupHiddenState();
        gameObject.SetActive(true);

        if (0f < showDelay)
            yield return new WaitForSeconds(showDelay);

        yield return PlayShowAnimation();

        if (0f < holdDuration)
            yield return new WaitForSeconds(holdDuration);

        yield return PlayHideAnimation();

        animationCoroutine = null;
        gameObject.SetActive(false);
    }

    private IEnumerator PlayShowAnimation()
    {
        bool titleRevealed = false;
        float lastBgDelay = AssignRandomBgDelays();
        float totalBgDuration = lastBgDelay + bgExpandDuration;
        float elapsed = 0f;

        while (elapsed < totalBgDuration)
        {
            elapsed += Time.deltaTime;
            UpdateBgPieces(elapsed);

            if (false == titleRevealed && elapsed >= lastBgDelay + titleRevealDelay)
            {
                titleRevealed = true;
                SetGraphicAlpha(titleText, 1f);
                titleAnimator?.PlayRevealBounce();
            }

            yield return null;
        }

        SetBgPiecesComplete();

        if (false == titleRevealed)
        {
            SetGraphicAlpha(titleText, 1f);
            titleAnimator?.PlayRevealBounce();
        }

        SetGraphicAlpha(subText, 1f);
        subAnimator?.PlayRevealBounce();
    }

    private IEnumerator PlayHideAnimation()
    {
        if (null == canvasGroup)
            yield break;

        float elapsed = 0f;
        canvasGroup.alpha = 1f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeOutDuration);
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, ratio);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    private void CacheReferences()
    {
        if (null == canvasGroup)
            canvasGroup = GetComponent<CanvasGroup>();

        if (null == canvasGroup)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (null == titleText && texts[i].name == "TitleText")
                titleText = texts[i];
            else if (null == subText && texts[i].name == "SubText")
                subText = texts[i];
        }

        if (null != titleText && null == titleAnimator)
            titleAnimator = titleText.GetComponent<TMPInlineStyleAnimator>();

        if (null != subText && null == subAnimator)
            subAnimator = subText.GetComponent<TMPInlineStyleAnimator>();

        graphicTargets = GetComponentsInChildren<Graphic>(true);
        CacheBgPieces();
    }

    private void RefreshTexts()
    {
        if (null != titleText)
            titleText.text = ResolveText(forestType);

        if (null != subText)
            subText.text = ResolveText(dungeonState);
    }

    private string ResolveText<T>(T _enumValue) where T : struct, System.Enum
    {
        if (null == localizationManager)
            return _enumValue.ToString();

        string localizedText = localizationManager.GetText(_enumValue);
        return string.IsNullOrEmpty(localizedText) ? _enumValue.ToString() : localizedText;
    }

    private void SetupHiddenState()
    {
        CacheReferences();

        if (null != canvasGroup)
            canvasGroup.alpha = 1f;

        SetBgPiecesHidden();

        if (null == graphicTargets)
            return;

        for (int i = 0; i < graphicTargets.Length; i++)
            SetGraphicAlpha(graphicTargets[i], 0f);
    }

    private void CacheBgPieces()
    {
        if (null != bgPieces && bgPieces.Length > 0)
            return;

        List<BgPiece> pieces = new List<BgPiece>(4);
        RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (rectTransform.name.StartsWith("BG_") == false)
                continue;

            Graphic graphic = rectTransform.GetComponent<Graphic>();
            if (null == graphic)
                continue;

            pieces.Add(new BgPiece
            {
                rectTransform = rectTransform,
                graphic = graphic,
                targetWidth = Mathf.Max(rectTransform.sizeDelta.x, HiddenWidth),
                targetAlpha = graphic.color.a
            });
        }

        pieces.Sort((left, right) => string.CompareOrdinal(left.rectTransform.name, right.rectTransform.name));
        bgPieces = pieces.ToArray();
    }

    private float AssignRandomBgDelays()
    {
        if (null == bgPieces || bgPieces.Length == 0)
            return 0f;

        float[] delays = { 0f, 0.07f, 0.14f, 0.21f };

        for (int i = delays.Length - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            float temp = delays[i];
            delays[i] = delays[randomIndex];
            delays[randomIndex] = temp;
        }

        float lastDelay = 0f;
        for (int i = 0; i < bgPieces.Length; i++)
        {
            float delay = delays[i % delays.Length];
            bgPieces[i].delay = delay;
            lastDelay = Mathf.Max(lastDelay, delay);
        }

        return lastDelay;
    }

    private void UpdateBgPieces(float _elapsed)
    {
        if (null == bgPieces)
            return;

        for (int i = 0; i < bgPieces.Length; i++)
        {
            BgPiece piece = bgPieces[i];
            float progress = bgExpandDuration <= 0f ? 1f : Mathf.Clamp01((_elapsed - piece.delay) / bgExpandDuration);
            float alphaProgress = bgExpandDuration <= 0f ? 1f : Mathf.Clamp01(_elapsed / bgExpandDuration);
            SetBgPiece(piece, Mathf.Lerp(HiddenWidth, piece.targetWidth, EaseOutCubic(progress)), Mathf.Lerp(0f, piece.targetAlpha, alphaProgress));
        }
    }

    private float EaseOutCubic(float _value)
    {
        float inverse = 1f - Mathf.Clamp01(_value);
        return 1f - (inverse * inverse * inverse);
    }

    private void SetBgPiecesHidden()
    {
        if (null == bgPieces)
            return;

        for (int i = 0; i < bgPieces.Length; i++)
            SetBgPiece(bgPieces[i], HiddenWidth, 0f);
    }

    private void SetBgPiecesComplete()
    {
        if (null == bgPieces)
            return;

        for (int i = 0; i < bgPieces.Length; i++)
            SetBgPiece(bgPieces[i], bgPieces[i].targetWidth, bgPieces[i].targetAlpha);
    }

    private void SetBgPiece(BgPiece _piece, float _width, float _alpha)
    {
        if (null == _piece || null == _piece.rectTransform)
            return;

        Vector2 sizeDelta = _piece.rectTransform.sizeDelta;
        sizeDelta.x = _width;
        _piece.rectTransform.sizeDelta = sizeDelta;

        SetGraphicAlpha(_piece.graphic, _alpha);
    }

    private void SetGraphicAlpha(Graphic _graphic, float _alpha)
    {
        if (null == _graphic)
            return;

        Color color = _graphic.color;
        color.a = _alpha;
        _graphic.color = color;
    }
}
