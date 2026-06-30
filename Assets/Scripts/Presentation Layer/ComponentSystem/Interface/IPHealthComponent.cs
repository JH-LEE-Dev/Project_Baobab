using UnityEngine;

public interface IPHealthComponent : IBaseHealthComponent
{
    float GetMaxStamina();
    float GetCurrentStamina();
}
