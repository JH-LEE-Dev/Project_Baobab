using System;

/// <summary>
/// 로그라이크 결과 화면. 기획서 3-7 "결과 화면과 런 종료" - 본편 결과 UI(UIView_Result)의 구조와 연출을 비슷하게.
///
/// [흐름] OpenResult → 플레이어가 확인 → ConfirmRequestedEvent → 시스템이 Hide를 부르고 디스토션·진입 연출로 로비 이동.
/// 이 화면은 ESC로 닫히지 않는다(프리팹 bCloseableByESC = false). 시스템이 ESC 키도 잠근다.
///
/// [UI 담당 구현 지점] OnResultPresented에서 항목을 그린다.
///   도달 웨이브(언리밋은 Unlimit-N, 포기는 마지막으로 클리어한 웨이브), 플레이 시간, 총 부순 나무, 총 획득 코인,
///   보유 카드(레벨 아이콘 그대로). 신기록이면 clearRank/unlimitRank가 0 이상.
/// </summary>
public class UIView_RoguelikeResult : UIView_RoguelikeModalBase
{
    public event Action ConfirmRequestedEvent;

    public RoguelikeResultViewData Data { get; private set; }

    public virtual void OpenResult(in RoguelikeResultViewData _data)
    {
        Data = _data;
        Show();
        OnResultPresented();
    }

    /// <summary>[UI 담당] 결과를 그린다.</summary>
    protected virtual void OnResultPresented() { }

    /// <summary>[UI 담당] 확인 버튼에서 부른다.</summary>
    protected void RequestConfirm() => ConfirmRequestedEvent?.Invoke();

    public override void OnDestroy()
    {
        ConfirmRequestedEvent = null;
        base.OnDestroy();
    }
}
