using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUD_EquipmentItem : MonoBehaviour
{
    private Image mainImg;
    private Image subImg;
    private TMP_Text srcText;
    private EquipmentSpriteData spriteData;

    public void Initialize(EquipmentSpriteData _spriteData)
    {
        spriteData = _spriteData;
        
        Image[] imgs = GetComponentsInChildren<Image>();
        if (imgs.Length >= 2)
        {
            mainImg = imgs[0];
            subImg = imgs[1];
        }
        else if (imgs.Length == 1)
        {
            mainImg = imgs[0];
        }

        srcText = GetComponentInChildren<TMP_Text>();
    }

    public void ChangeImage(EquipmentType _inType)
    {
        if (null == spriteData || null == mainImg)
            return;

        if (spriteData.TryGetSprites(_inType, out Sprite main, out Sprite sub))
        {
            mainImg.sprite = main;
            mainImg.enabled = (null != main);

            if (sub != null)
            {
                subImg.sprite = sub;
                SetSubImgVisibility(true);
            }
            else
                SetSubImgVisibility(false);
        }

        CheckVisibilityByEquipmentType(_inType);
    }

    public void ChangeText(int cnt)
    {
        if (null == srcText)
            return;

        srcText.text = cnt.ToString(); 
    }

    private void CheckVisibilityByEquipmentType(EquipmentType _inType)
    {
        bool needsAmmo = (_inType == EquipmentType.Rifle);
        
        SetTextVisibility(needsAmmo);

        if (!needsAmmo) 
            SetSubImgVisibility(false);
    }

    public void SetTextVisibility(bool isTrigger) => srcText?.gameObject.SetActive(isTrigger);
    public void SetMainImgVisibility(bool isTrigger) => mainImg?.gameObject.SetActive(isTrigger);
    public void SetSubImgVisibility(bool isTrigger) => subImg?.gameObject.SetActive(isTrigger);
}
