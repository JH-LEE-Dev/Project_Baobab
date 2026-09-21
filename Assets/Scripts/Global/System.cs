
public struct SYSTEM_VAR
{
    /// <summary>
    /// 원석 / 용광로 시스템 전체 스위치. false면 이 계열이 도입되기 전의 동작으로 돌아간다.
    ///
    /// 코드는 하나도 지우지 않고 "들어오는 길"만 막아둔 것이다. 끊어둔 지점은 아래 세 곳이며,
    /// 다시 켜려면 이 값을 true로 바꾸기만 하면 된다(이 상수를 grep하면 전부 찾을 수 있다).
    ///   1. InDungeonObjectManager.OnTreeDead  : 보석 나무가 원석 대신 원목을 떨어뜨린다(원래 동작).
    ///   2. UI_Inventory.UpdateCurrencyState   : 황금/다이아/프리즘 원석 재화 칸을 숨긴다.
    ///   3. BlastFurnaceManager.IncreaseFurnaceCount : 용광로 특성이 용광로를 열지 못하게 한다.
    ///
    /// 용광로 HUD는 따로 막지 않았다. 해금 수가 0이면 NotifyUIState가 모두 unlocked = false인
    /// 목록을 보내고, 그건 특성을 하나도 찍지 않은 상태와 똑같다.
    ///
    /// 보석 나무 자체(체력 회생 / 보석 셰이더 / 전용 타격·사망 연출)는 이 계열보다 먼저 있던
    /// 기능이므로 건드리지 않는다. 여기서 끄는 것은 "보석 나무가 떨군 원석"부터다.
    ///
    /// 세이브에 있는 원석 보유량·획득 이력 필드도 그대로 둔다. 값이 들어올 일이 없어 0으로 남고,
    /// 나중에 이 스위치를 켜면 저장해 둔 값이 그대로 이어진다.
    ///
    /// const가 아니라 static readonly인 이유: const로 두면 컴파일러가 분기를 접어버려
    /// 막아둔 블록마다 "도달할 수 없는 코드"(CS0162) 경고가 콘솔에 깔린다.
    /// </summary>
    public static readonly bool GEM_ORE_SYSTEM_ENABLED = false;

    public const int MAX_INVENTORY_CNT = 10;
    public const int MAX_STORAGE_CNT = 10;
    public const int MAX_TREE_CNT = 3000;

    public const int MAX_ANIMAL_CNT = 100;
    public const int MAX_UI_HPBAR_CNT = 30;
    public const int MAX_ENV_OBJ_CNT = 1500;
    public const int MAX_CLOUD_OBJ_CNT = 1000;
    public const int MAX_BIRDSHADOW_OBJ_CNT = 200;
}