using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class HUD_PopupNav_SubRegionGroup : MonoBehaviour
{
    [Header("References")]
    [Tooltip("서브지역 버튼들이 자식으로 배치될 컨테이너 (이동/배치 기준점)")]
    [SerializeField] private RectTransform container;
    [Tooltip("기본 프리팹 (초기화 후 비활성화됨)")]
    [SerializeField] private HUD_PopupNav_SubRegionBtn subRegionBtnPrefab;
    [Tooltip("서브지역 팝업 시 대상 대지역과의 X 오프셋 간격")]
    [SerializeField] private float anchorOffsetX = 200f;
    [Tooltip("서브지역 노출 시 요소간 딜레이(순차 등장)")]
    [SerializeField] private float appearSequenceDelay = 0.1f;

    private HUD_PopupNav_Main mainController;
    private LocalizationManager localizationManager;
    private IMapDataProvider mapDataProvider;
    
    private readonly List<HUD_PopupNav_SubRegionBtn> subRegionButtons = new List<HUD_PopupNav_SubRegionBtn>(16);
    private readonly List<HUD_PopupNav_SubRegionBtn> activeSubRegionButtons = new List<HUD_PopupNav_SubRegionBtn>(8);

    private Coroutine sequenceCoroutine;
    private WaitForSeconds cachedSequenceWait;
    
    private MapType pendingMapType;
    private Transform pendingRegionTransform;

    public void Initialize(HUD_PopupNav_Main _mainController, LocalizationManager _localizationManager)
    {
        mainController = _mainController;
        localizationManager = _localizationManager;

        if (null != subRegionBtnPrefab)
        {
            subRegionBtnPrefab.gameObject.SetActive(false);
        }
        
        cachedSequenceWait = new WaitForSeconds(appearSequenceDelay);
    }

    public void ShowSubRegionsForMap(MapType _mapType, Transform _regionBtnTransform, IMapDataProvider _provider)
    {
        mapDataProvider = _provider;
        
        if (0 < activeSubRegionButtons.Count)
        {
            pendingMapType = _mapType;
            pendingRegionTransform = _regionBtnTransform;
            PlayDisappearSequence(OnDisappearSequenceCompleteForToggle);
        }
        else
        {
            SetupAndPlayAppearSequence(_mapType, _regionBtnTransform);
        }
    }

    private void OnDisappearSequenceCompleteForToggle()
    {
        SetupAndPlayAppearSequence(pendingMapType, pendingRegionTransform);
    }

    private void SetupAndPlayAppearSequence(MapType _mapType, Transform _regionBtnTransform)
    {
        if (null == mapDataProvider)
        {
            return;
        }

        MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == _db.mapDatas)
        {
            return;
        }

        activeSubRegionButtons.Clear();
        for (int i = 0; i < subRegionButtons.Count; i++)
        {
            subRegionButtons[i].gameObject.SetActive(false);
        }

        // 대상 대지역 정보 탐색
        for (int i = 0; i < _db.mapDatas.Count; i++)
        {
            if (_db.mapDatas[i].mapType == _mapType)
            {
                MapEnvironmentDataInfo _regionInfo = _db.mapDatas[i];
                if (null != _regionInfo.forestDatas)
                {
                    for (int j = 0; j < _regionInfo.forestDatas.Count; j++)
                    {
                        ForestEnvironmentInfo _subInfo = _regionInfo.forestDatas[j];
                        HUD_PopupNav_SubRegionBtn _btn = GetOrCreateSubRegionButton(j);
                        _btn.gameObject.SetActive(true);
                        _btn.Initialize(mainController, _subInfo, localizationManager, _regionInfo.mapType);
                        activeSubRegionButtons.Add(_btn);
                    }
                }
                break;
            }
        }

        // 컨테이너 앵커링 (해당 대지역 버튼 우측으로 이동)
        if (null != _regionBtnTransform && null != container)
        {
            container.position = _regionBtnTransform.position;
            container.anchoredPosition += new Vector2(anchorOffsetX, 0f);
        }

        // 순차 노출 애니메이션 재생
        PlayAppearSequence();
    }

    private HUD_PopupNav_SubRegionBtn GetOrCreateSubRegionButton(int _index)
    {
        if (_index < subRegionButtons.Count)
        {
            return subRegionButtons[_index];
        }

        HUD_PopupNav_SubRegionBtn _newBtn = Instantiate(subRegionBtnPrefab, container);
        subRegionButtons.Add(_newBtn);
        return _newBtn;
    }

    private void PlayDisappearSequence(Action _onComplete)
    {
        if (null != sequenceCoroutine)
        {
            StopCoroutine(sequenceCoroutine);
        }
        sequenceCoroutine = StartCoroutine(CoPlayDisappearSequence(_onComplete));
    }

    private System.Collections.IEnumerator CoPlayDisappearSequence(Action _onComplete)
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            activeSubRegionButtons[i].PlayDisappearMotion(null);
            yield return cachedSequenceWait;
        }

        // 마지막 애니메이션 종료 대기용 고정 딜레이 (애니메이션 길이에 맞춰 적절히)
        yield return new WaitForSeconds(0.3f);

        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            activeSubRegionButtons[i].gameObject.SetActive(false);
        }

        _onComplete?.Invoke();
    }

    private void PlayAppearSequence()
    {
        if (null != sequenceCoroutine)
        {
            StopCoroutine(sequenceCoroutine);
        }
        sequenceCoroutine = StartCoroutine(CoPlayAppearSequence());
    }

    private System.Collections.IEnumerator CoPlayAppearSequence()
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            activeSubRegionButtons[i].PlayAppearMotion();
            yield return cachedSequenceWait;
        }
    }

    public void PlayUnlockProduction(ForestType _forestType, Action _onComplete)
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            if (activeSubRegionButtons[i].GetForestType() == _forestType)
            {
                activeSubRegionButtons[i].PlayUnlockMotion(_onComplete);
                return;
            }
        }
        
        _onComplete?.Invoke();
    }

    public void SetSelectSubRegion(ForestType _forestType)
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            activeSubRegionButtons[i].SetSelectedState(_forestType == activeSubRegionButtons[i].GetForestType());
        }
    }

    public void ClearAllNewIndicators()
    {
        for (int i = 0; i < subRegionButtons.Count; i++)
        {
            if (true == subRegionButtons[i].gameObject.activeSelf)
            {
                subRegionButtons[i].ClearNewIndicator();
            }
        }
    }
}
