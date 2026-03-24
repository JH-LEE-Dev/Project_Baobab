using UnityEngine;

public class PHealthComponent : PComponent,IPHealthComponent
{
    [SerializeField] private float maxHealth;
    private float currentHealth;
    private float prevHealth;

    [SerializeField] private float maxStamina;
    private float currentStamina;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        currentStamina = maxStamina;
        currentHealth = maxHealth;
        prevHealth = currentHealth;
    }

    public void DecreaseHealth(float _damage)
    {
        prevHealth = currentHealth;

        if (currentHealth - _damage <= 0)
        {
            currentHealth = 0;
            return;
        }

        currentHealth -= _damage;
    }

    public void DecreaseStamina(float _stamina)
    {
        if (currentStamina - _stamina <= 0)
        {
            currentStamina = 0;
            return;
        }

        currentStamina -= _stamina;
    }

    public void IncreaseStamina(float _stamina)
    {
        if (currentStamina + _stamina >= maxStamina)
        {
            currentStamina = maxStamina;
            return;
        }

        currentStamina += _stamina;
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

