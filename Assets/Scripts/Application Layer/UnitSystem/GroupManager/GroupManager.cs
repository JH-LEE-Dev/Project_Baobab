using System.Collections.Generic;
using UnityEngine;

public class GroupManager : MonoBehaviour
{
    //외부 의존성
    private IEnvironmentProvider environmentProvider;

    //내부 의존성
    private IReadOnlyList<AnimalGroup> animalGroups;
    private List<Vector3Int> tempReservedCells = new List<Vector3Int>(16);

    //설정
    [SerializeField] private float minMoveInterval = 5f;
    [SerializeField] private float maxMoveInterval = 10f;
    [SerializeField] private int scatterRadius = 3;

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
    }

    public void SetAnimalGroup(IReadOnlyList<AnimalGroup> _animalGroups)
    {
        animalGroups = _animalGroups;

        for (int i = 0; i < animalGroups.Count; i++)
        {
            animalGroups[i].moveTimer = Random.Range(minMoveInterval, maxMoveInterval);
        }
    }

    public void Update()
    {
        if (animalGroups == null) return;

        float deltaTime = Time.deltaTime;
        for (int i = 0; i < animalGroups.Count; i++)
        {
            AnimalGroup group = animalGroups[i];
            group.moveTimer -= deltaTime;

            if (group.moveTimer <= 0f)
            {
                MoveGroupToRandomPoint(group);
                group.moveTimer = Random.Range(minMoveInterval, maxMoveInterval);
            }
        }
    }

    private void MoveGroupToRandomPoint(AnimalGroup _group)
    {
        if (_group.members.Count == 0) return;

        // 1. 전체 이동 가능한 타일 중 하나를 특정 지점으로 선택
        List<Vector3> walkablePositions = environmentProvider.tilemapDataProvider.GetWalkableTileWorldPositions();
        if (walkablePositions == null || walkablePositions.Count == 0) return;

        Vector3 targetPoint = walkablePositions[Random.Range(0, walkablePositions.Count)];
        Vector3Int targetCell = environmentProvider.tilemapDataProvider.WorldToCell(targetPoint);

        // 2. 그룹 멤버들을 특정 지점 주변으로 분산 이동
        tempReservedCells.Clear();

        for (int i = 0; i < _group.members.Count; i++)
        {
            Animal animal = _group.members[i];
            Vector3Int bestCell = FindNearbyAvailableTile(targetCell, scatterRadius, tempReservedCells);
            
            tempReservedCells.Add(bestCell);
            animal.MoveTo(environmentProvider.tilemapDataProvider.CellToWorld(bestCell),targetCell,scatterRadius);
        }
    }

    private Vector3Int FindNearbyAvailableTile(Vector3Int _centerCell, int _maxRadius, List<Vector3Int> _alreadyReserved)
    {
        // 0부터 _maxRadius까지 확장하며 검색 (나선형과 유사하게 사각형 레이어별로 검색)
        for (int r = 0; r <= _maxRadius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    // 가장자리 타일만 검사 (이미 안쪽은 이전 r에서 검사됨)
                    if (r > 0 && Mathf.Abs(x) != r && Mathf.Abs(y) != r) continue;

                    Vector3Int candidate = new Vector3Int(_centerCell.x + x, _centerCell.y + y, 0);

                    // 1. 걸을 수 있는가?
                    if (!environmentProvider.tilemapDataProvider.IsWalkable(candidate)) continue;

                    // 2. 현재 다른 오브젝트에 의해 점유 중인가?
                    if (environmentProvider.pathfindGridProvider.IsOccupied(candidate)) continue;

                    // 3. 이 프레임에서 이 그룹의 다른 멤버가 이미 예약했는가?
                    bool alreadyTaken = false;
                    for (int i = 0; i < _alreadyReserved.Count; i++)
                    {
                        if (_alreadyReserved[i] == candidate)
                        {
                            alreadyTaken = true;
                            break;
                        }
                    }

                    if (!alreadyTaken) return candidate;
                }
            }
        }

        // 못 찾으면 약간의 오프셋을 주어 반환 (최후의 수단)
        return _centerCell;
    }
}
