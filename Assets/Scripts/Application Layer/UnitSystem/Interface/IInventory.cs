using System;
using System.Collections.Generic;
using UnityEngine;

public interface IInventory
{
    IReadOnlyList<IInventorySlot> inventorySlots { get; } //인벤토리 슬롯 데이터
    int currentSlotCnt { get; } //슬롯 개수
    int maxItemCntPerSlot { get; } //슬롯에 누적 가능한 개수
    int maxCapacity { get; } //보관 가능한 총 아이템 수
    int currentItemCount { get; } //현재 보관된 총 아이템 수

    Transform GetTransform();
    long money { get; }
    long carrot { get; }
    public event Action InventoryIsFullEvent;
}
