using UnityEngine;
using UnityEngine.Pool;

public class LogItemPoolingManager : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private LogItem logItemPrefab;
    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;

    // 내부 의존성
    private IObjectPool<LogItem> logPool;

    private bool bDisableCustomSortable = false;

    public void Initialize(bool _bDisableCustomSortable)
    {
        bDisableCustomSortable = _bDisableCustomSortable;

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
        return GetLogItem(_data.treeType, _data.logState);
    }

    // TreeType/LogState는 값 타입이라 호출 시점의 값을 그대로 복사해 사용한다.
    // DropAllItem처럼 원본 LogItemData가 풀로 반환되어 리셋될 수 있는(예약된) 상황에서
    // 나중에(지연 스폰 등) 안전하게 꺼내 쓰기 위해, 참조가 아닌 값으로 받는 경로가 필요할 때 사용한다.
    public LogItem GetLogItem(TreeType _treeType, LogState _logState)
    {
        LogItem item = logPool.Get();

        LogItemTypeData typeData = logItemTypeDataBase.Get(_treeType);

        if (typeData != null)
        {
            item.Initialize(typeData, typeData.color, _logState, null, bDisableCustomSortable);
        }
        else
        {
            Debug.LogError($"[LogItemPoolingManager] No LogItemTypeData found for TreeType: {_treeType}");
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
        _item.ResetItem();
        _item.gameObject.SetActive(true);
    }

    private void OnReleaseLogItem(LogItem _item)
    {
        _item.gameObject.transform.SetParent(transform);
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
