using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class LootManager : MonoBehaviour
{
    // 외부 의존성
    public event Action<LootItem> LootItemAcquiredEvent;

    [SerializeField] private List<LootDropData> lootProbDatas;
    [SerializeField] private LootItem lootItemPrefab;
    [SerializeField] private LootItemTypeDataBase lootItemTypeDataBase;

    // 내부 의존성
    private IObjectPool<LootItem> lootPool;
    // 최적화: 인덱스 기반 관리로 HashSet 제거
    private List<LootItem> activeItemsList = new List<LootItem>(128); // 최적화: 순회용 리스트
    private List<LootItem> cleanupList = new List<LootItem>(128); // ClearAll용 재사용 리스트

    /// <summary>
    /// 매니저 초기화 및 오브젝트 풀 설정
    /// </summary>
    public void Initialize()
    {
        lootPool = new ObjectPool<LootItem>(
            createFunc: CreateLootItem,
            actionOnGet: OnGetLootItem,
            actionOnRelease: OnReleaseLootItem,
            actionOnDestroy: OnDestroyLootItem,
            collectionCheck: true,
            defaultCapacity: 50,
            maxSize: 300 // 최적화: 전리품은 로그보다는 적으므로 300 정도로 설정
        );

        BindEvents();
    }

    private void Update()
    {
        if (activeItemsList.Count == 0) return;

        float deltaTime = Time.deltaTime;
        // 최적화 및 버그 수정: ManualUpdate 중 아이템이 해제(Release)되어 리스트가 변형될 수 있으므로 역순 순회
        for (int i = activeItemsList.Count - 1; i >= 0; i--)
        {
            activeItemsList[i].ManualUpdate(deltaTime);
        }
    }

    /// <summary>
    /// 매니저 해제
    /// </summary>
    public void Release()
    {
        ReleaseEvents();
    }

    private void BindEvents()
    {
    }

    private void ReleaseEvents()
    {
    }

    /// <summary>
    /// 아이템 획득 시 호출되는 콜백
    /// </summary>
    private void OnLootItemAcquired(LootItem _item)
    {
        LootItemAcquiredEvent?.Invoke(_item);
        lootPool.Release(_item);
    }

    private LootItem CreateLootItem()
    {
        if (lootItemPrefab == null)
            return null;

        LootItem newItem = Instantiate(lootItemPrefab, transform);

        // 이벤트 바인딩 (생성 시 한 번만)
        newItem.lootItemAcquiredEvent -= OnLootItemAcquired;
        newItem.lootItemAcquiredEvent += OnLootItemAcquired;

        return newItem;
    }

    private void OnGetLootItem(LootItem _item)
    {
        _item.UpdateIndex = activeItemsList.Count;
        activeItemsList.Add(_item);

        _item.gameObject.SetActive(true);
        _item.ResetItem();
    }

    private void OnReleaseLootItem(LootItem _item)
    {
        // 최적화: Swap-with-last 방식을 이용한 리스트 삭제 (O(1))
        int idx = _item.UpdateIndex;
        if (idx != -1 && idx < activeItemsList.Count)
        {
            int lastIdx = activeItemsList.Count - 1;
            if (idx != lastIdx)
            {
                LootItem lastItem = activeItemsList[lastIdx];
                activeItemsList[idx] = lastItem;
                lastItem.UpdateIndex = idx;
            }
            activeItemsList.RemoveAt(lastIdx);
            _item.UpdateIndex = -1;
        }

        _item.gameObject.SetActive(false);
    }

    private void OnDestroyLootItem(LootItem _item)
    {
        if (_item != null)
        {
            _item.lootItemAcquiredEvent -= OnLootItemAcquired;
            OnReleaseLootItem(_item);
            Destroy(_item.gameObject);
        }
    }

    public void ClearAll()
    {
        int count = activeItemsList.Count;
        if (count == 0) return;

        cleanupList.Clear();
        cleanupList.AddRange(activeItemsList);

        for (int i = 0; i < cleanupList.Count; i++)
        {
            lootPool.Release(cleanupList[i]);
        }
        
        activeItemsList.Clear();
        cleanupList.Clear();
    }

    /// <summary>
    /// 전리품 아이템 스폰 및 연출
    /// </summary>
    /// <param name="_spawnPos">스폰 시작 위치</param>
    /// <param name="_targetLootType">스폰할 전리품 종류 (기본값 None이면 랜덤)</param>
    public void SpawnLootItem(Vector3 _spawnPos, LootType _targetLootType = LootType.None)
    {
        // 1~3개 랜덤 스폰
        int spawnCount = UnityEngine.Random.Range(1, 4);

        for (int i = 0; i < spawnCount; i++)
        {
            LootType selectedType = (_targetLootType == LootType.None) ? GetRandomLootType() : _targetLootType;
            if (selectedType == LootType.None) continue;

            LootItemTypeData typeData = lootItemTypeDataBase.Get(selectedType);
            if (typeData == null) continue;

            LootItem lootItem = lootPool.Get();
            lootItem.transform.position = _spawnPos;
            lootItem.Initialize(typeData);

            // 포물선 운동 설정
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float randomDist = UnityEngine.Random.Range(0.4f, 1.2f); 
            Vector3 endPos = _spawnPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0) * randomDist;

            float height = UnityEngine.Random.Range(0.6f, 1.2f);
            float duration = UnityEngine.Random.Range(0.3f, 0.6f);

            lootItem.Launch(_spawnPos, endPos, height, duration);
        }
    }

    /// <summary>
    /// 가중치 기반 랜덤 전리품 타입 결정
    /// </summary>
    private LootType GetRandomLootType()
    {
        if (lootProbDatas == null || lootProbDatas.Count == 0) return LootType.None;

        float totalProb = 0f;
        for (int i = 0; i < lootProbDatas.Count; i++)
        {
            totalProb += lootProbDatas[i].probability;
        }

        if (totalProb <= 0) return LootType.None;

        float randomVal = UnityEngine.Random.Range(0f, totalProb);
        float currentProb = 0f;

        for (int i = 0; i < lootProbDatas.Count; i++)
        {
            currentProb += lootProbDatas[i].probability;
            if (randomVal <= currentProb)
            {
                return lootProbDatas[i].lootType;
            }
        }

        return lootProbDatas[0].lootType;
    }

    /// <summary>
    /// 특정 타입의 전리품을 즉시 획득 처리
    /// </summary>
    /// <param name="_type">획득할 전리품 타입</param>
    public void AcquireLootItem(LootType _type)
    {
        if (_type == LootType.None) return;

        LootItemTypeData typeData = lootItemTypeDataBase.Get(_type);
        if (typeData == null) return;

        LootItem lootItem = lootPool.Get();
        lootItem.Initialize(typeData);

        // 즉시 획득 처리 (이벤트 발생 및 풀 반환)
        OnLootItemAcquired(lootItem);
    }

    /// <summary>
    /// 외부에서 아이템을 풀로 반환할 때 사용
    /// </summary>
    public void ReturnToPool(LootItem _item)
    {
        if (_item != null)
        {
            lootPool.Release(_item);
        }
    }
}
