using UnityEngine;

public interface IHealthComponent : IBaseHealthComponent
{
    float GetMaxSP();
    float GetCurrentSP();
    float GetPrevSP();
    void ApplyDamageBrand(float _multiplier);
    bool IsBranded { get; }
    // 남은 체력이 최대 체력의 _ratio 이하인가(float 오차 여유 포함). 최후의 일격·약점 판정용
    bool IsHealthRatioAtOrBelow(float _ratio);
}
