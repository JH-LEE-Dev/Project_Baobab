using System.Collections.Generic;
using UnityEngine;


public interface IInventory
{
    IReadOnlyList<IInventorySlot> inventorySlots { get; }
    int currentSlotCnt { get; }
    Transform GetTransform();
    int money { get; }
    int carrot { get; }
}
