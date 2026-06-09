using UnityEngine;

public class InDungeonResultManager : MonoBehaviour, IDungeonResultProvider
{
    private int treeKillCnt;
    private int lostLogItemCnt;

    public void Initialize()
    {

    }

    public void IncreaseTreeKillCnt()
    {
        treeKillCnt++;
    }

    public void IncreaseLostLogItemCnt(int _cnt)
    {
        lostLogItemCnt += _cnt;
    }

    public int GetTreeKillCnt()
    {
        return treeKillCnt;
    }

    public int GetLostLogItemCnt()
    {
        return lostLogItemCnt;
    }

    public void Reset()
    {
        treeKillCnt = 0;
        lostLogItemCnt = 0;
    }
}
