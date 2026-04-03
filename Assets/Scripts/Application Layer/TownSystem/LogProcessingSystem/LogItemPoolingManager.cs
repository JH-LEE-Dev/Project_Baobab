using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class LogItemPoolingManager : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private LogItem logItemPrefab;
    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;

    // 내부 의존성
    private IObjectPool<LogItem> logPool;

    public void Initialize()
    {
        logPool = new ObjectPool<LogItem>(
            createFunc: CreateLogItem,
            actionOnGet: OnGetLogItem,
            actionOnRelease: OnReleaseLogItem,
            actionOnDestroy: OnDestroyLogItem,
            collectionCheck: true,
            defaultCapacity: 20,
            maxSize: 100
        );
    }

    public void Release()
    {
        // 풀 관련 자원 해제 로직 필요 시 구현
    }

    // 퍼블릭 초기화 및 제어 메서드

    public LogItem GetLogItem(LogItemData _data)
    {
        LogItem item = logPool.Get();

        LogItemTypeData typeData = logItemTypeDataBase.Get(_data.treeType);

        if (typeData != null)
        {
            item.Initialize(typeData, _data.logState, _data.color);
        }
        else
        {
            Debug.LogError($"[LogItemPoolingManager] No LogItemTypeData found for TreeType: {_data.treeType}");
        }

        return item;
    }

    public void ReturnLogItem(LogItem _item)
    {
        logPool.Release(_item);
    }

    // 내부 풀 관리 메서드

    private LogItem CreateLogItem()
    {
        LogItem newItem = Instantiate(logItemPrefab, transform);
        newItem.IsDropItem(false);

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
        if (_item != null)
        {
            Destroy(_item.gameObject);
        }
    }
}
