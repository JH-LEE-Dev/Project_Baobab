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
    private List<CarrotItem> activeItemsList = new List<CarrotItem>(128); // 최적화: 순회 및 컬링용 리스트
    private List<CarrotItem> cleanupList = new List<CarrotItem>(128); // ClearAll용 재사용 리스트
    private float dropMultiplier = 1.0f;

    [Header("Optimization")]
    [SerializeField] private float cullingUpdateInterval = 0.05f;
    private float cullingUpdateTimer = 0f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private bool isCullingDirty = false;

    [SerializeField] private List<CarrotSpawnData> carrotSpawnData;

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

    public void SetupCullingGroup()
    {
        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.onStateChanged = OnCullingStateChanged;
        }

        cullingGroup.targetCamera = Camera.main;
        spheres = new BoundingSphere[500];
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

        // 아이템 로직 업데이트 (HashSet 복사 없이 직접 순회하도록 로직 최적화 가능)
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
            spheres[i].radius = 1f; // 아이템 감지 반경
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
        newItem.Initialize();

        return newItem;
    }

    private void OnGetCarrotItem(CarrotItem _item)
    {
        _item.gameObject.SetActive(true);
        _item.ResetItem();
        activeItems.Add(_item);
        activeItemsList.Add(_item);
        isCullingDirty = true;
    }

    private void OnReleaseCarrotItem(CarrotItem _item)
    {
        _item.gameObject.SetActive(false);
        activeItems.Remove(_item);
        activeItemsList.Remove(_item);
        isCullingDirty = true;
    }

    private void OnDestroyCarrotItem(CarrotItem _item)
    {
        _item.CarrotItemAcquired -= CarrotItemAcquired;
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
            carrotPool.Release(cleanupList[i]);
        }

        activeItems.Clear();
        activeItemsList.Clear();
        cleanupList.Clear();
        isCullingDirty = true;
    }

    public void SpawnCarrotItem(Vector3 _position, AnimalType _animalType)
    {
        int minSB = minSpawnBundle;
        int maxSB = maxSpawnBundle;
        int minAPB = minAmountPerBundle;
        int maxAPB = maxAmountPerBundle;

        // carrotSpawnData에서 해당하는 AnimalType의 데이터를 찾아 적용
        if (carrotSpawnData != null)
        {
            for (int i = 0; i < carrotSpawnData.Count; i++)
            {
                if (carrotSpawnData[i].animalType == _animalType)
                {
                    minSB = carrotSpawnData[i].minSpawnBundle;
                    maxSB = carrotSpawnData[i].maxSpawnBundle;
                    minAPB = carrotSpawnData[i].minAmountPerBundle;
                    maxAPB = carrotSpawnData[i].maxAmountPerBundle;
                    break;
                }
            }
        }

        int bundlesToSpawn = UnityEngine.Random.Range(minSB, maxSB + 1);

        for (int i = 0; i < bundlesToSpawn; i++)
        {
            CarrotItem carrotItem = carrotPool.Get();

            carrotItem.transform.position = _position;

            int randomAmount = UnityEngine.Random.Range(minAPB, maxAPB + 1);
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
