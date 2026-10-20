using System;
using System.Collections.Generic;

/// <summary>카드 선택 한 번의 결과. 카드 UI 연출과 효과 적용이 이 값을 그대로 받는다.</summary>
public struct CardPurchaseResult
{
    public int offerIndex;
    public CardDefinitionSO card;
    /// <summary>고른 뒤의 레벨.</summary>
    public int newLevel;
    public bool bNewCard;
    public ECardDisplayGrade displayGrade;
}

public enum ECardShopResult
{
    Success = 0,
    NotOpen = 1,
    InvalidIndex = 2,
    RerollNotAllowed = 4,
}

/// <summary>
/// 카드 선택 단계의 규칙(제시·선택·리롤). 기획서 v2.4 4-1 "선택 단계 규칙".
///   - 모든 카드는 무료다. 골드·가격은 없다.
///   - 리롤은 선택 단계마다 정해진 횟수(1회)만. 쓰지 않아도 다음 선택으로 이월하지 않는다. 첫 선택도 같다.
///   - 건너뛰기는 없다. 카드를 고르면 선택이 닫히고 런 컨트롤러가 다음 단계(지형 전환 또는 전투)로 넘어간다.
/// 이름의 Buy·Purchase는 v2.3까지의 구매 흐름에서 온 것으로, 지금은 "고르기"를 뜻한다(UI 이벤트 이름과 맞춘다).
/// </summary>
public sealed class CardShop
{
    public event Action OffersChangedEvent;
    public event Action<CardPurchaseResult> CardPurchasedEvent;
    public event Action ModifiersChangedEvent;

    private readonly CardOfferGenerator generator = new CardOfferGenerator();
    private readonly List<CardOffer> offers = new List<CardOffer>(8);
    private readonly List<CardEffectEntry> effectScratch = new List<CardEffectEntry>(32);

    private CardDatabaseSO database;
    private RoguelikeBalanceSO balance;
    private RunBuildState build;
    private RunModifiers modifiers;

    public bool IsOpen { get; private set; }
    public bool IsFirstPick { get; private set; }
    /// <summary>이번 선택 단계에 남은 리롤 횟수.</summary>
    public int RerollsLeft { get; private set; }
    public IReadOnlyList<CardOffer> Offers => offers;

    public bool CanReroll => IsOpen && RerollsLeft > 0;

    public void Initialize(CardDatabaseSO _database, RoguelikeBalanceSO _balance, RunBuildState _build, RunModifiers _modifiers)
    {
        database = _database;
        balance = _balance;
        build = _build;
        modifiers = _modifiers;
    }

    public void ResetRun(int _seed)
    {
        generator.ResetRun(_seed);
        offers.Clear();
        IsOpen = false;
        IsFirstPick = false;
        RerollsLeft = 0;
        RecalculateModifiers();
    }

    public void Open(bool _bFirstPick)
    {
        IsFirstPick = _bFirstPick;
        RerollsLeft = Math.Max(0, balance.rerollsPerSelection);
        IsOpen = true;
        generator.BeginSelection();
        Regenerate(false);
    }

    public void Close()
    {
        IsOpen = false;
        RerollsLeft = 0;
    }

    /// <summary>
    /// 제시된 _offerIndex번 카드를 고른다. 성공하면 선택이 닫힌다.
    /// _discardedOfferIndices에는 같은 배타 그룹이라 함께 사라져야 하는 다른 제시 카드의 인덱스가 담긴다(기획서 10-3 "배타 카드 소멸").
    /// </summary>
    public ECardShopResult TryBuy(int _offerIndex, out CardPurchaseResult _result, List<int> _discardedOfferIndices)
    {
        _result = default;
        _discardedOfferIndices?.Clear();

        if (false == IsOpen) return ECardShopResult.NotOpen;
        if (_offerIndex < 0 || _offerIndex >= offers.Count || false == offers[_offerIndex].IsValid) return ECardShopResult.InvalidIndex;

        CardOffer offer = offers[_offerIndex];

        // 제시 이후 이미 MAX가 됐거나 배타로 막힌 카드는 고를 수 없다.
        if (build.IsMax(offer.card) || build.IsExcluded(offer.card)) return ECardShopResult.InvalidIndex;

        build.Acquire(offer.card);

        _result = new CardPurchaseResult
        {
            offerIndex = _offerIndex,
            card = offer.card,
            newLevel = build.GetLevel(offer.card),
            bNewCard = offer.bNewCard,
            displayGrade = offer.displayGrade,
        };

        if (null != _discardedOfferIndices && 0 != offer.card.exclusiveGroup)
        {
            for (int i = 0; i < offers.Count; i++)
            {
                if (i == _offerIndex || false == offers[i].IsValid) continue;
                if (offers[i].card.exclusiveGroup == offer.card.exclusiveGroup) _discardedOfferIndices.Add(i);
            }
        }

        Close();
        RecalculateModifiers();
        CardPurchasedEvent?.Invoke(_result);

        return ECardShopResult.Success;
    }

    /// <summary>제시 카드를 전부 바꾼다. 선택 단계마다 정해진 횟수만 쓸 수 있다(기획서 4-1 "리롤").</summary>
    public ECardShopResult TryReroll()
    {
        if (false == IsOpen) return ECardShopResult.NotOpen;
        if (false == CanReroll) return ECardShopResult.RerollNotAllowed;

        RerollsLeft--;
        Regenerate(true);

        return ECardShopResult.Success;
    }

    /// <summary>디버그·테스트용. 제시와 무관하게 카드를 바로 얻는다(선택 횟수에는 센다).</summary>
    public bool GrantDirect(CardDefinitionSO _card)
    {
        if (false == build.Acquire(_card)) return false;
        RecalculateModifiers();
        return true;
    }

    public void RecalculateModifiers()
    {
        // 보유 카드나 레벨이 바뀌는 모든 경로(선택·디버그 지급·런 초기화)가 여기를 지난다.
        RunEffectResolver.Resolve(build, modifiers, effectScratch);
        ModifiersChangedEvent?.Invoke();
    }

    private void Regenerate(bool _bReroll)
    {
        CardOfferContext ctx = new CardOfferContext
        {
            database = database,
            build = build,
            balance = balance,
            bFirstPick = IsFirstPick,
            bReroll = _bReroll,
        };

        generator.Generate(in ctx, offers);
        OffersChangedEvent?.Invoke();
    }
}
