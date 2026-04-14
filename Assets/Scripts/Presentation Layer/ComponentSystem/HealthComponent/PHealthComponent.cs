using UnityEngine;

public class PHealthComponent : PComponent, IPHealthComponent
{
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

    /// <summary>
    /// 컴포넌트 초기화
    /// </summary>
    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        currentStamina = maxStamina;
        currentHealth = maxHealth;
        prevHealth = currentHealth;
    }

    /// <summary>
    /// 체력 감소 (단발성 피해 등)
    /// </summary>
    public void DecreaseHealth(float _damage)
    {
        prevHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - _damage);
    }

    /// <summary>
    /// 스태미나 감소 (초당 변화량 적용)
    /// </summary>
    public void DecreaseStamina()
    {
        // staminaDecAmount는 초당 변화량이므로 Time.deltaTime을 곱함
        float amount = staminaDecAmount * Time.deltaTime;
        currentStamina = Mathf.Max(0, currentStamina - amount);
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
}
