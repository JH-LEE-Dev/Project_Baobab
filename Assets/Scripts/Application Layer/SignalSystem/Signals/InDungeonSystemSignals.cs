
using UnityEngine;

public struct DungeonReadySignal
{
    public DungeonData dungeonData;
    public DungeonReadySignal(DungeonData _dungeonData)
    {
        dungeonData = _dungeonData;
    }
}

public struct DecalreDungeonTypeSignal
{
    public MapType mapType;
    public DecalreDungeonTypeSignal(MapType _mapType)
    {
        mapType = _mapType;
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
