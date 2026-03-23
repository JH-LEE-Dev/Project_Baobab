using UnityEngine;
using System.Collections.Generic;

public class PathFindComponent : MonoBehaviour
{
    // // 내부 데이터 구조체
    private struct Node
    {
        public Vector3Int cellPos;
        public int gCost;
        public int hCost;
        public Vector3Int parentPos;

        public int fCost => gCost + hCost;

        public Node(Vector3Int _cellPos, int _gCost, int _hCost, Vector3Int _parentPos)
        {
            cellPos = _cellPos;
            gCost = _gCost;
            hCost = _hCost;
            parentPos = _parentPos;
        }
    }

    // // 외부 의존성
    private ITilemapDataProvider tilemapDataProvider;

    // // 재사용 컬렉션 (GC 최소화)
    private Dictionary<Vector3Int, Node> openSet = new Dictionary<Vector3Int, Node>(100);
    private Dictionary<Vector3Int, Node> closedSet = new Dictionary<Vector3Int, Node>(200);
    private List<Vector3Int> neighbors = new List<Vector3Int>(8);
    private List<Vector3> finalPath = new List<Vector3>(50);
    private List<Vector3> candidateTargets = new List<Vector3>(100);

    // // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(ITilemapDataProvider _tilemapDataProvider)
    {
        tilemapDataProvider = _tilemapDataProvider;
    }

    /// <summary>
    /// 특정 거리 이상의 무작위 목표 지점을 반환합니다.
    /// </summary>
    public Vector3 GetRandomTarget(Vector3 _currentPos, float _minDistance)
    {
        if (tilemapDataProvider == null) return _currentPos;

        candidateTargets.Clear();
        List<Vector3> walkablePosList = tilemapDataProvider.GetWalkableTileWorldPositions();
        float minSqrDist = _minDistance * _minDistance;

        for (int i = 0; i < walkablePosList.Count; i++)
        {
            if ((walkablePosList[i] - _currentPos).sqrMagnitude >= minSqrDist)
            {
                candidateTargets.Add(walkablePosList[i]);
            }
        }

        if (candidateTargets.Count > 0)
        {
            return candidateTargets[Random.Range(0, candidateTargets.Count)];
        }

        return _currentPos;
    }

    /// <summary>
    /// 특정 거리 이상의 무작위 지점을 찾아 A* 알고리즘으로 경로를 반환합니다.
    /// </summary>
    public List<Vector3> FindPath(Vector3 _startWorld, float _minDistance)
    {
        if (tilemapDataProvider == null) return null;

        Vector3 endWorld = GetRandomTarget(_startWorld, _minDistance);
        
        // 시작점과 목표지점이 같으면(이동 불가 등) null 반환
        if ((endWorld - _startWorld).sqrMagnitude < 0.01f) return null;

        Vector3Int startCell = tilemapDataProvider.WorldToCell(_startWorld);
        Vector3Int endCell = tilemapDataProvider.WorldToCell(endWorld);

        openSet.Clear();
        closedSet.Clear();
        finalPath.Clear();

        openSet.Add(startCell, new Node(startCell, 0, GetDistance(startCell, endCell), startCell));

        while (openSet.Count > 0)
        {
            Node currentNode = GetLowestFCostNode();

            if (currentNode.cellPos == endCell)
            {
                return RetracePath(startCell, currentNode);
            }

            openSet.Remove(currentNode.cellPos);
            closedSet.Add(currentNode.cellPos, currentNode);

            GetNeighbors(currentNode.cellPos);
            for (int i = 0; i < neighbors.Count; i++)
            {
                Vector3Int neighbor = neighbors[i];

                if (closedSet.ContainsKey(neighbor)) continue;

                int newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode.cellPos, neighbor);
                bool isInOpenSet = openSet.TryGetValue(neighbor, out Node neighborNode);

                if (newMovementCostToNeighbor < neighborNode.gCost || !isInOpenSet)
                {
                    Node newNode = new Node(neighbor, newMovementCostToNeighbor, GetDistance(neighbor, endCell), currentNode.cellPos);
                    
                    if (!isInOpenSet) openSet.Add(neighbor, newNode);
                    else openSet[neighbor] = newNode;
                }
            }
        }

        return null;
    }

    // // 프라이빗 로직 메서드

    private Node GetLowestFCostNode()
    {
        Node lowestNode = default;
        int minFCost = int.MaxValue;

        foreach (var kvp in openSet)
        {
            if (kvp.Value.fCost < minFCost)
            {
                minFCost = kvp.Value.fCost;
                lowestNode = kvp.Value;
            }
            else if (kvp.Value.fCost == minFCost)
            {
                if (kvp.Value.hCost < lowestNode.hCost)
                {
                    lowestNode = kvp.Value;
                }
            }
        }

        return lowestNode;
    }

    private void GetNeighbors(Vector3Int _cell)
    {
        neighbors.Clear();
        // 상하좌우 및 대각선 8방향
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                Vector3Int neighborPos = new Vector3Int(_cell.x + x, _cell.y + y, 0);
                if (tilemapDataProvider.IsWalkable(neighborPos))
                {
                    neighbors.Add(neighborPos);
                }
            }
        }
    }

    private int GetDistance(Vector3Int _a, Vector3Int _b)
    {
        int dstX = Mathf.Abs(_a.x - _b.x);
        int dstY = Mathf.Abs(_a.y - _b.y);

        if (dstX > dstY) return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    private List<Vector3> RetracePath(Vector3Int _startCell, Node _endNode)
    {
        Node curr = _endNode;
        while (curr.cellPos != _startCell)
        {
            finalPath.Add(tilemapDataProvider.CellToWorld(curr.cellPos));
            curr = closedSet[curr.parentPos];
        }
        finalPath.Reverse();
        return finalPath;
    }
}
