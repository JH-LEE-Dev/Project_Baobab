using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool; // 유니티 풀링 시스템 추가

public class InDungeonUnitSpawner : MonoBehaviour
{
    public event Action<IReadOnlyList<AnimalGroup>> AnimalSpawnedEvent;

    // // 외부 의존성
    private IEnvironmentProvider environmentProvider;
    private ITilemapDataProvider tilemapDataProvider;

    // // 내부 의존성
    [SerializeField] private Animal animalPrefab;
    private IObjectPool<Animal> animalPool; // 오브젝트 풀 선언

    private List<AnimalGroup> animalGroups = new List<AnimalGroup>();
    private List<Animal> allSpawnedAnimals = new List<Animal>(SYSTEM_VAR.MAX_ANIMAL_CNT);

    // // 풀 설정 변수
    [SerializeField] private bool collectionCheck = true;
    [SerializeField] private int defaultCapacity = 20;
    [SerializeField] private int maxSize = SYSTEM_VAR.MAX_ANIMAL_CNT;

    // // 퍼블릭 메서드

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        tilemapDataProvider = environmentProvider.tilemapDataProvider;

        // 풀 초기화
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

        // 1. 전체 가용 타일 복사 (중복 선택 방지용)
        List<Vector3> walkablePositions = tilemapDataProvider.GetWalkableTileWorldPositions();
        if (walkablePositions == null || walkablePositions.Count == 0) return;

        List<Vector3> availablePositions = new List<Vector3>(walkablePositions);

        int totalToSpawn = environmentProvider.densityProvider.GetAnimalStartCnt();
        int currentlySpawned = 0;

        while (currentlySpawned < totalToSpawn && availablePositions.Count > 0)
        {
            // 2. 그룹 사이즈 결정 (3~5마리, 남은 수량 고려)
            int remaining = totalToSpawn - currentlySpawned;
            int groupSize = UnityEngine.Random.Range(3, 6);
            groupSize = Mathf.Min(groupSize, remaining);

            // 3. 그룹 중심점 선택
            int centerIdx = UnityEngine.Random.Range(0, availablePositions.Count);
            Vector3 centerPos = availablePositions[centerIdx];
            Vector3Int centerCell = tilemapDataProvider.WorldToCell(centerPos);

            AnimalGroup newGroup = new AnimalGroup(centerPos);
            
            // 4. 주변 가용한 타일 수집
            List<Vector3Int> candidateCells = new List<Vector3Int>(25);
            for (int x = -2; x <= 2; x++)
            {
                for (int y = -2; y <= 2; y++)
                {
                    Vector3Int neighborCell = centerCell + new Vector3Int(x, y, 0);
                    if (tilemapDataProvider.IsWalkable(neighborCell))
                    {
                        candidateCells.Add(neighborCell);
                    }
                }
            }

            // 5. 수집된 타일을 무작위로 섞음
            for (int i = 0; i < candidateCells.Count; i++)
            {
                int rnd = UnityEngine.Random.Range(i, candidateCells.Count);
                Vector3Int temp = candidateCells[i];
                candidateCells[i] = candidateCells[rnd];
                candidateCells[rnd] = temp;
            }

            // 6. 무작위 타일에 스폰
            int spawnedInGroup = 0;
            for (int i = 0; i < candidateCells.Count && spawnedInGroup < groupSize; i++)
            {
                Vector3Int targetCell = candidateCells[i];
                Vector3 spawnPos = tilemapDataProvider.CellToWorld(targetCell);
                
                // 실제 스폰 진행 (풀링 사용)
                Animal animal = animalPool.Get();
                animal.transform.position = spawnPos;
                animal.Initialize(environmentProvider);
                
                newGroup.members.Add(animal);
                allSpawnedAnimals.Add(animal);
                
                environmentProvider.densityProvider.UpdateAnimalCnt(true);
                
                spawnedInGroup++;
                currentlySpawned++;

                // 선택된 타일은 가용 목록에서 제거
                RemoveFromAvailable(availablePositions, spawnPos);
            }

            if (newGroup.members.Count > 0)
            {
                animalGroups.Add(newGroup);
            }
        }

        AnimalSpawnedEvent?.Invoke(animalGroups);
    }

    /// <summary>
    /// 동물을 풀로 반납합니다. 외부 매니저에서 호출될 수 있습니다.
    /// </summary>
    public void ReleaseAnimal(Animal _animal)
    {
        animalPool.Release(_animal);
    }

    /// <summary>
    /// 스폰된 모든 동물을 풀로 반납하고 상태를 초기화합니다.
    /// </summary>
    public void ReleaseAllAnimals()
    {
        if (allSpawnedAnimals == null || animalPool == null) return;

        for (int i = 0; i < allSpawnedAnimals.Count; i++)
        {
            Animal animal = allSpawnedAnimals[i];
            if (animal != null)
            {
                animalPool.Release(animal);
                // 개수 동기화가 필요한 경우 호출
                environmentProvider.densityProvider.UpdateAnimalCnt(false);
            }
        }

        allSpawnedAnimals.Clear();
        animalGroups.Clear();
    }

    // // 풀링 콜백 메서드

    private Animal CreateAnimal()
    {
        // 부모를 transform(Spawner)으로 설정하여 생성
        return Instantiate(animalPrefab, transform);
    }

    private void OnGetAnimal(Animal _animal)
    {
        _animal.gameObject.SetActive(true);
        // 상태 초기화 등이 필요한 경우 여기서 수행
    }

    private void OnReleaseAnimal(Animal _animal)
    {
        _animal.gameObject.SetActive(false);
    }

    private void OnDestroyAnimal(Animal _animal)
    {
        Destroy(_animal.gameObject);
    }

    // // 프라이빗 헬퍼 메서드

    private void RemoveFromAvailable(List<Vector3> _list, Vector3 _pos)
    {
        // 간단한 거리 기반 매칭으로 제거 (좌표가 정확히 일치하므로)
        for (int i = 0; i < _list.Count; i++)
        {
            if (Vector3.SqrMagnitude(_list[i] - _pos) < 0.01f)
            {
                _list[i] = _list[_list.Count - 1];
                _list.RemoveAt(_list.Count - 1);
                break;
            }
        }
    }
}
