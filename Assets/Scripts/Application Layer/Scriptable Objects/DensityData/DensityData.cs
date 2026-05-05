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