using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 사진을 격자 메쉬로 그리고, 화면 정중앙에서 퍼지는 링(고리) 자리의 꼭짓점을 바깥으로 밀어 일그러뜨린다(디스토션 링).
/// 링이 지나간 안쪽은 같은 메쉬에 검은 원판을 얹어 덮는다 - 꼭짓점 색이 검정이면 사진 위에 검게 그려진다. 셰이더 없음.
/// 링 반지름·두께·세기는 화면 대각선 반 길이 대비 비율이다(1이면 정중앙에서 모서리까지).
/// </summary>
public sealed class DistortedScreenImage : RawImage
{
    private const int Columns = 64;
    private const int Rows = 36;
    private const int DiscSegments = 96;
    // 접히지 않는 한계(약 0.65)보다 조금 낮게.
    private const float MaxPushPerHalfWidth = 0.6f;

    private float ringRadius;
    private float ringWidth = 0.1f;
    private float ringStrength;

    public void SetRing(float _radius, float _width, float _strength)
    {
        ringRadius = _radius;
        ringWidth = _width;
        ringStrength = _strength;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper _vh)
    {
        _vh.Clear();
        if (null == texture) return;

        Rect rect = GetPixelAdjustedRect();
        Vector2 center = rect.center;
        float halfDiagonal = 0.5f * Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height);
        float radius = ringRadius * halfDiagonal;
        float halfWidth = Mathf.Max(1f, ringWidth * halfDiagonal * 0.5f);
        // 미는 양이 띠 반폭의 약 0.65배를 넘으면 이웃 꼭짓점 순서가 뒤집혀 화면이 접힌다(미는 함수의 기울기 1 - 4·push·k(1-k²)/반폭 > 0).
        // 인스펙터에서 세기를 올려도 그 아래로 묶는다.
        float push = Mathf.Min(ringStrength * halfDiagonal, halfWidth * MaxPushPerHalfWidth);
        Color32 tint = color;

        for (int y = 0; y <= Rows; y++)
        {
            float v = (float)y / Rows;

            for (int x = 0; x <= Columns; x++)
            {
                float u = (float)x / Columns;
                Vector2 pos = new Vector2(rect.xMin + u * rect.width, rect.yMin + v * rect.height);

                // 링 띠 안의 꼭짓점만 바깥으로 민다. 띠 가장자리에서 0이라 띠 밖 화면과 안쪽 원판에 이어 붙는다.
                Vector2 offset = pos - center;
                float dist = offset.magnitude;
                float k = (dist - radius) / halfWidth;
                if (k > -1f && k < 1f && dist > 0.0001f)
                {
                    float bump = 1f - k * k;
                    pos += offset / dist * (push * bump * bump);
                }

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

        // 링 안쪽은 검은 화면으로 채워진다(링 안쪽 가장자리까지).
        float discRadius = radius - halfWidth;
        if (discRadius <= 0f) return;

        Color32 black = new Color32(0, 0, 0, tint.a);
        Vector2 centerUv = new Vector2(0.5f, 0.5f);
        int centerIndex = _vh.currentVertCount;
        _vh.AddVert(center, black, centerUv);

        for (int i = 0; i < DiscSegments; i++)
        {
            float angle = (float)i / DiscSegments * Mathf.PI * 2f;
            _vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * discRadius, black, centerUv);
        }

        for (int i = 0; i < DiscSegments; i++)
        {
            _vh.AddTriangle(centerIndex, centerIndex + 1 + (i + 1) % DiscSegments, centerIndex + 1 + i);
        }
    }
}
