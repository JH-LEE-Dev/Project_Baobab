using UnityEngine;

public interface IHealthComponent : IBaseHealthComponent
{
    float GetMaxSP();
    float GetCurrentSP();
    float GetPrevSP();
    void ApplyDamageBrand(float _multiplier);
    bool IsBranded { get; }
}
