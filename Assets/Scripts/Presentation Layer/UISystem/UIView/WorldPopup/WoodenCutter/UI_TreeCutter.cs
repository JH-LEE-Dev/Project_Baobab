using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;

public class UI_TreeCutter : MonoBehaviour
{
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject mainVisual;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private float yOffset = 30f;

    private ILogItemData cachedItemData;
    private float remaining = 0f;

    private UI_InventorySlot slot;
    public UI_InventorySlot Slot { get { return slot;  } set { slot = value; } }

    [SerializeField] private string popupTag = "Popup";
    [SerializeField] private string popdownTag = "Popdown";

    private MotionEntry popup;
    private MotionEntry popdown;

    public void Initialize()
    {
        if (null != uiSlotPrefab)
        {
            slot = Instantiate(uiSlotPrefab, mainVisual.transform).GetComponent<UI_InventorySlot>();

            if (null != slot)
            {
                slot.Initialize();
                slot.DisableRayCast();
            }
        }

        omp?.Initialize();
        OnHide();
    }

    public void BindItemData(ILogItemData _itemData)
    {
        cachedItemData = _itemData;

        if (null != slot)
        {
            slot.UpdateImage(_itemData.sprite, _itemData.color);
        }
    }

    public void Refresh()
    {
        if (null != slot && null != cachedItemData)
        {
            slot.UpdateImage(cachedItemData.sprite, cachedItemData.color);
        }
    }

    public void BindRemaining(float _remaining) => remaining = _remaining;

    public void BindPosition(Vector3 _newPos)
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (null != rt)
        {
            rt.position = _newPos + Vector3.up * yOffset;
        }
    }

    public void ResetCutter()
    {
        cachedItemData = null;
        remaining = 0f;

        slot?.ResetData();
    }

    public void OnShow()
    {
        gameObject.SetActive(true);

        if (null == omp)
            return;
        
        omp.SettingEntryMotion(popdown, true, true);
        popup = omp.Play(popupTag, bReset: true);
    }

    public void OnHide()
    {
        if (null == omp)
            return;
        
        omp.SettingEntryMotion(popup, true, true);
        popdown = omp.Play(popdownTag, bReset: true, _onComplete: OnCompletedAnimation);
    }

    private void OnCompletedAnimation() => gameObject.SetActive(false);
}