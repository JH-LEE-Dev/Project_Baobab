using UnityEngine;
using UnityEngine.UI;

public class AbilityLine : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform rootRectTransform;
    [SerializeField] private Image lineImage;

    // 라인 스프라이트와 배치 좌표를 적용한다.
    public void Setup(Sprite _sprite, Vector2 _anchoredPosition)
    {
        gameObject.SetActive(true);

        if (lineImage != null)
        {
            lineImage.sprite = _sprite;
            lineImage.SetNativeSize();
        }

        if (lineImage != null)
            ResetLineRectTransform();

        if (rootRectTransform != null)
        {
            rootRectTransform.localScale = Vector3.one;
            rootRectTransform.localRotation = Quaternion.identity;
            rootRectTransform.anchoredPosition = _anchoredPosition;
        }
    }

    // 가로 또는 세로 선분을 하나의 오브젝트로 배치하고 길이만 늘린다.
    public void SetupScaled(Sprite _sprite, Vector2 _anchoredPosition, bool _isHorizontal, float _length)
    {
        gameObject.SetActive(true);

        if (lineImage != null)
        {
            lineImage.sprite = _sprite;
            lineImage.SetNativeSize();
        }

        if (rootRectTransform == null)
            return;

        rootRectTransform.localScale = Vector3.one;
        rootRectTransform.localRotation = Quaternion.identity;
        rootRectTransform.anchoredPosition = _anchoredPosition;

        if (lineImage == null || lineImage.sprite == null)
        {
            return;
        }

        ResetLineRectTransform();
        RectTransform lineRect = lineImage.rectTransform;
        float nativeWidth = Mathf.Max(lineRect.rect.width, 1f);
        float nativeHeight = Mathf.Max(lineRect.rect.height, 1f);

        if (_isHorizontal)
            lineRect.sizeDelta = new Vector2(Mathf.Max(_length, 1f), nativeHeight);
        else
            lineRect.sizeDelta = new Vector2(nativeWidth, Mathf.Max(_length, 1f));
    }

    // 코너 기준 한쪽 끝점을 고정한 상태로 width/height만 늘린다.
    public void SetupAnchoredSize(Sprite _sprite, Vector2 _cornerPosition, bool _isHorizontal, float _length, bool _anchorAtStart)
    {
        gameObject.SetActive(true);

        if (lineImage != null)
        {
            lineImage.sprite = _sprite;
            lineImage.SetNativeSize();
        }

        if (rootRectTransform == null || lineImage == null || lineImage.sprite == null)
            return;

        rootRectTransform.localScale = Vector3.one;
        rootRectTransform.localRotation = Quaternion.identity;
        rootRectTransform.anchoredPosition = _cornerPosition;

        ResetLineRectTransform();
        RectTransform lineRect = lineImage.rectTransform;
        float nativeWidth = Mathf.Max(lineRect.rect.width, 1f);
        float nativeHeight = Mathf.Max(lineRect.rect.height, 1f);
        float snappedLength = Mathf.Max(Mathf.Round(_length), 1f);

        if (_isHorizontal)
        {
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(_anchorAtStart ? 0f : 1f, 0.5f);
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.sizeDelta = new Vector2(snappedLength, nativeHeight);
        }
        else
        {
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, _anchorAtStart ? 0f : 1f);
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.sizeDelta = new Vector2(nativeWidth, snappedLength);
        }
    }

    // 풀링된 라인이 이전 모드의 pivot/anchor/sizeDelta를 들고 오지 않도록 초기화한다.
    private void ResetLineRectTransform()
    {
        if (lineImage == null)
            return;

        RectTransform lineRect = lineImage.rectTransform;
        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.anchoredPosition = Vector2.zero;
        lineRect.localScale = Vector3.one;
        lineRect.localRotation = Quaternion.identity;
    }

    // 풀에 반환된 라인을 숨긴다.
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // 라인 루트를 가장 뒤쪽 형제로 이동시킨다.
    public void MoveBehindSiblings()
    {
        if (rootRectTransform != null)
            rootRectTransform.SetAsFirstSibling();
    }
}
