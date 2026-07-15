using System;
using UnityEngine;

public class EHealthComponent : EComponent, IHealthComponent
{
    public event Action ShieldBrokenEvent;
    public event Action ShieldRegenedEvent;
    public event Action ShieldRecoveringEvent;
    public event Action EnemyIsDeadEvent;

    // 실드 회복 중 프레임마다 이벤트를 쏘면 UI/Signal 체인에 과도한 부하가 걸리므로 일정 간격으로만 알림
    private const float shieldRecoverNotifyInterval = 0.15f;
    private float lastShieldRecoverNotifyTime = -100f;

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
    private bool bFirstDamage = false;
    public bool bIsFirstDamage => bFirstDamage;

    private ISporeShieldStatProvider shieldStatProvider;
    private float EffectiveSpRegen => spRegen * Mathf.Max(0f, 1f - (shieldStatProvider?.ShieldRegenReductionMul ?? 0f));

    // 발현 낙인 - 별자리 발현 광선에 맞은 나무에 영구 적용되는 데미지 배율 (나무가 죽어 리셋될 때까지 유지)
    private float brandedDamageMultiplier = 1f;

    public void ApplyDamageBrand(float _multiplier)
    {
        brandedDamageMultiplier = Mathf.Max(brandedDamageMultiplier, _multiplier);
    }

    public bool IsBranded => brandedDamageMultiplier > 1f;

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

    public void Initialize(ISporeShieldStatProvider _shieldStatProvider = null)
    {
        shieldStatProvider = _shieldStatProvider;

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
        bFirstDamage = false;
        brandedDamageMultiplier = 1f;

        enabled = (!isShieldBroken && currentSP < maxSP && spRegen > 0f && regenStrategy != null);
    }

    public void DecreaseHealth(float _damage)
    {
        if (bFirstDamage == false)
            bFirstDamage = true;

        prevSP = currentSP;
        prevHealth = currentHealth;
        lastHitTimestamp = Time.time;

        _damage *= brandedDamageMultiplier;

        float remainingDamage = _damage;

        if (currentSP > 0f)
        {
            // 원래 포자막이 흡수했을 데미지량
            float shieldPortion = Mathf.Min(currentSP, _damage);

            // 포자 절단 - 흡수분에만 배율 적용 (잘못된 데이터로 음수가 되어 포자막이 역회복되는 것을 방지)
            float shieldDamageMultiplier = Mathf.Max(0f, shieldStatProvider?.ShieldDamageMultiplier ?? 1f);
            float amplifiedShieldDamage = shieldPortion * shieldDamageMultiplier;

            // 포자 관통력 - 흡수된 데미지의 일부를 체력에 전달
            float shieldPenetrationPercent = Mathf.Max(0f, shieldStatProvider?.ShieldPenetrationPercent ?? 0f);
            float penetrationDamage = amplifiedShieldDamage * shieldPenetrationPercent;

            currentSP = Mathf.Clamp(currentSP - amplifiedShieldDamage, 0f, maxSP);
            // 오버플로우(shieldPortion을 넘는 원본 데미지)는 기존과 동일하게 버려지고, 관통력으로 인한 데미지만 체력에 전달됨
            remainingDamage = penetrationDamage;

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
            float newSP = regenStrategy.CalculateOnEnableRegen(currentSP, maxSP, EffectiveSpRegen, disableTimestamp, enableTime, lastHitTimestamp);

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
            currentSP = regenStrategy.CalculateRegen(currentSP, maxSP, EffectiveSpRegen, Time.deltaTime, lastHitTimestamp);

            if (currentSP > 0f && isShieldBroken)
            {
                isShieldBroken = false;
                ShieldRegenedEvent?.Invoke();
            }

            // 리젠 완료 시 Update 비활성화
            bool regenCompleted = currentSP >= maxSP;
            if (regenCompleted)
            {
                currentSP = maxSP;
                enabled = false;
            }

            if (currentSP > prevSP)
            {
                // 회복 완료 시점은 간격과 무관하게 항상 알려 마지막 갱신이 누락되지 않게 한다
                if (regenCompleted || Time.time - lastShieldRecoverNotifyTime >= shieldRecoverNotifyInterval)
                {
                    lastShieldRecoverNotifyTime = Time.time;
                    ShieldRecoveringEvent?.Invoke();
                }
            }
        }
        else
        {
            enabled = false;
        }
    }
}





