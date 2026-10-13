using System;
using System.Collections.Generic;

/// <summary>
/// 순위표(런 기록 3탭). 기획서 3-6 - 최근 기록 / 클리어 랭킹(1~5위) / 언리밋 랭킹(1~5위).
/// 로비의 순위표 오브젝트를 치면 열린다. 프리팹에서 bCloseableByESC를 켜 두면 ESC/패드 B로 닫힌다.
///
/// [UI 담당 구현 지점] OnRecordsPresented에서 탭을 그린다. 탭 전환은 inputReader.UITabShiftEvent(LB/RB)를 쓰면 된다.
/// </summary>
public class UIView_RoguelikeRecords : UIView_RoguelikeModalBase
{
    public event Action ClosedEvent;

    public RoguelikeRecordViewData Recent { get; private set; }
    protected readonly List<RoguelikeRecordViewData> clearRanking = new List<RoguelikeRecordViewData>(5);
    protected readonly List<RoguelikeRecordViewData> unlimitRanking = new List<RoguelikeRecordViewData>(5);

    public IReadOnlyList<RoguelikeRecordViewData> ClearRanking => clearRanking;
    public IReadOnlyList<RoguelikeRecordViewData> UnlimitRanking => unlimitRanking;

    public virtual void OpenRecords(in RoguelikeRecordViewData _recent, IReadOnlyList<RoguelikeRecordViewData> _clearRanking,
        IReadOnlyList<RoguelikeRecordViewData> _unlimitRanking)
    {
        Recent = _recent;
        Copy(_clearRanking, clearRanking);
        Copy(_unlimitRanking, unlimitRanking);

        Show();
        OnRecordsPresented();
    }

    /// <summary>[UI 담당] 세 탭을 그린다.</summary>
    protected virtual void OnRecordsPresented() { }

    protected override void OnHideContent()
    {
        ClosedEvent?.Invoke();
    }

    private static void Copy(IReadOnlyList<RoguelikeRecordViewData> _from, List<RoguelikeRecordViewData> _to)
    {
        _to.Clear();
        if (null == _from) return;

        for (int i = 0; i < _from.Count; i++) _to.Add(_from[i]);
    }

    public override void OnDestroy()
    {
        ClosedEvent = null;
        base.OnDestroy();
    }
}
