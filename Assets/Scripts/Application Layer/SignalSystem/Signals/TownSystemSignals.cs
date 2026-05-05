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

public struct MoneyEarnedSignal
{
    public int money;
    public MoneyEarnedSignal(int _money)
    {
        money = _money;
    }
}

public struct TentInteractSignal
{
    public bool bInteract;
    public TentInteractSignal(bool _bInteract)
    {
        bInteract = _bInteract;
    }
}

public struct GoToDungeonSignal
{
    public MapType type;
    public ForestType forestType;
    public GoToDungeonSignal(MapType _type, ForestType _forestType)
    {
        type = _type;
        forestType = _forestType;
    }
}