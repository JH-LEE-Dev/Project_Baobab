using System;
using UnityEngine;

public class EHealthComponent : EComponent, IHealthComponent
{
    public event Action ShieldBrokenEvent;
    public event Action ShieldRegenedEvent;
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
    private bool isShieldBroken;

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
        isShieldBroken = (maxSP <= 0f);

        disableTimestamp = -1f;
        lastHitTimestamp = -100f;

        regenStrategy = _regenStrategy;

        isSetup = true;

        // 리젠이 불가능하거나 이미 꽉 차있거나 실드가 깨진 상태라면 Update 호출 비활성화
        enabled = (!isShieldBroken && currentSP < maxSP && spRegen > 0f && regenStrategy != null);
    }

    public void Initialize()
    {
        currentHealth = maxHealth;
        prevHealth = maxHealth;
        currentSP = maxSP;
        prevSP = maxSP;
        isShieldBroken = (maxSP <= 0f);
        disableTimestamp = -1f;
        lastHitTimestamp = -100f;

        enabled = (!isShieldBroken && currentSP < maxSP && spRegen > 0f && regenStrategy != null);
    }

    public void Reset()
    {
        currentHealth = maxHealth;
        prevHealth = maxHealth;
        currentSP = maxSP;
        prevSP = maxSP;
        isShieldBroken = (maxSP <= 0f);
        disableTimestamp = -1f;
        lastHitTimestamp = -100f;

        enabled = (!isShieldBroken && currentSP < maxSP && spRegen > 0f && regenStrategy != null);
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
                currentSP = 0f;
                remainingDamage = 0f;
            }

            if (currentSP <= 0f && !isShieldBroken)
            {
                isShieldBroken = true;
                ShieldBrokenEvent?.Invoke();
            }
        }

        // 쉴드가 깎였으므로 리젠 연산을 위해 Update 활성화 (단, 쉴드가 깨진 상태면 활성화하지 않음)
        if (!isShieldBroken && currentSP < maxSP && spRegen > 0f && regenStrategy != null)
        {
            enabled = true;
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

        // 실드가 이미 깨진 상태라면 리젠하지 않음
        if (!isShieldBroken && disableTimestamp > 0f && regenStrategy != null)
        {
            float enableTime = Time.time;
            float newSP = regenStrategy.CalculateOnEnableRegen(currentSP, maxSP, spRegen, disableTimestamp, enableTime, lastHitTimestamp);
            
            if (Mathf.Abs(newSP - currentSP) > 0.0001f)
            {
                prevSP = currentSP;
                currentSP = newSP;

                if (currentSP > 0f && isShieldBroken)
                {
                    isShieldBroken = false;
                    ShieldRegenedEvent?.Invoke();
                }
            }
            disableTimestamp = -1f;
        }

        // 활성화되었을 때 리젠할 필요가 없거나 실드가 깨졌다면 Update 비활성화
        if (isShieldBroken || currentSP >= maxSP || spRegen <= 0f || regenStrategy == null)
        {
            enabled = false;
        }
    }

    private void OnDisable()
    {
        disableTimestamp = Time.time;
    }

    private void Update()
    {
        if (!isShieldBroken && currentSP < maxSP && spRegen > 0f && regenStrategy != null)
        {
            prevSP = currentSP;
            currentSP = regenStrategy.CalculateRegen(currentSP, maxSP, spRegen, Time.deltaTime, lastHitTimestamp);

            if (currentSP > 0f && isShieldBroken)
            {
                isShieldBroken = false;
                ShieldRegenedEvent?.Invoke();
            }

            // 리젠 완료 시 Update 비활성화
            if (currentSP >= maxSP)
            {
                currentSP = maxSP;
                enabled = false;
            }
        }
        else
        {
            enabled = false;
        }
    }
}





