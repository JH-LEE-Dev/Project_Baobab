using UnityEngine;

public interface ILogProcessingSystemCH
{
    public void IncreaseConveyorSpeed(float _amount);
    public void LogProcessorSpeedUp(float _amount);
    public void ExpandProcessLineCnt(float _amount);
    public void SetRemoteDeposit(bool _bActive);
}
