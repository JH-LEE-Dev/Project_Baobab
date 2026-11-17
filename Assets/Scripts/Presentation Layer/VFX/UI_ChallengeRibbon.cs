using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 도전과제 창 달성 칸의 금색 리본(왼쪽 접힌 끝 + 몸통 + 오른쪽 제비꼬리). 시안 B 리본(Docs/Roguelike/_Work/ChallengeUI_B_Ribbon_Plan.md 2장)을 정수 사각형 쿼드로 직접 그린다.
    /// 스프라이트를 늘리면 꼬리 대각선 계단이 깨지므로(언어마다 높이가 다르다) 폭 · 높이를 받아 매번 정수 격자에 맞춰 만든다.
    ///
    /// [좌표] 이 그래픽의 사각형 왼쪽 위가 원점이고 x는 오른쪽, y는 아래로 증가한다(칸 안 시뮬레이션 좌표와 같다). 사각형 크기는 SetSize가 (접힌 끝 3 + 몸통 폭 + 꼬리 길이) x 높이로 맞춘다.
    /// [모양] 몸통 #E8A838, 맨 위 1px #FFE08A, 맨 아래 1px #A8611C. 접힌 끝은 x 0~2, 위아래 2px 안쪽, #A8611C + 바깥 1열 #7A4214(몸통 왼쪽 세로선은 없다 - 접힌 끝 바깥선과 2px 간격으로 나란한 HUGGING이라 지웠다).
    ///   꼬리 길이 = floor((높이 - 1) / 2), 행마다 가운데 행으로부터의 거리만큼(1:1 계단 - JAGGIES, 한 행에 한 칸씩만 줄어 L자 이중 픽셀이 없다 - DOUBLE PIXEL). 꼬리 맨 윗 · 아랫 행은 몸통 윗선 · 아랫선 색으로 끝까지 잇는다.
    /// [펼침] SetReveal(비율)은 몸통이 왼쪽에서 오른쪽으로 펼쳐지는 비율(이미 이징된 0~1)이다. 꼬리는 펼쳐진 몸통 끝에 붙어 함께 밀려 나온다. 값이 바뀔 때만 메쉬를 다시 만든다.
    /// 쿼드는 매번 VertexHelper에 바로 쌓으므로(몸통 1 + 윗선 1 + 아랫선 1 + 접힌 끝 2 + 꼬리 높이만큼) 초기화 뒤 힙 할당이 없다. 마스크 안에 두는 그래픽이라 maskable은 기본값을 유지하고 마우스를 받지 않는다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class UI_ChallengeRibbon : MaskableGraphic
    {
        /// <summary>왼쪽 접힌 끝 폭(px).</summary>
        public const int FoldWidth = 3;
        private const int FoldInset = 2;

        private static readonly Color32 BodyColor = new Color32(0xE8, 0xA8, 0x38, 255);
        private static readonly Color32 TopLineColor = new Color32(0xFF, 0xE0, 0x8A, 255);
        private static readonly Color32 BottomLineColor = new Color32(0xA8, 0x61, 0x1C, 255);
        private static readonly Color32 FoldColor = new Color32(0xA8, 0x61, 0x1C, 255);
        private static readonly Color32 FoldEdgeColor = new Color32(0x7A, 0x42, 0x14, 255);

        //상태 변수
        private int bodyWidth;
        private int ribbonHeight;
        private float revealRatio = 1.0f;
        private int revealWidth;

        /// <summary>제비꼬리 길이 = floor((높이 - 1) / 2).</summary>
        public static int TailLength(int _height)
        {
            return 0 < _height ? (_height - 1) / 2 : 0;
        }

        /// <summary>접힌 끝 + 몸통 + 꼬리를 모두 편 전체 폭.</summary>
        public static int TotalWidth(int _bodyWidth, int _height)
        {
            return FoldWidth + _bodyWidth + TailLength(_height);
        }

        public int BodyWidth => bodyWidth;
        public int RibbonHeight => ribbonHeight;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>몸통 폭과 높이(px)를 정하고 사각형 크기를 (접힌 끝 + 몸통 + 꼬리) x 높이로 맞춘다. 같으면 아무것도 하지 않는다.</summary>
        public void SetSize(int _width, int _height)
        {
            int width = Mathf.Max(0, _width);
            int height = Mathf.Max(0, _height);
            if (width == bodyWidth && height == ribbonHeight) return;

            bodyWidth = width;
            ribbonHeight = height;
            revealWidth = ComputeRevealWidth();
            rectTransform.sizeDelta = new Vector2(TotalWidth(bodyWidth, ribbonHeight), ribbonHeight);
            SetVerticesDirty();
        }

        /// <summary>펼침 비율(0~1, 이미 이징한 값). 펼쳐진 몸통 폭이 바뀔 때만 메쉬를 다시 만든다.</summary>
        public void SetReveal(float _ratio)
        {
            revealRatio = Mathf.Clamp01(_ratio);
            int width = ComputeRevealWidth();
            if (width == revealWidth) return;

            revealWidth = width;
            SetVerticesDirty();
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private int ComputeRevealWidth()
        {
            return Mathf.Min(bodyWidth, Mathf.FloorToInt(bodyWidth * revealRatio + 0.5f));
        }

        // 리본 좌표(왼쪽 위 원점, y 아래로) [_x0, _x1) x [_y0, _y1)를 사각형 하나로 쌓는다.
        private static void AddQuad(VertexHelper _vh, float _left, float _top, int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (_x1 <= _x0 || _y1 <= _y0) return;

            int v = _vh.currentVertCount;
            _vh.AddVert(new Vector3(_left + _x0, _top - _y1, 0.0f), _color, Vector2.zero);
            _vh.AddVert(new Vector3(_left + _x0, _top - _y0, 0.0f), _color, Vector2.zero);
            _vh.AddVert(new Vector3(_left + _x1, _top - _y0, 0.0f), _color, Vector2.zero);
            _vh.AddVert(new Vector3(_left + _x1, _top - _y1, 0.0f), _color, Vector2.zero);
            _vh.AddTriangle(v, v + 1, v + 2);
            _vh.AddTriangle(v, v + 2, v + 3);
        }

        protected override void OnPopulateMesh(VertexHelper _vh)
        {
            _vh.Clear();
            if (0 >= revealWidth || 0 >= ribbonHeight) return;

            Rect rect = rectTransform.rect;
            float left = rect.xMin;
            float top = rect.yMax;
            int height = ribbonHeight;
            int bodyEnd = FoldWidth + revealWidth;

            // 몸통 + 윗선 + 아랫선
            AddQuad(_vh, left, top, FoldWidth, 0, bodyEnd, height, BodyColor);
            AddQuad(_vh, left, top, FoldWidth, 0, bodyEnd, 1, TopLineColor);
            AddQuad(_vh, left, top, FoldWidth, height - 1, bodyEnd, height, BottomLineColor);

            // 왼쪽 접힌 끝: 위아래 2px 안쪽, 안쪽 2열 #A8611C + 바깥 1열 #7A4214
            AddQuad(_vh, left, top, 0, FoldInset, FoldWidth, height - FoldInset, FoldColor);
            AddQuad(_vh, left, top, 0, FoldInset, 1, height - FoldInset, FoldEdgeColor);

            // 오른쪽 제비꼬리: 행 k의 길이 = floor(가운데 행으로부터의 거리) - 1:1 계단
            for (int k = 0; height > k; k++)
            {
                int length = Mathf.Abs(2 * k - (height - 1)) / 2;
                if (0 >= length) continue;

                Color32 color = 0 == k ? TopLineColor : (height - 1 == k ? BottomLineColor : BodyColor);
                AddQuad(_vh, left, top, bodyEnd, k, bodyEnd + length, k + 1, color);
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
