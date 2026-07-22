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

    public event Action<LogProcessLine> LineBusyEvent;
    public event Action<LogProcessLine> LineFreedEvent;
    public event Action<LogProcessLine, LogItem, ILogItemData> LogReadyForEvaluationEvent;
    public event Action<int> LineMoneyEarnedEvent;

    private bool isBusy = false;

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
    }

    private void ReleaseEvents()
    {
        inBelt.LogOutEvent -= LogToCutter;
        inBelt.BeltStopEvent -= OnBeltStop;
        cutter.CuttingDoneEvent -= CuttingDone;
        outBelt.LogOutEvent -= OnLogOutToEvaluator;
        evaluator.logEvaluatedEvent -= OnLogEvaluated;
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
        outBelt.LogIn(cutter.GetCuttingLogItem());
        LineFreedEvent?.Invoke(this);
    }

    private void OnLogOutToEvaluator(LogItem _item, ILogItemData _itemData)
    {
        LogReadyForEvaluationEvent?.Invoke(this, _item, _itemData);
    }

    private void OnLogEvaluated(int _money)
    {
        LineMoneyEarnedEvent?.Invoke(_money);
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

    public void SetMapType(MapType _mapType)
    {
        cutter.SetMapType(_mapType);
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
