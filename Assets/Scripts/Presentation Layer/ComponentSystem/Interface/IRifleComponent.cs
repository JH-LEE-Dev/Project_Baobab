
using System;

public interface IRifleComponent
{
    public float durability { get; }
    public event Action AttackCoolTimeStartEvent;
    public event Action ReloadStartEvent;
}
