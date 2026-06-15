using UnityEngine;

public class DeadState : CharacterState
{
    public override void Enter()
    {
        character.rb.linearVelocity = Vector2.zero;
        ctx.moveInput = Vector2.zero;
    }

    public override void Exit()
    {
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
