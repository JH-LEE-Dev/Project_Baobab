using UnityEngine;

public class DensityManager : MonoBehaviour, IDensityProvider, IDensityCH
{
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

    public void Initialize()
    {

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

        maxTreeCnt = (int)(grassTileCnt * 0.3f * treeDensityMultiplier);
        maxAnimalCnt = (int)(walkableTilesCnt * 0.05f * rabbitDensityMultiplier);

        // applyToStartCnt가 true이면 현재 배율을 적용, 아니면 기본값 사용
        if (applyToStartCnt)
        {
            treeStartCnt = (int)(grassTileCnt * 0.1f * treeDensityMultiplier);
            animalStartCnt = (int)(walkableTilesCnt * 0.01f * rabbitDensityMultiplier);
        }
        else
        {
            treeStartCnt = (int)(grassTileCnt * 0.1f);
            animalStartCnt = (int)(walkableTilesCnt * 0.01f);
        }
    }

    public void IncreaseTreeDensity(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        treeDensityMultiplier += (_amount / 100.0f);
        Debug.Log("AAA");
        if (grassTileCnt > 0)
        {
            maxTreeCnt = (int)(grassTileCnt * 0.3f * treeDensityMultiplier);

            if (applyToStartCnt)
            {
                treeStartCnt = (int)(grassTileCnt * 0.1f * treeDensityMultiplier);
            }
        }

        Debug.Log($"[DensityManager] Tree Density Increased: {treeDensityMultiplier * 100}% (MaxTree: {maxTreeCnt}, StartTree: {treeStartCnt})");
    }

    public void IncreaseRabbitDensity(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        rabbitDensityMultiplier += (_amount / 100.0f);

        if (walkableTilesCnt > 0)
        {
            maxAnimalCnt = (int)(walkableTilesCnt * 0.05f * rabbitDensityMultiplier);

            if (applyToStartCnt)
            {
                animalStartCnt = (int)(walkableTilesCnt * 0.01f * rabbitDensityMultiplier);
            }
        }

        Debug.Log($"[DensityManager] Rabbit Density Increased: {rabbitDensityMultiplier * 100}% (MaxAnimal: {maxAnimalCnt}, StartAnimal: {animalStartCnt})");
    }
}
