using System.Collections.Generic;
using UnityEngine;

public class InDungeonUnitSpawner : MonoBehaviour
{
    // 외부 의존성
    private IEnvironmentProvider environmentProvider;
    private ITilemapDataProvider tilemapDataProvider;


    // 내부 의존성
    [SerializeField] private Animal animalPrefab;
    [SerializeField] private int spawnCount = 10;

    private List<Animal> spawnedAnimals = new List<Animal>(SYSTEM_VAR.MAX_ANIMAL_CNT);

    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        tilemapDataProvider = environmentProvider.tilemapDataProvider;
    }

    public void SpawnAnimals()
    {
        if (tilemapDataProvider == null || animalPrefab == null)
        {
            return;
        }

        List<Vector3> walkablePositions = tilemapDataProvider.GetWalkableTileWorldPositions();
        if (walkablePositions == null || walkablePositions.Count == 0)
        {
            return;
        }

        int targetCount = Mathf.Min(spawnCount, SYSTEM_VAR.MAX_ANIMAL_CNT);
        int availableCount = walkablePositions.Count;

        for (int i = 0; i < targetCount; i++)
        {
            if (availableCount <= 0)
            {
                break;
            }

            // 무작위 인덱스 선택
            int randomIndex = Random.Range(0, availableCount);
            Vector3 spawnPos = walkablePositions[randomIndex];

            // 선택된 위치를 리스트의 마지막 요소와 교체하여 중복 선택 방지 (O(1) 삭제 효과)
            walkablePositions[randomIndex] = walkablePositions[availableCount - 1];
            availableCount--;

            // 동물 생성 및 리스트 추가
            Animal animal = Instantiate(animalPrefab, spawnPos, Quaternion.identity, transform);
            animal.Initialize(environmentProvider);
            spawnedAnimals.Add(animal);
        }
    }
}
