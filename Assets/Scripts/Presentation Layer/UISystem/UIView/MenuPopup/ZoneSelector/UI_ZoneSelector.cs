using UnityEngine;
using System.Collections.Generic;
using System;

public class UI_ZoneSelector : MonoBehaviour
{
    [Header("Pre-created Regions")]
    [SerializeField] private GameObject regionPrefab;
    [SerializeField] private Transform slotContainer;
    private List<UI_ZoneRegion> regions;

    private Action<DungeonType> onZoneSelected;
    private Action<bool> onSelectionStatusChanged;
    private ZoneDatabase zoneDatabase;
    private UI_ZoneInfo zoneInfo;

    private int selectedRegionId = -1;
    private int selectedZoneId = -1;

    public void Initialize(int _capacity, Action<DungeonType> _onZoneSelected, ZoneDatabase _zoneDatabase, UI_ZoneInfo _zoneInfo, Action<bool> _onSelectionStatusChanged)
    {
        onZoneSelected = _onZoneSelected;
        zoneDatabase = _zoneDatabase;
        zoneInfo = _zoneInfo;
        onSelectionStatusChanged = _onSelectionStatusChanged;
        regions = new(_capacity);

        if (null == regionPrefab || null == slotContainer) return;

        for (int i = 0; i < _capacity; ++i)
        {
            UI_ZoneRegion region = Instantiate(regionPrefab, slotContainer).GetComponent<UI_ZoneRegion>();
            if (region != null)
            {
                region.SetVisible(false);
                regions.Add(region);
            }
        }
    }

    public void OpenRegion(int _regionId, int _zoneCount)
    {
        if (_regionId >= 0 && _regionId < regions.Count)
        {
            regions[_regionId].Initialize(_regionId, _zoneCount, HandleZoneClick, HandleZoneHoverEnter, HandleZoneHoverExit);
            regions[_regionId].SetVisible(true);

            // 요구사항: 창이 처음 열릴 때(지역이 개방될 때) 첫 번째 슬롯을 자동으로 클릭한 상태로 시작
            HandleZoneClick(_regionId, 0);
        }
    }

    public void UnlockZone(int _regionId, int _zoneId)
    {
        if (_regionId >= 0 && _regionId < regions.Count)
        {
            regions[_regionId].UnlockZone(_zoneId);
        }
    }

    private void HandleZoneClick(int _regionId, int _zoneId)
    {
        if (selectedRegionId == _regionId && selectedZoneId == _zoneId)
        {
            SetSlotHighlight(selectedRegionId, selectedZoneId, false);
            selectedRegionId = -1;
            selectedZoneId = -1;
            
            // 해제 시에도 정보창을 끄지 않고 마지막 정보를 유지합니다.
            onSelectionStatusChanged?.Invoke(false);
            onZoneSelected?.Invoke(DungeonType.None);
        }
        else
        {
            if (selectedRegionId != -1)
            {
                SetSlotHighlight(selectedRegionId, selectedZoneId, false);
            }

            selectedRegionId = _regionId;
            selectedZoneId = _zoneId;
            SetSlotHighlight(selectedRegionId, selectedZoneId, true);

            UpdateInfoDisplay(_regionId, _zoneId);
            onSelectionStatusChanged?.Invoke(true);

            ZoneData data = zoneDatabase.GetZoneData(_regionId, _zoneId);
            if (data != null)
            {
                onZoneSelected?.Invoke(data.DungeonType);
            }
        }
    }

    private void HandleZoneHoverEnter(int _regionId, int _zoneId)
    {
        if (selectedRegionId != -1) 
            return;
            
        UpdateInfoDisplay(_regionId, _zoneId);
    }

    private void HandleZoneHoverExit()
    {
        // 정보창을 숨기지 않고 마지막 정보를 유지합니다.
    }

    private void UpdateInfoDisplay(int _regionId, int _zoneId)
    {
        if (zoneDatabase == null || zoneInfo == null) return;
        
        ZoneData data = zoneDatabase.GetZoneData(_regionId, _zoneId);
        if (data != null) zoneInfo.Show(data);
    }

    private void SetSlotHighlight(int _regionId, int _zoneId, bool _active)
    {
        if (_regionId >= 0 && _regionId < regions.Count)
        {
            regions[_regionId].SetSlotHighlight(_zoneId, _active);
        }
    }

    public void OnShow() { gameObject.SetActive(true); }
    public void OnHide() { gameObject.SetActive(false); }
}
