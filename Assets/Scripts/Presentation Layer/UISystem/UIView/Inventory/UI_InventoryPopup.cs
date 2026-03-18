using System.Collections.Generic;
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

                newSlot.Initialize(null);
                newSlot.DisableRayCast();
                newSlot.gameObject.SetActive(false);

                slots.Add(newSlot);
            }
        }

        rect = GetComponent<RectTransform>();
    }

    public void ShowItems(Vector2 position/*, 목록을 받아 옴*/)
    {
        // 임시. TODO :: 나중엔 받아온 목록만큼 노출
        for (int i = 0; i < 4; ++i)
            slots[i].gameObject.SetActive(true);

        if (null != rect)
        {
            Debug.Log("너 처음에 호출 되잖아 싯팔아");
            rect.position = position;
            Canvas.ForceUpdateCanvases();
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
