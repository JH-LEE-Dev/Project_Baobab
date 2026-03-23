using UnityEngine;

public interface IHealthComponent
{
    float GetMaxHealth();
    float GetCurrentHealth();
    float GetPrevHealth();
}
