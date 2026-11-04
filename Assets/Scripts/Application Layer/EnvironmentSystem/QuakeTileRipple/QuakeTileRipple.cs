using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>지진 바닥 물결 연출 수치.</summary>
[Serializable]
public sealed class QuakeTileRippleSettings
{
    [Tooltip("물결 앞머리 속도(바닥 평면 거리/초). 아이소 화면에서 가로로는 이 속도, 세로로는 절반 속도로 퍼진다.")]
    public float frontSpeed = 10f;
    [Tooltip("진원 가까이에서 마루가 솟는 최대 높이(월드 단위, 1 = 32픽셀). 멀어질수록 줄어든다.")]
    public float rippleHeight = 0.375f;
    [Tooltip("물결 한 파장의 길이(바닥 평면 거리, 대략 칸 수). 짧을수록 칸마다 높이 차가 커져 출렁임이 거칠다.")]
    public float rippleWavelength = 4f;
    [Tooltip("앞머리 뒤로 물결이 몇 파장 동안 이어지다 잦아드는지. 마루 → 골 → 마루 순으로 줄어든다.")]
    public float rippleWaves = 2f;
    [Tooltip("진원(캐릭터 발밑)에서 이 거리까지는 높이가 0에서 차오른다. 캐릭터가 선 칸이 튀어 발이 바닥에 묻히거나 뜨지 않게 한다.")]
    public float calmRadius = 1.2f;
    [Tooltip("물결이 닿는 거리(바닥 평면 거리). 여기서 높이가 0이 된다. 기준 화면(가로 20)의 가장자리쯤이면 화면 끝에서 자연스럽게 잦아든다.")]
    public float reachRadius = 15f;
    [Tooltip("진원에서 멀어질 때 높이가 줄어드는 곡선. 1이면 거리에 비례해 줄고, 클수록 진원 가까이에서 빨리 약해진다.")]
    public float reachFalloffPower = 1.2f;
    [Tooltip("물결 골(내려간 칸)이 가장 깊을 때 어두워지는 정도(0~1). 높이 차만으로는 골이 잘 안 읽힌다.")]
    public float troughShade = 0.3f;

    [Header("아웃라인")]
    [Tooltip("출렁이는 칸 윗면 테두리 색. 바닥과 같은 재질(조명 영향)로 그린다. 알파는 최대 진하기다.")]
    public Color outlineColor = new Color(1f, 0.64f, 0.38f, 0.9f);
    [Tooltip("칸에 물결이 닿은 뒤 테두리가 다 드러나는 구간(물결 길이 대비 비율).")]
    [Range(0.01f, 1f)] public float outlineFadeIn = 0.08f;
    [Tooltip("물결 끝에서 테두리가 사라지기 시작하는 지점(물결 길이 대비 비율). 거기서 물결 끝까지 서서히 사라진다.")]
    [Range(0f, 0.99f)] public float outlineFadeOutStart = 0.4f;
    [Tooltip("진원에서 먼 칸(물결이 낮은 칸)의 테두리 진하기 곡선. 1이면 높이에 비례, 작을수록 먼 칸까지 테두리가 또렷하다.")]
    [Range(0.1f, 1f)] public float outlineFalloffPower = 0.5f;
    [Tooltip("칸이 수평(높이 0)을 지날 때의 테두리 진하기 비율. 마루·골에서 1이다. 낮을수록 마루·골의 띠만 또렷하다.")]
    [Range(0f, 1f)] public float outlineOnLevel = 0.15f;

    [Header("용암 틈 빛")]
    [Tooltip("칸이 앞 칸보다 솟아 틈(옆벽)이 드러나면, 틈 바닥을 따라 용암 빛을 비춘다. 물(용암) 층과 같은 재질·HDR 세기로 그려 블룸·낮밤 밝기가 용암과 같다.")]
    public bool crackGlow = true;

    [Header("물체")]
    [Tooltip("물결 위의 물체(나무·원목·아이템·차량·캐릭터 등)도 선 칸 높이만큼 함께 출렁인다. 화면을 그리는 동안에만 옮겨 판정·정렬·충돌은 그대로다.")]
    public bool rideObjects = true;

    [Header("범위")]
    [Tooltip("화면 바깥으로 이만큼(월드 거리, 가로·세로 같게)까지 복제층에 옮겨 그린다. 연출(약 2.3초) 중 캐릭터를 따라 카메라가 움직여도" +
             " 복제층 경계가 화면에 들어오지 않게 넉넉히 둔다(이동 속도 3 기준 약 7).")]
    public float viewMargin = 8f;

    [Header("손맛")]
    [Tooltip("시작할 때 카메라 흔들림 세기(옵션 100% 기준). 시간이 지나며 0으로 잦아든다. 0이면 흔들지 않는다.")]
    public float cameraShakeIntensity = 3f;
    [Tooltip("카메라 흔들림 시간(초). 물결이 화면 끝에 닿을 무렵까지 잦아들며 이어진다.")]
    public float cameraShakeSeconds = 1.1f;
    [Tooltip("시작할 때 카메라를 아래로 한 번 찍는 거리(월드, 옵션 100% 기준). 찍은 뒤 되튀며 잦아들고 흔들림이 이어진다. 0이면 찍지 않는다.")]
    public float cameraKickDistance = 0.3f;
    [Tooltip("카메라 찍기가 잦아드는 시간(초).")]
    public float cameraKickSeconds = 0.3f;

    [Header("시작 한 방")]
    [Tooltip("진원 둘레 첫 고리 높이를 이 비율만큼 더 키운다(0.6 = 최대 1.6배). 진원에서 멀어질수록 원래 높이로 돌아온다. 이웃 칸 높이 차 한도(옆벽 15픽셀, 물결 높이·파장·칸 크기로 정해진다)를 넘는 만큼은 잘리므로, 물결이 높으면 실제로 커지는 비율이 이보다 작다.")]
    public float epicenterBoost = 0.6f;
    [Tooltip("첫 고리를 키우는 거리(바닥 평면 거리). 진원에서 이 거리까지 키운 몫이 0으로 줄어든다.")]
    public float epicenterBoostRadius = 5f;

    [Header("피해 범위")]
    [Tooltip("피해 범위(지진이 넘겨줄 때만) 바깥 물결 높이 비율. 범위 안은 그대로, 바깥은 이만큼 낮게 출렁여 어디까지 맞는지 보인다.")]
    [Range(0f, 1f)] public float strikeOutsideScale = 0.45f;
    [Tooltip("피해 범위 경계에서 안쪽 높이가 바깥 높이로 바뀌는 폭(바닥 평면 거리). 피해 범위가 이 폭의 한가운데다.")]
    public float strikeEdge = 1.5f;

    [Header("나무 떨림")]
    [Tooltip("물결 앞머리가 닿은 나무가 잠깐 떨고(나무 셰이더), 지진에 맞은 나무는 위로 한 번 튄다(그리는 동안만 - 그림자는 바닥에 남는다).")]
    public bool treeShake = true;
    [Tooltip("피해 범위 안 나무의 떨림 각도(도).")]
    public float treeShiverDegrees = 3.5f;
    [Tooltip("피해 범위 밖 나무의 떨림 각도(도). 물결이 닿는 거리로 갈수록 0으로 줄어든다.")]
    public float treeShiverOutsideDegrees = 1.2f;
    [Tooltip("나무가 떠는 시간(초).")]
    public float treeShiverSeconds = 0.45f;
    [Tooltip("나무 떨림 진동수(Hz).")]
    public float treeShiverHz = 13f;
    [Tooltip("지진에 맞은 나무가 튀는 높이(월드, 1 = 32픽셀, 최대 15픽셀).")]
    public float treePopHeight = 0.16f;
    [Tooltip("나무가 튀었다 내려오는 시간(초).")]
    public float treePopSeconds = 0.2f;
}

/// <summary>
/// 지진 바닥 물결(본편 던전). 진원(캐릭터 자리)에서 바닥 칸들이 동심원 물결로 솟았다 가라앉으며 퍼져 나가고,
/// 출렁이는 동안 칸마다 윗면 테두리(아웃라인)가 드러나 한 칸 한 칸이 따로 움직이는 게 보인다.
///
/// 칸 높이는 앞머리에서 떨어진 거리의 매끄러운 함수다(마루 → 골 → 마루로 잦아드는 감쇠 물결). 이웃 칸 높이가 연속이라
/// 띠 전체가 한 물결로 출렁인다. 진폭은 진원 가까이(calmRadius)에서 0부터 차오르고 reachRadius로 갈수록 0으로 줄어
/// 화면 끝에서 자연스럽게 잦아든다. 높이는 픽셀(1/32) 단위로 끊어 도트가 번지지 않게 한다.
///
/// [지형은 그대로 - 보이기만 한다]
///   실제 타일맵(바닥·물·해안선)은 건드리지 않는다. 연출 동안 화면 둘레의 칸을 모든 타일 층 위의 복제층(리프트)에 옮겨 그리고
///   복제층의 칸만 움직인다. 끝나면 복제층을 숨기고 다음 프레임들에 나눠 비운다. 충돌·길찾기·나무 자리 등 게임 판정은 바뀌지 않는다.
///   - 움직이는 칸만 복제하면 앞 칸이 뒤 칸의 검은 옆면을 가리지 못한다(복제층이 실제 층보다 위에 그려진다). 그래서 화면 둘레
///     영역을 통째로 옮긴다([복제 범위]).
///   - 물(용암)도 같은 앞뒤 순서로 복제층에 그린다. 실제 층처럼 앞 칸의 물이 물가 절벽 아랫부분을 덮어야 한다. 물·해안선은 출렁이지 않는다.
///   - 스텐실 층(구름 그림자 가림)은 옮기지 않는다. 실제 층이 제자리에 그대로 있어 그대로 맞다.
///   - 복제층은 원래 층의 재질(속성 블록의 용암 HDR 세기는 재질 사본에 옮겨 담는다)·애니메이션 시간을 그대로 따른다.
///     끝나고 실제 층으로 돌아가도 밝기나 출렁임 위상이 튀지 않는다.
///   - 연출(약 2초) 동안 실제 층에 생긴 변화(나무가 쓰러져 되살아난 장식 타일 등)는 복제층이 숨겨질 때 드러난다.
///
/// [복제 범위 - 화면(+여유)을 덮는 칸 사각형 전부]
///   복제층은 모든 실제 층보다 위에 그려지므로, 복제한 칸과 실제 층에 남은 칸의 경계에서는 그리는 순서가 실제와 달라진다.
///   - 복제한 칸 바로 앞(아래) 칸이 남으면 복제한 칸의 옆면이 그 앞 칸 윗면을 덮는다(검은 절벽 띠).
///   - 복제한 칸 바로 뒤(위) 칸이 남으면, 뒤 칸의 위 층 타일(용암 모서리 등)이 앞 칸 위로 겹치던 부분을 복제한 앞 칸 바닥이 덮는다(실측).
///   그래서 출렁이는 칸 둘레만 고르지 않고 영역을 통째로 옮겨 경계를 화면(+여유) 밖에만 둔다.
///   대신 비어 있지 않은 칸만 층마다 한 번에 쓴다(타일 쓰기는 칸 수에 비례해 비싸고 빈 칸도 든다).
///   출렁이는 동안 바뀐 칸도 프레임마다 모아 층마다 한 번에 쓴다(칸마다 SetTile·SetColor를 따로 부르면 호출 비용이 크다).
///
/// [시작 준비 - 옮겨 쓰기는 여러 프레임에]
///   화면(+여유) 영역을 한 프레임에 다 옮겨 쓰면 시작 순간 한 프레임이 눈에 띄게 걸린다. 그래서 Begin은 칸 목록만 만들고,
///   복제층은 숨긴 채로 Tick마다 CopyCostPerTick만큼씩 나눠 쓴다(IsPreparing). 다 쓴 Tick에 복제층을 보이고 다음 Tick부터 물결이 퍼진다.
///   숨긴 동안은 실제 층만 보이니 화면은 그대로이고, 물결은 그만큼(몇 프레임) 늦게 시작한다.
///
/// [높이는 피벗을 옮긴 그림으로 - 앞뒤 순서가 높이에 흔들리지 않게]
///   칸마다 따로 정렬되는 타일맵(Individual)은 칸 변형(SetTransformMatrix)으로 옮긴 만큼 정렬 위치도 옮겨진다.
///   솟은 칸이 한 줄 간격(8픽셀)보다 높아지면 뒤 칸보다 먼저 그려져 뒤 칸에 덮인다(실측). 그래서 칸을 움직이지 않고
///   같은 그림의 피벗만 옮긴 사본 타일(높이 픽셀마다 하나, 처음 쓸 때 만들어 계속 재사용)로 바꿔 끼운다. 정렬은 칸 자리 그대로다.
///   바닥 위 장식(돌·풀) 층도 같은 방식으로 바닥과 같은 정렬 순서에 칸마다 섞는다(제 칸 바닥·테두리 다음, 앞 칸 바닥보다 먼저).
///   실제 층처럼 모든 바닥 위에 그리면 내려간 칸의 장식이 솟은 앞 칸 위로 비친다. 장식 층이 바닥보다 비켜 놓인 만큼(DecoTilemapYOffset)도 피벗에 담는다.
///
/// [아웃라인]
///   칸 윗면(마름모) 테두리 타일을 따로 만든 복제층에 깐다. 바닥 복제층과 정렬 순서가 같고 아주 조금 앞에 놓여,
///   칸마다 "제 바닥 바로 다음, 앞 칸 바닥보다 먼저" 그려진다 - 솟은 앞 칸이 뒤 칸의 테두리를 가린다.
///   테두리는 바닥과 같은 재질이라 조명(낮·밤)을 똑같이 받는다. 칸에 물결이 닿으면 드러나고 물결이 지나가면 사라진다.
///
/// [용암 틈 빛]
///   뒤 칸이 앞 칸보다 솟으면 둘 사이에 뒤 칸 옆벽이 드러난다. 그 틈 바닥(앞 칸의 뒤쪽 모서리 바로 위)을 따라 용암 빛을 깔아
///   솟은 칸 아래로 용암이 비치는 것처럼 보이게 한다. 드러난 벽 높이만큼만 그리고, 바닥에서 위로 갈수록 붉어지며 옅어진다.
///   물(용암) 복제층과 같은 재질 사본을 써서 HDR 세기·조명이 용암과 같다 - 깊은 틈만 블룸 임계값을 넘어 은은하게 번진다.
///   앞 칸 바닥·테두리 다음, 앞 칸 장식보다 먼저 그려 돌 같은 장식이나 더 앞의 칸이 틈을 자연스럽게 가린다.
///
/// [물체]
///   나무·원목·아이템·차량·캐릭터는 타일보다 위 정렬 층이라 복제층과 같이 움직이지 않는다. QuakeObjectRide가 화면을 그리는 동안에만
///   선 칸 높이(PixelsAt)만큼 옮겼다 되돌린다 - 게임 코드는 원래 자리만 본다.
/// </summary>
public sealed class QuakeTileRipple
{
    // 도트 한 픽셀(월드). 픽셀 퍼펙트 카메라 Assets PPU와 같다(PixelOutline.hlsl PIXEL_OUTLINE_PPU).
    internal const float PixelsPerUnit = 32f;
    // 아웃라인(한 번)·틈 빛(한 번 반)·장식(두 번) 복제층을 바닥 복제층보다 이만큼씩 앞(아래)에 둔다. 같은 칸끼리만 앞뒤가 갈리고
    // (바로 앞 줄은 0.25 아래) 화면에는 안 보이는 거리다.
    internal const float SortNudge = 0.001f;
    // 틈 빛 띠의 최대 높이(픽셀). 칸 옆벽 높이(16)와 같다.
    private const int MaxGlowRows = 16;
    // 이웃 칸 높이 차의 한도(픽셀). 옆벽(16)보다 크면 뒤 칸 옆벽 아래로 틈이 뚫린다. 칸마다 픽셀로 반올림해 1픽셀 더 벌어질 수 있어 한 줄 뺀다.
    private const int MaxNeighborRise = MaxGlowRows - 1;
    // 이보다 얕은 틈(드러난 벽 줄 수)은 비추지 않는다. 물결 꼬리의 얕은 높이 차까지 빛나면 화면이 용암 격자처럼 번잡하다.
    private const int GlowMinRows = 2;
    // 틈 빛 진하기: GlowMinRows줄 틈 → GlowFullRows줄 틈에서 최소 → 최대. 틈 바닥에서 GlowFadeRows줄 위로 갈수록 옅어진다.
    // 용암 층 HDR 세기(4스테이지 2.25)가 곱해져, 최대 진하기 쪽만 블룸 임계값을 넘는다.
    private const float GlowMinStrength = 0.3f;
    private const float GlowMaxStrength = 0.85f;
    private const int GlowFullRows = 6;
    private const int GlowFadeRows = 5;
    // 이만큼 깊은 틈부터 노란 심(용암 가장자리 색)이 보인다.
    private const int GlowCoreRows = 6;
    // 아웃라인 진하기 단계 수. 단계가 바뀐 칸만 색을 다시 쓴다.
    internal const int OutlineSteps = 16;
    // 피벗을 옮긴 사본 타일을 만들 수 있는 범위(픽셀, 위아래 각각).
    internal const int MaxOffsetPixels = 64;
    // 끝난 뒤 복제층을 비울 때 한 프레임에 비우는 칸 수(띠 하나). 이보다 적게 쓴 층은 ClearAllTiles로 한 번에 비운다.
    private const int ClearStripCells = 1536;
    private const int ClearAllBelow = 512;
    // 시작할 때 복제층에 옮겨 쓰는 몫([시작 준비]). Tick 한 번에 칸 비용 합이 이만큼이 될 때까지 쓴다(모든 층 합).
    // 보통 칸은 1, 애니메이션 칸(용암)은 AnimatedCopyCost다 - 쓰기와 애니메이션 시간 맞추기로 보통 칸의 약 세 배가 든다(실측).
    // 4스테이지 기본 화면(영역 약 8800칸, 쓸 칸 약 9400칸)이 서너 번에 나뉜다.
    private const int CopyCostPerTick = 3200;
    private const int AnimatedCopyCost = 3;

    private struct RippleCell
    {
        public int index;
        public Vector3Int cell;
        public float key;              // 진원에서의 바닥 평면 거리
        public float amplitude;        // 이 칸의 최대 높이(진원 거리 감쇠 적용)
        public float outlineStrength;  // 테두리 진하기 중 진원 거리로 정해지는 몫(매 프레임 Pow를 다시 하지 않는다)
    }

    // 시작 준비 때 옮겨 쓸 층 하나. floorLayer가 0 이상이면 바닥 층(영역 타일은 floorBlocks에 이미 떠 두었다),
    // -1이면 물·해안선 층(그 층 차례가 오면 blockBuffer에 뜬다).
    private struct CopyJob
    {
        public Tilemap source;
        public Tilemap lift;
        public int floorLayer;
        public Vector2Int offsetPixels;
    }

    // 끝난 복제층 비우기 한 번. bAll이면 ClearAllTiles, 아니면 row부터 띠 하나(clearStrip)를 비운다.
    private struct ClearJob
    {
        public Tilemap lift;
        public int row;
        public bool bAll;
    }

    private sealed class RippleCellComparer : IComparer<RippleCell>
    {
        public int Compare(RippleCell _a, RippleCell _b) => _a.key.CompareTo(_b.key);
    }

    private static readonly RippleCellComparer cellComparer = new RippleCellComparer();
    // 나무 셰이더 지진 떨림 전역값(TreeWindSway.hlsl ApplyQuakeShake).
    private static readonly int QuakeShakeAId = Shader.PropertyToID("_QuakeShakeA");
    private static readonly int QuakeShakeBId = Shader.PropertyToID("_QuakeShakeB");
    private static readonly int QuakeShakeCId = Shader.PropertyToID("_QuakeShakeC");
    private static readonly int QuakeShakeDId = Shader.PropertyToID("_QuakeShakeD");
    private static Tile outlineTile;
    // 틈 빛 타일(띠 높이 1~MaxGlowRows별, 0번은 비움). 왼쪽 위·오른쪽 위 모서리용.
    private static Tile[] glowLeftTiles;
    private static Tile[] glowRightTiles;
    // 원래 타일 → 피벗을 옮긴 사본(가로·세로 픽셀 오프셋별). 타일 에셋은 씬이 바뀌어도 그대로라 한 번 만든 사본을 계속 쓴다.
    private static readonly Dictionary<TileBase, Dictionary<int, TileBase>> offsetTiles = new Dictionary<TileBase, Dictionary<int, TileBase>>(16);
    // SetTiles에 넘기는 배열(길이 2의 거듭제곱별로 재사용). SetTiles는 배열 전체를 쓰므로 남는 자리는 마지막 항목을 되풀이해 채운다
    // (같은 칸에 같은 값을 다시 쓰는 것이라 결과가 같다). 길이가 딱 맞는 배열을 매번 만들지 않는다.
    private static readonly Vector3Int[][] positionBatches = new Vector3Int[24][];
    private static readonly TileBase[][] tileBatches = new TileBase[24][];
    private static readonly TileChangeData[][] changeBatches = new TileChangeData[24][];

    private QuakeTileRippleSettings settings;

    // 옮겨 그릴 실제 층(스텐실 제외). 바닥(움직임) - 0번이 바닥 블록, 물·해안선(제자리).
    private readonly List<Tilemap> floorLayers = new List<Tilemap>(4);
    private readonly List<Tilemap> waterLayers = new List<Tilemap>(4);
    private readonly List<Tilemap> shoreLayers = new List<Tilemap>(2);

    // 복제층(각 층과 같은 순서)과 아웃라인 복제층. 그리드가 새로 만들어지면 다시 만든다.
    private readonly List<Tilemap> floorLifts = new List<Tilemap>(4);
    private readonly List<Tilemap> waterLifts = new List<Tilemap>(4);
    private readonly List<Tilemap> shoreLifts = new List<Tilemap>(2);
    // 바닥 복제층마다 피벗에 더할 자리(픽셀). 장식 층이 바닥 층보다 비켜 놓인 만큼이다. 0번(바닥 블록)은 0.
    private readonly List<Vector2Int> floorLiftOffsets = new List<Vector2Int>(4);
    private Tilemap outlineLift;
    // 틈 빛 복제층. 칸의 왼쪽 위·오른쪽 위 모서리 위(뒤 칸의 드러난 옆벽)에 그린다.
    private Tilemap glowLeftLift;
    private Tilemap glowRightLift;
    private bool bGlow;
    private Transform liftParent;
    private MaterialPropertyBlock propertyBlock;
    // 타일 종류별 애니메이션 여부(복제층 칸의 애니메이션 시간을 맞출지).
    private readonly Dictionary<TileBase, bool> animatedTiles = new Dictionary<TileBase, bool>(32);
    // 실제 층에 속성 블록이 있는 복제층의 재질 사본(ApplySourceMaterial).
    private readonly Dictionary<TilemapRenderer, Material> liftMaterials = new Dictionary<TilemapRenderer, Material>(4);

    private BoundsInt region;
    // 영역 칸 (0,0)의 칸 중심(월드)과 칸 좌표 x·y가 하나 늘 때 움직이는 거리. 칸 중심을 칸마다 그리드에 묻지 않고 계산한다.
    private Vector3 cellOrigin;
    private Vector3 cellStepX;
    private Vector3 cellStepY;
    // 영역 칸 수. 영역 크기 배열(blockBuffer·floorBlocks·orderOfIndex)은 늘리기만 하고 앞의 이만큼만 쓴다.
    private int regionCells;
    private TileBase[] blockBuffer;
    // 바닥 층마다 영역의 원래 타일(높이를 바꿀 때 사본을 고르는 기준).
    private readonly List<TileBase[]> floorBlocks = new List<TileBase[]>(4);
    // 복제층마다 옮겨 쓴 칸 수. 하나도 안 쓴 층은 비우지 않는다.
    private readonly Dictionary<Tilemap, int> liftWritten = new Dictionary<Tilemap, int>(8);
    private TileBase[] clearStrip = new TileBase[0];
    private int clearStripRows;
    // 시작 준비: 옮겨 쓸 층 차례(물 → 해안선 → 바닥)와 지금 층·그 층에서 다음에 볼 영역 칸 번호.
    private readonly List<CopyJob> copyJobs = new List<CopyJob>(8);
    private int copyJob;
    private int copyCell;
    private bool bPreparing;
    // 옮겨 쓸 칸을 모으는 버퍼(CopyStep).
    private readonly List<Vector3Int> copyPositions = new List<Vector3Int>(4096);
    private readonly List<TileBase> copyTiles = new List<TileBase>(4096);
    private readonly List<Vector3Int> copyAnimatedCells = new List<Vector3Int>(1024);
    // 이번 프레임에 복제층마다 바꿀 칸(바닥 복제층별·테두리·틈 빛). Tick 끝에 층마다 한 번에 쓴다(FlushChanges).
    private readonly List<List<TileChangeData>> floorChanges = new List<List<TileChangeData>>(4);
    private readonly List<TileChangeData> outlineChanges = new List<TileChangeData>(512);
    private readonly List<TileChangeData> glowLeftChanges = new List<TileChangeData>(256);
    private readonly List<TileChangeData> glowRightChanges = new List<TileChangeData>(256);

    private readonly List<RippleCell> cells = new List<RippleCell>(2048);
    // cells와 같은 순서. 지금 그려 둔 높이(픽셀)와 아웃라인 단계. 바뀐 칸만 다시 쓴다.
    private short[] lastPixels = new short[0];
    private sbyte[] lastOutlineSteps = new sbyte[0];
    // 틈 빛: 지금 깔아 둔 띠 높이(왼쪽·오른쪽)와 그때의 칸 높이. 높이가 바뀐 칸과 그 앞 칸만 다시 계산한다.
    private sbyte[] lastGlowLeft = new sbyte[0];
    private sbyte[] lastGlowRight = new sbyte[0];
    private short[] glowPixels = new short[0];
    private bool[] glowDirty = new bool[0];
    private readonly List<int> glowDirtyOrders = new List<int>(256);
    // 영역 칸 번호 → cells 순서(물결이 닿지 않는 칸은 -1). 이웃 칸 높이를 찾는다.
    private int[] orderOfIndex = new int[0];

    private Vector3 center;
    // 지진이 넘겨준 피해 범위(바닥 평면 거리). 0이면 없음 - 물결 높이·나무 떨림이 범위를 가르지 않는다.
    private float strikeRadius;
    private float elapsed;
    private int nextCell;   // 아직 물결이 닿지 않은 첫 칸
    private int doneCell;   // 물결이 다 지나가지 않은 첫 칸
    private bool bPlaying;
    private Grid rippleGrid;
    // 복제층에 옮겨 그린 영역(월드). 이 안의 물체만 함께 출렁인다.
    private Rect regionRect;
    private readonly QuakeObjectRide objectRide = new QuakeObjectRide();
    // 물체 출렁임은 첫 Tick에 시작한다. 시작하는 프레임의 일(복제층 옮겨 쓰기)과 나눠 한 프레임이 무거워지지 않게 한다.
    // 첫 Tick까지는 물결이 아직 어느 칸에도 닿지 않아 물체가 움직일 일이 없다.
    private bool bObjectRidePending;
    // 물결이 끝나 숨겨 둔 복제층을 비우는 일(HideLifts). Tick마다 하나씩 한다.
    private readonly List<ClearJob> pendingClears = new List<ClearJob>(32);

    public bool IsPlaying => bPlaying;

    /// <summary>시작했지만 아직 복제층을 옮겨 쓰는 중이라 물결이 퍼지기 전(화면은 실제 층 그대로).</summary>
    public bool IsPreparing => bPreparing;

    /// <summary>물결을 준비하거나 돌고 있거나, 끝난 복제층을 아직 비우는 중(이때도 Tick을 불러야 한다).</summary>
    public bool IsBusy => bPreparing || bPlaying || pendingClears.Count > 0;

    public void Initialize(QuakeTileRippleSettings _settings)
    {
        settings = _settings ?? new QuakeTileRippleSettings();
        ClearTreeShake();
    }

    /// <summary>
    /// 물결을 시작한다. _floor·_water·_shore는 실제 시각 타일맵 묶음(TileMapGenerator.GetVisualTilemapsForRun과 같은 순서),
    /// _grid는 그 그리드, _viewRect는 지금 화면(월드). 이미 돌던 물결은 바로 끝내고 새로 시작한다.
    /// 물결이 닿을 바닥이 없으면 false. 물결은 복제층을 다 옮겨 쓴 Tick부터 퍼진다(IsPreparing, [시작 준비]).
    /// _strikeRadius(바닥 평면 거리)를 넘기면 그 안은 크게, 바깥은 낮게 출렁이고 안쪽 나무만 튄다. 0이면 범위를 가르지 않는다.
    /// </summary>
    public bool Begin(Grid _grid, List<Tilemap> _floor, List<Tilemap> _water, List<Tilemap> _shore, Vector3 _center, Rect _viewRect, float _strikeRadius = 0f)
    {
        Stop();
        // 지난 영역을 마저 비운다. 새 영역이 달라 덮어쓰지 못한 칸이 남으면 안 된다.
        FinishClears();
        if (null == _grid) return false;

        BindLayers(_floor, _water, _shore);
        if (floorLayers.Count == 0) return false;
        if (false == EnsureLifts(_grid.transform)) return false;

        center = _center;
        strikeRadius = Mathf.Max(0f, _strikeRadius);
        rippleGrid = _grid;
        BuildRegion(_grid, _viewRect);
        ReadFloorBlocks();
        BuildCells();
        if (cells.Count == 0) return false;

        // 옮겨 쓰는 동안은 숨겨 둔다. 새로 만든 복제층(EnsureLifts)은 렌더러가 켜진 채라, 그대로 두면 반쯤 쓴 층이 보인다.
        SetLiftsVisible(false);
        BuildCopyJobs();
        bPreparing = true;
        return true;
    }

    /// <summary>
    /// 물결이 돌거나 끝난 복제층을 비우는 중이면(IsBusy) 매 프레임 부른다(게임 시간).
    /// 물결이 다 지나가면 복제층을 숨기고 false를 돌려준다. 그 뒤 부를 때마다 숨긴 복제층을 한 층씩 비운다.
    /// </summary>
    public bool Tick(float _deltaTime)
    {
        if (bPreparing)
        {
            if (false == CopyStep()) return true;

            // 다 옮겨 썼다. 이 프레임부터 복제층이 보이고(아직 모든 칸이 제자리) 다음 Tick부터 물결이 퍼진다.
            bPreparing = false;
            ApplyGlowMaterial();
            SetLiftsVisible(true);

            elapsed = 0f;
            nextCell = 0;
            doneCell = 0;
            bPlaying = true;
            BeginTreeShake();
            // 물체를 칸 높이만큼 옮기지 않아도(rideObjects 꺼짐) 맞은 나무가 튀려면 나무를 그리는 동안 높이를 담아야 한다.
            bObjectRidePending = settings.rideObjects || TreePopEnabled;
            return true;
        }

        if (false == bPlaying)
        {
            ClearOnePending();
            return false;
        }

        if (bObjectRidePending)
        {
            bObjectRidePending = false;
            objectRide.Begin(this, regionRect, settings.rideObjects, TreePopEnabled ? settings.treePopHeight : 0f, settings.treePopSeconds);
            Shader.SetGlobalVector(QuakeShakeDId, new Vector4(1f, 0f, 0f, 0f));
        }

        elapsed += _deltaTime;
        float length = RippleLength;
        float front = elapsed * Mathf.Max(0.1f, settings.frontSpeed);

        while (nextCell < cells.Count && cells[nextCell].key <= front) nextCell++;

        for (int i = doneCell; i < nextCell; i++)
        {
            float distance = front - cells[i].key;
            if (distance >= length)
            {
                if (i == doneCell) doneCell++;
                RestCell(i);
                continue;
            }

            PoseCell(i, distance, length);
        }

        FlushGlow();
        FlushChanges();
        objectRide.Tick(_deltaTime);
        if (settings.treeShake) Shader.SetGlobalVector(QuakeShakeAId, new Vector4(center.x, center.y, front, strikeRadius));

        if (doneCell < cells.Count) return true;

        Stop();
        return false;
    }

    /// <summary>바로 끝낸다(복제층을 숨겨 실제 층만 보이게 한다). 맵을 새로 깔거나 던전을 떠날 때도 부른다.</summary>
    public void Stop()
    {
        if (false == bPlaying && false == bPreparing) return;

        // 준비 중이었으면 복제층은 아직 숨겨져 있고, 그때까지 옮겨 쓴 칸만 비운다(liftWritten).
        bPreparing = false;
        bPlaying = false;
        bObjectRidePending = false;
        objectRide.Stop();
        ClearTreeShake();
        HideLifts();
    }

    /// <summary>
    /// 지진에 맞은 나무(TreeObj 루트)를 알린다. 물결 위 물체로 함께 출렁이는 중이면 그리는 동안 위로 한 번 튄다(QuakeObjectRide).
    /// 물결이 돌지 않으면(준비 중·끝남) 무시한다.
    /// </summary>
    public void MarkStruck(Transform _target)
    {
        if (bPlaying) objectRide.MarkStruck(_target);
    }

    // 지진에 맞은 나무가 튀는지(나무 떨림 설정의 일부).
    private bool TreePopEnabled => settings.treeShake && settings.treePopHeight > 0f;

    /// <summary>나무 셰이더 떨림을 켠다. 물결 동안 바뀌지 않는 값은 여기서 한 번만 넘기고, 앞머리(A)는 Tick마다 넘긴다.</summary>
    private void BeginTreeShake()
    {
        if (false == settings.treeShake) return;

        Shader.SetGlobalVector(QuakeShakeAId, new Vector4(center.x, center.y, 0f, strikeRadius));
        Shader.SetGlobalVector(QuakeShakeBId, new Vector4(1f, Mathf.Max(0.1f, settings.frontSpeed), Mathf.Max(0.1f, settings.reachRadius), Mathf.Max(0.01f, settings.strikeEdge)));
        Shader.SetGlobalVector(QuakeShakeCId, new Vector4(settings.treeShiverDegrees, settings.treeShiverOutsideDegrees, settings.treeShiverSeconds, settings.treeShiverHz));
    }

    /// <summary>
    /// 나무 셰이더 지진 상태를 끈다(떨림 세기 0, 물결 높이 풀기 꺼짐 - 셰이더가 버텍스를 그대로 돌려준다).
    /// 연출 도중 시스템이 꺼질 때(씬 정리·에디터 플레이 종료)도 부른다. 전역값은 씬이 바뀌어도 남는다.
    /// </summary>
    public static void ClearTreeShake()
    {
        Shader.SetGlobalVector(QuakeShakeBId, Vector4.zero);
        Shader.SetGlobalVector(QuakeShakeDId, Vector4.zero);
    }

    /// <summary>_world(지면 위치)가 선 칸의 지금 높이(픽셀, 위가 +). 물결이 닿지 않는 칸·물 칸·영역 밖은 0.</summary>
    public int PixelsAt(Vector3 _world)
    {
        if (false == bPlaying || null == rippleGrid) return 0;

        Vector3Int cell = rippleGrid.WorldToCell(_world);
        int x = cell.x - region.xMin;
        int y = cell.y - region.yMin;
        if (x < 0 || y < 0 || x >= region.size.x || y >= region.size.y) return 0;

        int order = orderOfIndex[x + y * region.size.x];
        return order < 0 ? 0 : lastPixels[order];
    }

    // // 내부

    private void BindLayers(List<Tilemap> _floor, List<Tilemap> _water, List<Tilemap> _shore)
    {
        CollectVisible(_floor, floorLayers);
        CollectVisible(_water, waterLayers);
        CollectVisible(_shore, shoreLayers);
    }

    /// <summary>렌더러가 켜져 있고 스텐실 재질이 아닌 층만 고른다. 스텐실은 실제 층이 제자리에서 그대로 맞다.</summary>
    private static void CollectVisible(List<Tilemap> _from, List<Tilemap> _into)
    {
        _into.Clear();
        if (null == _from) return;

        for (int i = 0; i < _from.Count; i++)
        {
            Tilemap layer = _from[i];
            if (null == layer) continue;

            TilemapRenderer renderer = layer.GetComponent<TilemapRenderer>();
            if (null == renderer || false == renderer.enabled) continue;

            Material material = renderer.sharedMaterial;
            if (null != material && null != material.shader && material.shader.name.Contains("Stencil")) continue;

            _into.Add(layer);
        }
    }

    /// <summary>
    /// 복제층을 (처음, 또는 그리드가 바뀌었을 때) 만든다. 실제 층과 앞뒤 순서가 같도록, 정렬 순서가 같은 층은 같은 순서를,
    /// 다른 층은 그 차례대로 모든 실제 타일 층보다 위의 번호를 준다(바닥·물은 같은 번호라 칸 위치로 앞뒤가 섞인다).
    /// 아웃라인과 바닥 위 장식은 바닥 블록과 같은 번호에 아주 조금 앞으로 놓아 칸마다 섞는다.
    /// </summary>
    private bool EnsureLifts(Transform _gridTransform)
    {
        if (null != liftParent && liftParent == _gridTransform && null != outlineLift && null != glowLeftLift && null != glowRightLift && LiftsMatchSources()) return true;

        DestroyLifts();

        int highestOrder = int.MinValue;
        HighestOrder(floorLayers, ref highestOrder);
        HighestOrder(waterLayers, ref highestOrder);
        HighestOrder(shoreLayers, ref highestOrder);
        if (int.MinValue == highestOrder) return false;

        // 실제 정렬 순서 → 복제층 정렬 순서. 순서값이 작은 것부터 highestOrder + 1, + 2 ...
        List<int> orders = new List<int>(8);
        AddOrders(floorLayers, orders);
        AddOrders(waterLayers, orders);
        AddOrders(shoreLayers, orders);
        orders.Sort();

        CreateLifts(floorLayers, floorLifts, orders, highestOrder);
        CreateLifts(waterLayers, waterLifts, orders, highestOrder);
        CreateLifts(shoreLayers, shoreLifts, orders, highestOrder);

        Tilemap ground = floorLayers[0];
        Tilemap groundLift = floorLifts[0];
        int groundOrder = groundLift.GetComponent<TilemapRenderer>().sortingOrder;

        outlineLift = CreateLift(ground, ground.GetComponent<TilemapRenderer>(), groundOrder, "QuakeRippleOutline");
        outlineLift.transform.localPosition += new Vector3(0f, -SortNudge, 0f);

        // 틈 빛: 제 칸 바닥·테두리 다음, 제 칸 장식보다 먼저(돌이 틈 앞을 가린다). 재질은 시작할 때 물 복제층 것으로 맞춘다(ApplyGlowMaterial).
        glowLeftLift = CreateLift(ground, ground.GetComponent<TilemapRenderer>(), groundOrder, "QuakeRippleGlowLeft");
        glowLeftLift.transform.localPosition += new Vector3(0f, -1.5f * SortNudge, 0f);
        glowRightLift = CreateLift(ground, ground.GetComponent<TilemapRenderer>(), groundOrder, "QuakeRippleGlowRight");
        glowRightLift.transform.localPosition += new Vector3(0f, -1.5f * SortNudge, 0f);

        // 장식 층: 바닥 자리에 두고(정렬은 칸 자리) 비켜 놓인 만큼은 피벗으로 그린다.
        floorLiftOffsets.Clear();
        floorLiftOffsets.Add(Vector2Int.zero);
        for (int l = 1; l < floorLifts.Count; l++)
        {
            Vector3 offset = floorLayers[l].transform.localPosition - ground.transform.localPosition;
            floorLiftOffsets.Add(new Vector2Int(Mathf.RoundToInt(offset.x * PixelsPerUnit), Mathf.RoundToInt(offset.y * PixelsPerUnit)));
            floorLifts[l].transform.localPosition = groundLift.transform.localPosition + new Vector3(0f, -2f * SortNudge, 0f);
            floorLifts[l].GetComponent<TilemapRenderer>().sortingOrder = groundOrder;
        }

        floorChanges.Clear();
        for (int l = 0; l < floorLifts.Count; l++) floorChanges.Add(new List<TileChangeData>(512));

        liftParent = _gridTransform;
        return true;
    }

    private bool LiftsMatchSources()
    {
        return LiftsMatch(floorLayers, floorLifts) && LiftsMatch(waterLayers, waterLifts) && LiftsMatch(shoreLayers, shoreLifts);
    }

    private static bool LiftsMatch(List<Tilemap> _sources, List<Tilemap> _lifts)
    {
        if (_sources.Count != _lifts.Count) return false;
        for (int i = 0; i < _lifts.Count; i++)
        {
            if (null == _lifts[i]) return false;
        }

        return true;
    }

    private static void AddOrders(List<Tilemap> _layers, List<int> _orders)
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            int order = _layers[i].GetComponent<TilemapRenderer>().sortingOrder;
            if (false == _orders.Contains(order)) _orders.Add(order);
        }
    }

    private static int LiftOrder(int _sourceOrder, List<int> _orders, int _highestOrder)
    {
        return _highestOrder + 1 + _orders.IndexOf(_sourceOrder);
    }

    private static void CreateLifts(List<Tilemap> _sources, List<Tilemap> _lifts, List<int> _orders, int _highestOrder)
    {
        _lifts.Clear();
        for (int i = 0; i < _sources.Count; i++)
        {
            TilemapRenderer renderer = _sources[i].GetComponent<TilemapRenderer>();
            _lifts.Add(CreateLift(_sources[i], renderer, LiftOrder(renderer.sortingOrder, _orders, _highestOrder), "QuakeRippleLift_" + _sources[i].name));
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
        renderer.sortOrder = _sourceRenderer.sortOrder;
        renderer.maskInteraction = _sourceRenderer.maskInteraction;
        // 칸마다 따로 정렬해야 솟은 칸과 이웃 칸의 앞뒤가 맞는다.
        renderer.mode = TilemapRenderer.Mode.Individual;
        return tilemap;
    }

    private static void HighestOrder(List<Tilemap> _layers, ref int _order)
    {
        for (int i = 0; i < _layers.Count; i++)
        {
            int order = _layers[i].GetComponent<TilemapRenderer>().sortingOrder;
            if (order > _order) _order = order;
        }
    }

    private void DestroyLifts()
    {
        DestroyAll(floorLifts);
        DestroyAll(waterLifts);
        DestroyAll(shoreLifts);
        if (null != outlineLift) DestroyObject(outlineLift.gameObject);
        if (null != glowLeftLift) DestroyObject(glowLeftLift.gameObject);
        if (null != glowRightLift) DestroyObject(glowRightLift.gameObject);
        outlineLift = null;
        glowLeftLift = null;
        glowRightLift = null;
        liftParent = null;
        pendingClears.Clear();
        liftWritten.Clear();

        foreach (Material copy in liftMaterials.Values)
        {
            if (null != copy) DestroyObject(copy);
        }

        liftMaterials.Clear();
    }

    private static void DestroyAll(List<Tilemap> _lifts)
    {
        for (int i = 0; i < _lifts.Count; i++)
        {
            if (null != _lifts[i]) DestroyObject(_lifts[i].gameObject);
        }

        _lifts.Clear();
    }

    private static void DestroyObject(UnityEngine.Object _object)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(_object);
        else UnityEngine.Object.DestroyImmediate(_object);
    }

    /// <summary>화면(+여유) 사각형이 덮는 칸의 최소 사각형(칸 좌표). 그리드 밖 바다(외곽 물)도 그대로 포함한다.</summary>
    private void BuildRegion(Grid _grid, Rect _viewRect)
    {
        float margin = Mathf.Max(0f, settings.viewMargin);
        Rect rect = Rect.MinMaxRect(_viewRect.xMin - margin, _viewRect.yMin - margin, _viewRect.xMax + margin, _viewRect.yMax + margin);
        regionRect = rect;

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int c = 0; c < 4; c++)
        {
            Vector3 corner = new Vector3((c & 1) == 0 ? rect.xMin : rect.xMax, (c & 2) == 0 ? rect.yMin : rect.yMax, 0f);
            Vector3Int cell = _grid.WorldToCell(corner);
            if (cell.x < minX) minX = cell.x;
            if (cell.y < minY) minY = cell.y;
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }

        // 칸 중심은 칸 좌표의 1차식이다(아이소 그리드). 기준 칸과 x·y 한 칸 차이만 그리드에 묻는다.
        Vector3 zero = _grid.GetCellCenterWorld(Vector3Int.zero);
        cellStepX = _grid.GetCellCenterWorld(Vector3Int.right) - zero;
        cellStepY = _grid.GetCellCenterWorld(Vector3Int.up) - zero;

        // 모서리 칸이 반쯤 걸치는 경우를 위해 한 칸씩 더.
        region = new BoundsInt(minX - 1, minY - 1, 0, maxX - minX + 3, maxY - minY + 3, 1);
        cellOrigin = _grid.GetCellCenterWorld(region.min);

        regionCells = region.size.x * region.size.y;
        if (null == blockBuffer || blockBuffer.Length < regionCells)
        {
            blockBuffer = new TileBase[regionCells];
            floorBlocks.Clear();
        }

        while (floorBlocks.Count < floorLayers.Count) floorBlocks.Add(new TileBase[blockBuffer.Length]);
    }

    /// <summary>바닥 층마다 영역의 원래 타일을 뜬다(물결이 닿는 칸 고르기·높이 사본 고르기의 기준).</summary>
    private void ReadFloorBlocks()
    {
        for (int l = 0; l < floorLayers.Count; l++) floorLayers[l].GetTilesBlockNonAlloc(region, floorBlocks[l]);
    }

    /// <summary>옮겨 쓸 층 차례를 세운다(물 → 해안선 → 바닥). 칸은 Tick마다 CopyStep이 나눠 쓴다.</summary>
    private void BuildCopyJobs()
    {
        if (null == propertyBlock) propertyBlock = new MaterialPropertyBlock();

        liftWritten.Clear();
        copyJobs.Clear();
        copyJob = 0;
        copyCell = 0;

        for (int l = 0; l < waterLayers.Count; l++) copyJobs.Add(new CopyJob { source = waterLayers[l], lift = waterLifts[l], floorLayer = -1 });
        for (int l = 0; l < shoreLayers.Count; l++) copyJobs.Add(new CopyJob { source = shoreLayers[l], lift = shoreLifts[l], floorLayer = -1 });
        for (int l = 0; l < floorLayers.Count; l++) copyJobs.Add(new CopyJob { source = floorLayers[l], lift = floorLifts[l], floorLayer = l, offsetPixels = floorLiftOffsets[l] });
    }

    /// <summary>
    /// 실제 층에서 뜬 영역의 비어 있지 않은 칸을 복제층에 이어서 쓴다(모든 층 합 CopyCostPerTick만큼). 다 옮겼으면 true.
    /// 층마다 처음 쓸 때 재질(속성 블록 포함)·색을 맞추고, 쓴 칸의 애니메이션 시간도 실제 층에 맞춘다.
    /// 장식 층은 비켜 놓인 만큼(offsetPixels) 피벗을 옮긴 사본으로 쓴다.
    /// </summary>
    private bool CopyStep()
    {
        int budget = CopyCostPerTick;
        while (copyJob < copyJobs.Count)
        {
            CopyJob job = copyJobs[copyJob];
            if (0 == copyCell) BeginCopyJob(job);
            TileBase[] tiles = job.floorLayer >= 0 ? floorBlocks[job.floorLayer] : blockBuffer;

            copyPositions.Clear();
            copyTiles.Clear();
            copyAnimatedCells.Clear();
            int i = copyCell;
            for (; i < regionCells && budget > 0; i++)
            {
                if (null == tiles[i]) continue;

                Vector3Int cell = CellOf(i);
                copyPositions.Add(cell);
                copyTiles.Add(OffsetTile(tiles[i], job.offsetPixels));

                if (IsAnimated(job.source, tiles[i], cell))
                {
                    copyAnimatedCells.Add(cell);
                    budget -= AnimatedCopyCost;
                }
                else
                {
                    budget -= 1;
                }
            }

            copyCell = i;
            WriteCopied(job);

            // 이 층을 다 쓰기 전에 이번 몫이 끝났다.
            if (copyCell < regionCells) return false;

            copyJob++;
            copyCell = 0;
            if (budget <= 0) break;
        }

        return copyJob >= copyJobs.Count;
    }

    private void BeginCopyJob(CopyJob _job)
    {
        _job.lift.color = _job.source.color;
        ApplySourceMaterial(_job.source.GetComponent<TilemapRenderer>(), _job.lift.GetComponent<TilemapRenderer>());

        // 바닥 층은 Begin에서 이미 떴다(ReadFloorBlocks). 물·해안선은 제 차례에 뜬다(그 층을 다 쓸 때까지 blockBuffer를 쓴다).
        if (_job.floorLayer < 0) _job.source.GetTilesBlockNonAlloc(region, blockBuffer);
    }

    private void WriteCopied(CopyJob _job)
    {
        if (copyPositions.Count == 0) return;

        SetTiles(_job.lift, copyPositions, copyTiles);
        liftWritten.TryGetValue(_job.lift, out int written);
        liftWritten[_job.lift] = written + copyPositions.Count;

        // 애니메이션 타일(용암·물결)만 실제 층의 그 칸 시간에 맞춘다. 안 맞추면 끝나고 실제 층으로 돌아갈 때 출렁임 위상이 튄다.
        for (int c = 0; c < copyAnimatedCells.Count; c++)
        {
            Vector3Int cell = copyAnimatedCells[c];
            _job.lift.SetAnimationTime(cell, _job.source.GetAnimationTime(cell));
        }
    }

    // // 한 번에 쓰기

    /// <summary>길이가 2의 거듭제곱이고 _count 이상인 재사용 배열.</summary>
    private static T[] Batch<T>(T[][] _pool, int _count)
    {
        int slot = 0;
        while ((1 << slot) < _count) slot++;
        if (slot >= _pool.Length) return new T[_count];

        return _pool[slot] ??= new T[1 << slot];
    }

    private static void SetTiles(Tilemap _lift, List<Vector3Int> _positions, List<TileBase> _tiles)
    {
        int count = _positions.Count;
        Vector3Int[] positions = Batch(positionBatches, count);
        TileBase[] tiles = Batch(tileBatches, count);
        for (int i = 0; i < positions.Length; i++)
        {
            int k = i < count ? i : count - 1;
            positions[i] = _positions[k];
            tiles[i] = _tiles[k];
        }

        _lift.SetTiles(positions, tiles);
    }

    /// <summary>이번 프레임에 모아 둔 칸 변경을 복제층마다 한 번에 쓴다.</summary>
    private void FlushChanges()
    {
        for (int l = 0; l < floorLifts.Count; l++) FlushChanges(floorLifts[l], floorChanges[l]);
        FlushChanges(outlineLift, outlineChanges);
        FlushChanges(glowLeftLift, glowLeftChanges);
        FlushChanges(glowRightLift, glowRightChanges);
    }

    internal static void FlushChanges(Tilemap _lift, List<TileChangeData> _changes)
    {
        int count = _changes.Count;
        if (0 == count) return;

        TileChangeData[] batch = Batch(changeBatches, count);
        for (int i = 0; i < batch.Length; i++) batch[i] = _changes[i < count ? i : count - 1];

        _lift.SetTiles(batch, true);
        _changes.Clear();
    }

    private void ClearChanges()
    {
        for (int l = 0; l < floorChanges.Count; l++) floorChanges[l].Clear();
        outlineChanges.Clear();
        glowLeftChanges.Clear();
        glowRightChanges.Clear();
    }

    /// <summary>
    /// 복제층 재질을 실제 층과 같게 한다. 실제 층의 속성 블록(용암 HDR 세기 등)은 재질 사본에 옮겨 담는다 -
    /// Individual 모드 타일맵에 속성 블록을 넣으면 칸마다의 스프라이트 텍스처 연결이 깨져 모든 칸이 한 그림으로 그려진다(실측).
    /// </summary>
    private void ApplySourceMaterial(TilemapRenderer _source, TilemapRenderer _lift)
    {
        Material material = _source.sharedMaterial;
        _source.GetPropertyBlock(propertyBlock);
        if (null == material || propertyBlock.isEmpty)
        {
            _lift.sharedMaterial = material;
            return;
        }

        if (false == liftMaterials.TryGetValue(_lift, out Material copy) || null == copy || copy.shader != material.shader)
        {
            copy = new Material(material) { name = material.name + " (QuakeRipple)" };
            liftMaterials[_lift] = copy;
        }
        else
        {
            copy.CopyPropertiesFromMaterial(material);
        }

        Shader shader = material.shader;
        int count = shader.GetPropertyCount();
        for (int p = 0; p < count; p++)
        {
            int id = shader.GetPropertyNameId(p);
            switch (shader.GetPropertyType(p))
            {
                case UnityEngine.Rendering.ShaderPropertyType.Float:
                case UnityEngine.Rendering.ShaderPropertyType.Range:
                    if (propertyBlock.HasFloat(id)) copy.SetFloat(id, propertyBlock.GetFloat(id));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Color:
                    if (propertyBlock.HasColor(id)) copy.SetColor(id, propertyBlock.GetColor(id));
                    break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector:
                    if (propertyBlock.HasVector(id)) copy.SetVector(id, propertyBlock.GetVector(id));
                    break;
            }
        }

        _lift.sharedMaterial = copy;
    }

    private bool IsAnimated(Tilemap _source, TileBase _tile, Vector3Int _cell)
    {
        if (animatedTiles.TryGetValue(_tile, out bool bAnimated)) return bAnimated;

        bAnimated = _source.GetAnimationFrameCount(_cell) > 1;
        animatedTiles.Add(_tile, bAnimated);
        return bAnimated;
    }

    /// <summary>
    /// 복제층을 숨기고 비울 차례에 올린다. 한 번에 비우면 끝나는 순간(화면이 잠잠해진 때) 한 프레임이 눈에 띄게 걸리므로,
    /// 숨기기만 하고 비우기는 다음 프레임부터 조금씩 한다(Tick). 숨긴 동안은 보이지 않으니 언제 비워도 같다.
    /// 많이 쓴 층은 영역을 띠(ClearStripCells칸)로 나눠 비우고, 조금 쓴 층·테두리·틈 빛은 ClearAllTiles로 한 번에 비운다.
    /// </summary>
    private void HideLifts()
    {
        SetLiftsVisible(false);
        ClearChanges();

        pendingClears.Clear();
        QueueClears(floorLifts);
        QueueClears(waterLifts);
        QueueClears(shoreLifts);
        QueueClearAll(outlineLift);
        QueueClearAll(glowLeftLift);
        QueueClearAll(glowRightLift);
        liftWritten.Clear();
    }

    private void QueueClears(List<Tilemap> _lifts)
    {
        for (int l = 0; l < _lifts.Count; l++)
        {
            Tilemap lift = _lifts[l];
            if (null == lift || false == liftWritten.TryGetValue(lift, out int written) || 0 == written) continue;

            if (written < ClearAllBelow)
            {
                QueueClearAll(lift);
                continue;
            }

            int width = region.size.x;
            int rows = region.size.y;
            clearStripRows = Mathf.Clamp(ClearStripCells / width, 1, rows);
            if (clearStrip.Length != width * clearStripRows) clearStrip = new TileBase[width * clearStripRows];

            // 마지막 띠도 같은 크기로 하려고 끝에 맞춰 당긴다(이미 비운 줄을 한 번 더 비울 뿐이다).
            for (int row = 0; row < rows; row += clearStripRows)
            {
                pendingClears.Add(new ClearJob { lift = lift, row = Mathf.Min(row, rows - clearStripRows) });
            }
        }
    }

    private void QueueClearAll(Tilemap _lift)
    {
        if (null != _lift) pendingClears.Add(new ClearJob { lift = _lift, bAll = true });
    }

    private void SetLiftsVisible(bool _bVisible)
    {
        SetVisible(floorLifts, _bVisible);
        SetVisible(waterLifts, _bVisible);
        SetVisible(shoreLifts, _bVisible);
        if (null != outlineLift) outlineLift.GetComponent<TilemapRenderer>().enabled = _bVisible;
        if (null != glowLeftLift) glowLeftLift.GetComponent<TilemapRenderer>().enabled = _bVisible && bGlow;
        if (null != glowRightLift) glowRightLift.GetComponent<TilemapRenderer>().enabled = _bVisible && bGlow;
    }

    private static void SetVisible(List<Tilemap> _lifts, bool _bVisible)
    {
        for (int l = 0; l < _lifts.Count; l++)
        {
            if (null != _lifts[l]) _lifts[l].GetComponent<TilemapRenderer>().enabled = _bVisible;
        }
    }

    private void ClearOnePending()
    {
        int last = pendingClears.Count - 1;
        if (last < 0) return;

        ClearJob job = pendingClears[last];
        pendingClears.RemoveAt(last);
        if (null == job.lift) return;

        if (job.bAll)
        {
            job.lift.ClearAllTiles();
            return;
        }

        BoundsInt strip = new BoundsInt(region.xMin, region.yMin + job.row, 0, region.size.x, clearStripRows, 1);
        job.lift.SetTilesBlock(strip, clearStrip);
    }

    private void FinishClears()
    {
        while (pendingClears.Count > 0) ClearOnePending();
    }

    /// <summary>물결이 닿는 바닥 칸(바닥 블록이 있고 reachRadius 안)을 진원 거리 순으로 모은다.</summary>
    private void BuildCells()
    {
        cells.Clear();

        float reach = Mathf.Max(0.1f, settings.reachRadius);
        float calm = Mathf.Max(0f, settings.calmRadius);
        float maxAmplitude = MaxAmplitude();
        TileBase[] ground = floorBlocks[0];
        int width = region.size.x;

        for (int i = 0; i < regionCells; i++)
        {
            if (null == ground[i]) continue;

            Vector3 pos = cellOrigin + cellStepX * (i % width) + cellStepY * (i / width);
            // 바닥 평면 거리(아이소는 세로가 절반으로 눌려 보이므로 세로를 두 배로 편다). 진원에서 동심원으로 퍼진다.
            float key = new Vector2(pos.x - center.x, (pos.y - center.y) * 2f).magnitude;
            if (key >= reach) continue;

            // 진폭: 진원 둘레(calm)에서 0부터 차오르고, 멀어질수록 reach에서 0으로 잦아든다.
            float rise = calm > 0f ? Mathf.SmoothStep(0f, 1f, key / calm) : 1f;
            float falloff = Mathf.Pow(1f - key / reach, Mathf.Max(0.1f, settings.reachFalloffPower));
            float amplitude = Mathf.Min(maxAmplitude, settings.rippleHeight * rise * falloff * EpicenterScale(key) * StrikeScale(key));
            // 테두리 진하기 중 진원 거리 몫: 크게 출렁이는 칸일수록 진하다(PoseCell).
            float strength = settings.rippleHeight > 0f ? Mathf.Clamp01(amplitude / settings.rippleHeight) : 0f;

            cells.Add(new RippleCell
            {
                index = i,
                cell = CellOf(i),
                key = key,
                amplitude = amplitude,
                outlineStrength = Mathf.Pow(strength, settings.outlineFalloffPower),
            });
        }

        cells.Sort(cellComparer);

        if (lastPixels.Length < cells.Count)
        {
            lastPixels = new short[cells.Count];
            lastOutlineSteps = new sbyte[cells.Count];
            lastGlowLeft = new sbyte[cells.Count];
            lastGlowRight = new sbyte[cells.Count];
            glowPixels = new short[cells.Count];
            glowDirty = new bool[cells.Count];
        }

        Array.Clear(lastPixels, 0, cells.Count);
        Array.Clear(lastOutlineSteps, 0, cells.Count);
        Array.Clear(lastGlowLeft, 0, cells.Count);
        Array.Clear(lastGlowRight, 0, cells.Count);
        Array.Clear(glowPixels, 0, cells.Count);
        Array.Clear(glowDirty, 0, cells.Count);
        glowDirtyOrders.Clear();

        if (orderOfIndex.Length < regionCells) orderOfIndex = new int[ground.Length];
        for (int i = 0; i < regionCells; i++) orderOfIndex[i] = -1;
        for (int o = 0; o < cells.Count; o++) orderOfIndex[cells[o].index] = o;
    }

    /// <summary>
    /// 칸 진폭의 한도. 물결이 가장 가파른 곳(앞머리, 기울기 = 진폭 × 2π/파장)에서 이웃 칸 높이 차가 MaxNeighborRise 픽셀을 넘지 않게 한다.
    /// 이웃 칸은 진원 거리로 칸 한 걸음(cellStepX·cellStepY 중 긴 쪽)만큼 떨어져 있다. 기본 물결(첫 고리를 키우지 않은 높이)은 이 한도 아래다.
    /// </summary>
    private float MaxAmplitude()
    {
        float step = Mathf.Max(FloorDistance(cellStepX), FloorDistance(cellStepY));
        float slope = 2f * Mathf.PI / Mathf.Max(0.1f, settings.rippleWavelength) * Mathf.Max(0.01f, step);
        return MaxNeighborRise / PixelsPerUnit / slope;
    }

    // 바닥 평면 거리(세로를 두 배로 편다 - 진원 거리 key와 같은 공식).
    private static float FloorDistance(Vector3 _step) => new Vector2(_step.x, _step.y * 2f).magnitude;

    /// <summary>진원 둘레 첫 고리를 키우는 배율. 진원에서 epicenterBoostRadius까지 1 + epicenterBoost에서 1로 줄어든다.</summary>
    private float EpicenterScale(float _key)
    {
        if (settings.epicenterBoost <= 0f || settings.epicenterBoostRadius <= 0f) return 1f;
        return 1f + settings.epicenterBoost * (1f - Mathf.SmoothStep(0f, 1f, _key / settings.epicenterBoostRadius));
    }

    /// <summary>
    /// 피해 범위 배율. 범위 안 1, 경계 폭(strikeEdge)을 지나며 strikeOutsideScale로 줄어든다. 피해 범위가 경계 폭의 한가운데라
    /// 높이가 절반쯤 줄어든 곳이 실제로 맞는 끝이다(나무 셰이더 떨림 각도도 같은 경계). 범위가 없으면 1.
    /// </summary>
    private float StrikeScale(float _key)
    {
        if (strikeRadius <= 0f) return 1f;
        float edge = Mathf.Max(0.01f, settings.strikeEdge);
        float outside = Mathf.SmoothStep(0f, 1f, (_key - strikeRadius) / edge + 0.5f);
        return Mathf.Lerp(1f, Mathf.Clamp01(settings.strikeOutsideScale), outside);
    }

    private Vector3Int CellOf(int _index) => new Vector3Int(region.xMin + _index % region.size.x, region.yMin + _index / region.size.x, 0);

    /// <summary>앞머리 뒤로 물결이 이어지는 거리(바닥 평면 거리). 이만큼 지나간 칸은 제자리에서 멈춘다.</summary>
    private float RippleLength => Mathf.Max(0.1f, settings.rippleWavelength) * Mathf.Max(0.25f, settings.rippleWaves);

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
    /// 앞머리에서 _distance만큼 뒤에 있는 칸. 높이는 감쇠 물결 sin(2π·d/λ) × (1 - d/L) × 칸 진폭이다.
    /// 앞머리(0)와 끝(L)에서 0이고, 끝이 반 파장의 배수면 sin도 0이라 멈출 때 튀지 않는다.
    /// </summary>
    private void PoseCell(int _order, float _distance, float _length)
    {
        RippleCell cell = cells[_order];

        float wave = Mathf.Sin(2f * Mathf.PI * _distance / Mathf.Max(0.1f, settings.rippleWavelength));
        float height = cell.amplitude * wave * (1f - _distance / _length);
        short pixels = (short)Mathf.Clamp(Mathf.RoundToInt(height * PixelsPerUnit), -MaxOffsetPixels, MaxOffsetPixels);
        bool bMoved = pixels != lastPixels[_order];
        if (bMoved)
        {
            lastPixels[_order] = pixels;
            QueueHeight(_order);
        }

        // 테두리: 이 칸이 얼마나 크게 출렁이는지(진원 거리), 물결 진행, 지금 마루·골에 있는지(수평을 지날 때는 옅게)로 진하기가 정해진다.
        // 마루·골을 따라 진한 띠가 퍼져 나가 칸마다 솟고 꺼지는 게 읽히고, 물결 전체가 격자 무늬로 덮이지 않는다.
        float swing = Mathf.Lerp(Mathf.Clamp01(settings.outlineOnLevel), 1f, Mathf.Abs(wave));
        float outline = OutlineEnvelope(_distance, _length) * cell.outlineStrength * swing;
        QueueOutline(_order, (sbyte)Mathf.RoundToInt(outline * OutlineSteps), bMoved);
    }

    /// <summary>물결이 다 지나간 칸: 제자리·원래 색, 테두리 없음.</summary>
    private void RestCell(int _order)
    {
        bool bMoved = 0 != lastPixels[_order];
        if (bMoved)
        {
            lastPixels[_order] = 0;
            QueueHeight(_order);
        }

        QueueOutline(_order, 0, bMoved);
    }

    /// <summary>바닥 복제층(블록·장식)의 그 칸을 지금 높이(lastPixels)의 사본으로 바꾸도록 모은다. 골(내려간 칸)은 깊이만큼 어둡게 한다.</summary>
    private void QueueHeight(int _order)
    {
        RippleCell cell = cells[_order];
        int pixels = lastPixels[_order];

        float shade = 1f;
        if (pixels < 0 && settings.rippleHeight > 0f)
        {
            shade = 1f - Mathf.Clamp01(settings.troughShade) * Mathf.Clamp01(-pixels / PixelsPerUnit / settings.rippleHeight);
        }

        for (int l = 0; l < floorLifts.Count; l++)
        {
            TileBase source = floorBlocks[l][cell.index];
            if (null == source) continue;

            // SetTile처럼 타일의 색·변형을 그대로 쓰고, 골이면 색만 어둡게 덮는다.
            TileBase tile = OffsetTile(source, floorLiftOffsets[l] + new Vector2Int(0, pixels));
            Tile sprite = tile as Tile;
            Color color = shade < 1f ? new Color(shade, shade, shade, 1f) : (null != sprite ? sprite.color : Color.white);
            Matrix4x4 transform = null != sprite ? sprite.transform : Matrix4x4.identity;
            floorChanges[l].Add(new TileChangeData(cell.cell, tile, color, transform));
        }

        // 틈 빛: 이 칸(제 높이가 바뀌어 띠 자리·높이가 바뀐다)과, 이 칸을 뒤 칸으로 두는 앞 칸 둘(칸 좌표 x-1, y-1).
        if (false == bGlow) return;

        MarkGlowDirty(_order);
        int width = region.size.x;
        if (cell.index % width > 0) MarkGlowDirty(orderOfIndex[cell.index - 1]);
        if (cell.index >= width) MarkGlowDirty(orderOfIndex[cell.index - width]);
    }

    // // 용암 틈 빛

    /// <summary>
    /// 틈 빛 복제층 재질을 물(용암) 복제층과 같게 한다(같은 재질 사본이라 HDR 세기·조명이 용암과 같다). 물 층이 없거나 꺼 두면 그리지 않는다.
    /// </summary>
    private void ApplyGlowMaterial()
    {
        Material material = waterLifts.Count > 0 ? waterLifts[0].GetComponent<TilemapRenderer>().sharedMaterial : null;
        bGlow = settings.crackGlow && null != material;
        if (false == bGlow) return;

        glowLeftLift.GetComponent<TilemapRenderer>().sharedMaterial = material;
        glowRightLift.GetComponent<TilemapRenderer>().sharedMaterial = material;
    }

    private void MarkGlowDirty(int _order)
    {
        if (_order < 0 || glowDirty[_order]) return;

        glowDirty[_order] = true;
        glowDirtyOrders.Add(_order);
    }

    /// <summary>이번 프레임에 높이가 바뀐 칸 둘레의 틈 빛을 다시 깐다.</summary>
    private void FlushGlow()
    {
        for (int i = 0; i < glowDirtyOrders.Count; i++)
        {
            int order = glowDirtyOrders[i];
            glowDirty[order] = false;
            UpdateGlow(order);
        }

        glowDirtyOrders.Clear();
    }

    /// <summary>
    /// 이 칸(앞 칸)의 왼쪽 위·오른쪽 위 모서리에 생긴 틈 빛. 뒤 칸이 이 칸보다 솟은 만큼 뒤 칸 옆벽이 드러나고,
    /// 그 바닥(이 칸 모서리 바로 위)을 따라 용암 빛을 깐다. 띠는 이 칸 높이에 붙어 움직인다.
    /// 그림 틀상 이웃 칸 마름모가 한 줄 겹쳐 있어, 드러나는 벽은 높이 차보다 한 줄 적다.
    /// </summary>
    private void UpdateGlow(int _order)
    {
        RippleCell cell = cells[_order];
        int pixels = lastPixels[_order];
        int width = region.size.x;
        int x = cell.index % width;
        int y = cell.index / width;

        // 왼쪽 위 이웃은 칸 좌표 (x, y+1), 오른쪽 위 이웃은 (x+1, y).
        int left = GlowRows(y + 1 < region.size.y ? cell.index + width : -1, pixels);
        int right = GlowRows(x + 1 < width ? cell.index + 1 : -1, pixels);
        bool bMoved = pixels != glowPixels[_order];
        glowPixels[_order] = (short)pixels;

        if (left != lastGlowLeft[_order] || (left > 0 && bMoved))
        {
            lastGlowLeft[_order] = (sbyte)left;
            TileBase tile = left > 0 ? OffsetTile(GlowTile(left, true), new Vector2Int(0, pixels)) : null;
            glowLeftChanges.Add(new TileChangeData(cell.cell, tile, Color.white, Matrix4x4.identity));
        }

        if (right != lastGlowRight[_order] || (right > 0 && bMoved))
        {
            lastGlowRight[_order] = (sbyte)right;
            TileBase tile = right > 0 ? OffsetTile(GlowTile(right, false), new Vector2Int(0, pixels)) : null;
            glowRightChanges.Add(new TileChangeData(cell.cell, tile, Color.white, Matrix4x4.identity));
        }
    }

    /// <summary>뒤 칸(_backIndex, 영역 밖이면 -1)이 이 칸보다 솟아 드러난 옆벽 줄 수. 뒤 칸이 바닥이 아니거나(물·빈칸) 틈이 얕으면 0.</summary>
    private int GlowRows(int _backIndex, int _pixels)
    {
        if (_backIndex < 0 || null == floorBlocks[0][_backIndex]) return 0;

        int backOrder = orderOfIndex[_backIndex];
        int backPixels = backOrder >= 0 ? lastPixels[backOrder] : 0;
        int rows = Mathf.Min(backPixels - _pixels - 1, MaxGlowRows);
        return rows >= GlowMinRows ? rows : 0;
    }

    /// <summary>
    /// 틈 빛 타일(띠 _rows줄). 칸 그림과 같은 32픽셀 폭에, 칸 윗면 마름모 위쪽 16줄을 더 덮는 그림이다(피벗이 칸 중심).
    /// 모서리 바로 위가 틈 바닥이라 가장 밝고, 위로 갈수록 주황 → 붉은 빛이 옅어진다. 색은 용암 타일 그림의 색 그대로이고
    /// 재질의 HDR 세기가 곱해져 깊은 틈에서 블룸이 난다. 틈이 좁으면 은은하다.
    /// </summary>
    private static Tile GlowTile(int _rows, bool _bLeft)
    {
        if (null == glowLeftTiles) BuildGlowTiles();
        return (_bLeft ? glowLeftTiles : glowRightTiles)[_rows];
    }

    // 용암 타일 그림(Stage04_WaterTile)의 색: 가장자리 노랑 → 호박 → 주황 → 붉은 주황.
    private static readonly Color32[] glowPalette =
    {
        new Color32(254, 247, 45, 255),
        new Color32(251, 185, 25, 255),
        new Color32(242, 125, 9, 255),
        new Color32(223, 67, 5, 255),
        new Color32(215, 51, 3, 255),
    };

    private static void BuildGlowTiles()
    {
        const int size = 32;
        // 띠 높이별 32x32 칸을 가로로, 왼쪽(아래 줄)·오른쪽(위 줄)을 세로로 늘어놓은 한 장.
        Texture2D texture = new Texture2D(size * MaxGlowRows, size * 2, TextureFormat.RGBA32, false);
        texture.name = "QuakeRippleCrackGlow";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[texture.width * texture.height];
        for (int side = 0; side < 2; side++)
        {
            for (int rows = 1; rows <= MaxGlowRows; rows++)
            {
                // 틈이 깊을수록 밝다. 좁은 틈은 은은한 주황만 비치고(블룸 임계값 아래), 깊어지면 호박색 선이 블룸을 내며,
                // 아주 깊으면(GlowCoreRows줄 이상) 용암 가장자리처럼 노란 심이 보인다.
                float strength = Mathf.Lerp(GlowMinStrength, GlowMaxStrength, Mathf.Clamp01((rows - GlowMinRows) / (float)(GlowFullRows - GlowMinRows)));
                int firstColor = rows >= GlowCoreRows ? 0 : 1;
                int originX = (rows - 1) * size;
                int originY = side * size;

                for (int column = 0; column < size / 2; column++)
                {
                    // 칸 그림 기준 열. 그 열에서 칸 윗면 마름모가 시작하는 줄(위에서부터): 꼭짓점 쪽 0 ~ 옆 꼭짓점 쪽 7.
                    int x = 0 == side ? column : size / 2 + column;
                    int faceTop = 0 == side ? (size / 2 - 1 - x) / 2 : (x - size / 2) / 2;

                    for (int d = 0; d < rows; d++)
                    {
                        // 틈 그림은 칸 그림보다 16줄 위에서 시작한다(그림 줄 = 칸 줄 + 16). 텍스처는 아래가 0번 줄이다.
                        int tileRow = faceTop - 1 - d;
                        int glowRow = tileRow + size / 2;
                        if (glowRow < 0) break;

                        // 틈 바닥에서 멀어질수록 붉어지며 옅어진다.
                        Color32 color = glowPalette[Mathf.Min(firstColor + d, glowPalette.Length - 1)];
                        float alpha = strength * Mathf.Clamp01(1f - d / (float)GlowFadeRows);
                        color.a = (byte)Mathf.RoundToInt(255f * alpha);
                        pixels[(originX + x) + (originY + size - 1 - glowRow) * texture.width] = color;
                    }
                }
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        glowLeftTiles = new Tile[MaxGlowRows + 1];
        glowRightTiles = new Tile[MaxGlowRows + 1];
        for (int side = 0; side < 2; side++)
        {
            Tile[] tiles = 0 == side ? glowLeftTiles : glowRightTiles;
            for (int rows = 1; rows <= MaxGlowRows; rows++)
            {
                Rect rect = new Rect((rows - 1) * size, side * size, size, size);
                Tile tile = ScriptableObject.CreateInstance<Tile>();
                tile.name = "QuakeRippleCrackGlow_" + (0 == side ? "L" : "R") + rows;
                // 피벗은 그림 아래 가운데 = 칸 그림의 중심(칸 그림 위에서 16줄).
                tile.sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                tile.sprite.name = tile.name;
                tile.colliderType = Tile.ColliderType.None;
                tile.flags = TileFlags.None;
                tiles[rows] = tile;
            }
        }
    }

    /// <summary>
    /// 테두리 진하기를 _step 단계로 바꾸도록 모은다(0이면 테두리 타일을 걷는다). 칸 높이가 바뀌었으면(_bMoved) 진하기가 그대로여도
    /// 테두리를 새 높이의 사본으로 다시 깐다. 진하기는 칸 색(알파)에 담는다.
    /// </summary>
    private void QueueOutline(int _order, sbyte _step, bool _bMoved)
    {
        sbyte last = lastOutlineSteps[_order];
        if (_step == last && (false == _bMoved || 0 == _step)) return;

        lastOutlineSteps[_order] = _step;
        Vector3Int cell = cells[_order].cell;
        if (_step > 0)
        {
            Color color = settings.outlineColor;
            color.a *= _step / (float)OutlineSteps;
            outlineChanges.Add(new TileChangeData(cell, OffsetTile(OutlineTile, new Vector2Int(0, lastPixels[_order])), color, Matrix4x4.identity));
        }
        else if (last > 0)
        {
            outlineChanges.Add(new TileChangeData(cell, null, Color.white, Matrix4x4.identity));
        }
    }

    /// <summary>
    /// _source를 _offsetPixels만큼 옮겨 그리는 사본 타일(피벗만 옮긴 같은 그림, 칸 색을 바꿀 수 있게 잠금 없음).
    /// 0이면 원래 타일, 그림 하나짜리 타일(Tile)이 아니면 옮기지 못하므로 원래 타일을 그대로 쓴다.
    /// </summary>
    internal static TileBase OffsetTile(TileBase _source, Vector2Int _offsetPixels)
    {
        if (null == _source || Vector2Int.zero == _offsetPixels) return _source;
        if (false == _source is Tile tile || null == tile.sprite) return _source;

        int x = Mathf.Clamp(_offsetPixels.x, -MaxOffsetPixels, MaxOffsetPixels);
        int y = Mathf.Clamp(_offsetPixels.y, -MaxOffsetPixels, MaxOffsetPixels);
        int key = (x + MaxOffsetPixels) * (2 * MaxOffsetPixels + 1) + (y + MaxOffsetPixels);

        if (false == offsetTiles.TryGetValue(_source, out Dictionary<int, TileBase> variants))
        {
            variants = new Dictionary<int, TileBase>(32);
            offsetTiles.Add(_source, variants);
        }

        if (variants.TryGetValue(key, out TileBase variant)) return variant;

        Sprite sprite = tile.sprite;
        Rect rect = sprite.rect;
        Vector2 pivot = sprite.pivot;
        float ppu = sprite.pixelsPerUnit;
        // 그림을 위로 y픽셀 올리려면 피벗(그림 안의 기준점)을 그만큼 아래로 내린다. 오프셋은 화면 픽셀(32 PPU) 기준이다.
        Vector2 shifted = new Vector2(pivot.x - x * ppu / PixelsPerUnit, pivot.y - y * ppu / PixelsPerUnit);

        int secondaryCount = sprite.GetSecondaryTextureCount();
        SecondarySpriteTexture[] secondary = new SecondarySpriteTexture[secondaryCount];
        if (secondaryCount > 0) sprite.GetSecondaryTextures(secondary);

        Sprite moved = Sprite.Create(sprite.texture, rect, new Vector2(shifted.x / rect.width, shifted.y / rect.height), ppu, 0,
            SpriteMeshType.FullRect, sprite.border, false, secondary);
        moved.name = sprite.name + "_Quake" + x + "_" + y;

        Tile copy = ScriptableObject.CreateInstance<Tile>();
        copy.name = moved.name;
        copy.sprite = moved;
        copy.color = tile.color;
        copy.transform = tile.transform;
        copy.gameObject = null;
        copy.flags = TileFlags.None;
        copy.colliderType = Tile.ColliderType.None;

        variants.Add(key, copy);
        return copy;
    }

    /// <summary>
    /// 칸 윗면(마름모) 테두리 타일. 바닥 블록 그림(32x32: 위 16줄이 윗면 마름모, 아래는 옆면)과 같은 틀로 만든다.
    /// 마름모 각 줄의 양 끝 2픽셀(2:1 아이소 선)과 맨 위·아래 줄이 테두리다. 이웃 칸의 테두리와 같은 픽셀에 겹쳐,
    /// 칸들이 제자리에 있으면 한 줄로 보이고 높이가 어긋나면 칸마다 따로 보인다.
    /// </summary>
    internal static Tile OutlineTile
    {
        get
        {
            if (null == outlineTile) outlineTile = CreateFaceTile("QuakeRippleOutline", 0);
            return outlineTile;
        }
    }

    /// <summary>
    /// 칸 윗면(마름모) 타일을 만든다. 테두리는 불투명 흰색, 안쪽은 _fillAlpha(0이면 비움) 흰색이다. 칸 색으로 물들인다.
    /// 지진 물결 테두리(OutlineTile)는 안쪽이 빈 것이고, 로그라이크 파도는 마루를 밝히는 데 안쪽을 채운 것을 쓴다.
    /// </summary>
    internal static Tile CreateFaceTile(string _name, byte _fillAlpha)
    {
        const int size = 32;
        const int faceRows = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = _name;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color32[] pixels = new Color32[size * size];
        Color32 rim = new Color32(255, 255, 255, 255);
        Color32 fill = new Color32(255, 255, 255, _fillAlpha);
        for (int row = 0; row < faceRows; row++)
        {
            // 윗면 마름모의 그 줄 폭: 위에서부터 4, 8, ... 32, 32, ... 4픽셀.
            int fromEdge = row < faceRows / 2 ? row : faceRows - 1 - row;
            int left = size / 2 - 2 - 2 * fromEdge;
            int right = size / 2 + 1 + 2 * fromEdge;
            bool bCapRow = 0 == row || faceRows - 1 == row;

            // 텍스처는 아래가 0번 줄이다.
            int y = size - 1 - row;
            for (int x = left; x <= right; x++)
            {
                if (bCapRow || x <= left + 1 || x >= right - 1) pixels[x + y * size] = rim;
                else if (_fillAlpha > 0) pixels[x + y * size] = fill;
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);

        Tile tile = ScriptableObject.CreateInstance<Tile>();
        tile.name = _name;
        tile.sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        tile.colliderType = Tile.ColliderType.None;
        tile.flags = TileFlags.None;
        return tile;
    }
}
