using System;
using System.Collections.Generic;
using UnityEngine;

public class UI_Storage : MonoBehaviour
{
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject mainVisual;
    [SerializeField] private float yOffset = 30f;

    private const int defaultCap = 2;

    private IInventory storage;
    private List<UI_InventorySlot> storageSlots;

    public void Initialize()
    {
        storageSlots = new List<UI_InventorySlot>(SYSTEM_VAR.MAX_STORAGE_CNT);
        gameObject.SetActive(false);
    }

    public void BindStorage(IInventory _storage)
    {
        storage = _storage;
        if (storage != null)
        {
            UpdateMaxSlotCount(storage.inventorySlots.Count);
            RectTransform rect = GetComponent<RectTransform>();

            if (null != rect)
            {
                Vector3 newPos = storage.GetTransform().position;
                newPos.y += yOffset;
                rect.position = newPos;
            }
        }
    }

    public void UpdateMaxSlotCount(int _cnt)
    {
        if (null == uiSlotPrefab)
            return;

        int needCount = _cnt - storageSlots.Count;

        while (0 < needCount--)
        {
            UI_InventorySlot slot = Instantiate(uiSlotPrefab, mainVisual.transform).GetComponent<UI_InventorySlot>();

            if (null == slot)
                return;

            slot.Initialize();
            storageSlots.Add(slot);
        }
    }

    public void Refresh()
    {
        if (null == storage)
            return;

        if (false == gameObject.activeSelf)
            gameObject.SetActive(true);

        UpdateSlots(storage.inventorySlots);
    }

    private void UpdateSlots(IReadOnlyList<IInventorySlot> _items)
    {
        if (null == _items)
            return;

        int itemCount = _items.Count;

        for (int i = 0; i < storageSlots.Count; ++i)
        {
            UI_InventorySlot slot = storageSlots[i];

            if (i < itemCount)
            {
                IInventorySlot item = _items[i];

                if (false == slot.gameObject.activeSelf)
                    slot.gameObject.SetActive(true);

                slot.UpdateBindSlotData(item);
                slot.UpdateItemCount(item.count);
            }
            else
            {
                if (true == slot.gameObject.activeSelf)
                {
                    slot.ResetData();
                    slot.gameObject.SetActive(false);
                }
            }
        }
    }

    public void OnShow()
    {
        gameObject.SetActive(true);
    }

    public void OnHide()
    {
         gameObject.SetActive(false);
    }
}
