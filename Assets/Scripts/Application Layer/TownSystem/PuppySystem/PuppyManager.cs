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

    private const int IslandWaypointCapacity = 16;

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

    [Header("Run Around Island (4)")]
    [Tooltip("섬 일주 경로. 이 순서대로(또는 거꾸로) 이어 달린다. 연못을 감싸 돌며 나무 다리를 건너도록 찍어 둔다.")]
    [SerializeField] private Transform[] islandLoopPoints;

    [Header("Spawn")]
    [Tooltip("마을에 도착했을 때 플레이어 도착 지점에서 이 거리 범위 안의 무작위 위치에 강아지가 나타난다.")]
    [SerializeField] private Vector2 spawnDistance = new Vector2(1.5f, 4f);

    [Header("Interaction")]
    [SerializeField] private float puppyInteractRadius = 1f;
    [SerializeField] private float bowlInteractRadius = 0.7f;
    [SerializeField, Tooltip("이미 상호작용 대상이 된 쪽은 이 배율만큼 넓은 범위까지 유지한다(경계에서 깜빡임 방지).")]
    private float interactExitRadiusMultiplier = 1.25f;
    [SerializeField] private float interactCooldown = 1f;

    //외부 의존성
    private InputManager inputManager;
    private Character character;
    private ITilemapDataProvider tilemapDataProvider;

    // 마을 길찾기 데이터에 나무 다리 칸을 더한 강아지 전용 길찾기 데이터. 마을 Grid가 바뀔 때마다 다리 칸을 다시 찾는다.
    private PuppyTilemapDataProvider puppyTilemapDataProvider;

    private Puppy puppy;
    private PuppyBowl bowl;
    private GameObject house;

    // 코어루프 기물의 상호작용 영역. 플레이어가 이 안에 있으면 강아지/밥그릇보다 그 기물이 우선한다.
    private readonly List<Collider2D> priorityZones = new List<Collider2D>(8);

    private bool bInTown = false;

    private EInteractTarget currentTarget = EInteractTarget.None;
    private bool bLastInteractState = false;
    private float lastPuppyInteractTime = -999f;
    private float lastBowlInteractTime = -999f;

    // 밥그릇과 이어진 걸을 수 있는 영역(=강아지가 다닐 수 있는 섬). 무작위 자리를 여기서 뽑는다.
    private bool[] regionMask;
    private int[] regionQueue;
    private readonly List<Vector2> islandWaypoints = new List<Vector2>(IslandWaypointCapacity);

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
    /// 코어루프 기물의 상호작용 영역을 등록한다. 플레이어가 이 영역 안에 있는 동안에는 강아지/밥그릇이
    /// 반응하지 않는다(코어루프 기물 우선). 강아지의 이동에는 영향을 주지 않는다.
    /// </summary>
    public void ClearPriorityZones()
    {
        priorityZones.Clear();
    }

    public void AddPriorityZone(Collider2D _zone)
    {
        if (null != _zone && false == priorityZones.Contains(_zone))
            priorityZones.Add(_zone);
    }

    /// <summary>
    /// 마을에 도착했을 때(TownSystem.StartTownSystem) 호출한다. 마을 Grid와 길찾기 데이터가 준비된 뒤여야 한다.
    /// _arrivalAnchor는 플레이어가 나타나는 지점으로, 강아지가 그 근처 무작위 위치에서 반긴다.
    /// </summary>
    public void EnterTown(ITilemapDataProvider _tilemapDataProvider, TownTileManager _townTileManager, Transform _arrivalAnchor)
    {
        // 길찾기 데이터 없이는 강아지가 움직일 수 없으므로 이번 방문엔 내보내지 않는다.
        if (null == _tilemapDataProvider) return;

        puppyTilemapDataProvider = new PuppyTilemapDataProvider(_tilemapDataProvider);
        puppyTilemapDataProvider.RefreshBridgeCells(_townTileManager);
        tilemapDataProvider = puppyTilemapDataProvider;
        bInTown = true;

        SetObjectsActive(true);

        BuildRegion();
        BuildIslandWaypoints();

        Vector2 _anchor = null != _arrivalAnchor ? (Vector2)_arrivalAnchor.position : (Vector2)bowlSpawnPoint.position;
        if (false == TryGetRandomPoint(_anchor, spawnDistance.x, spawnDistance.y, out Vector2 _spawn))
            _spawn = GetEatPoint(_anchor, new Vector2(0.6f, 0.4f));

        puppy.EnterTown(_spawn, tilemapDataProvider);
    }

    /// <summary>던전으로 떠나거나(로그라이크 포함) 마을 Grid가 사라질 때 호출한다.</summary>
    public void ExitTown()
    {
        if (false == bInTown) return;

        bInTown = false;
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
        if (EInteractTarget.None == currentTarget || false == HasOwner || true == IsOwnerInPriorityZone()) return;

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

        if (true == HasOwner && false == IsOwnerInPriorityZone())
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
    private bool IsOwnerInPriorityZone()
    {
        Collider2D _ownerCollider = character.col;
        if (null == _ownerCollider) return false;

        for (int i = 0; i < priorityZones.Count; i++)
        {
            Collider2D _zone = priorityZones[i];
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
    /// _origin에서 [_minDistance, _maxDistance] 거리 안의, 섬 영역에 속하는 무작위 자리.
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
            if (false == IsWalkablePoint(_candidate)) continue;

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
    /// 섬 일주(4)에 쓸 웨이포인트. 기획에서 연못을 감싸 돌며 나무 다리까지 건너기를 원해서, 자동으로 계산하지 않고
    /// GameInstaller에 찍어 둔 경로(islandLoopPoints)를 그대로 쓴다. 각도 누적의 기준점은 경로의 무게중심이다.
    /// </summary>
    private void BuildIslandWaypoints()
    {
        islandWaypoints.Clear();
        if (null == islandLoopPoints) return;

        Vector2 _sum = Vector2.zero;
        for (int i = 0; i < islandLoopPoints.Length; i++)
        {
            if (null == islandLoopPoints[i]) continue;

            Vector2 _point = islandLoopPoints[i].position;
            islandWaypoints.Add(_point);
            _sum += _point;
        }

        if (0 == islandWaypoints.Count) return;

        IslandCenter = _sum / islandWaypoints.Count;

        // 강아지는 "다음 번호 = 반시계 방향"으로 보고 돈 각도를 쌓으므로, 경로를 시계 방향으로 찍었다면 뒤집는다.
        // (아이소메트릭 바닥 기준이라 세로를 두 배로 펴서 넓이 부호를 잰다)
        float _signedArea = 0f;
        for (int i = 0; i < islandWaypoints.Count; i++)
        {
            Vector2 _a = islandWaypoints[i];
            Vector2 _b = islandWaypoints[(i + 1) % islandWaypoints.Count];
            _signedArea += _a.x * (_b.y * 2f) - _b.x * (_a.y * 2f);
        }

        if (_signedArea < 0f)
            islandWaypoints.Reverse();
    }

    private void Update()
    {
        if (false == bInTown) return;

        UpdateInteractTarget();
    }

    private void OnDestroy()
    {
        ReleaseEvents();
    }
}
