using System;
using System.Collections.Generic;
using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;

public class UI_Storage : MonoBehaviour
{
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject mainVisual;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private float yOffset = 30f;

    private const int defaultCap = 2;

    private IInventory storage;
    private List<UI_InventorySlot> storageSlots;
    public bool isOpening { get; private set; } = false;

    [SerializeField] private string popupTag = "Popup";
    [SerializeField] private string popdownTag = "Popdown";

    private MotionEntry popup;
    private MotionEntry popdown;


    public void Initialize()
    {
        storageSlots = new List<UI_InventorySlot>(SYSTEM_VAR.MAX_STORAGE_CNT);
        gameObject.SetActive(false);

        omp?.Initialize();
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
            slot.DisableRayCast();

            storageSlots.Add(slot);
        }
    }

    public void Refresh()
    {
        if (null == storage)
            return;

        UpdateSlots(storage.inventorySlots);
    }

    private void UpdateSlots(IReadOnlyList<IInventorySlot> _items)
    {
        if (null == _items)
            return;

        int itemCount = storage.currentSlotCnt;

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
        gameObject.SetActive(isOpening = true);

        if (null == omp)
            return;

        omp.SettingEntryMotion(popdown, true, true);
        popup = omp.Play(popupTag, bReset: true);
    }

    public void OnHide()
    {
        if (null == omp)
            return;

        omp.SettingEntryMotion(popup, true, true);
        popdown = omp.Play(popdownTag, bReset: true, _onComplete: OnCompleteAnim);
    }

    private void OnCompleteAnim () => gameObject.SetActive(isOpening = false);
}
