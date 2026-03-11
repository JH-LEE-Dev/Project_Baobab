using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public class TilemapWaterFiller : EditorWindow
{
    [MenuItem("Tools/Fill Water Around")]
    public static void FillWaterAround()
    {
        GameObject tilemapObj = GameObject.Find("Grid/Tilemap");
        if (tilemapObj == null)
        {
            Debug.LogError("Grid/Tilemap not found!");
            return;
        }

        Tilemap tilemap = tilemapObj.GetComponent<Tilemap>();
        if (tilemap == null)
        {
            Debug.LogError("Tilemap component not found on Grid/Tilemap!");
            return;
        }

        TileBase waterTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileMap/Tiles/Tile_Water_00.asset");
        if (waterTile == null)
        {
            Debug.LogError("Water tile not found at Assets/TileMap/Tiles/Tile_Water_00.asset!");
            return;
        }

        tilemap.CompressBounds();
        BoundsInt bounds = tilemap.cellBounds;
        
        // Expand bounds by 10 cells
        int offset = 10;
        BoundsInt fillBounds = new BoundsInt(
            bounds.xMin - offset, bounds.yMin - offset, bounds.zMin,
            bounds.size.x + (offset * 2), bounds.size.y + (offset * 2), bounds.size.z
        );

        int count = 0;
        for (int x = fillBounds.xMin; x < fillBounds.xMax; x++)
        {
            for (int y = fillBounds.yMin; y < fillBounds.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, bounds.zMin);
                if (!tilemap.HasTile(pos))
                {
                    tilemap.SetTile(pos, waterTile);
                    count++;
                }
            }
        }

        Debug.Log($"Filled {count} water tiles around the existing tiles.");
        EditorUtility.SetDirty(tilemapObj);
    }
}
