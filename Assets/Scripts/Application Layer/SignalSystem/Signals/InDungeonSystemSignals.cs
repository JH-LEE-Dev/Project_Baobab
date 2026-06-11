
using UnityEngine;

public struct DungeonReadySignal
{
    public DungeonData dungeonData;
    public ForestType forestType;
    public DungeonReadySignal(DungeonData _dungeonData, ForestType _forestType)
    {
        dungeonData = _dungeonData;
        forestType = _forestType;
    }
}

public struct DecalreDungeonTypeSignal
{
    public MapType mapType;
    public ForestType forestType;
    public DecalreDungeonTypeSignal(MapType _mapType, ForestType _forestType)
    {
        mapType = _mapType;
        forestType = _forestType;
    }
}

public struct DungeonStartSignal
{
    public Vector3 characterPos;
    public DungeonStartSignal(Vector3 _characterPos)
    {
        characterPos = _characterPos;
    }
}

public struct ItemAcquiredSignal
{
    public Item item;
    public ItemAcquiredSignal(Item _item)
    {
        item = _item;
    }
}

public struct TreeGetHitSignal
{
    public TreeObj treeObj;
    public TreeGetHitSignal(TreeObj _treeObj)
    {
        treeObj = _treeObj;
    }
}

public struct GoToHomeSignal { }

public struct CarrotItemAcquiredSignal
{
    public float amount;
    public CarrotItemAcquiredSignal(float _amount)
    {
        amount = _amount;
    }
}

public struct AnimalHitSignal
{
    public IAnimalObj animal;
    public AnimalHitSignal(IAnimalObj _animal)
    {
        animal = _animal;
    }
}

public struct TreeIsDeadSignal
{
    public TreeType type;
    public TreeIsDeadSignal(TreeType _type)
    {
        type = _type;
    }
}

public struct AnimalIsDeadSignal
{
    public AnimalType type;
    public AnimalIsDeadSignal(AnimalType _type)
    {
        type = _type;
    }
}

public struct GoToHiddenMapSignal
{
    public MapType mapType;
    public ForestType forestType;
    public GoToHiddenMapSignal(MapType _mapType, ForestType _forestType)
    {
        mapType = _mapType;
        forestType = _forestType;
    }
}

public struct OffroadSpawnedSignal
{
    public OffroadVehicleObj offroadVehicleObj;
    public OffroadSpawnedSignal(OffroadVehicleObj _offroadVehicleObj)
    {
        offroadVehicleObj = _offroadVehicleObj;
    }
}

public struct OffroadInteractStateChangedSignal
{
    public bool state;
    public OffroadInteractStateChangedSignal(bool _state)
    {
        state = _state;
    }
}

public struct ShopInteractStateChangedSignal
{
    public bool state;
    public ShopInteractStateChangedSignal(bool _state)
    {
        state = _state;
    }
}

public struct LogItemProcessorActiveStateSignal
{
    public bool state;
    public LogItemProcessorActiveStateSignal(bool _state)
    {
        state = _state;
    }
}

public struct GameEndSignal { }

public struct DropAllItemSignal { }

public struct InventoryItemTransferToOffroadContainerSignal { }