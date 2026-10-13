using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class LootManager : MonoBehaviour
{
    // 외부 의존성
    // 획득 데이터 확정(영구 플래그·결과창 집계 등). 전리품 하나당 정확히 한 번 발행된다.
    // 런 진행 중에는 도착과 같은 프레임이고, 런 종료 시점에 공중에 있던 것은 도착보다 먼저 발행된다.
    public event Action<LootType> LootDataCommittedEvent;
    // 전리품이 실제로 캐릭터에게 도착했을 때의 획득 피드백(사운드·뽀잉 연출)용.
    // 데이터는 담지 않으므로, 도착하지 못한 채 정리되는 전리품(CommitAndReleaseAllActive)은 발행하지 않는다.
    public event Action<LootType> LootArrivedEvent;

    [SerializeField] private List<LootDropData> lootProbDatas;
    [SerializeField] private LootItem lootItemPrefab;
    [SerializeField] private LootItemTypeDataBase lootItemTypeDataBase;

    // 내부 의존성
    private IObjectPool<LootItem> lootPool;
    // 최적화: 인덱스 기반 관리로 HashSet 제거
    private List<LootItem> activeItemsList = new List<LootItem>(128); // 최적화: 순회용 리스트
    private List<LootItem> cleanupList = new List<LootItem>(128); // ClearAll용 재사용 리스트

    // 런이 끝난 뒤(CommitRunEnd ~ ResetRunEndCommit)에 새로 떨어지는 전리품은 스폰 즉시 데이터를 확정한다.
    // 사망 후 남은 공격 효과 등으로 나무가 쓰러져도 결과창보다 먼저 데이터에 들어가게 하기 위함이다.
    private bool bCommitOnSpawn;

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
            collectionCheck: PoolSettings.CollectionCheck,
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
    /// 흡입이 끝나 캐릭터에게 도착했을 때 호출되는 콜백. 아직 확정되지 않았으면 데이터를 확정하고,
    /// 확정 여부와 무관하게 획득 피드백을 낸 뒤 풀로 돌려보낸다.
    /// </summary>
    private void OnLootItemArrived(LootItem _item)
    {
        CommitData(_item);
        LootArrivedEvent?.Invoke(_item.LootType);
        TryReleaseLootItem(_item);
    }

    private void CommitData(LootItem _item)
    {
        if (_item.IsDataCommitted) return;

        _item.IsDataCommitted = true;
        LootDataCommittedEvent?.Invoke(_item.LootType);
    }

    /// <summary>
    /// 런이 끝나는 순간(사망 판정 / 귀환 확정) 호출한다. 공중에 있는 전리품의 획득 데이터를 지금 확정하고,
    /// 이후 새로 떨어지는 전리품도 스폰 즉시 확정한다. 낙하/흡입 연출은 건드리지 않으므로 화면에서는
    /// 평소처럼 빨려 들어와 도착하고, 그때 피드백만 난다. 결과창과 사망 시 구제 판정이 이 데이터를 읽는다.
    /// </summary>
    public void CommitRunEnd()
    {
        bCommitOnSpawn = true;

        if (activeItemsList.Count == 0) return;

        // 확정 이벤트 처리 중 목록이 바뀌어도 안전하도록 사본을 순회한다.
        cleanupList.Clear();
        cleanupList.AddRange(activeItemsList);

        for (int i = 0; i < cleanupList.Count; i++)
        {
            CommitData(cleanupList[i]);
        }

        cleanupList.Clear();
    }

    /// <summary>새 원정이 시작될 때 CommitRunEnd()의 "스폰 즉시 확정" 상태를 푼다.</summary>
    public void ResetRunEndCommit()
    {
        bCommitOnSpawn = false;
    }

    private LootItem CreateLootItem()
    {
        if (null == lootItemPrefab)
            return null;

        LootItem newItem = Instantiate(lootItemPrefab, transform);

        // 이벤트 바인딩 (생성 시 한 번만)
        newItem.lootItemAcquiredEvent -= OnLootItemArrived;
        newItem.lootItemAcquiredEvent += OnLootItemArrived;

        // 첫 스폰 시 발생하는 셰이더 컴파일 지연(푸른색 박스 잔상)을 원천 방지하기 위한 사전 웜업
        newItem.Prewarm();

        return newItem;
    }

    /// <summary>
    /// 이미 풀에 들어가 있는 항목을 다시 반환하지 않도록 막고 반환한다. 반환이 실제로
    /// 일어났으면 true. IsPooled는 풀의 actionOnGet/actionOnRelease에서만 갱신된다.
    /// </summary>
    private bool TryReleaseLootItem(LootItem _item)
    {
        if (_item == null || _item.IsPooled) return false;

        lootPool.Release(_item);
        return true;
    }

    private void OnGetLootItem(LootItem _item)
    {
        _item.IsPooled = false;
        _item.UpdateIndex = activeItemsList.Count;
        activeItemsList.Add(_item);

        _item.gameObject.SetActive(true);
        _item.ResetItem();
    }

    private void OnReleaseLootItem(LootItem _item)
    {
        _item.IsPooled = true;
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
            _item.lootItemAcquiredEvent -= OnLootItemArrived;
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
            TryReleaseLootItem(cleanupList[i]);
        }

        activeItemsList.Clear();
        cleanupList.Clear();
    }

    /// <summary>
    /// 던전 정리 시점(재도전 카메라 상승 완료 / 마을 도착)에 남아 있는 전리품을 정리한다. 데이터는
    /// 런 종료 시점(CommitRunEnd)에 이미 확정됐으므로 피드백 없이 풀로 돌려보내기만 한다.
    /// 확정되지 않은 것이 남아 있다면 런 종료 훅이 빠진 경로이므로, 유실되지 않게 여기서 확정하고 알린다.
    /// </summary>
    public void CommitAndReleaseAllActive()
    {
        int count = activeItemsList.Count;
        if (count == 0) return;

        cleanupList.Clear();
        cleanupList.AddRange(activeItemsList);

        for (int i = 0; i < cleanupList.Count; i++)
        {
            LootItem item = cleanupList[i];
            if (item.IsDataCommitted == false)
            {
                Debug.LogError($"[LootManager] 런 종료 시점에 확정되지 않은 전리품({item.LootType})이 정리 단계까지 남았습니다. " +
                    "CommitRunEnd() 호출이 빠진 런 종료 경로가 있는지 확인하세요. 획득 데이터는 지금 확정합니다.");
                CommitData(item);
            }

            TryReleaseLootItem(item);
        }

        cleanupList.Clear();
    }

    /// <summary>
    /// 전리품 아이템 스폰 및 연출
    /// </summary>
    /// <param name="_spawnPos">스폰 시작 위치</param>
    /// <param name="_targetLootType">스폰할 전리품 종류 (기본값 None이면 랜덤)</param>
    /// <param name="_autoAcquireTarget">지정 시 범위 판정 없이 착지 즉시 이 타겟으로 흡입되어 습득된다</param>
    public void SpawnLootItem(Vector3 _spawnPos, LootType _targetLootType = LootType.None, Transform _autoAcquireTarget = null)
    {
        int spawnCount = 1;

        for (int i = 0; i < spawnCount; i++)
        {
            LootType selectedType = (_targetLootType == LootType.None) ? GetRandomLootType() : _targetLootType;
            if (selectedType == LootType.None) continue;

            LootItemTypeData typeData = lootItemTypeDataBase.Get(selectedType);
            if (typeData == null) continue;

            LootItem lootItem = lootPool.Get();
            lootItem.transform.position = _spawnPos;
            lootItem.Initialize(typeData);

            // 런이 이미 끝난 뒤의 드랍은 연출을 기다리지 않고 데이터부터 확정한다(CommitRunEnd 참조).
            if (bCommitOnSpawn)
            {
                CommitData(lootItem);
            }

            if (_autoAcquireTarget != null)
            {
                lootItem.SetSuckTarget(_autoAcquireTarget);
            }

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

        // 즉시 획득 처리 (데이터 확정 + 획득 피드백 + 풀 반환)
        OnLootItemArrived(lootItem);
    }

    /// <summary>
    /// 외부에서 아이템을 풀로 반환할 때 사용
    /// </summary>
    public void ReturnToPool(LootItem _item)
    {
        if (_item != null)
        {
            TryReleaseLootItem(_item);
        }
    }
}
