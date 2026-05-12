using System;
    
public interface IInventorySlot
{
    public IItemData itemData { get; }
    public int count { get; }
    public LogStateCount[] logStateCounts { get; }
    public TreeTypeCount[] treeTypeCounts { get; }
    public event Action SlotUpdatedEvent;
}
