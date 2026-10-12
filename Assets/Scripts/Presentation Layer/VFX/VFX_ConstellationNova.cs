using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 별자리 발현의 "별자리 마법진 + 별 폭발" 연출. 별자리 중심의 바닥에 마법진이 그려지며 펼쳐지고, 별이 은하 나선팔로 휘감겨
    /// 마법진 밖까지 터져 나간다. 기존 별자리 빔(ConstellationPixelLaser)을 대체하지 않고 같은 발현 시점에 추가로 재생된다.
    /// 스프라이트 시트 대신 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다(VFX_BrandStampBurst, 별자리 레이저와 같은 방식).
    ///
    /// 절제와 위계 - 요소를 줄이고 크기/밝기 위계를 둔다.
    /// - 박자: 임팩트 섬광 -> 짧은 정지감 -> 마법진이 그려짐 -> 유지 -> 해체.
    /// - 섬광과 코어는 1px 선이 아니라 굵기가 계단식으로 변하는 도트 별(✦)이다. 크기는 연속으로 줄지 않고 4단계로 끊어서 줄어든다.
    /// - 마법진(바닥 타원) 4겹: 바깥 2px 링(호 단위 틈) + 점선 링 + 12개 눈금 / 9각 별 / 룬 링(금색 룬 6개) / 코어.
    ///   회전은 12fps 스텝으로 끊어 돌고, 바깥 링은 고정한다.
    /// - 별: 나선팔 3개로 휘감기며 particleSpread만큼 마법진 밖까지 터진다. 큰 "주인공 별"만 꼬리를 끌고 밝으며 나머지는 어두운 1px 점이다.
    /// - 방사 스파크: 충격파와 함께 달려나가는 큰 `x`/`+` 별. 불씨는 소수만 바깥으로 퍼지며 오른다.
    /// - 빛기둥: 9각 별 꼭짓점 9곳에서 솟고(3곳 걸러 하나만 높다) 끝에 금색 `+` 별이 맺힌다.
    /// - 소멸: 마법진이 바깥 링부터 안쪽으로 파도처럼 침식되며, 일부 픽셀이 `.`, `+`, `x` 독립 입자로 풀려 소용돌이치며 떠올라 사라진다.
    ///
    /// 렌더링은 바닥 메쉬(마법진, 충격파)와 공중 메쉬(별, 스파크, 빛기둥, 불씨, 풀려난 분해 입자)로 나뉜다.
    /// 바닥 메쉬는 나무/캐릭터(Objects 레이어)보다 아래 레이어(ShockWave)에, 공중 메쉬는 루트 렌더러(FlyingItem)에 그려져
    /// 마법진이 나무와 캐릭터를 가리지 않는다.
    ///
    /// 모든 요소의 위치는 경과 시간만으로 닫힌 식으로 계산하므로(적분 없음) 꼬리도 과거 시각의 위치를 다시 계산해 그린다.
    /// 색은 흰색/하늘색/파랑/금색 4색만 쓰고 밝기는 정점 알파에 실린 HDR 발광 세기(BrandWrapGlow 셰이더)로만 낸다(반투명 페이드 없음).
    /// 정적 버퍼와 고정 크기 배열만 써서 재생 중 힙 할당이 없고, 가로로 이어진 픽셀은 하나의 쿼드로 합쳐 정점 수를 줄인다.
    ///
    /// 범위 조절: maxRadius(월드 유닛) 또는 재생 직전의 SetRadius로 정한다. 마법진, 별, 기둥, 불씨 전부가 이 값에 비례한다.
    /// 과한 정도는 density(0.25~2)로 조절한다 - 별, 주인공 별, 불씨, 스파크 개수가 함께 늘고 준다.
    /// 풀 인스턴스로 쓰이며, 루트 ParticleSystem의 수명(TotalDuration 이상)이 끝나면 VFXComponent가 풀로 돌려보낸다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_ConstellationNova : MonoBehaviour
    {
        [Header("범위 (월드 유닛, 32 PPU 기준 1유닛 = 32px)")]
        [SerializeField, Tooltip("바깥 마법진 링이 닿는 반지름. 눈에 보이는 폭발 범위이자 피격 범위와 맞추는 값이다. 재생 직전에 SetRadius로 덮어쓸 수 있다.")]
        private float maxRadius = 4.0f;
        [SerializeField, Tooltip("바닥 타원의 세로 비율(쿼터뷰). 히트 판정의 Y축 2배 보정과 같은 0.5가 기본이다.")]
        private float groundSquashY = 0.5f;
        [SerializeField, Tooltip("별과 방사 스파크가 퍼지는 최대 거리 배율(마법진 링 반지름 대비). 1이면 링 안쪽까지만, 클수록 링 밖으로 팡 터져 나간다. 피격 범위에는 영향이 없다.")]
        private float particleSpread = 1.3f;

        [Header("밀도 (과한 느낌 조절)")]
        [SerializeField, Tooltip("별, 주인공 별, 불씨, 스파크 개수에 곱하는 전체 배율. 1이 기본이고 낮출수록 절제된다.")]
        private float density = 1.0f;
        [SerializeField, Tooltip("density 1 기준 반지름 1유닛당 별 개수(최대 StarCapacity)")]
        private float starsPerUnit = 17.5f;
        [SerializeField, Tooltip("density 1 기준 크고 밝은 주인공 별 개수. 꼬리를 끌고 반짝인다.")]
        private int heroStarCount = 8;
        [SerializeField, Tooltip("density 1 기준 반지름 1유닛당 불씨 개수(최대 EmberCapacity)")]
        private float embersPerUnit = 5.5f;
        [SerializeField, Tooltip("density 1 기준 반지름 1유닛당 방사 스파크 개수(최대 SparkCapacity)")]
        private float sparksPerUnit = 2.0f;

        [Header("정렬")]
        [SerializeField, Tooltip("공중 메쉬를 루트 렌더러(VFXComponent가 정한 소팅)보다 이 값만큼 위에 그린다")]
        private int sortingOrderOffset = 0;

        [Header("바닥 메쉬 (마법진이 나무/캐릭터 뒤에 그려지도록 분리)")]
        [SerializeField, Tooltip("마법진과 충격파를 그리는 바닥 메쉬. 프리팹의 GroundMesh 자식에 연결한다. 비어 있으면 모두 공중 메쉬 하나로 그린다.")]
        private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField, Tooltip("바닥 메쉬 소팅 레이어. 나무/캐릭터가 있는 Objects보다 아래여야 한다. 도끼 충격파와 같은 ShockWave가 기본이다.")]
        private string groundSortingLayerName = "ShockWave";
        [SerializeField, Tooltip("바닥 메쉬 소팅 오더")]
        private int groundSortingOrder = 0;

        private const float PixelsPerUnit = 32.0f;
        private const float TwoPi = Mathf.PI * 2.0f;

        // 전체 타임라인(초). 루트 ParticleSystem 수명은 이 값 이상이어야 한다.
        private const float TimelineLength = 2.8f;
        private const float DissolveStart = 1.6f;
        private const float DissolveTime = 0.9f;

        // 마법진 분해: 샘플마다 시작 시각 = (1 - 반지름 비율) * Sweep + 지터. 시작 시각이 되면 대부분의 픽셀은 그 자리에서 사라지고
        // DissolveSpawnRate만큼만 `.`, `+`, `x` 독립 입자가 되어 떠오른다. 입자 수명은 소멸 진행도(0~1) 단위다.
        private const float DissolveSweep = 0.5f;
        private const float DissolveJitter = 0.2f;
        private const float DissolveParticleLife = 0.35f;
        private const float DissolveSpawnRate = 0.22f;
        // 입자가 `+`/`x` 모양을 유지하는 수명 비율(이후에는 `.` 점으로 줄어든다)
        private const float DissolveShapeEnd = 0.55f;
        private const float DissolveLiftMinPx = 10.0f;
        private const float DissolveLiftRangePx = 26.0f;
        private const float DissolveSwirlPx = 10.0f;
        private const float DissolveOutPx = 10.0f;
        private const float DissolveRingTime = 0.4f;
        private const int DissolveKeep = 0;
        private const int DissolveGone = 1;
        private const int DissolveParticle = 2;

        private const int StarCapacity = 300;
        private const int EmberCapacity = 150;
        private const int SparkCapacity = 16;
        private const int MinStars = 24;
        private const int MinEmbers = 8;
        private const int MinSparks = 4;
        private const int LayerCount = 4;
        // 마법진 별 다각형의 꼭짓점 수. 빛기둥이 같은 값을 써서 꼭짓점에 정확히 붙는다.
        private const int StarPolygonPoints = 9;
        private const int QuadBudget = 12000;
        private const int MaxLinePixels = 2048;

        // 기준 반지름(px). 높이/드리프트 같은 크기 값은 실제 반지름과 이 값의 비율로 맞춘다.
        private const float ReferenceRadiusPx = 150.0f;
        private const float GroundGlow = 0.28f;
        // 마법진 구조선(링, 별 다각형)의 발광 세기. 블룸 임계값 근처로 낮춰야 색이 흰색으로 날아가지 않고 선이 또렷하다.
        private const float LineGlow = 0.07f;
        // 마법진 회전을 끊어 돌리는 스텝 속도(초당 칸 수)
        private const float StepRate = 12.0f;

        // 충격파
        private const float ShockDuration = 0.4f;
        private const float ShockFadeStart = 0.2f;
        private const float ShockFadeTime = 0.25f;
        private const float ShockEnd = 0.45f;

        // 빛기둥
        private const float PillarStart = 0.7f;
        private const float PillarRiseTime = 0.3f;
        private const float PillarShrinkStart = 1.4f;
        private const float PillarShrinkTime = 0.3f;
        private const float PillarEnd = 1.7f;
        private const float PillarHeightPx = 70.0f;

        // 별 / 불씨
        private const float StarExpandTime = 0.9f;
        private const float StarRiseTime = 1.6f;
        private const float StarRisePx = 80.0f;
        private const float StarSwirl = 3.4f;
        private const float StarSpin = 0.55f;
        private const float TrailSpacing = 0.03f;
        private const float TrailActiveTime = 0.45f;
        private const int TrailDots = 4;
        private const float EmberStart = 0.35f;
        private const float EmberEnd = 2.3f;
        private const float EmberRisePx = 95.0f;
        private const float EmberOutward = 0.45f;

        // 방사 스파크
        private const float SparkTravelTime = 0.75f;
        private const float SparkLife = 1.3f;
        private const int SparkTrailDots = 5;
        private const float SparkTrailSpacing = 0.03f;

        // 시작 섬광 - 4단계로 끊어서 줄어드는 도트 별
        private const float FlashStageTime = 0.06f;
        private const float FlashEnd = 0.24f;
        private const int FlashStageCount = 4;

        // 마법진 중앙 코어의 도트 별(7x7). W 흰색, I 하늘색, B 파랑. 큰 것과 작은 것을 번갈아 맥동한다.
        private static readonly string[] CoreStarLarge = { "...B...", "...I...", "..IWI..", "BIWWWIB", "..IWI..", "...I...", "...B..." };
        private static readonly string[] CoreStarSmall = { ".......", "...I...", "..IWI..", ".IWWWI.", "..IWI..", "...I...", "......." };
        private const int CoreStarSize = 7;

        // 레이어별 시작 시각 / 펼쳐지는 데 걸리는 시간(초). 임팩트 뒤 짧은 정지감을 두고 바깥에서 안쪽 순서로 그려진다.
        private static readonly float[] LayerDelay = { 0.12f, 0.3f, 0.45f, 0.55f };
        private static readonly float[] LayerDuration = { 0.5f, 0.5f, 0.4f, 0.35f };

        // 팔레트 (별 감쌈, 각인 폭발과 같은 계열)
        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorIce = new Color32(170, 220, 255, 255);
        private static readonly Color32 ColorBlue = new Color32(91, 146, 230, 255);
        private static readonly Color32 ColorGold = new Color32(255, 212, 92, 255);

        private struct NovaStar
        {
            public float u;
            public float armAngle;
            public float noise;
            public float delay;
            public float rise;
            public int kind;
            public int colorIndex;
            public int seed;
            public bool bHero;
        }

        private struct NovaSpark
        {
            public float angle;
            public float distance;
            public float lift;
            public float delay;
            public int colorIndex;
        }

        private struct NovaEmber
        {
            public float offset;
            public float angle;
            public float radiusScale;
            public float speed;
        }

        // 쿼드는 바닥 + 공중 합쳐 최대 12000개(메쉬별 정점은 UInt16 인덱스 한도 안). 공용 버퍼라 인스턴스끼리 공유한다
        // (메인 스레드 단일 실행, 매 프레임 Clear 후 채운다).
        private static readonly PixelQuadBuffer skyBuffer = new PixelQuadBuffer(QuadBudget / 2);
        private static readonly PixelQuadBuffer groundBuffer = new PixelQuadBuffer(QuadBudget / 2);

        // 내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Mesh groundMesh;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly NovaStar[] stars = new NovaStar[StarCapacity];
        private readonly NovaEmber[] embers = new NovaEmber[EmberCapacity];
        private readonly NovaSpark[] sparks = new NovaSpark[SparkCapacity];
        private readonly float[] layerRadius = new float[LayerCount];
        private readonly float[] layerFrac = new float[LayerCount];

        // 상태 변수
        private float elapsed;
        private bool bFirstFrame;
        private bool bBeginPending;
        private bool bInitialized;
        private float overrideRadius;
        private float currentRadius;
        private float radiusPx;
        private float sizeScale;
        private int starCount;
        private int heroCount;
        private int emberCount;
        private int sparkCount;
        private int centerX;
        private int centerY;
        private float vanish;
        private float stepTime;
        private uint randomState;
        private Vector3 meshBoundsSize = new Vector3(20.0f, 20.0f, 10.0f);

        // 지금 그리는 대상 버퍼(바닥 또는 공중)와 바닥 메쉬 소팅 레이어 ID
        private PixelQuadBuffer activeBuffer;
        private int groundSortingLayerId;

        // 같은 줄에서 이어지는 픽셀을 하나의 쿼드로 합치기 위한 진행 중인 줄
        private bool bRunActive;
        private int runX0;
        private int runX1;
        private int runY;
        private Color32 runColor;

        // 분해 연출에서 픽셀의 고정 식별자. 마법진은 스텝으로 도는 중이라 좌표 해시를 쓰면 입자가 깜빡이므로, 그리는 도형과 샘플 번호로 해시한다.
        private int primitiveCounter;
        private int plotSalt;
        private int plotIndex;

        // 마지막으로 메쉬 렌더러에 적용한 루트 소팅 값. 같으면 매 프레임 세터 호출을 건너뛴다(OnEnable에서 무효화).
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 이번 재생의 반지름(월드 유닛)을 정한다. 풀에서 꺼내져 활성화된 직후(첫 LateUpdate 전)에 호출해야 한다.
        /// 0 이하이면 인스펙터의 maxRadius를 쓴다.
        /// </summary>
        public void SetRadius(float _radiusUnits)
        {
            overrideRadius = _radiusUnits;
        }

        /// <summary>이번 재생에서 실제로 쓰는 최대 반지름(월드 유닛). 재생 시작 전에는 인스펙터 값이다.</summary>
        public float CurrentRadius => 0.0f < currentRadius ? currentRadius : maxRadius;

        /// <summary>전체 재생 시간(초). 루트 ParticleSystem 수명은 이 값 이상이어야 한다.</summary>
        public static float TotalDuration => TimelineLength;

        /// <summary>
        /// 경과 시간(초)에서의 충격파 선두 반지름(월드 유닛, 가로 기준)을 돌려준다. ShockDuration(0.4초)에 maxRadius에 닿는다.
        /// 피격 타이밍을 퍼지는 파동에 맞출 때 쓸 수 있다.
        /// </summary>
        public float EvaluateRadius(float _elapsed)
        {
            return CurrentRadius * EaseOutCubic(Mathf.Clamp01(_elapsed / ShockDuration));
        }

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            mesh = new Mesh { name = "VFX_ConstellationNova" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            if (null != groundMeshFilter && null != groundMeshRenderer)
            {
                groundMesh = new Mesh { name = "VFX_ConstellationNova_Ground" };
                groundMesh.MarkDynamic();
                groundMeshFilter.sharedMesh = groundMesh;

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

            currentRadius = 0.0f < overrideRadius ? overrideRadius : maxRadius;
            currentRadius = Mathf.Max(0.5f, currentRadius);
            radiusPx = currentRadius * PixelsPerUnit;
            sizeScale = radiusPx / ReferenceRadiusPx;

            float scale = Mathf.Max(0.0f, density);
            starCount = Mathf.Clamp(Mathf.RoundToInt(currentRadius * starsPerUnit * scale), MinStars, StarCapacity);
            heroCount = Mathf.Clamp(Mathf.RoundToInt(heroStarCount * scale), 0, starCount);
            emberCount = Mathf.Clamp(Mathf.RoundToInt(currentRadius * embersPerUnit * scale), MinEmbers, EmberCapacity);
            sparkCount = Mathf.Clamp(Mathf.RoundToInt(currentRadius * sparksPerUnit * scale), MinSparks, SparkCapacity);

            // 메쉬 경계: 가장 먼 가로 거리와 솟구침 높이 + 여유
            float reach = Mathf.Max(1.0f, particleSpread) * 1.2f + EmberOutward;
            float halfX = (radiusPx * reach + 64.0f) / PixelsPerUnit;
            float riseMax = Mathf.Max(PillarHeightPx, StarRisePx + EmberRisePx) * sizeScale;
            float halfY = (radiusPx * reach * groundSquashY + riseMax + 64.0f) / PixelsPerUnit;
            meshBoundsSize = new Vector3(halfX * 2.0f, halfY * 2.0f, 10.0f);

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            SetupStars();
            SetupEmbers();
            SetupSparks();
        }

        // ---------- 난수 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        // 격자 좌표 해시(0~1). 같은 값이면 같은 결과라 소멸 연출에서 점이 깜빡이지 않고 한 번에 빠진다.
        private static float Hash01(int _x, int _y)
        {
            uint h = unchecked((uint)(_x * 374761393) + (uint)(_y * 668265263));
            h = unchecked((h ^ (h >> 13)) * 1274126177u);
            h ^= h >> 16;
            return (h & 0x3FF) / 1024.0f;
        }

        // ---------- 설정 ----------

        // 별마다 나선팔(3개), 반지름 위치, 모양, 색을 정한다. 앞쪽 heroCount개는 크고 밝은 주인공 별이다.
        // 이후 위치는 시간으로만 계산한다.
        private void SetupStars()
        {
            for (int i = 0; i < starCount; i++)
            {
                NovaStar star = new NovaStar();
                star.bHero = i < heroCount;

                // 주인공 별은 바깥 쪽(0.55~1.0)에 둬서 퍼지는 궤적이 잘 보이게 한다.
                // 나머지 중 30%는 바깥 쉘(0.78~1.0)에 둬서 도넛 모양으로 퍼지게 하고, 그 밖은 중심 쪽이 약간 더 촘촘하다.
                if (true == star.bHero) star.u = Mathf.Lerp(0.55f, 1.0f, Rand01());
                else star.u = Rand01() < 0.3f ? Mathf.Lerp(0.78f, 1.0f, Rand01()) : Mathf.Pow(Rand01(), 0.7f);

                star.armAngle = (i % 3) * TwoPi / 3.0f;
                star.noise = (Rand01() - 0.5f) * 0.7f;
                star.delay = 0.12f + Rand01() * 0.22f;
                star.rise = Mathf.Lerp(0.3f, 1.0f, Rand01());

                // 0 점, 1 반짝이는 `+`/`x`. 주인공 별은 항상 반짝이고 나머지는 대부분 점이다.
                star.kind = true == star.bHero || Rand01() >= 0.85f ? 1 : 0;

                // 0 흰색, 1 하늘색, 2 파랑, 3 금색 - 중심은 밝고 바깥은 푸르다
                float colorRoll = Rand01();
                if (true == star.bHero) star.colorIndex = 0 == (i % 3) ? 3 : 0;
                else if (star.u < 0.22f) star.colorIndex = colorRoll < 0.5f ? 0 : 3;
                else if (star.u < 0.62f) star.colorIndex = 1;
                else star.colorIndex = 2;

                star.seed = (int)(Rand01() * 100.0f);
                stars[i] = star;
            }
        }

        // 방사 스파크 - 균등 각도로 충격파와 함께 바깥으로 달려나간다
        private void SetupSparks()
        {
            for (int i = 0; i < sparkCount; i++)
            {
                NovaSpark spark = new NovaSpark();
                spark.angle = TwoPi * (i + (Rand01() - 0.5f) * 0.6f) / sparkCount;
                spark.distance = Mathf.Lerp(0.85f, 1.15f, Rand01()) * Mathf.Max(1.0f, particleSpread);
                spark.lift = Mathf.Lerp(14.0f, 46.0f, Rand01());
                spark.delay = 0.12f + Rand01() * 0.06f;
                spark.colorIndex = 0 == (i % 4) ? 3 : (0 == (i % 2) ? 0 : 1);
                sparks[i] = spark;
            }
        }

        private void SetupEmbers()
        {
            for (int i = 0; i < emberCount; i++)
            {
                NovaEmber ember = new NovaEmber();
                ember.offset = Rand01();
                ember.angle = Rand01() * TwoPi;
                ember.radiusScale = 0.25f + 0.75f * Rand01();
                ember.speed = 0.6f + 0.5f * Rand01();
                embers[i] = ember;
            }
        }

        // ---------- 계산 ----------

        private static float EaseOutCubic(float _t)
        {
            float inv = 1.0f - _t;
            return 1.0f - inv * inv * inv;
        }

        private static float EaseOutQuint(float _t)
        {
            float inv = 1.0f - _t;
            float inv2 = inv * inv;
            return 1.0f - inv2 * inv2 * inv;
        }

        private static Color32 GlowColor(Color32 _base, float _glow)
        {
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255.0f);
            return new Color32(_base.r, _base.g, _base.b, alpha);
        }

        private static Color32 PaletteColor(int _index)
        {
            if (0 == _index) return ColorWhite;
            if (1 == _index) return ColorIce;
            if (2 == _index) return ColorBlue;
            return ColorGold;
        }

        // 시각 _time에서의 별의 중심 기준 픽셀 오프셋. 닫힌 식이라 꼬리가 과거 시각의 위치를 그대로 다시 계산한다.
        private void EvaluateStar(int _index, float _time, out float _x, out float _y)
        {
            float age = Mathf.Max(0.0f, _time - stars[_index].delay);
            float expand = EaseOutQuint(Mathf.Clamp01(age / StarExpandTime));
            float u = stars[_index].u;

            float angle = stars[_index].armAngle + u * StarSwirl + stars[_index].noise + _time * StarSpin * (1.1f - u * 0.6f);
            float radius = radiusPx * Mathf.Max(1.0f, particleSpread) * u * (0.08f + 0.92f * expand);
            float lift = StarRisePx * sizeScale * stars[_index].rise * EaseOutCubic(Mathf.Clamp01(age / StarRiseTime)) * (1.0f - 0.5f * u);

            // 소멸 구간에는 별들도 천천히 위로 떠올라 흩어진다
            lift += vanish * vanish * (30.0f + 40.0f * stars[_index].rise) * sizeScale;

            _x = Mathf.Cos(angle) * radius;
            _y = Mathf.Sin(angle) * radius * groundSquashY + lift;
        }

        // 스파크의 중심 기준 픽셀 오프셋(닫힌 식). 강한 감속으로 팡 나가고, 이동이 끝난 뒤에는 천천히 떠오른다.
        private void EvaluateSpark(int _index, float _age, float _cos, float _sin, out float _x, out float _y)
        {
            float t = Mathf.Clamp01(_age / SparkTravelTime);
            float distance = radiusPx * sparks[_index].distance * EaseOutQuint(t);
            float lift = sparks[_index].lift * sizeScale * EaseOutCubic(t);

            if (_age > SparkTravelTime) lift += (_age - SparkTravelTime) * 22.0f * sizeScale;

            _x = _cos * distance;
            _y = _sin * distance + lift;
        }

        // ---------- 쿼드 쓰기 (같은 줄 병합) ----------

        private static bool IsSameColor(Color32 _a, Color32 _b)
        {
            return _a.r == _b.r && _a.g == _b.g && _a.b == _b.b && _a.a == _b.a;
        }

        private void FlushRun()
        {
            if (false == bRunActive) return;
            bRunActive = false;

            AddRect(runX0, runY, runX1, runY + 1, runColor);
        }

        // 지금 그리는 대상 버퍼를 바꾼다. 진행 중인 줄은 이전 버퍼에 먼저 쓴다.
        private void SetActiveBuffer(PixelQuadBuffer _buffer)
        {
            FlushRun();
            activeBuffer = _buffer;
        }

        // 두 버퍼를 합친 쿼드 수가 예산을 넘었는지
        private bool IsOverBudget()
        {
            return QuadBudget * 4 <= groundBuffer.VertexCount + skyBuffer.VertexCount;
        }

        // 월드 픽셀 좌표 직사각형 [x0,x1) x [y0,y1)를 지금 그리는 버퍼에 쿼드로 추가한다.
        private void AddRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (true == IsOverBudget()) return;

            activeBuffer.AddWorldRect(_x0, _y0, _x1, _y1, _color);
        }

        // 풀려난 분해 입자를 공중 버퍼에 직접 쓴다(바닥 메쉬를 그리는 중에도 공중 메쉬로 나간다).
        private void AddSkyRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            if (true == IsOverBudget()) return;

            skyBuffer.AddWorldRect(_x0, _y0, _x1, _y1, _color);
        }

        // `.`(0), `+`(1), `x`(2) 입자 하나를 공중 버퍼에 쓴다
        private void EmitParticle(int _x, int _y, Color32 _color, int _shape)
        {
            AddSkyRect(_x, _y, _x + 1, _y + 1, _color);

            if (1 == _shape)
            {
                AddSkyRect(_x - 1, _y, _x, _y + 1, _color);
                AddSkyRect(_x + 1, _y, _x + 2, _y + 1, _color);
                AddSkyRect(_x, _y - 1, _x + 1, _y, _color);
                AddSkyRect(_x, _y + 1, _x + 1, _y + 2, _color);
            }
            else if (2 == _shape)
            {
                AddSkyRect(_x - 1, _y - 1, _x, _y, _color);
                AddSkyRect(_x + 1, _y - 1, _x + 2, _y, _color);
                AddSkyRect(_x - 1, _y + 1, _x, _y + 2, _color);
                AddSkyRect(_x + 1, _y + 1, _x + 2, _y + 2, _color);
            }
        }

        // 마법진 샘플 하나의 분해 상태를 계산한다. 아직 제자리면 Keep, 사라졌으면 Gone, 입자로 풀려 날아가는 중이면 Particle이며
        // 이때 위치와 색, 모양(_shape)을 바꿔 돌려준다. 시작 시각은 바깥 링일수록 빠르고(파도처럼 안쪽으로 번진다) 샘플별 지터를 더한다.
        // 시작 시각이 되면 대부분의 샘플은 그 자리에서 사라지고 일부만 독립 입자가 된다(선 조각이 통째로 날아가지 않는다).
        private int ResolveDissolve(ref int _x, ref int _y, ref Color32 _color, out int _shape)
        {
            _shape = 0;

            float dx = _x - centerX;
            float dy = (_y - centerY) / groundSquashY;
            float length = Mathf.Sqrt(dx * dx + dy * dy);
            float ratio = Mathf.Min(1.0f, length / radiusPx);

            float h = Hash01(plotSalt, plotIndex);
            float start = (1.0f - ratio) * DissolveSweep + h * DissolveJitter;
            float local = vanish - start;

            if (0.0f > local) return DissolveKeep;

            // 입자가 될 샘플만 남기고 나머지는 시작 시각에 바로 사라진다
            float spawnRoll = Hash01(plotIndex + 53, plotSalt + 29);
            if (DissolveSpawnRate <= spawnRoll) return DissolveGone;

            float t = local / DissolveParticleLife;
            if (1.0f <= t) return DissolveGone;

            // 후반에는 프레임을 건너뛰며 점멸하다가 사라진다
            if (0.75f < t && 0 != ((Mathf.FloorToInt(elapsed * 30.0f) + plotIndex) & 1)) return DissolveGone;

            float h2 = Hash01(plotIndex + 31, plotSalt + 17);
            float inv = 0.0f < length ? 1.0f / length : 0.0f;
            float ux = dx * inv;
            float uy = dy * inv;

            // 가속 상승(t^1.5) + 반시계 소용돌이 + 바깥 방향 + 좌우 흔들림. 샘플마다 값이 달라 입자가 제각각 흩어진다.
            float rise = t * Mathf.Sqrt(t);
            float lift = (DissolveLiftMinPx + DissolveLiftRangePx * h2) * sizeScale * rise;
            float swirl = DissolveSwirlPx * (0.5f + h) * sizeScale * t;
            float outward = DissolveOutPx * (0.3f + h2) * sizeScale * t;
            float wobble = Mathf.Sin(t * 9.0f + h * TwoPi) * 2.0f * sizeScale;

            float ox = ux * outward - uy * swirl + wobble;
            float oy = (uy * outward + ux * swirl) * groundSquashY + lift;

            _x += Mathf.RoundToInt(ox);
            _y += Mathf.RoundToInt(oy);

            // 풀린 직후는 잠깐 흰색이고 올라가며 하늘색 -> 파랑으로 식는다. 입자마다 위상이 다른 점멸을 섞는다.
            Color32 baseColor = t < 0.2f ? ColorWhite : (t < 0.7f ? ColorIce : ColorBlue);
            float glow = Mathf.Lerp(0.8f, 0.25f, t);
            if (0 != (Mathf.FloorToInt(t * 14.0f + h * 7.0f) & 1)) glow *= 0.5f;
            _color = GlowColor(baseColor, glow);

            // 모양은 샘플마다 고정(`.` 60% / `+` 25% / `x` 15%). 초반에만 `+`/`x`이고 올라가며 `.` 점으로 줄어든다.
            float shapeRoll = Hash01(plotIndex * 5 + 1, plotSalt * 7 + 3);
            int kind = shapeRoll < 0.6f ? 0 : (shapeRoll < 0.85f ? 1 : 2);
            _shape = t < DissolveShapeEnd ? kind : 0;

            return DissolveParticle;
        }

        // 픽셀 하나를 찍는다. 같은 줄에서 이어지는 같은 색 픽셀은 하나의 쿼드로 합친다.
        // 소멸 구간에는 마법진 샘플(_bDrift)이 입자로 풀려 날아가거나 사라지고, 그 밖의 픽셀은 해시로 빠진다.
        private void Plot(int _x, int _y, Color32 _color, bool _bDrift)
        {
            if (0.0f < vanish)
            {
                if (true == _bDrift)
                {
                    int shape;
                    int state = ResolveDissolve(ref _x, ref _y, ref _color, out shape);
                    if (DissolveGone == state) return;

                    if (DissolveParticle == state)
                    {
                        EmitParticle(_x, _y, _color, shape);
                        return;
                    }
                }
                else if (Hash01(_x, _y) < vanish)
                {
                    return;
                }
            }

            if (true == bRunActive && runY == _y && true == IsSameColor(runColor, _color))
            {
                if (runX0 <= _x && _x < runX1) return;
                if (_x == runX1)
                {
                    runX1++;
                    return;
                }

                if (_x == runX0 - 1)
                {
                    runX0--;
                    return;
                }
            }

            FlushRun();
            bRunActive = true;
            runX0 = _x;
            runX1 = _x + 1;
            runY = _y;
            runColor = _color;
        }

        // 바닥 타원 위의 점(반지름 _radius px, 각도 _angle)을 픽셀로 찍는다. _yOffset은 바닥에서 띄운 높이.
        private void PlotEllipse(float _angle, float _radius, float _yOffset, Color32 _color, bool _bDrift)
        {
            int px = centerX + Mathf.RoundToInt(Mathf.Cos(_angle) * _radius);
            int py = centerY + Mathf.RoundToInt(Mathf.Sin(_angle) * _radius * groundSquashY + _yOffset);
            Plot(px, py, _color, _bDrift);
        }

        // 브레젠험 직선. _dash가 0이면 실선, 아니면 _dash칸 중 앞 절반만 찍는 점선.
        private void DrawLine(float _fx0, float _fy0, float _fx1, float _fy1, Color32 _color, int _dash, bool _bDrift)
        {
            int x0 = Mathf.RoundToInt(_fx0);
            int y0 = Mathf.RoundToInt(_fy0);
            int x1 = Mathf.RoundToInt(_fx1);
            int y1 = Mathf.RoundToInt(_fy1);

            int dx = Mathf.Abs(x1 - x0);
            int dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            int dashOn = Mathf.Max(1, _dash >> 1);

            if (true == _bDrift) plotSalt = ++primitiveCounter;

            for (int n = 0; n < MaxLinePixels; n++)
            {
                plotIndex = n;
                if (0 == _dash || n % _dash < dashOn) Plot(x0, y0, _color, _bDrift);

                if (x0 == x1 && y0 == y1) break;

                int doubled = error * 2;
                if (doubled >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (doubled <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        // 선을 오프셋 좌표(중심 기준)로 그린다
        private void DrawLineLocal(float _ax, float _ay, float _bx, float _by, Color32 _color, int _dash, bool _bDrift)
        {
            DrawLine(centerX + _ax, centerY + _ay, centerX + _bx, centerY + _by, _color, _dash, _bDrift);
        }

        // 바닥 타원 호. _frac(0~1)만큼만 그려서 "그려지는" 연출을 만든다. _gaps가 0보다 크면 원을 _gaps등분해 각 구간 끝에 틈을 낸다.
        private void DrawRing(float _radius, float _rotation, Color32 _color, float _frac, int _dash, bool _bDrift, float _yOffset, int _gaps = 0)
        {
            if (0.0f >= _frac || 0.5f >= _radius) return;

            int total = Mathf.Max(16, Mathf.CeilToInt(TwoPi * _radius * 1.3f));
            int drawCount = Mathf.FloorToInt(total * Mathf.Min(1.0f, _frac));
            int dashOn = Mathf.Max(1, _dash >> 1);

            if (true == _bDrift) plotSalt = ++primitiveCounter;

            for (int i = 0; i < drawCount; i++)
            {
                plotIndex = i;
                if (0 < _dash && i % _dash >= dashOn) continue;

                float angle = _rotation + TwoPi * i / total;
                if (0 < _gaps)
                {
                    float q = angle / TwoPi;
                    q -= Mathf.Floor(q);
                    if ((q * _gaps * 5.0f) % 5.0f < 0.55f) continue;
                }

                PlotEllipse(angle, _radius, _yOffset, _color, _bDrift);
            }
        }

        // {n/k} 별 다각형. 변을 하나씩 순서대로 그려서 _frac만큼 진행한다.
        private void DrawStarPolygon(int _n, int _k, float _radius, float _rotation, Color32 _color, float _frac, int _dash, bool _bDrift)
        {
            if (0.0f >= _frac) return;

            for (int i = 0; i < _n; i++)
            {
                float f = Mathf.Clamp01(_frac * _n - i);
                if (0.0f >= f) break;

                float a1 = _rotation + TwoPi * i / _n;
                float a2 = _rotation + TwoPi * ((i + _k) % _n) / _n;
                float x1 = Mathf.Cos(a1) * _radius;
                float y1 = Mathf.Sin(a1) * _radius * groundSquashY;
                float x2 = Mathf.Cos(a2) * _radius;
                float y2 = Mathf.Sin(a2) * _radius * groundSquashY;

                DrawLineLocal(x1, y1, x1 + (x2 - x1) * f, y1 + (y2 - y1) * f, _color, _dash, _bDrift);
            }
        }

        // 두 반지름 사이를 잇는 방사형 눈금
        private void DrawTicks(float _radiusInner, float _radiusOuter, int _count, float _rotation, Color32 _color, float _frac, bool _bDrift)
        {
            int drawCount = Mathf.FloorToInt(_count * Mathf.Min(1.0f, _frac));
            for (int i = 0; i < drawCount; i++)
            {
                float angle = _rotation + TwoPi * i / _count;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle) * groundSquashY;
                DrawLineLocal(cos * _radiusInner, sin * _radiusInner, cos * _radiusOuter, sin * _radiusOuter, _color, 0, _bDrift);
            }
        }

        // `+` 또는 `x` 모양 별. _arm이 0이면 점 하나. 분해 구간에는 칸마다 독립 입자로 풀린다.
        private void DrawSparkle(int _px, int _py, int _arm, bool _bPlus, Color32 _color, bool _bDrift)
        {
            int cell = 0;
            if (true == _bDrift) plotSalt = ++primitiveCounter;

            plotIndex = cell++;
            Plot(_px, _py, _color, _bDrift);

            for (int d = 1; d <= _arm; d++)
            {
                if (true == _bPlus)
                {
                    plotIndex = cell++;
                    Plot(_px + d, _py, _color, _bDrift);
                    plotIndex = cell++;
                    Plot(_px - d, _py, _color, _bDrift);
                    plotIndex = cell++;
                    Plot(_px, _py + d, _color, _bDrift);
                    plotIndex = cell++;
                    Plot(_px, _py - d, _color, _bDrift);
                }
                else
                {
                    plotIndex = cell++;
                    Plot(_px + d, _py + d, _color, _bDrift);
                    plotIndex = cell++;
                    Plot(_px - d, _py + d, _color, _bDrift);
                    plotIndex = cell++;
                    Plot(_px + d, _py - d, _color, _bDrift);
                    plotIndex = cell++;
                    Plot(_px - d, _py - d, _color, _bDrift);
                }
            }
        }

        // ---------- 그리기 ----------

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            bool bSplit = null != groundMesh;

            skyBuffer.Clear();
            skyBuffer.SetWorldTransform(transform.worldToLocalMatrix, pos.z);

            // 바닥 메쉬가 없으면(프리팹 참조 누락) 모두 공중 메쉬 하나로 그린다
            PixelQuadBuffer groundTarget = skyBuffer;
            if (true == bSplit)
            {
                groundBuffer.Clear();
                groundBuffer.SetWorldTransform(groundMeshRenderer.transform.worldToLocalMatrix, pos.z);
                groundTarget = groundBuffer;
            }

            centerX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            centerY = Mathf.FloorToInt(pos.y * PixelsPerUnit);
            bRunActive = false;
            primitiveCounter = 0;

            vanish = Mathf.Clamp01((elapsed - DissolveStart) / DissolveTime);
            stepTime = Mathf.Floor(elapsed * StepRate) / StepRate;

            // 바닥: 충격파, 마법진, 분해 시작 고리. 풀려난 분해 입자는 공중 버퍼로 바로 나간다.
            SetActiveBuffer(groundTarget);
            DrawShockwave();
            DrawMagicCircle();
            DrawDissolveRing();

            // 공중: 빛기둥, 스파크, 별, 불씨, 중심 섬광
            SetActiveBuffer(skyBuffer);
            DrawPillars();
            DrawSparks();
            DrawStars();
            DrawEmbers();
            DrawCore();
            FlushRun();

            skyBuffer.Upload(mesh, meshBoundsSize);
            meshRenderer.enabled = true;

            if (true == bSplit)
            {
                groundBuffer.Upload(groundMesh, meshBoundsSize);
                groundMeshRenderer.enabled = true;
            }
        }

        // 충격파 - 점선 고리 하나가 바닥으로 퍼진다. maxRadius에 닿는다.
        private void DrawShockwave()
        {
            if (ShockEnd <= elapsed) return;

            float fade = 1.0f - Mathf.Clamp01((elapsed - ShockFadeStart) / ShockFadeTime);
            float radius = radiusPx * EaseOutCubic(Mathf.Clamp01(elapsed / ShockDuration));

            DrawRing(radius, 0.0f, GlowColor(ColorIce, 0.7f * fade), 1.0f, 4, false, 0.0f);
        }

        // 마법진 4겹 - 바깥에서 안쪽으로 "그려지듯" 펼쳐진다. 회전은 12fps 스텝이고 바깥 링은 고정이다.
        private void DrawMagicCircle()
        {
            for (int i = 0; i < LayerCount; i++)
            {
                float since = elapsed - LayerDelay[i];
                layerRadius[i] = 0.4f + 0.6f * EaseOutCubic(Mathf.Clamp01(since / LayerDuration[i]));
                layerFrac[i] = EaseOutCubic(Mathf.Clamp01(since / (LayerDuration[i] * 0.9f)));
            }

            float r0 = radiusPx;

            // 0. 바깥 2px 링(호 단위 틈 3곳) + 안쪽 점선 링 + 12개 눈금
            float r = r0 * layerRadius[0];
            float frac = layerFrac[0];
            Color32 outerColor = GlowColor(ColorIce, 0.95f * GroundGlow);
            DrawRing(r, 0.0f, outerColor, frac, 0, true, 0.0f, 3);
            DrawRing(Mathf.Max(0.0f, r - 1.0f), 0.0f, outerColor, frac, 0, true, 0.0f, 3);
            DrawRing(Mathf.Max(0.0f, r - 7.0f), stepTime * 0.1f, GlowColor(ColorBlue, LineGlow), frac, 2, true, 0.0f);
            DrawTicks(Mathf.Max(0.0f, r - 4.0f), Mathf.Max(0.0f, r - 1.0f), 12, stepTime * 0.1f, GlowColor(ColorWhite, 0.6f * GroundGlow), frac, true);

            // 1. 9각 별 하나
            DrawStarPolygon(StarPolygonPoints, 4, r0 * 0.78f * layerRadius[1], -stepTime * 0.25f, GlowColor(ColorBlue, 1.2f * LineGlow), layerFrac[1], 0, true);

            // 2. 룬 링 - 링이 다 그려진 뒤 금색 룬 6개가 켜진다
            r = r0 * 0.46f * layerRadius[2];
            DrawRing(r, 0.0f, GlowColor(ColorBlue, LineGlow), layerFrac[2], 3, true, 0.0f);
            if (0.95f < layerFrac[2])
            {
                Color32 runeColor = GlowColor(ColorGold, 0.55f);
                for (int i = 0; i < 6; i++)
                {
                    float angle = stepTime * 0.15f + TwoPi * i / 6.0f;
                    int px = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * r);
                    int py = centerY + Mathf.RoundToInt(Mathf.Sin(angle) * r * groundSquashY);
                    DrawSparkle(px, py, 2, true, runeColor, true);
                }
            }

            // 3. 코어 링 + 도트 별
            DrawRing(r0 * 0.13f * layerRadius[3], -stepTime * 0.6f, GlowColor(ColorWhite, 0.3f), layerFrac[3], 0, true, 0.0f);
            if (0.5f < layerFrac[3]) DrawCoreStar();
        }

        // 코어 도트 별 - 7x7와 5x5 두 크기를 번갈아 맥동한다. 칸마다 고정된 번호를 줘서 분해될 때 입자가 깜빡이지 않는다.
        private void DrawCoreStar()
        {
            string[] rows = 0 == (Mathf.FloorToInt(elapsed * 6.0f) & 1) ? CoreStarLarge : CoreStarSmall;
            int half = CoreStarSize / 2;

            plotSalt = ++primitiveCounter;

            for (int y = 0; y < CoreStarSize; y++)
            {
                for (int x = 0; x < CoreStarSize; x++)
                {
                    char cell = rows[y][x];
                    if ('.' == cell) continue;

                    Color32 color;
                    if ('W' == cell) color = GlowColor(ColorWhite, 0.35f);
                    else if ('I' == cell) color = GlowColor(ColorIce, 0.2f);
                    else color = GlowColor(ColorBlue, 0.15f);

                    plotIndex = y * CoreStarSize + x;
                    Plot(centerX + x - half, centerY + half - y, color, true);
                }
            }
        }

        // 빛기둥 - 9각 별의 꼭짓점 9곳에서 점선 기둥이 솟는다. 3곳 걸러 하나만 높고 나머지는 절반 높이다.
        // 꼭짓점 개수와 각도는 마법진 9각 별과 같은 값(StarPolygonPoints)에서 가져와서 기둥이 별 꼭짓점 사이로 어긋나지 않는다.
        private void DrawPillars()
        {
            if (elapsed <= PillarStart || PillarEnd <= elapsed) return;

            float rise = EaseOutCubic(Mathf.Clamp01((elapsed - PillarStart) / PillarRiseTime));
            float shrink = 1.0f - Mathf.Clamp01((elapsed - PillarShrinkStart) / PillarShrinkTime);
            float height = PillarHeightPx * sizeScale * rise * shrink;
            if (2.0f > height) return;

            // 기둥은 소멸 구간에 들어가기 전에 이미 줄어들므로 해시 소멸 없이 쿼드로 바로 쓴다
            float radius = radiusPx * 0.78f * layerRadius[1];
            float rotation = -stepTime * 0.25f;
            for (int v = 0; v < StarPolygonPoints; v++)
            {
                float angle = rotation + TwoPi * v / StarPolygonPoints;
                int bx = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int by = centerY + Mathf.RoundToInt(Mathf.Sin(angle) * radius * groundSquashY);
                DrawPillar(bx, by, 0 == (v % 3) ? height : height * 0.5f);
            }
        }

        private void DrawPillar(int _bx, int _by, float _height)
        {
            int total = Mathf.RoundToInt(_height);

            for (int y = 0; y < total; y += 3)
            {
                float fade = 1.0f - y / (_height + 1.0f) * 0.65f;
                bool bTop = y > total - 10;
                Color32 color = GlowColor(bTop ? ColorWhite : ColorIce, fade * 0.7f);

                int segmentTop = Mathf.Min(total, y + 2);
                AddRect(_bx, _by + y, _bx + 1, _by + segmentTop, color);
            }

            // 끝에 금색 `+` 별이 맺힌다
            if (8 < total)
            {
                int topY = _by + total;
                Color32 gold = GlowColor(ColorGold, 0.8f);
                AddRect(_bx - 2, topY, _bx + 3, topY + 1, gold);
                AddRect(_bx, topY - 2, _bx + 1, topY + 3, gold);
            }
        }

        // 분해 시작 충격파 - 마법진이 풀리는 순간 점선 고리가 한 번 더 바깥으로 퍼진다
        private void DrawDissolveRing()
        {
            float since = elapsed - DissolveStart;
            if (0.0f >= since || DissolveRingTime <= since) return;

            float t = since / DissolveRingTime;
            float radius = radiusPx * Mathf.Lerp(0.4f, 1.1f, EaseOutQuint(t));
            float fade = 1.0f - t;

            DrawRing(radius, 0.0f, GlowColor(ColorIce, 0.4f * fade), 1.0f, 3, false, 0.0f);
        }

        // 방사 스파크 - 충격파와 함께 바깥으로 팡 튀어 나가는 큰 `x`/`+` 별. 긴 꼬리를 끌고 위로 떠오르며 사라진다.
        private void DrawSparks()
        {
            for (int i = 0; i < sparkCount; i++)
            {
                float age = elapsed - sparks[i].delay;
                if (0.0f > age) continue;

                float life = age / SparkLife;
                if (1.0f <= life) continue;

                // 후반에는 프레임을 건너뛰며 점멸하다가 사라진다
                if (0.75f < life && 0 != ((Mathf.FloorToInt(elapsed * 30.0f) + i) & 1)) continue;

                float cos = Mathf.Cos(sparks[i].angle);
                float sin = Mathf.Sin(sparks[i].angle) * groundSquashY;

                float x;
                float y;
                EvaluateSpark(i, age, cos, sin, out x, out y);

                // 이동 중에는 긴 점 꼬리를 남긴다
                if (age < SparkTravelTime)
                {
                    for (int k = 1; k <= SparkTrailDots; k++)
                    {
                        float pastAge = age - k * SparkTrailSpacing;
                        if (0.0f > pastAge) break;

                        float tx;
                        float ty;
                        EvaluateSpark(i, pastAge, cos, sin, out tx, out ty);
                        Plot(centerX + Mathf.RoundToInt(tx), centerY + Mathf.RoundToInt(ty), GlowColor(ColorIce, 0.55f - k * 0.08f), false);
                    }
                }

                // 큰 별 -> 중간 -> 작은 별로 계단식으로 줄어든다. `+`와 `x`가 빠르게 번갈아 반짝인다.
                int arm = life < 0.45f ? 3 : (life < 0.7f ? 2 : 1);
                bool bPlus = 0 == ((Mathf.FloorToInt(age * 14.0f) + i) & 1);
                float glow = 1.0f - life * 0.5f;
                DrawSparkle(centerX + Mathf.RoundToInt(x), centerY + Mathf.RoundToInt(y), arm, bPlus, GlowColor(PaletteColor(sparks[i].colorIndex), glow), false);
            }
        }

        // 은하 나선 - 별이 나선팔로 휘감기며 퍼지고 위로 솟구친다. 주인공 별만 파란 점 꼬리를 남기고 크게 반짝인다.
        private void DrawStars()
        {
            for (int i = 0; i < starCount; i++)
            {
                float age = elapsed - stars[i].delay;
                if (0.0f > age) continue;

                // 소멸 구간에는 프레임을 건너뛰며 점멸한다
                if (0.35f < vanish && 0 != ((Mathf.FloorToInt(elapsed * 30.0f) + i) & 1)) continue;

                float x;
                float y;
                EvaluateStar(i, elapsed, out x, out y);

                if (true == stars[i].bHero && age < TrailActiveTime)
                {
                    for (int k = 1; k <= TrailDots; k++)
                    {
                        float pastTime = elapsed - k * TrailSpacing;
                        if (pastTime < stars[i].delay) break;

                        float tx;
                        float ty;
                        EvaluateStar(i, pastTime, out tx, out ty);
                        Plot(centerX + Mathf.RoundToInt(tx), centerY + Mathf.RoundToInt(ty), GlowColor(ColorBlue, 0.6f - k * 0.1f), false);
                    }
                }

                // 별마다 위상이 다른 3단계 반짝임(어두움/보통/밝음)
                int twinkle = (Mathf.FloorToInt(age * 12.0f) + stars[i].seed) % 3;
                float glow = 0 == twinkle ? 0.5f : (1 == twinkle ? 0.75f : 1.0f);

                // 중심 쪽은 별이 겹겹이 쌓여 블룸이 번지므로 어둡게 두고, 바깥으로 갈수록 밝게 한다.
                // 주인공이 아닌 별은 한 단계 더 어둡게 해서 주인공 별이 눈에 띄게 한다.
                glow *= 0.8f * (0.35f + 0.65f * Mathf.Clamp01(stars[i].u * 1.6f));
                if (false == stars[i].bHero) glow *= 0.55f;

                int px = centerX + Mathf.RoundToInt(x);
                int py = centerY + Mathf.RoundToInt(y);

                if (0 == stars[i].kind)
                {
                    Plot(px, py, GlowColor(PaletteColor(stars[i].colorIndex), glow), false);
                    continue;
                }

                int arm = true == stars[i].bHero ? (age < 1.2f ? 3 : 2) : (age < 1.1f ? 2 : 1);
                bool bPlus = 0 == ((Mathf.FloorToInt(age * 8.0f) + stars[i].seed) & 1);
                Color32 color = true == stars[i].bHero ? GlowColor(ColorWhite, 1.0f) : GlowColor(PaletteColor(stars[i].colorIndex), glow);
                DrawSparkle(px, py, arm, bPlus, color, false);
            }
        }

        // 불씨 - 소수의 `.` 입자가 바깥으로 퍼지며 올라간다
        private void DrawEmbers()
        {
            if (elapsed <= EmberStart || EmberEnd <= elapsed) return;

            for (int i = 0; i < emberCount; i++)
            {
                // 마지막 0.3초는 프레임을 건너뛰며 점멸하다가 사라진다
                if (EmberEnd - 0.3f < elapsed && 0 != ((Mathf.FloorToInt(elapsed * 30.0f) + i) & 1)) continue;

                float p = (elapsed * embers[i].speed + embers[i].offset) % 1.0f;

                // 오르면서 바깥쪽으로 퍼진다
                float radius = radiusPx * embers[i].radiusScale * (1.0f + EmberOutward * p);
                int px = centerX + Mathf.RoundToInt(Mathf.Cos(embers[i].angle) * radius);
                int py = centerY + Mathf.RoundToInt(Mathf.Sin(embers[i].angle) * radius * groundSquashY + p * EmberRisePx * sizeScale);

                Color32 baseColor = p < 0.35f ? ColorWhite : (p < 0.7f ? ColorIce : ColorBlue);
                Plot(px, py, GlowColor(baseColor, (1.0f - p * 0.6f) * 0.4f), false);
            }
        }

        // 중심에서 d칸 떨어진 곳의 반 굵기(0이면 1px). 중심부는 두껍고 끝으로 갈수록 계단식으로 가늘어진다.
        private static int FlashHalfThickness(int _stage, int _d)
        {
            if (0 == _stage) return _d <= 2 ? 2 : (_d <= 5 ? 1 : 0);
            if (1 == _stage) return _d <= 3 ? 1 : 0;
            return _d <= 1 ? 1 : 0;
        }

        // 시작 순간의 십자 섬광(임팩트) - 굵기가 변하는 도트 별을 4단계로 끊어서 줄인다. 중심은 흰색, 중간은 하늘색, 끝은 파랑이고
        // 큰 두 단계에는 대각선에 금색 도트 블록이 붙는다. 이후 중심은 마법진 코어가 맡는다.
        private void DrawCore()
        {
            if (FlashEnd <= elapsed) return;

            int stage = Mathf.Min(FlashStageCount - 1, Mathf.FloorToInt(elapsed / FlashStageTime));
            int arm = 0 == stage ? 12 : (1 == stage ? 9 : (2 == stage ? 6 : 3));
            int midReach = Mathf.Max(5, Mathf.RoundToInt(arm * 0.6f));

            // 발광을 낮게 둬서 블룸이 두꺼운 도트 블록의 윤곽을 덮지 않게 한다(도트로 읽히는 것이 우선이다)
            Color32 white = GlowColor(ColorWhite, 0.12f);
            Color32 ice = GlowColor(ColorIce, 0.08f);
            Color32 blue = GlowColor(ColorBlue, 0.05f);

            for (int d = 0; d <= arm; d++)
            {
                int h = FlashHalfThickness(stage, d);
                Color32 color = d <= 3 ? white : (d <= midReach ? ice : blue);

                // 오른쪽, 왼쪽, 위, 아래 팔의 d번째 칸(굵기 2h+1)
                AddRect(centerX + d, centerY - h, centerX + d + 1, centerY + h + 1, color);
                AddRect(centerX - d, centerY - h, centerX - d + 1, centerY + h + 1, color);
                AddRect(centerX - h, centerY + d, centerX + h + 1, centerY + d + 1, color);
                AddRect(centerX - h, centerY - d, centerX + h + 1, centerY - d + 1, color);
            }

            if (2 <= stage) return;

            // 대각선 금색 도트 블록(2x2 한 쌍과 끝의 1x1)
            Color32 gold = GlowColor(ColorGold, 0.2f);
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    int x0 = sx < 0 ? centerX - 5 : centerX + 4;
                    int y0 = sy < 0 ? centerY - 5 : centerY + 4;
                    AddRect(x0, y0, x0 + 2, y0 + 2, gold);

                    int tipX = centerX + sx * 8;
                    int tipY = centerY + sy * 8;
                    AddRect(tipX, tipY, tipX + 1, tipY + 1, gold);
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
                meshRenderer.enabled = false;
                if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
                return;
            }

            // 첫 프레임은 시간을 더하지 않아서 t=0의 섬광 프레임이 반드시 한 번 그려진다
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

            // VFXComponent가 재생할 때 자식 렌더러 전체에 같은 소팅을 덮어쓰므로, 바닥 메쉬는 자기 레이어로 다시 맞춘다
            if (null != groundMesh)
            {
                if (groundMeshRenderer.sortingLayerID != groundSortingLayerId) groundMeshRenderer.sortingLayerID = groundSortingLayerId;
                if (groundMeshRenderer.sortingOrder != groundSortingOrder) groundMeshRenderer.sortingOrder = groundSortingOrder;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            overrideRadius = 0.0f;

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
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
            groundSquashY = Mathf.Clamp(groundSquashY, 0.2f, 1.0f);
            particleSpread = Mathf.Clamp(particleSpread, 1.0f, 2.5f);
            density = Mathf.Clamp(density, 0.25f, 2.0f);
            starsPerUnit = Mathf.Max(1.0f, starsPerUnit);
            heroStarCount = Mathf.Max(0, heroStarCount);
            embersPerUnit = Mathf.Max(0.0f, embersPerUnit);
            sparksPerUnit = Mathf.Max(0.0f, sparksPerUnit);
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
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * groundSquashY, 0.0f);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
