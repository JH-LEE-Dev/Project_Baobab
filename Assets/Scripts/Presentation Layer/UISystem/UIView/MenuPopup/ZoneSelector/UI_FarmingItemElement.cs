using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UI_FarmingItemElement : MonoBehaviour
{
    //외부 의존성
    [SerializeField] private Image itemIcon;
    [SerializeField] private TMP_Text itemNameText;

    //퍼블릭 초기화 및 제어 메서드
    public void Initialize(FarmingItemData _data)
    {
        if (_data == null) 
            return;

        if (itemIcon != null) 
            itemIcon.sprite = _data.itemIcon;
        
        if (itemNameText != null) 
            itemNameText.text = _data.itemName;
    }
}
