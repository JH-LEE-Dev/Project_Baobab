using System;
using UnityEngine;

public class RifleComponent : WeaponComponent, IRifleComponent
{
    public event Action RifleReadyEvent;
    // 내부 의존성
    private RifleAnimation rifleAnimation;
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private readonly int bReadyHash = Animator.StringToHash("bReady");

    private bool bReady = false;
    private bool bFired = false;

    float IRifleComponent.durability => durability;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);
        
        // 내부 컴포넌트 참조 구성
        rifleAnimation = GetComponent<RifleAnimation>();
    }

    public override void SetFacingDir(Transform _attackTransform)
    {
        // 타겟 정보 전달 (Animation에서 관리)
        if (null != rifleAnimation)
        {
            rifleAnimation.SetTarget(_attackTransform);
        }

        // 타겟 방향 벡터 계산
        Vector2 direction = (_attackTransform.position - transform.parent.parent.position);

        if (direction.sqrMagnitude < 0.01f) return;

        // 4방향 판정 (0: 우/좌, 1: 상, 3: 하)
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;

        int dirIndex;
        if (angle >= 65f && angle <= 115f) // 상: 90도 기준 +-25도 (총 50도)
        {
            dirIndex = 1;
        }
        else if (angle >= 245f && angle <= 295f) // 하: 270도 기준 +-25도 (총 50도)
        {
            dirIndex = 3;
        }
        else
        {
            // 그 외 범위(좌/우)는 0으로 설정
            dirIndex = 0;
        }

        anim.SetFloat(facingDirHash, dirIndex);

        // bReady 상태에 따라 뒤쪽(-1)으로 정렬할 각도 범위 결정
        bool isBehind;
        if (bReady)
        {
            isBehind = (angle >= 22.5f && angle <= 157.5f);
        }
        else
        {
            isBehind = (angle >= 337.5f || angle <= 202.5f);
        }

        spriteRenderer.sortingOrder = isBehind ? -1 : 1;
    }

    public override void LeftButtonClicked()
    {
        if (bCanAction == false)
            return;

        if (bReady == true)
        {
            if (bFired == false)
            {
                Fire();
            }
        }
        else
        {
            bReady = true;
            RifleReadyEvent?.Invoke();
            anim.SetBool(bReadyHash, bReady);
        }
    }

    private void Fire()
    {
        if (null == rifleAnimation) return;

        bFired = true;

        OnFireStart();
    }

    private void OnFireStart()
    {
        Debug.Log("Rifle: 발사 시작");

        rifleAnimation.PlayRecoil(OnFireFinish);
    }

    private void OnFireFinish()
    {
        Debug.Log("Rifle: 발사 동작 완료");

        bReady = false;
        bFired = false;
        anim.SetBool(bReadyHash, bReady);
    }

    public override void SetEnable(bool _boolean)
    {
        base.SetEnable(_boolean);

        bReady = false;
        bFired = false;
        anim.SetBool(bReadyHash, bReady);
    }
}
