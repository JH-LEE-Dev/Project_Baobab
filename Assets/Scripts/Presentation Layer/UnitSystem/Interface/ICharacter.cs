using UnityEngine;

public interface ICharacter
{
    public IPHealthComponent pHealthComponent { get; }
    public Transform GetTransform();
    public IStatComponent statComponent { get; }
    public IArmComponent armComponent { get; }
}
