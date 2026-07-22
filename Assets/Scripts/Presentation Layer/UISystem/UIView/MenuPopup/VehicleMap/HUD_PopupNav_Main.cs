using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;
using TMPro;

public class HUD_PopupNav_Main : MonoBehaviour
{
    // 외부 의존성
    [Header("Nav Image Animation")]
    [Tooltip("내비게이션 이미지 전체를 묶는 컨테이너 (아래에서 위로 등장)")]
    [SerializeField] private RectTransform navImageContainer;
    [Tooltip("내비게이션 이미지 바운스 업 연출 시간")]
    [SerializeField] private float navImageBounceDuration = 0.35f;
    [Tooltip("내비게이션 이미지 바운스 업 이즈(Ease)")]
    [SerializeField] private Ease navImageBounceEase = Ease.OutBack;

    [Header("Dim Background Animation")]
    [Tooltip("빈 배경(Dim) 클릭 감지용 영역 (레이캐스트용 이미지)")]
    [SerializeField] private Image backgroundDimImage;
    [Tooltip("빈 배경(Dim) (알파값 등장용)")]
    [SerializeField] private CanvasGroup dimBackgroundCanvasGroup;
    [Tooltip("DimBG 알파 페이드 인 연출 시간")]
    [SerializeField] private float dimFadeDuration = 0.2f;
    [Tooltip("DimBG 알파 페이드 인 이즈(Ease)")]
    [SerializeField] private Ease dimFadeEase = Ease.Linear;

    [Header("Interactive UI Panel Animation")]
    [Tooltip("상호작용 가능한 UI 요소들을 담고 있는 배경 패널 (Y 스케일 등장)")]
    [SerializeField] private RectTransform interactiveUIPanel;
    [Tooltip("상호작용 패널 Y 스케일 쫀득한 펴짐 연출 시간")]
    [SerializeField] private float panelScaleDuration = 0.3f;
    [Tooltip("상호작용 패널 Y 스케일 쫀득한 펴짐 이즈(Ease)")]
    [SerializeField] private Ease panelScaleEase = Ease.OutBack;
    [Tooltip("버튼들 순차 등장 시작 전 대기 딜레이")]
    [SerializeField] private float delayBeforeButtons = 0.01f;

    [Header("Extra UI (Close / Confirm) Animation")]
    [Tooltip("서브지역 선택 시 노출될 확정(이동) 버튼 (레이캐스트용 이미지)")]
    [SerializeField] private Image confirmCheckImage;
    [Tooltip("닫기 버튼 (레이캐스트용 이미지)")]
    [SerializeField] private Image closeImage;
    [Tooltip("닫기 버튼 등 부가 UI 연출 시간")]
    [SerializeField] private float extraUIAnimDuration = 0.2f;
    [Tooltip("닫기 버튼 등 부가 UI 연출 이즈(Ease)")]
    [SerializeField] private Ease extraUIAnimEase = Ease.OutBack;

    [Header("Region Name UI")]
    [Tooltip("현재 선택된 대지역 이름을 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI currentRegionNameText;

    [Header("Navigation Groups")]
    [Tooltip("대지역 관리 그룹")]
    [SerializeField] private HUD_PopupNav_RegionGroup regionGroup;
    [Tooltip("서브지역 관리 그룹")]
    [SerializeField] private HUD_PopupNav_SubRegionGroup subRegionGroup;
    [Tooltip("식생 정보 관리 팝업")]
    [SerializeField] private HUD_PopupNav_TreeInfoView treeInfoView;

    [Header("Popup Lifecycle Animation")]
    [Tooltip("팝업 등장 트위닝 소요 시간 (기존)")]
    [SerializeField] private float appearDuration = 0.5f;
    [Tooltip("팝업 퇴장 트위닝 소요 시간 (기존)")]
    [SerializeField] private float disappearDuration = 0.5f;

    private Tween appearTween;
    private Tween disappearTween;

    [Header("Settings")]
    [Tooltip("해금 연출 기본 지연 시간")]
    [SerializeField] private float defaultUnlockDelay = 1.0f;
    [Tooltip("다중 대지역 해금 시 배속 (예: 2배속이면 2.0)")]
    [SerializeField] private float multiRegionUnlockSpeedRate = 2.0f;

    [Header("Debug")]
    [Tooltip("체크 시 내비게이션을 열 때 모든 지역 및 서브지역을 강제로 해금 처리합니다.")]
    [SerializeField] private bool debugForceUnlockAll = false;

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

    // 캐싱된 델리게이트 (GC Alloc 방지)
    private TweenCallback onAppearMidwayCallback;
    private TweenCallback onAppearCompleteCallback;
    private TweenCallback onSubRegionUnlockDelayCompleteCallback;
    
    private UnityEngine.Events.UnityAction onBackgroundDimClickedAction;
    private UnityEngine.Events.UnityAction onCloseButtonClickedAction;
    private UnityEngine.Events.UnityAction onConfirmCheckButtonClickedAction;

    public bool IsInputBlocked => isInputBlocked || isUnlockingProductionActive || isClosing;
    public bool IsUnlockingProductionActive => isUnlockingProductionActive;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(IMapDataProvider _provider, LocalizationManager _localizer, Action _onClose, Action<MapType, ForestType> _onConfirm)
    {
        mapDataProvider = _provider;
        localizationManager = _localizer;
        onNavigationClosedCallback = _onClose;
        onConfirmMapSelectedCallback = _onConfirm;

        onAppearMidwayCallback = OnAppearMidway;
        onAppearCompleteCallback = OnAppearComplete;
        onSubRegionUnlockDelayCompleteCallback = OnSubRegionUnlockDelayComplete;
        
        onBackgroundDimClickedAction = OnBackgroundDimClicked;
        onCloseButtonClickedAction = OnCloseButtonClicked;
        onConfirmCheckButtonClickedAction = OnConfirmCheckButtonClicked;

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

        BindClickEvents();
    }

    public class SimpleClickHandler : MonoBehaviour, IPointerClickHandler
    {
        public UnityEngine.Events.UnityAction onClick;
        public void OnPointerClick(PointerEventData eventData) { onClick?.Invoke(); }
    }

    private void AddClickListener(GameObject _go, UnityEngine.Events.UnityAction _action)
    {
        if (null == _go) return;
        Button _btn = _go.GetComponent<Button>();
        if (null != _btn)
        {
            _btn.onClick.RemoveAllListeners();
            _btn.onClick.AddListener(_action);
        }
        else
        {
            SimpleClickHandler _handler = _go.GetComponent<SimpleClickHandler>();
            if (null == _handler) _handler = _go.AddComponent<SimpleClickHandler>();
            _handler.onClick = _action;
        }
    }

    private void BindClickEvents()
    {
        if (null != backgroundDimImage) AddClickListener(backgroundDimImage.gameObject, onBackgroundDimClickedAction);
        if (null != confirmCheckImage) AddClickListener(confirmCheckImage.gameObject, onConfirmCheckButtonClickedAction);
        if (null != closeImage) AddClickListener(closeImage.gameObject, onCloseButtonClickedAction);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        isClosing = false;
        isInputBlocked = true;
        isUnlockingProductionActive = false;
        currentSelectedMapType = MapType.None;
        currentSelectedForestType = ForestType.None;
        
        if (null != confirmCheckImage)
        {
            confirmCheckImage.gameObject.SetActive(false);
        }
        
        if (null != treeInfoView)
        {
            treeInfoView.SetVisibility(false);
        }

        if (debugForceUnlockAll && null != mapDataProvider)
        {
            MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
            if (null != _db.mapDatas)
            {
                for (int i = 0; i < _db.mapDatas.Count; i++)
                {
                    mapDataProvider.MarkMapUnlocked(_db.mapDatas[i].mapType);
                    mapDataProvider.MarkMapUnlockAnimationPlayed(_db.mapDatas[i].mapType);
                    if (null != _db.mapDatas[i].forestDatas)
                    {
                        for (int j = 0; j < _db.mapDatas[i].forestDatas.Count; j++)
                        {
                            mapDataProvider.MarkUnlocked(_db.mapDatas[i].mapType, _db.mapDatas[i].forestDatas[j].forestType);
                            mapDataProvider.MarkUnlockAnimationPlayed(_db.mapDatas[i].mapType, _db.mapDatas[i].forestDatas[j].forestType);
                        }
                    }
                }
            }
        }

        InitFirstPlayableRegionUnlock();

        if (null != appearTween && true == appearTween.IsActive())
        {
            appearTween.Kill();
        }

        // 초기 상태 세팅
        if (null != navImageContainer)
        {
            // 임시로 아래로 200px 정도 내림
            navImageContainer.anchoredPosition = new Vector2(navImageContainer.anchoredPosition.x, -200f);
        }
        
        if (null != dimBackgroundCanvasGroup)
        {
            dimBackgroundCanvasGroup.alpha = 0f;
        }

        if (null != interactiveUIPanel)
        {
            interactiveUIPanel.localScale = new Vector3(1f, 0f, 1f);
        }

        if (null != closeImage) closeImage.transform.localScale = Vector3.zero;
        if (null != confirmCheckImage) confirmCheckImage.transform.localScale = Vector3.zero;

        // 시퀀스 생성 전, 대지역 버튼 생성 및 초기화(스케일 0 세팅)를 미리 수행해야 
        // 하단의 PlayAppearSequence() 에서 DOTween 시퀀스를 정상적으로 조립할 수 있습니다.
        if (null != regionGroup)
        {
            regionGroup.SetupRegions(mapDataProvider);
        }

        Sequence _seq = DOTween.Sequence();
        
        // 1. 내비게이션 이미지용 UI가 아래에서 위로 스무스하게 등장 (살짝 바운스)
        if (null != navImageContainer)
        {
            _seq.Append(navImageContainer.DOAnchorPosY(0f, navImageBounceDuration).SetEase(navImageBounceEase));
        }

        // 2. 빠르게 DimBG 알파 증가 (내비게이션용 이미지를 가려줌)
        if (null != dimBackgroundCanvasGroup)
        {
            _seq.Insert(navImageBounceDuration * 0.5f, dimBackgroundCanvasGroup.DOFade(1f, dimFadeDuration).SetEase(dimFadeEase));
        }

        // 3. 상호작용 가능한 UI BG 패널 Y 스케일 쫀득하게 펼쳐짐
        if (null != interactiveUIPanel)
        {
            _seq.Insert(navImageBounceDuration, interactiveUIPanel.DOScaleY(1f, panelScaleDuration).SetEase(panelScaleEase));
        }

        // 4. Region 버튼들 순차 등장
        // (DOTween 시퀀스는 미리 구성되므로 SetupRegions는 이전에 호출하여 버튼들이 생성되어 있어야 합니다)
        _seq.AppendInterval(delayBeforeButtons);
        if (null != regionGroup)
        {
            _seq.Append(regionGroup.PlayAppearSequence());
        }

        // 5. 첫번째 대지역 선택 및 서브지역 연출 준비 (OnMainPopupAppearComplete 역할 포함)
        _seq.AppendCallback(onAppearMidwayCallback);

        // 6. 닫기/확인 버튼 등장
        if (null != closeImage)
        {
            _seq.Append(closeImage.transform.DOScale(1f, extraUIAnimDuration).SetEase(extraUIAnimEase));
        }

        _seq.OnComplete(onAppearCompleteCallback);

        appearTween = _seq;
    }

    private void OnAppearMidway()
    {
        InitFirstPlayableRegionUnlock();
        OnMainPopupAppearCompleteForAnimation();
    }

    private void OnAppearComplete()
    {
        isInputBlocked = false;
    }

    private void OnMainPopupAppearCompleteForAnimation()
    {
        // 원래 Open() 끝에서 호출되던 로직
        BuildUnlockQueues();

        if (0 < regionUnlockList.Count || 0 < subRegionUnlockList.Count)
        {
            ProcessNextUnlock();
        }
        else
        {
            RestoreSessionState(); // 내부에서 HandleRegionSelected 호출 -> 서브지역 생성 및 자체 연출 재생 완료됨
        }
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
            subRegionGroup.ResetState();
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
        // OnMainPopupAppearCompleteForAnimation 으로 대체됨 (이 메서드는 이제 직접 호출되지 않음)
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
            delayedCallTween = DOVirtual.DelayedCall(_delay, onSubRegionUnlockDelayCompleteCallback).SetEase(Ease.Linear);
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

    // IPointerClickHandler 구현은 개별 컴포넌트(SimpleClickHandler)로 위임하여 제거함

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
            HandleRegionSelected(runtimeLastSelectedMapType, true, false);
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
                            HandleRegionSelected(_db.mapDatas[i].mapType, true, false);
                            break;
                        }
                    }
                }
            }
        }
    }

    // 퍼블릭 콜백 핸들러 (버튼들에서 호출)
    public void HandleRegionSelected(MapType _mapType, bool _force = false, bool _playClickAnim = true)
    {
        if (false == _force && true == IsInputBlocked && false == isUnlockingProductionActive)
        {
            return;
        }

        if (currentSelectedMapType == _mapType)
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

        if (null != currentRegionNameText && null != localizationManager)
        {
            string _localizedName = localizationManager.GetText(_mapType);
            if (false == string.IsNullOrEmpty(_localizedName))
            {
                currentRegionNameText.text = _localizedName;
            }
        }

        if (null != regionGroup)
        {
            regionGroup.SetSelectRegion(_mapType, _playClickAnim);
            Transform _regionBtnTransform = regionGroup.GetRegionTransform(_mapType);
            
            if (null != subRegionGroup)
            {
                subRegionGroup.ShowSubRegionsForMap(_mapType, _regionBtnTransform, mapDataProvider);
                // 탭 전환 시에도 자연스러운 순차 등장을 위해 시퀀스 재생
                Sequence _subSeq = subRegionGroup.PlayAppearSequence();
                _subSeq.Play();
            }
        }
    }

    public void HandleSubRegionHovered(ForestType _forestType, Transform _subRegionTransform, ForestEnvironmentInfo _info)
    {
        if (true == IsInputBlocked)
        {
            return;
        }

        Debug.Log($"[HUD_PopupNav_Main] 서브지역 호버 감지됨: {_forestType}");

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
