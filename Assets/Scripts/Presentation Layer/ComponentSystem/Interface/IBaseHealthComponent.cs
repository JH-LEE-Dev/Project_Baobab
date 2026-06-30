using UnityEngine;

public interface IBaseHealthComponent
{
    float GetMaxHealth();
    float GetCurrentHealth();
    float GetPrevHealth();
    bool bIsFirstDamage { get; }
}
