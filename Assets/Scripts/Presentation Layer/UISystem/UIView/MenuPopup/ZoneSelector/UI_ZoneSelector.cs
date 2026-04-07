using UnityEngine;
using System.Collections.Generic;
using System;

public class UI_ZoneSelector : MonoBehaviour
{
    // 외부 의존성
    [Header("Pre-created Regions")]
    [SerializeField] private GameObject regionPrefab;
    [SerializeField] private Transform slotContainer;
    private List<UI_ZoneRegion> regions;

    // 내부 의존성
    private Action<int, int> onZoneSelected;

    public void Initialize(int _capacity, Action<int, int> _onZoneSelected)
    {
        onZoneSelected = _onZoneSelected;
        regions = new(_capacity);

        if (null == regionPrefab || null == slotContainer)
            return;

        for (int i = 0; i < _capacity; ++i)
        {
            UI_ZoneRegion region = Instantiate(regionPrefab, slotContainer).GetComponent<UI_ZoneRegion>();
            if (null == region)
                continue;

            region.SetVisible(false);
            regions.Add(region);
        }
    }

    // --- 지역(Region) 단위 개방 타이밍: 구역 개수를 함께 받아 초기화하며 오픈 ---
    public void OpenRegion(int _regionId, int _zoneCount)
    {
        if (_regionId >= 0 && _regionId < regions.Count)
        {
            // 시그니처 일치: (지역ID, 구역수, 콜백)
            regions[_regionId].Initialize(_regionId, _zoneCount, HandleZoneClick);
            regions[_regionId].SetVisible(true);
        }
    }

    public void UnlockZone(int _regionId, int _zoneId)
    {
        if (_regionId >= 0 && _regionId < regions.Count)
        {
            regions[_regionId].UnlockZone(_zoneId);
        }
    }

    public void OnShow()
    {
        gameObject.SetActive(true);
    }

    public void OnHide()
    {
        gameObject.SetActive(false);
    }

    private void HandleZoneClick(int _regionId, int _zoneId)
    {
        // 슬롯 -> 지역 -> 관리자로 전달된 클릭 신호를 최종 팝업으로 중계
        onZoneSelected?.Invoke(_regionId, _zoneId);
    }
}
