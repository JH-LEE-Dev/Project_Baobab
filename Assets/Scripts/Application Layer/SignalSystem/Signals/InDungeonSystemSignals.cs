
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

// 플레이어가 슬롯 하나의 이관을 "시작"할 때 발행된다(아이템이 실제로 빠져나가기 전).
// 적재 수량 판정에는 쓸 수 없으므로 아래 ItemStoredInOffroadContainerSignal을 사용할 것.
public struct InventoryItemTransferToOffroadContainerSignal { }

// 플레이어가 넣은 아이템이 날아가는 연출을 끝내고 OffroadContainer에 "실제로 적재된" 시점에
// 아이템 하나마다 발행된다. 인벤토리에서 빠졌다는 사실만 알리는 ItemRemovedFromInventorySignal과
// 달리(그 신호는 버리기/유실/빈 슬롯 정리와 구분되지 않고, 슬롯이 비워질 때 한 번 더 울린다)
// 이 신호만이 컨테이너에 실제로 들어간 수량을 정확히 나타낸다.
// 럼버잭 NPC 등 플레이어가 아닌 주체의 납품은 포함하지 않는다.
public struct ItemStoredInOffroadContainerSignal
{
    public ItemType itemType;
    public int count;

    public ItemStoredInOffroadContainerSignal(ItemType _itemType, int _count)
    {
        itemType = _itemType;
        count = _count;
    }
}

public struct DeclareDungeonStateSignal
{
    public DungeonState dungeonState;
    public DeclareDungeonStateSignal(DungeonState _dungeonState)
    {
        dungeonState = _dungeonState;
    }
}

public struct CharacterRideStartSignal { }