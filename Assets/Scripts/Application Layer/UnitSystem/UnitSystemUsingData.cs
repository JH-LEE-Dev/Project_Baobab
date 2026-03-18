using System;

[Serializable]
public class InventorySlot : IInventorySlot
{
    public ItemData itemData;
    public int count;

    public InventorySlot(ItemData _data, int _count)
    {
        itemData = _data;
        count = _count;
    }

    IItemData IInventorySlot.itemData => itemData;

    int IInventorySlot.count => count;
}