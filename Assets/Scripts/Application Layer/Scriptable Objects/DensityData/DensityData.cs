using System.Collections.Generic;

[System.Serializable]
public class DensityData
{
    public ForestType forestType;
    public List<TreeDensityData> spawnTreeTypes;
    public List<AnimalDensityData> spawnAnimalTypes;
    public float treeStartDensityRatio;
    public float treeMaxDensityRatio;
    public float animalStartDensityRatio;
    public float animalMaxDensityRatio;
    public float treeRegenMinTime;
    public float treeRegenMaxTime;
    public float animalRegenMinTime;
    public float animalRegenMaxTime;
    public float limitHiddenGauge;
}

[System.Serializable]
public class MapDensityData
{
    public MapType mapType;
    public List<DensityData> densityData;
}

[System.Serializable]
public struct TreeDensityData
{
    public TreeType treeType;
    public float regenProb;
}

[System.Serializable]
public struct AnimalDensityData
{
    public AnimalType animalType;
    public float regenProb;
}

public struct ForestHiddenGaugeData
{
    public ForestType forestType;
    public float hiddenGauge;
}

[System.Serializable]
public struct AnimalHiddenGaugeAmountData
{
    public AnimalType animalType;
    public float minAmount;
    public float maxAmount;
}

[System.Serializable]
public struct TreeHiddenGaugeAmountData
{
    public TreeType treeType;
    public float minAmount;
    public float maxAmount;
}

[System.Serializable]
public struct ForestEnvironmentInfo
{
    public ForestType forestType;
    public List<TreeDensityData> spawnTreeTypes;
    public List<AnimalDensityData> spawnAnimalTypes;
    public float limitHiddenGauge;
    public float currentHiddenGauge;
}

[System.Serializable]
public struct MapEnvironmentDataInfo
{
    public MapType mapType;
    public List<ForestEnvironmentInfo> forestDatas;
}

[System.Serializable]
public struct MapEnvironmentDatabase
{
    public List<MapEnvironmentDataInfo> mapDatas;
}

