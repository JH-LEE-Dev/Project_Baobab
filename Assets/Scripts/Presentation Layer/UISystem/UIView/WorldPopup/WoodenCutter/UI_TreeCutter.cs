using UnityEngine;

public class UI_TreeCutter : MonoBehaviour
{
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject mainVisual;
    [SerializeField] private float yOffset = 30f;

    private ILogItemData cachedItemData;

    private UI_InventorySlot slot;
    public UI_InventorySlot Slot { get { return slot;  } set { slot = value; } }

    public void Initialize()
    {
        if (null != uiSlotPrefab)
        {
            slot = Instantiate(uiSlotPrefab, mainVisual.transform).GetComponent<UI_InventorySlot>();

            if (null != slot)
            {
                slot.Initialize();
                slot.DisableRayCast();
            }
        }
    }

    public void BindItemData(ILogItemData _itemData)
    {
        cachedItemData = _itemData;

        if (null != slot)
        {
            slot.UpdateImage(_itemData.sprite, _itemData.color);
        }
    }
}