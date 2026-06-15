using UnityEngine;

public interface IHealthComponent
{
    float GetMaxHealth();
    float GetCurrentHealth();
    float GetPrevHealth();
    float GetMaxSP();
    float GetCurrentSP();
    float GetPrevSP();
}
