using UnityEngine;

public class ItemData : IItemData
{
    public ItemType itemType;
    public Sprite sprite;
    public Color color;

    ItemType IItemData.itemType => itemType;

    Sprite IItemData.sprite => sprite;

    Color IItemData.color => color;

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
        color = _item.color;
    }

    public virtual void Reset()
    {
        itemType = ItemType.None;
    }
}
