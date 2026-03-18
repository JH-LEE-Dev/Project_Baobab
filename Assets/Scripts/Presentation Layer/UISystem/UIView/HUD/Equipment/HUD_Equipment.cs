using UnityEngine;

public class HUD_Equipment : MonoBehaviour
{
    private HUD_EquipmentField equipmentField;
    private HUD_EquipmentItem equipmentItem;

    public void Initialize()
    {
        equipmentField = GetComponentInChildren<HUD_EquipmentField>();
        equipmentField?.Initialize();

        equipmentItem = GetComponentInChildren<HUD_EquipmentItem>();

        if (null != equipmentItem)
        {
            equipmentItem.Initialize();
        }
    }

    public void OnDestroy()
    {
        equipmentField?.OnDestroy();
        equipmentItem?.OnDestroy();
    }

    public void OnShow()
    {
        equipmentField?.OnShow();
        equipmentItem?.OnShow();
    }

    public void OnHide()
    {
        equipmentField?.OnHide();
        equipmentItem?.OnHide();
    }

    public void UpdateAmmoCount(int cnt) => equipmentItem?.ChangeText(cnt);

    public void UpdateState(EquipmentType type, int ammoCnt)
    {
        equipmentItem?.ChangeImage(type);
        equipmentItem?.ChangeText(ammoCnt);
    }
}
