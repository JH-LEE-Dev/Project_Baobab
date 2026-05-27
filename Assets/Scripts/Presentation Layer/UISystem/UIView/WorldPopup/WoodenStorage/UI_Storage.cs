using System;
using System.Collections.Generic;
using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;

public class UI_Storage : MonoBehaviour
{
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject mainVisual;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private Vector2 offset;

    private const int defaultCap = 2;

    private IInventory storage;
    private List<UI_InventorySlot> storageSlots;
    public bool isOpening { get; private set; } = false;

    [SerializeField] private string popupTag = "Popup";
    [SerializeField] private string popdownTag = "Popdown";

    private MotionEntry popup;
    private MotionEntry popdown;
    RectTransform rect;


    public void Initialize(Vector2 _offset)
    {
        storageSlots = new List<UI_InventorySlot>(SYSTEM_VAR.MAX_STORAGE_CNT);
        gameObject.SetActive(false);
        offset = _offset;

        omp?.Initialize();

        rect = GetComponent<RectTransform>();
    }

    public void BindStorage(IInventory _storage)
    {
        storage = _storage;
        if (storage != null)
            UpdateMaxSlotCount(storage.inventorySlots.Count);
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

        UpdateMaxSlotCount(storage.inventorySlots.Count);
        UpdateSlots(storage.inventorySlots);
    }

    public void UpdateSlots(IReadOnlyList<IInventorySlot> _items = null)
    {
        if (null == _items && null == storage)
            return;

        if (null == _items)
            _items = storage.inventorySlots;

        int itemCount = storage.currentSlotCnt;

        for (int i = 0; i < storageSlots.Count; ++i)
        {
            UI_InventorySlot slot = storageSlots[i];
            IInventorySlot item = _items[i];

            slot.gameObject.SetActive(i < itemCount);
            slot.UpdateBindSlotData(item);
        }
    }

    public void OnShow()
    {
        gameObject.SetActive(isOpening = true);

        if (null != rect)
        {
            Vector2 newPos = storage.GetTransform().position;
            newPos += offset;

            rect.position = newPos;
        }

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
