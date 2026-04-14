using System;
using UnityEngine;

public class RifleComponent : WeaponComponent, IRifleComponent
{
    public event Action RifleReadyEvent;
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private readonly int bReadyHash = Animator.StringToHash("bReady");

    private bool bReady = false;
    private bool bFired = false;

    float IRifleComponent.durability => durability;

    public override void SetFacingDir(Transform _attackTransform)
    {
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
            // (Left 판정 범위인 115~245도 구간도 포함하며, 이미 부모에서 Flip 처리가 되므로 0을 사용)
            dirIndex = 0;
        }

        anim.SetFloat(facingDirHash, dirIndex);

        // bReady 상태에 따라 뒤쪽(-1)으로 정렬할 각도 범위 결정
        bool isBehind;
        if (bReady)
        {
            // 준비 상태일 때는 좁은 범위 (좌상, 우상, 상)
            isBehind = (angle >= 22.5f && angle <= 157.5f);
        }
        else
        {
            // 비준비 상태일 때는 확장된 위쪽 반원 범위 (0~180도에서 양옆으로 22.5도씩 확장)
            // 337.5도 ~ 360도 또는 0도 ~ 202.5도
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
        bFired = true;

        //발사 애니메이션이 끝나면 실행할 코드
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
