using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 상세 HUD 전리품 빛(UI_LootRevealGlow)의 픽셀 입자·반짝임을 그리는 UGUI 그래픽. 1 캔버스 단위 = 게임 1px 격자에 정수 사각형을 쌓는다.
    /// 호출 측이 BeginQuads → Dot/Rect/Shape → Commit 순서로 채우고, Commit만 SetVerticesDirty를 부른다(30fps 틱이 바뀔 때만).
    /// 사각형 배열은 미리 잡아 재사용하므로 재생 중 힙 할당이 없다. 정점 색은 호출 측이 감마 rgb + 발광 알파로 넣는다(UIPixelGlow).
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UI_LootGlowParticles : MaskableGraphic
    {
        private const int QuadCapacity = 256;

        private struct PixelQuad
        {
            public int x0;
            public int y0;
            public int x1;
            public int y1;
            public Color32 color;
        }

        //상태 변수
        private PixelQuad[] quads = new PixelQuad[QuadCapacity];
        private int quadCount;
        private int committedCount = -1;

        // 퍼블릭 초기화 및 제어 메서드

        public void BeginQuads()
        {
            quadCount = 0;
        }

        /// <summary>그린 내용을 메쉬에 반영한다. 지난번도 비어 있었고 이번에도 비어 있으면 아무것도 하지 않는다.</summary>
        public void Commit()
        {
            if (0 == quadCount && 0 == committedCount) return;

            committedCount = quadCount;
            SetVerticesDirty();
        }

        /// <summary>내용을 비우고 메쉬에서도 지운다.</summary>
        public void ClearQuads()
        {
            quadCount = 0;
            Commit();
        }

        /// <summary>정수 칸 [_x0, _x1) x [_y0, _y1) (부모 기준 px)</summary>
        public void Rect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (_x1 <= _x0 || _y1 <= _y0) return;

            // 꽉 차면 사각형을 버리지 않고 배열을 두 배로 늘린다
            if (quads.Length <= quadCount) System.Array.Resize(ref quads, quads.Length * 2);

            ref PixelQuad q = ref quads[quadCount];
            q.x0 = _x0;
            q.y0 = _y0;
            q.x1 = _x1;
            q.y1 = _y1;
            q.color = _color;
            quadCount++;
        }

        public void Dot(int _x, int _y, Color32 _color)
        {
            Rect(_x, _y, _x + 1, _y + 1, _color);
        }

        /// <summary>
        /// 입자 하나(시뮬레이션 모양): `.` 1px, `+` `x` 3x3, `●` 2x2(기준 칸에서 오른쪽·아래), `◆` 5x5 마름모. y는 위쪽이 +.
        /// </summary>
        public void Shape(int _shape, int _x, int _y, Color32 _color)
        {
            switch (_shape)
            {
                case PixelPainter.ShapePlus:
                    Rect(_x, _y + 1, _x + 1, _y + 2, _color);
                    Rect(_x - 1, _y, _x + 2, _y + 1, _color);
                    Rect(_x, _y - 1, _x + 1, _y, _color);
                    break;

                case PixelPainter.ShapeCross:
                    Dot(_x - 1, _y + 1, _color);
                    Dot(_x + 1, _y + 1, _color);
                    Dot(_x, _y, _color);
                    Dot(_x - 1, _y - 1, _color);
                    Dot(_x + 1, _y - 1, _color);
                    break;

                case PixelPainter.ShapeCircle:
                    Rect(_x, _y - 1, _x + 2, _y + 1, _color);
                    break;

                case PixelPainter.ShapeDiamond:
                    Rect(_x, _y + 2, _x + 1, _y + 3, _color);
                    Rect(_x - 1, _y + 1, _x + 2, _y + 2, _color);
                    Rect(_x - 2, _y, _x + 3, _y + 1, _color);
                    Rect(_x - 1, _y - 1, _x + 2, _y, _color);
                    Rect(_x, _y - 2, _x + 1, _y - 1, _color);
                    break;

                default:
                    Dot(_x, _y, _color);
                    break;
            }
        }

        // 일반 비즈니스 로직 및 내부 메서드

        protected override void OnPopulateMesh(VertexHelper _vh)
        {
            _vh.Clear();

            for (int i = 0; i < quadCount; i++)
            {
                ref PixelQuad q = ref quads[i];
                int v = _vh.currentVertCount;
                _vh.AddVert(new Vector3(q.x0, q.y0, 0.0f), q.color, Vector4.zero);
                _vh.AddVert(new Vector3(q.x0, q.y1, 0.0f), q.color, Vector4.zero);
                _vh.AddVert(new Vector3(q.x1, q.y1, 0.0f), q.color, Vector4.zero);
                _vh.AddVert(new Vector3(q.x1, q.y0, 0.0f), q.color, Vector4.zero);
                _vh.AddTriangle(v, v + 1, v + 2);
                _vh.AddTriangle(v, v + 2, v + 3);
            }
        }

        // 유니티 라이프사이클 이벤트 함수

        protected override void Awake()
        {
            base.Awake();
            raycastTarget = false;
        }
    }
}
