using System;
using UnityEngine;

public class LogProcessingManager : MonoBehaviour, ILogProcessingSystemCH
{
    public event Action<bool> LogProcessorIsActiveEvent;
    public event Action<bool> ShopInteracteStateChangedEvent;
    public event Action LogContainerSpecChangedEvent;
    public event Action<int> EarnMoneyEvent;
    public event Action ContainerUpdatedEvent;
    public event Action<bool> InteractStateChangedEvent;

    // 외부 의존성

    [SerializeField] GameObject shopPrefab;
    [SerializeField] GameObject shopSpawnPoint;


    private GameObject shopObj;

    private IInventory inventory;
    private InputManager inputManager;
    private LogItemPoolingManager logItemPoolingManager;
    private LogInBelt logInBelt;
    private LogInBelt logOutBelt;
    public LogCutter logCutter { get; private set; }
    public LogEvaluator logEvaluator { get; private set; }

    public LogContainer logContainer { get; private set; }

    public ShopNPC shopNPC { get; private set; }

    private Collider2D logContainerCol;
    private Collider2D shopNPCCol;

    private Character character;

    private int preCutItemCnt = 0;
    private bool bLogProcessorActive = false;
    private float logProcessorSpeedMul = 1f;
    private int logProcessingStack = 0;
    private float amountMultiplier = 0f;


    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;

        logItemPoolingManager = GetComponentInChildren<LogItemPoolingManager>();
        logItemPoolingManager.Initialize(false);

        shopObj = Instantiate(shopPrefab, shopSpawnPoint.transform.position,
        Quaternion.identity, this.transform);

        logContainer = shopObj.GetComponentInChildren<LogContainer>();
        logContainer.Initialize(inputManager, logItemPoolingManager);
        logContainerCol = logContainer.GetComponent<Collider2D>();

        logEvaluator = shopObj.GetComponentInChildren<LogEvaluator>();
        logEvaluator.Initialize();

        shopNPC = shopObj.GetComponentInChildren<ShopNPC>();
        shopNPC.Initialize(inputManager);
        shopNPCCol = shopNPC.GetComponent<Collider2D>();

        LogInBelt[] belts = shopObj.GetComponentsInChildren<LogInBelt>();
        for (int i = 0; i < belts.Length; i++)
        {
            if (belts[i].name == "LogInBeltGrid")
            {
                logInBelt = belts[i];
            }
            else if (belts[i].name == "LogOutBeltGrid")
            {
                logOutBelt = belts[i];
            }
        }

        if (logInBelt != null) logInBelt.Initialize();
        if (logOutBelt != null) logOutBelt.Initialize();

        logCutter = GetComponentInChildren<LogCutter>();
        logCutter.Initialize();


        BindEvents();
    }

    public void Release()
    {
        logContainer.Release();
        shopNPC.Release();
        ReleaseEvents();
    }

    public void DI_Inventory(IInventory _inventory)
    {
        inventory = _inventory;
        logContainer.DI_Inventory(inventory);
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
        logContainer.SetCharTransform(character.centerTransform);
        shopNPC.SetCharacterTransform(character.centerTransform);
    }

    private void BindEvents()
    {
        logContainer.ContainerUpdatedEvent -= ContainerUpdated;
        logContainer.ContainerUpdatedEvent += ContainerUpdated;

        logContainer.InteractStateEvent -= InteractStateChanged;
        logContainer.InteractStateEvent += InteractStateChanged;

        logContainer.LogOutEvent -= LogOutFromContainer;
        logContainer.LogOutEvent += LogOutFromContainer;

        logInBelt.LogOutEvent -= LogToCutter;
        logInBelt.LogOutEvent += LogToCutter;

        logCutter.CuttingDoneEvent -= CuttingDone;
        logCutter.CuttingDoneEvent += CuttingDone;

        logOutBelt.LogOutEvent -= LogToEvaluator;
        logOutBelt.LogOutEvent += LogToEvaluator;

        logEvaluator.logEvaluatedEvent -= LogEvaluated;
        logEvaluator.logEvaluatedEvent += LogEvaluated;

        shopNPC.EarnMoneyEvent -= EarnMoney;
        shopNPC.EarnMoneyEvent += EarnMoney;

        logContainer.ContainerSpecChangedEvent -= LogContainerSpecChanged;
        logContainer.ContainerSpecChangedEvent += LogContainerSpecChanged;

        logInBelt.BeltStopEvent -= InBeltStop;
        logInBelt.BeltStopEvent += InBeltStop;

        shopNPC.InteractStateEvent -= ShopInteractStateChanged;
        shopNPC.InteractStateEvent += ShopInteractStateChanged;

        logContainer.ItemAddedEvent -= ItemAddedInContainer;
        logContainer.ItemAddedEvent += ItemAddedInContainer;

        logContainer.LogContainerIsEmptyEvent -= LogContainerIsEmpty;
        logContainer.LogContainerIsEmptyEvent += LogContainerIsEmpty;
    }

    private void ReleaseEvents()
    {
        logContainer.ContainerUpdatedEvent -= ContainerUpdated;
        logContainer.InteractStateEvent -= InteractStateChanged;
        logContainer.LogOutEvent -= LogOutFromContainer;
        logInBelt.LogOutEvent -= LogToCutter;
        logCutter.CuttingDoneEvent -= CuttingDone;
        logOutBelt.LogOutEvent -= LogToEvaluator;
        logEvaluator.logEvaluatedEvent -= LogEvaluated;
        shopNPC.EarnMoneyEvent -= EarnMoney;
        logContainer.ContainerSpecChangedEvent -= LogContainerSpecChanged;
        logInBelt.BeltStopEvent -= InBeltStop;
        shopNPC.InteractStateEvent -= ShopInteractStateChanged;
        logContainer.ItemAddedEvent -= ItemAddedInContainer;
        logContainer.LogContainerIsEmptyEvent -= LogContainerIsEmpty;
    }

    public void PopulateSaveData(ref LogProcessingSaveData _saveData)
    {
        // 리스트 초기화 (중요)
        _saveData.Initialize();

        if (logContainer != null)
        {
            logContainer.PopulateContainerSaveData(ref _saveData.containerInventoryData);
            _saveData.bStop = logContainer.GetbStop();
            _saveData.transferInterval = logContainer.GetTransferInterval();

            // 타이밍 정보 저장
            _saveData.lastTransferTimeElapsed = logContainer.GetLastTransferTimeElapsed();
            _saveData.lastOutputTimeElapsed = logContainer.GetLastOutputTimeElapsed();
            _saveData.lastInterval = logContainer.GetLastInterval();
        }

        if (shopNPC != null)
        {
            _saveData.shopMoney = shopNPC.GetMoney();
            _saveData.bFirstTimeEarnMoney = shopNPC.GetbFirstTimeEarnMoney();
        }

        if (logInBelt != null) logInBelt.PopulateSaveData(ref _saveData.logInBeltData);
        if (logOutBelt != null) logOutBelt.PopulateSaveData(ref _saveData.logOutBeltData);
        if (logCutter != null) _saveData.cutterData = logCutter.GetSaveData();

        _saveData.logProcessingStack = logProcessingStack;
    }

    public void LoadSaveData(LogProcessingSaveData _data)
    {
        if (logContainer != null)
        {
            logContainer.LoadSaveData(_data);
        }

        if (shopNPC != null)
        {
            shopNPC.LoadSaveData(_data.shopMoney, _data.bFirstTimeEarnMoney);
        }

        if (logInBelt != null) logInBelt.LoadSaveData(_data.logInBeltData, logItemPoolingManager);
        if (logOutBelt != null) logOutBelt.LoadSaveData(_data.logOutBeltData, logItemPoolingManager);
        if (logCutter != null) logCutter.LoadSaveData(_data.cutterData, logItemPoolingManager);

        logProcessingStack = _data.logProcessingStack;

        // 로드 후 현재 가공 전(컨테이너 + 첫 번째 벨트 + 커터)인 아이템 총 개수로 preCutItemCnt 동기화
        if (logContainer != null)
        {
            preCutItemCnt = ((IInventory)logContainer).currentItemCount;
            if (_data.logInBeltData.activeItems != null)
                preCutItemCnt += _data.logInBeltData.activeItems.Count;
            if (_data.cutterData.bIsCutting)
                preCutItemCnt += 1;

            UpdateProcessorActiveState();
        }

        UpdateProcessorSpeed();

        Debug.Log("[LogProcessingManager] Log Processing System Save Data Loaded.");
    }

    private void ContainerUpdated()
    {
        ContainerUpdatedEvent.Invoke();
    }

    private void InteractStateChanged(bool _boolean)
    {
        InteractStateChangedEvent.Invoke(_boolean);
    }

    private void LogOutFromContainer(LogItemData _itemData)
    {
        logInBelt.LogIn(logItemPoolingManager.GetLogItem(_itemData));
    }

    private void LogToCutter(LogItem _item, ILogItemData _itemData)
    {
        logCutter.StartCutting(_item, _itemData);
    }

    private void CuttingDone()
    {
        logContainer.SetbStop(false);
        logInBelt.StartBelt();
        logOutBelt.LogIn(logCutter.GetCuttingLogItem());

        --preCutItemCnt;
        UpdateProcessorActiveState();
    }

    private void LogToEvaluator(LogItem _item, ILogItemData _itemData)
    {
        ++logProcessingStack;
        if (logProcessingStack >= 10)
            logProcessingStack = 10;

        UpdateProcessorSpeed();

        logItemPoolingManager.ReturnLogItem(_item);
        logEvaluator.EvaluateLog(_itemData);
    }

    private void LogEvaluated(int _money)
    {
        shopNPC.InsertMoney(_money);
    }

    private void EarnMoney(int _money)
    {
        EarnMoneyEvent.Invoke(_money);
    }

    private void LogContainerSpecChanged()
    {
        LogContainerSpecChangedEvent.Invoke();
    }

    public void IncreaseConveyorSpeed(float _percentage)
    {
        if (logInBelt != null) logInBelt.IncreaseSpeed(_percentage);
        if (logOutBelt != null) logOutBelt.IncreaseSpeed(_percentage);
    }

    public void SetMapType(MapType _mapType)
    {
        logCutter.SetMapType(_mapType);
    }

    private void InBeltStop()
    {
        logContainer.SetbStop(true);
    }

    private void ShopInteractStateChanged(bool _boolean)
    {
        ShopInteracteStateChangedEvent.Invoke(_boolean);
    }

    private void UpdateProcessorActiveState()
    {
        bool currentActive = preCutItemCnt > 0;
        if (currentActive != bLogProcessorActive)
        {
            bLogProcessorActive = currentActive;
            LogProcessorIsActiveEvent?.Invoke(currentActive);
        }
    }

    private void ItemAddedInContainer()
    {
        ++preCutItemCnt;
        UpdateProcessorActiveState();
    }

    public void DisableShopObj()
    {
        if (shopObj != null)
        {
            Vector3 targetPos = new Vector3(-99f, -99f, 0f);
            Vector3 offset = targetPos - shopObj.transform.position;

            if (logInBelt != null) logInBelt.ShiftItems(offset);
            if (logOutBelt != null) logOutBelt.ShiftItems(offset);

            shopObj.transform.position = targetPos;
        }
    }

    public void EnableShopObj()
    {
        if (shopObj != null && shopSpawnPoint != null)
        {
            Vector3 targetPos = shopSpawnPoint.transform.position;
            Vector3 offset = targetPos - shopObj.transform.position;

            if (logInBelt != null) logInBelt.ShiftItems(offset);
            if (logOutBelt != null) logOutBelt.ShiftItems(offset);

            shopObj.transform.position = targetPos;
        }
    }

    private void Update()
    {
        CalcDistForInteraction();
    }

    private void CalcDistForInteraction()
    {
        if (character == null || logContainer == null || shopNPC == null) return;

        bool containerActive = logContainer.gameObject.activeInHierarchy;
        bool shopNPCActive = shopNPC.gameObject.activeInHierarchy;

        if (!containerActive && !shopNPCActive) return;

        if (!containerActive)
        {
            shopNPC.SetCanReach(true);
            return;
        }

        if (!shopNPCActive)
        {
            logContainer.SetCanReach(true);
            return;
        }

        if (logContainer.isPhysicalOverlapped && shopNPC.isPhysicalOverlapped)
        {
            Vector3 playerPos = character.centerTransform.position;
            float distToContainerSq = (logContainerCol != null) ? (logContainerCol.ClosestPoint(playerPos) - (Vector2)playerPos).sqrMagnitude : (logContainer.transform.position - playerPos).sqrMagnitude;
            float distToShopNPCSq = (shopNPCCol != null) ? (shopNPCCol.ClosestPoint(playerPos) - (Vector2)playerPos).sqrMagnitude : (shopNPC.transform.position - playerPos).sqrMagnitude;

            // 두 콜라이더의 교집합 영역에 있을 경우 (둘 다 거리 0) 중심점 기준으로 다시 판별
            if (distToContainerSq == 0f && distToShopNPCSq == 0f)
            {
                distToContainerSq = (logContainer.transform.position - playerPos).sqrMagnitude;
                distToShopNPCSq = (shopNPC.transform.position - playerPos).sqrMagnitude;
            }

            if (distToContainerSq <= distToShopNPCSq)
            {
                logContainer.SetCanReach(true);
                shopNPC.SetCanReach(false);
            }
            else
            {
                logContainer.SetCanReach(false);
                shopNPC.SetCanReach(true);
            }
        }
        else
        {
            logContainer.SetCanReach(true);
            shopNPC.SetCanReach(true);
        }
    }

    public void LogProcessorSpeedUp(float _amount)
    {
        amountMultiplier = _amount;
        UpdateProcessorSpeed();
    }

    private void UpdateProcessorSpeed()
    {
        if (logProcessingStack == 0 || amountMultiplier <= 0f)
        {
            logProcessorSpeedMul = 1f; // 스택이 0이거나 배수가 안 들어왔을 때는 기본 속도(1배)
        }
        else
        {
            // 받아온 amount 배수 * stack 적용 (최소 1배 보장)
            logProcessorSpeedMul = Mathf.Max(1f, amountMultiplier * logProcessingStack);
        }

        if (logInBelt != null) logInBelt.SetGlobalSpeedMultiplier(logProcessorSpeedMul);
        if (logOutBelt != null) logOutBelt.SetGlobalSpeedMultiplier(logProcessorSpeedMul);
        if (logCutter != null) logCutter.SetGlobalSpeedMultiplier(logProcessorSpeedMul);
        if (logContainer != null) logContainer.SetGlobalSpeedMultiplier(logProcessorSpeedMul);
    }

    private void LogContainerIsEmpty()
    {
        logProcessingStack = 0;
        UpdateProcessorSpeed();
    }
}
