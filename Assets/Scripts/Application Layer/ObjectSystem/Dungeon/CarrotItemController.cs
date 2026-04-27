using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class CarrotItemController : MonoBehaviour, ICarrotItemCH
{
    public event Action<CarrotItem> CarrotItemAcquiredEvent;

    // 외부 의존성
    [SerializeField] private CarrotItem carrotItemPrefab;
    [SerializeField] private int minAmountPerBundle = 3;
    [SerializeField] private int maxAmountPerBundle = 5;
    [SerializeField] private int minSpawnBundle = 2;
    [SerializeField] private int maxSpawnBundle = 3;

    // 내부 의존성
    private IObjectPool<CarrotItem> carrotPool;
    // 최적화: 검색 및 삭제 속도 향상을 위해 HashSet 사용 (O(1))
    private HashSet<CarrotItem> activeItems = new HashSet<CarrotItem>(128);
    private List<CarrotItem> activeItemsList = new List<CarrotItem>(128); // 최적화: 순회용 캐싱 리스트
    private List<CarrotItem> cleanupList = new List<CarrotItem>(128); // ClearAll용 재사용 리스트
    private float dropMultiplier = 1.0f;

    public void Initialize()
    {
        carrotPool = new ObjectPool<CarrotItem>(
            createFunc: CreateCarrotItem,
            actionOnGet: OnGetCarrotItem,
            actionOnRelease: OnReleaseCarrotItem,
            actionOnDestroy: OnDestroyCarrotItem,
            collectionCheck: true,
            defaultCapacity: 50,
            maxSize: 500
        );
    }

    private void Update()
    {
        if (activeItems.Count == 0) return;

        // HashSet을 직접 순회하면 GC 할당이 발생할 수 있고 변조에 취약하므로 리스트로 복사 후 순회
        activeItemsList.Clear();
        foreach (var item in activeItems)
        {
            activeItemsList.Add(item);
        }

        float deltaTime = Time.deltaTime;
        for (int i = 0; i < activeItemsList.Count; i++)
        {
            activeItemsList[i].ManualUpdate(deltaTime);
        }
    }

    private void CarrotItemAcquired(CarrotItem _item)
    {
        CarrotItemAcquiredEvent?.Invoke(_item);
        carrotPool.Release(_item);
    }

    private CarrotItem CreateCarrotItem()
    {
        CarrotItem newItem = Instantiate(carrotItemPrefab, transform);
        newItem.CarrotItemAcquired -= CarrotItemAcquired;
        newItem.CarrotItemAcquired += CarrotItemAcquired;
        return newItem;
    }

    private void OnGetCarrotItem(CarrotItem _item)
    {
        _item.gameObject.SetActive(true);
        _item.Initialize(); 
        activeItems.Add(_item);
    }

    private void OnReleaseCarrotItem(CarrotItem _item)
    {
        _item.gameObject.SetActive(false);
        activeItems.Remove(_item);
    }

    private void OnDestroyCarrotItem(CarrotItem _item)
    {
        _item.CarrotItemAcquired -= CarrotItemAcquired;
        activeItems.Remove(_item);
        Destroy(_item.gameObject);
    }

    public void ClearAll()
    {
        if (activeItems.Count == 0) return;

        // HashSet을 직접 순회하며 Release하면 컬렉션 변조 에러가 발생하므로 임시 리스트 사용
        cleanupList.Clear();
        foreach (var item in activeItems)
        {
            cleanupList.Add(item);
        }

        for (int i = 0; i < cleanupList.Count; i++)
        {
            carrotPool.Release(cleanupList[i]);
        }
        
        activeItems.Clear();
        cleanupList.Clear();
    }

    public void SpawnCarrotItem(Vector3 _position)
    {
        int bundlesToSpawn = UnityEngine.Random.Range(minSpawnBundle, maxSpawnBundle + 1);

        for (int i = 0; i < bundlesToSpawn; i++)
        {
            CarrotItem carrotItem = carrotPool.Get();

            carrotItem.transform.position = _position;

            int randomAmount = UnityEngine.Random.Range(minAmountPerBundle, maxAmountPerBundle + 1);
            float finalAmount = randomAmount * dropMultiplier;
            carrotItem.SetAmount(finalAmount);

            // 포물선 운동 설정
            Vector3 startPos = _position;
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float randomDist = UnityEngine.Random.Range(0.25f, 0.75f);
            Vector3 endPos = startPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0) * randomDist;

            float height = UnityEngine.Random.Range(0.5f, 1.0f);
            float duration = UnityEngine.Random.Range(0.25f, 0.5f);

            carrotItem.Launch(startPos, endPos, height, duration);
        }
    }

    public void ReturnToPool(CarrotItem _item)
    {
        carrotPool.Release(_item);
    }

    public void IncreaseCarrotDrop(float _amount)
    {
        dropMultiplier += (_amount / 100.0f);
        Debug.Log($"[CarrotItemController] Carrot Bundle Capacity Increased: {dropMultiplier * 100}%");
    }

    public CarrotSaveData GetSaveData()
    {
        return new CarrotSaveData { dropMultiplier = dropMultiplier };
    }

    public void LoadSaveData(CarrotSaveData _data)
    {
        dropMultiplier = _data.dropMultiplier;
        Debug.Log("[CarrotItemController] Carrot Save Data Loaded.");
    }
}
