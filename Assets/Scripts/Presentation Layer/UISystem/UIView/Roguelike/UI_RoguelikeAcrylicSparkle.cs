using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 레어 카드가 아크릴 케이스에 끼워지는 순간(딸깍) 케이스 입구 · 모서리에서 튀는 픽셀 입자 8개.
/// UI_RoguelikeCardAura와 같은 방식(텍스처 없이 색 사각형 메쉬만, 1px 단위 정수 사각형, 불투명)이고 30fps 틱(SetFrame)으로 끊어 그린다.
///   모양은 . + x 뿐이고, 입자마다 방향(dx, dy)이 정해져 있다. 이동은 처음 3틱은 틱마다 1칸, 그 뒤는 2틱마다 1칸(감속).
///   색: 처음 2틱 흰색 → 수명 60%까지 밝은 민트 → 그 뒤 어두운 민트(이때부터 . 모양). 수명이 끝나면 사라진다.
/// 좌표는 이 RectTransform(케이스 56x87, 피벗 왼쪽 아래)의 왼쪽 아래 기준 정수 픽셀이다. 입자 위치의 y는 위에서 아래로 센 케이스 그림 좌표로 적어 둔 값이다.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UI_RoguelikeAcrylicSparkle : MaskableGraphic
{
    private enum EShape : byte
    {
        Dot,
        Plus,
        Cross,
    }

    private struct Spark
    {
        public int x;
        public int y;
        public int dx;
        public int dy;
        public EShape shape;
        public int life;

        public Spark(int _x, int _y, int _dx, int _dy, EShape _shape, int _life)
        {
            x = _x;
            y = _y;
            dx = _dx;
            dy = _dy;
            shape = _shape;
            life = _life;
        }
    }

    private const int CaseHeight = 87;
    private const float ShapeChangeRatio = 0.6f;
    private const int WhiteFrames = 2;
    private const int FastFrames = 3;

    // 시안(case_sim)의 P 표와 같은 값. 케이스 왼쪽 위 기준 (x, y 아래로), 이동 방향, 수명(틱).
    private static readonly Spark[] Sparks =
    {
        new Spark(0, 1, -1, -1, EShape.Plus, 8),
        new Spark(55, 1, 1, -1, EShape.Plus, 8),
        new Spark(-1, 6, -1, 0, EShape.Cross, 7),
        new Spark(56, 6, 1, 0, EShape.Cross, 7),
        new Spark(14, 0, 0, -1, EShape.Dot, 5),
        new Spark(41, 0, 0, -1, EShape.Dot, 5),
        new Spark(1, 85, -1, 1, EShape.Cross, 6),
        new Spark(54, 85, 1, 1, EShape.Cross, 6),
    };

    private static readonly Color32 WhiteColor = new Color32(255, 255, 255, 255);
    private static readonly Color32 BrightColor = new Color32(191, 255, 224, 255);
    private static readonly Color32 DarkColor = new Color32(95, 211, 156, 255);

    //상태 변수
    private int frame = -1;

    /// <summary>딸깍 순간부터 센 30fps 틱 번호. 0 미만이면 입자를 모두 지운다. 같은 값이면 다시 그리지 않는다.</summary>
    public void SetFrame(int _frame)
    {
        if (_frame == frame) return;

        frame = _frame;
        SetVerticesDirty();
    }

    public void Clear()
    {
        SetFrame(-1);
    }

    protected override void OnPopulateMesh(VertexHelper _vh)
    {
        _vh.Clear();
        if (0 > frame) return;

        int originX = Mathf.RoundToInt(rectTransform.rect.min.x);
        int originY = Mathf.RoundToInt(rectTransform.rect.min.y);
        int step = Mathf.Min(frame, FastFrames) + Mathf.Max(0, frame - FastFrames) / 2;

        for (int i = 0; i < Sparks.Length; i++)
        {
            Spark spark = Sparks[i];
            if (frame >= spark.life) continue;

            // 시안과 같은 경계: 모양은 수명 60%를 지나면(>) 점, 색은 수명 60%에 닿으면(>=) 어두운 민트.
            float changeFrame = spark.life * ShapeChangeRatio;
            EShape shape = frame > changeFrame ? EShape.Dot : spark.shape;
            Color32 color = WhiteFrames > frame ? WhiteColor : (frame >= changeFrame ? DarkColor : BrightColor);

            int cx = spark.x + spark.dx * (step + 1);
            int cy = spark.y + spark.dy * (step + 1);

            AddCell(_vh, originX, originY, cx, cy, color);
            if (EShape.Plus == shape)
            {
                AddCell(_vh, originX, originY, cx + 1, cy, color);
                AddCell(_vh, originX, originY, cx - 1, cy, color);
                AddCell(_vh, originX, originY, cx, cy + 1, color);
                AddCell(_vh, originX, originY, cx, cy - 1, color);
            }
            else if (EShape.Cross == shape)
            {
                AddCell(_vh, originX, originY, cx + 1, cy + 1, color);
                AddCell(_vh, originX, originY, cx - 1, cy - 1, color);
                AddCell(_vh, originX, originY, cx + 1, cy - 1, color);
                AddCell(_vh, originX, originY, cx - 1, cy + 1, color);
            }
        }
    }

    /// <summary>케이스 왼쪽 위 기준 (_cx, _cy)칸(y는 아래로)을 1px 사각형으로 그린다.</summary>
    private static void AddCell(VertexHelper _vh, int _originX, int _originY, int _cx, int _cy, Color32 _color)
    {
        int left = _originX + _cx;
        int bottom = _originY + (CaseHeight - 1) - _cy;
        int index = _vh.currentVertCount;
        _vh.AddVert(new Vector3(left, bottom), _color, Vector2.zero);
        _vh.AddVert(new Vector3(left + 1, bottom), _color, Vector2.zero);
        _vh.AddVert(new Vector3(left + 1, bottom + 1), _color, Vector2.zero);
        _vh.AddVert(new Vector3(left, bottom + 1), _color, Vector2.zero);
        _vh.AddTriangle(index, index + 1, index + 2);
        _vh.AddTriangle(index + 2, index + 3, index);
    }
}
