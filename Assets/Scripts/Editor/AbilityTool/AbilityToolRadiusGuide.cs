using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class AbilityToolRadiusGuide : MaskableGraphic
{
    private const int CircleSegmentCount = 256;

    public void Configure(float _radius, Color _color)
    {
        RectTransform guideRect = rectTransform;
        guideRect.anchorMin = new Vector2(0.5f, 0.5f);
        guideRect.anchorMax = new Vector2(0.5f, 0.5f);
        guideRect.pivot = new Vector2(0.5f, 0.5f);
        guideRect.sizeDelta = Vector2.one * Mathf.Max(_radius, 0f) * 2f;

        color = _color;
        raycastTarget = false;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper _vertexHelper)
    {
        _vertexHelper.Clear();

        Rect drawRect = rectTransform.rect;
        float radius = Mathf.Min(drawRect.width, drawRect.height) * 0.5f;
        if (radius <= 0f)
            return;

        Vector2 center = drawRect.center;
        Color32 vertexColor = color;
        _vertexHelper.AddVert(center, vertexColor, Vector2.zero);

        for (int i = 0; i <= CircleSegmentCount; i++)
        {
            float angle = i * Mathf.PI * 2f / CircleSegmentCount;
            Vector2 point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            _vertexHelper.AddVert(point, vertexColor, Vector2.zero);
        }

        for (int i = 0; i < CircleSegmentCount; i++)
            _vertexHelper.AddTriangle(0, i + 1, i + 2);
    }
}
