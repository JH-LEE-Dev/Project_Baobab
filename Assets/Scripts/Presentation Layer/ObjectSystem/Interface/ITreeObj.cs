using UnityEngine;

public interface ITreeObj
{
    public IHealthComponent health { get; }
    public Transform GetTransform();

    public bool bDead { get; }
}
