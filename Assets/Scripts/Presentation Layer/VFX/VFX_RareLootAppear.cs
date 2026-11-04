using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 희귀 전리품(LootItem) 등장 이펙트. 승인된 시뮬레이션 시안 G3를 그대로 옮겼다.
    ///  ① 등장(0~1.2초): 아이템 백열 섬광, 큰 오라, 가로/세로 렌즈 줄, 계속 돌며 긴 빛살이 길어졌다 짧아지는 4갈래 별빛,
    ///     아이템 둘레 5곳에서 차례로 회전하며 커졌다 작아지는 주변 별, 가장자리 반짝임 입자, 선택 요소 A 섬광 원반(0.08초)·C 파편 분사(0.25초).
    ///     오라·렌즈 줄·별빛·백열은 0 / 0.18 / 0.36초에 세 번 번쩍이는 리듬을 타고, 전체는 0.15초 최대 밝기 유지 후 줄어든다.
    ///     빛기둥은 이 순간부터 켜져 밝고 굵게 솟았다가 유지 세기로 내려온다. 아이템 아우라(반지름 약 47px)와 아우라 심도 이때부터 켜진다.
    ///  ② 비행: 빛기둥은 아이템 중심에서 위로 솟고(아래 끝은 둥근 밑동으로 부드럽게 사라진다) 아이템과 같이 움직인다.
    ///     아이템은 큰 아우라 안에서 빛을 내고 그 위로 빛기둥이 이어진다. 빛 꼬리(부드러운 빛 + 1px 심선)가 뒤따르고,
    ///     빛기둥을 타고 `◆` `●` `+` `x` `.` 입자가 올라간다
    ///  ③ 착지: 짧은 섬광(0.1초 유지)과 바닥 렌즈 줄, 빛 웅덩이가 켜진다
    ///  색은 모든 전리품이 같은 황금빛(인스펙터 goldCoreColor / goldMainColor / goldDeepColor)이다.
    ///  ④ 유지: 빛기둥·오라가 숨 쉬듯 일렁이고, 가장자리 반짝임과 기둥을 타는 입자·알갱이가 이어진다. 흡입이 시작되면 빛기둥·입자·둘레 빛·웅덩이는 0.15초에 걸쳐 꺼지고, 원형 아우라(오라·아우라 심)만 아이템을 따라가다 획득 때 사라진다
    ///
    /// 연결: LootItem 프리팹의 VFX(vfxRoot) 자식이다. LootItem.Launch -> PlayVFX가 vfxRoot를 켜는 순간이 등장 시점이고,
    /// 비행 높이는 vfxRoot.localPosition.y(LootItem이 비주얼 높이와 맞춘다)로 읽어 착지를 판단한다. 흡입은 루트의 이동/축소로 판단한다.
    /// Application Layer 코드는 건드리지 않는다. 아이템 아웃라인 색은 outlineRenderer의 MaterialPropertyBlock(_OutlineColor)으로 바꾸고,
    /// LootItem.ResetItem이 블록을 비워 원래 색으로 돌린다.
    ///
    /// 그리는 방식
    /// - 부드러운 빛(오라, 빛기둥, 빛 웅덩이, 렌즈 줄, 별빛, 꼬리 빛, 둘레 빛): 빌더가 구운 그라데이션 텍스처 스프라이트. 바깥은 반투명 덮기(SpriteGlowTint),
    ///   심은 가산(SpriteGlowAdditive). 바깥을 반투명으로 덮어 밝은 바닥에서도 색이 바뀌지 않게 한다.
    ///   둘레 빛은 아이템마다 실루엣을 블러로 번지게 구운 스프라이트라 계단 없이 아이템 모양을 따라 번진다.
    /// - 정수 픽셀(PixelQuadBuffer): 꼬리 심선(가산, 아이템 뒤), 백열 섬광(가산, 아이템 앞),
    ///   기둥을 타는 입자·반짝임·파편·유지 알갱이(BrandWrapGlow, 맨 앞).
    ///
    /// 위치 기준(지터 방지): 카메라가 픽셀 그리드에 스냅하지 않아 아이템 스프라이트는 소수 위치로 부드럽게 움직인다.
    /// - 빛(오라, 빛기둥, 렌즈 줄, 별빛, 주변 별, 꼬리 빛, 웅덩이, 둘레 빛)은 반올림 없이 소수 위치를 그대로 따라간다.
    /// - 아이템 그림에 겹치는 백열은 메쉬를 스프라이트 원점에 두고 스프라이트 픽셀 격자 기준 로컬 px로 그린다.
    ///   스프라이트와 한 몸으로 움직이고, 흡입 때 같이 작아진다.
    /// - 입자(기둥을 타는 입자, 반짝임, 파편, 유지 알갱이)와 꼬리 심선은 다른 절차적 이펙트처럼 월드 px 격자에 스냅한다.
    /// - 아이템 아웃라인은 LootItem 프리팹의 NoSnap 아웃라인 머티리얼(빌더가 교체)로 아이템 그림과 같은 위치에 그린다.
    ///
    /// 픽셀 규칙
    /// - JAGGIES/DOUBLE PIXEL: 꼬리 심선은 PixelPainter.Line(Bresenham + L자 모서리 제거)만 쓴다.
    /// - HUGGING: 평행하게 달리는 선이 없다. 둘레 빛은 선이 아니라 블러 텍스처라 1px 아웃라인 옆에 평행선을 만들지 않는다.
    /// - 입자는 `.` `+` `x` `●` `◆`(기둥을 타는 입자는 PixelPainter.Shape 정의)와 반짝임용 5x5 `+`만 쓰고, 입자 하나는 한 색이다
    ///   (반짝임·파편의 `+` `x`만 가운데 한 칸을 한 단계 밝게).
    ///
    /// 위치·난수: 모든 입자 위치는 경과 시간만으로 정하는 닫힌 식이다. 난수는 재생 시작 때만 뽑는다. 힙 할당은 초기화 때 한 번뿐이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_RareLootAppear : MonoBehaviour
    {
        //외부 의존성
        [Header("참조 (빌더가 채움)")]
        [SerializeField, Tooltip("스프라이트별 실루엣·둘레 데이터")]
        private RareLootVfxData data;
        [SerializeField, Tooltip("아이템 본체 스프라이트(LootItem의 Animator 자식). 정렬 기준이자 데이터 조회 키.")]
        private SpriteRenderer itemRenderer;
        [SerializeField, Tooltip("LootItem 아웃라인 렌더러. _OutlineColor를 이펙트 색으로 바꾼다.")]
        private SpriteRenderer outlineRenderer;
        [SerializeField, Tooltip("부드러운 빛 스프라이트 36개. 순서는 SoftPoolOuter ~ SoftItemCore 상수와 같다.")]
        private SpriteRenderer[] softRenderers = new SpriteRenderer[SoftCount];
        [SerializeField] private MeshRenderer addFrontRenderer;
        [SerializeField, Tooltip("입자: 기둥을 타는 입자·반짝임·파편·유지 알갱이(월드 px 격자)")] private MeshRenderer glowRenderer;
        [SerializeField, Tooltip("꼬리 심선(가산, 월드 px 격자)")] private MeshRenderer trailLineRenderer;

        [Header("색 (모든 전리품 공통 황금빛, sRGB)")]
        [SerializeField, Tooltip("밝은 색. 심·흰 섬광 바로 아래 단계.")]
        private Color goldCoreColor = new Color(1.0f, 0.965f, 0.847f, 1.0f);   // #FFF6D8
        [SerializeField, Tooltip("기본색. 오라·빛기둥·아웃라인.")]
        private Color goldMainColor = new Color(1.0f, 0.745f, 0.239f, 1.0f);   // #FFBE3D
        [SerializeField, Tooltip("진한 색. 입자가 식는 마지막 단계.")]
        private Color goldDeepColor = new Color(1.0f, 0.459f, 0.078f, 1.0f);   // #FF7514

        [Header("세기")]
        [SerializeField, Range(0.0f, 2.0f), Tooltip("입자 정점 알파(발광 세기)에 곱하는 배율.")]
        private float glowScale = 1.0f;
        [SerializeField, Tooltip("가산 스프라이트 머티리얼의 _Glow 값. 빌더가 머티리얼과 같은 값으로 맞춘다.")]
        private float additiveGlow = 2.0f;
        [SerializeField, Tooltip("빛기둥 위로 솟는 입자 전용 발광(정점 알파): x = 아래, y = 중간, z = 위. 셰이더 배율 = 1.3 + 2.9 × 값 (0.24 ≈ 2.0배, 0.17 ≈ 1.8배, 0.10 ≈ 1.6배).")]
        private Vector3 beamMoteGlow = new Vector3(0.24f, 0.17f, 0.10f);

        [Header("등장 임팩트")]
        [SerializeField, Tooltip("A. 섬광 원반: 등장 첫 0.08초 동안 아이템 중심에 큰 흰 원 섬광. 비교 결과 기본은 끔(C 파편 분사만 사용).")]
        private bool bImpactFlash = false;
        [SerializeField, Tooltip("C. 파편 분사: 등장 순간 픽셀 조각 8개가 짧게 튀었다가 사라진다.")]
        private bool bImpactShards = true;

        private const float PixelsPerUnit = 32.0f;
        private const float PixelUnit = 1.0f / 32.0f;
        private const float Scale = 1.3f;
        private const int QuadBudget = 1600;

        // 부드러운 빛 슬롯
        private const int SoftPoolOuter = 0;
        private const int SoftPoolCore = 1;
        private const int SoftBeamOuter = 2;
        private const int SoftBeamCore = 3;
        private const int SoftAuraOuter = 4;
        private const int SoftAuraCore = 5;
        private const int SoftHoldAura = 6;
        private const int SoftStreakH = 7;
        private const int SoftStreakV = 8;
        private const int SoftLandStreak = 9;
        private const int SoftGlint = 10;     // 4개
        private const int SoftTrailGlow = 14; // 8개
        private const int SoftImpactFlash = 22;
        private const int SoftStarH = 23;     // 5개(주변 별 가로 빛살)
        private const int SoftStarV = 28;     // 5개(주변 별 세로 빛살)
        private const int SoftRimGlow = 33;   // 둘레 빛(아이템 실루엣 모양 블러, 스프라이트는 아이템별로 바꿔 끼운다)
        private const int SoftBeamJoint = 34; // 빛기둥과 아이템 이음부 빛(아이템 앞, 가산)
        private const int SoftItemCore = 35;  // 아우라 심(아이템 뒤, 가산): 아이템 자체가 빛을 내는 것처럼 밝힌다
        private const int GlintCount = 4;
        private const int TrailGlowCount = 8;
        private const int StarCount = 5;
        public const int SoftCount = 36;

        // 슬롯별 정렬 오프셋(아이템 스프라이트 기준). 시뮬레이션의 그리는 순서와 같다.
        private static readonly int[] SoftOrderOffset = { -10, -9, -8, -7, -3, -2, -3, 2, 2, 2, 2, 2, 2, 2, -6, -6, -6, -6, -6, -6, -6, -6, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, -4, 1, -1 };
        private const int TrailLineOrderOffset = -5;
        private const int AddFrontOrderOffset = 1;
        private const int GlowOrderOffset = 3;

        // 타임라인(초)
        // 등장·착지 번쩍은 최대 밝기를 잠시 유지한 뒤 감쇠한다(바로 줄어들면 너무 빨리 지나가 느낌이 나지 않는다).
        private const float AppearPeakHold = 0.15f;
        private const float AppearAuraTime = 1.2f;
        private const float StreakTime = 0.9f;
        private const float BurnTime = 0.7f;
        private const float BeamSurgeTime = 1.0f;      // 등장 순간 빛기둥이 밝고 굵게 솟았다가 유지 세기로 내려오는 시간
        private const float LandPeakHold = 0.1f;
        private const float LandBurnTime = 0.3f;
        private const float LandStreakTime = 0.5f;
        private const float ImpactFlashTime = 0.08f;
        private const float ShardLife = 0.25f;
        private const float RimIntroTime = 0.6f;
        private const float TrailFadeTime = 0.25f;
        private const float LeaveFadeTime = 0.15f;
        private const float HoldDetailDelay = 0.6f;

        // 크기(px, 반지름/반길이). 시안 G3 값 x Scale.
        private const float AuraAppearRadiusStart = 42.0f * Scale;
        private const float AuraAppearRadiusShrink = 10.0f * Scale;
        private const float AuraCoreRadius = 14.0f * Scale;
        private const float HoldAuraRadius = 36.0f * Scale;   // 아이템 아우라(비행 중에도 켜짐)
        private const float ItemCoreRadius = 20.0f;           // 아우라 심(px)
        private const float PoolRadius = 34.0f * Scale;
        private const float PoolCoreRadius = 10.0f * Scale;
        private const float PoolSquash = 0.35f;
        private const float PoolCoreSquash = 0.4f;
        private const float BeamHeight = 150.0f * Scale;
        private const float BeamOuterSigma = 9.5f * Scale;
        private const float BeamCoreSigma = 2.0f * Scale;
        private const float BeamJointWidth = 20.0f;     // 이음부 빛 폭(px)
        private const float BeamJointHeight = 34.0f;    // 이음부 빛 높이(px)
        private const float BeamFootDepth = 14.0f;      // 빛기둥 둥근 밑동이 아이템 중심 아래로 내려오는 깊이(px). 빌더 BeamFootDepthPx와 같다
        private const float BeamJointRaise = 10.0f;     // 이음부 빛 중심을 아이템 중심보다 올리는 양(px): 아랫부분만 아이템 위쪽에 살짝 겹친다
        private const float BeamTextureSpan = 2.6f * 1.8f; // 빛기둥 텍스처 가로 = 밑동(나팔) 기준 +-2.6 x 1.8 시그마(빌더 BeamSpan x BeamFlare와 같다)
        private const float StreakHLength = 130.0f;
        private const float StreakVLength = 46.0f;
        private const float LandStreakLength = 65.0f;
        private const float ImpactFlashRadius = 45.0f;

        // 연속 섬광 리듬: 0 / 0.18 / 0.36초에 번쩍(세기 100 / 70 / 45%). 번쩍 하나는 0.04초 최대 후 0.14초에 걸쳐 꺼진다.
        private static readonly float[] PulseTime = { 0.0f, 0.18f, 0.36f };
        private static readonly float[] PulseStrength = { 1.0f, 0.7f, 0.45f };
        private const float PulsePeak = 0.04f;
        private const float PulseFall = 0.14f;

        // 별빛 회전·맥동
        private const float GlintSpinSpeed = 120.0f;   // 도/초
        private const float GlintBreathHz = 3.0f;      // 긴 빛살이 길어졌다 짧아지는 빈도

        // 주변 별: 아이템 중심 기준 고정 위치(px, y 위쪽), 차례로 회전하며 커졌다 작아진다
        private static readonly float[] StarOffsetX = { -17.0f, 16.0f, 19.0f, -14.0f, 3.0f };
        private static readonly float[] StarOffsetY = { 9.0f, 15.0f, -5.0f, -11.0f, 21.0f };
        private static readonly float[] StarHalfLength = { 11.0f, 9.0f, 12.0f, 8.0f, 10.0f };
        private const float StarFirstDelay = 0.06f;
        private const float StarInterval = 0.09f;
        private const float StarLife = 0.36f;

        // 파편 분사: 손으로 정한 방향(도, 화면 반시계)·거리(px)·지연(초). 대칭이 되지 않게 각도와 거리를 어긋나게 둔다.
        private static readonly float[] ShardAngle = { 12.0f, 58.0f, 101.0f, 147.0f, 196.0f, 231.0f, 284.0f, 326.0f };
        private static readonly float[] ShardDistance = { 11.0f, 9.0f, 12.0f, 8.0f, 10.0f, 7.0f, 12.0f, 9.0f };
        private static readonly float[] ShardDelay = { 0.0f, 0.01f, 0.0f, 0.02f, 0.01f, 0.03f, 0.0f, 0.02f };

        // 꼬리
        private const int HistoryCapacity = 48;
        private const float TrailGlowStep = 0.024f;
        private const float TrailLineStep = 0.016f;
        private const int TrailLineSegments = 6;

        // 반짝임: (가로 비율, 위에서 아래 비율, 시작 초)
        private static readonly float[] TwinkleFx = { 0.0f, 1.0f, 0.45f, 0.95f, -0.05f };
        private static readonly float[] TwinkleFy = { 0.1f, 0.3f, 1.05f, 0.9f, 0.6f };
        private static readonly float[] TwinkleStart = { 0.04f, 0.1f, 0.16f, 0.22f, 0.28f };
        private const float TwinkleLife = 0.28f;
        private static readonly float[] HoldTwinkleFx = { 0.9f, 0.05f, 0.7f };
        private static readonly float[] HoldTwinkleFy = { 0.05f, 0.85f, 0.95f };
        private static readonly float[] HoldTwinkleOffset = { 0.0f, 1.1f, 2.2f };
        private const float HoldTwinkleCycle = 3.3f;
        private const float HoldTwinkleLife = 0.5f;

        // 기둥을 타는 입자와 유지 알갱이
        private const int BeamDashCount = 6;
        // 기둥을 타고 오르는 입자의 처음 모양(차례로 돌려 쓴다). 올라가며 작은 모양으로 줄어든다.
        private static readonly int[] BeamMoteShapes = { PixelPainter.ShapeDiamond, PixelPainter.ShapePlus, PixelPainter.ShapeCircle, PixelPainter.ShapeCross, PixelPainter.ShapePlus, PixelPainter.ShapeDot };
        private const int HoldMoteCount = 5;

        // 입자 톤: 0 밝은(core) 1 중간(main) 2 진한(deep), 발광 세기(가이드 2.2)
        private static readonly float[] ToneGlow = { 1.0f, 0.75f, 0.45f };

        private const int GlyphDot = 0;
        private const int GlyphPlus = 1;
        private const int GlyphCross = 2;
        private const int GlyphPlus5 = 3;

        private struct SoftState
        {
            public bool bOn;
            public Vector3 position;
            public Vector3 scale;
            public float angle;
            public Color color;
        }

        // 쿼드 버퍼는 인스턴스끼리 공유한다(메인 스레드 단일 실행, 레이어마다 채우고 바로 올린다).
        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(QuadBudget);

        //내부 의존성
        private readonly PixelPainter painter = new PixelPainter();
        private Transform rootTransform;      // LootItem 루트
        private Transform vfxRootTransform;   // LootItem.vfxRoot (비주얼 높이를 따라감)
        private Mesh addFrontMesh;
        private Mesh glowMesh;
        private Mesh trailLineMesh;
        private MaterialPropertyBlock outlineBlock;
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private readonly SoftState[] softStates = new SoftState[SoftCount];
        private readonly float[] historyTime = new float[HistoryCapacity];
        private readonly float[] historyX = new float[HistoryCapacity];
        private readonly float[] historyY = new float[HistoryCapacity];
        private readonly Color32[] toneColors = new Color32[3];
        private readonly Color32[] beamMoteColors = new Color32[3];
        private readonly float[] dashSpeed = new float[BeamDashCount];
        private readonly int[] dashOffset = new int[BeamDashCount];
        private readonly int[] dashShape = new int[BeamDashCount];
        private readonly float[] dashPhase = new float[BeamDashCount];
        private readonly float[] moteSpeed = new float[HoldMoteCount];
        private readonly float[] moteOffset = new float[HoldMoteCount];
        private readonly float[] motePhase = new float[HoldMoteCount];

        //상태 변수
        private bool bInitialized;
        private bool bFirstFrame;
        private bool bActive;
        private int entryIndex = -1;
        private float rimGlowSizePx;          // 둘레 빛 스프라이트 한 변(px). 0이면 둘레 빛 없음
        private Color coreColor;
        private Color mainColor;
        private Color deepColor;
        private Color32 coreAdd;
        private Color32 whiteAdd;
        private float elapsed;
        private float landTime;
        private float leaveTime;
        private bool bAirborne;
        private Vector3 landRootPosition;
        private int historyCount;
        private int historyHead;
        private uint randomState = 1u;
        private int appliedSortingLayerID = int.MinValue;
        private int appliedSortingOrder = int.MinValue;
        private ulong addFrontHash;
        private int addFrontVertexCount = -1;
        private ulong glowHash;
        private int glowVertexCount = -1;
        private ulong trailLineHash;
        private int trailLineVertexCount = -1;

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            vfxRootTransform = transform.parent;
            rootTransform = null != vfxRootTransform ? vfxRootTransform.parent : null;
            outlineBlock = new MaterialPropertyBlock();

            addFrontMesh = CreateMesh(addFrontRenderer, "RareLoot_AddFront");
            glowMesh = CreateMesh(glowRenderer, "RareLoot_Glow");
            trailLineMesh = CreateMesh(trailLineRenderer, "RareLoot_TrailLine");
        }

        private static Mesh CreateMesh(MeshRenderer _renderer, string _name)
        {
            if (null == _renderer) return null;

            MeshFilter filter = _renderer.GetComponent<MeshFilter>();
            if (null == filter) return null;

            Mesh mesh = new Mesh { name = _name };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 400);

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            return mesh;
        }

        // 재생 시작: LootItem.Initialize가 스프라이트를 정한 뒤 Launch에서 vfxRoot가 켜지므로, 첫 LateUpdate에서 스프라이트를 읽는다
        private void Begin()
        {
            entryIndex = null != data && null != itemRenderer ? data.IndexOf(itemRenderer.sprite) : -1;
            bActive = null != itemRenderer && null != itemRenderer.sprite;

            // 색은 모든 전리품이 같은 황금빛이다
            coreColor = goldCoreColor;
            mainColor = goldMainColor;
            deepColor = goldDeepColor;

            BuildTones();
            ApplyOutlineColor();

            // 둘레 빛: 아이템별로 구워 둔 실루엣 블러 스프라이트를 끼운다(피벗이 아이템 스프라이트 피벗과 같다)
            rimGlowSizePx = 0.0f;
            SpriteRenderer rimGlow = SoftRimGlow < softRenderers.Length ? softRenderers[SoftRimGlow] : null;
            if (0 <= entryIndex && null != rimGlow)
            {
                Sprite glow = data.Get(entryIndex).rimGlow;
                if (null != glow)
                {
                    if (rimGlow.sprite != glow) rimGlow.sprite = glow;
                    rimGlowSizePx = glow.texture.width;
                }
            }

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            for (int i = 0; i < BeamDashCount; i++)
            {
                dashSpeed[i] = RandRange(50.0f, 100.0f);
                dashOffset[i] = Mathf.RoundToInt(RandRange(-5.0f, 5.0f));
                dashShape[i] = BeamMoteShapes[i % BeamMoteShapes.Length];
                dashPhase[i] = RandRange(0.0f, 3.0f);
            }

            for (int i = 0; i < HoldMoteCount; i++)
            {
                moteSpeed[i] = RandRange(12.0f, 22.0f);
                moteOffset[i] = RandRange(-4.0f, 4.0f);
                motePhase[i] = RandRange(0.0f, 10.0f);
            }
        }

        private void BuildTones()
        {
            bool bLinear = ColorSpace.Linear == QualitySettings.activeColorSpace;
            toneColors[0] = ToVertex(coreColor, ToneGlow[0] * glowScale, bLinear);
            toneColors[1] = ToVertex(mainColor, ToneGlow[1] * glowScale, bLinear);
            toneColors[2] = ToVertex(deepColor, ToneGlow[2] * glowScale, bLinear);
            beamMoteColors[0] = ToVertex(coreColor, beamMoteGlow.x * glowScale, bLinear);
            beamMoteColors[1] = ToVertex(mainColor, beamMoteGlow.y * glowScale, bLinear);
            beamMoteColors[2] = ToVertex(deepColor, beamMoteGlow.z * glowScale, bLinear);
            coreAdd = ToVertex(coreColor, 1.0f, bLinear);
            whiteAdd = new Color32(255, 255, 255, 255);
        }

        // 정점 색은 셰이더가 그대로 출력하므로 선형 색 공간에서는 sRGB 목표 색을 선형으로 바꿔 넣는다
        private static Color32 ToVertex(Color _srgb, float _alpha, bool _bLinear)
        {
            Color color = true == _bLinear ? _srgb.linear : _srgb;
            color.a = Mathf.Clamp01(_alpha);
            return color;
        }

        private static Color32 WithAlpha(Color32 _color, float _alpha)
        {
            _color.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(_alpha) * 255.0f);
            return _color;
        }

        private void ApplyOutlineColor()
        {
            if (null == outlineRenderer || false == bActive) return;

            outlineRenderer.GetPropertyBlock(outlineBlock);
            outlineBlock.SetColor(OutlineColorId, mainColor * 1.15f);
            outlineRenderer.SetPropertyBlock(outlineBlock);
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

        private static float EaseOutCubic(float _x)
        {
            float inv = 1.0f - _x;
            return 1.0f - inv * inv * inv;
        }

        // ---------- 상태 ----------

        // 아이템 비주얼 높이로 착지를, 루트의 이동/축소로 흡입 시작을 판단한다
        private void UpdatePhase()
        {
            float height = null != vfxRootTransform ? vfxRootTransform.localPosition.y : 0.0f;

            if (0.01f < height) bAirborne = true;

            if (0.0f > landTime && true == bAirborne && 0.0001f >= height)
            {
                landTime = elapsed;
                landRootPosition = null != rootTransform ? rootTransform.position : transform.position;
            }

            if (0.0f <= landTime && 0.0f > leaveTime && null != rootTransform)
            {
                Vector3 moved = rootTransform.position - landRootPosition;
                if (0.999f > rootTransform.localScale.x || PixelUnit * PixelUnit < moved.x * moved.x + moved.y * moved.y)
                {
                    leaveTime = elapsed;
                }
            }
        }

        private float LeaveFade()
        {
            if (0.0f > leaveTime) return 1.0f;
            return Mathf.Max(0.0f, 1.0f - (elapsed - leaveTime) / LeaveFadeTime);
        }

        // 착지 후 빛기둥·오라 세기: 착지 직후 1에서 0.7까지 내려가고 이후 천천히 일렁인다
        private static float HoldLevel(float _k)
        {
            if (0.5f > _k) return 1.0f - _k / 0.5f * 0.3f;
            return 0.7f + 0.08f * Mathf.Sin(_k * 2.6f);
        }

        // 연속 섬광 리듬(0~1): 가장 밝은 번쩍 하나의 값
        private static float Pulse(float _t)
        {
            float best = 0.0f;
            for (int i = 0; i < PulseTime.Length; i++)
            {
                float x = _t - PulseTime[i];
                if (0.0f > x) continue;

                float v = PulsePeak > x ? 1.0f : Mathf.Max(0.0f, 1.0f - (x - PulsePeak) / PulseFall);
                best = Mathf.Max(best, v * PulseStrength[i]);
            }

            return best;
        }

        // 번쩍 세기: _hold초 동안 1을 유지하고, _length초까지 줄어 0이 된다
        private static float PeakDecay(float _time, float _hold, float _length)
        {
            if (_hold > _time) return 1.0f;
            if (_length <= _time) return 0.0f;
            return 1.0f - EaseOutCubic((_time - _hold) / (_length - _hold));
        }

        // 아이템 중심(월드 px, 소수) 기록. 꼬리가 지난 위치를 읽는다.
        private void RecordHistory(float _x, float _y)
        {
            historyHead = (historyHead + 1) % HistoryCapacity;
            historyTime[historyHead] = elapsed;
            historyX[historyHead] = _x;
            historyY[historyHead] = _y;
            if (HistoryCapacity > historyCount) historyCount++;
        }

        private bool SampleHistory(float _time, out float _x, out float _y)
        {
            _x = 0.0f;
            _y = 0.0f;
            if (0 == historyCount) return false;

            int index = historyHead;
            for (int i = 0; i < historyCount; i++)
            {
                if (historyTime[index] <= _time)
                {
                    int newer = (index + 1) % HistoryCapacity;
                    if (0 == i || historyTime[newer] <= historyTime[index])
                    {
                        _x = historyX[index];
                        _y = historyY[index];
                        return true;
                    }

                    float f = (_time - historyTime[index]) / (historyTime[newer] - historyTime[index]);
                    _x = Mathf.Lerp(historyX[index], historyX[newer], f);
                    _y = Mathf.Lerp(historyY[index], historyY[newer], f);
                    return true;
                }

                index = (index - 1 + HistoryCapacity) % HistoryCapacity;
            }

            return false;
        }



        private void SyncSorting()
        {
            int layer = itemRenderer.sortingLayerID;
            int order = itemRenderer.sortingOrder;
            if (layer == appliedSortingLayerID && order == appliedSortingOrder) return;

            appliedSortingLayerID = layer;
            appliedSortingOrder = order;

            for (int i = 0; i < SoftCount && i < softRenderers.Length; i++)
            {
                if (null == softRenderers[i]) continue;
                softRenderers[i].sortingLayerID = layer;
                softRenderers[i].sortingOrder = order + SoftOrderOffset[i];
            }

            SetMeshSorting(addFrontRenderer, layer, order + AddFrontOrderOffset);
            SetMeshSorting(glowRenderer, layer, order + GlowOrderOffset);
            SetMeshSorting(trailLineRenderer, layer, order + TrailLineOrderOffset);
        }

        private static void SetMeshSorting(MeshRenderer _renderer, int _layer, int _order)
        {
            if (null == _renderer) return;
            _renderer.sortingLayerID = _layer;
            _renderer.sortingOrder = _order;
        }

        // ---------- 부드러운 빛 ----------

        // 슬롯 하나를 켜고 월드 위치(px)·크기(px)·각도·색을 쓴다. 지난번과 같으면 쓰지 않는다.
        private void SetSoft(int _slot, float _xPx, float _yPx, float _widthPx, float _heightPx, float _angle, Color _color)
        {
            SpriteRenderer sr = softRenderers[_slot];
            if (null == sr) return;

            if (0.002f >= _color.a || 0.01f >= _widthPx || 0.01f >= _heightPx)
            {
                HideSoft(_slot);
                return;
            }

            ref SoftState s = ref softStates[_slot];
            Vector3 position = new Vector3(_xPx * PixelUnit, _yPx * PixelUnit, transform.position.z);
            Vector3 scale = new Vector3(_widthPx * PixelUnit, _heightPx * PixelUnit, 1.0f);

            if (false == s.bOn)
            {
                s.bOn = true;
                sr.enabled = true;
            }

            if (position.x != s.position.x || position.y != s.position.y || position.z != s.position.z)
            {
                s.position = position;
                sr.transform.position = position;
            }

            if (scale.x != s.scale.x || scale.y != s.scale.y)
            {
                s.scale = scale;
                sr.transform.localScale = scale;
            }

            if (_angle != s.angle)
            {
                s.angle = _angle;
                sr.transform.rotation = Quaternion.Euler(0.0f, 0.0f, _angle);
            }

            if (_color.r != s.color.r || _color.g != s.color.g || _color.b != s.color.b || _color.a != s.color.a)
            {
                s.color = _color;
                sr.color = _color;
            }
        }

        private void HideSoft(int _slot)
        {
            if (false == softStates[_slot].bOn) return;
            softStates[_slot].bOn = false;
            if (null != softRenderers[_slot]) softRenderers[_slot].enabled = false;
        }

        private void HideAll()
        {
            for (int i = 0; i < SoftCount; i++)
            {
                softStates[i].bOn = true;
                softStates[i].angle = float.NaN;
                HideSoft(i);
            }

            if (null != addFrontRenderer) addFrontRenderer.enabled = false;
            if (null != glowRenderer) glowRenderer.enabled = false;
            if (null != trailLineRenderer) trailLineRenderer.enabled = false;
        }

        // 반투명 덮기 색(SpriteGlowTint): 색 그대로, 알파 = 덮는 정도
        private static Color Blend(Color _color, float _alpha)
        {
            _color.a = Mathf.Clamp01(_alpha);
            return _color;
        }

        // 가산 색(SpriteGlowAdditive): 머티리얼 _Glow 배율을 나눠 원하는 세기(_intensity)가 되게 알파를 정한다
        private Color Add(Color _color, float _intensity)
        {
            _color.a = Mathf.Clamp01(_intensity / Mathf.Max(0.01f, additiveGlow));
            return _color;
        }

        private void UpdateSoftLights(float _pivotX, float _pivotY, float _centerX, float _centerY, float _groundX, float _groundY)
        {
            float t = elapsed;
            float k = 0.0f <= landTime ? elapsed - landTime : -1.0f;
            float leave = LeaveFade();
            float pulse = Pulse(t);   // 연속 섬광 리듬(번쩍 세 번)

            // 둘레 빛: 아이템 실루엣 모양으로 부드럽게 번지는 반투명 빛. 아이템 원점(소수 위치)에 겹쳐 함께 움직인다.
            if (0.0f < rimGlowSizePx)
            {
                float s;
                if (RimIntroTime > t) s = 0.5f * (1.0f - EaseOutCubic(t / RimIntroTime)) + 0.45f;
                else if (HoldDetailDelay < k) s = (0.32f + 0.08f * Mathf.Sin(k * 3.0f)) * leave;
                else s = 0.45f * leave;

                SetSoft(SoftRimGlow, _pivotX, _pivotY, rimGlowSizePx, rimGlowSizePx, 0.0f, Blend(mainColor, s));
            }
            else
            {
                HideSoft(SoftRimGlow);
            }

            // ① 등장 오라: 전체는 천천히 줄고, 그 위에서 번쩍 리듬에 맞춰 밝아졌다 어두워진다
            if (AppearAuraTime > t)
            {
                float u = t / AppearAuraTime;
                float env = PeakDecay(t, AppearPeakHold, AppearAuraTime);
                float r = (AuraAppearRadiusStart - AuraAppearRadiusShrink * u) * (0.9f + 0.1f * pulse);
                SetSoft(SoftAuraOuter, _centerX, _centerY, r * 2.0f, r * 2.0f, 0.0f, Blend(mainColor, 0.85f * env * (0.5f + 0.5f * pulse)));
                SetSoft(SoftAuraCore, _centerX, _centerY, AuraCoreRadius * 2.0f, AuraCoreRadius * 2.0f, 0.0f, Add(coreColor, 1.6f * env * (0.35f + 0.65f * pulse)));
            }
            else
            {
                HideSoft(SoftAuraOuter);
                HideSoft(SoftAuraCore);
            }

            // ① 렌즈 줄 + 별빛: 렌즈 줄은 번쩍마다 길고 밝아지고, 별빛은 계속 돌며 긴 빛살이 길어졌다 짧아진다
            if (StreakTime > t)
            {
                float u = t / StreakTime;
                float env = PeakDecay(t, AppearPeakHold, StreakTime);
                float streakScale = (1.0f - 0.3f * u) * (0.85f + 0.15f * pulse);
                SetSoft(SoftStreakH, _centerX, _centerY, StreakHLength * streakScale * 2.0f, (2.6f - 1.4f * u) * 2.0f, 0.0f, Add(mainColor, 1.3f * env * (0.4f + 0.6f * pulse)));
                SetSoft(SoftStreakV, _centerX, _centerY, StreakVLength * streakScale * 2.0f, 1.8f * 2.0f, 90.0f, Add(mainColor, 0.9f * env * (0.4f + 0.6f * pulse)));

                float spin = t * GlintSpinSpeed;
                float breath = 0.8f + 0.2f * Mathf.Sin(t * GlintBreathHz * Mathf.PI * 2.0f);
                for (int i = 0; i < GlintCount; i++)
                {
                    float length = (2 > i ? 36.0f * breath : 23.0f) * (1.0f - 0.7f * u) * (0.85f + 0.15f * pulse);
                    float angle = (2 > i ? i * 90.0f : 45.0f + (i - 2) * 90.0f) - spin;
                    SetSoft(SoftGlint + i, _centerX, _centerY, length * 2.0f, 2.0f, angle, Add(coreColor, 1.3f * env * (0.45f + 0.55f * pulse)));
                }
            }
            else
            {
                HideSoft(SoftStreakH);
                HideSoft(SoftStreakV);
                for (int i = 0; i < GlintCount; i++) HideSoft(SoftGlint + i);
            }

            // A. 섬광 원반: 첫 0.08초 동안 크기 그대로 밝기만 빠르게 꺼진다
            if (true == bImpactFlash && ImpactFlashTime > t)
            {
                SetSoft(SoftImpactFlash, _centerX, _centerY, ImpactFlashRadius * 2.0f, ImpactFlashRadius * 2.0f, 0.0f, Add(coreColor, 2.0f * (1.0f - t / ImpactFlashTime)));
            }
            else
            {
                HideSoft(SoftImpactFlash);
            }

            // ① 주변 별: 아이템 둘레 고정 자리에서 차례로 회전하며 커졌다 작아진다(가로·세로 빛살 두 개가 4갈래 별 하나)
            for (int i = 0; i < StarCount; i++)
            {
                float a = t - (StarFirstDelay + i * StarInterval);
                if (0.0f > a || StarLife <= a)
                {
                    HideSoft(SoftStarH + i);
                    HideSoft(SoftStarV + i);
                    continue;
                }

                float u = a / StarLife;
                float size = Mathf.Sin(u * Mathf.PI);
                float half = StarHalfLength[i] * Scale * size;
                float angle = 45.0f + u * 120.0f;
                float x = _centerX + StarOffsetX[i];
                float y = _centerY + StarOffsetY[i];
                Color color = Add(coreColor, 1.8f * size);
                SetSoft(SoftStarH + i, x, y, half * 2.0f, 2.0f, angle, color);
                SetSoft(SoftStarV + i, x, y, half * 1.4f, 2.0f, angle + 90.0f, color);
            }

            // ② 꼬리 빛(지난 위치를 따라 작아지는 반투명 원)
            bool bTrail = 0.0f < t && (0.0f > k || TrailFadeTime > k);
            float trailFade = 0.0f > k ? 1.0f : 1.0f - k / TrailFadeTime;
            for (int i = 0; i < TrailGlowCount; i++)
            {
                int slot = SoftTrailGlow + i;
                float sampleTime = t - (i + 1) * TrailGlowStep;
                if (0.0f <= landTime) sampleTime = Mathf.Min(sampleTime, landTime);

                if (false == bTrail || 0.0f > sampleTime || false == SampleHistory(sampleTime, out float x, out float y))
                {
                    HideSoft(slot);
                    continue;
                }

                float u = (i + 1) / (float)TrailGlowCount;
                float r = (6.0f * (1.0f - u) + 2.0f) * Scale;
                SetSoft(slot, x, y, r * 2.0f, r * 2.0f, 0.0f, Blend(mainColor, 0.55f * (1.0f - u) * trailFade));
            }

            // ①~④ 빛기둥: 등장 순간부터 켜지고, 아래끝이 아이템 중심에 붙어 아이템 위로만 솟는다(비행 중에도 아이템과 같이 움직인다).
            // 등장 순간에는 밝고 굵게 솟았다가 유지 세기로 내려온다(따로 있던 분출 빛을 여기에 합쳤다).
            float landFlash = 0.0f <= k ? Mathf.Max(0.0f, 1.0f - k / 0.3f) : 0.0f;
            float level = 0.0f <= k ? HoldLevel(k) : 0.7f + 0.08f * Mathf.Sin(t * 2.6f);
            float surge = PeakDecay(t, AppearPeakHold, BeamSurgeTime);

            if (0.0f < leave)
            {
                float widen = 1.0f + 0.6f * surge;

                // 빛기둥 높이 = 위쪽 기둥 + 아이템 중심 아래 둥근 밑동(피벗이 밑동 위에 있어 아래 끝이 수평으로 잘리지 않는다)
                float beamSpriteHeight = BeamHeight + BeamFootDepth;
                SetSoft(SoftBeamOuter, _centerX, _centerY, BeamOuterSigma * BeamTextureSpan * 2.0f * widen, beamSpriteHeight, 0.0f, Blend(mainColor, 0.8f * (level + landFlash * 0.6f + surge * 0.5f) * leave));
                SetSoft(SoftBeamCore, _centerX, _centerY, BeamCoreSigma * BeamTextureSpan * 2.0f * widen, beamSpriteHeight, 0.0f, Add(coreColor, 1.15f * (level + landFlash * 0.8f + surge * 1.2f) * leave));

                // 이음부 빛: 아이템 윗부분에 세로로 긴 가산 빛을 얹어 빛기둥이 아이템에서 뿜어 나오는 것처럼 잇는다
                // (빛기둥 밑동은 아이템 뒤에 가려지고 아이템 바로 위는 둘레 빛·오라와 섞여 묻히기 때문)
                SetSoft(SoftBeamJoint, _centerX, _centerY + BeamJointRaise, BeamJointWidth * widen, BeamJointHeight, 0.0f, Add(coreColor, 0.9f * (level + landFlash * 0.6f + surge * 0.8f) * leave));
            }
            else
            {
                HideSoft(SoftBeamOuter);
                HideSoft(SoftBeamCore);
                HideSoft(SoftBeamJoint);
            }

            // 아이템 아우라: 비행 중에도 켜져 아이템을 큰 빛 덩어리로 감싸고, 그 위로 빛기둥이 이어진다.
            // 아우라 심은 아이템 뒤에서 아이템 자체가 빛을 내는 것처럼 밝힌다.
            // 흡입 중에도 꺼지지 않고 아이템을 따라가며(루트 축소에 맞춰 함께 작아진다) 획득 시 vfxRoot가 꺼질 때 사라진다.
            SetSoft(SoftHoldAura, _centerX, _centerY, HoldAuraRadius * 2.0f, HoldAuraRadius * 2.0f, 0.0f, Blend(mainColor, 0.85f * (level + landFlash * 0.3f)));
            SetSoft(SoftItemCore, _centerX, _centerY, ItemCoreRadius * 2.0f, ItemCoreRadius * 2.0f, 0.0f, Add(coreColor, 0.9f * (level + landFlash * 0.6f + surge * 0.8f)));

            // ③④ 빛 웅덩이(착지 후)
            if (0.0f <= k && 0.0f < leave)
            {
                float on = Mathf.Min(1.0f, k / 0.06f);
                float flash = Mathf.Max(0.0f, 1.0f - k / 0.3f);
                float hold = HoldLevel(k);

                SetSoft(SoftPoolOuter, _groundX, _groundY - 5.0f, PoolRadius * 2.0f, PoolRadius * 2.0f * PoolSquash, 0.0f, Blend(mainColor, (0.55f * hold + 0.3f * flash) * on * leave));
                SetSoft(SoftPoolCore, _groundX, _groundY - 5.0f, PoolCoreRadius * 2.0f, PoolCoreRadius * 2.0f * PoolCoreSquash, 0.0f, Add(coreColor, (0.35f * hold + 0.4f * flash) * on * leave));
            }
            else
            {
                HideSoft(SoftPoolOuter);
                HideSoft(SoftPoolCore);
            }

            // ③ 착지 렌즈 줄
            if (0.0f <= k && LandStreakTime > k)
            {
                float u = k / LandStreakTime;
                SetSoft(SoftLandStreak, _groundX, _groundY - 4.0f, LandStreakLength * (1.0f - 0.3f * u) * 2.0f, 1.6f * 2.0f, 0.0f, Add(mainColor, 1.0f * PeakDecay(k, LandPeakHold, LandStreakTime)));
            }
            else
            {
                HideSoft(SoftLandStreak);
            }
        }

        // ---------- 정수 픽셀 ----------

        // 아이템 기준 메쉬를 아이템 스프라이트 원점(소수 위치 그대로)에 둔다. 이 메쉬들은 스프라이트 픽셀 격자 기준 로컬 px로 그리므로,
        // 아이템이 소수 px만큼 움직여도 스프라이트와 같은 양만큼 같이 움직여 어긋나지 않는다(반올림으로 생기는 지터 제거).
        private void PlaceAtItem(MeshRenderer _renderer)
        {
            if (null == _renderer) return;

            Transform target = _renderer.transform;
            Vector3 position = itemRenderer.transform.position;
            Vector3 current = target.position;
            if (position.x != current.x || position.y != current.y || position.z != current.z) target.position = position;
        }

        // _pivotPxX/_pivotPxY: 아이템 스프라이트 원점의 월드 px(소수).
        // 아이템 그림에 겹치는 백열은 아이템 기준 로컬 px로 그려 아이템과 함께 부드럽게 움직이고,
        // 입자(기둥을 타는 입자, 반짝임, 파편, 유지 알갱이)와 꼬리 심선은 다른 절차적 이펙트처럼 월드 px 격자에 그린다.
        private void RebuildPixels(float _pivotPxX, float _pivotPxY)
        {
            float t = elapsed;
            float k = 0.0f <= landTime ? elapsed - landTime : -1.0f;
            float leave = LeaveFade();
            bool bHasEntry = 0 <= entryIndex;
            Matrix4x4 local = Matrix4x4.identity;

            int centerX = 0;
            int centerY = 0;
            if (true == bHasEntry)
            {
                Vector4 b = data.Get(entryIndex).bounds;
                centerX = Mathf.RoundToInt((b.x + b.z + 1.0f) * 0.5f);
                centerY = Mathf.RoundToInt((b.y + b.w + 1.0f) * 0.5f);
            }

            // 입자를 월드 격자에 둘 때 쓰는 아이템 원점(월드 px 정수)
            int worldPivotX = Mathf.RoundToInt(_pivotPxX);
            int worldPivotY = Mathf.RoundToInt(_pivotPxY);

            PlaceAtItem(addFrontRenderer);

            // 꼬리 심선(가산, 아이템 뒤, 월드 px 격자)
            if (null != trailLineMesh)
            {
                painter.Begin(buffer, trailLineRenderer.transform.worldToLocalMatrix, 0.0f);

                bool bTrail = 0.0f < t && (0.0f > k || TrailFadeTime > k);
                if (true == bTrail)
                {
                    float trailFade = 0.0f > k ? 1.0f : 1.0f - k / TrailFadeTime;
                    for (int i = TrailLineSegments; i >= 1; i--)
                    {
                        float t1 = t - i * TrailLineStep;
                        float t0 = t1 - TrailLineStep;
                        if (0.0f > t0) continue;
                        if (0.0f <= landTime)
                        {
                            t0 = Mathf.Min(t0, landTime);
                            t1 = Mathf.Min(t1, landTime);
                        }

                        if (false == SampleHistory(t0, out float ax, out float ay) || false == SampleHistory(t1, out float bx, out float by)) continue;

                        Color32 color = WithAlpha(coreAdd, 1.2f * (1.0f - i / 7.0f) * trailFade);
                        painter.Line(Mathf.RoundToInt(ax), Mathf.RoundToInt(ay), Mathf.RoundToInt(bx), Mathf.RoundToInt(by), 0, 0, 0, color);
                    }
                }

                painter.Commit(trailLineMesh, trailLineRenderer, ref trailLineHash, ref trailLineVertexCount);
            }

            // 백열 섬광(가산, 아이템 앞, 아이템 기준)
            if (null != addFrontMesh)
            {
                painter.Begin(buffer, local, 0.0f);

                float burn = PeakDecay(t, AppearPeakHold, BurnTime) * (0.4f + 0.6f * Pulse(t));
                if (0.0f <= k) burn = Mathf.Max(burn, 0.7f * PeakDecay(k, LandPeakHold, LandBurnTime));

                if (true == bHasEntry && 0.0f < burn)
                {
                    Vector2Int[] silhouette = data.Get(entryIndex).silhouette;
                    Color32 color = WithAlpha(whiteAdd, 1.4f * burn);
                    for (int i = 0; i < silhouette.Length; i++) painter.Dot(silhouette[i].x, silhouette[i].y, color);
                }

                painter.Commit(addFrontMesh, addFrontRenderer, ref addFrontHash, ref addFrontVertexCount);
            }

            // 입자: 기둥을 타는 입자·반짝임·파편·유지 알갱이(BrandWrapGlow, 맨 앞, 월드 px 격자 - 기존 절차적 이펙트와 같은 방식)
            if (null != glowMesh)
            {
                painter.Begin(buffer, glowRenderer.transform.worldToLocalMatrix, 0.0f);
                if (0.5f < leave) DrawBeamMotes(worldPivotX + centerX, worldPivotY + centerY);
                if (true == bImpactShards) DrawShards(_pivotPxX, _pivotPxY);
                if (true == bHasEntry) DrawTwinkles(worldPivotX, worldPivotY, k, leave);
                DrawHoldMotes(worldPivotX + centerX, worldPivotY + centerY, k, leave);
                painter.Commit(glowMesh, glowRenderer, ref glowHash, ref glowVertexCount);
            }
        }

        // C. 파편 분사: 아이템 중심에서 조각 8개가 급감속하며 12px 안쪽까지 튀었다가 `+` -> `x` -> `.`로 줄어 사라진다
        // 기둥을 타고 오르는 입자: 아이템 중심에서 빛기둥을 따라 올라가며 `◆`/`●` -> `+`/`x` -> `.`로 줄고 색이 식는다(월드 px 격자)
        private void DrawBeamMotes(int _baseX, int _baseY)
        {
            for (int i = 0; i < BeamDashCount; i++)
            {
                float d = Mathf.Repeat(elapsed * dashSpeed[i] + dashPhase[i] * 40.0f, BeamHeight);
                float u = d / BeamHeight;
                int shape = dashShape[i];
                // 등장 구간: . -> 한 단계 작은 모양 -> 원래 모양 순으로 커진다
                if (0.06f > u) shape = PixelPainter.ShapeDot;
                else if (0.12f > u) shape = ShrinkShape(shape);
                else if (0.35f <= u) shape = ShrinkShape(shape);
                if (0.7f <= u) shape = PixelPainter.ShapeDot;

                int tone = 0.35f > u ? 0 : (0.7f > u ? 1 : 2);
                painter.Shape(shape, _baseX + dashOffset[i], _baseY + Mathf.RoundToInt(d), beamMoteColors[tone]);
            }
        }

        // 한 단계 작은 입자 모양: ◆/● -> +, + -> x, x -> .
        private static int ShrinkShape(int _shape)
        {
            switch (_shape)
            {
                case PixelPainter.ShapeDiamond:
                case PixelPainter.ShapeCircle:
                    return PixelPainter.ShapePlus;
                case PixelPainter.ShapePlus:
                    return PixelPainter.ShapeCross;
                default:
                    return PixelPainter.ShapeDot;
            }
        }

        private void DrawShards(float _pivotPxX, float _pivotPxY)
        {
            if (0 > entryIndex) return;

            Vector4 b = data.Get(entryIndex).bounds;
            float centerX = _pivotPxX + (b.x + b.z + 1.0f) * 0.5f;
            float centerY = _pivotPxY + (b.y + b.w + 1.0f) * 0.5f;

            for (int i = 0; i < ShardAngle.Length; i++)
            {
                float a = elapsed - ShardDelay[i];
                if (0.0f > a || ShardLife <= a) continue;

                float u = a / ShardLife;
                float inv = 1.0f - u;
                float travel = ShardDistance[i] * (1.0f - inv * inv * inv * inv);
                float radians = ShardAngle[i] * Mathf.Deg2Rad;
                int x = Mathf.RoundToInt(centerX + Mathf.Cos(radians) * travel);
                int y = Mathf.RoundToInt(centerY + Mathf.Sin(radians) * travel);

                int glyph = 0.4f > u ? GlyphPlus : (0.7f > u ? GlyphCross : GlyphDot);
                int tone = 0.3f > u ? 0 : (0.7f > u ? 1 : 2);
                DrawGlyph(glyph, x, y, tone);
            }
        }

        private void DrawTwinkles(int _pivotX, int _pivotY, float _k, float _leave)
        {
            Vector4 b = data.Get(entryIndex).bounds;

            // 등장 가장자리 반짝임
            for (int i = 0; i < TwinkleStart.Length; i++)
            {
                float a = elapsed - TwinkleStart[i];
                if (0.0f > a || TwinkleLife < a) continue;

                float u = a / TwinkleLife;
                int glyph = 0.15f > u ? GlyphDot : (0.4f > u ? GlyphPlus : (0.6f > u ? GlyphPlus5 : (0.8f > u ? GlyphCross : GlyphDot)));
                EdgePoint(b, TwinkleFx[i], TwinkleFy[i], out int x, out int y);
                x += _pivotX;
                y += _pivotY;
                DrawGlyph(glyph, x, y, 1);
            }

            // 착지 우상단 반짝임
            if (0.0f <= _k && 0.3f > _k)
            {
                float u = _k / 0.3f;
                int glyph = 0.3f > u ? GlyphPlus5 : (0.6f > u ? GlyphPlus : GlyphDot);
                EdgePoint(b, 1.1f, -0.1f, out int x, out int y);
                x += _pivotX;
                y += _pivotY;
                DrawGlyph(glyph, x, y, 1);
            }

            // 유지 반짝임 순환
            if (HoldDetailDelay < _k && 0.5f < _leave)
            {
                float hk = _k - HoldDetailDelay;
                for (int i = 0; i < HoldTwinkleFx.Length; i++)
                {
                    float c = Mathf.Repeat(hk + HoldTwinkleOffset[i], HoldTwinkleCycle);
                    if (HoldTwinkleLife < c) continue;

                    float u = c / HoldTwinkleLife;
                    int glyph = (0.2f > u || 0.8f < u) ? GlyphDot : ((0.4f > u || 0.6f < u) ? GlyphPlus : GlyphPlus5);
                    EdgePoint(b, HoldTwinkleFx[i], HoldTwinkleFy[i], out int x, out int y);
                    x += _pivotX;
                    y += _pivotY;
                    DrawGlyph(glyph, x, y, 1);
                }
            }
        }

        // 기둥을 타고 오르는 유지 알갱이(아이템 위 6px부터)
        private void DrawHoldMotes(int _centerX, int _centerY, float _k, float _leave)
        {
            if (HoldDetailDelay >= _k || 0.5f > _leave) return;

            float hk = _k - HoldDetailDelay;
            for (int i = 0; i < HoldMoteCount; i++)
            {
                float d = Mathf.Repeat(hk * moteSpeed[i] + motePhase[i] * 9.0f, 60.0f);
                int glyph = 35.0f > d ? (0 != (i & 1) ? GlyphPlus : GlyphDot) : GlyphDot;
                DrawGlyph(glyph, _centerX + Mathf.RoundToInt(moteOffset[i]), _centerY + 6 + Mathf.RoundToInt(d), 20.0f > d ? 0 : 1);
            }
        }

        // 실루엣 경계 비율 위치(가로 0 = 왼쪽, 세로 0 = 위쪽)를 아이템 기준 로컬 px로
        private static void EdgePoint(in Vector4 _bounds, float _fx, float _fy, out int _x, out int _y)
        {
            _x = Mathf.RoundToInt(_bounds.x + (_bounds.z - _bounds.x) * _fx);
            _y = Mathf.RoundToInt(_bounds.w - (_bounds.w - _bounds.y) * _fy);
        }

        // 입자 하나. 기본색 한 가지이고 `+` `x`만 가운데 한 칸을 한 단계 밝게 칠한다.
        private void DrawGlyph(int _glyph, int _x, int _y, int _tone)
        {
            Color32 color = toneColors[_tone];
            Color32 center = toneColors[Mathf.Max(0, _tone - 1)];

            switch (_glyph)
            {
                case GlyphPlus:
                    painter.Dot(_x, _y + 1, color);
                    painter.Dot(_x - 1, _y, color);
                    painter.Dot(_x, _y, center);
                    painter.Dot(_x + 1, _y, color);
                    painter.Dot(_x, _y - 1, color);
                    break;

                case GlyphPlus5:
                    painter.Dot(_x, _y + 2, color);
                    painter.Dot(_x, _y + 1, color);
                    painter.HLine(_x - 2, _x - 1, _y, color);
                    painter.Dot(_x, _y, center);
                    painter.HLine(_x + 1, _x + 2, _y, color);
                    painter.Dot(_x, _y - 1, color);
                    painter.Dot(_x, _y - 2, color);
                    break;

                case GlyphCross:
                    painter.Dot(_x - 1, _y + 1, color);
                    painter.Dot(_x + 1, _y + 1, color);
                    painter.Dot(_x, _y, center);
                    painter.Dot(_x - 1, _y - 1, color);
                    painter.Dot(_x + 1, _y - 1, color);
                    break;

                default:
                    painter.Dot(_x, _y, color);
                    break;
            }
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();

            bFirstFrame = true;
            bActive = false;
            elapsed = 0.0f;
            landTime = -1.0f;
            leaveTime = -1.0f;
            bAirborne = false;
            historyCount = 0;
            historyHead = 0;
            appliedSortingLayerID = int.MinValue;
            appliedSortingOrder = int.MinValue;
            addFrontVertexCount = -1;
            glowVertexCount = -1;
            trailLineVertexCount = -1;

            HideAll();
        }

        private void LateUpdate()
        {
            if (true == bFirstFrame)
            {
                Begin();
            }

            if (false == bActive) return;

            // 첫 프레임은 시간을 더하지 않아서 t=0 장면이 반드시 한 번 그려진다
            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;

            UpdatePhase();
            SyncSorting();

            // 위치는 반올림하지 않는다. 카메라가 픽셀 그리드에 스냅하지 않아 아이템 스프라이트가 소수 위치로 부드럽게 움직이므로,
            // 이펙트를 정수 px로 반올림하면 아이템과 0~1px씩 엇갈려 떨린다. 부드러운 빛은 소수 위치를 그대로 따라가고,
            // 아이템에 붙은 픽셀 층은 RebuildPixels에서 아이템 기준 로컬 px로 그린다.
            Vector3 pivot = itemRenderer.transform.position;
            float pivotX = pivot.x * PixelsPerUnit;
            float pivotY = pivot.y * PixelsPerUnit;
            float centerX = pivotX;
            float centerY = pivotY;
            if (0 <= entryIndex)
            {
                Vector4 b = data.Get(entryIndex).bounds;
                centerX = pivotX + (b.x + b.z + 1.0f) * 0.5f;
                centerY = pivotY + (b.y + b.w + 1.0f) * 0.5f;
            }

            // 바닥 = 루트 위치(착지하면 피벗과 같다)
            Vector3 ground = null != rootTransform ? rootTransform.position : pivot;
            float groundX = 0.0f <= landTime ? pivotX : ground.x * PixelsPerUnit;
            float groundY = 0.0f <= landTime ? pivotY : ground.y * PixelsPerUnit;

            RecordHistory(centerX, centerY);

            UpdateSoftLights(pivotX, pivotY, centerX, centerY, groundX, groundY);

            // 화면 밖이면 픽셀 메쉬 재구성을 건너뛴다(스프라이트는 렌더러 컬링이 처리)
            Vector3 cullCenter = new Vector3(groundX * PixelUnit, (groundY + 90.0f) * PixelUnit, ground.z);
            Vector3 cullSize = new Vector3(160.0f * PixelUnit + 2.0f * PixelVfxCulling.MarginUnits, 300.0f * PixelUnit + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(cullCenter, cullSize))
            {
                        addFrontVertexCount = -1;
                glowVertexCount = -1;
                trailLineVertexCount = -1;
                if (null != addFrontRenderer && true == addFrontRenderer.enabled) addFrontRenderer.enabled = false;
                if (null != glowRenderer && true == glowRenderer.enabled) glowRenderer.enabled = false;
                if (null != trailLineRenderer && true == trailLineRenderer.enabled) trailLineRenderer.enabled = false;
                return;
            }

            RebuildPixels(pivotX, pivotY);
        }

        private void OnDisable()
        {
            HideAll();
        }

        private void OnDestroy()
        {
            DestroyMesh(ref addFrontMesh);
            DestroyMesh(ref glowMesh);
            DestroyMesh(ref trailLineMesh);
        }

        private static void DestroyMesh(ref Mesh _mesh)
        {
            if (null == _mesh) return;
            Destroy(_mesh);
            _mesh = null;
        }

        private void OnValidate()
        {
            glowScale = Mathf.Clamp(glowScale, 0.0f, 2.0f);
            beamMoteGlow.x = Mathf.Clamp01(beamMoteGlow.x);
            beamMoteGlow.y = Mathf.Clamp01(beamMoteGlow.y);
            beamMoteGlow.z = Mathf.Clamp01(beamMoteGlow.z);

#if UNITY_EDITOR
            if (true == bInitialized && true == bActive)
            {
                coreColor = goldCoreColor;
                mainColor = goldMainColor;
                deepColor = goldDeepColor;
                BuildTones();
                ApplyOutlineColor();
            }
#endif
        }
    }
}
