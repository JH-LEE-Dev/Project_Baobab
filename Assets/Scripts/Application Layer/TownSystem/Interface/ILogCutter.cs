
using System;
using UnityEngine;

public interface ILogCutter
{
    public event Action<ILogItemData> CuttingStartEvent;
    public ILogItemData logToCut { get; }
    public float timeRemaining { get; }
    public Transform GetTransform();
}
