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
    [SerializeField] private int maxEntities = 10000; // 최대 관리 객체 수

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;
    [SerializeField] private bool showFullGrid = false;
    [SerializeField] private Color objectColor = Color.cyan;
    [SerializeField] private Color activeGridColor = new Color(0, 1, 0, 0.1f);

    // 1번 최적화: 나눗셈 연산을 피하기 위한 역수 캐싱
    private float invCellSize;

    // Linked List in Array 구조
    private int[] staticHeads;
    private int[] dynamicHeads;
    private CollisionEntity[] entities;
    private int[] nextPointers;
    private int freeListHead;

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

        // 그리드 헤드 배열 초기화 (-1은 비어있음을 의미)
        staticHeads = new int[_totalCells];
        dynamicHeads = new int[_totalCells];
        for (int i = 0; i < _totalCells; i++)
        {
            staticHeads[i] = -1;
            dynamicHeads[i] = -1;
        }

        // 엔티티 풀 및 연결 포인터 초기화
        entities = new CollisionEntity[maxEntities];
        nextPointers = new int[maxEntities];
        
        // 프리 리스트(사용 가능한 빈 슬롯) 체인 생성
        for (int i = 0; i < maxEntities - 1; i++)
        {
            nextPointers[i] = i + 1;
        }
        nextPointers[maxEntities - 1] = -1;
        freeListHead = 0;

        Debug.Log($"<color=green><b>[CollisionSystem]</b> Optimized Grid Initialized: {gridCount.x}x{gridCount.y} cells, Max Entities: {maxEntities}.</color>");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int WorldToGridIndex(Vector2 _worldPos)
    {
        int _x = (int)((_worldPos.x - gridOrigin.x) * invCellSize);
        int _y = (int)((_worldPos.y - gridOrigin.y) * invCellSize);

        if (_x < 0 || _x >= gridCount.x || _y < 0 || _y >= gridCount.y) return -1;
        return _x + _y * gridCount.x;
    }

    public void Register(IStaticCollidable _obj, bool _isStatic = true)
    {
        int _gridIndex = WorldToGridIndex(_obj.Position + _obj.Offset);
        if (_gridIndex < 0 || freeListHead == -1) return;

        // 중복 등록 방지 체크 (해당 셀의 체인 순회)
        int[] _heads = _isStatic ? staticHeads : dynamicHeads;
        int _curr = _heads[_gridIndex];
        while (_curr != -1)
        {
            if (entities[_curr].owner == _obj) return;
            _curr = nextPointers[_curr];
        }

        // 프리 리스트에서 슬롯 하나 할당
        int _newIdx = freeListHead;
        freeListHead = nextPointers[_newIdx];

        // 데이터 채우기
        entities[_newIdx] = CreateEntity(_obj);
        
        // 해당 그리드 체인의 맨 앞에 삽입 (LIFO)
        nextPointers[_newIdx] = _heads[_gridIndex];
        _heads[_gridIndex] = _newIdx;
    }

    public void Unregister(IStaticCollidable _obj, bool _isStatic = true)
    {
        int _gridIndex = WorldToGridIndex(_obj.Position + _obj.Offset);
        if (_gridIndex < 0) return;

        int[] _heads = _isStatic ? staticHeads : dynamicHeads;
        int _curr = _heads[_gridIndex];
        int _prev = -1;

        while (_curr != -1)
        {
            if (entities[_curr].owner == _obj)
            {
                // 링크 연결 끊기
                if (_prev == -1) _heads[_gridIndex] = nextPointers[_curr];
                else nextPointers[_prev] = nextPointers[_curr];

                // 슬롯을 프리 리스트로 반환
                entities[_curr].owner = null; // 참조 해제
                nextPointers[_curr] = freeListHead;
                freeListHead = _curr;
                return;
            }
            _prev = _curr;
            _curr = nextPointers[_curr];
        }
    }

    public void UpdatePosition(IStaticCollidable _obj, Vector2 _oldPos, Vector2 _newPos)
    {
        int _oldGridIdx = WorldToGridIndex(_oldPos + _obj.Offset);
        int _newGridIdx = WorldToGridIndex(_newPos + _obj.Offset);

        if (_oldGridIdx == _newGridIdx)
        {
            if (_newGridIdx >= 0)
            {
                // 같은 셀이면 데이터만 갱신
                int _curr = dynamicHeads[_newGridIdx];
                while (_curr != -1)
                {
                    if (entities[_curr].owner == _obj)
                    {
                        entities[_curr].center = _newPos + _obj.Offset;
                        break;
                    }
                    _curr = nextPointers[_curr];
                }
            }
            return;
        }

        // 셀이 바뀌었으면 이전 셀에서 직접 제거 (Unregister를 호출하면 _obj.Position 때문에 엉뚱한 셀을 뒤짐)
        if (_oldGridIdx >= 0)
        {
            int _curr = dynamicHeads[_oldGridIdx];
            int _prev = -1;
            while (_curr != -1)
            {
                if (entities[_curr].owner == _obj)
                {
                    // 링크 끊기
                    if (_prev == -1) dynamicHeads[_oldGridIdx] = nextPointers[_curr];
                    else nextPointers[_prev] = nextPointers[_curr];

                    // 프리 리스트로 반환
                    entities[_curr].owner = null;
                    nextPointers[_curr] = freeListHead;
                    freeListHead = _curr;
                    break;
                }
                _prev = _curr;
                _curr = nextPointers[_curr];
            }
        }

        // 새로운 위치에 등록
        Register(_obj, false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CollisionEntity CreateEntity(IStaticCollidable _obj)
    {
        return new CollisionEntity
        {
            center = _obj.Position + _obj.Offset,
            radius = _obj.Radius,
            layerBit = (1 << _obj.Layer),
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
                // Static 및 Dynamic 체인 순차 확인
                if (InternalCheck(staticHeads[_index], _start, _ab, _invAb2, _bulletRadius, _layerMask, out _hitObject)) return true;
                if (InternalCheck(dynamicHeads[_index], _start, _ab, _invAb2, _bulletRadius, _layerMask, out _hitObject)) return true;
            }
        }

        return false;
    }

    private bool InternalCheck(int _headIdx, Vector2 _start, Vector2 _ab, float _invAb2, float _bulletRadius, int _mask, out IStaticCollidable _hit)
    {
        _hit = null;
        int _curr = _headIdx;
        while (_curr != -1)
        {
            if ((entities[_curr].layerBit & _mask) != 0)
            {
                float _combinedRadius = _bulletRadius + entities[_curr].radius;
                if (SqrDistancePointToSegmentOptimized(_start, _ab, _invAb2, entities[_curr].center) < _combinedRadius * _combinedRadius)
                {
                    _hit = entities[_curr].owner;
                    return true;
                }
            }
            _curr = nextPointers[_curr];
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
                InternalCollect(staticHeads[_index], _center, _radius, _layerMask, _results);
                InternalCollect(dynamicHeads[_index], _center, _radius, _layerMask, _results);
            }
        }
    }

    private void InternalCollect(int _headIdx, Vector2 _center, float _radius, int _mask, List<IStaticCollidable> _results)
    {
        int _curr = _headIdx;
        while (_curr != -1)
        {
            if ((entities[_curr].layerBit & _mask) != 0)
            {
                float _combinedRadius = _radius + entities[_curr].radius;
                float _dx = entities[_curr].center.x - _center.x;
                float _dy = entities[_curr].center.y - _center.y;
                if ((_dx * _dx + _dy * _dy) <= _combinedRadius * _combinedRadius)
                {
                    _results.Add(entities[_curr].owner);
                }
            }
            _curr = nextPointers[_curr];
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
        if (!showDebug || staticHeads == null) return;

        int _totalCells = gridCount.x * gridCount.y;
        for (int i = 0; i < _totalCells; i++)
        {
            int _x = i % gridCount.x;
            int _y = i / gridCount.x;
            Vector3 _cellPos = new Vector3(gridOrigin.x + _x * cellSize + cellSize * 0.5f, gridOrigin.y + _y * cellSize + cellSize * 0.5f, 0);

            bool _hasStatic = staticHeads[i] != -1;
            bool _hasDynamic = dynamicHeads[i] != -1;

            if (_hasStatic || _hasDynamic)
            {
                Gizmos.color = activeGridColor;
                Gizmos.DrawWireCube(_cellPos, new Vector3(cellSize, cellSize, 0));

                Gizmos.color = objectColor;
                
                // Static 객체 시각화
                int _curr = staticHeads[i];
                while (_curr != -1)
                {
                    Gizmos.DrawWireSphere(entities[_curr].center, entities[_curr].radius);
                    _curr = nextPointers[_curr];
                }

                // Dynamic 객체 시각화
                _curr = dynamicHeads[i];
                while (_curr != -1)
                {
                    Gizmos.DrawWireSphere(entities[_curr].center, entities[_curr].radius);
                    _curr = nextPointers[_curr];
                }
            }
            else if (showFullGrid)
            {
                Gizmos.color = new Color(1, 1, 1, 0.02f);
                Gizmos.DrawWireCube(_cellPos, new Vector3(cellSize, cellSize, 0));
            }
        }
    }
}
