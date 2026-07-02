using UnityEngine;
using System.Collections.Generic;

public class PathFindComponent : MonoBehaviour
{
    // // 외부 의존성
    private ITilemapDataProvider tilemapDataProvider;
    private IPathfindTreeProvider pathfindTreeProvider;

    // // 내부 의존성 (재사용을 위한 컬렉션, GC 최소화)
    // Dictionary 대체: 1차원 배열 최적화
    private int[] parentIndices;
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

    public void Initialize(ITilemapDataProvider _tilemapDataProvider, IPathfindTreeProvider _pathfindTreeProvider)
    {
        tilemapDataProvider = _tilemapDataProvider;
        pathfindTreeProvider = _pathfindTreeProvider;

        gridWidth = tilemapDataProvider.GridWidth;
        gridHeight = tilemapDataProvider.GridHeight;

        int size = gridWidth * gridHeight;
        parentIndices = new int[size];

        bfsQueue = new int[size];
        bfsVisited = new int[size];
        bfsVisitedCounter = 0;

        System.Array.Fill(parentIndices, -1);
    }

    private int PosToIndex(Vector3Int _pos) => _pos.x + _pos.y * gridWidth;
    private Vector3Int IndexToPos(int _index) => new Vector3Int(_index % gridWidth, _index / gridWidth, 0);

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
