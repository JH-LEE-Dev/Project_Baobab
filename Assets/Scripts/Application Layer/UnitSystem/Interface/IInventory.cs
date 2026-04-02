using System.Collections.Generic;
using UnityEngine;


public interface IInventory
{
    IReadOnlyList<IInventorySlot> inventorySlots {get;}
    Transform GetTransform();
}
