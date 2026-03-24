using UnityEngine;

public interface IPHealthComponent
{
    float GetMaxHealth();
    float GetCurrentHealth();
    float GetPrevHealth();

    float GetMaxStamina();
    float GetCurrentStamina();
}
