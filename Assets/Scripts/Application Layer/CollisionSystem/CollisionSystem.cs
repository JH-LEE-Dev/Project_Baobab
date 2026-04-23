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
    public int layerBit;
    public IStaticCollidable owner;
    public int gridIndex; // 최적화: 자신이 속한 그리드 인덱스 저장
    public bool isStatic; // 최적화: 스테틱 여부 저장
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

    // Doubly Linked List in Array 구조
    private int[] staticHeads;
    private int[] dynamicHeads;
    private CollisionEntity[] entities;
    private int[] nextPointers;
    private int[] prevPointers; // 최적화: 이전 노드 추적용
    private int freeListHead;

    // 객체별 엔티티 인덱스 직접 추적 (O(1) 접근 핵심)
    private Dictionary<IStaticCollidable, int> ownerToEntityIndex;

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
        prevPointers = new int[maxEntities];

        // 프리 리스트(사용 가능한 빈 슬롯) 체인 생성
        for (int i = 0; i < maxEntities - 1; i++)
        {
            nextPointers[i] = i + 1;
            prevPointers[i] = -1;
        }
        nextPointers[maxEntities - 1] = -1;
        prevPointers[maxEntities - 1] = -1;
        freeListHead = 0;

        ownerToEntityIndex = new Dictionary<IStaticCollidable, int>(maxEntities);

        Debug.Log($"<color=green><b>[CollisionSystem]</b> Doubly Linked Grid Initialized: {gridCount.x}x{gridCount.y} cells, Max Entities: {maxEntities}.</color>");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int WorldToGridIndex(Vector2 _worldPos)
    {
        int _x = Mathf.FloorToInt((_worldPos.x - gridOrigin.x) * invCellSize);
        int _y = Mathf.FloorToInt((_worldPos.y - gridOrigin.y) * invCellSize);

        if (_x < 0 || _x >= gridCount.x || _y < 0 || _y >= gridCount.y) return -1;
        return _x + _y * gridCount.x;
    }

    public void Register(IStaticCollidable _obj, bool _isStatic = true)
    {
        // 이미 등록된 경우 안전하게 제거 후 재등록 (중복/유령 방지)
        if (ownerToEntityIndex.ContainsKey(_obj))
        {
            Unregister(_obj, _isStatic);
        }

        int _gridIndex = WorldToGridIndex(_obj.Position + _obj.Offset);
        if (_gridIndex < 0 || freeListHead == -1) return;

        // 프리 리스트에서 슬롯 하나 할당
        int _newIdx = freeListHead;
        freeListHead = nextPointers[_newIdx];

        // 데이터 채우기
        entities[_newIdx] = CreateEntity(_obj, _gridIndex, _isStatic);

        // 해당 그리드 체인의 맨 앞에 삽입 (LIFO) 및 이중 연결 설정
        int[] _heads = _isStatic ? staticHeads : dynamicHeads;
        int _oldHead = _heads[_gridIndex];

        nextPointers[_newIdx] = _oldHead;
        prevPointers[_newIdx] = -1; // 새 헤드가 됨

        if (_oldHead != -1) prevPointers[_oldHead] = _newIdx;
        _heads[_gridIndex] = _newIdx;

        // 인덱스 기록 (엔티티 인덱스를 저장)
        ownerToEntityIndex[_obj] = _newIdx;
    }

    public void Unregister(IStaticCollidable _obj, bool _isStatic = true)
    {
        // 기록된 엔티티 인덱스가 없으면 이미 제거된 것
        if (!ownerToEntityIndex.TryGetValue(_obj, out int _idx)) return;

        ref var _ent = ref entities[_idx];
        int _prev = prevPointers[_idx];
        int _next = nextPointers[_idx];

        // 1. 링크 연결 끊기 (O(1))
        if (_prev == -1) // 현재 노드가 헤드인 경우
        {
            int[] _heads = _ent.isStatic ? staticHeads : dynamicHeads;
            _heads[_ent.gridIndex] = _next;
        }
        else
        {
            nextPointers[_prev] = _next;
        }

        if (_next != -1) prevPointers[_next] = _prev;

        // 2. 슬롯을 프리 리스트로 반환
        _ent.owner = null;
        nextPointers[_idx] = freeListHead;
        prevPointers[_idx] = -1;
        freeListHead = _idx;

        // 기록 삭제
        ownerToEntityIndex.Remove(_obj);
    }

    public void ClearAll()
    {
        if (staticHeads == null || dynamicHeads == null) return;

        int _totalCells = gridCount.x * gridCount.y;
        for (int i = 0; i < _totalCells; i++)
        {
            staticHeads[i] = -1;
            dynamicHeads[i] = -1;
        }

        for (int i = 0; i < maxEntities; i++)
        {
            entities[i].owner = null;
            nextPointers[i] = i + 1;
            prevPointers[i] = -1;
        }
        nextPointers[maxEntities - 1] = -1;
        freeListHead = 0;

        ownerToEntityIndex.Clear();

        Debug.Log("<color=yellow><b>[CollisionSystem]</b> All colliders cleared and system reset.</color>");
    }

    public void UpdatePosition(IStaticCollidable _obj, Vector2 _newPos)
    {
        // 기존 엔티티 인덱스 확인
        if (!ownerToEntityIndex.TryGetValue(_obj, out int _idx)) return;

        int _newGridIdx = WorldToGridIndex(_newPos + _obj.Offset);
        ref var _ent = ref entities[_idx];

        if (_ent.gridIndex == _newGridIdx)
        {
            // 같은 셀이면 루프 없이 데이터만 즉시 갱신
            _ent.center = _newPos + _obj.Offset;
            return;
        }

        // 셀이 바뀌었으면 확실하게 제거 후 새 셀에 등록
        bool _wasStatic = _ent.isStatic;
        Unregister(_obj, _wasStatic);
        Register(_obj, _wasStatic);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private CollisionEntity CreateEntity(IStaticCollidable _obj, int _gridIdx, bool _isStatic)
    {
        return new CollisionEntity
        {
            center = _obj.Position + _obj.Offset,
            radius = _obj.Radius,
            layerBit = (1 << _obj.Layer),
            owner = _obj,
            gridIndex = _gridIdx,
            isStatic = _isStatic
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

        int _minX = Mathf.Clamp(Mathf.FloorToInt((_minX_val - gridOrigin.x) * invCellSize), 0, gridCount.x - 1);
        int _maxX = Mathf.Clamp(Mathf.FloorToInt((_maxX_val - gridOrigin.x) * invCellSize), 0, gridCount.x - 1);
        int _minY = Mathf.Clamp(Mathf.FloorToInt((_minY_val - gridOrigin.y) * invCellSize), 0, gridCount.y - 1);
        int _maxY = Mathf.Clamp(Mathf.FloorToInt((_maxY_val - gridOrigin.y) * invCellSize), 0, gridCount.y - 1);

        for (int x = _minX; x <= _maxX; x++)
        {
            for (int y = _minY; y <= _maxY; y++)
            {
                int _index = x + y * gridCount.x;
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
            ref var _ent = ref entities[_curr];
            if ((_ent.layerBit & _mask) != 0)
            {
                float _combinedRadius = _bulletRadius + _ent.radius;
                if (SqrDistancePointToSegmentOptimized(_start, _ab, _invAb2, _ent.center) < _combinedRadius * _combinedRadius)
                {
                    _hit = _ent.owner;
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

        // 1. 중심점이 위치한 셀 인덱스를 먼저 찾습니다.
        int _centerX = Mathf.FloorToInt((_center.x - gridOrigin.x) * invCellSize);
        int _centerY = Mathf.FloorToInt((_center.y - gridOrigin.y) * invCellSize);

        // 2. 반지름이 커버할 수 있는 셀의 칸 수(Span)를 계산합니다.
        // 경계선에 걸쳐 있을 때를 대비해 CeilToInt + 1로 여유 있게 범위를 잡습니다.
        int _span = Mathf.CeilToInt(_radius * invCellSize) + 1;

        int _minX = Mathf.Clamp(_centerX - _span, 0, gridCount.x - 1);
        int _maxX = Mathf.Clamp(_centerX + _span, 0, gridCount.x - 1);
        int _minY = Mathf.Clamp(_centerY - _span, 0, gridCount.y - 1);
        int _maxY = Mathf.Clamp(_centerY + _span, 0, gridCount.y - 1);

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
            ref var _ent = ref entities[_curr];
            if ((_ent.layerBit & _mask) != 0)
            {
                float _combinedRadius = _radius + _ent.radius;
                float _dx = _ent.center.x - _center.x;
                float _dy = _ent.center.y - _center.y;
                if ((_dx * _dx + _dy * _dy) <= _combinedRadius * _combinedRadius)
                {
                    _results.Add(_ent.owner);
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

            if (staticHeads[i] != -1 || dynamicHeads[i] != -1)
            {
                Gizmos.color = activeGridColor;
                Gizmos.DrawWireCube(_cellPos, new Vector3(cellSize, cellSize, 0));
                Gizmos.color = objectColor;
                DrawChain(staticHeads[i]);
                DrawChain(dynamicHeads[i]);
            }
            else if (showFullGrid)
            {
                Gizmos.color = new Color(1, 1, 1, 0.02f);
                Gizmos.DrawWireCube(_cellPos, new Vector3(cellSize, cellSize, 0));
            }
        }
    }

    private void DrawChain(int _headIdx)
    {
        int _curr = _headIdx;
        while (_curr != -1)
        {
            Gizmos.DrawWireSphere(entities[_curr].center, entities[_curr].radius);
            _curr = nextPointers[_curr];
        }
    }
}
