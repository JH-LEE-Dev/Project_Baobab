using UnityEngine;

public class ItemData : IItemData
{
    public ItemType itemType;

    ItemType IItemData.itemType => itemType;

    public virtual bool IsSameType(ItemData _other)
    {
        if (_other == null) return false;
        return itemType == _other.itemType;
    }

    public virtual void CopyFrom(Item _item)
    {
        if (_item == null) return;
        itemType = _item.itemType;
    }

    public virtual void Reset()
    {
        itemType = ItemType.None;
    }
}
