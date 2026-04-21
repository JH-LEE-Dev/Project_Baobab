using UnityEngine;
using System.Collections.Generic;

public class PathFindComponent : MonoBehaviour
{
    private struct Node
    {
        public Vector3Int pos;
        public int gCost;
        public int hCost;

        public int fCost => gCost + hCost;

        public Node(Vector3Int _pos, int _gCost, int _hCost)
        {
            pos = _pos;
            gCost = _gCost;
            hCost = _hCost;
        }
    }

    /// <summary>
    /// A* 전용 최소 힙 (GC 최소화 및 O(log N) 성능 확보)
    /// </summary>
    private class FastPriorityQueue
    {
        private Node[] nodes;
        private int count;

        public int Count => count;

        public FastPriorityQueue(int _capacity)
        {
            nodes = new Node[_capacity];
            count = 0;
        }

        public void Clear() => count = 0;

        public void Push(Node _node)
        {
            if (count >= nodes.Length) return;

            nodes[count] = _node;
            int i = count;
            count++;

            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (!IsHigherPriority(nodes[i], nodes[p])) break;

                Node temp = nodes[i];
                nodes[i] = nodes[p];
                nodes[p] = temp;
                i = p;
            }
        }

        public Node Pop()
        {
            Node result = nodes[0];
            count--;
            if (count > 0)
            {
                nodes[0] = nodes[count];
                int i = 0;
                while (true)
                {
                    int left = i * 2 + 1;
                    int right = i * 2 + 2;
                    int best = i;

                    if (left < count && IsHigherPriority(nodes[left], nodes[best])) best = left;
                    if (right < count && IsHigherPriority(nodes[right], nodes[best])) best = right;

                    if (best == i) break;

                    Node temp = nodes[i];
                    nodes[i] = nodes[best];
                    nodes[best] = temp;
                    i = best;
                }
            }
            return result;
        }

        private bool IsHigherPriority(Node _a, Node _b)
        {
            if (_a.fCost < _b.fCost) return true;
            if (_a.fCost == _b.fCost) return _a.hCost < _b.hCost;
            return false;
        }
    }

    // // 외부 의존성
    private ITilemapDataProvider tilemapDataProvider;
    private IPathfindGridProvider pathfindGridProvider;

    // // 내부 데이터 (경로 공유 및 GC 최소화)
    private readonly List<Vector3> currentPath = new List<Vector3>(64);
    public IReadOnlyList<Vector3> Path => currentPath;

    // // 내부 의존성 (재사용을 위한 컬렉션, GC 최소화)
    private readonly FastPriorityQueue openList = new FastPriorityQueue(1024);
    private readonly Dictionary<Vector3Int, Vector3Int> parentMap = new Dictionary<Vector3Int, Vector3Int>(1024);
    private readonly Dictionary<Vector3Int, int> gCostMap = new Dictionary<Vector3Int, int>(1024);

    private static readonly Vector3Int[] neighborOffsets = new Vector3Int[]
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
        new Vector3Int(1, 1, 0), new Vector3Int(-1, 1, 0),
        new Vector3Int(1, -1, 0), new Vector3Int(-1, -1, 0)
    };

    private static readonly int[] neighborCosts = new int[] { 10, 10, 10, 10, 14, 14, 14, 14 };

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
    public bool FindPath(Vector3 _startWorldPos, Vector3 _endWorldPos, List<Vector3> _pathResult)
    {
        _pathResult.Clear();
        Vector3Int startPos = tilemapDataProvider.WorldToCell(_startWorldPos);
        Vector3Int targetPos = tilemapDataProvider.WorldToCell(_endWorldPos);

        if (!tilemapDataProvider.IsWalkable(targetPos) || pathfindGridProvider.IsOccupied(targetPos))
        {
            return false;
        }

        openList.Clear();
        parentMap.Clear();
        gCostMap.Clear();

        Node startNode = new Node(startPos, 0, GetDistance(startPos, targetPos));
        openList.Push(startNode);
        gCostMap[startPos] = 0;

        int iterations = 0;
        while (openList.Count > 0)
        {
            Node currentNode = openList.Pop();

            // 이미 더 좋은 경로를 찾은 노드라면 스킵
            if (gCostMap.TryGetValue(currentNode.pos, out int bestGCost) && currentNode.gCost > bestGCost)
            {
                continue;
            }

            if (currentNode.pos == targetPos)
            {
                RetracePath(startPos, targetPos, _pathResult);
                return true;
            }

            for (int i = 0; i < neighborOffsets.Length; i++)
            {
                Vector3Int neighborPos = currentNode.pos + neighborOffsets[i];

                if (!tilemapDataProvider.IsWalkable(neighborPos) || pathfindGridProvider.IsOccupied(neighborPos))
                {
                    continue;
                }

                // 대각선 이동 시 코너 커팅 방지
                if (i >= 4)
                {
                    Vector3Int side1 = currentNode.pos + new Vector3Int(neighborOffsets[i].x, 0, 0);
                    Vector3Int side2 = currentNode.pos + new Vector3Int(0, neighborOffsets[i].y, 0);
                    if (!tilemapDataProvider.IsWalkable(side1) || !tilemapDataProvider.IsWalkable(side2))
                    {
                        continue;
                    }
                }

                int newGCost = currentNode.gCost + neighborCosts[i];
                if (!gCostMap.TryGetValue(neighborPos, out int currentNeighborGCost) || newGCost < currentNeighborGCost)
                {
                    gCostMap[neighborPos] = newGCost;
                    parentMap[neighborPos] = currentNode.pos;
                    openList.Push(new Node(neighborPos, newGCost, GetDistance(neighborPos, targetPos)));
                }
            }

            if (++iterations > 500) break;
        }

        return false;
    }

    private int GetDistance(Vector3Int _a, Vector3Int _b)
    {
        int dstX = Mathf.Abs(_a.x - _b.x);
        int dstY = Mathf.Abs(_a.y - _b.y);
        if (dstX > dstY) return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    private void RetracePath(Vector3Int _startPos, Vector3Int _targetPos, List<Vector3> _pathResult)
    {
        Vector3Int curr = _targetPos;
        while (curr != _startPos)
        {
            _pathResult.Add(tilemapDataProvider.CellToWorld(curr));
            curr = parentMap[curr];
        }
        _pathResult.Reverse();
    }
}
