using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 포자 대폭발 연출. 쓰러진 나무 자리로 포자 알갱이가 한 점에 빨려 들어가 압축되었다가(응집) 터지며, 기존 포자 폭발(SporeExplosionVFX)과
    /// 같은 연막 스프라이트 프레임을 1:1 픽셀 그대로 여러 장 겹친 큰 연막과 사방으로 튀는 점선, 고속 알갱이 층, 충격파 고리로 그려진다.
    /// 알갱이는 낙하 도중 공중에서 분해되어 사라지며 바닥에 닿지 않는다.
    ///
    /// 구성
    /// - 연막(SpriteRenderer 자식 N개): 기존 연막 스프라이트 프레임. 확대/회전/보간 없이 정수 픽셀 격자에 맞춘다.
    /// - 바닥 메쉬(ShockWave 레이어): 범위 윤곽, 충격파 고리.
    /// - 공중 메쉬(루트 렌더러 레이어): 응집 알갱이, 압축 핵, 십자 별, 방사 점선, 구름/고속 알갱이.
    ///
    /// 픽셀 규칙
    /// - JAGGIES: 모든 타원은 midpoint 알고리즘 한 가지로만 그린다(곡선 흔들림 없음).
    /// - DOUBLE PIXEL: 선과 윤곽은 L자 모서리 픽셀을 제거해 1px 굵기를 유지한다.
    /// - HUGGING: 충격파 두 겹은 다른 시점에 퍼져 붙지 않고, 윤곽 위에 그림자/보조선을 겹치지 않는다.
    ///
    /// 색은 옅은 3톤만 쓰고 발광은 쓰지 않는다(정점 알파 0). 단계(SetTier 1~3)에 따라 초록/연보라/금으로 바뀌며, 0이면 기존 연막과 같은 민트다.
    /// 연막 스프라이트도 단계별 색으로 교체한 시트(SporeExplosion_Tier1~3)를 쓴다.
    /// 위치는 경과 시간만으로 계산하는 닫힌 식이라 프레임 속도와 무관하고 상태 저장이 없다. 재생 중 힙 할당이 없다.
    /// 풀 인스턴스로 쓰이며 루트 ParticleSystem 수명이 끝나면 풀로 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_SporeBigExplosion : MonoBehaviour
    {
        [Header("범위 (월드 유닛, 32 PPU 기준 1유닛 = 32px)")]
        [SerializeField, Tooltip("타원 범위의 가로 반지름. 피격 판정(sporeBigExplosionRange)과 같은 값이어야 한다. 세로는 0.5배. 재생 직전 SetRange로 덮어쓸 수 있다.")]
        private float rangeRadius = 2.5f;

        [Header("밀도")]
        [SerializeField, Tooltip("알갱이 개수에 곱하는 배율. 1이 기본이다.")]
        private float density = 1.0f;

        [Header("연막(기존 포자 폭발 스프라이트 프레임)")]
        [SerializeField, Tooltip("연막 SpriteRenderer 자식들. 프리팹 빌더가 만든다.")]
        private SpriteRenderer[] puffRenderers;
        [SerializeField, Tooltip("SporeExplosionVFX와 같은 스프라이트 프레임 배열(0번은 흰 원판이라 1번부터 쓴다). 단계 0(민트)에서 쓴다.")]
        private Sprite[] puffFrames;
        [SerializeField, Tooltip("1단계(초록) 연막 프레임")]
        private Sprite[] puffFramesTier1;
        [SerializeField, Tooltip("2단계(연보라) 연막 프레임")]
        private Sprite[] puffFramesTier2;
        [SerializeField, Tooltip("3단계(금) 연막 프레임")]
        private Sprite[] puffFramesTier3;
        [SerializeField, Tooltip("어두운 연막(스모크) 프레임. 단계 0(민트)")]
        private Sprite[] smokeFramesTier0;
        [SerializeField, Tooltip("어두운 연막(스모크) 프레임. 1단계(초록)")]
        private Sprite[] smokeFramesTier1;
        [SerializeField, Tooltip("어두운 연막(스모크) 프레임. 2단계(연보라)")]
        private Sprite[] smokeFramesTier2;
        [SerializeField, Tooltip("어두운 연막(스모크) 프레임. 3단계(금)")]
        private Sprite[] smokeFramesTier3;
        [SerializeField, Tooltip("밝은 연막 최대 개수(0~5). 실제 개수는 재생마다 3~이 값 사이에서 무작위로 정해진다. puffRenderers의 앞 5개가 밝은 연막, 뒤 2개가 스모크다.")]
        private int puffCount = 5;
        [SerializeField, Tooltip("스모크 개수(0~2). 줄이면 어두운 연기 비중이 줄어든다.")]
        private int smokeCount = 2;
        [SerializeField, Tooltip("연막 높이 보정(픽셀). 올리면 나무 위로, 내리면 바닥에 가깝게 나온다.")]
        private float puffHeightPx = 18.0f;

        [Header("발광 (후처리 블룸 임계값을 넘겨야 번진다: 정점 알파 0~1 = 머티리얼 GlowMin~GlowMax 배율)")]
        [SerializeField, Range(0.0f, 1.0f), Tooltip("기본 톤 알갱이/고리/점선의 발광 세기")]
        private float glowBase = 0.15f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("밝은 톤 알갱이의 발광 세기")]
        private float glowLight = 0.45f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("중심 핵/십자 별/방사 점선 머리처럼 가장 강한 부분의 발광 세기")]
        private float glowHot = 1.0f;

        [Header("정렬")]
        [SerializeField, Tooltip("공중 메쉬를 루트 렌더러(VFXComponent가 정한 소팅)보다 이 값만큼 위에 그린다")]
        private int sortingOrderOffset = 0;

        [Header("바닥 메쉬 (고리가 나무/캐릭터 뒤에 그려지도록 분리)")]
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField, Tooltip("바닥 메쉬 소팅 레이어. 나무/캐릭터가 있는 Objects보다 아래여야 한다.")]
        private string groundSortingLayerName = "ShockWave";
        [SerializeField] private int groundSortingOrder = 0;

        // 바닥 타원의 세로 비율(쿼터뷰). 보이는 범위와 판정 범위가 같은 비율을 쓰도록 상수로 둔다.
        public const float GroundSquashY = 0.5f;

        private const float PixelsPerUnit = 32.0f;
        private const float TwoPi = Mathf.PI * 2.0f;

        // 타임라인(초). 응집 구간이 먼저 오고 그 뒤가 폭발 시각 0이다.
        private const float PreTime = 0.2f;
        private const float BurstLength = 1.1f;
        private const float TimelineLength = PreTime + BurstLength;

        // 응집
        private const float InhaleTravelTime = 0.15f;
        private const int InhaleCapacity = 320;
        private const int BaseInhale = 300;
        private const float InhaleTrailBack = 0.14f;
        private const int InhaleTrailMaxPixels = 7;

        // 구름/고속 알갱이
        private const int CloudGroupCount = 9;
        private const int CloudCapacity = 4800;
        private const float Gravity = 190.0f;
        private const float VerticalDrag = 1.2f;
        private const float FallGravityMul = 1.45f;
        private const float FallSwayRamp = 0.1f;
        private const float TrailTime = 0.16f;
        private const float TrailBackTime = 0.0417f;
        private const int MaxTrailPixels = 8;
        private const float DarkenMarginPx = 5.0f;
        private const float BrightHeightPx = 16.0f;
        private const int BaseGroupParticles = 140;
        private const int BaseColumn = 160;
        private const int BaseSparkle = 100;
        private const int BaseBlast = 300;

        // 방사 점선
        private const int RayCount = 22;
        private const float RayGrowTime = 0.1f;
        private const float RayEnd = 0.22f;

        // 충격파 고리
        private const float RingTravelTime = 0.30f;
        private const float RingHoldEnd = 0.7f;

        private const int MaxOutlineRadius = 140;
        private const int QuadBudget = 16000;
        private const int LineBufferSize = 160;

        // 연막 스프라이트 재생
        private const float PuffFrameRate = 22.0f;
        private const int PuffFirstFrame = 1;
        private const int PuffFrameCount = 14;
        private const int PuffMax = 5;
        private const int SmokeMax = 2;
        private const int PuffTotal = PuffMax + SmokeMax;
        private const int SmokeFirstFrame = 3;
        private const float SmokeFrameRate = 24.0f;

        // 단계별 팔레트(어두운/기본/밝은). 0 = 기존 연막과 같은 민트, 1 초록, 2 연보라, 3 금. 발광은 쓰지 않는다(정점 알파 0).
        private const int TierCount = 4;
        private static readonly byte[] TierColorBytes =
        {
            159, 228, 213, 201, 255, 247, 255, 255, 255,
            143, 217, 138, 196, 247, 189, 244, 255, 240,
            201, 184, 242, 228, 218, 251, 248, 244, 255,
            240, 207, 131, 255, 233, 180, 255, 251, 238,
        };
        private static readonly Color32[] tierPalette = new Color32[TierCount * 3];
        private static bool bPaletteReady;

        private Color32 colorDeep;
        private Color32 colorBase;
        private Color32 colorLight;
        private Color32 colorHot;

        // 연막 기준 배치(픽셀 오프셋 x, 아래 방향 양수 y, 시작 지연, 상승 폭). 재생마다 Begin에서 무작위로 흩뜨린다.
        private static readonly int[] PuffBaseX = { 0, -26, 27, -12, 14 };
        private static readonly int[] PuffBaseY = { -4, 6, 8, -14, 14 };
        private static readonly float[] PuffBaseDelay = { 0.0f, 0.015f, 0.025f, 0.04f, 0.05f };
        private static readonly float[] PuffBaseRise = { 8.0f, 4.0f, 10.0f, 6.0f, 3.0f };

        // 스모크가 들어갈 자리(밝은 연막 사이 이음새): 이 중 무작위로 2곳
        private static readonly int[] SmokeSeamX = { -13, 13, -20, 7, 22, -6 };
        private static readonly int[] SmokeSeamY = { 1, 3, 11, -9, -6, 12 };

        // 십자 별(6단계)
        private static readonly int[] StarLenRight = { 34, 26, 18, 11, 6, 3 };
        private static readonly int[] StarLenLeft = { 24, 18, 13, 8, 5, 3 };
        private static readonly int[] StarLenUp = { 13, 10, 7, 5, 3, 2 };
        private static readonly int[] StarLenDown = { 10, 8, 5, 4, 3, 2 };
        private static readonly int[] StarCoreRadius = { 9, 10, 8, 6, 4, 2 };

        private struct CloudParticle
        {
            public float cosA;
            public float sinA;
            public float reach;      // 항력으로 도달하는 최대 가로 거리(px)
            public float drag;
            public float zv;
            public float z0;
            public float delay;
            public float life;
            public float phase;
            public float apex;       // 정점 시각(이후 하강)
            public byte shape;       // 0 점, 1 +, 2 x
            public byte trail;       // 1이면 초반 속도선을 그린다
            public float swayAmp;    // 흔들림 진폭(px)
            public float swayFreq;   // 흔들림 주기(rad/s)
            public float drift;      // 정점 이후 바깥으로 계속 퍼지는 가속(px/s^2)
            public float killZ;      // 하강 중 이 높이(px) 아래가 되면 소멸
        }

        private struct InhaleParticle
        {
            public float startX;
            public float startY;
            public float delay;
            public byte big;
        }

        private struct RayData
        {
            public float cosA;
            public float sinA;
            public float length;
            public float delay;
        }

        // 타원 윤곽 표(정수 반지름별). 최초 사용 때 한 번 만든다(재생 중 할당 없음).
        private static bool[,] outlineScratch;
        private static int[][] outlineX;
        private static int[][] outlineY;
        private static float[][] outlineAngle;

        private static readonly PixelQuadBuffer skyBuffer = new PixelQuadBuffer(QuadBudget);
        private static readonly PixelQuadBuffer groundBuffer = new PixelQuadBuffer(2400);
        private static readonly int[] linePixelX = new int[LineBufferSize];
        private static readonly int[] linePixelY = new int[LineBufferSize];

        // 내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Mesh groundMesh;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly CloudParticle[] cloud = new CloudParticle[CloudCapacity];
        private readonly InhaleParticle[] inhale = new InhaleParticle[InhaleCapacity];
        private readonly RayData[] rays = new RayData[RayCount];
        private readonly float[] ringGapCenter = new float[10];
        private readonly float[] ringGapHalf = new float[10];

        // 상태 변수
        private float elapsed;
        private bool bFirstFrame;
        private bool bBeginPending;
        private bool bInitialized;
        private float overrideRange;
        private float currentRange;
        private int rangeRx;
        private int rangeRy;
        private float sizeScale;
        private int cloudCount;
        private int inhaleCount;
        private int centerX;
        private int centerY;
        private uint randomState;
        private Vector3 meshBoundsSize = new Vector3(14.0f, 10.0f, 10.0f);
        private int groundSortingLayerId;
        private PixelQuadBuffer activeBuffer;
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;
        private bool bRunActive;
        private int runX0;
        private int runX1;
        private int runY;
        private Color32 runColor;
        private int activeBounds;
        private readonly int[] boundsMinX = new int[2];
        private readonly int[] boundsMinY = new int[2];
        private readonly int[] boundsMaxX = new int[2];
        private readonly int[] boundsMaxY = new int[2];

        // 연막 세터 캐시: 마지막으로 쓴 값과 같으면 Transform/SpriteRenderer 세터를 부르지 않는다
        private readonly int[] puffLastX = new int[PuffTotal];
        private readonly int[] puffLastY = new int[PuffTotal];
        private readonly int[] puffLastFlip = new int[PuffTotal];
        private readonly Sprite[] puffLastSprite = new Sprite[PuffTotal];
        private readonly bool[] puffShown = new bool[PuffTotal];

        private int activePuffCount;
        private int activeSmokeCount;
        private Sprite[] activeSmokeFrames;
        private readonly int[] puffX = new int[PuffTotal];
        private readonly int[] puffY = new int[PuffTotal];
        private readonly float[] puffDelay = new float[PuffTotal];
        private readonly float[] puffRise = new float[PuffTotal];
        private readonly bool[] puffFlip = new bool[PuffTotal];
        private readonly bool[] puffActive = new bool[PuffTotal];
        private readonly int[] puffStartFrame = new int[PuffTotal];
        private readonly int[] puffRank = new int[PuffTotal];
        private int overrideTier;
        private int currentTier;
        private Sprite[] activePuffFrames;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 이번 재생의 타원 가로 반지름(월드 유닛)을 정한다. 풀에서 꺼내져 활성화된 직후(첫 LateUpdate 전)에 호출해야 한다.
        /// 0 이하이면 인스펙터의 rangeRadius를 쓴다.
        /// </summary>
        public void SetRange(float _radiusUnits)
        {
            overrideRange = _radiusUnits;
        }

        /// <summary>
        /// 이번 재생의 전리품 성장 단계(1 씨앗 ~ 3 나무)를 정한다. 단계에 따라 연막/알갱이/고리 색이 달라진다.
        /// 0 이하(기본)면 기존 연막과 같은 민트 색이다. 풀에서 꺼내져 활성화된 직후(첫 LateUpdate 전)에 호출해야 한다.
        /// </summary>
        public void SetTier(int _tier)
        {
            overrideTier = Mathf.Clamp(_tier, 0, 3);
        }

        /// <summary>이번 재생에서 실제로 쓰는 타원 가로 반지름(월드 유닛).</summary>
        public float CurrentRange => 0.0f < currentRange ? currentRange : rangeRadius;

        /// <summary>전체 재생 시간(초). 루트 ParticleSystem 수명은 이 값 이상이어야 한다.</summary>
        public static float TotalDuration => TimelineLength;

        /// <summary>폭발 순간(응집이 끝나는 시각)까지의 시간(초). 판정 타이밍을 이 시점에 맞춘다.</summary>
        public static float BurstDelay => PreTime;

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            mesh = new Mesh { name = "VFX_SporeBigExplosion" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            if (null != groundMeshFilter && null != groundMeshRenderer)
            {
                groundMesh = new Mesh { name = "VFX_SporeBigExplosion_Ground" };
                groundMesh.MarkDynamic();
                groundMeshFilter.sharedMesh = groundMesh;

                groundMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                groundMeshRenderer.receiveShadows = false;
                groundMeshRenderer.enabled = false;

                groundSortingLayerId = SortingLayer.NameToID(groundSortingLayerName);
            }

            EnsureOutlineTable();
            EnsurePalette();
        }

        private static void EnsurePalette()
        {
            if (true == bPaletteReady) return;
            bPaletteReady = true;

            for (int i = 0; i < tierPalette.Length; i++)
            {
                int b = i * 3;
                tierPalette[i] = VertexColor(TierColorBytes[b], TierColorBytes[b + 1], TierColorBytes[b + 2]);
            }
        }

        // 풀에서 꺼내져 활성화되면 범위가 정해질 시간을 주기 위해 시작을 첫 LateUpdate로 미룬다.
        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bFirstFrame = true;

            currentRange = 0.0f < overrideRange ? overrideRange : rangeRadius;
            currentRange = Mathf.Max(0.5f, currentRange);
            rangeRx = Mathf.Clamp(Mathf.RoundToInt(currentRange * PixelsPerUnit), 4, MaxOutlineRadius);
            rangeRy = Mathf.Max(1, Mathf.RoundToInt(rangeRx * GroundSquashY));
            sizeScale = rangeRx / 80.0f;

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            currentTier = Mathf.Clamp(overrideTier, 0, TierCount - 1);
            colorDeep = tierPalette[currentTier * 3];
            colorBase = tierPalette[currentTier * 3 + 1];
            colorLight = tierPalette[currentTier * 3 + 2];

            // 발광: 정점 알파에 실린 값이 셰이더에서 GlowMin~GlowMax 배율이 된다(짙은 톤은 0 = 발광 없음)
            colorBase.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(glowBase) * 255.0f);
            colorLight.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(glowLight) * 255.0f);
            colorHot = tierPalette[currentTier * 3 + 2];
            colorHot.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(glowHot) * 255.0f);

            Sprite[] tierFrames = 1 == currentTier ? puffFramesTier1 : (2 == currentTier ? puffFramesTier2 : (3 == currentTier ? puffFramesTier3 : puffFrames));
            activePuffFrames = (null != tierFrames && PuffFirstFrame < tierFrames.Length) ? tierFrames : puffFrames;

            float scale = Mathf.Max(0.1f, density);
            inhaleCount = Mathf.Clamp(Mathf.RoundToInt(BaseInhale * scale), 8, InhaleCapacity);

            SetupPuffLayout();
            for (int i = 0; i < PuffTotal; i++)
            {
                puffLastX[i] = int.MinValue;
                puffLastY[i] = int.MinValue;
                puffLastFlip[i] = -1;
                puffLastSprite[i] = null;
            }

            // 가장 많이 쓰는 반지름(범위 윤곽)을 미리 만들어 첫 재생의 히치를 줄인다
            EnsureOutline(rangeRx);

            // 메쉬 경계: 가로 도달 거리 + 상승 높이 + 연막 크기 여유
            float halfX = (rangeRx * 1.3f + 64.0f) / PixelsPerUnit;
            float halfY = (rangeRy * 1.3f + 110.0f * sizeScale + 64.0f) / PixelsPerUnit;
            meshBoundsSize = new Vector3(halfX * 2.0f, halfY * 2.0f, 10.0f);

            SetupInhale();
            SetupCloud(scale);
            SetupRays();
            SetupRingGaps();
        }

        // ---------- 난수 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        private float RandRange(float _min, float _max)
        {
            return _min + (_max - _min) * Rand01();
        }

        // 근사 정규분포(합 3개)
        private float RandGauss(float _sigma)
        {
            return (Rand01() + Rand01() + Rand01() - 1.5f) * 2.0f * _sigma;
        }

        // ---------- 준비 ----------

        // 연막/스모크 배치를 재생마다 무작위로 정한다(모든 대폭발이 같은 모양이 되지 않게). 닫힌 식은 그대로이고 값만 뽑는다.
        private void SetupPuffLayout()
        {
            for (int i = 0; i < PuffTotal; i++) puffActive[i] = false;

            int available = null != puffRenderers ? puffRenderers.Length : 0;

            // 밝은 연막 개수: 3장 이상, 최대 puffCount. 0.6 / 0.4 확률로 한 장씩 더한다.
            int maxBright = Mathf.Clamp(Mathf.Min(puffCount, Mathf.Min(PuffMax, available)), 0, PuffMax);
            int bright = Mathf.Min(maxBright, 3);
            if (bright < maxBright && Rand01() < 0.6f) bright++;
            if (bright < maxBright && Rand01() < 0.4f) bright++;
            if (null == activePuffFrames || activePuffFrames.Length <= PuffFirstFrame) bright = 0;
            activePuffCount = bright;

            // 배치 전체: 좌우 반전 + ±25도 비틀기
            bool bMirror = Rand01() < 0.5f;
            float twist = RandRange(-25.0f, 25.0f) * Mathf.Deg2Rad;
            float cosT = Mathf.Cos(twist);
            float sinT = Mathf.Sin(twist);
            float limitX = rangeRx * 0.55f;
            float limitY = rangeRy * 0.5f;

            for (int i = 0; i < bright; i++)
            {
                float bx = PuffBaseX[i] * sizeScale;
                float by = PuffBaseY[i] * sizeScale;
                if (true == bMirror) bx = -bx;

                float rx = bx * cosT - by * sinT + RandRange(-5.0f, 5.0f);
                float ry = bx * sinT + by * cosT + RandRange(-3.0f, 3.0f);

                puffX[i] = Mathf.RoundToInt(Mathf.Clamp(rx, -limitX, limitX));
                puffY[i] = Mathf.RoundToInt(Mathf.Clamp(ry, -limitY, limitY));
                puffDelay[i] = Mathf.Max(0.0f, PuffBaseDelay[i] + RandRange(-0.01f, 0.01f));
                puffRise[i] = PuffBaseRise[i] * RandRange(0.6f, 1.4f);
                puffFlip[i] = Rand01() < 0.5f;
                puffStartFrame[i] = PuffFirstFrame + (Rand01() < 0.3f ? Mathf.FloorToInt(RandRange(1.0f, 3.0f)) : 0);
                puffActive[i] = true;
            }

            // 스모크: 이음새 자리 중 무작위 서로 다른 곳
            switch (currentTier)
            {
                case 1: activeSmokeFrames = smokeFramesTier1; break;
                case 2: activeSmokeFrames = smokeFramesTier2; break;
                case 3: activeSmokeFrames = smokeFramesTier3; break;
                default: activeSmokeFrames = smokeFramesTier0; break;
            }

            int smokeAvailable = Mathf.Max(0, available - PuffMax);
            int smoke = Mathf.Clamp(Mathf.Min(smokeCount, Mathf.Min(SmokeMax, smokeAvailable)), 0, SmokeMax);
            if (null == activeSmokeFrames || activeSmokeFrames.Length <= SmokeFirstFrame) smoke = 0;
            activeSmokeCount = smoke;

            int firstSeam = Mathf.Clamp(Mathf.FloorToInt(Rand01() * SmokeSeamX.Length), 0, SmokeSeamX.Length - 1);
            for (int k = 0; k < smoke; k++)
            {
                int seam = (firstSeam + k * (1 + Mathf.FloorToInt(Rand01() * (SmokeSeamX.Length - 1)))) % SmokeSeamX.Length;
                int i = PuffMax + k;
                float bx = SmokeSeamX[seam] * sizeScale;
                float by = SmokeSeamY[seam] * sizeScale;
                if (true == bMirror) bx = -bx;

                puffX[i] = Mathf.RoundToInt(Mathf.Clamp(bx * cosT - by * sinT + RandRange(-3.0f, 3.0f), -limitX, limitX));
                puffY[i] = Mathf.RoundToInt(Mathf.Clamp(bx * sinT + by * cosT + RandRange(-2.0f, 2.0f), -limitY, limitY));
                puffDelay[i] = RandRange(0.012f, 0.035f);
                puffRise[i] = RandRange(4.0f, 12.0f);
                puffFlip[i] = Rand01() < 0.5f;
                puffStartFrame[i] = SmokeFirstFrame;
                puffActive[i] = true;
            }

            // 깊이 순서: 아래쪽(오프셋 y가 큰 것)일수록 앞. 밝은 연막과 스모크를 한 줄로 합쳐 사이사이에 끼운다.
            for (int i = 0; i < PuffTotal; i++)
            {
                int rank = 0;
                if (true == puffActive[i])
                {
                    for (int j = 0; j < PuffTotal; j++)
                    {
                        if (j == i || false == puffActive[j]) continue;
                        if (puffY[j] < puffY[i] || (puffY[j] == puffY[i] && j < i)) rank++;
                    }
                }

                puffRank[i] = rank;
            }
        }

        private void SetupInhale()
        {
            for (int i = 0; i < inhaleCount; i++)
            {
                float angle = RandRange(0.0f, TwoPi);
                float ratio = RandRange(0.35f, 1.05f);
                inhale[i].startX = Mathf.Cos(angle) * rangeRx * ratio;
                inhale[i].startY = Mathf.Sin(angle) * rangeRy * ratio + RandRange(0.0f, 22.0f) * sizeScale;
                inhale[i].delay = RandRange(0.0f, 0.06f);
                inhale[i].big = (byte)(Rand01() < 0.18f ? 1 : 0);
            }
        }

        private void SetupCloud(float _scale)
        {
            int groupParticles = Mathf.Max(8, Mathf.RoundToInt(BaseGroupParticles * _scale));
            int column = Mathf.RoundToInt(BaseColumn * _scale);
            int sparkle = Mathf.RoundToInt(BaseSparkle * _scale);
            int blast = Mathf.RoundToInt(BaseBlast * _scale);

            int index = 0;
            float baseAngle = RandRange(0.0f, TwoPi);

            for (int g = 0; g < CloudGroupCount; g++)
            {
                baseAngle += RandRange(0.5f, 1.1f);
                float groupReach = rangeRx * RandRange(0.35f, 0.95f);
                float groupZv = RandRange(35.0f, 120.0f);
                float lag = RandRange(0.0f, 0.05f);

                for (int i = 0; i < groupParticles && index < CloudCapacity; i++)
                {
                    float angle = baseAngle + RandGauss(0.28f);
                    float reach = Mathf.Max(5.0f, groupReach * (1.0f + RandGauss(0.16f)));
                    float zv = Mathf.Max(50.0f, groupZv * (1.0f + RandGauss(0.22f)));
                    FillCloud(index++, angle, reach, RandRange(5.5f, 8.5f), zv, RandRange(11.0f, 19.0f), lag + RandRange(0.0f, 0.025f), RandRange(0.45f, 0.95f), 0);
                }
            }

            // 중심에서 거의 수직으로 솟는 기둥
            for (int i = 0; i < column && index < CloudCapacity; i++)
            {
                FillCloud(index++, RandRange(0.0f, TwoPi), RandRange(1.2f, 8.0f), 5.0f, RandRange(70.0f, 150.0f), RandRange(11.0f, 18.0f),
                    RandRange(0.0f, 0.05f), RandRange(0.4f, 0.8f), 0);
            }

            // 반짝임(+ / x)
            for (int i = 0; i < sparkle && index < CloudCapacity; i++)
            {
                FillCloud(index++, RandRange(0.0f, TwoPi), rangeRx * RandRange(0.2f, 1.0f), 5.0f, RandRange(55.0f, 130.0f), RandRange(11.0f, 19.0f),
                    RandRange(0.0f, 0.08f), RandRange(0.3f, 0.7f), (byte)(Rand01() < 0.5f ? 1 : 2));
            }

            // 고속 층: 폭발 직후 0.1초 안에 범위 가장자리(최대 1.3배)까지 도달하는 짧은 수명의 알갱이
            for (int i = 0; i < blast && index < CloudCapacity; i++)
            {
                bool bFar = Rand01() < 0.45f;
                float reach = rangeRx * (true == bFar ? RandRange(0.95f, 1.3f) : RandRange(0.3f, 0.95f));
                FillCloud(index++, RandRange(0.0f, TwoPi), reach, 15.0f, RandRange(20.0f, 70.0f), RandRange(12.0f, 20.0f),
                    RandRange(0.0f, 0.012f), RandRange(0.2f, 0.42f), (byte)(Rand01() < 0.75f ? 0 : 1));
            }

            cloudCount = index;
        }

        private void FillCloud(int _index, float _angle, float _reach, float _drag, float _zv, float _z0, float _delay, float _life, byte _shape)
        {
            float gOverC = Gravity / VerticalDrag;

            cloud[_index].cosA = Mathf.Cos(_angle);
            cloud[_index].sinA = Mathf.Sin(_angle);
            cloud[_index].reach = _reach * 1.05f;
            cloud[_index].drag = _drag;
            cloud[_index].zv = _zv;
            cloud[_index].z0 = _z0;
            cloud[_index].delay = _delay;
            cloud[_index].life = _life;
            cloud[_index].phase = RandRange(0.0f, TwoPi);
            cloud[_index].apex = Mathf.Log((_zv + gOverC) / gOverC) / VerticalDrag;
            cloud[_index].shape = _shape;
            cloud[_index].trail = (byte)(0 == (_index % 3) ? 1 : 0);
            cloud[_index].swayAmp = RandRange(0.8f, 2.4f);
            cloud[_index].swayFreq = RandRange(7.0f, 15.0f);
            cloud[_index].drift = RandRange(10.0f, 40.0f);
            cloud[_index].killZ = RandRange(6.0f, 16.0f);
        }

        private void SetupRays()
        {
            for (int i = 0; i < RayCount; i++)
            {
                float angle = i * TwoPi / RayCount + RandRange(-0.12f, 0.12f);
                rays[i].cosA = Mathf.Cos(angle);
                rays[i].sinA = Mathf.Sin(angle);
                rays[i].length = RandRange(24.0f, 56.0f) * sizeScale;
                rays[i].delay = RandRange(0.0f, 0.025f);
            }
        }

        private void SetupRingGaps()
        {
            for (int ring = 0; ring < 2; ring++)
            {
                float angle = RandRange(0.0f, TwoPi);
                for (int k = 0; k < 5; k++)
                {
                    angle += RandRange(0.8f, 1.4f);
                    ringGapCenter[ring * 5 + k] = Mathf.Repeat(angle, TwoPi);
                    ringGapHalf[ring * 5 + k] = RandRange(0.12f, 0.25f);
                }
            }
        }

        // ---------- 닫힌 식 ----------

        private static float EaseOutCubic(float _x)
        {
            float inv = 1.0f - Mathf.Clamp01(_x);
            return 1.0f - inv * inv * inv;
        }

        private float CloudHeight(in CloudParticle _p, float _tt)
        {
            float gOverC = Gravity / VerticalDrag;
            float tUp = Mathf.Min(_tt, _p.apex);
            float z = _p.z0 + (_p.zv + gOverC) * (1.0f - Mathf.Exp(-VerticalDrag * tUp)) / VerticalDrag - gOverC * tUp;

            if (_p.apex < _tt)
            {
                // 정점 이후: 속도 0에서 시작해 1.45배 중력으로 떨어진다(닫힌 식)
                float dt = _tt - _p.apex;
                float g2 = Gravity * FallGravityMul / VerticalDrag;
                z -= g2 * (dt - (1.0f - Mathf.Exp(-VerticalDrag * dt)) / VerticalDrag);
            }

            return z * sizeScale;
        }

        // 정점 이후 낙하 속도(px/s, 양수)
        private float CloudFallSpeed(in CloudParticle _p, float _tt)
        {
            if (_tt <= _p.apex) return 0.0f;

            float dt = _tt - _p.apex;
            return Gravity * FallGravityMul / VerticalDrag * (1.0f - Mathf.Exp(-VerticalDrag * dt)) * sizeScale;
        }

        private void CloudPosition(in CloudParticle _p, float _tt, out float _x, out float _y)
        {
            float e = 1.0f - Mathf.Exp(-_p.drag * _tt);
            float dist = _p.reach * e;
            float sway = Mathf.Sin(_tt * _p.swayFreq + _p.phase) * _p.swayAmp * Mathf.Min(1.0f, _tt / 0.15f);

            if (_p.apex < _tt)
            {
                // 정점 이후에도 바깥으로 천천히 계속 흩날리고(입자마다 다른 속도), 흔들림이 커진다
                float dt = _tt - _p.apex;
                dist += 0.5f * _p.drift * dt * dt;
                sway *= 1.0f + 1.5f * Mathf.Min(1.0f, dt / FallSwayRamp);
            }

            dist *= sizeScale;
            _x = _p.cosA * dist + sway;
            _y = _p.sinA * dist * GroundSquashY;
        }

        // ---------- 그리기 도우미 ----------

        private void SetActiveBuffer(PixelQuadBuffer _buffer)
        {
            FlushRun();
            activeBuffer = _buffer;
            activeBounds = _buffer == skyBuffer ? 0 : 1;
        }

        // 같은 줄에서 같은 색으로 이어지는 픽셀을 한 쿼드로 합쳐 정점 수를 줄인다(결과 픽셀은 같다).
        private void FlushRun()
        {
            if (false == bRunActive) return;

            bRunActive = false;
            EmitRect(runX0, runY, runX1, runY + 1, runColor);
        }

        // 쿼드 한 개를 버퍼에 넣고 실제 그린 픽셀 범위(월드 픽셀)를 모은다. 메쉬 경계를 좁게 잡는 데 쓴다.
        private void EmitRect(int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            activeBuffer.AddWorldRect(_x0, _y0, _x1, _y1, _color);

            int b = activeBounds;
            if (_x0 < boundsMinX[b]) boundsMinX[b] = _x0;
            if (_y0 < boundsMinY[b]) boundsMinY[b] = _y0;
            if (boundsMaxX[b] < _x1) boundsMaxX[b] = _x1;
            if (boundsMaxY[b] < _y1) boundsMaxY[b] = _y1;
        }

        private static bool SameColor(Color32 _a, Color32 _b)
        {
            return _a.r == _b.r && _a.g == _b.g && _a.b == _b.b && _a.a == _b.a;
        }

        private void AddRun(int _x0, int _x1Exclusive, int _y, Color32 _color)
        {
            if (true == bRunActive && runY == _y && runX1 == _x0 && true == SameColor(runColor, _color))
            {
                runX1 = _x1Exclusive;
                return;
            }

            FlushRun();
            bRunActive = true;
            runX0 = _x0;
            runX1 = _x1Exclusive;
            runY = _y;
            runColor = _color;
        }

        private static Color32 VertexColor(byte _r, byte _g, byte _b)
        {
            // 정점 색은 셰이더가 그대로 출력하므로, 선형 색 공간에서는 sRGB 목표 색을 선형으로 바꿔 넣어야 화면에서 같은 색으로 보인다.
            Color color = new Color(_r / 255.0f, _g / 255.0f, _b / 255.0f, 1.0f);
            if (ColorSpace.Linear == QualitySettings.activeColorSpace) color = color.linear;
            Color32 result = (Color32)color;
            result.a = 0; // 정점 알파 0 = 발광 최소(머티리얼 GlowMin 1.0이면 색 그대로)
            return result;
        }

        private void Dot(int _x, int _y, Color32 _color)
        {
            AddRun(_x, _x + 1, _y, _color);
        }

        private void HLine(int _x0, int _x1, int _y, Color32 _color)
        {
            if (_x1 < _x0) return;
            AddRun(_x0, _x1 + 1, _y, _color);
        }

        private void VLine(int _x, int _y0, int _y1, Color32 _color)
        {
            if (_y1 < _y0) return;

            FlushRun();
            EmitRect(_x, _y0, _x + 1, _y1 + 1, _color);
        }

        private void DrawShape(int _x, int _y, int _shape, Color32 _color, Color32 _center)
        {
            if (0 == _shape)
            {
                Dot(_x, _y, _color);
            }
            else if (1 == _shape)
            {
                Dot(_x - 1, _y, _color);
                Dot(_x + 1, _y, _color);
                Dot(_x, _y - 1, _color);
                Dot(_x, _y + 1, _color);
                Dot(_x, _y, _center);
            }
            else
            {
                Dot(_x - 1, _y - 1, _color);
                Dot(_x + 1, _y + 1, _color);
                Dot(_x + 1, _y - 1, _color);
                Dot(_x - 1, _y + 1, _color);
                Dot(_x, _y, _center);
            }
        }

        // 채운 타원의 가로 반쪽 폭. 타원 판정은 (x/(rx+0.5))^2 + (y/(ry+0.5))^2 <= 1 이다. 바깥이면 -1.
        private static int FillHalfWidth(int _rx, int _ry, int _y)
        {
            float ny = _y / (_ry + 0.5f);
            float rest = 1.0f - ny * ny;
            if (rest < 0.0f) return -1;
            return Mathf.FloorToInt((_rx + 0.5f) * Mathf.Sqrt(rest));
        }

        private void DrawFillEllipse(int _cx, int _cy, int _rx, int _ry, Color32 _color)
        {
            for (int y = -_ry; y <= _ry; y++)
            {
                int hw = FillHalfWidth(_rx, _ry, y);
                if (hw < 0) continue;
                HLine(_cx - hw, _cx + hw, _cy + y, _color);
            }
        }

        // 두께 _w의 고리를 점 단위로 그리며 끊김 구간(각도)을 건너뛴다. 바깥 채운 타원에서 안쪽 채운 타원을 뺀 판정이라
        // 가장자리는 midpoint 곡선과 같다.
        private void DrawRingBandGapped(int _rx, int _ry, int _w, Color32 _color, int _ring)
        {
            int innerRx = Mathf.Max(0, _rx - _w);
            int innerRy = Mathf.Max(0, Mathf.RoundToInt(_ry - _w * (float)_ry / Mathf.Max(1, _rx)));

            for (int y = -_ry; y <= _ry; y++)
            {
                int wo = FillHalfWidth(_rx, _ry, y);
                if (wo < 0) continue;

                int wi = -1;
                if (-innerRy <= y && y <= innerRy && 0 < innerRx) wi = FillHalfWidth(innerRx, innerRy, y);

                for (int x = -wo; x <= wo; x++)
                {
                    // 안쪽 타원 구간은 한 번에 건너뛴다(-wi에서 wi로 점프, 결과는 같다)
                    if (0 <= wi && x == -wi)
                    {
                        x = wi;
                        continue;
                    }

                    float angle = Mathf.Repeat(Mathf.Atan2(y / (float)Mathf.Max(1, _ry), x / (float)Mathf.Max(1, _rx)), TwoPi);
                    if (true == InRingGap(_ring, angle)) continue;

                    Dot(centerX + x, centerY + y, _color);
                }
            }
        }

        private bool InRingGap(int _ring, float _angle)
        {
            for (int k = 0; k < 5; k++)
            {
                float delta = Mathf.Abs(Mathf.Repeat(_angle - ringGapCenter[_ring * 5 + k] + Mathf.PI, TwoPi) - Mathf.PI);
                if (delta < ringGapHalf[_ring * 5 + k]) return true;
            }

            return false;
        }

        // 정수 격자 직선(Bresenham) + L자 모서리 제거. 결과 픽셀 수를 돌려준다(버퍼를 넘으면 0).
        private static int BuildLine(int _x0, int _y0, int _x1, int _y1)
        {
            int dx = Mathf.Abs(_x1 - _x0);
            int dy = Mathf.Abs(_y1 - _y0);
            int sx = _x0 < _x1 ? 1 : -1;
            int sy = _y0 < _y1 ? 1 : -1;
            int err = dx - dy;
            int x = _x0;
            int y = _y0;
            int n = 0;

            while (true)
            {
                if (n >= LineBufferSize) return 0;
                linePixelX[n] = x;
                linePixelY[n] = y;
                n++;

                if (x == _x1 && y == _y1) break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

            // 앞뒤 픽셀이 이미 대각으로 이어져 있으면 가운데(L자 모서리)를 제거한다
            int w = 0;
            for (int i = 0; i < n; i++)
            {
                if (0 < i && i < n - 1 && w > 0)
                {
                    int px = linePixelX[w - 1];
                    int py = linePixelY[w - 1];
                    int nx = linePixelX[i + 1];
                    int ny = linePixelY[i + 1];
                    if (Mathf.Abs(nx - px) <= 1 && Mathf.Abs(ny - py) <= 1) continue;
                }

                linePixelX[w] = linePixelX[i];
                linePixelY[w] = linePixelY[i];
                w++;
            }

            return w;
        }

        // ---------- 윤곽 표 (midpoint 타원 + L자 제거) ----------

        // 표 껍데기만 만든다. 반지름별 윤곽은 처음 쓰일 때 만들어 재사용한다(전부 미리 만들면 첫 재생에서 큰 할당과 히치가 난다).
        private static void EnsureOutlineTable()
        {
            if (null != outlineX) return;

            outlineX = new int[MaxOutlineRadius + 1][];
            outlineY = new int[MaxOutlineRadius + 1][];
            outlineAngle = new float[MaxOutlineRadius + 1][];
            outlineScratch = new bool[MaxOutlineRadius * 2 + 3, MaxOutlineRadius + 3];
        }

        private static void EnsureOutline(int _rx)
        {
            if (null != outlineX[_rx]) return;

            int ry = Mathf.Max(1, Mathf.RoundToInt(_rx * GroundSquashY));
            BuildOutline(_rx, ry, out outlineX[_rx], out outlineY[_rx], out outlineAngle[_rx]);
        }

        private static void BuildOutline(int _rx, int _ry, out int[] _xs, out int[] _ys, out float[] _angles)
        {
            int w = _rx * 2 + 3;
            int h = _ry * 2 + 3;
            bool[,] grid = outlineScratch;
            for (int cx = 0; cx < w; cx++)
            {
                for (int cy = 0; cy < h; cy++) grid[cx, cy] = false;
            }

            int ox = _rx + 1;
            int oy = _ry + 1;

            int x = 0;
            int y = _ry;
            long rx2 = (long)_rx * _rx;
            long ry2 = (long)_ry * _ry;
            double d1 = ry2 - rx2 * _ry + 0.25 * rx2;
            long dx = 2 * ry2 * x;
            long dy = 2 * rx2 * y;

            while (dx < dy)
            {
                Plot4(grid, ox, oy, x, y);
                if (d1 < 0)
                {
                    x++;
                    dx += 2 * ry2;
                    d1 += dx + ry2;
                }
                else
                {
                    x++;
                    y--;
                    dx += 2 * ry2;
                    dy -= 2 * rx2;
                    d1 += dx - dy + ry2;
                }
            }

            double d2 = ry2 * (x + 0.5) * (x + 0.5) + rx2 * (y - 1.0) * (y - 1.0) - rx2 * ry2;
            while (y >= 0)
            {
                Plot4(grid, ox, oy, x, y);
                if (d2 > 0)
                {
                    y--;
                    dy -= 2 * rx2;
                    d2 += rx2 - dy;
                }
                else
                {
                    y--;
                    x++;
                    dx += 2 * ry2;
                    dy -= 2 * rx2;
                    d2 += dx - dy + rx2;
                }
            }

            // L자 모서리 제거(대각 연결은 유지)
            bool bChanged = true;
            while (true == bChanged)
            {
                bChanged = false;
                for (int gx = 1; gx < w - 1; gx++)
                {
                    for (int gy = 1; gy < h - 1; gy++)
                    {
                        if (false == grid[gx, gy]) continue;

                        for (int sx = -1; sx <= 1; sx += 2)
                        {
                            for (int sy = -1; sy <= 1; sy += 2)
                            {
                                if (true == grid[gx + sx, gy] && true == grid[gx, gy + sy] && false == grid[gx + sx, gy + sy])
                                {
                                    grid[gx, gy] = false;
                                    bChanged = true;
                                    sx = 2;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            int count = 0;
            for (int gx = 0; gx < w; gx++)
            {
                for (int gy = 0; gy < h; gy++)
                {
                    if (true == grid[gx, gy]) count++;
                }
            }

            _xs = new int[count];
            _ys = new int[count];
            _angles = new float[count];
            int n = 0;
            for (int gx = 0; gx < w; gx++)
            {
                for (int gy = 0; gy < h; gy++)
                {
                    if (false == grid[gx, gy]) continue;
                    _xs[n] = gx - ox;
                    _ys[n] = gy - oy;
                    _angles[n] = Mathf.Repeat(Mathf.Atan2((gy - oy) / (float)_ry, (gx - ox) / (float)_rx), TwoPi);
                    n++;
                }
            }

            // 각도 순으로 정렬(삽입 정렬, 최초 1회)
            for (int i = 1; i < count; i++)
            {
                float a = _angles[i];
                int px = _xs[i];
                int py = _ys[i];
                int j = i - 1;
                while (j >= 0 && _angles[j] > a)
                {
                    _angles[j + 1] = _angles[j];
                    _xs[j + 1] = _xs[j];
                    _ys[j + 1] = _ys[j];
                    j--;
                }

                _angles[j + 1] = a;
                _xs[j + 1] = px;
                _ys[j + 1] = py;
            }
        }

        private static void Plot4(bool[,] _grid, int _ox, int _oy, int _x, int _y)
        {
            _grid[_ox + _x, _oy + _y] = true;
            _grid[_ox - _x, _oy + _y] = true;
            _grid[_ox + _x, _oy - _y] = true;
            _grid[_ox - _x, _oy - _y] = true;
        }

        // ---------- 한 프레임 ----------

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            bool bSplit = null != groundMesh;

            skyBuffer.Clear();
            skyBuffer.SetWorldTransform(transform.worldToLocalMatrix, pos.z);

            bRunActive = false;
            for (int b = 0; b < 2; b++)
            {
                boundsMinX[b] = int.MaxValue;
                boundsMinY[b] = int.MaxValue;
                boundsMaxX[b] = int.MinValue;
                boundsMaxY[b] = int.MinValue;
            }

            PixelQuadBuffer groundTarget = skyBuffer;
            if (true == bSplit)
            {
                groundBuffer.Clear();
                groundBuffer.SetWorldTransform(groundMeshRenderer.transform.worldToLocalMatrix, pos.z);
                groundTarget = groundBuffer;
            }

            centerX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            centerY = Mathf.FloorToInt(pos.y * PixelsPerUnit);

            float t = elapsed - PreTime; // 폭발 시각 0 기준

            SetActiveBuffer(groundTarget);
            if (0.0f <= t)
            {
                DrawRangeOutlineFlash(t);
                DrawRings(t);
            }

            SetActiveBuffer(skyBuffer);
            if (t < 0.0f)
            {
                DrawInhale();
                DrawChargeCore();
            }
            else
            {
                DrawRays(t);
                DrawStar(t);
                DrawCloud(t);
            }

            FlushRun();

            // 메쉬 경계는 실제로 그린 픽셀 범위로 좁게 잡는다(GPU 프러스텀 컬링이 효과를 내게 한다)
            skyBuffer.Upload(mesh, 0);
            ApplyTightBounds(mesh, transform.position, 0);
            meshRenderer.enabled = true;

            if (true == bSplit)
            {
                groundBuffer.Upload(groundMesh, 0);
                ApplyTightBounds(groundMesh, groundMeshRenderer.transform.position, 1);
                groundMeshRenderer.enabled = true;
            }

            UpdatePuffs(t);
        }

        // 이번 프레임에 그린 월드 픽셀 범위를 메쉬 로컬 경계로 바꿔 적용한다(값이 바뀔 때만 쓴다).
        private void ApplyTightBounds(Mesh _mesh, Vector3 _objectPosition, int _index)
        {
            Bounds bounds;

            if (boundsMaxX[_index] < boundsMinX[_index])
            {
                bounds = new Bounds(Vector3.zero, new Vector3(0.1f, 0.1f, PixelVfxCulling.BoundsDepth));
            }
            else
            {
                float minX = boundsMinX[_index] / PixelsPerUnit - _objectPosition.x;
                float minY = boundsMinY[_index] / PixelsPerUnit - _objectPosition.y;
                float maxX = boundsMaxX[_index] / PixelsPerUnit - _objectPosition.x;
                float maxY = boundsMaxY[_index] / PixelsPerUnit - _objectPosition.y;

                bounds = new Bounds(
                    new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0.0f),
                    new Vector3(maxX - minX + 0.1f, maxY - minY + 0.1f, PixelVfxCulling.BoundsDepth));
            }

            Bounds current = _mesh.bounds;
            if (current.center != bounds.center || current.size != bounds.size) _mesh.bounds = bounds;
        }

        // 응집: 범위 안의 알갱이가 한 점으로 빨려 들어간다(뒤에 짧은 속도선)
        private void DrawInhale()
        {
            Color32 deep = colorDeep;
            Color32 baseColor = colorBase;
            Color32 light = colorLight;
            float coreOffsetY = 6.0f * sizeScale;

            for (int i = 0; i < inhaleCount; i++)
            {
                float pr = Mathf.Clamp01((elapsed - inhale[i].delay) / InhaleTravelTime);
                float e = pr * pr * pr;
                int px = centerX + Mathf.RoundToInt(inhale[i].startX * (1.0f - e));
                int py = centerY + Mathf.RoundToInt(inhale[i].startY * (1.0f - e) + coreOffsetY * e);
                Color32 color = e > 0.75f ? light : (e > 0.3f ? baseColor : deep);

                // 속도선
                if (0.12f < pr)
                {
                    float pr0 = Mathf.Max(0.0f, pr - InhaleTrailBack);
                    float e0 = pr0 * pr0 * pr0;
                    int bx = centerX + Mathf.RoundToInt(inhale[i].startX * (1.0f - e0));
                    int by = centerY + Mathf.RoundToInt(inhale[i].startY * (1.0f - e0) + coreOffsetY * e0);
                    int n = BuildLine(bx, by, px, py);
                    if (0 < n && n <= InhaleTrailMaxPixels)
                    {
                        Color32 trail = e > 0.3f ? baseColor : deep;
                        for (int k = 0; k < n; k++) Dot(linePixelX[k], linePixelY[k], trail);
                    }
                }

                Dot(px, py, color);
                if (0 != inhale[i].big)
                {
                    Dot(px + 1, py, color);
                    Dot(px - 1, py, color);
                    Dot(px, py + 1, color);
                    Dot(px, py - 1, color);
                }
            }
        }

        // 압축 핵: 압축될수록 작고 밝아진다(마지막 구간은 1px씩 떨림)
        private void DrawChargeCore()
        {
            float cp = elapsed / PreTime;
            if (cp <= 0.4f) return;

            int rc = cp < 0.6f ? 3 : (cp < 0.75f ? 5 : (cp < 0.9f ? 4 : 3));
            int jitter = cp < 0.9f ? 0 : (0 != (Mathf.FloorToInt(elapsed * 120.0f) & 1) ? 1 : -1);
            int cy = centerY + Mathf.RoundToInt(6.0f * sizeScale);

            DrawFillEllipse(centerX + jitter, cy, rc, rc, colorHot);
            if (0.75f < cp) DrawRingOutline(centerX + jitter, cy, rc + 2, colorBase);
        }

        // 원형 1px 윤곽(채운 원 - 안쪽 채운 원)
        private void DrawRingOutline(int _cx, int _cy, int _r, Color32 _color)
        {
            for (int y = -_r; y <= _r; y++)
            {
                int wo = FillHalfWidth(_r, _r, y);
                if (wo < 0) continue;

                int wi = -1;
                if (-(_r - 1) <= y && y <= _r - 1) wi = FillHalfWidth(_r - 1, _r - 1, y);

                if (wi < 0)
                {
                    HLine(_cx - wo, _cx + wo, _cy + y, _color);
                }
                else
                {
                    HLine(_cx - wo, _cx - wi - 1, _cy + y, _color);
                    HLine(_cx + wi + 1, _cx + wo, _cy + y, _color);
                }
            }
        }

        // 범위 윤곽: 폭발 직후 얇은 1px 선이 잠깐만 나타나 범위를 알린다(면으로 칠하는 장판 번쩍임은 쓰지 않는다)
        private void DrawRangeOutlineFlash(float _t)
        {
            if (0.05f <= _t) return;
            DrawOutline(rangeRx, _t < 0.03f ? colorLight : colorBase, 1, 0, false);
        }

        // 윤곽을 그린다. _step 간격으로 점선, _bGaps가 true면 _ring번 고리의 끊김 구간을 건너뛴다.
        private void DrawOutline(int _rx, Color32 _color, int _step, int _ring, bool _bGaps)
        {
            _rx = Mathf.Clamp(_rx, 2, MaxOutlineRadius);
            EnsureOutline(_rx);
            int[] xs = outlineX[_rx];
            int[] ys = outlineY[_rx];
            float[] angles = outlineAngle[_rx];

            for (int i = 0; i < xs.Length; i += _step)
            {
                if (true == _bGaps && true == InRingGap(_ring, angles[i])) continue;
                Dot(centerX + xs[i], centerY + ys[i], _color);
            }
        }

        // 충격파 고리 두 겹: 첫 번째는 2px 두께, 두 번째는 1px. 끊김은 각도 구간으로 깔끔히 자르고 범위 윤곽은 점선으로 남긴다.
        private void DrawRings(float _t)
        {
            for (int n = 0; n < 2; n++)
            {
                float delay = 0 == n ? 0.02f : 0.09f;
                float pr = (_t - delay) / RingTravelTime;
                if (pr <= 0.0f) continue;

                Color32 color = 0 == n ? colorLight : colorBase;

                if (pr < 1.0f)
                {
                    int rx = Mathf.Max(2, Mathf.RoundToInt(rangeRx * EaseOutCubic(pr)));
                    if (0 == n && 4 < rx)
                    {
                        int ry = Mathf.Max(1, Mathf.RoundToInt(rx * GroundSquashY));
                        DrawRingBandGapped(rx, ry, 2, color, 0);
                    }
                    else
                    {
                        DrawOutline(rx, color, 1, n, true);
                    }
                }
                else if (0 == n && _t < RingHoldEnd)
                {
                    // 범위 윤곽: 3픽셀 간격 점선 + 12칸마다 위로 3px 결정 점, 깜빡임 없이 색 단계만 낮춘다
                    Color32 hold = _t < 0.5f ? colorBase : colorDeep;
                    EnsureOutline(rangeRx);
                    int[] xs = outlineX[rangeRx];
                    int[] ys = outlineY[rangeRx];
                    for (int i = 0; i < xs.Length; i += 3)
                    {
                        if (0 != (Mathf.FloorToInt(_t * 18.0f) & 1) && 0 != ((i / 3) & 1)) continue;
                        Dot(centerX + xs[i], centerY + ys[i], hold);

                        if (0 == (i % 12) && _t < 0.55f)
                        {
                            Dot(centerX + xs[i], centerY + ys[i] + 1, colorBase);
                            Dot(centerX + xs[i], centerY + ys[i] + 2, colorBase);
                            Dot(centerX + xs[i], centerY + ys[i] + 3, colorLight);
                        }
                    }
                }
            }
        }

        // 방사 점선: 중심에서 바깥으로 뻗는 1px 직선(처음 0.06초는 2px)
        private void DrawRays(float _t)
        {
            if (RayEnd <= _t) return;

            int cy = centerY + Mathf.RoundToInt(6.0f * sizeScale);

            for (int i = 0; i < RayCount; i++)
            {
                float age = _t - rays[i].delay;
                if (age < 0.0f) continue;

                float e = EaseOutCubic(age / RayGrowTime);
                float head = rays[i].length * e;
                float tail = rays[i].length * Mathf.Max(0.0f, (age - 0.06f) / 0.16f);

                int x1 = centerX + Mathf.RoundToInt(rays[i].cosA * head);
                int y1 = cy + Mathf.RoundToInt(rays[i].sinA * head * GroundSquashY);
                int x0 = centerX + Mathf.RoundToInt(rays[i].cosA * tail);
                int y0 = cy + Mathf.RoundToInt(rays[i].sinA * tail * GroundSquashY);

                int n = BuildLine(x0, y0, x1, y1);
                if (n <= 0) continue;

                bool bHorizontal = Mathf.Abs(rays[i].cosA) > Mathf.Abs(rays[i].sinA * GroundSquashY);
                for (int k = 0; k < n; k++)
                {
                    bool bHead = k > n * 0.7f;
                    Dot(linePixelX[k], linePixelY[k], true == bHead ? colorHot : colorBase);

                    if (age < 0.06f && k > n * 0.25f)
                    {
                        Color32 thick = k > n * 0.8f ? colorHot : colorBase;
                        if (true == bHorizontal) Dot(linePixelX[k], linePixelY[k] + 1, thick);
                        else Dot(linePixelX[k] + 1, linePixelY[k], thick);
                    }
                }
            }
        }

        // 중심 십자 별: 수평/수직 직선과 원판만 쓴다(계단 없음)
        private void DrawStar(float _t)
        {
            if (0.1f <= _t) return;

            int frame = Mathf.Min(5, Mathf.Max(0, Mathf.FloorToInt((_t - 0.0167f) / 0.0167f)));
            int cy = centerY + Mathf.RoundToInt(6.0f * sizeScale);
            Color32 light = colorLight;
            Color32 baseColor = colorBase;

            Color32 hot = colorHot;

            DrawStarArm(centerX + 1, centerX + StarLenRight[frame] - 1, cy, hot, baseColor);
            DrawStarArm(centerX - StarLenLeft[frame] + 1, centerX - 1, cy, hot, baseColor);
            DrawStarArmVertical(centerX, cy + 1, cy + StarLenUp[frame] - 1, hot, baseColor);
            DrawStarArmVertical(centerX, cy - StarLenDown[frame] + 1, cy - 1, hot, baseColor);

            int rc = StarCoreRadius[frame];
            DrawFillEllipse(centerX, cy, rc, rc, hot);
        }

        private void DrawStarArm(int _x0, int _x1, int _y, Color32 _near, Color32 _far)
        {
            if (_x1 < _x0) return;
            int len = _x1 - _x0 + 1;
            int split = _x0 + Mathf.RoundToInt(len * 0.6f);
            HLine(_x0, Mathf.Min(_x1, split - 1), _y, _near);
            HLine(Mathf.Max(_x0, split), _x1, _y, _far);
        }

        private void DrawStarArmVertical(int _x, int _y0, int _y1, Color32 _near, Color32 _far)
        {
            if (_y1 < _y0) return;
            int len = _y1 - _y0 + 1;
            int split = _y0 + Mathf.RoundToInt(len * 0.6f);
            VLine(_x, _y0, Mathf.Min(_y1, split - 1), _near);
            VLine(_x, Mathf.Max(_y0, split), _y1, _far);
        }

        // 구름/고속 알갱이: 바닥에 닿기 전에 낙하 중 공중에서 분해되어 사라진다
        private void DrawCloud(float _t)
        {
            Color32 deep = colorDeep;
            Color32 baseColor = colorBase;
            Color32 light = colorLight;

            float brightHeight = BrightHeightPx * sizeScale;

            for (int i = 0; i < cloudCount; i++)
            {
                float tt = _t - cloud[i].delay;
                if (tt < 0.0f || cloud[i].life < tt) continue;

                float z = CloudHeight(in cloud[i], tt);
                bool bDescending = cloud[i].apex < tt;

                float killHeight = cloud[i].killZ * sizeScale;
                if (true == bDescending && z < killHeight) continue;

                float life = tt / cloud[i].life;
                Color32 color;
                if (true == bDescending && z < killHeight + DarkenMarginPx * sizeScale) color = deep;
                else if (z > brightHeight && life < 0.5f) color = light;
                else if (life < 0.8f) color = baseColor;
                else color = deep;

                CloudPosition(in cloud[i], tt, out float gx, out float gy);
                int x = centerX + Mathf.RoundToInt(gx);
                int y = centerY + Mathf.RoundToInt(gy + z);

                // 초반 속도선: 3개 중 1개만, 짧은 직선(최대 8픽셀)
                if (_t < TrailTime && 0 != cloud[i].trail)
                {
                    float back = Mathf.Max(0.0f, tt - TrailBackTime);
                    CloudPosition(in cloud[i], back, out float bx, out float by);
                    float bz = CloudHeight(in cloud[i], back);
                    int n = BuildLine(centerX + Mathf.RoundToInt(bx), centerY + Mathf.RoundToInt(by + bz), x, y);
                    if (0 < n && n <= MaxTrailPixels)
                    {
                        for (int k = 0; k < n; k++)
                        {
                            if (linePixelX[k] == x && linePixelY[k] == y) continue;
                            Dot(linePixelX[k], linePixelY[k], baseColor);
                        }
                    }
                }

                // 낙하 꼬리: 하강이 빨라질수록 위로 1~3px(속도가 낮으면 없음)
                if (true == bDescending)
                {
                    int tail = Mathf.Min(3, Mathf.FloorToInt(CloudFallSpeed(in cloud[i], tt) / 70.0f));
                    for (int k = 1; k <= tail; k++) Dot(x, y + k, 1 == k ? baseColor : deep);
                }

                DrawShape(x, y, cloud[i].shape, color, light);
            }
        }

        // 연막: 기존 포자 폭발 스프라이트 프레임을 22fps로, 스모크는 24fps로 재생한다. 확대/회전 없이 정수 픽셀 격자에 맞춘다.
        // 배치/지연/상승/반전은 Begin의 SetupPuffLayout이 재생마다 무작위로 정해 둔 값이다.
        private void UpdatePuffs(float _t)
        {
            if (null == puffRenderers) return;

            float z = transform.position.z;

            for (int i = 0; i < puffRenderers.Length; i++)
            {
                SpriteRenderer sr = puffRenderers[i];
                if (null == sr) continue;

                if (i >= PuffTotal || false == puffActive[i])
                {
                    SetPuffShown(i, sr, false);
                    continue;
                }

                bool bSmoke = PuffMax <= i;
                Sprite[] frames = true == bSmoke ? activeSmokeFrames : activePuffFrames;
                float rate = true == bSmoke ? SmokeFrameRate : PuffFrameRate;

                float age = _t - puffDelay[i];
                int frame = puffStartFrame[i] + Mathf.FloorToInt(age * rate);
                if (age < 0.0f || null == frames || frames.Length <= frame || PuffFrameCount <= frame)
                {
                    SetPuffShown(i, sr, false);
                    continue;
                }

                float rise = puffRise[i] * Mathf.Min(1.0f, age / (true == bSmoke ? 0.5f : 0.4f));
                int px = centerX + puffX[i];
                int py = centerY + Mathf.RoundToInt(puffHeightPx * sizeScale) - puffY[i] + Mathf.RoundToInt(rise);

                if (px != puffLastX[i] || py != puffLastY[i])
                {
                    puffLastX[i] = px;
                    puffLastY[i] = py;
                    sr.transform.position = new Vector3(px / PixelsPerUnit, py / PixelsPerUnit, z);
                }

                int flip = true == puffFlip[i] ? 1 : 0;
                if (flip != puffLastFlip[i])
                {
                    puffLastFlip[i] = flip;
                    sr.flipX = puffFlip[i];
                }

                Sprite sprite = frames[frame];
                if (sprite != puffLastSprite[i])
                {
                    puffLastSprite[i] = sprite;
                    sr.sprite = sprite;
                }

                SetPuffShown(i, sr, true);
            }
        }

        private void SetPuffShown(int _index, SpriteRenderer _renderer, bool _bShown)
        {
            if (_index < PuffTotal)
            {
                if (_bShown == puffShown[_index]) return;
                puffShown[_index] = _bShown;
            }

            _renderer.enabled = _bShown;
        }

        private void HidePuffs()
        {
            if (null == puffRenderers) return;

            for (int i = 0; i < puffRenderers.Length; i++)
            {
                if (null == puffRenderers[i]) continue;

                if (i < PuffTotal)
                {
                    if (false == puffShown[i] && false == puffRenderers[i].enabled) continue;
                    puffShown[i] = false;
                }

                puffRenderers[i].enabled = false;
            }
        }

        private void ApplyPuffSorting(int _layerId, int _order)
        {
            if (null == puffRenderers) return;

            for (int i = 0; i < puffRenderers.Length; i++)
            {
                if (null == puffRenderers[i]) continue;
                puffRenderers[i].sortingLayerID = _layerId;
                // 밝은 연막과 스모크의 깊이 순서(랭크)를 반영한다. 알갱이 메쉬(_order + 1)보다 항상 아래.
                int rank = i < PuffTotal ? puffRank[i] : 0;
                puffRenderers[i].sortingOrder = _order - PuffTotal + rank;
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
            HidePuffs();
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
                HidePuffs();
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
                    // 연막은 알갱이 메쉬보다 한 칸 아래에 그려 알갱이가 연막 위에 보이게 한다
                    ApplyPuffSorting(rootLayer, rootOrder + sortingOrderOffset - 1);
                }
            }

            if (null != groundMesh)
            {
                if (groundMeshRenderer.sortingLayerID != groundSortingLayerId) groundMeshRenderer.sortingLayerID = groundSortingLayerId;
                if (groundMeshRenderer.sortingOrder != groundSortingOrder) groundMeshRenderer.sortingOrder = groundSortingOrder;
            }

            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            Vector3 cullingSize = new Vector3(meshBoundsSize.x + margin, meshBoundsSize.y + margin, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(transform.position, cullingSize))
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                if (null != groundMeshRenderer && true == groundMeshRenderer.enabled) groundMeshRenderer.enabled = false;
                HidePuffs();
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            overrideRange = 0.0f;
            overrideTier = 0;

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
            HidePuffs();
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
            rangeRadius = Mathf.Max(0.5f, rangeRadius);
            density = Mathf.Clamp(density, 0.25f, 2.0f);
            puffCount = Mathf.Clamp(puffCount, 0, PuffMax);
            smokeCount = Mathf.Clamp(smokeCount, 0, SmokeMax);
        }

        // 씬 뷰에서 타원 범위를 보여 줘서 피격 범위와 눈으로 맞출 수 있게 한다.
        private void OnDrawGizmosSelected()
        {
            float radius = true == Application.isPlaying && 0.0f < currentRange ? currentRange : rangeRadius;
            Vector3 center = transform.position;

            Gizmos.color = new Color(0.62f, 0.89f, 0.84f, 0.9f);

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
