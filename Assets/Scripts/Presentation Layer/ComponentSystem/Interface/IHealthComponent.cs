using UnityEngine;

public interface IHealthComponent : IBaseHealthComponent
{
    float GetMaxSP();
    float GetCurrentSP();
    float GetPrevSP();
}
