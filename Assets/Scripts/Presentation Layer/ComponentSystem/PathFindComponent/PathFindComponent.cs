using UnityEngine;
using System.Collections.Generic;

public class PathFindComponent : MonoBehaviour
{
    private struct Node
    {
        public int index;
        public int gCost;
        public int hCost;

        public int fCost => gCost + hCost;

        public Node(int _index, int _gCost, int _hCost)
        {
            index = _index;
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
    private IPathfindTreeProvider pathfindTreeProvider;

    // // 내부 데이터 (경로 공유 및 GC 최소화)
    private readonly List<Vector3> currentPath = new List<Vector3>(64);
    public IReadOnlyList<Vector3> Path => currentPath;

    // // 내부 의존성 (재사용을 위한 컬렉션, GC 최소화)
    private readonly FastPriorityQueue openList = new FastPriorityQueue(1024);

    // Dictionary 대체: 1차원 배열 최적화
    private int[] parentIndices;
    private int[] gCosts;
    private int gridWidth;
    private int gridHeight;

    // BFS 전용 최적화 큐 및 방문 배열
    private int[] bfsQueue;
    private int[] bfsVisited;
    private int bfsVisitedCounter = 0;

    private static readonly Vector3Int[] neighborOffsets = new Vector3Int[]
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
        new Vector3Int(1, 1, 0), new Vector3Int(-1, 1, 0),
        new Vector3Int(1, -1, 0), new Vector3Int(-1, -1, 0)
    };

    private static readonly int[] neighborCosts = new int[] { 10, 10, 10, 10, 14, 14, 14, 14 };

    public void Initialize(ITilemapDataProvider _tilemapDataProvider, IPathfindTreeProvider _pathfindTreeProvider)
    {
        tilemapDataProvider = _tilemapDataProvider;
        pathfindTreeProvider = _pathfindTreeProvider;

        gridWidth = tilemapDataProvider.GridWidth;
        gridHeight = tilemapDataProvider.GridHeight;

        int size = gridWidth * gridHeight;
        
        if (parentIndices == null || parentIndices.Length != size)
        {
            parentIndices = new int[size];
            gCosts = new int[size];
            bfsQueue = new int[size];
            bfsVisited = new int[size];
        }
        else
        {
            System.Array.Clear(bfsQueue, 0, size);
            System.Array.Clear(bfsVisited, 0, size);
        }

        bfsVisitedCounter = 0;

        System.Array.Fill(parentIndices, -1);
        System.Array.Fill(gCosts, int.MaxValue);
    }

    /// <summary>
    /// 내부 리스트(currentPath)를 사용하여 길을 찾습니다.
    /// </summary>
    public bool FindPath(Vector3 _startWorldPos, Vector3 _endWorldPos, int _maxIterations = 500)
    {
        return FindPath(_startWorldPos, _endWorldPos, currentPath, _maxIterations);
    }

    /// <summary>
    /// A* 알고리즘을 사용하여 두 지점 사이의 길을 찾습니다. (나무가 아닌 임의의 지점까지 이동할 때 사용)
    /// </summary>
    public bool FindPath(Vector3 _startWorldPos, Vector3 _endWorldPos, List<Vector3> _pathResult, int _maxIterations = 500)
    {
        _pathResult.Clear();
        Vector3Int startPos = tilemapDataProvider.WorldToCell(_startWorldPos);
        Vector3Int targetPos = tilemapDataProvider.WorldToCell(_endWorldPos);

        // 범위 밖 예외 처리
        if (startPos.x < 0 || startPos.x >= gridWidth || startPos.y < 0 || startPos.y >= gridHeight ||
            targetPos.x < 0 || targetPos.x >= gridWidth || targetPos.y < 0 || targetPos.y >= gridHeight)
        {
            return false;
        }

        if (!tilemapDataProvider.IsWalkable(targetPos) || tilemapDataProvider.HasRockDeco(targetPos))
        {
            return false;
        }

        ResetArrays();

        int startIndex = PosToIndex(startPos);
        int targetIndex = PosToIndex(targetPos);

        openList.Clear();

        gCosts[startIndex] = 0;
        openList.Push(new Node(startIndex, 0, GetDistance(startPos, targetPos)));

        int iterations = 0;
        while (openList.Count > 0)
        {
            Node currentNode = openList.Pop();

            // 이미 더 좋은 경로를 찾은 노드라면 스킵
            if (currentNode.gCost > gCosts[currentNode.index])
            {
                continue;
            }

            if (currentNode.index == targetIndex)
            {
                RetracePath(startIndex, targetIndex, _pathResult);
                return true;
            }

            Vector3Int currentPos = IndexToPos(currentNode.index);

            for (int i = 0; i < neighborOffsets.Length; i++)
            {
                Vector3Int neighborPos = currentPos + neighborOffsets[i];

                if (neighborPos.x < 0 || neighborPos.x >= gridWidth || neighborPos.y < 0 || neighborPos.y >= gridHeight)
                    continue;

                if (!tilemapDataProvider.IsWalkable(neighborPos) || tilemapDataProvider.HasRockDeco(neighborPos))
                {
                    continue;
                }

                // 대각선 이동 시 코너 커팅 방지
                if (i >= 4)
                {
                    Vector3Int side1 = currentPos + new Vector3Int(neighborOffsets[i].x, 0, 0);
                    Vector3Int side2 = currentPos + new Vector3Int(0, neighborOffsets[i].y, 0);
                    if (!tilemapDataProvider.IsWalkable(side1) || tilemapDataProvider.HasRockDeco(side1) ||
                        !tilemapDataProvider.IsWalkable(side2) || tilemapDataProvider.HasRockDeco(side2))
                    {
                        continue;
                    }
                }

                int neighborIndex = PosToIndex(neighborPos);
                int newGCost = currentNode.gCost + neighborCosts[i];

                if (newGCost < gCosts[neighborIndex])
                {
                    gCosts[neighborIndex] = newGCost;
                    parentIndices[neighborIndex] = currentNode.index;
                    openList.Push(new Node(neighborIndex, newGCost, GetDistance(neighborPos, targetPos)));
                }
            }

            if (++iterations > _maxIterations) break;
        }

        return false;
    }

    /// <summary>
    /// _nearWorldPos 자체가 이동 불가 타일이어도(오브젝트가 자기 발밑을 길찾기 이동 불가 타일로
    /// 막아둔 경우 등) 그 주변에서 BFS로 가장 가까운 이동 가능한 타일을 찾아 그 타일까지의
    /// 경로를 채웁니다. _nearWorldPos가 이미 이동 가능한 타일이면 그냥 FindPath와 동일하게 동작합니다.
    /// </summary>
    public bool FindPathNear(Vector3 _startWorldPos, Vector3 _nearWorldPos, List<Vector3> _pathResult)
    {
        Vector3Int nearCell = tilemapDataProvider.WorldToCell(_nearWorldPos);

        if (nearCell.x < 0 || nearCell.x >= gridWidth || nearCell.y < 0 || nearCell.y >= gridHeight)
        {
            _pathResult.Clear();
            return false;
        }

        if (tilemapDataProvider.IsWalkable(nearCell) && !tilemapDataProvider.HasRockDeco(nearCell))
        {
            return FindPath(_startWorldPos, _nearWorldPos, _pathResult);
        }

        // GC-Free BFS 상태 초기화 (막힌 타일 주변을 걸을 수 있는 타일이 나올 때까지 바깥으로 훑는다)
        bfsVisitedCounter++;
        if (bfsVisitedCounter == int.MaxValue)
        {
            System.Array.Fill(bfsVisited, 0);
            bfsVisitedCounter = 1;
        }

        int head = 0;
        int tail = 0;
        int nearIndex = PosToIndex(nearCell);
        bfsQueue[tail++] = nearIndex;
        bfsVisited[nearIndex] = bfsVisitedCounter;

        while (head < tail)
        {
            int currentIndex = bfsQueue[head++];
            Vector3Int currentPos = IndexToPos(currentIndex);

            for (int i = 0; i < neighborOffsets.Length; i++)
            {
                Vector3Int neighborPos = currentPos + neighborOffsets[i];
                if (neighborPos.x < 0 || neighborPos.x >= gridWidth || neighborPos.y < 0 || neighborPos.y >= gridHeight)
                    continue;

                int neighborIndex = PosToIndex(neighborPos);
                if (bfsVisited[neighborIndex] == bfsVisitedCounter)
                    continue;
                bfsVisited[neighborIndex] = bfsVisitedCounter;

                if (tilemapDataProvider.IsWalkable(neighborPos) && !tilemapDataProvider.HasRockDeco(neighborPos))
                {
                    // 걸을 수 있는 타일을 찾음 - 그 지점까지 실제 A* 경로를 계산해서 반환
                    Vector3 walkableWorldPos = tilemapDataProvider.CellToWorld(neighborPos);
                    return FindPath(_startWorldPos, walkableWorldPos, _pathResult);
                }

                // 막힌 타일이라도 그 너머를 확인하기 위해 계속 확장한다
                bfsQueue[tail++] = neighborIndex;
            }
        }

        _pathResult.Clear();
        return false;
    }

    private void ResetArrays()
    {
        System.Array.Fill(parentIndices, -1);
        System.Array.Fill(gCosts, int.MaxValue);
    }

    private int PosToIndex(Vector3Int _pos) => _pos.x + _pos.y * gridWidth;
    private Vector3Int IndexToPos(int _index) => new Vector3Int(_index % gridWidth, _index / gridWidth, 0);

    private int GetDistance(Vector3Int _a, Vector3Int _b)
    {
        int dstX = Mathf.Abs(_a.x - _b.x);
        int dstY = Mathf.Abs(_a.y - _b.y);
        if (dstX > dstY) return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    private void RetracePath(int _startIndex, int _targetIndex, List<Vector3> _pathResult)
    {
        int curr = _targetIndex;
        while (curr != _startIndex)
        {
            _pathResult.Add(tilemapDataProvider.CellToWorld(IndexToPos(curr)));
            curr = parentIndices[curr];
            if (curr == -1) break; // 안전장치
        }
        _pathResult.Reverse();
    }

    public bool FindNearestTreePath(Vector3 _startWorldPos, out ITreeObj _targetTree, List<Vector3> _pathResult)
    {
        _targetTree = null;
        _pathResult.Clear();
        if (pathfindTreeProvider == null) return false;

        Vector3Int startPos = tilemapDataProvider.WorldToCell(_startWorldPos);

        if (startPos.x < 0 || startPos.x >= gridWidth || startPos.y < 0 || startPos.y >= gridHeight)
            return false;

        // GC-Free BFS 상태 초기화
        bfsVisitedCounter++;
        if (bfsVisitedCounter == int.MaxValue)
        {
            System.Array.Fill(bfsVisited, 0);
            bfsVisitedCounter = 1;
        }

        int head = 0;
        int tail = 0;

        int startIndex = PosToIndex(startPos);
        bfsQueue[tail++] = startIndex;
        bfsVisited[startIndex] = bfsVisitedCounter;
        parentIndices[startIndex] = -1;

        while (head < tail)
        {
            int currentIndex = bfsQueue[head++];
            Vector3Int currentPos = IndexToPos(currentIndex);

            for (int i = 0; i < neighborOffsets.Length; i++)
            {
                Vector3Int neighborPos = currentPos + neighborOffsets[i];
                if (neighborPos.x < 0 || neighborPos.x >= gridWidth || neighborPos.y < 0 || neighborPos.y >= gridHeight)
                    continue;

                int neighborIndex = PosToIndex(neighborPos);

                // 1. 나무가 있는지 먼저 확인 (나무 타일은 IsWalkable이 false이므로 먼저 체크해야 함)
                ITreeObj tree = pathfindTreeProvider.GetTreeAt(neighborIndex);
                if (tree != null)
                {
                    // 다른 NPC가 이미 타겟팅 중인 나무는 건너뛰고 계속 탐색 (같은 나무 동시 타겟팅 방지)
                    if (tree.bReserved)
                        continue;

                    _targetTree = tree;
                    // 나무가 있는 타일로는 이동할 수 없으므로, 현재 타일(currentIndex)까지의 경로를 반환
                    RetracePath(startIndex, currentIndex, _pathResult);
                    return true;
                }

                // 2. 나무가 없다면 이동 가능한지 확인
                if (!tilemapDataProvider.IsWalkable(neighborPos) || tilemapDataProvider.HasRockDeco(neighborPos))
                    continue;

                // 대각선 이동 시 코너 커팅 방지
                if (i >= 4)
                {
                    Vector3Int side1 = currentPos + new Vector3Int(neighborOffsets[i].x, 0, 0);
                    Vector3Int side2 = currentPos + new Vector3Int(0, neighborOffsets[i].y, 0);
                    if (!tilemapDataProvider.IsWalkable(side1) || tilemapDataProvider.HasRockDeco(side1) ||
                        !tilemapDataProvider.IsWalkable(side2) || tilemapDataProvider.HasRockDeco(side2))
                    {
                        continue;
                    }
                }

                if (bfsVisited[neighborIndex] != bfsVisitedCounter)
                {
                    bfsVisited[neighborIndex] = bfsVisitedCounter;
                    parentIndices[neighborIndex] = currentIndex;
                    bfsQueue[tail++] = neighborIndex;
                }
            }
        }

        return false;
    }
}
