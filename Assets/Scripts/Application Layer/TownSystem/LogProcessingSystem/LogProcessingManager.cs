using System;
using System.Collections.Generic;
using UnityEngine;

public class LogProcessingManager : MonoBehaviour, ILogProcessingSystemCH, ICutterCH, ILogEvaluatorCH
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

    // LogInBelt(입고/출고) + LogCutter + LogEvaluator 한 세트. 씬에는 최대 3세트까지 배치되고,
    // activeLineCount만큼만 활성화되어 실제로 라우팅 대상이 된다.
    private List<LogProcessLine> allLines = new List<LogProcessLine>(3);
    private int activeLineCount = 0;
    private int lastLineIdx = -1;

    // "대표 라인"의 커터 - UI(가공 진행률 표시 등)가 단일 대상을 필요로 하는 곳에서만 사용
    public LogCutter logCutter => allLines.Count > 0 ? allLines[0].Cutter : null;

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

        shopNPC = shopObj.GetComponentInChildren<ShopNPC>();
        shopNPC.Initialize(inputManager);
        shopNPCCol = shopNPC.GetComponent<Collider2D>();

        allLines.Clear();
        allLines.AddRange(shopObj.GetComponentsInChildren<LogProcessLine>(true));
        allLines.Sort((a, b) => a.LineIndex.CompareTo(b.LineIndex));

        if (activeLineCount <= 0) activeLineCount = 1; // 세이브 로드 전 기본값 (LoadSaveData에서 덮어씀)
        activeLineCount = Mathf.Clamp(activeLineCount, 1, allLines.Count);

        for (int i = 0; i < allLines.Count; i++)
        {
            bool bActive = i < activeLineCount;
            allLines[i].gameObject.SetActive(bActive);
            if (bActive)
            {
                allLines[i].Initialize();
                BindLineEvents(allLines[i]);
            }
        }

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

        shopNPC.EarnMoneyEvent -= EarnMoney;
        shopNPC.EarnMoneyEvent += EarnMoney;

        logContainer.ContainerSpecChangedEvent -= LogContainerSpecChanged;
        logContainer.ContainerSpecChangedEvent += LogContainerSpecChanged;

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
        shopNPC.EarnMoneyEvent -= EarnMoney;
        logContainer.ContainerSpecChangedEvent -= LogContainerSpecChanged;
        shopNPC.InteractStateEvent -= ShopInteractStateChanged;
        logContainer.ItemAddedEvent -= ItemAddedInContainer;
        logContainer.LogContainerIsEmptyEvent -= LogContainerIsEmpty;

        for (int i = 0; i < activeLineCount; i++) ReleaseLineEvents(allLines[i]);
    }

    private void BindLineEvents(LogProcessLine _line)
    {
        _line.LineBusyEvent -= OnLineBusy;
        _line.LineBusyEvent += OnLineBusy;

        _line.LineFreedEvent -= OnLineFreed;
        _line.LineFreedEvent += OnLineFreed;

        _line.LogReadyForEvaluationEvent -= LogToEvaluator;
        _line.LogReadyForEvaluationEvent += LogToEvaluator;

        _line.LineMoneyEarnedEvent -= LogEvaluated;
        _line.LineMoneyEarnedEvent += LogEvaluated;
    }

    private void ReleaseLineEvents(LogProcessLine _line)
    {
        _line.LineBusyEvent -= OnLineBusy;
        _line.LineFreedEvent -= OnLineFreed;
        _line.LogReadyForEvaluationEvent -= LogToEvaluator;
        _line.LineMoneyEarnedEvent -= LogEvaluated;
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

        _saveData.activeLineCount = activeLineCount;
        for (int i = 0; i < activeLineCount; i++)
        {
            LogProcessLineSaveData lineData = new LogProcessLineSaveData();
            lineData.Initialize();
            allLines[i].PopulateSaveData(ref lineData);
            _saveData.lineDatas.Add(lineData);
        }

        _saveData.logProcessingStack = logProcessingStack;
    }

    /// <summary>
    /// 저장 시점에 LogContainer로 날아오던(아직 커밋 안 된) 로그를 세이브 데이터에만 가상 착지시킨다.
    /// 반드시 PopulateSaveData 이후에 호출해야 한다(슬롯 리스트가 초기화/구성된 뒤여야 병합 가능).
    /// </summary>
    public void AppendTransitToSaveData(ref LogProcessingSaveData _saveData)
    {
        if (logContainer != null)
        {
            logContainer.AppendTransitToSaveData(ref _saveData.containerInventoryData);
        }
    }

    /// <summary>
    /// OffroadContainer 저장 정산이 되돌릴 자리가 없을 때 LogContainer로 전진 납품(fallback)하기 위해
    /// LogContainer의 슬롯당 최대 보관 개수를 노출한다.
    /// </summary>
    public int GetContainerMaxItemsPerSlot()
    {
        return logContainer != null ? logContainer.maxItemCntPerSlot : 0;
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

        int savedLineCount = Mathf.Clamp(_data.activeLineCount > 0 ? _data.activeLineCount : 1, 1, allLines.Count);

        for (int i = 0; i < allLines.Count; i++)
        {
            bool bWasActive = i < activeLineCount;
            bool bShouldBeActive = i < savedLineCount;

            if (bShouldBeActive && !bWasActive)
            {
                allLines[i].gameObject.SetActive(true);
                allLines[i].Initialize();
                BindLineEvents(allLines[i]);
            }
            else if (!bShouldBeActive && bWasActive)
            {
                ReleaseLineEvents(allLines[i]);
                allLines[i].gameObject.SetActive(false);
            }

            if (bShouldBeActive && i < _data.lineDatas.Count)
            {
                allLines[i].LoadSaveData(_data.lineDatas[i], logItemPoolingManager);
            }
        }
        activeLineCount = savedLineCount;

        logProcessingStack = _data.logProcessingStack;

        // 로드 후 현재 가공 전(컨테이너 + 활성 라인의 입고벨트 + 커터)인 아이템 총 개수로 preCutItemCnt 동기화
        if (logContainer != null)
        {
            preCutItemCnt = ((IInventory)logContainer).currentItemCount;
            for (int i = 0; i < activeLineCount && i < _data.lineDatas.Count; i++)
            {
                if (_data.lineDatas[i].inBeltData.activeItems != null)
                    preCutItemCnt += _data.lineDatas[i].inBeltData.activeItems.Count;
                // 입고벨트 끝단 퇴출대기(커터 투입 직전) 아이템도 아직 가공 전이므로 포함
                if (_data.lineDatas[i].inBeltData.deactivatingItems != null)
                    preCutItemCnt += _data.lineDatas[i].inBeltData.deactivatingItems.Count;
                if (_data.lineDatas[i].cutterData.bIsCutting)
                    preCutItemCnt += 1;
            }

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
        LogProcessLine line = GetAvailableLine();
        if (line == null) return; // bStop이 이미 막아주므로 정상 경로에서는 도달하지 않음
        line.LogIn(logItemPoolingManager.GetLogItem(_itemData));
    }

    private LogProcessLine GetAvailableLine()
    {
        // 라운드로빈으로 다음 라인부터 검사해 첫 유휴 라인을 반환 (부하 분산)
        for (int i = 1; i <= activeLineCount; i++)
        {
            int idx = (lastLineIdx + i) % activeLineCount;
            if (!allLines[idx].IsBusy)
            {
                lastLineIdx = idx;
                return allLines[idx];
            }
        }
        return null;
    }

    private void OnLineBusy(LogProcessLine _line)
    {
        for (int i = 0; i < activeLineCount; i++)
        {
            if (!allLines[i].IsBusy) return; // 하나라도 여유 있으면 컨테이너는 계속 공급
        }
        logContainer.SetbStop(true);
    }

    private void OnLineFreed(LogProcessLine _line)
    {
        logContainer.SetbStop(false);

        --preCutItemCnt;
        UpdateProcessorActiveState();
    }

    private void LogToEvaluator(LogProcessLine _line, LogItem _item, ILogItemData _itemData)
    {
        ++logProcessingStack;
        if (logProcessingStack >= 10)
            logProcessingStack = 10;

        UpdateProcessorSpeed();

        logItemPoolingManager.ReturnLogItem(_item);
        _line.Evaluator.EvaluateLog(_itemData);
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

    // 영구 스킬 효과는 아직 비활성인 라인까지 포함해 모든 라인에 적용한다.
    // (증설되어 나중에 활성화될 때 이미 반영돼 있어야 base 스탯으로 돌아가지 않음.
    //  Initialize()는 이 수치들을 리셋하지 않으므로 미리 적용해 둬도 안전하다.)
    public void IncreaseConveyorSpeed(float _percentage)
    {
        for (int i = 0; i < allLines.Count; i++) allLines[i].IncreaseConveyorSpeed(_percentage);
    }

    public void SetMapType(MapType _mapType)
    {
        for (int i = 0; i < allLines.Count; i++) allLines[i].SetMapType(_mapType);
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

            for (int i = 0; i < activeLineCount; i++) allLines[i].ShiftItems(offset);

            shopObj.transform.position = targetPos;
        }
    }

    public void EnableShopObj()
    {
        if (shopObj != null && shopSpawnPoint != null)
        {
            Vector3 targetPos = shopSpawnPoint.transform.position;
            Vector3 offset = targetPos - shopObj.transform.position;

            for (int i = 0; i < activeLineCount; i++) allLines[i].ShiftItems(offset);

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

        for (int i = 0; i < activeLineCount; i++) allLines[i].SetGlobalSpeedMultiplier(logProcessorSpeedMul);
        if (logContainer != null) logContainer.SetGlobalSpeedMultiplier(logProcessorSpeedMul);
    }

    private void LogContainerIsEmpty()
    {
        logProcessingStack = 0;
        UpdateProcessorSpeed();
    }

    // 세트(입고벨트+출고벨트+커터+평가기) 증설 - 스킬/재화 트리 등에서 호출
    public void ExpandProcessLineCnt(float _amount)
    {
        int previousCount = activeLineCount;
        int newCount = Mathf.Clamp(activeLineCount + (int)_amount, 1, allLines.Count);

        for (int i = activeLineCount; i < newCount; i++)
        {
            allLines[i].gameObject.SetActive(true);
            allLines[i].Initialize();
            BindLineEvents(allLines[i]);
        }

        for (int i = newCount; i < activeLineCount; i++)
        {
            ReleaseLineEvents(allLines[i]);
            allLines[i].gameObject.SetActive(false);
        }

        activeLineCount = newCount;

        // 라인 수가 바뀌었으니 현재 전역 속도 배율을 새로 활성화된 라인에도 반영
        UpdateProcessorSpeed();

        // 라인이 늘었다면 유휴 라인이 새로 생긴 것이므로, 컨테이너 공급 정지를 풀어 즉시 가동시킨다.
        if (newCount > previousCount && logContainer != null)
        {
            logContainer.SetbStop(false);
        }
    }

    // ICutterCH - 스킬 효과를 (비활성 포함) 모든 라인의 커터에 브로드캐스트
    public void IncreaseCutSpeed(float _amount)
    {
        for (int i = 0; i < allLines.Count; i++) allLines[i].Cutter.IncreaseCutSpeed(_amount);
    }

    public void SetPowerSupply(bool _bPowerSupply)
    {
        for (int i = 0; i < allLines.Count; i++) allLines[i].Cutter.SetPowerSupply(_bPowerSupply);
    }

    // ILogEvaluatorCH - 스킬 효과를 (비활성 포함) 모든 라인의 평가기에 브로드캐스트
    public void IncreaseLogValueMultiplier(float _amount)
    {
        for (int i = 0; i < allLines.Count; i++) allLines[i].Evaluator.IncreaseLogValueMultiplier(_amount);
    }

    public void IncreaseTopgradeAssessmentChance(float _amount)
    {
        for (int i = 0; i < allLines.Count; i++) allLines[i].Evaluator.IncreaseTopgradeAssessmentChance(_amount);
    }
}
