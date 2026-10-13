using System;
using System.Collections.Generic;

/// <summary>구매 한 번의 결과. 카드 UI 연출과 효과 적용이 이 값을 그대로 받는다.</summary>
public struct CardPurchaseResult
{
    public int offerIndex;
    public CardDefinitionSO card;
    /// <summary>구매 후 레벨.</summary>
    public int newLevel;
    public int pricePaid;
    public bool bNewCard;
    public ECardDisplayGrade displayGrade;
}

public enum ECardShopResult
{
    Success = 0,
    NotOpen = 1,
    InvalidIndex = 2,
    NotEnoughGold = 3,
    RerollNotAllowed = 4,
    SkipNotAllowed = 5,
}

/// <summary>
/// 카드 선택 단계의 규칙(제시·구매·건너뛰기·리롤). 기획서 3-3 "선택 단계 규칙".
/// 구매하거나 건너뛰면 선택이 닫히고 런 컨트롤러가 다음 단계(지형 전환 또는 전투)로 넘어간다.
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
    private RunWallet wallet;
    private RunModifiers modifiers;

    public bool IsOpen { get; private set; }
    public bool IsFirstPick { get; private set; }
    public IReadOnlyList<CardOffer> Offers => offers;

    public void Initialize(CardDatabaseSO _database, RoguelikeBalanceSO _balance, RunBuildState _build, RunWallet _wallet, RunModifiers _modifiers)
    {
        database = _database;
        balance = _balance;
        build = _build;
        wallet = _wallet;
        modifiers = _modifiers;
    }

    public void ResetRun(int _seed)
    {
        generator.ResetRun(_seed);
        offers.Clear();
        IsOpen = false;
        IsFirstPick = false;
        RecalculateModifiers();
    }

    public int RerollCost => CardPricing.GetRerollCost(build.RerollCount, modifiers, balance);

    public bool CanReroll => IsOpen && CardPricing.CanReroll(IsFirstPick, modifiers);

    public bool CanSkip => IsOpen && (false == IsFirstPick || balance.firstPickSkippable);

    public void Open(bool _bFirstPick)
    {
        IsFirstPick = _bFirstPick;
        IsOpen = true;
        Regenerate();
    }

    public void Close()
    {
        IsOpen = false;
    }

    /// <summary>
    /// 제시된 _offerIndex번 카드를 산다. 성공하면 선택이 닫힌다.
    /// _discardedOfferIndices에는 같은 배타 그룹이라 함께 사라져야 하는 다른 제시 카드의 인덱스가 담긴다(기획서 5-3 "배타 카드 소멸").
    /// </summary>
    public ECardShopResult TryBuy(int _offerIndex, out CardPurchaseResult _result, List<int> _discardedOfferIndices)
    {
        _result = default;
        _discardedOfferIndices?.Clear();

        if (false == IsOpen) return ECardShopResult.NotOpen;
        if (_offerIndex < 0 || _offerIndex >= offers.Count || false == offers[_offerIndex].IsValid) return ECardShopResult.InvalidIndex;

        CardOffer offer = offers[_offerIndex];

        // 제시 이후 이미 MAX가 됐거나 배타로 막힌 카드는 살 수 없다. 골드를 빼기 전에 걸러야 "골드만 빠지는" 구매가 없다.
        if (build.IsMax(offer.card) || build.IsExcluded(offer.card)) return ECardShopResult.InvalidIndex;
        if (false == wallet.TrySpend(offer.price)) return ECardShopResult.NotEnoughGold;

        build.Acquire(offer.card);

        _result = new CardPurchaseResult
        {
            offerIndex = _offerIndex,
            card = offer.card,
            newLevel = build.GetLevel(offer.card),
            pricePaid = offer.price,
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

        IsOpen = false;
        RecalculateModifiers();
        CardPurchasedEvent?.Invoke(_result);

        return ECardShopResult.Success;
    }

    public ECardShopResult TryReroll()
    {
        if (false == IsOpen) return ECardShopResult.NotOpen;
        if (false == CardPricing.CanReroll(IsFirstPick, modifiers)) return ECardShopResult.RerollNotAllowed;
        if (false == wallet.TrySpend(RerollCost)) return ECardShopResult.NotEnoughGold;

        build.IncreaseRerollCount();
        Regenerate();

        return ECardShopResult.Success;
    }

    public ECardShopResult TrySkip()
    {
        if (false == IsOpen) return ECardShopResult.NotOpen;
        if (false == CanSkip) return ECardShopResult.SkipNotAllowed;

        IsOpen = false;
        return ECardShopResult.Success;
    }

    /// <summary>디버그·테스트용. 제시와 무관하게 카드를 바로 얻는다(골드 차감 없음).</summary>
    public bool GrantDirect(CardDefinitionSO _card)
    {
        if (false == build.Acquire(_card)) return false;
        RecalculateModifiers();
        return true;
    }

    public void RecalculateModifiers()
    {
        RunEffectResolver.Resolve(build, modifiers, effectScratch);
        ModifiersChangedEvent?.Invoke();
    }

    private void Regenerate()
    {
        CardOfferContext ctx = new CardOfferContext
        {
            database = database,
            build = build,
            modifiers = modifiers,
            balance = balance,
            gold = wallet.Gold,
            bFirstPick = IsFirstPick,
        };

        generator.Generate(in ctx, offers);
        OffersChangedEvent?.Invoke();
    }
}
