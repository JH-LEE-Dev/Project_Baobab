using System;
using UnityEngine;

public class AxeComponent : WeaponComponent, IAxeComponent
{
    public event Action AxeAttackedEvent;
    public event Action<bool> DeclareCanSwapEvent;
    public event Action<bool> DeclareAttackStateEvent;
    public event Action AttackEvent;
    public event Action DurabilityEmptyEvent;

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

        // 공격 성공 시 콤보 누적 및 타이머 초기화 (최대 10중첩)
        attackComboStack = Mathf.Min(attackComboStack + 1, 10);
        comboResetTimer = COMBO_RESET_TIME;
    }

    public override void ResetDurability()
    {
        durability = ctx.characterStat.axeDurability;
        UpdateSpriteByDurability();
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
