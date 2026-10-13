/// <summary>
/// 본편 코인 HUD(UI_Inventory의 CurrencyCounterHUD)가 런 골드를 읽게 하는 재화 출처. 기획서 5-1 "코인 HUD - 현재 보유 골드,
/// 본 게임의 코인 HUD를 그대로 사용한다". 코인 외 재화는 로그라이크에 없으므로 0이다.
/// </summary>
public sealed class RunGoldMoneyData : IMoneyData
{
    private readonly IRoguelikeRunInfo runInfo;

    public RunGoldMoneyData(IRoguelikeRunInfo _runInfo)
    {
        runInfo = _runInfo;
    }

    public long money => null != runInfo ? runInfo.Gold : 0;
    public long carrot => 0;
    public long sunEssence => 0;
    public long moonEssence => 0;
    public long lightningEssence => 0;
    public long goldOre => 0;
    public long diamondOre => 0;
    public long prismOre => 0;
    public long GemOrePouchCapacity => 0;
    public long TotalGemOre => 0;

    public long GetMoney(MoneyType _moneyType) => MoneyType.Coin == _moneyType ? money : 0;

    public bool HasEverAcquired(MoneyType _moneyType) => MoneyType.Coin == _moneyType;
}
