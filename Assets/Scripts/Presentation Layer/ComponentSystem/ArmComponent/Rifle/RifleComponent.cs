using UnityEngine;
using System.Collections.Generic;
using System;

public class RifleComponent : WeaponComponent, IRifleComponent
{
    public event Action<bool> DeclareAttackStateEvent;
    public event Action AttackCoolTimeStartEvent;
    public event Action ReloadStartEvent;

    //외부 의존성
    [SerializeField] private Transform muzzlePoint;

    private BulletObjManager bulletObjManager;

    // 내부 의존성
    private RifleAnimation rifleAnimation;
    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private readonly int bReadyHash = Animator.StringToHash("bReady");

    private bool bReady = false;
    private bool bFired = false;
    private bool bInCoolDown = false;
    private bool bLeftButtonClicked = false;
    private bool bReload = false;

    float IRifleComponent.durability => durability;
    int IRifleComponent.mag => mag;
    int IRifleComponent.ammo => ammo;

    [SerializeField] private CircleCollider2D mouseCol;
    [SerializeField] private LayerMask targetLayer; // 일반 타겟 레이어
    [SerializeField] private LayerMask aimCorrectionLayer; // 조준 보정 전용 레이어

    private bool bAimCorrection = false;
    private Transform attackTransform;
    private int mag;
    private int ammo;

    // 조준 보정을 위한 변수 (커스텀 시스템 활용)
    private List<IStaticCollidable> correctionResults = new List<IStaticCollidable>(16);

    private float originalSpeed;
    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);

        // 내부 컴포넌트 참조 구성
        rifleAnimation = GetComponent<RifleAnimation>();
        bulletObjManager = GetComponentInChildren<BulletObjManager>();
        bulletObjManager.Initialize(ctx);

        mag = ctx.characterStat.magCap;
        ammo = ctx.characterStat.ammoCap;

        // 물리 엔진용 콜라이더 비활성화
        if (mouseCol != null) mouseCol.enabled = false;
    }

    public override void SetFacingDir(Transform _attackTransform)
    {
        attackTransform = _attackTransform;
        mouseTransform = _attackTransform.position; // 마우스 좌표 업데이트

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
        if (bCanAction == false || ctx.bWhileChangingWeapon == true || bReload == true)
            return;

        bLeftButtonClicked = true;

        if (bInCoolDown == false)
            Fire();
    }

    public override void LeftButtonReleased()
    {
        bLeftButtonClicked = false;
    }

    private void EnterReady(bool _useDelay)
    {
        bReady = true;
        anim.SetBool(bReadyHash, bReady);

        if (_useDelay)
        {
            StopCoroutine(nameof(RifleReadyCoolDownRoutine));
            StartCoroutine(nameof(RifleReadyCoolDownRoutine));
        }
        else
        {
            bInCoolDown = false;
        }
    }

    private System.Collections.IEnumerator RifleReadyCoolDownRoutine()
    {
        bInCoolDown = true;
        yield return new WaitForSeconds(ctx.characterStat.rifleReadyTime);
        bInCoolDown = false;

        if (bLeftButtonClicked && bFired == false)
        {
            Fire();
        }
    }

    private void Fire()
    {
        if (null == rifleAnimation || mag == 0 || bReload == true || bFired == true) return;

        // 1. 총알 생성 및 발사
        if (bulletObjManager != null && muzzlePoint != null)
        {
            StartCoroutine(nameof(FireAfterDelayRoutine));

            originalSpeed = ctx.characterStat.speed;
            ctx.characterStat.speed = ctx.characterStat.speed * ctx.characterStat.speedDecreaseWhileFire;

            Quaternion fireRotation;

            if (bAimCorrection && CollisionSystem.Instance != null)
            {
                // 조준 보정 로직: mouseTransform 주변의 Collidable 탐색
                Vector2 searchPos = mouseTransform;
                float radius = mouseCol != null ? mouseCol.radius : 1f;

                CollisionSystem.Instance.GetCollidablesInRadius(searchPos, radius, targetLayer, correctionResults);

                if (correctionResults.Count > 0)
                {
                    Vector2 closestTargetPos = Vector2.zero;
                    float minDistanceSqr = float.MaxValue;
                    bool targetFound = false;

                    for (int i = 0; i < correctionResults.Count; i++)
                    {
                        var target = correctionResults[i];
                        float distSqr = (searchPos - target.Position).sqrMagnitude;
                        if (distSqr < minDistanceSqr)
                        {
                            minDistanceSqr = distSqr;
                            closestTargetPos = target.Position;
                            targetFound = true;
                        }
                    }

                    if (targetFound)
                    {
                        // 가장 가까운 타겟을 향한 방향 계산
                        Vector2 targetDir = (closestTargetPos - (Vector2)muzzlePoint.position).normalized;
                        float angle = Mathf.Atan2(targetDir.y, targetDir.x) * Mathf.Rad2Deg;
                        fireRotation = Quaternion.Euler(0, 0, angle);
                    }
                    else
                    {
                        fireRotation = muzzlePoint.rotation * Quaternion.Euler(0, 0, -90f);
                    }
                }
                else
                {
                    fireRotation = muzzlePoint.rotation * Quaternion.Euler(0, 0, -90f);
                }
            }
            else
            {
                // 기본 발사 (보정 없음)
                fireRotation = muzzlePoint.rotation * Quaternion.Euler(0, 0, -90f);
            }

            bulletObjManager.GetBullet(muzzlePoint.position, fireRotation);
        }

        // 2. 후속 처리
        DecreaseMagAmmo();
        bFired = true;

        OnFireStart();
    }

    private void OnFireStart()
    {
        DeclareAttackStateEvent?.Invoke(true);

        rifleAnimation.PlayRecoil(OnFireFinish);
    }

    private void OnFireFinish()
    {
        AttackCoolTimeStartEvent?.Invoke();
    }

    private System.Collections.IEnumerator FireAfterDelayRoutine()
    {
        yield return new WaitForSeconds(ctx.characterStat.shotDelay);

        DeclareAttackStateEvent?.Invoke(false);

        bReady = false;
        bFired = false;
        bInCoolDown = false;
        anim.SetBool(bReadyHash, bReady);

        ctx.characterStat.speed = originalSpeed;

        EnterReady(false);
        // 버튼을 계속 누르고 있다면 딜레이 없이 즉시 재조준
        if (bLeftButtonClicked)
        {
            Fire();
        }
    }

    public override void SetEnable(bool _boolean)
    {
        base.SetEnable(_boolean);

        if (_boolean == false)
        {
            StopCoroutine(nameof(ReloadRoutine));
            bReload = false;
        }

        bReady = false;
        bFired = false;
        bInCoolDown = false;
        bLeftButtonClicked = false;
        anim.SetBool(bReadyHash, bReady);
    }

    public void CancelReady()
    {
        if (bFired == true || bCanAction == false)
            return;

        bReady = false;
        bInCoolDown = false;

        anim.SetBool(bReadyHash, bReady);
    }

    private void DecreaseMagAmmo()
    {
        mag -= 1;
    }

    public void Reload()
    {
        if (bCanAction == false || bFired == true || bReload == true || ammo == 0 || mag == ctx.characterStat.magCap)
            return;

        if (bReady)
        {
            CancelReady();
        }

        ReloadStartEvent?.Invoke();
        StartCoroutine(nameof(ReloadRoutine));
    }

    private System.Collections.IEnumerator ReloadRoutine()
    {
        bReload = true;

        yield return new WaitForSeconds(ctx.characterStat.reloadDuration);

        int amount = 0;
        if (ctx.characterStat.magCap < ammo)
        {
            ammo -= ctx.characterStat.magCap;
            amount = ctx.characterStat.magCap;
        }
        else
        {
            amount = ammo;
            ammo = 0;
        }

        mag = amount;
        bReload = false;
    }

    public void ResetAmmo()
    {
        ammo = ctx.characterStat.ammoCap;
        mag = ctx.characterStat.magCap;
    }

    public void ActivateAimCorrection()
    {
        bAimCorrection = true;
    }

    public void DeActivateAimCorrection()
    {
        bAimCorrection = false;
    }

    private void OnDrawGizmos()
    {
        if (bAimCorrection == false || mouseCol == null) return;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(mouseTransform, mouseCol.radius);
    }
}
