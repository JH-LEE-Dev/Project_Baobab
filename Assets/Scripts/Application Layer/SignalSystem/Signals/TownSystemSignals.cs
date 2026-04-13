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
    public DungeonType type;
    public GoToDungeonSignal(DungeonType _type)
    {
        type = _type;
    }
}