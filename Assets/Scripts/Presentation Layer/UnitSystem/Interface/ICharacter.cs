using UnityEngine;

public interface ICharacter
{
    public IPHealthComponent pHealthComponent { get; }
    public Transform GetTransform();
}
