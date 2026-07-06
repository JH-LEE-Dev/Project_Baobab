using UnityEngine;
using UnityEngine.Tilemaps;

public class TownTileManager : MonoBehaviour
{
    [SerializeField] private GameObject tilemapPrefab;

    private GameObject currentGrid;

    public Tilemap GroundTilemap { get; private set; }
    public Tilemap WaterTilemap { get; private set; }
    public Tilemap WaterCornerTilemap { get; private set; }
    public Tilemap DecoTilemap { get; private set; }
    public Tilemap ColliderTilemap { get; private set; }
    public Tilemap BuildingColliderTilemap { get; private set; }
    public Tilemap WaterColliderTilemap { get; private set; }
    public Tilemap RockColliderTilemap { get; private set; }
    public Tilemap WaterStencilTilemap { get; private set; }
    public Tilemap GroundStencilTilemap { get; private set; }

    public void Initialize()
    {
        
    }

    public void CreateGrid()
    {
        if (tilemapPrefab == null)
        {
            Debug.LogError("Tilemap Prefab is not assigned in TownTileManager.");
            return;
        }

        if (currentGrid != null)
        {
            Destroy(currentGrid);
        }

        // Instantiate the Grid prefab as a child of this manager
        currentGrid = Instantiate(tilemapPrefab, transform);
        
        // Find all Tilemap components in the instantiated prefab and map them
        Tilemap[] tilemaps = currentGrid.GetComponentsInChildren<Tilemap>(true);
        foreach (var tilemap in tilemaps)
        {
            switch (tilemap.gameObject.name)
            {
                case "GroundTilemap": GroundTilemap = tilemap; break;
                case "WaterTilemap": WaterTilemap = tilemap; break;
                case "WaterCornerTilemap": WaterCornerTilemap = tilemap; break;
                case "DecoTilemap": DecoTilemap = tilemap; break;
                case "ColliderTilemap": ColliderTilemap = tilemap; break;
                case "BuildingColliderTilemap": BuildingColliderTilemap = tilemap; break;
                case "WaterColliderTilemap": WaterColliderTilemap = tilemap; break;
                case "RockColliderTilemap": RockColliderTilemap = tilemap; break;
                case "WaterStencilTilemap": WaterStencilTilemap = tilemap; break;
                case "GroundStencilTilemap": GroundStencilTilemap = tilemap; break;
            }
        }
    }
}
