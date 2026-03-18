using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class UI_InventoryPopup : MonoBehaviour
{
    [SerializeField] private GameObject slotPrefab;

    private List<UI_InventorySlot> slots;

    private RectTransform rect;

    public void Initialize(int defaultCap)
    {
        slots = new List<UI_InventorySlot>(defaultCap);

        if (null != slotPrefab)
        {
            for (int i = 0; i < defaultCap; ++i)
            {
                UI_InventorySlot newSlot = Instantiate(slotPrefab, this.transform).GetComponent<UI_InventorySlot>();
                if (null == newSlot)
                    continue;

                newSlot.Initialize();
                newSlot.DisableRayCast();
                newSlot.gameObject.SetActive(false);

                slots.Add(newSlot);
            }
        }

        rect = GetComponent<RectTransform>();
    }

    public void ShowItems(ILogItemData iLogItemData, LogStateCount[] _logStateCounts, Vector2 position)
    {
        if (null == _logStateCounts)
            return;

        for (int i = 0; i < _logStateCounts.Length; ++i)
        {
            slots[i].gameObject.SetActive(true);
            
            slots[i].UpdateImage(iLogItemData.sprite);
            slots[i].UpdateItemCount(_logStateCounts[i].count);
        }

        if (null != rect)
        {
            rect.position = position;
            rect.position = GlobalUI.KeepInsideScreenforUI(rect); 
        }
    }

    public void InvisibleSlots()
    {
        foreach (UI_InventorySlot slot in slots)
        {
            slot.gameObject.SetActive(false);
        }
    }
}
