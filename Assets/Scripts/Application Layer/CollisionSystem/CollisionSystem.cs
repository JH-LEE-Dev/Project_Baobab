using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// 충돌 연산에 필요한 최소한의 데이터만 담은 구조체입니다.
/// </summary>
public struct CollisionEntity
{
    public Vector2 center; 
    public float radius;
    public int layerBit; // 2번 최적화: 미리 계산된 비트마스크 (1 << layer)
    public IStaticCollidable owner; 
}

public interface IStaticCollidable
{
    Vector2 Position { get; }
    Vector2 Offset { get; }
    float Radius { get; }
    int Layer { get; }
    void TakeDamage(float _damage);
}

public class CollisionSystem : MonoBehaviour
{
    private static CollisionSystem instance;
    public static CollisionSystem Instance
    {
        get
        {
            if (instance == null) return null;
            return instance;
        }
    }

    [Header("Grid Settings")]
    [SerializeField] private float cellSize = 1.5f;
    [SerializeField] private Vector2Int gridCount = new Vector2Int(200, 200);
    [SerializeField] private Vector2 gridOrigin = new Vector2(-150, -150);

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;
    [SerializeField] private bool showFullGrid = false;
    [SerializeField] private Color objectColor = Color.cyan;
    [SerializeField] private Color activeGridColor = new Color(0, 1, 0, 0.1f);

    private List<CollisionEntity>[] staticGrid;
    private List<CollisionEntity>[] dynamicGrid;
    
    // 1번 최적화: 나눗셈 연산을 피하기 위한 역수 캐싱
    private float invCellSize;

    private void Awake()
    {
        if (instance == null) instance = this;
        else if (instance != this) { Destroy(gameObject); return; }

        InitializeGrids();
    }

    private void InitializeGrids()
    {
        invCellSize = 1f / cellSize;
        int _totalCells = gridCount.x * gridCount.y;
        staticGrid = new List<CollisionEntity>[_totalCells];
        dynamicGrid = new List<CollisionEntity>[_totalCells];

        for (int i = 0; i < _totalCells; i++)
        {
            staticGrid[i] = new List<CollisionEntity>(4);
            dynamicGrid[i] = new List<CollisionEntity>(4);
        }
        Debug.Log($"<color=green><b>[CollisionSystem]</b> Grid Initialized: {gridCount.x}x{gridCount.y} cells.</color>");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int WorldToGridIndex(Vector2 _worldPos)
    {
        // 1번 최적화 적용: 곱셈 연산으로 변경
        int _x = (int)((_worldPos.x - gridOrigin.x) * invCellSize);
        int _y = (int)((_worldPos.y - gridOrigin.y) * invCellSize);

        if (_x < 0 || _x >= gridCount.x || _y < 0 || _y >= gridCount.y) return -1;
        return _x + _y * gridCount.x;
    }

    public void Register(IStaticCollidable _obj, bool _isStatic = true)
    {
        int _index = WorldToGridIndex(_obj.Position + _obj.Offset);
        if (_index < 0) return;

        var _targetGrid = _isStatic ? staticGrid[_index] : dynamicGrid[_index];

        for (int i = 0; i < _targetGrid.Count; i++)
        {
            if (_targetGrid[i].owner == _obj) return;
        }

        _targetGrid.Add(CreateEntity(_obj));
    }

    public void Unregister(IStaticCollidable _obj, bool _isStatic = true)
    {
        int _index = WorldToGridIndex(_obj.Position + _obj.Offset);
        if (_index < 0) return;

        var _targetGrid = _isStatic ? staticGrid[_index] : dynamicGrid[_index];
        for (int i = 0; i < _targetGrid.Count; i++)
        {
            if (_targetGrid[i].owner == _obj)
            {
                _targetGrid.RemoveAt(i);
                return;
            }
        }
    }

    public void UpdatePosition(IStaticCollidable _obj, Vector2 _oldPos, Vector2 _newPos)
    {
        int _oldIndex = WorldToGridIndex(_oldPos + _obj.Offset);
        int _newIndex = WorldToGridIndex(_newPos + _obj.Offset);

        if (_oldIndex == _newIndex)
        {
            if (_newIndex >= 0)
            {
                var _list = dynamicGrid[_newIndex];
                for (int i = 0; i < _list.Count; i++)
                {
                    if (_list[i].owner == _obj)
                    {
                        var _entity = _list[i];
                        _entity.center = _newPos + _obj.Offset;
                        _list[i] = _entity;
                        break;
                    }
                }
            }
            return;
        }

        if (_oldIndex >= 0)
        {
            var _oldList = dynamicGrid[_oldIndex];
            for (int i = 0; i < _oldList.Count; i++)
            {
                if (_oldList[i].owner == _obj) { _oldList.RemoveAt(i); break; }
            }
        }

        if (_newIndex >= 0)
        {
            dynamicGrid[_newIndex].Add(CreateEntity(_obj));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CollisionEntity CreateEntity(IStaticCollidable _obj)
    {
        return new CollisionEntity
        {
            center = _obj.Position + _obj.Offset,
            radius = _obj.Radius,
            layerBit = (1 << _obj.Layer), // 2번 최적화: 비트마스크 미리 계산
            owner = _obj
        };
    }

    public bool CheckCollision(Vector2 _start, Vector2 _end, float _bulletRadius, int _layerMask, out IStaticCollidable _hitObject)
    {
        _hitObject = null;

        Vector2 _ab = _end - _start;
        float _ab2 = _ab.x * _ab.x + _ab.y * _ab.y;
        float _invAb2 = _ab2 > 0 ? 1f / _ab2 : 0;

        float _minX_val = (_start.x < _end.x ? _start.x : _end.x) - _bulletRadius;
        float _maxX_val = (_start.x > _end.x ? _start.x : _end.x) + _bulletRadius;
        float _minY_val = (_start.y < _end.y ? _start.y : _end.y) - _bulletRadius;
        float _maxY_val = (_start.y > _end.y ? _start.y : _end.y) + _bulletRadius;

        int _minX = Mathf.Clamp((int)((_minX_val - gridOrigin.x) * invCellSize), 0, gridCount.x - 1);
        int _maxX = Mathf.Clamp((int)((_maxX_val - gridOrigin.x) * invCellSize), 0, gridCount.x - 1);
        int _minY = Mathf.Clamp((int)((_minY_val - gridOrigin.y) * invCellSize), 0, gridCount.y - 1);
        int _maxY = Mathf.Clamp((int)((_maxY_val - gridOrigin.y) * invCellSize), 0, gridCount.y - 1);

        for (int x = _minX; x <= _maxX; x++)
        {
            for (int y = _minY; y <= _maxY; y++)
            {
                int _index = x + y * gridCount.x;
                if (InternalCheck(staticGrid[_index], _start, _ab, _invAb2, _bulletRadius, _layerMask, out _hitObject)) return true;
                if (InternalCheck(dynamicGrid[_index], _start, _ab, _invAb2, _bulletRadius, _layerMask, out _hitObject)) return true;
            }
        }

        return false;
    }

    private bool InternalCheck(List<CollisionEntity> _list, Vector2 _start, Vector2 _ab, float _invAb2, float _bulletRadius, int _mask, out IStaticCollidable _hit)
    {
        _hit = null;
        int _count = _list.Count;
        for (int i = 0; i < _count; i++)
        {
            if ((_list[i].layerBit & _mask) == 0) continue;

            float _combinedRadius = _bulletRadius + _list[i].radius;
            if (SqrDistancePointToSegmentOptimized(_start, _ab, _invAb2, _list[i].center) < _combinedRadius * _combinedRadius)
            {
                _hit = _list[i].owner;
                return true;
            }
        }
        return false;
    }

    public void GetCollidablesInRadius(Vector2 _center, float _radius, int _layerMask, List<IStaticCollidable> _results)
    {
        _results.Clear();

        int _minX = Mathf.Clamp((int)((_center.x - _radius - gridOrigin.x) * invCellSize), 0, gridCount.x - 1);
        int _maxX = Mathf.Clamp((int)((_center.x + _radius - gridOrigin.x) * invCellSize), 0, gridCount.x - 1);
        int _minY = Mathf.Clamp((int)((_center.y - _radius - gridOrigin.y) * invCellSize), 0, gridCount.y - 1);
        int _maxY = Mathf.Clamp((int)((_center.y + _radius - gridOrigin.y) * invCellSize), 0, gridCount.y - 1);

        for (int x = _minX; x <= _maxX; x++)
        {
            for (int y = _minY; y <= _maxY; y++)
            {
                int _index = x + y * gridCount.x;
                InternalCollect(staticGrid[_index], _center, _radius, _layerMask, _results);
                InternalCollect(dynamicGrid[_index], _center, _radius, _layerMask, _results);
            }
        }
    }

    private void InternalCollect(List<CollisionEntity> _list, Vector2 _center, float _radius, int _mask, List<IStaticCollidable> _results)
    {
        int _count = _list.Count;
        for (int i = 0; i < _count; i++)
        {
            if ((_list[i].layerBit & _mask) == 0) continue;

            float _combinedRadius = _radius + _list[i].radius;
            float _dx = _list[i].center.x - _center.x;
            float _dy = _list[i].center.y - _center.y;
            if ((_dx * _dx + _dy * _dy) <= _combinedRadius * _combinedRadius)
            {
                _results.Add(_list[i].owner);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float SqrDistancePointToSegmentOptimized(Vector2 _a, Vector2 _ab, float _invAb2, Vector2 _p)
    {
        float _apX = _p.x - _a.x;
        float _apY = _p.y - _a.y;
        float _t = (_apX * _ab.x + _apY * _ab.y) * _invAb2;
        _t = _t < 0 ? 0 : (_t > 1 ? 1 : _t);
        
        float _dx = _apX - _t * _ab.x;
        float _dy = _apY - _t * _ab.y;
        return _dx * _dx + _dy * _dy;
    }

    private void OnDrawGizmos()
    {
        if (!showDebug || staticGrid == null) return;

        int _totalCells = gridCount.x * gridCount.y;
        for (int i = 0; i < _totalCells; i++)
        {
            int _x = i % gridCount.x;
            int _y = i / gridCount.x;
            Vector3 _cellPos = new Vector3(gridOrigin.x + _x * cellSize + cellSize * 0.5f, gridOrigin.y + _y * cellSize + cellSize * 0.5f, 0);

            if (staticGrid[i].Count > 0 || dynamicGrid[i].Count > 0)
            {
                Gizmos.color = activeGridColor;
                Gizmos.DrawWireCube(_cellPos, new Vector3(cellSize, cellSize, 0));

                Gizmos.color = objectColor;
                foreach (var e in staticGrid[i]) Gizmos.DrawWireSphere(e.center, e.radius);
                foreach (var e in dynamicGrid[i]) Gizmos.DrawWireSphere(e.center, e.radius);
            }
            else if (showFullGrid)
            {
                Gizmos.color = new Color(1, 1, 1, 0.02f);
                Gizmos.DrawWireCube(_cellPos, new Vector3(cellSize, cellSize, 0));
            }
        }
    }
}
