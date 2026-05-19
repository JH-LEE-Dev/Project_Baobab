using System.Collections.Generic;
using UnityEngine;
using System;

public interface IInventory
{
    IReadOnlyList<IInventorySlot> inventorySlots { get; }
    int currentSlotCnt { get; }
    Transform GetTransform();
    long money { get; }
    long carrot { get; }
}
