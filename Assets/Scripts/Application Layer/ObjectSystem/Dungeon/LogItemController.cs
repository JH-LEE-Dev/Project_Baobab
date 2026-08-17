using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class LogItemController : MonoBehaviour, ILogItemControllerCH
{
    public event Action<Item> LogItemAcquiredEvent;

    // 외부 의존성
    [SerializeField] private LogItem logItemPrefab;
    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;
    [SerializeField] private List<LogDropCntData> logDropCntDatas;
    // TreeGrade.Fascinating 이상인 나무는 종류(TreeType)와 무관하게 이 범위로 드랍 개수가 고정된다.
    [SerializeField] private int highGradeDropCntMin = 4;
    [SerializeField] private int highGradeDropCntMax = 7;

    // 내부 의존성
    private IObjectPool<LogItem> logPool;
    // 최적화: 인덱스 기반 관리로 HashSet 제거
    private List<LogItem> activeItemsList = new List<LogItem>(256); // 마스터 리스트 (컬링 그룹용)
    private List<LogItem> activeItemsForUpdate = new List<LogItem>(256); // 업데이트 리스트 (가시성 기준)

    [Header("Optimization")]
    [SerializeField] private bool enableCulling = true;
    [SerializeField] private float cullingUpdateInterval = 0.05f;
    private float cullingUpdateTimer = 0f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;

    private IInventoryChecker inventoryChecker;

    private ICharacter character;

    private ITilemapDataProvider tilemapDataProvider;
    // 물 타일 폴백 시 "몇 월드 유닛 = 1타일"인지 알아야 해서, 실제 그리드 셀 크기를 한 번만 계산해 캐싱한다.
    // Initialize() 시점엔 아직 던전 타일맵이 생성되기 전이라 여기서는 측정할 수 없고, 나무가 실제로
    // 존재하는(= 맵 생성이 끝난) SpawnLogItem 최초 호출 시점에 지연 계산한다.
    private float tileWorldSize = 1f;
    private bool tileWorldSizeMeasured = false;

    private VFXComponent vfxComponent;

    [Header("Skill Attribute")]
    private float jackPotChance = 0f;
    private float jackPotAmount = 2f;

    public void Initialize(IInventoryChecker _inventoryChecker, ICharacter _character, ITilemapDataProvider _tilemapDataProvider)
    {
        inventoryChecker = _inventoryChecker;
        character = _character;
        tilemapDataProvider = _tilemapDataProvider;
        tileWorldSizeMeasured = false;

        vfxComponent = GetComponent<VFXComponent>();
        vfxComponent.Initialize();

        logPool = new ObjectPool<LogItem>(
            createFunc: CreateLogItem,
            actionOnGet: OnGetLogItem,
            actionOnRelease: OnReleaseLogItem,
            actionOnDestroy: OnDestroyLogItem,
            collectionCheck: true,
            defaultCapacity: 200,
            maxSize: 1000 // 최적화: 나무가 많은 게임 특성상 풀 크기를 넉넉하게 설정
        );
    }

    public void SetupCullingGroup()
    {
        if (!enableCulling) return;

        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.onStateChanged = OnCullingStateChanged;
        }

        cullingGroup.targetCamera = Camera.main;
        spheres = new BoundingSphere[1000];
        cullingGroup.SetBoundingSpheres(spheres);
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (!enableCulling) return;
        if (_ev.index >= activeItemsList.Count) return;

        bool isVisible = _ev.isVisible;
        UpdateItemVisibility(activeItemsList[_ev.index], isVisible);
    }

    private void UpdateItemVisibility(LogItem _item, bool _isVisible)
    {
        if (_item.gameObject.activeSelf != _isVisible)
        {
            _item.gameObject.SetActive(_isVisible);
        }

        if (_isVisible)
        {
            if (_item.UpdateIndex == -1 && _item.IsMoving)
            {
                _item.UpdateIndex = activeItemsForUpdate.Count;
                activeItemsForUpdate.Add(_item);
                _item.bCanGetSortingOrder = true;
            }
        }
        else
        {
            int idx = _item.UpdateIndex;
            if (idx != -1)
            {
                int lastIdx = activeItemsForUpdate.Count - 1;
                if (idx != lastIdx)
                {
                    LogItem lastItem = activeItemsForUpdate[lastIdx];
                    activeItemsForUpdate[idx] = lastItem;
                    lastItem.UpdateIndex = idx;
                }
                activeItemsForUpdate.RemoveAt(lastIdx);
                _item.UpdateIndex = -1;
                _item.bCanGetSortingOrder = false;
            }
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        // 최적화: 가시 영역 내의 아이템만 업데이트
        if (activeItemsForUpdate.Count > 0)
        {
            // ManualUpdate 중 아이템이 해제(Release)되어 리스트가 변형될 수 있으므로 역순 순회
            for (int i = activeItemsForUpdate.Count - 1; i >= 0; i--)
            {
                activeItemsForUpdate[i].ManualUpdate(deltaTime);
            }
        }

        // 컬링 구체 위치 업데이트 (스로틀링) - 마스터 리스트 기반
        if (enableCulling && cullingGroup != null && activeItemsList.Count > 0)
        {
            cullingUpdateTimer += deltaTime;
            if (cullingUpdateTimer >= cullingUpdateInterval)
            {
                UpdateCullingSpheres();
                cullingUpdateTimer = 0f;
            }
        }
    }

    private void UpdateCullingSpheres()
    {
        int count = activeItemsForUpdate.Count;
        for (int i = 0; i < count; i++)
        {
            var item = activeItemsForUpdate[i];
            if (item.IsMoving && item.PoolIndex != -1)
            {
                spheres[item.PoolIndex].position = item.transform.position;
            }
        }
    }

    private void RefreshCullingGroup()
    {
        if (!enableCulling || cullingGroup == null) return;

        int count = activeItemsList.Count;
        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(count);

        for (int i = 0; i < count; i++)
        {
            UpdateItemVisibility(activeItemsList[i], cullingGroup.IsVisible(i));
        }
    }

    private void LogItemAcquired(LogItem _item)
    {
        if (_item.CustomAcquirer != null)
        {
            // NPC 등 특정 소비자가 지정된 경우, 전역(플레이어) 이벤트 체인을 타지 않고 직접 귀속시킨다
            _item.CustomAcquirer.ItemAcquired(_item);
        }
        else
        {
            LogItemAcquiredEvent?.Invoke(_item);
        }

        logPool.Release(_item);
    }

    private LogItem CreateLogItem()
    {
        LogItem newItem = Instantiate(logItemPrefab, transform);
        newItem.LogItemAcquired -= LogItemAcquired;
        newItem.LogItemAcquired += LogItemAcquired;

        newItem.LogItemActivatedEvent -= LogItemActivated;
        newItem.LogItemActivatedEvent += LogItemActivated;

        newItem.LogItemDeActivatedEvent -= LogItemDeActivated;
        newItem.LogItemDeActivatedEvent += LogItemDeActivated;

        newItem.SetVfxComponent(vfxComponent);

        return newItem;
    }

    private void OnGetLogItem(LogItem _item)
    {
        // 최적화: 마스터 리스트 추가 및 인덱스 설정 (O(1))
        _item.PoolIndex = activeItemsList.Count;
        activeItemsList.Add(_item);

        // BoundingSphere 즉시 동기화
        if (enableCulling)
        {
            if (spheres == null)
            {
                spheres = new BoundingSphere[1000];
                if (cullingGroup != null) cullingGroup.SetBoundingSpheres(spheres);
            }

            if (spheres.Length <= _item.PoolIndex)
            {
                Array.Resize(ref spheres, Mathf.Max(spheres.Length * 2, _item.PoolIndex + 1));
                if (cullingGroup != null) cullingGroup.SetBoundingSpheres(spheres);
            }
            spheres[_item.PoolIndex] = new BoundingSphere(_item.transform.position, 1f);
        }

        if (enableCulling && cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(activeItemsList.Count);
            // 즉시 가시성 체크하여 활성화 및 업데이트 등록 여부 결정
            UpdateItemVisibility(_item, cullingGroup.IsVisible(_item.PoolIndex));
        }
        else
        {
            _item.gameObject.SetActive(true);
            // 컬링이 꺼져 있거나 컬링 그룹이 없으면 무조건 업데이트 리스트에 추가
            _item.UpdateIndex = activeItemsForUpdate.Count;
            activeItemsForUpdate.Add(_item);
            _item.bCanGetSortingOrder = true;
        }

        _item.ResetItem();
    }

    private void OnReleaseLogItem(LogItem _item)
    {
        // 최적화: 업데이트 리스트에서 제거
        UpdateItemVisibility(_item, false);

        // 최적화: 마스터 리스트에서 Swap-with-last 방식을 이용한 제거 (O(1))
        int idx = _item.PoolIndex;
        if (idx != -1 && idx < activeItemsList.Count)
        {
            int lastIdx = activeItemsList.Count - 1;
            if (idx != lastIdx)
            {
                LogItem lastItem = activeItemsList[lastIdx];
                activeItemsList[idx] = lastItem;
                lastItem.PoolIndex = idx;
                if (enableCulling && spheres != null) spheres[idx] = spheres[lastIdx];
            }
            activeItemsList.RemoveAt(lastIdx);
            _item.PoolIndex = -1;

            if (enableCulling && cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(activeItemsList.Count);
            }
        }

        _item.gameObject.SetActive(false);
    }

    private void OnDestroyLogItem(LogItem _item)
    {
        _item.LogItemAcquired -= LogItemAcquired;
        _item.LogItemActivatedEvent -= LogItemActivated;
        _item.LogItemDeActivatedEvent -= LogItemDeActivated;

        OnReleaseLogItem(_item);
        Destroy(_item.gameObject);
    }

    /// <summary>
    /// 타운 귀환(DropAllItem/Off로드 탑승) 확정 시점에 호출한다. 이미 캐릭터를 향해 흡입(Sucking) 중이던
    /// 아이템은 습득 처리 없이 그대로 풀로 반환해 자연스럽게 사라지게 하고, 아직 흡입을 시작하지 않은
    /// 아이템(흡입 대상으로 예약만 된 상태 포함)은 bCanAcquired를 꺼서 더 이상 습득되지 않도록 막는다.
    /// </summary>
    public void CancelActiveSucking()
    {
        for (int i = activeItemsList.Count - 1; i >= 0; i--)
        {
            LogItem item = activeItemsList[i];

            if (item.MoveState == ItemMoveState.Sucking)
            {
                logPool.Release(item);
            }
            else
            {
                item.SetbCanAcquired(false);
            }
        }
    }

    public void ClearAll()
    {
        int count = activeItemsList.Count;
        if (count == 0) return;

        for (int i = count - 1; i >= 0; i--)
        {
            logPool.Release(activeItemsList[i]);
        }

        activeItemsList.Clear();
        activeItemsForUpdate.Clear();

        if (enableCulling && cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(0);
        }
    }

    public void SpawnLogItem(TreeObj _treeObj, float _multiplier)
    {
        if (!tileWorldSizeMeasured)
        {
            MeasureTileWorldSize();
        }

        TreeData treeData = _treeObj.treeData;
        LogState logType = GetLogStateFromTreeGrade(treeData.grade);

        int minCnt, maxCnt;
        if (treeData.grade >= TreeGrade.Fascinating)
        {
            minCnt = highGradeDropCntMin;
            maxCnt = highGradeDropCntMax;
        }
        else
        {
            LogDropCntData dropCntData = GetDropCntData(treeData.type);
            minCnt = dropCntData.minCnt;
            maxCnt = dropCntData.maxCnt;
        }

        int spawnCount = Mathf.RoundToInt(UnityEngine.Random.Range(minCnt, maxCnt + 1) * _multiplier);

        if (UnityEngine.Random.value < jackPotChance)
        {
            spawnCount = Mathf.RoundToInt(spawnCount * jackPotAmount);
        }

        Vector3 spawnPos = _treeObj.transform.position;

        for (int i = 0; i < spawnCount; i++)
        {
            LogItem logItem = logPool.Get();

            logItem.transform.position = spawnPos;
            var info = logItemTypeDataBase.Get(treeData.type);
            logItem.Initialize(info, info.color, logType, character);
            logItem.SetInventoryChecker(inventoryChecker);

            // 포물선 운동 설정
            Vector3 startPos = spawnPos;
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float randomDist = UnityEngine.Random.Range(1.25f, 1.75f);
            Vector3 endPos = startPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0) * randomDist;

            // 물 타일에 착지하면 캐릭터가 접근할 수 없다. 나무 위치에 그대로 겹쳐 떨어지면 부자연스러우니,
            // 같은 방향으로 1타일 이내까지만 당겨서 한 번 더 확인하고, 그마저도 물이면 그때만 나무 위치로 대체한다
            if (tilemapDataProvider != null && tilemapDataProvider.IsWaterTile(tilemapDataProvider.WorldToCell(endPos)))
            {
                float pulledDist = Mathf.Min(randomDist, tileWorldSize * 0.9f);
                Vector3 pulledPos = startPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0) * pulledDist;

                endPos = tilemapDataProvider.IsWaterTile(tilemapDataProvider.WorldToCell(pulledPos)) ? spawnPos : pulledPos;
            }

            float height = UnityEngine.Random.Range(0.75f, 1.25f);

            float randomRotation = UnityEngine.Random.Range(1, 3) * 360f * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

            logItem.Launch(startPos, endPos, height, randomRotation);
        }
    }

    // Initialize() 시점엔 던전 타일맵이 아직 생성 전이라 측정이 불가능하므로, 나무가 실제로 존재하는
    // (= 맵 생성이 끝난) 첫 SpawnLogItem 호출 시점에 한 번만 측정해 캐싱한다.
    private void MeasureTileWorldSize()
    {
        tileWorldSizeMeasured = true;

        if (tilemapDataProvider == null) return;

        Vector3Int originCell = tilemapDataProvider.WorldToCell(Vector3.zero);
        float measuredSize = Vector3.Distance(tilemapDataProvider.CellToWorld(originCell), tilemapDataProvider.CellToWorld(originCell + Vector3Int.right));
        if (measuredSize > 0f) tileWorldSize = measuredSize;
    }

    private LogDropCntData GetDropCntData(TreeType _type)
    {
        for (int i = 0; i < logDropCntDatas.Count; i++)
        {
            if (logDropCntDatas[i].treeType == _type)
            {
                return logDropCntDatas[i];
            }
        }

        // 기본값 반환 (데이터가 없을 경우)
        return new LogDropCntData { treeType = _type, minCnt = 2, maxCnt = 4 };
    }

    private LogState GetLogStateFromTreeGrade(TreeGrade _grade)
    {
        switch (_grade)
        {
            case TreeGrade.Normal: return LogState.Normal;
            case TreeGrade.Fascinating: return LogState.Fascinating;
            case TreeGrade.Advanced: return LogState.Advanced;
            case TreeGrade.Perfect: return LogState.Perfect;
            default: return LogState.Normal;
        }
    }

    public void ReturnToPool(LogItem _item)
    {
        logPool.Release(_item);
    }

    private void OnDestroy()
    {
        if (cullingGroup != null)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }
    }

    private void LogItemActivated(LogItem _logItem)
    {
        if (_logItem.UpdateIndex == -1)
        {
            _logItem.UpdateIndex = activeItemsForUpdate.Count;
            activeItemsForUpdate.Add(_logItem);
            _logItem.bCanGetSortingOrder = true;
        }
    }

    private void LogItemDeActivated(LogItem _logItem)
    {
        int idx = _logItem.UpdateIndex;
        if (idx != -1)
        {
            int lastIdx = activeItemsForUpdate.Count - 1;
            if (idx != lastIdx)
            {
                LogItem lastItem = activeItemsForUpdate[lastIdx];
                activeItemsForUpdate[idx] = lastItem;
                lastItem.UpdateIndex = idx;
            }
            activeItemsForUpdate.RemoveAt(lastIdx);
            _logItem.UpdateIndex = -1;
            _logItem.bCanGetSortingOrder = false;
        }
    }

    public void IncreaseJackPotChance(float _amount)
    {
        //0~1의 값.
        jackPotChance = (_amount / 100f);
    }

    public void IncreaseJackPotAmount(float _amount)
    {
        jackPotAmount = _amount;
    }
}