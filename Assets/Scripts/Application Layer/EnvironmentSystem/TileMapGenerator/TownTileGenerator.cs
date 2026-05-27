using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class TownTileGenerator : MonoBehaviour
{
    // //외부 의존성
    [Header("타일맵 컴포넌트")]
    [SerializeField] private Tilemap groundTilemap;
    [SerializeField] private Tilemap waterTilemap;
    [SerializeField] private Tilemap waterCornerTilemap;
    [SerializeField] private Tilemap waterCollisionTilemap;
    [SerializeField] private Tilemap waterStencilTilemap;
    [SerializeField] private Tilemap groundStencilTilemap;
    [SerializeField] private Tilemap collisionTilemap;

    [Header("물 타일 에셋")]
    [SerializeField] private TileBase waterTile;
    [SerializeField] private TileBase waterTile_BorderRU;
    [SerializeField] private TileBase waterTile_BorderRD;
    [SerializeField] private TileBase waterTile_BorderLU;
    [SerializeField] private TileBase waterTile_BorderLD;
    [SerializeField] private TileBase waterTile_BorderRU_RD;
    [SerializeField] private TileBase waterTile_BorderRU_LU;
    [SerializeField] private TileBase waterTile_BorderRU_LD;
    [SerializeField] private TileBase waterTile_BorderRD_LU;
    [SerializeField] private TileBase waterTile_BorderRD_LD;
    [SerializeField] private TileBase waterTile_BorderLU_LD;
    [SerializeField] private TileBase waterTile_BorderRU_RD_LU;
    [SerializeField] private TileBase waterTile_BorderRU_RD_LD;
    [SerializeField] private TileBase waterTile_BorderRU_LU_LD;
    [SerializeField] private TileBase waterTile_BorderRD_LU_LD;
    [SerializeField] private TileBase waterTile_BorderAll;

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

    [Header("스텐실 및 콜라이더")]
    [SerializeField] private TileBase stencilTile;
    [SerializeField] private TileBase groundStencilTile;
    [SerializeField] private TileBase treeCollisionTile;

    [Header("생성 설정")]
    [SerializeField] private int waterDistanceLimit = 20;

    // //내부 의존성
    private readonly Vector3Int[] directions = new Vector3Int[]
    {
        new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0),
        new Vector3Int(0, -1, 0)
    };

    // //퍼블릭 초기화 및 제어 메서드

    public void GenerateTownWater()
    {
        if (groundTilemap == null)
        {
            Debug.LogError("GroundTilemap이 할당되지 않았습니다.");
            return;
        }

        ClearWaterTiles();

        BoundsInt bounds = groundTilemap.cellBounds;
        int approxSize = bounds.size.x * bounds.size.y;
        if (approxSize <= 0)
        {
            approxSize = 1000;
        }

        Queue<Vector3Int> queue = new Queue<Vector3Int>(approxSize);
        Dictionary<Vector3Int, int> distanceMap = new Dictionary<Vector3Int, int>(approxSize);
        List<Vector3Int> landPositions = new List<Vector3Int>(approxSize);

        for (int x = bounds.xMin; x < bounds.xMax; x++)
        {
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, 0);
                if (groundTilemap.HasTile(pos))
                {
                    queue.Enqueue(pos);
                    distanceMap[pos] = 0;
                    landPositions.Add(pos);
                }
            }
        }

        List<Vector3Int> waterPositions = new List<Vector3Int>(approxSize);

        while (queue.Count > 0)
        {
            Vector3Int current = queue.Dequeue();
            int currentDist = distanceMap[current];

            if (currentDist >= waterDistanceLimit)
            {
                continue;
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3Int neighbor = current + directions[i];
                if (!distanceMap.ContainsKey(neighbor))
                {
                    if (!groundTilemap.HasTile(neighbor))
                    {
                        distanceMap[neighbor] = currentDist + 1;
                        queue.Enqueue(neighbor);
                        waterPositions.Add(neighbor);
                    }
                }
            }
        }

        for (int i = 0; i < waterPositions.Count; i++)
        {
            Vector3Int pos = waterPositions[i];

            if (waterTilemap != null)
            {
                waterTilemap.SetTile(pos, GetWaterTile(pos));
            }
            if (waterCornerTilemap != null)
            {
                waterCornerTilemap.SetTile(pos, GetWaterCornerTile(pos));
            }
            if (waterCollisionTilemap != null)
            {
                waterCollisionTilemap.SetTile(pos, treeCollisionTile);
            }
            if (waterStencilTilemap != null)
            {
                waterStencilTilemap.SetTile(pos, stencilTile);
            }
        }

        for (int i = 0; i < landPositions.Count; i++)
        {
            Vector3Int pos = landPositions[i];
            bool isShoreline = false;

            for (int j = 0; j < 4; j++)
            {
                Vector3Int neighbor = pos + directions[j];
                if (IsWater(neighbor))
                {
                    isShoreline = true;
                    break;
                }
            }

            if (isShoreline && groundStencilTilemap != null)
            {
                groundStencilTilemap.SetTile(pos, groundStencilTile);
            }
        }

        TreeObj[] trees = FindObjectsByType<TreeObj>(FindObjectsInactive.Include);
        if (trees != null && trees.Length > 0 && collisionTilemap != null)
        {
            float halfCellY = collisionTilemap.layoutGrid != null ? collisionTilemap.layoutGrid.cellSize.y * 0.5f : 0f;

            for (int idx = 0; idx < trees.Length; idx++)
            {
                if (trees[idx] != null)
                {
                    Vector3 spawnPos = trees[idx].transform.position;
                    Vector3 adjustedPos = spawnPos;
                    adjustedPos.y -= halfCellY;

                    Vector3Int cellPos = collisionTilemap.WorldToCell(adjustedPos);

                    collisionTilemap.SetTile(cellPos, treeCollisionTile);

                    bool isWaterNearby = false;
                    if (waterTilemap != null)
                    {
                        isWaterNearby = waterTilemap.HasTile(cellPos + new Vector3Int(-1, -1, 0)) ||
                                       waterTilemap.HasTile(cellPos + new Vector3Int(-2, -2, 0));
                    }
                    trees[idx].SetOnWaterObjectState(isWaterNearby);
                }
            }
        }

        Debug.Log($"물 타일 생성 완료! 생성된 물 타일 수: {waterPositions.Count}");
    }

    public void ClearWaterTiles()
    {
        if (waterTilemap != null) waterTilemap.ClearAllTiles();
        if (waterCornerTilemap != null) waterCornerTilemap.ClearAllTiles();
        if (waterCollisionTilemap != null) waterCollisionTilemap.ClearAllTiles();
        if (waterStencilTilemap != null) waterStencilTilemap.ClearAllTiles();
        if (groundStencilTilemap != null) groundStencilTilemap.ClearAllTiles();

        TreeObj[] trees = FindObjectsByType<TreeObj>(FindObjectsInactive.Include);
        if (trees != null && trees.Length > 0 && collisionTilemap != null)
        {
            float halfCellY = collisionTilemap.layoutGrid != null ? collisionTilemap.layoutGrid.cellSize.y * 0.5f : 0f;
            for (int idx = 0; idx < trees.Length; idx++)
            {
                if (trees[idx] != null)
                {
                    Vector3 adjustedPos = trees[idx].transform.position;
                    adjustedPos.y -= halfCellY;
                    Vector3Int cellPos = collisionTilemap.WorldToCell(adjustedPos);
                    collisionTilemap.SetTile(cellPos, null);

                    trees[idx].SetOnWaterObjectState(false);
                }
            }
        }
    }

    private TileBase GetWaterTile(in Vector3Int _pos)
    {
        int mask = 0;
        if (IsLand(_pos + new Vector3Int(1, 0, 0))) mask |= 1;  // RU
        if (IsLand(_pos + new Vector3Int(0, -1, 0))) mask |= 2;  // RD
        if (IsLand(_pos + new Vector3Int(0, 1, 0))) mask |= 4;  // LU
        if (IsLand(_pos + new Vector3Int(-1, 0, 0))) mask |= 8;  // LD

        switch (mask)
        {
            case 1: return waterTile_BorderRU;
            case 2: return waterTile_BorderRD;
            case 3: return waterTile_BorderRU_RD;
            case 4: return waterTile_BorderLU;
            case 5: return waterTile_BorderRU_LU;
            case 6: return waterTile_BorderRD_LU;
            case 7: return waterTile_BorderRU_RD_LU;
            case 8: return waterTile_BorderLD;
            case 9: return waterTile_BorderRU_LD;
            case 10: return waterTile_BorderRD_LD;
            case 11: return waterTile_BorderRU_RD_LD;
            case 12: return waterTile_BorderLU_LD;
            case 13: return waterTile_BorderRU_LU_LD;
            case 14: return waterTile_BorderRD_LU_LD;
            case 15: return waterTile_BorderAll;
            default: return waterTile;
        }
    }

    private TileBase GetWaterCornerTile(in Vector3Int _pos)
    {
        int cornerMask = 0;
        if (IsLand(_pos + new Vector3Int(1, 1, 0)) && !IsLand(_pos + new Vector3Int(1, 0, 0)) && !IsLand(_pos + new Vector3Int(0, 1, 0))) cornerMask |= 1;  // U
        if (IsLand(_pos + new Vector3Int(1, -1, 0)) && !IsLand(_pos + new Vector3Int(1, 0, 0)) && !IsLand(_pos + new Vector3Int(0, -1, 0))) cornerMask |= 2;  // R
        if (IsLand(_pos + new Vector3Int(-1, -1, 0)) && !IsLand(_pos + new Vector3Int(0, -1, 0)) && !IsLand(_pos + new Vector3Int(-1, 0, 0))) cornerMask |= 4;  // D
        if (IsLand(_pos + new Vector3Int(-1, 1, 0)) && !IsLand(_pos + new Vector3Int(0, 1, 0)) && !IsLand(_pos + new Vector3Int(-1, 0, 0))) cornerMask |= 8;  // L

        switch (cornerMask)
        {
            case 1: return waterTileCornerU;
            case 2: return waterTileCornerR;
            case 3: return waterTileCornerUR;
            case 4: return waterTileCornerD;
            case 5: return waterTileCornerUD;
            case 6: return waterTileCornerRD;
            case 7: return waterTileCornerURD;
            case 8: return waterTileCornerL;
            case 9: return waterTileCornerUL;
            case 10: return waterTileCornerRL;
            case 11: return waterTileCornerURL;
            case 12: return waterTileCornerDL;
            case 13: return waterTileCornerUDL;
            case 14: return waterTileCornerRDL;
            case 15: return waterTileCornerAll;
            default: return null;
        }
    }

    private bool IsLand(in Vector3Int _pos)
    {
        return groundTilemap.HasTile(_pos);
    }

    private bool IsWater(in Vector3Int _pos)
    {
        return !groundTilemap.HasTile(_pos);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(TownTileGenerator))]
public class TownTileGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TownTileGenerator generator = (TownTileGenerator)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate Town Water Tiles", GUILayout.Height(30)))
        {
            generator.GenerateTownWater();
        }

        if (GUILayout.Button("Clear Town Water Tiles", GUILayout.Height(30)))
        {
            generator.ClearWaterTiles();
        }
    }
}
#endif
