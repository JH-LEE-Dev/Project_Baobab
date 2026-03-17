using UnityEngine;
using UnityEditor;
using UnityEngine.Tilemaps;

public class TilemapForestGenerator : EditorWindow
{
    [MenuItem("Tools/Generate Forest Environment")]
    public static void GenerateForest()
    {
        // 1. 그리드 및 타일맵 찾기 (TileMapGenerator 패턴 적용)
        GameObject gridObj = GameObject.Find("Grid");
        if (gridObj == null)
        {
            Debug.LogError("Grid 오브젝트를 찾을 수 없습니다!");
            return;
        }

        Tilemap groundTilemap = null;
        Tilemap colliderTilemap = null;

        Tilemap[] maps = gridObj.GetComponentsInChildren<Tilemap>();
        foreach (var m in maps)
        {
            if (m.name == "GroundTilemap") groundTilemap = m;
            else if (m.name == "ColliderTilemap") colliderTilemap = m;
        }

        if (groundTilemap == null || colliderTilemap == null)
        {
            Debug.LogError("GroundTilemap 또는 ColliderTilemap을 찾을 수 없습니다! 이름을 확인해주세요.");
            return;
        }

        // 2. 에셋 로드
        TileBase waterTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileMap/Tiles/Tile_Water_00.asset");
        TileBase grassTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileMap/Tiles/Tile_Grass.asset");
        TileBase rockTile = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/TileMap/Tiles/Tile_Rock.asset");
        GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Trees/Tree.prefab");

        if (waterTile == null || grassTile == null || rockTile == null || treePrefab == null)
        {
            Debug.LogError("필요한 타일 또는 나무 프리팹 에셋을 찾을 수 없습니다. 경로를 확인해주세요.");
            return;
        }

        // 나무들을 담을 부모 객체 생성
        GameObject treeRoot = GameObject.Find("Trees");
        if (treeRoot == null)
        {
            treeRoot = new GameObject("Trees");
            Undo.RegisterCreatedObjectUndo(treeRoot, "Create Trees Root");
        }

        groundTilemap.CompressBounds();
        BoundsInt bounds = groundTilemap.cellBounds;
        
        // 주변으로 확장할 칸 수
        int offset = 30;
        BoundsInt fillBounds = new BoundsInt(
            bounds.xMin - offset, bounds.yMin - offset, bounds.zMin,
            bounds.size.x + (offset * 2), bounds.size.y + (offset * 2), 1
        );

        // Undo 기록 시작
        Undo.RecordObjects(new Object[] { groundTilemap, colliderTilemap }, "Generate Forest Tiles");

        // Perlin Noise를 위한 랜덤 오프셋 (매번 다른 지형 생성)
        float noiseOffsetX = Random.Range(0f, 1000f);
        float noiseOffsetY = Random.Range(0f, 1000f);
        float terrainScale = 0.1f; // 지형 노이즈 스케일
        
        int tileCount = 0;
        int treeCount = 0;

        for (int x = fillBounds.xMin; x < fillBounds.xMax; x++)
        {
            for (int y = fillBounds.yMin; y < fillBounds.yMax; y++)
            {
                Vector3Int pos = new Vector3Int(x, y, bounds.zMin);
                
                // 1. 지형 타일 배치 (둘 다 비어있는 경우만)
                if (!groundTilemap.HasTile(pos) && !colliderTilemap.HasTile(pos))
                {
                    float noiseValue = Mathf.PerlinNoise((x + noiseOffsetX) * terrainScale, (y + noiseOffsetY) * terrainScale);
                    
                    if (noiseValue < 0.3f) 
                    {
                        // 물 타일은 ColliderTilemap에 배치
                        colliderTilemap.SetTile(pos, waterTile);
                    }
                    else 
                    {
                        // 나머지 지형은 GroundTilemap에 배치
                        TileBase selectedTile = noiseValue > 0.7f ? rockTile : grassTile;
                        groundTilemap.SetTile(pos, selectedTile);
                    }
                    tileCount++;
                }

                // 2. 나무 배치 (현재 타일이 풀 타일인 경우에만)
                if (groundTilemap.GetTile(pos) == grassTile)
                {
                    // 단순 랜덤을 사용하여 30% 밀도로 배치
                    if (Random.value < 0.2f) 
                    {
                        Vector3 worldPos = groundTilemap.GetCellCenterWorld(pos);
                        
                        // 정확히 타일 중앙에서 Y축으로 타일 크기의 절반만큼 오프셋 적용
                        worldPos.y += groundTilemap.cellSize.y * 0.5f;

                        // 나무 인스턴스화 및 Undo 등록
                        GameObject treeInst = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab);
                        Undo.RegisterCreatedObjectUndo(treeInst, "Instantiate Tree");
                        
                        treeInst.transform.position = worldPos;
                        treeInst.transform.parent = treeRoot.transform;

                        // 나무 크기에 변화를 주어 자연스러움 증대
                        float randomScale = Random.Range(0.85f, 1.2f);
                        treeInst.transform.localScale = Vector3.one * randomScale;

                        treeCount++;
                    }
                }
            }
        }

        Debug.Log($"자연 환경 생성 완료: {tileCount}개의 타일 배치, {treeCount}그루의 나무 심기 완료.");
        EditorUtility.SetDirty(groundTilemap);
        EditorUtility.SetDirty(colliderTilemap);
        EditorUtility.SetDirty(treeRoot);
    }
}
