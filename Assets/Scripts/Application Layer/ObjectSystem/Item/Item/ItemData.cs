using UnityEngine;

public class ItemData : IItemData
{
    public ItemType itemType;
    public Sprite sprite;
    ItemType IItemData.itemType => itemType;

    Sprite IItemData.sprite => sprite;

    public virtual bool IsSameType(ItemData _other)
    {
        if (_other == null) return false;
        return itemType == _other.itemType;
    }

    public virtual void CopyFrom(Item _item)
    {
        if (_item == null) return;
        itemType = _item.itemType;
        sprite  = _item.sprite;
    }

    public virtual void Reset()
    {
        itemType = ItemType.None;
    }
}
