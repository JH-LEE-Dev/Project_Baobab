using System.Collections.Generic;


public interface IInventory
{
    IReadOnlyList<IInventorySlot> inventorySlots {get;}
}
