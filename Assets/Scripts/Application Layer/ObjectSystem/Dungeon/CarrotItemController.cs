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
    // 최적화: 인덱스 기반 관리로 HashSet 제거
    private List<CarrotItem> activeItemsList = new List<CarrotItem>(128); // 마스터 리스트 (컬링 그룹용)
    private List<CarrotItem> activeItemsForUpdate = new List<CarrotItem>(128); // 업데이트 리스트 (가시성 기준)
    private List<CarrotItem> cleanupList = new List<CarrotItem>(128); // ClearAll용 재사용 리스트
    private float dropMultiplier = 1.0f;

    [Header("Optimization")]
    [SerializeField] private float cullingUpdateInterval = 0.05f;
    private float cullingUpdateTimer = 0f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;

    [SerializeField] private List<CarrotSpawnData> carrotSpawnData;

    public void Initialize()
    {
        carrotPool = new ObjectPool<CarrotItem>(
            createFunc: CreateCarrotItem,
            actionOnGet: OnGetCarrotItem,
            actionOnRelease: OnReleaseCarrotItem,
            actionOnDestroy: OnDestroyCarrotItem,
            collectionCheck: PoolSettings.CollectionCheck,
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
        cullingGroup.SetBoundingSpheres(spheres);
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (_ev.index >= activeItemsList.Count) return;

        bool isVisible = _ev.isVisible;
        UpdateItemVisibility(activeItemsList[_ev.index], isVisible);
    }

    private void UpdateItemVisibility(CarrotItem _item, bool _isVisible)
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
                    CarrotItem lastItem = activeItemsForUpdate[lastIdx];
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
            UpdateItemVisibility(activeItemsList[i], cullingGroup.IsVisible(i));
        }
    }

    private void CarrotItemAcquired(CarrotItem _item)
    {
        CarrotItemAcquiredEvent?.Invoke(_item);
        TryReleaseCarrotItem(_item);
    }

    private CarrotItem CreateCarrotItem()
    {
        CarrotItem newItem = Instantiate(carrotItemPrefab, transform);
        newItem.CarrotItemAcquired -= CarrotItemAcquired;
        newItem.CarrotItemAcquired += CarrotItemAcquired;
        newItem.Initialize();

        return newItem;
    }

    /// <summary>
    /// 이미 풀에 들어가 있는 항목을 다시 반환하지 않도록 막고 반환한다. 반환이 실제로
    /// 일어났으면 true. IsPooled는 풀의 actionOnGet/actionOnRelease에서만 갱신된다.
    /// </summary>
    private bool TryReleaseCarrotItem(CarrotItem _item)
    {
        if (_item == null || _item.IsPooled) return false;

        carrotPool.Release(_item);
        return true;
    }

    private void OnGetCarrotItem(CarrotItem _item)
    {
        _item.IsPooled = false;
        // 최적화: 마스터 리스트 추가 및 인덱스 설정 (O(1))
        _item.PoolIndex = activeItemsList.Count;
        activeItemsList.Add(_item);

        // BoundingSphere 즉시 동기화
        if (spheres == null)
        {
            spheres = new BoundingSphere[500];
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

    private void OnReleaseCarrotItem(CarrotItem _item)
    {
        _item.IsPooled = true;
        // 최적화: 업데이트 리스트에서 제거
        UpdateItemVisibility(_item, false);

        // 최적화: 마스터 리스트에서 Swap-with-last 방식을 이용한 제거 (O(1))
        int idx = _item.PoolIndex;
        if (idx != -1 && idx < activeItemsList.Count)
        {
            int lastIdx = activeItemsList.Count - 1;
            if (idx != lastIdx)
            {
                CarrotItem lastItem = activeItemsList[lastIdx];
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

    private void OnDestroyCarrotItem(CarrotItem _item)
    {
        _item.CarrotItemAcquired -= CarrotItemAcquired;
        OnReleaseCarrotItem(_item);
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
            TryReleaseCarrotItem(cleanupList[i]);
        }

        activeItemsList.Clear();
        activeItemsForUpdate.Clear();
        cleanupList.Clear();

        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(0);
        }
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
        TryReleaseCarrotItem(_item);
    }

    public void IncreaseCarrotDrop(float _amount)
    {
        dropMultiplier += (_amount / 100.0f);
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
