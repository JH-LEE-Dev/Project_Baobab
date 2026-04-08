using UnityEngine;
using System.Collections.Generic;
using System;

public class UI_ZoneSelector : MonoBehaviour
{
    [Header("Pre-created Regions")]
    [SerializeField] private GameObject regionPrefab;
    [SerializeField] private Transform slotContainer;
    private List<UI_ZoneRegion> regions;

    private Action<int, int> onZoneSelected;
    private Action<bool> onSelectionStatusChanged;
    private ZoneDatabase zoneDatabase;
    private UI_ZoneInfo zoneInfo;

    private int selectedRegionId = -1;
    private int selectedZoneId = -1;

    public void Initialize(int _capacity, Action<int, int> _onZoneSelected, ZoneDatabase _zoneDatabase, UI_ZoneInfo _zoneInfo, Action<bool> _onSelectionStatusChanged)
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

            // 초기 로딩 시 첫 번째 슬롯 자동 선택
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
            
            onSelectionStatusChanged?.Invoke(false);
            // 선택 해제 시에는 현재 정보창을 유지하거나 초기화할 수 있습니다.
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
        }

        onZoneSelected?.Invoke(_regionId, _zoneId);
    }

    private void HandleZoneHoverEnter(int _regionId, int _zoneId)
    {
        // 고정 여부와 상관없이 마우스가 올라간 슬롯의 정보를 표시합니다.
        UpdateInfoDisplay(_regionId, _zoneId);
    }

    private void HandleZoneHoverExit()
    {
        // 마우스가 슬롯을 벗어났을 때, 고정된(선택된) 슬롯이 있다면 그 정보로 되돌립니다.
        if (selectedRegionId != -1)
        {
            UpdateInfoDisplay(selectedRegionId, selectedZoneId);
        }
        // 고정된 슬롯이 없다면 마지막 호버 정보를 그대로 유지합니다.
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
