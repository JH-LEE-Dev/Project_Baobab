using UnityEngine;

[CreateAssetMenu(fileName = "NormalRegenStrategy", menuName = "Game/Regen/NormalRegenStrategy")]
public class NormalRegenStrategy : SPRegenStrategySO
{
    public override float CalculateRegen(float _currentSP, float _maxSP, float _spRegen, float _deltaTime, float _lastHitTimestamp)
    {
        return Mathf.Min(_maxSP, _currentSP + (_spRegen * _deltaTime));
    }

    public override float CalculateOnEnableRegen(float _currentSP, float _maxSP, float _spRegen, float _disableTimestamp, float _enableTimestamp, float _lastHitTimestamp)
    {
        float elapsed = _enableTimestamp - _disableTimestamp;
        if (elapsed <= 0f)
        {
            return _currentSP;
        }
        return Mathf.Min(_maxSP, _currentSP + (elapsed * _spRegen));
    }
}
