using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 다이아·무지개 보석 나무가 쓰러지며 튄 원목 하나를 따라가는 잔광 꼬리. 원목이 포물선으로 나는 동안 지나온 자리에 1px 빛줄을 남기고
    /// 반짝이 알갱이를 몇 개 흘린다(다이아는 푸른빛, 무지개는 띠 색이 흐른다). 원목이 땅에 닿으면 꼬리가 짧아지며 사라진다.
    ///
    /// 원목을 읽기만 하고 아무것도 바꾸지 않는다. 정렬은 매 프레임 원목 스프라이트의 층·순서 바로 뒤(보석 아우라보다도 뒤)를 따른다.
    /// 32 PPU 절차적 쿼드 메쉬(BrandWrapGlow), 고정 크기 배열만 쓴다. InDungeonVFXManager가 런타임에 만들어 돌려 쓴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_GemLogTrail : MonoBehaviour
    {
        private const float PixelsPerUnit = 32f;

        // 메쉬 버퍼를 미리 잡아 둘 크기(쿼드). 꼬리 두 줄 + 반짝이 열 개를 넉넉히 덮는다. 넘으면 그때 늘린다.
        private const int QuadReserve = 160;

        // 원목 스프라이트 정렬 순서에서 뺄 값. 보석 아우라가 -2이므로 그보다 뒤에 둔다.
        private const int SortingBehindLog = 3;

        // 지나온 자리 기록
        private const int HistoryCapacity = 24;
        private const float TrailLife = 0.16f;          // 이만큼 지난 자리까지 꼬리로 그린다

        // 흘리는 반짝이
        private const int SparkCapacity = 10;
        private const float SparkInterval = 0.05f;
        private const float SparkLife = 0.32f;

        // 원목이 땅에 닿은 뒤 남은 꼬리가 다 사라질 때까지
        private const float FadeOutTime = TrailLife + SparkLife;

        // 원목을 놓쳤을 때를 대비한 상한(정상 비행은 1초 안쪽)
        private const float MaxDuration = 2.5f;

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(256);
        private static readonly PixelPainter painter = new PixelPainter();

        private struct Sample
        {
            public int x;
            public int y;
            public float time;
        }

        private struct Spark
        {
            public bool bActive;
            public float x;
            public float y;
            public float vy;
            public float age;
            public int seed;
        }

        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ulong meshHash;
        private int meshVertexCount = -1;
        private int appliedSortingLayerId = int.MinValue;
        private int appliedSortingOrder = int.MinValue;

        private readonly Sample[] history = new Sample[HistoryCapacity];
        private int historyStart;
        private int historyCount;
        private readonly Spark[] sparks = new Spark[SparkCapacity];
        private int sparkCursor;
        private float sparkTimer;

        private LogItem log;
        private bool bPlaying;
        private bool bFollowing;
        private float age;
        private float releasedAt;
        private int tier;
        private uint randomState = 1u;

        public bool IsPlaying => bPlaying;

        public static VFX_GemLogTrail Create(Transform _parent)
        {
            if (null == GemVfxShared.Material) return null;

            GameObject go = new GameObject("GemLogTrailVFX");
            go.transform.SetParent(_parent, false);
            VFX_GemLogTrail vfx = go.AddComponent<VFX_GemLogTrail>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>날기 시작한 _log를 따라간다. _tier: 2 다이아(푸른빛), 3 무지개.</summary>
        public void Play(LogItem _log, int _tier)
        {
            log = _log;
            tier = Mathf.Clamp(_tier, 2, 3);
            historyStart = 0;
            historyCount = 0;
            sparkCursor = 0;
            sparkTimer = SparkInterval * Rand01();
            for (int i = 0; i < SparkCapacity; i++) sparks[i].bActive = false;
            age = 0f;
            releasedAt = 0f;
            bFollowing = null != _log;
            bPlaying = bFollowing;
            meshHash = 0UL;
            meshVertexCount = -1;
            meshRenderer.enabled = false;

            // 쉬는 동안은 컴포넌트를 꺼 두어 매 프레임 LateUpdate 호출 자체가 없게 한다.
            enabled = bPlaying;
        }

        public void StopImmediate()
        {
            Halt();
            if (enabled) enabled = false;
        }

        private void Halt()
        {
            bPlaying = false;
            bFollowing = false;
            log = null;
            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        private void Initialize()
        {
            meshRenderer = GemVfxShared.CreateMeshRenderer(gameObject, "VFX_GemLogTrail", QuadReserve, out mesh);
            randomState = unchecked((uint)Random.Range(1, int.MaxValue) * 2654435761u) | 1u;
            enabled = false;
        }

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535f;
        }

        private void LateUpdate()
        {
            if (false == bPlaying)
            {
                enabled = false;
                return;
            }

            float dt = Time.deltaTime;
            age += dt;

            // 원목이 땅에 닿았거나(포물선 비행이 끝남) 풀로 돌아갔으면 더는 따라가지 않고 남은 꼬리만 사라지게 둔다.
            if (bFollowing && (null == log || false == log.isActiveAndEnabled || false == log.IsLaunching || age > MaxDuration))
            {
                bFollowing = false;
                log = null;
                releasedAt = age;
            }

            if (bFollowing)
            {
                Vector3 pos = log.VisualPosition;
                int x = Mathf.RoundToInt(pos.x * PixelsPerUnit);
                int y = Mathf.RoundToInt(pos.y * PixelsPerUnit);
                PushSample(x, y);

                // 원목 정렬은 날아가는 동안 바뀔 수 있어 매 프레임 읽되, 렌더러에는 바뀌었을 때만 쓴다.
                int layerId;
                int order;
                log.GetSpriteSorting(out layerId, out order);
                order -= SortingBehindLog;
                if (layerId != appliedSortingLayerId)
                {
                    appliedSortingLayerId = layerId;
                    meshRenderer.sortingLayerID = layerId;
                }

                if (order != appliedSortingOrder)
                {
                    appliedSortingOrder = order;
                    meshRenderer.sortingOrder = order;
                }

                sparkTimer -= dt;
                if (sparkTimer <= 0f)
                {
                    sparkTimer += SparkInterval;
                    sparks[sparkCursor] = new Spark
                    {
                        bActive = true,
                        x = x + (Rand01() - 0.5f) * 6f,
                        y = y + (Rand01() - 0.5f) * 4f,
                        vy = -Mathf.Lerp(10f, 30f, Rand01()),
                        age = 0f,
                        seed = (int)(Rand01() * 997f),
                    };
                    sparkCursor = (sparkCursor + 1) % SparkCapacity;
                }
            }
            else if (age - releasedAt >= FadeOutTime)
            {
                StopImmediate();
                return;
            }

            for (int i = 0; i < SparkCapacity; i++)
            {
                if (false == sparks[i].bActive) continue;
                sparks[i].age += dt;
                sparks[i].y += sparks[i].vy * dt;
                if (sparks[i].age >= SparkLife) sparks[i].bActive = false;
            }

            Draw();
        }

        private void PushSample(int _x, int _y)
        {
            if (historyCount > 0)
            {
                Sample last = history[(historyStart + historyCount - 1) % HistoryCapacity];
                if (last.x == _x && last.y == _y) return;
            }

            if (historyCount == HistoryCapacity)
            {
                historyStart = (historyStart + 1) % HistoryCapacity;
                historyCount--;
            }

            history[(historyStart + historyCount) % HistoryCapacity] = new Sample { x = _x, y = _y, time = age };
            historyCount++;
        }

        private void Draw()
        {
            painter.Begin(buffer, transform.worldToLocalMatrix, 0f);

            // 꼬리: 최근 자리일수록 밝다(흰빛 -> 보석 색 -> 진한 색). 가장 최근 자리(원목 바로 밑)는 원목에 가려지므로 그리지 않는다.
            for (int i = historyCount - 2; i >= 1; i--)
            {
                Sample a = history[(historyStart + i) % HistoryCapacity];
                Sample b = history[(historyStart + i - 1) % HistoryCapacity];
                float sampleAge = age - a.time;
                if (sampleAge >= TrailLife) break;

                float u = sampleAge / TrailLife;
                Color32 color = 3 <= tier
                    ? GemTierPalette.WithGlow(u < 0.15f ? GemTierPalette.White : GemTierPalette.Hue(i + (int)(age * 18f)), u < 0.5f ? 0.8f : 0.35f)
                    : GemTierPalette.WithGlow(GemTierPalette.Tier(2, u < 0.15f ? 0 : (u < 0.5f ? 1 : (u < 0.8f ? 2 : 3))), u < 0.5f ? 0.8f : 0.3f);
                painter.Line(b.x, b.y, a.x, a.y, 0, 0, 0, color);

                // 머리 쪽 절반은 한 줄 더 겹쳐 2px로 굵게 보인다.
                if (u < 0.5f) painter.Line(b.x, b.y - 1, a.x, a.y - 1, 0, 0, 0, color);
            }

            for (int i = 0; i < SparkCapacity; i++)
            {
                if (false == sparks[i].bActive) continue;

                float u = sparks[i].age / SparkLife;
                if (u > 0.7f && 0 != ((int)(sparks[i].age * 30f) & 1)) continue;

                int px = Mathf.RoundToInt(sparks[i].x);
                int py = Mathf.RoundToInt(sparks[i].y);
                Color32 color = GemTierPalette.WithGlow(
                    u < 0.25f ? GemTierPalette.White : GemTierPalette.TierOrHue(tier, 1, sparks[i].seed),
                    u < 0.5f ? 0.9f : 0.4f);

                if (u < 0.45f) painter.Shape(0 == (sparks[i].seed & 1) ? PixelPainter.ShapePlus : PixelPainter.ShapeCross, px, py, color);
                else painter.Dot(px, py, color);
            }

            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
        }

        private void OnDisable()
        {
            Halt();
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }
        }
    }
}
