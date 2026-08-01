using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TownTileManager : MonoBehaviour
{
    // 제재소 증설로 추가되는 건물 충돌 타일맵의 이름 규칙.
    // BuildingColliderTilemap_1 = 2번째 가공 라인, BuildingColliderTilemap_2 = 3번째 가공 라인.
    private const string BuildingColliderExpansionPrefix = "BuildingColliderTilemap_";

    [SerializeField] private GameObject tilemapPrefab;

    private GameObject currentGrid;

    // 이름 뒤 숫자(_1, _2) 순서로 정렬해서 담는다. 증설 단계 N이면 앞에서 N개만 활성화한다.
    private readonly List<Tilemap> buildingColliderExpansions = new List<Tilemap>(2);

    // Grid는 던전 진입 시 파괴되고 마을 복귀 시 새로 생성되므로, 증설 단계는 매니저 쪽에 남겨두고
    // CreateGrid()마다 다시 적용한다. (증설이 던전 안에서 해금되는 경우도 이 값으로 보관된다)
    private int buildingExpansionCount = 0;

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

    /// <summary>
    /// 제재소 증설분 건물 충돌 타일맵들. 아직 증설되지 않은 것은 GameObject가 꺼져 있으므로,
    /// 길찾기 등에서 조회할 때는 반드시 활성 여부를 함께 확인해야 한다.
    /// </summary>
    public IReadOnlyList<Tilemap> BuildingColliderExpansionTilemaps => buildingColliderExpansions;

    public void Initialize()
    {

    }

    /// <summary>
    /// 던전 진입 등으로 마을 Grid가 더 이상 필요 없을 때 파괴합니다. 마을로 돌아오면
    /// CreateGrid()가 다시 새로 만들어줍니다.
    /// </summary>
    public void DestroyGrid()
    {
        if (currentGrid != null)
        {
            Destroy(currentGrid);
            currentGrid = null;
        }

        GroundTilemap = null;
        WaterTilemap = null;
        WaterCornerTilemap = null;
        DecoTilemap = null;
        ColliderTilemap = null;
        BuildingColliderTilemap = null;
        WaterColliderTilemap = null;
        RockColliderTilemap = null;
        WaterStencilTilemap = null;
        GroundStencilTilemap = null;

        // buildingExpansionCount는 유지한다(다음 CreateGrid에서 같은 단계로 복원하기 위함).
        buildingColliderExpansions.Clear();
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

        buildingColliderExpansions.Clear();

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
                default:
                    if (tilemap.gameObject.name.StartsWith(BuildingColliderExpansionPrefix))
                        buildingColliderExpansions.Add(tilemap);
                    break;
            }
        }

        // "_1", "_2" 접미사 순서를 하이라키 배치 순서에 의존하지 않도록 이름으로 정렬한다.
        buildingColliderExpansions.Sort((a, b) => string.CompareOrdinal(a.gameObject.name, b.gameObject.name));

        ApplyBuildingExpansion();
    }

    /// <summary>
    /// 제재소 증설 단계를 반영해 추가 건물 충돌 타일맵을 켜고 끕니다.
    /// _count는 "기본 건물 외에 추가로 지어진 동 수"(가공 라인 수 - 1)입니다.
    /// Grid가 아직 없을 때(던전 중) 호출되면 값만 기억해두고 CreateGrid()에서 적용합니다.
    /// </summary>
    public void SetBuildingExpansionCount(int _count)
    {
        buildingExpansionCount = Mathf.Max(0, _count);
        ApplyBuildingExpansion();
    }

    private void ApplyBuildingExpansion()
    {
        for (int i = 0; i < buildingColliderExpansions.Count; i++)
        {
            Tilemap expansion = buildingColliderExpansions[i];
            if (expansion == null) continue;

            bool bActive = i < buildingExpansionCount;
            if (expansion.gameObject.activeSelf != bActive)
                expansion.gameObject.SetActive(bActive);
        }
    }
}
