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
    [SerializeField] private TileBase sandTile;
    [SerializeField] private TileBase grassTile;
    [SerializeField] private TileBase mountainTile;
    [SerializeField] private TileBase treeCollisionTile;
    [SerializeField] private List<TileBase> grassDecoTiles;
    [SerializeField] private List<TileBase> groundDecoTiles;
    [SerializeField] private List<TileBase> rockDecoTiles;
    [SerializeField] private List<TileBase> waterDecoTiles;
    [SerializeField] private List<TileBase> insectDecoTiles;
    [SerializeField] private TileBase stencilTile;
    [SerializeField] private TileBase groundStencilTile;

    // // 퍼블릭 초기화 및 제어 메서드
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

    public TileBase SandTile => sandTile;
    public TileBase GrassTile => grassTile;
    public TileBase MountainTile => mountainTile;
    public TileBase TreeCollisionTile => treeCollisionTile;
    public List<TileBase> GrassDecoTiles => grassDecoTiles;
    public List<TileBase> GroundDecoTiles => groundDecoTiles;
    public List<TileBase> RockDecoTiles => rockDecoTiles;
    public List<TileBase> WaterDecoTiles => waterDecoTiles;
    public List<TileBase> InsectDecoTiles => insectDecoTiles;
    public TileBase StencilTile => stencilTile;
    public TileBase GroundStencilTile => groundStencilTile;
}
