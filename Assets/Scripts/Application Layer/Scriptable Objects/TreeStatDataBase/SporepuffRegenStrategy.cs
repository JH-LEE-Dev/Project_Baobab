using UnityEngine;

[CreateAssetMenu(fileName = "SporepuffRegenStrategy", menuName = "Game/Regen/SporepuffRegenStrategy")]
public class SporepuffRegenStrategy : SPRegenStrategySO
{
    private const float delayTime = 0f;

    public override float CalculateRegen(float _currentSP, float _maxSP, float _spRegen, float _deltaTime, float _lastHitTimestamp)
    {
        if (Time.time - _lastHitTimestamp < delayTime)
        {
            return _currentSP;
        }
        return Mathf.Min(_maxSP, _currentSP + (_spRegen * _deltaTime));
    }

    public override float CalculateOnEnableRegen(float _currentSP, float _maxSP, float _spRegen, float _disableTimestamp, float _enableTimestamp, float _lastHitTimestamp)
    {
        float elapsed = _enableTimestamp - _disableTimestamp;
        if (elapsed <= 0f)
        {
            return _currentSP;
        }

        float regenStartTime = _lastHitTimestamp + delayTime;
        float activeRegenDuration = elapsed;

        if (_enableTimestamp < regenStartTime)
        {
            activeRegenDuration = 0f;
        }
        else if (_disableTimestamp < regenStartTime)
        {
            activeRegenDuration = _enableTimestamp - regenStartTime;
        }

        if (activeRegenDuration > 0f)
        {
            return Mathf.Min(_maxSP, _currentSP + (activeRegenDuration * _spRegen));
        }

        return _currentSP;
    }
}
