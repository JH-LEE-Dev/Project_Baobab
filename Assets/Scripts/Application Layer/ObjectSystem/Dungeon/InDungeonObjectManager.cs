using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using System.Collections;
using System;

public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider, IInDungeonObjManagerCH
{
    // // 이벤트
    public event Action ActivateWarningUIEvent;
    public event Action DropAllItemEvent;
    public event Action RideOffroadEvent;
    public event Action<bool> OffroadInteractStateChangedEvent;
    public event Action<bool> RepairBoxInteractStateChangedEvent;
    public event Action<OffroadVehicleObj> OffroadSpawnedEvent;
    public event Action<TreeType> TreeDeadEvent;
    public event Action PortalActivatedEvent;
    public event Action<Item> ItemAcquiredEvent;
    public event Action<CarrotItem> CarrotItemAcquiredEvent;
    public event Action<TreeObj> TreeGetHitEvent;

    // // 외부 의존성
    private IEnvironmentProvider environmentProvider;
    public ItemManager itemManager { get; private set; }
    private DungeonData dungeonData;
    private LootManager lootManager;
    private InputManager inputManager;
    private IInventory characterInventory;

    // // 내부 의존성
    [Header("Tree Settings")]
    [SerializeField] private TreeObj treePrefab;

    [Header("Optimization")]
    [SerializeField] private float cullingDistance = 25f;

    [Header("Portal")]
    [SerializeField] private OffroadVehicleObj offroadVehiclePrefab;

    // // 내부 상태 및 컬렉션
    public OffroadVehicleObj offroadVehicle;
    private List<Vector3> grassTileWorldPositions;
    private List<Vector3> availablePositions = new List<Vector3>(2500);
    private List<TreeObj> activeTrees = new List<TreeObj>(2500);

    // 최적화: 인덱스 기반 관리로 HashSet 제거
    private List<TreeObj> activeTreesForUpdate = new List<TreeObj>(2500);
    public IReadOnlyList<TreeObj> ActiveTrees => activeTreesForUpdate;

    private IObjectPool<TreeObj> treePool;
    private Coroutine growthCoroutine;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;
    private Camera mainCam; // 최적화: 카메라 캐싱

    public IReadOnlyList<ITreeObj> trees => activeTrees;

    [SerializeField] private TreeVisualDataBase treeVisualDataBase;
    [SerializeField] private TreeStatDataBase treeStatDataBase;
    [SerializeField] private List<TreeGradeStatMultiplierData> treeGradeStatMultiplierDatas;

    [System.Serializable]
    public struct MapTypeTreeGenerationData
    {
        public MapType mapType;
        public TreeGenerationStrategySO strategy;
    }

    [Header("Tree Generation")]
    [SerializeField] private TreeGenerationStrategySO currentTreeGenerationStrategy;
    [SerializeField] private List<MapTypeTreeGenerationData> mapTypeTreeGenerationDatas;

    private float treeGrowTime = 10f;

    private HiddenMapGrade hiddenMapGrade = HiddenMapGrade.None;

    [SerializeField] private List<HiddenMapTreeGradeProbData> hiddenMapTreeGradeDatas;

    private Character character;
    private OffroadContainer offroadContainer;
    private IInventoryChecker inventoryChecker;
    private InDungeonResultManager inDungeonResultManager;
    private MapType currentMapType;

    private InDungeonVFXManager inDungeonVFXManager;

    private float growthSpeedMul = 0f;
    public float GrowthSpeedMul => growthSpeedMul;

    // // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(IEnvironmentProvider _environmentProvider, IInventoryChecker _inventoryChecker, InputManager _inputManager,
    IInventory _characterInventory, OffroadContainer _offroadContainer, InDungeonResultManager _inDungeonResultManager)
    {
        offroadContainer = _offroadContainer;
        characterInventory = _characterInventory;
        inputManager = _inputManager;
        environmentProvider = _environmentProvider;
        mainCam = Camera.main;
        inventoryChecker = _inventoryChecker;
        inDungeonResultManager = _inDungeonResultManager;

        itemManager = GetComponentInChildren<ItemManager>();
        itemManager.Initialize(_inventoryChecker, character);

        lootManager = GetComponentInChildren<LootManager>();
        lootManager.Initialize();

        inDungeonVFXManager = GetComponentInChildren<InDungeonVFXManager>();
        inDungeonVFXManager.Initialize();

        cullingDistances = new float[] { cullingDistance };
        spheres = new BoundingSphere[2500]; // 최대 개수에 맞춰 미리 할당
        onCullingStateChangedDelegate = OnCullingStateChanged;

        treePool = new ObjectPool<TreeObj>(
            createFunc: OnCreateTree,
            actionOnGet: OnGetTree,
            actionOnRelease: OnReleaseTree,
            actionOnDestroy: OnDestroyTree,
            collectionCheck: true,
            defaultCapacity: 200,
            maxSize: 2500
        );

        if (itemManager != null)
        {
            itemManager.LogItemAcquiredEvent -= OnItemAcquired;
            itemManager.LogItemAcquiredEvent += OnItemAcquired;

            itemManager.CarrotItemAcquiredEvent -= CarrotItemAcquired;
            itemManager.CarrotItemAcquiredEvent += CarrotItemAcquired;
        }
    }

    public void Release()
    {
        StopGrowth();
        ClearTrees();

        if (offroadVehicle != null)
        {
            offroadVehicle.PortalActivated -= OnPortalActivated;
        }

        if (cullingGroup != null)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }

        itemManager.LogItemAcquiredEvent -= OnItemAcquired;

        itemManager.CarrotItemAcquiredEvent -= CarrotItemAcquired;
    }

    public void SetupItemManagerCulling()
    {
        itemManager.SetupCulling();
    }

    public void SetupForMapType(MapType _mapType)
    {
        if (mapTypeTreeGenerationDatas == null) return;

        currentMapType = _mapType;

        for (int i = 0; i < mapTypeTreeGenerationDatas.Count; i++)
        {
            if (mapTypeTreeGenerationDatas[i].mapType == _mapType)
            {
                if (mapTypeTreeGenerationDatas[i].strategy != null)
                {
                    currentTreeGenerationStrategy = Instantiate(mapTypeTreeGenerationDatas[i].strategy);
                    currentTreeGenerationStrategy.currentMapType = _mapType;
                }
                return;
            }
        }
    }

    public void SetDungeonData(DungeonData _dungeonData)
    {
        dungeonData = _dungeonData;
    }

    public void ReadyTrees(List<Vector3> _grassTileWorldPositions)
    {
        grassTileWorldPositions = _grassTileWorldPositions;
        SpawnInitialTrees();
    }

    public void ReadyPortal()
    {
        if (offroadVehicle == null)
        {
            offroadVehicle = Instantiate(offroadVehiclePrefab, transform);
            offroadVehicle.Initialize(PortalType.ToTownPortal, environmentProvider, inputManager, characterInventory, offroadContainer,
            character.centerTransform);
            OffroadSpawnedEvent?.Invoke(offroadVehicle);
        }

        var pos = environmentProvider.tilemapDataProvider.GetPortalSpawnPosition();
        pos.y -= 0.25f;

        offroadVehicle.transform.position = pos;
        offroadVehicle.ResetPortal();
        offroadVehicle.gameObject.SetActive(true);
        offroadVehicle.SetCanTravel(true);
        offroadVehicle.col.enabled = false;
        BindPortalEvents();

        if (currentMapType == MapType.StarrootForest || currentMapType == MapType.MagmaForest)
        {
            offroadVehicle.ChangeSprite(currentMapType);
        }
        else
        {
            offroadVehicle.ResetSprite();
        }
    }

    public Vector3 GetPlayerStartPos()
    {
        return environmentProvider.tilemapDataProvider.GetPlayerSpawnPosition();
    }

    public void ClearObjManager()
    {
        if (offroadVehicle != null)
        {
            offroadVehicle.SetVisualActive(false);
            offroadVehicle.gameObject.SetActive(false);
        }

        if (itemManager != null)
            itemManager.ReleaseAllItems();

        StopGrowth();
        ClearTrees();
    }

    // // 프라이빗 로직 메서드

    private void SpawnInitialTrees()
    {
        if (grassTileWorldPositions == null || grassTileWorldPositions.Count == 0) return;

        SetupCullingGroup();
        StopGrowth();
        ClearTrees();

        if (currentTreeGenerationStrategy != null)
        {
            currentTreeGenerationStrategy.SpawnInitialTrees(this, grassTileWorldPositions);
        }

        RefreshCullingGroup();

        // 3. 5초 후 성장 루틴 시작
        growthCoroutine = StartCoroutine(StartGrowthAfterDelay());
    }

    private IEnumerator StartGrowthAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        if (currentTreeGenerationStrategy != null)
        {
            growthCoroutine = StartCoroutine(currentTreeGenerationStrategy.GrowthRoutine(this));
        }
    }

    public IEnvironmentProvider EnvironmentProvider => environmentProvider;
    public int AvailablePositionsCount => availablePositions.Count;

    public void ClearAvailablePositions()
    {
        availablePositions.Clear();
    }

    public void AddAvailablePosition(Vector3 _pos)
    {
        availablePositions.Add(_pos);
    }

    public void ShuffleAvailablePositions()
    {
        ShufflePositions(availablePositions);
    }

    public void SwapRandomAvailablePositionWithLast()
    {
        int lastIdx = availablePositions.Count - 1;
        int swapIdx = UnityEngine.Random.Range(0, lastIdx);
        Vector3 temp = availablePositions[lastIdx];
        availablePositions[lastIdx] = availablePositions[swapIdx];
        availablePositions[swapIdx] = temp;
    }

    public TreeObj SpawnTreeAt(Vector3 _spawnPos, bool _isGrowing)
    {
        Vector3Int cellPos = environmentProvider.tilemapDataProvider.WorldToCell(_spawnPos);

        if (!environmentProvider.pathfindGridProvider.IsOccupied(cellPos) &&
            !environmentProvider.tilemapDataProvider.HasRockDeco(cellPos))
        {
            TreeObj tree = treePool.Get();
            tree.transform.position = _spawnPos;

            bool isWaterNearby = environmentProvider.tilemapDataProvider.IsWaterTile(cellPos + new Vector3Int(-1, -1, 0)) ||
                                 environmentProvider.tilemapDataProvider.IsWaterTile(cellPos + new Vector3Int(-2, -2, 0));
            tree.SetOnWaterObjectState(isWaterNearby);

            tree.PoolIndex = activeTrees.Count;
            activeTrees.Add(tree);

            if (spheres.Length <= tree.PoolIndex)
            {
                System.Array.Resize(ref spheres, Mathf.Max(spheres.Length * 2, tree.PoolIndex + 1));
                cullingGroup.SetBoundingSpheres(spheres);
            }
            spheres[tree.PoolIndex] = new BoundingSphere(_spawnPos, 3f);

            environmentProvider.tilemapDataProvider.SetTreeCollisionTile(_spawnPos);
            environmentProvider.densityProvider.UpdateTreeCnt(true);

            if (cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(activeTrees.Count);
                UpdateTreeVisibility(tree, cullingGroup.IsVisible(tree.PoolIndex) && (cullingGroup.GetDistance(tree.PoolIndex) == 0));
            }
            else
            {
                tree.gameObject.SetActive(true);
            }

            if (_isGrowing)
            {
                tree.SetIsSapling(true, treeGrowTime);
            }

            tree.SetSortOrder();
            tree.EnableOutline();

            return tree;
        }

        return null;
    }

    public bool SpawnOneTreeFromAvailable(bool _isGrowing)
    {
        int count = availablePositions.Count;
        if (count == 0) return false;

        // 랜덤한 시작 지점부터 순회하며 빈 공간을 찾음
        int startIdx = UnityEngine.Random.Range(0, count);
        for (int i = 0; i < count; i++)
        {
            int checkIdx = (startIdx + i) % count;
            Vector3 spawnPos = availablePositions[checkIdx];
            Vector3Int cellPos = environmentProvider.tilemapDataProvider.WorldToCell(spawnPos);

            // 해당 타일이 점유 중(플레이어, 몬스터 등)이 아니면 생성 진행
            if (!environmentProvider.pathfindGridProvider.IsOccupied(cellPos) &&
                !environmentProvider.tilemapDataProvider.HasRockDeco(cellPos))
            {
                int lastIdx = availablePositions.Count - 1;
                availablePositions[checkIdx] = availablePositions[lastIdx];
                availablePositions.RemoveAt(lastIdx);

                TreeObj tree = SpawnTreeAt(spawnPos, _isGrowing);
                if (tree != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ClearTrees()
    {
        for (int i = activeTrees.Count - 1; i >= 0; i--)
        {
            if (activeTrees[i] != null)
            {
                environmentProvider.tilemapDataProvider.ClearTreeCollisionTile(activeTrees[i].transform.position);
                environmentProvider.densityProvider.UpdateTreeCnt(false);
                activeTrees[i].transform.position = new Vector2(-10000f, -10000f);
                treePool.Release(activeTrees[i]);
            }
        }
        activeTrees.Clear();
        activeTreesForUpdate.Clear();
        if (cullingGroup != null) cullingGroup.SetBoundingSphereCount(0);
    }

    private void StopGrowth()
    {
        if (growthCoroutine != null)
        {
            StopCoroutine(growthCoroutine);
            growthCoroutine = null;
        }
    }

    private void SetupCullingGroup()
    {
        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.onStateChanged = onCullingStateChangedDelegate;
        }

        mainCam = Camera.main;

        cullingGroup.targetCamera = mainCam;
        cullingGroup.SetBoundingDistances(cullingDistances);
        cullingGroup.SetDistanceReferencePoint(mainCam.transform);
        cullingGroup.SetBoundingSpheres(spheres);
    }

    private void RefreshCullingGroup()
    {
        // 최적화: 전체 갱신은 던전 시작 시나 대규모 변경 시에만 사용 (O(N))
        int count = activeTrees.Count;
        for (int i = 0; i < count; i++)
        {
            spheres[i].position = activeTrees[i].transform.position;
            spheres[i].radius = 3f;
            activeTrees[i].PoolIndex = i;
        }

        cullingGroup.SetBoundingSphereCount(count);

        activeTreesForUpdate.Clear();
        for (int i = 0; i < count; i++)
        {
            bool shouldBeActive = cullingGroup.IsVisible(i) && (cullingGroup.GetDistance(i) == 0);
            UpdateTreeVisibility(activeTrees[i], shouldBeActive);
        }
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (_ev.index >= activeTrees.Count) return;

        bool shouldBeActive = _ev.isVisible && (_ev.currentDistance == 0);
        UpdateTreeVisibility(activeTrees[_ev.index], shouldBeActive);
    }

    private void UpdateTreeVisibility(TreeObj _tree, bool _shouldBeActive)
    {
        if (_tree.bDead == true && _shouldBeActive == true)
            return;

        if (_tree.gameObject.activeSelf != _shouldBeActive)
        {
            _tree.gameObject.SetActive(_shouldBeActive);
        }

        if (_shouldBeActive)
        {
            if (_tree.UpdateIndex == -1)
            {
                _tree.UpdateIndex = activeTreesForUpdate.Count;
                activeTreesForUpdate.Add(_tree);
            }
        }
        else
        {
            int idx = _tree.UpdateIndex;
            if (idx != -1)
            {
                int lastIdx = activeTreesForUpdate.Count - 1;
                if (idx != lastIdx)
                {
                    TreeObj lastTree = activeTreesForUpdate[lastIdx];
                    activeTreesForUpdate[idx] = lastTree;
                    lastTree.UpdateIndex = idx;
                }
                activeTreesForUpdate.RemoveAt(lastIdx);
                _tree.UpdateIndex = -1;
            }
        }
    }

    private void ShufflePositions(List<Vector3> _list)
    {
        for (int i = 0; i < _list.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, _list.Count);
            Vector3 temp = _list[i];
            _list[i] = _list[randomIndex];
            _list[randomIndex] = temp;
        }
    }

    private void OnItemAcquired(Item _item)
    {
        ItemAcquiredEvent?.Invoke(_item);
    }

    private void BindPortalEvents()
    {
        if (offroadVehicle == null) return;
        offroadVehicle.PortalActivated -= OnPortalActivated;
        offroadVehicle.PortalActivated += OnPortalActivated;

        offroadVehicle.GameEndEvent -= GameEnd;
        offroadVehicle.GameEndEvent += GameEnd;

        offroadVehicle.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        offroadVehicle.OffroadInteractStateChangedEvent += OffroadInteractStateChanged;

        offroadVehicle.RepairBoxInteractStateChangedEvent -= RepairBoxInteractStateChanged;
        offroadVehicle.RepairBoxInteractStateChangedEvent += RepairBoxInteractStateChanged;
    }

    private void OnPortalActivated()
    {
        PortalActivatedEvent?.Invoke();
    }

    private void OnTreeDead(TreeObj _treeObj)
    {
        inDungeonVFXManager.PlayTreeDeadVFX(_treeObj.treeVisualComponent);

        environmentProvider.tilemapDataProvider.ClearTreeCollisionTile(_treeObj.transform.position);
        environmentProvider.densityProvider.UpdateTreeCnt(false);

        float dropMultiplier = 1f;
        if (treeGradeStatMultiplierDatas != null)
        {
            for (int i = 0; i < treeGradeStatMultiplierDatas.Count; i++)
            {
                if (treeGradeStatMultiplierDatas[i].treeGrade == _treeObj.treeData.grade)
                {
                    dropMultiplier = treeGradeStatMultiplierDatas[i].dropMultiplier;
                    break;
                }
            }
        }

        itemManager.SpawnLogItem(_treeObj, dropMultiplier);

        // 죽은 위치 재사용 준비
        Vector3 deadPos = _treeObj.transform.position;
        if (currentTreeGenerationStrategy != null)
        {
            currentTreeGenerationStrategy.OnTreeDead(this, deadPos);
        }

        treePool.Release(_treeObj);
        TreeDeadEvent?.Invoke(_treeObj.treeData.type);

        inDungeonResultManager.IncreaseTreeKillCnt();
    }

    // // 오브젝트 풀 콜백

    private TreeObj OnCreateTree()
    {
        TreeObj tree = Instantiate(treePrefab, transform);
        tree.Initialize(environmentProvider);
        return tree;
    }

    private TreeData CalculateRandomTreeData()
    {
        TreeType type = environmentProvider.densityProvider.GetTreeTypeToSpawn();
        TreeGrade grade = TreeGrade.Normal;

        // 1. 등급 결정 (히든 맵 또는 일반 던전 데이터 기반)
        if (hiddenMapGrade != HiddenMapGrade.None && hiddenMapTreeGradeDatas != null)
        {
            for (int i = 0; i < hiddenMapTreeGradeDatas.Count; i++)
            {
                if (hiddenMapTreeGradeDatas[i].grade == hiddenMapGrade)
                {
                    List<HiddenMapTreeGradeData> probList = hiddenMapTreeGradeDatas[i].probability;
                    if (probList != null && probList.Count > 0)
                    {
                        float rand = UnityEngine.Random.Range(0f, 100f);
                        float cumulative = 0f;
                        for (int j = 0; j < probList.Count; j++)
                        {
                            cumulative += probList[j].probability;
                            if (rand <= cumulative)
                            {
                                grade = probList[j].treeGrade;
                                break;
                            }
                        }
                    }
                    break;
                }
            }
        }
        else if (dungeonData.treeGradeProbs != null && dungeonData.treeGradeProbs.Count > 0)
        {
            float rand = UnityEngine.Random.Range(0f, 1f);
            float cumulative = 0f;
            for (int i = 0; i < dungeonData.treeGradeProbs.Count; i++)
            {
                cumulative += dungeonData.treeGradeProbs[i].probability;
                if (rand <= cumulative)
                {
                    grade = dungeonData.treeGradeProbs[i].grade;
                    break;
                }
            }
        }

        // 2. 스탯 계산 (배율 적용)
        TreeStatData statData = treeStatDataBase.Get(type);
        float multiplier = 1f;

        if (treeGradeStatMultiplierDatas != null)
        {
            for (int i = 0; i < treeGradeStatMultiplierDatas.Count; i++)
            {
                if (treeGradeStatMultiplierDatas[i].treeGrade == grade)
                {
                    multiplier = treeGradeStatMultiplierDatas[i].hpMultiplier;
                    break;
                }
            }
        }

        statData.hp *= multiplier;

        return new TreeData(type, grade, treeVisualDataBase.Get(type), statData);
    }

    private void OnGetTree(TreeObj _tree)
    {
        _tree.ApplyData(CalculateRandomTreeData());
        _tree.TreeDeadEvent -= OnTreeDead;
        _tree.TreeDeadEvent += OnTreeDead;
        _tree.TreeGetHitEvent -= OnTreeHit;
        _tree.TreeGetHitEvent += OnTreeHit;
    }

    private void OnReleaseTree(TreeObj _tree)
    {
        // 최적화: 업데이트 리스트에서 제거
        UpdateTreeVisibility(_tree, false);

        // 최적화: Swap-with-last O(1) 증분 업데이트로 마스터 리스트에서 제거
        int index = _tree.PoolIndex;
        if (index >= 0 && index < activeTrees.Count)
        {
            int lastIdx = activeTrees.Count - 1;
            if (index != lastIdx)
            {
                TreeObj lastTree = activeTrees[lastIdx];
                activeTrees[index] = lastTree;
                lastTree.PoolIndex = index;
                spheres[index] = spheres[lastIdx];
            }
            activeTrees.RemoveAt(lastIdx);

            if (cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(activeTrees.Count);
            }
        }

        _tree.bDead = true;
        _tree.PoolIndex = -1;
        _tree.UpdateIndex = -1;
        _tree.ResetTree();
        _tree.TreeDeadEvent -= OnTreeDead;
        _tree.TreeGetHitEvent -= OnTreeHit;
        //_tree.transform.position = new Vector2(-10000f, -10000f);
        _tree.gameObject.SetActive(false);
    }

    private void OnDestroyTree(TreeObj _tree)
    {
        if (_tree != null) Destroy(_tree.gameObject);
    }

    // // 유니티 이벤트 함수

    private void Update()
    {
        // 최적화 및 버그 수정: ManualUpdate 중 나무가 죽거나 상태가 변하여 리스트가 변형될 수 있으므로 역순 순회
        // 추가 최적화: 더 이상 업데이트가 필요 없는 경우(ManualUpdate가 false 반환 시) 즉시 O(1)로 제외
        for (int i = activeTreesForUpdate.Count - 1; i >= 0; i--)
        {
            TreeObj tree = activeTreesForUpdate[i];
            if (!tree.ManualUpdate())
            {
                int lastIdx = activeTreesForUpdate.Count - 1;
                if (i != lastIdx)
                {
                    TreeObj lastTree = activeTreesForUpdate[lastIdx];
                    activeTreesForUpdate[i] = lastTree;
                    lastTree.UpdateIndex = i;
                }
                activeTreesForUpdate.RemoveAt(lastIdx);
                tree.UpdateIndex = -1;
            }
        }
    }

    private void OnDestroy()
    {
        StopGrowth();
        ClearTrees();

        if (offroadVehicle != null)
        {
            offroadVehicle.PortalActivated -= OnPortalActivated;
            offroadVehicle.GameEndEvent -= GameEnd;
            offroadVehicle.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
            offroadVehicle.RepairBoxInteractStateChangedEvent -= RepairBoxInteractStateChanged;
        }

        if (cullingGroup != null)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }
    }

    private void OnTreeHit(TreeObj _treeObj)
    {
        inDungeonVFXManager.PlayTreeHitVFX(_treeObj.treeVisualComponent);

        if (currentTreeGenerationStrategy != null)
        {
            currentTreeGenerationStrategy.OnTreeGetHit(this, _treeObj);
        }
        TreeGetHitEvent?.Invoke(_treeObj);
    }

    public void CreateWelcomeNoobLoot()
    {
        if (lootManager == null)
            return;

        lootManager.AcquireLootItem(LootType.WelcomeNoob);
    }

    public void SpawnCarrots(Animal _animal)
    {
        itemManager.SpawnCarrotItem(_animal.transform.position, _animal.animalType);
    }

    private void CarrotItemAcquired(CarrotItem _item)
    {
        CarrotItemAcquiredEvent?.Invoke(_item);
    }

    public void SetHiddenMapGrade(HiddenMapGrade _hiddenMapGrade)
    {
        hiddenMapGrade = _hiddenMapGrade;
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
    }

    private void GameEnd()
    {
        if (CheckWarningUIActivate() == false)
        {
            HandleGameEnd();
            return;
        }

        StopGrowth();

        character.SetStaminaDecrease(false);
        character.PauseCharacter(true);
        inputManager.PauseMove(true);
        inputManager.PauseInteractKey(true);

        ActivateWarningUIEvent?.Invoke();
    }

    public void AbortGameEnd(bool _bAbort)
    {
        if (_bAbort == true)
            character.SetStaminaDecrease(true);

        character.PauseCharacter(false);
        inputManager.PauseMove(false);
        inputManager.PauseInteractKey(false);

        if (growthCoroutine == null && currentTreeGenerationStrategy != null)
        {
            growthCoroutine = StartCoroutine(currentTreeGenerationStrategy.GrowthRoutine(this));
        }
    }

    public void HandleGameEnd()
    {
        AbortGameEnd(false);

        character.DisableAttackComponent();
        character.SetStaminaDecrease(false);

        inputManager.PauseMove(true);
        inputManager.PauseInteractKey(true);

        if (inventoryChecker.bInventoryIsEmpty == false)
        {
            DropAllItemEvent?.Invoke();
            StartCoroutine(GameEndRoutine());
        }
        else
            RideOffroadEvent?.Invoke();
    }

    private IEnumerator GameEndRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        RideOffroadEvent?.Invoke();
    }

    private void OffroadInteractStateChanged(bool _boolean)
    {
        OffroadInteractStateChangedEvent?.Invoke(_boolean);
    }

    private void RepairBoxInteractStateChanged(bool _boolean)
    {
        RepairBoxInteractStateChangedEvent?.Invoke(_boolean);
    }

    private bool CheckWarningUIActivate()
    {
        if (offroadContainer.currentItemCount == offroadContainer.maxCapacity)
        {
            return false;
        }

        if (character.IsAxeDurabilityZero() == true && inventoryChecker.bInventoryIsEmpty == true)
        {
            return false;
        }

        return true;
    }

    public void IncreaseGrowthSpeed(float _amount)
    {
        growthSpeedMul += (_amount / 100f);
    }

    public void IncreaseRepairBoxCount(float _amount)
    {
        if(offroadVehicle != null)
        {
            offroadVehicle.IncreaseRepairBoxCount(_amount);
        }
    }

    public void IncreaseRepairAmount(float _amount)
    {
         if(offroadVehicle != null)
        {
            offroadVehicle.IncreaseRepairAmount(_amount);
        }
    }
}