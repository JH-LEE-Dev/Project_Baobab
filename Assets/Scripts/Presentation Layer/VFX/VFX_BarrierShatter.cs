using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 3스테이지 배리어가 별자리 발현에 깨질 때의 파편 연출. 32 PPU 픽셀 격자에 맞춘 절차적 쿼드 메쉬(PixelPainter + PixelQuadBuffer)이고
    /// VFXComponent 풀(태그 TreeBarrierShatterEffect)로 나무 밑동 위치에 재생한다. 전체 1.0초.
    ///
    /// 나무에 붙은 껍질(VFX_TreeBarrier 셰이더)이 맞은 자리부터 금이 번지고 마름모 조각이 차례로 떨어져 나가는데,
    /// 이 이펙트의 파편은 같은 시각표(0.08초 + 맞은 자리에서의 거리 / 230px/s)에 그 자리에서 튀어나온다.
    ///
    /// 구성(전부 한 메쉬)
    /// - 임팩트 섬광(0~0.1초): 맞은 자리에서 큰 별 섬광이 3단계로 줄어든다.
    /// - 유리 파편 22개: 껍질 표면에서 바깥으로 튀어 포물선으로 떨어지고 바닥에서 한 번 튕긴다. 0.05초마다 모양이 바뀌어 빙글빙글 도는 것처럼 보이고, 끝에는 깜빡이며 사라진다.
    /// - 바닥 고리(0.06~0.42초): 밑동에서 점선 타원 고리가 계단식으로 퍼지며 식는다.
    /// - 별가루 14개: 껍질이 있던 자리에서 `.` `+` `x`가 천천히 떠오르며 깜빡이다 사라진다.
    /// - 빛줄기 16개: 껍질 조각이 떨어지는 시각표 그대로 표면에서 바깥으로 쏘아져 급감속한다. 꼬리는 1px 직선이고 하양 -> 하늘색 -> 파랑으로 식는다.
    /// - 껍질 해방 고리(0.1~0.38초): 껍질 윤곽 그대로의 타원이 계단식으로 바깥으로 부풀며 점선으로 흩어진다.
    ///
    /// 파편 궤적은 재생 시작 때 120Hz 표로 한 번 계산하고(튕김은 닫힌 식이 없다), 나머지는 경과 시간만으로 정해진다. 재생 중 힙 할당이 없다.
    /// 픽셀 규칙: 고리는 PixelEllipse 윤곽(JAGGIES), 파편/별가루는 정해진 작은 모양만 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_BarrierShatter : MonoBehaviour
    {
        [Header("밀도 / 발광")]
        [SerializeField, Range(0.25f, 2.0f), Tooltip("파편, 별가루 개수 배율")] private float density = 1.0f;
        [SerializeField, Range(0.0f, 2.0f), Tooltip("정점 알파(발광 세기) 배율")] private float glowScale = 1.0f;

        [Header("정렬")]
        [SerializeField, Tooltip("루트 렌더러 소팅보다 이 값만큼 위에 그린다")] private int sortingOrderOffset = 0;

        private const float PixelsPerUnit = 32.0f;
        private const float TimelineLength = 1.0f;
        private const float TableStep = 1.0f / 120.0f;
        private const int TableSamples = 120;

        private const int ShardCapacity = 44;
        private const int DustCapacity = 28;
        private const int BaseShardCount = 22;
        private const int BaseDustCount = 14;
        private const int QuadBudget = 1200;

        // 셰이더(TreeBarrier)의 조각 떨어짐 시각표와 같은 값
        private const float PopStart = 0.08f;
        private const float PopSpeed = 230.0f;

        private const float ShardBounce = 0.3f;
        private const float ShardFriction = 0.55f;
        private const float GroundSquashY = 0.5f;

        private const float RingStart = 0.06f;
        private const float RingStepTime = 0.04f;
        private const int RingStepCount = 9;
        private const float RingRadiusMin = 12.0f;
        private const float RingRadiusMax = 40.0f;

        private const int StreakCapacity = 32;
        private const int BaseStreakCount = 16;
        private const float StreakDrag = 7.0f;          // 클수록 빨리 멈춘다. 이동 거리 = 속도 / 감쇠
        private const float StreakGravity = 70.0f;
        private const float StreakTrailTime = 0.05f;    // 꼬리 = 이만큼 전의 위치

        private const float BurstStart = 0.1f;
        private const float BurstStepTime = 0.035f;
        private const int BurstStepCount = 8;
        private const float BurstScaleMin = 1.06f;
        private const float BurstScaleMax = 1.75f;

        private const int ToneCount = 6;

        // 별빛 유리 색(밝음 -> 어두움). 배리어 껍질/스파크와 같은 팔레트.
        private static readonly byte[] ToneR = { 255, 214, 160, 104, 70, 46 };
        private static readonly byte[] ToneG = { 255, 240, 214, 160, 104, 62 };
        private static readonly byte[] ToneB = { 255, 255, 255, 236, 214, 160 };
        private static readonly float[] ToneGlow = { 0.34f, 0.27f, 0.2f, 0.13f, 0.08f, 0.04f };

        // 파편 모양(4프레임 회전). 작은 파편은 2~3px, 큰 파편은 4~6px.
        private static readonly int[][] SmallShardX = { new[] { 0, 1, 0 }, new[] { 0, 1 }, new[] { 0, 1, 1 }, new[] { 0, 1 } };
        private static readonly int[][] SmallShardY = { new[] { 0, 0, 1 }, new[] { 0, 1 }, new[] { 0, 0, 1 }, new[] { 0, 0 } };
        private static readonly int[][] LargeShardX = { new[] { 0, 1, 2, 0, 1, 0 }, new[] { 0, 1, 1, 2 }, new[] { 0, 1, 2, 1, 2, 2 }, new[] { 0, 0, 1, 1 } };
        private static readonly int[][] LargeShardY = { new[] { 0, 0, 0, 1, 1, 2 }, new[] { 0, 0, 1, 1 }, new[] { 0, 0, 0, 1, 1, 2 }, new[] { 0, 1, 1, 2 } };

        private struct Shard
        {
            public float delay;
            public float endTime;
            public int sampleCount;
            public bool bLarge;
            public int frameOffset;
            public float spinRate;
        }

        private struct Dust
        {
            public float delay;
            public float life;
            public float x0;
            public float y0;
            public float rise;
            public float swayPhase;
            public int shape;
        }

        private struct Streak
        {
            public float delay;
            public float life;
            public float x0;
            public float y0;
            public float dirX;
            public float dirY;
            public float speed;
        }

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(QuadBudget);

        //내부 의존성
        private readonly PixelPainter painter = new PixelPainter();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly Color32[] toneColors = new Color32[ToneCount];
        private readonly Shard[] shards = new Shard[ShardCapacity];
        private readonly float[] shardX = new float[ShardCapacity * TableSamples];
        private readonly float[] shardY = new float[ShardCapacity * TableSamples];
        private readonly Dust[] dusts = new Dust[DustCapacity];
        private readonly Streak[] streaks = new Streak[StreakCapacity];

        //상태 변수
        private Vector2 impactLocalPx = new Vector2(0.0f, VFX_TreeBarrier.ShellCenterYPx);
        private float elapsed;
        private bool bFirstFrame;
        private bool bBeginPending;
        private bool bInitialized;
        private int shardTotal;
        private int dustTotal;
        private int streakTotal;
        private int originX;
        private int originY;
        private uint randomState;
        private ulong meshHash;
        private int meshVertexCount = -1;
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;

        /// <summary>껍질 위 맞은 자리(월드). 풀에서 꺼낸 직후, 첫 LateUpdate 전에 부른다.</summary>
        public void SetImpact(Vector3 _worldImpact)
        {
            Vector3 origin = transform.position;
            impactLocalPx = new Vector2((_worldImpact.x - origin.x) * PixelsPerUnit, (_worldImpact.y - origin.y) * PixelsPerUnit);
        }

        // ---------- 초기화 ----------

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            mesh = new Mesh { name = "VFX_BarrierShatter" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 800);

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            BuildTones();
        }

        private void BuildTones()
        {
            bool bLinear = ColorSpace.Linear == QualitySettings.activeColorSpace;

            for (int i = 0; i < ToneCount; i++)
            {
                Color color = new Color32(ToneR[i], ToneG[i], ToneB[i], 255);
                if (true == bLinear) color = color.linear;
                color.a = Mathf.Clamp01(ToneGlow[i] * glowScale);
                toneColors[i] = color;
            }
        }

        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bFirstFrame = true;
            meshVertexCount = -1;

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            SetupShards();
            SetupDusts();
            SetupStreaks();
        }

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        private float RandRange(float _min, float _max)
        {
            return _min + (_max - _min) * Rand01();
        }

        // 파편: 껍질 표면(0.8~1배)에서 바깥 법선 방향으로 튄다. 떨어져 나가는 시각은 셰이더 조각과 같은 시각표다.
        private void SetupShards()
        {
            shardTotal = Mathf.Min(ShardCapacity, Mathf.RoundToInt(BaseShardCount * density));
            float rx = VFX_TreeBarrier.ShellRadiusXPx;
            float ry = VFX_TreeBarrier.ShellRadiusYPx;
            float cy = VFX_TreeBarrier.ShellCenterYPx;

            for (int i = 0; i < shardTotal; i++)
            {
                // 둘레를 고르게 나눠 뭉치지 않게 한다
                float angle = (i + RandRange(0.1f, 0.9f)) / shardTotal * Mathf.PI * 2.0f;
                float k = RandRange(0.8f, 1.0f);
                float x = Mathf.Cos(angle) * rx * k;
                float y = cy + Mathf.Sin(angle) * ry * k;

                Vector2 normal = new Vector2(Mathf.Cos(angle) / rx, Mathf.Sin(angle) / ry).normalized;
                float speed = RandRange(40.0f, 100.0f);
                float vx = normal.x * speed;
                float vy = normal.y * speed + RandRange(15.0f, 50.0f);
                float gravity = RandRange(300.0f, 380.0f);
                float groundY = RandRange(-7.0f, 3.0f);
                float life = RandRange(0.5f, 0.8f);

                float delay = PopStart + Vector2.Distance(new Vector2(x, y), impactLocalPx) / PopSpeed + RandRange(0.0f, 0.04f);

                float endTime = life;
                int samples = TableSamples;
                bool bBounced = false;
                int baseIndex = i * TableSamples;

                for (int s = 0; s < TableSamples; s++)
                {
                    float t = s * TableStep;
                    shardX[baseIndex + s] = x;
                    shardY[baseIndex + s] = y;

                    if (endTime <= t)
                    {
                        samples = s + 1;
                        break;
                    }

                    vx *= 0.995f;
                    vy -= gravity * TableStep;
                    x += vx * TableStep;
                    y += vy * TableStep;

                    if (0.0f > vy && y < groundY)
                    {
                        y = groundY;
                        if (false == bBounced)
                        {
                            bBounced = true;
                            vy = -vy * ShardBounce;
                            vx *= ShardFriction;
                            // 튕긴 뒤에는 남은 수명을 줄여 바닥에 오래 머물지 않게 한다
                            endTime = Mathf.Min(endTime, t + 0.22f);
                        }
                        else
                        {
                            vy = 0.0f;
                            vx *= 0.6f;
                        }
                    }
                }

                shards[i].delay = delay;
                shards[i].endTime = endTime;
                shards[i].sampleCount = samples;
                shards[i].bLarge = Rand01() < 0.3f;
                shards[i].frameOffset = (int)(Rand01() * 4.0f);
                shards[i].spinRate = RandRange(14.0f, 26.0f);
            }
        }

        private void SetupDusts()
        {
            dustTotal = Mathf.Min(DustCapacity, Mathf.RoundToInt(BaseDustCount * density));
            float rx = VFX_TreeBarrier.ShellRadiusXPx;
            float ry = VFX_TreeBarrier.ShellRadiusYPx;
            float cy = VFX_TreeBarrier.ShellCenterYPx;

            for (int i = 0; i < dustTotal; i++)
            {
                float angle = RandRange(0.0f, Mathf.PI * 2.0f);
                float k = Mathf.Sqrt(Rand01()) * 0.95f;

                dusts[i].x0 = Mathf.Cos(angle) * rx * k;
                dusts[i].y0 = cy + Mathf.Sin(angle) * ry * k;
                dusts[i].delay = RandRange(0.14f, 0.36f);
                dusts[i].life = RandRange(0.4f, 0.62f);
                dusts[i].rise = RandRange(10.0f, 22.0f);
                dusts[i].swayPhase = RandRange(0.0f, Mathf.PI * 2.0f);
                float roll = Rand01();
                dusts[i].shape = roll < 0.6f ? PixelPainter.ShapeDot : (roll < 0.85f ? PixelPainter.ShapePlus : PixelPainter.ShapeCross);
            }
        }

        // 빛줄기: 껍질 표면에서 바깥 법선(조금 흔들림) 방향으로 쏘아진다. 쏘는 시각은 파편과 같은 조각 떨어짐 시각표다.
        private void SetupStreaks()
        {
            streakTotal = Mathf.Min(StreakCapacity, Mathf.RoundToInt(BaseStreakCount * density));
            float rx = VFX_TreeBarrier.ShellRadiusXPx;
            float ry = VFX_TreeBarrier.ShellRadiusYPx;
            float cy = VFX_TreeBarrier.ShellCenterYPx;

            for (int i = 0; i < streakTotal; i++)
            {
                float angle = (i + RandRange(0.2f, 0.8f)) / streakTotal * Mathf.PI * 2.0f;
                float k = RandRange(0.92f, 1.0f);
                float x = Mathf.Cos(angle) * rx * k;
                float y = cy + Mathf.Sin(angle) * ry * k;

                Vector2 normal = new Vector2(Mathf.Cos(angle) / rx, Mathf.Sin(angle) / ry).normalized;
                float tilt = RandRange(-0.25f, 0.25f);
                float cos = Mathf.Cos(tilt);
                float sin = Mathf.Sin(tilt);

                streaks[i].x0 = x;
                streaks[i].y0 = y;
                streaks[i].dirX = normal.x * cos - normal.y * sin;
                streaks[i].dirY = normal.x * sin + normal.y * cos;
                streaks[i].speed = RandRange(200.0f, 320.0f);
                streaks[i].life = RandRange(0.28f, 0.42f);
                streaks[i].delay = PopStart + Vector2.Distance(new Vector2(x, y), impactLocalPx) / PopSpeed + RandRange(0.0f, 0.03f);
            }
        }

        private void StreakPosition(int _index, float _time, out int _x, out int _y)
        {
            float travel = streaks[_index].speed / StreakDrag * (1.0f - Mathf.Exp(-StreakDrag * _time));
            _x = originX + Mathf.RoundToInt(streaks[_index].x0 + streaks[_index].dirX * travel);
            _y = originY + Mathf.RoundToInt(streaks[_index].y0 + streaks[_index].dirY * travel - 0.5f * StreakGravity * _time * _time);
        }

        // ---------- 그리기 ----------

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            originX = Mathf.FloorToInt(pos.x * PixelsPerUnit + 0.5f);
            originY = Mathf.FloorToInt(pos.y * PixelsPerUnit + 0.5f);

            painter.Begin(buffer, transform.worldToLocalMatrix, pos.z);

            DrawRing(painter);
            DrawBurst(painter);
            DrawDusts(painter);
            DrawStreaks(painter);
            DrawShards(painter);
            DrawFlash(painter);

            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
        }

        // 바닥 고리: 계단식(0.04초마다 한 단계)으로 커지며 색이 식고, 후반에는 점선 간격이 벌어진다.
        private void DrawRing(PixelPainter _p)
        {
            float t = elapsed - RingStart;
            if (0.0f > t) return;

            int step = Mathf.FloorToInt(t / RingStepTime);
            if (RingStepCount <= step) return;

            float u = (step + 1) / (float)RingStepCount;
            float eased = 1.0f - (1.0f - u) * (1.0f - u);
            int rx = Mathf.RoundToInt(Mathf.Lerp(RingRadiusMin, RingRadiusMax, eased));
            int ry = Mathf.Max(2, Mathf.RoundToInt(rx * GroundSquashY));

            int tone = Mathf.Min(ToneCount - 1, 1 + step / 2);
            int dashOn = step < RingStepCount / 2 ? 3 : 1;
            int dashOff = step < RingStepCount / 2 ? 2 : 3;
            _p.RingArc(originX, originY, rx, ry, dashOn, dashOff, step * 2.0f, toneColors[tone]);
        }

        // 껍질 해방 고리: 껍질 윤곽 타원이 계단식으로 부풀며 밝음 -> 어두움으로 식고, 뒤로 갈수록 점선이 성겨진다.
        private void DrawBurst(PixelPainter _p)
        {
            float t = elapsed - BurstStart;
            if (0.0f > t) return;

            int step = Mathf.FloorToInt(t / BurstStepTime);
            if (BurstStepCount <= step) return;

            float u = step / (float)(BurstStepCount - 1);
            float scale = Mathf.Lerp(BurstScaleMin, BurstScaleMax, 1.0f - (1.0f - u) * (1.0f - u));
            int rx = Mathf.RoundToInt(VFX_TreeBarrier.ShellRadiusXPx * scale);
            int ry = Mathf.RoundToInt(VFX_TreeBarrier.ShellRadiusYPx * scale);
            int cy = originY + Mathf.RoundToInt(VFX_TreeBarrier.ShellCenterYPx);

            int tone = Mathf.Min(ToneCount - 1, step / 2);
            int dashOn = step < 2 ? 1 : (step < 5 ? 4 : 2);
            int dashOff = step < 2 ? 0 : (step < 5 ? 2 : 4);
            _p.RingArc(originX, cy, rx, ry, dashOn, dashOff, -step * 3.0f, toneColors[tone]);
        }

        // 빛줄기: 머리는 처음엔 하얗고, 꼬리(조금 전 위치까지의 직선)는 날아가며 하늘색 -> 파랑으로 식는다. 끝에는 깜빡이며 사라진다.
        private void DrawStreaks(PixelPainter _p)
        {
            for (int i = 0; i < streakTotal; i++)
            {
                float time = elapsed - streaks[i].delay;
                if (0.0f > time || streaks[i].life <= time) continue;

                float fraction = time / streaks[i].life;
                if (0.75f < fraction && 0 == (Mathf.FloorToInt(time * 30.0f) & 1)) continue;

                StreakPosition(i, time, out int hx, out int hy);
                StreakPosition(i, Mathf.Max(0.0f, time - StreakTrailTime), out int tx, out int ty);

                int bodyTone = Mathf.Clamp(Mathf.FloorToInt(fraction * 4.0f) + 1, 1, ToneCount - 1);
                if (hx != tx || hy != ty) _p.Line(tx, ty, hx, hy, 0, 0, 0, toneColors[bodyTone]);
                _p.Dot(hx, hy, toneColors[fraction < 0.45f ? 0 : bodyTone - 1]);
            }
        }

        private void DrawShards(PixelPainter _p)
        {
            for (int i = 0; i < shardTotal; i++)
            {
                float time = elapsed - shards[i].delay;
                if (0.0f > time || shards[i].endTime <= time) continue;

                float fraction = time / shards[i].endTime;
                if (0.7f < fraction && 0 == (Mathf.FloorToInt(time * 30.0f) & 1)) continue;

                int sample = Mathf.Min(Mathf.FloorToInt(time / TableStep), shards[i].sampleCount - 1);
                int baseIndex = i * TableSamples;
                int x = originX + Mathf.RoundToInt(shardX[baseIndex + sample]);
                int y = originY + Mathf.RoundToInt(shardY[baseIndex + sample]);

                int frame = (Mathf.FloorToInt(time * shards[i].spinRate) + shards[i].frameOffset) & 3;
                int[] offsetsX = true == shards[i].bLarge ? LargeShardX[frame] : SmallShardX[frame];
                int[] offsetsY = true == shards[i].bLarge ? LargeShardY[frame] : SmallShardY[frame];

                // 갓 떨어진 파편은 하얗게 번쩍이고, 날아가며 하늘색 -> 파랑으로 식는다. 첫 픽셀이 반사광(가장 밝은 색)이다.
                int bodyTone = Mathf.Clamp(Mathf.FloorToInt(fraction * 4.0f) + 1, 1, ToneCount - 1);
                for (int k = 0; k < offsetsX.Length; k++)
                {
                    int tone = 0 == k ? Mathf.Max(0, bodyTone - 1) : bodyTone;
                    if (time < 0.03f) tone = 0;
                    _p.Dot(x + offsetsX[k], y + offsetsY[k], toneColors[tone]);
                }
            }
        }

        private void DrawDusts(PixelPainter _p)
        {
            for (int i = 0; i < dustTotal; i++)
            {
                float age = elapsed - dusts[i].delay;
                if (0.0f > age || dusts[i].life <= age) continue;

                float fraction = age / dusts[i].life;
                if (0.6f < fraction && 0 == (Mathf.FloorToInt(age * 30.0f) & 1)) continue;

                float x = dusts[i].x0 + Mathf.Sin(age * 7.0f + dusts[i].swayPhase) * 1.5f;
                float y = dusts[i].y0 + dusts[i].rise * fraction;
                int tone = fraction < 0.3f ? 0 : (fraction < 0.65f ? 1 : 2);
                int shape = fraction < 0.5f ? dusts[i].shape : PixelPainter.ShapeDot;
                _p.SpacedShape(shape, originX + Mathf.RoundToInt(x), originY + Mathf.RoundToInt(y), toneColors[tone]);
            }
        }

        // 임팩트 섬광(0~0.1초): 맞은 자리의 원판 + 가로/세로 광선 + 대각선 짧은 광선. 3단계로 줄어든다.
        private void DrawFlash(PixelPainter _p)
        {
            if (0.1f <= elapsed) return;

            int cx = originX + Mathf.RoundToInt(impactLocalPx.x);
            int cy = originY + Mathf.RoundToInt(impactLocalPx.y);
            int stage = elapsed < 0.034f ? 0 : (elapsed < 0.067f ? 1 : 2);
            int ray = 0 == stage ? 12 : (1 == stage ? 8 : 4);
            int diagonal = 0 == stage ? 5 : (1 == stage ? 3 : 0);
            int radius = 0 == stage ? 3 : (1 == stage ? 2 : 1);

            for (int i = 1; i <= ray; i++)
            {
                Color32 color = i < ray * 0.6f ? toneColors[0] : toneColors[2];
                _p.Dot(cx + i, cy, color);
                _p.Dot(cx - i, cy, color);
                _p.Dot(cx, cy + i, color);
                _p.Dot(cx, cy - i, color);
            }

            for (int i = 1; i <= diagonal; i++)
            {
                _p.Dot(cx + i, cy + i, toneColors[1]);
                _p.Dot(cx - i, cy + i, toneColors[1]);
                _p.Dot(cx + i, cy - i, toneColors[1]);
                _p.Dot(cx - i, cy - i, toneColors[1]);
            }

            float rr = radius * radius + radius * 0.5f;
            for (int y = -radius; y <= radius; y++)
            {
                int w = Mathf.FloorToInt(Mathf.Sqrt(Mathf.Max(0.0f, rr - y * y)));
                _p.HLine(cx - w, cx + w, cy + y, toneColors[0]);
            }
        }

        // ---------- 유니티 이벤트 ----------

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            appliedRootSortingLayerID = int.MinValue;
            appliedRootSortingOrder = int.MinValue;
            bBeginPending = true;
            elapsed = 0.0f;

            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (true == bBeginPending)
            {
                bBeginPending = false;
                Begin();
            }

            if (TimelineLength <= elapsed)
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                return;
            }

            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;

            if (null != rootParticleRenderer)
            {
                int rootLayer = rootParticleRenderer.sortingLayerID;
                int rootOrder = rootParticleRenderer.sortingOrder;
                if (rootLayer != appliedRootSortingLayerID || rootOrder != appliedRootSortingOrder)
                {
                    appliedRootSortingLayerID = rootLayer;
                    appliedRootSortingOrder = rootOrder;
                    meshRenderer.sortingLayerID = rootLayer;
                    meshRenderer.sortingOrder = rootOrder + sortingOrderOffset;
                }
            }

            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            // 빛줄기(좌우 약 60px)와 해방 고리(위로 약 75px)까지 덮는다
            Vector3 size = new Vector3(130.0f / PixelsPerUnit + margin, 130.0f / PixelsPerUnit + margin, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(transform.position + new Vector3(0.0f, 22.0f / PixelsPerUnit, 0.0f), size))
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                meshVertexCount = -1;
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }
        }

        private void OnValidate()
        {
            density = Mathf.Clamp(density, 0.25f, 2.0f);
            glowScale = Mathf.Clamp(glowScale, 0.0f, 2.0f);

#if UNITY_EDITOR
            if (true == bInitialized) BuildTones();
#endif
        }
    }
}
