using System;
using System.Collections.Generic;
using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_PopupNav_RegionGroup : MonoBehaviour
{
    [Header("References")]
    [Tooltip("대지역 버튼 부모(컨테이너)")]
    [SerializeField] private RectTransform container;
    [Tooltip("기본 프리팹 (초기화 후 비활성화됨)")]
    [SerializeField] private HUD_PopupNav_RegionBtn regionBtnPrefab;

    private HUD_PopupNav_Main mainController;
    private LocalizationManager localizationManager;
    private readonly List<HUD_PopupNav_RegionBtn> regionButtons = new List<HUD_PopupNav_RegionBtn>(8);

    public void Initialize(HUD_PopupNav_Main _mainController, LocalizationManager _localizationManager)
    {
        mainController = _mainController;
        localizationManager = _localizationManager;

        if (null != regionBtnPrefab)
        {
            regionBtnPrefab.gameObject.SetActive(false);
        }
    }

    public void SetupRegions(IMapDataProvider _mapDataProvider)
    {
        if (null == _mapDataProvider)
        {
            return;
        }

        MapEnvironmentDatabase _db = _mapDataProvider.GetMapEnvironmentDatabase();
        if (null == _db.mapDatas)
        {
            return;
        }

        // 기존 생성된 버튼 초기화 및 비활성화
        for (int i = 0; i < regionButtons.Count; i++)
        {
            regionButtons[i].gameObject.SetActive(false);
        }

        int _btnIndex = 0;
        for (int i = 0; i < _db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo _regionInfo = _db.mapDatas[i];
            if (MapType.Town == _regionInfo.mapType)
            {
                continue;
            }

            HUD_PopupNav_RegionBtn _btn = GetOrCreateRegionButton(_btnIndex);
            _btn.gameObject.SetActive(true);
            _btn.Initialize(mainController, _regionInfo, localizationManager);
            _btnIndex++;
        }
    }

    private HUD_PopupNav_RegionBtn GetOrCreateRegionButton(int _index)
    {
        if (_index < regionButtons.Count)
        {
            return regionButtons[_index];
        }

        HUD_PopupNav_RegionBtn _newBtn = Instantiate(regionBtnPrefab, container);
        regionButtons.Add(_newBtn);
        return _newBtn;
    }

    public void PlayUnlockProduction(MapType _mapType, float _speedRate, Action _onComplete)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (regionButtons[i].GetMapType() == _mapType)
            {
                regionButtons[i].PlayUnlockMotion(_onComplete);
                return;
            }
        }
        
        _onComplete?.Invoke();
    }

    public void SetSelectRegion(MapType _mapType)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            regionButtons[i].SetSelectedState(_mapType == regionButtons[i].GetMapType());
        }
    }

    public Transform GetRegionTransform(MapType _mapType)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (regionButtons[i].GetMapType() == _mapType)
            {
                return regionButtons[i].transform;
            }
        }
        return container;
    }

    public void ClearAllNewIndicators()
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (true == regionButtons[i].gameObject.activeSelf)
            {
                regionButtons[i].ClearNewIndicator();
            }
        }
    }
}
