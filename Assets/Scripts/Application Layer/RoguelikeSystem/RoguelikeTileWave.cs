using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>바닥 타일 파도 연출 수치. 기획서 6장 "진입 연출 - 바닥 타일 파도 간격 / 배경 완성 시간"(TBD)의 자리다.</summary>
[Serializable]
public sealed class RoguelikeTileWaveSettings
{
    [Tooltip("파도 앞머리 평균 속도(월드 단위/초). 클수록 타일 사이 간격이 짧다(기획서 6장 '바닥 타일 파도 간격'). 앞머리는 frontEaseOut만큼 중심에서 빠르고 바깥으로 갈수록 느려진다.")]
    public float frontSpeed = 11f;
    [Tooltip("앞머리 감속(0~0.8). 0이면 일정한 속도, 클수록 중심에서 빠르게(평균의 1 + 이 값 배) 출발해 바깥에서 느려진다(1 - 이 값 배). 평균 속도·전체 시간은 그대로다.")]
    [Range(0f, 0.8f)] public float frontEaseOut = 0.35f;
    [Tooltip("물결 마루가 가장 높이 솟는 높이(월드 단위, 1 = 32픽셀). 픽셀 단위로 끊어 그린다.")]
    public float rippleHeight = 0.375f;
    [Tooltip("물결 한 파장의 길이(바닥 평면 거리, 대략 칸 수). 길수록 이웃 칸 높이 차가 작아 매끄럽다.")]
    public float rippleWavelength = 8f;
    [Tooltip("앞머리 뒤로 물결이 몇 파장 동안 이어지다 잦아드는지. 마루 → 골 → 작은 마루 순으로 줄어든다.")]
    public float rippleWaves = 2f;
    [Tooltip("물결 골(내려간 칸)이 가장 깊을 때 어두워지는 정도(0~1). 타일에 명암이 없어 높이 차만으로는 물결이 잘 안 읽힌다.")]
    public float rippleShade = 0.18f;

    [Header("바뀌는 칸")]
    [Tooltip("칸이 새 지형으로 바뀌는 순간(진입은 블록이 나타나는 순간) 물결 위로 한 번 더 톡 솟는 높이(월드, 1 = 32픽셀). 0이면 끈다.")]
    public float swapPopHeight = 0.125f;
    [Tooltip("톡 솟았다 내려오는 구간(바닥 평면 거리). 짧을수록 빠르고 이웃 칸 높이 차가 커진다. 솟고 튕기는 구간이 물결 끝을 넘으면 그 안에 들어가게 줄여 쓴다(기본 물결에서 전환은 약 5, 진입은 6보다 길게 해도 같다).")]
    public float swapPopLength = 2f;
    [Tooltip("솟았다 내려온 뒤 살짝 아래로 튕기는 깊이(솟는 높이 대비 비율). 0이면 튕기지 않는다.")]
    [Range(0f, 1f)] public float swapPopSettle = 0.35f;

    [Header("앞머리 일렁임")]
    [Tooltip("파도 앞머리가 완벽한 타원이 아니라 일렁이며 퍼지게 칸마다 섞는 거리(바닥 평면 거리). 0이면 끈다.")]
    public float frontWobble = 0.6f;
    [Tooltip("일렁임 무늬의 크기(월드 단위당 진동수). 작을수록 크고 완만한 굴곡이다.")]
    public float frontWobbleScale = 0.12f;

    [Header("아웃라인")]
    [Tooltip("출렁이는 칸 윗면 테두리 색(지진 물결과 같은 연출). 바닥과 같은 재질(조명 영향)로 그린다. 알파는 최대 진하기다.")]
    public Color outlineColor = new Color(1f, 0.97f, 0.86f, 0.8f);
    [Tooltip("칸에 물결이 닿은 뒤 테두리가 다 드러나는 구간(물결 길이 대비 비율).")]
    [Range(0.01f, 1f)] public float outlineFadeIn = 0.08f;
    [Tooltip("물결 끝에서 테두리가 사라지기 시작하는 지점(물결 길이 대비 비율). 거기서 물결 끝까지 서서히 사라진다.")]
    [Range(0f, 0.99f)] public float outlineFadeOutStart = 0.3f;
    [Tooltip("칸이 수평(높이 0)을 지날 때의 테두리 진하기 비율. 마루·골에서 1이다. 낮을수록 마루·골의 띠만 또렷하다.")]
    [Range(0f, 1f)] public float outlineOnLevel = 0.15f;
    [Tooltip("골(내려간 칸)에서의 테두리 진하기 비율(마루 = 1). 낮을수록 골이 갈라진 틈처럼 보이지 않는다. outlineOnLevel보다 낮으면 outlineOnLevel로 쓴다(골이 수평일 때보다 옅어지지 않는다).")]
    [Range(0f, 1f)] public float outlineTroughLevel = 0.4f;
    [Header("빛 (가산)")]
    [Tooltip("파도 빛(마루 빛·바뀌는 칸 반짝임·바다 파문·물빛 틈 빛)을 그리는 가산 재질(Custom/Roguelike/WaveLightAdditive). 비우면 빛을 그리지 않는다.")]
    public Material lightMaterial;
    [Tooltip("가장 높이 솟은 칸 윗면에 더하는 빛의 세기(0~1). 낮게 솟은 칸일수록 옅고, 마루 높이(rippleHeight)의 절반 아래로 솟은 칸은 밝히지 않는다. 0이면 끈다.")]
    [Range(0f, 1f)] public float crestGlow = 0.1f;
    [Tooltip("마루 빛·반짝임 색(가산).")]
    public Color crestLightColor = new Color(1f, 0.92f, 0.72f, 1f);
    [Tooltip("칸이 새 지형으로 바뀌는 순간 윗면이 반짝이는 세기(0~1). 바뀌는 선을 따라 빛 띠가 퍼진다. 0이면 끈다.")]
    [Range(0f, 1f)] public float swapFlash = 0.14f;
    [Tooltip("반짝임이 사그라드는 구간(바닥 평면 거리). 짧을수록 빛 띠가 가늘다.")]
    public float swapFlashLength = 1.75f;
    [Tooltip("섬 둘레 바다에 앞머리를 따라 퍼지는 빛 고리 세기(0~1). 섬 가장자리에서 가장 밝고 멀어질수록 옅어진다. 0이면 끈다.")]
    [Range(0f, 1f)] public float seaRing = 0.1f;
    [Tooltip("바다 빛 고리 색(가산). 용암 티어는 lavaLightColor를 쓴다.")]
    public Color seaRingColor = new Color(0.78f, 0.93f, 1f, 1f);
    [Tooltip("용암 티어의 바다 빛 고리 색(가산).")]
    public Color lavaLightColor = new Color(1f, 0.72f, 0.3f, 1f);
    [Tooltip("칸이 앞 칸보다 솟아 옆벽(틈)이 드러나면 틈 바닥을 따라 빛을 비춘다(지진 물결의 용암 틈 빛). 용암 티어는 용암 빛 그대로, 다른 티어는 crackGlowColor 물빛이다.")]
    public bool crackGlow = true;
    [Tooltip("용암이 아닌 티어의 틈 빛 색(가산, 알파 = 세기).")]
    public Color crackGlowColor = new Color(0.62f, 0.9f, 1f, 0.7f);

    [Header("굴절 고리")]
    [Tooltip("앞머리를 따라 퍼지는 약한 굴절 고리 재질(Custom/Roguelike/WaveRefraction, 화면 캡처를 픽셀 단위로 민다). 비우면 끈다.")]
    public Material refractionMaterial;
    [Tooltip("굴절 고리가 화면을 미는 최대 픽셀 수(게임 픽셀). 0이면 끈다.")]
    public float refractionPixels = 2f;
    [Tooltip("굴절 고리 폭(바닥 평면 거리).")]
    public float refractionWidth = 1.2f;

    [Header("진입·배경")]
    [Tooltip("진입 5단계 '모든 배경이 N초 이내에 완성'의 N(기획서 10장 임시값 3초). 진입 파도는 바닥 파도 + 배경 조립이 이 시간 안에 끝나도록 앞머리 속도를 맞춘다(frontSpeed보다 느려지지 않는다).")]
    public float entryCompleteSeconds = 3f;
    [Tooltip("진입 4단계 '배경 조립': 바닥이 다 깔린 뒤 나머지 배경(해안선)이 조각 단위로 나타나는 데 걸리는 전체 시간(초).")]
    public float backgroundAssembleSeconds = 1.2f;
    [Tooltip("배경 조각 하나가 투명에서 드러나는 시간(초).")]
    public float backgroundPieceFadeSeconds = 0.3f;
    [Tooltip("배경 조각 한 변의 칸 수(퍼즐 조각 크기).")]
    public int backgroundPieceCells = 3;
    [Tooltip("기준 화면 바깥으로 이만큼(칸)까지는 파도로 바꾼다. 그보다 먼 칸은 화면 밖이라 바로 바꾼다.")]
    public float viewMarginTiles = 2f;
}

/// <summary>
/// 바닥 타일 파도(기획서 4-1 진입 3~5단계, 3-4 티어 전환 - 같은 연출을 공용으로 쓴다).
///
/// 섬 중앙에서 물결이 동심원으로 퍼진다(바닥 평면 기준 원 - 아이소 화면에서는 가로로 긴 타원). 칸 높이는 앞머리에서 떨어진
/// 거리만의 매끄러운 함수다(마루 → 골 → 작은 마루로 잦아드는 감쇠 물결) - 이웃 칸 높이가 연속이라 띠 전체가 한 물결로 출렁인다.
/// 첫 마루에서 그 칸이 새 타일로 바뀐다.
///   진입(Entry): 물은 처음부터 깔려 있고, 육지(바닥 블록)가 파도를 따라 솟아오르며 깔린다(3단계).
///               바닥이 다 깔리면 나머지 배경(해안선)이 퍼즐 조각처럼 하나씩 나타나 완성된다(4~5단계).
///   티어 전환(Transition): 직전 티어 지형이 파도를 따라 칸마다 새 티어 지형(바닥·물·해안선)으로 바뀐다.
///
/// [자연스럽게 - 빈 공간·구름 비침·물 섞임 없음]
///   - 실제 바닥·물·해안선 층은 솟지 않고 제자리에서 바뀌기만 한다. 파도 동안 화면 안 바닥은 전부 모든 타일 층 위의
///     복제층(리프트)이 그린다 - 움직이는 칸만 복제하면 앞 칸이 뒤 칸의 흙 옆면을 가리지 못해 굵은 흙 줄이 생긴다.
///     한 타일맵 안에서는 칸 순서대로 앞뒤가 정렬되므로, 솟은 칸의 옆면은 이웃과의 높이 차만큼만 보인다.
///     물도 같은 정렬 순서로 복제층에 그린다 - 실제 층처럼 앞 칸의 물이 물가 절벽 아랫부분을 덮어야, 파도 동안 절벽이
///     물 위로 드러났다가 끝나는 순간 잠기는 일이 없다. 물·해안선은 출렁이지 않는다.
///     실제 바닥을 들면 원래 자리가 비어 물이 드러나고, 지면 스텐실(GroundStencil)이 어긋나 구름 그림자가 육지에 비친다.
///   - 진입에서 섬 자리·물가 칸은 파도가 오기 전까지 깊은 물로 둔다(물가 모양 물을 두면 섬 윤곽이 미리 보인다).
///     물결이 닿는 순간 복제층의 그 칸에 블록이 제 크기로 나타나고 바다는 새 물로 바뀐다. 실제 층은 물결이 다 지나간 뒤 바꾼다.
///   - 지면 스텐실(GroundStencil) 복제층에는 솟은 블록과 같은 타일을 놓아, 블록 아래 남은 물 스텐실로 구름 그림자가 비치지 않게 한다.
///
/// [모습 - 지진 물결(QuakeTileRipple)과 같게]
///   - 높이는 픽셀(1/32) 단위로 끊고, 칸을 움직이지 않고 같은 그림의 피벗만 옮긴 사본 타일(QuakeTileRipple.OffsetTile)로 바꿔 끼운다.
///     칸 변형(SetTransformMatrix)은 정렬 위치까지 옮겨, 솟은 칸이 한 줄 간격(8픽셀)보다 높아지면 뒤 칸에 덮인다. 골은 깊이만큼 어둡게 한다.
///   - 출렁이는 동안 칸 윗면 테두리(아웃라인)가 드러나 한 칸 한 칸이 따로 움직이는 게 보인다. 테두리 복제층은 바닥 복제층과 정렬 순서가
///     같고 아주 조금 앞에 놓여, 칸마다 "제 바닥 바로 다음, 앞 칸 바닥보다 먼저" 그려진다(솟은 앞 칸이 뒤 칸 테두리를 가린다).
///   - 칸 변경은 프레임마다 모아 층마다 한 번에 쓴다. 바뀌는 순간(TileRevealedEvent)·실제 층을 바꾸는 시점은 그대로다.
///
/// 맵 생성(TileMapGenerator)은 평소처럼 한 번에 깐다. 이 클래스는 생성 직전/직후 타일을 떠 두었다가 화면 안 칸만
/// 시작 상태로 되돌려 놓고 순서대로 바꾼다. 충돌 타일맵은 건드리지 않는다(생성 결과가 곧바로 맞다).
/// 칸이 바뀌는 순간 TileRevealedEvent(칸 월드 위치)가 나간다 - 하이라이트·반짝임 같은 장식은 여기에 붙인다.
/// </summary>
public sealed class RoguelikeTileWave
{
    /// <summary>타일 한 칸이 새 지형으로 바뀌었다(물결 꼭대기, 칸 중심 월드 위치). 구독자가 없으면 비용이 없다.</summary>
    public event Action<Vector3> TileRevealedEvent;

    private struct WaveCell
    {
        public int index;
        public Vector3Int cell;
        public float key;
        public bool bInView;
        public Vector3 worldPos;
    }

    private struct BackgroundPiece
    {
        public int firstCell;
        public int cellCount;
        public float startTime;
    }

    private sealed class WaveCellComparer : IComparer<WaveCell>
    {
        public int Compare(WaveCell _a, WaveCell _b) => _a.key.CompareTo(_b.key);
    }

    private static readonly WaveCellComparer cellComparer = new WaveCellComparer();

    private sealed class SeaCellComparer : IComparer<SeaCell>
    {
        public int Compare(SeaCell _a, SeaCell _b) => _a.key.CompareTo(_b.key);
    }

    private static readonly SeaCellComparer seaCellComparer = new SeaCellComparer();

    // 층 묶음. 바닥(0번이 바닥 블록), 물(0번이 물 타일), 해안선(물 가장자리).
    private readonly List<Tilemap> floorLayers = new List<Tilemap>(4);
    private readonly List<Tilemap> waterLayers = new List<Tilemap>(4);
    private readonly List<Tilemap> shoreLayers = new List<Tilemap>(2);
    private readonly List<WaveCell> cells = new List<WaveCell>(4096);
    // cells와 같은 순서. 칸이 물결 어느 단계인지(StageWaiting → Reached → Swapped → Done).
    private byte[] cellStage = new byte[0];
    private const byte StageWaiting = 0;
    private const byte StageReached = 1;
    private const byte StageSwapped = 2;
    private const byte StageDone = 3;
    private readonly List<int> pieceCells = new List<int>(2048);
    private readonly List<BackgroundPiece> pieces = new List<BackgroundPiece>(512);

    // 층마다 [영역 칸 수] 크기. before = 생성 직전, after = 생성 결과, start = 파도 시작 상태(물 층만).
    private TileBase[][] floorBefore, floorAfter;
    private TileBase[][] waterBefore, waterAfter, waterStart;
    private TileBase[][] shoreBefore, shoreAfter;
    private TileBase[] emptyBlock;
    // 솟는 블록을 그리는 복제층(바닥 층과 같은 순서). 파도가 끝나면 비운다.
    // 스텐실 층 자리는 같은 블록 모양으로 지면 스텐실을 함께 들어 올린다 - 솟은 블록 밑에 남은 물 스텐실로 구름 그림자가 비치지 않게.
    private readonly List<Tilemap> lifts = new List<Tilemap>(4);
    // 해안선(물 모서리) 복제층. 바닥 복제층이 실제 해안선을 덮으므로 티어 전환 동안 해안선도 그 위에 따로 그린다.
    private readonly List<Tilemap> shoreLifts = new List<Tilemap>(2);
    // 물 복제층(물 층마다). 실제 층에서는 물이 바닥 블록의 아랫부분(물가 절벽)을 덮는다. 바닥만 복제층으로 올리면
    // 절벽이 물 위로 통째로 드러났다가 파도가 끝나는 순간 물속으로 잠긴다. 그래서 물도 같은 앞뒤 순서로 복제층에 그린다.
    // 물과 해안선은 출렁이지 않는다(제자리) - 솟고 가라앉는 건 육지뿐이다.
    private readonly List<Tilemap> waterLifts = new List<Tilemap>(4);
    // 칸 윗면 테두리 복제층. 바닥 블록 복제층과 정렬 순서가 같고 아주 조금 앞(아래)에 둔다.
    private Tilemap outlineLift;
    // 이번 프레임에 모은 칸 변경(바닥 복제층마다, 테두리). 프레임 끝에 층마다 한 번에 쓴다.
    private readonly List<List<TileChangeData>> floorChanges = new List<List<TileChangeData>>(4);
    private readonly List<TileChangeData> outlineChanges = new List<TileChangeData>(512);
    // cells와 같은 순서. 복제층에 깔린 칸 높이(픽셀)·테두리 단계, 복제층 바닥이 새 지형(floorAfter)인지(아니면 직전 지형).
    private short[] lastPixels = new short[0];
    private sbyte[] lastOutlineSteps = new sbyte[0];
    private bool[] liftShowsAfter = new bool[0];
    // lastPixels에 넣어 두면 다음 높이 갱신 때 높이가 같아도 그 칸의 바닥을 다시 쓴다(복제층에 놓을 지형이 바뀌었다).
    private const short PixelsDirty = short.MinValue;
    // 스텐실 리프트에 놓을 블록 모양을 가져올 바닥 층(스텐실이 아닌 첫 층). 없으면 -1.
    private int liftShapeLayer = -1;
    // 바닥 층 중 스텐실만 쓰는 층(GroundStencil). 복제층에는 블록 모양 타일을 놓는다(LiftTilesOf).
    private bool[] bStencilFloorLayer;
    private bool[] landCell;     // 직전이나 새 지형에 바닥이 있는 칸
    private bool[] nearLand;     // 새 지형의 바닥 칸과 그 둘레 한 칸

    private EnvironmentSystem environmentSystem;
    private RoguelikeTileWaveSettings settings;
    private BoundsInt region;
    private Vector2 viewHalfSize;
    private bool bHasRegion;
    private bool bHasBefore;
    private bool bPlaying;
    // PrepareAfterGenerate가 시작 상태를 깔아 두었고 Play가 아직 안 돌았다.
    private bool bPrepared;
    private bool bEntry;
    // 칸마다 따로 바꾸는 타일의 애니메이션(물결) 시간을 맞출 기준 칸. 따로 바꾼 칸은 재생 시점이 어긋나 주변과 이음선이 진다.
    private Vector3Int animRefCell;
    private bool bHasAnimRef;
    // 파도가 퍼지는 중심과 지금 앞머리 거리·속도(재생 중에만 의미가 있다). 칸 단위가 아닌 장식이 파도에 맞춰 바뀔 때 쓴다.
    private Vector3 waveCenter;
    // 앞머리 일렁임 무늬의 자리(파도마다 다른 무늬). 게임 난수(UnityEngine.Random)를 쓰지 않으려고 파도 번호로 정한다.
    // 로그라이크 씬을 다시 불러오면 이 객체도 새로 만들어지므로, 번호는 정적으로 두어 들어올 때마다 첫 파도 무늬가 같지 않게 한다.
    private static int waveSerial;
    private Vector2 wobbleOffset;
    private float frontDistance;
    private float frontSpeed;

    // // 빛(가산 재질 복제층)
    // 칸 윗면 빛(마루 빛 + 바뀌는 칸 반짝임). 테두리처럼 바닥 블록 복제층과 정렬 순서가 같고 조금 더 앞에 둔다.
    private Tilemap lightLift;
    // 바다 빛 고리. 물 복제층과 정렬 순서가 같고 조금 앞에 둔다(앞 칸 블록이 가린다).
    private Tilemap seaLift;
    // 틈 빛(왼쪽 위·오른쪽 위 모서리). 지진 물결과 같은 자리(앞 칸, 바닥 복제층 정렬 순서에서 한 번 반 앞).
    private Tilemap crackLeftLift, crackRightLift;
    private readonly List<TileChangeData> lightChanges = new List<TileChangeData>(512);
    private readonly List<TileChangeData> seaChanges = new List<TileChangeData>(512);
    private readonly List<TileChangeData> crackLeftChanges = new List<TileChangeData>(256);
    private readonly List<TileChangeData> crackRightChanges = new List<TileChangeData>(256);
    // cells와 같은 순서. 깔린 빛 단계·틈 빛 줄 수·틈 빛을 깐 칸 높이.
    private sbyte[] lastLightSteps = new sbyte[0];
    private sbyte[] lastCrackLeft = new sbyte[0];
    private sbyte[] lastCrackRight = new sbyte[0];
    private short[] crackPixels = new short[0];
    private bool[] crackDirty = new bool[0];
    private readonly List<int> crackDirtyOrders = new List<int>(512);
    // 영역 칸 번호 → cells 순서(없으면 -1). 틈 빛이 이웃 칸 높이를 찾는다.
    private int[] orderOfIndex = new int[0];
    // 바다 빛 고리를 그리는 물 칸(바닥이 없는 화면 안 물 칸, 파도 거리 순).
    private readonly List<SeaCell> seaCells = new List<SeaCell>(4096);
    private sbyte[] lastSeaSteps = new sbyte[0];
    // 용암 티어(물이 용암): 틈 빛은 용암 재질·색 그대로, 바다 빛 고리는 lavaLightColor.
    private bool bLava;
    // 티어 전환이면 직전 지형의 물이 용암이었는지. 첫 마루 전(아직 직전 지형인 칸)의 빛은 직전 티어 모양을 따른다.
    private bool bLavaBefore;
    private Material lavaCrackMaterial;
    private MaterialPropertyBlock lavaWaterBlock;
    private bool bCrackGlow;
    // 굴절 고리(화면 캡처를 미는 쿼드). 재생 중에만 켠다.
    private GameObject refractionObject;
    private MeshRenderer refractionRenderer;
    private MaterialPropertyBlock refractionBlock;
    private Mesh refractionMesh;
    // 쿼드 자리별 앞머리 일렁임(GetWaveDistance의 노이즈 몫). 셰이더가 고리 거리에 더해 칸 물결과 같은 굴곡으로 퍼진다.
    private Texture2D refractionWobble;
    private Color32[] refractionWobblePixels = new Color32[0];
    // 지금 복제층이 화면을 그리는 중(진입 배경 조각을 복제층 해안선에도 그린다).
    private bool bLiftsActive;

    private const int LightSteps = 16;
    private const int SeaSteps = 12;
    // 마루 높이의 이 비율 아래로 솟은 칸은 마루 빛을 받지 않는다.
    private const float CrestLightFrom = 0.5f;
    // 윗면 빛 타일 안쪽 진하기(테두리 = 255). 테두리가 조금 더 빛나 칸 모양이 읽힌다.
    private const byte LightFaceFill = 150;
    private static Tile lightFaceTile;
    private static Tile seaFaceTile;
    private static readonly int HDRIntensityId = Shader.PropertyToID("_HDRIntensity");
    private static readonly int RingCenterId = Shader.PropertyToID("_RingCenter");
    private static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");
    private static readonly int RingWidthId = Shader.PropertyToID("_RingWidth");
    private static readonly int RingPixelsId = Shader.PropertyToID("_RingPixels");
    private static readonly int WobbleTexId = Shader.PropertyToID("_WobbleTex");
    private static readonly int WobbleRangeId = Shader.PropertyToID("_WobbleRange");
    private static readonly int WobbleRectId = Shader.PropertyToID("_WobbleRect");
    // 굴절 일렁임 텍스처 한 칸의 월드 크기. 일렁임 무늬(굴곡 수 월드)와 섬 중심의 무늬 줄임(2 이내)을 고리 폭보다 훨씬 작은 오차로 따라간다.
    private const float WobbleTexelWorld = 0.25f;

    private struct SeaCell
    {
        public Vector3Int cell;
        public float key;
        public float amplitude;
    }

    public bool IsPlaying => bPlaying;

    /// <summary>시작 상태를 깔아 두었고 아직 재생하지 않았다(곧 Play가 돈다).</summary>
    public bool IsPrepared => bPrepared;

    /// <summary>
    /// 재생 중이면 true. _distance는 지금 새 지형으로 바뀐 선(중심에서 이 파도 거리 안의 칸은 바뀌었다), _speed는 그 선이 퍼지는 속도(파도 거리/초).
    /// 티어 전환은 첫 마루에서 바뀌므로 앞머리보다 그만큼 뒤다.
    /// </summary>
    public bool TryGetSwapFront(out float _distance, out float _speed)
    {
        _distance = frontDistance - SwapDistance;
        _speed = frontSpeed;
        return bPlaying;
    }

    /// <summary>섬 중심에서 _worldPos까지의 파도 거리(바닥 평면 거리 - 아이소는 세로를 두 배로 편다). 칸은 중심 위치로 잰다.</summary>
    public float GetWaveDistance(Vector3 _worldPos)
    {
        float distance = new Vector2(_worldPos.x - waveCenter.x, (_worldPos.y - waveCenter.y) * 2f).magnitude;
        if (settings.frontWobble <= 0f) return distance;

        // 앞머리 일렁임: 칸 자리마다 완만한 무늬(펄린 노이즈)만큼 거리를 늘이거나 줄인다. 무늬가 연속이라 이웃 칸은 비슷하게 밀려 물결이 끊기지 않는다.
        // 섬 중심 근처는 무늬를 줄여 첫 칸들이 한꺼번에 몰리거나 순서가 뒤집히지 않게 한다. 물 장식·구름도 이 거리를 써서 칸과 같이 바뀐다.
        float scale = Mathf.Max(0.001f, settings.frontWobbleScale);
        float noise = Mathf.PerlinNoise(_worldPos.x * scale + wobbleOffset.x, _worldPos.y * 2f * scale + wobbleOffset.y) * 2f - 1f;
        float nearCenter = Mathf.SmoothStep(0f, 1f, distance / 2f);
        return Mathf.Max(0f, distance + noise * settings.frontWobble * nearCenter);
    }

    /// <summary>마지막으로 준비(PrepareAfterGenerate)한 파도가 런 입장 파도인지(아니면 티어 전환).</summary>
    public bool IsEntryWave => bEntry;

    public void Initialize(EnvironmentSystem _environmentSystem, RoguelikeTileWaveSettings _settings)
    {
        environmentSystem = _environmentSystem;
        settings = _settings ?? new RoguelikeTileWaveSettings();
    }

    /// <summary>맵을 새로 깔기 직전에 부른다(티어 전환). 지금 화면 안 칸의 타일을 떠 둔다(아직 영역이 없으면 빈 상태로 친다).</summary>
    public void CaptureBefore()
    {
        bHasBefore = false;
        if (false == bHasRegion || false == BindLayers()) return;

        ReadLayers(floorLayers, floorBefore);
        ReadLayers(waterLayers, waterBefore);
        ReadLayers(shoreLayers, shoreBefore);
        bHasBefore = true;
    }

    /// <summary>
    /// 맵을 깐 직후에 부른다. 새 타일을 떠 두고 화면 안 칸을 파도 시작 상태로 되돌린다.
    /// _bEntry면 진입(물만 깔린 바다), 아니면 티어 전환(직전 지형). 티어 전환인데 직전 지형이 없으면 진입처럼 한다.
    /// _bLava면 새 지형의 물이 용암이다(틈 빛을 용암 빛으로, 바다 빛 고리를 용암 색으로).
    /// </summary>
    public void PrepareAfterGenerate(Vector3 _center, Vector2 _viewHalfSize, bool _bEntry, bool _bLava)
    {
        FinishImmediately();

        // 직전 지형은 바로 전에 준비한 파도의 새 지형이다(CaptureBefore가 그 화면을 떴다).
        bLavaBefore = bLava;
        bLava = _bLava;
        waveCenter = _center;
        waveSerial++;
        wobbleOffset = new Vector2(37.1f * (waveSerial % 1000), 91.7f * (waveSerial % 1000));
        if (false == BindLayers()) return;

        BuildRegion(_center, _viewHalfSize);
        if (false == bHasRegion) return;

        EnsureBuffers();
        ReadLayers(floorLayers, floorAfter);
        ReadLayers(waterLayers, waterAfter);
        ReadLayers(shoreLayers, shoreAfter);

        bEntry = _bEntry || false == bHasBefore;
        bHasBefore = false;

        BuildMasks();
        BuildWaterStart();

        WriteLayers(floorLayers, bEntry ? null : floorBefore);
        WriteLayers(shoreLayers, bEntry ? null : shoreBefore);
        WriteLayers(waterLayers, waterStart);

        PickAnimationReference();
        EnsureLifts();
        ApplyCrackMaterial();
        BuildOrder(_center);
        if (bEntry) BuildBackgroundPieces();
        bPrepared = true;
    }

    /// <summary>PrepareAfterGenerate 뒤에 재생한다. 끝나면 화면 안 칸이 전부 새 지형이다. 게임 시간으로 흐른다(일시정지면 멈춘다).</summary>
    public IEnumerator Play()
    {
        if (false == bPrepared) yield break;

        bPrepared = false;
        bPlaying = true;

        // 앞머리는 화면 안 첫 칸에서 출발한다. 그 앞(화면 밖 여유분) 칸은 첫 프레임에 바로 바뀐다.
        float startKey = cells.Count > 0 ? cells[0].key : 0f;
        for (int i = 0; i < cells.Count; i++)
        {
            if (false == cells[i].bInView) continue;
            startKey = cells[i].key;
            break;
        }

        float length = RippleLength;
        float lastKey = cells.Count > 0 ? cells[cells.Count - 1].key : startKey;
        float reachSpan = Mathf.Max(0f, lastKey - startKey);
        // 앞머리가 마지막 칸에 닿은 뒤 물결이 그 칸을 다 지나갈 때까지 간다.
        float span = reachSpan + length;
        float ease = Mathf.Clamp(settings.frontEaseOut, 0f, 0.8f);
        float seconds = span / Mathf.Max(0.1f, settings.frontSpeed);
        bool bAssemble = bEntry && pieces.Count > 0;
        float assembleSeconds = bAssemble ? Mathf.Max(0f, settings.backgroundAssembleSeconds) : 0f;
        if (bEntry && settings.entryCompleteSeconds > 0.05f)
        {
            // 진입 3~5단계가 entryCompleteSeconds 안에 끝나게 한다. 바닥은 앞머리가 닿는 칸에 깔리므로 마지막 칸에 닿으면 다 깔린 것이고,
            // 배경 조립(4단계)은 그때 시작해 남은 물결과 함께 돈다. 물결도 조립도 시간 안에 끝나도록 앞머리를 맞춘다(frontSpeed보다 느려지지 않는다).
            float budget = settings.entryCompleteSeconds;
            seconds = Mathf.Min(seconds, budget);
            float reachU = EaseInverse(span > 0f ? reachSpan / span : 0f, ease);
            if (bAssemble && reachU > 0.0001f && seconds * reachU + assembleSeconds > budget)
            {
                seconds = (budget - assembleSeconds) / reachU;
            }
        }
        seconds = Mathf.Max(0.05f, seconds);

        if (cellStage.Length < cells.Count) cellStage = new byte[cells.Count];
        Array.Clear(cellStage, 0, cells.Count);
        if (lastPixels.Length < cells.Count)
        {
            lastPixels = new short[cells.Count];
            lastOutlineSteps = new sbyte[cells.Count];
            liftShowsAfter = new bool[cells.Count];
            lastLightSteps = new sbyte[cells.Count];
            lastCrackLeft = new sbyte[cells.Count];
            lastCrackRight = new sbyte[cells.Count];
            crackPixels = new short[cells.Count];
            crackDirty = new bool[cells.Count];
        }
        Array.Clear(lastPixels, 0, cells.Count);
        Array.Clear(lastOutlineSteps, 0, cells.Count);
        Array.Clear(liftShowsAfter, 0, cells.Count);
        Array.Clear(lastLightSteps, 0, cells.Count);
        Array.Clear(lastCrackLeft, 0, cells.Count);
        Array.Clear(lastCrackRight, 0, cells.Count);
        Array.Clear(crackPixels, 0, cells.Count);
        Array.Clear(crackDirty, 0, cells.Count);
        crackDirtyOrders.Clear();
        ClearChanges();

        float seaLife = SeaRingLength;
        PrepareSeaRing(lastKey + length - seaLife);

        // 티어 전환: 화면 안 바닥·물·해안선을 전부 복제층으로 옮겨 그린다. 진입은 물만 시작 상태(섬 자리는 깊은 물)로 옮기고, 바닥은 물결이 닿는 칸부터 생긴다.
        // 물을 일부 칸만 복제층에 두면 그 가장자리에서 물 타일의 아랫부분이 앞 칸 물에 가려지지 않아 어두운 삼각형이 비친다.
        if (false == bEntry) FillLiftsWithBefore();
        else FillWaterLifts(waterStart);
        bLiftsActive = true;
        ShowRefraction(true);

        float elapsed = 0f;
        int next = 0;      // 아직 물결이 닿지 않은 첫 칸
        int done = 0;      // 물결이 다 지나가지 않은 첫 칸
        int seaNext = 0;   // 아직 빛 고리가 닿지 않은 첫 바다 칸
        int seaDone = 0;   // 빛 고리가 다 지나가지 않은 첫 바다 칸
        bool bAssembling = false;
        float assembleStart = 0f;

        frontSpeed = FrontSpeedAt(0f, span, seconds, ease);
        frontDistance = startKey;

        while (true)
        {
            // 앞머리는 중심에서 빠르고 바깥으로 갈수록 느려진다. 물결이 다 지나간 뒤(진입 배경 조립이 남았을 때)는 마지막 속도로 계속 퍼진다(구름·물 장식이 따라간다).
            float front = FrontAt(elapsed, startKey, span, seconds, ease);
            frontDistance = front;
            frontSpeed = FrontSpeedAt(elapsed, span, seconds, ease);

            if (bLiftsActive)
            {
                while (next < cells.Count && cells[next].key <= front)
                {
                    ReachCell(next);
                    next++;
                }

                for (int j = done; j < next; j++) UpdateCell(j, front - cells[j].key, length);
                while (done < next && StageDone == cellStage[done]) done++;

                // 바다 빛 고리: 앞머리를 따라 바다 칸을 지나간다(칸마다 seaLife 동안).
                while (seaNext < seaCells.Count && seaCells[seaNext].key <= front) seaNext++;
                while (seaDone < seaNext && front - seaCells[seaDone].key >= seaLife)
                {
                    QueueSea(seaDone, 0, false);
                    seaDone++;
                }
                for (int j = seaDone; j < seaNext; j++)
                {
                    float seaDistance = front - seaCells[j].key;
                    QueueSea(j, SeaRingStep(j, seaDistance, seaLife), SeaShowsBefore(seaDistance));
                }

                FlushCrack();
                FlushChanges();
                UpdateRefraction(front, lastKey + length);

                if (done >= cells.Count && seaDone >= seaCells.Count)
                {
                    // 모든 칸이 제자리·새 지형이다. 복제층을 비워 실제 층(해안선이 바닥 위에 그려지는 원래 순서)으로 돌린다.
                    ClearLifts();
                }
            }

            // 진입 4~5단계: 바닥이 다 깔리면(앞머리가 마지막 칸에 닿으면) 나머지 배경(해안선)이 퍼즐 조각처럼 하나씩 나타나 완성된다.
            bool bAssembled = false == bAssemble;
            if (bAssemble)
            {
                if (false == bAssembling && next >= cells.Count)
                {
                    bAssembling = true;
                    assembleStart = elapsed;
                    StartAssembly();
                }
                if (bAssembling) bAssembled = TickAssembly(elapsed - assembleStart);
            }

            if (false == bLiftsActive && bAssembled) break;
            yield return null;
            elapsed += Time.deltaTime;
        }

        // 칸마다 따로 바꾼 타일은 애니메이션(물결) 재생 시점이 제각각이라 주변과 이음선이 질 수 있다. 끝나면 영역을 한 번에 다시 깔아 맞춘다.
        WriteAllAfter();

        bPlaying = false;
    }

    /// <summary>앞머리 감속 곡선: u(0~1, 시간 비율) → 간 거리 비율. 처음 기울기 1 + _ease, 끝 기울기 1 - _ease.</summary>
    private static float Ease(float _u, float _ease) => _u + _ease * _u * (1f - _u);

    /// <summary>Ease의 역함수(간 거리 비율 → 시간 비율).</summary>
    private static float EaseInverse(float _ratio, float _ease)
    {
        _ratio = Mathf.Clamp01(_ratio);
        if (_ease <= 0.0001f) return _ratio;
        float b = 1f + _ease;
        return (b - Mathf.Sqrt(Mathf.Max(0f, b * b - 4f * _ease * _ratio))) / (2f * _ease);
    }

    /// <summary>재생 _elapsed초의 앞머리 거리. _seconds 뒤로는 마지막 속도로 계속 간다.</summary>
    private static float FrontAt(float _elapsed, float _startKey, float _span, float _seconds, float _ease)
    {
        if (_elapsed <= _seconds) return _startKey + _span * Ease(_elapsed / _seconds, _ease);
        return _startKey + _span + (_elapsed - _seconds) * (_span / _seconds) * (1f - _ease);
    }

    /// <summary>재생 _elapsed초의 앞머리 속도(파도 거리/초). 물결 뒤로는 마지막 속도 그대로다.</summary>
    private static float FrontSpeedAt(float _elapsed, float _span, float _seconds, float _ease)
    {
        float u = Mathf.Clamp01(_elapsed / _seconds);
        return Mathf.Max(0.1f, _span / _seconds * (1f + _ease * (1f - 2f * u)));
    }

    /// <summary>재생 도중 흐름이 끊길 때(포기·해제). 남은 칸을 바로 새 지형으로 채우고 솟아 있던 타일을 제자리에 놓는다.</summary>
    public void FinishImmediately()
    {
        if (false == bPlaying && false == bPrepared) return;

        bPlaying = false;
        bPrepared = false;

        WriteAllAfter();
    }

    /// <summary>로그라이크 시스템 해제. 재생 중이던 파도를 끝내고 이 파도가 만든 재질·메시·텍스처·굴절 쿼드를 지운다.</summary>
    public void Release()
    {
        FinishImmediately();

        if (null != refractionObject) UnityEngine.Object.Destroy(refractionObject);
        refractionObject = null;
        refractionRenderer = null;
        if (null != refractionMesh) UnityEngine.Object.Destroy(refractionMesh);
        refractionMesh = null;
        if (null != refractionWobble) UnityEngine.Object.Destroy(refractionWobble);
        refractionWobble = null;
        if (null != lavaCrackMaterial) UnityEngine.Object.Destroy(lavaCrackMaterial);
        lavaCrackMaterial = null;
    }

    // // 내부

    private bool BindLayers()
    {
        if (null == environmentSystem) return false;
        if (floorLayers.Count == 0 && waterLayers.Count == 0 && shoreLayers.Count == 0)
        {
            environmentSystem.GetVisualTilemapsForRun(floorLayers, waterLayers, shoreLayers);
        }

        return floorLayers.Count > 0;
    }

    /// <summary>기준 화면(+여유) 안에 중심이 들어오는 칸들의 최소 사각형. 섬은 매번 같은 자리라 한 번만 계산한다.</summary>
    private void BuildRegion(Vector3 _center, Vector2 _viewHalfSize)
    {
        if (bHasRegion) return;

        viewHalfSize = _viewHalfSize;
        Vector2 cellSize = environmentSystem.TileCellSize;
        float halfX = _viewHalfSize.x + settings.viewMarginTiles * cellSize.x;
        float halfY = _viewHalfSize.y + settings.viewMarginTiles * cellSize.y;

        BoundsInt grid = environmentSystem.TileGridBoundsForRun;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        for (int y = grid.yMin; y < grid.yMax; y++)
        {
            for (int x = grid.xMin; x < grid.xMax; x++)
            {
                Vector3 pos = environmentSystem.GetTileCellCenterWorldForRun(new Vector3Int(x, y, 0));
                if (Mathf.Abs(pos.x - _center.x) > halfX || Mathf.Abs(pos.y - _center.y) > halfY) continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (minX > maxX) return;

        region = new BoundsInt(minX, minY, 0, maxX - minX + 1, maxY - minY + 1, 1);
        bHasRegion = true;
    }

    private void EnsureBuffers()
    {
        int size = region.size.x * region.size.y;
        if (null != emptyBlock && emptyBlock.Length == size) return;

        emptyBlock = new TileBase[size];
        floorBefore = AllocLayers(floorLayers.Count, size);
        floorAfter = AllocLayers(floorLayers.Count, size);
        waterBefore = AllocLayers(waterLayers.Count, size);
        waterAfter = AllocLayers(waterLayers.Count, size);
        waterStart = AllocLayers(waterLayers.Count, size);
        shoreBefore = AllocLayers(shoreLayers.Count, size);
        shoreAfter = AllocLayers(shoreLayers.Count, size);
        landCell = new bool[size];
        nearLand = new bool[size];
    }

    private static TileBase[][] AllocLayers(int _layers, int _size)
    {
        TileBase[][] result = new TileBase[_layers][];
        for (int i = 0; i < _layers; i++) result[i] = new TileBase[_size];
        return result;
    }

    private void ReadLayers(List<Tilemap> _layers, TileBase[][] _into)
    {
        if (null == _into) return;
        for (int i = 0; i < _layers.Count; i++) _layers[i].GetTilesBlockNonAlloc(region, _into[i]);
    }

    /// <summary>_from이 null이면 영역을 비운다.</summary>
    private void WriteLayers(List<Tilemap> _layers, TileBase[][] _from)
    {
        if (false == bHasRegion || null == emptyBlock) return;
        for (int i = 0; i < _layers.Count; i++) _layers[i].SetTilesBlock(region, null != _from ? _from[i] : emptyBlock);
    }

    private void WriteAllAfter()
    {
        ClearLifts();
        WriteLayers(floorLayers, floorAfter);
        WriteLayers(waterLayers, waterAfter);
        WriteLayers(shoreLayers, shoreAfter);

        // 진입 배경 조립 도중에 끝났으면(포기·해제) 반쯤 나타난 조각이 있다. 같은 타일을 다시 깔면 낮춰 둔 진하기가 그대로 남으므로 불투명으로 되돌린다.
        if (bAssemblyRunning)
        {
            for (int p = 0; p < pieces.Count; p++) SetPieceAlpha(pieces[p], 1f);
            bAssemblyRunning = false;
        }

        // 칸마다 따로 깐 물·해안선(물 모서리)은 애니메이션 시작 시점이 제각각이고, 같은 타일을 다시 깔면 시점이 그대로 남는다.
        // 끝에 영역의 물·해안선을 한 기준 시간으로 맞춘다(물과 물 모서리가 따로 출렁이지 않게).
        SyncRegionAnimation(waterLayers);
        SyncRegionAnimation(shoreLayers);
    }

    private void SyncRegionAnimation(List<Tilemap> _layers)
    {
        if (false == bHasAnimRef || false == bHasRegion || null == emptyBlock) return;
        int size = emptyBlock.Length;
        for (int l = 0; l < _layers.Count; l++)
        {
            Tilemap layer = _layers[l];
            float time = ReferenceAnimationTime(layer);
            for (int i = 0; i < size; i++)
            {
                Vector3Int cell = CellOf(i);
                if (layer.HasTile(cell)) layer.SetAnimationTime(cell, time);
            }
        }
    }

    /// <summary>
    /// 칸을 맞출 기준 애니메이션 시간. 그 층의 기준 칸(영역 바로 바깥, 파도가 건드리지 않는 칸)에 타일이 있으면 그 시간,
    /// 없으면(물 모서리처럼 섬 둘레에만 있는 층) 물 층의 기준 시간을 쓴다 - 맵 생성 때 물과 함께 깔려 같은 시점에 돌던 층이다.
    /// 타일이 없는 칸의 시간(0)을 쓰면 그 칸만 처음부터 다시 돌아 물과 어긋난다.
    /// </summary>
    private float ReferenceAnimationTime(Tilemap _layer)
    {
        if (null != _layer && _layer.HasTile(animRefCell)) return _layer.GetAnimationTime(animRefCell);
        for (int l = 0; l < waterLayers.Count; l++)
        {
            if (waterLayers[l].HasTile(animRefCell)) return waterLayers[l].GetAnimationTime(animRefCell);
        }

        return 0f;
    }

    private void BuildMasks()
    {
        int size = emptyBlock.Length;
        int width = region.size.x;
        int height = region.size.y;

        Array.Clear(nearLand, 0, size);
        for (int i = 0; i < size; i++)
        {
            bool bNewLand = HasAnyTile(floorAfter, i);
            landCell[i] = bNewLand || (false == bEntry && HasAnyTile(floorBefore, i));
            if (false == bNewLand) continue;

            int x = i % width;
            int y = i / width;
            for (int dy = -1; dy <= 1; dy++)
            {
                int ny = y + dy;
                if (ny < 0 || ny >= height) continue;

                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    if (nx < 0 || nx >= width) continue;
                    nearLand[nx + ny * width] = true;
                }
            }
        }
    }

    /// <summary>
    /// 파도 시작 상태의 물. 진입은 새 물, 티어 전환은 직전 물이 기준이다.
    /// 진입에서 섬 자리·물가 칸은 깊은 물 한 칸의 타일(물 층마다)로 채운다 - 아직 바닥이 없는 자리라 바다로 보여야 하고,
    /// 물가 모양 물을 미리 두면 섬 윤곽이 먼저 보인다. 그 칸의 물은 물결 꼭대기에서 바닥과 함께 새 물로 바뀐다.
    /// </summary>
    private void BuildWaterStart()
    {
        int size = emptyBlock.Length;
        int sample = FindDeepWaterSample(bEntry ? waterAfter : waterBefore);
        if (sample < 0) sample = FindDeepWaterSample(waterAfter);

        for (int l = 0; l < waterLayers.Count; l++)
        {
            TileBase filler = sample >= 0 ? waterAfter[l][sample] : null;
            TileBase[] baseTiles = bEntry ? waterAfter[l] : waterBefore[l];
            TileBase[] start = waterStart[l];

            for (int i = 0; i < size; i++)
            {
                start[i] = bEntry && (landCell[i] || nearLand[i]) ? filler : baseTiles[i];
            }
        }
    }

    /// <summary>
    /// 들어 올리는 복제층(리프트)을 (처음 한 번) 만든다. 바닥 층마다 같은 격자·재질의 복제를 둔다. 모든 타일 층보다 위에 그리고 칸 단위로 정렬한다(솟은 블록끼리 앞뒤가 맞게).
    /// 지면 스텐실(GroundStencil - 구름 그림자를 가리는 층)의 복제에는 솟은 바닥 블록과 같은 타일을 놓아, 블록이 그려진 자리의 물 스텐실을 지운다.
    /// 진입에서 블록은 아직 물(물 스텐실 포함)뿐인 칸 위로 솟아오르므로, 이것이 없으면 솟은 블록 위로 구름 그림자가 비친다.
    ///
    /// 실제 바닥 타일은 들지 않고 제자리에 둔다 - 들면 원래 자리가 비어 물이 드러나고, 스텐실이 어긋나 구름 그림자가 육지에 비친다.
    /// </summary>
    private bool EnsureLifts()
    {
        if (lifts.Count > 0)
        {
            EnsureOverlayLifts();
            return true;
        }
        if (floorLayers.Count == 0) return false;

        bStencilFloorLayer = new bool[floorLayers.Count];
        for (int l = 0; l < floorLayers.Count; l++)
        {
            TilemapRenderer layerRenderer = null != floorLayers[l] ? floorLayers[l].GetComponent<TilemapRenderer>() : null;
            Material material = null != layerRenderer ? layerRenderer.sharedMaterial : null;
            bStencilFloorLayer[l] = null != material && null != material.shader && material.shader.name.Contains("Stencil");
        }

        liftShapeLayer = -1;
        for (int l = 0; l < floorLayers.Count; l++)
        {
            if (null != floorLayers[l] && false == bStencilFloorLayer[l]) { liftShapeLayer = l; break; }
        }

        int highestOrder = int.MinValue;
        HighestOrder(floorLayers, ref highestOrder);
        HighestOrder(waterLayers, ref highestOrder);
        HighestOrder(shoreLayers, ref highestOrder);
        if (int.MinValue == highestOrder) return false;

        // 복제층끼리의 앞뒤를 실제 층과 같게 맞춘다. 실제 바닥·물은 정렬 순서가 같고 둘 다 칸 단위 정렬이라 칸 위치로 앞뒤가 섞인다
        // (앞 칸의 물이 뒤 칸 블록의 절벽 아랫부분을 덮는다). 그래서 실제 정렬 순서가 같은 층은 복제층도 같은 순서를 주고,
        // 순서가 다른 층은 그 차례대로 모든 실제 층보다 위에 번호를 준다.
        List<LiftSlot> slots = new List<LiftSlot>(floorLayers.Count + waterLayers.Count + shoreLayers.Count);
        AddSlots(slots, floorLayers, 0);
        AddSlots(slots, waterLayers, 1);
        AddSlots(slots, shoreLayers, 2);
        slots.Sort((x, y) => x.rank != y.rank ? x.rank.CompareTo(y.rank) : x.sequence.CompareTo(y.sequence));

        for (int l = 0; l < floorLayers.Count; l++) lifts.Add(null);
        for (int l = 0; l < waterLayers.Count; l++) waterLifts.Add(null);
        for (int l = 0; l < shoreLayers.Count; l++) shoreLifts.Add(null);

        int liftOrder = highestOrder;
        for (int i = 0; i < slots.Count; i++)
        {
            LiftSlot slot = slots[i];
            if (0 == i || slots[i - 1].rank != slot.rank) liftOrder++;
            // 바닥 블록 층이 없으면 스텐실 복제층에 놓을 모양이 없다.
            if (0 == slot.group && bStencilFloorLayer[slot.layer] && liftShapeLayer < 0) continue;

            Tilemap tilemap = CreateLift(slot.source, slot.renderer, liftOrder, "RoguelikeTileWaveLift_" + slot.source.name);
            if (0 == slot.group) lifts[slot.layer] = tilemap;
            else if (1 == slot.group) waterLifts[slot.layer] = tilemap;
            else shoreLifts[slot.layer] = tilemap;
        }

        for (int l = 0; l < lifts.Count; l++) floorChanges.Add(new List<TileChangeData>(512));

        // 테두리: 바닥 블록 복제층과 같은 재질·정렬 순서에 아주 조금 앞으로 놓아 칸마다 제 바닥 바로 다음에 그린다.
        Tilemap groundLift = liftShapeLayer >= 0 ? lifts[liftShapeLayer] : null;
        if (null != groundLift)
        {
            Tilemap ground = floorLayers[liftShapeLayer];
            int groundOrder = groundLift.GetComponent<TilemapRenderer>().sortingOrder;
            outlineLift = CreateLift(ground, ground.GetComponent<TilemapRenderer>(), groundOrder, "RoguelikeTileWaveOutline");
            outlineLift.transform.localPosition += new Vector3(0f, -QuakeTileRipple.SortNudge, 0f);
        }

        EnsureOverlayLifts();
        return true;
    }

    /// <summary>
    /// 빛·틈 빛·바다 빛 복제층을 지금 설정에 맞춘다(준비할 때마다). 재질을 넣거나 빼고 틈 빛을 켜고 끈 것이 다음 파도부터 반영된다.
    /// 필요 없어진 층은 지운다(준비 전에 복제층은 비어 있다). 틈 빛 재질은 티어마다 ApplyCrackMaterial이 고른다.
    /// </summary>
    private void EnsureOverlayLifts()
    {
        Tilemap groundLift = liftShapeLayer >= 0 && liftShapeLayer < lifts.Count ? lifts[liftShapeLayer] : null;
        Tilemap ground = null != groundLift ? floorLayers[liftShapeLayer] : null;
        int groundOrder = null != groundLift ? groundLift.GetComponent<TilemapRenderer>().sortingOrder : 0;
        Material light = settings.lightMaterial;

        // 윗면 빛: 테두리 다음. 틈 빛: 지진 물결과 같이 한 번 반 앞(앞 칸 바닥 다음에 그려 뒤 칸 옆벽을 덮는다).
        lightLift = SyncOverlayLift(lightLift, null != ground && null != light, ground, groundOrder, 1.25f, "RoguelikeTileWaveLight", light);
        bool bCrack = null != ground && settings.crackGlow;
        crackLeftLift = SyncOverlayLift(crackLeftLift, bCrack, ground, groundOrder, 1.5f, "RoguelikeTileWaveCrackL", null);
        crackRightLift = SyncOverlayLift(crackRightLift, bCrack, ground, groundOrder, 1.5f, "RoguelikeTileWaveCrackR", null);

        // 바다 빛 고리: 물 복제층 다음(같은 칸 물 위, 앞 칸 블록 아래).
        Tilemap water = waterLayers.Count > 0 ? waterLayers[0] : null;
        Tilemap waterLift = waterLifts.Count > 0 ? waterLifts[0] : null;
        int waterOrder = null != waterLift ? waterLift.GetComponent<TilemapRenderer>().sortingOrder : 0;
        seaLift = SyncOverlayLift(seaLift, null != light && null != water && null != waterLift, water, waterOrder, 1f, "RoguelikeTileWaveSeaLight", light);
    }

    /// <summary>_bWanted면 장식 복제층을 두고(없으면 만들고, _material이 있으면 재질을 맞춘다) 아니면 지운다. 남은 층을 돌려준다.</summary>
    private static Tilemap SyncOverlayLift(Tilemap _lift, bool _bWanted, Tilemap _source, int _order, float _nudge, string _name, Material _material)
    {
        if (false == _bWanted)
        {
            if (null != _lift) UnityEngine.Object.Destroy(_lift.gameObject);
            return null;
        }

        if (null == _lift) return CreateOverlayLift(_source, _order, _nudge, _name, _material);
        if (null != _material) _lift.GetComponent<TilemapRenderer>().sharedMaterial = _material;
        return _lift;
    }

    /// <summary>_source와 같은 격자의 장식 복제층. 정렬 순서는 _order, 같은 칸 바닥보다 SortNudge × _nudge만큼 앞이다. _material이 null이면 _source 재질.</summary>
    private static Tilemap CreateOverlayLift(Tilemap _source, int _order, float _nudge, string _name, Material _material)
    {
        Tilemap lift = CreateLift(_source, _source.GetComponent<TilemapRenderer>(), _order, _name);
        lift.transform.localPosition += new Vector3(0f, -QuakeTileRipple.SortNudge * _nudge, 0f);
        if (null != _material) lift.GetComponent<TilemapRenderer>().sharedMaterial = _material;
        return lift;
    }

    /// <summary>
    /// 틈 빛 재질을 이번 티어에 맞춘다. 용암 티어는 물(용암) 층 재질 사본에 그 층의 HDR 세기를 담아(지진 물결과 같은 용암 빛·블룸),
    /// 다른 티어는 가산 빛 재질(물빛으로 물들인다). 쓸 재질이 없거나 꺼 두면 그리지 않는다.
    /// </summary>
    private void ApplyCrackMaterial()
    {
        Material material = null;
        if (settings.crackGlow && null != crackLeftLift && null != crackRightLift)
        {
            material = bLava ? LavaCrackMaterial() : settings.lightMaterial;
        }

        bCrackGlow = null != material;
        if (false == bCrackGlow) return;

        crackLeftLift.GetComponent<TilemapRenderer>().sharedMaterial = material;
        crackRightLift.GetComponent<TilemapRenderer>().sharedMaterial = material;
    }

    /// <summary>
    /// 물(용암) 층 재질 사본에 그 층 속성 블록의 HDR 세기를 옮긴 것. Individual 모드 타일맵에 속성 블록을 넣으면 칸마다 그림 연결이 깨져(지진 물결 실측) 사본을 쓴다.
    /// </summary>
    private Material LavaCrackMaterial()
    {
        TilemapRenderer water = waterLayers.Count > 0 && null != waterLayers[0] ? waterLayers[0].GetComponent<TilemapRenderer>() : null;
        Material source = null != water ? water.sharedMaterial : null;
        if (null == source) return null;

        if (null == lavaCrackMaterial || lavaCrackMaterial.shader != source.shader)
        {
            if (null != lavaCrackMaterial) UnityEngine.Object.Destroy(lavaCrackMaterial);
            lavaCrackMaterial = new Material(source) { name = source.name + " (RoguelikeCrack)" };
        }
        else
        {
            lavaCrackMaterial.CopyPropertiesFromMaterial(source);
        }

        if (null == lavaWaterBlock) lavaWaterBlock = new MaterialPropertyBlock();
        water.GetPropertyBlock(lavaWaterBlock);
        if (lavaWaterBlock.HasFloat(HDRIntensityId)) lavaCrackMaterial.SetFloat(HDRIntensityId, lavaWaterBlock.GetFloat(HDRIntensityId));
        return lavaCrackMaterial;
    }

    private struct LiftSlot
    {
        public int group;     // 0 바닥, 1 물, 2 해안선
        public int layer;     // 그 묶음 안의 층 번호
        public int rank;      // 실제 층의 앞뒤 순서
        public int sequence;  // 순서가 같을 때 원래 나열 순서
        public Tilemap source;
        public TilemapRenderer renderer;
    }

    private static void AddSlots(List<LiftSlot> _slots, List<Tilemap> _layers, int _group)
    {
        for (int l = 0; l < _layers.Count; l++)
        {
            Tilemap source = _layers[l];
            TilemapRenderer renderer = null != source ? source.GetComponent<TilemapRenderer>() : null;
            if (null == renderer) continue;

            _slots.Add(new LiftSlot
            {
                group = _group,
                layer = l,
                rank = renderer.sortingOrder,
                sequence = _slots.Count,
                source = source,
                renderer = renderer,
            });
        }
    }

    private static Tilemap CreateLift(Tilemap _source, TilemapRenderer _sourceRenderer, int _order, string _name)
    {
        GameObject go = new GameObject(_name);
        go.transform.SetParent(_source.transform.parent, false);
        go.transform.localPosition = _source.transform.localPosition;
        go.transform.localRotation = _source.transform.localRotation;
        go.transform.localScale = _source.transform.localScale;

        Tilemap tilemap = go.AddComponent<Tilemap>();
        tilemap.orientation = _source.orientation;
        tilemap.orientationMatrix = _source.orientationMatrix;
        tilemap.tileAnchor = _source.tileAnchor;
        tilemap.color = _source.color;

        TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
        renderer.sharedMaterial = _sourceRenderer.sharedMaterial;
        renderer.sortingLayerID = _sourceRenderer.sortingLayerID;
        renderer.sortingOrder = _order;
        // 칸마다 따로 정렬해야 솟은 칸과 이웃 칸의 앞뒤가 맞는다.
        renderer.mode = TilemapRenderer.Mode.Individual;
        return tilemap;
    }

    private static void HighestOrder(List<Tilemap> _layers, ref int _order)
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            TilemapRenderer renderer = null != _layers[i] ? _layers[i].GetComponent<TilemapRenderer>() : null;
            if (null != renderer && renderer.sortingOrder > _order) _order = renderer.sortingOrder;
        }
    }

    private void ClearLifts()
    {
        if (false == bHasRegion || null == emptyBlock) return;
        for (int l = 0; l < lifts.Count; l++)
        {
            if (null != lifts[l]) lifts[l].SetTilesBlock(region, emptyBlock);
        }
        for (int l = 0; l < shoreLifts.Count; l++)
        {
            if (null != shoreLifts[l]) shoreLifts[l].SetTilesBlock(region, emptyBlock);
        }
        for (int l = 0; l < waterLifts.Count; l++)
        {
            if (null != waterLifts[l]) waterLifts[l].SetTilesBlock(region, emptyBlock);
        }
        if (null != outlineLift) outlineLift.SetTilesBlock(region, emptyBlock);
        if (null != lightLift) lightLift.SetTilesBlock(region, emptyBlock);
        if (null != seaLift) seaLift.SetTilesBlock(region, emptyBlock);
        if (null != crackLeftLift) crackLeftLift.SetTilesBlock(region, emptyBlock);
        if (null != crackRightLift) crackRightLift.SetTilesBlock(region, emptyBlock);

        bLiftsActive = false;
        ShowRefraction(false);
        ClearChanges();
    }

    /// <summary>티어 전환 시작: 복제층에 직전 지형을 그대로 옮겨 놓는다(제자리).</summary>
    private void FillLiftsWithBefore()
    {
        for (int l = 0; l < lifts.Count; l++)
        {
            if (null != lifts[l]) lifts[l].SetTilesBlock(region, LiftTilesOf(floorBefore, l));
        }
        for (int l = 0; l < shoreLifts.Count; l++)
        {
            if (null != shoreLifts[l]) shoreLifts[l].SetTilesBlock(region, shoreBefore[l]);
        }
        for (int j = 0; j < cells.Count; j++)
        {
            SyncLiftAnimation(shoreLifts, shoreLayers, cells[j].cell);
        }

        FillWaterLifts(waterBefore);
    }

    /// <summary>물 복제층에 영역 전체의 물을 깐다(출렁이지 않으므로 변형 잠금은 그대로, 애니메이션 시간만 실제 층에 맞춘다).</summary>
    private void FillWaterLifts(TileBase[][] _water)
    {
        for (int l = 0; l < waterLifts.Count; l++)
        {
            if (null != waterLifts[l]) waterLifts[l].SetTilesBlock(region, _water[l]);
        }

        if (false == bHasAnimRef) return;
        int size = emptyBlock.Length;
        for (int i = 0; i < size; i++) SyncLiftAnimation(waterLifts, waterLayers, CellOf(i));
    }

    /// <summary>복제층 칸의 애니메이션(물결) 시간을 실제 층의 기준 칸에 맞춘다. 안 맞추면 복제층과 실제 층의 물이 따로 출렁여 이음선이 진다.</summary>
    private void SyncLiftAnimation(List<Tilemap> _lifts, List<Tilemap> _sources, Vector3Int _cell)
    {
        if (false == bHasAnimRef) return;
        for (int l = 0; l < _lifts.Count && l < _sources.Count; l++)
        {
            if (null == _lifts[l] || false == _lifts[l].HasTile(_cell)) continue;
            _lifts[l].SetAnimationTime(_cell, ReferenceAnimationTime(_sources[l]));
        }
    }

    /// <summary>복제층 l에 놓을 타일 배열. 스텐실 복제층은 바닥 블록과 같은 모양이어야 하므로 블록 층의 타일을 쓴다.</summary>
    private TileBase[] LiftTilesOf(TileBase[][] _floorTiles, int _liftIndex)
    {
        return bStencilFloorLayer[_liftIndex] ? _floorTiles[liftShapeLayer] : _floorTiles[_liftIndex];
    }

    /// <summary>바닥도 해안선도 없고 섬 둘레도 아닌 물 칸(깊은 물) 하나.</summary>
    private int FindDeepWaterSample(TileBase[][] _water)
    {
        if (waterLayers.Count == 0) return -1;

        int size = emptyBlock.Length;
        for (int i = 0; i < size; i++)
        {
            if (null == _water[0][i] || nearLand[i] || landCell[i] || HasAnyTile(shoreAfter, i)) continue;
            return i;
        }

        return -1;
    }

    /// <summary>애니메이션 기준 칸: 영역 바로 바깥 칸(생성 때 깔린 채 파도가 건드리지 않는다).</summary>
    private void PickAnimationReference()
    {
        bHasAnimRef = false;

        BoundsInt grid = environmentSystem.TileGridBoundsForRun;
        int midY = region.yMin + region.size.y / 2;
        if (region.xMin - 1 >= grid.xMin) animRefCell = new Vector3Int(region.xMin - 1, midY, 0);
        else if (region.xMax < grid.xMax) animRefCell = new Vector3Int(region.xMax, midY, 0);
        else return;

        bHasAnimRef = true;
    }

    private void BuildOrder(Vector3 _center)
    {
        cells.Clear();

        Vector2 cellSize = environmentSystem.TileCellSize;
        int width = region.size.x;
        int count = region.size.x * region.size.y;

        for (int i = 0; i < count; i++)
        {
            bool bNeeded = bEntry
                // 진입: 물은 이미 깔려 있다. 육지 칸과 그 둘레 물가 칸만 파도로 바꾼다(해안선은 4단계 배경 조립에서).
                ? nearLand[i]
                // 티어 전환: 바닥이 있거나(직전·새 지형 어느 쪽이든 - 물결에 출렁인다) 물·해안선 타일이 바뀌는 칸.
                // 바닥 없이 물·해안선이 그대로인 칸(먼 바다)은 바꿀 것도 출렁일 것도 없다(복제층에는 그대로 깔려 있다).
                : HasAnyTile(floorAfter, i) || HasAnyTile(floorBefore, i)
                  || false == SameTiles(waterBefore, waterAfter, i) || false == SameTiles(shoreBefore, shoreAfter, i);
            if (false == bNeeded) continue;

            Vector3Int cell = CellOf(i);
            Vector3 pos = environmentSystem.GetTileCellCenterWorldForRun(cell);
            Vector2 offset = new Vector2(pos.x - _center.x, pos.y - _center.y);

            cells.Add(new WaveCell
            {
                index = i,
                cell = cell,
                // 바닥 평면 거리(아이소는 세로가 절반으로 눌려 보이므로 세로를 두 배로 편다). 중앙에서 동심원으로 퍼지며 앞머리가 살짝 일렁인다.
                key = GetWaveDistance(pos),
                // 화면 가장자리에 걸친 칸(중심은 밖이어도 일부가 보이는 칸)도 화면 안으로 친다.
                bInView = Mathf.Abs(offset.x) <= viewHalfSize.x + cellSize.x && Mathf.Abs(offset.y) <= viewHalfSize.y + cellSize.y,
                worldPos = pos,
            });
        }

        cells.Sort(cellComparer);

        if (orderOfIndex.Length != count) orderOfIndex = new int[count];
        for (int i = 0; i < count; i++) orderOfIndex[i] = -1;
        for (int j = 0; j < cells.Count; j++) orderOfIndex[cells[j].index] = j;

        // 바다 빛 고리 자리: 직전·새 지형 어느 쪽에도 바닥이 없는 화면 안 물 칸. 섬 가장자리(새 바닥이 있는 가장 먼 칸)에서 가장 밝다.
        seaCells.Clear();
        landEdgeKey = 0f;
        for (int j = 0; j < cells.Count; j++)
        {
            if (HasAnyTile(floorAfter, cells[j].index)) landEdgeKey = Mathf.Max(landEdgeKey, cells[j].key);
        }
        if (null == seaLift || settings.seaRing <= 0f) return;

        for (int i = 0; i < count; i++)
        {
            if (landCell[i] || false == HasAnyTile(waterAfter, i)) continue;

            Vector3Int cell = CellOf(i);
            Vector3 pos = environmentSystem.GetTileCellCenterWorldForRun(cell);
            if (Mathf.Abs(pos.x - _center.x) > viewHalfSize.x + cellSize.x || Mathf.Abs(pos.y - _center.y) > viewHalfSize.y + cellSize.y) continue;

            seaCells.Add(new SeaCell { cell = cell, key = GetWaveDistance(pos) });
        }

        seaCells.Sort(seaCellComparer);
    }

    // 새 지형 바닥이 있는 가장 먼 칸의 파도 거리(BuildOrder). 바다 빛 고리가 여기서부터 옅어진다.
    private float landEdgeKey;

    /// <summary>바다 빛 고리가 한 칸을 지나가는 거리: 물결 첫 마루 하나(반 파장).</summary>
    private float SeaRingLength => Mathf.Max(0.1f, settings.rippleWavelength) * 0.5f;

    /// <summary>
    /// 바다 칸마다 고리 세기를 정한다. 섬 가장자리에서 1, _reach(물결이 끝날 때 고리가 다 지나갈 수 있는 가장 먼 거리)로 갈수록 0이다.
    /// 그보다 먼 칸은 고리가 닿기 전에 파도가 끝나므로 뺀다(끝나는 순간 빛이 툭 꺼지지 않게).
    /// </summary>
    private void PrepareSeaRing(float _reach)
    {
        int keep = 0;
        float from = Mathf.Min(landEdgeKey, _reach);
        for (int j = 0; j < seaCells.Count; j++)
        {
            SeaCell sea = seaCells[j];
            if (sea.key >= _reach) break;

            sea.amplitude = 1f - Mathf.SmoothStep(0f, 1f, (sea.key - from) / Mathf.Max(0.01f, _reach - from));
            seaCells[keep++] = sea;
        }

        seaCells.RemoveRange(keep, seaCells.Count - keep);
        if (lastSeaSteps.Length < seaCells.Count) lastSeaSteps = new sbyte[seaCells.Count];
        Array.Clear(lastSeaSteps, 0, seaCells.Count);
    }

    /// <summary>바다 칸 _order에 고리가 _distance만큼 지나간 때의 빛 단계(0 = 없음). 한 마루가 부드럽게 솟았다 진다.</summary>
    private sbyte SeaRingStep(int _order, float _distance, float _life)
    {
        if (_distance <= 0f || _distance >= _life) return 0;

        float wave = Mathf.Sin(Mathf.PI * _distance / _life);
        float light = Mathf.Clamp01(settings.seaRing) * seaCells[_order].amplitude * wave * wave;
        return (sbyte)Mathf.Clamp(Mathf.RoundToInt(light * SeaSteps), 0, SeaSteps);
    }

    /// <summary>
    /// 티어 전환에서 고리가 _distance만큼 지난 바다 칸이 아직 직전 물로 보이는지(물은 첫 마루에서 바뀐다). 직전과 새 티어의 빛 모양이 같으면 가릴 필요가 없어 false.
    /// </summary>
    private bool SeaShowsBefore(float _distance) => false == bEntry && bLavaBefore != bLava && _distance < SwapDistance;

    /// <summary>바다 칸 빛을 _step 단계로 바꾸도록 모은다(0이면 걷는다). _bBefore면 직전 티어 색(아직 직전 물로 보이는 칸)이다.</summary>
    private void QueueSea(int _order, sbyte _step, bool _bBefore)
    {
        // 단계와 색(직전/새 티어)이 그대로면 다시 깔지 않는다. 단계는 SeaSteps(12) 이하라 직전 색은 32를 더해 구분한다.
        sbyte key = (sbyte)(_step > 0 && _bBefore ? _step + 32 : _step);
        if (key == lastSeaSteps[_order]) return;

        lastSeaSteps[_order] = key;
        Vector3Int cell = seaCells[_order].cell;
        if (_step <= 0)
        {
            seaChanges.Add(new TileChangeData(cell, null, Color.white, Matrix4x4.identity));
            return;
        }

        bool bLavaColor = _bBefore ? bLavaBefore : bLava;
        Color color = bLavaColor ? settings.lavaLightColor : settings.seaRingColor;
        color.a = _step / (float)SeaSteps;
        seaChanges.Add(new TileChangeData(cell, SeaFaceTile, color, Matrix4x4.identity));
    }

    /// <summary>칸 윗면(마름모) 빛 타일: 테두리가 조금 더 밝은 흰 마름모. 칸 색(알파 = 세기)으로 물들여 가산으로 그린다.</summary>
    private static Tile LightFaceTile
    {
        get
        {
            if (null == lightFaceTile) lightFaceTile = QuakeTileRipple.CreateFaceTile("RoguelikeWaveLightFace", LightFaceFill);
            return lightFaceTile;
        }
    }

    /// <summary>
    /// 바다 빛 고리 타일: 고르게 찬 마름모. 테두리는 이웃 칸과 같은 픽셀에 겹치므로 절반 세기로 두어, 더해져도 안쪽과 같은 밝기가 된다
    /// (물 위에 칸 격자가 그려지지 않는다).
    /// </summary>
    private static Tile SeaFaceTile
    {
        get
        {
            if (null == seaFaceTile) seaFaceTile = QuakeTileRipple.CreateFaceTile("RoguelikeWaveSeaFace", 255, 128);
            return seaFaceTile;
        }
    }

    /// <summary>진입 4단계용: 해안선이 있는 칸을 정사각 조각(퍼즐)으로 묶고 순서를 섞는다.</summary>
    private void BuildBackgroundPieces()
    {
        pieceCells.Clear();
        pieces.Clear();
        if (shoreLayers.Count == 0) return;

        int width = region.size.x;
        int height = region.size.y;
        int side = Mathf.Max(1, settings.backgroundPieceCells);

        for (int py = 0; py < height; py += side)
        {
            for (int px = 0; px < width; px += side)
            {
                int first = pieceCells.Count;
                for (int y = py; y < Mathf.Min(height, py + side); y++)
                {
                    for (int x = px; x < Mathf.Min(width, px + side); x++)
                    {
                        int i = x + y * width;
                        if (HasAnyTile(shoreAfter, i)) pieceCells.Add(i);
                    }
                }

                if (pieceCells.Count > first) pieces.Add(new BackgroundPiece { firstCell = first, cellCount = pieceCells.Count - first });
            }
        }

        // 퍼즐처럼 하나씩: 순서를 섞는다.
        for (int i = pieces.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            BackgroundPiece tmp = pieces[i];
            pieces[i] = pieces[j];
            pieces[j] = tmp;
        }
    }

    // 배경 조립 진행(StartAssembly 뒤 TickAssembly가 넘긴다).
    private int nextPiece;
    private float pieceFadeSeconds;
    // 조립이 시작됐고 아직 모든 조각이 불투명해지지 않았다(도중에 끝나면 WriteAllAfter가 마저 불투명으로 맞춘다).
    private bool bAssemblyRunning;

    /// <summary>배경 조립을 시작한다. 조각마다 나타나는 시각을 backgroundAssembleSeconds 안에 고르게 나눈다.</summary>
    private void StartAssembly()
    {
        float total = Mathf.Max(0.01f, settings.backgroundAssembleSeconds);
        pieceFadeSeconds = Mathf.Clamp(settings.backgroundPieceFadeSeconds, 0.01f, total);
        float spread = Mathf.Max(0f, total - pieceFadeSeconds);
        float step = pieces.Count > 1 ? spread / (pieces.Count - 1) : 0f;

        for (int p = 0; p < pieces.Count; p++)
        {
            BackgroundPiece piece = pieces[p];
            piece.startTime = p * step;
            pieces[p] = piece;
        }

        nextPiece = 0;
        bAssemblyRunning = true;
    }

    /// <summary>조립 시작 뒤 _elapsed초의 조각 상태를 깐다. 다 나타났으면 true(모든 조각 불투명).</summary>
    private bool TickAssembly(float _elapsed)
    {
        while (nextPiece < pieces.Count && pieces[nextPiece].startTime <= _elapsed)
        {
            ShowPiece(pieces[nextPiece]);
            nextPiece++;
        }

        for (int p = 0; p < nextPiece; p++)
        {
            float t = Mathf.Clamp01((_elapsed - pieces[p].startTime) / pieceFadeSeconds);
            SetPieceAlpha(pieces[p], t);
        }

        if (nextPiece < pieces.Count || _elapsed < pieces[pieces.Count - 1].startTime + pieceFadeSeconds) return false;

        for (int p = 0; p < pieces.Count; p++) SetPieceAlpha(pieces[p], 1f);
        bAssemblyRunning = false;
        return true;
    }

    /// <summary>
    /// 조각을 투명하게 깐다. 물결이 아직 섬 가장자리를 지나는 중이면(복제층이 화면을 그린다) 해안선 복제층에도 깐다 -
    /// 실제 층은 복제층 아래라 안 보인다. 복제층을 비우면 실제 층의 같은 조각이 같은 진하기로 이어 보인다.
    /// </summary>
    private void ShowPiece(in BackgroundPiece _piece)
    {
        for (int c = 0; c < _piece.cellCount; c++)
        {
            int i = pieceCells[_piece.firstCell + c];
            Vector3Int cell = CellOf(i);
            for (int l = 0; l < shoreLayers.Count; l++)
            {
                if (null == shoreAfter[l][i]) continue;
                ShowPieceTile(shoreLayers[l], shoreLayers[l], cell, shoreAfter[l][i]);
                if (bLiftsActive && l < shoreLifts.Count && null != shoreLifts[l]) ShowPieceTile(shoreLifts[l], shoreLayers[l], cell, shoreAfter[l][i]);
            }
        }
    }

    private void ShowPieceTile(Tilemap _layer, Tilemap _animSource, Vector3Int _cell, TileBase _tile)
    {
        _layer.SetTile(_cell, _tile);
        _layer.SetTileFlags(_cell, TileFlags.None);
        _layer.SetColor(_cell, new Color(1f, 1f, 1f, 0f));
        if (bHasAnimRef) _layer.SetAnimationTime(_cell, ReferenceAnimationTime(_animSource));
    }

    private void SetPieceAlpha(in BackgroundPiece _piece, float _alpha)
    {
        Color color = new Color(1f, 1f, 1f, _alpha);
        for (int c = 0; c < _piece.cellCount; c++)
        {
            int i = pieceCells[_piece.firstCell + c];
            Vector3Int cell = CellOf(i);
            for (int l = 0; l < shoreLayers.Count; l++)
            {
                if (null == shoreAfter[l][i]) continue;
                shoreLayers[l].SetColor(cell, color);
                if (bLiftsActive && l < shoreLifts.Count && null != shoreLifts[l]) shoreLifts[l].SetColor(cell, color);
            }
        }
    }

    private Vector3Int CellOf(int _index) => new Vector3Int(region.xMin + _index % region.size.x, region.yMin + _index / region.size.x, 0);

    private static bool SameTiles(TileBase[][] _a, TileBase[][] _b, int _index)
    {
        if (null == _a || null == _b) return _a == _b;
        for (int i = 0; i < _a.Length && i < _b.Length; i++)
        {
            if (_a[i][_index] != _b[i][_index]) return false;
        }

        return true;
    }

    private static bool HasAnyTile(TileBase[][] _layers, int _index)
    {
        if (null == _layers) return false;
        for (int i = 0; i < _layers.Length; i++)
        {
            if (null != _layers[i] && null != _layers[i][_index]) return true;
        }

        return false;
    }

    /// <summary>앞머리 뒤로 물결이 이어지는 거리(바닥 평면 거리). 이만큼 지나간 칸은 제자리에서 멈춘다.</summary>
    private float RippleLength => Mathf.Max(0.1f, settings.rippleWavelength) * Mathf.Max(0.25f, settings.rippleWaves);

    /// <summary>첫 마루(가장 높이 솟는 지점)까지의 거리. 티어 전환은 여기서 새 지형으로 바뀐다.</summary>
    private float CrestDistance => Mathf.Max(0.1f, settings.rippleWavelength) * 0.25f;

    /// <summary>
    /// 앞머리에서 _distance만큼 뒤에 있는 칸의 높이. 감쇠 물결: sin(2π·d/λ) × (1 - d/L).
    /// 앞머리(0)와 끝(L)에서 높이 0. 끝(L)이 반 파장의 배수면 sin도 0이라 멈출 때 기울기가 0에 가까워 튀지 않는다.
    /// </summary>
    private float RippleHeight(float _distance, float _length)
    {
        if (_distance <= 0f || _distance >= _length) return 0f;

        float wavelength = Mathf.Max(0.1f, settings.rippleWavelength);
        float fade = 1f - _distance / _length;
        return settings.rippleHeight * Mathf.Sin(2f * Mathf.PI * _distance / wavelength) * fade + SwapPop(_distance, _length);
    }

    /// <summary>칸이 새 지형으로 바뀌는 거리(진입은 블록이 나타나는 앞머리, 티어 전환은 첫 마루).</summary>
    private float SwapDistance => bEntry ? 0f : CrestDistance;

    // 솟았다 내려온 뒤 아래로 튕기는 구간 길이(솟는 구간 대비).
    private const float SwapSettleSpan = 0.5f;

    /// <summary>
    /// 바뀌는 칸이 물결 위로 한 번 더 톡 솟는 높이. 바뀐 뒤 swapPopLength 동안 반 사인으로 솟았다 내려오고, 그 절반 동안 살짝 아래로 튕긴 뒤 멈춘다.
    /// 앞머리에서 떨어진 거리만의 매끄러운 함수라 이웃 칸이 차례로 솟아 띠가 끊기지 않는다. 솟고 튕기는 구간이 물결 끝(_length)을 넘으면
    /// 그 안에 들어가게 줄여, 물결이 끝나 칸이 제자리에 놓이는 순간 높이가 툭 떨어지지 않는다.
    /// </summary>
    private float SwapPop(float _distance, float _length)
    {
        float height = settings.swapPopHeight;
        if (height <= 0f) return 0f;

        float popLength = Mathf.Min(Mathf.Max(0.1f, settings.swapPopLength), (_length - SwapDistance) / (1f + SwapSettleSpan));
        if (popLength <= 0f) return 0f;

        float s = (_distance - SwapDistance) / popLength;
        if (s <= 0f) return 0f;
        if (s < 1f) return height * Mathf.Sin(Mathf.PI * s);
        if (s < 1f + SwapSettleSpan) return -height * Mathf.Clamp01(settings.swapPopSettle) * Mathf.Sin(Mathf.PI * (s - 1f) / SwapSettleSpan);
        return 0f;
    }

    /// <summary>물결 앞머리가 칸에 닿았다. 진입은 새 바닥 블록이 이 칸에 제 크기로 나타난다.</summary>
    private void ReachCell(int _order)
    {
        WaveCell cell = cells[_order];
        cellStage[_order] = StageReached;

        if (false == bEntry) return;

        // 진입: 블록은 제 크기로 나타나고 그 칸의 바다는 새 물(육지면 없음, 물가면 물가 물)로 바뀐다.
        // 블록을 작게 돋아나게 하면 이웃 블록·바다와 맞물리지 않아 가장자리가 톱니처럼 지저분해진다. 제 크기면 섬 가장자리가
        // 늘 완성된 모양(앞 칸 바다가 절벽 아랫부분을 덮는 모양)으로 퍼져 나간다. 블록은 이 프레임의 높이 갱신(UpdateCell)이 놓는다.
        SetLiftTiles(cell.cell, cell.index, null, waterAfter);
        ShowAfterOnLift(_order);

        TileRevealedEvent?.Invoke(cell.worldPos);
    }

    private void UpdateCell(int _order, float _distance, float _length)
    {
        byte stage = cellStage[_order];
        if (StageDone == stage) return;

        WaveCell cell = cells[_order];

        // 티어 전환: 첫 마루에서 새 지형으로 바꾼다(실제 층과 복제층 둘 다).
        if (false == bEntry && StageReached == stage && _distance >= SwapDistance)
        {
            SwapCell(cell.cell, cell.index);
            SetLiftTiles(cell.cell, cell.index, shoreAfter, waterAfter);
            ShowAfterOnLift(_order);
            TileRevealedEvent?.Invoke(cell.worldPos);
            stage = StageSwapped;
        }

        if (_distance >= _length)
        {
            // 물결이 다 지나갔다. 진입은 이제서야 실제 바닥·물을 새 지형으로 바꾼다 - 그 전까지 블록 밑(솟은 만큼 드러나는 틈)은 바다다.
            if (bEntry || StageSwapped != stage) SwapCell(cell.cell, cell.index);
            PoseCell(_order, _length, _length);
            cellStage[_order] = StageDone;
            return;
        }

        cellStage[_order] = stage;
        PoseCell(_order, _distance, _length);
    }

    /// <summary>
    /// 실제 층의 칸을 새 지형(바닥·물, 티어 전환이면 해안선까지)으로 한꺼번에 바꾼다. 파도 동안 화면에는 복제층이 그려지므로
    /// 바뀌는 순간이 보이지 않는다. 진입의 해안선은 4단계에서 따로 나타난다.
    /// </summary>
    private void SwapCell(Vector3Int _cell, int _index)
    {
        SetCellLayers(floorLayers, floorAfter, _cell, _index);
        if (false == bEntry) SetCellLayers(shoreLayers, shoreAfter, _cell, _index);
        SetCellLayers(waterLayers, waterAfter, _cell, _index);
    }

    private void SetCellLayers(List<Tilemap> _layers, TileBase[][] _tiles, Vector3Int _cell, int _index)
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            _layers[i].SetTile(_cell, _tiles[i][_index]);
            SyncAnimation(_layers[i], _cell);
        }
    }

    private void SyncAnimation(Tilemap _layer, Vector3Int _cell)
    {
        if (false == bHasAnimRef) return;
        _layer.SetAnimationTime(_cell, ReferenceAnimationTime(_layer));
    }

    /// <summary>복제층에 그 칸의 물(과 _shoreTiles가 있으면 해안선) 타일을 놓는다. 바닥은 높이와 함께 QueueFloor가 놓는다.</summary>
    private void SetLiftTiles(Vector3Int _cell, int _index, TileBase[][] _shoreTiles, TileBase[][] _waterTiles)
    {
        PlaceFlatLiftTiles(waterLifts, waterLayers, _waterTiles, _cell, _index);
        PlaceFlatLiftTiles(shoreLifts, shoreLayers, _shoreTiles, _cell, _index);
    }

    /// <summary>출렁이지 않는 복제층(물·해안선)에 그 칸의 타일을 놓는다. _tiles가 null이면 그대로 둔다.</summary>
    private void PlaceFlatLiftTiles(List<Tilemap> _lifts, List<Tilemap> _sources, TileBase[][] _tiles, Vector3Int _cell, int _index)
    {
        if (null == _tiles) return;
        for (int l = 0; l < _lifts.Count; l++)
        {
            Tilemap lift = _lifts[l];
            if (null == lift) continue;
            lift.SetTile(_cell, _tiles[l][_index]);
        }

        SyncLiftAnimation(_lifts, _sources, _cell);
    }

    /// <summary>이 칸의 바닥 복제층을 새 지형으로 바꾸도록 표시한다. 다음 높이 갱신(PoseCell)에서 높이가 그대로여도 다시 쓴다.</summary>
    private void ShowAfterOnLift(int _order)
    {
        liftShowsAfter[_order] = true;
        lastPixels[_order] = PixelsDirty;
    }

    /// <summary>테두리 진하기(0~1): 물결이 닿으면 빠르게 드러나고, 끝 무렵 서서히 사라진다.</summary>
    private float OutlineEnvelope(float _distance, float _length)
    {
        float t = _distance / _length;
        float fadeIn = Mathf.Clamp01(t / Mathf.Max(0.01f, settings.outlineFadeIn));
        float fadeOutStart = Mathf.Clamp(settings.outlineFadeOutStart, 0f, 0.99f);
        float fadeOut = 1f - Mathf.Clamp01((t - fadeOutStart) / (1f - fadeOutStart));
        return Mathf.SmoothStep(0f, 1f, fadeIn) * Mathf.SmoothStep(0f, 1f, fadeOut);
    }

    /// <summary>
    /// 앞머리에서 _distance만큼 뒤에 있는 칸의 높이(픽셀 단위로 끊는다)와 윗면 테두리. 물결이 다 지나간 칸(_distance ≥ _length)은 제자리·테두리 없음.
    /// </summary>
    private void PoseCell(int _order, float _distance, float _length)
    {
        float height = RippleHeight(_distance, _length);
        short pixels = (short)Mathf.Clamp(Mathf.RoundToInt(height * QuakeTileRipple.PixelsPerUnit), -QuakeTileRipple.MaxOffsetPixels, QuakeTileRipple.MaxOffsetPixels);
        short last = lastPixels[_order];
        bool bMoved = pixels != last;
        if (bMoved)
        {
            lastPixels[_order] = pixels;
            QueueFloor(_order, PixelsDirty == last);
            MarkCrackAround(_order);
        }

        // 테두리: 물결 진행과 지금 마루·골에 있는지(수평을 지날 때는 옅게)로 진하기가 정해진다. 바닥 블록이 없는 칸(물)은 테두리가 없다.
        float outline = 0f;
        float light = 0f;
        if (_distance < _length && HasBlockOnLift(_order))
        {
            light = CrestLight(pixels) + SwapFlash(_distance);
            float wave = Mathf.Sin(2f * Mathf.PI * _distance / Mathf.Max(0.1f, settings.rippleWavelength));
            // 마루에서 가장 또렷하고, 골에서는 옅게(골이 갈라진 틈처럼 보이지 않게).
            // 골 진하기는 수평일 때보다 옅어지지 않게 한다(골로 깊어질수록 테두리가 사라지면 물결의 명암 순서가 뒤집혀 보인다).
            float onLevel = Mathf.Clamp01(settings.outlineOnLevel);
            float peak = wave >= 0f ? 1f : Mathf.Max(onLevel, Mathf.Clamp01(settings.outlineTroughLevel));
            float swing = Mathf.Lerp(onLevel, peak, Mathf.Abs(wave));
            outline = OutlineEnvelope(_distance, _length) * swing;
        }

        QueueOutline(_order, (sbyte)Mathf.RoundToInt(outline * QuakeTileRipple.OutlineSteps), bMoved);
        QueueLight(_order, (sbyte)Mathf.Clamp(Mathf.RoundToInt(light * LightSteps), 0, LightSteps), bMoved);
    }

    /// <summary>복제층의 이 칸에 바닥 블록이 깔려 있다(새 지형으로 바뀐 칸은 새 블록, 아니면 직전 블록).</summary>
    private bool HasBlockOnLift(int _order)
    {
        if (liftShapeLayer < 0) return false;
        TileBase[][] tiles = liftShowsAfter[_order] ? floorAfter : floorBefore;
        return null != tiles[liftShapeLayer][cells[_order].index];
    }

    /// <summary>_pixels 높이로 솟은 칸 윗면의 마루 빛(0~1). 마루 높이(rippleHeight)에서 crestGlow, 그 CrestLightFrom 비율 아래는 0.</summary>
    private float CrestLight(int _pixels)
    {
        float glow = Mathf.Clamp01(settings.crestGlow);
        if (_pixels <= 0 || glow <= 0f) return 0f;

        float peak = Mathf.Max(1f, settings.rippleHeight * QuakeTileRipple.PixelsPerUnit);
        float rise = Mathf.Clamp01((_pixels / peak - CrestLightFrom) / (1f - CrestLightFrom));
        return glow * rise * rise;
    }

    /// <summary>칸이 새 지형으로 바뀐 직후의 반짝임(0~1). 바뀌는 순간 가장 밝고 swapFlashLength 동안 사그라든다.</summary>
    private float SwapFlash(float _distance)
    {
        float flash = Mathf.Clamp01(settings.swapFlash);
        if (flash <= 0f) return 0f;

        float s = (_distance - SwapDistance) / Mathf.Max(0.1f, settings.swapFlashLength);
        if (s < 0f || s >= 1f) return 0f;
        return flash * (1f - s) * (1f - s);
    }

    /// <summary>윗면 빛을 _step 단계로 바꾸도록 모은다(0이면 걷는다). 칸 높이가 바뀌었으면 새 높이의 사본으로 다시 깐다.</summary>
    private void QueueLight(int _order, sbyte _step, bool _bMoved)
    {
        if (null == lightLift) return;

        sbyte last = lastLightSteps[_order];
        if (_step == last && (false == _bMoved || 0 == _step)) return;

        lastLightSteps[_order] = _step;
        Vector3Int cell = cells[_order].cell;
        if (_step > 0)
        {
            Color color = settings.crestLightColor;
            color.a = _step / (float)LightSteps;
            TileBase tile = QuakeTileRipple.OffsetTile(LightFaceTile, new Vector2Int(0, lastPixels[_order]));
            lightChanges.Add(new TileChangeData(cell, tile, color, Matrix4x4.identity));
        }
        else if (last > 0)
        {
            lightChanges.Add(new TileChangeData(cell, null, Color.white, Matrix4x4.identity));
        }
    }

    // // 틈 빛

    /// <summary>칸 높이가 바뀌었다: 이 칸(띠 자리·높이가 바뀐다)과 이 칸을 뒤 칸으로 두는 앞 칸 둘(칸 좌표 x-1, y-1)의 틈 빛을 다시 깐다.</summary>
    private void MarkCrackAround(int _order)
    {
        if (false == bCrackGlow) return;

        MarkCrackDirty(_order);
        int index = cells[_order].index;
        int width = region.size.x;
        if (index % width > 0) MarkCrackDirty(orderOfIndex[index - 1]);
        if (index >= width) MarkCrackDirty(orderOfIndex[index - width]);
    }

    private void MarkCrackDirty(int _order)
    {
        if (_order < 0 || crackDirty[_order]) return;

        crackDirty[_order] = true;
        crackDirtyOrders.Add(_order);
    }

    private void FlushCrack()
    {
        for (int i = 0; i < crackDirtyOrders.Count; i++)
        {
            int order = crackDirtyOrders[i];
            crackDirty[order] = false;
            UpdateCrack(order);
        }

        crackDirtyOrders.Clear();
    }

    /// <summary>
    /// 이 칸(앞 칸)의 왼쪽 위·오른쪽 위 모서리에 생긴 틈 빛(지진 물결 QuakeTileRipple.UpdateGlow와 같은 모양). 뒤 칸이 이 칸보다 솟은 만큼
    /// 뒤 칸 옆벽이 드러나고 그 바닥을 따라 빛을 깐다. 두 칸 모두 블록일 때만(물가 절벽은 비추지 않는다).
    /// </summary>
    private void UpdateCrack(int _order)
    {
        int pixels = CrackHeight(_order);
        int index = cells[_order].index;
        int width = region.size.x;
        int x = index % width;
        int y = index / width;
        bool bBlock = HasCrackBlock(_order);

        // 왼쪽 위 이웃은 칸 좌표 (x, y+1), 오른쪽 위 이웃은 (x+1, y).
        int left = bBlock && y + 1 < region.size.y ? CrackRows(index + width, pixels) : 0;
        int right = bBlock && x + 1 < width ? CrackRows(index + 1, pixels) : 0;
        bool bMoved = pixels != crackPixels[_order];
        crackPixels[_order] = (short)pixels;

        Color color = bLava ? Color.white : settings.crackGlowColor;
        Vector3Int cell = cells[_order].cell;
        if (left != lastCrackLeft[_order] || (left > 0 && bMoved))
        {
            lastCrackLeft[_order] = (sbyte)left;
            crackLeftChanges.Add(new TileChangeData(cell, left > 0 ? QuakeTileRipple.OffsetTile(CrackTile(left, true), new Vector2Int(0, pixels)) : null, color, Matrix4x4.identity));
        }

        if (right != lastCrackRight[_order] || (right > 0 && bMoved))
        {
            lastCrackRight[_order] = (sbyte)right;
            crackRightChanges.Add(new TileChangeData(cell, right > 0 ? QuakeTileRipple.OffsetTile(CrackTile(right, false), new Vector2Int(0, pixels)) : null, color, Matrix4x4.identity));
        }
    }

    /// <summary>
    /// 틈 빛을 비출 블록이 있는 칸. 틈 빛 재질·색은 새 티어 것이라, 직전과 새 티어의 틈 빛이 다르면(용암 ↔ 물) 아직 직전 블록으로 보이는 칸은 빼서
    /// 직전 지형 틈에 새 티어 빛이 비치지 않게 한다. 칸이 바뀌면 높이가 다시 쓰여(ShowAfterOnLift) 이웃 틈도 다시 깔린다.
    /// </summary>
    private bool HasCrackBlock(int _order)
    {
        if (false == HasBlockOnLift(_order)) return false;
        return bEntry || bLavaBefore == bLava || liftShowsAfter[_order];
    }

    private Tile CrackTile(int _rows, bool _bLeft) => bLava ? QuakeTileRipple.GlowTile(_rows, _bLeft) : QuakeTileRipple.WhiteGlowTile(_rows, _bLeft);

    /// <summary>칸 높이(픽셀). 바닥을 다시 쓰라는 표시(PixelsDirty)가 남아 있으면 아직 놓이지 않은 칸이라 0으로 본다.</summary>
    private int CrackHeight(int _order)
    {
        short pixels = lastPixels[_order];
        return PixelsDirty == pixels ? 0 : pixels;
    }

    /// <summary>뒤 칸(_backIndex)이 이 칸보다 솟아 드러난 옆벽 줄 수. 뒤 칸이 파도 칸이 아니거나 블록이 없거나 틈이 얕으면 0.</summary>
    private int CrackRows(int _backIndex, int _pixels)
    {
        int backOrder = orderOfIndex[_backIndex];
        if (backOrder < 0 || false == HasCrackBlock(backOrder)) return 0;

        int rows = Mathf.Min(CrackHeight(backOrder) - _pixels - 1, QuakeTileRipple.MaxGlowRows);
        return rows >= QuakeTileRipple.GlowMinRows ? rows : 0;
    }

    /// <summary>
    /// 바닥 복제층(블록·스텐실)의 그 칸을 지금 높이(lastPixels)의 사본으로 바꾸도록 모은다. 골(내려간 칸)은 깊이만큼 어둡게 한다.
    /// _bTilesChanged면 복제층에 놓을 지형이 바뀐 것이라 빈 칸도 비우도록 쓴다.
    /// </summary>
    private void QueueFloor(int _order, bool _bTilesChanged)
    {
        WaveCell cell = cells[_order];
        int pixels = lastPixels[_order];
        TileBase[][] tiles = liftShowsAfter[_order] ? floorAfter : floorBefore;

        // 타일에 명암이 없어 높이 차만으로는 물결이 잘 안 읽힌다. 골(내려간 칸)을 깊이만큼 어둡게 해 마루와 골의 띠가 보이게 한다.
        float shade = 1f;
        if (pixels < 0 && settings.rippleHeight > 0f)
        {
            shade = 1f - Mathf.Clamp01(settings.rippleShade) * Mathf.Clamp01(-pixels / QuakeTileRipple.PixelsPerUnit / settings.rippleHeight);
        }

        for (int l = 0; l < lifts.Count && l < floorLayers.Count; l++)
        {
            if (null == lifts[l]) continue;

            TileBase source = LiftTilesOf(tiles, l)[cell.index];
            if (null == source && false == _bTilesChanged) continue;

            // SetTile처럼 타일의 색·변형을 그대로 쓰고, 골이면 색만 어둡게 덮는다.
            TileBase tile = QuakeTileRipple.OffsetTile(source, new Vector2Int(0, pixels));
            Tile sprite = tile as Tile;
            Color color = shade < 1f ? new Color(shade, shade, shade, 1f) : (null != sprite ? sprite.color : Color.white);
            Matrix4x4 transform = null != sprite ? sprite.transform : Matrix4x4.identity;
            floorChanges[l].Add(new TileChangeData(cell.cell, tile, color, transform));
        }
    }

    /// <summary>
    /// 테두리 진하기를 _step 단계로 바꾸도록 모은다(0이면 테두리 타일을 걷는다). 칸 높이가 바뀌었으면(_bMoved) 진하기가 그대로여도
    /// 테두리를 새 높이의 사본으로 다시 깐다. 진하기는 칸 색(알파)에 담는다.
    /// </summary>
    private void QueueOutline(int _order, sbyte _step, bool _bMoved)
    {
        if (null == outlineLift) return;

        sbyte last = lastOutlineSteps[_order];
        if (_step == last && (false == _bMoved || 0 == _step)) return;

        lastOutlineSteps[_order] = _step;
        Vector3Int cell = cells[_order].cell;
        if (_step > 0)
        {
            Color color = settings.outlineColor;
            color.a *= _step / (float)QuakeTileRipple.OutlineSteps;
            TileBase tile = QuakeTileRipple.OffsetTile(QuakeTileRipple.OutlineTile, new Vector2Int(0, lastPixels[_order]));
            outlineChanges.Add(new TileChangeData(cell, tile, color, Matrix4x4.identity));
        }
        else if (last > 0)
        {
            outlineChanges.Add(new TileChangeData(cell, null, Color.white, Matrix4x4.identity));
        }
    }

    // // 굴절 고리

    /// <summary>
    /// 굴절 고리 쿼드를 켜고 끈다(재질이 없거나 세기가 0이면 늘 꺼 둔다). 쿼드는 파도 영역(기준 화면 + 여유)을 덮고, 셰이더가 고리 자리만 그린다.
    /// 켜져 있는 동안만 화면 캡처(SortingLayerTextureUser → SortingLayerTextureGate)를 쓴다.
    /// </summary>
    private void ShowRefraction(bool _bShow)
    {
        bool bUse = _bShow && null != settings.refractionMaterial && settings.refractionPixels > 0f && bHasRegion && floorLayers.Count > 0;
        if (false == bUse)
        {
            if (null != refractionObject) refractionObject.SetActive(false);
            return;
        }

        if (null == refractionObject)
        {
            refractionObject = new GameObject("RoguelikeTileWaveRefraction");
            refractionObject.transform.SetParent(floorLayers[0].transform.parent, false);
            if (null == refractionMesh) refractionMesh = CreateQuadMesh();
            refractionObject.AddComponent<MeshFilter>().sharedMesh = refractionMesh;
            refractionRenderer = refractionObject.AddComponent<MeshRenderer>();
            refractionRenderer.sharedMaterial = settings.refractionMaterial;
            refractionRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            refractionRenderer.receiveShadows = false;
            // 캡처(Objects 레이어까지) 바로 다음 레이어의 맨 앞 - 캡처와 지금 화면이 같은 자리에서 그린다.
            refractionRenderer.sortingLayerName = "FlyingItem";
            refractionRenderer.sortingOrder = short.MinValue;
            refractionObject.AddComponent<PresentationLayer.VFX.SortingLayerTextureUser>();
            refractionBlock = new MaterialPropertyBlock();
            refractionObject.SetActive(false);
        }

        // 재질은 켤 때마다 지금 설정 것으로 둔다(재생 중 바꾼 재질이 다음 파도부터 반영된다).
        refractionRenderer.sharedMaterial = settings.refractionMaterial;

        Vector2 cellSize = environmentSystem.TileCellSize;
        Transform t = refractionObject.transform;
        t.position = new Vector3(waveCenter.x, waveCenter.y, 0f);
        Vector2 size = new Vector2(2f * (viewHalfSize.x + 2f * cellSize.x), 2f * (viewHalfSize.y + 2f * cellSize.y));
        // 쿼드는 바닥 층 부모(Grid) 아래에 있다. 부모 배율을 나눠 월드 크기가 size가 되게 한다.
        Vector3 parentScale = null != t.parent ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(size.x / NonZero(parentScale.x), size.y / NonZero(parentScale.y), 1f);

        // 칸 물결은 앞머리 일렁임(GetWaveDistance의 노이즈)만큼 들쭉날쭉 퍼지므로, 고리도 같은 굴곡을 따라야 첫 마루 위에 걸린다.
        // 일렁임 무늬는 파도마다 바뀐다(wobbleOffset) - 재생할 때마다 쿼드 자리별 몫을 다시 떠서 셰이더에 넘긴다.
        float range = BakeRefractionWobble(size);
        refractionRenderer.GetPropertyBlock(refractionBlock);
        refractionBlock.SetTexture(WobbleTexId, refractionWobble);
        refractionBlock.SetFloat(WobbleRangeId, range);
        // 셰이더는 일렁임 텍스처를 월드 좌표로 찾는다(쿼드 변환·UV와 상관없이 텍스처를 뜬 자리와 같다).
        refractionBlock.SetVector(WobbleRectId, new Vector4(waveCenter.x, waveCenter.y, size.x, size.y));
        refractionRenderer.SetPropertyBlock(refractionBlock);
    }

    private static float NonZero(float _value) => Mathf.Abs(_value) > 0.0001f ? _value : 1f;

    /// <summary>
    /// 월드 사각형(중심 waveCenter, 크기 _size)을 덮는 일렁임 텍스처를 채운다. 칸마다 GetWaveDistance - 일렁임 없는 거리를 담는다(±range를 0~1로 펴서).
    /// 텍셀 중심이 사각형 안 비율 (i + 0.5) / 너비 자리라 셰이더가 같은 비율로 이중선형 샘플링하면 그 사이가 이어진다. 돌려주는 값은 range(일렁임이 꺼져 있으면 0).
    /// </summary>
    private float BakeRefractionWobble(Vector2 _size)
    {
        int width = Mathf.Clamp(Mathf.CeilToInt(_size.x / WobbleTexelWorld), 2, 256);
        int height = Mathf.Clamp(Mathf.CeilToInt(_size.y / WobbleTexelWorld), 2, 256);
        if (null == refractionWobble || refractionWobble.width != width || refractionWobble.height != height)
        {
            if (null != refractionWobble) UnityEngine.Object.Destroy(refractionWobble);
            refractionWobble = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                name = "RoguelikeTileWaveRefractionWobble",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        float range = Mathf.Max(0f, settings.frontWobble);
        if (refractionWobblePixels.Length != width * height) refractionWobblePixels = new Color32[width * height];
        Color32[] pixels = refractionWobblePixels;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                byte value = 128;
                if (range > 0f)
                {
                    Vector3 pos = new Vector3(waveCenter.x + ((x + 0.5f) / width - 0.5f) * _size.x, waveCenter.y + ((y + 0.5f) / height - 0.5f) * _size.y, 0f);
                    float plain = new Vector2(pos.x - waveCenter.x, (pos.y - waveCenter.y) * 2f).magnitude;
                    float wobble = Mathf.Clamp(GetWaveDistance(pos) - plain, -range, range);
                    value = (byte)Mathf.RoundToInt((0.5f + 0.5f * wobble / range) * 255f);
                }
                pixels[x + y * width] = new Color32(value, 0, 0, 255);
            }
        }

        refractionWobble.SetPixels32(pixels);
        refractionWobble.Apply(false, false);
        return range;
    }

    /// <summary>
    /// 고리를 첫 마루 자리(앞머리 뒤 반의 반 파장)에 둔다. 섬 중심 근처에서 서서히 드러나고, 섬 가장자리를 지나 한 파장 안에 사라진다
    /// (_end = 물결이 끝날 때의 앞머리 - 그 전에 다 사라져 끝나는 순간 툭 꺼지지 않는다). 미는 픽셀이 반 픽셀 아래면 쿼드를 끈다.
    /// </summary>
    private void UpdateRefraction(float _front, float _end)
    {
        if (null == refractionObject || null == refractionRenderer) return;

        float radius = _front - CrestDistance;
        float fadeIn = Mathf.SmoothStep(0f, 1f, radius / 3f);
        float outFrom = Mathf.Min(landEdgeKey, _end - CrestDistance);
        float outTo = Mathf.Min(landEdgeKey + Mathf.Max(0.1f, settings.rippleWavelength), _end - CrestDistance);
        float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, (radius - outFrom) / Mathf.Max(0.01f, outTo - outFrom));
        float pixels = settings.refractionPixels * fadeIn * fadeOut;

        bool bVisible = pixels >= 0.5f && false == float.IsNaN(pixels);
        if (refractionObject.activeSelf != bVisible) refractionObject.SetActive(bVisible);
        if (false == bVisible) return;

        refractionRenderer.GetPropertyBlock(refractionBlock);
        refractionBlock.SetVector(RingCenterId, new Vector4(waveCenter.x, waveCenter.y, 0f, 0f));
        refractionBlock.SetFloat(RingRadiusId, radius);
        refractionBlock.SetFloat(RingWidthId, Mathf.Max(0.1f, settings.refractionWidth));
        refractionBlock.SetFloat(RingPixelsId, pixels);
        refractionRenderer.SetPropertyBlock(refractionBlock);
    }

    private static Mesh CreateQuadMesh()
    {
        Mesh mesh = new Mesh { name = "RoguelikeTileWaveRefractionQuad" };
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>이번 프레임에 모아 둔 칸 변경을 복제층마다 한 번에 쓴다(칸마다 SetTile·SetColor를 따로 부르면 호출 비용이 크다).</summary>
    private void FlushChanges()
    {
        for (int l = 0; l < floorChanges.Count && l < lifts.Count; l++)
        {
            if (null != lifts[l]) QuakeTileRipple.FlushChanges(lifts[l], floorChanges[l]);
            else floorChanges[l].Clear();
        }

        FlushOverlay(outlineLift, outlineChanges);
        FlushOverlay(lightLift, lightChanges);
        FlushOverlay(seaLift, seaChanges);
        FlushOverlay(crackLeftLift, crackLeftChanges);
        FlushOverlay(crackRightLift, crackRightChanges);
    }

    private static void FlushOverlay(Tilemap _lift, List<TileChangeData> _changes)
    {
        if (null != _lift) QuakeTileRipple.FlushChanges(_lift, _changes);
        else _changes.Clear();
    }

    private void ClearChanges()
    {
        for (int l = 0; l < floorChanges.Count; l++) floorChanges[l].Clear();
        outlineChanges.Clear();
        lightChanges.Clear();
        seaChanges.Clear();
        crackLeftChanges.Clear();
        crackRightChanges.Clear();
    }
}

/// <summary>
/// 파도가 새 지형으로 바꾼 선(섬 중심에서의 파도 거리)을 따라간다. 칸 단위가 아닌 것(물 장식·구름)이 파도에 맞춰 바뀔 때 쓴다.
/// 한번 출발하면 파도가 마지막으로 알린 속도(앞머리는 바깥으로 갈수록 느려진다)로 계속 퍼진다 - 파도가 먼 바다까지 가지 않고 끝나도
/// 선은 멈추지 않는다. 파도가 앞서 있으면 파도를 따른다.
/// </summary>
public sealed class RoguelikeWaveFront
{
    private bool bStarted;
    private float distance;
    private float speed;

    /// <summary>지금 선의 파도 거리. 이 안쪽은 새 지형으로 바뀌었다.</summary>
    public float Distance => distance;

    /// <summary>새 파도를 기다린다(RoguelikeTileWave.PrepareAfterGenerate 뒤).</summary>
    public void Reset()
    {
        bStarted = false;
        distance = 0f;
    }

    /// <summary>매 프레임(게임 시간). 파도가 아직 시작하지 않았으면 false. 돌 파도가 없으면(영역을 못 잡았다) 선이 곧바로 끝까지 간다.</summary>
    public bool Advance(RoguelikeTileWave _wave, float _deltaTime)
    {
        if (bStarted) distance += speed * _deltaTime;

        if (_wave.TryGetSwapFront(out float waveFront, out float waveSpeed))
        {
            distance = bStarted ? Mathf.Max(distance, waveFront) : waveFront;
            speed = Mathf.Max(0.1f, waveSpeed);
            bStarted = true;
            return true;
        }

        if (bStarted) return true;
        if (_wave.IsPrepared) return false;

        distance = float.MaxValue;
        return true;
    }
}
