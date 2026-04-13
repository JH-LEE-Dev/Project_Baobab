using UnityEngine;

public class LootItemData : ItemData
{
    public LootType lootType;

    public override bool IsSameType(ItemData _other)
    {
        if (base.IsSameType(_other) && _other is LootItemData otherLoot)
        {
            return lootType == otherLoot.lootType;
        }
        return false;
    }

    public override void CopyFrom(Item _item)
    {
        base.CopyFrom(_item);
        if (_item is LootItem lootItem)
        {
            lootType = lootItem.LootType;
        }
    }

    public override void Reset()
    {
        base.Reset();
        lootType = LootType.None;
        sprite = null;
    }
}
