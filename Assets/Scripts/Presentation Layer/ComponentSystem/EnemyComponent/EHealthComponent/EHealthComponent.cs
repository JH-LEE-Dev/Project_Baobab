using System;
using UnityEngine;

public class EHealthComponent : EComponent
{
    public event Action EnemyIsDeadEvent;

    [SerializeField] private float maxHealth;
    private float currentHealth;

    public void Initialize()
    {
        currentHealth = maxHealth;
    }

    public void Reset()
    {
        currentHealth = maxHealth;
    }
    public void DecreaseHealth(float _damage)
    {
        if (currentHealth - _damage <= 0)
        {
            currentHealth = 0;
            EnemyIsDeadEvent?.Invoke();
            return;
        }

        currentHealth -= _damage;
    }
}

