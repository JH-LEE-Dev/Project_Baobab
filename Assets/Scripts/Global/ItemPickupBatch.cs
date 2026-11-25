using System;
using UnityEngine;

/// <summary>
/// 원목 갱신 루프(LogItemController.Update) 한 번을 감싸는 구간 표시.
///
/// 흡입 반경이 넓으면 이 루프 한 번(한 프레임) 안에서 원목 수십 개가 연달아 습득되고, 습득마다 인벤토리 UI가
/// 같은 갱신을 되풀이한다. 루프는 한 프레임 안에서 끊김 없이 도는 구간이라 그 사이에 트윈이 진행되거나 화면이
/// 그려지지 않으므로, UI는 이 구간 안에서 결과가 같다고 확인된 갱신을 구간 끝(Ended)으로 미뤄 한 번에 처리한다.
/// (UI_Inventory.TryDeferItemAdded, UI_InventorySlot의 획득 모션)
///
/// 구간 밖에서는 아무 일도 하지 않으므로 다른 경로의 획득(마을의 상자 인출 등)은 예전 그대로다.
/// </summary>
public static class ItemPickupBatch
{
    /// <summary>구간이 끝난 직후(같은 Update 호출 안). 미뤄 둔 갱신을 여기서 처리한다.</summary>
    public static event Action Ended;

    private static int depth = 0;

    /// <summary>지금 구간 안인지.</summary>
    public static bool IsActive => 0 < depth;

    /// <summary>구간이 시작될 때마다 1씩 오르는 번호. 같은 번호 = 같은 구간.</summary>
    public static int Serial { get; private set; } = 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Ended = null;
        depth = 0;
        Serial = 0;
    }

    public static void Begin()
    {
        if (0 == depth)
            ++Serial;

        ++depth;
    }

    public static void End()
    {
        if (0 >= depth)
            return;

        --depth;

        if (0 == depth)
            Ended?.Invoke();
    }
}
