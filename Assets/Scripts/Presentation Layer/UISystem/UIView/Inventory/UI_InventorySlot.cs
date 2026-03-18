using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Item Data를 기록 해야 변동 사항에 대해서 주소 비교로 확인을 할 수 있는 상태인데
// 인벤토리 슬롯을 가지고 있어야 삭제할 때 정보를 넘길 수 있어서
// 쓸 데 없이 같은 주소를 2개를 참조하고 있는 이상한 비효율적인 형태가 있음
// 변화를 어떻게 가졌는 지 판단 해야 하고, 쓸 데 없는 보관 없애야 함.

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
        if (null == uiImage)
            return;

        uiImage.enabled = true;
        uiImage.sprite = _sprite;
    }

    public void UpdateBindSlotData(IItemData _item, LogStateCount[] _logStateCounts)
    {
        showItemData = _item;
        logStateCounts = _logStateCounts;
        UpdateImage(_item.sprite);
    }

    public void DisableRayCast()
    {
        if (null == uiImage)
            return;

        uiImage.raycastTarget = false;
    }
}
