using UnityEngine;

public struct TownStartedSignal
{
    public Transform characterPos;
    public TownStartedSignal(Transform _characterPos)
    {
        characterPos = _characterPos;
    }
}

public struct ContainerUpdatedSignal { }