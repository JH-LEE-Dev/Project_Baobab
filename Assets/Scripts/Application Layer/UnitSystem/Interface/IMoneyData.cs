using UnityEngine;

public interface IMoneyData
{
    long money { get; }
    long carrot { get; }
    long sunEssence { get; }
    long moonEssence { get; }
    long lightningEssence { get; }

    // 보석 나무 원석 재화 (황금/다이아/프리즘)
    long goldOre { get; }
    long diamondOre { get; }
    long prismOre { get; }

    /// <summary>
    /// 재화 종류로 현재 보유량을 돌려준다.
    /// HUD(CurrencyCounterHUD)가 MoneyType 하나만 들고 값을 끌어갈 수 있도록 하는 통합 진입점으로,
    /// 재화가 늘어날 때마다 UI 쪽에 if 사슬이 생기는 것을 막는다.
    /// </summary>
    long GetMoney(MoneyType _moneyType);
}
