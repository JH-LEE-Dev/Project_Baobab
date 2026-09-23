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

    // 8hp 나무를 1.6 데미지로 5대 때리면 결과가 정확히 0이 아니라 0.00000024가 남아, 엄격한
    // <= 0f 판정을 통과하지 못하고 6대째에야 죽었다. 이 비율 이하로 남은 티끌 체력은 죽은 것으로 본다.
    //
    // 아래 totalDamageTaken 방식 덕분에 오차가 타수에 비례해 커지지 않으므로, 실제로 필요한
    // 최소 기준은 전 구간(체력 13종 x 보석 4단계 x 타수 2~500) 통틀어 5.8e-08이다. 여기에 17배
    // 남겨 잡은 값이다. 최대 체력이 아주 작은 나무에서도 기준이 0으로 무너지지 않도록 하한 1을 둔다.
    private const float deathThresholdRatio = 0.000001f;

    //외부 의존성
    [SerializeField] private float maxHealth;

    // 보석 단계 체력 배율. maxHealth는 항상 일반 상태의 기준값 그대로 두고 이 배율만 갈아끼우므로,
    // 단계가 올라가도(황금 -> 다이아 -> 프리즘) 배율이 누적되지 않고 기준값이 오염되지도 않는다.
    private float gemHealthMultiplier = 1f;

    //내부 의존성
    private float currentHealth;
    private float prevHealth;

    // 지금까지 체력에 들어온 데미지의 누계. 체력을 float로 매번 빼면 반올림 오차가 타수만큼
    // 쌓여서(500대면 최대 체력의 5.8e-06), "다 깎았는데 안 죽는" 상황을 막으려면 사망 기준을
    // 그만큼 헐겁게 잡아야 한다. 누계를 double로 들고 남은 체력을 매번 다시 만들면 오차가
    // 타수와 무관하게 5.8e-08로 떨어져서, 기준을 100배 빡빡하게 잡을 수 있다.
    //
    // 체력을 가득 채우는 곳(Setup / Initialize / Reset / ReviveFullHealth)에서는 반드시 0으로
    // 되돌려야 한다. 안 그러면 이전 생애에 받은 데미지가 남아 되살아나자마자 죽는다.
    private double totalDamageTaken;

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
        gemHealthMultiplier = 1f;
        currentHealth = maxHealth;
        prevHealth = maxHealth;
        totalDamageTaken = 0.0;

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

        gemHealthMultiplier = 1f;
        currentHealth = maxHealth;
        prevHealth = maxHealth;
        totalDamageTaken = 0.0;
        currentSP = maxSP;
        prevSP = maxSP;
        isShieldBroken = (maxSP <= 0f);
        disableTimestamp = -1f;
        lastHitTimestamp = -100f;

        enabled = (!isShieldBroken && currentSP < maxSP && spRegen > 0f && regenStrategy != null);
    }

    public void Reset()
    {
        // 풀에서 재사용될 때 보석 단계 배율이 남아 있으면 안 되므로 1배로 되돌린다.
        gemHealthMultiplier = 1f;
        currentHealth = maxHealth;
        prevHealth = maxHealth;
        totalDamageTaken = 0.0;
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
            totalDamageTaken += remainingDamage;

            float maxHp = GetMaxHealth();

            if (totalDamageTaken >= maxHp - GetDeathThreshold())
            {
                currentHealth = 0f;
                EnemyIsDeadEvent?.Invoke();
                return;
            }

            // 남은 체력은 빼서 이어가는 게 아니라 누계에서 매번 다시 만든다. 이래야 오차가
            // 타수만큼 쌓이지 않는다.
            currentHealth = (float)(maxHp - totalDamageTaken);
        }
    }

    /// <summary>
    /// 남은 체력이 이 값 이하면 0으로 간주한다. 데미지 누계와 최대 체력이 정확히 맞아떨어져야 할
    /// 때(8hp를 1.6씩 5대) 부동소수 표현 오차로 "분명 다 깎았는데 안 죽는" 일이 없도록 하는 여유값이다.
    /// </summary>
    private float GetDeathThreshold()
    {
        return Mathf.Max(GetMaxHealth(), 1f) * deathThresholdRatio;
    }

    // 나무 등급별 셰이더 단계 전환용: 실제로 죽이지 않고 체력을 되살린다.
    // _maxHealthMultiplier는 그 나무의 일반 상태 최대 체력에 곱해지는 배율로, 보석 단계마다 다르다
    // (황금 2배, 다이아 3배, 프리즘 3.5배). 배율은 누적되지 않고 항상 기준값에 다시 곱해진다.
    //
    // 체력 바를 비롯한 소비처는 전부 현재 체력/최대 체력 비율로 계산하므로, 회복량만 늘리면 비율이
    // 1을 넘어 깨진다. 그래서 최대 체력(GetMaxHealth)도 함께 배율만큼 올리고 그 값으로 가득 채운다.
    //
    // currentSP/isShieldBroken은 건드리지 않는다 - 실드가 깨진 상태였다면 단계가 전환돼도 깨진 채로 유지되어야 한다.
    public void ReviveFullHealth(float _maxHealthMultiplier = 1f)
    {
        gemHealthMultiplier = Mathf.Max(0f, _maxHealthMultiplier);
        currentHealth = GetMaxHealth();
        prevHealth = currentHealth;
        // 최대 체력이 새 배율로 바뀌면서 체력도 가득 찼으므로 데미지 누계도 함께 비운다.
        totalDamageTaken = 0.0;
    }

    public float GetMaxHealth()
    {
        return maxHealth * gemHealthMultiplier;
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





