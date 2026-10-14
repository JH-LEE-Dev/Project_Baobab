using System;
using System.Collections.Generic;

/// <summary>
/// 카드 선택 화면. 기획서 3-3 "선택 단계 규칙", 5-2 "카드 HUD"·"카드 선택 화면 조작", 5-3 "카드 UI 연출".
///
/// [시스템 → 뷰] RoguelikeUICoordinator가 아래 public 메서드를 부른다.
/// [뷰 → 시스템] 플레이어 입력은 이벤트로만 올린다. 뷰는 골드 차감·구매 판정을 하지 않는다
///   (bAffordable=false인 카드를 눌러도 BuyRequestedEvent를 올려도 된다 - 시스템이 거절한다).
///
/// [흐름]
///   OpenSelection → (RerollRequested → ReplaceOffers)* → BuyRequested 또는 SkipRequested
///   → 시스템이 결과를 판정 → PlayPurchase(구매) 또는 CloseSelection(건너뛰기)
///   → 연출이 끝나면 뷰가 _onComplete를 부른다 → 시스템이 다음 단계(지형 전환/전투)로 넘어간다.
///   _onComplete를 부르지 않으면 시스템이 다음으로 넘어가지 못한다(대기 상한 뒤 강제 진행).
///
/// [UI 담당 구현 지점]
///   - 노드 UI 기반 카드, 레벨 표기(I~IV, MAX / 루비는 ±), 레어 셰이더, 처음 등장 자물쇠 연출
///   - 열릴 때 가장 왼쪽 카드 호버, CursorBox 사용, 게임패드 조작
///   - 가격 붉은 글씨(bAffordable=false), 리롤 비용·리롤 불가(canReroll=false), 건너뛰기 불가(canSkip=false)
///   - 배타 카드 소멸 연출(PlayPurchase의 _discardedIndices) → 구매 연출 뒤에 빠르게
///
/// [시간] 이 화면이 떠 있는 동안에도 보유 카드 창(Time.timeScale = 0)이나 ESC 메뉴로 일시정지가 걸릴 수 있다.
///   등장·호버·구매 연출은 unscaled 시간(DOTween SetUpdate(true), Animator UnscaledTime)으로 돌린다.
///   스케일 시간으로 돌리면 일시정지 동안 연출이 멈춘다. 구매 연출 대기 상한도 게임 시간이라 그동안 함께 멈춘다.
/// </summary>
public class UIView_RoguelikeCardSelect : UIView_RoguelikeModalBase
{
    /// <summary>제시된 _offerIndex번 카드를 사겠다.</summary>
    public event Action<int> BuyRequestedEvent;
    public event Action SkipRequestedEvent;
    public event Action RerollRequestedEvent;

    protected readonly List<RoguelikeCardViewData> offers = new List<RoguelikeCardViewData>(8);

    public IReadOnlyList<RoguelikeCardViewData> Offers => offers;
    public long Gold { get; private set; }
    public int RerollCost { get; private set; }
    public bool bCanReroll { get; private set; }
    public bool bCanSkip { get; private set; }
    public bool bFirstPick { get; private set; }

    /// <summary>선택 화면을 연다. 화면이 켜지고 카드 등장 연출을 시작한다.</summary>
    public virtual void OpenSelection(IReadOnlyList<RoguelikeCardViewData> _offers, long _gold, int _rerollCost, bool _bCanReroll, bool _bCanSkip, bool _bFirstPick)
    {
        CopyOffers(_offers);
        Gold = _gold;
        RerollCost = _rerollCost;
        bCanReroll = _bCanReroll;
        bCanSkip = _bCanSkip;
        bFirstPick = _bFirstPick;

        Show();
        OnOffersPresented(false);
    }

    /// <summary>리롤로 카드가 바뀌었다. 골드·리롤 비용도 함께 갱신된다.</summary>
    public virtual void ReplaceOffers(IReadOnlyList<RoguelikeCardViewData> _offers, long _gold, int _rerollCost, bool _bCanReroll)
    {
        CopyOffers(_offers);
        Gold = _gold;
        RerollCost = _rerollCost;
        bCanReroll = _bCanReroll;

        OnOffersPresented(true);
    }

    /// <summary>
    /// 구매가 확정됐다. 구매 연출 → (있다면) 배타 카드 소멸 연출 → 화면 닫기까지 재생한 뒤 _onComplete를 부른다.
    /// </summary>
    public virtual void PlayPurchase(in RoguelikeCardPurchaseViewData _purchase, IReadOnlyList<int> _discardedIndices, Action _onComplete)
    {
        // [UI 담당] 연출을 넣고 끝에서 Hide()와 _onComplete를 부른다. 기본 구현은 연출 없이 바로 닫는다.
        Hide();
        _onComplete?.Invoke();
    }

    /// <summary>건너뛰기 등으로 구매 없이 닫는다. 닫힘 연출 뒤 _onComplete를 부른다.</summary>
    public virtual void CloseSelection(Action _onComplete)
    {
        Hide();
        _onComplete?.Invoke();
    }

    /// <summary>구매·리롤이 거절됐다(골드 부족 등). 흔들림 같은 피드백을 준다.</summary>
    public virtual void PlayRejected(ECardShopResult _reason, int _offerIndex)
    {
    }

    /// <summary>[UI 담당] 카드가 처음 제시되거나(_bReroll=false) 리롤로 바뀌었을 때(_bReroll=true) 카드를 다시 그린다.</summary>
    protected virtual void OnOffersPresented(bool _bReroll)
    {
    }

    // // 입력 전달 (UI 담당이 버튼·카드 클릭에서 부른다)

    protected void RequestBuy(int _offerIndex) => BuyRequestedEvent?.Invoke(_offerIndex);
    protected void RequestSkip() => SkipRequestedEvent?.Invoke();
    protected void RequestReroll() => RerollRequestedEvent?.Invoke();

    private void CopyOffers(IReadOnlyList<RoguelikeCardViewData> _offers)
    {
        offers.Clear();
        if (null == _offers) return;

        for (int i = 0; i < _offers.Count; i++) offers.Add(_offers[i]);
    }

    public override void OnDestroy()
    {
        BuyRequestedEvent = null;
        SkipRequestedEvent = null;
        RerollRequestedEvent = null;
        base.OnDestroy();
    }
}
