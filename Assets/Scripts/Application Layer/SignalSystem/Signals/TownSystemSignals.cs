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
public struct ItemAddedToLogContainerSignal { }
public struct ReturnToTownCameraDownEndedSignal { }

// 마을에서 플레이어가 OffroadVehicle에 실제로 상호작용(인터랙트 키 입력)한 시점.
// PortalActivatedSignal은 던전 쪽 차량 상호작용에도 공용으로 쓰여 마을 전용 판별에는 쓸 수 없어 별도로 둔다.
public struct TownOffroadVehicleActivatedSignal { }

public struct ShopMoneyUpdatedSignal
{
    public int money;
    public ShopMoneyUpdatedSignal(int _money)
    {
        money = _money;
    }
}

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

public struct TentInteractStateChangedSignal
{
    public bool state;
    public TentInteractStateChangedSignal(bool _state)
    {
        state = _state;
    }
}

public struct StartDecreaseStaminaSignal { }

public struct PopupUIDownSignal { }
public struct PopupUIUpSignal { }

public struct ActivateCharacterSignal { }

// MainMenu → Dungeon 튜토리얼: 캐릭터 하차 후, 일반 던전 입장과 동일한 마무리
// (조작 잠금 해제 + ActivateCharacterSignal + HUD 복귀)를 실행하라는 신호.
// 일반 경로에서 카메라 하강 완료가 하던 역할을 튜토리얼에서는 이 신호가 대신한다.
public struct CompleteDungeonEntrySignal { }

public struct GoToMainMenuRequestedSignal { }
public struct GoToMainMenuSignal { }