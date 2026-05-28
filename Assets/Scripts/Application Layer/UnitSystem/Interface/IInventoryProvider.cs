using UnityEngine;
using System;
using System.Collections.Generic;

public interface IInventoryProvider
{
    int currentSlotCnt { get; }
    bool CanAddItem(ItemData _sourceData, int _pendingCount = 0);
    bool AddItemByData(ItemData _sourceData, LogState _state);
    void ItemDeleted(IInventorySlot _inventorySlot);
    bool HasTransferableItem(ICollection<IInventorySlot> _excludeSlots, IInventoryProvider _targetContainer);
    IInventorySlot GetFirstTransferableSlot(ICollection<IInventorySlot> _excludeSlots, IInventoryProvider _targetContainer);
}
