using System;
using UnityEngine;

// LogInBelt(입고) + LogCutter + LogInBelt(출고) + LogEvaluator 한 세트를 감싸는 배선 전용 래퍼.
// 세트 내부에서 완결되는 배선(입고벨트->커터->출고벨트)만 여기서 처리하고,
// 매니저 전역 상태(preCutItemCnt, bStop, logProcessingStack 등)를 건드리는 로직은
// 이벤트로 릴레이만 하고 LogProcessingManager에 그대로 둔다.
public class LogProcessLine : MonoBehaviour
{
    [SerializeField] private int lineIndex;
    [SerializeField] private LogInBelt inBelt;
    [SerializeField] private LogInBelt outBelt;
    [SerializeField] private LogCutter cutter;
    [SerializeField] private LogEvaluator evaluator;
    [SerializeField] private Sprite topgradeTimesSprite;
    [SerializeField] private Sprite topgradeTwoSprite;

    public event Action<LogProcessLine> LineBusyEvent;
    public event Action<LogProcessLine> LineFreedEvent;
    public event Action<LogProcessLine, LogItem, ILogItemData> LogReadyForEvaluationEvent;
    public event Action<long> LineMoneyEarnedEvent;

    private bool isBusy = false;

    // 특등품 평가 연출(쌍둥이 원목 + 평가기 "쾅"). 동작에는 관여하지 않는 순수 연출이라 프리팹을 건드리지 않고
    // 처음 쓸 때 붙인다. 라인 GameObject에 붙어 있어서 라인이 꺼지면(철거·세이브 로드) 함께 멈추고 정리된다.
    private TopgradeAssessmentPresenter topgradePresenter;

    // 평가 순간 평가기에서 상점으로 금화가 날아가는 연출을 그려 줄 상점. 돈 처리(LineMoneyEarnedEvent)와는 별개인 그림 통로다.
    private ShopNPC moneyVisualShop;

    public int LineIndex => lineIndex;
    public LogInBelt InBelt => inBelt;
    public LogInBelt OutBelt => outBelt;
    public LogCutter Cutter => cutter;
    public LogEvaluator Evaluator => evaluator;
    public bool IsBusy => isBusy;

    public void Initialize()
    {
        isBusy = false;
        inBelt.Initialize();
        outBelt.Initialize();
        outBelt.SetStopsOnLogOut(false); // 평가기는 용량 제약이 없으므로 outBelt는 배출 시 멈추지 않는다.
        cutter.Initialize();
        evaluator.Initialize();
        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void BindEvents()
    {
        inBelt.LogOutEvent -= LogToCutter;
        inBelt.LogOutEvent += LogToCutter;

        inBelt.BeltStopEvent -= OnBeltStop;
        inBelt.BeltStopEvent += OnBeltStop;

        cutter.CuttingDoneEvent -= CuttingDone;
        cutter.CuttingDoneEvent += CuttingDone;

        outBelt.LogOutEvent -= OnLogOutToEvaluator;
        outBelt.LogOutEvent += OnLogOutToEvaluator;

        evaluator.logEvaluatedEvent -= OnLogEvaluated;
        evaluator.logEvaluatedEvent += OnLogEvaluated;

        evaluator.TopgradeAssessedEvent -= OnTopgradeAssessedVisual;
        evaluator.TopgradeAssessedEvent += OnTopgradeAssessedVisual;
    }

    private void ReleaseEvents()
    {
        inBelt.LogOutEvent -= LogToCutter;
        inBelt.BeltStopEvent -= OnBeltStop;
        cutter.CuttingDoneEvent -= CuttingDone;
        outBelt.LogOutEvent -= OnLogOutToEvaluator;
        evaluator.logEvaluatedEvent -= OnLogEvaluated;
        evaluator.TopgradeAssessedEvent -= OnTopgradeAssessedVisual;
    }

    public void LogIn(LogItem _item)
    {
        inBelt.LogIn(_item);
    }

    private void LogToCutter(LogItem _item, ILogItemData _itemData)
    {
        cutter.StartCutting(_item, _itemData);
    }

    private void OnBeltStop()
    {
        isBusy = true;
        LineBusyEvent?.Invoke(this);
    }

    private void CuttingDone()
    {
        isBusy = false;
        inBelt.StartBelt();

        LogItem cutLog = cutter.GetCuttingLogItem();
        // 특등품 평가를 여기서 미리 굴린다(확률은 평가 순간과 같다). 결과는 원목에 적혀 평가기까지 간다.
        evaluator.PreRollTopgrade(cutLog);
        outBelt.LogIn(cutLog);
        if (topgradePresenter != null) topgradePresenter.OnLogCut(cutLog);

        LineFreedEvent?.Invoke(this);
    }

    /// <summary>
    /// 특등품 연출이 쓸 원목 풀을 넘겨 연출을 준비한다. 꺼져 있는 라인에도 미리 불러 둘 수 있다
    /// (라인이 켜질 때 그대로 쓰인다). 여러 번 불러도 연출 컴포넌트는 하나만 붙는다.
    /// </summary>
    public void SetupTopgradePresentation(LogItemPoolingManager _pool)
    {
        if (topgradePresenter == null)
        {
            topgradePresenter = GetComponent<TopgradeAssessmentPresenter>();
            if (topgradePresenter == null) topgradePresenter = gameObject.AddComponent<TopgradeAssessmentPresenter>();
        }

        topgradePresenter.Setup(_pool, cutter, outBelt, evaluator, topgradeTimesSprite, topgradeTwoSprite);
    }

    private void OnLogOutToEvaluator(LogItem _item, ILogItemData _itemData)
    {
        LogReadyForEvaluationEvent?.Invoke(this, _item, _itemData);
    }

    private void OnLogEvaluated(long _money)
    {
        LineMoneyEarnedEvent?.Invoke(_money);

        if (moneyVisualShop != null && evaluator.Storage != null)
            moneyVisualShop.PlayDepositVisual(evaluator.Storage.transform.position, false);
    }

    // 특등품이면 금화가 더 날아간다(돈은 이미 2배로 처리된 뒤다).
    private void OnTopgradeAssessedVisual(long _basePrice, long _finalPrice)
    {
        if (moneyVisualShop != null && evaluator.Storage != null)
            moneyVisualShop.PlayDepositVisual(evaluator.Storage.transform.position, true);
    }

    /// <summary>평가 순간 금화가 날아갈 상점을 넘긴다(그림 전용). 꺼진 라인에도 미리 불러 둘 수 있다.</summary>
    public void SetupMoneyPresentation(ShopNPC _shop)
    {
        moneyVisualShop = _shop;
    }

    public void SetGlobalSpeedMultiplier(float _mul)
    {
        inBelt.SetGlobalSpeedMultiplier(_mul);
        outBelt.SetGlobalSpeedMultiplier(_mul);
        cutter.SetGlobalSpeedMultiplier(_mul);
    }

    public void IncreaseConveyorSpeed(float _percentage)
    {
        inBelt.IncreaseSpeed(_percentage);
        outBelt.IncreaseSpeed(_percentage);
    }

    /// <summary>
    /// 이 라인이 컨테이너에서 원목을 꺼내오는 주기에 곱할 배율. 입고벨트의 컨베이어 특성 배율을
    /// 그대로 쓴다. 출고 주기가 벨트 속도에 반비례해야 벨트 위 원목 사이 "거리"가 특성과 무관하게
    /// 일정하게 유지된다(빨라진 벨트 뒤로 빈 구간만 길어지는 것을 막는다).
    /// </summary>
    public float SupplyRateMultiplier => inBelt.SpeedMultiplier;

    public void SetMapType(MapType _mapType)
    {
        cutter.SetMapType(_mapType);
        inBelt.SetMapType(_mapType);
        outBelt.SetMapType(_mapType);
        evaluator.SetMapType(_mapType);
        if (topgradePresenter != null) topgradePresenter.SetMapType(_mapType);
    }

    public void ShiftItems(Vector3 _offset)
    {
        inBelt.ShiftItems(_offset);
        outBelt.ShiftItems(_offset);
    }

    public void PopulateSaveData(ref LogProcessLineSaveData _saveData)
    {
        inBelt.PopulateSaveData(ref _saveData.inBeltData);
        outBelt.PopulateSaveData(ref _saveData.outBeltData);
        _saveData.cutterData = cutter.GetSaveData();
    }

    public void LoadSaveData(LogProcessLineSaveData _data, LogItemPoolingManager _poolingManager)
    {
        inBelt.LoadSaveData(_data.inBeltData, _poolingManager);
        outBelt.LoadSaveData(_data.outBeltData, _poolingManager);
        cutter.LoadSaveData(_data.cutterData, _poolingManager);

        // 커터 가공 중이거나, 입고벨트 끝단에서 커터 투입을 대기 중인 아이템이 있으면 라인은 바쁜 상태다.
        // (출고벨트 위 아이템은 이미 가공이 끝난 것이라 라인 바쁨과 무관.)
        bool inBeltHasPending = _data.inBeltData.deactivatingItems != null
                                && _data.inBeltData.deactivatingItems.Count > 0;
        isBusy = _data.cutterData.bIsCutting || inBeltHasPending;
    }
}
