using UnityEngine;

public abstract class SPRegenStrategySO : ScriptableObject
{
    public abstract float CalculateRegen(float _currentSP, float _maxSP, float _spRegen, float _deltaTime, float _lastHitTimestamp);
    public abstract float CalculateOnEnableRegen(float _currentSP, float _maxSP, float _spRegen, float _disableTimestamp, float _enableTimestamp, float _lastHitTimestamp);
}
