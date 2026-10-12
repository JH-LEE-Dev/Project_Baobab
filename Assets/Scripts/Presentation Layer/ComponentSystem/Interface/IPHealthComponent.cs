using System;
using UnityEngine;

public interface IPHealthComponent : IBaseHealthComponent
{
    /// <summary>스태미나가 한 번에 줄어든 순간 발생합니다. 인자는 최대치 대비 실제로 줄어든 비율(0~1)입니다. 매 프레임 쌓이는 지속 소모에서는 발생하지 않습니다.</summary>
    event Action<float> StaminaDamagedEvent;

    /// <summary>스태미나가 한 번에 회복된 순간 발생합니다. 인자는 최대치 대비 실제로 늘어난 비율(0~1)입니다. 매 프레임 쌓이는 자연 회복에서는 발생하지 않습니다.</summary>
    event Action<float> StaminaRecoveredEvent;

    /// <summary>한 번의 회복이 최대치에 막혀 일부 또는 전부 버려진 순간 발생합니다. 휴식 구역 안에서는 발생하지 않습니다.</summary>
    event Action StaminaRecoverWastedEvent;

    /// <summary>차량 휴식 구역 안에서 회복 중인지(휴식 특성이 열려 있고 원 안에 있음).</summary>
    bool IsInRestArea { get; }

    float GetMaxStamina();
    float GetCurrentStamina();
    void RestoreStaminaByPercent(float _percent);
}
