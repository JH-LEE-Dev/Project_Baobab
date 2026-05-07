using TMPro;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(TMP_Text))]
public sealed class TMPPixelSnap : MonoBehaviour
{
    [SerializeField, Min(0.0001f)] private float pixelUnit = 1f;
    [SerializeField] private bool snapX = true;
    [SerializeField] private bool snapY = true;
    [SerializeField] private bool snapAnchoredPosition = true;
    [SerializeField] private bool snapLocalPosition;

    private RectTransform rectTransformCache;
    private TMP_Text text;
    private Vector2 lastAnchoredPosition;
    private Vector3 lastLocalPosition;

    private RectTransform RectTransform
    {
        get
        {
            if (rectTransformCache == null)
            {
                rectTransformCache = (RectTransform)transform;
            }

            return rectTransformCache;
        }
    }

    private TMP_Text Text
    {
        get
        {
            if (text == null)
            {
                text = GetComponent<TMP_Text>();
            }

            return text;
        }
    }

    private void OnEnable()
    {
        SnapNow();
    }

    private void LateUpdate()
    {
        RectTransform rect = RectTransform;

        if (rect.anchoredPosition == lastAnchoredPosition && rect.localPosition == lastLocalPosition)
        {
            return;
        }

        SnapNow();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        pixelUnit = Mathf.Max(0.0001f, pixelUnit);
        SnapNow();
    }
#endif

    [ContextMenu("Snap Now")]
    public void SnapNow()
    {
        RectTransform rect = RectTransform;

        if (snapAnchoredPosition)
        {
            Vector2 position = rect.anchoredPosition;
            position.x = snapX ? Snap(position.x) : position.x;
            position.y = snapY ? Snap(position.y) : position.y;
            rect.anchoredPosition = position;
        }

        if (snapLocalPosition)
        {
            Vector3 position = rect.localPosition;
            position.x = snapX ? Snap(position.x) : position.x;
            position.y = snapY ? Snap(position.y) : position.y;
            rect.localPosition = position;
        }

        lastAnchoredPosition = rect.anchoredPosition;
        lastLocalPosition = rect.localPosition;
        Text.SetVerticesDirty();
    }

    private float Snap(float value)
    {
        return Mathf.Round(value / pixelUnit) * pixelUnit;
    }
}
