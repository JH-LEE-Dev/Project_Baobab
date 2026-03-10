using UnityEngine;
using UnityEngine.Pool;
using System.Collections.Generic;
using System.Collections;
using System;

public class InDungeonObjectManager : MonoBehaviour
{
    // // 이벤트
    public event Action<PortalType> PortalActivatedEvent;

    // 외부 의존성
    private IEnvironmentProvider environmentProvider;

    // // 내부 의존성
    [Header("Tree Settings")]
    [SerializeField] private TreeObj treePrefab;
    [SerializeField, Range(0f, 1f)] private float startDensity = 0.1f; // 시작 시 나무 밀도
    [SerializeField, Range(0f, 1f)] private float maxDensity = 0.3f;   // 목표 최대 나무 밀도
    [SerializeField] private float spawnInterval = 1.0f;               // 나무가 하나씩 추가 생성되는 간격

    [Header("Portal")]
    [SerializeField] private PortalObj portalPrefab;
    [SerializeField] private Transform portalSpawnPoint;
    private PortalObj portal;
    private List<Vector3> grassTileWorldPositions;
    private List<Vector3> availablePositions = new List<Vector3>();

    // // 내부 의존성 (풀링 및 성장 제어)
    private IObjectPool<TreeObj> treePool;
    private List<TreeObj> activeTrees = new List<TreeObj>(1000);
    private Coroutine growthCoroutine;

    private DungeonData dungeonData;


    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;

        // 오브젝트 풀 초기화 (최대 사이즈 확보)
        treePool = new ObjectPool<TreeObj>(
            createFunc: OnCreateTree,
            actionOnGet: OnGetTree,
            actionOnRelease: OnReleaseTree,
            actionOnDestroy: OnDestroyTree,
            collectionCheck: true,
            defaultCapacity: 200,
            maxSize: 5000
        );
    }

    public void SpawnTree()
    {
        if (grassTileWorldPositions == null || grassTileWorldPositions.Count == 0) return;

        StopGrowth();
        ClearTrees();

        // 1. 모든 위치를 사용 가능 목록으로 초기화 및 셔플
        availablePositions.Clear();
        availablePositions.AddRange(grassTileWorldPositions);
        ShuffleList(availablePositions);

        // 2. 시작 밀도만큼 즉시 스폰
        int totalPossible = grassTileWorldPositions.Count;
        int startCount = Mathf.RoundToInt(totalPossible * startDensity);

        for (int i = 0; i < startCount; i++)
        {
            SpawnOneTreeFromAvailable();
        }

        // 3. 점진적 생성 루틴 시작
        growthCoroutine = StartCoroutine(GrowthRoutine());
    }

    public void ReadyTrees(List<Vector3> _grassTileWorldPositions)
    {
        grassTileWorldPositions = _grassTileWorldPositions;
        SpawnTree();
    }

    public void ReadyPortalAndCharacter()
    {
        if (portal == null)
        {
            portal = Instantiate(portalPrefab, transform);
            portal.Initialize(PortalType.ToTownPortal);
        }

        portal.transform.position = environmentProvider.tilemapDataProvider.GetPortalSpawnPosition();

        BindEvents();
    }

    public Vector3 GetPlayerStartPos()
    {
        return environmentProvider.tilemapDataProvider.GetPlayerSpawnPosition();
    }

    public void SetDungeonData(DungeonData _dungeonData)
    {
        dungeonData = _dungeonData;
    }


    private IEnumerator GrowthRoutine()
    {
        int totalPossible = grassTileWorldPositions.Count;
        int maxCount = Mathf.RoundToInt(totalPossible * maxDensity);

        while (true)
        {
            yield return new WaitForSeconds(spawnInterval);

            // 최대 밀도보다 적고 빈 자리가 있다면 하나 생성
            if (activeTrees.Count < maxCount && availablePositions.Count > 0)
            {
                SpawnOneTreeFromAvailable();
            }
        }
    }

    private void SpawnOneTreeFromAvailable()
    {
        if (availablePositions.Count == 0) return;

        // 리스트 내에서 랜덤하게 하나 선택
        int randomIndex = UnityEngine.Random.Range(0, availablePositions.Count);
        Vector3 spawnPos = availablePositions[randomIndex];

        // 선택된 요소를 마지막 요소와 교체 후 제거 (O(1) 성능 유지하며 랜덤 추출)
        int lastIndex = availablePositions.Count - 1;
        availablePositions[randomIndex] = availablePositions[lastIndex];
        availablePositions.RemoveAt(lastIndex);

        TreeObj tree = treePool.Get();
        tree.transform.position = spawnPos;
        activeTrees.Add(tree);
    }

    private void StopGrowth()
    {
        if (growthCoroutine != null)
        {
            StopCoroutine(growthCoroutine);
            growthCoroutine = null;
        }
    }

    private void ClearTrees()
    {
        int count = activeTrees.Count;
        for (int i = 0; i < count; i++)
        {
            if (activeTrees[i] != null)
            {
                treePool.Release(activeTrees[i]);
            }
        }
        activeTrees.Clear();
    }

    private void ShuffleList<T>(List<T> _list)
    {
        for (int i = 0; i < _list.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(i, _list.Count);
            T temp = _list[i];
            _list[i] = _list[randomIndex];
            _list[randomIndex] = temp;
        }
    }

    private TreeObj OnCreateTree()
    {
        TreeObj tree = Instantiate(treePrefab, transform);
        tree.Initialize(environmentProvider, CalcTreeData());

        return tree;
    }

    private TreeData CalcTreeData()
    {
        if (dungeonData == null)
        {
            return new TreeData(TreeType.None, TreeGrade.None,TreeState.Idle);
        }

        // 1. 나무 종류 선택 (단순 랜덤)
        TreeType selectedType = TreeType.None;
        if (dungeonData.treeTypes != null && dungeonData.treeTypes.Count > 0)
        {
            int typeIdx = UnityEngine.Random.Range(0, dungeonData.treeTypes.Count);
            selectedType = dungeonData.treeTypes[typeIdx];
        }

        // 2. 나무 등급 선택 (가중치 확률 기반)
        TreeGrade selectedGrade = TreeGrade.Normal;
        if (dungeonData.treeGradeProbs != null && dungeonData.treeGradeProbs.Count > 0)
        {
            float randomVal = UnityEngine.Random.Range(0f, 1f);
            float cumulativeProb = 0f;

            for (int i = 0; i < dungeonData.treeGradeProbs.Count; i++)
            {
                cumulativeProb += dungeonData.treeGradeProbs[i].probability;
                if (randomVal <= cumulativeProb)
                {
                    selectedGrade = dungeonData.treeGradeProbs[i].grade;
                    break;
                }
            }
        }

        return new TreeData(selectedType, selectedGrade,TreeState.Idle);
    }

    private void OnGetTree(TreeObj _tree)
    {
        _tree.gameObject.SetActive(true);
        _tree.TreeDeadEvent -= TreeIsDead;
        _tree.TreeDeadEvent += TreeIsDead;
    }

    private void OnReleaseTree(TreeObj _tree)
    {
        _tree.ResetTree();
        _tree.TreeDeadEvent -= TreeIsDead;
        _tree.gameObject.SetActive(false);
    }

    private void OnDestroyTree(TreeObj _tree)
    {
        Destroy(_tree.gameObject);
    }

    private void OnDestroy()
    {
        StopGrowth();
        ClearTrees();
        ReleaseEvents();
    }

    private void BindEvents()
    {
        if (portal == null) return;
        portal.PortalActivated -= PortalActivated;
        portal.PortalActivated += PortalActivated;
    }

    private void ReleaseEvents()
    {
        if (portal != null) portal.PortalActivated -= PortalActivated;
    }

    private void PortalActivated(PortalType _type)
    {
        StopGrowth();
        ClearTrees();
        PortalActivatedEvent?.Invoke(_type);
    }

    private void TreeIsDead(TreeObj _treeObj)
    {
        // 죽은 나무의 위치를 사용 가능 목록에 추가
        availablePositions.Add(_treeObj.transform.position);

        // 방금 추가된 위치가 다음에 바로 나오지 않도록 리스트 내의 임의의 위치와 섞음
        if (availablePositions.Count > 1)
        {
            int lastIdx = availablePositions.Count - 1;
            int swapIdx = UnityEngine.Random.Range(0, lastIdx);
            Vector3 temp = availablePositions[lastIdx];
            availablePositions[lastIdx] = availablePositions[swapIdx];
            availablePositions[swapIdx] = temp;
        }

        treePool.Release(_treeObj);
        activeTrees.Remove(_treeObj);
    }
}
