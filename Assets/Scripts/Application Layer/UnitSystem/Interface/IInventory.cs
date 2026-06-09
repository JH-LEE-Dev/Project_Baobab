using System;
using System.Collections.Generic;
using UnityEngine;

public interface IInventory
{
    IReadOnlyList<IInventorySlot> inventorySlots { get; }
    int currentSlotCnt { get; }
    int maxItemCntPerSlot { get; }
    int maxCapacity { get; }
    int currentItemCount { get; }
    Transform GetTransform();
    long money { get; }
    long carrot { get; }
    public event Action InventoryIsFullEvent;
}
