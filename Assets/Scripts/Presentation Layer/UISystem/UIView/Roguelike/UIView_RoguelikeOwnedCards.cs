using System;
using System.Collections.Generic;

/// <summary>
/// 보유 카드 확인 UI. 기획서 5-2 "보유 카드 확인" - 전투 중에도 보유 카드 종류와 레벨을 볼 수 있다.
/// 프리팹에서 bCloseableByESC를 켜 두면 ESC/패드 B로 닫힌다.
///
/// 열려 있는 동안 게임이 일시정지된다(Time.timeScale = 0, RoguelikeUICoordinator가 건다). 전투 중에 열어도
/// 나무 스폰·임계치 피해가 진행되지 않는다. 닫히면(어떤 경로든) 원래 속도로 돌아간다.
/// 런이 진행 중인 단계(런 시작~웨이브 클리어·런 클리어)에서만 열리고, 쓰러짐·결과·로비로 넘어가면 시스템이 닫는다.
/// [UI 담당 구현 지점] OnCardsChanged에서 목록을 그린다. 닫기 버튼은 Hide()를 부르면 된다.
/// 일시정지 중이므로 이 화면의 트윈·애니메이션은 반드시 unscaled 시간(DOTween SetUpdate(true), Animator UnscaledTime)으로 돌린다.
/// </summary>
public class UIView_RoguelikeOwnedCards : UIView_RoguelikeModalBase
{
    public event Action ClosedEvent;

    protected readonly List<RoguelikeOwnedCardViewData> cards = new List<RoguelikeOwnedCardViewData>(32);

    public IReadOnlyList<RoguelikeOwnedCardViewData> Cards => cards;

    public virtual void SetCards(IReadOnlyList<RoguelikeOwnedCardViewData> _cards)
    {
        cards.Clear();
        if (null != _cards)
        {
            for (int i = 0; i < _cards.Count; i++) cards.Add(_cards[i]);
        }

        OnCardsChanged();
    }

    /// <summary>[UI 담당] 목록을 다시 그린다.</summary>
    protected virtual void OnCardsChanged() { }

    protected override void OnHideContent()
    {
        ClosedEvent?.Invoke();
    }

    public override void OnDestroy()
    {
        ClosedEvent = null;
        base.OnDestroy();
    }
}
