using UnityEngine;

public interface IShadowDataProvider
{
    public float currentTimePercent { get; }
    public float dayCycleSpeed { get; }
    public float minHeightScale { get; }
    public float maxHeightScale { get; }
    public Quaternion CurrentShadowRotation { get; }
    public float CurrentShadowScaleY { get; }
    public bool IsShadowActive { get; }
}
