using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    //외부 의존성
    [SerializeField] private Image uiImage;
    public Action<IItemData, LogStateCount[], Vector2> enterSlot;
    public Action exitSlot;
    public Action<IInventorySlot> deleteItem;

    //내부 의존성
    private IItemData showItemData;
    public IItemData ShowItemData { get { return showItemData; } }

    private IInventorySlot invSlotRef;

    private int showCnt = 0;
    public int ShowCnt { get { return showCnt; } }
    
    private TMP_Text countText;

    public void Initialize()
    {
        UpdateImage(null, Color.white);
        if (null != uiImage && null != uiImage.sprite && uiImage.sprite.texture.isReadable)
            uiImage.alphaHitTestMinimumThreshold = 0.1f;

        countText = gameObject.GetComponentInChildren<TMP_Text>();
        UpdateItemCount(0);
    }

    public void ResetData()
    {
        UpdateImage(null, Color.white);
        UpdateItemCount(0);

        invSlotRef = null;
        showItemData = null;
    }

    public void UpdateItemCount(int _newCnt)
    {
        if (null == countText)
            return;

        countText.enabled = 0 < _newCnt;

        if (showCnt == _newCnt)
            return;

        showCnt = _newCnt;
        countText.text = _newCnt.ToString();
    }

    public void UpdateImage(Sprite _sprite, Color _color)
    {
        if (null == uiImage || uiImage.sprite == _sprite)
            return;

        uiImage.sprite = _sprite;
        uiImage.color = _color;
        uiImage.enabled = null != _sprite;
    }

    public void UpdateBindSlotData(IInventorySlot _newSlot)
    {
        if (invSlotRef == _newSlot && showItemData == _newSlot.itemData)
            return;

        showItemData = _newSlot.itemData;
        invSlotRef = _newSlot;

        if (null == showItemData)
            return;

        UpdateImage(showItemData.sprite, showItemData.color);
    }

    public void DisableRayCast()
    {
        if (null == uiImage)
            return;

        uiImage.raycastTarget = false;
    }

    // 유니티 이벤트 함수 및 인터페이스 구현
    public virtual void OnPointerClick(PointerEventData _eventData)
    {
        Debug.Log("아이템 삭제 요청");

        if (null != deleteItem)
            deleteItem.Invoke(invSlotRef);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        Debug.Log("슬롯에 마우스 올라옴");

        if (null != enterSlot)
            enterSlot.Invoke(showItemData, invSlotRef?.logStateCounts, uiImage.rectTransform.position);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        Debug.Log("슬롯에 마우스 빠짐");

        if (null != exitSlot)
            exitSlot.Invoke();
    }
}
