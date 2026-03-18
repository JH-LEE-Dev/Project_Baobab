using UnityEngine;

public interface IInventorySlot
{
    public IItemData itemData { get; }
    public int count { get; }
}
