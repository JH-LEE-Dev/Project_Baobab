using System;
using UnityEngine;

public class EHealthComponent : EComponent, IHealthComponent
{
    public event Action EnemyIsDeadEvent;

    [SerializeField] private float maxHealth;
    private float currentHealth;
    private float prevHealth;

    public void Initialize()
    {
        currentHealth = maxHealth;
        prevHealth = maxHealth;
    }

    public void Reset()
    {
        currentHealth = maxHealth;
        prevHealth = maxHealth;
    }

    public void DecreaseHealth(float _damage)
    {
        prevHealth = currentHealth;

        if (currentHealth - _damage <= 0)
        {
            currentHealth = 0;

            EnemyIsDeadEvent?.Invoke();
            return;
        }

        currentHealth -= _damage;
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
}

