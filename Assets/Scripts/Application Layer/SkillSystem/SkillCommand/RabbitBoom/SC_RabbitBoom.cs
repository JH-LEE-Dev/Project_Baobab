using UnityEngine;

[CreateAssetMenu(fileName = "Rabbit Boom", menuName = "Game/Skill Command/Rabbit Boom")]
public class SC_RabbitBoom : SkillCommand
{
    public override void Execute(ICommandHandleSystem _system)
    {
        _system.densityCH.IncreaseRabbitDensity(amount);
    }

    public override void Undo(ICommandHandleSystem _system)
    {
        _system.densityCH.IncreaseRabbitDensity(-amount);
    }
}
