using UnityEngine;
using System.Collections.Generic;

public class PathFindComponent : MonoBehaviour
{
    private struct Node
    {
        public Vector3Int pos;
        public int gCost;
        public int hCost;
        public Vector3Int parentPos;

        public int fCost => gCost + hCost;

        public Node(Vector3Int _pos, int _gCost, int _hCost, Vector3Int _parentPos)
        {
            pos = _pos;
            gCost = _gCost;
            hCost = _hCost;
            parentPos = _parentPos;
        }
    }

    // // 외부 의존성
    private ITilemapDataProvider tilemapDataProvider;
    private IPathfindGridProvider pathfindGridProvider;

    // // 내부 데이터 (경로 공유 및 GC 최소화)
    private readonly List<Vector3> currentPath = new List<Vector3>(64);
    public IReadOnlyList<Vector3> Path => currentPath;

    // // 내부 의존성 (재사용을 위한 컬렉션, GC 최소화)
    private readonly List<Node> openList = new List<Node>(256);
    private readonly Dictionary<Vector3Int, Node> closedNodes = new Dictionary<Vector3Int, Node>(256);
    private static readonly Vector3Int[] neighborOffsets = new Vector3Int[]
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
        new Vector3Int(1, 1, 0), new Vector3Int(-1, 1, 0),
        new Vector3Int(1, -1, 0), new Vector3Int(-1, -1, 0)
    };

    public void Initialize(ITilemapDataProvider _tilemapDataProvider, IPathfindGridProvider _pathfindGridProvider)
    {
        tilemapDataProvider = _tilemapDataProvider;
        pathfindGridProvider = _pathfindGridProvider;
    }

    // // 점유 관리 API
    public bool IsOccupied(Vector3Int _cellPos) => pathfindGridProvider.IsOccupied(_cellPos);
    public bool Occupy(Vector3Int _cellPos) => pathfindGridProvider.Occupy(_cellPos);
    public void Release(Vector3Int _cellPos) => pathfindGridProvider.Release(_cellPos);
    public Vector3Int WorldToCell(Vector3 _worldPos) => tilemapDataProvider.WorldToCell(_worldPos);
    public Vector3 CellToWorld(Vector3Int _cellPos) => tilemapDataProvider.CellToWorld(_cellPos);
    public bool IsWalkable(Vector3Int _cellPos) => tilemapDataProvider.IsWalkable(_cellPos);

    /// <summary>
    /// 내부 리스트(currentPath)를 사용하여 길을 찾습니다.
    /// </summary>
    public bool FindPath(Vector3 _startWorldPos, Vector3 _endWorldPos)
    {
        return FindPath(_startWorldPos, _endWorldPos, currentPath);
    }

    /// <summary>
    /// A* 알고리즘을 사용하여 두 지점 사이의 길을 찾습니다.
    /// </summary>
    /// <param name="_startWorldPos">시작 월드 좌표</param>
    /// <param name="_endWorldPos">목표 월드 좌표</param>
    /// <param name="_pathResult">결과 경로를 담을 리스트 (외부에서 관리)</param>
    /// <returns>경로를 찾았는지 여부</returns>
    public bool FindPath(Vector3 _startWorldPos, Vector3 _endWorldPos, List<Vector3> _pathResult)
    {
        _pathResult.Clear();
        Vector3Int startPos = tilemapDataProvider.WorldToCell(_startWorldPos);
        Vector3Int targetPos = tilemapDataProvider.WorldToCell(_endWorldPos);

        // 도착 지점이 이동 불가능하거나 다른 유닛에 의해 점유되어 있으면 즉시 종료
        if (!tilemapDataProvider.IsWalkable(targetPos) || pathfindGridProvider.IsOccupied(targetPos))
        {
            return false;
        }

        openList.Clear();
        closedNodes.Clear();

        openList.Add(new Node(startPos, 0, GetDistance(startPos, targetPos), startPos));

        while (openList.Count > 0)
        {
            int currentNodeIndex = 0;
            for (int i = 1; i < openList.Count; i++)
            {
                if (openList[i].fCost < openList[currentNodeIndex].fCost ||
                    (openList[i].fCost == openList[currentNodeIndex].fCost && openList[i].hCost < openList[currentNodeIndex].hCost))
                {
                    currentNodeIndex = i;
                }
            }

            Node currentNode = openList[currentNodeIndex];
            openList.RemoveAt(currentNodeIndex);
            closedNodes[currentNode.pos] = currentNode;

            // 목표 도달
            if (currentNode.pos == targetPos)
            {
                RetracePath(startPos, currentNode, _pathResult);
                return true;
            }

            for (int i = 0; i < neighborOffsets.Length; i++)
            {
                Vector3Int offset = neighborOffsets[i];
                Vector3Int neighborPos = currentNode.pos + offset;

                // 1. 기본 이동 가능 여부, 방문 여부, 그리고 다른 유닛 점유 여부 확인
                if (!tilemapDataProvider.IsWalkable(neighborPos) ||
                    closedNodes.ContainsKey(neighborPos) ||
                    pathfindGridProvider.IsOccupied(neighborPos))
                {
                    continue;
                }

                // 2. 이동 경로 양 옆 타일 체크 (코너 커팅 방지)
                if (offset.x != 0 && offset.y != 0) // 대각선 이동 시 (1,1) 등
                {
                    // 양 옆은 직교 방향 타일
                    Vector3Int side1 = currentNode.pos + new Vector3Int(offset.x, 0, 0);
                    Vector3Int side2 = currentNode.pos + new Vector3Int(0, offset.y, 0);

                    // 양 옆 타일 중 하나라도 갈 수 없으면, 대각선으로 가로질러 갈 수 없음.
                    if (!tilemapDataProvider.IsWalkable(side1) || !tilemapDataProvider.IsWalkable(side2))
                    {
                        continue;
                    }
                }

                int newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode.pos, neighborPos);
                int openIndex = FindInOpenList(neighborPos);

                if (openIndex == -1)
                {
                    openList.Add(new Node(neighborPos, newMovementCostToNeighbor, GetDistance(neighborPos, targetPos), currentNode.pos));
                }
                else if (newMovementCostToNeighbor < openList[openIndex].gCost)
                {
                    // 더 짧은 경로를 찾은 경우 업데이트
                    openList[openIndex] = new Node(neighborPos, newMovementCostToNeighbor, GetDistance(neighborPos, targetPos), currentNode.pos);
                }
            }

            // 성능 저하 방지를 위한 최대 탐색 수 제한
            if (closedNodes.Count > 1000) break;
        }

        return false;
    }

    private int FindInOpenList(Vector3Int _pos)
    {
        for (int i = 0; i < openList.Count; i++)
        {
            if (openList[i].pos == _pos) return i;
        }
        return -1;
    }

    private int GetDistance(Vector3Int _a, Vector3Int _b)
    {
        int dstX = Mathf.Abs(_a.x - _b.x);
        int dstY = Mathf.Abs(_a.y - _b.y);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    private void RetracePath(Vector3Int _startPos, Node _endNode, List<Vector3> _pathResult)
    {
        Node currentNode = _endNode;
        while (currentNode.pos != _startPos)
        {
            _pathResult.Add(tilemapDataProvider.CellToWorld(currentNode.pos));
            currentNode = closedNodes[currentNode.parentPos];
        }
        _pathResult.Reverse();
    }
}
