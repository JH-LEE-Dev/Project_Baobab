using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
    private UIDepthController depthController;
    private InputManager inputManager;
    private Action onNavigationClosedCallback;
    private Action cachedOnSubRegionsShown;
    private Action<MapType, ForestType> onConfirmMapSelectedCallback;
    private Tween delayedCallTween;
    // 세션 유지 데이터 (게임 실행 중 유지)
    private static MapType runtimeLastSelectedMapType = MapType.None;
    private static MapType runtimeLastVisitedMapType = MapType.None;
    private static ForestType runtimeLastVisitedForestType = ForestType.None;
    public enum ENavFocusArea
    {
        None,
        RegionList,
        SubRegionList
    }

    private ENavFocusArea currentFocusArea = ENavFocusArea.None;
    private int focusedRegionIndex = 0;
    private int focusedSubRegionIndex = 0;
    private bool isNavigationAxisInUse = false;
    private float nextNavigationAllowedTime = 0f;
    private const float NAVIGATION_ACTIVATE_THRESHOLD = 0.6f;
    private const float NAVIGATION_RELEASE_THRESHOLD = 0.2f;
    private const float NAVIGATION_COOLDOWN = 0.2f;
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
    private Action<Vector2> cachedOnMoveEvent;
    private Action cachedOnInteractionKeyPressed;
    private Action cachedOnUICancel;
    private Action<EInputDeviceType> cachedOnInputDeviceChanged;
    private UnityEngine.Events.UnityAction onBackgroundDimClickedAction;
    private float demoNoticeClosedGraceTime = 0f;
    public bool IsInputBlocked => isInputBlocked || isUnlockingProductionActive || isClosing || IsTransitioning || (null != demoNotice && demoNotice.IsDemoNoticeActive) || (Time.unscaledTime < demoNoticeClosedGraceTime);
    public bool IsUnlockingProductionActive => isUnlockingProductionActive;
    public bool IsDemoNoticeShowing => null != demoNotice && demoNotice.IsDemoNoticeActive;
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

            if (null != inputManager && true == inputManager.IsGamepadMode)
            {
                SetupInitialGamepadFocus();
            }
        }
    }

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(
    IMapDataProvider _provider,
    LocalizationManager _localizer,
    ICursorBoxUI _cursorBoxUI,
    Action _onClose,
    Action<MapType, ForestType> _onConfirm,
    UIDepthController _depthController = null,
    InputManager _inputManager = null)
    {
        mapDataProvider = _provider;
        localizationManager = _localizer;
        cursorBoxUI = _cursorBoxUI;
        onNavigationClosedCallback = _onClose;
        cachedOnSubRegionsShown = OnSubRegionsShown;
        onConfirmMapSelectedCallback = _onConfirm;
        depthController = _depthController;
        inputManager = _inputManager;
        onAppearMidwayCallback = OnAppearMidway;
        onAppearCompleteCallback = OnAppearComplete;
        onSubRegionUnlockDelayCompleteCallback = OnSubRegionUnlockDelayComplete;
        onDungeonConfirmDelayCompleteCallback = OnDungeonConfirmDelayComplete;
        onPanelOpenStartedCallback = PlayPanelOpenSound;
        onAdditionalElementsAppearStartedCallback = PlayAdditionalElementsAppearSounds;
        onNavImageDownStartedCallback = PlayNavImageDownSound;
        onBackgroundDimClickedAction = OnBackgroundDimClicked;
        if (null == cachedOnMoveEvent) cachedOnMoveEvent = OnMoveInputReceived;
        if (null == cachedOnInteractionKeyPressed) cachedOnInteractionKeyPressed = OnInteractionKeyPressed;
        if (null == cachedOnUICancel) cachedOnUICancel = OnUICancelPressed;
        if (null == cachedOnInputDeviceChanged) cachedOnInputDeviceChanged = OnInputDeviceChanged;
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.MoveEvent -= cachedOnMoveEvent;
            inputManager.inputReader.MoveEvent += cachedOnMoveEvent;
            inputManager.inputReader.InteractionKeyPressedEvent -= cachedOnInteractionKeyPressed;
            inputManager.inputReader.InteractionKeyPressedEvent += cachedOnInteractionKeyPressed;
            inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
            inputManager.inputReader.UICancelEvent += cachedOnUICancel;
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
            inputManager.inputReader.InputDeviceChangedEvent += cachedOnInputDeviceChanged;
        }

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
            demoNotice.Initialize(this, localizationManager, depthController, cursorBoxUI, inputManager);
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
        ResetOpenState();
        InitFirstPlayableRegionUnlock();
        CheckAndStartUnlockProduction();
        PlayAppearSequence();
    }

    private void ResetOpenState()
    {
        if (null != demoNotice)
        {
            demoNotice.ResetNotice();
        }

        isUnlockingProductionActive = false;
        hasPendingDungeonConfirm = false;
        currentSelectedMapType = MapType.None;
        currentSelectedForestType = ForestType.None;
        currentFocusArea = ENavFocusArea.None;
        isNavigationAxisInUse = false;
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
            ForceUnlockAllMapsForDebug();
        }

        if (null != appearTween && true == appearTween.IsActive())
        {
            appearTween.Kill();
            appearTween = null;
        }
    }

    private void ForceUnlockAllMapsForDebug()
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

    private void CheckAndStartUnlockProduction()
    {
        BuildUnlockQueues();
        if (0 < regionUnlockList.Count || 0 < subRegionUnlockList.Count)
        {
            isPendingUnlockProcess = true;
            StartUnlockProduction();
        }
    }

    private void PlayAppearSequence()
    {
        if (null != navImageContainer)
        {
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

        if (null != regionGroup)
        {
            regionGroup.SetupRegions(mapDataProvider);
        }

        Sequence _seq = DOTween.Sequence();
        if (null != navImageContainer)
        {
            _seq.Append(navImageContainer.DOAnchorPosY(0f, navImageBounceDuration).SetEase(navImageBounceEase));
        }

        if (null != dimBackgroundCanvasGroup)
        {
            _seq.Insert(navImageBounceDuration * 0.5f, dimBackgroundCanvasGroup.DOFade(1f, dimFadeDuration).SetEase(dimFadeEase));
        }

        if (null != interactiveUIPanel)
        {
            _seq.InsertCallback(navImageBounceDuration, onPanelOpenStartedCallback);
            _seq.Insert(navImageBounceDuration, interactiveUIPanel.DOScaleY(1f, panelScaleDuration).SetEase(panelScaleEase));
        }

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
        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            if (ENavFocusArea.None == currentFocusArea)
            {
                SetupInitialGamepadFocus();
            }
        }
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

        RestoreSessionState();
        if (true == isPendingUnlockProcess)
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
        if (null != demoNotice && true == demoNotice.IsDemoNoticeActive)
        {
            if (false == demoNotice.IsHiding)
            {
                demoNotice.HideDemoNoticeOverlay();
            }

            return;
        }

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
            for (int i = subRegionUnlockList.Count - 1; 0 <= i; i--)
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
        if (ForestType.None != runtimeLastVisitedForestType && MapType.None != runtimeLastVisitedMapType && false == IsDemoRestrictedMapType(runtimeLastVisitedMapType))
        {
            HandleRegionSelected(runtimeLastVisitedMapType, true, true);
            return true;
        }

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
            if (true == _force && true == _playClickAnim)
            {
                Sound.PlayUI(SoundID.NaviSelectStart);
                if (null != currentRegionNameText)
                {
                    if (null != regionNameTween && true == regionNameTween.IsActive())
                    {
                        regionNameTween.Kill();
                        regionNameTween = null;
                        currentRegionNameText.transform.localScale = Vector3.one;
                    }

                    regionNameTween = currentRegionNameText.transform.DOPunchScale(
                    new Vector3(0f, -regionNamePunchScaleY, 0f),
                    regionNameAnimDuration,
                    regionNameAnimVibrato,
                    regionNameAnimElasticity
                    );
                }
            }

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
        if (true == _playClickAnim)
        {
            Sound.PlayUI(SoundID.NaviSelectStart);
        }

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
            if (MapType.None != _mapType && null != subRegionGroup)
            {
                subRegionGroup.ShowSubRegionsForMap(_mapType, _regionBtnTransform, mapDataProvider, cachedOnSubRegionsShown);
            }
            else
            {
                IsTransitioning = false;
            }
        }
        else
        {
            IsTransitioning = false;
        }
    }

    private void OnSubRegionsShown()
    {
        IsTransitioning = false;
        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            if (ENavFocusArea.SubRegionList == currentFocusArea && null != subRegionGroup)
            {
                subRegionGroup.FocusSubRegionButton(focusedSubRegionIndex);
            }
        }
        else
        {
            if (null != subRegionGroup)
            {
                subRegionGroup.EvaluateAllHoverStates();
            }

            if (null != regionGroup)
            {
                regionGroup.EvaluateAllHoverStates();
            }
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
        runtimeLastVisitedMapType = currentSelectedMapType;
        runtimeLastVisitedForestType = _forestType;
        runtimeLastSelectedMapType = currentSelectedMapType;
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

#region Gamepad Navigation

    public int GetFirstUnlockedAndPlayableRegionIndex()
    {
        if (null == regionGroup) return 0;
        IReadOnlyList<HUD_PopupNav_RegionBtn> _activeRegions = regionGroup.GetActiveRegionButtons();
        if (null == _activeRegions || 0 == _activeRegions.Count) return 0;

        for (int i = 0; i < _activeRegions.Count; i++)
        {
            if (null != _activeRegions[i] && true == _activeRegions[i].IsUnlocked && false == IsDemoRestrictedMapType(_activeRegions[i].GetMapType()))
            {
                return i;
            }
        }
        return 0;
    }

    private void SetupInitialGamepadFocus()
    {
        if (null == inputManager || false == inputManager.IsGamepadMode)
        {
            currentFocusArea = ENavFocusArea.None;
            return;
        }

        if (true == isClosing)
        {
            return;
        }

        // 1. 데모 제한 대지역이거나 유효하지 않은 경우 안전 대지역으로 보정
        MapType _targetMap = currentSelectedMapType;
        if (MapType.None == _targetMap || true == IsDemoRestrictedMapType(_targetMap))
        {
            _targetMap = maxPlayableMapTypeInDemo;
            currentSelectedMapType = _targetMap;
        }

        // 2. 이전에 소지역을 방문했던 흔적이 있다면 해당 소지역에서 포커스 시작 (단, 해당 소지역이 해금 상태일 때만)
        if (ForestType.None != runtimeLastVisitedForestType && null != subRegionGroup)
        {
            int _visitedSubIdx = subRegionGroup.GetSubRegionIndex(runtimeLastVisitedForestType);
            if (0 <= _visitedSubIdx)
            {
                IReadOnlyList<HUD_PopupNav_SubRegionBtn> _subs = subRegionGroup.GetActiveSubRegionButtons();
                if (null != _subs && _visitedSubIdx < _subs.Count && null != _subs[_visitedSubIdx] && true == _subs[_visitedSubIdx].IsUnlocked)
                {
                    currentFocusArea = ENavFocusArea.SubRegionList;
                    focusedSubRegionIndex = _visitedSubIdx;
                    subRegionGroup.FocusSubRegionButton(focusedSubRegionIndex);
                    if (null != regionGroup) regionGroup.StopAllHoverEffects();
                    return;
                }
            }
        }

        // 3. 대지역 포커스 설정: 반드시 해금되고 데모 제한이 아닌 유효 대지역으로 설정
        currentFocusArea = ENavFocusArea.RegionList;
        if (null != regionGroup)
        {
            int _curIdx = regionGroup.GetActiveRegionIndex(_targetMap);
            IReadOnlyList<HUD_PopupNav_RegionBtn> _regions = regionGroup.GetActiveRegionButtons();
            if (0 <= _curIdx && null != _regions && _curIdx < _regions.Count && null != _regions[_curIdx] && true == _regions[_curIdx].IsUnlocked && false == IsDemoRestrictedMapType(_regions[_curIdx].GetMapType()))
            {
                focusedRegionIndex = _curIdx;
            }
            else
            {
                focusedRegionIndex = GetFirstUnlockedAndPlayableRegionIndex();
            }

            regionGroup.FocusRegionButton(focusedRegionIndex);
        }

        if (null != subRegionGroup)
        {
            subRegionGroup.StopAllHoverEffects();
        }
    }

    private void Update()
    {
        if (false == gameObject.activeInHierarchy || true == isClosing) return;
        if (null == inputManager || false == inputManager.IsGamepadMode) return;
        // 1. 방향 입력 (LeftStick & D-Pad & Keyboard)
        Vector2 _dir = Vector2.zero;
        if (null != Gamepad.current)
        {
            Vector2 _stick = Gamepad.current.leftStick.ReadValue();
            Vector2 _dpad = Gamepad.current.dpad.ReadValue();
            if (Mathf.Abs(_stick.x) >= 0.5f || Mathf.Abs(_stick.y) >= 0.5f)
            {
                _dir = _stick;
            }
            else if (Mathf.Abs(_dpad.x) >= 0.5f || Mathf.Abs(_dpad.y) >= 0.5f)
            {
                _dir = _dpad;
            }
        }

        if (Vector2.zero == _dir && null != Keyboard.current)
        {
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) _dir.y = 1f;
            else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) _dir.y = -1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) _dir.x = -1f;
            else if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) _dir.x = 1f;
        }

        ProcessDirectionalInput(_dir);
        // 2. 선택/확정 버튼: 리바인딩된 상호작용 키(Interaction Action) 또는 UI/Submit 키 검사
        bool _submitPressed = false;
        if (null != inputManager && true == inputManager.WasInteractionPressedThisFrame)
        {
            _submitPressed = true;
        }
        else if (null != Gamepad.current && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            _submitPressed = true;
        }
        else if (null != Keyboard.current && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
        {
            _submitPressed = true;
        }

        if (true == _submitPressed)
        {
            OnInteractionKeyPressed();
        }

        // 3. 취소/뒤로가기 버튼 (B / East)
        if (null != Gamepad.current && Gamepad.current.buttonEast.wasPressedThisFrame)
        {
            OnUICancelPressed();
        }
    }

    private void ProcessDirectionalInput(Vector2 _input)
    {
        float _absX = Mathf.Abs(_input.x);
        float _absY = Mathf.Abs(_input.y);
        bool _hasDirectionalInput = (_absX >= NAVIGATION_ACTIVATE_THRESHOLD || _absY >= NAVIGATION_ACTIVATE_THRESHOLD);
        bool _isReleased = (_absX < NAVIGATION_RELEASE_THRESHOLD && _absY < NAVIGATION_RELEASE_THRESHOLD);
        if (true == _isReleased)
        {
            isNavigationAxisInUse = false;
            return;
        }

        if (true == isNavigationAxisInUse)
        {
            return;
        }

        if (false == _hasDirectionalInput)
        {
            return;
        }

        if (Time.unscaledTime < nextNavigationAllowedTime)
        {
            return;
        }

        if (true == IsInputBlocked)
        {
            return;
        }

        isNavigationAxisInUse = true;
        nextNavigationAllowedTime = Time.unscaledTime + NAVIGATION_COOLDOWN;
        if (ENavFocusArea.RegionList == currentFocusArea)
        {
            HandleRegionNavigationInput(_input);
        }
        else if (ENavFocusArea.SubRegionList == currentFocusArea)
        {
            HandleSubRegionNavigationInput(_input);
        }
        else
        {
            SetupInitialGamepadFocus();
        }
    }

    private void OnMoveInputReceived(Vector2 _input)
    {
        ProcessDirectionalInput(_input);
    }

    private void HandleRegionNavigationInput(Vector2 _input)
    {
        if (null == regionGroup) return;
        IReadOnlyList<HUD_PopupNav_RegionBtn> _activeRegions = regionGroup.GetActiveRegionButtons();
        if (null == _activeRegions || 0 == _activeRegions.Count) return;
        // 우측 입력 (Right Stick / DPad Right / D / RightArrow) -> 소지역 필드로 포커스 이동
        if (_input.x >= NAVIGATION_ACTIVATE_THRESHOLD)
        {
            if (0 <= focusedRegionIndex && focusedRegionIndex < _activeRegions.Count)
            {
                HUD_PopupNav_RegionBtn _curBtn = _activeRegions[focusedRegionIndex];
                if (null != _curBtn && true == _curBtn.IsUnlocked)
                {
                    if (true == IsDemoRestrictedMapType(_curBtn.GetMapType()))
                    {
                        ShowDemoNoticeOverlay(_curBtn.GetMapType());
                        return;
                    }

                    HandleRegionSelected(_curBtn.GetMapType(), _force: true, _playClickAnim: false);
                    currentFocusArea = ENavFocusArea.SubRegionList;
                    regionGroup.StopAllHoverEffects();
                    if (null != subRegionGroup)
                    {
                        focusedSubRegionIndex = subRegionGroup.GetFirstUnlockedSubRegionIndex();
                        subRegionGroup.FocusSubRegionButton(focusedSubRegionIndex);
                        Sound.PlayUI(SoundID.NaviSubHover);
                    }

                    return;
                }
                else
                {
                    Sound.PlayUI(SoundID.NaviLocked);
                    return;
                }
            }
        }

        // 상하 입력 -> 대지역 간 이동
        int _delta = 0;
        if (_input.y >= NAVIGATION_ACTIVATE_THRESHOLD)
        {
            _delta = -1;
        }
        else if (_input.y <= -NAVIGATION_ACTIVATE_THRESHOLD)
        {
            _delta = 1;
        }

        if (0 == _delta) return;
        int _prevIdx = focusedRegionIndex;
        int _nextIdx = Mathf.Clamp(focusedRegionIndex + _delta, 0, _activeRegions.Count - 1);
        if (_prevIdx != _nextIdx)
        {
            HUD_PopupNav_RegionBtn _candidateBtn = _activeRegions[_nextIdx];
            if (null != _candidateBtn)
            {
                if (false == _candidateBtn.IsUnlocked)
                {
                    Sound.PlayUI(SoundID.NaviLocked);
                    return;
                }

                if (true == IsDemoRestrictedMapType(_candidateBtn.GetMapType()))
                {
                    ShowDemoNoticeOverlay(_candidateBtn.GetMapType());
                    return;
                }

                focusedRegionIndex = _nextIdx;
                regionGroup.FocusRegionButton(focusedRegionIndex);
                Sound.PlayUI(SoundID.NaviSelectStart);
                HandleRegionSelected(_candidateBtn.GetMapType(), _force: true, _playClickAnim: false);
            }
        }
    }

    private void HandleSubRegionNavigationInput(Vector2 _input)
    {
        if (null == subRegionGroup) return;
        IReadOnlyList<HUD_PopupNav_SubRegionBtn> _activeSubRegions = subRegionGroup.GetActiveSubRegionButtons();
        if (null == _activeSubRegions || 0 == _activeSubRegions.Count) return;
        // 소지역 필드는 오직 좌우(X축) 조작만 처리 (Y축 무시)
        int _delta = 0;
        if (_input.x <= -NAVIGATION_ACTIVATE_THRESHOLD)
        {
            _delta = -1;
        }
        else if (_input.x >= NAVIGATION_ACTIVATE_THRESHOLD)
        {
            _delta = 1;
        }

        if (0 == _delta) return;
        int _candidateIdx = focusedSubRegionIndex + _delta;
        // 좌측으로 범위를 벗어날 경우(1번 소지역에서 좌측 입력) -> 대지역 필드로 포커스 복귀
        if (0 > _candidateIdx)
        {
            currentFocusArea = ENavFocusArea.RegionList;
            subRegionGroup.StopAllHoverEffects();
            if (null != regionGroup)
            {
                regionGroup.FocusRegionButton(focusedRegionIndex);
                Sound.PlayUI(SoundID.NaviMainHover);
            }

            return;
        }

        if (_activeSubRegions.Count <= _candidateIdx)
        {
            return;
        }

        HUD_PopupNav_SubRegionBtn _candidateBtn = _activeSubRegions[_candidateIdx];
        if (null == _candidateBtn) return;
        // 요구사항 2: 잠긴 소지역으로 넘어가려고 하면 잠김 연출을 보여주고 실제로 키를 옮기지 않는다!
        if (false == _candidateBtn.IsUnlocked)
        {
            Sound.PlayUI(SoundID.NaviLocked);
            _candidateBtn.PlayLockedInteraction();
            return;
        }

        focusedSubRegionIndex = _candidateIdx;
        subRegionGroup.FocusSubRegionButton(focusedSubRegionIndex);
        Sound.PlayUI(SoundID.NaviSubHover);
    }

    private void OnInteractionKeyPressed()
    {
        if (false == gameObject.activeInHierarchy || true == isClosing) return;
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        // 데모 안내 패널이 열려있는 경우: IsInputBlocked 검사 전에 팝업에 입력 위임
        if (null != demoNotice && true == demoNotice.IsDemoNoticeShowing)
        {
            demoNotice.HandleInteractionKey();
            return;
        }

        if (true == IsInputBlocked) return;

        if (ENavFocusArea.RegionList == currentFocusArea)
        {
            if (null == regionGroup) return;
            IReadOnlyList<HUD_PopupNav_RegionBtn> _activeRegions = regionGroup.GetActiveRegionButtons();
            if (null == _activeRegions || focusedRegionIndex >= _activeRegions.Count) return;
            HUD_PopupNav_RegionBtn _btn = _activeRegions[focusedRegionIndex];
            if (null == _btn) return;
            if (false == _btn.IsUnlocked)
            {
                Sound.PlayUI(SoundID.NaviLocked);
                return;
            }

            if (true == IsDemoRestrictedMapType(_btn.GetMapType()))
            {
                ShowDemoNoticeOverlay(_btn.GetMapType());
                return;
            }

            // 요구사항 1: 대지역에서 A버튼(선택)을 눌러야 소지역 필드로 키가 넘어감
            HandleRegionSelected(_btn.GetMapType(), _force: true, _playClickAnim: false);
            currentFocusArea = ENavFocusArea.SubRegionList;
            if (null != regionGroup)
            {
                regionGroup.StopAllHoverEffects();
            }

            if (null != subRegionGroup)
            {
                focusedSubRegionIndex = subRegionGroup.GetFirstUnlockedSubRegionIndex();
                subRegionGroup.FocusSubRegionButton(focusedSubRegionIndex);
                Sound.PlayUI(SoundID.NaviSubHover);
            }
        }
        else if (ENavFocusArea.SubRegionList == currentFocusArea)
        {
            if (null == subRegionGroup) return;
            IReadOnlyList<HUD_PopupNav_SubRegionBtn> _activeSubRegions = subRegionGroup.GetActiveSubRegionButtons();
            if (null == _activeSubRegions || focusedSubRegionIndex >= _activeSubRegions.Count) return;
            HUD_PopupNav_SubRegionBtn _btn = _activeSubRegions[focusedSubRegionIndex];
            if (null == _btn) return;
            if (false == _btn.IsUnlocked)
            {
                Sound.PlayUI(SoundID.NaviLocked);
                _btn.PlayLockedInteraction();
                return;
            }

            Sound.PlayUI(SoundID.MainClick);
            HandleSubRegionSelected(_btn.GetForestType());
        }
    }

    private void OnUICancelPressed()
    {
        if (false == gameObject.activeInHierarchy || true == isClosing) return;
        if (null != demoNotice && true == demoNotice.IsDemoNoticeActive)
        {
            if (false == demoNotice.IsHiding)
            {
                demoNotice.HideDemoNoticeOverlay();
            }

            return;
        }

        if (true == IsInputBlocked) return;
        // 소지역 필드에서 B버튼을 누르면 대지역 목록으로 복귀
        if (ENavFocusArea.SubRegionList == currentFocusArea)
        {
            currentFocusArea = ENavFocusArea.RegionList;
            if (null != subRegionGroup)
            {
                subRegionGroup.StopAllHoverEffects();
            }

            if (null != regionGroup)
            {
                int _curIdx = regionGroup.GetActiveRegionIndex(currentSelectedMapType);
                focusedRegionIndex = (0 <= _curIdx) ? _curIdx : 0;
                regionGroup.FocusRegionButton(focusedRegionIndex);
            }

            Sound.PlayUI(SoundID.NaviClose);
            return;
        }

        // 대지역 목록에서 B버튼을 누르면 팝업 닫기
        Close();
    }

    private void OnInputDeviceChanged(EInputDeviceType _device)
    {
        if (false == gameObject.activeInHierarchy || true == isClosing) return;
        if (true == IsInputBlocked) return;

        // 데모 안내 패널이 열려있는 동안에는 메인 맵의 포커스 계승 로직을 실행하지 않음
        if (null != demoNotice && true == demoNotice.IsDemoNoticeShowing) return;

        HUD_PopupNav_SubRegionBtn _hoveredSubBtn = (null != subRegionGroup) ? subRegionGroup.GetHoveredSubRegionButton() : null;
        HUD_PopupNav_RegionBtn _hoveredRegionBtn = (null != regionGroup) ? regionGroup.GetHoveredRegionButton() : null;
        if (EInputDeviceType.Gamepad == _device)
        {
            if (null != _hoveredSubBtn && true == _hoveredSubBtn.IsUnlocked)
            {
                // 마우스가 해금된 소지역 버튼 위에 있었던 경우: 소지역 필드 포커스 시작
                currentFocusArea = ENavFocusArea.SubRegionList;
                int _subIdx = subRegionGroup.GetSubRegionIndex(_hoveredSubBtn.GetForestType());
                focusedSubRegionIndex = (0 <= _subIdx) ? _subIdx : subRegionGroup.GetFirstUnlockedSubRegionIndex();
                subRegionGroup.FocusSubRegionButton(focusedSubRegionIndex);
                if (null != regionGroup) regionGroup.StopAllHoverEffects();
            }
            else if (null != _hoveredRegionBtn && true == _hoveredRegionBtn.IsUnlocked && false == IsDemoRestrictedMapType(_hoveredRegionBtn.GetMapType()))
            {
                // 마우스가 해금되고 데모 제한이 아닌 유효 대지역 버튼 위에 있었던 경우만 포커스 시작
                currentFocusArea = ENavFocusArea.RegionList;
                int _regIdx = regionGroup.GetActiveRegionIndex(_hoveredRegionBtn.GetMapType());
                focusedRegionIndex = (0 <= _regIdx) ? _regIdx : GetFirstUnlockedAndPlayableRegionIndex();
                regionGroup.FocusRegionButton(focusedRegionIndex);
                if (null != subRegionGroup) subRegionGroup.StopAllHoverEffects();
                HandleRegionSelected(_hoveredRegionBtn.GetMapType(), false, false);
            }
            else
            {
                // 마우스가 없거나, 잠긴 버튼/데모 제한 버튼 위에 있었던 경우: 안전한 초기 포커스로 보정
                if (null != regionGroup) regionGroup.StopAllHoverEffects();
                if (null != subRegionGroup) subRegionGroup.StopAllHoverEffects();
                SetupInitialGamepadFocus();
            }
        }
        else if (EInputDeviceType.KeyboardMouse == _device)
        {
            currentFocusArea = ENavFocusArea.None;
            if (null != _hoveredSubBtn)
            {
                // 마우스 위치에 소지역 버튼이 있으면 호버 유지 및 나머지 정리
                if (null != regionGroup) regionGroup.StopAllHoverEffects();
                if (null != subRegionGroup)
                {
                    int _subIdx = subRegionGroup.GetSubRegionIndex(_hoveredSubBtn.GetForestType());
                    subRegionGroup.FocusSubRegionButton(_subIdx);
                }
            }
            else if (null != _hoveredRegionBtn)
            {
                // 마우스 위치에 대지역 버튼이 있으면 호버 유지 및 나머지 정리
                if (null != subRegionGroup) subRegionGroup.StopAllHoverEffects();
                if (null != regionGroup)
                {
                    int _regIdx = regionGroup.GetActiveRegionIndex(_hoveredRegionBtn.GetMapType());
                    regionGroup.FocusRegionButton(_regIdx);
                }
            }
            else
            {
                // 마우스가 어떤 버튼에도 올라가 있지 않으면 마우스 좌표 기준으로 재평가
                if (null != regionGroup) regionGroup.EvaluateAllHoverStates();
                if (null != subRegionGroup) subRegionGroup.EvaluateAllHoverStates();
                if (null != subRegionGroup && null == subRegionGroup.GetHoveredSubRegionButton() &&
                null != regionGroup && null == regionGroup.GetHoveredRegionButton() &&
                null != cursorBoxUI)
                {
                    cursorBoxUI.HideImmediately();
                }
            }
        }
    }

#endregion

    /// <summary>
    /// 데모 제한을 적용해야 하는지입니다.
    ///
    /// 빌드에서는 인스펙터 값(isDemoVersion)을 보지 않고 BuildInfo만 따릅니다. 데모/정식은
    /// 세이브 변형과 Steam 앱까지 갈리는 구분이라, 씬 값에 맡기면 체크를 깜빡한 채로
    /// 업로드될 수 있기 때문입니다. 이제 BAOBAB_FULL_RELEASE 디파인 하나가 전부 결정합니다.
    ///
    /// 에디터에서는 인스펙터 토글로도 켤 수 있게 남겨둡니다. 디파인을 바꿔 재컴파일하지 않고도
    /// 데모 제한이 걸린 화면을 확인할 수 있어야 하기 때문입니다.
    /// (개발 중 기본값이 데모라 이 토글은 정식 빌드를 테스트할 때만 의미가 있습니다)
    /// </summary>
    private bool IsDemoRestrictionEnabled
    {
        get
        {
#if UNITY_EDITOR
            if (true == debugForceUnlockAll)
            {
                return false;
            }
#endif
            return BuildInfo.IsDemo;
        }
    }

    public bool IsGamepadMode => (null != inputManager && true == inputManager.IsGamepadMode);
    public HUD_PopupNav_RegionGroup RegionGroup => regionGroup;
    public HUD_PopupNav_SubRegionGroup SubRegionGroup => subRegionGroup;

    public void StopAllSubRegionHoverEffects()
    {
        if (null != subRegionGroup)
        {
            subRegionGroup.StopAllHoverEffects();
        }
    }

    public void StopAllRegionHoverEffects()
    {
        if (null != regionGroup)
        {
            regionGroup.StopAllHoverEffects();
        }
    }

    public bool IsDemoRestrictedMapType(MapType _mapType)
    {
        if (false == IsDemoRestrictionEnabled) return false;
        if (MapType.None == _mapType || MapType.Town == _mapType) return false;
        return maxPlayableMapTypeInDemo < _mapType;
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

    public void HandleDemoNoticeClosing()
    {
        // 데모 패널이 닫히기 시작할 때 대지역 버튼들을 부드럽게 언호버 및 선택 지역 상태로 복귀
        MapType _targetMap = (MapType.None != currentSelectedMapType && false == IsDemoRestrictedMapType(currentSelectedMapType))
        ? currentSelectedMapType
        : maxPlayableMapTypeInDemo;
        if (null != regionGroup)
        {
            regionGroup.SetSelectRegion(_targetMap, true);
        }
    }

    public void HandleDemoNoticeClosed()
    {
        demoNoticeClosedGraceTime = Time.unscaledTime + 0.15f;
        // 안전하게 현재 플레이 가능한 대지역으로 UI 복구 및 마우스 호버 상태 재평가
        MapType _targetMap = (MapType.None != currentSelectedMapType && false == IsDemoRestrictedMapType(currentSelectedMapType))
        ? currentSelectedMapType
        : maxPlayableMapTypeInDemo;

        currentSelectedMapType = _targetMap;

        if (null != regionGroup)
        {
            regionGroup.SetSelectRegion(_targetMap, false);
            regionGroup.EvaluateAllHoverStates();
            int _regIdx = regionGroup.GetActiveRegionIndex(_targetMap);
            focusedRegionIndex = (0 <= _regIdx) ? _regIdx : GetFirstUnlockedAndPlayableRegionIndex();
        }

        if (null != subRegionGroup)
        {
            subRegionGroup.EvaluateAllHoverStates();
        }

        if (null != inputManager && true == inputManager.IsGamepadMode && null != regionGroup)
        {
            currentFocusArea = ENavFocusArea.RegionList;
            regionGroup.FocusRegionButton(focusedRegionIndex);
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
        if (null != inputManager && null != inputManager.inputReader)
        {
            if (null != cachedOnMoveEvent) inputManager.inputReader.MoveEvent -= cachedOnMoveEvent;
            if (null != cachedOnInteractionKeyPressed) inputManager.inputReader.InteractionKeyPressedEvent -= cachedOnInteractionKeyPressed;
            if (null != cachedOnUICancel) inputManager.inputReader.UICancelEvent -= cachedOnUICancel;
            if (null != cachedOnInputDeviceChanged) inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
        }

        cachedOnMoveEvent = null;
        cachedOnInteractionKeyPressed = null;
        cachedOnUICancel = null;
        cachedOnInputDeviceChanged = null;
        inputManager = null;
    }
}
