using System.Collections.Generic;
using UnityEngine;

public class DensityManager : MonoBehaviour, IDensityProvider, IDensityCH, IMapDataProvider
{
    [SerializeField] private MapDensityDataBase densityDataBase;

    private int grassTileCnt;
    private int walkableTilesCnt;
    private int treeCnt;
    private int animalCnt;
    private int maxTreeCnt;
    private int maxAnimalCnt;
    private int animalStartCnt;
    private int treeStartCnt;

    private float treeDensityMultiplier = 1.0f;
    private float rabbitDensityMultiplier = 1.0f;

    [SerializeField] private bool applyToStartCnt = false;

    private MapType currentMapType;
    private DensityData currentDensityData;

    public void Initialize()
    {

    }

    public void SetDensityData(ForestType _forestType, MapType _mapType)
    {
        currentMapType = _mapType;
        currentDensityData = densityDataBase.Get(_mapType, _forestType);
    }

    public float GetTreeRegenTime()
    {
        if (currentDensityData == null) return 10f;
        return Random.Range(currentDensityData.treeRegenMinTime, currentDensityData.treeRegenMaxTime);
    }

    public float GetAnimalRegenTime()
    {
        if (currentDensityData == null) return 10f;
        return Random.Range(currentDensityData.animalRegenMinTime, currentDensityData.animalRegenMaxTime);
    }

    public TreeType GetTreeTypeToSpawn()
    {
        if (currentDensityData == null || currentDensityData.spawnTreeTypes == null || currentDensityData.spawnTreeTypes.Count == 0)
            return TreeType.None;

        float totalProb = 0;
        for (int i = 0; i < currentDensityData.spawnTreeTypes.Count; i++)
        {
            totalProb += currentDensityData.spawnTreeTypes[i].regenProb;
        }

        if (totalProb <= 0) return TreeType.None;

        float randomValue = Random.Range(0, totalProb);
        float cumulativeProb = 0;

        for (int i = 0; i < currentDensityData.spawnTreeTypes.Count; i++)
        {
            cumulativeProb += currentDensityData.spawnTreeTypes[i].regenProb;
            if (randomValue <= cumulativeProb)
            {
                return currentDensityData.spawnTreeTypes[i].treeType;
            }
        }

        return currentDensityData.spawnTreeTypes[0].treeType;
    }

    public AnimalType GetAnimalTypeToSpawn()
    {
        if (currentDensityData == null || currentDensityData.spawnAnimalTypes == null || currentDensityData.spawnAnimalTypes.Count == 0)
            return AnimalType.None;

        float totalProb = 0;
        for (int i = 0; i < currentDensityData.spawnAnimalTypes.Count; i++)
        {
            totalProb += currentDensityData.spawnAnimalTypes[i].regenProb;
        }

        if (totalProb <= 0) return AnimalType.None;

        float randomValue = Random.Range(0, totalProb);
        float cumulativeProb = 0;

        for (int i = 0; i < currentDensityData.spawnAnimalTypes.Count; i++)
        {
            cumulativeProb += currentDensityData.spawnAnimalTypes[i].regenProb;
            if (randomValue <= cumulativeProb)
            {
                return currentDensityData.spawnAnimalTypes[i].animalType;
            }
        }

        return currentDensityData.spawnAnimalTypes[0].animalType;
    }

    public void SetApplyToStartCnt(bool _value)
    {
        applyToStartCnt = _value;
    }

    public bool CanCreateAnimal()
    {
        if (animalCnt >= maxAnimalCnt)
            return false;
        else
            return true;
    }

    public bool CanCreateTree()
    {
        if (treeCnt >= maxTreeCnt)
            return false;
        else
            return true;
    }

    public void UpdateAnimalCnt(bool _up)
    {
        if (_up == false)
        {
            animalCnt -= 1;
            if (animalCnt < 0)
                animalCnt = 0;
        }
        else
        {
            animalCnt += 1;
            if (animalCnt > maxAnimalCnt)
                animalCnt = maxAnimalCnt;
        }
    }

    public void UpdateTreeCnt(bool _up)
    {
        if (_up == false)
        {
            treeCnt -= 1;
            if (treeCnt < 0)
                treeCnt = 0;
        }
        else
        {
            treeCnt += 1;
            if (treeCnt > maxTreeCnt)
                treeCnt = maxTreeCnt;
        }
    }

    public int GetTreeStartCnt()
    {
        return treeStartCnt;
    }

    public int GetAnimalStartCnt()
    {
        return animalStartCnt;
    }

    public void SetActiveTilesCnt(int _grassCnt, int _walkableCnt)
    {
        if (currentDensityData == null) return;

        treeCnt = 0;
        animalCnt = 0;

        grassTileCnt = _grassCnt;
        walkableTilesCnt = _walkableCnt;

        maxTreeCnt = (int)(grassTileCnt * currentDensityData.treeMaxDensityRatio * treeDensityMultiplier);
        maxAnimalCnt = (int)(walkableTilesCnt * currentDensityData.animalMaxDensityRatio * rabbitDensityMultiplier);

        // applyToStartCnt가 true이면 현재 배율을 적용, 아니면 기본값 사용
        if (applyToStartCnt)
        {
            treeStartCnt = (int)(grassTileCnt * currentDensityData.treeStartDensityRatio * treeDensityMultiplier);
            animalStartCnt = (int)(walkableTilesCnt * currentDensityData.animalStartDensityRatio * rabbitDensityMultiplier);
        }
        else
        {
            treeStartCnt = (int)(grassTileCnt * currentDensityData.treeStartDensityRatio);
            animalStartCnt = (int)(walkableTilesCnt * currentDensityData.animalStartDensityRatio);
        }
    }

    public void IncreaseTreeDensity(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        treeDensityMultiplier += (_amount / 100.0f);
    }

    public void IncreaseRabbitDensity(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        rabbitDensityMultiplier += (_amount / 100.0f);
    }

    public EnvironmentSaveData GetSaveData()
    {
        return new EnvironmentSaveData
        {
            treeDensityMultiplier = treeDensityMultiplier,
            rabbitDensityMultiplier = rabbitDensityMultiplier
        };
    }

    public void LoadSaveData(EnvironmentSaveData _data)
    {
        treeDensityMultiplier = _data.treeDensityMultiplier;
        rabbitDensityMultiplier = _data.rabbitDensityMultiplier;

        // 현재 타일 수 정보가 있다면 데이터 갱신
        if (grassTileCnt > 0 || walkableTilesCnt > 0)
        {
            SetActiveTilesCnt(grassTileCnt, walkableTilesCnt);
        }

        Debug.Log("[DensityManager] Environment Save Data Loaded.");
    }

    public MapDensityDataBase GetMapDataBase()
    {
        return densityDataBase;
    }
}
