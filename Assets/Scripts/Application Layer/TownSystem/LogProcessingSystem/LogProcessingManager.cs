using System;
using UnityEngine;

public class LogProcessingManager : MonoBehaviour
{
    public event Action FirstTimeEarnMoneyEvent;
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
    private LogEvaluator logEvaluator;

    public LogContainer logContainer { get; private set; }

    private ShopNPC shopNPC;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;

        shopObj = Instantiate(shopPrefab, shopSpawnPoint.transform.position,
        Quaternion.identity, this.transform);

        logContainer = shopObj.GetComponentInChildren<LogContainer>();
        logContainer.Initialize(inputManager);

        logEvaluator = shopObj.GetComponentInChildren<LogEvaluator>();
        logEvaluator.Initialize();

        shopNPC = shopObj.GetComponentInChildren<ShopNPC>();
        shopNPC.Initialize(inputManager);

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

        logItemPoolingManager = GetComponentInChildren<LogItemPoolingManager>();
        logItemPoolingManager.Initialize();

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

        shopNPC.FirstTimeEarnMoneyEvent -= FirstTimeEarnMoney;
        shopNPC.FirstTimeEarnMoneyEvent += FirstTimeEarnMoney;
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
        shopNPC.FirstTimeEarnMoneyEvent -= FirstTimeEarnMoney;
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
        logContainer.SetbStop(true);
    }

    private void CuttingDone()
    {
        logContainer.SetbStop(false);
        logInBelt.StartBelt();
        logOutBelt.LogIn(logCutter.GetCuttingLogItem());
    }

    private void LogToEvaluator(LogItem _item, ILogItemData _itemData)
    {
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

    private void FirstTimeEarnMoney()
    {
        FirstTimeEarnMoneyEvent.Invoke();
    }
}
