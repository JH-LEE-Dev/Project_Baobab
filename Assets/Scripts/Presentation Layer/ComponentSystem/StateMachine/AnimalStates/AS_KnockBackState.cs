using Unity.VisualScripting;
using UnityEngine;

public class AS_KnockBackState : AnimalState
{
    private Vector2 knockBackDir;
    private float knockBackForce;
    private float timer;
    private const float knockBackDuration = 0.2f;
    private const float idleWaitDuration = 1.5f;
    private Animator statusEffectAnimator;
    private SpriteRenderer statusEffectSR;

    public void SetKnockBack(Vector2 _dir, float _force)
    {
        knockBackDir = _dir;
        knockBackForce = _force;
    }

    public override void Enter()
    {
        bActivated = true;
        timer = knockBackDuration + idleWaitDuration;
        animal.rb.linearVelocity = knockBackDir * knockBackForce;
        animal.anim.SetBool(animal.isMovingHash, false);

        if (statusEffectAnimator == null)
            animal.statusEffectObject.GetComponent<Animator>();
        if (statusEffectSR == null)
            animal.statusEffectObject.GetComponent<SpriteRenderer>();

        if (animal.statusEffectObject != null)
            animal.statusEffectObject.SetActive(true);
    }

    public override void Exit()
    {
        bActivated = false;

        if (animal.statusEffectObject != null)
            animal.statusEffectObject.SetActive(false);
    }

    public override void Update()
    {
        if (!bActivated) return;

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            stateMachine.ChangeState<AS_IdleState>();
        }
    }

    public override void FixedUpdate()
    {
        if (!bActivated) return;

        // 지형 감속도를 반영하여 속도를 0으로 서서히 줄임
        animal.rb.linearVelocity = Vector2.MoveTowards(
            animal.rb.linearVelocity,
            Vector2.zero,
            animal.currentGroundData.deceleration * Time.fixedDeltaTime
        );
    }

    protected override void SubscribeEvents() { }
    protected override void UnSubscribeEvents() { }
}
