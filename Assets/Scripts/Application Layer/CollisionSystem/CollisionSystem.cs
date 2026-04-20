using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 충돌 연산에 필요한 최소한의 데이터만 담은 구조체입니다.
/// 인터페이스 접근(박싱)을 피하고 캐시 효율을 높이기 위해 사용합니다.
/// </summary>
public struct CollisionEntity
{
    public Vector2 center; // Position + Offset 계산된 최종 위치
    public float radius;
    public int layer;
    public IStaticCollidable owner; // 실제 컴포넌트 참조 (데미지 전달용)
}

public interface IStaticCollidable
{
    Vector2 Position { get; }
    Vector2 Offset { get; }
    float Radius { get; }
    int Layer { get; }
    void TakeDamage(float damage);
}

public class CollisionSystem : MonoBehaviour
{
    private static CollisionSystem instance;
    public static CollisionSystem Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<CollisionSystem>();
                if (instance == null)
                {
                    Debug.LogWarning("<color=red><b>[CollisionSystem]</b> No Instance found in scene!</color>");
                }
            }
            return instance;
        }
    }

    [Header("Grid Settings")]
    [SerializeField] private float cellSize = 1.5f;
    [SerializeField] private Vector2Int gridCount = new Vector2Int(200, 200); // 가로, 세로 격자 개수
    [SerializeField] private Vector2 gridOrigin = new Vector2(-150, -150); // 격자 시작점 (좌하단)

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;
    [SerializeField] private bool showFullGrid = false;
    [SerializeField] private Color objectColor = Color.cyan;
    [SerializeField] private Color activeGridColor = new Color(0, 1, 0, 0.1f);

    // 정적(나무) 및 동적(동물) 격자 분리 (1차원 배열로 최적화)
    private List<CollisionEntity>[] staticGrid;
    private List<CollisionEntity>[] dynamicGrid;

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) { Destroy(gameObject); return; }

        InitializeGrids();
    }

    private void InitializeGrids()
    {
        int totalCells = gridCount.x * gridCount.y;
        staticGrid = new List<CollisionEntity>[totalCells];
        dynamicGrid = new List<CollisionEntity>[totalCells];

        for (int i = 0; i < totalCells; i++)
        {
            staticGrid[i] = new List<CollisionEntity>(4);
            dynamicGrid[i] = new List<CollisionEntity>(4);
        }
        Debug.Log($"<color=green><b>[CollisionSystem]</b> Grid Initialized: {gridCount.x}x{gridCount.y} cells.</color>");
    }

    /// <summary>
    /// 객체를 시스템에 등록합니다.
    /// </summary>
    public void Register(IStaticCollidable obj, bool isStatic = true)
    {
        int index = WorldToGridIndex(obj.Position + obj.Offset);
        if (index < 0) return;

        var targetGrid = isStatic ? staticGrid[index] : dynamicGrid[index];

        // 중복 등록 방지
        for (int i = 0; i < targetGrid.Count; i++)
        {
            if (targetGrid[i].owner == obj) return;
        }

        targetGrid.Add(CreateEntity(obj));
    }

    public void Unregister(IStaticCollidable obj, bool isStatic = true)
    {
        int index = WorldToGridIndex(obj.Position + obj.Offset);
        if (index < 0) return;

        var targetGrid = isStatic ? staticGrid[index] : dynamicGrid[index];
        for (int i = 0; i < targetGrid.Count; i++)
        {
            if (targetGrid[i].owner == obj)
            {
                targetGrid.RemoveAt(i);
                return; // 하나만 제거하고 종료
            }
        }
    }

    /// <summary>
    /// 이동하는 객체(동물)의 격자 정보를 업데이트합니다.
    /// </summary>
    public void UpdatePosition(IStaticCollidable obj, Vector2 oldPos, Vector2 newPos)
    {
        int oldIndex = WorldToGridIndex(oldPos + obj.Offset);
        int newIndex = WorldToGridIndex(newPos + obj.Offset);

        if (oldIndex == newIndex)
        {
            // 같은 격자 내 이동이면 데이터만 갱신
            if (newIndex >= 0)
            {
                var list = dynamicGrid[newIndex];
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].owner == obj)
                    {
                        var entity = list[i];
                        entity.center = newPos + obj.Offset;
                        list[i] = entity;
                        break;
                    }
                }
            }
            return;
        }

        // 격자가 바뀌었을 때만 리스트 재배치
        if (oldIndex >= 0)
        {
            var oldList = dynamicGrid[oldIndex];
            for (int i = 0; i < oldList.Count; i++)
            {
                if (oldList[i].owner == obj) { oldList.RemoveAt(i); break; }
            }
        }

        if (newIndex >= 0)
        {
            dynamicGrid[newIndex].Add(CreateEntity(obj));
        }
    }

    private CollisionEntity CreateEntity(IStaticCollidable obj)
    {
        return new CollisionEntity
        {
            center = obj.Position + obj.Offset,
            radius = obj.Radius,
            layer = obj.Layer,
            owner = obj
        };
    }

    private int WorldToGridIndex(Vector2 worldPos)
    {
        int x = Mathf.FloorToInt((worldPos.x - gridOrigin.x) / cellSize);
        int y = Mathf.FloorToInt((worldPos.y - gridOrigin.y) / cellSize);

        if (x < 0 || x >= gridCount.x || y < 0 || y >= gridCount.y) return -1;
        return x + y * gridCount.x;
    }

    public bool CheckCollision(Vector2 start, Vector2 end, float bulletRadius, int layerMask, out IStaticCollidable hitObject)
    {
        hitObject = null;
        
        // 탐색 범위 격자 계산
        Vector2 min = Vector2.Min(start, end) - new Vector2(bulletRadius, bulletRadius);
        Vector2 max = Vector2.Max(start, end) + new Vector2(bulletRadius, bulletRadius);

        int minX = Mathf.FloorToInt((min.x - gridOrigin.x) / cellSize);
        int maxX = Mathf.FloorToInt((max.x - gridOrigin.x) / cellSize);
        int minY = Mathf.FloorToInt((min.y - gridOrigin.y) / cellSize);
        int maxY = Mathf.FloorToInt((max.y - gridOrigin.y) / cellSize);

        minX = Mathf.Clamp(minX, 0, gridCount.x - 1);
        maxX = Mathf.Clamp(maxX, 0, gridCount.x - 1);
        minY = Mathf.Clamp(minY, 0, gridCount.y - 1);
        maxY = Mathf.Clamp(maxY, 0, gridCount.y - 1);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int index = x + y * gridCount.x;
                
                // Static Grid 검사
                if (InternalCheck(staticGrid[index], start, end, bulletRadius, layerMask, out hitObject)) return true;
                // Dynamic Grid 검사
                if (InternalCheck(dynamicGrid[index], start, end, bulletRadius, layerMask, out hitObject)) return true;
            }
        }

        return false;
    }

    private bool InternalCheck(List<CollisionEntity> list, Vector2 start, Vector2 end, float radius, int mask, out IStaticCollidable hit)
    {
        hit = null;
        for (int i = 0; i < list.Count; i++)
        {
            var entity = list[i];
            if (((1 << entity.layer) & mask) == 0) continue;

            float combinedRadius = radius + entity.radius;
            if (SqrDistancePointToSegment(start, end, entity.center) < combinedRadius * combinedRadius)
            {
                hit = entity.owner;
                return true;
            }
        }
        return false;
    }

    public void GetCollidablesInRadius(Vector2 center, float radius, int layerMask, List<IStaticCollidable> results)
    {
        results.Clear();
        
        int minX = Mathf.Clamp(Mathf.FloorToInt((center.x - radius - gridOrigin.x) / cellSize), 0, gridCount.x - 1);
        int maxX = Mathf.Clamp(Mathf.FloorToInt((center.x + radius - gridOrigin.x) / cellSize), 0, gridCount.x - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt((center.y - radius - gridOrigin.y) / cellSize), 0, gridCount.y - 1);
        int maxY = Mathf.Clamp(Mathf.FloorToInt((center.y + radius - gridOrigin.y) / cellSize), 0, gridCount.y - 1);

        float radiusSq = radius * radius;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                int index = x + y * gridCount.x;
                InternalCollect(staticGrid[index], center, radius, layerMask, results);
                InternalCollect(dynamicGrid[index], center, radius, layerMask, results);
            }
        }
    }

    private void InternalCollect(List<CollisionEntity> list, Vector2 center, float radius, int mask, List<IStaticCollidable> results)
    {
        for (int i = 0; i < list.Count; i++)
        {
            var entity = list[i];
            if (((1 << entity.layer) & mask) == 0) continue;

            float combinedRadius = radius + entity.radius;
            if ((entity.center - center).sqrMagnitude <= combinedRadius * combinedRadius)
            {
                results.Add(entity.owner);
            }
        }
    }

    private float SqrDistancePointToSegment(Vector2 a, Vector2 b, Vector2 p)
    {
        Vector2 ab = b - a;
        Vector2 ap = p - a;
        float t = Vector2.Dot(ap, ab);
        float ab2 = ab.sqrMagnitude;
        if (ab2 > 0) t /= ab2;
        t = Mathf.Clamp01(t);
        Vector2 closestPoint = a + t * ab;
        return (p - closestPoint).sqrMagnitude;
    }

    private void OnDrawGizmos()
    {
        if (!showDebug || staticGrid == null) return;

        for (int i = 0; i < staticGrid.Length; i++)
        {
            int x = i % gridCount.x;
            int y = i / gridCount.x;
            Vector3 center = new Vector3(gridOrigin.x + x * cellSize + cellSize * 0.5f, gridOrigin.y + y * cellSize + cellSize * 0.5f, 0);

            bool hasObjects = staticGrid[i].Count > 0 || dynamicGrid[i].Count > 0;
            if (hasObjects)
            {
                Gizmos.color = activeGridColor;
                Gizmos.DrawWireCube(center, new Vector3(cellSize, cellSize, 0));
                
                Gizmos.color = objectColor;
                foreach (var e in staticGrid[i]) Gizmos.DrawWireSphere(e.center, e.radius);
                foreach (var e in dynamicGrid[i]) Gizmos.DrawWireSphere(e.center, e.radius);
            }
            else if (showFullGrid)
            {
                Gizmos.color = new Color(1, 1, 1, 0.02f);
                Gizmos.DrawWireCube(center, new Vector3(cellSize, cellSize, 0));
            }
        }
    }
}
