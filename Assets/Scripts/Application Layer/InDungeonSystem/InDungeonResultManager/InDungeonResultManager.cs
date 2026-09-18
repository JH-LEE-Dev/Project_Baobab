using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던전 한 판(런) 동안의 성과를 모아두는 곳. 결과창(UIView_Result)이 여기서 값을 읽어 간다.
///
/// 모든 값은 런 단위다. StartDungeonSystem()이 Reset()을 부르므로 던전에 들어갈 때마다 0에서
/// 다시 시작한다. 영구 누적이 필요한 값(소지 재화, 보유 전리품)은 여기가 아니라 각자의
/// 주인(InventoryManager, InDungeonObjectManager.currentOwnedLoots)이 들고 세이브에 저장한다.
/// </summary>
public class InDungeonResultManager : MonoBehaviour, IDungeonResultProvider
{
    // GemOreType을 인덱스로 쓰는 배열. None(0)은 쓰지 않지만 자리를 비워두면 (int)형변환으로
    // 그대로 색인할 수 있어 분기가 사라진다.
    private static readonly int gemOreTypeCount = Enum.GetValues(typeof(GemOreType)).Length;

    private int treeKillCnt;
    private int lostLogItemCnt;

    private readonly long[] acquiredGemOreAmounts = new long[gemOreTypeCount];

    // 얻은 순서를 유지한다. 종류가 다섯 뿐이라 Contains 선형 탐색으로 충분하다.
    private readonly List<LootType> acquiredLoots = new List<LootType>(4);

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

    /// <summary>
    /// 원석을 주웠을 때 그 재화량을 이번 런 몫으로 더한다.
    ///
    /// 여기 쌓이는 값은 "주운 것"만이다. 마을에서 용광로가 원석을 되돌려주는 환불 경로는
    /// 이 집계를 거치지 않으므로(InventoryManager.GemOreEarned를 직접 부른다) 섞이지 않는다.
    /// </summary>
    public void AddAcquiredGemOre(GemOreType _gemOreType, long _amount)
    {
        if (_amount <= 0) return;

        int index = (int)_gemOreType;
        if (index <= 0 || index >= acquiredGemOreAmounts.Length) return;

        acquiredGemOreAmounts[index] += _amount;
    }

    /// <summary>전리품을 얻었을 때 이번 런 목록에 남긴다. 같은 종류를 두 번 넣지 않는다.</summary>
    public void AddAcquiredLoot(LootType _lootType)
    {
        if (LootType.None == _lootType || LootType.Max == _lootType) return;
        if (true == acquiredLoots.Contains(_lootType)) return;

        acquiredLoots.Add(_lootType);
    }

    public int GetTreeKillCnt()
    {
        return treeKillCnt;
    }

    public int GetLostLogItemCnt()
    {
        return lostLogItemCnt;
    }

    public long GetAcquiredGemOreAmount(GemOreType _gemOreType)
    {
        int index = (int)_gemOreType;
        if (index <= 0 || index >= acquiredGemOreAmounts.Length) return 0;

        return acquiredGemOreAmounts[index];
    }

    public bool HasAcquiredAnyGemOre()
    {
        for (int i = 1; i < acquiredGemOreAmounts.Length; i++)
        {
            if (acquiredGemOreAmounts[i] > 0) return true;
        }

        return false;
    }

    public IReadOnlyList<LootType> GetAcquiredLoots()
    {
        return acquiredLoots;
    }

    public void Reset()
    {
        treeKillCnt = 0;
        lostLogItemCnt = 0;

        Array.Clear(acquiredGemOreAmounts, 0, acquiredGemOreAmounts.Length);
        acquiredLoots.Clear();
    }
}
