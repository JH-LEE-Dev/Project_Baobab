using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class LogItemController : MonoBehaviour, ILogItemCH
{
    public event Action<Item> LogItemAcquiredEvent;

    // 외부 의존성
    [SerializeField] private List<LogDropProbData> logProbDatas;
    [SerializeField] private LogItem logItemPrefab;
    [SerializeField] private LogItemTypeDataBase logItemTypeDataBase;
    [SerializeField] private List<LogDropCntData> logDropCntDatas;

    // 내부 의존성
    private IObjectPool<LogItem> logPool;
    // 최적화: 인덱스 기반 관리로 HashSet 제거
    private List<LogItem> activeItemsList = new List<LogItem>(256); // 마스터 리스트 (컬링 그룹용)
    private List<LogItem> activeItemsForUpdate = new List<LogItem>(256); // 업데이트 리스트 (가시성 기준)
    private List<LogItem> cleanupList = new List<LogItem>(256); // ClearAll용 재사용 리스트

    [Header("Optimization")]
    [SerializeField] private float cullingUpdateInterval = 0.05f;
    private float cullingUpdateTimer = 0f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;

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
        cullingGroup.SetBoundingSpheres(spheres);
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
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
            if (_item.UpdateIndex == -1)
            {
                _item.UpdateIndex = activeItemsForUpdate.Count;
                activeItemsForUpdate.Add(_item);
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
        if (cullingGroup != null && activeItemsList.Count > 0)
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
            UpdateItemVisibility(activeItemsList[i], cullingGroup.IsVisible(i));
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
        // 최적화: 마스터 리스트 추가 및 인덱스 설정 (O(1))
        _item.PoolIndex = activeItemsList.Count;
        activeItemsList.Add(_item);

        // BoundingSphere 즉시 동기화
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

        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(activeItemsList.Count);
            // 즉시 가시성 체크하여 활성화 및 업데이트 등록 여부 결정
            UpdateItemVisibility(_item, cullingGroup.IsVisible(_item.PoolIndex));
        }
        else
        {
            _item.gameObject.SetActive(true);
            // 컬링 그룹이 없으면 무조건 업데이트 리스트에 추가
            _item.UpdateIndex = activeItemsForUpdate.Count;
            activeItemsForUpdate.Add(_item);
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
                spheres[idx] = spheres[lastIdx];
            }
            activeItemsList.RemoveAt(lastIdx);
            _item.PoolIndex = -1;

            if (cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(activeItemsList.Count);
            }
        }

        _item.gameObject.SetActive(false);
    }

    private void OnDestroyLogItem(LogItem _item)
    {
        _item.LogItemAcquired -= LogItemAcquired;
        OnReleaseLogItem(_item);
        Destroy(_item.gameObject);
    }

    public void ClearAll()
    {
        int count = activeItemsList.Count;
        if (count == 0) return;

        cleanupList.Clear();
        cleanupList.AddRange(activeItemsList);

        for (int i = 0; i < cleanupList.Count; i++)
        {
            logPool.Release(cleanupList[i]);
        }

        activeItemsList.Clear();
        activeItemsForUpdate.Clear();
        cleanupList.Clear();

        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(0);
        }
    }

    public void SpawnLogItem(TreeObj _treeObj, float _multiplier)
    {
        TreeData treeData = _treeObj.treeData;
        LogDropProbData dropProbData = GetDropProbData(treeData.grade);

        if (dropProbData.probDatas == null || dropProbData.probDatas.Count == 0) return;

        LogDropCntData dropCntData = GetDropCntData(treeData.type);
        int spawnCount = Mathf.RoundToInt(UnityEngine.Random.Range(dropCntData.minCnt, dropCntData.maxCnt + 1) * _multiplier);

        for (int i = 0; i < spawnCount; i++)
        {
            LogState logType = GetRandomLogState(dropProbData);
            LogItem logItem = logPool.Get();

            logItem.transform.position = _treeObj.transform.position;
            logItem.Initialize(logItemTypeDataBase.Get(treeData.type), logType, _treeObj.GetColor());
            logItem.SetInventoryChecker(inventoryChecker);

            // 포물선 운동 설정
            Vector3 startPos = _treeObj.transform.position;
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float randomDist = UnityEngine.Random.Range(1.25f, 1.75f);
            Vector3 endPos = startPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0) * randomDist;

            float height = UnityEngine.Random.Range(0.75f, 1f);
            float duration = UnityEngine.Random.Range(0.75f, 0.75f);

            float randomRotation = UnityEngine.Random.Range(1, 3) * 360f * (UnityEngine.Random.value > 0.5f ? 1f : -1f);
            logItem.Launch(startPos, endPos, height, duration, randomRotation);
        }
    }

    private LogDropProbData GetDropProbData(TreeGrade _grade)
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

    private LogState GetRandomLogState(LogDropProbData _data)
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

    public void IncreaseDropProb(LogState _logState, float _amount)
    {
        if (logProbDatas == null) return;

        for (int i = 0; i < logProbDatas.Count; i++)
        {
            List<LogProbData> probList = logProbDatas[i].probDatas;
            if (probList == null) continue;

            int targetIndex = -1;
            float targetProb = 0f;

            // 1. 대상 인덱스와 현재 확률 찾기
            for (int j = 0; j < probList.Count; j++)
            {
                if (probList[j].type == _logState)
                {
                    targetIndex = j;
                    targetProb = probList[j].probability;
                    break;
                }
            }

            if (targetIndex == -1) continue;

            // 2. 더 높은 단계의 logState 중 더 높은 확률이 있는지 체크
            bool skipAdd = false;
            for (int j = 0; j < probList.Count; j++)
            {
                if (probList[j].type > _logState && probList[j].probability > targetProb)
                {
                    skipAdd = true;
                    break;
                }
            }

            // 3. 조건 만족 시 확률 증가
            if (!skipAdd)
            {
                LogProbData probData = probList[targetIndex];
                probData.probability += _amount;
                probList[targetIndex] = probData;
            }
        }
    }

    public LogDropProbSaveData GetSaveData()
    {
        return new LogDropProbSaveData
        {
            logProbDatas = new List<LogDropProbData>(logProbDatas)
        };
    }

    public void LoadSaveData(LogDropProbSaveData _data)
    {
        if (_data.logProbDatas == null) return;
        logProbDatas = new List<LogDropProbData>(_data.logProbDatas);
    }
}