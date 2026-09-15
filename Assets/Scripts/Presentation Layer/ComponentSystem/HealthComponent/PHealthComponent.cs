using System;
using UnityEngine;

public class PHealthComponent : PComponent, IPHealthComponent
{
    public event Action StaminaIsEmptyEvent;
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

    private bool bFirstDamage = false;
    public bool bIsFirstDamage => bFirstDamage;

    private bool bStaminaDecrease = false;

    // 튜토리얼 등에서 스태미나가 특정 비율 아래로 떨어지지 않도록 거는 최소치(0~1 비율, maxStamina 기준).
    // 0이면 바닥이 없다는 뜻(기존 동작과 동일하게 0까지 감소). StaminaReset()으로 원정이 끝나면 자동 해제된다.
    private float minStaminaRatio = 0f;

    /// <summary>
    /// 컴포넌트 초기화
    /// </summary>
    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        currentStamina = maxStamina;
        currentHealth = maxHealth;
        prevHealth = currentHealth;
        bFirstDamage = false;
    }

    /// <summary>
    /// 체력 감소 (단발성 피해 등)
    /// </summary>
    public void DecreaseHealth(float _damage)
    {
        prevHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - _damage);
        if (bFirstDamage == false)
            bFirstDamage = true;
    }

    /// <summary>
    /// 스태미나 감소 (초당 변화량 적용)
    /// </summary>
    public void DecreaseStamina()
    {
        float floor = minStaminaRatio * maxStamina;
        if (currentStamina <= floor || bStaminaDecrease == false)
            return;

        // staminaDecAmount는 초당 변화량이므로 Time.deltaTime을 곱함
        float amount = staminaDecAmount * Time.deltaTime;
        currentStamina = Mathf.Max(floor, currentStamina - amount);

        if (currentStamina <= 0)
        {
            StaminaIsEmptyEvent?.Invoke();
        }
    }

    /// <summary>
    /// 환경 위험 지형(용암 등)으로 인한 스태미나 추가 소모. 캐릭터 스탯 보정(staminaDecreaseAlpha)의
    /// 영향을 받지 않는 고정값으로, 최종 소모량에 그대로 더해진다.
    ///
    /// DecreaseStamina와 동일하게 bStaminaDecrease를 따른다. (아래 [소모 정지와 환경 피해] 참고)
    /// </summary>
    public void ApplyEnvironmentalStaminaDrain(float _drainPerSecond)
    {
        float floor = minStaminaRatio * maxStamina;
        if (currentStamina <= floor || _drainPerSecond <= 0f || bStaminaDecrease == false)
            return;

        float amount = _drainPerSecond * Time.deltaTime;
        currentStamina = Mathf.Max(floor, currentStamina - amount);

        if (currentStamina <= 0)
        {
            StaminaIsEmptyEvent?.Invoke();
        }
    }

    /// <summary>
    /// 단발성 스태미나 피해 (나무 열기 발산 등). Time.deltaTime과 무관하게 고정값을 즉시 차감한다.
    ///
    /// [소모 정지와 환경 피해]
    /// 환경 피해(용암 지속 피해 · 나무 열기)도 일반 소모와 똑같이 bStaminaDecrease를 따른다.
    /// 예전에는 이 두 경로만 플래그를 보지 않아, "던전의 위협을 전부 얼리는" 구간에서도 혼자 계속 깎았다.
    ///   · GameEnd(경고 UI) / HandleGameEnd(귀환 확정)는 NPC · 비행 아이템 · 부메랑 · 나무 성장 ·
    ///     이동 · 공격을 모두 멈추고 SetStaminaDecrease(false)까지 부른다. 그런데 PauseCharacter는
    ///     공격/커서만 끌 뿐 Character.Update()를 멈추지 않고, 나무 열기는 나무별 코루틴이라
    ///     StopGrowth()로도 멈추지 않아서 이 둘만 살아남았다.
    ///   · 그 구간은 PauseMove(true)로 이동이 잠겨 있어 플레이어가 용암에서 비켜설 수 없다.
    ///     minStaminaRatio는 기본 0이라 그대로 0까지 닿아 사망 시퀀스가 귀환 시퀀스와 겹쳤다.
    ///     (입장 쪽은 Character.Update의 bWhileReset 가드가 같은 사고를 이미 막고 있다)
    ///
    /// 차량 휴식 구역(StaminaRecoverCircle)도 같은 플래그로 소모를 멈추므로, 원 안에서는
    /// 환경 피해까지 함께 멈춘다. <b>휴식 구역은 회복만 하고 어떤 피해도 받지 않는 것이 기획 의도다.</b>
    /// </summary>
    public void DecreaseStaminaFlat(float _damage)
    {
        float floor = minStaminaRatio * maxStamina;
        if (currentStamina <= floor || _damage <= 0f || bStaminaDecrease == false)
            return;

        currentStamina = Mathf.Max(floor, currentStamina - _damage);

        if (currentStamina <= 0)
        {
            StaminaIsEmptyEvent?.Invoke();
        }
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

    /// <summary>
    /// 초당 변화량과 무관하게 최대 스태미나의 _percent(%)만큼 즉시 회복시킨다("포자 포션" 등 소비 아이템용).
    /// "회복력" 특성만큼 회복량이 증가한다.
    /// </summary>
    public void RestoreStaminaByPercent(float _percent)
    {
        float finalPercent = _percent * (1f + ctx.characterStat.recoveryPowerBonus / 100f);
        currentStamina = Mathf.Min(maxStamina, currentStamina + maxStamina * (finalPercent / 100f));
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

    public void SetMaxStamina(float _maxStamina)
    {
        float diff = _maxStamina - maxStamina;
        maxStamina = _maxStamina;

        // 최대치가 늘어난 만큼 현재치도 보정 (선택 사항이나 보통 긍정적 경험 제공)
        if (diff > 0)
        {
            currentStamina += diff;
        }

        currentStamina = Mathf.Min(currentStamina, maxStamina);
    }

    public void StaminaReset()
    {
        currentStamina = maxStamina;
        minStaminaRatio = 0f;
    }

    /// <summary>
    /// 스태미나가 maxStamina 대비 _percent(0~100)% 아래로는 떨어지지 않도록 바닥값을 건다.
    /// 0을 넘기면 바닥을 해제(자유롭게 0까지 감소)한다. StaminaReset()에서 자동으로 초기화된다.
    /// </summary>
    public void SetMinStaminaPercent(float _percent)
    {
        minStaminaRatio = Mathf.Clamp01(_percent / 100f);
    }

    /// <summary>
    /// 현재 스태미나 소모가 켜져 있는지. 남의 잠금 위에 겹쳐 잠그는 쪽이 "원래 값"을 저장해 두었다가
    /// 되돌리기 위해 읽는다. (선례: KnockBackState.bMovePausedBeforeKnockBack)
    /// </summary>
    public bool IsStaminaDecreasing => bStaminaDecrease;

    public void SetStaminaDecrease(bool _boolean)
    {
        bStaminaDecrease = _boolean;
    }

    // "체력의 원천", "휴식"(StaminaRecoverCircle)이 사용한다. "회복력" 특성만큼 회복량이 증가한다.
    public void StaminaRecover(float _amount)
    {
        float finalAmount = _amount * (1f + ctx.characterStat.recoveryPowerBonus / 100f);
        currentStamina = Mathf.Min(maxStamina, currentStamina + finalAmount);
    }
}
