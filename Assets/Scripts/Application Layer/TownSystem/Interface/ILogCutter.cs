
using System;
using UnityEngine;

public interface ILogCutter
{
    public event Action<ILogItemData> CuttingStartEvent;
    public event Action CuttingDoneEvent;
    public ILogItemData logToCut { get; }
    public float timeRemaining { get; }
    public float elapsedProcessingTime { get; }
    public float totalProcessingTime { get; }
    public Transform GetTransform();
    bool bIsCutting { get; }
}
