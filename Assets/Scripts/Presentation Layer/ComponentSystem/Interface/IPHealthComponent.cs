using UnityEngine;

public interface IPHealthComponent : IBaseHealthComponent
{
    float GetMaxStamina();
    float GetCurrentStamina();
    void RestoreStaminaByPercent(float _percent);
}
