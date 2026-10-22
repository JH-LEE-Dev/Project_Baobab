using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 로그라이크 스텔라(별) 폭발 연출. 별 표식의 "타원 궤도를 빙빙 도는 별" 느낌을 그대로 가져와, 별이 모였다가 터지면
    /// 궤도 3겹이 바깥으로 퍼지고 궤도를 도는 별에서 별가루가 흘러 바닥으로 싸악 떨어지며 퍼진다.
    /// 스프라이트 대신 32 PPU 격자에 맞춘 절차적 쿼드 메쉬로 매 프레임 다시 그린다(별자리 노바와 같은 방식).
    ///
    /// 박자(초): 0~0.22 별이 모임(차징) -> 도트 별 섬광 -> 궤도 3겹 확산 + 별가루 낙하 + 바닥 충격 고리 -> 바닥에 내려앉아 반짝이다 사라짐.
    ///
    /// 렌더링은 바닥 메쉬(충격 고리, 충격 띠, 내려앉은 별가루)와 공중 메쉬(별, 낙하 별가루, 섬광)로 나뉜다.
    /// 바닥 메쉬는 나무(Objects 레이어)보다 아래 레이어(ShockWave)에, 공중 메쉬는 루트 렌더러의 레이어에 그려진다.
    ///
    /// 범위 조절: SetRadius(월드 유닛)로 정한다(0 이하이면 maxRadius). 가장 바깥 궤도와 충격 고리, 별가루 낙하 범위가 이 값에 비례하고
    /// 별 수도 반지름에 맞춰 늘어난다. 반지름은 피격 범위(바닥 타원의 가로 반지름)와 같은 값을 넘긴다.
    ///
    /// 픽셀 규칙: JAGGIES(타원은 midpoint 한 가지), DOUBLE PIXEL(윤곽/선의 L자 모서리 제거), HUGGING(충격 고리끼리 3px 이상 띄움).
    /// 입자는 `.` `+` `x` `●` `◆` 다섯 가지만 쓰고, 모든 그림은 매 프레임(30fps 이상) 경과 시간으로 다시 계산한다.
    /// 정적 버퍼와 고정 크기 배열만 써서 재생 중 힙 할당이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_StellaBurst : MonoBehaviour
    {
        [Header("범위 (월드 유닛, 32 PPU 기준 1유닛 = 32px)")]
        [SerializeField, Tooltip("가장 바깥 궤도와 충격 고리가 닿는 반지름. 재생 직전에 SetRadius로 덮어쓸 수 있다.")]
        private float maxRadius = 3.0f;

        [Header("밀도")]
        [SerializeField, Tooltip("별, 별가루, 방사 별가루 개수에 곱하는 전체 배율. 1이 기본이다.")]
        private float density = 1.0f;

        [Header("형태")]
        [SerializeField, Tooltip("별이 바닥(재생 위치)에서 떠 있는 높이(px). 별 그림의 중심 높이에 맞춘다.")]
        private float starHeightPx = 34.0f;

        [Header("정렬")]
        [SerializeField, Tooltip("공중 메쉬를 루트 렌더러(VFXComponent가 정한 소팅)보다 이 값만큼 위에 그린다")]
        private int sortingOrderOffset = 0;

        [Header("바닥 메쉬 (충격 고리가 나무/캐릭터 뒤에 그려지도록 분리)")]
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField, Tooltip("나무/캐릭터가 있는 Objects보다 아래여야 한다. 도끼 충격파와 같은 ShockWave가 기본이다.")]
        private string groundSortingLayerName = "ShockWave";
        [SerializeField] private int groundSortingOrder = 0;

        // 바닥 타원의 세로 비율(쿼터뷰). 피격 범위 계산과 같은 값이다.
        public const float GroundSquashY = 0.5f;

        private const float PixelsPerUnit = 32.0f;
        private const float TwoPi = Mathf.PI * 2.0f;

        // 전체 타임라인(초). 루트 ParticleSystem 수명은 이 값 이상이어야 한다.
        private const float TimelineLength = 2.8f;
        private const float ChargeTime = 0.22f;
        private const int RingCount = 3;
        private const int MaxStarsPerRing = 40;
        private const int StarCapacity = RingCount * MaxStarsPerRing;
        private const int DustCapacity = 1600;
        private const int SparkCapacity = 96;
        private const int QuadBudget = 6000;

        // 별 하나가 별가루를 흘리는 간격(초). 크게 잡을수록 성기다(눈 피로를 줄이려고 담백하게 둔다).
        private const float DustSpawnStep = 0.25f;
        private const int HeroStarInterval = 8;
        private const int SparkBaseCount = 8;
        private const int SparkTrailDots = 1;
        private const float LandedDustLife = 0.4f;
        private const float StarFlashStage = 0.046f;
        private const float GroundGlow = 0.28f;
        private const float BandGlow = 0.12f;

        private static readonly float[] RingDelay = { 0.02f, 0.12f, 0.24f };
        private static readonly float[] RingFactor = { 0.5f, 0.78f, 1.0f };
        private static readonly float[] RingLife = { 1.0f, 1.1f, 1.2f };
        private static readonly float[] RingSpin = { 3.4f, -2.7f, 2.1f };
        private static readonly float[] RingHeightScale = { 1.0f, 0.85f, 0.7f };
        private static readonly int[] StarFlashArm = { 16, 13, 10, 8, 6, 4, 2 };

        // 낙하 중 모양이 줄어드는 순서(◆ ● + x .). 종류 0은 큰 별가루, 1은 중간, 2는 작은 별가루.
        private static readonly int[][] DustShapes =
        {
            new[] { PixelPainter.ShapeDiamond, PixelPainter.ShapeCircle, PixelPainter.ShapePlus, PixelPainter.ShapePlus, PixelPainter.ShapeDot },
            new[] { PixelPainter.ShapeCircle, PixelPainter.ShapePlus, PixelPainter.ShapeDot },
            new[] { PixelPainter.ShapePlus, PixelPainter.ShapeCross, PixelPainter.ShapeDot, PixelPainter.ShapeDot },
        };

        // 팔레트 (별 표식, 별자리 노바와 같은 계열)
        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorIce = new Color32(191, 230, 255, 255);
        private static readonly Color32 ColorIce2 = new Color32(140, 196, 255, 255);
        private static readonly Color32 ColorBlue = new Color32(91, 146, 230, 255);
        private static readonly Color32 ColorBlue2 = new Color32(52, 86, 158, 255);
        private static readonly Color32 ColorBlue3 = new Color32(31, 51, 102, 255);
        private static readonly Color32 ColorGold = new Color32(255, 212, 92, 255);
        private static readonly Color32 ColorGold2 = new Color32(224, 166, 52, 255);
        private static readonly Color32 ColorGold3 = new Color32(138, 90, 26, 255);

        // 시간 단계별 색(밝음 -> 어두움). 정점 알파 = 발광 세기.
        private static readonly Color32[] CoolSteps =
        {
            GlowColor(ColorWhite, 0.3f), GlowColor(ColorIce, 0.22f), GlowColor(ColorIce2, 0.16f), GlowColor(ColorBlue, 0.1f), GlowColor(ColorBlue2, 0.06f),
        };

        private static readonly Color32[] GoldSteps =
        {
            GlowColor(ColorWhite, 0.3f), GlowColor(ColorGold, 0.25f), GlowColor(ColorGold, 0.2f), GlowColor(ColorGold2, 0.12f), GlowColor(ColorGold3, 0.06f),
        };

        private struct StellaStar
        {
            public float angle;
            public bool bHero;
            public bool bGold;
        }

        private struct StellaDust
        {
            public float startTime;
            public float x;
            public float groundY;
            public float height;
            public float velocityX;
            public float velocityZ;
            public float gravity;
            public float fallTime;
            public int type;
            public int seed;
            public bool bGold;
        }

        private struct StellaSpark
        {
            public float cos;
            public float sin;
            public float speed;
            public float velocityZ;
            public float gravity;
            public float drag;
            public float fallTime;
            public int type;
            public bool bGold;
        }

        // 재생 중인 스텔라 폭발 수. 연쇄 폭발이 겹치면 뒤에 시작한 폭발의 입자 밀도를 낮춰 화면이 번잡해지지 않게 한다.
        private static int activeBurstCount;

        // 쿼드 버퍼는 인스턴스끼리 공유한다(메인 스레드 단일 실행, 매 프레임 Clear 후 채운다).
        private static readonly PixelQuadBuffer skyBuffer = new PixelQuadBuffer(QuadBudget);
        private static readonly PixelQuadBuffer groundBuffer = new PixelQuadBuffer(QuadBudget);

        // 내부 의존성
        private readonly PixelPainter skyPainter = new PixelPainter();
        private readonly PixelPainter groundPainter = new PixelPainter();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Mesh groundMesh;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly StellaStar[] stars = new StellaStar[StarCapacity];
        private readonly StellaDust[] dusts = new StellaDust[DustCapacity];
        private readonly StellaSpark[] sparks = new StellaSpark[SparkCapacity];
        private readonly int[] ringStarStart = new int[RingCount];
        private readonly int[] ringStarCount = new int[RingCount];
        private readonly float[] ringMaxRadius = new float[RingCount];

        // 상태 변수
        private float elapsed;
        private bool bFirstFrame;
        private bool bBeginPending;
        private bool bInitialized;
        private float overrideRadius;
        private float currentRadius;
        private float overlapScale = 1.0f;
        private bool bCounted;
        private float radiusPx;
        private int starTotal;
        private int dustTotal;
        private int sparkTotal;
        private int centerX;
        private int centerY;
        private uint randomState;
        private Vector3 cullingSize = new Vector3(20.0f, 20.0f, 10.0f);

        private int groundSortingLayerId;
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;
        private ulong skyHash;
        private int skyVertexCount = -1;
        private ulong groundHash;
        private int groundVertexCount = -1;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 이번 재생의 반지름(월드 유닛)을 정한다. 풀에서 꺼내져 활성화된 직후(첫 LateUpdate 전)에 호출해야 한다.
        /// 0 이하이면 인스펙터의 maxRadius를 쓴다.
        /// </summary>
        public void SetRadius(float _radiusUnits)
        {
            overrideRadius = _radiusUnits;
        }

        /// <summary>이번 재생에서 실제로 쓰는 반지름(월드 유닛).</summary>
        public float CurrentRadius => 0.0f < currentRadius ? currentRadius : maxRadius;

        /// <summary>전체 재생 시간(초). 루트 ParticleSystem 수명은 이 값 이상이어야 한다.</summary>
        public static float TotalDuration => TimelineLength;

        /// <summary>
        /// 충격 고리가 닿는 시각(초, 재생 시작 기준)을 반지름 비율(0~1)로 돌려준다. 피격 타이밍을 고리가 퍼지는 속도에 맞출 때 쓴다.
        /// </summary>
        public static float EvaluateShockArrivalTime(float _normalizedDistance)
        {
            float remaining = 1.0f - Mathf.Clamp01(_normalizedDistance);
            return ChargeTime + 0.5f * (1.0f - Mathf.Pow(remaining, 0.25f));
        }

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            mesh = new Mesh { name = "VFX_StellaBurst" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 1500);

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            if (null != groundMeshFilter && null != groundMeshRenderer)
            {
                groundMesh = new Mesh { name = "VFX_StellaBurst_Ground" };
                groundMesh.MarkDynamic();
                groundMeshFilter.sharedMesh = groundMesh;
                PixelQuadBuffer.ReserveMesh(groundMesh, 1500);

                groundMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                groundMeshRenderer.receiveShadows = false;
                groundMeshRenderer.enabled = false;

                groundSortingLayerId = SortingLayer.NameToID(groundSortingLayerName);
            }
        }

        // 풀에서 꺼내져 활성화되면 반지름이 정해질 시간을 주기 위해 시작을 첫 LateUpdate로 미룬다.
        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bFirstFrame = true;
            skyVertexCount = -1;
            groundVertexCount = -1;

            currentRadius = Mathf.Max(0.5f, 0.0f < overrideRadius ? overrideRadius : maxRadius);
            radiusPx = currentRadius * PixelsPerUnit;

            float reach = radiusPx * 1.25f + 48.0f;
            float halfY = (radiusPx * GroundSquashY + starHeightPx + 48.0f);
            cullingSize = new Vector3(reach * 2.0f / PixelsPerUnit, halfY * 2.0f / PixelsPerUnit, PixelVfxCulling.BoundsDepth);

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            SetupParticles();
        }

        // ---------- 설정 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        // 궤도 별, 별가루, 방사 별가루를 한 번에 만든다. 이후에는 난수를 새로 뽑지 않고 경과 시간으로만 위치를 계산한다.
        private void SetupParticles()
        {
            float scale = Mathf.Clamp(density * overlapScale, 0.25f, 2.0f);

            starTotal = 0;
            for (int r = 0; r < RingCount; r++)
            {
                ringMaxRadius[r] = radiusPx * RingFactor[r];
                int count = Mathf.Clamp(Mathf.RoundToInt((3.0f + ringMaxRadius[r] / 30.0f) * scale), 3, MaxStarsPerRing);
                ringStarStart[r] = starTotal;
                ringStarCount[r] = count;

                for (int i = 0; i < count; i++)
                {
                    StellaStar star = new StellaStar();
                    star.angle = TwoPi * i / count + Rand01() * 0.4f;
                    star.bHero = 0 == (i % HeroStarInterval);
                    star.bGold = 0 == (i % 3);
                    stars[starTotal++] = star;
                }
            }

            // 별마다 궤도 위에서 일정 간격으로 별가루를 흘린다. 별이 많아 용량이 모자라면 간격을 늘려 맞춘다.
            dustTotal = 0;
            for (int r = 0; r < RingCount; r++)
            {
                float lifeSpan = RingLife[r] * 0.8f;
                float step = Mathf.Max(DustSpawnStep / scale, lifeSpan * starTotal / DustCapacity);

                for (int i = 0; i < ringStarCount[r]; i++)
                {
                    int starIndex = ringStarStart[r] + i;
                    for (float ts = RingDelay[r] + 0.04f + Rand01() * 0.03f; ts < RingDelay[r] + lifeSpan && dustTotal < DustCapacity; ts += step)
                    {
                        float x;
                        float groundY;
                        float z;
                        float f;
                        float angle;
                        float depth;
                        if (false == EvaluateStar(r, starIndex, ts, 0.0f, out x, out groundY, out z, out f, out angle, out depth)) continue;

                        StellaDust dust = new StellaDust();
                        dust.startTime = ts;
                        dust.x = x;
                        dust.groundY = groundY;
                        dust.height = z;
                        dust.velocityX = Mathf.Cos(angle) * (14.0f + Rand01() * 34.0f) + (Rand01() - 0.5f) * 14.0f;
                        dust.velocityZ = (Rand01() - 0.4f) * 16.0f;
                        dust.gravity = 60.0f + Rand01() * 70.0f;
                        dust.fallTime = (dust.velocityZ + Mathf.Sqrt(dust.velocityZ * dust.velocityZ + 2.0f * dust.gravity * z)) / dust.gravity;

                        float roll = Rand01();
                        dust.type = roll < 0.15f ? 0 : (roll < 0.5f ? 1 : 2);
                        dust.bGold = Rand01() < 0.28f;
                        dust.seed = Mathf.FloorToInt(Rand01() * 6.0f);
                        dusts[dustTotal++] = dust;
                    }
                }
            }

            sparkTotal = Mathf.Min(SparkCapacity, Mathf.RoundToInt(SparkBaseCount * scale));
            for (int i = 0; i < sparkTotal; i++)
            {
                StellaSpark spark = new StellaSpark();
                float angle = Rand01() * TwoPi;
                spark.cos = Mathf.Cos(angle);
                spark.sin = -Mathf.Sin(angle);
                spark.velocityZ = 25.0f + Rand01() * 80.0f;
                spark.gravity = 100.0f + Rand01() * 60.0f;
                spark.drag = 3.0f + Rand01() * 1.5f;
                spark.fallTime = (spark.velocityZ + Mathf.Sqrt(spark.velocityZ * spark.velocityZ + 2.0f * spark.gravity * starHeightPx)) / spark.gravity;

                // 착지 거리가 범위 안에 고르게 흩어지도록 초기 속도를 정한다
                float landing = radiusPx * (0.2f + 0.85f * Rand01());
                spark.speed = landing * spark.drag / (1.0f - Mathf.Exp(-spark.drag * spark.fallTime));
                spark.type = i % 3;
                spark.bGold = Rand01() < 0.3f;
                sparks[i] = spark;
            }
        }

        // ---------- 계산 ----------

        private static float EaseOutCubic(float _t)
        {
            float inv = 1.0f - _t;
            return 1.0f - inv * inv * inv;
        }

        private static float EaseOutQuart(float _t)
        {
            float inv = 1.0f - _t;
            float inv2 = inv * inv;
            return 1.0f - inv2 * inv2;
        }

        private static Color32 GlowColor(Color32 _base, float _glow)
        {
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255.0f);
            return new Color32(_base.r, _base.g, _base.b, alpha);
        }

        // 궤도 별의 중심 기준 위치. 닫힌 식이라 꼬리가 과거 시각의 위치를 그대로 다시 계산한다.
        // 반환: 살아 있으면 true. _x/_groundY는 px 오프셋(바닥 기준), _z는 바닥에서 뜬 높이, _f는 수명 비율, _depth는 +일수록 화면 앞쪽.
        private bool EvaluateStar(int _ring, int _starIndex, float _tb, float _angleOffset, out float _x, out float _groundY, out float _z, out float _f, out float _angle, out float _depth)
        {
            _x = 0.0f;
            _groundY = 0.0f;
            _z = 0.0f;
            _f = 0.0f;
            _angle = 0.0f;
            _depth = 0.0f;

            float localTime = _tb - RingDelay[_ring];
            if (0.0f > localTime || RingLife[_ring] < localTime) return false;

            float expand = EaseOutCubic(Mathf.Min(1.0f, localTime / 0.55f));
            float radius = ringMaxRadius[_ring] * expand;
            float direction = Mathf.Sign(RingSpin[_ring]);
            float angle = stars[_starIndex].angle + RingSpin[_ring] * localTime + (1.0f - expand) * 2.2f * direction + _angleOffset;
            float sag = Mathf.Max(0.0f, localTime - 0.55f);

            _angle = angle;
            _x = radius * Mathf.Cos(angle);
            _groundY = -radius * GroundSquashY * Mathf.Sin(angle);
            _z = Mathf.Max(0.0f, starHeightPx * RingHeightScale[_ring] * (1.0f - 0.55f * expand) - sag * sag * 60.0f);
            _f = localTime / RingLife[_ring];
            _depth = Mathf.Sin(angle);
            return true;
        }

        // ---------- 그리기 ----------

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            bool bSplit = null != groundMesh;

            centerX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            centerY = Mathf.FloorToInt(pos.y * PixelsPerUnit);

            skyPainter.Begin(skyBuffer, transform.worldToLocalMatrix, pos.z);

            // 바닥 메쉬가 없으면(프리팹 참조 누락) 모두 공중 메쉬 하나로 그린다
            PixelPainter groundTarget = skyPainter;
            if (true == bSplit)
            {
                groundPainter.Begin(groundBuffer, groundMeshRenderer.transform.worldToLocalMatrix, pos.z);
                groundTarget = groundPainter;
            }

            DrawGround(groundTarget);
            DrawSky(skyPainter);

            skyPainter.Commit(mesh, meshRenderer, ref skyHash, ref skyVertexCount);
            if (true == bSplit) groundPainter.Commit(groundMesh, groundMeshRenderer, ref groundHash, ref groundVertexCount);
        }

        private void DrawGround(PixelPainter _p)
        {
            if (elapsed < ChargeTime) return;

            float tb = elapsed - ChargeTime;
            DrawShock(_p, tb);
            DrawLandedDust(_p, tb);
            DrawLandedSparks(_p, tb);
        }

        // 바닥 충격 - 점선 고리가 범위 끝까지 퍼지고, 그 뒤를 체커 띠가 따라간다. 두 번째 금색 고리는 3px 이상 떨어져 따라온다(HUGGING).
        private void DrawShock(PixelPainter _p, float _tb)
        {
            if (0.8f <= _tb) return;

            int radius = Mathf.RoundToInt(radiusPx * EaseOutQuart(Mathf.Clamp01(_tb / 0.5f)));
            if (radius < 2) return;

            Color32 ringColor = _tb < 0.12f ? GlowColor(ColorWhite, GroundGlow) : (_tb < 0.3f ? GlowColor(ColorIce, GroundGlow) : (_tb < 0.5f ? GlowColor(ColorIce2, GroundGlow) : (_tb < 0.65f ? GlowColor(ColorBlue, GroundGlow * 0.6f) : GlowColor(ColorBlue2, 0.0f))));
            _p.LatticeBand(centerX, centerY, radius, 6, GroundSquashY, 4, GlowColor(_tb < 0.35f ? ColorBlue2 : ColorBlue3, BandGlow));
            _p.Ring(centerX, centerY, radius, Mathf.Max(1, Mathf.RoundToInt(radius * GroundSquashY)), 2, 2, Mathf.FloorToInt(_tb * 60.0f), ringColor);

            float goldT = Mathf.Clamp01((_tb - 0.08f) / 0.55f);
            if (0.0f < goldT && goldT < 1.0f)
            {
                int goldRadius = Mathf.RoundToInt(radiusPx * 0.82f * EaseOutQuart(goldT));
                if (3 <= radius - goldRadius && 2 <= goldRadius)
                {
                    _p.Ring(centerX, centerY, goldRadius, Mathf.Max(1, Mathf.RoundToInt(goldRadius * GroundSquashY)), 1, 2, Mathf.FloorToInt(_tb * 50.0f), GlowColor(goldT < 0.6f ? ColorGold : ColorGold2, GroundGlow));
                }
            }
        }

        // 바닥에 내려앉은 별가루. 잠깐 `+`로 반짝인 뒤 점으로 깜빡이다 어두워지며 사라진다. 나무 뒤 레이어라 나무에 가려진다.
        private void DrawLandedDust(PixelPainter _p, float _tb)
        {
            for (int i = 0; i < dustTotal; i++)
            {
                float dt = _tb - dusts[i].startTime - dusts[i].fallTime;
                if (0.0f > dt || LandedDustLife < dt) continue;

                // 바닥 잔상은 별가루 일부만 남긴다(바닥이 지저분해지지 않게)
                if (0 != (dusts[i].seed % 6)) continue;

                float lf = dt / LandedDustLife;
                int lx = centerX + Mathf.FloorToInt(dusts[i].x + dusts[i].velocityX * dusts[i].fallTime);
                int ly = centerY + Mathf.FloorToInt(dusts[i].groundY);
                if (0.8f < lf && 0 != ((lx * 7 + ly * 3 + dusts[i].seed) & 1)) continue;

                bool bGold = dusts[i].bGold;
                if (lf < 0.12f)
                {
                    _p.Shape(PixelPainter.ShapePlus, lx, ly, bGold ? GoldSteps[1] : CoolSteps[1]);
                    continue;
                }

                int step = lf < 0.5f ? (0 != ((Mathf.FloorToInt(_tb * 14.0f) + dusts[i].seed) & 1) ? 1 : 2) : 4;
                _p.Dot(lx, ly, bGold ? GoldSteps[step] : CoolSteps[step]);
            }
        }

        private void DrawLandedSparks(PixelPainter _p, float _tb)
        {
            for (int i = 0; i < sparkTotal; i++)
            {
                float dt = _tb - sparks[i].fallTime;
                if (0.0f > dt || 0.45f < dt) continue;

                float lf = dt / 0.45f;
                float distance = sparks[i].speed * (1.0f - Mathf.Exp(-sparks[i].drag * sparks[i].fallTime)) / sparks[i].drag;
                int lx = centerX + Mathf.FloorToInt(sparks[i].cos * distance);
                int ly = centerY + Mathf.FloorToInt(sparks[i].sin * distance * GroundSquashY);
                if (0.75f < lf && 0 != ((lx + ly) & 1)) continue;

                bool bGold = sparks[i].bGold;
                if (lf < 0.15f) _p.Shape(PixelPainter.ShapeCross, lx, ly, bGold ? GoldSteps[1] : CoolSteps[0]);
                else _p.Dot(lx, ly, bGold ? GoldSteps[lf < 0.55f ? 3 : 4] : CoolSteps[lf < 0.55f ? 2 : 4]);
            }
        }

        private void DrawSky(PixelPainter _p)
        {
            if (elapsed < ChargeTime)
            {
                DrawCharge(_p);
                return;
            }

            float tb = elapsed - ChargeTime;
            DrawBurstStar(_p, tb);
            DrawRingStars(_p, tb);
            DrawFallingDust(_p, tb);
            DrawFlyingSparks(_p, tb);
        }

        // 차징 - 별가루가 나선으로 중심에 모이고 중심 별이 자란다
        private void DrawCharge(PixelPainter _p)
        {
            float u = elapsed / ChargeTime;
            int starY = centerY + Mathf.RoundToInt(starHeightPx);
            float pull = (1.0f - u) * (1.0f - u);

            for (int i = 0; i < 12; i++)
            {
                float radius = pull * (30.0f + (i % 3) * 8.0f);
                float angle = TwoPi * i / 12.0f + u * 9.0f;
                int x = centerX + Mathf.FloorToInt(Mathf.Cos(angle) * radius);
                int y = starY + Mathf.FloorToInt(Mathf.Sin(angle) * radius * GroundSquashY);
                _p.Shape(0 == (i & 1) ? PixelPainter.ShapeDot : PixelPainter.ShapePlus, x, y, 0 == (i % 3) ? GlowColor(ColorGold, 0.25f) : GlowColor(ColorIce, 0.2f));
            }

            DrawDotStar(_p, centerX, starY, 2 + Mathf.FloorToInt(u * 4.0f), GlowColor(ColorWhite, 0.2f), GlowColor(ColorIce, 0.1f));
        }

        // 굵기가 계단식으로 변하는 도트 별. 중심 3x3, 팔 끝으로 갈수록 가늘어지고 짧은 대각선 팔이 붙는다.
        private static void DrawDotStar(PixelPainter _p, int _x, int _y, int _arm, Color32 _core, Color32 _tip)
        {
            _p.Rect(_x - 1, _y - 1, _x + 2, _y + 2, _core);

            for (int i = 2; i <= _arm; i++)
            {
                int half = i < _arm * 0.45f ? 1 : 0;
                Color32 color = i < _arm * 0.5f ? _core : _tip;
                _p.Rect(_x - half, _y + i, _x + half + 1, _y + i + 1, color);
                _p.Rect(_x - half, _y - i, _x + half + 1, _y - i + 1, color);
                _p.Rect(_x - i, _y - half, _x - i + 1, _y + half + 1, color);
                _p.Rect(_x + i, _y - half, _x + i + 1, _y + half + 1, color);
            }

            int diagonal = Mathf.FloorToInt(_arm * 0.45f);
            for (int i = 2; i <= diagonal; i++)
            {
                _p.Dot(_x - i, _y - i, _tip);
                _p.Dot(_x + i, _y - i, _tip);
                _p.Dot(_x - i, _y + i, _tip);
                _p.Dot(_x + i, _y + i, _tip);
            }
        }

        // 폭발 순간의 중심 별 섬광 + 공중 충격 고리, 이후 작아지는 잔광
        private void DrawBurstStar(PixelPainter _p, float _tb)
        {
            int starY = centerY + Mathf.RoundToInt(starHeightPx);

            if (_tb < 0.32f)
            {
                int stage = Mathf.Min(StarFlashArm.Length - 1, Mathf.FloorToInt(_tb / StarFlashStage));
                DrawDotStar(_p, centerX, starY, StarFlashArm[stage], GlowColor(ColorWhite, 0.2f), GlowColor(_tb < 0.14f ? ColorIce : ColorGold, 0.12f));

                float ringT = Mathf.Clamp01(_tb / 0.26f);
                if (ringT < 1.0f)
                {
                    int radius = Mathf.RoundToInt(44.0f * EaseOutCubic(ringT));
                    if (2 <= radius)
                    {
                        _p.Ring(centerX, centerY + Mathf.RoundToInt(starHeightPx * 0.6f), radius, Mathf.Max(1, Mathf.RoundToInt(radius * GroundSquashY)), 3, 1, Mathf.FloorToInt(_tb * 80.0f), GlowColor(ringT < 0.4f ? ColorWhite : ColorIce, 0.25f));
                    }
                }
            }
            else if (_tb < 0.95f)
            {
                float f = (_tb - 0.32f) / 0.63f;
                _p.Shape(f < 0.3f ? PixelPainter.ShapeDiamond : (f < 0.6f ? PixelPainter.ShapePlus : PixelPainter.ShapeDot), centerX, starY, GlowColor(f < 0.4f ? ColorWhite : (f < 0.7f ? ColorGold : ColorGold2), 0.25f));
            }
        }

        // 별 표식처럼 타원 궤도를 도는 별 3겹. 큰 별(◆/●)만 점 꼬리를 끌고, 나머지는 `+`/`x`로 번갈아 반짝인다.
        private void DrawRingStars(PixelPainter _p, float _tb)
        {
            int twinkleTick = Mathf.FloorToInt(_tb * 12.0f);

            for (int r = 0; r < RingCount; r++)
            {
                for (int i = 0; i < ringStarCount[r]; i++)
                {
                    int index = ringStarStart[r] + i;
                    float x;
                    float groundY;
                    float z;
                    float f;
                    float angle;
                    float depth;
                    if (false == EvaluateStar(r, index, _tb, 0.0f, out x, out groundY, out z, out f, out angle, out depth) || 0.93f < f) continue;

                    int px = centerX + Mathf.FloorToInt(x);
                    int py = centerY + Mathf.FloorToInt(groundY + z);
                    bool bTwinkle = 0 != ((twinkleTick + i) & 1);
                    bool bFront = 0.0f < depth;
                    Color32 color = stars[index].bGold ? GlowColor(bFront ? ColorGold : ColorGold2, 0.28f) : GlowColor(bFront ? ColorWhite : ColorIce2, bFront ? 0.3f : 0.18f);

                    if (true == stars[index].bHero)
                    {
                        float direction = Mathf.Sign(RingSpin[r]);
                        for (int j = 2; j >= 1; j--)
                        {
                            float tx;
                            float tGroundY;
                            float tz;
                            float tf;
                            float tAngle;
                            float tDepth;
                            if (false == EvaluateStar(r, index, _tb, -direction * 0.16f * j, out tx, out tGroundY, out tz, out tf, out tAngle, out tDepth)) continue;

                            _p.Dot(centerX + Mathf.FloorToInt(tx), centerY + Mathf.FloorToInt(tGroundY + tz), GlowColor(1 == j ? ColorIce : ColorBlue, 0.16f));
                        }

                        int heroShape = f < 0.6f ? (bTwinkle ? PixelPainter.ShapeDiamond : PixelPainter.ShapeCircle) : (f < 0.85f ? PixelPainter.ShapePlus : PixelPainter.ShapeDot);
                        _p.Shape(heroShape, px, py, color);
                    }
                    else
                    {
                        _p.Shape(f < 0.7f ? (bTwinkle ? PixelPainter.ShapePlus : PixelPainter.ShapeCross) : PixelPainter.ShapeDot, px, py, color);
                    }
                }
            }
        }

        // 궤도를 도는 별에서 흘러나와 바닥으로 싸악 떨어지며 바깥으로 퍼지는 별가루. 떨어지는 동안 모양이 ◆ ● + x . 순으로 줄고 색이 식는다.
        private void DrawFallingDust(PixelPainter _p, float _tb)
        {
            for (int i = 0; i < dustTotal; i++)
            {
                float dt = _tb - dusts[i].startTime;
                if (0.0f > dt || dusts[i].fallTime <= dt) continue;

                float z = dusts[i].height + dusts[i].velocityZ * dt - 0.5f * dusts[i].gravity * dt * dt;
                if (0.0f >= z) continue;

                float f = dt / dusts[i].fallTime;
                int[] sequence = DustShapes[dusts[i].type];
                int shape = sequence[Mathf.Min(sequence.Length - 1, Mathf.FloorToInt(f * sequence.Length))];
                Color32[] steps = dusts[i].bGold ? GoldSteps : CoolSteps;
                Color32 color = steps[Mathf.Min(steps.Length - 1, Mathf.FloorToInt(f * steps.Length))];

                int x = centerX + Mathf.FloorToInt(dusts[i].x + dusts[i].velocityX * dt);
                int y = centerY + Mathf.FloorToInt(dusts[i].groundY + z);
                _p.Shape(shape, x, y, color);
            }
        }

        // 중심에서 퍼져 포물선으로 떨어지는 방사 별가루. 점 3개의 짧은 꼬리를 끈다.
        private void DrawFlyingSparks(PixelPainter _p, float _tb)
        {
            for (int i = 0; i < sparkTotal; i++)
            {
                if (_tb >= sparks[i].fallTime) continue;

                float f = _tb / sparks[i].fallTime;
                bool bGold = sparks[i].bGold;

                for (int j = SparkTrailDots; j >= 0; j--)
                {
                    float d = _tb - j * 0.028f;
                    if (0.0f > d) continue;

                    float distance = sparks[i].speed * (1.0f - Mathf.Exp(-sparks[i].drag * d)) / sparks[i].drag;
                    float z = Mathf.Max(0.0f, starHeightPx + sparks[i].velocityZ * d - 0.5f * sparks[i].gravity * d * d);
                    int x = centerX + Mathf.FloorToInt(sparks[i].cos * distance);
                    int y = centerY + Mathf.FloorToInt(sparks[i].sin * distance * GroundSquashY + z);

                    if (0 == j)
                    {
                        int shape = f < 0.45f ? (0 == sparks[i].type ? PixelPainter.ShapeCircle : (1 == sparks[i].type ? PixelPainter.ShapePlus : PixelPainter.ShapeCross)) : (f < 0.8f ? PixelPainter.ShapePlus : PixelPainter.ShapeDot);
                        Color32[] steps = bGold ? GoldSteps : CoolSteps;
                        _p.Shape(shape, x, y, steps[Mathf.Min(4, Mathf.FloorToInt(f * 5.0f))]);
                    }
                    else
                    {
                        _p.Dot(x, y, bGold ? GoldSteps[1 == j ? 1 : (2 == j ? 3 : 4)] : CoolSteps[1 == j ? 1 : (2 == j ? 3 : 4)]);
                    }
                }
            }
        }

        // 유니티 이벤트 함수

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

            // 먼저 시작한 폭발이 있으면 이 폭발은 입자를 줄인다(2번째 50%, 3번째부터 30%)
            activeBurstCount++;
            bCounted = true;
            overlapScale = 1 == activeBurstCount ? 1.0f : (2 == activeBurstCount ? 0.5f : 0.3f);

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
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
                if (null != groundMeshRenderer && true == groundMeshRenderer.enabled) groundMeshRenderer.enabled = false;
                return;
            }

            // 첫 프레임은 시간을 더하지 않아서 t=0 장면이 반드시 한 번 그려진다
            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;

            // 소팅은 VFXComponent가 자식 렌더러 전체에 같은 값으로 덮어쓰므로, 루트 렌더러 기준으로 다시 맞춘다
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

            if (null != groundMesh)
            {
                if (groundMeshRenderer.sortingLayerID != groundSortingLayerId) groundMeshRenderer.sortingLayerID = groundSortingLayerId;
                if (groundMeshRenderer.sortingOrder != groundSortingOrder) groundMeshRenderer.sortingOrder = groundSortingOrder;
            }

            // 화면 밖이면 메쉬 재구성을 건너뛰고 시간만 흘린다(그림이 경과 시간만으로 정해져 다시 들어와도 같은 그림이다)
            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            Vector3 size = new Vector3(cullingSize.x + margin, cullingSize.y + margin, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(transform.position + new Vector3(0.0f, starHeightPx * 0.5f / PixelsPerUnit, 0.0f), size))
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                if (null != groundMeshRenderer && true == groundMeshRenderer.enabled) groundMeshRenderer.enabled = false;
                skyVertexCount = -1;
                groundVertexCount = -1;
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            overrideRadius = 0.0f;

            if (true == bCounted)
            {
                bCounted = false;
                activeBurstCount = Mathf.Max(0, activeBurstCount - 1);
            }

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            activeBurstCount = 0;
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }

            if (null != groundMesh)
            {
                Destroy(groundMesh);
                groundMesh = null;
            }
        }

        private void OnValidate()
        {
            maxRadius = Mathf.Max(0.5f, maxRadius);
            density = Mathf.Clamp(density, 0.25f, 2.0f);
            starHeightPx = Mathf.Max(0.0f, starHeightPx);
        }

        // 씬 뷰에서 최대 반지름(바닥 타원)을 보여 줘서 피격 범위와 눈으로 맞출 수 있게 한다.
        private void OnDrawGizmosSelected()
        {
            float radius = true == Application.isPlaying && 0.0f < currentRadius ? currentRadius : maxRadius;
            Vector3 center = transform.position;

            Gizmos.color = new Color(0.4f, 0.9f, 1.0f, 0.9f);

            const int Segments = 48;
            Vector3 previous = center + new Vector3(radius, 0.0f, 0.0f);
            for (int i = 1; i <= Segments; i++)
            {
                float angle = TwoPi * i / Segments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * GroundSquashY, 0.0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
