using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class LogItemController : MonoBehaviour
{
    public event Action<Item> LogItemAcquiredEvent;

    // 외부 의존성
    [SerializeField] private List<LogDropData> logProbDatas;
    [SerializeField] private LogItem logItemPrefab;
    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;

    // 내부 의존성
    private IObjectPool<LogItem> logPool;
    // 최적화: 검색 및 삭제 속도 향상을 위해 HashSet 사용 (O(1))
    private HashSet<LogItem> activeItems = new HashSet<LogItem>(256);
    private List<LogItem> activeItemsList = new List<LogItem>(256); // 최적화: 순회 및 컬링용 리스트
    private List<LogItem> cleanupList = new List<LogItem>(256); // ClearAll용 재사용 리스트

    [Header("Optimization")]
    [SerializeField] private float cullingUpdateInterval = 0.05f;
    private float cullingUpdateTimer = 0f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private bool isCullingDirty = false;

    private IInventoryChecker inventoryChecker;

    public void Initialize(IInventoryChecker _inventoryChecker)
    {
        inventoryChecker = _inventoryChecker;

        logPool = new ObjectPool<LogItem>(
            createFunc: CreateLogItem,
            actionOnGet: OnGetLogItem,
            actionOnRelease: OnReleaseLogItem,
            actionOnDestroy: OnDestroyLogItem,
            collectionCheck: true,
            defaultCapacity: 100,
            maxSize: 1000 // 최적화: 나무가 많은 게임 특성상 풀 크기를 넉넉하게 설정
        );
    }

    public void SetupCullingGroup()
    {
        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.onStateChanged = OnCullingStateChanged;
        }

        cullingGroup.targetCamera = Camera.main;
        spheres = new BoundingSphere[1000];
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (_ev.index >= activeItemsList.Count) return;

        bool isVisible = _ev.isVisible;
        activeItemsList[_ev.index].gameObject.SetActive(isVisible);
    }

    private void Update()
    {
        if (activeItemsList.Count == 0) return;

        float deltaTime = Time.deltaTime;
        for (int i = 0; i < activeItemsList.Count; i++)
        {
            activeItemsList[i].ManualUpdate(deltaTime);
        }

        // 컬링 구체 위치 업데이트 (스로틀링)
        if (cullingGroup != null)
        {
            cullingUpdateTimer += deltaTime;
            if (cullingUpdateTimer >= cullingUpdateInterval)
            {
                UpdateCullingSpheres();
                cullingUpdateTimer = 0f;
            }
        }

        if (isCullingDirty)
        {
            RefreshCullingGroup();
            isCullingDirty = false;
        }
    }

    private void UpdateCullingSpheres()
    {
        int count = activeItemsList.Count;
        for (int i = 0; i < count; i++)
        {
            spheres[i].position = activeItemsList[i].transform.position;
            spheres[i].radius = 1f;
        }
    }

    private void RefreshCullingGroup()
    {
        int count = activeItemsList.Count;
        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(count);

        for (int i = 0; i < count; i++)
        {
            activeItemsList[i].gameObject.SetActive(cullingGroup.IsVisible(i));
        }
    }

    private void LogItemAcquired(LogItem _item)
    {
        LogItemAcquiredEvent?.Invoke(_item);
        logPool.Release(_item);
    }

    private LogItem CreateLogItem()
    {
        LogItem newItem = Instantiate(logItemPrefab, transform);
        newItem.LogItemAcquired -= LogItemAcquired;
        newItem.LogItemAcquired += LogItemAcquired;
        return newItem;
    }

    private void OnGetLogItem(LogItem _item)
    {
        _item.gameObject.SetActive(true);
        _item.ResetItem();
        activeItems.Add(_item);
        activeItemsList.Add(_item);
        isCullingDirty = true;
    }

    private void OnReleaseLogItem(LogItem _item)
    {
        _item.gameObject.SetActive(false);
        activeItems.Remove(_item);
        activeItemsList.Remove(_item);
        isCullingDirty = true;
    }

    private void OnDestroyLogItem(LogItem _item)
    {
        _item.LogItemAcquired -= LogItemAcquired;
        activeItems.Remove(_item);
        activeItemsList.Remove(_item);
        Destroy(_item.gameObject);
        isCullingDirty = true;
    }

    public void ClearAll()
    {
        if (activeItemsList.Count == 0) return;

        cleanupList.Clear();
        cleanupList.AddRange(activeItemsList);

        for (int i = 0; i < cleanupList.Count; i++)
        {
            logPool.Release(cleanupList[i]);
        }
        
        activeItems.Clear();
        activeItemsList.Clear();
        cleanupList.Clear();
        isCullingDirty = true;
    }

    public void SpawnLogItem(TreeObj _treeObj)
    {
        TreeData treeData = _treeObj.treeData;
        LogDropData dropData = GetDropData(treeData.grade);

        if (dropData.probDatas == null || dropData.probDatas.Count == 0) return;

        LogStateProbData stateProbData = GetStateProbData(dropData, treeData.treeState);
        if (stateProbData.probDatas == null || stateProbData.probDatas.Count == 0) return;

        int spawnCount = UnityEngine.Random.Range(2, 4); 

        for (int i = 0; i < spawnCount; i++)
        {
            LogState logType = GetRandomLogState(stateProbData);
            LogItem logItem = logPool.Get();

            logItem.transform.position = _treeObj.transform.position;
            logItem.Initialize(logItemTypeDataBase.Get(treeData.type), logType, _treeObj.GetColor());
            logItem.SetInventoryChecker(inventoryChecker);

            // 포물선 운동 설정
            Vector3 startPos = _treeObj.transform.position;
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float randomDist = UnityEngine.Random.Range(0.25f, 0.75f);
            Vector3 endPos = startPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0) * randomDist;

            float height = UnityEngine.Random.Range(0.5f, 1.0f);
            float duration = UnityEngine.Random.Range(0.25f, 0.5f);

            logItem.Launch(startPos, endPos, height, duration);
        }
    }

    private LogDropData GetDropData(TreeGrade _grade)
    {
        for (int i = 0; i < logProbDatas.Count; i++)
        {
            if (logProbDatas[i].treeGrade == _grade)
            {
                return logProbDatas[i];
            }
        }
        return default;
    }

    private LogStateProbData GetStateProbData(LogDropData _dropData, TreeState _state)
    {
        for (int i = 0; i < _dropData.probDatas.Count; i++)
        {
            if (_dropData.probDatas[i].treeState == _state)
            {
                return _dropData.probDatas[i];
            }
        }
        return default;
    }

    private LogState GetRandomLogState(LogStateProbData _data)
    {
        float totalProb = 0;
        for (int i = 0; i < _data.probDatas.Count; i++)
        {
            totalProb += _data.probDatas[i].probability;
        }

        float randomVal = UnityEngine.Random.Range(0f, totalProb);
        float currentProb = 0;

        for (int i = 0; i < _data.probDatas.Count; i++)
        {
            currentProb += _data.probDatas[i].probability;
            if (randomVal <= currentProb)
            {
                return _data.probDatas[i].type;
            }
        }

        return _data.probDatas[0].type;
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
}
