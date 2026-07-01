
public interface IDensityProvider
{
    bool CanCreateAnimal();
    bool CanCreateTree(MapType _mapType);
    void UpdateTreeCnt(bool _up);
    void UpdateAnimalCnt(bool _up);
    int GetTreeStartCnt(MapType _mapType);
    int GetAnimalStartCnt();
    void SetActiveTilesCnt(int _cnt1,int _cnt2);
    float GetTreeRegenTime();
    float GetAnimalRegenTime();
    TreeType GetTreeTypeToSpawn();
    AnimalType GetAnimalTypeToSpawn();
}

