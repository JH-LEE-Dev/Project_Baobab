using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using System.Collections;
using System;

public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider, IInDungeonObjManagerCH, IPathfindTreeProvider, ISporeShieldStatProvider
{
    // // 이벤트
    public event Action ActivateWarningUIEvent;
    public event Action DropAllItemEvent;
    public event Action RideOffroadEvent;
    public event Action<bool> OffroadInteractStateChangedEvent;
    public event Action<bool> RepairBoxInteractStateChangedEvent;
    public event Action<OffroadVehicleObj> OffroadSpawnedEvent;
    public event Action<TreeType, bool> TreeDeadEvent;
    public event Action PortalActivatedEvent;
    public event Action<Item> ItemAcquiredEvent;
    public event Action<CarrotItem> CarrotItemAcquiredEvent;
    public event Action<TreeObj> TreeGetHitEvent;
    public event Action<bool> NPCPauseRequestedEvent;
    public event Action FlyingItemPauseRequestedEvent;
    public event Action FlyingItemResumeRequestedEvent;
    public event Action FlyingItemDismissRequestedEvent;

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
    [SerializeField] private bool enableCulling = true;
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

    private TreeObj[] treeGridMap;
    private int gridWidth;

    public ITreeObj GetTreeAt(int _index)
    {
        if (_index < 0 || _index >= treeGridMap.Length) return null;
        return treeGridMap[_index];
    }

    public ITreeObj GetTreeAt(Vector3Int _cellPos)
    {
        int index = _cellPos.x + _cellPos.y * gridWidth;
        if (index < 0 || index >= treeGridMap.Length) return null;
        return treeGridMap[index];
    }

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

    // 포자막(Shield) 관련 스킬 스탯 - EHealthComponent가 ISporeShieldStatProvider로 읽어감
    private float shieldDamageMultiplier = 1f;   // 포자 절단
    private float shieldPenetrationPercent = 0f; // 포자 관통력
    private float shieldRegenReductionMul = 0f;  // 포자 회복 억제

    public float ShieldDamageMultiplier => shieldDamageMultiplier;
    public float ShieldPenetrationPercent => shieldPenetrationPercent;
    public float ShieldRegenReductionMul => shieldRegenReductionMul;

    // 포자막 폭발 관련 스킬 스탯
    [SerializeField] private LayerMask treeLayerForExplosion;
    private const float BaseShieldExplosionDamage = 50f;
    private const float BaseShieldExplosionRange = 1.5f; // 맞은 타일 주변 8개 이웃 타일(대각선 포함 3x3)이 포함되는 반경

    private bool bShieldExplosionUnlocked = false;
    private float shieldExplosionDamageMultiplier = 1f;
    private float shieldExplosionRangeMultiplier = 1f;
    private float shieldExplosionResearchChance = 0f;

    // 포자막 폭발 VFX - top/bottom 주변에서 인터벌을 두고 연쇄적으로 재생
    private const int SporeExplosionVfxMinCount = 4;
    private const int SporeExplosionVfxMaxCount = 5;
    private const float SporeExplosionVfxInterval = 0.08f;
    private const float SporeExplosionVfxSpread = 0.25f;
    // 코루틴 루프에서 매번 new WaitForSeconds를 생성하면 반복마다 GC 쓰레기가 생기므로 캐싱해서 재사용한다.
    private static readonly WaitForSeconds sporeExplosionVfxWait = new WaitForSeconds(SporeExplosionVfxInterval);

    // 별자리(Constellation) 관련 스킬 스탯 - StarrootForest 전용
    private const float BaseConstellationDamage = 5000f;
    private const float ConstellationBeamHalfThickness = 0.5f; // 선분 중심 기준 좌우 0.5씩(전체 폭 1.0)
    private const float ConstellationHitInterval = 0.2f;
    private static readonly WaitForSeconds constellationHitWait = new WaitForSeconds(ConstellationHitInterval);

    // 별자리 발현 광선 VFX - Drone의 연쇄 타격과 동일한 VFX_LightningZap을 재사용한다. 서로 다른 그룹이
    // 동시에 발현될 수 있으므로(풀링 없는 전용 인스턴스 하나로는 나중 광선이 먼저 광선을 덮어써버림)
    // 풀에서 매번 새 인스턴스를 꺼내 쓴다. 색상은 SetColor로 덮어쓰지 않고 프리팹 기본값을 그대로 쓴다.
    [SerializeField] private LightningZapCreator lightningZapCreator;

    private bool bConstellationManifestUnlocked = false;
    private float starMarkDamageMultiplier = 1f;          // 별표식 베기
    private float constellationDamageMultiplier = 1f;     // 별자리 데미지
    private int constellationHitCount = 1;                // 별자리 잔상
    private float manifestationBrandBonusMultiplier = 0f; // 발현 낙인

    // 그룹별 재진입 방지 가드 - groupStarPositions[groupId]와 동일한 List<Vector3> 참조를 키로 쓴다.
    // 광선 자신의 데미지가 같은 그룹의 다른 별 표식 나무를 맞혀 재귀적으로 재트리거하는 것만 막고,
    // 서로 다른 그룹(다른 List 인스턴스)은 동시에 진행돼도 서로 막지 않는다.
    private readonly HashSet<List<Vector3>> activeConstellationGroups = new HashSet<List<Vector3>>();

    // 발현 낙인이 찍힌 나무 위에서 반복 재생되는 스파크 VFX(VFX_Spark) - 낙인은 나무가 죽어 리셋될 때까지
    // 유지되므로(EHealthComponent.brandedDamageMultiplier), 그동안 인터벌마다 계속 재생한다. 매번 똑같은
    // 박자로 반짝이면 기계적으로 보이므로 인터벌 자체를 매번 랜덤화한다.
    private const float ManifestationBrandVfxIntervalMin = 0.5f;
    private const float ManifestationBrandVfxIntervalMax = 0.75f;
    // 나무 한 그루에 낙인 코루틴이 중복으로 여러 개 돌지 않도록(같은 나무가 여러 발현/여러 선분에 맞을 수 있음) 추적
    private readonly HashSet<TreeObj> manifestationBrandVfxTrees = new HashSet<TreeObj>();

    public float StarMarkDamageMultiplier => starMarkDamageMultiplier;

    // 별의 주시(Star Gaze) - 모든 숲에서 발동, 화면 범위 내 가장 가까운 나무에 주기적으로 별똥별 낙하
    private const float StarGazeInterval = 10f;
    private const float StarGazeDamage = 10000f;
    private const float StarGazeImpactRange = BaseShieldExplosionRange; // 포자막 폭발과 동일한 범위(고정값, 업그레이드와 무관)

    private bool bStarGazeUnlocked = false;
    private Coroutine starGazeCoroutine;

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

        lightningZapCreator?.Initialize();

        gridWidth = environmentProvider.tilemapDataProvider.GridWidth;
        int gridHeight = environmentProvider.tilemapDataProvider.GridHeight;
        treeGridMap = new TreeObj[gridWidth * gridHeight];

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
        // ResetPortal() 내부에서 발밑 타일 재등록 코루틴(StartCoroutine)을 실행하므로,
        // 코루틴이 비활성 상태의 게임 오브젝트에서 시작 실패하지 않도록 활성화를 먼저 한다.
        offroadVehicle.gameObject.SetActive(true);
        offroadVehicle.ResetPortal();
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

        // 별의 주시: 맵 전환과 무관하게 매니저 생애주기 동안 한 번만 시작해 계속 순환시킨다
        // (루틴 내부에서 currentMapType/스킬 해금 여부를 매 주기 확인하므로 재시작할 필요가 없다).
        if (starGazeCoroutine == null)
        {
            starGazeCoroutine = StartCoroutine(StarGazeRoutine());
        }
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

            int flatIdx = cellPos.x + cellPos.y * gridWidth;
            if (flatIdx >= 0 && flatIdx < treeGridMap.Length)
            {
                treeGridMap[flatIdx] = tree;
            }

            if (enableCulling)
            {
                if (spheres.Length <= tree.PoolIndex)
                {
                    System.Array.Resize(ref spheres, Mathf.Max(spheres.Length * 2, tree.PoolIndex + 1));
                    cullingGroup.SetBoundingSpheres(spheres);
                }
                spheres[tree.PoolIndex] = new BoundingSphere(_spawnPos, 3f);
            }

            environmentProvider.tilemapDataProvider.SetTreeCollisionTile(_spawnPos);
            environmentProvider.densityProvider.UpdateTreeCnt(true);

            if (enableCulling && cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(activeTrees.Count);
                UpdateTreeVisibility(tree, cullingGroup.IsVisible(tree.PoolIndex) && (cullingGroup.GetDistance(tree.PoolIndex) == 0));
            }
            else
            {
                tree.gameObject.SetActive(true);
                // Instantiate로 처음 생성된 나무는 이미 active 상태라 SetActive(true)가 OnEnable을 재실행하지 않음
                // 따라서 수동으로 올바른 위치에 재등록 (Register 내부에서 중복 등록은 안전하게 처리됨)
                CollisionSystem.Instance?.Register(tree, true);

                if (tree.UpdateIndex == -1)
                {
                    tree.UpdateIndex = activeTreesForUpdate.Count;
                    activeTreesForUpdate.Add(tree);
                }
            }

            if (_isGrowing)
            {
                // speedMul이 1 이상이 되어 시간이 0이 되면 무한루프에 빠질 수 있으므로, 최소 성장 시간(0.1초) 보장
                float scaledGrowTime = Mathf.Max(0.1f, treeGrowTime * (1f - growthSpeedMul));
                tree.SetIsSapling(true, scaledGrowTime);
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

                // 개별 나무 반환이 실패(예: 이미 반환된 나무 재반환)하더라도 전체 정리 루프가
                // 중단되어 나머지 나무가 다음 스테이지까지 남아버리는 일이 없도록 방어한다.
                try
                {
                    treePool.Release(activeTrees[i]);
                }
                catch (System.InvalidOperationException e)
                {
                    Debug.LogWarning($"[InDungeonObjectManager] 나무 정리 중 이미 반환된 오브젝트를 건너뜁니다: {e.Message}");
                }
            }
        }
        activeTrees.Clear();
        activeTreesForUpdate.Clear();
        System.Array.Clear(treeGridMap, 0, treeGridMap.Length);
        if (enableCulling && cullingGroup != null) cullingGroup.SetBoundingSphereCount(0);
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
        if (!enableCulling) return;

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
        if (!enableCulling || cullingGroup == null) return;

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
        if (!enableCulling) return;
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
        // 이미 풀로 반환된(= 활성 목록에서 빠진) 나무가 중복 사망 이벤트로 다시 들어오면
        // 여기서 걸러서 아이템 중복 지급, 밀도 카운트 오염, 풀 이중 반환 예외를 막는다.
        if (_treeObj.PoolIndex == -1) return;

        // 폭발 연구: 뭉글 포자 숲이 아닌 곳에서도 나무 벌목 시 일정 확률로 포자막 폭발 발생
        if (bShieldExplosionUnlocked && currentMapType != MapType.FluffySporeForest && shieldExplosionResearchChance > 0f)
        {
            if (UnityEngine.Random.value < shieldExplosionResearchChance)
            {
                TriggerShieldExplosion(_treeObj);
            }
        }

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
            currentTreeGenerationStrategy.OnTreeDead(this, _treeObj, deadPos);
        }

        treePool.Release(_treeObj);
        TreeDeadEvent?.Invoke(_treeObj.treeData.type, _treeObj.bLastHitByPlayer);

        inDungeonResultManager.IncreaseTreeKillCnt();
    }

    // // 오브젝트 풀 콜백

    private TreeObj OnCreateTree()
    {
        TreeObj tree = Instantiate(treePrefab, transform);
        tree.Initialize(environmentProvider, this);
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
        _tree.TreeShieldBrokenEvent -= OnTreeShieldBroken;
        _tree.TreeShieldBrokenEvent += OnTreeShieldBroken;
    }

    private void OnReleaseTree(TreeObj _tree)
    {
        // 최적화: 업데이트 리스트에서 제거
        UpdateTreeVisibility(_tree, false);

        Vector3Int cellPos = environmentProvider.tilemapDataProvider.WorldToCell(_tree.transform.position);
        int flatIdx = cellPos.x + cellPos.y * gridWidth;
        if (flatIdx >= 0 && flatIdx < treeGridMap.Length)
        {
            treeGridMap[flatIdx] = null;
        }

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
                if (spheres != null) spheres[index] = spheres[lastIdx];
            }
            activeTrees.RemoveAt(lastIdx);

            if (enableCulling && cullingGroup != null)
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
        _tree.TreeShieldBrokenEvent -= OnTreeShieldBroken;
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
            NPCPauseRequestedEvent?.Invoke(true);
            FlyingItemDismissRequestedEvent?.Invoke();
            HandleGameEnd();
            return;
        }

        NPCPauseRequestedEvent?.Invoke(true);
        FlyingItemPauseRequestedEvent?.Invoke();
        character.PauseBoomerangs();

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
        {
            character.SetStaminaDecrease(true);
            NPCPauseRequestedEvent?.Invoke(false);
            FlyingItemResumeRequestedEvent?.Invoke();
            character.ResumeBoomerangs();
        }

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
        FlyingItemDismissRequestedEvent?.Invoke();
        character.DismissBoomerangsWithShrink();

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

    public void IncreaseShieldDamageMultiplier(float _amount)
    {
        shieldDamageMultiplier += (_amount / 100f);
    }

    public void IncreaseShieldPenetration(float _amount)
    {
        shieldPenetrationPercent += (_amount / 100f);
    }

    public void IncreaseShieldRegenReduction(float _amount)
    {
        shieldRegenReductionMul += (_amount / 100f);
    }

    public void UnlockShieldExplosion(bool _boolean)
    {
        bShieldExplosionUnlocked = _boolean;
    }

    public void IncreaseShieldExplosionDamage(float _amount)
    {
        shieldExplosionDamageMultiplier += (_amount / 100f);
    }

    public void IncreaseShieldExplosionRange(float _amount)
    {
        shieldExplosionRangeMultiplier += (_amount / 100f);
    }

    public void IncreaseShieldExplosionResearchChance(float _amount)
    {
        shieldExplosionResearchChance += (_amount / 100f);
    }

    private void OnTreeShieldBroken(TreeObj _treeObj)
    {
        // 포자막 파괴 VFX는 포자막 폭발 스킬/맵 여부와 무관하게 항상 재생된다.
        inDungeonVFXManager.PlayShieldBrokenVFX(_treeObj.treeVisualComponent, _treeObj.GetTreeType());

        if (!bShieldExplosionUnlocked) return;
        if (currentMapType != MapType.FluffySporeForest) return;

        TriggerShieldExplosion(_treeObj);
    }

    private void TriggerShieldExplosion(TreeObj _source)
    {
        StartCoroutine(PlaySporeExplosionVfxRoutine(_source));

        float damage = BaseShieldExplosionDamage * Mathf.Max(0f, shieldExplosionDamageMultiplier);
        float range = BaseShieldExplosionRange * Mathf.Max(0f, shieldExplosionRangeMultiplier);

        if (CollisionSystem.Instance == null) return;

        // 재귀적인 연쇄 폭발(TakeDamage -> ShieldBrokenEvent -> TriggerShieldExplosion) 도중
        // 공유 버퍼가 덮어써지는 것을 막기 위해 매 호출마다 로컬 리스트를 사용한다.
        List<IStaticCollidable> scanResults = new List<IStaticCollidable>(32);
        CollisionSystem.Instance.GetCollidablesInRadius(_source.Position, range, treeLayerForExplosion.value, scanResults);

        Vector3 centerPos = _source.transform.position;
        float rangeSq = range * range;

        for (int i = 0; i < scanResults.Count; i++)
        {
            if (scanResults[i] is TreeObj tree && tree != _source && tree.bCanApplyDamage)
            {
                Vector3 targetPos = tree.transform.position;
                float dx = targetPos.x - centerPos.x;
                float dy = (targetPos.y - centerPos.y) * 2f; // 등각 타원 보정 (ShockWave와 동일 공식)
                float isoDistSq = dx * dx + dy * dy;

                if (isoDistSq <= rangeSq)
                {
                    tree.TakeDamage(damage);
                }
            }
        }
    }

    private IEnumerator PlaySporeExplosionVfxRoutine(TreeObj _source)
    {
        if (_source == null) yield break;

        Vector3 bottomPos = _source.transform.position;
        Vector3 topPos = _source.treeVisualComponent != null ? _source.treeVisualComponent.GetTopRootPosition() : bottomPos;

        int count = UnityEngine.Random.Range(SporeExplosionVfxMinCount, SporeExplosionVfxMaxCount + 1);

        for (int i = 0; i < count; i++)
        {
            Vector3 basePos = (i % 2 == 0) ? topPos : bottomPos;
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-SporeExplosionVfxSpread, SporeExplosionVfxSpread),
                UnityEngine.Random.Range(-SporeExplosionVfxSpread, SporeExplosionVfxSpread),
                0f);

            SporeExplosionVFX.Spawn(basePos + randomOffset);

            yield return sporeExplosionVfxWait;
        }
    }

    public void UnlockConstellationManifest(bool _boolean)
    {
        bConstellationManifestUnlocked = _boolean;
    }

    public void IncreaseStarMarkDamage(float _amount)
    {
        starMarkDamageMultiplier += (_amount / 100f);
    }

    public void IncreaseConstellationDamage(float _amount)
    {
        constellationDamageMultiplier += (_amount / 100f);
    }

    public void IncreaseConstellationHitCount(float _amount)
    {
        constellationHitCount += Mathf.RoundToInt(_amount);
    }

    public void IncreaseManifestationBrandBonus(float _amount)
    {
        manifestationBrandBonusMultiplier += (_amount / 100f);
    }

    // 별길 걸음 - 별 표식 나무 벌목 시 Stage3TreeGenerationStrategySO가 호출
    public void TriggerStarPathSpeedBoost()
    {
        character?.statComponent?.ActivateStarPathSpeedBoost();
    }

    // 별자리 발현 - 그룹의 모든 별 표식 나무가 벌목되면 Stage3TreeGenerationStrategySO가 호출
    public void TriggerConstellationManifestation(List<Vector3> _starPositions)
    {
        if (!bConstellationManifestUnlocked) return;
        if (_starPositions == null || _starPositions.Count < 2) return;

        // 광선 자체의 데미지(ApplyConstellationBeamDamage -> TakeDamage)가 같은 그룹의 다른 별 표식
        // 나무를 맞히면 TreeGetHitEvent가 다시 발생해 OnTreeGetHit -> TriggerConstellationManifestation을
        // 재귀적으로 다시 호출하는 문제가 있었다(같은 프레임 안에서 StartCoroutine이 첫 yield 전까지
        // 동기 실행되므로 재귀가 그대로 쌓임). _starPositions(그룹별로 항상 같은 List 인스턴스)를 키로
        // 재진입만 막아서, 같은 그룹의 자기 재트리거는 막되 서로 다른 그룹은 동시에 진행되게 둔다.
        if (activeConstellationGroups.Contains(_starPositions)) return;

        List<Vector3> path = BuildSimplePolygonPath(_starPositions);
        float damage = BaseConstellationDamage * Mathf.Max(0f, constellationDamageMultiplier);
        int hitCount = Mathf.Max(1, constellationHitCount);

        activeConstellationGroups.Add(_starPositions);
        StartCoroutine(ConstellationBeamRoutine(_starPositions, path, damage, hitCount));
    }

    // 중심점(centroid) 기준 각도 순으로 정렬해서 폐곡선을 만든다. 최근접 이웃(greedy) 방식과 달리,
    // 각도가 단조 증가하는 순서로만 변을 이으면 두 변이 서로 교차할 수 없다는 성질이 수학적으로
    // 보장되므로 - 어떤 별 배치에서도 항상 자기교차 없는 단순 다각형(simple polygon)이 나온다.
    // 최단 경로는 아닐 수 있지만, 별자리 그룹이 2~5개뿐이라 비용도 무시할 만하고 꼬임 방지가 우선이다.
    private List<Vector3> BuildSimplePolygonPath(List<Vector3> _points)
    {
        List<Vector3> sorted = new List<Vector3>(_points);

        Vector3 centroid = Vector3.zero;
        for (int i = 0; i < sorted.Count; i++) centroid += sorted[i];
        centroid /= sorted.Count;

        // 등각 투영 보정 - 기존 최근접 판정과 동일하게 Y축을 2배로 보정해서, 화면상 배치와 일관된
        // 순서로 각도를 계산한다.
        sorted.Sort((a, b) =>
        {
            float angleA = Mathf.Atan2((a.y - centroid.y) * 2f, a.x - centroid.x);
            float angleB = Mathf.Atan2((b.y - centroid.y) * 2f, b.x - centroid.x);
            return angleA.CompareTo(angleB);
        });

        // 별이 3개 이상이면 마지막 점에서 시작점으로 되돌아가는 구간을 하나 더 추가해서 다각형(폐곡선)을
        // 이루도록 한다. 별이 2개뿐이면 폐곡선을 만들 수 없으므로(다시 그으면 같은 선분을 중복으로
        // 왕복하게 됨) 그대로 열린 선 하나만 둔다.
        if (sorted.Count >= 3)
        {
            sorted.Add(sorted[0]);
        }

        return sorted;
    }

    private IEnumerator ConstellationBeamRoutine(List<Vector3> _groupKey, List<Vector3> _path, float _damagePerHit, int _hitCount)
    {
        try
        {
            for (int hit = 0; hit < _hitCount; hit++)
            {
                ApplyConstellationBeamDamage(_path, _damagePerHit);

                if (hit < _hitCount - 1)
                {
                    yield return constellationHitWait;
                }
            }
        }
        finally
        {
            activeConstellationGroups.Remove(_groupKey);
        }
    }

    private void ApplyConstellationBeamDamage(List<Vector3> _path, float _damage)
    {
        // 풀에서 매번 새 인스턴스를 꺼낸다 - 서로 다른 그룹(또는 같은 그룹의 다음 펄스)이 동시에 재생
        // 중이어도 각자 독립된 LineRenderer/트윈을 쓰므로 서로 덮어쓰지 않는다. 재생이 끝나면
        // VFX_LightningZap이 스스로 ReturnToPoolEvent를 발생시켜 자동으로 풀에 반환된다.
        lightningZapCreator?.Get()?.PlayZap(_path, _path.Count);

        if (CollisionSystem.Instance == null) return;

        bool bBrandActive = manifestationBrandBonusMultiplier > 0f;

        // 광선은 나무 8~13그루짜리 국지적 군집에만 영향을 주므로, 던전 전체 activeTrees(최대 2500개)를
        // 매 타격 틱마다 통째로 복사/순회하지 않도록 경로를 감싸는 반경만 CollisionSystem으로 조회한다.
        Vector3 center = Vector3.zero;
        for (int i = 0; i < _path.Count; i++) center += _path[i];
        center /= _path.Count;

        float maxDistSq = 0f;
        for (int i = 0; i < _path.Count; i++)
        {
            float distSq = (_path[i] - center).sqrMagnitude;
            if (distSq > maxDistSq) maxDistSq = distSq;
        }
        float scanRadius = Mathf.Sqrt(maxDistSq) + ConstellationBeamHalfThickness + 1f;

        List<IStaticCollidable> scanResults = new List<IStaticCollidable>(64);
        CollisionSystem.Instance.GetCollidablesInRadius(center, scanRadius, treeLayerForExplosion.value, scanResults);

        for (int i = 0; i < scanResults.Count; i++)
        {
            if (!(scanResults[i] is TreeObj tree) || !tree.bCanApplyDamage) continue;

            Vector3 rootPos = tree.transform.position;
            Vector3 topPos = tree.treeVisualComponent != null ? tree.treeVisualComponent.GetTopRootPosition() : rootPos;
            bool bHit = false;

            // 밑둥/top 둘 중 하나라도 광선(선분) 두께 안에 들어오면 타격으로 처리 (Boomerang과 동일한 판정 방식)
            for (int seg = 0; seg < _path.Count - 1 && !bHit; seg++)
            {
                bHit = DistancePointToSegment(topPos, _path[seg], _path[seg + 1]) <= ConstellationBeamHalfThickness
                    || DistancePointToSegment(rootPos, _path[seg], _path[seg + 1]) <= ConstellationBeamHalfThickness;
            }

            if (bHit)
            {
                tree.TakeDamage(_damage);

                // TakeDamage로 나무가 죽으면 즉시 풀로 반환되며 ResetTree()가 브랜드 배율을 1로 되돌리므로,
                // 죽지 않고 살아남은 나무에만 낙인을 적용한다.
                if (bBrandActive && !tree.bDead)
                {
                    tree.health.ApplyDamageBrand(1f + manifestationBrandBonusMultiplier);
                    StartManifestationBrandVfx(tree);
                }
            }
        }
    }

    // 낙인이 찍힌 나무에 대해 주기 재생 VFX 루틴을 시작한다(이미 이 나무에 루틴이 돌고 있다면 무시).
    // HashSet.Add가 처음 추가될 때만 true를 반환하므로 자연스러운 가드가 된다. Stage3TreeGenerationStrategySO
    // 등 외부에서도(테스트용 강제 낙인 등) 재사용할 수 있도록 public으로 둔다.
    public void StartManifestationBrandVfx(TreeObj _tree)
    {
        if (_tree == null) return;

        if (manifestationBrandVfxTrees.Add(_tree))
        {
            StartCoroutine(PlayManifestationBrandVfxRoutine(_tree));
        }
    }

    // 낙인이 찍힌 나무 위에서 [ManifestationBrandVfxIntervalMin, Max] 사이로 랜덤화된 인터벌마다
    // VFX_Spark를 재생한다. 종료 조건은 반드시 health.IsBranded(실제 낙인 배율 상태)로만 판단해야 한다 -
    // bDead는 나무가 죽는 순간 OnTreeDead -> treePool.Release -> ResetTree()가 같은 프레임 안에서 다시
    // false로 되돌리고, gameObject.activeInHierarchy도 죽음뿐 아니라 카메라 컬링(UpdateTreeVisibility)으로
    // 살아있는 동안에도 꺼졌다 켜졌다 하므로 둘 다 "이 나무가 여전히 낙인 상태인지"를 판단하는 데 쓸 수
    // 없다. 나무가 죽으면 ResetTree()가 브랜드 배율을 1로 되돌리므로 IsBranded가 정확히 false가 되고,
    // 풀에서 재사용되어 전혀 다른 나무가 되어도(재낙인되지 않는 한) 계속 false를 유지한다.
    private IEnumerator PlayManifestationBrandVfxRoutine(TreeObj _tree)
    {
        try
        {
            while (_tree != null && _tree.health != null && _tree.health.IsBranded)
            {
                if (_tree.treeVisualComponent != null)
                {
                    inDungeonVFXManager.PlayManifestationBrandVFX(_tree.treeVisualComponent);
                }

                yield return new WaitForSeconds(UnityEngine.Random.Range(ManifestationBrandVfxIntervalMin, ManifestationBrandVfxIntervalMax));
            }
        }
        finally
        {
            manifestationBrandVfxTrees.Remove(_tree);
        }
    }

    // 광선은 두 지점을 잇는 고정 폭 캡슐 형태라, 원형 AoE(ShockWave 등)와 달리 등각 보정이 필요 없다.
    // 보정을 넣으면 오히려 광선 방향에 따라 두께가 달라지는 문제가 생긴다.
    private static float DistancePointToSegment(Vector3 _point, Vector3 _segA, Vector3 _segB)
    {
        Vector3 ab = _segB - _segA;
        float abLenSq = ab.sqrMagnitude;

        if (abLenSq < 0.0001f) return Vector3.Distance(_point, _segA);

        float t = Mathf.Clamp01(Vector3.Dot(_point - _segA, ab) / abLenSq);
        Vector3 projection = _segA + ab * t;
        return Vector3.Distance(_point, projection);
    }

    public void UnlockStarGaze(bool _boolean)
    {
        bStarGazeUnlocked = _boolean;
    }

    private IEnumerator StarGazeRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(StarGazeInterval);

            if (!bStarGazeUnlocked) continue;
            if (character == null) continue;

            TreeObj nearest = FindNearestTreeInScreenEllipse();
            if (nearest == null) continue;

            Vector3 landingPos = nearest.transform.position;
            int explosionSortingOrder = nearest.treeVisualComponent != null
                ? nearest.treeVisualComponent.GetTopHighlightSortingOrder() + 1
                : 0;

            ShootingStarVFX.Spawn(landingPos, explosionSortingOrder, () =>
            {
                inDungeonVFXManager.PlayStarImpactExplosionVFX(landingPos, explosionSortingOrder);
                ApplyStarImpactDamage(landingPos);
                CameraMoveController.Instance?.ShakeCamera(9f, 0.45f);
            });
        }
    }

    // "타원 범위로(화면 전체) 나무가 있다면, 가장 가까운 나무로 별똥별이 떨어진다" -
    // 화면(카메라 타원) 경계 안에 있는 나무들 중에서만 등각 거리 기준 최근접 나무를 찾는다.
    private TreeObj FindNearestTreeInScreenEllipse()
    {
        Vector3 charPos = character.transform.position;
        TreeObj nearest = null;
        float nearestIsoSqr = float.MaxValue;

        for (int i = 0; i < activeTrees.Count; i++)
        {
            TreeObj tree = activeTrees[i];
            if (tree == null || tree.bDead || !tree.bCanApplyDamage) continue;

            Vector3 treePos = tree.transform.position;
            Vector3 dirToTree = treePos - charPos;
            float actualDist = dirToTree.magnitude;
            if (actualDist < 0.001f) continue;

            float maxDist = CameraBoundsUtil.GetMaxDistanceToEdge(dirToTree, 0f, 1f);
            if (maxDist <= 0.1f || actualDist > maxDist) continue; // 화면 타원 범위 밖

            float dx = treePos.x - charPos.x;
            float dy = (treePos.y - charPos.y) * 2f; // 등각 보정
            float isoSqr = dx * dx + dy * dy;

            if (isoSqr < nearestIsoSqr)
            {
                nearestIsoSqr = isoSqr;
                nearest = tree;
            }
        }

        return nearest;
    }

    private void ApplyStarImpactDamage(Vector3 _landingPos)
    {
        if (CollisionSystem.Instance == null) return;

        List<IStaticCollidable> scanResults = new List<IStaticCollidable>(32);
        CollisionSystem.Instance.GetCollidablesInRadius(_landingPos, StarGazeImpactRange, treeLayerForExplosion.value, scanResults);

        float rangeSq = StarGazeImpactRange * StarGazeImpactRange;

        for (int i = 0; i < scanResults.Count; i++)
        {
            if (scanResults[i] is TreeObj tree && tree.bCanApplyDamage)
            {
                Vector3 targetPos = tree.transform.position;
                float dx = targetPos.x - _landingPos.x;
                float dy = (targetPos.y - _landingPos.y) * 2f; // 등각 타원 보정
                float isoDistSq = dx * dx + dy * dy;

                if (isoDistSq <= rangeSq)
                {
                    tree.TakeDamage(StarGazeDamage);
                }
            }
        }
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