using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.UI;

public class UI_ZoneRegion : MonoBehaviour
{
    [Header("Prefabs & Containers")]
    [SerializeField] private UI_ZoneSelectSlot slotPrefab;
    [SerializeField] private Transform slotContainer;

    private List<UI_ZoneSelectSlot> zoneSlots = new List<UI_ZoneSelectSlot>();
    private int regionId;

    public void Initialize(int _regionId, int _zoneCount, Action<int, int> _onZoneClick, Action<int, int> _onHoverEnter, Action _onHoverExit)
    {
        regionId = _regionId;
        
        for (int i = 0; i < _zoneCount; ++i)
        {
            if (slotPrefab == null || slotContainer == null) break;

            UI_ZoneSelectSlot slot = Instantiate(slotPrefab, slotContainer);
            slot.Initialize(regionId, i, true, _onZoneClick, _onHoverEnter, _onHoverExit);
            zoneSlots.Add(slot);
        }
    }

    public void UnlockZone(int _zoneId)
    {
        if (_zoneId >= 0 && _zoneId < zoneSlots.Count)
        {
            zoneSlots[_zoneId].SetLockStatus(false);
        }
    }

    public void SetSlotHighlight(int _zoneId, bool _active)
    {
        if (_zoneId >= 0 && _zoneId < zoneSlots.Count)
        {
            zoneSlots[_zoneId].SetHighlight(_active);
        }
    }

    public void SetVisible(bool _visible)
    {
        gameObject.SetActive(_visible);
    }
}
