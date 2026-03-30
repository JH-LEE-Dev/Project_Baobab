using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InDungeonUnitSpawner : MonoBehaviour
{
    // 그룹 정보를 넘기지 않도록 이벤트 수정 (필요 시 Action<IReadOnlyList<Animal>> 등으로 변경 가능)
    public event Action AnimalSpawnedEvent;

    // // 외부 의존성
    private IEnvironmentProvider environmentProvider;
    private ITilemapDataProvider tilemapDataProvider;

    // // 내부 의존성
    [SerializeField] private Animal animalPrefab;
    private IObjectPool<Animal> animalPool;

    private List<Animal> allSpawnedAnimals = new List<Animal>(SYSTEM_VAR.MAX_ANIMAL_CNT);
    private List<int> availableIndices = new List<int>(1024); // GC 방지용 캐싱 인덱스 리스트

    // // 풀 설정 변수
    [SerializeField] private bool collectionCheck = true;
    [SerializeField] private int defaultCapacity = 20;
    [SerializeField] private int maxSize = SYSTEM_VAR.MAX_ANIMAL_CNT;

    // // 퍼블릭 메서드

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        tilemapDataProvider = environmentProvider.tilemapDataProvider;

        animalPool = new ObjectPool<Animal>(
            CreateAnimal, 
            OnGetAnimal, 
            OnReleaseAnimal, 
            OnDestroyAnimal, 
            collectionCheck, 
            defaultCapacity, 
            maxSize
        );
    }

    public void SpawnAnimals()
    {
        if (tilemapDataProvider == null || animalPrefab == null || environmentProvider.densityProvider == null)
        {
            return;
        }

        // 1. 전체 가용 타일 가져오기 (원본 리스트)
        List<Vector3> walkablePositions = tilemapDataProvider.GetWalkableTileWorldPositions();
        if (walkablePositions == null || walkablePositions.Count == 0) return;

        int totalToSpawn = environmentProvider.densityProvider.GetAnimalStartCnt();
        int spawnLimit = Mathf.Min(totalToSpawn, walkablePositions.Count);

        // 2. 컬렉션 재사용: 원본 리스트 보존 및 할당 방지를 위해 인덱스만 활용
        availableIndices.Clear();
        if (availableIndices.Capacity < walkablePositions.Count)
        {
            availableIndices.Capacity = walkablePositions.Count;
        }

        for (int i = 0; i < walkablePositions.Count; i++)
        {
            availableIndices.Add(i);
        }

        // 3. 필요한 수만큼만 부분 셔플(Partial Fisher-Yates) 및 순차 스폰
        for (int i = 0; i < spawnLimit; i++)
        {
            int rnd = UnityEngine.Random.Range(i, availableIndices.Count);
            int selectedIndex = availableIndices[rnd];
            
            // 선택된 인덱스 교환하여 중복 선택 방지
            availableIndices[rnd] = availableIndices[i];
            availableIndices[i] = selectedIndex;

            Vector3 spawnPos = walkablePositions[selectedIndex];
            
            Animal animal = animalPool.Get();
            animal.transform.position = spawnPos;
            animal.Initialize(environmentProvider);
            
            allSpawnedAnimals.Add(animal);
            environmentProvider.densityProvider.UpdateAnimalCnt(true);
        }

        AnimalSpawnedEvent?.Invoke();
    }

    public void ReleaseAnimal(Animal _animal)
    {
        animalPool.Release(_animal);
    }

    public void ReleaseAllAnimals()
    {
        if (allSpawnedAnimals == null || animalPool == null) return;

        for (int i = 0; i < allSpawnedAnimals.Count; i++)
        {
            Animal animal = allSpawnedAnimals[i];
            if (animal != null)
            {
                animalPool.Release(animal);
                environmentProvider.densityProvider.UpdateAnimalCnt(false);
            }
        }

        allSpawnedAnimals.Clear();
    }

    // // 풀링 콜백 메서드

    private Animal CreateAnimal()
    {
        return Instantiate(animalPrefab, transform);
    }

    private void OnGetAnimal(Animal _animal)
    {
        _animal.gameObject.SetActive(true);
    }

    private void OnReleaseAnimal(Animal _animal)
    {
        _animal.gameObject.SetActive(false);
    }

    private void OnDestroyAnimal(Animal _animal)
    {
        Destroy(_animal.gameObject);
    }
}
