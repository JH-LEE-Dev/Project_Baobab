using System.Collections;
using System;
using UnityEngine;

public class InDungeonSystem : MonoBehaviour
{
    public event Action ActivatePortalEvent;
    public event Action GoToMainMenuCurtainRevealEvent;

    private SignalHub signalHub;
    public InDungeonObjectManager inDungeonObjectManager { get; private set; }
    public InDungeonUnitSpawner inDungeonUnitSpawner { get; private set; }
    private IEnvironmentProvider environmentProvider;
    private HiddenmapManager hiddenmapManager;
    private InputManager inputManager;
    private IInventory characterInventory;
    private OffroadContainer offroadContainer;
    private InDungeonProductionManager inDungeonProductionManager;
    private Character character;
    private SkyCameraProductionManager skyCameraProductionManager;
    public InDungeonResultManager inDungeonResultManager { get; private set; }
    private InDungeonStateManager inDungeonStateManager;

    [Header("Dungeon Data Base")]
    [SerializeField] private DungeonValueDataBase dungeonDataBase;

    private MapType currentMapType;
    private ForestType currentForestType;

    private bool bCurrentlyDungeonScene = false;
    private bool prevbCurrentlyDungeonScene = false;
    private bool bRetryGame = false;
    private bool bGoingToMainMenu = false;
    private MapType selectedMapType;
    private ForestType selectedForestType;
    private bool bIsFromMainMenu = false;

    // "포자 포션" - 던전 입장 후 캐릭터가 실제로 조작 가능해진 시점(ActivateCharacterSignal)부터만
    // PotionKey를 허용한다. 매 던전 진입마다(StartDungeonSystem) 리셋된다.
    private bool bCharacterActivated;

    // 이번 던전에서 스테이지 BGM을 이미 재생했는지. 매 던전 진입마다(StartDungeonSystem) 리셋된다.
    private bool bDungeonBGMPlayed;

    // MainMenu → Dungeon 튜토리얼: 캐릭터를 차량 탑승 위치로 옮기기 직전의 위치(= 차를 타지 않았다면
    // 던전에서 서 있었어야 할 위치). 로고 연출이 끝난 뒤 하차할 때 정확히 이 자리로 되돌린다.
    private Vector3 characterDungeonStartPos;
    private bool bTutorialRideExitPending;
    private Coroutine tutorialRideExitCoroutine;

    // MainMenu → Dungeon 튜토리얼: OffroadContainer 상호작용은 "CutTree 완료 안내 UI가 완전히
    // 사라짐"과 "FillOffroadContainer 스텝이 실제로 시작됨(= 원목을 직접 2개 주움)" 두 조건이
    // 모두 만족된 뒤에만 열어준다. 안내 UI가 사라지는 즉시 열어버리면, 아직 원목을 1개만 주운
    // 상태에서 바로 컨테이너에 넣었을 때 TutorialSystem이 currentStep을 여전히 CutTree로 보고
    // 그 이관/제거 신호를 놓쳐버려 FillOffroadContainer가 영영 완료되지 않는 문제가 있었다.
    private bool bCutTreeQuestUIHideCompleted;
    private bool bFillOffroadContainerStepStarted;

    // MainMenu → Dungeon 튜토리얼: 피로도가 바닥값(19.1%)에 닿는지 감시하는 코루틴. 이번 원정 안에서만
    // 유효하므로 마을에 도착하면(TownStarted) 중단한다.
    private Coroutine waitStaminaFloorCoroutine;

    public void Initialize(SignalHub _signalHub, IEnvironmentProvider _environmentProvider, IInventoryChecker _inventoryChecker,
    InputManager _inputManager, IInventory _characterInventory, OffroadContainer _offroadContainer, SkyCameraProductionManager _skyCameraProductionManager,
    InDungeonResultManager _inDungeonResultManager)
    {
        inputManager = _inputManager;
        environmentProvider = _environmentProvider;
        signalHub = _signalHub;
        characterInventory = _characterInventory;
        offroadContainer = _offroadContainer;
        skyCameraProductionManager = _skyCameraProductionManager;
        inDungeonResultManager = _inDungeonResultManager;

        inDungeonObjectManager = GetComponentInChildren<InDungeonObjectManager>();
        inDungeonObjectManager.Initialize(environmentProvider, _inventoryChecker, inputManager, characterInventory, offroadContainer,
        inDungeonResultManager);

        inDungeonUnitSpawner = GetComponentInChildren<InDungeonUnitSpawner>();
        inDungeonUnitSpawner.Initialize(environmentProvider, inDungeonObjectManager, offroadContainer);

        hiddenmapManager = GetComponentInChildren<HiddenmapManager>();
        hiddenmapManager.Initialize();

        inDungeonStateManager = GetComponentInChildren<InDungeonStateManager>();

        inDungeonProductionManager = GetComponentInChildren<InDungeonProductionManager>();
        inDungeonProductionManager.Initialize(inputManager, _skyCameraProductionManager);

        BindEvents();
        SubscribeSignals();
    }

    public void Release()
    {
        ReleaseEvents();
        UnSubscribeSignals();

        if (tutorialRideExitCoroutine != null)
        {
            StopCoroutine(tutorialRideExitCoroutine);
            tutorialRideExitCoroutine = null;
        }

        inDungeonProductionManager.Release();
        inDungeonObjectManager.Release();
    }

    public void StartDungeonSystem(SceneChangeData _sceneChangeData)
    {
        inDungeonResultManager.Reset();
        bCharacterActivated = false;
        bTutorialRideExitPending = false;
        bDungeonBGMPlayed = false;
        bIsFromMainMenu = (_sceneChangeData.prevScene == SceneType.MainMenu);

        if (bIsFromMainMenu)
        {
            // MainMenu → Dungeon: Town의 포탈 선택(DungeonSelectedSignal)을 거치지 않으므로,
            // BGM 재생(CharacterActivated)/재도전(GoToDungeonSignal)/던전 상태 판정(CalcDungeonState)이
            // 참조하는 selectedMapType/selectedForestType을 여기서 직접 동기화해야 한다.
            selectedMapType = _sceneChangeData.mapType;
            selectedForestType = _sceneChangeData.forestType;

            // MainMenu → Dungeon: Town→Dungeon 왕복에서는 직전 MainMenu→Town 진입 시 SetWhereIsCharacter(false)가
            // 공격 인디케이터(RadiusIndicator, 프리팹 기본값 active)를 이미 꺼둔 상태로 던전에 들어온다.
            // 여기는 Town을 거치지 않으므로 그 단계가 없어, 명시적으로 꺼줘야 ActivateCharacterSignal 전까지
            // 인디케이터가 노출되지 않는다.
            character?.DisableAttackComponent();

            // MainMenu → Dungeon: 인게임 HUD를 확실히 숨긴 상태에서 시작
            signalHub.Publish(new PopupUIDownSignal());
            inputManager.PauseMove(true);
            inputManager.PauseESCKey(true);

            // 던전 셋업이 시작되는 이 시점부터 곧바로 컨테이너 상호작용을 잠근다. OnTreesReady()에서
            // 잠그는 것만으로는 늦다 - 그 앞의 나무 초기 스폰(ReadyTrees)은 여러 프레임에 걸쳐 도는
            // 코루틴이라, 그 사이 프레임들에서는 bCollisionEnabled가 아직 기본값(true)이고 컨테이너도
            // 아직 차량 위치로 옮겨지기(ReadyPortal) 전이라 캐릭터 스폰 지점과 겹쳐 있다. 그 상태로
            // 물리 스텝이 한 번이라도 돌면 OnTriggerEnter2D → InteractStateEvent(true)가 발행되어
            // 월드 팝업의 슬롯 UI가 열리고 슬롯 오픈 SFX(HUDEverySlotOpen)가 재생되어 버린다.
            offroadContainer.DisableCollision();
            bCutTreeQuestUIHideCompleted = false;
            bFillOffroadContainerStepStarted = false;
        }

        currentMapType = _sceneChangeData.mapType;
        currentForestType = _sceneChangeData.forestType;

        inDungeonObjectManager.SetDungeonData(dungeonDataBase.GetDungeonData(currentMapType));
        inDungeonObjectManager.SetupForMapType(currentMapType);
        inDungeonObjectManager.SetupForForestType(currentForestType);
        inDungeonObjectManager.SetupItemManagerCulling();

        signalHub.Publish(new DungeonReadySignal(dungeonDataBase.GetDungeonData(currentMapType), currentForestType));
    }

    private void BindEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
        inDungeonObjectManager.PortalActivatedEvent += PortalActivated;

        inDungeonObjectManager.ItemAcquiredEvent -= ItemAcquired;
        inDungeonObjectManager.ItemAcquiredEvent += ItemAcquired;

        inDungeonObjectManager.TreeGetHitEvent -= TreeGetHit;
        inDungeonObjectManager.TreeGetHitEvent += TreeGetHit;

        inDungeonObjectManager.TreeGemTransformedEvent -= TreeGemTransformed;
        inDungeonObjectManager.TreeGemTransformedEvent += TreeGemTransformed;

        inDungeonObjectManager.TreeShieldRecoveringEvent -= TreeShieldRecovering;
        inDungeonObjectManager.TreeShieldRecoveringEvent += TreeShieldRecovering;

        inDungeonObjectManager.CarrotItemAcquiredEvent -= CarrotItemAcquired;
        inDungeonObjectManager.CarrotItemAcquiredEvent += CarrotItemAcquired;

        inDungeonObjectManager.TreeDeadEvent -= TreeIsDead;
        inDungeonObjectManager.TreeDeadEvent += TreeIsDead;

        inDungeonObjectManager.OffroadSpawnedEvent -= OffroadSpawned;
        inDungeonObjectManager.OffroadSpawnedEvent += OffroadSpawned;

        inDungeonObjectManager.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        inDungeonObjectManager.OffroadInteractStateChangedEvent += OffroadInteractStateChanged;

        inDungeonObjectManager.RepairBoxInteractStateChangedEvent -= RepairBoxInteractStateChanged;
        inDungeonObjectManager.RepairBoxInteractStateChangedEvent += RepairBoxInteractStateChanged;

        inDungeonObjectManager.RideOffroadEvent -= RideOffroad;
        inDungeonObjectManager.RideOffroadEvent += RideOffroad;

        inDungeonObjectManager.DropAllItemEvent -= DropAllItem;
        inDungeonObjectManager.DropAllItemEvent += DropAllItem;

        inDungeonObjectManager.LostAndFoundBoxAcquiredEvent -= LostAndFoundBoxAcquired;
        inDungeonObjectManager.LostAndFoundBoxAcquiredEvent += LostAndFoundBoxAcquired;

        inputManager.inputReader.PotionKeyPressedEvent -= PotionKeyPressed;
        inputManager.inputReader.PotionKeyPressedEvent += PotionKeyPressed;

        inDungeonProductionManager.CharacterRideEndEvent -= CharacterRideEnd;
        inDungeonProductionManager.CharacterRideEndEvent += CharacterRideEnd;

        inDungeonProductionManager.CameraUpIsEndEvent -= CameraUpIsEnd;
        inDungeonProductionManager.CameraUpIsEndEvent += CameraUpIsEnd;

        inDungeonProductionManager.CameraDownEndEvent -= CameraDownIsEnd;
        inDungeonProductionManager.CameraDownEndEvent += CameraDownIsEnd;

        inDungeonProductionManager.RollbackSkyProductionEvent -= RollbackSkyProduction;
        inDungeonProductionManager.RollbackSkyProductionEvent += RollbackSkyProduction;

        inDungeonProductionManager.GoToMainMenuReadyEvent -= GoToMainMenuReady;
        inDungeonProductionManager.GoToMainMenuReadyEvent += GoToMainMenuReady;

        inDungeonProductionManager.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;
        inDungeonProductionManager.GoToMainMenuCurtainRevealEvent += GoToMainMenuCurtainReveal;

        inDungeonObjectManager.ActivateWarningUIEvent -= ActivateWarningUI;
        inDungeonObjectManager.ActivateWarningUIEvent += ActivateWarningUI;

        inDungeonObjectManager.NPCPauseRequestedEvent -= NPCPauseRequested;
        inDungeonObjectManager.NPCPauseRequestedEvent += NPCPauseRequested;

        inDungeonObjectManager.FlyingItemPauseRequestedEvent -= FlyingItemPauseRequested;
        inDungeonObjectManager.FlyingItemPauseRequestedEvent += FlyingItemPauseRequested;

        inDungeonObjectManager.FlyingItemResumeRequestedEvent -= FlyingItemResumeRequested;
        inDungeonObjectManager.FlyingItemResumeRequestedEvent += FlyingItemResumeRequested;

        inDungeonObjectManager.FlyingItemDismissRequestedEvent -= FlyingItemDismissRequested;
        inDungeonObjectManager.FlyingItemDismissRequestedEvent += FlyingItemDismissRequested;
    }

    private void ReleaseEvents()
    {
        inDungeonObjectManager.PortalActivatedEvent -= PortalActivated;
        inDungeonObjectManager.ItemAcquiredEvent -= ItemAcquired;
        inDungeonObjectManager.TreeGetHitEvent -= TreeGetHit;
        inDungeonObjectManager.TreeGemTransformedEvent -= TreeGemTransformed;
        inDungeonObjectManager.TreeShieldRecoveringEvent -= TreeShieldRecovering;
        inDungeonObjectManager.CarrotItemAcquiredEvent -= CarrotItemAcquired;
        inDungeonObjectManager.TreeDeadEvent -= TreeIsDead;
        inDungeonObjectManager.OffroadSpawnedEvent -= OffroadSpawned;
        inDungeonObjectManager.OffroadInteractStateChangedEvent -= OffroadInteractStateChanged;
        inDungeonObjectManager.RepairBoxInteractStateChangedEvent -= RepairBoxInteractStateChanged;
        inDungeonObjectManager.RideOffroadEvent -= RideOffroad;
        inDungeonObjectManager.DropAllItemEvent -= DropAllItem;
        inDungeonObjectManager.LostAndFoundBoxAcquiredEvent -= LostAndFoundBoxAcquired;
        inputManager.inputReader.PotionKeyPressedEvent -= PotionKeyPressed;
        inDungeonProductionManager.CharacterRideEndEvent -= CharacterRideEnd;
        inDungeonProductionManager.CameraUpIsEndEvent -= CameraUpIsEnd;
        inDungeonProductionManager.CameraDownEndEvent -= CameraDownIsEnd;
        inDungeonProductionManager.RollbackSkyProductionEvent -= RollbackSkyProduction;
        inDungeonProductionManager.GoToMainMenuReadyEvent -= GoToMainMenuReady;
        inDungeonProductionManager.GoToMainMenuCurtainRevealEvent -= GoToMainMenuCurtainReveal;
        inDungeonObjectManager.ActivateWarningUIEvent -= ActivateWarningUI;
        inDungeonObjectManager.NPCPauseRequestedEvent -= NPCPauseRequested;
        inDungeonObjectManager.FlyingItemPauseRequestedEvent -= FlyingItemPauseRequested;
        inDungeonObjectManager.FlyingItemResumeRequestedEvent -= FlyingItemResumeRequested;
        inDungeonObjectManager.FlyingItemDismissRequestedEvent -= FlyingItemDismissRequested;
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.Subscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.Subscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.Subscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.Subscribe<WarningUIClosedSignal>(WarningUIClosed);
        signalHub.Subscribe<ActivateCharacterSignal>(CharacterActivated);
        signalHub.Subscribe<TownStartedSignal>(TownStarted);
        signalHub.Subscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
        signalHub.Subscribe<CompanyLogoProductionCompletedSignal>(CompanyLogoProductionCompleted);
        signalHub.Subscribe<DungeonBGMStartSignal>(DungeonBGMStart);
        signalHub.Subscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.Subscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
        signalHub.Subscribe<TutorialQuestHideCompletedSignal>(TutorialQuestHideCompleted);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<MapGeneratedSignal>(MapGenerated);
        signalHub.UnSubscribe<GoHomeButtonClickedSignal>(GoHome);
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
        signalHub.UnSubscribe<RetryButtonClickedSignal>(RetryButtonClicked);
        signalHub.UnSubscribe<DungeonSelectedSignal>(DungeonSelected);
        signalHub.UnSubscribe<WarningUIClosedSignal>(WarningUIClosed);
        signalHub.UnSubscribe<ActivateCharacterSignal>(CharacterActivated);
        signalHub.UnSubscribe<TownStartedSignal>(TownStarted);
        signalHub.UnSubscribe<GoToMainMenuRequestedSignal>(GoToMainMenuRequested);
        signalHub.UnSubscribe<CompanyLogoProductionCompletedSignal>(CompanyLogoProductionCompleted);
        signalHub.UnSubscribe<DungeonBGMStartSignal>(DungeonBGMStart);
        signalHub.UnSubscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.UnSubscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
        signalHub.UnSubscribe<TutorialQuestHideCompletedSignal>(TutorialQuestHideCompleted);
    }

    private void PortalActivated()
    {
        signalHub.Publish(new PortalActivatedSignal());
    }

    private void MapGenerated(MapGeneratedSignal mapGeneratedSignal)
    {
        // 나무 초기 스폰은 프레임 분산 코루틴으로 실행되어 즉시 끝나지 않을 수 있다. 구름이 걷히는
        // 트리거(DungeonStartSignal/RollbackCameraMove 포함)를 담은 나머지 로직은 반드시 스폰이
        // 실제로 끝난 뒤(OnTreesReady)에 실행되어야, 스폰 도중 구름이 걷혀버리는 일이 없다.
        inDungeonObjectManager.ReadyTrees(mapGeneratedSignal.grassTilePositions, OnTreesReady);
    }

    private void OnTreesReady()
    {
        inDungeonObjectManager.ReadyPortal();
        inDungeonProductionManager.Offroad_DI(inDungeonObjectManager.offroadVehicle);

        // MainMenu → Dungeon: 나무가 전부 생성된 이 시점에 카메라를 상승 완료 위치로 즉시 배치.
        // 이후 DungeonStartSignal → TownSystem.DungeonStarted → RollbackCameraMove로 하강이 시작된다.
        if (bIsFromMainMenu)
        {
            skyCameraProductionManager.PrepareForDescend(character.transform);
        }

        signalHub.Publish(new DungeonStartSignal(inDungeonObjectManager.GetPlayerStartPos()));
        inDungeonUnitSpawner.SpawnNPC();

        signalHub.Publish(new DecalreDungeonTypeSignal(currentMapType, currentForestType));

        character.gameObject.SetActive(true);

        // MainMenu → Dungeon: 캐릭터를 차량 탑승 위치로 옮겨 탑승 상태로 표시한 뒤 다시 숨기고,
        // 차량은 시동이 걸린 공회전(덜덜거림)만 실행시킨다. 카메라는 캐릭터 원래 위치로 하강한 뒤
        // Follow/LookAt 없이 그 자리에 그대로 정지한다. 실제 하차는 스튜디오 로고 연출이 끝난 뒤
        // (CompanyLogoProductionCompletedSignal) 처리하고, 조작 활성화는 그 이후 별도 트리거에서 다룬다.
        if (bIsFromMainMenu)
        {
            skyCameraProductionManager.ClearFollowAndLookAtOnArrive();

            // 하차 시 되돌아갈 원래 위치를 탑승 위치로 옮기기 전에 기억해둔다.
            characterDungeonStartPos = character.transform.position;
            bTutorialRideExitPending = true;

            var offroadVehicle = inDungeonObjectManager.offroadVehicle;
            if (offroadVehicle != null && offroadVehicle.CharacterRidePoint != null)
            {
                character.transform.position = offroadVehicle.CharacterRidePoint.position;
            }

            character.bRide = true;
            character.gameObject.SetActive(false);

            if (offroadVehicle != null)
            {
                offroadVehicle.StartEngineIdle();
            }

            // MainMenu → Dungeon 튜토리얼: 처음엔 둘 다 상호작용 불가로 시작한다.
            // OffroadContainer는 나무 벌목 퀘스트가, OffroadVehicle은 원목 이관 퀘스트가 각각
            // 끝나고 그 완료 안내 UI까지 완전히 사라지면 TutorialQuestHideCompleted에서 열어준다.
            //
            // 컨테이너 잠금은 이미 StartDungeonSystem()에서 걸어뒀지만 여기서 한 번 더 건다.
            // 이 메서드 위쪽의 ReadyPortal()이 OffroadVehicleObj.ResetObject()를 호출하고,
            // 그 안에서 EnableCollision()을 무조건 다시 켜버리기 때문이다(차량 배치용 공통 경로).
            // 따라서 이 재잠금은 반드시 ReadyPortal() 이후에 있어야 한다.
            offroadVehicle?.SetCanTravel(false);
            offroadContainer.DisableCollision();
        }

        CameraMoveController.Instance.SetupCamera();

        if (bRetryGame == false)
        {
            prevbCurrentlyDungeonScene = bCurrentlyDungeonScene;
            bCurrentlyDungeonScene = true;
            inDungeonProductionManager.bCurrentlyDungeonScene = true;
        }
        else
        {
            inDungeonProductionManager.RollbackCameraMove();
        }

        // 스테이지 배너(HUD_Message)의 문구는 이 신호로만 채워지고, 실제 노출은 HUD가 올라오는 시점
        // (PopupUIUpSignal → OnHUDGoUp)에 일어난다. MainMenu → Dungeon 튜토리얼도 하차 후 HUD가 올라오므로,
        // 여기서 빠뜨리면 배너가 값이 비어 있는 상태(None)로 떠버린다.
        signalHub.Publish(new DeclareDungeonStateSignal(inDungeonStateManager.CalcDungeonState(selectedMapType)));
    }

    private void ItemAcquired(Item _item)
    {
        signalHub.Publish(new ItemAcquiredSignal(_item));
    }

    private void TreeGetHit(TreeObj _treeObj)
    {
        signalHub.Publish(new TreeGetHitSignal(_treeObj));
    }

    private void TreeGemTransformed(TreeObj _treeObj)
    {
        signalHub.Publish(new TreeGemTransformedSignal(_treeObj));
    }

    private void TreeShieldRecovering(TreeObj _treeObj)
    {
        signalHub.Publish(new TreeShieldRecoveringSignal(_treeObj));
    }

    private void GoHome(GoHomeButtonClickedSignal goHomeButtonClickedSignal)
    {
        inputManager.PauseESCKey(true); // 던전→타운(귀환) 연출 시작 - 종료 시점은 InDungeonProductionManager.CameraDownIsEnd()

        // 카메라가 하늘로 올라가는 연출 시간(moveDuration) 안에 반드시 다 꺼지도록 같은 시간으로 페이드아웃한다.
        Sound.FadeOutBGM(skyCameraProductionManager.MoveDuration);

        inDungeonProductionManager.StartSkyProduction();

        offroadContainer.col.enabled = false;
        if (inDungeonObjectManager.offroadVehicle != null)
            inDungeonObjectManager.offroadVehicle.col.enabled = false;

        signalHub.Publish(new StartSkyProductionSignal());
    }

    private void CarrotItemAcquired(CarrotItem _carrotItem)
    {
        signalHub.Publish(new CarrotItemAcquiredSignal(_carrotItem.amount));
    }

    public void ClearInDungeonSystem()
    {
        if (bRetryGame == false)
        {
            prevbCurrentlyDungeonScene = bCurrentlyDungeonScene;
            bCurrentlyDungeonScene = false;
            inDungeonProductionManager.bCurrentlyDungeonScene = false;
        }

        inDungeonObjectManager.ClearObjManager();
        inDungeonUnitSpawner.ReleaseAllNPC();
    }

    /// <summary>
    /// Dungeon → Town 귀환 시, TownSystem.StartTownSystem()의 동기 초기화(그리드 생성, NPC 스폰)가
    /// 전부 끝난 뒤에 호출되어야 한다. 예전엔 ClearInDungeonSystem()이 StartTownSystem()보다 먼저
    /// 구름 걷힘 코루틴(0.75초 타이머)을 걸어둬서, Town 초기화가 0.75초보다 오래 걸리면 초기화 도중
    /// 구름이 걷혀버릴 수 있는 시간 경쟁 구조였다. 다른 전환 경로(Town→Dungeon, Retry 등)와 동일하게
    /// "무거운 초기화가 끝난 뒤에만 구름이 걷힌다"는 인과적 순서를 보장하기 위해 분리했다.
    /// 조건/필드는 ClearInDungeonSystem()이 이미 갱신해둔 값을 그대로 사용한다(그 사이 다른 코드가
    /// 이 필드들을 건드리지 않으므로 안전).
    /// </summary>
    public void NotifyTownSystemReady()
    {
        if ((prevbCurrentlyDungeonScene != bCurrentlyDungeonScene) && bRetryGame == false)
        {
            inDungeonProductionManager.RollbackCameraMove();
        }
    }

    private void AnimalHit(Animal _animal)
    {
        signalHub.Publish(new AnimalHitSignal(_animal));
    }

    private void TreeIsDead(TreeType _type, bool isPlayerKilled)
    {
        signalHub.Publish(new TreeIsDeadSignal(_type, isPlayerKilled));
    }

    private void AnimalIsDead(Animal _animal)
    {
        signalHub.Publish(new AnimalIsDeadSignal(_animal.animalType));
    }

    public void SetHiddenMapGrade()
    {
        inDungeonObjectManager.SetHiddenMapGrade(hiddenmapManager.CalcHiddenMapGrade());
    }

    public void ResetHiddenMapGrade()
    {
        inDungeonObjectManager.SetHiddenMapGrade(HiddenMapGrade.None);
    }

    private void CharacterSpawned(CharacterSpawnedSignal _characterSpawnedSignal)
    {
        character = _characterSpawnedSignal.character;
        inDungeonObjectManager.SetCharacter(_characterSpawnedSignal.character);
        inDungeonProductionManager.Character_DI(character);

        // 럼버잭 NPC들이 셰이크웨이브를 쓸 때 캐릭터의 StatComponent를 그대로 참조하도록 뒤늦게 주입.
        // (InDungeonUnitSpawner.Initialize() 시점엔 캐릭터가 아직 스폰되기 전이라 여기서 넘겨줘야 한다)
        inDungeonUnitSpawner.SetPlayerStatForShockWave(character.statComponent);
        // 부메랑도 동일한 이유로 캐릭터 스폰 이후 뒤늦게 주입한다.
        inDungeonUnitSpawner.SetPlayerStatForBoomerang(character.statComponent);
    }

    private void GameEnd()
    {
        signalHub.Publish(new GameEndSignal());
    }

    private void OffroadSpawned(OffroadVehicleObj _offroadVehicleObj)
    {
        signalHub.Publish(new OffroadSpawnedSignal(_offroadVehicleObj));
    }

    private void OffroadInteractStateChanged(bool _boolean)
    {
        signalHub.Publish(new OffroadInteractStateChangedSignal(_boolean));
    }

    private void RepairBoxInteractStateChanged(bool _boolean)
    {
        signalHub.Publish(new RepairBoxInteractStateChangedSignal(_boolean));
    }

    private void RideOffroad()
    {
        signalHub.Publish(new PopupUIDownSignal());
        inDungeonProductionManager.StartCharacterRide();
        signalHub.Publish(new CharacterRideStartSignal());

        offroadContainer.col.enabled = false;
        if (inDungeonObjectManager.offroadVehicle != null)
            inDungeonObjectManager.offroadVehicle.col.enabled = false;
    }

    private void DropAllItem()
    {
        signalHub.Publish(new DropAllItemSignal());
    }

    private void LostAndFoundBoxAcquired()
    {
        signalHub.Publish(new LostAndFoundBoxAcquiredSignal());
    }

    // "포자 포션" - 던전에 입장해 캐릭터가 실제로 조작 가능해진 시점부터만 특수 키(PotionKey)로 마실 수 있다.
    private void PotionKeyPressed()
    {
        if (bCurrentlyDungeonScene == false || bCharacterActivated == false) return;

        inDungeonObjectManager.TryDrinkSporePotion();
    }

    private void CharacterActivated(ActivateCharacterSignal _signal)
    {
        bCharacterActivated = true;

        // 캐릭터가 실제로 움직일 수 있게 되는 시점에 스테이지별 BGM을 재생한다.
        PlayDungeonBGM();
    }

    // MainMenu → Dungeon 튜토리얼: 이 경로엔 ActivateCharacterSignal이 없어 BGM 재생 지점도 없으므로,
    // 카메라 하강이 끝나는 시점(TownSystem.CameraDownIsEnd)에 BGM만 따로 받아 재생한다.
    private void DungeonBGMStart(DungeonBGMStartSignal _signal)
    {
        PlayDungeonBGM();
    }

    // 같은 던전에서 두 번 재생되어 트랙이 처음부터 다시 시작되는 일이 없도록 1회만 재생한다.
    // (튜토리얼에서 여기로 먼저 재생된 뒤, 나중에 ActivateCharacterSignal이 와도 이어서 흐른다)
    private void PlayDungeonBGM()
    {
        if (bDungeonBGMPlayed)
            return;

        bDungeonBGMPlayed = true;

        // 하위 ForestType(_1/_2/_3)과 무관하게 같은 대지역은 같은 BGM을 공유한다.
        switch (currentMapType)
        {
            case MapType.WideGreenForest:
                Sound.PlayBGM(SoundID.Stage1BGM);
                break;
            case MapType.FluffySporeForest:
                Sound.PlayBGM(SoundID.Stage2BGM);
                break;
            case MapType.StarrootForest:
                Sound.PlayBGM(SoundID.Stage3BGM);
                break;
            case MapType.MagmaForest:
                Sound.PlayBGM(SoundID.Stage4BGM);
                break;
        }
    }

    // MainMenu → Dungeon 튜토리얼: 스튜디오 로고 UI 연출이 끝나면 차량 시동을 끄고, 그 1초 뒤 캐릭터를 내린다.
    private void CompanyLogoProductionCompleted(CompanyLogoProductionCompletedSignal _signal)
    {
        if (bIsFromMainMenu == false || bTutorialRideExitPending == false)
            return;

        bTutorialRideExitPending = false;

        if (tutorialRideExitCoroutine != null)
            StopCoroutine(tutorialRideExitCoroutine);

        tutorialRideExitCoroutine = StartCoroutine(TutorialRideExitCoroutine());
    }

    private IEnumerator TutorialRideExitCoroutine()
    {
        // 1. 공회전 중이던 차량의 시동이 꺼지는 연출을 먼저 끝까지 재생한다.
        var offroadVehicle = inDungeonObjectManager.offroadVehicle;
        if (offroadVehicle != null)
        {
            yield return offroadVehicle.EngineShutdownSequence();
        }

        // 2. 시동이 완전히 꺼지고 1초 뒤에 하차한다.
        yield return new WaitForSeconds(1f);

        if (character == null)
        {
            tutorialRideExitCoroutine = null;
            yield break;
        }

        // 차를 타지 않았다면 서 있었어야 할 위치로 되돌리고 탑승 상태를 해제한다.
        // Town → Dungeon 진입 연출과 동일하게, 이 시점의 캐릭터는 화면에 보이되 조작은 잠긴 상태다.
        // (StartDungeonSystem에서 걸어둔 PauseMove/PauseESCKey/DisableAttackComponent가 그대로 유지된다)
        character.transform.position = characterDungeonStartPos;
        character.bRide = false;
        character.gameObject.SetActive(true);
        character.SetFacingDirection(Vector2.down);

        Sound.Play(SoundID.OffroadClose, character.transform.position);
        Sound.Play(SoundID.GetItem04, character.transform.position);

        // 튜토리얼 전용: 아이템 획득 때와 동일한 뽀잉(감쇠 진동 스케일) + 하얀 스프라이트 플래시(셰이더) 연출로 하차를 강조한다.
        character.PlayItemAcquireBounce();
        character.PlayItemAcquireSpriteFlash();
        inDungeonObjectManager.PlayCharacterGetOffVFX(character.transform.position);

        // 하차 시점부터 카메라는 다시 캐릭터를 따라간다(하강이 끝난 자리와 같은 위치라 튐이 없다).
        skyCameraProductionManager.AttachFollowAndLookAt(character.transform);

        // 3. 하차 1초 뒤에 일반 던전 입장과 동일한 마무리(조작 개방 + 캐릭터 활성화 + HUD 복귀)를 실행한다.
        yield return new WaitForSeconds(1f);

        tutorialRideExitCoroutine = null;

        signalHub.Publish(new CompleteDungeonEntrySignal());
    }

    // MainMenu → Dungeon 튜토리얼: 퀘스트가 끝날 때마다 해당 상호작용을 순서대로 열어준다.
    // (처음엔 OnTreesReady()의 bIsFromMainMenu 분기에서 둘 다 잠가둔 상태로 시작한다)
    private void TutorialStepCompleted(TutorialStepCompletedSignal _signal)
    {
        switch (_signal.step)
        {
            case TutorialStep.FillOffroadContainer:
                // 튜토리얼 전용: 해당 퀘스트가 끝나면 피로도 바닥값이 19%로 낮아지는데,
                // 실제로 19%에 도달할 때 다음 퀘스트를 띄워주기 위해 여기서 체크를 시작한다.
                // (차량 상호작용은 퀘스트 UI가 완전히 사라진 후 TutorialQuestHideCompleted 에서 켠다)
                StartWaitUntilStaminaReachedFloor();
                break;
        }
    }

    private void TutorialQuestHideCompleted(TutorialQuestHideCompletedSignal _signal)
    {
        // 첫 퀘스트(나무 벌목)의 완료 안내 UI가 완전히 사라진 뒤에 컨테이너 상호작용을 열어준다.
        // 스텝 완료 즉시 열면 "나무를 벌목하세요" 안내가 아직 화면에 떠 있는 동안 컨테이너를
        // 쓸 수 있게 되어버린다.
        if (_signal.step == TutorialStep.CutTree)
        {
            bCutTreeQuestUIHideCompleted = true;
            TryEnableOffroadContainerCollision();
        }

        if (_signal.step == TutorialStep.FillOffroadContainer)
        {
            inDungeonObjectManager.offroadVehicle?.SetCanTravel(true);
        }
    }

    // TutorialSystem은 원목을 직접 2개 주워야 FillOffroadContainer 스텝을 시작한다(나무 한 그루만
    // 베고 바로 컨테이너로 가면 넣을 원목이 부족해서). 이 스텝이 실제로 시작된 신호를 받는다.
    private void TutorialStepStarted(TutorialStepStartedSignal _signal)
    {
        if (_signal.step == TutorialStep.FillOffroadContainer)
        {
            bFillOffroadContainerStepStarted = true;
            TryEnableOffroadContainerCollision();
        }
    }

    // CutTree 안내 UI가 사라짐과 FillOffroadContainer 스텝 시작(= 원목 2개 확보), 두 조건이 모두
    // 만족돼야 컨테이너를 열어준다. 어느 쪽이 먼저 만족될지 알 수 없다 - 원목을 빠르게 주우면 안내
    // UI가 사라지기 전에 스텝이 먼저 시작될 수 있고, 큰 나무를 베어 원목이 넉넉하면 안내 UI가 먼저
    // 사라질 수도 있다. 이렇게 게이팅해두면 컨테이너가 열리는 시점엔 currentStep이 항상
    // FillOffroadContainer이므로, TutorialSystem이 이관/제거 신호를 놓치는 경우가 생기지 않는다.
    private void TryEnableOffroadContainerCollision()
    {
        if (bCutTreeQuestUIHideCompleted == false || bFillOffroadContainerStepStarted == false)
            return;

        offroadContainer.EnableCollision();
    }

    private void StartWaitUntilStaminaReachedFloor()
    {
        StopWaitUntilStaminaReachedFloor();

        waitStaminaFloorCoroutine = StartCoroutine(WaitUntilStaminaReachedFloor());
    }

    private void StopWaitUntilStaminaReachedFloor()
    {
        if (waitStaminaFloorCoroutine == null)
            return;

        StopCoroutine(waitStaminaFloorCoroutine);
        waitStaminaFloorCoroutine = null;
    }

    private IEnumerator WaitUntilStaminaReachedFloor()
    {
        if (character != null && character.pHealthComponent != null)
        {
            float targetStamina = character.pHealthComponent.GetMaxStamina() * 0.191f;
            while (character.pHealthComponent.GetCurrentStamina() > targetStamina)
            {
                yield return null;
            }
        }

        waitStaminaFloorCoroutine = null;

        signalHub.Publish(new TutorialStaminaReachedFloorSignal());
    }

    // "포자 포션" - 실제 Town 씬이 로드된 시점에 이번 원정에서 마시지 않았다면 충전한다.
    private void TownStarted(TownStartedSignal _signal)
    {
        inDungeonObjectManager.RefillSporePotionCharge();

        // 피로도 바닥값 감시는 이번 원정 안에서만 의미가 있다. 플레이어가 바닥값에 닿기 전에 스스로
        // 귀환하면(GoHomeBeforeExhausted 스킵) 마을 진입 시 StaminaReset()으로 피로도가 최대치로
        // 돌아가 루프가 영원히 끝나지 않으므로, 여기서 확실히 정리한다.
        StopWaitUntilStaminaReachedFloor();
    }

    private void CharacterRideEnd()
    {
        GameEnd();
    }

    private void CameraUpIsEnd()
    {
        if (bCurrentlyDungeonScene == false)
            return;

        if (bRetryGame == false)
            signalHub.Publish(new GoToHomeSignal());
        else
        {
            inDungeonObjectManager.ClearObjManager();
            inDungeonUnitSpawner.ReleaseAllNPC();

            signalHub.Publish(new GoToDungeonSignal(selectedMapType, selectedForestType));
        }
    }

    private void CameraDownIsEnd()
    {
        if (bCurrentlyDungeonScene == true && bRetryGame == false)
            return;

        // 재도전으로 던전에 다시 들어가는 경우. 카메라 하강이 끝나면서 방금 PauseMove(false)로
        // 조작이 풀렸으므로(InDungeonProductionManager.CameraDownIsEnd), 조준도 여기서 같이 켠다.
        // 아래 PopupUIGoUPCoroutine의 ActivateCharacterSignal은 0.7초 뒤라 그동안 조준이 잠긴다.
        if (bCurrentlyDungeonScene == true)
        {
            signalHub.Publish(new EnableCharacterAimSignal());
        }

        StartCoroutine(PopupUIGoUPCoroutine());

        if (bRetryGame == true)
        {
            bRetryGame = false;
            inDungeonProductionManager.bRetryGame = false;
        }
    }

    private IEnumerator PopupUIGoUPCoroutine()
    {
        yield return new WaitForSeconds(0.7f);

        // 대기하는 0.7초 사이에 ESC로 메인메뉴 이탈이 요청됐다면, 이미 내려가고 있는 HUD를
        // 다시 올리면 안 되므로 여기서 멈춘다(HUDDown 직후 HUDUp이 뒤따라오던 레이스 컨디션 방지).
        if (bGoingToMainMenu == true)
            yield break;

        signalHub.Publish(new PopupUIUpSignal());

        if (bCurrentlyDungeonScene == true)
        {
            // AttackIndicator(공격 사거리 인디케이터)는 캐릭터가 움직일 수 있게 되는 시점이 아니라
            // HUD가 올라오는 이 시점에 함께 나타나야 자연스러우므로, 캐릭터 활성화를 여기로 옮겼다.
            signalHub.Publish(new ActivateCharacterSignal());

            StartCoroutine(StaminaDecreaseCoroutine());
        }
        else
        {
            offroadContainer.col.enabled = true;
            if (inDungeonObjectManager.offroadVehicle != null)
                inDungeonObjectManager.offroadVehicle.col.enabled = true;

            ActivatePortalEvent?.Invoke();

            inputManager.PauseInteractKey(false);

            // 던전→마을 귀환의 실제 완료 시점(카메라 하강 연출 종료 + 조작 가능). TownSystem 쪽에도 동일한
            // 신호 발행 코드가 있지만 그쪽은 bCurrentlyTownScene이 이미 true라 이 경로에서는 호출되지 않으므로,
            // 실제로 이 흐름을 타는 InDungeonSystem 쪽에서 발행해야 튜토리얼의 PutItemsInLogContainer 퀘스트가 시작된다.
            signalHub.Publish(new ReturnToTownCameraDownEndedSignal());
        }
    }

    private IEnumerator StaminaDecreaseCoroutine()
    {
        yield return new WaitForSeconds(0.7f);
        signalHub.Publish(new StartDecreaseStaminaSignal());

        offroadContainer.col.enabled = true;
        if (inDungeonObjectManager.offroadVehicle != null)
            inDungeonObjectManager.offroadVehicle.col.enabled = true;

        inputManager.PauseInteractKey(false);
    }

    private void RollbackSkyProduction()
    {
        signalHub.Publish(new RollbackSkyProductionSignal());
    }

    private void GoToMainMenuRequested(GoToMainMenuRequestedSignal _signal)
    {
        // Town에 있을 때는 TownSystem이 처리하고, 여기선 Dungeon이 실제로 활성화된 상태일 때만 처리한다.
        if (bCurrentlyDungeonScene == false || bGoingToMainMenu == true)
            return;

        bGoingToMainMenu = true;

        // 메인메뉴로 나갈 때도 카메라 상승 연출 시간 안에 BGM이 반드시 꺼지도록 페이드아웃한다.
        Sound.FadeOutBGM(skyCameraProductionManager.MoveDuration);

        signalHub.Publish(new StartSkyProductionSignal(true));
        signalHub.Publish(new PopupUIDownSignal());

        inDungeonProductionManager.StartGoToMainMenu();
    }

    private void GoToMainMenuReady()
    {
        signalHub.Publish(new GoToMainMenuSignal());
    }

    private void GoToMainMenuCurtainReveal()
    {
        GoToMainMenuCurtainRevealEvent?.Invoke();
    }

    private void RetryButtonClicked(RetryButtonClickedSignal _retryButtonClickedSignal)
    {
        bRetryGame = true;
        inDungeonProductionManager.bRetryGame = true;

        // 던전<->타운 전환(GoHome/GoToMainMenuRequested)과 동일하게, 카메라가 하늘로
        // 올라가는 연출 시간 안에 반드시 다 꺼지도록 같은 시간으로 페이드아웃한다.
        // 재생 재개는 새 던전 진입 후 CharacterActivated()가 담당한다.
        Sound.FadeOutBGM(skyCameraProductionManager.MoveDuration);

        // 리트라이 카메라 연출 시작 - 종료 시점은 InDungeonProductionManager.CameraDownIsEnd()
        inputManager.PauseESCKey(true);

        inDungeonProductionManager.StartSkyProduction();
        signalHub.Publish(new StartSkyProductionSignal());
    }

    private void DungeonSelected(DungeonSelectedSignal _dungeonSelectedSignal)
    {
        selectedMapType = _dungeonSelectedSignal.type;
        selectedForestType = _dungeonSelectedSignal.forestType;
    }

    private void ActivateWarningUI()
    {
        signalHub.Publish(new ActivateWarningUISignal());
    }

    private void WarningUIClosed(WarningUIClosedSignal _warningUIClosedSignal)
    {
        if (bCurrentlyDungeonScene == false)
            return;

        if (_warningUIClosedSignal.bResult == true)
        {
            inDungeonObjectManager.HandleGameEnd();
        }
        else
        {
            inDungeonObjectManager.AbortGameEnd(true);
        }
    }

    public void ActivatePortal()
    {
        if (inDungeonObjectManager.offroadVehicle != null)
            inDungeonObjectManager.offroadVehicle.col.enabled = true;
    }

    private void NPCPauseRequested(bool _pause)
    {
        if (_pause)
            inDungeonUnitSpawner.PauseAllNPC();
        else
            inDungeonUnitSpawner.ResumeAllNPC();
    }

    private void FlyingItemPauseRequested()
    {
        offroadContainer.PauseAllFlyingItems();
    }

    private void FlyingItemResumeRequested()
    {
        offroadContainer.ResumeAllFlyingItems();
    }

    private void FlyingItemDismissRequested()
    {
        offroadContainer.DismissAllFlyingItems();
    }
}
