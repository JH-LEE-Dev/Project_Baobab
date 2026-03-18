using UnityEngine;

public class Item : MonoBehaviour
{
    public ItemType itemType { get; private set; }

    public virtual void Initialize(ItemType _itemType)
    {
        itemType = _itemType;
    }

    public virtual void ResetItem()
    {
        
    }
}
