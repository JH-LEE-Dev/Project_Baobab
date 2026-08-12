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
    [Header("Demo Version Settings")]
    [Tooltip("체크 시 데모 버전으로 동작합니다. 인스펙터에서 켜고 끌 수 있습니다.")]
    [SerializeField] private bool isDemoVersion = false;
    [Tooltip("데모 버전에서 플레이 가능한 최대 대지역 (기본: WideGreenForest)")]
    [SerializeField] private MapType maxPlayableMapTypeInDemo = MapType.WideGreenForest;

    [Header("Demo Notice Group")]
    [Tooltip("데모 버전 제한 시 노출되는 안내 UI 컴포넌트")]
    [SerializeField] private HUD_PopupNav_DemoNotice demoNotice;

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



    [Header("Region Name UI")]
    [Tooltip("현재 선택된 대지역 이름을 표시할 텍스트")]
    [SerializeField] private TextMeshProUGUI currentRegionNameText;
    [Tooltip("이름 변경 시 Y 스케일 축소(뽀잉) 연출 강도 (예: 0.5)")]
    [SerializeField] private float regionNamePunchScaleY = 0.5f;
    [Tooltip("이름 변경 연출 시간")]
    [SerializeField] private float regionNameAnimDuration = 0.25f;
    [Tooltip("이름 변경 연출 진동수 (Vibrato)")]
    [SerializeField] private int regionNameAnimVibrato = 5;
    [Tooltip("이름 변경 연출 탄성 (Elasticity)")]
    [SerializeField] private float regionNameAnimElasticity = 1f;

    private Tween regionNameTween;

    [Header("Navigation Groups")]
    [Tooltip("대지역 관리 그룹")]
    [SerializeField] private HUD_PopupNav_RegionGroup regionGroup;
    [Tooltip("서브지역 관리 그룹")]
    [SerializeField] private HUD_PopupNav_SubRegionGroup subRegionGroup;
    [Tooltip("나무 비주얼 데이터베이스")]
    [SerializeField] private TreeVisualDataBase treeVisualDataBase;

    private Tween appearTween;
    private Tween disappearTween;

    [Header("SubRegion Field Animation")]
    [Tooltip("서브지역 영역 (오른쪽에서 날아옴)")]
    [SerializeField] private RectTransform subRegionFieldTransform;
    [Tooltip("시작 시 화면 우측 밖으로 밀려나는 거리 (Offset X)")]
    [SerializeField] private float subRegionFieldOffsetX = 1500f;
    [Tooltip("이동 연출 이즈(Ease)")]
    [SerializeField] private Ease subRegionFieldEase = Ease.OutBack;
    [Tooltip("바운스(반동) 강도 (낮을수록 약함, 기본 1.0 / DOTween 기본 1.7)")]
    [SerializeField] private float subRegionFieldOvershoot = 1.0f;

    [Header("Title Band Animation")]
    [Tooltip("타이틀 배경 검은색 띠 (가운데서 X축 쫙 펴짐)")]
    [SerializeField] private RectTransform titleBandTransform;
    [Tooltip("펼쳐지는 연출 이즈(Ease)")]
    [SerializeField] private Ease titleBandEase = Ease.OutBack;
    [Tooltip("바운스(반동) 강도 (낮을수록 약함, 기본 1.0 / DOTween 기본 1.7)")]
    [SerializeField] private float titleBandOvershoot = 1.0f;

    [Header("Sync Animation Settings")]
    [Tooltip("두 연출의 강제 재생 시간 (0이면 Region 버튼 전체 연출 시간과 동일하게 자동 동기화)")]
    [SerializeField] private float additionalAnimDurationOverride = 0f;

    private Vector2 subRegionFieldOriginalPos;

    [Header("Settings")]
    [Tooltip("다중 대지역 해금 시 배속 (예: 2배속이면 2.0)")]
    [SerializeField] private float multiRegionUnlockSpeedRate = 2.0f;
    [Tooltip("닫힘(내려가는) 연출이 끝난 뒤 던전 확정 콜백을 호출하기까지의 지연 시간")]
    [SerializeField] private float dungeonConfirmDelay = 0.25f;

    [Header("Debug")]
    [Tooltip("체크 시 내비게이션을 열 때 모든 지역 및 서브지역을 강제로 해금 처리합니다.")]
    [SerializeField] private bool debugForceUnlockAll = false;

    // 내부 의존성
    private IMapDataProvider mapDataProvider;
    private LocalizationManager localizationManager;
    private ICursorBoxUI cursorBoxUI;
    private Action onNavigationClosedCallback;
    private Action<MapType, ForestType> onConfirmMapSelectedCallback;
    private Tween delayedCallTween;
    
    // 세션 유지 데이터 (게임 실행 중 유지)
    private static MapType runtimeLastSelectedMapType = MapType.None;

    // 상태 변수
    private bool isUnlockingProductionActive = false;
    private bool hasPlayedUnlockStartSound = false;
    private bool isClosing = false;
    private bool isInputBlocked = false;
    private ForestType currentSelectedForestType = ForestType.None;
    private MapType currentSelectedMapType = MapType.None;
    private bool isPendingUnlockProcess = false;
    // 던전 확정 콜백은 닫힘(내려가는) 연출이 끝난 뒤 발동해야 하므로,
    // HandleSubRegionSelected에서 즉시 호출하지 않고 이 플래그로 예약해둔다.
    private bool hasPendingDungeonConfirm = false;
    private MapType pendingConfirmMapType = MapType.None;
    private ForestType pendingConfirmForestType = ForestType.None;
    private Tween dungeonConfirmDelayTween;

    // 언락 큐 구조체
    private struct UnlockInfo
    {
        public bool isRegion;
        public MapType mapType;
        public ForestType forestType;
    }
    
    private readonly List<UnlockInfo> regionUnlockList = new List<UnlockInfo>(4);
    private readonly List<UnlockInfo> subRegionUnlockList = new List<UnlockInfo>(8);


    private readonly List<ForestType> pendingSubRegionUnlockForestTypes = new List<ForestType>(4);
    private MapType pendingRegionUnlockMapType = MapType.None;
    private float cachedUnlockSpeedRate = 1.0f;

    // 캐싱된 델리게이트 (GC Alloc 방지)
    private TweenCallback onAppearMidwayCallback;
    private TweenCallback onAppearCompleteCallback;
    private TweenCallback onSubRegionUnlockDelayCompleteCallback;
    private TweenCallback onDungeonConfirmDelayCompleteCallback;
    private TweenCallback onPanelOpenStartedCallback;
    private TweenCallback onAdditionalElementsAppearStartedCallback;
    private TweenCallback onNavImageDownStartedCallback;
    
    private UnityEngine.Events.UnityAction onBackgroundDimClickedAction;

    public bool IsInputBlocked => isInputBlocked || isUnlockingProductionActive || isClosing || (null != demoNotice && demoNotice.IsDemoNoticeShowing);
    public bool IsUnlockingProductionActive => isUnlockingProductionActive;
    public bool IsDemoNoticeShowing => null != demoNotice && demoNotice.IsDemoNoticeShowing;
    public bool IsTransitioning { get; private set; }
    public bool IsDemoVersion { get => isDemoVersion; set => isDemoVersion = value; }
    public MapType MaxPlayableMapTypeInDemo { get => maxPlayableMapTypeInDemo; set => maxPlayableMapTypeInDemo = value; }

    public event Action OnUnlockProductionStarted;
    public event Action OnUnlockProductionEnded;

    // 플레이어가 들어갈 던전(하위 지역)을 클릭해 선택을 확정한 바로 그 순간에 발행된다.
    // 실제 DungeonSelectedEvent(HandleEnterDungeon)는 UI가 닫히는 연출 + dungeonConfirmDelay만큼
    // 늦게 발동되는데, 그 사이 구간도 이미 취소 불가능한 선택이므로 ESC는 이 시점부터 막아야 한다.
    public event Action DungeonConfirmStartedEvent;

    private void StartUnlockProduction()
    {
        if (false == isUnlockingProductionActive)
        {
            isUnlockingProductionActive = true;
            hasPlayedUnlockStartSound = false;
            OnUnlockProductionStarted?.Invoke();
        }
    }

    public void PlayUnlockStartSoundIfNeeded()
    {
        if (false == isUnlockingProductionActive || true == hasPlayedUnlockStartSound)
        {
            return;
        }

        hasPlayedUnlockStartSound = true;
        Sound.PlayUI(SoundID.NaviUnLockStart);
    }

    private void EndUnlockProduction()
    {
        if (true == isUnlockingProductionActive)
        {
            isUnlockingProductionActive = false;
            OnUnlockProductionEnded?.Invoke();
            
            if (null != regionGroup)
            {
                regionGroup.EvaluateAllHoverStates();
            }
            
            if (null != subRegionGroup)
            {
                subRegionGroup.EvaluateAllHoverStates();
            }
        }
    }

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(IMapDataProvider _provider, LocalizationManager _localizer, ICursorBoxUI _cursorBoxUI, Action _onClose, Action<MapType, ForestType> _onConfirm)
    {
        mapDataProvider = _provider;
        localizationManager = _localizer;
        cursorBoxUI = _cursorBoxUI;
        onNavigationClosedCallback = _onClose;
        onConfirmMapSelectedCallback = _onConfirm;

        onAppearMidwayCallback = OnAppearMidway;
        onAppearCompleteCallback = OnAppearComplete;
        onSubRegionUnlockDelayCompleteCallback = OnSubRegionUnlockDelayComplete;
        onDungeonConfirmDelayCompleteCallback = OnDungeonConfirmDelayComplete;
        onPanelOpenStartedCallback = PlayPanelOpenSound;
        onAdditionalElementsAppearStartedCallback = PlayAdditionalElementsAppearSounds;
        onNavImageDownStartedCallback = PlayNavImageDownSound;
        
        onBackgroundDimClickedAction = OnBackgroundDimClicked;

        if (null != regionGroup)
        {
            regionGroup.Initialize(this, localizationManager, cursorBoxUI);
        }

        if (null != subRegionGroup)
        {
            subRegionGroup.Initialize(this, localizationManager, cursorBoxUI, treeVisualDataBase);
        }

        if (null != demoNotice)
        {
            demoNotice.Initialize(this, localizationManager);
        }

        if (null != subRegionFieldTransform)
        {
            subRegionFieldOriginalPos = subRegionFieldTransform.anchoredPosition;
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
    }

    public void Open()
    {
        gameObject.SetActive(true);
        isClosing = false;
        isInputBlocked = true;
        Sound.PlayUI(SoundID.ResultUIOpen);

        if (null != demoNotice)
        {
            demoNotice.ResetNotice();
        }
        isUnlockingProductionActive = false;
        hasPendingDungeonConfirm = false;
        currentSelectedMapType = MapType.None;
        currentSelectedForestType = ForestType.None;

        if (null != dungeonConfirmDelayTween && true == dungeonConfirmDelayTween.IsActive())
        {
            dungeonConfirmDelayTween.Kill();
            dungeonConfirmDelayTween = null;
        }

        if (null != currentRegionNameText)
        {
            currentRegionNameText.text = "";
            currentRegionNameText.transform.localScale = Vector3.one;
        }

        if (true == debugForceUnlockAll && null != mapDataProvider)
        {
            MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
            if (null != _db.mapDatas)
            {
                for (int i = 0; i < _db.mapDatas.Count; i++)
                {
                    if (false == _db.mapDatas[i].isUnlocked)
                    {
                        mapDataProvider.MarkMapUnlocked(_db.mapDatas[i].mapType);
                        mapDataProvider.MarkMapUnlockAnimationPlayed(_db.mapDatas[i].mapType);
                    }
                    
                    if (null != _db.mapDatas[i].forestDatas)
                    {
                        for (int j = 0; j < _db.mapDatas[i].forestDatas.Count; j++)
                        {
                            if (false == _db.mapDatas[i].forestDatas[j].isUnlocked)
                            {
                                mapDataProvider.MarkUnlocked(_db.mapDatas[i].mapType, _db.mapDatas[i].forestDatas[j].forestType);
                                mapDataProvider.MarkUnlockAnimationPlayed(_db.mapDatas[i].mapType, _db.mapDatas[i].forestDatas[j].forestType);
                            }
                        }
                    }
                }
            }
        }

        InitFirstPlayableRegionUnlock();

        BuildUnlockQueues();
        if (0 < regionUnlockList.Count || 0 < subRegionUnlockList.Count)
        {
            isPendingUnlockProcess = true;
            StartUnlockProduction();
        }

        if (null != appearTween && true == appearTween.IsActive())
        {
            appearTween.Kill();
            appearTween = null;
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
            _seq.InsertCallback(navImageBounceDuration, onPanelOpenStartedCallback);
            _seq.Insert(navImageBounceDuration, interactiveUIPanel.DOScaleY(1f, panelScaleDuration).SetEase(panelScaleEase));
        }

        // 4. Region 버튼들 순차 등장
        // (DOTween 시퀀스는 미리 구성되므로 SetupRegions는 이전에 호출하여 버튼들이 생성되어 있어야 합니다)
        _seq.AppendInterval(delayBeforeButtons);
        
        Sequence _simultaneousSeq = DOTween.Sequence();
        float _animDuration = 0.5f;

        if (null != regionGroup)
        {
            Sequence _regionSeq = regionGroup.PlayAppearSequence();
            _simultaneousSeq.Join(_regionSeq);
            if (null != _regionSeq) _animDuration = _regionSeq.Duration();

            if (0f < additionalAnimDurationOverride)
            {
                _animDuration += additionalAnimDurationOverride;
            }
        }

        if (null != subRegionFieldTransform)
        {
            subRegionFieldTransform.anchoredPosition = new Vector2(subRegionFieldOriginalPos.x + subRegionFieldOffsetX, subRegionFieldOriginalPos.y);
            _simultaneousSeq.Join(subRegionFieldTransform.DOAnchorPosX(subRegionFieldOriginalPos.x, _animDuration).SetEase(subRegionFieldEase, subRegionFieldOvershoot));
        }

        if (null != titleBandTransform)
        {
            titleBandTransform.localScale = new Vector3(0f, 1f, 1f);
            _simultaneousSeq.Join(titleBandTransform.DOScaleX(1f, _animDuration).SetEase(titleBandEase, titleBandOvershoot));
        }

        _seq.AppendCallback(onAdditionalElementsAppearStartedCallback);
        _seq.Append(_simultaneousSeq);

        // 5. 첫번째 대지역 선택 및 서브지역 연출 준비 (OnMainPopupAppearComplete 역할 포함)
        _seq.AppendCallback(onAppearMidwayCallback);

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

    private void PlayPanelOpenSound()
    {
        Sound.PlayUI(SoundID.NaviOpen);
    }

    private void PlayAdditionalElementsAppearSounds()
    {
        Sound.PlayUI(SoundID.NaviRowAppear);
        Sound.PlayUI(SoundID.NaviSubBGAppear);
    }

    private void PlayNavImageDownSound()
    {
        Sound.PlayUI(SoundID.ResultUIClose);
    }

    private void OnMainPopupAppearCompleteForAnimation()
    {
        BuildUnlockQueues();

        if (0 < regionUnlockList.Count || 0 < subRegionUnlockList.Count)
        {
            isPendingUnlockProcess = true;
        }

        bool _sessionRestored = RestoreSessionState();

        if (false == _sessionRestored && true == isPendingUnlockProcess)
        {
            isPendingUnlockProcess = false;
            ProcessNextUnlock();
        }
    }

    public void Close(bool _isInstant = false, bool _playCloseSound = true)
    {
        if (true == isClosing)
        {
            return;
        }

        CloseMainPopup(_isInstant, _playCloseSound);
    }

    private void CloseMainPopup(bool _isInstant = false, bool _playCloseSound = true)
    {
        MarkCurrentRegionAsRead();
        MarkAllUnlockedAsRead();

        isClosing = true;
        isInputBlocked = true;

        if (null != demoNotice)
        {
            demoNotice.ResetNotice();
        }

        if (null != delayedCallTween && true == delayedCallTween.IsActive())
        {
            delayedCallTween.Kill();
            delayedCallTween = null;
        }

        if (null != subRegionGroup)
        {
            subRegionGroup.ClearAllNewIndicators();
            subRegionGroup.StopAllHoverEffects();
        }

        if (null != regionGroup)
        {
            regionGroup.ClearAllNewIndicators();
            regionGroup.StopAllHoverEffects();
        }

        if (null != disappearTween && true == disappearTween.IsActive())
        {
            disappearTween.Kill();
            disappearTween = null;
        }

        if (true == _isInstant)
        {
            OnMainPopupDisappearComplete();
            return;
        }

        if (true == _playCloseSound)
        {
            Sound.PlayUI(SoundID.NaviClose);
        }

        Sequence _seq = DOTween.Sequence();
        float _currentTime = 0f;

        // 패널 Y스케일 축소 (동시 진행)
        if (null != interactiveUIPanel)
        {
            _seq.Insert(_currentTime, interactiveUIPanel.DOScaleY(0f, panelScaleDuration).SetEase(panelScaleEase));
        }

        _currentTime += panelScaleDuration;

        // 2. Dim 배경 알파 감소
        if (null != dimBackgroundCanvasGroup)
        {
            _seq.Insert(_currentTime, dimBackgroundCanvasGroup.DOFade(0f, dimFadeDuration).SetEase(Ease.OutQuad));
        }

        _currentTime += dimFadeDuration;

        // 3. 내비게이션 이미지가 아래로 내려가며 퇴장
        if (null != navImageContainer)
        {
            _seq.InsertCallback(_currentTime, onNavImageDownStartedCallback);
            _seq.Insert(_currentTime, navImageContainer.DOAnchorPosY(-200f, navImageBounceDuration).SetEase(Ease.InBack));
        }

        _seq.OnComplete(OnMainPopupDisappearComplete);

        disappearTween = _seq;
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


    private void OnMainPopupDisappearComplete()
    {
        if (null != subRegionGroup)
        {
            subRegionGroup.ResetState();
        }
        
        gameObject.SetActive(false);

        // 던전 선택 확정 콜백(DungeonSelectedEvent로 이어짐)은 UI가 완전히 내려간 뒤,
        // dungeonConfirmDelay만큼 추가로 기다렸다가 발동한다.
        // onNavigationClosedCallback(TeleportUIClosedEvent)도 함께 미뤄야 한다 - 이 콜백이 바로
        // TownSystem.GetOffFromTheVehicle()로 이어지는데, 던전 확정 콜백(bCanGetOff=false 설정)보다
        // 먼저 실행되면 아직 bCanGetOff가 true인 상태라 캐릭터가 차에서 잘못 내려버린다.
        if (true == hasPendingDungeonConfirm)
        {
            hasPendingDungeonConfirm = false;

            if (null != dungeonConfirmDelayTween && true == dungeonConfirmDelayTween.IsActive())
            {
                dungeonConfirmDelayTween.Kill();
                dungeonConfirmDelayTween = null;
            }

            dungeonConfirmDelayTween = DOVirtual.DelayedCall(dungeonConfirmDelay, onDungeonConfirmDelayCompleteCallback).SetEase(Ease.Linear);
            return;
        }

        onNavigationClosedCallback?.Invoke();
    }

    private void OnDungeonConfirmDelayComplete()
    {
        dungeonConfirmDelayTween = null;
        // 순서 중요: 확정 콜백(bCanGetOff=false 등 상태 처리)이 onNavigationClosedCallback(TeleportUIClosedEvent)보다
        // 먼저 실행되어야 GetOffFromTheVehicle()이 안전하게 막힌다.
        onConfirmMapSelectedCallback?.Invoke(pendingConfirmMapType, pendingConfirmForestType);
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
        // 1순위: 현재 선택된(보여지고 있는) 맵의 서브지역 해금이 존재할 경우
        bool _hasCurrentMapSubRegionUnlock = false;
        for (int i = 0; i < subRegionUnlockList.Count; i++)
        {
            if (currentSelectedMapType == subRegionUnlockList[i].mapType)
            {
                _hasCurrentMapSubRegionUnlock = true;
                break;
            }
        }

        if (true == _hasCurrentMapSubRegionUnlock)
        {
            StartUnlockProduction();
            pendingSubRegionUnlockForestTypes.Clear();
            
            for (int i = subRegionUnlockList.Count - 1; i >= 0; i--)
            {
                UnlockInfo _subInfo = subRegionUnlockList[i];
                if (currentSelectedMapType == _subInfo.mapType)
                {
                    pendingSubRegionUnlockForestTypes.Add(_subInfo.forestType);
                    subRegionUnlockList.RemoveAt(i);
                }
            }

            // 트랜지션 완료 대기가 필요 없으므로 짧은 딜레이 후 바로 연출 진행
            float _delay = 0.1f;
            pendingRegionUnlockMapType = currentSelectedMapType; // mapType 캐싱 재사용
            delayedCallTween = DOVirtual.DelayedCall(_delay, onSubRegionUnlockDelayCompleteCallback).SetEase(Ease.Linear);
            return;
        }

        // 2순위: 대지역 해금이 1개 이상 존재할 경우
        if (0 < regionUnlockList.Count)
        {
            StartUnlockProduction();
            bool _isMultiRegion = 1 < regionUnlockList.Count;
            float _speedRate = true == _isMultiRegion ? multiRegionUnlockSpeedRate : 1.0f;
            
            PlayNextRegionUnlock(_speedRate);
            return;
        }

        // 3순위: 그 외(현재 보고 있지 않은) 대지역의 서브지역 해금은 조용히 해금 처리 (NEW 뱃지만 표시됨)
        if (0 < subRegionUnlockList.Count)
        {
            for (int i = 0; i < subRegionUnlockList.Count; i++)
            {
                UnlockInfo _subInfo = subRegionUnlockList[i];
                mapDataProvider.MarkUnlocked(_subInfo.mapType, _subInfo.forestType);
                mapDataProvider.MarkUnlockAnimationPlayed(_subInfo.mapType, _subInfo.forestType);
            }
            subRegionUnlockList.Clear();
        }

        EndUnlockProduction();
    }

    private void OnSubRegionUnlockDelayComplete()
    {
        for (int i = 0; i < pendingSubRegionUnlockForestTypes.Count; i++)
        {
            mapDataProvider.MarkUnlocked(pendingRegionUnlockMapType, pendingSubRegionUnlockForestTypes[i]);
        }

        if (null != subRegionGroup)
        {
            subRegionGroup.PlayUnlockProduction(pendingSubRegionUnlockForestTypes, OnSubRegionUnlockMotionComplete);
        }
        else
        {
            OnSubRegionUnlockMotionComplete();
        }
    }

    private void OnSubRegionUnlockMotionComplete()
    {
        for (int i = 0; i < pendingSubRegionUnlockForestTypes.Count; i++)
        {
            mapDataProvider.MarkUnlockAnimationPlayed(pendingRegionUnlockMapType, pendingSubRegionUnlockForestTypes[i]);
        }
        pendingSubRegionUnlockForestTypes.Clear();
        
        ProcessNextUnlock();
    }

    // IPointerClickHandler 구현은 개별 컴포넌트(SimpleClickHandler)로 위임하여 제거함

    private void PlayNextRegionUnlock(float _speedRate)
    {
        if (0 == regionUnlockList.Count)
        {
            ProcessNextUnlock();
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

    private bool RestoreSessionState()
    {
        if (MapType.None != runtimeLastSelectedMapType && false == IsDemoRestrictedMapType(runtimeLastSelectedMapType))
        {
            HandleRegionSelected(runtimeLastSelectedMapType, true, true);
            return true;
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
                        if (MapType.Town != _db.mapDatas[i].mapType && false == IsDemoRestrictedMapType(_db.mapDatas[i].mapType))
                        {
                            HandleRegionSelected(_db.mapDatas[i].mapType, true, true);
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }

    private void MarkCurrentRegionAsRead()
    {
        if (MapType.None != currentSelectedMapType && null != mapDataProvider)
        {
            mapDataProvider.MarkMapLevelAsRead(currentSelectedMapType);
            
            MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
            if (null != _db.mapDatas)
            {
                for (int i = 0; i < _db.mapDatas.Count; i++)
                {
                    if (currentSelectedMapType == _db.mapDatas[i].mapType)
                    {
                        List<ForestEnvironmentInfo> _forestDatas = _db.mapDatas[i].forestDatas;
                        if (null != _forestDatas)
                        {
                            for (int j = 0; j < _forestDatas.Count; j++)
                            {
                                mapDataProvider.MarkMapAsRead(currentSelectedMapType, _forestDatas[j].forestType);
                            }
                        }
                        break;
                    }
                }
            }

            if (null != regionGroup)
            {
                regionGroup.ClearNewIndicator(currentSelectedMapType);
            }
        }
    }

    private void MarkAllUnlockedAsRead()
    {
        if (null == mapDataProvider) return;
        
        MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null != _db.mapDatas)
        {
            for (int i = 0; i < _db.mapDatas.Count; i++)
            {
                if (true == _db.mapDatas[i].isUnlocked)
                {
                    mapDataProvider.MarkMapLevelAsRead(_db.mapDatas[i].mapType);
                    
                    List<ForestEnvironmentInfo> _forestDatas = _db.mapDatas[i].forestDatas;
                    if (null != _forestDatas)
                    {
                        for (int j = 0; j < _forestDatas.Count; j++)
                        {
                            if (true == _forestDatas[j].isUnlocked)
                            {
                                mapDataProvider.MarkMapAsRead(_db.mapDatas[i].mapType, _forestDatas[j].forestType);
                            }
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

        // 데모 버전 체크: 첫 대지역 해금 연출 이후 해당 대지역 클릭 시 데모 안내 표시
        if (false == _force && true == IsDemoRestrictedMapType(_mapType))
        {
            ShowDemoNoticeOverlay(_mapType);
            return;
        }

        IsTransitioning = true;

        MarkCurrentRegionAsRead();

    

        currentSelectedMapType = _mapType;
        runtimeLastSelectedMapType = _mapType;
        currentSelectedForestType = ForestType.None;
        Sound.PlayUI(SoundID.NaviSelectStart);

        if (null != currentRegionNameText && null != localizationManager)
        {
            string _localizedName = localizationManager.GetText(_mapType);
            if (false == string.IsNullOrEmpty(_localizedName))
            {
                currentRegionNameText.text = _localizedName;

                if (null != regionNameTween && true == regionNameTween.IsActive())
                {
                    regionNameTween.Kill();
                    regionNameTween = null;
                    currentRegionNameText.transform.localScale = Vector3.one;
                }

                if (true == _playClickAnim)
                {
                    regionNameTween = currentRegionNameText.transform.DOPunchScale(
                        new Vector3(0f, -regionNamePunchScaleY, 0f), 
                        regionNameAnimDuration, 
                        regionNameAnimVibrato, 
                        regionNameAnimElasticity
                    );
                }
            }
        }

        if (null != regionGroup)
        {
            regionGroup.SetSelectRegion(_mapType, _playClickAnim);
            Transform _regionBtnTransform = regionGroup.GetRegionTransform(_mapType);
            
            if (MapType.None != _mapType)
            {
                ShowSubRegions(_mapType, _regionBtnTransform);
            }
        }
        else
        {
            IsTransitioning = false;
        }
    }

    private void ShowSubRegions(MapType _mapType, Transform _regionBtnTransform)
    {
        if (null != subRegionGroup)
        {
            subRegionGroup.ShowSubRegionsForMap(_mapType, _regionBtnTransform, mapDataProvider, OnSubRegionsShown);
        }
        else
        {
            IsTransitioning = false;
        }
    }

    private void OnSubRegionsShown()
    {
        IsTransitioning = false;
        if (null != regionGroup) regionGroup.EvaluateAllHoverStates();
        if (null != subRegionGroup) subRegionGroup.EvaluateAllHoverStates();

        if (true == isPendingUnlockProcess)
        {
            isPendingUnlockProcess = false;
            ProcessNextUnlock();
        }
    }

    public void HandleSubRegionHovered(ForestType _forestType, Transform _subRegionTransform, ForestEnvironmentInfo _info)
    {
        if (true == IsInputBlocked)
        {
            return;
        }
    }

    public void HandleSubRegionUnhovered()
    {
        if (true == IsInputBlocked)
        {
            return;
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

        if (MapType.None != currentSelectedMapType && ForestType.None != currentSelectedForestType)
        {
            hasPendingDungeonConfirm = true;
            pendingConfirmMapType = currentSelectedMapType;
            pendingConfirmForestType = currentSelectedForestType;

            DungeonConfirmStartedEvent?.Invoke();

            Close(_playCloseSound: false);
        }
    }

    public bool IsDemoRestrictedMapType(MapType _mapType)
    {
        if (false == isDemoVersion) return false;
        if (_mapType == MapType.None || _mapType == MapType.Town) return false;
        return _mapType > maxPlayableMapTypeInDemo;
    }

    public void ShowDemoNoticeOverlay(MapType _restrictedMapType = MapType.None)
    {
        if (null != demoNotice)
        {
            if (null != cursorBoxUI)
            {
                cursorBoxUI.HideImmediately();
            }
            demoNotice.ShowDemoNoticeOverlay(_restrictedMapType);
        }
    }

    public void HideDemoNoticeOverlay()
    {
        if (null != demoNotice)
        {
            demoNotice.HideDemoNoticeOverlay();
        }
    }

    public void HandleDemoNoticeClosed()
    {
        // 안전하게 현재 플레이 가능한 대지역으로 UI 복구
        MapType _targetMap = (currentSelectedMapType != MapType.None && false == IsDemoRestrictedMapType(currentSelectedMapType))
            ? currentSelectedMapType
            : maxPlayableMapTypeInDemo;

        if (null != regionGroup)
        {
            regionGroup.SetSelectRegion(_targetMap, false);
        }
    }

    private void OnDestroy()
    {
        if (null != appearTween && appearTween.IsActive()) { appearTween.Kill(); appearTween = null; }
        if (null != disappearTween && disappearTween.IsActive()) { disappearTween.Kill(); disappearTween = null; }
        if (null != delayedCallTween && delayedCallTween.IsActive()) { delayedCallTween.Kill(); delayedCallTween = null; }
        if (null != regionNameTween && regionNameTween.IsActive()) { regionNameTween.Kill(); regionNameTween = null; }
        if (null != dungeonConfirmDelayTween && dungeonConfirmDelayTween.IsActive()) { dungeonConfirmDelayTween.Kill(); dungeonConfirmDelayTween = null; }
        if (null != demoNotice) { demoNotice.KillTweens(); }
    }
}
