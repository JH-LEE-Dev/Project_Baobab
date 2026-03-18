using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Main Settings")]   
    private Item bindItem;
    private Image uiImage;
    private TMP_Text countText;

    public Action<Item, Vector2> enterSlot;
    public Action exitSlot;
    public Action<Item> deleteItem;

    public void Initialize()
    {
        uiImage = gameObject.GetComponentInChildren<Image>();

        if (null != uiImage && uiImage.sprite != null && uiImage.sprite.texture.isReadable)
            uiImage.alphaHitTestMinimumThreshold = 0.1f;

        countText = gameObject.GetComponentInChildren<TMP_Text>();
    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("아이템 삭제 요청");
        deleteItem?.Invoke(bindItem);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        Debug.Log("슬롯에 마우스 올라옴");
        enterSlot?.Invoke(bindItem, uiImage.rectTransform.position);
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

        countText.text = newCnt.ToString();
    }

    public void UpdateImage(Image img)
    {
        if (null == img || null == uiImage)
            return;

        uiImage.sprite = img.sprite;
    }

    public void DisableRayCast()
    {
        if (null == uiImage)
            return;

        uiImage.raycastTarget = false;
        Debug.Log($"[{gameObject.name}]의 DisableRayCast 실행됨. 객체 ID: {uiImage.gameObject.GetInstanceID()}, 현재 RaycastTarget 상태: {uiImage.raycastTarget}");
    }
}
