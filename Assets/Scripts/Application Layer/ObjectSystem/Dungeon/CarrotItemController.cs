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
    private List<CarrotItem> activeItems = new List<CarrotItem>();
    private float dropMultiplier = 1.0f;

    public void Initialize()
    {
        carrotPool = new ObjectPool<CarrotItem>(
            createFunc: CreateCarrotItem,
            actionOnGet: OnGetCarrotItem,
            actionOnRelease: OnReleaseCarrotItem,
            actionOnDestroy: OnDestroyCarrotItem,
            collectionCheck: true,
            defaultCapacity: 10,
            maxSize: 100
        );
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
        _item.Initialize(); // CarrotItem.Initialize()는 매개변수가 없음
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
        for (int i = activeItems.Count - 1; i >= 0; i--)
        {
            carrotPool.Release(activeItems[i]);
        }
        activeItems.Clear();
    }

    public void SpawnCarrotItem(Vector3 _position)
    {
        int bundlesToSpawn = UnityEngine.Random.Range(minSpawnBundle, maxSpawnBundle + 1);

        for (int i = 0; i < bundlesToSpawn; i++)
        {
            CarrotItem carrotItem = carrotPool.Get();

            carrotItem.transform.position = _position;

            // 당근 묶음 개수 설정 (기본 랜덤값 * 드랍 배율)
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
        // _amount는 0보다 큰 퍼센트 (예: 100.0f는 100% 증가 즉 2배 용량)
        dropMultiplier += (_amount / 100.0f);

        Debug.Log($"[CarrotItemController] Carrot Bundle Capacity Increased: {dropMultiplier * 100}%");
    }
}
