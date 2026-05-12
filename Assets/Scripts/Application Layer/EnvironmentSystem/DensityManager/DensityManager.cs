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

    private Dictionary<ForestType, MapHiddenGaugeSaveData> hiddenGaugeData;
    public List<AnimalHiddenGaugeAmountData> animalHiddenGaugeAmounts;
    public List<TreeHiddenGaugeAmountData> treeHiddenGaugeAmounts;

    // //외부 의존성 및 캐시 데이터
    private MapEnvironmentDatabase cachedDatabase;
    private bool isDatabaseInitialized = false;

    public void Initialize()
    {
        hiddenGaugeData = new Dictionary<ForestType, MapHiddenGaugeSaveData>();
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

    public void PopulateSaveData(ref EnvironmentSaveData _data)
    {
        _data.treeDensityMultiplier = treeDensityMultiplier;
        _data.rabbitDensityMultiplier = rabbitDensityMultiplier;

        _data.hiddenGaugeDatas.Clear();
        foreach (var kvp in hiddenGaugeData)
        {
            _data.hiddenGaugeDatas.Add(kvp.Value);
        }

        // bCanAccess 정보 수집
        _data.mapAccessDatas.Clear();
        if (densityDataBase != null && densityDataBase.densityDatas != null)
        {
            for (int i = 0; i < densityDataBase.densityDatas.Count; i++)
            {
                var mapData = densityDataBase.densityDatas[i];
                for (int j = 0; j < mapData.densityData.Count; j++)
                {
                    var density = mapData.densityData[j];
                    _data.mapAccessDatas.Add(new MapAccessSaveData
                    {
                        mapType = mapData.mapType,
                        forestType = density.forestType,
                        bCanAccess = density.bCanAccess
                    });
                }
            }
        }
    }

    public void LoadSaveData(EnvironmentSaveData _data)
    {
        treeDensityMultiplier = _data.treeDensityMultiplier;
        rabbitDensityMultiplier = _data.rabbitDensityMultiplier;

        hiddenGaugeData.Clear();
        if (_data.hiddenGaugeDatas != null)
        {
            for (int i = 0; i < _data.hiddenGaugeDatas.Count; i++)
            {
                var saved = _data.hiddenGaugeDatas[i];
                hiddenGaugeData[saved.forestType] = saved;
            }
        }

        // bCanAccess 정보 복구
        if (_data.mapAccessDatas != null)
        {
            for (int i = 0; i < _data.mapAccessDatas.Count; i++)
            {
                var saved = _data.mapAccessDatas[i];
                var density = densityDataBase.Get(saved.mapType, saved.forestType);
                if (density != null)
                {
                    density.bCanAccess = saved.bCanAccess;
                }
            }
        }

        // 현재 타일 수 정보가 있다면 데이터 갱신
        if (grassTileCnt > 0 || walkableTilesCnt > 0)
        {
            SetActiveTilesCnt(grassTileCnt, walkableTilesCnt);
        }

        Debug.Log("[DensityManager] Environment Save Data Loaded.");
    }

    private void EnsureDatabaseInitialized()
    {
        if (isDatabaseInitialized) return;
        if (densityDataBase == null || densityDataBase.densityDatas == null) return;

        cachedDatabase.mapDatas = new List<MapEnvironmentDataInfo>(densityDataBase.densityDatas.Count);
        for (int i = 0; i < densityDataBase.densityDatas.Count; i++)
        {
            var mapData = densityDataBase.densityDatas[i];
            var mapInfo = new MapEnvironmentDataInfo();
            mapInfo.mapType = mapData.mapType;
            mapInfo.forestDatas = new List<ForestEnvironmentInfo>(mapData.densityData.Count);

            for (int j = 0; j < mapData.densityData.Count; j++)
            {
                var density = mapData.densityData[j];
                var forestInfo = new ForestEnvironmentInfo
                {
                    forestType = density.forestType,
                    spawnTreeTypes = density.spawnTreeTypes,
                    spawnAnimalTypes = density.spawnAnimalTypes,
                    limitHiddenGauge = density.limitHiddenGauge,
                    currentHiddenGauge = 0f,
                    bCanAccess = density.bCanAccess
                };
                mapInfo.forestDatas.Add(forestInfo);
            }
            cachedDatabase.mapDatas.Add(mapInfo);
        }
        isDatabaseInitialized = true;
    }

    public MapEnvironmentDatabase GetMapEnvironmentDatabase()
    {
        EnsureDatabaseInitialized();

        for (int i = 0; i < cachedDatabase.mapDatas.Count; i++)
        {
            var mapInfo = cachedDatabase.mapDatas[i];

            for (int j = 0; j < mapInfo.forestDatas.Count; j++)
            {
                var forestInfo = mapInfo.forestDatas[j];

                // forestType을 키로 게이지 정보 조회
                if (hiddenGaugeData.TryGetValue(forestInfo.forestType, out MapHiddenGaugeSaveData gaugeData))
                {
                    forestInfo.currentHiddenGauge = gaugeData.hiddenGauge;
                }
                else
                {
                    forestInfo.currentHiddenGauge = 0f;
                }

                // 최신 bCanAccess 정보 업데이트
                var originalData = densityDataBase.Get(mapInfo.mapType, forestInfo.forestType);
                if (originalData != null)
                {
                    forestInfo.bCanAccess = originalData.bCanAccess;
                }

                // 구조체 업데이트 (Write back to list)
                mapInfo.forestDatas[j] = forestInfo;
            }
        }

        return cachedDatabase;
    }

    public void AddHiddenGauge(AnimalType _type)
    {
        if (_type == AnimalType.None) return;

        float amount = 0f;
        for (int i = 0; i < animalHiddenGaugeAmounts.Count; i++)
        {
            if (animalHiddenGaugeAmounts[i].animalType == _type)
            {
                amount = Random.Range(animalHiddenGaugeAmounts[i].minAmount, animalHiddenGaugeAmounts[i].maxAmount);
                break;
            }
        }

        if (amount <= 0f) return;

        AddAmountToHiddenGauge(amount);
    }

    public void AddHiddenGauge(TreeType _type)
    {
        if (_type == TreeType.None) return;

        float amount = 0f;
        for (int i = 0; i < treeHiddenGaugeAmounts.Count; i++)
        {
            if (treeHiddenGaugeAmounts[i].treeType == _type)
            {
                amount = Random.Range(treeHiddenGaugeAmounts[i].minAmount, treeHiddenGaugeAmounts[i].maxAmount);
                break;
            }
        }

        if (amount <= 0f) return;

        AddAmountToHiddenGauge(amount);
    }

    private void AddAmountToHiddenGauge(float _amount)
    {
        if (currentDensityData == null) return;

        ForestType fType = currentDensityData.forestType;

        if (hiddenGaugeData.TryGetValue(fType, out MapHiddenGaugeSaveData data))
        {
            data.hiddenGauge += _amount;
            hiddenGaugeData[fType] = data;
        }   
        else
        {
            MapHiddenGaugeSaveData newData = new MapHiddenGaugeSaveData();
            newData.mapType = currentMapType;
            newData.forestType = fType;
            newData.hiddenGauge = _amount;
            hiddenGaugeData.Add(fType, newData);
        }
    }

    public bool IsCurrentlyHiddenMap(MapType _mapType, ForestType _forestType)
    {
        if (hiddenGaugeData.TryGetValue(_forestType, out MapHiddenGaugeSaveData data))
        {
            DensityData densityData = densityDataBase.Get(_mapType, _forestType);
            if (densityData == null) return false;

            if (data.hiddenGauge >= densityData.limitHiddenGauge)
            {
                data.hiddenGauge = 0f;
                hiddenGaugeData[_forestType] = data;
                return true;
            }
        }

        return false;
    }

    public void PrestigeLevelIncreased(int _level)
    {
        if (densityDataBase == null || densityDataBase.densityDatas == null) return;

        int globalForestIndex = 0;

        // 모든 MapType을 순차적으로 순회
        for (int i = 0; i < densityDataBase.densityDatas.Count; i++)
        {
            var mapData = densityDataBase.densityDatas[i];
            if (mapData.densityData == null) continue;

            // 각 맵의 ForestType을 순차적으로 순회
            for (int j = 0; j < mapData.densityData.Count; j++)
            {
                // 첫 번째 맵의 첫 번째 포레스트(globalForestIndex == 0)는 이미 해금되어 있음
                // globalForestIndex가 1 이상이고 현재 명성 레벨(_level) 이하인 경우 해금
                if (globalForestIndex > 0 && globalForestIndex <= _level)
                {
                    mapData.densityData[j].bCanAccess = true;
                }
                
                globalForestIndex++;
            }
        }
    }
}
