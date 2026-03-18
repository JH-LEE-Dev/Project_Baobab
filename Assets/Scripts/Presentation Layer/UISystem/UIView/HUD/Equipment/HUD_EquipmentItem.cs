using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD_EquipmentItem : MonoBehaviour
{
    private Image mainImg;
    private Image subImg;
    private TMP_Text srcText;

    private const string imgFolderPath = "Assets/Graphics/HUD/Equipment/";
    private const string ammoStr = "_Ammo";

    public void Initialize()
    {
        mainImg = GetComponentInChildren<Image>();
        subImg = GetComponentInChildren<Image>();
        srcText = GetComponentInChildren<TMP_Text>();

        ChangeImage(EquipmentType.Hatchet);
    }

    public void OnDestroy()
    {

    }

    public void OnShow()
    {
        
    }

    public void OnHide()
    {
        
    }

    public void ChangeImage(EquipmentType _inType)
    {
        if (null == mainImg)
            return;

        Sprite newSprite = GlobalUI.GetSpritefromPath(imgFolderPath, _inType.ToString());
        if (null == newSprite)
            return;

        mainImg.sprite = newSprite;

        CheckUsedAmmo(_inType);
    }

    public void ChangeText(int cnt)
    {
        if (null == srcText)
            return;

        srcText.text = cnt.ToString(); 
    }

    private void CheckUsedAmmo(EquipmentType _inType)
    {
        switch (_inType)
        {
            case EquipmentType.Rifle:
                SetSubImgVisibility(true);
                SetTextVisibility(true);
                break;

            default:
                SetSubImgVisibility(false);
                SetTextVisibility(false);
                return;
        }

        if (null == subImg)
            return;

        string ammoName = _inType.ToString() + ammoStr;

        Sprite newSprite = GlobalUI.GetSpritefromPath(imgFolderPath, ammoName);
        if (null == newSprite)
            return;
        
        subImg.sprite = newSprite;
    }

    public void SetTextVisibility(bool isTrigger) => srcText?.gameObject.SetActive(isTrigger);
    public void SetMainImgVisibility(bool isTrigger) => mainImg?.gameObject.SetActive(isTrigger);
    public void SetSubImgVisibility(bool isTrigger) => subImg?.gameObject.SetActive(isTrigger);
}
