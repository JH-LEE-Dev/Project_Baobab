using System;
using UnityEngine;

public class AxeComponent : WeaponComponent, IAxeComponent
{
    public event Action AxeAttackedEvent;
    public event Action<bool> DeclareCanSwapEvent;
    public event Action<bool> DeclareAttackStateEvent;
    public event Action AttackEvent;
    public event Action DurabilityEmptyEvent;
    public event Action DurabilityRestoredEvent;

    // 외부 의존성
    [SerializeField] private Sprite halfDurabilityAxe;
    [SerializeField] private Sprite zeroDurabilityAxe;

    // 내부 의존성
    private AxeAnimation axeAnimation;
    private bool bAttacked = false;
    private bool bLeftButtonClicked = false;
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private bool bIsSpeedReduced = false;
    private int sortingOrder = 0;
    private Sprite originalSprite;
    private Sprite targetSprite;

    // 공격 리듬 콤보
    private int attackComboStack = 0;
    private float comboResetTimer = 0f;
    private const float COMBO_RESET_TIME = 3f;

    float IAxeComponent.durability => durability;

    private bool bCanAttack = false;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        // 내부 컴포넌트 참조 구성
        axeAnimation = GetComponent<AxeAnimation>();

        durability = ctx.characterStat.axeDurability;

        if (null != spriteRenderer)
        {
            originalSprite = spriteRenderer.sprite;
        }

        UpdateSpriteByDurability();
    }

    public override void SetFacingDir(Transform _attackTransform)
    {
        // Arm 위치에서 attackTransform까지의 방향 벡터 계산
        Vector2 direction = (_attackTransform.position - transform.parent.parent.position);

        if (direction.sqrMagnitude < 0.01f)
            return;

        // 8방향 인덱스 계산
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        //int dirIndex = Mathf.RoundToInt(angle / 45f) % 8;

        //if (bAttacked == false)
        //anim.SetFloat(facingDirHash, dirIndex);

        // 정렬 레이어 처리
        sortingOrder = (angle > 0 && angle < 180) ? -1 : 1;
    }

    public override void LeftButtonClicked()
    {
        if (ctx.inputManager.IsCursorHoveredOnUI() || bCanAttack == false) return;

        bLeftButtonClicked = true;

        if (bAttacked || null == axeAnimation || bCanAction == false || durability == 0f || ctx.bWhileChangingWeapon == true) return;

        OnAttackStart();
    }

    public override void LeftButtonReleased()
    {
        bLeftButtonClicked = false;
    }

    private void OnAttackStart()
    {
        bAttacked = true;
        Sound.Play(SoundID.Swing, transform.position);
        axeAnimation.PlaySwing(OnAttackImpact);

        if (!bIsSpeedReduced)
        {
            bIsSpeedReduced = true;
            ctx.characterStat.AddActionState();
        }

        DeclareCanSwapEvent?.Invoke(false);
        DeclareAttackStateEvent?.Invoke(true);
    }

    private void OnAttackImpact()
    {
        // 스윙 트윈(DOTween)은 GameObject가 꺼져도 계속 돌기 때문에, 차량 탑승/씬 전환 연출로
        // 캐릭터가 SetActive(false) 된 뒤에 이 콜백이 도착할 수 있다. 그 상태에서는 코루틴을 시작할 수
        // 없어(비활성 오브젝트) bAttacked가 true로 굳어버리고, 이후 좌클릭 공격이 영구히 막힌다.
        // 이미 캐릭터가 꺼진 뒤이므로 공격 판정도 내지 않고 상태만 즉시 정리한다.
        if (false == isActiveAndEnabled)
        {
            ResetAttackState();
            return;
        }

        AttackEvent?.Invoke();
        axeAnimation.PlayReturn(OnAttackFinish);
        StartCoroutine(nameof(AttackCoolDownRoutine));
    }

    private void OnAttackFinish()
    {
        DeclareCanSwapEvent?.Invoke(true);
    }

    private System.Collections.IEnumerator AttackCoolDownRoutine()
    {
        float currentCoolTime = GetEffectiveAxeAttackCoolTime();
        yield return new WaitForSeconds(currentCoolTime);

        bAttacked = false;

        if (bIsSpeedReduced)
        {
            bIsSpeedReduced = false;
            ctx.characterStat.RemoveActionState();
        }

        DeclareAttackStateEvent?.Invoke(false);

        if (bLeftButtonClicked && durability > 0f && bCanAttack == true)
        {
            OnAttackStart();
        }
    }

    public override void DecreaseDurability()
    {
        DecreaseDurabilityInternal(true);
    }

    // ShockWaveMastery로 허공에 충격파만 나갔을 때 사용 - 내구도는 나무를 벨 때와 동일하게 깎이지만,
    // 실제로 맞춘 대상이 없으므로 공격 리듬 콤보는 쌓이지 않아야 한다.
    public void DecreaseDurabilityWithoutCombo()
    {
        DecreaseDurabilityInternal(false);
    }

    private void DecreaseDurabilityInternal(bool _bIncrementCombo)
    {
        if (UnityEngine.Random.Range(0f, 100f) >= ctx.characterStat.axeDurabilityDecIgnoreChance)
            durability -= ctx.characterStat.axeDurabilityDecAmount;

        if (durability < 0f)
        {
            durability = 0f;
        }

        UpdateSpriteByDurability();

        if (durability == 0f)
            DurabilityEmptyEvent?.Invoke();

        AxeAttackedEvent?.Invoke();

        if (_bIncrementCombo)
        {
            // 공격 성공 시 콤보 누적 및 타이머 초기화 (최대 10중첩)
            attackComboStack = Mathf.Min(attackComboStack + 1, 10);
            comboResetTimer = COMBO_RESET_TIME;
        }
    }

    public override void ResetDurability()
    {
        durability = ctx.characterStat.axeDurability;
        UpdateSpriteByDurability();

        if (0f < durability)
        {
            DurabilityRestoredEvent?.Invoke();
        }
    }

    public void RepairDurability(float percentage)
    {
        float healAmount = ctx.characterStat.axeDurability * percentage;
        durability += healAmount;

        if (durability > ctx.characterStat.axeDurability)
        {
            durability = ctx.characterStat.axeDurability;
        }
        AxeAttackedEvent?.Invoke();

        UpdateSpriteByDurability();

        if (0f < durability)
        {
            DurabilityRestoredEvent?.Invoke();
        }
    }

    private void UpdateSpriteByDurability()
    {
        float maxDurability = ctx.characterStat.axeDurability;
        if (durability <= 0f)
        {
            targetSprite = zeroDurabilityAxe != null ? zeroDurabilityAxe : originalSprite;
        }
        else if (durability <= maxDurability * 0.5f)
        {
            targetSprite = halfDurabilityAxe != null ? halfDurabilityAxe : originalSprite;
        }
        else
        {
            targetSprite = originalSprite;
        }
    }

    public void SetbAttack(bool _boolean)
    {
        bAttacked = _boolean;

        if (!_boolean)
        {
            StopCoroutine(nameof(AttackCoolDownRoutine));
            if (bIsSpeedReduced)
            {
                bIsSpeedReduced = false;
                ctx.characterStat.RemoveActionState();
            }
        }
    }

    public override void SetEnable(bool _boolean)
    {
        base.SetEnable(_boolean);

        // 도끼가 꺼지는 시점(무기 리셋/사망/마을↔던전 전환)에 진행 중이던 공격 상태를 반드시 정리한다.
        // 유니티는 GameObject가 SetActive(false) 되면 그 위의 코루틴을 영구 중단하고 재활성화해도
        // 되살리지 않기 때문에, 차량 탑승 연출(TownProductionManager/InDungeonProductionManager의
        // CharacterRideRoutine) 중에 AttackCoolDownRoutine이 죽으면 bAttacked가 true로 남아
        // 다음 던전에서 좌클릭 공격이 영구히 막혀버린다. 캐릭터는 씬을 넘어 재사용되므로
        // (GameInstaller에서 1회만 생성) 스스로 풀리지 않는다.
        if (false == _boolean)
        {
            ResetAttackState();
        }
    }

    /// <summary>
    /// 스윙 트윈/쿨다운 코루틴을 정리하고 공격 관련 상태를 초기값으로 되돌립니다.
    /// 공격 중 상태를 전제로 켜 둔 이동속도 감소와 스왑/회전 잠금도 함께 해제합니다.
    /// </summary>
    private void ResetAttackState()
    {
        StopCoroutine(nameof(AttackCoolDownRoutine));

        if (null != axeAnimation)
        {
            axeAnimation.ResetPose();
        }

        bAttacked = false;
        bLeftButtonClicked = false;
        attackComboStack = 0;
        comboResetTimer = 0f;

        if (bIsSpeedReduced)
        {
            bIsSpeedReduced = false;

            if (null != ctx && null != ctx.characterStat)
            {
                ctx.characterStat.RemoveActionState();
            }
        }

        // OnAttackStart에서 false/true로 걸어둔 잠금을 되돌린다. 스윙이 중간에 끊기면
        // OnAttackFinish(트윈 콜백)와 AttackCoolDownRoutine이 실행되지 않아 이 둘이 잠긴 채 남는다.
        DeclareCanSwapEvent?.Invoke(true);
        DeclareAttackStateEvent?.Invoke(false);
    }

    public void Refresh()
    {

    }

    public void SortingOrder()
    {
        spriteRenderer.sortingOrder += sortingOrder;
    }

    private float GetEffectiveAxeAttackCoolTime()
    {
        if (ctx == null || ctx.characterStat == null) return 1.2f;

        float baseCoolTime = ctx.characterStat.axeAttackCoolTime; // 오염되지 않은 순수 쿨타임
        float decreaseRatio = (ctx.characterStat.attackRythmSpeedMul / 100f) * attackComboStack;

        // 쿨타임이 음수가 되지 않도록 방어 (최소 0.05초)
        return Mathf.Max(0.05f, baseCoolTime * (1f - decreaseRatio));
    }

    private void Update()
    {
        if (attackComboStack > 0)
        {
            comboResetTimer -= Time.deltaTime;
            if (comboResetTimer <= 0f)
            {
                attackComboStack = 0;
            }
        }
    }

    private void LateUpdate()
    {
        if (null != spriteRenderer && null != targetSprite)
        {
            spriteRenderer.sprite = targetSprite;
        }
    }

    public void SetbCanAttack(bool _boolean)
    {
        bCanAttack = _boolean;
    }

    public bool IsDurabilityZero()
    {
        return durability == 0f;
    }
}
