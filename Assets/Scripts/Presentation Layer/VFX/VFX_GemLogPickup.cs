using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 캐릭터가 보석 원목(황금 Fascinating / 다이아 Advanced / 무지개 Perfect)을 주울 때 캐릭터 몸에서 터지는 연출. 원목이 빨려 들어가 사라지는
    /// 발밑에서 작은 빛이 톡 터지고, 보석 색 반짝이가 몸을 타고 솟아오르며, 발밑에 실선 고리가 번지고, 머리 위에 반짝이 별이 켜진다.
    /// 한꺼번에 여러 개를 주우면 터짐과 반짝이는 원목마다 쌓이고, 고리와 별은 짧은 간격을 두고만 새로 켜져 화면이 뭉개지지 않는다.
    ///
    /// 캐릭터를 따라다닌다(모든 조각이 캐릭터 발 위치 기준 픽셀 오프셋). 32 PPU 절차적 쿼드 메쉬(BrandWrapGlow, 정점 알파 = 발광 세기),
    /// 반투명 페이드 없이 계단식 크기 변화와 점멸로만 사라지고 점선은 쓰지 않는다. 고정 크기 배열만 써서 재생 중 힙 할당이 없다.
    /// 그림일 뿐이라 줍기 판정과 무관하다. InDungeonVFXManager가 하나 만들어 쓰고, 쉬는 동안은 컴포넌트를 꺼 둔다.
    /// 오브젝트를 대상의 픽셀 위치로 옮겨 대상 기준으로 그리므로 대상이 움직여도 모양이 같은 프레임은 메쉬를 올리지 않고,
    /// 바닥 고리는 그림을 정하는 상태(단계·점멸·무지개 띠)가 같으면 그리기부터 건너뛴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_GemLogPickup : MonoBehaviour
    {
        private const float PixelsPerUnit = 32f;
        private const float TwoPi = Mathf.PI * 2f;

        // 캐릭터 발 기준 높이(px). 캐릭터 그림은 발에서 머리 꼭대기까지 약 18px이다.
        private const int FeetOffsetY = -1;
        private const int HeadOffsetY = 22;

        // 앞 조각(터짐·반짝이·별)은 날아다니는 것 층(FlyingItem)에 캐릭터 위치 기준 순서로, 고리는 바닥 충격파 층(ShockWave)에 그린다.
        private const int FrontSortingPrecision = 100;
        private const int FrontSortingOffset = 100;
        private const int GroundSortingOrder = 1;

        private const int FrontQuadReserve = 128;
        private const int GroundQuadReserve = 96;

        // 터짐(원목 하나마다)
        private const int PopCapacity = 8;
        private const float PopLife = 0.12f;

        // 몸을 타고 솟는 반짝이
        private const int SparkCapacity = 48;

        // 발밑 고리(짧은 간격으로만 새로 켜진다)
        private const int RingCapacity = 3;
        private const float RingLife = 0.3f;
        private const float RingInterval = 0.1f;

        // 머리 위 별(짧은 간격으로만 새로 켜진다)
        private const int StarCapacity = 3;
        private const float StarDelay = 0.05f;
        private const float StarLife = 0.32f;
        private const float StarInterval = 0.12f;

        private static readonly Color32 WhiteFull = new Color32(255, 255, 255, 255);

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(256);
        private static readonly PixelPainter painter = new PixelPainter();

        // 고리 발광 세 단계(처음 / 중간 / 끝물)와 무지개 띠 색(단계별)을 미리 만들어 둔다.
        private const int RingSteps = 7;
        private static readonly float[] ringGlows = { 0.9f, 0.5f, 0.2f };
        private static readonly Color32[][] ringHuesByGlow = BuildRingHues();

        // 고리 칸 하나의 상태 키 비트 수(바닥 메쉬 다시 그리기 판정용, 아래 GroundKey 참고).
        private const int RingKeyBits = 10;
        private const ulong NoGroundKey = ulong.MaxValue;

        private struct Pop
        {
            public bool bActive;
            public float age;
            public int tier;
            public int x;
            public int y;
        }

        private struct Spark
        {
            public bool bActive;
            public float delay;
            public float age;
            public float life;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public int tier;
            public int seed;
        }

        private struct Ring
        {
            public bool bActive;
            public float age;
            public int tier;
            public float endRadius;
        }

        private struct Star
        {
            public bool bActive;
            public float age;
            public int tier;
            public int x;
            public int arm;
        }

        private MeshRenderer frontRenderer;
        private Mesh frontMesh;
        private ulong frontHash;
        private int frontVertexCount = -1;

        private Transform groundTransform;
        private MeshRenderer groundRenderer;
        private Mesh groundMesh;
        private ulong groundHash;
        private int groundVertexCount = -1;

        private readonly Pop[] pops = new Pop[PopCapacity];
        private readonly Spark[] sparks = new Spark[SparkCapacity];
        private readonly Ring[] rings = new Ring[RingCapacity];
        private readonly Star[] stars = new Star[StarCapacity];
        private int popCursor;
        private int sparkCursor;
        private int ringCursor;
        private int starCursor;

        // 살아 있는 조각 수(종류별). 0인 종류는 매 프레임 배열을 돌지 않는다.
        private int livePops;
        private int liveSparks;
        private int liveRings;
        private int liveStars;

        private Transform anchor;
        private float clock;
        private float lastRingTime = float.NegativeInfinity;
        private float lastStarTime = float.NegativeInfinity;
        private int appliedFrontOrder = int.MinValue;
        private bool bPlaying;
        private uint randomState = 1u;

        // 이 오브젝트를 대상의 픽셀 위치로 옮겨 두고 조각은 그 기준 오프셋으로 그린다. 그러면 메쉬 내용이 대상의 이동과 무관해져
        // 대상이 달려도 조각 모양이 그대로인 프레임은 메쉬를 다시 올리지 않는다(화면 결과는 월드 좌표로 그릴 때와 비트 단위로 같다).
        private int placedX = int.MinValue;
        private int placedY = int.MinValue;
        private ulong appliedGroundKey = NoGroundKey;

        public bool IsPlaying => bPlaying;

        public static VFX_GemLogPickup Create(Transform _parent)
        {
            if (null == GemVfxShared.Material) return null;

            GameObject go = new GameObject("GemLogPickupVFX");
            go.transform.SetParent(_parent, false);
            VFX_GemLogPickup vfx = go.AddComponent<VFX_GemLogPickup>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>
        /// 보석 원목 하나를 주웠다. _anchor는 원목이 빨려 들어간 캐릭터(발 위치), _tier는 1 황금 / 2 다이아 / 3 무지개.
        /// 재생 중에 다시 부르면 조각이 더해진다.
        /// </summary>
        public void Play(Transform _anchor, int _tier)
        {
            if (null == _anchor) return;

            if (anchor != _anchor)
            {
                // 다른 대상으로 옮겨 가면 남은 조각은 버린다(조각은 대상 기준 오프셋이라 그대로 두면 엉뚱한 자리로 튄다).
                Halt();
                anchor = _anchor;
            }

            int tier = Mathf.Clamp(_tier, 1, 3);
            if (false == bPlaying)
            {
                clock = 0f;
                lastRingTime = float.NegativeInfinity;
                lastStarTime = float.NegativeInfinity;
                frontHash = 0UL;
                frontVertexCount = -1;
                groundHash = 0UL;
                groundVertexCount = -1;
                appliedGroundKey = NoGroundKey;
            }

            AddPop(tier);
            AddSparks(tier);

            if (clock - lastRingTime >= RingInterval)
            {
                lastRingTime = clock;
                AddRing(tier);
            }

            if (clock - lastStarTime >= StarInterval)
            {
                lastStarTime = clock;
                AddStar(tier);
            }

            bPlaying = true;
            enabled = true;
        }

        public void StopImmediate()
        {
            Halt();
            if (enabled) enabled = false;
        }

        private void Halt()
        {
            bPlaying = false;
            anchor = null;
            for (int i = 0; i < PopCapacity; i++) pops[i].bActive = false;
            for (int i = 0; i < SparkCapacity; i++) sparks[i].bActive = false;
            for (int i = 0; i < RingCapacity; i++) rings[i].bActive = false;
            for (int i = 0; i < StarCapacity; i++) stars[i].bActive = false;
            livePops = 0;
            liveSparks = 0;
            liveRings = 0;
            liveStars = 0;
            placedX = int.MinValue;
            placedY = int.MinValue;
            if (null != frontRenderer) frontRenderer.enabled = false;
            if (null != groundRenderer) groundRenderer.enabled = false;
        }

        // //조각 추가

        // 원목이 사라진 발밑 자리(좌우로 1px 흔들어 겹친 터짐이 한 덩어리로 보이지 않게 한다).
        private void AddPop(int _tier)
        {
            if (false == pops[popCursor].bActive) livePops++;
            pops[popCursor] = new Pop
            {
                bActive = true,
                age = 0f,
                tier = _tier,
                x = Mathf.RoundToInt((Rand01() - 0.5f) * 4f),
                y = FeetOffsetY + Mathf.RoundToInt(Rand01() * 2f),
            };
            popCursor = (popCursor + 1) % PopCapacity;
        }

        // 발밑에서 몸을 타고 솟는 반짝이. 단계가 높을수록 많고 높이 솟는다.
        private void AddSparks(int _tier)
        {
            int count = 1 == _tier ? 3 : (2 == _tier ? 4 : 6);
            float rise = 1 == _tier ? 90f : (2 == _tier ? 105f : 120f);
            for (int i = 0; i < count; i++)
            {
                float spread = count > 1 ? (i / (float)(count - 1) - 0.5f) : 0f;
                if (false == sparks[sparkCursor].bActive) liveSparks++;
                sparks[sparkCursor] = new Spark
                {
                    bActive = true,
                    delay = i * 0.03f,
                    age = 0f,
                    life = Mathf.Lerp(0.38f, 0.52f, Rand01()),
                    x = spread * 8f + (Rand01() - 0.5f) * 2f,
                    y = FeetOffsetY + Rand01() * 3f,
                    vx = spread * 36f + (Rand01() - 0.5f) * 10f,
                    vy = rise * Mathf.Lerp(0.8f, 1.15f, Rand01()),
                    tier = _tier,
                    seed = (int)(Rand01() * 997f),
                };
                sparkCursor = (sparkCursor + 1) % SparkCapacity;
            }
        }

        private void AddRing(int _tier)
        {
            if (false == rings[ringCursor].bActive) liveRings++;
            rings[ringCursor] = new Ring
            {
                bActive = true,
                age = 0f,
                tier = _tier,
                endRadius = 1 == _tier ? 12f : (2 == _tier ? 14f : 16f),
            };
            ringCursor = (ringCursor + 1) % RingCapacity;
        }

        // 머리 위 별: 좌우로 조금 비켜 켠다.
        private void AddStar(int _tier)
        {
            if (false == stars[starCursor].bActive) liveStars++;
            stars[starCursor] = new Star
            {
                bActive = true,
                age = 0f,
                tier = _tier,
                x = (0 == (starCursor & 1) ? 1 : -1) * Mathf.RoundToInt(Mathf.Lerp(4f, 8f, Rand01())),
                arm = 1 == _tier ? 4 : (2 == _tier ? 5 : 6),
            };
            starCursor = (starCursor + 1) % StarCapacity;
        }

        // //초기화

        private void Initialize()
        {
            frontRenderer = GemVfxShared.CreateMeshRenderer(gameObject, "VFX_GemLogPickup_Front", FrontQuadReserve, out frontMesh);
            frontRenderer.sortingLayerID = GemVfxShared.FlyingItemLayerId;

            GameObject ground = new GameObject("Ground");
            groundTransform = ground.transform;
            groundTransform.SetParent(transform, false);
            groundRenderer = GemVfxShared.CreateMeshRenderer(ground, "VFX_GemLogPickup_Ground", GroundQuadReserve, out groundMesh);
            groundRenderer.sortingLayerID = GemVfxShared.ShockWaveLayerId;
            groundRenderer.sortingOrder = GroundSortingOrder;

            randomState = unchecked((uint)Random.Range(1, int.MaxValue) * 2654435761u) | 1u;
            enabled = false;
        }

        private static Color32[][] BuildRingHues()
        {
            Color32[][] table = new Color32[ringGlows.Length][];
            for (int i = 0; i < ringGlows.Length; i++)
            {
                table[i] = new Color32[GemTierPalette.HueCount];
                GemTierPalette.FillHuesWithGlow(table[i], ringGlows[i]);
            }

            return table;
        }

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535f;
        }

        // //매 프레임

        private void LateUpdate()
        {
            if (false == bPlaying || null == anchor || false == anchor.gameObject.activeInHierarchy)
            {
                StopImmediate();
                return;
            }

            if (false == Simulate(Time.deltaTime))
            {
                StopImmediate();
                return;
            }

            Render();
        }

        // 시간을 흘린다. 남은 조각이 없으면 false.
        private bool Simulate(float _dt)
        {
            clock += _dt;

            if (0 < livePops)
            {
                for (int i = 0; i < PopCapacity; i++)
                {
                    if (false == pops[i].bActive) continue;
                    pops[i].age += _dt;
                    if (pops[i].age >= PopLife)
                    {
                        pops[i].bActive = false;
                        livePops--;
                    }
                }
            }

            if (0 < liveSparks)
            {
                float damping = Mathf.Exp(-3.2f * _dt);
                for (int i = 0; i < SparkCapacity; i++)
                {
                    if (false == sparks[i].bActive) continue;
                    if (sparks[i].delay > 0f)
                    {
                        sparks[i].delay -= _dt;
                        continue;
                    }

                    sparks[i].age += _dt;
                    sparks[i].vx *= damping;
                    sparks[i].vy *= damping;
                    sparks[i].x += sparks[i].vx * _dt;
                    sparks[i].y += sparks[i].vy * _dt;
                    if (sparks[i].age >= sparks[i].life)
                    {
                        sparks[i].bActive = false;
                        liveSparks--;
                    }
                }
            }

            if (0 < liveRings)
            {
                for (int i = 0; i < RingCapacity; i++)
                {
                    if (false == rings[i].bActive) continue;
                    rings[i].age += _dt;
                    if (rings[i].age >= RingLife)
                    {
                        rings[i].bActive = false;
                        liveRings--;
                    }
                }
            }

            if (0 < liveStars)
            {
                for (int i = 0; i < StarCapacity; i++)
                {
                    if (false == stars[i].bActive) continue;
                    stars[i].age += _dt;
                    if (stars[i].age >= StarDelay + StarLife)
                    {
                        stars[i].bActive = false;
                        liveStars--;
                    }
                }
            }

            return 0 < livePops + liveSparks + liveRings + liveStars;
        }

        private void Render()
        {
            Vector3 anchorPos = anchor.position;
            int ax = Mathf.RoundToInt(anchorPos.x * PixelsPerUnit);
            int ay = Mathf.RoundToInt(anchorPos.y * PixelsPerUnit);

            int order = -Mathf.RoundToInt(anchorPos.y * FrontSortingPrecision) + FrontSortingOffset;
            if (order != appliedFrontOrder)
            {
                appliedFrontOrder = order;
                frontRenderer.sortingOrder = order;
            }

            if (ax != placedX || ay != placedY)
            {
                placedX = ax;
                placedY = ay;
                transform.position = new Vector3(ax / PixelsPerUnit, ay / PixelsPerUnit, 0f);
            }

            // 바닥 고리는 계단식으로만 바뀌므로 그림을 정하는 상태가 직전과 같으면 그리기부터 건너뛴다(메쉬는 대상 기준이라 그대로 맞다).
            ulong groundKey = GroundKey();
            if (groundKey != appliedGroundKey || 0 > groundVertexCount)
            {
                appliedGroundKey = groundKey;
                painter.Begin(buffer, groundTransform.worldToLocalMatrix, 0f);
                if (0 != groundKey) DrawRings(ax, ay);
                painter.Commit(groundMesh, groundRenderer, ref groundHash, ref groundVertexCount);
            }

            painter.Begin(buffer, transform.worldToLocalMatrix, 0f);
            if (0 < liveSparks) DrawSparks(ax, ay);
            if (0 < livePops) DrawPops(ax, ay);
            if (0 < liveStars) DrawStars(ax, ay);
            painter.Commit(frontMesh, frontRenderer, ref frontHash, ref frontVertexCount);
        }

        // 고리 하나가 이번 프레임에 그릴 모양의 단계(0 ~ RingSteps-1). 끝물 점멸로 꺼진 프레임이거나 죽었으면 -1.
        private int RingStep(int _r)
        {
            if (false == rings[_r].bActive) return -1;

            float age = rings[_r].age;
            if (age / RingLife > 0.82f && 0 != ((int)(age * 30f) & 1)) return -1;
            return (int)Mathf.Floor(age / RingLife * RingSteps);
        }

        // 바닥 메쉬 내용을 정하는 상태 전부를 칸 순서대로 겹치지 않게 비트로 묶는다(칸마다 RingKeyBits).
        // 칸 하나: 보임 1비트 + 단계 3비트 + 보석 단계 2비트 + 무지개 띠 시작 색 4비트(무지개 둘레를 그리는 단계에서만, 아니면 0).
        // 0이면 그릴 것이 없다.
        private ulong GroundKey()
        {
            if (0 == liveRings) return 0UL;

            ulong key = 0UL;
            for (int r = 0; r < RingCapacity; r++)
            {
                int step = RingStep(r);
                if (0 > step) continue;

                int tier = rings[r].tier;
                float u = step / (float)(RingSteps - 1);
                ulong band = 0UL;
                if (3 <= tier && u >= 0.15f) band = 1UL + (ulong)((int)(rings[r].age * 30f) % GemTierPalette.HueCount);

                ulong cell = 1UL | ((ulong)step << 1) | ((ulong)tier << 4) | (band << 6);
                key |= cell << (r * RingKeyBits);
            }

            return key;
        }

        // //그리기

        // 터짐: 흰 덩이로 번쩍 -> 보석 색 대각 광선 4갈래 -> 끝점 네 개만 남았다 사라진다.
        private void DrawPops(int _ax, int _ay)
        {
            for (int i = 0; i < PopCapacity; i++)
            {
                if (false == pops[i].bActive) continue;

                int x = _ax + pops[i].x;
                int y = _ay + pops[i].y;
                float a = pops[i].age;
                int tier = pops[i].tier;

                if (a < 0.035f)
                {
                    painter.Shape(PixelPainter.ShapeCircle, x, y, WhiteFull);
                    painter.HLine(x - 4, x + 3, y, WhiteFull);
                }
                else if (a < 0.08f)
                {
                    Color32 ray = GemTierPalette.WithGlow(GemTierPalette.TierOrHue(tier, 1, i * 2), 0.85f);
                    for (int d = 1; d <= 3; d++)
                    {
                        painter.Dot(x + d, y + d, ray);
                        painter.Dot(x - d, y + d, ray);
                        painter.Dot(x + d, y - d, ray);
                        painter.Dot(x - d, y - d, ray);
                    }
                    painter.Shape(PixelPainter.ShapePlus, x, y, WhiteFull);
                }
                else
                {
                    Color32 tip = GemTierPalette.WithGlow(GemTierPalette.TierOrHue(tier, 2, i * 2 + 1), 0.45f);
                    painter.Dot(x + 4, y + 4, tip);
                    painter.Dot(x - 4, y + 4, tip);
                    painter.Dot(x + 4, y - 4, tip);
                    painter.Dot(x - 4, y - 4, tip);
                }
            }
        }

        // 몸을 타고 솟는 반짝이: + 로 솟아(머리 흰빛) 점으로 줄고, 끝물에 점멸한다.
        private void DrawSparks(int _ax, int _ay)
        {
            for (int i = 0; i < SparkCapacity; i++)
            {
                if (false == sparks[i].bActive || sparks[i].delay > 0f) continue;

                float u = sparks[i].age / sparks[i].life;
                if (u > 0.72f && 0 != ((int)(sparks[i].age * 30f) & 1)) continue;

                int x = _ax + Mathf.RoundToInt(sparks[i].x);
                int y = _ay + Mathf.RoundToInt(sparks[i].y);
                int tier = sparks[i].tier;
                Color32 color = u < 0.18f
                    ? WhiteFull
                    : GemTierPalette.WithGlow(GemTierPalette.TierOrHue(tier, u < 0.55f ? 1 : 2, sparks[i].seed), u < 0.55f ? 0.8f : 0.4f);

                if (u < 0.4f) painter.Shape(0 == (sparks[i].seed & 1) ? PixelPainter.ShapePlus : PixelPainter.ShapeCross, x, y, color);
                else painter.Dot(x, y, color);
            }
        }

        // 발밑 고리: 끊기지 않는 실선으로 계단식으로 커지며 흰빛 -> 보석 색 -> 진한 색으로 식고, 마지막에 깜빡이며 사라진다(처음 잠깐은 두 겹).
        private void DrawRings(int _ax, int _ay)
        {
            int cx = _ax;
            int cy = _ay + FeetOffsetY;
            for (int r = 0; r < RingCapacity; r++)
            {
                int step = RingStep(r);
                if (0 > step) continue;

                float age = rings[r].age;
                float u = step / (float)(RingSteps - 1);
                float grow = 1f - (1f - u) * (1f - u);
                int rx = Mathf.RoundToInt(Mathf.Lerp(4f, rings[r].endRadius, grow));
                int ry = Mathf.Max(1, Mathf.RoundToInt(rx * 0.5f));
                int glowIndex = u < 0.25f ? 0 : (u < 0.6f ? 1 : 2);

                DrawRingOutline(cx, cy, rx, ry, rings[r].tier, u, glowIndex, age);
                if (u < 0.3f && rx > 2) DrawRingOutline(cx, cy, rx - 1, Mathf.Max(1, ry - 1), rings[r].tier, u, glowIndex, age);
            }
        }

        private void DrawRingOutline(int _cx, int _cy, int _rx, int _ry, int _tier, float _u, int _glowIndex, float _age)
        {
            if (3 > _tier || _u < 0.15f)
            {
                Color32 color = _u < 0.25f ? GemTierPalette.White : GemTierPalette.Tier(_tier, _u < 0.6f ? 1 : 3);
                painter.RingArc(_cx, _cy, _rx, _ry, 1, 0, 0f, GemTierPalette.WithGlow(color, ringGlows[_glowIndex]));
                return;
            }

            // 무지개: 둘레를 따라 띠 여섯 색이 두 번 돌고 시계 방향으로 흐른다.
            int count = PixelEllipse.Build(_rx, _ry);
            if (count <= 0) return;

            Color32[] ringHues = ringHuesByGlow[_glowIndex];
            int bands = GemTierPalette.HueCount * 2;
            int band = (int)(_age * 30f);
            int remainder = 0;
            for (int i = 0; i < count; i++)
            {
                painter.Dot(_cx + PixelEllipse.OutX[i], _cy + PixelEllipse.OutY[i], ringHues[band % GemTierPalette.HueCount]);

                remainder += bands;
                while (remainder >= count)
                {
                    remainder -= count;
                    band++;
                }
            }
        }

        // 머리 위 별: 작은 + 로 켜져 4갈래 별(가운데 흰 3x3 + 대각 광선)로 번쩍였다가 계단식으로 줄고 점멸하며 사라진다.
        private void DrawStars(int _ax, int _ay)
        {
            for (int s = 0; s < StarCapacity; s++)
            {
                if (false == stars[s].bActive) continue;

                float starAge = stars[s].age - StarDelay;
                if (starAge < 0f) continue;

                float u = starAge / StarLife;
                int x = _ax + stars[s].x;
                int y = _ay + HeadOffsetY;
                int armMax = stars[s].arm;
                Color32 armColor = GemTierPalette.WithGlow(GemTierPalette.TierOrHue(stars[s].tier, 1, s * 2 + (int)(starAge * 20f)), 0.8f);

                if (u < 0.15f)
                {
                    painter.Shape(PixelPainter.ShapePlus, x, y, WhiteFull);
                }
                else if (u < 0.45f)
                {
                    painter.HLine(x - armMax, x + armMax, y, armColor);
                    painter.Rect(x, y - armMax, x + 1, y + armMax + 1, armColor);
                    painter.HLine(x - armMax / 2, x + armMax / 2, y, WhiteFull);
                    painter.Rect(x, y - armMax / 2, x + 1, y + armMax / 2 + 1, WhiteFull);
                    painter.Rect(x - 1, y - 1, x + 2, y + 2, WhiteFull);
                    for (int d = 2; d <= armMax / 3 + 1; d++)
                    {
                        painter.Dot(x + d, y + d, armColor);
                        painter.Dot(x - d, y + d, armColor);
                        painter.Dot(x + d, y - d, armColor);
                        painter.Dot(x - d, y - d, armColor);
                    }
                }
                else if (u < 0.72f)
                {
                    int arm = Mathf.Max(2, armMax * 3 / 5);
                    painter.HLine(x - arm, x + arm, y, armColor);
                    painter.Rect(x, y - arm, x + 1, y + arm + 1, armColor);
                    painter.Dot(x, y, WhiteFull);
                }
                else
                {
                    if (0 != ((int)(starAge * 30f) & 1)) continue;
                    painter.Shape(PixelPainter.ShapePlus, x, y, armColor);
                }
            }
        }

        // //수명

        private void OnDisable()
        {
            Halt();
        }

        private void OnDestroy()
        {
            if (null != frontMesh)
            {
                Destroy(frontMesh);
                frontMesh = null;
            }

            if (null != groundMesh)
            {
                Destroy(groundMesh);
                groundMesh = null;
            }
        }
    }
}
