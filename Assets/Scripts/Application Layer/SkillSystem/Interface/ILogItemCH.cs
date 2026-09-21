using UnityEngine;

public interface ILogItemControllerCH
{
    public void IncreaseJackPotChance(float _amount);
    public void IncreaseJackPotAmount(float _amount);

    /// <summary>
    /// "수종 개량" 특성. 켜지면 획득하는 모든 원목이 현재 지역에서 가장 가치가 높은 수종으로 바뀐다.
    /// (바닥에 떨어져 있는 동안은 원래 수종 그대로이고, 흡입이 시작되는 순간에 바뀐다)
    /// </summary>
    public void SetSpeciesImprovement(bool _boolean);
}
