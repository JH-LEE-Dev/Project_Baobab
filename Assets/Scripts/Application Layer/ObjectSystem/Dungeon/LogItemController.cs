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
            defaultCapacity: 10,
            maxSize: 100
        );
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
    }

    private void OnReleaseLogItem(LogItem _item)
    {
        _item.gameObject.SetActive(false);
    }

    private void OnDestroyLogItem(LogItem _item)
    {
        _item.LogItemAcquired -= LogItemAcquired;
        Destroy(_item.gameObject);
    }

    public void SpawnLogItem(TreeObj _treeObj)
    {
        TreeData treeData = _treeObj.treeData;
        LogDropData dropData = GetDropData(treeData.grade);

        if (dropData.probDatas == null || dropData.probDatas.Count == 0) return;

        LogStateProbData stateProbData = GetStateProbData(dropData, treeData.treeState);
        if (stateProbData.probDatas == null || stateProbData.probDatas.Count == 0) return;

        int spawnCount = UnityEngine.Random.Range(2, 4); // 2~3개

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
}
