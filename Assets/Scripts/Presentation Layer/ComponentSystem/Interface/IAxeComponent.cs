using System;

public interface IAxeComponent
{
    public float durability { get; }
    public event Action AxeAttackedEvent;
    public event Action DurabilityEmptyEvent;
    public event Action DurabilityRestoredEvent;
}
