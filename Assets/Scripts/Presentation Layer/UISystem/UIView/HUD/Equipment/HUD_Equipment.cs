using UnityEngine;

public class HUD_Equipment : MonoBehaviour
{
    private HUD_EquipmentField equipmentField;
    private HUD_EquipmentItem equipmentItem;

    [SerializeField] private EquipmentSpriteData spriteData;

    public void Initialize()
    {
        equipmentField = GetComponentInChildren<HUD_EquipmentField>();
        equipmentField?.Initialize();

        equipmentItem = GetComponentInChildren<HUD_EquipmentItem>();
        equipmentItem?.Initialize(spriteData);
    }

    public void OnDestroy()
    {
        equipmentField?.OnDestroy();
    }

    public void OnShow()
    {
        equipmentField?.OnShow();
    }

    public void OnHide()
    {
        equipmentField?.OnHide();
    }

    public void UpdateAmmoCount(int cnt) => equipmentItem?.ChangeText(cnt);

    public void UpdateState(WeaponMode type, int ammoCnt)
    {
        equipmentItem?.ChangeImage(type);
        equipmentItem?.ChangeText(ammoCnt);
    }
}
