using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

public sealed class UI_BlastFurnaceStatus : MonoBehaviour
{
    private const float GlyphSize = 8f;
    private const float BorderOverlap = 1f;
    // All three 16 px ore icons have opaque columns 1 through 13.
    private const float IconSize = 16f;
    private const float IconVisibleLeft = 1f;
    private const float IconVisibleWidth = 13f;
    private const float IconToFractionGap = 1f;

    [Header("Ore")]
    [SerializeField] private Image oreIcon;
    [SerializeField] private Sprite goldIcon;
    [SerializeField] private Sprite diamondIcon;
    [SerializeField] private Sprite prismIcon;

    [Header("Fraction")]
    [SerializeField] private RectTransform storedOreDigits;
    [SerializeField] private RectTransform requiredOreDigits;
    [SerializeField] private RectTransform fractionLine;
    [SerializeField] private RectTransform lineLeftCap;
    [SerializeField] private RectTransform lineCenter;
    [SerializeField] private RectTransform lineRightCap;
    [SerializeField] private Sprite[] numberSprites = new Sprite[10];

    [Header("Progress")]
    [SerializeField] private Slider progressSlider;
    [SerializeField] private Image progressFill;
    [SerializeField] private Color progressColor = new Color(1f, 0.72f, 0.23f, 1f);

    private GemOreType displayedType;
    private bool hasDisplayedType;
    private int displayedStoredOre = -1;
    private int displayedRequiredOre = -1;

    public void SetData(BlastFurnaceUIData data)
    {
        SetValues(data.gemOreType, data.storedOre, data.orePerIngot,
            data.running ? data.progress01 : 0f);
    }

    public void SetValues(GemOreType oreType, int storedOre, int requiredOre, float progress01)
    {
        if (false == hasDisplayedType || displayedType != oreType)
        {
            hasDisplayedType = true;
            displayedType = oreType;
            if (null != oreIcon)
            {
                oreIcon.sprite = GetOreIcon(oreType);
                oreIcon.enabled = null != oreIcon.sprite;
            }
        }

        if (displayedStoredOre != storedOre || displayedRequiredOre != requiredOre)
        {
            displayedStoredOre = storedOre;
            displayedRequiredOre = requiredOre;

            float topWidth = SetNumber(storedOreDigits, Mathf.Max(0, storedOre));
            float bottomWidth = SetNumber(requiredOreDigits, Mathf.Max(0, requiredOre));
            UpdateLayout(Mathf.Max(topWidth, bottomWidth));
        }

        if (null != progressFill)
            progressFill.color = progressColor;
        if (null != progressSlider)
            progressSlider.SetValueWithoutNotify(Mathf.Clamp01(progress01));
    }

    private void UpdateLayout(float numberWidth)
    {
        float lineWidth = Mathf.Max(3f, numberWidth);
        float groupWidth = IconVisibleWidth + IconToFractionGap + lineWidth;
        // Odd-width artwork must start on a whole pixel; the group may sit 0.5 px off center.
        float groupLeft = -Mathf.Floor(groupWidth * 0.5f);
        float iconCenter = groupLeft + IconSize * 0.5f - IconVisibleLeft;
        float fractionCenter = groupLeft + IconVisibleWidth + IconToFractionGap + lineWidth * 0.5f;

        RectTransform widgetRect = transform as RectTransform;
        if (null != widgetRect)
            widgetRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(20f, groupWidth));

        if (null != oreIcon)
            SetAnchoredX(oreIcon.rectTransform, iconCenter);
        SetAnchoredX(storedOreDigits, fractionCenter);
        SetAnchoredX(requiredOreDigits, fractionCenter);
        if (null == fractionLine)
            return;

        SetAnchoredX(fractionLine, fractionCenter);
        fractionLine.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, lineWidth);
        SetAnchoredX(lineLeftCap, -lineWidth * 0.5f + 0.5f);
        if (null != lineCenter)
            lineCenter.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, lineWidth - 2f);
        SetAnchoredX(lineRightCap, lineWidth * 0.5f - 0.5f);
    }

    private static void SetAnchoredX(RectTransform rect, float x)
    {
        if (null == rect)
            return;
        Vector2 position = rect.anchoredPosition;
        position.x = x;
        rect.anchoredPosition = position;
    }

    private Sprite GetOreIcon(GemOreType oreType)
    {
        switch (oreType)
        {
            case GemOreType.Gold: return goldIcon;
            case GemOreType.Diamond: return diamondIcon;
            case GemOreType.Prism: return prismIcon;
            default: return null;
        }
    }

    private float SetNumber(RectTransform root, int value)
    {
        if (null == root || null == numberSprites || numberSprites.Length < 10)
            return 0f;

        string text = value.ToString(CultureInfo.InvariantCulture);
        int count = text.Length;
        float visibleWidth = 0f;
        for (int i = 0; i < count; i++)
            visibleWidth += GetInkWidth(text[i]);
        visibleWidth -= (count - 1) * BorderOverlap;

        while (root.childCount < count)
        {
            GameObject glyphObject = new GameObject(
                "Digit_" + root.childCount, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            glyphObject.transform.SetParent(root, false);
            glyphObject.GetComponent<Image>().raycastTarget = false;
        }

        float cursor = -visibleWidth * 0.5f;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            child.gameObject.SetActive(i < count);
            if (i >= count)
                continue;

            Image glyphImage = child.GetComponent<Image>();
            RectTransform glyphRect = child as RectTransform;
            if (null == glyphImage || null == glyphRect)
                continue;

            char digit = text[i];
            glyphImage.sprite = numberSprites[digit - '0'];
            glyphImage.raycastTarget = false;
            glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
            glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
            glyphRect.pivot = new Vector2(0.5f, 0.5f);
            glyphRect.sizeDelta = new Vector2(GlyphSize, GlyphSize);
            glyphRect.anchoredPosition = new Vector2(
                cursor - GetInkLeft(digit) + GlyphSize * 0.5f, 0f);
            cursor += GetInkWidth(digit) - BorderOverlap;
        }

        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, visibleWidth);
        return visibleWidth;
    }

    private static float GetInkWidth(char digit)
    {
        return '1' == digit ? 3f : 5f;
    }

    private static float GetInkLeft(char digit)
    {
        return '1' == digit ? 3f : 2f;
    }
}