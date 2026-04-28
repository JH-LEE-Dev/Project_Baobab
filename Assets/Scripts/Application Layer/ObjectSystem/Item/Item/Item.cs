using UnityEngine;

public class Item : MonoBehaviour
{
    public ItemType itemType { get; private set; }
    public Sprite sprite;
    public Color color;

    public virtual void Initialize(ItemType _itemType)
    {
        itemType = _itemType;
    }

    public virtual void ResetItem()
    {

    }

    public virtual void SetSuckTarget(Transform _target)
    {

    }
}
