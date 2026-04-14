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

    public RectTransform RootRectTransform => rootRectTransform;
    public TMP_Text TitleAndLevelText => titleAndLevelText;
    public TMP_Text DescriptionText => descriptionText;
    public TMP_Text CostText => costText;

    // 툴팁에 표시할 문자열을 설정한다.
    public void SetContent(string _titleAndLevel, string _description, string _cost)
    {
        if (titleAndLevelText != null)
            titleAndLevelText.text = _titleAndLevel;

        if (descriptionText != null)
            descriptionText.text = _description;

        if (costText != null)
            costText.text = _cost;
    }

    // 툴팁 루트 RectTransform을 반환한다.
    public RectTransform GetRoot()
    {
        return rootRectTransform;
    }

    // 현재 레이아웃 기준 툴팁의 크기를 반환한다.
    public Vector2 GetSize()
    {
        RectTransform target = backgroundRectTransform != null ? backgroundRectTransform : rootRectTransform;
        if (target == null)
            return Vector2.zero;

        LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        return target.rect.size;
    }

    // 툴팁을 지정한 로컬 좌표에 배치한다.
    public void SetAnchoredPosition(Vector2 _anchoredPosition)
    {
        if (rootRectTransform == null)
            return;

        rootRectTransform.anchoredPosition = _anchoredPosition;
    }

    // 툴팁을 표시하고 레이아웃을 갱신한다.
    public void Show()
    {
        gameObject.SetActive(true);

        if (backgroundRectTransform != null)
            LayoutRebuilder.ForceRebuildLayoutImmediate(backgroundRectTransform);
    }

    // 툴팁을 숨긴다.
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
