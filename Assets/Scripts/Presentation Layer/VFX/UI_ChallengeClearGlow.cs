using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 도전과제 창의 달성 칸 연출(승인된 시뮬레이션 https://claude.ai/artifact/VCZpYTh7sskVdJgvELxkVv 의 칸 안쪽 요소를 그대로 옮겼다).
    ///  등장(1회): 칸 번쩍, 테두리를 도는 빛, 깃발 통통. 반복: 광택 쓸기, 테두리 반짝임, 떠오르는 금가루.
    ///  튀는 조각은 칸 밖으로 나가야 하므로 UI_ChallengeClearShards(판 아래 공용 층)가 그린다.
    ///
    /// 그리는 방식: 칸 전체를 덮는 UI_LootGlowParticles(원점 = 칸 왼쪽 아래, 1 캔버스 단위 = 게임 1px)에 정수 사각형을 쌓는다. 30fps 틱이 바뀔 때만 다시 그리고 Commit한다.
    /// 시뮬레이션은 640x360 게임 픽셀에 y가 아래로 증가하므로, 칸 왼쪽 위 기준 "시뮬레이션 좌표"로 계산해 그릴 때만 UGUI 좌표(y 위로 증가)로 뒤집는다.
    /// 정점 알파 = 가산 세기(UIGlow 가산 모드, _GlowMax 1). 시간은 호출 측이 넘기는 unscaled 시간이고 난수는 인스턴스별 xorshift라 전역 상태를 건드리지 않는다.
    ///
    /// 틱당 최대 쿼드 수(UI_LootGlowParticles 기본 용량 256 이하): 광택 112(줄 3..58 = 56줄 x 2) + 테두리 빛 46 + 금가루 24 x 3(+ 모양) = 최대 230.
    ///  (칸 번쩍 1개 구간 rt 0~0.45는 광택 시작 0.7 전에 끝나고, 반짝임 최대 5개 구간 rt 0.9~는 테두리 빛 종료 0.85 뒤라 서로 겹치지 않는다.
    ///   금가루 배열 24칸이 전부 `+` 모양인 이론상 최악이 230이고, 실제 금가루는 평균 4개 안팎이라 보통 170 미만이다.)
    /// 초기화 뒤 힙 할당 없음.
    /// </summary>
    [DisallowMultipleComponent]
    public class UI_ChallengeClearGlow : MonoBehaviour
    {
        public const int RowWidth = 340;
        // 기본(최소) 칸 높이. 실제 높이는 언어 · 팁 줄 수에 따라 늘어나므로 그릴 때 칸의 rect 높이를 읽어 rowHeight에 담는다.
        public const int RowHeight = 62;

        // 시뮬레이션 색: 흰 #FFF6D8, 금 #FFD45A, 호박 #FF9D2E (감마 rgb, 알파는 그릴 때 가산 세기로 넣는다)
        public static readonly Color32 ColorWhite = new Color32(0xFF, 0xF6, 0xD8, 255);
        public static readonly Color32 ColorGold = new Color32(0xFF, 0xD4, 0x5A, 255);
        public static readonly Color32 ColorAmber = new Color32(0xFF, 0x9D, 0x2E, 255);

        private const float TickRate = 30.0f;
        private const float TickInterval = 1.0f / TickRate;
        private const int MaxCatchUpTicks = 6;
        // 등장 요소 없이 반복 연출만 돌릴 때의 시작 시각(모든 등장 구간이 끝난 뒤, 반복 연출 시작 이후)
        private const float RepeatStartTime = 1.0f;

        // 테두리 둘레: 칸 가장자리에서 1px 안쪽 사각형, 왼쪽 위에서 시계 방향
        private const int PerimeterOffset = 1;
        private const int PerimeterWidth = RowWidth - 3;

        private const float FlashEnd = 0.45f;
        private const float RunnerStart = 0.05f;
        private const float RunnerEnd = 0.85f;
        private const float RunnerDuration = 0.7f;
        private const int RunnerLength = 46;
        private const float SheenStart = 0.7f;
        private const float SheenPeriod = 4.5f;
        private const float SheenDuration = 0.9f;
        private const float SheenOrderPhase = 0.5f;
        private const int SheenBandWidth = 12;
        private const int SheenSeamOffset = 4;
        private const int SheenSeamWidth = 3;
        private const float GlintStart = 0.9f;
        private const float GlintPeriod = 2.8f;
        private const float GlintDuration = 0.4f;
        private const float GlintSpotPhase = 0.47f;
        private const float GlintOrderPhase = 0.3f;
        private const float FlagBounceEnd = 0.4f;

        private const int MoteCapacity = 24;
        private const float MoteSpawnChance = 0.10f;
        private const int MoteSpawnMargin = 8;
        private const int MoteSpawnBottom = 6;
        private const float MoteTopLimit = 5.0f;

        private static readonly float[] GlintSpots = { 0.06f, 0.21f, 0.37f, 0.55f, 0.72f, 0.9f };
        // 깃발 통통: 틱별 y 오프셋(음수 = 위로, 시뮬레이션 좌표)
        private static readonly int[] FlagBounce = { -4, -5, -4, -2, 0, -2, -1, 0, -1, 0, 0, 0 };

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
        [SerializeField, Tooltip("달성 깃발(ClearedMark). 깃발 통통 오프셋을 anchoredPosition에 더한다.")]
        private RectTransform flag;

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
        private bool bFlagCached;
        private Vector2 flagBasePosition;
        private int flagOffset;
        private int rowHeight = RowHeight;
        private int perimeterHeight = RowHeight - 3;
        private float perimeterLength = 2.0f * (PerimeterWidth + RowHeight - 3);

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 연출을 시작한다. _delay초 뒤부터 등장 요소(번쩍 · 도는 빛 · 깃발 통통)가 시작되고, 광택 · 반짝임은 그 뒤에 이어진다.
        /// _order는 달성 칸끼리의 순번(광택 · 반짝임 위상). _bEntrance가 false면 등장 요소 없이 반복 연출만 돈다(_delay 무시).
        /// </summary>
        public void Play(float _delay, int _order, bool _bEntrance)
        {
            RestoreFlag();
            if (null != flag && false == bFlagCached)
            {
                bFlagCached = true;
                flagBasePosition = flag.anchoredPosition;
            }

            order = _order;
            delay = true == _bEntrance ? _delay : 0.0f;
            elapsed = true == _bEntrance ? 0.0f : RepeatStartTime;
            lastTick = int.MinValue;
            simTick = Mathf.FloorToInt(elapsed * TickRate);
            flagOffset = 0;
            seedCounter++;
            rngState = 0x9E3779B9u ^ (uint)(_order * 7919 + 17) ^ (seedCounter * 2654435761u);
            if (0u == rngState) rngState = 1u;

            for (int i = 0; MoteCapacity > i; i++) motes[i].bActive = false;

            bPlaying = true;
            if (null != graphic) graphic.ClearQuads();
        }

        /// <summary>즉시 멈추고 그래픽을 비우며 깃발 위치를 되돌린다.</summary>
        public void Stop()
        {
            bPlaying = false;
            for (int i = 0; MoteCapacity > i; i++) motes[i].bActive = false;
            if (null != graphic) graphic.ClearQuads();
            RestoreFlag();
        }

        /// <summary>_deltaTime(unscaled)만큼 진행한다. 30fps 틱 번호가 바뀔 때만 다시 그리고 Commit한다.</summary>
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
            perimeterHeight = height - 3;
            perimeterLength = 2.0f * (PerimeterWidth + perimeterHeight);
        }

        private void RestoreFlag()
        {
            if (false == bFlagCached || null == flag) return;

            flag.anchoredPosition = flagBasePosition;
            flagOffset = 0;
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

        // 테두리 둘레 위의 점(시뮬레이션 좌표). 시계 방향, 왼쪽 위에서 시작.
        private void Perimeter(float _distance, out float _x, out float _y)
        {
            float d = ((_distance % perimeterLength) + perimeterLength) % perimeterLength;
            if (PerimeterWidth > d)
            {
                _x = PerimeterOffset + d;
                _y = PerimeterOffset;
                return;
            }

            d -= PerimeterWidth;
            if (perimeterHeight > d)
            {
                _x = PerimeterOffset + PerimeterWidth;
                _y = PerimeterOffset + d;
                return;
            }

            d -= perimeterHeight;
            if (PerimeterWidth > d)
            {
                _x = PerimeterOffset + PerimeterWidth - d;
                _y = PerimeterOffset + perimeterHeight;
                return;
            }

            d -= PerimeterWidth;
            _x = PerimeterOffset;
            _y = PerimeterOffset + perimeterHeight - d;
        }

        // 틱 하나: 금가루 생성(칸당 확률 0.10) 뒤 이동 · 소멸
        private void StepMotes()
        {
            if (MoteSpawnChance > NextFloat()) SpawnMote();

            for (int i = 0; MoteCapacity > i; i++)
            {
                if (false == motes[i].bActive) continue;

                motes[i].life += TickInterval;
                motes[i].y -= motes[i].speed * TickInterval;
                if (motes[i].life >= motes[i].max || MoteTopLimit >= motes[i].y) motes[i].bActive = false;
            }
        }

        private void SpawnMote()
        {
            float x = MoteSpawnMargin + Mathf.Floor(NextFloat() * (RowWidth - MoteSpawnMargin * 2));
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
                motes[i].y = rowHeight - MoteSpawnBottom;
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
            graphic.BeginQuads();
            ApplyFlag(rt);
            if (SheenStart < rt) DrawSheen(rt);
            if (0.0f <= rt && FlashEnd > rt) DrawFlash(rt);
            if (RunnerStart <= rt && RunnerEnd > rt) DrawRunner(rt);
            if (GlintStart < rt) DrawGlints(rt);
            DrawMotes();
            graphic.Commit();
        }

        private void ApplyFlag(float _rt)
        {
            if (null == flag || false == bFlagCached) return;

            int offset = 0;
            if (0.0f <= _rt && FlagBounceEnd > _rt) offset = FlagBounce[Mathf.Min(FlagBounce.Length - 1, Mathf.FloorToInt(_rt * TickRate))];
            if (offset == flagOffset) return;

            flagOffset = offset;
            flag.anchoredPosition = new Vector2(flagBasePosition.x, flagBasePosition.y - offset);
        }

        // 광택 쓸기: 줄마다 x를 floor(yy/2)만큼 왼쪽으로 민 12px 띠(알파 0.10) + 안쪽 +4 위치 3px 심(알파 0.16), 흰색
        private void DrawSheen(float _rt)
        {
            float ph = ((_rt - SheenStart + order * SheenOrderPhase) % SheenPeriod) / SheenDuration;
            if (1.0f <= ph) return;

            float hx = -40.0f + ph * (RowWidth + 80.0f);
            Color32 band = WithAlpha(ColorWhite, 0.10f);
            Color32 seam = WithAlpha(ColorWhite, 0.16f);
            for (int yy = 3; rowHeight - 3 > yy; yy++)
            {
                int sx = RoundHalfUp(hx - (yy >> 1));
                PutRect(sx, yy, SheenBandWidth, 1, band);
                PutRect(sx + SheenSeamOffset, yy, SheenSeamWidth, 1, seam);
            }
        }

        // 칸 번쩍: 흰 0.85(0.1초) -> 흰 0.5 -> 금 0.25 -> 금 0.1, 칸 안쪽 사방 2px 안
        private void DrawFlash(float _rt)
        {
            float a = 0.1f > _rt ? 0.85f : (0.2f > _rt ? 0.5f : (0.32f > _rt ? 0.25f : 0.1f));
            Color32 c = WithAlpha(0.2f > _rt ? ColorWhite : ColorGold, a);
            PutRect(2, 2, RowWidth - 4, rowHeight - 4, c);
        }

        // 테두리를 도는 빛: 머리가 0.7초에 둘레 한 바퀴, 뒤로 46px(0~5 흰 1.0, 6~19 금 0.7, 20~45 호박 0.35), 점마다 2x2
        private void DrawRunner(float _rt)
        {
            int head = RoundHalfUp((_rt - RunnerStart) / RunnerDuration * perimeterLength);
            Color32 white = WithAlpha(ColorWhite, 1.0f);
            Color32 gold = WithAlpha(ColorGold, 0.7f);
            Color32 amber = WithAlpha(ColorAmber, 0.35f);
            for (int k = 0; RunnerLength > k; k++)
            {
                int d = head - k;
                if (0 > d || perimeterLength < d) continue;

                Perimeter(d, out float px, out float py);
                PutRect(RoundHalfUp(px), RoundHalfUp(py), 2, 2, 6 > k ? white : (20 > k ? gold : amber));
            }
        }

        // 테두리 반짝임: 고정 자리 6곳이 4프레임으로 `.` 흰 -> `+` 금(가운데 흰) -> `x` 금 0.9 -> `.` 호박 0.8
        private void DrawGlints(float _rt)
        {
            for (int k = 0; GlintSpots.Length > k; k++)
            {
                float ph = ((_rt - GlintStart + k * GlintSpotPhase + order * GlintOrderPhase) % GlintPeriod) / GlintDuration;
                if (1.0f <= ph) continue;

                Perimeter(GlintSpots[k] * perimeterLength, out float fx, out float fy);
                int px = RoundHalfUp(fx);
                int py = RoundHalfUp(fy);
                int frame = Mathf.FloorToInt(ph * 4.0f);
                if (0 == frame)
                {
                    PutShape(PixelPainter.ShapeDot, px, py, WithAlpha(ColorWhite, 1.0f));
                }
                else if (1 == frame)
                {
                    PutShape(PixelPainter.ShapePlus, px, py, WithAlpha(ColorGold, 1.0f));
                    PutShape(PixelPainter.ShapeDot, px, py, WithAlpha(ColorWhite, 1.0f));
                }
                else if (2 == frame)
                {
                    PutShape(PixelPainter.ShapeCross, px, py, WithAlpha(ColorGold, 0.9f));
                }
                else
                {
                    PutShape(PixelPainter.ShapeDot, px, py, WithAlpha(ColorAmber, 0.8f));
                }
            }
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
