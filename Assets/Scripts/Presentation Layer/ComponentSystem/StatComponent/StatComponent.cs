using System;
using UnityEngine;

public class StatComponent : PComponent, IStatComponent, ICharacterStatCH, ICharacterStatForNPC
{
    public event Action CanHuntEvent;
    // 시간제 이동속도 버프(속도의 원천 · 별길 걸음) 중 하나가 걸릴 때 / 모두 풀릴 때. 헤이스트 VFX를 켜고 끈다.
    // 끝 이벤트의 인자가 참이면 씬 전환 등으로 강제 해제된 것이라 연출 없이 바로 끈다.
    public event Action HasteStartedEvent;
    public event Action<bool> HasteEndedEvent;
    // 속도의 원천이 걸릴 때마다(다시 걸려 시간이 처음부터일 때 포함, 인자는 지속 시간) / 씬 전환 등으로 강제로 풀릴 때.
    // 시간이 다 돼 풀리는 경우는 따로 알리지 않는다(같은 지속 시간을 받은 쪽이 스스로 끝낸다).
    public event Action<float> SourceOfSpeedActivatedEvent;
    public event Action SourceOfSpeedForceEndedEvent;

    [Header("For Debugging")]
    // 에디터에서 플레이를 누르기 전에 캐릭터 소지금을 원하는 값으로 맞춰두기 위한 치트 값이다.
    // bOverrideStartMoney를 켰을 때만 적용되며, UnitSystem이 에디터에서만 반영하므로 빌드에는 영향이 없다.
    [Tooltip("체크하면 게임 시작 시 캐릭터 소지금을 아래 값으로 강제한다. (에디터 전용)")]
    public bool bOverrideStartMoney = false;
    [Tooltip("bOverrideStartMoney가 켜져 있을 때 적용할 소지금. 이어하기로 들어가도 세이브 값 대신 이 값이 들어간다.")]
    public long startMoney = 0;

    [Header("Character Stat")]
    public float pickupRangeMultiplier = 1f;
    private PercentAccumulator pickupRangeAccum;

    [Header("Movement")]
    public float originalSpeed = 1f;
    // [로그라이크] 공격 중 감속을 적용하지 않는다(기획서 v2.2 3-1). 본편은 행동 중 speedDecreaseWhileAction배로 줄어든다.
    public float speed => (activeActionCount > 0 && false == GameModeContext.IsRoguelike) ? originalSpeed * speedDecreaseWhileAction : originalSpeed;

    private int activeActionCount = 0;

    public void AddActionState()
    {
        activeActionCount++;
    }

    public void RemoveActionState()
    {
        activeActionCount--;
        if (activeActionCount < 0) activeActionCount = 0;
    }
    public float baseSpeed { get; private set; }
    public float speedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator speedAccum;
    public float sourceOfSpeedAmount = 0f;
    private PercentAccumulator sourceOfSpeedAmountAccum;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float staminaIncreaseAlpha = 0f;
    public float staminaDecreaseAlpha = 0f;
    public float baseMaxStamina { get; private set; }
    public float maxStaminaBonus { get; private set; } = 0f;
    public float sourceOfStaminaRecoverAmount = 0f;
    private float currentSourceOfSpeedBonus = 0f;
    private Coroutine sourceOfSpeedCoroutine;
    public float staminaRecoverAmount = 0f;

    [Header("General Weapon Settings")]
    public float weaponChangeCoolTime = 0.5f;
    public bool bCanHunting = false;
    public float baseWeaponChangeCoolTime { get; private set; }
    public float switchSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator switchSpeedAccum;

    [Header("Axe Settings")]
    public float axeDamage = 1f;
    public float speedDecreaseWhileAction = 0.5f;
    private PercentAccumulator speedDecreaseWhileActionAccum;
    public float axeDurability = 30f;
    public float axeDurabilityDecAmount = 1f;
    public float axeAttackCoolTime = 1.2f;
    public float axeAttackRangeMultiplier = 1f;
    private PercentAccumulator axeAttackRangeAccum;
    public float axeDurabilityDecIgnoreChance = 0f;
    public float baseAxeDamage { get; private set; }
    public float axeDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator axeDamageAccum;
    public float baseAxeAttackCoolTime { get; private set; }
    public float axeAttackSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator axeAttackSpeedAccum;

    [Header("Axe - Shockwave")]
    public float shockWaveChance = 0f;
    public float shockWaveDamage = 1f;
    public float shockWaveSpeed = 2f;
    public float shockWaveDuration = 0.2f;
    public float shockWaveCreateDelay = 0f;
    public float baseShockWaveDamage { get; private set; }
    public float shockWaveDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator shockWaveDamageAccum;
    public float baseShockWaveSpeed { get; private set; }
    public float shockWaveSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator shockWaveSpeedAccum;
    public bool bShockWaveCritical = false;
    public bool bShockWaveEnforcement = false;
    public bool bShockWaveMastery = false;
    public bool bOverheat = false; // [미사용] 과열은 이제 4스테이지 맵 규칙이라 특성 없이 작동한다. 특성 재구성 때 정리
    public bool bShockWaveOverheatBoost = false; // "과열 충격파" 특성 - 충격파가 과열 수치에 영향을 받는다(과열 0이어도 적용)

    [Header("Dash")]
    // 대시의 기본 저장 횟수·충전 쿨타임은 "원목 보험 증서" 성장 단계가 정한다(Character의 Dash Settings).
    // 여기는 그 위에 특성 등으로 더하는 보너스다.
    public int dashMaxChargesBonus = 0;
    public float dashRechargePerSecondBonus = 0f; // 초당 충전량에 더한다 (쿨타임 4초 = 초당 0.25)

    [Header("Axe - Boomerang")]
    public int boomerangCount = 0; // "부메랑" 스킬 레벨 = 동시에 존재 가능한 부메랑 개수 (0이면 미해금 상태로 발사되지 않음)
    public float boomerangDamage = 1f;
    public float boomerangHitRadius = 0.5f; // "범위"
    public float boomerangMajorAxisRatio = 1f; // "사정거리" (CameraBoundsUtil 타원 장축 비율)
    public float boomerangCooldown = 2.5f; // "쿨타임"
    public float boomerangDamageInterval = 0.3f; // "공격 속도"가 반영되는 판정 주기
    public bool bBoomerangCritical = false;
    public bool bBoomerangOverheatBoost = false; // "과열 부메랑" 특성 - 부메랑이 과열 수치에 영향을 받는다(과열 0이어도 적용)
    public float baseBoomerangDamage { get; private set; }
    public float boomerangDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator boomerangDamageAccum;
    public float baseBoomerangHitRadius { get; private set; }
    public float boomerangRangeMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator boomerangRangeAccum;
    public float baseBoomerangMajorAxisRatio { get; private set; }
    public float boomerangDistanceMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator boomerangDistanceAccum;
    public float baseBoomerangCooldown { get; private set; }
    public float boomerangCooldownReductionAlpha = 0f;
    public float baseBoomerangDamageInterval { get; private set; }
    public float boomerangAttackSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator boomerangAttackSpeedAccum;

    [Header("Axe - Drone")]
    public int droneCount = 0; // "드론" 스킬 레벨 = 던전 입장 시 캐릭터를 따라다니는 드론 개수 (0이면 미해금 상태로 소환되지 않음)
    public float droneDamage = 5f;
    public float droneAttackRange = 3f; // "범위" - 드론이 나무를 탐지/공격하는 반경
    public float droneActiveDuration = 3f; // "지속시간" - 공격 키를 누르면 활성화되는 시간
    public float droneDamageInterval = 1f; // "공격 속도"가 반영되는 판정 주기
    public float baseDroneDamage { get; private set; }
    public float droneDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneDamageAccum;
    public float baseDroneAttackRange { get; private set; }
    public float droneRangeMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneRangeAccum;
    public float baseDroneActiveDuration { get; private set; }
    public float droneDurationMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneDurationAccum;
    public float baseDroneDamageInterval { get; private set; }
    public float droneAttackSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneAttackSpeedAccum;
    public int droneChainCount = 0; // "연쇄공격" - 드론의 공격이 주변 나무로 전이되는 횟수 (0이면 전이 없음)
    public float droneChainRange = 1.5f; // "연쇄공격 범위" - 전이 대상을 찾는 반경
    public float baseDroneChainRange { get; private set; }
    public float droneChainRangeMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator droneChainRangeAccum;
    public bool bDroneOverheatBoost = false; // "과열 드론" 특성 - 드론이 과열 수치에 영향을 받는다(과열 0이어도 적용)

    // [미사용] 지속시간제 과열의 특성 값. 과열이 0~100 스택으로 바뀌면서 효과가 없다. 특성 재구성 때 정리
    [Header("Overheat - 미사용 (옛 지속시간제 특성)")]
    public float overheatEfficiencyBonus = 0f;
    public float overheatConsumptionReductionAlpha = 0f;
    public float overheatGainBonusAlpha = 0f;
    public float heatRecoveryAmount = 0f;
    public bool bOverheatPermanent = false;

    // 과열 수치(4스테이지 불씨)는 OverheatComponent가 들고 있고, 수치 1당 효과는 여기서 조정한다.
    // 공식에 쓰는 과열 수치는 항상 내림한 정수(HUD에 보이는 값)다.
    [Header("Overheat - 과열 수치 1당 효과")]
    public float overheatMaxValue = 100f;
    public float overheatDamagePerLevel = 0.02f; // 화상을 뺀 모든 공격의 피해 마지막에 곱한다 (1 + 과열 * 값)
    public float overheatAxeAttackSpeedPerLevel = 0.005f; // 도끼 공격속도 마지막에 곱한다 (1 + 과열 * 값)

    [Header("Overheat - 구간 (화상 비율 · 드론 연쇄 공통)")]
    // 과열 수치가 이 값 이상이면 다음 구간. 기본값이면 0~33 / 34~66 / 67~99 / 100의 4구간이다.
    public int[] overheatTierThresholds = { 34, 67, 100 };

    [Header("Overheat - 화상 (과열 충격파 · 부메랑 · 드론 공통)")]
    public float[] overheatBurnRatios = { 0.02f, 0.03f, 0.04f, 0.05f }; // 구간별 틱 피해(실제 들어간 피해 대비)
    public int overheatBurnTickCount = 6; // 틱 횟수
    public float overheatBurnTickInterval = 0.5f; // 틱 간격(초)

    [Header("Overheat - 과열 충격파")]
    public float shockWaveOverheatDurationBase = 1.25f; // 유지시간 배율 = 기본값 + 과열 * 1당 값
    public float shockWaveOverheatDurationPerLevel = 0.0275f;

    [Header("Overheat - 과열 부메랑")]
    public float boomerangOverheatHitRadiusPerLevel = 0.02f; // 판정 범위 배율 = 1 + 과열 * 값
    public float boomerangOverheatCooldownMul = 0.5f; // 최종 쿨타임 배율

    [Header("Overheat - 과열 드론")]
    public int[] droneOverheatChainBonus = { 1, 2, 3, 4 }; // 구간별 연쇄 횟수 추가(합연산)
    public float droneOverheatChainRangeMul = 3f; // 연쇄 탐색 범위 배율

    [Header("Stamina Recovery")]
    public float recoveryPowerBonus = 0f; // "회복력" - 모든 피로도 회복 효과(전리품 포션, 체력의 원천, 휴식) 증가율(%)

    [Header("Rifle Settings")]
    public float rifleDamage = 10f;
    public float rifleReadyTime = 0;
    public float shotDelay = 1f;
    public int magCap = 2;
    public int ammoCap = 6;
    public float reloadDuration = 3f;
    public float gunPenetrationChance = 0f;
    public float baseRifleDamage { get; private set; }
    public float rifleDamageMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator rifleDamageAccum;
    public float baseShotDelay { get; private set; }
    public float rifleAttackSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator rifleAttackSpeedAccum;
    public float baseReloadDuration { get; private set; }
    public float reloadSpeedMultiplier { get; private set; } = 1.0f;
    private PercentAccumulator reloadSpeedAccum;

    [Header("Rifle - Ricochet")]
    public int ricochetCnt = 0;
    public float ricochetAngle = 90f;
    public float ricochetDist = 0.5f;
    public float ricochetDamage = 1f;
    private ValueAccumulator ricochetDamageAccum;

    [Header("Attack")]
    public float weakPointDamageMul = 0f;
    private ValueAccumulator weakPointDamageAccum;
    public float helloDamageMul = 0f;
    private ValueAccumulator helloDamageAccum;
    public bool bMultiAttack = false;
    public float finalAttackHealthPercent = 0f;
    public float attackRythmSpeedMul = 0f;
    public bool bWhirlWind = false;

    [Header("Critical")]
    public float criticalChance = 0f;
    private PercentAccumulator criticalChanceAccum;
    public float ciriticalDamageMul = 2f;
    private PercentAccumulator ciriticalDamageMulAccum;

    // 인터페이스 구현 프로퍼티들
    float IStatComponent.speed => speed;
    float IStatComponent.weaponChangeCoolTime => weaponChangeCoolTime;

    float IStatComponent.axeDamage => axeDamage;
    float IStatComponent.axeDurability => axeDurability;
    float IStatComponent.axeDurabilityDecAmount => axeDurabilityDecAmount;
    float IStatComponent.axeAttackCoolTime => axeAttackCoolTime;

    float IStatComponent.rifleDamage => rifleDamage;
    float IStatComponent.rifleReadyTime => rifleReadyTime;
    float IStatComponent.afterShotTime => shotDelay;
    int IStatComponent.magCap => magCap;
    int IStatComponent.ammoCap => ammoCap;
    float IStatComponent.reloadDuration => reloadDuration;

    bool IStatComponent.bCanHunting => bCanHunting;

    // ICharacterStatForNPC 구현 - NPC(럼버잭 등)가 캐릭터와 동일한 셰이크웨이브 스탯을 그대로 참조할 때 사용
    float ICharacterStatForNPC.shockWaveChance => shockWaveChance;
    float ICharacterStatForNPC.shockWaveDamage => shockWaveDamage;
    float ICharacterStatForNPC.shockWaveSpeed => shockWaveSpeed;
    float ICharacterStatForNPC.shockWaveDuration => shockWaveDuration;
    float ICharacterStatForNPC.shockWaveCreateDelay => shockWaveCreateDelay;
    bool ICharacterStatForNPC.bShockWaveMastery => bShockWaveMastery;
    bool ICharacterStatForNPC.bShockWaveCritical => bShockWaveCritical;
    bool ICharacterStatForNPC.bShockWaveEnforcement => bShockWaveEnforcement;
    bool ICharacterStatForNPC.bShockWaveOverheatBoost => bShockWaveOverheatBoost;
    float ICharacterStatForNPC.shockWaveOverheatDurationBase => shockWaveOverheatDurationBase;
    float ICharacterStatForNPC.shockWaveOverheatDurationPerLevel => shockWaveOverheatDurationPerLevel;
    int ICharacterStatForNPC.overheatBurnTickCount => overheatBurnTickCount;
    float ICharacterStatForNPC.overheatBurnTickInterval => overheatBurnTickInterval;
    float ICharacterStatForNPC.criticalChance => criticalChance;
    float ICharacterStatForNPC.ciriticalDamageMul => ciriticalDamageMul;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);
        baseMaxStamina = maxStamina;
        baseSpeed = originalSpeed;
        baseAxeDamage = axeDamage;
        baseAxeAttackCoolTime = axeAttackCoolTime;
        baseRifleDamage = rifleDamage;
        baseShotDelay = shotDelay;
        baseWeaponChangeCoolTime = weaponChangeCoolTime;
        baseReloadDuration = reloadDuration;
        baseShockWaveDamage = shockWaveDamage;
        baseShockWaveSpeed = shockWaveSpeed;
        baseBoomerangDamage = boomerangDamage;
        baseBoomerangHitRadius = boomerangHitRadius;
        baseBoomerangMajorAxisRatio = boomerangMajorAxisRatio;
        baseBoomerangCooldown = boomerangCooldown;
        baseBoomerangDamageInterval = boomerangDamageInterval;
        baseDroneDamage = droneDamage;
        baseDroneAttackRange = droneAttackRange;
        baseDroneActiveDuration = droneActiveDuration;
        baseDroneDamageInterval = droneDamageInterval;
        baseDroneChainRange = droneChainRange;
    }

    public void IncreaseAxeDamage(float _amount)
    {
        axeDamageMultiplier = axeDamageAccum.Add(axeDamageMultiplier, _amount);
        axeDamage = baseAxeDamage * axeDamageMultiplier;
    }

    /// <summary>도끼·충격파·부메랑·드론의 기준값(증가 배율을 곱하기 전 값). 로그라이크가 런 동안 바꿔 끼운다.</summary>
    public struct CombatBaseStats
    {
        public float axeDamage;
        public float axeAttackCoolTime;
        public float shockWaveDamage;
        public float boomerangDamage;
        public float boomerangDamageInterval;
        public float boomerangCooldown;
        public float boomerangMajorAxisRatio;
        public float droneDamage;
        public float droneDamageInterval;
    }

    public CombatBaseStats GetCombatBaseStats()
    {
        return new CombatBaseStats
        {
            axeDamage = baseAxeDamage,
            axeAttackCoolTime = baseAxeAttackCoolTime,
            shockWaveDamage = baseShockWaveDamage,
            boomerangDamage = baseBoomerangDamage,
            boomerangDamageInterval = baseBoomerangDamageInterval,
            boomerangCooldown = baseBoomerangCooldown,
            boomerangMajorAxisRatio = baseBoomerangMajorAxisRatio,
            droneDamage = baseDroneDamage,
            droneDamageInterval = baseDroneDamageInterval,
        };
    }

    /// <summary>
    /// [로그라이크 전용] 기준값을 바꾸고, 이미 쌓인 증가 배율을 그대로 다시 곱해 현재 값을 맞춘다.
    /// 본편은 부르지 않는다. 로그라이크는 런이 끝나면 GetCombatBaseStats로 받아 둔 원래 값으로 되돌린다.
    /// </summary>
    public void SetCombatBaseStats(in CombatBaseStats _stats)
    {
        baseAxeDamage = _stats.axeDamage;
        axeDamage = baseAxeDamage * axeDamageMultiplier;

        baseAxeAttackCoolTime = _stats.axeAttackCoolTime;
        RecalculateAxeAttackCoolTime();

        baseShockWaveDamage = _stats.shockWaveDamage;
        shockWaveDamage = baseShockWaveDamage * shockWaveDamageMultiplier;

        baseBoomerangDamage = _stats.boomerangDamage;
        boomerangDamage = baseBoomerangDamage * boomerangDamageMultiplier;

        baseBoomerangDamageInterval = _stats.boomerangDamageInterval;
        boomerangDamageInterval = baseBoomerangDamageInterval / boomerangAttackSpeedMultiplier;

        baseBoomerangCooldown = _stats.boomerangCooldown;
        boomerangCooldown = baseBoomerangCooldown * Mathf.Max(0f, 1f - (boomerangCooldownReductionAlpha / 100.0f));

        baseBoomerangMajorAxisRatio = _stats.boomerangMajorAxisRatio;
        boomerangMajorAxisRatio = baseBoomerangMajorAxisRatio * boomerangDistanceMultiplier;

        baseDroneDamage = _stats.droneDamage;
        droneDamage = baseDroneDamage * droneDamageMultiplier;

        baseDroneDamageInterval = _stats.droneDamageInterval;
        droneDamageInterval = baseDroneDamageInterval / droneAttackSpeedMultiplier;
    }

    public void CanHunting()
    {
        bCanHunting = true;
        CanHuntEvent?.Invoke();
    }

    public void IncreaseSwitchSpeed(float _amount)
    {
        switchSpeedMultiplier = switchSpeedAccum.Add(switchSpeedMultiplier, _amount);
        weaponChangeCoolTime = baseWeaponChangeCoolTime / switchSpeedMultiplier;
    }

    public void IncreaseGunDamage(float _amount)
    {
        rifleDamageMultiplier = rifleDamageAccum.Add(rifleDamageMultiplier, _amount);
        rifleDamage = baseRifleDamage * rifleDamageMultiplier;
    }

    public void StaminaDecreaseAlpha(float _amount)
    {
        staminaDecreaseAlpha += _amount;
    }

    public void StaminaIncreaseAlpha(float _amount)
    {
        staminaIncreaseAlpha += _amount;
    }

    public void IncreaseMaxStamina(float _amount)
    {
        maxStaminaBonus += _amount;
        maxStamina = baseMaxStamina + maxStaminaBonus;
    }

    public void ResetSpeed()
    {
        activeActionCount = 0;
    }

    public void IncreaseAmmoCap(int _amount)
    {
        ammoCap += _amount;
    }

    public void IncreaseMagCap(int _amount)
    {
        magCap += _amount;
    }

    public void IncreaseGunPenetration(float _amount)
    {
        gunPenetrationChance += _amount;
    }

    public void IncreaseRicochetCnt(int _amount)
    {
        ricochetCnt += _amount;
    }

    public void IncreaseSpeedWhileAction(float _amount)
    {
        speedDecreaseWhileAction = speedDecreaseWhileActionAccum.Add(speedDecreaseWhileAction, _amount);
    }

    public void IncreaseShockWaveChance(float _amount)
    {
        shockWaveChance += _amount;
    }

    public void IncreaseShockWaveDamage(float _amount)
    {
        shockWaveDamageMultiplier = shockWaveDamageAccum.Add(shockWaveDamageMultiplier, _amount);
        shockWaveDamage = baseShockWaveDamage * shockWaveDamageMultiplier;
    }

    public void IncreaseShockWaveSpeed(float _amount)
    {
        shockWaveSpeedMultiplier = shockWaveSpeedAccum.Add(shockWaveSpeedMultiplier, _amount);
        shockWaveSpeed = baseShockWaveSpeed * shockWaveSpeedMultiplier;
    }

    public void IncreaseAxeRangeMultiplier(float _amount)
    {
        axeAttackRangeMultiplier = axeAttackRangeAccum.Add(axeAttackRangeMultiplier, _amount);
    }

    public void IncreaseAxeDurability(float _amount)
    {
        axeDurability += _amount;
    }

    public void IncreaseAxeDurabilityDecIgnoreChance(float _amount)
    {
        axeDurabilityDecIgnoreChance += _amount;
    }

    public void IncreasePickupRange(float _amount)
    {
        pickupRangeMultiplier = pickupRangeAccum.Add(pickupRangeMultiplier, _amount);
    }

    public void IncreaseRicochetRange(float _amount)
    {
        ricochetAngle += ricochetAngle * (_amount / 100.0f);
        ricochetDist += ricochetDist * (_amount / 100.0f);
    }

    public void IncreaseRicochetDamage(float _amount)
    {
        ricochetDamage = ricochetDamageAccum.Add(ricochetDamage, _amount);
    }

    public void IncreaseReloadSpeed(float _amount)
    {
        reloadSpeedMultiplier = reloadSpeedAccum.Add(reloadSpeedMultiplier, _amount);
        reloadDuration = baseReloadDuration / reloadSpeedMultiplier;
    }

    public void IncreaseRifleAttackSpeed(float _amount)
    {
        rifleAttackSpeedMultiplier = rifleAttackSpeedAccum.Add(rifleAttackSpeedMultiplier, _amount);
        shotDelay = baseShotDelay / rifleAttackSpeedMultiplier;
    }

    public void IncreaseMovementSpeed(float _amount)
    {
        speedMultiplier = speedAccum.Add(speedMultiplier, _amount);
        originalSpeed = baseSpeed * speedMultiplier;
    }

    public void IncreaseAxeAttackSpeed(float _amount)
    {
        axeAttackSpeedMultiplier = axeAttackSpeedAccum.Add(axeAttackSpeedMultiplier, _amount);
        RecalculateAxeAttackCoolTime();
    }

    /// <summary>
    /// 과열 수치로 오르는 도끼 공격속도 배율. 특성 배율(axeAttackSpeedMultiplier)과 따로 들고 있다가 마지막에 곱한다.
    /// OverheatComponent만 넣는다. 과열이 0이면 1이라 본편 다른 스테이지와 로그라이크는 값이 그대로다.
    /// </summary>
    public float overheatAxeAttackSpeedMultiplier { get; private set; } = 1.0f;

    public void SetOverheatAxeAttackSpeedMultiplier(float _multiplier)
    {
        overheatAxeAttackSpeedMultiplier = _multiplier;
        RecalculateAxeAttackCoolTime();
    }

    private void RecalculateAxeAttackCoolTime()
    {
        axeAttackCoolTime = baseAxeAttackCoolTime / (axeAttackSpeedMultiplier * overheatAxeAttackSpeedMultiplier);
    }

    /// <summary>과열 수치(내림한 정수)가 속한 구간. 기본값이면 0~33 = 0, 34~66 = 1, 67~99 = 2, 100 = 3.</summary>
    public int GetOverheatTier(int _overheatLevel)
    {
        int tier = 0;
        if (overheatTierThresholds == null) return tier;

        for (int i = 0; i < overheatTierThresholds.Length; i++)
        {
            if (_overheatLevel >= overheatTierThresholds[i]) tier++;
        }
        return tier;
    }

    /// <summary>화상 틱 피해 비율(실제 들어간 피해 대비). 과열 충격파·부메랑·드론이 같이 쓴다.</summary>
    public float GetOverheatBurnRatio(int _overheatLevel)
    {
        return GetTierValue(overheatBurnRatios, GetOverheatTier(_overheatLevel));
    }

    /// <summary>과열 드론의 연쇄 횟수 추가분.</summary>
    public int GetDroneOverheatChainBonus(int _overheatLevel)
    {
        if (droneOverheatChainBonus == null || droneOverheatChainBonus.Length == 0) return 0;
        return droneOverheatChainBonus[Mathf.Clamp(GetOverheatTier(_overheatLevel), 0, droneOverheatChainBonus.Length - 1)];
    }

    private static float GetTierValue(float[] _values, int _tier)
    {
        if (_values == null || _values.Length == 0) return 0f;
        return _values[Mathf.Clamp(_tier, 0, _values.Length - 1)];
    }

    public void IncreaseWeakPointDamageMul(float _amount)
    {
        weakPointDamageMul = weakPointDamageAccum.Add(weakPointDamageMul, _amount);
    }

    public void IncreaseHelloDamage(float _amount)
    {
        helloDamageMul = helloDamageAccum.Add(helloDamageMul, _amount);
    }

    public void SetMultiAttack(bool _boolean)
    {
        bMultiAttack = _boolean;
    }

    public void SetFinalAttackHealthPercent(float _percent)
    {
        finalAttackHealthPercent = (_percent / 100.0f);
    }

    public void SetAttackRythmSpeedAmount(float _percent)
    {
        attackRythmSpeedMul = _percent;
    }

    public void ActivateWhirlWind(bool _boolean)
    {
        bWhirlWind = _boolean;
    }

    public void IncreaseCriticalChance(float _amount)
    {
        criticalChance = criticalChanceAccum.Add(criticalChance, _amount);
    }

    public void IncreaseCriticalDamage(float _amount)
    {
        ciriticalDamageMul = ciriticalDamageMulAccum.Add(ciriticalDamageMul, _amount);
    }

    public void ActivateShockWaveCritical(bool _boolean)
    {
        bShockWaveCritical = _boolean;
    }

    public void ActivateShockWaveEnforcement(bool _boolean)
    {
        bShockWaveEnforcement = _boolean;
    }

    public void ShockWaveMastery(bool _boolean)
    {
        bShockWaveMastery = _boolean;
    }

    public void ActivateOverheat(bool _boolean)
    {
        bOverheat = _boolean;
    }

    public void ActivateShockWaveOverheatBoost(bool _boolean)
    {
        bShockWaveOverheatBoost = _boolean;
    }

    public void IncreaseOverheatEfficiency(float _amount)
    {
        overheatEfficiencyBonus += _amount;
    }

    public void IncreaseOverheatConsumptionReduction(float _amount)
    {
        overheatConsumptionReductionAlpha += _amount;
    }

    public void IncreaseOverheatGainBonus(float _amount)
    {
        overheatGainBonusAlpha += _amount;
    }

    public void IncreaseHeatRecoveryAmount(float _amount)
    {
        heatRecoveryAmount += _amount;
    }

    public void ActivateOverheatPermanent(bool _boolean)
    {
        bOverheatPermanent = _boolean;
    }

    public void IncreaseRecoveryPower(float _amount)
    {
        recoveryPowerBonus += _amount;
    }

    public void IncreaseSourceOfStaminaRecoverAmount(float _amount)
    {
        sourceOfStaminaRecoverAmount += _amount;
    }

    public void IncreaseSourceOfSpeedAmount(float _amount)
    {
        sourceOfSpeedAmount = sourceOfSpeedAmountAccum.Add(sourceOfSpeedAmount, _amount);
    }

    private float sourceOfSpeedTimer = 0f;
    private const float SourceOfSpeedDuration = 3f;

    public void ActivateSourceOfSpeed()
    {
        if (sourceOfSpeedAmount <= 0) return;

        sourceOfSpeedTimer = SourceOfSpeedDuration;

        if (sourceOfSpeedCoroutine == null)
        {
            sourceOfSpeedCoroutine = StartCoroutine(SourceOfSpeedRoutine());
            HasteStartedEvent?.Invoke();
        }

        SourceOfSpeedActivatedEvent?.Invoke(SourceOfSpeedDuration);
    }

    private System.Collections.IEnumerator SourceOfSpeedRoutine()
    {
        currentSourceOfSpeedBonus = sourceOfSpeedAmount;
        IncreaseMovementSpeed(currentSourceOfSpeedBonus * 100.0f);

        while (sourceOfSpeedTimer > 0f)
        {
            sourceOfSpeedTimer -= Time.deltaTime;
            yield return null;
        }

        IncreaseMovementSpeed(-currentSourceOfSpeedBonus * 100.0f);
        currentSourceOfSpeedBonus = 0f;
        sourceOfSpeedCoroutine = null;

        if (starPathSpeedCoroutine == null) HasteEndedEvent?.Invoke(false);
    }

    public void Reset()
    {
        bool bHasteActive = sourceOfSpeedCoroutine != null || starPathSpeedCoroutine != null;
        bool bSourceOfSpeedForceEnded = false;

        if (sourceOfSpeedCoroutine != null)
        {
            StopCoroutine(sourceOfSpeedCoroutine);
            IncreaseMovementSpeed(-currentSourceOfSpeedBonus * 100.0f);
            currentSourceOfSpeedBonus = 0f;
            sourceOfSpeedCoroutine = null;
            bSourceOfSpeedForceEnded = true;
        }

        if (starPathSpeedCoroutine != null)
        {
            StopCoroutine(starPathSpeedCoroutine);
            IncreaseMovementSpeed(-currentStarPathSpeedBonus * 100.0f);
            currentStarPathSpeedBonus = 0f;
            starPathSpeedCoroutine = null;
        }

        if (bHasteActive) HasteEndedEvent?.Invoke(true);
        if (bSourceOfSpeedForceEnded) SourceOfSpeedForceEndedEvent?.Invoke();
    }

    // 별길 걸음 - 별 표식 나무 벌목 시 일정 시간 이동속도 증가 (SourceOfSpeed와 별개의 타이머로 관리)
    public float starPathSpeedBoostAmount = 0f;
    private PercentAccumulator starPathSpeedBoostAmountAccum;
    private float starPathSpeedTimer = 0f;
    private float currentStarPathSpeedBonus = 0f;
    private Coroutine starPathSpeedCoroutine;

    public void IncreaseStarPathSpeedBoost(float _amount)
    {
        starPathSpeedBoostAmount = starPathSpeedBoostAmountAccum.Add(starPathSpeedBoostAmount, _amount);
    }

    public void ActivateStarPathSpeedBoost()
    {
        if (starPathSpeedBoostAmount <= 0f) return;

        starPathSpeedTimer = 5f;

        if (starPathSpeedCoroutine == null)
        {
            starPathSpeedCoroutine = StartCoroutine(StarPathSpeedRoutine());
            HasteStartedEvent?.Invoke();
        }
    }

    private System.Collections.IEnumerator StarPathSpeedRoutine()
    {
        // 버프가 지속되는 동안 스킬 레벨업으로 amount가 바뀌어도 더한 만큼만 정확히 되돌리도록 스냅샷을 사용한다.
        currentStarPathSpeedBonus = starPathSpeedBoostAmount;
        IncreaseMovementSpeed(currentStarPathSpeedBonus * 100.0f);

        while (starPathSpeedTimer > 0f)
        {
            starPathSpeedTimer -= Time.deltaTime;
            yield return null;
        }

        IncreaseMovementSpeed(-currentStarPathSpeedBonus * 100.0f);
        currentStarPathSpeedBonus = 0f;
        starPathSpeedCoroutine = null;

        if (sourceOfSpeedCoroutine == null) HasteEndedEvent?.Invoke(false);
    }

    public void IncreaseStaminaRecoverAmount(float _amount)
    {
        staminaRecoverAmount = _amount;
    }

    public void IncreaseBoomerangCount(int _amount)
    {
        boomerangCount += _amount;
    }

    public void IncreaseDashMaxCharges(int _amount)
    {
        dashMaxChargesBonus += _amount;
    }

    public void IncreaseDashRechargePerSecond(float _amount)
    {
        dashRechargePerSecondBonus += _amount;
    }

    public void IncreaseBoomerangDamage(float _amount)
    {
        boomerangDamageMultiplier = boomerangDamageAccum.Add(boomerangDamageMultiplier, _amount);
        boomerangDamage = baseBoomerangDamage * boomerangDamageMultiplier;
    }

    public void IncreaseBoomerangRange(float _amount)
    {
        boomerangRangeMultiplier = boomerangRangeAccum.Add(boomerangRangeMultiplier, _amount);
        boomerangHitRadius = baseBoomerangHitRadius * boomerangRangeMultiplier;
    }

    public void IncreaseBoomerangDistance(float _amount)
    {
        boomerangDistanceMultiplier = boomerangDistanceAccum.Add(boomerangDistanceMultiplier, _amount);
        boomerangMajorAxisRatio = baseBoomerangMajorAxisRatio * boomerangDistanceMultiplier;
    }

    public void IncreaseBoomerangCooldownReduction(float _amount)
    {
        boomerangCooldownReductionAlpha += _amount;
        boomerangCooldown = baseBoomerangCooldown * Mathf.Max(0f, 1f - (boomerangCooldownReductionAlpha / 100.0f));
    }

    public void IncreaseBoomerangAttackSpeed(float _amount)
    {
        boomerangAttackSpeedMultiplier = boomerangAttackSpeedAccum.Add(boomerangAttackSpeedMultiplier, _amount);
        boomerangDamageInterval = baseBoomerangDamageInterval / boomerangAttackSpeedMultiplier;
    }

    public void ActivateBoomerangCritical(bool _boolean)
    {
        bBoomerangCritical = _boolean;
    }

    public void ActivateBoomerangOverheatBoost(bool _boolean)
    {
        bBoomerangOverheatBoost = _boolean;
    }

    public void IncreaseDroneCount(int _amount)
    {
        droneCount += _amount;
    }

    public void IncreaseDroneDamage(float _amount)
    {
        droneDamageMultiplier = droneDamageAccum.Add(droneDamageMultiplier, _amount);
        droneDamage = baseDroneDamage * droneDamageMultiplier;
    }

    public void IncreaseDroneRange(float _amount)
    {
        droneRangeMultiplier = droneRangeAccum.Add(droneRangeMultiplier, _amount);
        droneAttackRange = baseDroneAttackRange * droneRangeMultiplier;
    }

    public void IncreaseDroneDuration(float _amount)
    {
        droneDurationMultiplier = droneDurationAccum.Add(droneDurationMultiplier, _amount);
        droneActiveDuration = baseDroneActiveDuration * droneDurationMultiplier;
    }

    public void IncreaseDroneAttackSpeed(float _amount)
    {
        droneAttackSpeedMultiplier = droneAttackSpeedAccum.Add(droneAttackSpeedMultiplier, _amount);
        droneDamageInterval = baseDroneDamageInterval / droneAttackSpeedMultiplier;
    }

    public void IncreaseDroneChainCount(int _amount)
    {
        droneChainCount += _amount;
    }

    public void IncreaseDroneChainRange(float _amount)
    {
        droneChainRangeMultiplier = droneChainRangeAccum.Add(droneChainRangeMultiplier, _amount);
        droneChainRange = baseDroneChainRange * droneChainRangeMultiplier;
    }

    public void ActivateDroneOverheatBoost(bool _boolean)
    {
        bDroneOverheatBoost = _boolean;
    }
}
