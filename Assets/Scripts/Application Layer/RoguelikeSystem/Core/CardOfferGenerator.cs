using System.Collections.Generic;
using UnityEngine;

/// <summary>제시된 카드 한 장. UI가 그리는 데 필요한 판단은 전부 여기서 끝낸다.</summary>
public struct CardOffer
{
    public CardDefinitionSO card;
    /// <summary>구매 후 레벨(처음 구매는 1). 루비·프리즘은 1.</summary>
    public int levelAfterPurchase;
    public int price;
    public ECardDisplayGrade displayGrade;
    /// <summary>보유하지 않은 카드. 자물쇠가 풀리는 등장 연출 대상(기획서 5-3 "첫 등장 카드").</summary>
    public bool bNewCard;
    /// <summary>이번 판에서 처음 제시된 카드.</summary>
    public bool bFirstSeen;
    /// <summary>에메랄드·프리즘 보장 노출로 들어온 카드.</summary>
    public bool bGuaranteed;

    public bool IsValid => null != card;

    public bool IsAffordable(long _gold) => price <= _gold;
}

/// <summary>제시 카드 생성에 필요한 입력. 생성기는 이 값들을 읽기만 한다.</summary>
public struct CardOfferContext
{
    public CardDatabaseSO database;
    public RunBuildState build;
    public RunModifiers modifiers;
    public RoguelikeBalanceSO balance;
    public long gold;
    public bool bFirstPick;
}

/// <summary>
/// 카드 제시 파이프라인. 기획서 3-3의 적용 순서를 그대로 따른다.
///   1. 후보 풀: MAX·배타 그룹 제외·조건 미충족 프리즘을 뺀다. (배타 제외가 가장 먼저라 보장 노출로 되살아나지 않는다)
///   2. 보장 노출: 에메랄드·프리즘 조건 카드를 한 번에 cap장까지. 못 넣은 카드는 다음 선택으로 이월된다.
///   3. 구매 가능 보장: 보유 골드로 단독 구매 가능한 카드가 affordableGuaranteeCount장 이상 되도록 채운다.
///   4. 파트너 확정 노출: 조합 재료 한 장이 MAX면 파트너를 남은 칸에 1장 넣는다.
///   5. 가중치: 남은 칸을 업그레이드 우선·파트너 유도 가중치(편향 상한 적용)로 뽑는다.
///
/// [보장끼리 부딪칠 때] 보장 노출 카드가 칸을 먼저 차지하므로, 칸이 모자라면 구매 가능 보장이
/// 다 채워지지 않을 수 있다(예: 3칸 중 2칸이 비싼 프리즘·에메랄드). 기획서의 우선순위대로 둔 결과다.
/// 후보 중 살 수 있는 카드가 애초에 모자랄 때도 있는 만큼만 넣는다.
///
/// 리롤도 같은 파이프라인을 다시 돌린다. 리스트는 모두 재사용하므로 호출마다 할당이 없다.
/// </summary>
public sealed class CardOfferGenerator
{
    private readonly List<CardDefinitionSO> candidates = new List<CardDefinitionSO>(64);
    private readonly List<CardDefinitionSO> eligibleGuaranteed = new List<CardDefinitionSO>(16);
    // 보장 노출 대기열. 앞에서부터 꺼내 보여주고, 보여준 카드는 뒤로 보내 다음 선택에서 다른 카드가 먼저 나오게 한다.
    private readonly List<CardDefinitionSO> guaranteedQueue = new List<CardDefinitionSO>(16);
    private readonly List<CardDefinitionSO> takenThisRound = new List<CardDefinitionSO>(8);
    private readonly List<CardDefinitionSO> forcedPartnerCandidates = new List<CardDefinitionSO>(8);

    private System.Random rng = new System.Random();

    public void ResetRun(int _seed)
    {
        rng = new System.Random(_seed);
        guaranteedQueue.Clear();
    }

    public void Generate(in CardOfferContext _ctx, List<CardOffer> _results)
    {
        _results.Clear();

        if (null == _ctx.database || null == _ctx.build || null == _ctx.balance) return;

        int slots = Mathf.Max(1, _ctx.balance.offerCount);

        BuildCandidates(in _ctx);
        FillGuaranteed(in _ctx, slots, _results);
        FillAffordable(in _ctx, slots, _results);
        FillForcedPartner(in _ctx, slots, _results);
        FillWeighted(in _ctx, slots, _results);
        Shuffle(_results);

        for (int i = 0; i < _results.Count; i++)
        {
            _ctx.build.MarkSeen(_results[i].card);
        }
    }

    // // 1. 후보 풀

    private void BuildCandidates(in CardOfferContext _ctx)
    {
        candidates.Clear();

        for (int i = 0; i < _ctx.database.Count; i++)
        {
            CardDefinitionSO card = _ctx.database.Get(i);
            if (IsEligible(in _ctx, card)) candidates.Add(card);
        }
    }

    private static bool IsEligible(in CardOfferContext _ctx, CardDefinitionSO _card)
    {
        if (null == _card) return false;

        RunBuildState build = _ctx.build;
        if (build.IsMax(_card)) return false;
        if (build.IsExcluded(_card)) return false;

        if (ECardKind.Prism == _card.kind)
        {
            if (null == _card.comboMaterialA || null == _card.comboMaterialB) return false;
            if (false == build.IsMax(_card.comboMaterialA) || false == build.IsMax(_card.comboMaterialB)) return false;
        }

        if (_ctx.bFirstPick)
        {
            if (ECardKind.Ruby == _card.kind || ECardKind.Prism == _card.kind) return false;
            if (_ctx.balance.firstPickBronzeOnly && ECardGrade.Bronze != _card.grade) return false;
            if (_ctx.balance.firstPickActiveOnly && ECardKind.Active != _card.kind) return false;
        }

        return true;
    }

    // // 2. 보장 노출

    private void FillGuaranteed(in CardOfferContext _ctx, int _slots, List<CardOffer> _results)
    {
        eligibleGuaranteed.Clear();

        // 프리즘을 먼저 모은다. 새로 대기열에 들어갈 때 프리즘이 에메랄드보다 앞에 서게 된다.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                CardDefinitionSO card = candidates[i];
                ECardDisplayGrade grade = CardPricing.GetDisplayGrade(card, _ctx.build.GetLevel(card) + 1);

                bool bPrismPass = 0 == pass;
                if (bPrismPass && ECardDisplayGrade.Prism == grade) eligibleGuaranteed.Add(card);
                else if (false == bPrismPass && ECardDisplayGrade.Emerald == grade) eligibleGuaranteed.Add(card);
            }
        }

        // 조건이 풀린 카드(구매했거나 배타로 빠진 카드)는 대기열에서 뺀다.
        for (int i = guaranteedQueue.Count - 1; i >= 0; i--)
        {
            if (false == eligibleGuaranteed.Contains(guaranteedQueue[i])) guaranteedQueue.RemoveAt(i);
        }

        for (int i = 0; i < eligibleGuaranteed.Count; i++)
        {
            if (false == guaranteedQueue.Contains(eligibleGuaranteed[i])) guaranteedQueue.Add(eligibleGuaranteed[i]);
        }

        int take = Mathf.Min(Mathf.Max(0, _ctx.balance.guaranteedSlotCap), _slots, guaranteedQueue.Count);

        takenThisRound.Clear();
        for (int i = 0; i < take; i++)
        {
            CardDefinitionSO card = guaranteedQueue[i];
            takenThisRound.Add(card);
            candidates.Remove(card);
            AddOffer(in _ctx, card, true, _results);
        }

        // 보여준 카드는 대기열 뒤로 보낸다. 이번에 못 나온 카드가 다음 선택에서 먼저 나온다.
        for (int i = 0; i < takenThisRound.Count; i++)
        {
            guaranteedQueue.Remove(takenThisRound[i]);
            guaranteedQueue.Add(takenThisRound[i]);
        }
    }

    // // 3. 구매 가능 보장

    private void FillAffordable(in CardOfferContext _ctx, int _slots, List<CardOffer> _results)
    {
        int target = Mathf.Min(Mathf.Max(0, _ctx.balance.affordableGuaranteeCount), _slots);

        int affordable = 0;
        for (int i = 0; i < _results.Count; i++)
        {
            if (_results[i].IsAffordable(_ctx.gold)) affordable++;
        }

        while (affordable < target && _results.Count < _slots)
        {
            int pick = PickWeighted(in _ctx, true);
            if (pick < 0) break;

            CardDefinitionSO card = candidates[pick];
            candidates.RemoveAt(pick);
            AddOffer(in _ctx, card, false, _results);
            affordable++;
        }
    }

    // // 4. 파트너 확정 노출

    private void FillForcedPartner(in CardOfferContext _ctx, int _slots, List<CardOffer> _results)
    {
        if (false == _ctx.balance.forcePartnerWhenMaterialMax || _results.Count >= _slots) return;

        forcedPartnerCandidates.Clear();

        for (int i = 0; i < candidates.Count; i++)
        {
            CardDefinitionSO card = candidates[i];
            if (IsForcedPartner(in _ctx, card)) forcedPartnerCandidates.Add(card);
        }

        if (0 == forcedPartnerCandidates.Count) return;

        CardDefinitionSO chosen = forcedPartnerCandidates[rng.Next(forcedPartnerCandidates.Count)];
        candidates.Remove(chosen);
        AddOffer(in _ctx, chosen, false, _results);
    }

    /// <summary>_card와 짝을 이루는 조합 재료가 이미 MAX인지(그리고 그 조합을 아직 사지 않았는지).</summary>
    private static bool IsForcedPartner(in CardOfferContext _ctx, CardDefinitionSO _card)
    {
        CardDatabaseSO db = _ctx.database;
        for (int i = 0; i < db.Count; i++)
        {
            CardDefinitionSO prism = db.Get(i);
            if (null == prism || ECardKind.Prism != prism.kind || _ctx.build.IsOwned(prism)) continue;
            if (false == prism.IsComboMaterial(_card)) continue;

            CardDefinitionSO other = prism.comboMaterialA == _card ? prism.comboMaterialB : prism.comboMaterialA;
            if (null != other && _ctx.build.IsMax(other)) return true;
        }

        return false;
    }

    // // 5. 가중치

    private void FillWeighted(in CardOfferContext _ctx, int _slots, List<CardOffer> _results)
    {
        while (_results.Count < _slots)
        {
            int pick = PickWeighted(in _ctx, false);
            if (pick < 0) break;

            CardDefinitionSO card = candidates[pick];
            candidates.RemoveAt(pick);
            AddOffer(in _ctx, card, false, _results);
        }
    }

    /// <summary>후보 중 하나를 가중치로 뽑아 인덱스를 돌려준다. 없으면 -1.</summary>
    private int PickWeighted(in CardOfferContext _ctx, bool _bAffordableOnly)
    {
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (_bAffordableOnly && false == IsAffordable(in _ctx, candidates[i])) continue;
            total += GetWeight(in _ctx, candidates[i]);
        }

        if (total <= 0f) return -1;

        float roll = (float)(rng.NextDouble() * total);
        float cumulative = 0f;
        int last = -1;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (_bAffordableOnly && false == IsAffordable(in _ctx, candidates[i])) continue;

            last = i;
            cumulative += GetWeight(in _ctx, candidates[i]);
            if (roll < cumulative) return i;
        }

        // 부동소수 누적 오차로 끝까지 못 걸렸으면 마지막 후보.
        return last;
    }

    private static float GetWeight(in CardOfferContext _ctx, CardDefinitionSO _card)
    {
        RoguelikeBalanceSO balance = _ctx.balance;
        float baseWeight = Mathf.Max(0.0001f, balance.baseWeight);
        float weight = baseWeight;

        if (_ctx.build.IsOwned(_card))
        {
            weight *= 1f + Mathf.Max(0f, balance.upgradeWeightBonus);
        }

        if (IsPartnerTarget(in _ctx, _card))
        {
            weight *= 1f + Mathf.Max(0f, balance.partnerWeightBonus);
        }

        float cap = baseWeight * Mathf.Max(1f, balance.maxWeightMultiplier);
        return Mathf.Min(weight, cap);
    }

    /// <summary>조합 재료 한 장이 partnerStartLevel 이상이라 이 카드의 노출을 유도해야 하는지.</summary>
    private static bool IsPartnerTarget(in CardOfferContext _ctx, CardDefinitionSO _card)
    {
        CardDatabaseSO db = _ctx.database;
        int startLevel = Mathf.Max(1, _ctx.balance.partnerStartLevel);

        for (int i = 0; i < db.Count; i++)
        {
            CardDefinitionSO prism = db.Get(i);
            if (null == prism || ECardKind.Prism != prism.kind || _ctx.build.IsOwned(prism)) continue;
            if (false == prism.IsComboMaterial(_card)) continue;

            CardDefinitionSO other = prism.comboMaterialA == _card ? prism.comboMaterialB : prism.comboMaterialA;
            if (null != other && _ctx.build.GetLevel(other) >= startLevel) return true;
        }

        return false;
    }

    // // 공통

    private static bool IsAffordable(in CardOfferContext _ctx, CardDefinitionSO _card)
    {
        return GetOfferPrice(in _ctx, _card, _ctx.build.GetLevel(_card) + 1) <= _ctx.gold;
    }

    private static int GetOfferPrice(in CardOfferContext _ctx, CardDefinitionSO _card, int _levelAfter)
    {
        if (_ctx.bFirstPick && _ctx.balance.firstPickFree) return 0;
        return CardPricing.GetPrice(_card, _levelAfter, _ctx.modifiers, _ctx.balance);
    }

    private static void AddOffer(in CardOfferContext _ctx, CardDefinitionSO _card, bool _bGuaranteed, List<CardOffer> _results)
    {
        int levelAfter = _ctx.build.GetLevel(_card) + 1;

        _results.Add(new CardOffer
        {
            card = _card,
            levelAfterPurchase = levelAfter,
            price = GetOfferPrice(in _ctx, _card, levelAfter),
            displayGrade = CardPricing.GetDisplayGrade(_card, levelAfter),
            bNewCard = false == _ctx.build.IsOwned(_card),
            bFirstSeen = false == _ctx.build.HasSeen(_card),
            bGuaranteed = _bGuaranteed,
        });
    }

    private void Shuffle(List<CardOffer> _results)
    {
        for (int i = _results.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            CardOffer tmp = _results[i];
            _results[i] = _results[j];
            _results[j] = tmp;
        }
    }
}
