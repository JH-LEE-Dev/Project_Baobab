
public interface IDensityProvider
{
    bool CanCreateAnimal();
    bool CanCreateTree();
    void UpdateTreeCnt(bool _up);
    void UpdateAnimalCnt(bool _up);
    int GetTreeStartCnt();
    int GetAnimalStartCnt();
    void SetActiveTilesCnt(int _cnt1,int _cnt2);
    float GetTreeRegenTime();
    float GetAnimalRegenTime();
}

