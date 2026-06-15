using System;
using UnityEngine;

public class EHealthComponent : EComponent, IHealthComponent
{
    public event Action EnemyIsDeadEvent;

    //외부 의존성
    [SerializeField] private float maxHealth;
    
    //내부 의존성
    private float currentHealth;
    private float prevHealth;

    private float maxSP;
    private float spRegen;

    private float currentSP;
    private float prevSP;

    private TreeType treeType;
    private float disableTimestamp;
    private float lastHitTimestamp;
    private bool isSetup;

    private SPRegenStrategySO regenStrategy;

    public void Setup(TreeType _treeType, float _maxHealth, float _maxSP, float _spRegen, SPRegenStrategySO _regenStrategy)
    {
        maxHealth = _maxHealth;
        currentHealth = maxHealth;
        prevHealth = maxHealth;

        treeType = _treeType;

        maxSP = _maxSP;
        spRegen = _spRegen;
        currentSP = maxSP;
        prevSP = maxSP;

        disableTimestamp = -1f;
        lastHitTimestamp = -100f;

        regenStrategy = _regenStrategy;

        isSetup = true;
    }

    public void Initialize()
    {
        currentHealth = maxHealth;
        prevHealth = maxHealth;
        currentSP = maxSP;
        prevSP = maxSP;
        disableTimestamp = -1f;
        lastHitTimestamp = -100f;
    }

    public void Reset()
    {
        currentHealth = maxHealth;
        prevHealth = maxHealth;
        currentSP = maxSP;
        prevSP = maxSP;
        disableTimestamp = -1f;
        lastHitTimestamp = -100f;
    }

    public void DecreaseHealth(float _damage)
    {
        prevSP = currentSP;
        prevHealth = currentHealth;
        lastHitTimestamp = Time.time;

        float remainingDamage = _damage;

        if (currentSP > 0f)
        {
            if (currentSP >= remainingDamage)
            {
                currentSP -= remainingDamage;
                remainingDamage = 0f;
            }
            else
            {
                remainingDamage -= currentSP;
                currentSP = 0f;
            }
        }

        if (remainingDamage > 0f)
        {
            if (currentHealth - remainingDamage <= 0f)
            {
                currentHealth = 0f;
                EnemyIsDeadEvent?.Invoke();
                return;
            }

            currentHealth -= remainingDamage;
        }
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
    
    public float GetMaxSP()
    {
        return maxSP;
    }

    public float GetCurrentSP()
    {
        return currentSP;
    }

    public float GetPrevSP()
    {
        return prevSP;
    }

    private void OnEnable()
    {
        if (!isSetup)
        {
            return;
        }

        if (disableTimestamp > 0f && regenStrategy != null)
        {
            float enableTime = Time.time;
            float newSP = regenStrategy.CalculateOnEnableRegen(currentSP, maxSP, spRegen, disableTimestamp, enableTime, lastHitTimestamp);
            
            if (Mathf.Abs(newSP - currentSP) > 0.0001f)
            {
                prevSP = currentSP;
                currentSP = newSP;
            }
            disableTimestamp = -1f;
        }
    }

    private void OnDisable()
    {
        disableTimestamp = Time.time;
    }

    private void Update()
    {
        if (currentSP < maxSP && spRegen > 0f && regenStrategy != null)
        {
            prevSP = currentSP;
            currentSP = regenStrategy.CalculateRegen(currentSP, maxSP, spRegen, Time.deltaTime, lastHitTimestamp);
        }
    }
}





