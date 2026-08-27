/// <summary>
/// 진동을 울리는 게임 상황입니다. 파형은 HapticPresets에, 호출 시점은 각 게임 로직에 있습니다.
///
/// 항목을 추가하면 HapticPresets의 표에도 반드시 같이 추가하세요. (Count는 항상 마지막)
/// </summary>
public enum EHapticEvent
{
    /// <summary>나무가 타격당함. 도끼 평타와 쇼크웨이브가 같은 항목을 씁니다(둘이 겹쳐도 한 번만 울리도록).</summary>
    TreeImpact,

    /// <summary>나무가 파괴됨.</summary>
    TreeDestroy,

    /// <summary>마을 → 숲 이동 연출에서 차량 시동이 걸림.</summary>
    VehicleIgnition,

    /// <summary>특성(스킬)을 찍음.</summary>
    SkillPoint,

    /// <summary>프레스티지 레벨이 올라감.</summary>
    PrestigeLevelUp,

    /// <summary>원목/코인이 캐릭터나 상자에 박힘. (NPC는 제외)</summary>
    ItemImpact,

    /// <summary>차량에 탑승함. (마을/던전 공통)</summary>
    VehicleBoard,

    /// <summary>마을 → 숲 이동 연출에서 상자가 차 위에 착지함.</summary>
    ContainerLanding,

    /// <summary>튜토리얼에서 캐릭터가 차량에서 내림.</summary>
    VehicleDismount,

    /// <summary>스태미너가 다 닳아 캐릭터가 사망함.</summary>
    StaminaDeath,

    /// <summary>Fascinating 이상 등급의 원목이 생성됨.</summary>
    RareLogSpawn,

    /// <summary>DropAllItem으로 원목을 흘림.</summary>
    ItemDropped,

    Count
}
