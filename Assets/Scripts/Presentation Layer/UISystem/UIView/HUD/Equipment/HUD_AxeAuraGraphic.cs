using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 도끼 HUD 아우라용 UI 그래픽. 픽셀 격자에 맞춘 색 직사각형(쿼드)들을 그대로 UI 메쉬로 내보낸다.
    /// 좌표는 격자 칸 단위(원점 = 이미지 중심)이고, 정점 색은 알파까지 불투명(반투명 없음)이다.
    /// 월드 메쉬를 쓰는 캐릭터 아우라(PixelQuadBuffer)를 HUD 캔버스에 맞춰 옮긴 것이다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class HUD_AxeAuraGraphic : MaskableGraphic
    {
        private struct Quad
        {
            public int x0;
            public int y0;
            public int x1;
            public int y1;
            public Color32 color;
        }

        // //내부 의존성
        private readonly List<Quad> quads = new List<Quad>(1024);

        // //상태 변수
        private float cellScale = 1.0f;

        public int QuadCount => quads.Count;

        // //퍼블릭 초기화 및 제어 메서드

        public void SetCellScale(float _scale)
        {
            cellScale = _scale;
        }

        public void Clear()
        {
            quads.Clear();
        }

        public void AddRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            Quad quad;
            quad.x0 = _x0;
            quad.y0 = _y0;
            quad.x1 = _x1;
            quad.y1 = _y1;
            quad.color = _color;
            quads.Add(quad);
        }

        /// <summary>
        /// 쌓아 둔 쿼드를 메쉬로 반영한다. 호출한 프레임에만 UI가 다시 만든다(프레임마다 자동으로 다시 만들지 않는다).
        /// </summary>
        public void Flush()
        {
            SetVerticesDirty();
        }

        // //유니티 이벤트 함수 및 오버라이드

        protected override void OnPopulateMesh(VertexHelper _vh)
        {
            _vh.Clear();

            UIVertex vertex = UIVertex.simpleVert;
            for (int i = 0; i < quads.Count; i++)
            {
                Quad quad = quads[i];
                float x0 = quad.x0 * cellScale;
                float y0 = quad.y0 * cellScale;
                float x1 = quad.x1 * cellScale;
                float y1 = quad.y1 * cellScale;
                int baseIndex = _vh.currentVertCount;

                vertex.color = quad.color;
                vertex.position = new Vector3(x0, y0, 0.0f);
                _vh.AddVert(vertex);
                vertex.position = new Vector3(x0, y1, 0.0f);
                _vh.AddVert(vertex);
                vertex.position = new Vector3(x1, y1, 0.0f);
                _vh.AddVert(vertex);
                vertex.position = new Vector3(x1, y0, 0.0f);
                _vh.AddVert(vertex);

                _vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
                _vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex);
            }
        }
    }
}
