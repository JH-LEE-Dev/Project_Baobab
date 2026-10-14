using UnityEngine;
using UnityEngine.UI;

/// <summary>화면 사진을 격자 메쉬로 그리고 꼭짓점을 가로로 출렁이게 한다(디스토션). 가장자리는 화면 밖으로 조금 키워 틈이 보이지 않게 한다.</summary>
public sealed class DistortedScreenImage : RawImage
{
    private const int Columns = 24;
    private const int Rows = 16;

    private float strength;
    private float waves = 3f;
    private float phase;

    public void SetWave(float _strength, float _waves, float _phase)
    {
        strength = _strength;
        waves = _waves;
        phase = _phase;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper _vh)
    {
        _vh.Clear();
        if (null == texture) return;

        Rect rect = GetPixelAdjustedRect();
        // 출렁이는 만큼 키워 가장자리 틈을 덮는다.
        float pad = strength * 1.2f;
        float width = rect.width * (1f + pad * 2f);
        float height = rect.height * (1f + pad * 2f);
        float left = rect.center.x - width * 0.5f;
        float bottom = rect.center.y - height * 0.5f;
        Color32 tint = color;

        for (int y = 0; y <= Rows; y++)
        {
            float v = (float)y / Rows;
            // 세로 위치마다 가로로 밀고(물결), 가로 위치마다 세로로 조금 민다(일렁임).
            float shiftX = Mathf.Sin(v * waves * Mathf.PI * 2f + phase) * strength * rect.width;

            for (int x = 0; x <= Columns; x++)
            {
                float u = (float)x / Columns;
                float shiftY = Mathf.Sin(u * waves * Mathf.PI * 2f + phase * 0.7f) * strength * 0.5f * rect.height;

                Vector3 pos = new Vector3(left + u * width + shiftX, bottom + v * height + shiftY, 0f);
                _vh.AddVert(pos, tint, new Vector2(u, v));
            }
        }

        int stride = Columns + 1;
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                int i = x + y * stride;
                _vh.AddTriangle(i, i + stride, i + stride + 1);
                _vh.AddTriangle(i, i + stride + 1, i + 1);
            }
        }
    }
}
