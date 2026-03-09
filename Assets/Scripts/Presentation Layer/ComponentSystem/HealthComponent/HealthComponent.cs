using UnityEngine;

public class PHealthComponent : PComponent
{
    [SerializeField] private float maxHealth;
    private float currentHealth;

    [SerializeField] private float maxStamina;
    private float currentStamina;

    public float CurrentStamina => currentStamina;
    public float MaxStamina => maxStamina;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        currentStamina = maxStamina;
        currentHealth = maxHealth;
    }

    public void DecreaseHealth(float _damage)
    {
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
}

