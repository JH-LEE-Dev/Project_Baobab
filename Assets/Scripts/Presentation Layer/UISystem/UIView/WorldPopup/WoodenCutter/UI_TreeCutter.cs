using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;

public class UI_TreeCutter : MonoBehaviour
{
    [SerializeField] private GameObject uiSlotPrefab;
    [SerializeField] private GameObject mainVisual;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private HUD_ProgressBar progressBar;
    [SerializeField] private Vector3 offset;

    private ILogItemData cachedItemData;
    private float remaining = 0f;
    private ILogCutter logCutter;

    private UI_InventorySlot slot;
    public UI_InventorySlot Slot { get { return slot;  } set { slot = value; } }

    [SerializeField] private string popupTag = "Popup";
    [SerializeField] private string popdownTag = "Popdown";

    private MotionEntry popup;
    private MotionEntry popdown;
    private bool bOpen = false;

    public void Initialize(Vector2 _offset)
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

        offset = _offset;

        omp?.Initialize();
        progressBar?.Initialize();

        OnHide();
    }

    public void BindItemData(ILogItemData _itemData)
    {
        cachedItemData = _itemData;

        if (null != slot)
        {
            if (null != _itemData)
                slot.UpdateImage(_itemData.sprite, Color.white);
            else
                slot.ResetData();
        }

        if (null != _itemData)
            OnShow();
    }

    public void Refresh()
    {
        if (null != slot && null != cachedItemData)
        {
            slot.UpdateImage(cachedItemData.sprite, Color.white);
        }
    }

    public void BindRemaining(float _remaining) => remaining = _remaining;

    public void BindLogCutter(ILogCutter _logCutter)
    {
        logCutter = _logCutter;
    }

    public void BindPosition(Vector3 _newPos)
    {
        RectTransform rt = GetComponent<RectTransform>();
        if (null != rt)
            rt.position = _newPos + offset;
    }

    public void ResetCutter()
    {
        cachedItemData = null;
        remaining = 0f;

        slot?.ResetData();

        OnHide();
    }

    public void OnShow()
    {
        if (null == cachedItemData)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        if (null == omp)
            return;

        bOpen = true;


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

    private void OnCompletedAnimation()
    {
        bOpen = false;
        gameObject.SetActive(false);
    }

    private void Update()
    {
        if (true == bOpen)
        {
            float _ratio = 0f;
            if (null != logCutter)
            {
                float _total = logCutter.totalProcessingTime;
                if (0f < _total)
                    _ratio = Mathf.Clamp01(logCutter.elapsedProcessingTime / _total);
            }

            progressBar?.UpdateValue(_ratio);
        }
    }
}