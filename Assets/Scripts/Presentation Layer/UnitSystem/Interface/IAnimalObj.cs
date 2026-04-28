using UnityEngine;

public interface IAnimalObj
{
    public IHealthComponent health { get; }
    public Transform GetTransform();

    public bool bDead { get; }
}
