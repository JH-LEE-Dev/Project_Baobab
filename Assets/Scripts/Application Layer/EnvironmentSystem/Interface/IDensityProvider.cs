
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

    /// <summary>
    /// 현재 지역(ForestType)에 실제로 자라는 수종 중 가치가 가장 높은 것을 돌려준다.
    /// 가치 순서는 TreeType enum 인덱스와 같다(LogItemValueDataBase.asset의 가치가 enum 순서대로 매겨져 있다).
    /// "수종 개량" 특성이 변환 목표를 고를 때 쓴다. 지역 정보가 없으면 TreeType.None.
    /// </summary>
    TreeType GetMostValuableTreeType();

    AnimalType GetAnimalTypeToSpawn();
}

