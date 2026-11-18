using TMPro;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 도전과제 창의 달성 칸 연출(시안 B 리본, Docs/Roguelike/_Work/ChallengeUI_B_Ribbon_Plan.md 3장). 모든 연출은 리본 사각형(SetRibbonRect, 칸 왼쪽 위 기준 몸통 사각형) 안에서 돈다.
    ///  등장(1회): 리본이 왼쪽에서 펼쳐진다(0.30초, OutCubic) - 60%가 지나면 제목이 나타난다. 달성 깃발은 없다(리본이 달성 표시).
    ///  반복(펼침이 끝난 뒤): 리본 위 대각선 광택(3.4초 주기 0.6초, 칸마다 0.53초씩 위상이 밀림), 리본 위 · 아래 가장자리 `+` 반짝임(2.2 ~ 3.1초 주기 0.5초), 리본 안에서 떠오르는 금가루.
    ///  튀는 조각은 칸 밖으로 나가야 하므로 UI_ChallengeClearShards(판 아래 공용 층)가 그린다.
    ///  칸 테두리가 없어져 이전의 칸 번쩍 · 테두리를 도는 빛 · 칸 전체 광택 · 테두리 반짝임은 없다.
    ///
    /// 그리는 방식: 칸 전체를 덮는 UI_LootGlowParticles(원점 = 칸 왼쪽 아래, 1 캔버스 단위 = 게임 1px)에 정수 사각형을 쌓는다. 60fps 틱이 바뀔 때만 다시 그리고 Commit한다.
    /// 시뮬레이션은 640x360 게임 픽셀에 y가 아래로 증가하므로, 칸 왼쪽 위 기준 "시뮬레이션 좌표"로 계산해 그릴 때만 UGUI 좌표(y 위로 증가)로 뒤집는다.
    /// 정점 알파 = 가산 세기(UIGlow 가산 모드, _GlowMax 1). 시간은 호출 측이 넘기는 unscaled 시간이고 난수는 인스턴스별 xorshift라 전역 상태를 건드리지 않는다.
    /// 광택의 가산 세기는 기존 칸 광택과 같은 값(띠 0.10, 가운데 2px 심 0.16)을 쓴다(가산이라 시뮬레이션의 알파 블렌드 0.22 / 0.30을 그대로 쓰면 너무 밝다).
    ///
    /// 틱당 최대 쿼드 수(UI_LootGlowParticles 기본 용량 256 이하): 광택 2 x 리본 높이(높이 40이어도 80) + 반짝임 3 + 금가루 24 x 3(+ 모양) = 최대 약 155.
    ///  (금가루 배열 24칸이 전부 `+` 모양인 이론상 최악이 72이고, 실제 금가루는 평균 4개 안팎이다.)
    /// 초기화 뒤 힙 할당 없음.
    /// </summary>
    [DisallowMultipleComponent]
    public class UI_ChallengeClearGlow : MonoBehaviour
    {
        public const int RowWidth = 340;
        // 기본(최소) 칸 높이. 실제 높이는 언어 · 팁 줄 수에 따라 늘어나므로 그릴 때 칸의 rect 높이를 읽어 rowHeight에 담는다.
        public const int RowHeight = 62;
        /// <summary>리본이 펼쳐지는 시간(초). 호출 측이 튀는 조각 시작 시각을 맞출 때 쓴다.</summary>
        public const float RevealSeconds = 0.30f;

        // 시뮬레이션 색: 흰 #FFF6D8, 금 #FFD45A, 호박 #FF9D2E (감마 rgb, 알파는 그릴 때 가산 세기로 넣는다)
        public static readonly Color32 ColorWhite = new Color32(0xFF, 0xF6, 0xD8, 255);
        public static readonly Color32 ColorGold = new Color32(0xFF, 0xD4, 0x5A, 255);
        public static readonly Color32 ColorAmber = new Color32(0xFF, 0x9D, 0x2E, 255);

        // 반짝임 색(시뮬레이션): 큰 `+` · 점 #FFF6C8, 작은 `+` #FFE08A
        private static readonly Color32 ColorTwinkleLight = new Color32(0xFF, 0xF6, 0xC8, 255);
        private static readonly Color32 ColorTwinkleGold = new Color32(0xFF, 0xE0, 0x8A, 255);

        private const float TickRate = 60.0f;
        private const float TickInterval = 1.0f / TickRate;
        private const int MaxCatchUpTicks = 12;
        // 등장 요소 없이 반복 연출만 돌릴 때의 시작 시각(펼침이 끝난 뒤)
        private const float RepeatStartTime = 1.0f;

        private const float TitleShowRatio = 0.6f;

        private const float GlossPeriod = 3.4f;
        private const float GlossDuration = 0.6f;
        private const float GlossOrderPhase = 0.53f;
        private const int GlossBandWidth = 4;
        private const int GlossSeamOffset = 1;
        private const int GlossSeamWidth = 2;

        private const int TwinkleSeedBase = 3;
        private const float TwinklePeriodBase = 2.2f;
        private const float TwinklePeriodStep = 0.23f;
        private const float TwinkleSeedPhase = 0.37f;
        private const float TwinkleDuration = 0.5f;
        private const float TwinkleFrame = 0.1f;
        private const int TwinkleEdgeMargin = 6;

        private const int MoteCapacity = 24;
        private const float MoteSpawnChance = 0.05f;
        private const int MoteSpawnMargin = 4;
        private const int MoteSpawnBottom = 2;
        private const int MoteTopMargin = 1;

        private struct Mote
        {
            public bool bActive;
            public bool bPlus;
            public bool bGold;
            public float x;
            public float y;
            public float life;
            public float max;
            public float speed;
        }

        // 재생마다 다른 난수 시드를 주기 위한 카운터(UnityEngine.Random의 전역 상태를 쓰지 않는다)
        private static uint seedCounter;

        //외부 의존성
        [SerializeField, Tooltip("칸 전체를 덮는 자식 그래픽(원점 = 칸 왼쪽 아래).")]
        private UI_LootGlowParticles graphic;
        [SerializeField, Tooltip("금색 리본 그래픽. 펼침 비율을 넘긴다.")]
        private UI_ChallengeRibbon ribbon;
        [SerializeField, Tooltip("리본 위 제목 글자. 펼침이 60%를 지나면 보이게 한다.")]
        private TMP_Text titleText;

        //내부 의존성
        private readonly Mote[] motes = new Mote[MoteCapacity];

        //상태 변수
        private bool bPlaying;
        private float elapsed;
        private float delay;
        private int order;
        private int lastTick = int.MinValue;
        private int simTick;
        private uint rngState = 1u;
        private bool bTitleVisible = true;
        private int rowHeight = RowHeight;
        // 리본 몸통 사각형(칸 왼쪽 위 기준, 시뮬레이션 좌표)
        private int ribbonX = 8;
        private int ribbonY = 6;
        private int ribbonWidth = 100;
        private int ribbonHeight = 15;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>리본 몸통 사각형을 정한다(칸 왼쪽 위 기준 px). 광택 · 반짝임 · 금가루가 이 안에서 돈다.</summary>
        public void SetRibbonRect(int _x, int _y, int _width, int _height)
        {
            ribbonX = _x;
            ribbonY = _y;
            ribbonWidth = Mathf.Max(1, _width);
            ribbonHeight = Mathf.Max(1, _height);
        }

        /// <summary>
        /// 연출을 시작한다. _delay초 뒤부터 리본이 펼쳐지고, 광택 · 반짝임 · 금가루는 펼침이 끝난 뒤 이어진다.
        /// _order는 달성 칸끼리의 순번(광택 · 반짝임 위상). _bEntrance가 false면 펼침 없이 리본이 이미 펼쳐진 채 반복 연출만 돈다(_delay 무시).
        /// </summary>
        public void Play(float _delay, int _order, bool _bEntrance)
        {
            order = _order;
            delay = true == _bEntrance ? _delay : 0.0f;
            elapsed = true == _bEntrance ? 0.0f : RepeatStartTime;
            lastTick = int.MinValue;
            simTick = Mathf.FloorToInt(elapsed * TickRate);
            seedCounter++;
            rngState = 0x9E3779B9u ^ (uint)(_order * 7919 + 17) ^ (seedCounter * 2654435761u);
            if (0u == rngState) rngState = 1u;

            for (int i = 0; MoteCapacity > i; i++) motes[i].bActive = false;

            // 등장이면 첫 틱 전에도 리본은 접힌 채 제목이 가려져 있어야 한다(지연 동안 다 펼쳐진 모습이 비치지 않게).
            ApplyReveal(true == _bEntrance ? 0.0f : 1.0f);

            bPlaying = true;
            if (null != graphic) graphic.ClearQuads();
        }

        /// <summary>즉시 멈추고 그래픽을 비우며 리본 · 제목을 원래대로(펼친 채 보이게) 되돌린다.</summary>
        public void Stop()
        {
            bPlaying = false;
            for (int i = 0; MoteCapacity > i; i++) motes[i].bActive = false;
            if (null != graphic) graphic.ClearQuads();
            ApplyReveal(1.0f);
        }

        /// <summary>_deltaTime(unscaled)만큼 진행한다. 60fps 틱 번호가 바뀔 때만 다시 그리고 Commit한다.</summary>
        public void Tick(float _deltaTime)
        {
            if (false == bPlaying || null == graphic) return;

            elapsed += _deltaTime;
            int tick = Mathf.FloorToInt(elapsed * TickRate);
            if (tick == lastTick) return;

            lastTick = tick;

            int steps = 0;
            while (simTick < tick)
            {
                if (MaxCatchUpTicks <= steps)
                {
                    simTick = tick;
                    break;
                }

                StepMotes();
                simTick++;
                steps++;
            }

            Draw(tick);
        }

        // 일반 비즈니스 로직 및 내부 메서드

        // 칸(이 컴포넌트가 붙은 RectTransform)의 실제 높이를 읽어 높이 의존 값을 맞춘다. 같으면 바로 돌아간다(할당 없음).
        private void RefreshRowHeight()
        {
            int height = Mathf.RoundToInt(((RectTransform)transform).rect.height);
            if (0 >= height || height == rowHeight) return;

            rowHeight = height;
        }

        // 펼침 진행 _linear(0~1, 이징 전)을 리본과 제목에 반영한다. 리본은 SetReveal이 값이 같으면 건너뛴다.
        private void ApplyReveal(float _linear)
        {
            float p = Mathf.Clamp01(_linear);
            float inverse = 1.0f - p;
            if (null != ribbon) ribbon.SetReveal(1.0f - inverse * inverse * inverse);

            bool bVisible = TitleShowRatio < p;
            if (bVisible == bTitleVisible || null == titleText) return;

            bTitleVisible = bVisible;
            titleText.canvasRenderer.SetAlpha(true == bVisible ? 1.0f : 0.0f);
        }

        private float NextFloat()
        {
            rngState ^= rngState << 13;
            rngState ^= rngState >> 17;
            rngState ^= rngState << 5;
            return (rngState & 0xFFFFFFu) * (1.0f / 16777216.0f);
        }

        private static int RoundHalfUp(float _value)
        {
            return Mathf.FloorToInt(_value + 0.5f);
        }

        private static Color32 WithAlpha(in Color32 _color, float _alpha)
        {
            Color32 c = _color;
            c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(_alpha) * 255.0f);
            return c;
        }

        // 시뮬레이션 좌표(칸 왼쪽 위 기준, y 아래로 증가) -> 그래픽 좌표(칸 왼쪽 아래 기준, y 위로 증가)
        private void PutRect(int _x, int _y, int _width, int _height, in Color32 _color)
        {
            graphic.Rect(_x, rowHeight - _y - _height, _x + _width, rowHeight - _y, _color);
        }

        // 입자 모양: 기준 칸(_x, _y)은 시뮬레이션과 같은 칸이다(● 는 오른쪽 · 아래로 2x2).
        private void PutShape(int _shape, int _x, int _y, in Color32 _color)
        {
            graphic.Shape(_shape, _x, rowHeight - 1 - _y, _color);
        }

        // 가운데 칸 (_x, _y)에서 팔이 _arm칸인 `+`
        private void PutPlus(int _x, int _y, int _arm, in Color32 _color)
        {
            PutRect(_x - _arm, _y, _arm * 2 + 1, 1, _color);
            PutRect(_x, _y - _arm, 1, _arm, _color);
            PutRect(_x, _y + 1, 1, _arm, _color);
        }

        // 틱 하나: 금가루 생성(칸당 확률 0.05, 초당 약 3개, 펼침이 끝난 뒤부터) 뒤 이동 · 소멸
        private void StepMotes()
        {
            bool bRevealed = RevealSeconds <= simTick * TickInterval - delay;
            if (true == bRevealed && MoteSpawnChance > NextFloat()) SpawnMote();

            float topLimit = ribbonY + MoteTopMargin;
            for (int i = 0; MoteCapacity > i; i++)
            {
                if (false == motes[i].bActive) continue;

                motes[i].life += TickInterval;
                motes[i].y -= motes[i].speed * TickInterval;
                if (motes[i].life >= motes[i].max || topLimit >= motes[i].y) motes[i].bActive = false;
            }
        }

        // 리본 안쪽 아래에서 생겨 위로 떠오른다(리본 사각형 밖으로 나가지 않는다)
        private void SpawnMote()
        {
            float x = ribbonX + MoteSpawnMargin + Mathf.Floor(NextFloat() * Mathf.Max(1, ribbonWidth - MoteSpawnMargin * 2));
            float max = 1.0f + NextFloat() * 0.8f;
            float speed = 9.0f + NextFloat() * 9.0f;
            bool bPlus = 0.25f > NextFloat();
            bool bGold = 0.5f > NextFloat();

            for (int i = 0; MoteCapacity > i; i++)
            {
                if (true == motes[i].bActive) continue;

                motes[i].bActive = true;
                motes[i].bPlus = bPlus;
                motes[i].bGold = bGold;
                motes[i].x = x;
                motes[i].y = ribbonY + ribbonHeight - MoteSpawnBottom;
                motes[i].life = 0.0f;
                motes[i].max = max;
                motes[i].speed = speed;
                return;
            }
        }

        private void Draw(int _tick)
        {
            float rt = _tick / TickRate - delay;

            RefreshRowHeight();
            ApplyReveal(rt / RevealSeconds);
            graphic.BeginQuads();
            if (RevealSeconds <= rt)
            {
                DrawGloss(rt - RevealSeconds);
                DrawTwinkle(rt - RevealSeconds);
            }
            DrawMotes();
            graphic.Commit();
        }

        // 리본 광택: 줄마다 x를 1px씩 왼쪽으로 민 대각선(1:1 계단) 4px 띠(가산 0.10) + 안쪽 +1 위치 2px 심(0.16), 흰색. 리본 몸통 안으로 잘라낸다.
        private void DrawGloss(float _t)
        {
            float t = (_t + order * GlossOrderPhase) % GlossPeriod;
            if (GlossDuration < t) return;

            int pos = RoundHalfUp(-ribbonHeight + (ribbonWidth + 2.0f * ribbonHeight) * (t / GlossDuration));
            Color32 band = WithAlpha(ColorWhite, 0.10f);
            Color32 seam = WithAlpha(ColorWhite, 0.16f);
            for (int yy = 0; ribbonHeight > yy; yy++)
            {
                int xx = pos + (ribbonHeight - yy);
                PutClipped(xx, GlossBandWidth, yy, band);
                PutClipped(xx + GlossSeamOffset, GlossSeamWidth, yy, seam);
            }
        }

        // 리본 몸통 줄 _yy(몸통 위 기준)의 [_x, _x + _width)를 몸통 폭으로 잘라 그린다.
        private void PutClipped(int _x, int _width, int _yy, in Color32 _color)
        {
            int x0 = Mathf.Max(0, _x);
            int x1 = Mathf.Min(ribbonWidth, _x + _width);
            if (x1 <= x0) return;

            PutRect(ribbonX + x0, ribbonY + _yy, x1 - x0, 1, _color);
        }

        // 리본 위 · 아래 가장자리 반짝임: `.` -> 작은 `+` -> 큰 `+` -> 작은 `+` -> `.` (0.1초씩 5칸, 0.5초). 입자 모양은 픽셀 규칙의 예외다.
        private void DrawTwinkle(float _t)
        {
            int seed = order + TwinkleSeedBase;
            float period = TwinklePeriodBase + (seed % 5) * TwinklePeriodStep;
            float shifted = _t + seed * TwinkleSeedPhase;
            float t = shifted % period;
            if (TwinkleDuration < t) return;

            int k = Mathf.FloorToInt(seed * 7 + Mathf.Floor(shifted / period) * 13);
            int span = Mathf.Max(1, ribbonWidth + UI_ChallengeRibbon.TailLength(ribbonHeight) - TwinkleEdgeMargin - 2);
            int px = ribbonX + TwinkleEdgeMargin + (k * 37) % span;
            int py = 0 != (k % 2) ? ribbonY : ribbonY + ribbonHeight - 1;
            int frame = Mathf.FloorToInt(t / TwinkleFrame);
            if (0 == frame || 4 <= frame) PutRect(px, py, 1, 1, WithAlpha(ColorTwinkleLight, 1.0f));
            else if (2 == frame) PutPlus(px, py, 2, WithAlpha(ColorTwinkleLight, 1.0f));
            else PutPlus(px, py, 1, WithAlpha(ColorTwinkleGold, 1.0f));
        }

        // 떠오르는 금가루: 알파는 수명 15% 전 0.5, 70% 뒤 0.45(모양도 `.`), 그 사이 0.9
        private void DrawMotes()
        {
            for (int i = 0; MoteCapacity > i; i++)
            {
                if (false == motes[i].bActive) continue;

                float q = motes[i].life / motes[i].max;
                int shape = (0.7f < q || false == motes[i].bPlus) ? PixelPainter.ShapeDot : PixelPainter.ShapePlus;
                float alpha = 0.15f > q ? 0.5f : (0.7f < q ? 0.45f : 0.9f);
                PutShape(shape, RoundHalfUp(motes[i].x), RoundHalfUp(motes[i].y), WithAlpha(true == motes[i].bGold ? ColorGold : ColorAmber, alpha));
            }
        }

        // 유니티 라이프사이클 이벤트 함수

        private void OnDisable()
        {
            if (true == bPlaying) Stop();
        }
    }
}
