using UnityEngine;

public class DensityManager : MonoBehaviour, IDensityProvider, IDensityCH
{
    // 밀도 설정 상수
    [SerializeField] private float TreeMaxDensityRatio = 0.07f;
    [SerializeField] private float TreeStartDensityRatio = 0.04f;
    [SerializeField] private float RabbitMaxDensityRatio = 0.05f;
    [SerializeField] private float RabbitStartDensityRatio = 0.02f;

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
    [SerializeField] private float treeRegenMinTime = 10f;
    [SerializeField] private float treeRegenMaxTime = 15f;
    [SerializeField] private float rabbitRegenMinTime = 10f;
    [SerializeField] private float rabbitRegenMaxTime = 15f;

    [SerializeField] private bool applyToStartCnt = false;

    public void Initialize()
    {

    }

    public float GetTreeRegenTime()
    {
        return Random.Range(treeRegenMinTime, treeRegenMaxTime);
    }

    public float GetAnimalRegenTime()
    {
        return Random.Range(rabbitRegenMinTime, rabbitRegenMaxTime);
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
        treeCnt = 0;
        animalCnt = 0;

        grassTileCnt = _grassCnt;
        walkableTilesCnt = _walkableCnt;

        maxTreeCnt = (int)(grassTileCnt * TreeMaxDensityRatio * treeDensityMultiplier);
        maxAnimalCnt = (int)(walkableTilesCnt * RabbitMaxDensityRatio * rabbitDensityMultiplier);

        // applyToStartCnt가 true이면 현재 배율을 적용, 아니면 기본값 사용
        if (applyToStartCnt)
        {
            treeStartCnt = (int)(grassTileCnt * TreeStartDensityRatio * treeDensityMultiplier);
            animalStartCnt = (int)(walkableTilesCnt * RabbitStartDensityRatio * rabbitDensityMultiplier);
        }
        else
        {
            treeStartCnt = (int)(grassTileCnt * TreeStartDensityRatio);
            animalStartCnt = (int)(walkableTilesCnt * RabbitStartDensityRatio);
        }
    }

    public void IncreaseTreeDensity(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        treeDensityMultiplier += (_amount / 100.0f);

        if (grassTileCnt > 0)
        {
            maxTreeCnt = (int)(grassTileCnt * TreeMaxDensityRatio * treeDensityMultiplier);

            if (applyToStartCnt)
            {
                treeStartCnt = (int)(grassTileCnt * TreeStartDensityRatio * treeDensityMultiplier);
            }
        }

        //Debug.Log($"[DensityManager] Tree Density Increased: {treeDensityMultiplier * 100}% (MaxTree: {maxTreeCnt}, StartTree: {treeStartCnt})");
    }

    public void IncreaseRabbitDensity(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        rabbitDensityMultiplier += (_amount / 100.0f);

        if (walkableTilesCnt > 0)
        {
            maxAnimalCnt = (int)(walkableTilesCnt * RabbitMaxDensityRatio * rabbitDensityMultiplier);

            if (applyToStartCnt)
            {
                animalStartCnt = (int)(walkableTilesCnt * RabbitStartDensityRatio * rabbitDensityMultiplier);
            }
        }

        //Debug.Log($"[DensityManager] Rabbit Density Increased: {rabbitDensityMultiplier * 100}% (MaxAnimal: {maxAnimalCnt}, StartAnimal: {animalStartCnt})");
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
}
