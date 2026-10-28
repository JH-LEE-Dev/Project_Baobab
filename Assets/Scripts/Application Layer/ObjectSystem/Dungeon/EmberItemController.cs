using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 4스테이지(MagmaForest) 불씨의 생성·소멸을 맡는다. 주운 뒤 과열 수치를 올리는 것은 InDungeonObjectManager가 한다.
///
/// 불씨가 생기는 경로는 셋이다(수치는 전부 인스펙터에서 조정).
/// 1) 입장 배치: 나무가 다 심어진 직후, 나무 1그루당 평균 initialEmbersPerTree개를 그 나무 옆 빈 칸에 놓는다.
/// 2) 자연 생성: naturalSpawnInterval초마다 캐릭터 주변(거의 한 화면, naturalSpawnHalfExtents)의 빈 칸 하나에 놓는다.
/// 3) 벌목 드랍: 나무가 쓰러질 때와 희귀나무 등급이 바뀔 때마다 dropCountMin~Max개를 튀어나오게 한다.
///    이렇게 떨어진 불씨만 droppedLifetime초 뒤 사라진다(1, 2는 남아 있다).
/// 과열이 최대면 2, 3은 더 생기지 않는다. 이미 바닥에 있는 불씨는 계속 주울 수 있다(효과만 없다).
///
/// 이펙트 담당 연결 지점: 이 오브젝트의 VFXComponent에 이펙트를 등록하고 아래 태그를 채우면 된다(비어 있으면 재생하지 않는다).
/// </summary>
public class EmberItemController : MonoBehaviour
{
    public event Action<EmberItem> EmberAcquiredEvent;

    [SerializeField] private EmberItem emberItemPrefab;

    [Header("적용 맵")]
    [SerializeField] private List<MapType> enabledMapTypes = new List<MapType> { MapType.MagmaForest };

    [Header("획득")]
    [Tooltip("불씨 1개를 주울 때 오르는 과열 수치")]
    [SerializeField] private float overheatPerEmber = 1f;

    [Header("입장 배치")]
    [Tooltip("나무 1그루당 평균 불씨 수. 1보다 작으면 그 확률로 1개, 크면 정수부만큼 + 소수부 확률로 1개 더")]
    [SerializeField, Min(0f)] private float initialEmbersPerTree = 0.25f;

    [Header("자연 생성")]
    [SerializeField, Min(0.1f)] private float naturalSpawnInterval = 5f;
    [Tooltip("캐릭터를 중심으로 자연 생성 칸을 고르는 범위(월드 단위 반폭·반높이). 기준 화면은 10 x 5.6")]
    [SerializeField] private Vector2 naturalSpawnHalfExtents = new Vector2(9f, 5f);
    [Tooltip("빈 칸을 찾을 때 시도하는 횟수. 다 실패하면 이번 주기는 건너뛴다")]
    [SerializeField, Min(1)] private int naturalSpawnAttempts = 20;

    [Header("벌목 드랍")]
    [SerializeField, Min(0)] private int dropCountMin = 2;
    [SerializeField, Min(0)] private int dropCountMax = 3;
    [Tooltip("벌목으로 떨어진 불씨가 사라지기까지의 시간(초)")]
    [SerializeField] private float droppedLifetime = 20f;

    [Header("이펙트 연결 (이펙트 담당)")]
    [Tooltip("바닥에 놓여 있는 동안 붙는 루프 이펙트")]
    [SerializeField] private string groundLoopVfxTag = "";
    [Tooltip("주운 순간 캐릭터 자리에서 한 번")]
    [SerializeField] private string acquireVfxTag = "";
    [Tooltip("수명이 다해 사라지는 순간 그 자리에서 한 번")]
    [SerializeField] private string expireVfxTag = "";

    private IObjectPool<EmberItem> emberPool;
    // 꺼내 쓰는 중인 불씨 전부(ClearAll용)와, 그중 매 프레임 갱신이 필요한 것(움직이는 중·착지 연출 중·수명이 도는 중).
    // 바닥에 가만히 놓인 수명 없는 불씨(입장 배치·자연 생성)는 흡입이 걸릴 때까지 갱신 목록에서 빠진다.
    private readonly List<EmberItem> activeEmbers = new List<EmberItem>(128);
    private readonly List<EmberItem> updatingEmbers = new List<EmberItem>(64);
    private readonly List<EmberItem> cleanupList = new List<EmberItem>(128);
    // 칸마다 바닥에 놓인 불씨 수. 입장 배치·자연 생성이 불씨가 있는 칸에 겹쳐 놓지 않게 한다.
    // 벌목 드랍은 칸을 가리지 않고 흩뿌려 한 칸에 여럿이 놓일 수 있으므로 있고 없음이 아니라 개수를 센다.
    private readonly Dictionary<Vector3Int, int> groundEmberCounts = new Dictionary<Vector3Int, int>();

    private ICharacter character;
    private IEnvironmentProvider environmentProvider;
    private VFXComponent vfxComponent;

    // 이번 원정에서 불씨가 나오는지(맵 + 로그라이크 기믹 끔 여부). BeginRun에서 정한다.
    private bool bEnabledThisRun;
    // 자연 생성·드랍을 지금 해도 되는지(원정 종료 중·사망·과열 최대면 false). InDungeonObjectManager가 넘겨준다.
    private Func<bool> canSpawnMore;
    private float naturalSpawnTimer;

    private static readonly Vector3Int[] NeighborOffsets =
    {
        new Vector3Int(-1, -1, 0), new Vector3Int(0, -1, 0), new Vector3Int(1, -1, 0),
        new Vector3Int(-1, 0, 0), new Vector3Int(1, 0, 0),
        new Vector3Int(-1, 1, 0), new Vector3Int(0, 1, 0), new Vector3Int(1, 1, 0),
    };
    private readonly List<Vector3Int> neighborBuffer = new List<Vector3Int>(8);

    public bool bEnabled => bEnabledThisRun;

    public void Initialize(ICharacter _character, IEnvironmentProvider _environmentProvider)
    {
        character = _character;
        environmentProvider = _environmentProvider;

        vfxComponent = GetComponent<VFXComponent>();
        if (vfxComponent != null) vfxComponent.Initialize();

        emberPool = new ObjectPool<EmberItem>(
            createFunc: CreateEmberItem,
            actionOnGet: OnGetEmberItem,
            actionOnRelease: OnReleaseEmberItem,
            actionOnDestroy: OnDestroyEmberItem,
            collectionCheck: PoolSettings.CollectionCheck,
            defaultCapacity: 32,
            maxSize: 512
        );
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;

        for (int i = 0; i < activeEmbers.Count; i++)
        {
            activeEmbers[i].SetCharacter(_character);
        }
    }

    /// <summary>
    /// 새 원정이 시작될 때(나무 초기 스폰 직전) 부른다. 이전 원정의 불씨를 치우고 이번 맵에서 불씨가 나오는지 정한다.
    /// </summary>
    public void BeginRun(MapType _mapType, bool _bStageGimmicksDisabled, Func<bool> _canSpawnMore)
    {
        ClearAll();

        bEnabledThisRun = emberItemPrefab != null && false == _bStageGimmicksDisabled && enabledMapTypes.Contains(_mapType);
        canSpawnMore = _canSpawnMore;
        naturalSpawnTimer = 0f;
    }

    /// <summary>나무 초기 스폰이 끝난 직후 부른다. 나무 옆 빈 칸에 불씨를 드문드문 놓는다.</summary>
    public void SpawnInitialEmbers(IReadOnlyList<TreeObj> _trees)
    {
        if (!bEnabledThisRun || _trees == null || initialEmbersPerTree <= 0f) return;

        for (int i = 0; i < _trees.Count; i++)
        {
            TreeObj tree = _trees[i];
            if (tree == null || tree.bIsSapling) continue;

            int count = RollCount(initialEmbersPerTree);
            for (int c = 0; c < count; c++)
            {
                if (!TryPickNeighborCell(tree.CellPos, false, out Vector3Int cell)) break;
                PlaceEmber(cell, 0f);
            }
        }
    }

    /// <summary>나무가 쓰러지거나 희귀나무 등급이 바뀐 자리에서 불씨를 튀어나오게 한다.</summary>
    public void SpawnDropEmbers(Vector3 _treePosition)
    {
        if (!CanSpawnNow()) return;

        int count = UnityEngine.Random.Range(dropCountMin, Mathf.Max(dropCountMin, dropCountMax) + 1);
        ITilemapDataProvider tilemap = environmentProvider?.tilemapDataProvider;
        Vector3Int treeCell = tilemap != null ? tilemap.WorldToCell(_treePosition) : default;

        for (int i = 0; i < count; i++)
        {
            EmberItem ember = emberPool.Get();
            if (ember == null) continue;

            ember.Initialize(character, droppedLifetime);

            // 원목과 같은 흩뿌림(LogItemController.SpawnLogItem과 같은 거리·높이).
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float randomDist = UnityEngine.Random.Range(1.25f, 1.75f);
            Vector3 endPos = _treePosition + new Vector3(randomDir.x, randomDir.y * 0.5f, 0f) * randomDist;

            // 떨어질 칸이 물·용암·나무처럼 서 있을 수 없는 칸이면 나무 바로 옆의 설 수 있는 칸으로 바꾼다.
            // 나무 자리로 당기면 등급만 바뀌고 살아 있는 희귀나무는 밑동에 가려지고 걸어서 닿을 수 없다.
            // 옆 칸이 전부 막혔을 때만 나무 자리에 둔다(쓰러진 나무는 그 칸이 비어 있다).
            if (tilemap != null && !IsCellLandable(tilemap.WorldToCell(endPos)))
            {
                endPos = TryPickNeighborCell(treeCell, true, out Vector3Int landCell) ? tilemap.CellToWorld(landCell) : _treePosition;
            }

            ember.Launch(_treePosition, endPos, UnityEngine.Random.Range(0.75f, 1.25f));
        }
    }

    /// <summary>원정이 끝날 때(마을 도착·재도전 정리) 부른다. 불씨를 치우고 다음 BeginRun까지 아무것도 만들지 않는다.</summary>
    public void EndRun()
    {
        bEnabledThisRun = false;
        canSpawnMore = null;
        ClearAll();
    }

    /// <summary>
    /// 귀환이 확정된 순간(InDungeonObjectManager.HandleGameEnd) 부른다. 캐릭터에게 날아가던 불씨를 거둬 탑승 연출 중에
    /// 도착해 주워지지 않게 한다(원목·원석과 같은 처리). 새로 빨려가는 것은 캐릭터 쪽 감지가 꺼져서 막힌다.
    /// </summary>
    public void CancelActiveSucking()
    {
        for (int i = updatingEmbers.Count - 1; i >= 0; i--)
        {
            if (i >= updatingEmbers.Count) continue;

            EmberItem ember = updatingEmbers[i];
            if (ember.MoveState == ItemMoveState.Sucking) TryReleaseEmber(ember);
        }
    }

    public void ClearAll()
    {
        cleanupList.Clear();
        cleanupList.AddRange(activeEmbers);
        for (int i = 0; i < cleanupList.Count; i++)
        {
            TryReleaseEmber(cleanupList[i]);
        }
        cleanupList.Clear();

        // 회수하면서 칸 개수가 모두 0으로 돌아왔어야 한다. 다음 원정의 빈 칸 판정에 남지 않도록 비운다.
        groundEmberCounts.Clear();
    }

    private void Update()
    {
        if (updatingEmbers.Count > 0)
        {
            float deltaTime = Time.deltaTime;

            // ManualUpdate 중 획득·소멸·정지로 목록에서 빠질 수 있다(마지막 원소와 자리를 바꿔 뺀다).
            // 역순으로 돌면 그 자리에 들어오는 원소는 이미 이번 프레임에 갱신한 것이라 건너뛰거나 두 번 돌지 않는다.
            for (int i = updatingEmbers.Count - 1; i >= 0; i--)
            {
                if (i >= updatingEmbers.Count) continue;
                updatingEmbers[i].ManualUpdate(deltaTime);
            }
        }

        UpdateNaturalSpawn();
    }

    private void UpdateNaturalSpawn()
    {
        if (!CanSpawnNow() || character == null) return;

        naturalSpawnTimer += Time.deltaTime;
        if (naturalSpawnTimer < naturalSpawnInterval) return;
        naturalSpawnTimer = 0f;

        Transform characterTransform = character.GetTransform();
        if (characterTransform == null) return;

        if (TryPickNaturalCell(characterTransform.position, out Vector3Int cell))
        {
            PlaceEmber(cell, 0f);
        }
    }

    private bool CanSpawnNow()
    {
        return bEnabledThisRun && (canSpawnMore == null || canSpawnMore());
    }

    private static int RollCount(float _average)
    {
        int count = Mathf.FloorToInt(_average);
        if (UnityEngine.Random.value < _average - count) count++;
        return count;
    }

    private void PlaceEmber(Vector3Int _cell, float _lifetime)
    {
        ITilemapDataProvider tilemap = environmentProvider?.tilemapDataProvider;
        if (tilemap == null) return;

        EmberItem ember = emberPool.Get();
        if (ember == null) return;

        ember.Initialize(character, _lifetime);
        ember.Place(tilemap.CellToWorld(_cell));
    }

    // _bAllowEmberStack: 불씨가 이미 놓인 칸도 후보로 본다(벌목 드랍의 착지 보정 - 흩뿌림은 원래 칸을 가리지 않는다).
    private bool TryPickNeighborCell(Vector3Int _center, bool _bAllowEmberStack, out Vector3Int _cell)
    {
        neighborBuffer.Clear();
        for (int i = 0; i < NeighborOffsets.Length; i++)
        {
            Vector3Int candidate = _center + NeighborOffsets[i];
            if (_bAllowEmberStack ? IsCellLandable(candidate) : IsCellFree(candidate)) neighborBuffer.Add(candidate);
        }

        if (neighborBuffer.Count == 0)
        {
            _cell = default;
            return false;
        }

        _cell = neighborBuffer[UnityEngine.Random.Range(0, neighborBuffer.Count)];
        return true;
    }

    private bool TryPickNaturalCell(Vector3 _center, out Vector3Int _cell)
    {
        ITilemapDataProvider tilemap = environmentProvider.tilemapDataProvider;

        for (int i = 0; i < naturalSpawnAttempts; i++)
        {
            Vector3 point = _center + new Vector3(
                UnityEngine.Random.Range(-naturalSpawnHalfExtents.x, naturalSpawnHalfExtents.x),
                UnityEngine.Random.Range(-naturalSpawnHalfExtents.y, naturalSpawnHalfExtents.y),
                0f);

            Vector3Int candidate = tilemap.WorldToCell(point);
            if (IsCellFree(candidate))
            {
                _cell = candidate;
                return true;
            }
        }

        _cell = default;
        return false;
    }

    // 불씨가 놓일 수 있는 칸: 걸을 수 있고(나무·장애물 없음) 물·바위 장식·위험 지형이 아닌 칸.
    private bool IsCellLandable(Vector3Int _cell)
    {
        ITilemapDataProvider tilemap = environmentProvider?.tilemapDataProvider;
        if (tilemap == null) return false;

        if (!tilemap.IsWalkable(_cell)) return false;
        if (tilemap.IsWaterTile(_cell) || tilemap.HasRockDeco(_cell)) return false;
        if (tilemap.GetHazardStaminaDrainPerSecond(_cell) > 0f) return false;

        return true;
    }

    // 비어 있는 칸: 놓일 수 있는 칸 중 유닛이 서 있지 않고 불씨가 이미 놓이지 않은 칸(입장 배치·자연 생성용).
    private bool IsCellFree(Vector3Int _cell)
    {
        if (!IsCellLandable(_cell)) return false;
        if (environmentProvider.pathfindGridProvider != null && environmentProvider.pathfindGridProvider.IsOccupied(_cell)) return false;
        if (groundEmberCounts.ContainsKey(_cell)) return false;

        return true;
    }

    // ── 획득 / 소멸 ──

    private void OnEmberAcquired(EmberItem _ember)
    {
        PlayOneShotVfx(acquireVfxTag, _ember.transform.position);
        EmberAcquiredEvent?.Invoke(_ember);
        TryReleaseEmber(_ember);
    }

    private void OnEmberExpired(EmberItem _ember)
    {
        PlayOneShotVfx(expireVfxTag, _ember.transform.position);
        TryReleaseEmber(_ember);
    }

    public float OverheatPerEmber => overheatPerEmber;

    private void OnEmberGroundedChanged(EmberItem _ember, bool _bGrounded)
    {
        if (_bGrounded)
        {
            ITilemapDataProvider tilemap = environmentProvider?.tilemapDataProvider;
            if (tilemap != null && false == _ember.bHasGroundCell)
            {
                Vector3Int cell = tilemap.WorldToCell(_ember.transform.position);
                _ember.GroundCell = cell;
                _ember.bHasGroundCell = true;
                groundEmberCounts.TryGetValue(cell, out int count);
                groundEmberCounts[cell] = count + 1;
            }

            if (vfxComponent != null && !string.IsNullOrEmpty(groundLoopVfxTag) && _ember.GroundVfx == null)
            {
                // 불씨 자식으로 붙이지 않는다. 불씨는 회수되면 바로 꺼지므로, 자식이면 꺼지는 연출이 멈춘 채 다음 재사용 때 다시 보인다.
                _ember.GroundVfx = vfxComponent.Play(groundLoopVfxTag, _ember.transform.position, Quaternion.identity);
            }
        }
        else
        {
            if (_ember.bHasGroundCell)
            {
                Vector3Int cell = _ember.GroundCell;
                _ember.bHasGroundCell = false;
                if (groundEmberCounts.TryGetValue(cell, out int count))
                {
                    if (count <= 1) groundEmberCounts.Remove(cell);
                    else groundEmberCounts[cell] = count - 1;
                }
            }

            if (vfxComponent != null && _ember.GroundVfx != null)
            {
                vfxComponent.Stop(_ember.GroundVfx, false);
            }
            _ember.GroundVfx = null;
        }
    }

    private void OnEmberUpdateNeededChanged(EmberItem _ember, bool _bNeedsUpdate)
    {
        if (_bNeedsUpdate)
        {
            if (_ember.UpdateIndex != -1) return;
            _ember.UpdateIndex = updatingEmbers.Count;
            updatingEmbers.Add(_ember);
        }
        else
        {
            RemoveAtSwapBack(updatingEmbers, _ember, _ember.UpdateIndex, (_e, _i) => _e.UpdateIndex = _i);
            _ember.UpdateIndex = -1;
        }
    }

    // 마지막 원소와 자리를 바꿔 O(1)로 뺀다. 자리를 옮긴 원소의 인덱스는 _setIndex로 고친다.
    private static void RemoveAtSwapBack(List<EmberItem> _list, EmberItem _item, int _index, Action<EmberItem, int> _setIndex)
    {
        int lastIdx = _list.Count - 1;
        if (_index < 0 || _index > lastIdx || _list[_index] != _item) return;

        if (_index != lastIdx)
        {
            EmberItem last = _list[lastIdx];
            _list[_index] = last;
            _setIndex(last, _index);
        }
        _list.RemoveAt(lastIdx);
    }

    private void PlayOneShotVfx(string _tag, Vector3 _position)
    {
        if (vfxComponent == null || string.IsNullOrEmpty(_tag)) return;
        vfxComponent.Play(_tag, _position, Quaternion.identity);
    }

    // ── 풀 ──

    private EmberItem CreateEmberItem()
    {
        if (emberItemPrefab == null) return null;

        EmberItem newItem = Instantiate(emberItemPrefab, transform);
        newItem.EmberAcquiredEvent += OnEmberAcquired;
        newItem.EmberExpiredEvent += OnEmberExpired;
        newItem.EmberGroundedChangedEvent += OnEmberGroundedChanged;
        newItem.EmberUpdateNeededChangedEvent += OnEmberUpdateNeededChanged;
        newItem.gameObject.SetActive(false);
        return newItem;
    }

    private bool TryReleaseEmber(EmberItem _ember)
    {
        if (_ember == null || _ember.IsPooled) return false;

        emberPool.Release(_ember);
        return true;
    }

    private void OnGetEmberItem(EmberItem _ember)
    {
        if (_ember == null) return;

        _ember.IsPooled = false;
        _ember.ResetItem();
        _ember.PoolIndex = activeEmbers.Count;
        activeEmbers.Add(_ember);
        _ember.gameObject.SetActive(true);
    }

    private void OnReleaseEmberItem(EmberItem _ember)
    {
        if (_ember == null) return;

        // ResetItem이 바닥 상태와 갱신 필요 여부를 끄면서 칸 개수·바닥 루프 이펙트·갱신 목록을 정리한다
        // (OnEmberGroundedChanged, OnEmberUpdateNeededChanged).
        _ember.ResetItem();

        RemoveAtSwapBack(activeEmbers, _ember, _ember.PoolIndex, (_e, _i) => _e.PoolIndex = _i);
        _ember.PoolIndex = -1;

        _ember.IsPooled = true;
        _ember.gameObject.SetActive(false);
    }

    private void OnDestroyEmberItem(EmberItem _ember)
    {
        if (_ember == null) return;

        _ember.EmberAcquiredEvent -= OnEmberAcquired;
        _ember.EmberExpiredEvent -= OnEmberExpired;
        _ember.EmberGroundedChangedEvent -= OnEmberGroundedChanged;
        _ember.EmberUpdateNeededChangedEvent -= OnEmberUpdateNeededChanged;
        Destroy(_ember.gameObject);
    }
}
