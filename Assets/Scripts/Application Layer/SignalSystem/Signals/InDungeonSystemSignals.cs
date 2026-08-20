
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

// MainMenu → Dungeon 튜토리얼: 카메라 하강 완료 시점에 던전 BGM만 재생하라는 신호.
// (이 경로에서는 ActivateCharacterSignal이 아직 발행되지 않아 BGM 재생 지점이 없다)
public struct DungeonBGMStartSignal { }

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

/// <summary>
/// 나무가 보석 단계(황금/다이아/무지개)로 변한 순간 발행된다. 어떤 나무가 변했는지를 전달한다.
/// </summary>
public struct TreeGemTransformedSignal
{
    public TreeObj treeObj;
    public TreeGemTransformedSignal(TreeObj _treeObj)
    {
        treeObj = _treeObj;
    }
}

public struct TreeShieldRecoveringSignal
{
    public TreeObj treeObj;
    public TreeShieldRecoveringSignal(TreeObj _treeObj)
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
    public bool isPlayerKilled;
    public TreeIsDeadSignal(TreeType _type, bool _isPlayerKilled = true)
    {
        type = _type;
        isPlayerKilled = _isPlayerKilled;
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

public struct RepairBoxInteractStateChangedSignal
{
    public bool state;
    public RepairBoxInteractStateChangedSignal(bool _state)
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

public struct LostAndFoundBoxAcquiredSignal { }

public struct InventoryItemTransferToOffroadContainerSignal { }

public struct DeclareDungeonStateSignal
{
    public DungeonState dungeonState;
    public DeclareDungeonStateSignal(DungeonState _dungeonState)
    {
        dungeonState = _dungeonState;
    }
}

public struct CharacterRideStartSignal { }