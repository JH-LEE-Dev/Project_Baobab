using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

public class UI_InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    //외부 의존성
    [SerializeField] private Image uiImage;
    [SerializeField] private ObjectMotionPlayer omp;
    public Action<UI_InventorySlot, IItemData, Vector2> enterSlot;
    public Action exitSlot;
    public Action<IInventorySlot> deleteItem;

    //내부 의존성
    private IItemData showItemData;
    public IItemData ShowItemData { get { return showItemData; } }

    private IInventorySlot invSlotRef;
    public IInventorySlot InvSlotRef { get { return invSlotRef; } }

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

        // 함수 바인딩 빼주기
        if (null != invSlotRef)
            invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;
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

        // 아이템 데이터가 없다면.
        if (null == _newSlot.itemData)
        {
            ResetData();
            return;
        }

        // 이전에 바인딩 된 주소가 남아있다면 함수 바인딩 빼주기
        if (null != invSlotRef)
            invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;

        showItemData = _newSlot.itemData;
        invSlotRef = _newSlot;

        if (null != invSlotRef)
        {
            invSlotRef.SlotUpdatedEvent -= PlayItemInteraction;
            invSlotRef.SlotUpdatedEvent += PlayItemInteraction;
        }

        if (null == showItemData)
            return;

        UpdateImage(showItemData.sprite, showItemData.color);
    }

    private void PlayItemInteraction()
    {
        omp?.Play("ItemInteraction", bReset: true);

        if (null != invSlotRef)
            UpdateItemCount(invSlotRef.count);
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
        if (null != deleteItem)
            deleteItem.Invoke(invSlotRef);
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (null != enterSlot)
            enterSlot.Invoke(this, showItemData, uiImage.rectTransform.position);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (null != exitSlot)
            exitSlot.Invoke();
    }
}
