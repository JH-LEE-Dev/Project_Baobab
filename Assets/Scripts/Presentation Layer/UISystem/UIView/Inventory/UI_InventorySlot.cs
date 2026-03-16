using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UI_InventorySlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Main Settings")]   
    private UIView_Inventory commander;
    private Image uiImage;
    private TMP_Text countText;

    public void Initialize(UIView_Inventory owner)
    {
        commander = owner;

        uiImage = gameObject.GetComponentInChildren<Image>();
        countText = gameObject.GetComponentInChildren<TMP_Text>();

    }

    public virtual void OnPointerClick(PointerEventData eventData)
    {

    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        
    }

    public void UpdateItemCount(int newCnt)
    {
        if (null == countText)
            return;

        countText.text = newCnt.ToString();
    }

    public void UpdateImage(Image img)
    {
        if (null == img)
            return;

        if (null == uiImage)
            return;

        uiImage.sprite = img.sprite;
    }
}
