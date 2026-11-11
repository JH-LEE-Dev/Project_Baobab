using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "StageTileData", menuName = "ScriptableObjects/StageTileData", order = 1)]
public class StageTileDataSO : ScriptableObject
{
    // //외부 의존성
    // (없음)

    // //내부 의존성
    [Header("물 타일")]
    [SerializeField] private TileBase waterTile;
    [SerializeField] private TileBase waterTileBorderRU;
    [SerializeField] private TileBase waterTileBorderRD;
    [SerializeField] private TileBase waterTileBorderLU;
    [SerializeField] private TileBase waterTileBorderLD;
    [SerializeField] private TileBase waterTileBorderRURD;
    [SerializeField] private TileBase waterTileBorderRULU;
    [SerializeField] private TileBase waterTileBorderRULD;
    [SerializeField] private TileBase waterTileBorderRDLU;
    [SerializeField] private TileBase waterTileBorderRDLD;
    [SerializeField] private TileBase waterTileBorderLULD;
    [SerializeField] private TileBase waterTileBorderRURDLU;
    [SerializeField] private TileBase waterTileBorderRURDLD;
    [SerializeField] private TileBase waterTileBorderRULULD;
    [SerializeField] private TileBase waterTileBorderRDLULD;
    [SerializeField] private TileBase waterTileBorderAll;

    [Header("물 코너 타일")]
    [SerializeField] private TileBase waterTileCornerU;
    [SerializeField] private TileBase waterTileCornerR;
    [SerializeField] private TileBase waterTileCornerD;
    [SerializeField] private TileBase waterTileCornerL;
    [SerializeField] private TileBase waterTileCornerUR;
    [SerializeField] private TileBase waterTileCornerUD;
    [SerializeField] private TileBase waterTileCornerUL;
    [SerializeField] private TileBase waterTileCornerRD;
    [SerializeField] private TileBase waterTileCornerRL;
    [SerializeField] private TileBase waterTileCornerDL;
    [SerializeField] private TileBase waterTileCornerURD;
    [SerializeField] private TileBase waterTileCornerURL;
    [SerializeField] private TileBase waterTileCornerUDL;
    [SerializeField] private TileBase waterTileCornerRDL;
    [SerializeField] private TileBase waterTileCornerAll;

    [Header("기타 타일 에셋")]
    [SerializeField] private List<TileBase> sandTiles;
    [SerializeField] private List<TileBase> grassTiles;
    [SerializeField] private List<TileBase> mountainTiles;
    [SerializeField] private TileBase treeCollisionTile;
    [SerializeField] private List<TileBase> grassDecoTiles;
    [SerializeField] private List<TileBase> groundDecoTiles;
    [SerializeField] private List<TileBase> bloomGrassDecoTiles;
    [SerializeField] private List<TileBase> bloomGroundDecoTiles;
    [SerializeField] private List<TileBase> waterDecoTiles;
    [SerializeField] private List<TileBase> bloomWaterDecoTiles;
    [SerializeField] private List<TileBase> insectDecoTiles;
    [SerializeField] private TileBase stencilTile;
    [SerializeField] private TileBase groundStencilTile;
    [SerializeField] private List<AnimatedObj> animatedObjPrefabs;
    [SerializeField] private List<DecoSpritePatternAnimator> waterAnimatedObjPrefabs;
    // WaterAnimatedObjPrefabs(DecoSpritePatternAnimator)와 별개로, AnimatedObj 타입 프리팹을 물 위에 스폰하기 위한 목록.
    [SerializeField] private List<AnimatedObj> waterAnimatedOtherTypeObjPrefabs;
    [SerializeField] [FormerlySerializedAs("staticObjPrefabs")] private List<StaticObj> grassStaticObjPrefabs;
    [SerializeField] [FormerlySerializedAs("staticObjPrefabs")] private List<StaticObj> sandStaticObjPrefabs;
    [SerializeField] private List<StaticObj> shorelineStaticObjPrefabs;
    [SerializeField] private List<AnimatedObj> shorelineAnimatedObjPrefabs;

    [Header("블룸(Bloom) HDR 강도 설정")]
    [SerializeField] private float bloomDecoHDRIntensity = 1f;
    // 켜면 블룸 데코 그림의 밝은 칸(불빛)에만 HDR 강도를 줘서 그 부분만 은은하게 번지게 한다. 끄면(기본) 그림 전체에 준다.
    [SerializeField] private bool bloomDecoGlowOnly = false;
    // 빛나기 시작하는 밝기·다 빛나는 밝기(텍스처 rgb 중 가장 큰 값, 선형 공간 0~1). bloomDecoGlowOnly를 켰을 때만 쓴다.
    [SerializeField] private Vector2 bloomDecoGlowRange = new Vector2(0.2f, 0.6f);
    [SerializeField] private float bloomWaterDecoHDRIntensity = 1f;
    [Space(10)]
    [SerializeField] private bool useWaterTileBloom = false;
    [SerializeField] private float waterTileHDRIntensity = 1f;

    [Header("데코 타일맵 오프셋 설정")]
    // DecoTilemap 오브젝트의 로컬 Y 위치. Grid 프리팹에 저장된 값은 0.5지만 스테이지마다 타일 아트의
    // 기준선이 달라서, 그대로 두면 데코가 한 칸 떠 보이는 스테이지가 있다(Stage1이 그렇다).
    // Grid는 던전마다 새로 만들지 않고 재사용하므로 GenerateMap()이 던전 진입마다 이 값을 다시 적용한다.
    [SerializeField] private float decoTilemapYOffset = 0.5f;
    // true면 GroundDecoTiles/GrassDecoTiles를 DecoTilemap에 얹지 않고 GroundTilemap의 타일 자체로 깐다.
    // 데코가 그라운드 위에 겹쳐 그려지는 대신 그라운드를 대체한다(Stage1이 이 방식).
    // 블룸 데코(BloomGroundDecoTiles/BloomGrassDecoTiles)는 이 설정과 무관하게 BloomDecoTilemap으로 간다.
    [SerializeField] private bool useGroundTilemapForDeco = false;

    [Header("오브젝트 밀도 설정")]
    [SerializeField, Range(0f, 0.1f)] [FormerlySerializedAs("rockDecoDensity")] private float grassStaticObjDensity = 0.0005f;
    [SerializeField, Range(0f, 0.1f)] [FormerlySerializedAs("rockDecoDensity")] private float sandStaticObjDensity = 0.0005f;
    [SerializeField, Range(0f, 0.1f)] private float shorelineStaticObjDensity = 0.0005f;
    [SerializeField, Range(0f, 0.1f)] private float animatedObjDensity = 0.0025f;
    [SerializeField, Range(0f, 0.1f)] private float shorelineAnimatedObjDensity = 0.0025f;
    // 섬 내부에 고립된 웅덩이(바다와 연결되지 않은 물)에 적용되는 밀도.
    [SerializeField, Range(0f, 1f)] private float waterAnimatedObjDensity = 0.1f;
    [SerializeField, Range(0f, 1f)] private float waterAnimatedOtherTypeObjDensity = 0.1f;
    // 섬을 감싸는 바다(섬과 연결된 물, 그리드 안팎 구분 없이 전부 동일 적용)에 적용되는 밀도.
    [SerializeField, Range(0f, 1f)] private float waterAnimatedObjSeaDensity = 0.01f;
    [SerializeField, Range(0f, 1f)] private float waterAnimatedOtherTypeObjSeaDensity = 0.01f;

    [Header("데코 타일 밀도 설정")]
    [SerializeField, Range(0f, 1f)] private float waterDecoDensity = 1f;
    [SerializeField, Range(0f, 1f)] private float sandDecoDensity = 0.05f;
    [SerializeField, Range(0f, 1f)] private float groundDecoDensity = 0.01f;
    [SerializeField, Range(0f, 1f)] private float grassDecoDensity = 0.35f;

    [Header("블룸(Bloom) 데코 타일 밀도 설정")]
    [SerializeField, Range(0f, 1f)] private float bloomSandDecoDensity = 0.01f;
    [SerializeField, Range(0f, 1f)] private float bloomGroundDecoDensity = 0.002f;
    [SerializeField, Range(0f, 1f)] private float bloomGrassDecoDensity = 0.07f;
    [SerializeField] private bool useBloomWaterDecoDensity = false;
    [SerializeField, Range(0f, 1f)] private float bloomWaterDecoDensity = 0.1f;

    [Header("분위기 연출(렌더링 전용)")]
    [SerializeField] private WindGustSettings windGust = new WindGustSettings();
    [SerializeField] private WaterShimmerSettings waterShimmer = new WaterShimmerSettings();
    [SerializeField] private StageColorGradingSettings colorGrading = new StageColorGradingSettings();
    [SerializeField] private ShoreReedSettings shoreReeds = new ShoreReedSettings();
    [SerializeField] private SporeHazeSettings sporeHaze = new SporeHazeSettings();
    [SerializeField] private SporeGlowSettings sporeGlow = new SporeGlowSettings();
    [SerializeField] private WaterSporeSettings waterSpores = new WaterSporeSettings();
    // 물(용암) 위 거품
    [SerializeField] private WaterBubbleSettings waterBubbles = new WaterBubbleSettings();
    [SerializeField] private MushroomSporeSettings mushroomSpores = new MushroomSporeSettings();
    [SerializeField] private BloomDecoTwinkleSettings bloomDecoTwinkle = new BloomDecoTwinkleSettings();
    [SerializeField] private TreeStarTwinkleSettings treeStarTwinkle = new TreeStarTwinkleSettings();
    [SerializeField] private DecoBreathSettings decoBreath = new DecoBreathSettings();
    [SerializeField] private HitSparkleSettings hitSparkles = new HitSparkleSettings();
    // 용암 아지랑이(LavaHeatHaze 머티리얼). 포자 안개와 같은 방식으로 카메라를 따라다니는 스프라이트 한 장에 그린다.
    // 화면 캡처 뒤에 그려야 하므로 정렬 자리는 Objects 레이어보다 뒤로 둔다.
    [SerializeField] private SporeHazeSettings heatHaze = new SporeHazeSettings();
    // 용암 불씨(물 타일맵 칸에서 튀어 오르는 알갱이)
    [SerializeField] private LavaEmberSettings lavaEmbers = new LavaEmberSettings();
    // 용암 빛 번짐(용암 둘레 바닥이 주황으로 물듦)
    [SerializeField] private LavaGlowSettings lavaGlow = new LavaGlowSettings();
    // 용암 온도 무늬(용암 안쪽에 식은 곳·뜨거운 곳)
    [SerializeField] private WaterMagmaSettings waterMagma = new WaterMagmaSettings();
    // 켜면 물(용암) 위에 겹쳐 그리던 나무·캐릭터 반사(OnWater 머티리얼)를 그리지 않는다
    [SerializeField] private bool hideWaterReflections = false;
    // 용암 위에 나무 그림자를 그리지 않는다(물·육지 스텐실 머티리얼을 바꿔 끼운다)
    [SerializeField] private LavaShadowMaskSettings lavaShadowMask = new LavaShadowMaskSettings();
    // 용암 기둥(용암 칸에서 가끔 작은 알갱이 기둥이 솟는다)
    [SerializeField] private LavaSpoutSettings lavaSpouts = new LavaSpoutSettings();
    // 데코 증기(블룸 데코에서 가끔 옅은 증기가 피어오른다)
    [SerializeField] private DecoSteamSettings decoSteam = new DecoSteamSettings();
    // 불씨 데코 숨쉬기(블룸 데코 타일맵을 숨쉬기 머티리얼로 그린다)
    [SerializeField] private BloomDecoBreathSettings bloomDecoBreath = new BloomDecoBreathSettings();
    // 나무 열기 차오름(열기를 내뿜기 전까지 나무 줄기가 점점 달아오른다)
    [SerializeField] private TreeHeatChargeSettings treeHeatCharge = new TreeHeatChargeSettings();
    // 물(용암)·물가·스텐실 타일 애니메이션을 타일맵 대신 셰이더로 돌린다(StageTileFrameAnimation). 보이는 그림·박자는 같고,
    // 타일맵이 매 프레임 화면 밖까지 모든 애니메이션 타일을 훑던 비용이 없어진다. 조건에 안 맞는 타일이 있으면 저절로 원래대로 깐다.
    [SerializeField] private bool shaderTileAnimation = false;

    [Header("위험 지형 설정")]
    [SerializeField] private float waterHazardStaminaDrainPerSecond = 0f;
    [SerializeField] private float treeHeatStaminaDamage = 0f;

    // // 퍼블릭 초기화 및 제어 메서드
    public float GrassStaticObjDensity => grassStaticObjDensity;
    public float SandStaticObjDensity => sandStaticObjDensity;
    public float ShorelineStaticObjDensity => shorelineStaticObjDensity;
    public float AnimatedObjDensity => animatedObjDensity;
    public float ShorelineAnimatedObjDensity => shorelineAnimatedObjDensity;
    public float WaterAnimatedObjDensity => waterAnimatedObjDensity;
    public float WaterAnimatedOtherTypeObjDensity => waterAnimatedOtherTypeObjDensity;
    public float WaterAnimatedObjSeaDensity => waterAnimatedObjSeaDensity;
    public float WaterAnimatedOtherTypeObjSeaDensity => waterAnimatedOtherTypeObjSeaDensity;
    public float WaterDecoDensity => waterDecoDensity;
    public float SandDecoDensity => sandDecoDensity;
    public float GroundDecoDensity => groundDecoDensity;
    public float GrassDecoDensity => grassDecoDensity;
    public float BloomSandDecoDensity => bloomSandDecoDensity;
    public float BloomGroundDecoDensity => bloomGroundDecoDensity;
    public float BloomGrassDecoDensity => bloomGrassDecoDensity;
    public bool UseBloomWaterDecoDensity => useBloomWaterDecoDensity;
    public float BloomWaterDecoDensity => bloomWaterDecoDensity;
    public float DecoTilemapYOffset => decoTilemapYOffset;
    public bool UseGroundTilemapForDeco => useGroundTilemapForDeco;
    public float BloomDecoHDRIntensity => bloomDecoHDRIntensity;
    public bool BloomDecoGlowOnly => bloomDecoGlowOnly;
    public Vector2 BloomDecoGlowRange => bloomDecoGlowRange;
    public float BloomWaterDecoHDRIntensity => bloomWaterDecoHDRIntensity;
    public bool UseWaterTileBloom => useWaterTileBloom;
    public float WaterTileHDRIntensity => waterTileHDRIntensity;
    public float WaterHazardStaminaDrainPerSecond => waterHazardStaminaDrainPerSecond;
    public float TreeHeatStaminaDamage => treeHeatStaminaDamage;
    public WindGustSettings WindGust => windGust;
    public WaterShimmerSettings WaterShimmer => waterShimmer;
    public StageColorGradingSettings ColorGrading => colorGrading;
    public ShoreReedSettings ShoreReeds => shoreReeds;
    public SporeHazeSettings SporeHaze => sporeHaze;
    public SporeGlowSettings SporeGlow => sporeGlow;
    public WaterSporeSettings WaterSpores => waterSpores;
    public WaterBubbleSettings WaterBubbles => waterBubbles;
    public MushroomSporeSettings MushroomSpores => mushroomSpores;
    public BloomDecoTwinkleSettings BloomDecoTwinkle => bloomDecoTwinkle;
    public TreeStarTwinkleSettings TreeStarTwinkle => treeStarTwinkle;
    public DecoBreathSettings DecoBreath => decoBreath;
    public HitSparkleSettings HitSparkles => hitSparkles;
    public SporeHazeSettings HeatHaze => heatHaze;
    public LavaEmberSettings LavaEmbers => lavaEmbers;
    public LavaGlowSettings LavaGlow => lavaGlow;
    public WaterMagmaSettings WaterMagma => waterMagma;
    public bool HideWaterReflections => hideWaterReflections;
    public LavaShadowMaskSettings LavaShadowMask => lavaShadowMask;
    public LavaSpoutSettings LavaSpouts => lavaSpouts;
    public DecoSteamSettings DecoSteam => decoSteam;
    public BloomDecoBreathSettings BloomDecoBreath => bloomDecoBreath;
    public TreeHeatChargeSettings TreeHeatCharge => treeHeatCharge;
    public bool ShaderTileAnimation => shaderTileAnimation;
    public List<AnimatedObj> AnimatedObjPrefabs => animatedObjPrefabs;
    public List<DecoSpritePatternAnimator> WaterAnimatedObjPrefabs => waterAnimatedObjPrefabs;
    public List<AnimatedObj> WaterAnimatedOtherTypeObjPrefabs => waterAnimatedOtherTypeObjPrefabs;
    public List<StaticObj> GrassStaticObjPrefabs => grassStaticObjPrefabs;
    public List<StaticObj> SandStaticObjPrefabs => sandStaticObjPrefabs;
    public List<StaticObj> ShorelineStaticObjPrefabs => shorelineStaticObjPrefabs;
    public List<AnimatedObj> ShorelineAnimatedObjPrefabs => shorelineAnimatedObjPrefabs;
    public TileBase WaterTile => waterTile;
    public TileBase WaterTileBorderRU => waterTileBorderRU;
    public TileBase WaterTileBorderRD => waterTileBorderRD;
    public TileBase WaterTileBorderLU => waterTileBorderLU;
    public TileBase WaterTileBorderLD => waterTileBorderLD;
    public TileBase WaterTileBorderRURD => waterTileBorderRURD;
    public TileBase WaterTileBorderRULU => waterTileBorderRULU;
    public TileBase WaterTileBorderRULD => waterTileBorderRULD;
    public TileBase WaterTileBorderRDLU => waterTileBorderRDLU;
    public TileBase WaterTileBorderRDLD => waterTileBorderRDLD;
    public TileBase WaterTileBorderLULD => waterTileBorderLULD;
    public TileBase WaterTileBorderRURDLU => waterTileBorderRURDLU;
    public TileBase WaterTileBorderRURDLD => waterTileBorderRURDLD;
    public TileBase WaterTileBorderRULULD => waterTileBorderRULULD;
    public TileBase WaterTileBorderRDLULD => waterTileBorderRDLULD;
    public TileBase WaterTileBorderAll => waterTileBorderAll;

    public TileBase WaterTileCornerU => waterTileCornerU;
    public TileBase WaterTileCornerR => waterTileCornerR;
    public TileBase WaterTileCornerD => waterTileCornerD;
    public TileBase WaterTileCornerL => waterTileCornerL;
    public TileBase WaterTileCornerUR => waterTileCornerUR;
    public TileBase WaterTileCornerUD => waterTileCornerUD;
    public TileBase WaterTileCornerUL => waterTileCornerUL;
    public TileBase WaterTileCornerRD => waterTileCornerRD;
    public TileBase WaterTileCornerRL => waterTileCornerRL;
    public TileBase WaterTileCornerDL => waterTileCornerDL;
    public TileBase WaterTileCornerURD => waterTileCornerURD;
    public TileBase WaterTileCornerURL => waterTileCornerURL;
    public TileBase WaterTileCornerUDL => waterTileCornerUDL;
    public TileBase WaterTileCornerRDL => waterTileCornerRDL;
    public TileBase WaterTileCornerAll => waterTileCornerAll;

    public List<TileBase> SandTiles => sandTiles;
    public List<TileBase> GrassTiles => grassTiles;
    public List<TileBase> MountainTiles => mountainTiles;
    public TileBase TreeCollisionTile => treeCollisionTile;
    public List<TileBase> GrassDecoTiles => grassDecoTiles;
    public List<TileBase> BloomGrassDecoTiles => bloomGrassDecoTiles;
    public List<TileBase> GroundDecoTiles => groundDecoTiles;
    public List<TileBase> BloomGroundDecoTiles => bloomGroundDecoTiles;
    public List<TileBase> WaterDecoTiles => waterDecoTiles;
    public List<TileBase> BloomWaterDecoTiles => bloomWaterDecoTiles;
    public List<TileBase> InsectDecoTiles => insectDecoTiles;
    public TileBase StencilTile => stencilTile;
    public TileBase GroundStencilTile => groundStencilTile;
}
