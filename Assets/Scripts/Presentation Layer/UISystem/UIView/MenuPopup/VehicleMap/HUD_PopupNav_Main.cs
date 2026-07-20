using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_PopupNav_Main : MonoBehaviour, IPointerClickHandler
{
    // 외부 의존성
    [Header("UI References")]
    [Tooltip("빈 배경(Dim) 클릭 감지용 영역 (레이캐스트용 이미지)")]
    [SerializeField] private Image backgroundDimImage;
    [Tooltip("서브지역 선택 시 노출될 확정(이동) 버튼 (레이캐스트용 이미지)")]
    [SerializeField] private Image confirmCheckImage;
    [Tooltip("닫기 버튼 (레이캐스트용 이미지)")]
    [SerializeField] private Image closeImage;

    [Header("Navigation Groups")]
    [Tooltip("대지역 관리 그룹")]
    [SerializeField] private HUD_PopupNav_RegionGroup regionGroup;
    [Tooltip("서브지역 관리 그룹")]
    [SerializeField] private HUD_PopupNav_SubRegionGroup subRegionGroup;
    [Tooltip("식생 정보 관리 팝업")]
    [SerializeField] private HUD_PopupNav_TreeInfoView treeInfoView;

    [Header("DOTween Settings (Placeholders)")]
    [Tooltip("팝업 등장/퇴장 트위닝 관련 설정")]
    [SerializeField] private float appearDuration = 0.5f;
    [SerializeField] private float disappearDuration = 0.5f;

    private Tween appearTween;
    private Tween disappearTween;

    [Header("Settings")]
    [Tooltip("해금 연출 기본 지연 시간")]
    [SerializeField] private float defaultUnlockDelay = 1.0f;
    [Tooltip("다중 대지역 해금 시 배속 (예: 2배속이면 2.0)")]
    [SerializeField] private float multiRegionUnlockSpeedRate = 2.0f;

    // 내부 의존성
    private IMapDataProvider mapDataProvider;
    private LocalizationManager localizationManager;
    private Action onNavigationClosedCallback;
    private Action<MapType, ForestType> onConfirmMapSelectedCallback;
    private Tween delayedCallTween;
    
    // 세션 유지 데이터 (게임 실행 중 유지)
    private static MapType runtimeLastSelectedMapType = MapType.None;

    // 상태 변수
    private bool isUnlockingProductionActive = false;
    private bool isClosing = false;
    private bool isInputBlocked = false;
    private ForestType currentSelectedForestType = ForestType.None;
    private MapType currentSelectedMapType = MapType.None;

    // 언락 큐 구조체
    private struct UnlockInfo
    {
        public bool isRegion;
        public MapType mapType;
        public ForestType forestType;
    }
    
    private readonly List<UnlockInfo> regionUnlockList = new List<UnlockInfo>(4);
    private readonly List<UnlockInfo> subRegionUnlockList = new List<UnlockInfo>(8);

    private UnlockInfo pendingSubRegionUnlockInfo;
    private MapType pendingRegionUnlockMapType = MapType.None;
    private float cachedUnlockSpeedRate = 1.0f;

    public bool IsInputBlocked => isInputBlocked || isUnlockingProductionActive || isClosing;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(IMapDataProvider _provider, LocalizationManager _localizer, Action _onClose, Action<MapType, ForestType> _onConfirm)
    {
        mapDataProvider = _provider;
        localizationManager = _localizer;
        onNavigationClosedCallback = _onClose;
        onConfirmMapSelectedCallback = _onConfirm;

        if (null != regionGroup)
        {
            regionGroup.Initialize(this, localizationManager);
        }

        if (null != subRegionGroup)
        {
            subRegionGroup.Initialize(this, localizationManager);
        }

        if (null != treeInfoView)
        {
            treeInfoView.Initialize();
            treeInfoView.SetVisibility(false);
        }

        if (null != confirmCheckImage)
        {
            confirmCheckImage.gameObject.SetActive(false);
        }
    }

    public void Open()
    {
        gameObject.SetActive(true);
        isClosing = false;
        isInputBlocked = true;
        isUnlockingProductionActive = false;
        currentSelectedForestType = ForestType.None;
        
        if (null != confirmCheckImage)
        {
            confirmCheckImage.gameObject.SetActive(false);
        }
        
        if (null != treeInfoView)
        {
            treeInfoView.SetVisibility(false);
        }

        InitFirstPlayableRegionUnlock();

        if (null != appearTween && true == appearTween.IsActive())
        {
            appearTween.Kill();
        }

        // [TODO] 추후 전체 팝업 DOTween 등장 연출 작성
        // appearTween = ...
        
        // 임시 즉시 완료
        OnMainPopupAppearComplete();
    }

    public void Close()
    {
        if (true == isClosing)
        {
            return;
        }

        isClosing = true;
        isInputBlocked = true;

        if (null != delayedCallTween && true == delayedCallTween.IsActive())
        {
            delayedCallTween.Kill();
            delayedCallTween = null;
        }

        if (null != subRegionGroup)
        {
            subRegionGroup.ClearAllNewIndicators();
        }

        if (null != regionGroup)
        {
            regionGroup.ClearAllNewIndicators();
        }

        if (null != disappearTween && true == disappearTween.IsActive())
        {
            disappearTween.Kill();
        }

        // [TODO] 추후 전체 팝업 DOTween 퇴장 연출 작성
        // disappearTween = ...

        // 임시 즉시 완료
        OnMainPopupDisappearComplete();
    }

    // 내부 이벤트 핸들러
    private void OnBackgroundDimClicked()
    {
        if (true == IsInputBlocked)
        {
            return;
        }
        Close();
    }

    private void OnCloseButtonClicked()
    {
        if (true == IsInputBlocked)
        {
            return;
        }
        Close();
    }

    private void OnConfirmCheckButtonClicked()
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        if (MapType.None != currentSelectedMapType && ForestType.None != currentSelectedForestType)
        {
            onConfirmMapSelectedCallback?.Invoke(currentSelectedMapType, currentSelectedForestType);
            Close();
        }
    }

    private void OnMainPopupAppearComplete()
    {
        isInputBlocked = false;

        if (null != regionGroup)
        {
            regionGroup.SetupRegions(mapDataProvider);
        }

        BuildUnlockQueues();

        if (0 < regionUnlockList.Count || 0 < subRegionUnlockList.Count)
        {
            ProcessNextUnlock();
        }
        else
        {
            RestoreSessionState();
        }
    }

    private void OnMainPopupDisappearComplete()
    {
        gameObject.SetActive(false);
        onNavigationClosedCallback?.Invoke();
    }

    // 내부 로직 - 해금 관리
    private void InitFirstPlayableRegionUnlock()
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

        for (int i = 0; i < _db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo _regionInfo = _db.mapDatas[i];
            if (MapType.Town == _regionInfo.mapType)
            {
                continue;
            }

            if (false == _regionInfo.isUnlocked)
            {
                mapDataProvider.MarkMapUnlocked(_regionInfo.mapType);
                mapDataProvider.MarkMapUnlockAnimationPlayed(_regionInfo.mapType);
                mapDataProvider.MarkMapLevelAsRead(_regionInfo.mapType);
                
                if (null != _regionInfo.forestDatas && 0 < _regionInfo.forestDatas.Count)
                {
                    ForestType _firstSubRegion = _regionInfo.forestDatas[0].forestType;
                    mapDataProvider.MarkUnlocked(_regionInfo.mapType, _firstSubRegion);
                    mapDataProvider.MarkUnlockAnimationPlayed(_regionInfo.mapType, _firstSubRegion);
                    mapDataProvider.MarkMapAsRead(_regionInfo.mapType, _firstSubRegion);
                }
            }
            break; // 첫 번째 플레이 가능한 지역 하나만 체크하고 종료
        }
    }

    private void BuildUnlockQueues()
    {
        regionUnlockList.Clear();
        subRegionUnlockList.Clear();

        if (null == mapDataProvider)
        {
            return;
        }

        MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == _db.mapDatas)
        {
            return;
        }

        for (int i = 0; i < _db.mapDatas.Count; i++)
        {
            MapEnvironmentDataInfo _regionInfo = _db.mapDatas[i];
            if (MapType.Town == _regionInfo.mapType)
            {
                continue;
            }

            if (true == _regionInfo.bCanAccess && false == _regionInfo.isUnlocked)
            {
                UnlockInfo _info;
                _info.isRegion = true;
                _info.mapType = _regionInfo.mapType;
                _info.forestType = ForestType.None;
                regionUnlockList.Add(_info);
            }

            if (null != _regionInfo.forestDatas)
            {
                for (int j = 0; j < _regionInfo.forestDatas.Count; j++)
                {
                    ForestEnvironmentInfo _subInfo = _regionInfo.forestDatas[j];
                    if (true == _subInfo.bCanAccess && false == _subInfo.isUnlocked)
                    {
                        UnlockInfo _subUnlockInfo;
                        _subUnlockInfo.isRegion = false;
                        _subUnlockInfo.mapType = _regionInfo.mapType;
                        _subUnlockInfo.forestType = _subInfo.forestType;
                        subRegionUnlockList.Add(_subUnlockInfo);
                    }
                }
            }
        }
    }

    private void ProcessNextUnlock()
    {
        // 1순위: 대지역 해금이 1개 이상 존재할 경우
        if (0 < regionUnlockList.Count)
        {
            // 서브지역 해금은 모두 스킵 처리 (조용히 해금)
            for (int i = 0; i < subRegionUnlockList.Count; i++)
            {
                UnlockInfo _sub = subRegionUnlockList[i];
                mapDataProvider.MarkUnlocked(_sub.mapType, _sub.forestType);
                mapDataProvider.MarkUnlockAnimationPlayed(_sub.mapType, _sub.forestType);
            }
            subRegionUnlockList.Clear();

            isUnlockingProductionActive = true;
            bool _isMultiRegion = 1 < regionUnlockList.Count;
            float _speedRate = true == _isMultiRegion ? multiRegionUnlockSpeedRate : 1.0f;
            
            PlayNextRegionUnlock(_speedRate);
            return;
        }

        // 2순위: 서브지역 해금만 존재할 경우
        if (0 < subRegionUnlockList.Count)
        {
            isUnlockingProductionActive = true;
            
            // 가장 나중에 갈 수 있는 최신 맵 1개만 추출 (마지막 요소)
            UnlockInfo _latestSub = subRegionUnlockList[subRegionUnlockList.Count - 1];
            
            // 나머지는 스킵 처리
            for (int i = 0; i < subRegionUnlockList.Count - 1; i++)
            {
                UnlockInfo _skipSub = subRegionUnlockList[i];
                mapDataProvider.MarkUnlocked(_skipSub.mapType, _skipSub.forestType);
                mapDataProvider.MarkUnlockAnimationPlayed(_skipSub.mapType, _skipSub.forestType);
            }
            subRegionUnlockList.Clear();

            // 최신 서브지역 해금 연출을 위해 해당 대지역으로 자동 전환
            HandleRegionSelected(_latestSub.mapType);
            
            // 트랜지션 완료를 기다린 후 서브지역 자물쇠 파괴 연출 진행
            float _delay = defaultUnlockDelay;
            pendingSubRegionUnlockInfo = _latestSub;
            delayedCallTween = DOVirtual.DelayedCall(_delay, OnSubRegionUnlockDelayComplete).SetEase(Ease.Linear);
        }
    }

    private void OnSubRegionUnlockDelayComplete()
    {
        mapDataProvider.MarkUnlocked(pendingSubRegionUnlockInfo.mapType, pendingSubRegionUnlockInfo.forestType);
        if (null != subRegionGroup)
        {
            subRegionGroup.PlayUnlockProduction(pendingSubRegionUnlockInfo.forestType, OnSubRegionUnlockMotionComplete);
        }
        else
        {
            OnSubRegionUnlockMotionComplete();
        }
    }

    private void OnSubRegionUnlockMotionComplete()
    {
        mapDataProvider.MarkUnlockAnimationPlayed(pendingSubRegionUnlockInfo.mapType, pendingSubRegionUnlockInfo.forestType);
        isUnlockingProductionActive = false;
    }

    public void OnPointerClick(PointerEventData _eventData)
    {
        if (null == _eventData || null == _eventData.pointerPress)
        {
            return;
        }

        GameObject _pressedObj = _eventData.pointerPress;

        if (null != backgroundDimImage && _pressedObj == backgroundDimImage.gameObject)
        {
            OnBackgroundDimClicked();
        }
        else if (null != confirmCheckImage && _pressedObj == confirmCheckImage.gameObject)
        {
            OnConfirmCheckButtonClicked();
        }
        else if (null != closeImage && _pressedObj == closeImage.gameObject)
        {
            OnCloseButtonClicked();
        }
    }

    private void PlayNextRegionUnlock(float _speedRate)
    {
        if (0 == regionUnlockList.Count)
        {
            isUnlockingProductionActive = false;
            RestoreSessionState();
            return;
        }

        UnlockInfo _target = regionUnlockList[0];
        regionUnlockList.RemoveAt(0);

        mapDataProvider.MarkMapUnlocked(_target.mapType);
        pendingRegionUnlockMapType = _target.mapType;
        cachedUnlockSpeedRate = _speedRate;

        if (null != regionGroup)
        {
            regionGroup.PlayUnlockProduction(_target.mapType, _speedRate, OnRegionUnlockProductionComplete);
        }
        else
        {
            OnRegionUnlockProductionComplete();
        }
    }

    private void OnRegionUnlockProductionComplete()
    {
        mapDataProvider.MarkMapUnlockAnimationPlayed(pendingRegionUnlockMapType);
        PlayNextRegionUnlock(cachedUnlockSpeedRate);
    }

    private void RestoreSessionState()
    {
        if (MapType.None != runtimeLastSelectedMapType)
        {
            HandleRegionSelected(runtimeLastSelectedMapType);
        }
        else
        {
            // 아직 한 번도 선택 안 했다면 첫 지역 선택
            if (null != mapDataProvider)
            {
                MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
                if (null != _db.mapDatas)
                {
                    for (int i = 0; i < _db.mapDatas.Count; i++)
                    {
                        if (MapType.Town != _db.mapDatas[i].mapType)
                        {
                            HandleRegionSelected(_db.mapDatas[i].mapType);
                            break;
                        }
                    }
                }
            }
        }
    }

    // 퍼블릭 콜백 핸들러 (버튼들에서 호출)
    public void HandleRegionSelected(MapType _mapType)
    {
        if (true == IsInputBlocked && false == isUnlockingProductionActive)
        {
            return;
        }

        if (_mapType == currentSelectedMapType)
        {
            // 동일한 대지역 재클릭 무시 (토글 안함)
            return;
        }

        currentSelectedMapType = _mapType;
        runtimeLastSelectedMapType = _mapType;
        currentSelectedForestType = ForestType.None;

        if (null != confirmCheckImage)
        {
            confirmCheckImage.gameObject.SetActive(false);
        }

        if (null != treeInfoView)
        {
            treeInfoView.SetVisibility(false);
        }

        if (null != regionGroup)
        {
            regionGroup.SetSelectRegion(_mapType);
            Transform _regionBtnTransform = regionGroup.GetRegionTransform(_mapType);
            
            if (null != subRegionGroup)
            {
                subRegionGroup.ShowSubRegionsForMap(_mapType, _regionBtnTransform, mapDataProvider);
            }
        }
    }

    public void HandleSubRegionHovered(ForestType _forestType, Transform _subRegionTransform, ForestEnvironmentInfo _info)
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        if (null != treeInfoView)
        {
            treeInfoView.ShowTreeInfo(_info, _subRegionTransform);
        }
    }

    public void HandleSubRegionUnhovered()
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        if (null != treeInfoView)
        {
            treeInfoView.SetVisibility(false);
        }
    }

    public void HandleSubRegionSelected(ForestType _forestType)
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        currentSelectedForestType = _forestType;

        if (null != subRegionGroup)
        {
            subRegionGroup.SetSelectSubRegion(_forestType);
        }

        if (null != confirmCheckImage && false == confirmCheckImage.gameObject.activeSelf)
        {
            confirmCheckImage.gameObject.SetActive(true);
        }
    }
}
