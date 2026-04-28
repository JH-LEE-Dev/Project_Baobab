using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using System.Collections;
using System;
using Unity.VisualScripting;

public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider
{
    // // 이벤트
    public event Action PortalActivatedEvent;
    public event Action<Item> ItemAcquiredEvent;
    public event Action<CarrotItem> CarrotItemAcquiredEvent;
    public event Action<TreeObj> TreeGetHitEvent;

    // // 외부 의존성
    private IEnvironmentProvider environmentProvider;
    public ItemManager itemManager { get; private set; }
    private DungeonData dungeonData;
    private LootManager lootManager;

    // // 내부 의존성
    [Header("Tree Settings")]
    [SerializeField] private TreeObj treePrefab;

    [Header("Optimization")]
    [SerializeField] private float cullingDistance = 25f;

    [Header("Portal")]
    [SerializeField] private PortalObj portalPrefab;

    // // 내부 상태 및 컬렉션
    private PortalObj portal;
    private List<Vector3> grassTileWorldPositions;
    private List<Vector3> availablePositions = new List<Vector3>(2500);
    private List<TreeObj> activeTrees = new List<TreeObj>(2500);

    // 최적화: HashSet을 사용하여 Contains 중복 체크 속도 향상 (O(1))
    private List<TreeObj> activeTreesForUpdate = new List<TreeObj>(2500);
    public IReadOnlyList<TreeObj> ActiveTrees => activeTreesForUpdate;
    private HashSet<TreeObj> activeTreesForUpdateSet = new HashSet<TreeObj>(2500);

    private IObjectPool<TreeObj> treePool;
    private Coroutine growthCoroutine;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;
    private bool isCullingDirty = false;
    private Camera mainCam; // 최적화: 카메라 캐싱

    public IReadOnlyList<ITreeObj> trees => activeTrees;

    // // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(IEnvironmentProvider _environmentProvider, DungeonData _dungeonData, IInventoryChecker _inventoryChecker)
    {
        environmentProvider = _environmentProvider;
        dungeonData = _dungeonData;
        mainCam = Camera.main;

        itemManager = GetComponentInChildren<ItemManager>();
        itemManager.Initialize(_inventoryChecker);

        lootManager = GetComponentInChildren<LootManager>();
        lootManager.Initialize();

        cullingDistances = new float[] { cullingDistance };
        spheres = new BoundingSphere[1000];
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

        if (portal != null)
        {
            portal.PortalActivated -= OnPortalActivated;
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
        if (portal == null)
        {
            portal = Instantiate(portalPrefab, transform);
            portal.Initialize(PortalType.ToTownPortal);
        }

        portal.ResetPortal();
        portal.transform.position = environmentProvider.tilemapDataProvider.GetPortalSpawnPosition();

        BindPortalEvents();
    }

    public Vector3 GetPlayerStartPos()
    {
        return environmentProvider.tilemapDataProvider.GetPlayerSpawnPosition();
    }

    public void ClearObjManager()
    {
        if (portal != null)
            portal.gameObject.SetActive(false);
        else
            return;
            
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

        // 1. 위치 목록 준비 및 셔플
        availablePositions.Clear();
        for (int i = 0; i < grassTileWorldPositions.Count; i++)
        {
            availablePositions.Add(grassTileWorldPositions[i]);
        }
        ShufflePositions(availablePositions);

        // 2. 초기 개수 스폰
        int startCount = environmentProvider.densityProvider.GetTreeStartCnt();
        for (int i = 0; i < startCount; i++)
        {
            SpawnOneTreeFromAvailable();
        }

        RefreshCullingGroup();

        // 3. 5초 후 성장 루틴 시작
        growthCoroutine = StartCoroutine(StartGrowthAfterDelay());
    }

    private IEnumerator StartGrowthAfterDelay()
    {
        yield return new WaitForSeconds(5f);
        growthCoroutine = StartCoroutine(GrowthRoutine());
    }

    private bool SpawnOneTreeFromAvailable()
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
            if (!environmentProvider.pathfindGridProvider.IsOccupied(cellPos))
            {
                int lastIdx = availablePositions.Count - 1;
                // Swap-with-last for O(1) removal
                availablePositions[checkIdx] = availablePositions[lastIdx];
                availablePositions.RemoveAt(lastIdx);

                TreeObj tree = treePool.Get();
                tree.transform.position = spawnPos;
                tree.gameObject.SetActive(true);
                activeTrees.Add(tree);

                environmentProvider.tilemapDataProvider.SetTreeCollisionTile(spawnPos);
                environmentProvider.densityProvider.UpdateTreeCnt(true);
                //Debug.Log($"<color=yellow>[InDungeonObjectManager]</color> Tree Spawned. Current Active Trees: {activeTrees.Count}");
                return true;
            }
        }

        return false;
    }

    private IEnumerator GrowthRoutine()
    {
        while (true)
        {
            float interval = environmentProvider.densityProvider.GetTreeRegenTime();
            yield return new WaitForSeconds(interval);

            if (environmentProvider.densityProvider.CanCreateTree() && availablePositions.Count > 0)
            {
                // 성공적으로 생성했을 때만 컬링 그룹 갱신 플래그 설정
                if (SpawnOneTreeFromAvailable())
                {
                    isCullingDirty = true;
                }
            }
        }
    }

    private void ClearTrees()
    {
        // 역순 순회로 안전하게 해제
        for (int i = activeTrees.Count - 1; i >= 0; i--)
        {
            if (activeTrees[i] != null)
            {
                environmentProvider.tilemapDataProvider.ClearTreeCollisionTile(activeTrees[i].transform.position);
                environmentProvider.densityProvider.UpdateTreeCnt(false);

                // 순서 변경: 먼저 풀에 반환(OnDisable 호출)하여 정상적으로 Unregister 되게 함
                treePool.Release(activeTrees[i]);
                // 해제된 객체의 위치를 나중에 옮김
                activeTrees[i].transform.position = new Vector2(-10000f, -10000f);
            }
        }
        activeTrees.Clear();
        activeTreesForUpdate.Clear();
        activeTreesForUpdateSet.Clear();
        isCullingDirty = true;
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

        if (mainCam == null) mainCam = Camera.main;
        cullingGroup.targetCamera = mainCam;
        cullingGroup.SetBoundingDistances(cullingDistances);
        cullingGroup.SetDistanceReferencePoint(mainCam.transform);
    }

    private void RefreshCullingGroup()
    {
        int count = activeTrees.Count;
        if (spheres == null || spheres.Length < count)
        {
            spheres = new BoundingSphere[Mathf.Max(count + 100, 1000)];
        }

        for (int i = 0; i < count; i++)
        {
            spheres[i].position = activeTrees[i].transform.position;
            spheres[i].radius = 3f;
        }

        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(count);

        activeTreesForUpdate.Clear();
        activeTreesForUpdateSet.Clear();
        for (int i = 0; i < count; i++)
        {
            bool isVisible = cullingGroup.IsVisible(i);
            bool isNear = cullingGroup.GetDistance(i) == 0;
            bool shouldBeActive = isVisible && isNear;

            activeTrees[i].gameObject.SetActive(shouldBeActive);
            if (shouldBeActive)
            {
                activeTreesForUpdate.Add(activeTrees[i]);
                activeTreesForUpdateSet.Add(activeTrees[i]);
            }
        }
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (_ev.index >= activeTrees.Count) return;

        bool shouldBeActive = _ev.isVisible && (_ev.currentDistance == 0);
        TreeObj tree = activeTrees[_ev.index];

        if (tree.gameObject.activeSelf != shouldBeActive)
        {
            tree.gameObject.SetActive(shouldBeActive);
        }

        if (shouldBeActive)
        {
            // 최적화: HashSet을 사용하여 O(1) 검색
            if (activeTreesForUpdateSet.Add(tree))
            {
                activeTreesForUpdate.Add(tree);
            }
        }
        else
        {
            if (activeTreesForUpdateSet.Remove(tree))
            {
                activeTreesForUpdate.Remove(tree);
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
        if (portal == null) return;
        portal.PortalActivated -= OnPortalActivated;
        portal.PortalActivated += OnPortalActivated;
    }

    private void OnPortalActivated()
    {
        PortalActivatedEvent?.Invoke();
    }

    private void OnTreeDead(TreeObj _treeObj)
    {
        environmentProvider.tilemapDataProvider.ClearTreeCollisionTile(_treeObj.transform.position);
        environmentProvider.densityProvider.UpdateTreeCnt(false);
        itemManager.SpawnLogItem(_treeObj);

        // 죽은 위치 재사용 준비
        Vector3 deadPos = _treeObj.transform.position;
        availablePositions.Add(deadPos);

        if (availablePositions.Count > 1)
        {
            int lastIdx = availablePositions.Count - 1;
            int swapIdx = UnityEngine.Random.Range(0, lastIdx);
            Vector3 temp = availablePositions[lastIdx];
            availablePositions[lastIdx] = availablePositions[swapIdx];
            availablePositions[swapIdx] = temp;
        }

        // activeTrees 최적화 (Swap-with-last)
        int index = activeTrees.IndexOf(_treeObj);
        if (index >= 0)
        {
            int lastIdx = activeTrees.Count - 1;
            if (index != lastIdx)
            {
                activeTrees[index] = activeTrees[lastIdx];
            }
            activeTrees.RemoveAt(lastIdx);
        }

        if (activeTreesForUpdateSet.Remove(_treeObj))
        {
            activeTreesForUpdate.Remove(_treeObj);
        }

        isCullingDirty = true;

        treePool.Release(_treeObj);
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
        if (dungeonData == null)
        {
            return new TreeData(TreeType.None, TreeGrade.None, TreeState.Idle);
        }

        TreeType type = TreeType.None;
        if (dungeonData.treeTypes != null && dungeonData.treeTypes.Count > 0)
        {
            type = dungeonData.treeTypes[UnityEngine.Random.Range(0, dungeonData.treeTypes.Count)];
        }

        TreeGrade grade = TreeGrade.Normal;
        if (dungeonData.treeGradeProbs != null && dungeonData.treeGradeProbs.Count > 0)
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

        return new TreeData(type, grade, TreeState.Idle);
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
        _tree.ResetTree();
        _tree.TreeDeadEvent -= OnTreeDead;
        _tree.TreeGetHitEvent -= OnTreeHit;
        _tree.gameObject.SetActive(false);
    }

    private void OnDestroyTree(TreeObj _tree)
    {
        if (_tree != null) Destroy(_tree.gameObject);
    }

    // // 유니티 이벤트 함수

    private void Update()
    {
        for (int i = 0; i < activeTreesForUpdate.Count; i++)
        {
            activeTreesForUpdate[i].ManualUpdate();
        }

        if (isCullingDirty)
        {
            RefreshCullingGroup();
            isCullingDirty = false;
        }
    }

    private void OnDestroy()
    {
        StopGrowth();
        ClearTrees();

        if (portal != null)
        {
            portal.PortalActivated -= OnPortalActivated;
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
        itemManager.SpawnCarrotItem(_animal.transform.position);
    }

    private void CarrotItemAcquired(CarrotItem _item)
    {
        CarrotItemAcquiredEvent?.Invoke(_item);
    }
}
