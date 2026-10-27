using UnityEngine;

public interface IOffroadContainerCH
{
    public void ExpandInventorySlotCnt(float _amount);
    public void LogCapacityIncrease(float _amount);
    public void ItemTransferSpeedUP(float _amount);
    public void ColliderRangeIncrease(float _amount);
    public void SetRemoteLogTransport(bool _bActive);
    public void SetAutoLoadInRestArea(bool _bActive);
    public void IncreaseLogInsurancePercent(float _amount);
}
