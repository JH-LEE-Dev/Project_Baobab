using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

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
    [SerializeField] private List<StaticObj> staticObjPrefabs;
    [SerializeField] private float bloomDecoHDRIntensity = 1f;

    [Header("오브젝트 밀도 설정")]
    [SerializeField, Range(0f, 0.1f)] private float rockDecoDensity = 0.0005f;
    [SerializeField, Range(0f, 0.1f)] private float animatedObjDensity = 0.0025f;
    [SerializeField, Range(0f, 1f)] private float waterAnimatedObjDensity = 0.1f;

    [Header("데코 타일 밀도 설정")]
    [SerializeField, Range(0f, 1f)] private float waterDecoDensity = 1f;
    [SerializeField, Range(0f, 1f)] private float sandDecoDensity = 0.05f;
    [SerializeField, Range(0f, 1f)] private float groundDecoDensity = 0.01f;
    [SerializeField, Range(0f, 1f)] private float grassDecoDensity = 0.35f;

    [Header("블룸(Bloom) 데코 타일 밀도 설정")]
    [SerializeField, Range(0f, 1f)] private float bloomSandDecoDensity = 0.01f;
    [SerializeField, Range(0f, 1f)] private float bloomGroundDecoDensity = 0.002f;
    [SerializeField, Range(0f, 1f)] private float bloomGrassDecoDensity = 0.07f;

    // // 퍼블릭 초기화 및 제어 메서드
    public float RockDecoDensity => rockDecoDensity;
    public float AnimatedObjDensity => animatedObjDensity;
    public float WaterAnimatedObjDensity => waterAnimatedObjDensity;
    public float WaterDecoDensity => waterDecoDensity;
    public float SandDecoDensity => sandDecoDensity;
    public float GroundDecoDensity => groundDecoDensity;
    public float GrassDecoDensity => grassDecoDensity;
    public float BloomSandDecoDensity => bloomSandDecoDensity;
    public float BloomGroundDecoDensity => bloomGroundDecoDensity;
    public float BloomGrassDecoDensity => bloomGrassDecoDensity;
    public float BloomDecoHDRIntensity => bloomDecoHDRIntensity;
    public List<AnimatedObj> AnimatedObjPrefabs => animatedObjPrefabs;
    public List<DecoSpritePatternAnimator> WaterAnimatedObjPrefabs => waterAnimatedObjPrefabs;
    public List<StaticObj> StaticObjPrefabs => staticObjPrefabs;
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
