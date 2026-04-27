
using System;

public interface IRifleComponent
{
    public float durability { get; }
    public int mag { get; }
    public int ammo { get; }
    public event Action AttackCoolTimeStartEvent;
    public event Action ReloadStartEvent;
    public event Action RifleFiredEvent;
    public event Action ReloadFinishedEvent;
}
