using UnityEngine;
using System.Collections.Generic;
using System;

public class UI_ZoneRegion : MonoBehaviour
{
    // 외부 의존성
    [Header("Prefabs & Containers")]
    [SerializeField] private UI_ZoneSelectSlot slotPrefab;
    [SerializeField] private Transform slotContainer;

    // 내부 의존성
    private List<UI_ZoneSelectSlot> zoneSlots = new List<UI_ZoneSelectSlot>();
    private int regionId;

    public void Initialize(int _regionId, int _zoneCount, Action<int, int> _onZoneClick)
    {
        regionId = _regionId;
        
        // 타이밍 1: 지역마다 다른 구역 수(_zoneCount)에 맞춰 슬롯들을 동적으로 생성
        for (int i = 0; i < _zoneCount; ++i)
        {
            if (slotPrefab == null || slotContainer == null) 
                break;

            UI_ZoneSelectSlot slot = Instantiate(slotPrefab, slotContainer);
            // 구역 명칭 생성 (예: Region 0의 1번째 구역 -> "1-1")
            string zoneName = $"{regionId + 1}-{i + 1}";
            
            // 초기 상태는 모두 잠금(true)으로 생성
            slot.Initialize(regionId, i, zoneName, true, _onZoneClick);
            zoneSlots.Add(slot);
        }
    }

    public void UnlockZone(int _zoneId)
    {
        // 타이밍 2: 이 지역 내부의 특정 구역을 잠금 해제
        if (_zoneId >= 0 && _zoneId < zoneSlots.Count)
        {
            zoneSlots[_zoneId].SetLockStatus(false);
        }
    }

    public void SetVisible(bool _visible)
    {
        // 타이밍 3: 지역 묶음 전체의 가시성 제어
        gameObject.SetActive(_visible);
    }

    public void OnShow() 
    { 
        /* 지역 활성화 시 연출 */ 
    }
    
    public void OnHide() 
    { 
        /* 지역 비활성화 시 상태 정리 */ 
    }
}
