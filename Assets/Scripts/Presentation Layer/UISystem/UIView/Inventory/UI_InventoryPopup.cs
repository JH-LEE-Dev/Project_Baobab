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

    public void ShowItems(ILogItemData iLogItemData, Vector2 position, LogStateCount[] _logStateCounts)
    {
        // 임시. TODO :: 나중엔 받아온 목록만큼 노출
        for (int i = 0; i < _logStateCounts.Length; ++i)
        {
            slots[i].gameObject.SetActive(true);
            
            string itemName = iLogItemData.treeType.ToString() + "_" + iLogItemData.logState.ToString();
            slots[i].UpdateImage(itemName);
            slots[i].UpdateItemCount(_logStateCounts[i].count);
        }

        if (null != rect)
        {
            rect.position = position;
            // 화면 영역 밖으로 나간 위치 보정
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
