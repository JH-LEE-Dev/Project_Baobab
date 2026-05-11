using System;
    
public interface IInventorySlot
{
    public IItemData itemData { get; }
    public int count { get; }
    public LogStateCount[] logStateCounts { get; }
    public event Action SlotUpdatedEvent;
}
