using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Main Settings")]
    private IItemData showItemData;
    public IItemData ShowItemData { get { return showItemData; } }

    private IInventorySlot invSlotRef;

    private int showCnt = 0;
    public int ShowCnt { get { return showCnt; } }
    
    [SerializeField] private Image uiImage;
    private TMP_Text countText;

    public Action<IItemData, LogStateCount[], Vector2> enterSlot;
    public Action exitSlot;
    public Action<IInventorySlot> deleteItem;

    public void Initialize()
    {
        if (null != uiImage)
            uiImage.enabled = false;
        
        if (null != uiImage && uiImage.sprite != null && uiImage.sprite.texture.isReadable)
            uiImage.alphaHitTestMinimumThreshold = 0.1f;

        countText = gameObject.GetComponentInChildren<TMP_Text>();
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("아이템 삭제 요청");

        deleteItem?.Invoke(invSlotRef);
    }

    public void ResetData()
    {
        if (null != uiImage)
            uiImage.enabled = false;

        invSlotRef = null;
        showItemData = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("슬롯에 마우스 올라옴");

        enterSlot?.Invoke(showItemData, invSlotRef?.logStateCounts, uiImage.rectTransform.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Debug.Log("슬롯에 마우스 빠짐");
        exitSlot?.Invoke();
    }

    public void UpdateItemCount(int newCnt)
    {
        if (null == countText)
            return;

        showCnt = newCnt;
        countText.text = newCnt.ToString();
    }

    public void UpdateImage(Sprite _sprite)
    {
        if (null == uiImage)
            return;

        uiImage.enabled = true;
        uiImage.sprite = _sprite;
    }

    public void UpdateBindSlotData(IInventorySlot _newSlot)
    {
        showItemData = _newSlot.itemData;
        invSlotRef = _newSlot;

        UpdateImage(showItemData.sprite);
    }

    public void DisableRayCast()
    {
        if (null == uiImage)
            return;

        uiImage.raycastTarget = false;
    }
}
