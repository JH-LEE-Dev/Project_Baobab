using System;
using UnityEngine;

public class AxeComponent : WeaponComponent, IAxeComponent
{
    public event Action AxeAttackedEvent;
    public event Action<bool> DeclareCanSwapEvent;
    public event Action<bool> DeclareAttackStateEvent;
    public event Action AttackEvent;

    // 내부 의존성
    private AxeAnimation axeAnimation;
    private bool bAttacked = false;
    private bool bLeftButtonClicked = false;
    private readonly int facingDirHash = Animator.StringToHash("facingDir");

    float IAxeComponent.durability => durability;

    private float originalSpeed;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        // 내부 컴포넌트 참조 구성
        axeAnimation = GetComponent<AxeAnimation>();

        durability = ctx.characterStat.axeDurability;
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

        int dirIndex = Mathf.RoundToInt(angle / 45f) % 8;

        if (bAttacked == false)
            anim.SetFloat(facingDirHash, dirIndex);

        // 정렬 레이어 처리
        spriteRenderer.sortingOrder = (angle > 0 && angle < 180) ? -1 : 1;
    }

    public override void LeftButtonClicked()
    {
        if (bAttacked || null == axeAnimation || bCanAction == false || durability == 0f || ctx.bWhileChangingWeapon == true) return;

        bLeftButtonClicked = true;

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

        originalSpeed = ctx.characterStat.originalSpeed;
        ctx.characterStat.speed = originalSpeed * ctx.characterStat.speedDecreaseWhileAction;

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
        yield return new WaitForSeconds(ctx.characterStat.axeAttackCoolTime);

        bAttacked = false;
        ctx.characterStat.speed = originalSpeed;
        DeclareAttackStateEvent?.Invoke(false);

        if (bLeftButtonClicked)
        {
            OnAttackStart();
        }
    }

    public override void DecreaseDurability()
    {
        if (UnityEngine.Random.value >= ctx.characterStat.axeDurabilityDecIgnoreChance)
            durability -= ctx.characterStat.axeDurabilityDecAmount;

        if (durability < 0f)
            durability = 0f;

        AxeAttackedEvent?.Invoke();
    }

    public override void ResetDurability()
    {
        durability = ctx.characterStat.axeDurability;
    }

    public void SetbAttack(bool _boolean)
    {
        bAttacked = _boolean;

        if (!_boolean)
        {
            StopCoroutine(nameof(AttackCoolDownRoutine));
            ctx.characterStat.speed = originalSpeed;
        }
    }

    public void Refresh()
    {
        
    }
}
