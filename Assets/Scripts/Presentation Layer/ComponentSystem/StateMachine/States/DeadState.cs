using UnityEngine;

public class DeadState : CharacterState
{
    public readonly int bDeadHash = Animator.StringToHash("bDead");

    public override void Enter()
    {
        character.anim.SetBool(bDeadHash,true);
        character.rb.linearVelocity = Vector2.zero;
    }

    public override void Exit()
    {
        character.anim.SetBool(bDeadHash,false);
    }

    public override void Update()
    {

    }

    protected override void SubscribeEvents()
    {

    }

    protected override void UnSubscribeEvents()
    {

    }
}
