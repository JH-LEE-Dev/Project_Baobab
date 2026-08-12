using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AbilityLineGraphic : MaskableGraphic
{
    private readonly List<AbilityLineMeshQuad> quads = new List<AbilityLineMeshQuad>();
    private Sprite lineSprite;

    public override Texture mainTexture => lineSprite != null ? lineSprite.texture : s_WhiteTexture;

    public void SetLineSprite(Sprite _lineSprite)
    {
        if (lineSprite == _lineSprite)
            return;

        lineSprite = _lineSprite;
        SetMaterialDirty();
        SetVerticesDirty();
    }

    public void SetQuads(IReadOnlyList<AbilityLineMeshQuad> _quads)
    {
        quads.Clear();

        if (_quads != null)
        {
            for (int i = 0; i < _quads.Count; i++)
                quads.Add(_quads[i]);
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper _vertexHelper)
    {
        _vertexHelper.Clear();

        if (lineSprite == null || quads.Count == 0)
            return;

        Vector4 outerUv = DataUtility.GetOuterUV(lineSprite);
        for (int i = 0; i < quads.Count; i++)
            AddQuad(_vertexHelper, quads[i], outerUv);
    }

    private static void AddQuad(
        VertexHelper _vertexHelper,
        AbilityLineMeshQuad _quad,
        Vector4 _outerUv)
    {
        int vertexStart = _vertexHelper.currentVertCount;
        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = _quad.Color;

        vertex.position = new Vector2(_quad.Rect.xMin, _quad.Rect.yMin);
        vertex.uv0 = new Vector2(_outerUv.x, _outerUv.y);
        _vertexHelper.AddVert(vertex);

        vertex.position = new Vector2(_quad.Rect.xMin, _quad.Rect.yMax);
        vertex.uv0 = new Vector2(_outerUv.x, _outerUv.w);
        _vertexHelper.AddVert(vertex);

        vertex.position = new Vector2(_quad.Rect.xMax, _quad.Rect.yMax);
        vertex.uv0 = new Vector2(_outerUv.z, _outerUv.w);
        _vertexHelper.AddVert(vertex);

        vertex.position = new Vector2(_quad.Rect.xMax, _quad.Rect.yMin);
        vertex.uv0 = new Vector2(_outerUv.z, _outerUv.y);
        _vertexHelper.AddVert(vertex);

        _vertexHelper.AddTriangle(vertexStart, vertexStart + 1, vertexStart + 2);
        _vertexHelper.AddTriangle(vertexStart, vertexStart + 2, vertexStart + 3);
    }
}

public readonly struct AbilityLineMeshQuad
{
    public Rect Rect { get; }
    public Color32 Color { get; }

    public AbilityLineMeshQuad(Rect _rect, Color _color)
    {
        Rect = _rect;
        Color = _color;
    }
}
