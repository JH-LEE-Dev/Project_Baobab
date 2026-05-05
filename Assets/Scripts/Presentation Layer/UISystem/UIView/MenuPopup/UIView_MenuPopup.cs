using System;
using PresentationLayer.UISystem.UIView.MenuPopup.Map;
using UnityEngine;

public class UIView_MenuPopup : UIView
{
    public event Action<MapType> DungeonSelectedEvent;

    // //외부 의존성
    [Header("Sub UI Prefabs")]
    [SerializeField] private GameObject mapSelectorPrefab;

    // //내부 의존성
    private HUD_MapSelector mapSelector;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (null == mapSelector && null != mapSelectorPrefab)
            mapSelector = Instantiate(mapSelectorPrefab, this.transform).GetComponent<HUD_MapSelector>();

        if (null != mapSelector)
            mapSelector.Initialize(HandleEnterDungeon);

        CloseTeleportUI();
    }

    private void HandleEnterDungeon(MapType _type)
    {
        if (MapType.None == _type)
            return;

        Debug.Log($"[UIView_MenuPopup] Entering Dungeon: {_type}");
        
        DungeonSelectedEvent?.Invoke(_type);
        CloseTeleportUI();
    }

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    public void TeleportUIOpen()
    {
        if (null != mapSelector)
            mapSelector.gameObject.SetActive(true);
    }

    public void CloseTeleportUI()
    {
        if (null != mapSelector)
            mapSelector.gameObject.SetActive(false);
    }
}
