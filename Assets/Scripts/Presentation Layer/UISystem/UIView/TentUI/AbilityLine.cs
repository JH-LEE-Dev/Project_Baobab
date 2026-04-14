using UnityEngine;
using UnityEngine.UI;

public class AbilityLine : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform rootRectTransform;
    [SerializeField] private Image lineImage;

    /// <summary>
    /// 라인 스프라이트와 배치 좌표를 적용한다.
    /// </summary>
    public void Setup(Sprite _sprite, Vector2 _anchoredPosition)
    {
        gameObject.SetActive(true);

        if (lineImage != null)
        {
            lineImage.sprite = _sprite;
            lineImage.SetNativeSize();
        }

        if (rootRectTransform != null)
        {
            rootRectTransform.localScale = Vector3.one;
            rootRectTransform.localRotation = Quaternion.identity;
            rootRectTransform.anchoredPosition = _anchoredPosition;
        }
    }

    /// <summary>
    /// 풀에 반환된 라인을 숨긴다.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 라인 루트를 가장 뒤쪽 형제로 이동시킨다.
    /// </summary>
    public void MoveBehindSiblings()
    {
        if (rootRectTransform != null)
            rootRectTransform.SetAsFirstSibling();
    }
}
