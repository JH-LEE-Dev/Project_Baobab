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
/// 카드 선택 단계의 규칙(제시·구매·건너뛰기·리롤·감정). 기획서 4-1 "선택 단계 규칙", 4-4 "카드 감정".
/// 구매하거나 건너뛰면 선택이 닫히고 런 컨트롤러가 다음 단계(지형 전환 또는 전투)로 넘어간다.
/// </summary>
public sealed class CardShop
{
    public event Action OffersChangedEvent;
    public event Action<CardPurchaseResult> CardPurchasedEvent;
    public event Action<CardAppraisalResult> CardAppraisedEvent;
    public event Action ModifiersChangedEvent;

    private readonly CardOfferGenerator generator = new CardOfferGenerator();
    private readonly CardAppraisal appraisal = new CardAppraisal();
    private readonly List<CardOffer> offers = new List<CardOffer>(8);
    private readonly List<CardEffectEntry> effectScratch = new List<CardEffectEntry>(32);

    private CardDatabaseSO database;
    private RoguelikeBalanceSO balance;
    private RunBuildState build;
    private RunWallet wallet;
    private RunModifiers modifiers;

    private int freeRerollsLeft;
    private int cachedCheapestFee = -1;
    private bool bCheapestFeeDirty = true;

    public bool IsOpen { get; private set; }
    public bool IsFirstPick { get; private set; }
    /// <summary>이번 선택 단계에 감정사가 와 있는지. 선택이 닫히면 남은 기회와 함께 사라진다.</summary>
    public bool IsAppraiserPresent { get; private set; }
    public IReadOnlyList<CardOffer> Offers => offers;

    /// <summary>이번 판에 감정사가 등장하는 웨이브(런 시작 때 정해진다).</summary>
    public int AppraiserWave => appraisal.AppraiserWave;

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
        appraisal.ResetRun(unchecked(_seed * 31 + 7), balance);
        offers.Clear();
        IsOpen = false;
        IsFirstPick = false;
        IsAppraiserPresent = false;
        freeRerollsLeft = 0;
        RecalculateModifiers();
    }

    /// <summary>지금 리롤하면 드는 비용. 첫 선택의 무료 리롤이 남아 있으면 0.</summary>
    public int RerollCost => HasFreeReroll ? 0 : CardPricing.GetRerollCost(build.RerollCount, modifiers, balance);

    public bool CanReroll
    {
        get
        {
            if (false == IsOpen) return false;
            if (IsFirstPick) return HasFreeReroll;
            return CardPricing.CanReroll(modifiers);
        }
    }

    public bool CanSkip => IsOpen && (false == IsFirstPick || balance.firstPickSkippable);

    private bool HasFreeReroll => IsOpen && IsFirstPick && freeRerollsLeft > 0;

    public bool IsAppraiserWave(int _waveNumber) => appraisal.IsAppraiserWave(_waveNumber);

    public void Open(bool _bFirstPick, bool _bAppraiserPresent = false)
    {
        IsFirstPick = _bFirstPick;
        IsAppraiserPresent = _bAppraiserPresent;
        freeRerollsLeft = _bFirstPick ? Math.Max(0, balance.firstPickFreeRerolls) : 0;
        bCheapestFeeDirty = true;
        IsOpen = true;
        generator.BeginSelection();
        Regenerate(false);
    }

    public void Close()
    {
        IsOpen = false;
        IsAppraiserPresent = false;
    }

    /// <summary>
    /// 제시된 _offerIndex번 카드를 산다. 성공하면 선택이 닫힌다.
    /// _discardedOfferIndices에는 같은 배타 그룹이라 함께 사라져야 하는 다른 제시 카드의 인덱스가 담긴다(기획서 10-3 "배타 카드 소멸").
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

        Close();
        RecalculateModifiers();
        CardPurchasedEvent?.Invoke(_result);

        return ECardShopResult.Success;
    }

    public ECardShopResult TryReroll()
    {
        if (false == IsOpen) return ECardShopResult.NotOpen;
        if (false == CanReroll) return ECardShopResult.RerollNotAllowed;

        // 첫 선택의 무료 리롤은 리롤 비용 상승 횟수에 넣지 않는다(기획서 4-1 "첫 선택").
        if (HasFreeReroll)
        {
            freeRerollsLeft--;
        }
        else
        {
            if (false == wallet.TrySpend(RerollCost)) return ECardShopResult.NotEnoughGold;
            build.IncreaseRerollCount();
        }

        Regenerate(true);

        return ECardShopResult.Success;
    }

    public ECardShopResult TrySkip()
    {
        if (false == IsOpen) return ECardShopResult.NotOpen;
        if (false == CanSkip) return ECardShopResult.SkipNotAllowed;

        Close();
        return ECardShopResult.Success;
    }

    // // 감정

    /// <summary>
    /// 감정 버튼을 쓸 수 있는지(기획서 4-4 "골드 부족 시 감정 버튼 잠금").
    /// 감정사가 와 있어도, 남은 횟수가 없거나 감정할 수 있는 카드가 없거나 보유 골드가 가장 싼 감정비보다 적으면 잠긴다.
    /// </summary>
    public bool CanAppraise
    {
        get
        {
            if (false == IsOpen || false == IsAppraiserPresent || false == appraisal.HasRemaining(balance)) return false;

            int cheapest = CheapestAppraisalFee;
            return cheapest >= 0 && cheapest <= wallet.Gold;
        }
    }

    /// <summary>
    /// 감정할 수 있는 카드 중 가장 싼 감정비. 대상 카드가 없으면 -1.
    /// 보유 카드·레벨·감정 이력이 바뀔 때만 다시 계산한다(UI가 버튼 잠금을 매 프레임 읽어도 싸다).
    /// </summary>
    public int CheapestAppraisalFee
    {
        get
        {
            if (false == bCheapestFeeDirty) return cachedCheapestFee;

            int cheapest = -1;
            IReadOnlyList<CardDefinitionSO> owned = build.OwnedCards;
            for (int i = 0; i < owned.Count; i++)
            {
                if (false == appraisal.IsEligible(owned[i], build)) continue;

                int fee = GetAppraisalFee(owned[i]);
                if (cheapest < 0 || fee < cheapest) cheapest = fee;
            }

            cachedCheapestFee = cheapest;
            bCheapestFeeDirty = false;
            return cheapest;
        }
    }

    /// <summary>감정사가 와 있고, 골드와 무관하게 맡길 수 있는 카드가 남아 있는지.</summary>
    public bool HasAppraisableCards => IsOpen && IsAppraiserPresent && appraisal.HasRemaining(balance) && CheapestAppraisalFee >= 0;

    /// <summary>_card가 지금 감정 대상인지(골드와 무관).</summary>
    public bool IsAppraisable(CardDefinitionSO _card)
    {
        return IsOpen && IsAppraiserPresent && appraisal.HasRemaining(balance) && appraisal.IsEligible(_card, build);
    }

    public int GetAppraisalFee(CardDefinitionSO _card)
    {
        return CardPricing.GetAppraisalFee(_card, build.GetLevel(_card), balance);
    }

    /// <summary>
    /// _card를 감정한다. 감정비를 내는 순간 결과가 확정되고, 승격이면 즉시 그 레벨이 된다(하락 없음).
    /// 선택 기회를 쓰지 않으므로 선택은 열린 채로 남는다. 제시 중인 같은 카드의 레벨·가격은 새 레벨 기준으로 바뀐다.
    /// </summary>
    public ECardAppraisalResult TryAppraise(CardDefinitionSO _card, out CardAppraisalResult _result)
    {
        _result = default;

        if (false == IsOpen || false == IsAppraiserPresent) return ECardAppraisalResult.NotAvailable;
        if (false == appraisal.HasRemaining(balance)) return ECardAppraisalResult.LimitReached;
        if (false == appraisal.IsEligible(_card, build)) return ECardAppraisalResult.NotEligible;

        int fee = GetAppraisalFee(_card);
        if (false == wallet.TrySpend(fee)) return ECardAppraisalResult.NotEnoughGold;

        int fromLevel = build.GetLevel(_card);
        int toLevel = appraisal.RollResultLevel(fromLevel, _card.EffectiveMaxLevel, balance);
        appraisal.MarkAppraised(_card);
        bCheapestFeeDirty = true;

        _result = new CardAppraisalResult
        {
            card = _card,
            fromLevel = fromLevel,
            toLevel = fromLevel,
            feePaid = fee,
        };

        if (build.PromoteTo(_card, toLevel))
        {
            _result.toLevel = build.GetLevel(_card);
            RecalculateModifiers();
            _result.bOffersChanged = RefreshOffersFor(_card);
        }

        CardAppraisedEvent?.Invoke(_result);
        return ECardAppraisalResult.Success;
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
        // 보유 카드나 레벨이 바뀌는 모든 경로(구매·감정 승격·디버그 지급·런 초기화)가 여기를 지난다.
        bCheapestFeeDirty = true;
        RunEffectResolver.Resolve(build, modifiers, effectScratch);
        ModifiersChangedEvent?.Invoke();
    }

    /// <summary>
    /// 감정으로 레벨이 바뀐 카드가 제시 중이면 그 칸의 구매 후 레벨·가격·표시 등급을 새로 맞춘다.
    /// MAX가 되어 더 살 수 없게 된 카드는 그 칸을 겹치지 않는 다른 카드로 바꾸고, 바꿀 카드가 없으면 칸을 뺀다.
    /// 어느 경우든 OffersChangedEvent 뒤에 UI가 제시 목록 전체를 다시 그린다(RoguelikeOffersChangedSignal).
    /// </summary>
    private bool RefreshOffersFor(CardDefinitionSO _card)
    {
        bool bChanged = false;
        bool bMax = build.IsMax(_card);

        for (int i = offers.Count - 1; i >= 0; i--)
        {
            if (offers[i].card != _card) continue;

            if (false == bMax)
            {
                offers[i] = generator.CreateOffer(BuildContext(), _card, offers[i].bGuaranteed);
            }
            else if (generator.TryCreateReplacement(BuildContext(), offers, i, out CardOffer replacement))
            {
                offers[i] = replacement;
            }
            else
            {
                offers.RemoveAt(i);
            }

            bChanged = true;
        }

        if (bChanged) OffersChangedEvent?.Invoke();
        return bChanged;
    }

    private CardOfferContext BuildContext(bool _bReroll = false)
    {
        return new CardOfferContext
        {
            database = database,
            build = build,
            modifiers = modifiers,
            balance = balance,
            gold = wallet.Gold,
            bFirstPick = IsFirstPick,
            bReroll = _bReroll,
        };
    }

    private void Regenerate(bool _bReroll)
    {
        CardOfferContext ctx = BuildContext(_bReroll);
        generator.Generate(in ctx, offers);
        OffersChangedEvent?.Invoke();
    }
}
