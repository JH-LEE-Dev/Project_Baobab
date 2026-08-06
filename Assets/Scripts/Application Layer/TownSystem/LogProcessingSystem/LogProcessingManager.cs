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

    // 활성 가공 라인 수가 바뀔 때(증설/철거, 세이브 로드) 알린다. TownSystem이 이 값에 맞춰
    // 마을 Grid의 증설분 건물 충돌 타일맵을 켜고 끈다.
    public event Action<int> ActiveLineCountChangedEvent;

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

    // 라인별 독립 출고 타이머. 각 라인은 다른 라인의 상태와 무관하게 자기 타이머로만
    // 컨테이너에서 원목을 꺼내온다(라인이 하나뿐일 때와 동일한 진입 간격을 보장하기 위함).
    // Time.time 절대값 대신 "커터가 바쁘지 않은 시간"만 누적하는 방식이라, 가공 중에는
    // 진행이 멈춘다(가공이 오래 걸릴수록 다음 출고가 즉시 튀어나오는 것을 방지).
    private float[] lineElapsedTime;
    private LogProcessLine pendingRequestLine;

    // "대표 라인"의 커터 - UI(가공 진행률 표시 등)가 단일 대상을 필요로 하는 곳에서만 사용
    public LogCutter logCutter => allLines.Count > 0 ? allLines[0].Cutter : null;

    /// <summary>현재 가동 중인 가공 라인 수(1~3). 제재소 건물 증설 단계와 1:1로 대응한다.</summary>
    public int ActiveLineCount => activeLineCount;

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
    private bool bRemoteDepositActive = false;


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

        lineElapsedTime = new float[allLines.Count];
        ResetLineTimers(0, lineElapsedTime.Length);

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
        shopNPC.SetCharacter(character);
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
        _line.LineFreedEvent -= OnLineFreed;
        _line.LineFreedEvent += OnLineFreed;

        _line.LogReadyForEvaluationEvent -= LogToEvaluator;
        _line.LogReadyForEvaluationEvent += LogToEvaluator;

        _line.LineMoneyEarnedEvent -= LogEvaluated;
        _line.LineMoneyEarnedEvent += LogEvaluated;
    }

    private void ReleaseLineEvents(LogProcessLine _line)
    {
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

            // 라인별 독립 출고 타이머도 그대로 저장해서, 로드 시 "이 라인이 다음 원목을 받기까지
            // 얼마나 남았는지"를 저장 시점과 동일하게 복원한다.
            lineData.lastOutputTimeElapsed = (lineElapsedTime != null && i < lineElapsedTime.Length)
                ? lineElapsedTime[i]
                : 0f;

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

            // 스킬 세이브 로드(원격 입금 재적용)가 로그 가공 세이브 로드보다 먼저 실행되므로,
            // 여기서 복원된 상점 잔액이 잠긴 상점에 갇혀 유실되지 않도록 다시 한번 정산한다.
            if (bRemoteDepositActive)
            {
                shopNPC.ClearMoneyToPlayer();
            }
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
        ActiveLineCountChangedEvent?.Invoke(activeLineCount);

        // 라인별 독립 출고 타이머를 저장된 값 그대로 복원한다. 세이브 시점에 "다음 원목까지
        // 남은 시간"이 얼마였는지를 정확히 재현해서, 저장/로드가 제재소 상태를 바꿔놓지 않게 한다.
        if (lineElapsedTime == null || lineElapsedTime.Length != allLines.Count)
            lineElapsedTime = new float[allLines.Count];

        for (int i = 0; i < activeLineCount; i++)
        {
            if (i < _data.lineDatas.Count)
                lineElapsedTime[i] = _data.lineDatas[i].lastOutputTimeElapsed;
            else
                ResetLineTimers(i, i + 1); // 저장된 라인 데이터가 없으면(비정상 케이스) 즉시 출고 가능하게 대체
        }

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
        // TakeFirstItem()은 항상 라인별 폴링(PollLineSupply)에서, 어느 라인이 요청했는지를
        // pendingRequestLine에 미리 담아둔 뒤 동기 호출된다. 그래서 이 이벤트는 항상 그 라인으로
        // 바로 전달하면 되고, 더 이상 "지금 비어있는 라인"을 다시 찾을 필요가 없다.
        pendingRequestLine?.LogIn(logItemPoolingManager.GetLogItem(_itemData));
    }

    private void OnLineFreed(LogProcessLine _line)
    {
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
        if (bRemoteDepositActive)
        {
            EarnMoneyEvent?.Invoke(_money);
        }
        else
        {
            shopNPC.InsertMoney(_money);
        }
    }

    public void SetRemoteDeposit(bool _bActive)
    {
        bRemoteDepositActive = _bActive;

        if (_bActive)
        {
            shopNPC.ClearMoneyToPlayer();
        }

        shopNPC.SetRemoteDepositLock(_bActive);
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
        shopNPC?.SetMapType(_mapType);
        logContainer?.SetMapType(_mapType);
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
        PollLineSupply();
    }

    // 라인마다 독립된 타이머로 컨테이너에서 원목을 하나씩 꺼내온다. 다른 라인이 바쁜지 여부는
    // 전혀 참조하지 않으므로, 라인이 여러 개여도 각 라인은 자기 혼자 있을 때와 동일한 진입
    // 간격을 유지한다(라운드로빈으로 인해 진입 타이밍이 뒤섞여 벨트 간격이 불규칙해지던 문제 해결).
    private void PollLineSupply()
    {
        if (logContainer == null || lineElapsedTime == null) return;

        float interval = logContainer.GetEffectiveTransferInterval();

        for (int i = 0; i < activeLineCount; i++)
        {
            // 커터가 가공 중(라인이 바쁨)인 동안은 타이머 진행 자체를 멈춘다. 가공이 오래
            // 걸려도 그동안 쌓인 경과시간이 인터벌을 이미 채워버려 가공 완료 즉시 다음 원목이
            // 튀어나오는 것을 막기 위함.
            if (allLines[i].IsBusy) continue;

            lineElapsedTime[i] += Time.deltaTime;

            if (lineElapsedTime[i] < interval) continue;
            if (!logContainer.HasAvailableItem()) continue;

            pendingRequestLine = allLines[i];
            logContainer.TakeFirstItem();
            pendingRequestLine = null;

            lineElapsedTime[i] = 0f;
        }
    }

    private void ResetLineTimers(int _fromIdx, int _toIdxExclusive)
    {
        if (lineElapsedTime == null) return;

        float interval = logContainer != null ? logContainer.GetEffectiveTransferInterval() : 0f;
        for (int i = _fromIdx; i < _toIdxExclusive && i < lineElapsedTime.Length; i++)
        {
            // 즉시 출고 가능하도록 경과시간을 인터벌만큼 채워둔다.
            lineElapsedTime[i] = interval;
        }
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
            // 기본 1배 + 스택당 amount 배수만큼 가산 (최소 1배 보장)
            logProcessorSpeedMul = Mathf.Max(1f, 1f + (amountMultiplier * logProcessingStack));
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

        // 새로 활성화된 라인은 자기 타이머부터 시작해야 하므로, 즉시 출고 가능한 상태로 리셋한다.
        if (newCount > previousCount)
        {
            ResetLineTimers(previousCount, newCount);
        }

        // 제재소 건물(증설분 충돌 타일맵)도 라인 수에 맞춰 켜고 꺼야 한다.
        if (newCount != previousCount)
        {
            ActiveLineCountChangedEvent?.Invoke(activeLineCount);
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
