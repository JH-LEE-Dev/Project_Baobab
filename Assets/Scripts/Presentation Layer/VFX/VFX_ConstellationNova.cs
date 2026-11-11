using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 별자리 발현의 "픽셀 각인 마법진" 연출. 별자리 중심의 바닥에 마법진 전체가 0프레임에 흰색으로 한 번에 각인되고
    /// 팔레트 한 단계씩 식으며, 남은 기운(빛 머리, 룬 점등, 여진)이 돌다가 바깥부터 꺼진다. 승인된 시안 3(타원 1개 + 부드러운 빛)을 그대로 옮겼다.
    /// 스프라이트 시트 대신 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다(PixelPainter / PixelQuadBuffer).
    ///
    /// 구성(반지름 R = 반지름 유닛 x 32px, 기준 128px)
    /// - 바닥 메쉬(ShockWave 레이어, 나무/캐릭터 아래): 마법진 픽셀. 바깥 링 타원 1개(R, R/2), 팔각별(마름모 + 직사각형),
    ///   직사각형 모서리 눈금 4곳, 중심 마름모, 세운 5x5 룬 12개(반지름 0.9R). 모든 도형이 좌우 대칭이다.
    /// - 공중 메쉬(루트 렌더러 소팅): 중심 4갈래 별 + 1px 섬광 줄, 꼭짓점 8곳 스파크, 떠오르는 입자(`+` `x` `.`), 해체 입자.
    /// - 소프트 메쉬(루트 + 1, 가산 NovaSoftGlow): 임팩트 섬광, 바닥 빛, 빛 커튼, 빛기둥. 정점 색 보간 그라데이션이다.
    ///
    /// 밝기는 반투명 페이드 없이 팔레트 5단계(꺼짐/남색/파랑/하늘/흰색)와 BrandWrapGlow 정점 알파 발광(GlowTable)으로만 낸다.
    /// 정점 색은 sRGB 팔레트를 선형 값으로 바꿔 넣는다(선형 색 공간에서 화면에 팔레트 색 그대로 나오도록).
    /// 모든 위치는 경과 시간만으로 닫힌 식으로 계산하고(적분 없음), 픽셀 부분은 30fps 단위로 갱신한다.
    /// 마법진 픽셀 표는 반지름이 같으면 모든 인스턴스가 공유하고, 정적 버퍼와 고정 크기 배열만 써서 재생 중 힙 할당이 없다.
    ///
    /// 모양: shape = Ellipse(던전, 위 구성 그대로) 또는 ArenaDiamond(로그라이크 아레나 사각형 노바). ArenaDiamond는 같은 팔레트/타임라인/발광 규칙으로
    /// 아레나 가장자리를 따르는 2:1 마름모(반폭 A, 반높이 A/2) 테두리 + 내접 직사각형 + 룬 10개 + 좌우 꼭짓점 별 각인을 그리고,
    /// 빛 커튼은 마름모 둘레(높이 고정 46px), 바닥 빛은 마름모 채움이다. 범위는 SetArenaExtents(보이는 가장자리 반폭/반높이, 유닛)로 정한다.
    ///
    /// 범위 조절: maxRadius(월드 유닛) 또는 재생 직전의 SetRadius로 정한다. 풀 인스턴스로 쓰이며 루트 ParticleSystem의
    /// 수명(TotalDuration 이상)이 끝나면 VFXComponent가 풀로 돌려보낸다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_ConstellationNova : MonoBehaviour
    {
        /// <summary>마법진 팔레트. Star: 파랑 선 + 금빛 룬, Gold: 금빛 선 + 얼음빛 룬, Violet: 보라 선 + 금빛 룬</summary>
        public enum NovaPalette
        {
            Star = 0,
            Gold = 1,
            Violet = 2,
        }

        /// <summary>바닥 도형 모양. Ellipse: 던전 별자리 발현(원형 마법진), ArenaDiamond: 로그라이크 아레나 가장자리를 따르는 2:1 마름모 마법진</summary>
        public enum ENovaShape
        {
            Ellipse = 0,
            ArenaDiamond = 1,
        }

        [Header("모양")]
        [SerializeField, Tooltip("마법진 모양. Ellipse는 던전판(기본), ArenaDiamond는 로그라이크 아레나 사각형(마름모) 노바.")]
        private ENovaShape shape = ENovaShape.Ellipse;
        [SerializeField, Tooltip("ArenaDiamond 기본 반폭(월드 유닛). 아레나 보이는 가장자리 기준이며 재생 직전에 SetArenaExtents로 덮어쓸 수 있다.")]
        private float arenaHalfWidth = 9.5f;
        [SerializeField, Tooltip("ArenaDiamond 기본 반높이(월드 유닛). 마름모 비율은 항상 2:1이라 반폭이 없을 때만 반폭 = 2 x 반높이로 쓴다.")]
        private float arenaHalfHeight = 4.75f;
        [SerializeField, Tooltip("ArenaDiamond 빛 커튼의 최대 높이(px). 반지름 비율로 키우지 않고 고정이다(키우면 화면을 덮는다).")]
        private float arenaCurtainHeightPx = 46.0f;

        [Header("범위 (월드 유닛, 32 PPU 기준 1유닛 = 32px)")]
        [SerializeField, Tooltip("바깥 마법진 링이 닿는 반지름. 눈에 보이는 폭발 범위이자 피격 범위와 맞추는 값이다. 재생 직전에 SetRadius로 덮어쓸 수 있다.")]
        private float maxRadius = 4.0f;

        [Header("색")]
        [SerializeField, Tooltip("마법진 팔레트(선 색 + 룬/강조 색). 기본은 별빛 파랑 + 금빛 룬.")]
        private NovaPalette palette = NovaPalette.Star;

        [Header("부드러운 빛")]
        [SerializeField, Tooltip("임팩트 섬광, 바닥 빛, 빛 커튼, 빛기둥(소프트 메쉬) 전체 켜기/끄기")]
        private bool bSoftLight = true;
        [SerializeField, Tooltip("빛 커튼의 최대 높이(px, 반지름 128px 기준). 실제 반지름에 비례해 커진다.")]
        private float curtainHeightPx = 74.0f;
        [SerializeField, Tooltip("빛기둥의 최대 높이(px, 반지름 128px 기준). 실제 반지름에 비례해 커진다.")]
        private float pillarHeightPx = 130.0f;

        [Header("정렬")]
        [SerializeField, Tooltip("공중 메쉬를 루트 렌더러(VFXComponent가 정한 소팅)보다 이 값만큼 위에 그린다. 소프트 메쉬는 여기에 1을 더한다.")]
        private int sortingOrderOffset = 0;

        [Header("바닥 메쉬 (마법진이 나무/캐릭터 뒤에 그려지도록 분리)")]
        [SerializeField, Tooltip("마법진을 그리는 바닥 메쉬. 프리팹의 GroundMesh 자식에 연결한다. 비어 있으면 모두 공중 메쉬 하나로 그린다.")]
        private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField, Tooltip("바닥 메쉬 소팅 레이어. 나무/캐릭터가 있는 Objects보다 아래여야 한다. 도끼 충격파와 같은 ShockWave가 기본이다.")]
        private string groundSortingLayerName = "ShockWave";
        [SerializeField, Tooltip("바닥 메쉬 소팅 오더")]
        private int groundSortingOrder = 0;

        [Header("소프트 메쉬 (부드러운 빛, 가산)")]
        [SerializeField, Tooltip("부드러운 빛을 그리는 소프트 메쉬. 프리팹의 SoftMesh 자식에 연결한다(NovaSoftGlow 머티리얼).")]
        private MeshFilter softMeshFilter;
        [SerializeField] private MeshRenderer softMeshRenderer;

        // 바닥 타원의 세로 비율(쿼터뷰, 2:1 아이소메트릭). 마법진 그림과 별자리 발현 폭발 판정(GetGroundDistance)이 같이 쓰는 값이라
        // 인스펙터로 노출하지 않는다 - 한쪽만 바뀌면 보이는 범위와 피격 범위가 어긋난다.
        public const float GroundSquashY = 0.5f;

        /// <summary>소프트 메쉬 정점 알파에 실리는 세기를 곱하는 HDR 배율. 머티리얼의 _Glow와 같은 값이어야 한다(빌더가 맞춘다).</summary>
        public const float SoftGlowMultiplier = 2.0f;

        private const float PixelsPerUnit = 32.0f;
        private const float PixelUnit = 1.0f / 32.0f;
        private const float TwoPi = Mathf.PI * 2.0f;
        private const float FramesPerSecond = 30.0f;

        // 전체 타임라인(초). 루트 ParticleSystem 수명은 이 값 이상이어야 한다.
        private const float TimelineLength = 2.8f;
        // 마지막 입자가 사라지는 시각 이후에는 그릴 것이 없다
        private const float ContentEnd = 2.4f;
        // 피격 파동 시간(EvaluateRadius / EvaluateShockArrivalTime). 화면 연출이 아니라 판정 타이밍 기준이다.
        private const float ShockDuration = 0.4f;

        // 기준 반지름(px). 높이 같은 크기 값은 실제 반지름과 이 값의 비율로 맞춘다.
        private const float ReferenceRadiusPx = 128.0f;

        // 해체: 바깥(반지름 1)부터 DissolveStart에 시작하고 안쪽은 DissolveSweep만큼 늦게 시작한다. 0.1초마다 팔레트 한 단계 꺼진다.
        private const float DissolveStart = 1.2f;
        private const float DissolveSweep = 0.4f;

        // 마법진 픽셀 그룹
        private const int GroupRune = 0;
        private const int GroupOct = 1;
        private const int GroupOct2 = 2;
        private const int GroupTick = 3;
        private const int GroupCore = 4;
        private const int GroupRing = 5;

        // 팔레트 램프(선 / 강조), 단계(0 꺼짐, 1 남색, 2 파랑, 3 하늘, 4 흰색)
        private const int RampLine = 0;
        private const int RampAcc = 1;
        private const int LevelCount = 5;

        private const int SigilCapacity = 8192;
        private const int RuneCount = 12;
        private const int MoteCount = 54;
        private const int DissolveCapacity = 1024;
        private const int QuadCapacityGround = 2048;
        private const int QuadCapacitySky = 1024;

        // 아레나 마름모: 마법진 약 2,330칸(바닥 쿼드 최대 약 1,650, 공중 최대 약 560 실측)이라 위 용량으로 충분하다
        private const int ArenaRuneCount = 10;
        private const int ArenaMoteCount = 80;
        private const int ArenaEdgeInset = 4;
        private const int ArenaCoreHalf = 24;
        private const float ArenaReferenceA = 300.0f;
        private const int GroupMark = 6;

        // 소프트 메쉬 구성(정점 수 / 인덱스 수는 반지름과 무관한 고정값이라 메쉬 형태를 한 번만 만든다)
        // 부분 순서 = 끝나는 시각 순서(섬광, 기둥, 커튼, 바닥 빛). 그래서 살아 있는 부분은 항상 뒤쪽 연속 구간이다.
        private const int SoftSegments = 48;
        private const int FlashRows = 6;
        private const int PillarRows = 6;
        private const int PillarCols = 7;
        private const int CurtainRows = 6;
        private const int GroundRows = 5;
        private const int PartFlash = 0;
        private const int PartPillar = 1;
        private const int PartCurtain = 2;
        private const int PartGround = 3;
        private const int ArenaSegmentsPerEdge = SoftSegments / 4;

        private const int FlashVertexStart = 0;
        private const int PillarVertexStart = FlashVertexStart + FlashRows * SoftSegments;
        private const int CurtainVertexStart = PillarVertexStart + PillarRows * PillarCols;
        private const int GroundVertexStart = CurtainVertexStart + CurtainRows * SoftSegments;
        private const int SoftVertexTotal = GroundVertexStart + GroundRows * SoftSegments;

        private const int FlashIndexStart = 0;
        private const int PillarIndexStart = FlashIndexStart + (FlashRows - 1) * SoftSegments * 6;
        private const int CurtainIndexStart = PillarIndexStart + (PillarRows - 1) * (PillarCols - 1) * 6;
        private const int GroundIndexStart = CurtainIndexStart + (CurtainRows - 1) * SoftSegments * 6;
        private const int SoftIndexTotal = GroundIndexStart + (GroundRows - 1) * SoftSegments * 6;

        // 소프트 부분이 끝나는 시각(초)
        private const float FlashEnd = 0.16f;
        private const float PillarEnd = 0.5f;
        private const float CurtainEnd = 0.6f;
        private const float GroundEnd = DissolveStart + 0.5f;

        // 빛 커튼 최대 높이 중 처음부터 있는 몫(34 / 74)
        private const float CurtainBaseFraction = 34.0f / 74.0f;
        // 빛 커튼 좌우 끝 겹침 배율 상한
        private const float CurtainStackMax = 10.0f;

        // 룬 번호 -> 글리프 번호. 0번(정면)과 6번(뒤쪽)은 단독, 1~5번과 11~7번은 같은 룬을 좌우 짝으로 둔다.
        private static readonly int[] RuneOf = { 0, 1, 2, 3, 4, 5, 6, 5, 4, 3, 2, 1 };

        // 손으로 그린 5x5 룬 7종. 전부 가운데 열 기준 좌우 대칭이다(첫 줄이 위).
        private static readonly string[][] Glyphs =
        {
            new[] { "..#..", ".#.#.", "#.#.#", ".#.#.", "..#.." },
            new[] { "..#..", ".###.", "#.#.#", "..#..", "..#.." },
            new[] { "#.#.#", "#.#.#", ".###.", "..#..", "..#.." },
            new[] { ".###.", "#...#", "#.#.#", "#...#", ".###." },
            new[] { "#####", ".#.#.", "..#..", ".#.#.", "#####" },
            new[] { "#...#", ".#.#.", "..#..", "..#..", ".###." },
            new[] { "..#..", "#.#.#", "#####", "#.#.#", "..#.." },
        };

        // 아레나 룬 기준 표(시안 A=300 기준 px, y는 시안 좌표라 아래가 +). 한 줄이 한 변 위치이고 x가 0이 아니면 좌우 거울 복사한다.
        // 순서: 아래 꼭짓점 근처, 아래 변 바깥/안쪽, 위 변 안쪽/바깥, 위 꼭짓점 근처
        private static readonly int[] ArenaRuneBaseX = { 0, 202, 68, 68, 202, 0 };
        private static readonly int[] ArenaRuneBaseY = { 134, 34, 101, -101, -34, -134 };
        private static readonly int[] ArenaRuneBaseGlyph = { 0, 1, 2, 3, 4, 6 };

        // 4갈래 별 팔 길이(7, 5, 3, 1)별 반두께 표(손으로 정한 값)
        private static readonly int[] StarArms = { 7, 5, 3, 1 };
        private static readonly int[][] StarHalfThickness =
        {
            new[] { 2, 2, 2, 1, 1, 0, 0, 0 },
            new[] { 1, 1, 1, 0, 0, 0 },
            new[] { 1, 0, 0, 0 },
            new[] { 0, 0 },
        };

        // 발광 세기(정점 알파) 단계표: 꺼짐, 남색, 파랑, 하늘, 흰색
        private static readonly float[] GlowTable = { 0.0f, 0.04f, 0.12f, 0.26f, 0.62f };

        // sRGB 팔레트 램프(단계 1~4, 0번은 쓰지 않음)
        private static readonly Color32[] RampBlue = { new Color32(0, 0, 0, 0), new Color32(40, 52, 140, 255), new Color32(72, 120, 255, 255), new Color32(160, 222, 255, 255), new Color32(255, 255, 255, 255) };
        private static readonly Color32[] RampGold = { new Color32(0, 0, 0, 0), new Color32(130, 60, 30, 255), new Color32(232, 136, 48, 255), new Color32(255, 206, 92, 255), new Color32(255, 247, 220, 255) };
        private static readonly Color32[] RampViolet = { new Color32(0, 0, 0, 0), new Color32(64, 34, 130, 255), new Color32(150, 96, 255, 255), new Color32(222, 186, 255, 255), new Color32(255, 250, 255, 255) };
        private static readonly Color32[] RampIce = { new Color32(0, 0, 0, 0), new Color32(40, 70, 150, 255), new Color32(90, 170, 255, 255), new Color32(180, 236, 255, 255), new Color32(255, 255, 255, 255) };

        // 소프트 빛 색 비율(정점 RGB)
        private static readonly Vector3 FlashTint = new Vector3(0.6f, 0.8f, 1.0f);
        private static readonly Vector3 GroundTint = new Vector3(0.25f, 0.5f, 1.0f);
        private static readonly Vector3 CurtainTint = new Vector3(0.5f, 0.78f, 1.0f);
        private static readonly Vector3 PillarTint = new Vector3(0.55f, 0.8f, 1.0f);

        // 소프트 부분별 행 표본. 값의 곡선이 부드러워 보이도록 정점 사이 선형 보간이 곡선에 가깝게 나오는 위치를 골랐다.
        private static readonly float[] FlashRowD = { 0.0f, 0.35f, 0.7f, 1.0f, 1.4f, 2.0f };
        private static readonly float[] GroundRowR = { 0.0f, 0.5f, 0.8f, 1.0f, 1.05f };
        // 아레나 바닥 빛 행(마름모 거리). 세기 0.12 + 0.88 r^3을 정점 선형 보간으로 따라가도록 바깥쪽에 촘촘히 두고 마지막 행이 테두리다.
        private static readonly float[] ArenaGroundRowR = { 0.0f, 0.55f, 0.8f, 0.95f, 1.0f };
        private static readonly float[] PillarRowFraction = { 0.0f, 0.0f, 0.2f, 0.45f, 0.7f, 1.0f };
        private static readonly float[] PillarColumnC = { -2.4f, -1.6f, -0.8f, 0.0f, 0.8f, 1.6f, 2.4f };
        private static readonly float[] CurtainRowFraction = { 0.0f, 0.0f, 0.2f, 0.45f, 0.75f, 1.0f };

        private static readonly int[] PartVertexStart = { FlashVertexStart, PillarVertexStart, CurtainVertexStart, GroundVertexStart };
        private static readonly int[] PartIndexStart = { FlashIndexStart, PillarIndexStart, CurtainIndexStart, GroundIndexStart };

        private static readonly float[] SegmentCos = BuildSegmentTable(true);
        private static readonly float[] SegmentSin = BuildSegmentTable(false);
        private static readonly float[] CurtainStack = BuildCurtainStack();
        private static readonly ushort[] SoftIndices = BuildSoftIndices();
        private static readonly VertexAttributeDescriptor[] SoftLayout =
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3),
            new VertexAttributeDescriptor(VertexAttribute.Color, VertexAttributeFormat.Float32, 4),
        };
        private const MeshUpdateFlags SoftUploadFlags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontResetBoneBounds | MeshUpdateFlags.DontRecalculateBounds;

        private struct StarCell
        {
            public sbyte dx;
            public sbyte dy;
            public byte level;
        }

        // 마법진 픽셀 하나. 좌표는 중심 기준 정수 오프셋(y가 위)이다.
        private struct SigilPixel
        {
            public short x;
            public short y;
            public byte group;
            public byte rest;
            public byte ramp;
            public sbyte rune;
            public float r;
            public float a;
            public float h;
            public float dissolveStart;
        }

        private struct Mote
        {
            public int ox;
            public int oy;
            public int ramp;
            public int shape;
            public float t0;
            public float life;
            public float rise;
            public float sway;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SoftVertex
        {
            public Vector3 position;
            public Color color;
        }

        private static readonly StarCell[][] StarSprites = BuildStarSprites();

        // 마법진 픽셀 표: 반지름(px)이 같으면 모든 인스턴스가 공유한다(메인 스레드 단일 실행). 행(y) -> 열(x) 순서로 정렬되어 있다.
        private static readonly SigilPixel[] sigilPending = new SigilPixel[SigilCapacity];
        private static readonly SigilPixel[] sigil = new SigilPixel[SigilCapacity];
        private static readonly int[] ringIndices = new int[SigilCapacity];
        private static readonly int[] dissolveIndices = new int[DissolveCapacity];
        private static readonly int[] runeGlyphArena = new int[RuneCount];
        private static readonly int[] runeCenterX = new int[RuneCount];
        private static readonly int[] runeCenterY = new int[RuneCount];
        private static readonly float[] runeCenterA = new float[RuneCount];
        private static int[] sigilGrid = new int[0];
        private static int sigilCount;
        private static int pendingCount;
        private static int ringCount;
        private static int dissolveCount;
        // 표 키: Ellipse는 반지름(px, 양수), ArenaDiamond는 -(반폭 A px). 키가 같으면 모든 인스턴스가 표를 공유한다.
        private static int sigilRadius = 0;
        private static int runeTotal = RuneCount;
        private static bool buildIsArena;
        private static int buildA;
        private static int buildB;
        private static int buildRadius;
        private static int gridExtentX;
        private static int gridExtentY;
        private static int gridWidth;

        // 쿼드는 바닥 / 공중을 차례로 하나의 공용 버퍼로 채워 올린다(메인 스레드 단일 실행, 매 번 Clear 후 채운다).
        private static readonly PixelQuadBuffer quadBuffer = new PixelQuadBuffer(QuadCapacityGround);
        private static readonly PixelPainter painter = new PixelPainter();
        private static readonly SoftVertex[] softScratch = new SoftVertex[SoftVertexTotal];

        // 내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Mesh groundMesh;
        private Mesh softMesh;
        private ParticleSystemRenderer rootParticleRenderer;

        private readonly Mote[] motes = new Mote[ArenaMoteCount];
        // 팔레트 x 램프 x 단계 색표(정점 색 = 선형 팔레트, 알파 = 발광 세기)
        private readonly Color32[] colorTable = new Color32[2 * LevelCount];

        // 상태 변수
        private float elapsed;
        private bool bFirstFrame;
        private bool bBeginPending;
        private bool bInitialized;
        private float overrideRadius;
        private float overrideHalfWidth;
        private float overrideHalfHeight;
        private bool bArena;
        private int arenaA;
        private int arenaB;
        private int sigilKey;
        private int moteCount;
        private float ringHalo;
        private float currentRadius;
        private float radiusPx;
        private float sizeScale;
        private int ringRadiusX;
        private int ringRadiusY;
        private int centerX;
        private int centerY;
        private uint randomState;
        private Vector3 meshBoundsSize = new Vector3(20.0f, 20.0f, 10.0f);

        // 30fps 프레임 상태(프레임이 바뀔 때만 계산)
        private int builtFrame = -1;
        private float frameTime;
        private int cooledLevel;
        private int baseLevel2;
        private int baseLevel3;
        private int ringAmp;
        private float ringPhase;
        private bool bRuneActive;
        private float runePhase;
        private float innerWave0;
        private float innerWave1;

        private int groundVertexCount = -1;
        private ulong groundHash;
        private int skyVertexCount = -1;
        private ulong skyHash;

        private int groundSortingLayerId;
        private int appliedSoftFirstPart = -1;

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

        /// <summary>
        /// ArenaDiamond 전용: 이번 재생의 아레나 보이는 가장자리 반폭/반높이(월드 유닛)를 정한다. 풀에서 꺼내져 활성화된 직후(첫 LateUpdate 전)에 호출해야 한다.
        /// 마법진 테두리는 가장자리보다 4px 안쪽, 반폭 A = 짝수 px(반폭 x 32 - 4), 반높이 = A / 2로 맞춰 정확한 2:1 계단을 유지한다.
        /// 반폭이 0 이하일 때만 반높이 x 2를 반폭으로 쓰고, 둘 다 0 이하이면 인스펙터 값을 쓴다. Ellipse 모양에서는 아무 영향이 없다.
        /// </summary>
        public void SetArenaExtents(float _halfWidthUnits, float _halfHeightUnits)
        {
            overrideHalfWidth = _halfWidthUnits;
            overrideHalfHeight = _halfHeightUnits;
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

        /// <summary>
        /// 충격파 선두가 반지름 비율 _normalizedDistance(0~1)에 닿는 시각(초)을 돌려준다. EvaluateRadius의 역함수라
        /// 반지름과 무관하고 인스턴스 없이 쓸 수 있다. 피격을 파동에 맞춰 바깥으로 순서대로 적용할 때 쓴다.
        /// </summary>
        public static float EvaluateShockArrivalTime(float _normalizedDistance)
        {
            // EaseOutCubic(x) = 1 - (1 - x)^3 의 역함수: x = 1 - (1 - 비율)^(1/3)
            float remaining = 1.0f - Mathf.Clamp01(_normalizedDistance);
            return ShockDuration * (1.0f - Mathf.Pow(remaining, 1.0f / 3.0f));
        }

        /// <summary>
        /// 쿼터뷰 바닥 거리(월드 유닛, 마법진 타원의 가로 반지름 기준). 세로 차이를 GroundSquashY로 나눠 바닥 타원을 원으로 편 거리라,
        /// 이 값이 반지름 이하이면 마법진 타원 안이다. 별자리 발현 폭발 판정과 별자리 생성 제약이 모두 이 함수를 써서
        /// 보이는 마법진과 실제 범위가 같은 비율을 쓰게 한다.
        /// </summary>
        public static float GetGroundDistance(Vector3 _a, Vector3 _b)
        {
            float dx = _a.x - _b.x;
            float dy = (_a.y - _b.y) / GroundSquashY;
            return Mathf.Sqrt(dx * dx + dy * dy);
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
            PixelQuadBuffer.ReserveMesh(mesh, QuadCapacitySky);
            meshFilter.sharedMesh = mesh;

            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            if (null != groundMeshFilter && null != groundMeshRenderer)
            {
                groundMesh = new Mesh { name = "VFX_ConstellationNova_Ground" };
                groundMesh.MarkDynamic();
                PixelQuadBuffer.ReserveMesh(groundMesh, QuadCapacityGround);
                groundMeshFilter.sharedMesh = groundMesh;

                groundMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                groundMeshRenderer.receiveShadows = false;
                groundMeshRenderer.enabled = false;

                groundSortingLayerId = SortingLayer.NameToID(groundSortingLayerName);
            }

            if (null != softMeshFilter && null != softMeshRenderer)
            {
                softMesh = new Mesh { name = "VFX_ConstellationNova_Soft" };
                softMesh.MarkDynamic();
                softMesh.SetVertexBufferParams(SoftVertexTotal, SoftLayout);
                softMesh.SetIndexBufferParams(SoftIndexTotal, IndexFormat.UInt16);
                softMesh.SetIndexBufferData(SoftIndices, 0, 0, SoftIndexTotal, SoftUploadFlags);
                softMesh.subMeshCount = 1;
                softMesh.SetSubMesh(0, new SubMeshDescriptor(0, 0, MeshTopology.Triangles), SoftUploadFlags);
                softMeshFilter.sharedMesh = softMesh;

                softMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                softMeshRenderer.receiveShadows = false;
                softMeshRenderer.enabled = false;
            }
        }

        // 풀에서 꺼내져 활성화되면 반지름이 정해질 시간을 주기 위해 시작을 첫 LateUpdate로 미룬다.
        private void Begin()
        {
            EnsureInitialized();

            elapsed = 0.0f;
            bFirstFrame = true;
            builtFrame = -1;
            groundVertexCount = -1;
            groundHash = 0UL;
            skyVertexCount = -1;
            skyHash = 0UL;
            appliedSoftFirstPart = -1;

            bArena = ENovaShape.ArenaDiamond == shape;

            // 메쉬 경계(화면 밖 판단용): 가장 먼 가로 거리와 솟구침 높이 + 여유
            float halfWidthPx;
            float riseMaxPx;
            if (true == bArena)
            {
                float halfWidthUnits = 0.0f < overrideHalfWidth ? overrideHalfWidth : (0.0f < overrideHalfHeight ? overrideHalfHeight * 2.0f : arenaHalfWidth);
                arenaA = Mathf.Max(16, EvenRound(halfWidthUnits * PixelsPerUnit - ArenaEdgeInset));
                arenaB = arenaA / 2;
                currentRadius = arenaA * PixelUnit;
                radiusPx = arenaA;
                sizeScale = 1.0f;
                ringRadiusX = arenaA;
                ringRadiusY = arenaB;
                moteCount = ArenaMoteCount;
                ringHalo = 0.05f;

                // 위쪽: 꼭짓점 + 입자 솟구침/스파크(최대 약 66px) + 빛 커튼, 가운데: 빛기둥
                halfWidthPx = arenaA + 24.0f;
                riseMaxPx = Mathf.Max(arenaB + Mathf.Max(arenaCurtainHeightPx, 66.0f), pillarHeightPx) + 24.0f;
            }
            else
            {
                currentRadius = 0.0f < overrideRadius ? overrideRadius : maxRadius;
                currentRadius = Mathf.Max(0.5f, currentRadius);
                radiusPx = currentRadius * PixelsPerUnit;
                sizeScale = radiusPx / ReferenceRadiusPx;
                ringRadiusX = Mathf.Max(8, Mathf.RoundToInt(radiusPx));
                ringRadiusY = Mathf.Max(1, Mathf.RoundToInt(ringRadiusX * GroundSquashY));
                moteCount = MoteCount;
                ringHalo = 0.06f;

                halfWidthPx = Mathf.Max(ringRadiusX * 1.32f, 64.0f) + 16.0f;
                riseMaxPx = Mathf.Max(ringRadiusX * 0.66f, Mathf.Max(ringRadiusY + Mathf.Max(curtainHeightPx * sizeScale, 60.0f), pillarHeightPx * sizeScale)) + 24.0f;
            }

            meshBoundsSize = new Vector3(halfWidthPx * 2.0f * PixelUnit, riseMaxPx * 2.0f * PixelUnit, 10.0f);

            if (null != softMesh) softMesh.bounds = new Bounds(Vector3.zero, meshBoundsSize);

            Vector3 pos = transform.position;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            sigilKey = true == bArena ? -arenaA : ringRadiusX;
            EnsureSigilTable(sigilKey);
            BuildColorTable();
            SetupMotes();
        }

        // ---------- 난수 / 수학 ----------

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        // 격자 좌표 해시(0~1). 같은 값이면 같은 결과라 해체 입자로 풀릴 픽셀이 재생마다 같은 자리에서 정해진다.
        private static float Hash01(int _a, int _b)
        {
            uint h = unchecked((uint)(_a * 374761393) + (uint)(_b * 668265263));
            h = unchecked((h ^ (h >> 13)) * 1274126177u);
            h ^= h >> 16;
            return (float)(h / 4294967296.0);
        }

        private static float EaseOutCubic(float _t)
        {
            float inv = 1.0f - _t;
            return 1.0f - inv * inv * inv;
        }

        // 양수 쪽으로 반올림(0.5는 올림). 시안의 Math.round와 같은 규칙이라 대칭 좌표가 어긋나지 않는다.
        private static int RoundHalfUp(float _value)
        {
            return Mathf.FloorToInt(_value + 0.5f);
        }

        private static int EvenRound(float _value)
        {
            return 2 * Mathf.RoundToInt(_value * 0.5f);
        }

        private static float Wrap01(float _value)
        {
            return _value - Mathf.Floor(_value);
        }

        // ---------- 마법진 픽셀 표 (반지름별 1회) ----------

        // 키가 음수이면 아레나 마름모(반폭 = -키), 양수이면 타원(반지름 = 키)
        private static void EnsureSigilTable(int _key)
        {
            if (_key == sigilRadius) return;

            sigilRadius = _key;
            if (0 > _key) BuildArenaSigilTable(-_key);
            else BuildSigilTable(_key);
        }

        // 마법진 격자(중복 방지용)를 필요한 크기로 잡는다
        private static void PrepareSigilGrid(int _extentX, int _extentY)
        {
            gridExtentX = _extentX;
            gridExtentY = _extentY;
            gridWidth = gridExtentX * 2 + 1;
            int gridSize = gridWidth * (gridExtentY * 2 + 1);
            if (sigilGrid.Length < gridSize) sigilGrid = new int[gridSize];

            pendingCount = 0;
        }

        // 룬 글리프를 중심 기준으로 찍는다. 글리프 첫 줄이 위이므로 y를 뒤집는다.
        private static void AddRunePixels(int _count, bool _bArena)
        {
            for (int k = 0; k < _count; k++)
            {
                string[] glyph = Glyphs[true == _bArena ? runeGlyphArena[k] : RuneOf[k]];
                for (int row = 0; row < 5; row++)
                {
                    for (int col = 0; col < 5; col++)
                    {
                        if ('#' != glyph[row][col]) continue;
                        AddSigilPixel(runeCenterX[k] + col - 2, runeCenterY[k] - row + 2, GroupRune, 3, RampAcc, k);
                    }
                }
            }
        }

        // 격자를 행(아래에서 위) -> 열(왼쪽에서 오른쪽) 순서로 훑어 정렬된 표를 만든다(같은 줄 쿼드 병합에 유리). 훑으면서 격자를 비운다.
        private static void CollectSigilGrid()
        {
            sigilCount = 0;
            ringCount = 0;
            dissolveCount = 0;
            int gridHeight = gridExtentY * 2 + 1;
            for (int row = 0; row < gridHeight; row++)
            {
                int rowStart = row * gridWidth;
                for (int col = 0; col < gridWidth; col++)
                {
                    int cell = sigilGrid[rowStart + col];
                    if (0 == cell) continue;

                    sigilGrid[rowStart + col] = 0;
                    if (SigilCapacity <= sigilCount) continue;

                    sigil[sigilCount] = sigilPending[cell - 1];
                    if (GroupRing == sigil[sigilCount].group) ringIndices[ringCount++] = sigilCount;
                    if (0.05f >= sigil[sigilCount].h && dissolveCount < DissolveCapacity) dissolveIndices[dissolveCount++] = sigilCount;
                    sigilCount++;
                }
            }
        }

        // 아레나 마름모 마법진 표(반폭 A px, 반높이 A/2). 시안 renderSquare와 같은 구성이며 A=300 기준 위치를 비례해 옮긴다.
        // 순서(앞이 우선): 룬, 좌우 꼭짓점 별 각인, 테두리 마름모, 내접 직사각형, 중심 마름모
        private static void BuildArenaSigilTable(int _a)
        {
            int b = _a / 2;
            buildIsArena = true;
            buildA = _a;
            buildB = b;
            runeTotal = ArenaRuneCount;

            PrepareSigilGrid(_a + 8, b + 8);
            BuildArenaRuneCenters(_a);
            AddRunePixels(ArenaRuneCount, true);

            // 좌우 꼭짓점: 작은 4갈래 별(팔 3) 각인. 위치는 A=300 기준 280px를 비례해 옮긴다.
            int markX = RoundHalfUp(_a * 280.0f / ArenaReferenceA);
            StarCell[] mark = StarSprites[2];
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int i = 0; i < mark.Length; i++)
                {
                    AddSigilPixel(sx * markX + mark[i].dx, mark[i].dy, GroupMark, Mathf.Min(3, (int)mark[i].level), RampLine, -1);
                }
            }

            // 테두리 마름모(아레나 가장자리): 정확한 2칸-1칸 계단
            AddIsoLine(_a, 0, 0, b, GroupRing, 3);
            AddIsoLine(-_a, 0, 0, b, GroupRing, 3);
            AddIsoLine(_a, 0, 0, -b, GroupRing, 3);
            AddIsoLine(-_a, 0, 0, -b, GroupRing, 3);

            // 내접 직사각형(수평/수직): 꼭짓점이 테두리 변 중점에 닿아 팔각별처럼 읽힌다
            int rectY = b / 2;
            int rect = rectY * 2;
            for (int x = -rect; x <= rect; x++)
            {
                AddSigilPixel(x, -rectY, GroupOct2, 2, RampLine, -1);
                AddSigilPixel(x, rectY, GroupOct2, 2, RampLine, -1);
            }

            for (int y = -rectY; y <= rectY; y++)
            {
                AddSigilPixel(-rect, y, GroupOct2, 2, RampLine, -1);
                AddSigilPixel(rect, y, GroupOct2, 2, RampLine, -1);
            }

            // 중심 마름모
            AddIsoLine(ArenaCoreHalf, 0, 0, ArenaCoreHalf / 2, GroupCore, 2);
            AddIsoLine(-ArenaCoreHalf, 0, 0, ArenaCoreHalf / 2, GroupCore, 2);
            AddIsoLine(ArenaCoreHalf, 0, 0, -ArenaCoreHalf / 2, GroupCore, 2);
            AddIsoLine(-ArenaCoreHalf, 0, 0, -ArenaCoreHalf / 2, GroupCore, 2);

            CollectSigilGrid();
        }

        // 아레나 룬 10개의 중심. 기준 표(A=300)를 A/300 비례로 옮기고, 오른쪽(x > 0)만 계산해 왼쪽은 x 부호만 뒤집어 복사한다(픽셀 단위 좌우 대칭).
        private static void BuildArenaRuneCenters(int _a)
        {
            float scale = _a / ArenaReferenceA;
            int k = 0;
            for (int i = 0; i < ArenaRuneBaseX.Length; i++)
            {
                int cx = RoundHalfUp(ArenaRuneBaseX[i] * scale);
                int cySim = RoundHalfUp(ArenaRuneBaseY[i] * scale);
                for (int mirror = 0; mirror < 2; mirror++)
                {
                    if (1 == mirror && 0 == ArenaRuneBaseX[i]) break;

                    int x = 0 == mirror ? cx : -cx;
                    float a = Mathf.Atan2(cySim / GroundSquashY, x) / TwoPi;
                    runeCenterX[k] = x;
                    runeCenterY[k] = -cySim;
                    runeCenterA[k] = a - Mathf.Floor(a);
                    runeGlyphArena[k] = ArenaRuneBaseGlyph[i];
                    k++;
                }
            }
        }

        private static void BuildSigilTable(int _radius)
        {
            buildIsArena = false;
            runeTotal = RuneCount;
            buildRadius = _radius;
            int ry = Mathf.Max(1, Mathf.RoundToInt(_radius * GroundSquashY));
            int runeRadius = Mathf.RoundToInt(_radius * 0.9f);
            int diamond = Mathf.Max(2, EvenRound(_radius * 0.8f));
            int diamondY = diamond / 2;
            int rect = Mathf.Max(2, EvenRound(diamond * 0.707f));
            int rectY = rect / 2;
            int core = Mathf.Max(2, EvenRound(_radius * 0.156f));
            int coreY = core / 2;
            int tickEnd = Mathf.Max(4, Mathf.RoundToInt(8.0f * _radius / ReferenceRadiusPx));

            PrepareSigilGrid(_radius + 8, ry + 8);
            BuildRuneCenters(runeRadius);

            // 룬 먼저(우선순위)
            AddRunePixels(RuneCount, false);

            // 팔각별: 마름모(정확한 2칸-1칸 계단) + 직사각형(수평/수직)
            AddIsoLine(-diamond, 0, 0, diamondY, GroupOct, 3);
            AddIsoLine(diamond, 0, 0, diamondY, GroupOct, 3);
            AddIsoLine(-diamond, 0, 0, -diamondY, GroupOct, 3);
            AddIsoLine(diamond, 0, 0, -diamondY, GroupOct, 3);

            for (int x = -rect; x <= rect; x++)
            {
                AddSigilPixel(x, -rectY, GroupOct2, 2, RampLine, -1);
                AddSigilPixel(x, rectY, GroupOct2, 2, RampLine, -1);
            }

            for (int y = -rectY; y <= rectY; y++)
            {
                AddSigilPixel(-rect, y, GroupOct2, 2, RampLine, -1);
                AddSigilPixel(rect, y, GroupOct2, 2, RampLine, -1);
            }

            // 직사각형 모서리 바깥 2:1 눈금 4곳
            for (int sy = -1; sy <= 1; sy += 2)
            {
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    for (int i = 2; i <= tickEnd; i++)
                    {
                        AddSigilPixel(sx * (rect + i), sy * (rectY + i / 2), GroupTick, 3, RampLine, -1);
                    }
                }
            }

            // 중심 마름모
            AddIsoLine(-core, 0, 0, coreY, GroupCore, 2);
            AddIsoLine(core, 0, 0, coreY, GroupCore, 2);
            AddIsoLine(-core, 0, 0, -coreY, GroupCore, 2);
            AddIsoLine(core, 0, 0, -coreY, GroupCore, 2);

            // 바깥 링 타원 1개. PixelEllipse 윤곽에서 1사분면만 취해 네 사분면으로 거울 복사한다(좌우/상하 대칭 보장).
            int ringPoints = PixelEllipse.Build(_radius, ry);
            for (int i = 0; i < ringPoints; i++)
            {
                int x = PixelEllipse.OutX[i];
                int y = PixelEllipse.OutY[i];
                if (0 > x || 0 > y) continue;

                AddSigilPixel(x, y, GroupRing, 3, RampLine, -1);
                AddSigilPixel(-x, y, GroupRing, 3, RampLine, -1);
                AddSigilPixel(x, -y, GroupRing, 3, RampLine, -1);
                AddSigilPixel(-x, -y, GroupRing, 3, RampLine, -1);
            }

            CollectSigilGrid();
        }

        // 룬 12개의 중심. 0~6번(정면 -> 한쪽 -> 뒤쪽)만 계산하고 7~11번은 x 부호만 뒤집어 복사해 픽셀 단위 대칭을 보장한다.
        // 부동소수 반올림으로 57.5px 같은 자리가 1px 어긋나는 것을 막으려 1e-9를 더해 double로 계산한다(시안과 같은 식).
        private static void BuildRuneCenters(int _runeRadius)
        {
            for (int k = 0; k < RuneCount; k++)
            {
                int source = 6 >= k ? k : 12 - k;
                double angle = source / 12.0 * 6.283185307179586 + 1.5707963267948966;
                int cx = (int)System.Math.Floor(System.Math.Cos(angle) * _runeRadius + 1e-9 + 0.5);
                int cySim = (int)System.Math.Floor(System.Math.Sin(angle) * _runeRadius * 0.5 + 1e-9 + 0.5);
                if (0 == source || 6 == source) cx = 0;
                if (6 < k) cx = -cx;

                // 시안 좌표는 y가 아래라 룬 중심의 둘레 위치(시계 방향 0~1)도 시안 좌표로 계산한다. 저장은 y가 위인 좌표다.
                float a = Mathf.Atan2(cySim / GroundSquashY, cx) / TwoPi;
                runeCenterX[k] = cx;
                runeCenterY[k] = -cySim;
                runeCenterA[k] = a - Mathf.Floor(a);
            }
        }

        // 2:1 아이소 대각선. x가 한 칸 가는 동안 두 칸마다 y가 한 칸 바뀐다.
        private static void AddIsoLine(int _x0, int _y0, int _x1, int _y1, int _group, int _rest)
        {
            int steps = Mathf.Abs(_x1 - _x0);
            int sx = _x1 > _x0 ? 1 : -1;
            int sy = _y1 > _y0 ? 1 : (_y1 < _y0 ? -1 : 0);
            for (int i = 0; i <= steps; i++)
            {
                AddSigilPixel(_x0 + sx * i, _y0 + sy * (i / 2), _group, _rest, RampLine, -1);
            }
        }

        // 마법진 픽셀 하나를 임시 표에 넣는다. 이미 있는 자리면 먼저 넣은 것이 우선이다.
        private static void AddSigilPixel(int _x, int _y, int _group, int _rest, int _ramp, int _rune)
        {
            int col = _x + gridExtentX;
            int row = _y + gridExtentY;
            if (0 > col || col >= gridWidth || 0 > row || row >= gridExtentY * 2 + 1 || SigilCapacity <= pendingCount) return;

            int cellIndex = row * gridWidth + col;
            if (0 != sigilGrid[cellIndex]) return;

            // 시안 좌표(y가 아래)로 바꿔 둘레 위치와 해시를 계산한다
            int simY = -_y;
            float r;
            if (true == buildIsArena) r = Mathf.Min(1.0f, Mathf.Abs(_x) / (float)buildA + Mathf.Abs(simY) / (float)buildB);
            else r = Mathf.Min(1.0f, Mathf.Sqrt(_x * (float)_x + (simY / GroundSquashY) * (simY / GroundSquashY)) / buildRadius);
            float a = Mathf.Atan2(simY / GroundSquashY, _x) / TwoPi;
            a -= Mathf.Floor(a);
            if (0 <= _rune) a = runeCenterA[_rune];

            SigilPixel pixel = new SigilPixel();
            pixel.x = (short)_x;
            pixel.y = (short)_y;
            pixel.group = (byte)_group;
            pixel.rest = (byte)_rest;
            pixel.ramp = (byte)_ramp;
            pixel.rune = (sbyte)_rune;
            pixel.r = r;
            pixel.a = a;
            pixel.h = Hash01(_x * 7 + 3, simY * 13 + 5);
            pixel.dissolveStart = DissolveStart + (1.0f - r) * DissolveSweep;

            sigilPending[pendingCount] = pixel;
            pendingCount++;
            sigilGrid[cellIndex] = pendingCount;
        }

        // ---------- 정적 표 만들기 ----------

        private static float[] BuildSegmentTable(bool _bCos)
        {
            float[] table = new float[SoftSegments];
            for (int i = 0; i < SoftSegments; i++)
            {
                float angle = TwoPi * i / SoftSegments;
                table[i] = true == _bCos ? Mathf.Cos(angle) : Mathf.Sin(angle);
            }

            // 적도(0도, 180도)와 극(90도, 270도)은 부동소수 잡음 없이 정확한 값으로 둔다
            if (true == _bCos)
            {
                table[SoftSegments / 4] = 0.0f;
                table[SoftSegments * 3 / 4] = 0.0f;
            }
            else
            {
                table[0] = 0.0f;
                table[SoftSegments / 2] = 0.0f;
            }

            return table;
        }

        // 빛 커튼 둘레 정점별 겹침 배율. 시안은 링 픽셀마다 세로 기둥을 하나씩 세워 더하므로, 링이 세로로 서는 좌우 끝에서는
        // 한 열에 여러 기둥이 겹쳐 밝아진다(원통을 옆에서 본 두께). 링 접선 기울기(세로 / 가로)만큼 곱해 같은 밝기를 낸다.
        private static float[] BuildCurtainStack()
        {
            float[] stack = new float[SoftSegments];
            for (int i = 0; i < SoftSegments; i++)
            {
                float slope = GroundSquashY * Mathf.Abs(SegmentCos[i]) / Mathf.Max(0.001f, Mathf.Abs(SegmentSin[i]));
                stack[i] = Mathf.Clamp(slope, 1.0f, CurtainStackMax);
            }

            return stack;
        }

        private static StarCell[][] BuildStarSprites()
        {
            StarCell[][] sprites = new StarCell[StarArms.Length][];
            for (int s = 0; s < StarArms.Length; s++)
            {
                int arm = StarArms[s];
                int[] half = StarHalfThickness[s];
                int count = 0;
                for (int pass = 0; pass < 2; pass++)
                {
                    if (1 == pass) sprites[s] = new StarCell[count];
                    count = 0;

                    for (int dy = -arm; dy <= arm; dy++)
                    {
                        for (int dx = -arm; dx <= arm; dx++)
                        {
                            int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                            int m = Mathf.Min(Mathf.Abs(dx), Mathf.Abs(dy));
                            if (m > half[d]) continue;

                            // 중심 사각 모서리 깎기
                            if (3 <= arm && m == half[d] && 2 >= d && 2 == m && 2 == d) continue;

                            int e = Mathf.Abs(dx) + Mathf.Abs(dy);
                            int level = e <= Mathf.Max(1, arm >> 1) ? 4 : (d >= arm - (5 <= arm ? 1 : 0) ? 2 : 3);
                            if (1 >= arm) level = 0 == d ? 4 : 3;

                            if (1 == pass)
                            {
                                sprites[s][count].dx = (sbyte)dx;
                                sprites[s][count].dy = (sbyte)dy;
                                sprites[s][count].level = (byte)level;
                            }

                            count++;
                        }
                    }
                }
            }

            return sprites;
        }

        // 소프트 메쉬 인덱스: 부분마다 (행 x 열) 격자를 두 삼각형씩. 둘레 격자는 열이 끝에서 처음으로 이어진다.
        private static ushort[] BuildSoftIndices()
        {
            ushort[] indices = new ushort[SoftIndexTotal];
            int cursor = 0;
            AddGridIndices(indices, ref cursor, FlashVertexStart, FlashRows, SoftSegments, true);
            AddGridIndices(indices, ref cursor, PillarVertexStart, PillarRows, PillarCols, false);
            AddGridIndices(indices, ref cursor, CurtainVertexStart, CurtainRows, SoftSegments, true);
            AddGridIndices(indices, ref cursor, GroundVertexStart, GroundRows, SoftSegments, true);
            return indices;
        }

        private static void AddGridIndices(ushort[] _indices, ref int _cursor, int _vertexStart, int _rows, int _cols, bool _bWrap)
        {
            int colSteps = true == _bWrap ? _cols : _cols - 1;
            for (int row = 0; row < _rows - 1; row++)
            {
                for (int col = 0; col < colSteps; col++)
                {
                    int nextCol = (col + 1) % _cols;
                    int v00 = _vertexStart + row * _cols + col;
                    int v01 = _vertexStart + row * _cols + nextCol;
                    int v10 = _vertexStart + (row + 1) * _cols + col;
                    int v11 = _vertexStart + (row + 1) * _cols + nextCol;

                    _indices[_cursor++] = (ushort)v00;
                    _indices[_cursor++] = (ushort)v10;
                    _indices[_cursor++] = (ushort)v11;
                    _indices[_cursor++] = (ushort)v00;
                    _indices[_cursor++] = (ushort)v11;
                    _indices[_cursor++] = (ushort)v01;
                }
            }
        }

        // ---------- 색 / 입자 설정 ----------

        // sRGB 팔레트(0~255)를 선형 값으로 바꾼 뒤 8비트로 넣는다(선형 색 공간에서 화면에 팔레트 색 그대로 나온다).
        // 알파는 발광 세기 단계표(GlowTable)다.
        private static byte ToLinearByte(byte _srgb)
        {
            float c = _srgb / 255.0f;
            float linear = c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(linear) * 255.0f);
        }

        private void BuildColorTable()
        {
            Color32[] lineRamp = RampBlue;
            Color32[] accRamp = RampGold;
            if (NovaPalette.Gold == palette)
            {
                lineRamp = RampGold;
                accRamp = RampIce;
            }
            else if (NovaPalette.Violet == palette)
            {
                lineRamp = RampViolet;
                accRamp = RampGold;
            }

            for (int level = 0; level < LevelCount; level++)
            {
                byte alpha = (byte)Mathf.RoundToInt(GlowTable[level] * 255.0f);
                Color32 line = lineRamp[level];
                Color32 acc = accRamp[level];
                colorTable[RampLine * LevelCount + level] = new Color32(ToLinearByte(line.r), ToLinearByte(line.g), ToLinearByte(line.b), alpha);
                colorTable[RampAcc * LevelCount + level] = new Color32(ToLinearByte(acc.r), ToLinearByte(acc.g), ToLinearByte(acc.b), alpha);
            }
        }

        private Color32 GetColor(int _ramp, int _level)
        {
            return colorTable[_ramp * LevelCount + Mathf.Clamp(_level, 0, LevelCount - 1)];
        }

        // 떠오르는 입자(타원 54개, 아레나 80개): 3개 중 1개는 룬 위, 나머지는 바깥 링(아레나는 테두리) 픽셀에서 올라온다. 초반(제곱 분포)에 몰린다.
        private void SetupMotes()
        {
            for (int i = 0; i < moteCount; i++)
            {
                Mote mote = new Mote();
                if (0 == i % 3)
                {
                    int rune = Mathf.Min(runeTotal - 1, Mathf.FloorToInt(Rand01() * runeTotal));
                    mote.ox = runeCenterX[rune] + RoundHalfUp((Rand01() - 0.5f) * 4.0f);
                    mote.oy = runeCenterY[rune] + 3;
                    mote.ramp = RampAcc;
                }
                else
                {
                    int ring = 0 < ringCount ? Mathf.Min(ringCount - 1, Mathf.FloorToInt(Rand01() * ringCount)) : 0;
                    mote.ox = 0 < ringCount ? sigil[ringIndices[ring]].x : 0;
                    mote.oy = 0 < ringCount ? sigil[ringIndices[ring]].y : 0;
                    mote.ramp = RampLine;
                }

                float q = Rand01();
                mote.t0 = 0.03f + q * q * 1.05f;
                mote.life = 0.55f + Rand01() * 0.45f;
                mote.rise = 20.0f + Rand01() * 30.0f;
                mote.sway = Rand01() * 6.28f;
                mote.shape = Rand01() < 0.55f ? PixelPainter.ShapePlus : (Rand01() < 0.5f ? PixelPainter.ShapeCross : PixelPainter.ShapeDot);
                motes[i] = mote;
            }
        }

        // ---------- 픽셀 그리기 (30fps 프레임이 바뀔 때만) ----------

        // 프레임 상태를 계산한다. 시간 t는 30fps 프레임 시각으로 끊는다.
        private void PrepareFrame(int _frame)
        {
            frameTime = _frame / FramesPerSecond;
            float t = frameTime;

            // 0프레임에 전체가 흰색으로 박히고 3프레임마다 한 단계씩 기본 단계로 식는다
            int cooled = 4 - _frame / 3;
            cooledLevel = cooled;
            baseLevel2 = Mathf.Max(2, cooled);
            baseLevel3 = Mathf.Max(3, cooled);

            // 남은 기운: 바깥 링 빛 머리 2개가 감속하며 돌고 점점 약해진다
            ringAmp = t < 0.55f ? 2 : (t < 1.05f ? 1 : 0);
            float slow = EaseOutCubic(Mathf.Min(1.0f, t / 1.2f));
            ringPhase = 0.9f * slow + 0.25f;

            // 룬은 반대 방향 순서로 한 단계 밝아진다
            bRuneActive = t < 1.05f;
            runePhase = -0.7f * slow + 0.25f;

            // 안쪽 도형: 임팩트 뒤 바깥으로 퍼지는 여진 2번(0.12초, 0.5초)
            innerWave0 = InnerWave(t, 0.12f);
            innerWave1 = InnerWave(t, 0.5f);
        }

        private static float InnerWave(float _t, float _start)
        {
            float w = (_t - _start) / 0.45f * 0.95f;
            return 0.0f < w && w < 0.95f ? w : -1.0f;
        }

        // 마법진 픽셀 하나의 팔레트 단계(0이면 꺼짐)
        private int EvaluateSigilLevel(ref SigilPixel _p)
        {
            int rest = _p.rest;
            float t = frameTime;

            if (t >= _p.dissolveStart)
            {
                int remain = rest - Mathf.FloorToInt((t - _p.dissolveStart) * 10.0f + 1e-6f);
                return 0 < remain ? remain : 0;
            }

            int level = 3 == rest ? baseLevel3 : (2 == rest ? baseLevel2 : Mathf.Max(rest, cooledLevel));
            int boost = 0;

            if (GroupRing == _p.group)
            {
                if (0 < ringAmp)
                {
                    float d0 = Wrap01(ringPhase - _p.a);
                    if (d0 < 0.012f) boost = Mathf.Max(boost, ringAmp);
                    else if (d0 < ringHalo && t < 0.8f) boost = Mathf.Max(boost, 1);

                    float d1 = Wrap01(ringPhase + 0.5f - _p.a);
                    if (d1 < 0.012f) boost = Mathf.Max(boost, ringAmp);
                    else if (d1 < ringHalo && t < 0.8f) boost = Mathf.Max(boost, 1);
                }
            }
            else if (GroupRune == _p.group)
            {
                if (true == bRuneActive && Wrap01(_p.a - runePhase) < 0.05f) boost = 1;
            }
            else
            {
                if (0.0f < innerWave0 && _p.r <= innerWave0 && _p.r > innerWave0 - 0.07f) boost = 1;
                if (0.0f < innerWave1 && _p.r <= innerWave1 && _p.r > innerWave1 - 0.07f) boost = 1;
            }

            return Mathf.Min(4, level + boost);
        }

        // 입자 모양 하나. 중심은 _level, 팔은 한 단계 낮다. 모양: `.` `+` `x`
        private void EmitShape(int _x, int _y, int _shape, int _ramp, int _level)
        {
            if (0 >= _level) return;

            painter.Dot(_x, _y, GetColor(_ramp, _level));
            if (PixelPainter.ShapeDot == _shape || 1 >= _level) return;

            Color32 arm = GetColor(_ramp, _level - 1);
            if (PixelPainter.ShapePlus == _shape)
            {
                painter.Dot(_x - 1, _y, arm);
                painter.Dot(_x + 1, _y, arm);
                painter.Dot(_x, _y - 1, arm);
                painter.Dot(_x, _y + 1, arm);
            }
            else
            {
                painter.Dot(_x - 1, _y - 1, arm);
                painter.Dot(_x + 1, _y - 1, arm);
                painter.Dot(_x - 1, _y + 1, arm);
                painter.Dot(_x + 1, _y + 1, arm);
            }
        }

        // 마법진 픽셀 전체를 그린다(룬은 해체 중 최대 3px 떠오른다)
        private void DrawSigil()
        {
            float t = frameTime;
            for (int i = 0; i < sigilCount; i++)
            {
                int level = EvaluateSigilLevel(ref sigil[i]);
                if (0 >= level) continue;

                int y = sigil[i].y;
                if (GroupRune == sigil[i].group && t > sigil[i].dissolveStart)
                {
                    y += Mathf.Min(3, Mathf.FloorToInt((t - sigil[i].dissolveStart) * 10.0f));
                }

                painter.Dot(centerX + sigil[i].x, centerY + y, GetColor(sigil[i].ramp, level));
            }
        }

        private void DrawStar(int _spriteIndex, int _shift)
        {
            StarCell[] sprite = StarSprites[_spriteIndex];
            for (int i = 0; i < sprite.Length; i++)
            {
                int level = sprite[i].level + _shift;
                if (0 >= level) continue;

                painter.Dot(centerX + sprite[i].dx, centerY + sprite[i].dy, GetColor(RampLine, level));
            }
        }

        // 공중 층: 섬광 줄, 중심 별, 꼭짓점 스파크, 떠오르는 입자, 해체 입자 순서(뒤에 그린 것이 위)
        private void DrawSky(int _frame)
        {
            float t = frameTime;

            // 1px 가로/세로 섬광 줄(끝으로 갈수록 팔레트 한 단계씩 낮아짐)
            if (4 > _frame)
            {
                int lengthX = _frame < 1 ? 56 : (_frame < 2 ? 40 : (_frame < 3 ? 24 : 12));
                int lengthY = _frame < 1 ? 20 : (_frame < 2 ? 14 : (_frame < 3 ? 9 : 4));
                float midX = lengthX * 0.55f;
                for (int x = -lengthX; x <= lengthX; x++)
                {
                    int a = Mathf.Abs(x);
                    painter.Dot(centerX + x, centerY, GetColor(RampLine, 10 > a ? 4 : (a < midX ? 3 : 2)));
                }

                for (int y = -lengthY; y <= lengthY; y++)
                {
                    painter.Dot(centerX, centerY + y, GetColor(RampLine, 6 > Mathf.Abs(y) ? 4 : 3));
                }
            }

            // 중심 4갈래 별: 7 -> 5 -> 3, 이후 작은 별이 맥동하다 식는다
            if (3 > _frame) DrawStar(0, 0);
            else if (5 > _frame) DrawStar(1, 0);
            else if (7 > _frame) DrawStar(2, 0);
            else if (1.0f > t)
            {
                bool bSmall = 0 != ((_frame >> 3) & 1);
                DrawStar(true == bSmall ? 2 : 3, true == bSmall ? -1 : 0);
            }
            else if (1.4f > t)
            {
                DrawStar(3, -1 - Mathf.FloorToInt((t - 1.0f) * 6.0f));
            }

            // 꼭짓점 8곳 스파크: 0초에 위로 튀어 오른다
            if (0.6f > t)
            {
                int diamond;
                int diamondY;
                int rect;
                int rectY;
                if (true == bArena)
                {
                    // 마름모 꼭짓점 4 + 내접 직사각형 꼭짓점 4(고정 별 9칸 중 8칸 자리와 같은 방향)
                    diamond = arenaA;
                    diamondY = arenaB;
                    rectY = arenaB / 2;
                    rect = rectY * 2;
                }
                else
                {
                    diamond = Mathf.Max(2, EvenRound(ringRadiusX * 0.8f));
                    diamondY = diamond / 2;
                    rect = Mathf.Max(2, EvenRound(diamond * 0.707f));
                    rectY = rect / 2;
                }

                float u = t / 0.6f;
                float inv = 1.0f - u;
                float ease = 1.0f - inv * inv * inv;
                for (int i = 0; i < 8; i++)
                {
                    int ox;
                    int oy;
                    SparkOrigin(i, diamond, diamondY, rect, rectY, out ox, out oy);

                    int rise = RoundHalfUp((4 > i ? 44.0f : 30.0f) * ease);
                    int shape = u < 0.35f ? PixelPainter.ShapePlus : (u < 0.6f ? PixelPainter.ShapeCross : PixelPainter.ShapeDot);
                    EmitShape(centerX + ox, centerY + oy + rise, shape, 0 != (i & 1) ? RampAcc : RampLine, 4 - Mathf.FloorToInt(u * 3.5f));
                }
            }

            // 떠오르는 입자
            for (int i = 0; i < moteCount; i++)
            {
                float age = t - motes[i].t0;
                if (0.0f > age || age >= motes[i].life) continue;

                float u = age / motes[i].life;
                float inv = 1.0f - u;
                int x = centerX + motes[i].ox + RoundHalfUp(Mathf.Sin(motes[i].sway + u * 5.0f) * 1.2f);
                int y = centerY + motes[i].oy + RoundHalfUp(motes[i].rise * (1.0f - inv * inv));
                EmitShape(x, y, u < 0.45f ? motes[i].shape : PixelPainter.ShapeDot, motes[i].ramp, 4 - Mathf.FloorToInt(u * 4.0f));
            }

            // 해체 입자: 꺼지는 픽셀의 5%가 `+`에서 `.`로 줄며 떠오른다
            if (DissolveStart < t)
            {
                for (int n = 0; n < dissolveCount; n++)
                {
                    int index = dissolveIndices[n];
                    float age = t - (sigil[index].dissolveStart + sigil[index].rest * 0.1f);
                    if (0.0f > age || age >= 0.45f) continue;

                    float u = age / 0.45f;
                    int lift = RoundHalfUp(4.0f + 14.0f * u);
                    EmitShape(centerX + sigil[index].x, centerY + sigil[index].y + lift, u < 0.3f ? PixelPainter.ShapePlus : PixelPainter.ShapeDot, sigil[index].ramp, 3 - Mathf.FloorToInt(u * 3.0f));
                }
            }
        }

        // 스파크 8곳: 팔각별 꼭짓점(아레나는 마름모 꼭짓점 + 내접 직사각형 꼭짓점). 마름모 좌우 끝, 마름모 위/아래 끝, 직사각형 네 모서리 순서(위쪽이 +y)
        private static void SparkOrigin(int _index, int _diamond, int _diamondY, int _rect, int _rectY, out int _x, out int _y)
        {
            switch (_index)
            {
                case 0: _x = _diamond; _y = 0; break;
                case 1: _x = -_diamond; _y = 0; break;
                case 2: _x = 0; _y = -_diamondY; break;
                case 3: _x = 0; _y = _diamondY; break;
                case 4: _x = _rect; _y = -_rectY; break;
                case 5: _x = -_rect; _y = -_rectY; break;
                case 6: _x = _rect; _y = _rectY; break;
                default: _x = -_rect; _y = _rectY; break;
            }
        }

        private void RebuildPixelMeshes(int _frame)
        {
            Vector3 pos = transform.position;
            centerX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            centerY = Mathf.FloorToInt(pos.y * PixelsPerUnit);

            // 다른 모양/크기의 인스턴스가 표를 바꿔 놨으면 이 인스턴스의 표로 되돌린다(같으면 정수 비교 한 번)
            EnsureSigilTable(sigilKey);
            PrepareFrame(_frame);

            bool bSplit = null != groundMesh;
            if (true == bSplit)
            {
                Matrix4x4 groundWorldToLocal = groundMeshRenderer.transform.worldToLocalMatrix;
                painter.Begin(quadBuffer, in groundWorldToLocal, pos.z);
                DrawSigil();
                painter.Commit(groundMesh, groundMeshRenderer, ref groundHash, ref groundVertexCount);
            }

            Matrix4x4 skyWorldToLocal = transform.worldToLocalMatrix;
            painter.Begin(quadBuffer, in skyWorldToLocal, pos.z);

            // 바닥 메쉬가 없으면(프리팹 참조 누락) 마법진도 공중 메쉬 하나로 그린다
            if (false == bSplit) DrawSigil();
            DrawSky(_frame);
            painter.Commit(mesh, meshRenderer, ref skyHash, ref skyVertexCount);
        }

        // ---------- 부드러운 빛 (소프트 메쉬, 매 프레임 정점 위치/색만 갱신) ----------

        private static void WriteVertex(int _index, float _x, float _y, in Vector3 _tint, float _intensity)
        {
            softScratch[_index].position = new Vector3(_x, _y, 0.0f);
            softScratch[_index].color = new Color(_tint.x, _tint.y, _tint.z, _intensity * (1.0f / SoftGlowMultiplier));
        }

        // 임팩트 섬광: 가운데가 가장 밝은 작은 타원 원반(가로 20px x 세로 11px 단위의 d, 0~2)
        private void UpdateFlash(float _t, float _ox, float _oy)
        {
            float a = 1.4f * (1.0f - _t / FlashEnd);
            for (int row = 0; row < FlashRows; row++)
            {
                float d = FlashRowD[row];
                float intensity = a * Mathf.Exp(-1.6f * d * d);
                for (int s = 0; s < SoftSegments; s++)
                {
                    float x = _ox + d * 20.0f * SegmentCos[s] * PixelUnit;
                    float y = _oy + d * 11.0f * SegmentSin[s] * PixelUnit;
                    WriteVertex(FlashVertexStart + row * SoftSegments + s, x, y, in FlashTint, intensity);
                }
            }
        }

        // 빛기둥: 중심에서 솟구쳐 0.5초 안에 식는다. 가로 단면은 가우시안(7열), 세로로 갈수록 가늘고 어두워진다.
        private void UpdatePillar(float _t, float _ox, float _oy)
        {
            float u = _t / PillarEnd;
            float a = 1.8f * (u < 0.04f ? 1.0f : 1.0f - (u - 0.04f) / 0.96f);
            float height = pillarHeightPx * sizeScale * Mathf.Min(1.0f, 0.55f + u * 3.0f);
            for (int row = 0; row < PillarRows; row++)
            {
                // 행 0은 바닥 아래 6px(0에서 시작해 올라오는 밑단), 나머지는 높이 비율
                float yPx = 0 == row ? -6.0f : height * PillarRowFraction[row];
                float q = Mathf.Max(0.0f, yPx / height);
                float vertical = 0 == row ? 0.0f : 1.0f - Mathf.Pow(q, 1.6f);
                float width = 8.0f * (1.0f - 0.5f * q) * (1.0f - 0.5f * u);
                for (int col = 0; col < PillarCols; col++)
                {
                    float c = PillarColumnC[col];
                    float intensity = a * vertical * Mathf.Exp(-c * c / 0.7f);
                    WriteVertex(PillarVertexStart + row * PillarCols + col, _ox + c * width * PixelUnit, _oy + yPx * PixelUnit, in PillarTint, intensity);
                }
            }
        }

        // 빛 커튼: 바깥 링 테두리에서 위로 솟는 원통형 빛 벽. 위로 갈수록 옅어지고 뒤쪽 벽(링 위쪽 반)은 55%.
        private void UpdateCurtain(float _t, float _ox, float _oy)
        {
            if (true == bArena)
            {
                UpdateCurtainArena(_t, _ox, _oy);
                return;
            }

            float u = _t / CurtainEnd;
            float a = 0.55f * (u < 0.05f ? 1.0f : Mathf.Pow(1.0f - (u - 0.05f) / 0.95f, 1.6f));
            float height = curtainHeightPx * sizeScale * (CurtainBaseFraction + (1.0f - CurtainBaseFraction) * EaseOutCubic(Mathf.Min(1.0f, _t / 0.35f)));
            height = Mathf.Max(8.0f, height);
            for (int row = 0; row < CurtainRows; row++)
            {
                // 행 0은 링 위(아래쪽 2px는 1.3배), 행 1은 2px 위, 나머지는 높이 비율
                float yPx = 0 == row ? 0.0f : (1 == row ? 2.0f : height * CurtainRowFraction[row]);
                float q = yPx / height;
                float weight = Mathf.Pow(1.0f - q, 1.8f) * (0 == row ? 1.3f : 1.0f);
                for (int s = 0; s < SoftSegments; s++)
                {
                    float back = 0.0001f < SegmentSin[s] ? 0.55f : 1.0f;
                    float x = _ox + ringRadiusX * SegmentCos[s] * PixelUnit;
                    float y = _oy + (ringRadiusY * SegmentSin[s] + yPx) * PixelUnit;
                    WriteVertex(CurtainVertexStart + row * SoftSegments + s, x, y, in CurtainTint, a * back * weight * CurtainStack[s]);
                }
            }
        }

        // 바닥 빛: 마법진 안쪽을 은은하게 밝힌다. 0초에 가장 밝고 식은 뒤 해체와 함께 꺼진다.
        private void UpdateGround(float _t, float _ox, float _oy)
        {
            if (true == bArena)
            {
                UpdateGroundArena(_t, _ox, _oy);
                return;
            }

            float ga = (0.07f + 0.2f * Mathf.Max(0.0f, 1.0f - _t / 0.35f)) * (1.0f - Mathf.Clamp01((_t - DissolveStart) / 0.5f));
            for (int row = 0; row < GroundRows; row++)
            {
                float r = GroundRowR[row];
                float intensity = ga * (0.35f + 0.65f * r * r) * (1.0f < r ? (1.05f - r) / 0.05f : 1.0f);
                for (int s = 0; s < SoftSegments; s++)
                {
                    float x = _ox + ringRadiusX * r * SegmentCos[s] * PixelUnit;
                    float y = _oy + ringRadiusX * GroundSquashY * r * SegmentSin[s] * PixelUnit;
                    WriteVertex(GroundVertexStart + row * SoftSegments + s, x, y, in GroundTint, intensity);
                }
            }
        }

        private static void ArenaCorner(int _index, int _a, int _b, out float _x, out float _y)
        {
            switch (_index & 3)
            {
                case 0: _x = _a; _y = 0.0f; break;
                case 1: _x = 0.0f; _y = _b; break;
                case 2: _x = -_a; _y = 0.0f; break;
                default: _x = 0.0f; _y = -_b; break;
            }
        }

        // 마름모 둘레의 _segment번 정점 위치(px, 중심 기준). 변마다 12분할이라 정점 0/12/24/36이 정확히 네 꼭짓점이다(오른쪽, 위, 왼쪽, 아래).
        private void GetArenaPerimeter(int _segment, out float _x, out float _y)
        {
            int edge = _segment / ArenaSegmentsPerEdge;
            float f = (_segment - edge * ArenaSegmentsPerEdge) / (float)ArenaSegmentsPerEdge;
            float x0;
            float y0;
            float x1;
            float y1;
            ArenaCorner(edge, arenaA, arenaB, out x0, out y0);
            ArenaCorner(edge + 1, arenaA, arenaB, out x1, out y1);
            _x = x0 + (x1 - x0) * f;
            _y = y0 + (y1 - y0) * f;
        }

        // 아레나 빛 커튼: 마름모 둘레에서 위로 솟는 빛 벽. 높이는 반지름과 무관하게 고정(arenaCurtainHeightPx)이고, 뒤쪽(위쪽 두 변)은 55%.
        // 시안은 둘레 픽셀마다 기둥 하나라 겹침이 없으므로 둘레 겹침 배율(CurtainStack)은 쓰지 않는다.
        private void UpdateCurtainArena(float _t, float _ox, float _oy)
        {
            float u = _t / CurtainEnd;
            float a = 0.5f * (u < 0.05f ? 1.0f : Mathf.Pow(1.0f - (u - 0.05f) / 0.95f, 1.6f));
            float height = arenaCurtainHeightPx * (CurtainBaseFraction + (1.0f - CurtainBaseFraction) * EaseOutCubic(Mathf.Min(1.0f, _t / 0.35f)));
            height = Mathf.Max(8.0f, height);
            for (int row = 0; row < CurtainRows; row++)
            {
                float yPx = 0 == row ? 0.0f : (1 == row ? 2.0f : height * CurtainRowFraction[row]);
                float q = yPx / height;
                float weight = Mathf.Pow(1.0f - q, 1.8f) * (0 == row ? 1.3f : 1.0f);
                for (int s = 0; s < SoftSegments; s++)
                {
                    float px;
                    float py;
                    GetArenaPerimeter(s, out px, out py);
                    float back = 0.001f < py ? 0.55f : 1.0f;
                    WriteVertex(CurtainVertexStart + row * SoftSegments + s, _ox + px * PixelUnit, _oy + (py + yPx) * PixelUnit, in CurtainTint, a * back * weight);
                }
            }
        }

        // 아레나 바닥 빛: 마름모 채움. 가장자리 위주(0.12 + 0.88 r^3)로 은은하게 밝히고 0초에 가장 밝다가 해체와 함께 꺼진다.
        private void UpdateGroundArena(float _t, float _ox, float _oy)
        {
            float ga = (0.035f + 0.12f * Mathf.Max(0.0f, 1.0f - _t / 0.3f)) * (1.0f - Mathf.Clamp01((_t - DissolveStart) / 0.5f));
            for (int row = 0; row < GroundRows; row++)
            {
                float r = ArenaGroundRowR[row];
                float intensity = ga * (0.12f + 0.88f * r * r * r);
                for (int s = 0; s < SoftSegments; s++)
                {
                    float px;
                    float py;
                    GetArenaPerimeter(s, out px, out py);
                    WriteVertex(GroundVertexStart + row * SoftSegments + s, _ox + px * r * PixelUnit, _oy + py * r * PixelUnit, in GroundTint, intensity);
                }
            }
        }

        private void UpdateSoftMesh()
        {
            if (null == softMesh) return;

            float t = elapsed;
            if (false == bSoftLight || GroundEnd <= t)
            {
                if (true == softMeshRenderer.enabled) softMeshRenderer.enabled = false;
                return;
            }

            // 끝난 부분은 건너뛴다. 부분 순서가 끝나는 시각 순서라 살아 있는 부분은 항상 뒤쪽 연속 구간이다.
            int first = PartFlash;
            if (FlashEnd <= t) first = PartPillar;
            if (PillarEnd <= t) first = PartCurtain;
            if (CurtainEnd <= t) first = PartGround;

            // 소프트 메쉬 중심 = 마법진 중심 픽셀의 한가운데(픽셀 격자와 같은 자리)
            Vector3 softPos = softMeshFilter.transform.position;
            float ox = (centerX + 0.5f) * PixelUnit - softPos.x;
            float oy = (centerY + 0.5f) * PixelUnit - softPos.y;

            if (PartFlash >= first) UpdateFlash(t, ox, oy);
            if (PartPillar >= first) UpdatePillar(t, ox, oy);
            if (PartCurtain >= first) UpdateCurtain(t, ox, oy);
            UpdateGround(t, ox, oy);

            int vertexStart = PartVertexStart[first];
            int indexStart = PartIndexStart[first];
            softMesh.SetVertexBufferData(softScratch, vertexStart, vertexStart, SoftVertexTotal - vertexStart, 0, SoftUploadFlags);

            // 살아 있는 부분이 바뀔 때만 그릴 인덱스 범위를 다시 정한다
            if (first != appliedSoftFirstPart)
            {
                appliedSoftFirstPart = first;
                SubMeshDescriptor subMesh = new SubMeshDescriptor(indexStart, SoftIndexTotal - indexStart, MeshTopology.Triangles);
                subMesh.firstVertex = 0;
                subMesh.vertexCount = SoftVertexTotal;
                softMesh.SetSubMesh(0, subMesh, SoftUploadFlags);
            }

            if (false == softMeshRenderer.enabled) softMeshRenderer.enabled = true;
        }

        private void DisableRenderers()
        {
            if (null != meshRenderer && true == meshRenderer.enabled) meshRenderer.enabled = false;
            if (null != groundMeshRenderer && true == groundMeshRenderer.enabled) groundMeshRenderer.enabled = false;
            if (null != softMeshRenderer && true == softMeshRenderer.enabled) softMeshRenderer.enabled = false;
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
            if (null != softMeshRenderer) softMeshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (true == bBeginPending)
            {
                bBeginPending = false;
                Begin();
            }

            if (TimelineLength <= elapsed || ContentEnd <= elapsed)
            {
                DisableRenderers();
                return;
            }

            // 첫 프레임은 시간을 더하지 않아서 t=0의 각인 프레임이 반드시 한 번 그려진다
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

                    if (null != softMeshRenderer)
                    {
                        softMeshRenderer.sortingLayerID = rootLayer;
                        softMeshRenderer.sortingOrder = rootOrder + sortingOrderOffset + 1;
                    }
                }
            }

            // VFXComponent가 재생할 때 자식 렌더러 전체에 같은 소팅을 덮어쓰므로, 바닥 메쉬는 자기 레이어로 다시 맞춘다
            if (null != groundMesh)
            {
                if (groundMeshRenderer.sortingLayerID != groundSortingLayerId) groundMeshRenderer.sortingLayerID = groundSortingLayerId;
                if (groundMeshRenderer.sortingOrder != groundSortingOrder) groundMeshRenderer.sortingOrder = groundSortingOrder;
            }

            // 화면 밖이면 메쉬 재구성을 건너뛰고 시간만 흘린다. 그림은 경과 시간만으로 다시 계산되므로(프레임 사이에 이어지는
            // 상태 없음) 다시 화면에 들어오는 첫 프레임에 그 시각의 그림을 그대로 그린다. 건너뛰는 동안은 렌더러를 꺼 두고
            // (그리기 쪽이 다시 켠다) 해시 비교값을 무효화해 돌아온 첫 프레임에 반드시 다시 올린다.
            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            Vector3 cullingSize = new Vector3(meshBoundsSize.x + margin, meshBoundsSize.y + margin, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(transform.position, cullingSize))
            {
                DisableRenderers();
                builtFrame = -1;
                groundVertexCount = -1;
                skyVertexCount = -1;
                return;
            }

            // 픽셀 부분은 30fps 프레임이 바뀔 때만 다시 그리고, 부드러운 빛은 매 프레임 정점 값만 갱신한다
            int frame = Mathf.FloorToInt(elapsed * FramesPerSecond + 0.001f);
            if (frame != builtFrame)
            {
                builtFrame = frame;
                RebuildPixelMeshes(frame);
            }

            UpdateSoftMesh();
        }

        private void OnDisable()
        {
            overrideRadius = 0.0f;
            overrideHalfWidth = 0.0f;
            overrideHalfHeight = 0.0f;

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
            if (null != softMeshRenderer) softMeshRenderer.enabled = false;
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

            if (null != softMesh)
            {
                Destroy(softMesh);
                softMesh = null;
            }
        }

        private void OnValidate()
        {
            maxRadius = Mathf.Max(0.5f, maxRadius);
            curtainHeightPx = Mathf.Max(8.0f, curtainHeightPx);
            pillarHeightPx = Mathf.Max(8.0f, pillarHeightPx);
            arenaHalfWidth = Mathf.Max(1.0f, arenaHalfWidth);
            arenaHalfHeight = Mathf.Max(0.5f, arenaHalfHeight);
            arenaCurtainHeightPx = Mathf.Max(8.0f, arenaCurtainHeightPx);
            BuildColorTable();
        }

        // 씬 뷰에서 최대 반지름(바닥 타원)을 보여 줘서 피격 범위와 눈으로 맞출 수 있게 한다.
        private void OnDrawGizmosSelected()
        {
            if (ENovaShape.ArenaDiamond == shape)
            {
                float halfWidth = true == Application.isPlaying && true == bArena ? arenaA * PixelUnit : arenaHalfWidth - ArenaEdgeInset * PixelUnit;
                Vector3 arenaCenter = transform.position;
                Gizmos.color = new Color(0.4f, 0.9f, 1.0f, 0.9f);
                Vector3 right = arenaCenter + new Vector3(halfWidth, 0.0f, 0.0f);
                Vector3 top = arenaCenter + new Vector3(0.0f, halfWidth * GroundSquashY, 0.0f);
                Vector3 left = arenaCenter + new Vector3(-halfWidth, 0.0f, 0.0f);
                Vector3 bottom = arenaCenter + new Vector3(0.0f, -halfWidth * GroundSquashY, 0.0f);
                Gizmos.DrawLine(right, top);
                Gizmos.DrawLine(top, left);
                Gizmos.DrawLine(left, bottom);
                Gizmos.DrawLine(bottom, right);
                return;
            }

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
