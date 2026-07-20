using System;
using UnityEngine;

public class PHealthComponent : PComponent, IPHealthComponent
{
    public event Action StaminaIsEmptyEvent;
    // 외부 의존성 (SerializeField)
    [SerializeField] private float maxHealth;
    [SerializeField] private float maxStamina;

    // 내부 의존성
    private float currentHealth;
    private float prevHealth;
    private float currentStamina;

    private float staminaDecAmount = 0f;
    private float staminaIncAmount = 0f;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;

    private bool bFirstDamage = false;
    public bool bIsFirstDamage => bFirstDamage;

    private bool bStaminaDecrease = false;

    /// <summary>
    /// 컴포넌트 초기화
    /// </summary>
    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        currentStamina = maxStamina;
        currentHealth = maxHealth;
        prevHealth = currentHealth;
        bFirstDamage = false;
    }

    /// <summary>
    /// 체력 감소 (단발성 피해 등)
    /// </summary>
    public void DecreaseHealth(float _damage)
    {
        prevHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - _damage);
        if (bFirstDamage == false)
            bFirstDamage = true;
    }

    /// <summary>
    /// 스태미나 감소 (초당 변화량 적용)
    /// </summary>
    public void DecreaseStamina()
    {
        if (currentStamina <= 0 || bStaminaDecrease == false)
            return;

        // staminaDecAmount는 초당 변화량이므로 Time.deltaTime을 곱함
        float amount = staminaDecAmount * Time.deltaTime;
        currentStamina = Mathf.Max(0, currentStamina - amount);

        if (currentStamina <= 0)
        {
            StaminaIsEmptyEvent?.Invoke();
        }
    }

    /// <summary>
    /// 환경 위험 지형(용암 등)으로 인한 스태미나 추가 소모. 캐릭터 스탯 보정(staminaDecreaseAlpha)의
    /// 영향을 받지 않는 고정값으로, 최종 소모량에 그대로 더해진다.
    /// </summary>
    public void ApplyEnvironmentalStaminaDrain(float _drainPerSecond)
    {
        if (currentStamina <= 0 || _drainPerSecond <= 0f)
            return;

        float amount = _drainPerSecond * Time.deltaTime;
        currentStamina = Mathf.Max(0, currentStamina - amount);

        if (currentStamina <= 0)
        {
            StaminaIsEmptyEvent?.Invoke();
        }
    }

    /// <summary>
    /// 스태미나 회복 (초당 변화량 적용)
    /// </summary>
    public void IncreaseStamina()
    {
        // staminaIncAmount는 초당 변화량이므로 Time.deltaTime을 곱함
        float amount = staminaIncAmount * Time.deltaTime;
        currentStamina = Mathf.Min(maxStamina, currentStamina + amount);
    }

    /// <summary>
    /// 초당 변화량과 무관하게 최대 스태미나의 _percent(%)만큼 즉시 회복시킨다("포자 포션" 등 소비 아이템용).
    /// </summary>
    public void RestoreStaminaByPercent(float _percent)
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + maxStamina * (_percent / 100f));
    }

    public void SetStaminaIncreaseAmount(float _staminaIncAmount)
    {
        staminaIncAmount = _staminaIncAmount;
    }

    public void SetStaminaDecreaseAmount(float _staminaDecAmount)
    {
        staminaDecAmount = _staminaDecAmount;
    }

    public float GetMaxHealth()
    {
        return maxHealth;
    }

    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    public float GetPrevHealth()
    {
        return prevHealth;
    }

    public float GetMaxStamina()
    {
        return maxStamina;
    }

    public float GetCurrentStamina()
    {
        return currentStamina;
    }

    public void SetMaxStamina(float _maxStamina)
    {
        float diff = _maxStamina - maxStamina;
        maxStamina = _maxStamina;

        // 최대치가 늘어난 만큼 현재치도 보정 (선택 사항이나 보통 긍정적 경험 제공)
        if (diff > 0)
        {
            currentStamina += diff;
        }

        currentStamina = Mathf.Min(currentStamina, maxStamina);
    }

    public void StaminaReset()
    {
        currentStamina = maxStamina;
    }

    public void SetStaminaDecrease(bool _boolean)
    {
        bStaminaDecrease = _boolean;
    }

    public void StaminaRecover(float _amount)
    {
        currentStamina = Mathf.Min(maxStamina, currentStamina + _amount);
    }
}
