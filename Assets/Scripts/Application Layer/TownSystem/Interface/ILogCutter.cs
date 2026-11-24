
using System;
using UnityEngine;

public interface ILogCutter
{
    public event Action<ILogItemData> CuttingStartEvent;
    public event Action CuttingDoneEvent;
    public ILogItemData logToCut { get; }
    // 레일에 올라와 다음에 들어올 원목(없으면 null). 부를 때마다 다시 채우는 객체라 그 자리에서만 읽는다.
    public ILogItemData pendingLog { get; }
    public float timeRemaining { get; }
    public float elapsedProcessingTime { get; }
    public float totalProcessingTime { get; }
    public Transform GetTransform();
    bool bIsCutting { get; }
}
