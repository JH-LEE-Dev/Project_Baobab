using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class LogItemController : MonoBehaviour, ILogItemControllerCH, ILogItemAuraProvider
{
    public event Action<Item> LogItemAcquiredEvent;

    // 외부 의존성
    [SerializeField] private LogItem logItemPrefab;
    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;
    [SerializeField] private List<LogDropCntData> logDropCntDatas;

    [Header("Gem Aura")]
    [Tooltip("보석 등급 원목이 드랍될 때 붙는 아우라. LogState별로 프리셋 프리팹을 연결한다.")]
    [SerializeField] private List<LogStateAuraData> logStateAuraDatas;
    [SerializeField] private int auraPoolDefaultCapacity = 8;
    [SerializeField] private int auraPoolMaxSize = 64;

    private Dictionary<LogState, IObjectPool<ItemAuraEffectController>> auraPools;
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

        BuildAuraPools();

        logPool = new ObjectPool<LogItem>(
            createFunc: CreateLogItem,
            actionOnGet: OnGetLogItem,
            actionOnRelease: OnReleaseLogItem,
            actionOnDestroy: OnDestroyLogItem,
            collectionCheck: PoolSettings.CollectionCheck,
            defaultCapacity: 200,
            maxSize: 1000 // 최적화: 나무가 많은 게임 특성상 풀 크기를 넉넉하게 설정
        );
    }

    /// <summary>
    /// Initialize() 시점엔 캐릭터가 아직 스폰되기 전이라 null이 들어온다. 캐릭터 스폰 이후
    /// ItemManager.SetCharacter를 통해 뒤늦게 주입받는다. SpawnLogItem에서 LogItem마다 이 값을
    /// 넘겨주므로, 나무가 죽기 전(= 첫 LogItem 스폰 전)에만 채워지면 충분하다.
    /// </summary>
    public void SetCharacter(ICharacter _character)
    {
        character = _character;
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
            // (진동도 여기서는 울리지 않는다 - 손에 잡히는 것은 캐릭터가 주울 때뿐이어야 한다)
            _item.CustomAcquirer.ItemAcquired(_item);
        }
        else
        {
            // 캐릭터가 필드 위 원목을 주운 순간 (아주 짧은 톡 하는 진동)
            Rumble.Play(EHapticEvent.ItemPickup);

            LogItemAcquiredEvent?.Invoke(_item);
        }

        TryReleaseLogItem(_item);
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
        newItem.SetAuraProvider(this);

        return newItem;
    }

    /// <summary>
    /// 이미 풀에 들어가 있는 항목을 다시 반환하지 않도록 막고 반환한다. 반환이 실제로
    /// 일어났으면 true. IsPooled는 풀의 actionOnGet/actionOnRelease에서만 갱신된다.
    /// </summary>
    private bool TryReleaseLogItem(LogItem _item)
    {
        if (_item == null || _item.IsPooled) return false;

        logPool.Release(_item);
        return true;
    }

    private void OnGetLogItem(LogItem _item)
    {
        _item.IsPooled = false;
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
        _item.IsPooled = true;
        // 빌려간 아우라를 여기서 바로 회수한다. ResetItem은 다음 획득 때 호출되므로,
        // 그때까지 기다리면 풀에서 쉬고 있는 원목들이 아우라를 붙든 채로 남는다.
        _item.ReleaseGemAura();

        // 반짝임 파티클도 같은 이유로 여기서 회수한다. 자식으로 매달린 채 부모가 꺼지면
        // 파티클의 activeSelf는 true로 남아 VFX 풀이 "사용 중"으로 오인하고, 그 인스턴스는
        // 영영 재사용되지 못한 채 누수된다.
        _item.StopGemShiny();

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
                TryReleaseLogItem(item);
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
            TryReleaseLogItem(activeItemsList[i]);
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

        // 보석 등급 원목이 생성되는 순간의 전용 효과음.
        // 원목 하나마다 재생하면 4~7개가 겹쳐 뭉개지므로 드랍 묶음당 한 번만 울린다.
        if (logType > LogState.Normal && spawnCount > 0)
        {
            Sound.Play(SoundID.NiceItem, spawnPos);

            // 효과음과 같은 이유로 진동도 드랍 묶음당 한 번만 울린다.
            // (원목 하나마다 울리면 4~7번이 겹쳐 한 덩어리로 뭉개진다)
            Rumble.Play(EHapticEvent.RareLogSpawn);
        }

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
        TryReleaseLogItem(_item);
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

    // // 보석 아우라 (ILogItemAuraProvider)

    private void BuildAuraPools()
    {
        if (auraPools != null) return;

        auraPools = new Dictionary<LogState, IObjectPool<ItemAuraEffectController>>();
        if (logStateAuraDatas == null) return;

        for (int i = 0; i < logStateAuraDatas.Count; i++)
        {
            LogStateAuraData data = logStateAuraDatas[i];
            if (data.auraPrefab == null || auraPools.ContainsKey(data.logState)) continue;

            // 루프 변수를 그대로 캡처하면 모든 풀이 마지막 프리팹을 쓰게 되므로 지역 변수로 고정한다.
            ItemAuraEffectController prefab = data.auraPrefab;

            auraPools.Add(data.logState, new ObjectPool<ItemAuraEffectController>(
                createFunc: () => Instantiate(prefab, transform),
                actionOnGet: OnGetAura,
                actionOnRelease: OnReleaseAura,
                actionOnDestroy: OnDestroyAura,
                collectionCheck: true,
                defaultCapacity: auraPoolDefaultCapacity,
                maxSize: auraPoolMaxSize
            ));
        }
    }

    public ItemAuraEffectController GetAura(LogState _logState)
    {
        if (auraPools != null && auraPools.TryGetValue(_logState, out IObjectPool<ItemAuraEffectController> pool))
        {
            return pool.Get();
        }
        return null;
    }

    public void ReleaseAura(LogState _logState, ItemAuraEffectController _aura)
    {
        if (_aura == null) return;

        if (auraPools != null && auraPools.TryGetValue(_logState, out IObjectPool<ItemAuraEffectController> pool))
        {
            pool.Release(_aura);
            return;
        }

        // 풀을 못 찾으면(설정 변경 등) 고아로 남기지 않도록 파기한다.
        Destroy(_aura.gameObject);
    }

    private void OnGetAura(ItemAuraEffectController _aura)
    {
        _aura.gameObject.SetActive(true);
    }

    private void OnReleaseAura(ItemAuraEffectController _aura)
    {
        // 원목에 붙어 있던 것을 떼어내 컨트롤러 아래로 되돌린다. 원목이 비활성화돼도 아우라가 함께 사라지지 않게 한다.
        _aura.transform.SetParent(transform, false);
        _aura.transform.localPosition = Vector3.zero;
        _aura.gameObject.SetActive(false);
    }

    private void OnDestroyAura(ItemAuraEffectController _aura)
    {
        if (_aura != null) Destroy(_aura.gameObject);
    }
}
