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

    private LogStateCount[] logStateCounts;
    public LogStateCount[] LogStateCounts { get { return logStateCounts; } }


    private int showCnt = 0;
    public int ShowCnt { get { return showCnt; } }
    
    [SerializeField] private Image uiImage;
    private TMP_Text countText;

    public Action<IItemData, LogStateCount[], Vector2> enterSlot;
    public Action exitSlot;
    public Action<IItemData> deleteItem;

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

        if (null != uiImage)
            uiImage.enabled = false;

        deleteItem?.Invoke(showItemData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("슬롯에 마우스 올라옴");

        // // 통나무 타입이 아니면 UI 꺼버리고 호출 안함 / 이전에 켜져있었을 수도 있으니까 방지
        // if (showItemData.itemType != ItemType.Log)
        // {
        //     exitSlot?.Invoke();
        //     return;
        // }

        enterSlot?.Invoke(showItemData, logStateCounts, uiImage.rectTransform.position);
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
        if (null == _sprite || null == uiImage)
            return;

        uiImage.enabled = true;
        uiImage.sprite = _sprite;
    }

    public void UpdateBindSlotData(IItemData _item, LogStateCount[] _logStateCounts)
    {
        showItemData = _item;
        logStateCounts = _logStateCounts;

        Debug.Log(showItemData + " " + logStateCounts);
        // 이름 경로 생성
        //UpdateImage();
    }

    public void DisableRayCast()
    {
        if (null == uiImage)
            return;

        uiImage.raycastTarget = false;
    }
}
