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

public struct LootPillarInteractStateChangedSignal
{
    public bool state;
    public LootType lootType;
    public LootPillarInteractStateChangedSignal(bool _state, LootType _lootType)
    {
        state = _state;
        lootType = _lootType;
    }
}

public struct LootPillarInteractSignal
{
    public bool bInteract;
    public LootType lootType;
    public LootPillarInteractSignal(bool _bInteract, LootType _lootType)
    {
        bInteract = _bInteract;
        lootType = _lootType;
    }
}

public struct StartDecreaseStaminaSignal { }

public struct PopupUIDownSignal { }
public struct PopupUIUpSignal { }

public struct ActivateCharacterSignal { }

// 캐릭터 조준(마우스 추적)만 먼저 켜라는 신호. ActivateCharacterSignal은 AttackIndicator 노출까지
// 함께 처리하느라 HUD가 올라오는 시점(조작 해제 0.7초 뒤)에 발행되는데, 그 0.7초 동안 조준이
// 갱신되지 않아 마우스를 움직여도 캐릭터/팔이 이전 방향을 그대로 보고 있는 문제가 있었다.
// 그래서 "조준 활성화"만 떼어내 조작 잠금이 풀리는 시점에 즉시 발행한다.
public struct EnableCharacterAimSignal { }

// MainMenu → Dungeon 튜토리얼: 캐릭터 하차 후, 일반 던전 입장과 동일한 마무리
// (조작 잠금 해제 + ActivateCharacterSignal + HUD 복귀)를 실행하라는 신호.
// 일반 경로에서 카메라 하강 완료가 하던 역할을 튜토리얼에서는 이 신호가 대신한다.
public struct CompleteDungeonEntrySignal { }

public struct GoToMainMenuRequestedSignal { }
public struct GoToMainMenuSignal { }