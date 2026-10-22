using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 마을 강아지(Puppy)와 밥그릇·개집을 관리한다. 강아지는 마을에만 존재하고(방생 개념),
/// 마을에 도착할 때마다 플레이어 근처의 무작위 위치에서 반긴다.
///
/// 상호작용
///   - 강아지와 밥그릇 중 플레이어에게 가까운 하나만 반응한다(아웃라인 + 머리 위 상호작용 키).
///   - 플레이어가 코어루프 기물(집·상점·원목 상자·차량 등)의 상호작용 영역 안에 있으면 그쪽이 우선이라
///     강아지/밥그릇은 반응하지 않는다.
///   - 강아지 상호작용 쿨타임은 1초. 밥그릇은 강아지가 먹고 있는 동안 상호작용할 수 없다.
///
/// 강아지는 코어루프 기물의 상호작용 영역에 웬만하면 들어가지 않는다. 목적지·따라갈 자리가 영역 안이면
/// 영역 밖으로 밀어낸 자리를 쓴다(PushOutOfAvoidZones).
/// </summary>
public class PuppyManager : MonoBehaviour
{
    private enum EInteractTarget
    {
        None,
        Puppy,
        Bowl,
    }

    // 상하좌우 이웃(섬 영역 BFS용). 대각선은 코너를 끼고 새는 것을 막으려고 쓰지 않는다.
    private static readonly Vector3Int[] RegionNeighbors =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0), new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
    };

    private const int IslandWaypointCount = 16;

    /// <summary>강아지/밥그릇 상호작용 가능 상태가 바뀔 때. TownSystem이 받아 캐릭터 위 상호작용 키를 켜고 끈다.</summary>
    public event Action<bool> InteractStateChangedEvent;

    //내부 의존성
    [Header("Prefabs")]
    [SerializeField] private Puppy puppyPrefab;
    [SerializeField] private PuppyBowl bowlPrefab;
    [SerializeField] private GameObject housePrefab;

    [Header("Placement")]
    [Tooltip("개집 자리(용광로가 있던 곳). 개집 밑면 중심이 이 위치에 놓인다.")]
    [SerializeField] private Transform houseSpawnPoint;
    [Tooltip("밥그릇 자리. 강아지가 8방향 어디서든 먹을 수 있도록 주변 한 칸이 모두 걸을 수 있는 곳이어야 한다.")]
    [SerializeField] private Transform bowlSpawnPoint;
    [SerializeField, Range(PuppyBowl.EmptyLevel, PuppyBowl.FullLevel)] private int initialBowlLevel = 1;

    [Header("Spawn")]
    [Tooltip("마을에 도착했을 때 플레이어 도착 지점에서 이 거리 범위 안의 무작위 위치에 강아지가 나타난다.")]
    [SerializeField] private Vector2 spawnDistance = new Vector2(1.5f, 4f);

    [Header("Interaction")]
    [SerializeField] private float puppyInteractRadius = 1f;
    [SerializeField] private float bowlInteractRadius = 0.7f;
    [SerializeField, Tooltip("이미 상호작용 대상이 된 쪽은 이 배율만큼 넓은 범위까지 유지한다(경계에서 깜빡임 방지).")]
    private float interactExitRadiusMultiplier = 1.25f;
    [SerializeField] private float interactCooldown = 1f;

    [Header("Avoid Zones")]
    [Tooltip("코어루프 기물 상호작용 영역에서 이만큼 더 떨어진 곳까지 피한다.")]
    [SerializeField] private float avoidMargin = 0.25f;

    //외부 의존성
    private InputManager inputManager;
    private Character character;
    private ITilemapDataProvider tilemapDataProvider;

    private Puppy puppy;
    private PuppyBowl bowl;
    private GameObject house;

    private readonly List<Collider2D> avoidZones = new List<Collider2D>(8);

    private bool bInTown = false;

    // 마을에 들어온 직후 한 프레임 미뤄 강아지를 내보낼지(EnterTown 참고)와, 그때 기준이 될 플레이어 도착 지점.
    private bool bPendingPuppySpawn = false;
    private Vector2 arrivalAnchorPosition;

    private EInteractTarget currentTarget = EInteractTarget.None;
    private bool bLastInteractState = false;
    private float lastPuppyInteractTime = -999f;
    private float lastBowlInteractTime = -999f;

    // 밥그릇과 이어진 걸을 수 있는 영역(=강아지가 다닐 수 있는 섬). 무작위 자리와 섬 일주 경로를 여기서 뽑는다.
    private bool[] regionMask;
    private int[] regionQueue;
    // 마을 그리드(70x68 = 4760칸)를 다 담아도 다시 할당되지 않을 만큼 잡아 둔다.
    private readonly List<Vector3Int> regionCells = new List<Vector3Int>(5000);
    private readonly List<Vector2> islandWaypoints = new List<Vector2>(IslandWaypointCount);

    public PuppyBowl Bowl => bowl;
    public IReadOnlyList<Vector2> IslandWaypoints => islandWaypoints;
    public Vector2 IslandCenter { get; private set; }

    public bool HasOwner => null != character && true == character.gameObject.activeInHierarchy && false == character.bRide && false == character.bDead;
    public Vector2 OwnerPosition => null != character ? (Vector2)character.transform.position : Vector2.zero;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;

        // 메인메뉴에서 곧바로 던전으로 가는 경로에서도 참조가 비지 않도록 생성은 여기서 한 번만 하고,
        // 마을에 들어올 때까지 꺼 둔다.
        house = Instantiate(housePrefab, houseSpawnPoint.position, Quaternion.identity, transform);
        SortingGroup _houseSortingGroup = house.GetComponentInChildren<SortingGroup>();
        if (null != _houseSortingGroup)
            _houseSortingGroup.sortingOrder = -Mathf.RoundToInt(house.transform.position.y * 100f);

        bowl = Instantiate(bowlPrefab, bowlSpawnPoint.position, Quaternion.identity, transform);
        bowl.Initialize(initialBowlLevel);
        bowl.RefreshSortingOrder();

        puppy = Instantiate(puppyPrefab, bowlSpawnPoint.position, Quaternion.identity, transform);
        puppy.Initialize(this);

        SetObjectsActive(false);
        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
    }

    /// <summary>
    /// 강아지가 웬만하면 들어가지 않을 코어루프 기물의 상호작용 영역을 등록한다.
    /// 플레이어가 이 영역 안에 있는 동안에는 강아지/밥그릇 상호작용도 막힌다(코어루프 기물 우선).
    /// </summary>
    public void ClearAvoidZones()
    {
        avoidZones.Clear();
    }

    public void AddAvoidZone(Collider2D _zone)
    {
        if (null != _zone && false == avoidZones.Contains(_zone))
            avoidZones.Add(_zone);
    }

    /// <summary>
    /// 마을에 도착했을 때(TownSystem.StartTownSystem) 호출한다. 마을 Grid와 길찾기 데이터가 준비된 뒤여야 한다.
    /// _arrivalAnchor는 플레이어가 나타나는 지점으로, 강아지가 그 근처 무작위 위치에서 반긴다.
    /// </summary>
    public void EnterTown(ITilemapDataProvider _tilemapDataProvider, Transform _arrivalAnchor)
    {
        // 길찾기 데이터 없이는 강아지가 움직일 수 없으므로 이번 방문엔 내보내지 않는다.
        if (null == _tilemapDataProvider) return;

        tilemapDataProvider = _tilemapDataProvider;
        bInTown = true;

        house.SetActive(true);
        bowl.gameObject.SetActive(true);

        BuildRegion();

        // 강아지 자리와 섬 일주 경로는 다음 Update에서 정한다. 전리품 기둥은 이 호출 뒤에 세워져
        // (GameInstaller: StartTownSystem → SpawnAcquiredPillars) 그 영역이 아직 회피 대상에 없기 때문이다.
        arrivalAnchorPosition = null != _arrivalAnchor ? (Vector2)_arrivalAnchor.position : (Vector2)bowlSpawnPoint.position;
        bPendingPuppySpawn = true;
    }

    private void SpawnPuppy()
    {
        bPendingPuppySpawn = false;

        BuildIslandWaypoints();

        if (false == TryGetRandomPoint(arrivalAnchorPosition, spawnDistance.x, spawnDistance.y, out Vector2 _spawn))
            _spawn = GetEatPoint(arrivalAnchorPosition, new Vector2(0.6f, 0.4f));

        puppy.gameObject.SetActive(true);
        puppy.EnterTown(_spawn, tilemapDataProvider);
    }

    /// <summary>던전으로 떠나거나(로그라이크 포함) 마을 Grid가 사라질 때 호출한다.</summary>
    public void ExitTown()
    {
        if (false == bInTown) return;

        bInTown = false;
        bPendingPuppySpawn = false;
        puppy.ExitTown();
        ApplyInteractTarget(EInteractTarget.None);
        SetObjectsActive(false);
    }

    private void SetObjectsActive(bool _bActive)
    {
        house.SetActive(_bActive);
        bowl.gameObject.SetActive(_bActive);
        puppy.gameObject.SetActive(_bActive);
    }

    private void BindEvents()
    {
        if (null == inputManager || null == inputManager.inputReader) return;

        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;
    }

    private void ReleaseEvents()
    {
        if (null != inputManager && null != inputManager.inputReader)
            inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
    }

    // ───────────────────────────── 상호작용 ─────────────────────────────

    private void InteractionKeyPressed()
    {
        if (false == bInTown) return;

        // 대상은 지난 프레임 Update에서 정한 것이라, 플레이어가 바로 이번 프레임에 코어루프 기물 영역에 들어섰다면
        // 그 기물과 함께 반응해 버린다(기물은 트리거로 먼저 알아챈다). 누른 순간 한 번 더 확인한다.
        if (EInteractTarget.None == currentTarget || false == HasOwner || true == IsOwnerInAvoidZone()) return;

        switch (currentTarget)
        {
            case EInteractTarget.Puppy:
                if (Time.time - lastPuppyInteractTime < interactCooldown) return;

                lastPuppyInteractTime = Time.time;
                puppy.Interact();
                break;

            case EInteractTarget.Bowl:
                if (true == puppy.IsEating) return;
                if (Time.time - lastBowlInteractTime < interactCooldown) return;

                lastBowlInteractTime = Time.time;
                bowl.Fill();
                puppy.NotifyBowlFilled();
                break;
        }
    }

    private void UpdateInteractTarget()
    {
        EInteractTarget _target = EInteractTarget.None;

        if (true == HasOwner && false == IsOwnerInAvoidZone())
        {
            Vector2 _owner = OwnerPosition;
            float _bestDistance = float.MaxValue;

            if (PuppyBehavior.None != puppy.CurrentBehavior)
            {
                float _radius = puppyInteractRadius * (EInteractTarget.Puppy == currentTarget ? interactExitRadiusMultiplier : 1f);
                float _distance = ((Vector2)puppy.transform.position - _owner).magnitude;
                if (_distance <= _radius)
                {
                    _target = EInteractTarget.Puppy;
                    _bestDistance = _distance;
                }
            }

            if (false == puppy.IsEating)
            {
                float _radius = bowlInteractRadius * (EInteractTarget.Bowl == currentTarget ? interactExitRadiusMultiplier : 1f);
                float _distance = ((Vector2)bowl.transform.position - _owner).magnitude;
                if (_distance <= _radius && _distance < _bestDistance)
                    _target = EInteractTarget.Bowl;
            }
        }

        ApplyInteractTarget(_target);
    }

    private void ApplyInteractTarget(EInteractTarget _target)
    {
        if (_target == currentTarget) return;

        currentTarget = _target;
        puppy.SetOutline(EInteractTarget.Puppy == _target);
        bowl.SetOutline(EInteractTarget.Bowl == _target);

        // 강아지 ↔ 밥그릇으로 대상만 바뀔 때는 상호작용 키를 다시 띄우지 않는다.
        bool _bCanInteract = EInteractTarget.None != _target;
        if (_bCanInteract == bLastInteractState) return;

        bLastInteractState = _bCanInteract;
        InteractStateChangedEvent?.Invoke(_bCanInteract);
    }

    // 코어루프 기물 우선: 플레이어 콜라이더가 그 상호작용 영역에 걸쳐 있으면 강아지/밥그릇은 반응하지 않는다.
    private bool IsOwnerInAvoidZone()
    {
        Collider2D _ownerCollider = character.col;
        if (null == _ownerCollider) return false;

        for (int i = 0; i < avoidZones.Count; i++)
        {
            Collider2D _zone = avoidZones[i];
            if (false == IsZoneActive(_zone)) continue;

            if (true == _zone.Distance(_ownerCollider).isOverlapped)
                return true;
        }

        return false;
    }

    // ───────────────────────────── 강아지가 쓰는 위치 질의 ─────────────────────────────

    public bool IsWalkablePoint(Vector2 _point)
    {
        if (null == tilemapDataProvider) return false;

        return tilemapDataProvider.IsWalkable(tilemapDataProvider.WorldToCell(_point));
    }

    /// <summary>
    /// _point가 막힌 칸이면 주변 _maxRing칸 안에서 가장 가까운 걸을 수 있는 칸의 자리를 찾는다.
    /// 주인이 길찾기 기준으로는 막힌 칸(나무·차량 발밑 옆 등)에 서 있을 때 강아지가 그 옆까지 갈 길을 찾는 데 쓴다.
    /// </summary>
    public bool TryGetNearestWalkablePoint(Vector2 _point, int _maxRing, out Vector2 _result)
    {
        _result = _point;
        if (null == tilemapDataProvider) return false;

        Vector3Int _center = tilemapDataProvider.WorldToCell(_point);
        if (true == tilemapDataProvider.IsWalkable(_center))
        {
            _result = tilemapDataProvider.CellToWorld(_center);
            return true;
        }

        float _bestSqr = float.MaxValue;
        bool _bFound = false;
        for (int _ring = 1; _ring <= _maxRing && false == _bFound; _ring++)
        {
            for (int _x = -_ring; _x <= _ring; _x++)
            {
                for (int _y = -_ring; _y <= _ring; _y++)
                {
                    if (Mathf.Abs(_x) != _ring && Mathf.Abs(_y) != _ring) continue;

                    Vector3Int _cell = _center + new Vector3Int(_x, _y, 0);
                    if (false == tilemapDataProvider.IsWalkable(_cell)) continue;

                    Vector2 _world = tilemapDataProvider.CellToWorld(_cell);
                    float _sqr = (_world - _point).sqrMagnitude;
                    if (_sqr < _bestSqr)
                    {
                        _bestSqr = _sqr;
                        _result = _world;
                        _bFound = true;
                    }
                }
            }
        }

        return _bFound;
    }

    /// <summary>
    /// 강아지가 주인에게 갈 때 향할 자리. 주인이 코어루프 기물 상호작용 영역 안에 있으면 그 영역 바깥 가장자리에서
    /// 기다린다(기획 규칙 4). 기다릴 자리는 강아지(_from) 쪽 가장자리를 우선으로 하고, 그 자리가 건물 같은 걸을 수
    /// 없는 칸이면 가장자리를 따라 돌며 가장 가까운 걸을 수 있는 자리를 찾는다. 주인 위치에서 바깥으로 밀어내기만
    /// 하면 그 자리가 집 건물 안쪽처럼 갈 수 없는 곳이 되어, 강아지가 멈추거나 영역 안으로 파고들었다.
    /// </summary>
    public Vector2 GetOwnerGoal(Vector2 _from)
    {
        Vector2 _owner = OwnerPosition;

        for (int i = 0; i < avoidZones.Count; i++)
        {
            Collider2D _zone = avoidZones[i];
            if (false == IsZoneActive(_zone)) continue;

            GetZoneCircle(_zone, out Vector2 _center, out float _radius);
            _radius += avoidMargin;
            if ((_owner - _center).sqrMagnitude >= _radius * _radius) continue;

            Vector2 _direction = _from - _center;
            if (_direction.sqrMagnitude < 0.0001f) _direction = _owner - _center;

            return FindWalkableZoneEdge(_center, _radius, _direction);
        }

        return _owner;
    }

    /// <summary>
    /// _point가 코어루프 기물 상호작용 영역 안이면, 그 영역 밖의 걸을 수 있는 가장 가까운 가장자리 자리를 돌려준다.
    /// 강아지가 멈춰 있는 행동(바라보기·앉기·자기 등)을 영역 안에서 시작하지 않도록 먼저 걸어 나갈 자리를 정할 때 쓴다.
    /// </summary>
    public bool TryGetAvoidZoneExit(Vector2 _point, out Vector2 _exit)
    {
        for (int i = 0; i < avoidZones.Count; i++)
        {
            Collider2D _zone = avoidZones[i];
            if (false == IsZoneActive(_zone)) continue;

            GetZoneCircle(_zone, out Vector2 _center, out float _radius);
            _radius += avoidMargin;
            if ((_point - _center).sqrMagnitude >= _radius * _radius) continue;

            _exit = FindWalkableZoneEdge(_center, _radius, _point - _center);
            return true;
        }

        _exit = _point;
        return false;
    }

    // 영역 가장자리(_center에서 _radius) 위에서 _direction 쪽을 우선으로, 양옆으로 22.5도씩 돌며
    // 걸을 수 있고 다른 영역에도 걸리지 않는 자리를 찾는다. 가장자리가 집 건물 안쪽처럼 막혀 있을 수 있어서다.
    private Vector2 FindWalkableZoneEdge(Vector2 _center, float _radius, Vector2 _direction)
    {
        const int SearchSteps = 8;
        const float SearchStepAngle = Mathf.PI / 8f;

        if (_direction.sqrMagnitude < 0.0001f) _direction = Vector2.down;

        float _baseAngle = Mathf.Atan2(_direction.y, _direction.x);
        for (int _step = 0; _step <= SearchSteps; _step++)
        {
            for (int _sign = 1; _sign >= -1; _sign -= 2)
            {
                float _angle = _baseAngle + _sign * _step * SearchStepAngle;
                Vector2 _candidate = _center + new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle)) * _radius;
                if (true == IsWalkablePoint(_candidate) && false == IsInsideAvoidZone(_candidate))
                    return _candidate;

                if (0 == _step) break;
            }
        }

        return _center + _direction.normalized * _radius;
    }

    /// <summary>_point가 코어루프 기물 상호작용 영역 안이면 영역 중심에서 바깥으로 밀어낸 자리를 돌려준다.</summary>
    public Vector2 PushOutOfAvoidZones(Vector2 _point)
    {
        for (int i = 0; i < avoidZones.Count; i++)
        {
            Collider2D _zone = avoidZones[i];
            if (false == IsZoneActive(_zone)) continue;

            GetZoneCircle(_zone, out Vector2 _center, out float _radius);
            _radius += avoidMargin;

            Vector2 _offset = _point - _center;
            if (_offset.sqrMagnitude >= _radius * _radius) continue;

            Vector2 _direction = _offset.sqrMagnitude > 0.0001f ? _offset.normalized : Vector2.down;
            _point = _center + _direction * _radius;
        }

        return _point;
    }

    public bool IsInsideAvoidZone(Vector2 _point)
    {
        for (int i = 0; i < avoidZones.Count; i++)
        {
            Collider2D _zone = avoidZones[i];
            if (false == IsZoneActive(_zone)) continue;

            GetZoneCircle(_zone, out Vector2 _center, out float _radius);
            _radius += avoidMargin;

            if ((_point - _center).sqrMagnitude < _radius * _radius)
                return true;
        }

        return false;
    }

    /// <summary>
    /// _origin에서 [_minDistance, _maxDistance] 거리 안의, 섬 영역에 속하고 코어루프 기물 영역 밖인 무작위 자리.
    /// </summary>
    public bool TryGetRandomPoint(Vector2 _origin, float _minDistance, float _maxDistance, out Vector2 _point)
    {
        const int MaxTries = 40;

        for (int i = 0; i < MaxTries; i++)
        {
            float _angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float _distance = UnityEngine.Random.Range(_minDistance, _maxDistance);

            // 아이소메트릭 바닥 기준 거리라 화면 세로는 절반이다.
            Vector2 _candidate = _origin + new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle) * 0.5f) * _distance;
            if (false == IsInRegion(_candidate)) continue;

            _point = tilemapDataProvider.CellToWorld(tilemapDataProvider.WorldToCell(_candidate));
            if (true == IsInsideAvoidZone(_point)) continue;

            return true;
        }

        _point = _origin;
        return false;
    }

    /// <summary>
    /// 밥그릇 둘레 8방향 자리 중 걸을 수 있고 _from에서 가장 가까운 곳. 강아지는 여기 서서 밥그릇을 바라보며 먹는다.
    /// </summary>
    public Vector2 GetEatPoint(Vector2 _from, Vector2 _offset)
    {
        Vector2 _bowl = bowl.transform.position;
        Vector2 _best = _bowl + new Vector2(0f, -_offset.y);
        float _bestSqr = float.MaxValue;

        for (int i = 0; i < 8; i++)
        {
            float _angle = i * Mathf.PI * 0.25f;
            Vector2 _candidate = _bowl + new Vector2(Mathf.Cos(_angle) * _offset.x, Mathf.Sin(_angle) * _offset.y);
            if (false == IsWalkablePoint(_candidate) || true == IsInsideAvoidZone(_candidate)) continue;

            float _sqr = (_candidate - _from).sqrMagnitude;
            if (_sqr < _bestSqr)
            {
                _bestSqr = _sqr;
                _best = _candidate;
            }
        }

        return _best;
    }

    private static bool IsZoneActive(Collider2D _zone)
    {
        return null != _zone && true == _zone.enabled && true == _zone.gameObject.activeInHierarchy;
    }

    private static void GetZoneCircle(Collider2D _zone, out Vector2 _center, out float _radius)
    {
        if (_zone is CircleCollider2D _circle)
        {
            Vector3 _scale = _circle.transform.lossyScale;
            _center = _circle.transform.TransformPoint(_circle.offset);
            _radius = _circle.radius * Mathf.Max(Mathf.Abs(_scale.x), Mathf.Abs(_scale.y));
            return;
        }

        Bounds _bounds = _zone.bounds;
        _center = _bounds.center;
        _radius = Mathf.Max(_bounds.extents.x, _bounds.extents.y);
    }

    // ───────────────────────────── 섬 영역 / 일주 경로 ─────────────────────────────

    private bool IsInRegion(Vector2 _point)
    {
        if (null == tilemapDataProvider || null == regionMask) return false;

        Vector3Int _cell = tilemapDataProvider.WorldToCell(_point);
        int _width = tilemapDataProvider.GridWidth;
        if (_cell.x < 0 || _cell.x >= _width || _cell.y < 0 || _cell.y >= tilemapDataProvider.GridHeight) return false;

        return regionMask[_cell.x + _cell.y * _width];
    }

    // 밥그릇 칸에서 시작해 걸을 수 있는 칸을 BFS로 넓혀 강아지가 다닐 수 있는 섬 영역을 만든다.
    private void BuildRegion()
    {
        regionCells.Clear();
        if (null == tilemapDataProvider) return;

        int _width = tilemapDataProvider.GridWidth;
        int _height = tilemapDataProvider.GridHeight;
        int _size = _width * _height;

        if (null == regionMask || regionMask.Length != _size)
        {
            regionMask = new bool[_size];
            regionQueue = new int[_size];
        }
        else
        {
            Array.Clear(regionMask, 0, _size);
        }

        Vector3Int _start = tilemapDataProvider.WorldToCell(bowlSpawnPoint.position);
        if (_start.x < 0 || _start.x >= _width || _start.y < 0 || _start.y >= _height) return;
        if (false == tilemapDataProvider.IsWalkable(_start)) return;

        int _head = 0;
        int _tail = 0;
        int _startIndex = _start.x + _start.y * _width;
        regionMask[_startIndex] = true;
        regionQueue[_tail++] = _startIndex;

        while (_head < _tail)
        {
            int _index = regionQueue[_head++];
            Vector3Int _cell = new Vector3Int(_index % _width, _index / _width, 0);
            regionCells.Add(_cell);

            for (int i = 0; i < RegionNeighbors.Length; i++)
            {
                Vector3Int _next = _cell + RegionNeighbors[i];
                if (_next.x < 0 || _next.x >= _width || _next.y < 0 || _next.y >= _height) continue;

                int _nextIndex = _next.x + _next.y * _width;
                if (true == regionMask[_nextIndex]) continue;
                if (false == tilemapDataProvider.IsWalkable(_next)) continue;

                regionMask[_nextIndex] = true;
                regionQueue[_tail++] = _nextIndex;
            }
        }
    }

    /// <summary>
    /// 섬 일주(4)에 쓸 웨이포인트. 섬 영역의 무게중심에서 16방향으로 뻗어 나가 영역이 끝나는 거리의 70% 지점을
    /// 각도 순서대로 담는다. 물가에 너무 붙지 않고, 코어루프 기물 영역에 걸리는 지점은 뺀다.
    /// </summary>
    private void BuildIslandWaypoints()
    {
        islandWaypoints.Clear();
        if (0 == regionCells.Count) return;

        Vector2 _sum = Vector2.zero;
        for (int i = 0; i < regionCells.Count; i++)
        {
            _sum += (Vector2)tilemapDataProvider.CellToWorld(regionCells[i]);
        }

        IslandCenter = _sum / regionCells.Count;

        const float Step = 0.25f;
        const float MaxDistance = 60f;
        const float InsideRatio = 0.7f;

        for (int i = 0; i < IslandWaypointCount; i++)
        {
            float _angle = i * Mathf.PI * 2f / IslandWaypointCount;
            Vector2 _direction = new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle) * 0.5f);

            // 중심에서 바깥으로 걸어가며 영역 안에 처음 들어선 거리와 처음 빠져나간 거리를 찾는다.
            float _enter = -1f;
            float _exit = -1f;
            for (float _r = 0f; _r <= MaxDistance; _r += Step)
            {
                bool _bInside = IsInRegion(IslandCenter + _direction * _r);
                if (true == _bInside && _enter < 0f) _enter = _r;
                if (false == _bInside && 0f <= _enter)
                {
                    _exit = _r;
                    break;
                }
            }

            if (_enter < 0f || _exit < 0f) continue;

            Vector2 _point = IslandCenter + _direction * (_enter + (_exit - _enter) * InsideRatio);
            if (false == IsInRegion(_point)) continue;

            _point = tilemapDataProvider.CellToWorld(tilemapDataProvider.WorldToCell(_point));
            if (true == IsInsideAvoidZone(_point)) continue;

            int _last = islandWaypoints.Count - 1;
            if (0 <= _last && (islandWaypoints[_last] - _point).sqrMagnitude < 0.01f) continue;

            islandWaypoints.Add(_point);
        }
    }

    private void Update()
    {
        if (false == bInTown) return;

        if (true == bPendingPuppySpawn)
            SpawnPuppy();

        UpdateInteractTarget();
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }
}
