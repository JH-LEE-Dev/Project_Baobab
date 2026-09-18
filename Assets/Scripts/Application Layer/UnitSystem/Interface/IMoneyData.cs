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

    /// <summary>
    /// 이 재화를 한 번이라도 얻은 적이 있는지. 보유량이 0으로 돌아가도 true로 남고 세이브에 저장된다.
    ///
    /// HUD가 "아직 발견하지 못한 재화"를 숨기는 데 쓴다. 보유량만으로 판정하면 원석을 전부
    /// 용광로에 넣은 순간 다시 숨겨지고, 게임을 껐다 켜면 발견 사실 자체가 사라진다.
    ///
    /// 원석이 아닌 재화(코인/당근 등)는 숨기는 개념이 없으므로 항상 true다.
    /// </summary>
    bool HasEverAcquired(MoneyType _moneyType);
}
