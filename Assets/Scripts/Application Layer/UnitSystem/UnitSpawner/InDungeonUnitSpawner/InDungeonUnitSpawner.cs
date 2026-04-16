using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InDungeonUnitSpawner : MonoBehaviour
{
    public event Action<Animal> AnimalIsDeadEvent;
    //그룹 정보를 넘기지 않도록 이벤트 수정 (필요 시 Action<IReadOnlyList<Animal>> 등으로 변경 가능)
    public event Action AnimalSpawnedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;
    private ITilemapDataProvider tilemapDataProvider;

    //내부 의존성
    [Header("Spawn Settings")]
    [SerializeField] private Animal animalPrefab;
    [SerializeField] private float spawnInterval = 2.0f;

    private IObjectPool<Animal> animalPool;
    private Coroutine growthCoroutine;
    private WaitForSeconds spawnYield;

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

        spawnYield = new WaitForSeconds(spawnInterval);

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
    {return;
        if (tilemapDataProvider == null || animalPrefab == null || environmentProvider.densityProvider == null)
        {
            return;
        }

        StopGrowth();

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

            availableIndices[rnd] = availableIndices[i];
            availableIndices[i] = selectedIndex;

            Vector3 spawnPos = walkablePositions[selectedIndex];
            SpawnAnimalAt(spawnPos);
        }

        AnimalSpawnedEvent?.Invoke();

        // 4. 5초 후 점진적 스폰 루틴 시작
        growthCoroutine = StartCoroutine(StartGrowthAfterDelay());
    }

    private IEnumerator StartGrowthAfterDelay()
    {
        yield return new WaitForSeconds(5f);
        growthCoroutine = StartCoroutine(GrowthRoutine());
    }

    private IEnumerator GrowthRoutine()
    {
        while (true)
        {
            yield return spawnYield;

            if (environmentProvider.densityProvider.CanCreateAnimal())
            {
                SpawnOneAnimalFromAvailable();
            }
        }
    }

    private bool SpawnOneAnimalFromAvailable()
    {
        List<Vector3> walkablePositions = tilemapDataProvider.GetWalkableTileWorldPositions();
        int count = walkablePositions.Count;
        if (count == 0) return false;

        int startIdx = UnityEngine.Random.Range(0, count);
        for (int i = 0; i < count; i++)
        {
            int checkIdx = (startIdx + i) % count;
            Vector3 spawnPos = walkablePositions[checkIdx];
            Vector3Int cellPos = tilemapDataProvider.WorldToCell(spawnPos);

            if (!environmentProvider.pathfindGridProvider.IsOccupied(cellPos))
            {
                SpawnAnimalAt(spawnPos);
                return true;
            }
        }
        return false;
    }

    private void SpawnAnimalAt(Vector3 _pos)
    {
        Animal animal = animalPool.Get();
        animal.transform.position = _pos;
        animal.Initialize(environmentProvider);

        allSpawnedAnimals.Add(animal);
        environmentProvider.densityProvider.UpdateAnimalCnt(true);

        animal.AnimalIsDeadEvent -= AnimalIsDead;
        animal.AnimalIsDeadEvent += AnimalIsDead;
    }

    public void ReleaseAnimal(Animal _animal)
    {
        animalPool.Release(_animal);
    }

    public void ReleaseAllAnimals()
    {
        StopGrowth();

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

    private void StopGrowth()
    {
        if (growthCoroutine != null)
        {
            StopCoroutine(growthCoroutine);
            growthCoroutine = null;
        }
    }

    // // 풀링 콜백 메서드

    private Animal CreateAnimal()
    {
        return Instantiate(animalPrefab, transform);
    }

    private void OnGetAnimal(Animal _animal)
    {
        _animal.gameObject.SetActive(true);
        _animal.Reset();
    }

    private void OnReleaseAnimal(Animal _animal)
    {
        _animal.gameObject.SetActive(false);
    }

    private void OnDestroyAnimal(Animal _animal)
    {
        Destroy(_animal.gameObject);
    }

    private void AnimalIsDead(Animal _aniaml)
    {
        environmentProvider.densityProvider.UpdateAnimalCnt(false);
        AnimalIsDeadEvent?.Invoke(_aniaml);
        allSpawnedAnimals.Remove(_aniaml);
        ReleaseAnimal(_aniaml);
    }
}
