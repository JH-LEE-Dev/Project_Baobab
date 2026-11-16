using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 도전과제 창 달성 칸의 "튀는 조각" 공용 층(승인된 시뮬레이션 https://claude.ai/artifact/VCZpYTh7sskVdJgvELxkVv).
    /// 조각은 칸 밖으로 튀어야 하므로 본문 마스크 밖(판 아래 공용 그래픽)에 그린다. 칸마다 Burst를 부르면 지연 뒤 12개가 한꺼번에 생긴다
    /// (앞 4개는 깃발 자리, 나머지 8개는 윗변). 모양 k%3 = ◆ / + / ●, 색 k%3 = 흰 / 금 / 호박. 중력 낙하, 수명 35% 뒤 한 단계 작게(◆ -> +, 나머지 -> ●), 70% 뒤 `.`, 85% 뒤 알파 0.5.
    ///
    /// 좌표는 그래픽 로컬 좌표(1 캔버스 단위 = 게임 1px, y 위로 증가)다. 30fps 틱이 바뀔 때만 다시 그리고 Commit한다.
    /// 틱당 최대 쿼드 수: 갓 생긴 조각이 가장 크다(◆ 5쿼드, + 3쿼드, ● 1쿼드, 칸당 4 x (5 + 3 + 1) = 36). 본문에 보이는 칸은 최대 5칸이라 5 x 36 = 180, 배열이 전부 찬 64개여도 192(256 이하).
    /// 조각 · 대기 목록은 고정 배열이고 난수는 Burst마다 넘기는 시드의 정수 xorshift다. 초기화 뒤 힙 할당 없음.
    /// </summary>
    [DisallowMultipleComponent]
    public class UI_ChallengeClearShards : MonoBehaviour
    {
        private const int ShardCapacity = 64;
        private const int PendingCapacity = 8;
        private const int ShardsPerBurst = 12;
        private const int FlagShardCount = 4;
        private const float TickRate = 30.0f;
        private const float TickInterval = 1.0f / TickRate;
        private const int MaxCatchUpTicks = 6;
        private const float Gravity = 260.0f;

        // 칸 안 깃발 자리(칸 왼쪽 위 기준, 시뮬레이션 수치): 오른쪽에서 21px + 5, 위에서 30px
        private const float FlagSpotRight = 21.0f - 5.0f;
        private const float FlagSpotDown = 30.0f;
        private const float TopSpotMargin = 14.0f;
        private const float TopSpotDown = 2.0f;

        private struct Shard
        {
            public bool bActive;
            public int kind;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float life;
            public float max;
        }

        private struct Pending
        {
            public bool bActive;
            public Vector2 topLeft;
            public float width;
            public float delay;
            public int seed;
        }

        // ◆ + ● 순서(k % 3)
        private static readonly int[] KindShapes = { PixelPainter.ShapeDiamond, PixelPainter.ShapePlus, PixelPainter.ShapeCircle };

        //외부 의존성
        [SerializeField, Tooltip("판 전체를 덮는 그래픽(본문 마스크 밖). 좌표는 이 그래픽 로컬 좌표다.")]
        private UI_LootGlowParticles graphic;

        //내부 의존성
        private readonly Shard[] shards = new Shard[ShardCapacity];
        private readonly Pending[] pendings = new Pending[PendingCapacity];

        //상태 변수
        private int activeCount;
        private int pendingCount;
        private float accumulated;
        private uint rngState = 1u;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 칸 하나의 조각 12개를 _delay초 뒤에 터뜨린다.
        /// _rowTopLeft = 칸 왼쪽 위 모서리(그래픽 로컬 좌표, 정수), _rowWidth = 칸 폭, _seed = 이 칸의 난수 시드.
        /// </summary>
        public void Burst(Vector2 _rowTopLeft, float _rowWidth, float _delay, int _seed)
        {
            for (int i = 0; PendingCapacity > i; i++)
            {
                if (true == pendings[i].bActive) continue;

                pendings[i].bActive = true;
                pendings[i].topLeft = _rowTopLeft;
                pendings[i].width = _rowWidth;
                pendings[i].delay = _delay;
                pendings[i].seed = _seed;
                pendingCount++;
                return;
            }
        }

        /// <summary>대기 중인 것과 살아 있는 조각을 모두 지우고 그래픽을 비운다.</summary>
        public void Clear()
        {
            for (int i = 0; ShardCapacity > i; i++) shards[i].bActive = false;
            for (int i = 0; PendingCapacity > i; i++) pendings[i].bActive = false;
            activeCount = 0;
            pendingCount = 0;
            accumulated = 0.0f;
            if (null != graphic) graphic.ClearQuads();
        }

        /// <summary>_deltaTime(unscaled)만큼 진행한다. 30fps 틱이 지나갈 때만 다시 그리고, 할 일이 없으면 바로 돌아간다.</summary>
        public void Tick(float _deltaTime)
        {
            if (null == graphic || (0 == activeCount && 0 == pendingCount)) return;

            accumulated += _deltaTime;
            bool bStepped = false;
            int steps = 0;
            while (TickInterval <= accumulated)
            {
                accumulated -= TickInterval;
                if (MaxCatchUpTicks <= steps) continue;

                Step();
                steps++;
                bStepped = true;
            }

            if (true == bStepped) Draw();
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private float NextFloat()
        {
            rngState ^= rngState << 13;
            rngState ^= rngState >> 17;
            rngState ^= rngState << 5;
            return (rngState & 0xFFFFFFu) * (1.0f / 16777216.0f);
        }

        // 틱 하나: 조각 이동(중력) -> 대기 지연 감소 -> 지연이 끝난 칸의 조각 생성
        private void Step()
        {
            for (int i = 0; ShardCapacity > i; i++)
            {
                if (false == shards[i].bActive) continue;

                shards[i].life += TickInterval;
                shards[i].vy -= Gravity * TickInterval;
                shards[i].x += shards[i].vx * TickInterval;
                shards[i].y += shards[i].vy * TickInterval;
                if (shards[i].life >= shards[i].max)
                {
                    shards[i].bActive = false;
                    activeCount--;
                }
            }

            for (int i = 0; PendingCapacity > i; i++)
            {
                if (false == pendings[i].bActive) continue;

                pendings[i].delay -= TickInterval;
                if (0.0f < pendings[i].delay) continue;

                pendings[i].bActive = false;
                pendingCount--;
                Spawn(in pendings[i]);
            }
        }

        private void Spawn(in Pending _pending)
        {
            rngState = (uint)_pending.seed * 2654435761u + 0x9E3779B9u;
            if (0u == rngState) rngState = 1u;

            for (int k = 0; ShardsPerBurst > k; k++)
            {
                bool bFromFlag = FlagShardCount > k;
                float x = bFromFlag
                    ? _pending.topLeft.x + _pending.width - FlagSpotRight + (NextFloat() - 0.5f) * 6.0f
                    : _pending.topLeft.x + TopSpotMargin + NextFloat() * (_pending.width - TopSpotMargin * 2.0f);
                float y = bFromFlag ? _pending.topLeft.y - FlagSpotDown : _pending.topLeft.y - TopSpotDown;
                float vx = (NextFloat() - 0.5f) * 60.0f;
                float vy = 40.0f + NextFloat() * 70.0f;
                float max = 0.45f + NextFloat() * 0.4f;

                for (int i = 0; ShardCapacity > i; i++)
                {
                    if (true == shards[i].bActive) continue;

                    shards[i].bActive = true;
                    shards[i].kind = k % 3;
                    shards[i].x = x;
                    shards[i].y = y;
                    shards[i].vx = vx;
                    shards[i].vy = vy;
                    shards[i].life = 0.0f;
                    shards[i].max = max;
                    activeCount++;
                    break;
                }
            }
        }

        private static Color32 KindColor(int _kind, float _alpha)
        {
            Color32 c = 0 == _kind ? UI_ChallengeClearGlow.ColorWhite : (1 == _kind ? UI_ChallengeClearGlow.ColorGold : UI_ChallengeClearGlow.ColorAmber);
            c.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(_alpha) * 255.0f);
            return c;
        }

        private void Draw()
        {
            graphic.BeginQuads();
            for (int i = 0; ShardCapacity > i; i++)
            {
                if (false == shards[i].bActive) continue;

                float q = shards[i].life / shards[i].max;
                int kind = shards[i].kind;
                int shape;
                if (0.35f > q) shape = KindShapes[kind];
                else if (0.7f > q) shape = 0 == kind ? PixelPainter.ShapePlus : PixelPainter.ShapeCircle;
                else shape = PixelPainter.ShapeDot;

                // 시뮬레이션은 y 아래로 증가 + 반올림(half up)이라 위로 증가하는 좌표에서는 올림 - 1 이 같은 칸이다.
                int cellX = Mathf.FloorToInt(shards[i].x + 0.5f);
                int cellY = Mathf.CeilToInt(shards[i].y - 0.5f) - 1;
                graphic.Shape(shape, cellX, cellY, KindColor(kind, 0.85f < q ? 0.5f : 1.0f));
            }

            graphic.Commit();
        }

        // 유니티 라이프사이클 이벤트 함수

        private void OnDisable()
        {
            if (0 < activeCount || 0 < pendingCount) Clear();
        }
    }
}
