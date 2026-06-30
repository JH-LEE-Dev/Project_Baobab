using UnityEngine;

public interface ILogItemControllerCH
{
    public void IncreaseDropProb(LogState _logState, float _amount);
    public void IncreaseJackPotChance(float _amount);
    public void IncreaseJackPotAmount(float _amount);
}
