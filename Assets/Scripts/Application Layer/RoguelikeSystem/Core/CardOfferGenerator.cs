using System.Collections.Generic;
using UnityEngine;

/// <summary>제시된 카드 한 장. UI가 그리는 데 필요한 판단은 전부 여기서 끝낸다.</summary>
public struct CardOffer
{
    public CardDefinitionSO card;
    /// <summary>고른 뒤의 레벨(처음 선택은 1). 계약·프리즘은 1.</summary>
    public int levelAfterPurchase;
    public ECardDisplayGrade displayGrade;
    /// <summary>보유하지 않은 카드. 자물쇠가 풀리는 등장 연출 대상(기획서 10-3 "첫 등장 카드").</summary>
    public bool bNewCard;
    /// <summary>이번 판에서 처음 제시된 카드.</summary>
    public bool bFirstSeen;
    /// <summary>에메랄드·프리즘 보장 노출로 들어온 카드(리롤로 나온 카드는 보장이 아니다).</summary>
    public bool bGuaranteed;

    public bool IsValid => null != card;
}

/// <summary>제시 카드 생성에 필요한 입력. 생성기는 이 값들을 읽기만 한다.</summary>
public struct CardOfferContext
{
    public CardDatabaseSO database;
    public RunBuildState build;
    public RoguelikeBalanceSO balance;
    public bool bFirstPick;
    /// <summary>같은 선택 단계 안의 리롤인지. 리롤은 우선순위 없이 진짜 랜덤으로 뽑는다(기획서 v2.5 4-2 "리롤").</summary>
    public bool bReroll;
}

/// <summary>
/// 카드 제시 파이프라인. 기획서 v2.5 4-2 "카드 노출 규칙"의 구성 우선순위(프리즘 > MAX 레벨 > 패시브 2장 보장 > 랜덤)를 그대로 따른다.
///   0. 제외: MAX·배타 그룹·보유 계약·조건 미충족 프리즘을 뺀다. (가장 먼저라 보장 노출로 되살아나지 않는다)
///   1. 프리즘 · MAX 보장 노출: 프리즘 조건 카드와 에메랄드(MAX 직전 레벨 보유) 카드를 합쳐 한 번에 cap(2)장까지. 못 넣은 카드는 다음 선택으로 이월된다.
///      서로 배타인 프리즘이 함께 대기 중이면 cap과 무관하게 함께 노출한다(최대 제시 수).
///   2. 공통 패시브 보장: 남은 칸에 공통 패시브를 guaranteedPassiveCount(2)장까지 가중치로 넣는다. 첫 선택은 제외.
///   3. 파트너 확정 노출: 조합 재료 한 장이 MAX면 파트너를 남은 칸에 1장 넣는다(부록 A-1 "MAX면 한 선택에 1장 확실 노출").
///   4. 가중치 랜덤: 남은 칸을 종류 비중 × 업그레이드 우선 × 파트너 유도(편향 상한 적용)로 뽑는다.
///
/// 리롤은 위 우선순위를 쓰지 않는다: 제외만 거친 노출 가능 카드에서 균등하게 진짜 랜덤으로 뽑는다. 방금 나온 카드도 다시 나올 수 있고,
/// 한 선택 안에서는 같은 카드가 두 번 나오지 않는다. 리롤로 나오지 않은 프리즘 · MAX는 조건이 유지되는 동안 다음 선택에 다시 보장된다.
///
/// 후보가 모자라면 있는 만큼만 넣는다(MAX 카드나 보유 계약으로 채우지 않는다).
/// 리스트는 모두 재사용하므로 호출마다 할당이 없다.
/// </summary>
public sealed class CardOfferGenerator
{
    /// <summary>보장 노출 대기 카드. 먼저 쌓인 순서와 노출 이력을 들고 있다.</summary>
    private sealed class GuaranteedEntry
    {
        public CardDefinitionSO card;
        public bool bPrism;
        public int enqueueOrder;
        /// <summary>보장으로 노출된 선택 단계 수. 적게 노출된 카드가 먼저 나와 이월된 카드가 다음 선택에 나온다.</summary>
        public int shownSelections;
    }

    private readonly List<CardDefinitionSO> candidates = new List<CardDefinitionSO>(64);
    private readonly List<GuaranteedEntry> guaranteedQueue = new List<GuaranteedEntry>(16);
    private readonly List<GuaranteedEntry> entryPool = new List<GuaranteedEntry>(16);
    private readonly List<GuaranteedEntry> takeScratch = new List<GuaranteedEntry>(8);
    private readonly List<CardDefinitionSO> forcedPartnerCandidates = new List<CardDefinitionSO>(8);

    private System.Random rng = new System.Random();
    private int enqueueCounter;

    private static readonly System.Comparison<GuaranteedEntry> GuaranteedOrder = CompareGuaranteed;

    public void ResetRun(int _seed)
    {
        rng = new System.Random(_seed);
        for (int i = 0; i < guaranteedQueue.Count; i++) entryPool.Add(guaranteedQueue[i]);
        guaranteedQueue.Clear();
        enqueueCounter = 0;
    }

    public void Generate(in CardOfferContext _ctx, List<CardOffer> _results)
    {
        _results.Clear();

        if (null == _ctx.database || null == _ctx.build || null == _ctx.balance) return;

        int slots = Mathf.Max(1, _ctx.balance.offerCount);

        BuildCandidates(in _ctx);

        if (_ctx.bReroll)
        {
            FillUniformRandom(in _ctx, slots, _results);
        }
        else
        {
            FillGuaranteed(in _ctx, slots, _results);
            FillGuaranteedPassives(in _ctx, slots, _results);
            FillForcedPartner(in _ctx, slots, _results);
            FillWeighted(in _ctx, slots, _results);
        }

        Shuffle(_results);

        for (int i = 0; i < _results.Count; i++)
        {
            _ctx.build.MarkSeen(_results[i].card);
        }
    }

    /// <summary>_card 한 장의 제시 정보를 지금 빌드 기준으로 만든다.</summary>
    public CardOffer CreateOffer(in CardOfferContext _ctx, CardDefinitionSO _card, bool _bGuaranteed)
    {
        int levelAfter = _ctx.build.GetLevel(_card) + 1;

        return new CardOffer
        {
            card = _card,
            levelAfterPurchase = levelAfter,
            displayGrade = CardRarity.GetDisplayGrade(_card, levelAfter),
            bNewCard = false == _ctx.build.IsOwned(_card),
            bFirstSeen = false == _ctx.build.HasSeen(_card),
            bGuaranteed = _bGuaranteed,
        };
    }

    // // 0. 제외

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
        // 계약은 레벨 상한이 1이라 보유하면 MAX로 빠진다.
        if (build.IsMax(_card)) return false;
        if (build.IsExcluded(_card)) return false;

        if (ECardKind.Prism == _card.kind)
        {
            if (null == _card.comboMaterialA || null == _card.comboMaterialB) return false;
            if (false == build.IsMax(_card.comboMaterialA) || false == build.IsMax(_card.comboMaterialB)) return false;
        }

        if (_ctx.bFirstPick)
        {
            // 계약은 첫 선택을 제외한 어느 선택에서나 나온다(기획서 6-4). 첫 선택은 액티브 7종 중에서(4-1).
            if (ECardKind.Ruby == _card.kind || ECardKind.Prism == _card.kind) return false;
            if (_ctx.balance.firstPickActiveOnly && ECardKind.Active != _card.kind) return false;
        }

        return true;
    }

    // // 1. 프리즘 · MAX 보장 노출

    private void FillGuaranteed(in CardOfferContext _ctx, int _slots, List<CardOffer> _results)
    {
        SyncGuaranteedQueue(in _ctx);
        if (0 == guaranteedQueue.Count) return;

        guaranteedQueue.Sort(GuaranteedOrder);

        int cap = Mathf.Min(Mathf.Max(0, _ctx.balance.guaranteedSlotCap), _slots);
        takeScratch.Clear();

        for (int i = 0; i < guaranteedQueue.Count && takeScratch.Count < cap; i++) takeScratch.Add(guaranteedQueue[i]);

        // 서로 배타인(같은 배타 그룹이거나 재료가 겹치는) 프리즘은 함께 노출한다(양자택일). 슬롯 상한의 예외이며 최대 제시 수 − 1장까지
        // (4장이면 3장, 남은 한 칸은 패시브 보장, 기획서 v2.5 4-2).
        int conflictLimit = Mathf.Max(cap, _slots - 1);
        for (int i = 0; i < takeScratch.Count && takeScratch.Count < conflictLimit; i++)
        {
            GuaranteedEntry taken = takeScratch[i];
            if (false == taken.bPrism) continue;

            for (int j = 0; j < guaranteedQueue.Count && takeScratch.Count < conflictLimit; j++)
            {
                GuaranteedEntry other = guaranteedQueue[j];
                if (other.bPrism && taken.card.ConflictsWith(other.card) && false == takeScratch.Contains(other)) takeScratch.Add(other);
            }
        }

        for (int i = 0; i < takeScratch.Count; i++)
        {
            GuaranteedEntry entry = takeScratch[i];
            candidates.Remove(entry.card);
            _results.Add(CreateOffer(in _ctx, entry.card, true));
            entry.shownSelections++;
        }
    }

    /// <summary>후보 중 에메랄드·프리즘 조건 카드로 대기열을 맞춘다. 조건이 풀린 카드(선택·배타)는 빠지고 새 카드는 뒤에 쌓인다.</summary>
    private void SyncGuaranteedQueue(in CardOfferContext _ctx)
    {
        for (int i = guaranteedQueue.Count - 1; i >= 0; i--)
        {
            CardDefinitionSO card = guaranteedQueue[i].card;
            if (candidates.Contains(card) && IsGuaranteedGrade(in _ctx, card)) continue;

            entryPool.Add(guaranteedQueue[i]);
            guaranteedQueue.RemoveAt(i);
        }

        // 프리즘을 먼저 쌓는다. 같은 선택에서 새로 쌓이는 카드끼리도 프리즘이 앞선다.
        for (int pass = 0; pass < 2; pass++)
        {
            bool bPrismPass = 0 == pass;
            for (int i = 0; i < candidates.Count; i++)
            {
                CardDefinitionSO card = candidates[i];
                ECardDisplayGrade grade = CardRarity.GetDisplayGrade(card, _ctx.build.GetLevel(card) + 1);
                bool bMatch = bPrismPass ? ECardDisplayGrade.Prism == grade : ECardDisplayGrade.Emerald == grade;
                if (false == bMatch || IsQueued(card)) continue;

                GuaranteedEntry entry = RentEntry();
                entry.card = card;
                entry.bPrism = bPrismPass;
                entry.enqueueOrder = enqueueCounter++;
                entry.shownSelections = 0;
                guaranteedQueue.Add(entry);
            }
        }
    }

    private static bool IsGuaranteedGrade(in CardOfferContext _ctx, CardDefinitionSO _card)
    {
        ECardDisplayGrade grade = CardRarity.GetDisplayGrade(_card, _ctx.build.GetLevel(_card) + 1);
        return ECardDisplayGrade.Prism == grade || ECardDisplayGrade.Emerald == grade;
    }

    private bool IsQueued(CardDefinitionSO _card)
    {
        for (int i = 0; i < guaranteedQueue.Count; i++)
        {
            if (guaranteedQueue[i].card == _card) return true;
        }

        return false;
    }

    private GuaranteedEntry RentEntry()
    {
        if (0 == entryPool.Count) return new GuaranteedEntry();

        GuaranteedEntry entry = entryPool[entryPool.Count - 1];
        entryPool.RemoveAt(entryPool.Count - 1);
        return entry;
    }

    /// <summary>프리즘 → 에메랄드, 같은 종류 안에서는 덜 노출된 카드 → 먼저 쌓인 카드 순.</summary>
    private static int CompareGuaranteed(GuaranteedEntry _a, GuaranteedEntry _b)
    {
        if (_a.bPrism != _b.bPrism) return _a.bPrism ? -1 : 1;
        if (_a.shownSelections != _b.shownSelections) return _a.shownSelections.CompareTo(_b.shownSelections);
        return _a.enqueueOrder.CompareTo(_b.enqueueOrder);
    }

    // // 2. 공통 패시브 보장

    private void FillGuaranteedPassives(in CardOfferContext _ctx, int _slots, List<CardOffer> _results)
    {
        // 첫 선택은 액티브 7종 중에서만 제시한다(기획서 v2.5 4-1). 패시브 보장은 그다음 선택부터.
        if (_ctx.bFirstPick) return;

        int target = Mathf.Min(_results.Count + Mathf.Max(0, _ctx.balance.guaranteedPassiveCount), _slots);
        while (_results.Count < target)
        {
            int pick = PickWeighted(in _ctx, true);
            if (pick < 0) break;

            CardDefinitionSO card = candidates[pick];
            candidates.RemoveAt(pick);
            _results.Add(CreateOffer(in _ctx, card, false));
        }
    }

    // // 3. 파트너 확정 노출

    private void FillForcedPartner(in CardOfferContext _ctx, int _slots, List<CardOffer> _results)
    {
        if (false == _ctx.balance.forcePartnerWhenMaterialMax || _results.Count >= _slots) return;

        // 이미 제시에 파트너가 들어가 있으면 한 선택 1장 조건을 채웠다.
        for (int i = 0; i < _results.Count; i++)
        {
            if (IsForcedPartner(in _ctx, _results[i].card)) return;
        }

        forcedPartnerCandidates.Clear();

        for (int i = 0; i < candidates.Count; i++)
        {
            CardDefinitionSO card = candidates[i];
            if (IsForcedPartner(in _ctx, card)) forcedPartnerCandidates.Add(card);
        }

        if (0 == forcedPartnerCandidates.Count) return;

        CardDefinitionSO chosen = forcedPartnerCandidates[rng.Next(forcedPartnerCandidates.Count)];
        candidates.Remove(chosen);
        _results.Add(CreateOffer(in _ctx, chosen, false));
    }

    /// <summary>_card와 짝을 이루는 조합 재료가 이미 MAX인지(그리고 그 조합을 아직 고르지 않았는지).</summary>
    private static bool IsForcedPartner(in CardOfferContext _ctx, CardDefinitionSO _card)
    {
        if (null == _card || ECardKind.Prism == _card.kind) return false;

        CardDatabaseSO db = _ctx.database;
        for (int i = 0; i < db.Count; i++)
        {
            CardDefinitionSO prism = db.Get(i);
            // 이미 고른 조합, 배타 그룹으로 막힌 조합(더는 얻을 수 없다)은 파트너를 유도하지 않는다.
            if (null == prism || ECardKind.Prism != prism.kind || _ctx.build.IsOwned(prism) || _ctx.build.IsExcluded(prism)) continue;
            if (false == prism.IsComboMaterial(_card)) continue;

            CardDefinitionSO other = prism.comboMaterialA == _card ? prism.comboMaterialB : prism.comboMaterialA;
            if (null != other && _ctx.build.IsMax(other)) return true;
        }

        return false;
    }

    // // 4. 가중치 랜덤

    private void FillWeighted(in CardOfferContext _ctx, int _slots, List<CardOffer> _results)
    {
        while (_results.Count < _slots)
        {
            int pick = PickWeighted(in _ctx, false);
            if (pick < 0) break;

            CardDefinitionSO card = candidates[pick];
            candidates.RemoveAt(pick);
            _results.Add(CreateOffer(in _ctx, card, false));
        }
    }

    /// <summary>후보 중 하나를 가중치로 뽑아 인덱스를 돌려준다. _bPassiveOnly면 공통 패시브만 뽑는다. 없으면 -1.</summary>
    private int PickWeighted(in CardOfferContext _ctx, bool _bPassiveOnly)
    {
        float total = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (_bPassiveOnly && ECardKind.Passive != candidates[i].kind) continue;
            total += GetWeight(in _ctx, candidates[i]);
        }

        if (total <= 0f) return -1;

        float roll = (float)(rng.NextDouble() * total);
        float cumulative = 0f;
        int last = -1;

        for (int i = 0; i < candidates.Count; i++)
        {
            if (_bPassiveOnly && ECardKind.Passive != candidates[i].kind) continue;

            float weight = GetWeight(in _ctx, candidates[i]);
            if (weight <= 0f) continue;

            last = i;
            cumulative += weight;
            if (roll < cumulative) return i;
        }

        // 부동소수 누적 오차로 끝까지 못 걸렸으면 마지막 후보.
        return last;
    }

    /// <summary>최종 가중치 = 기본 × 종류 비중 × 업그레이드 우선 × 파트너 유도, 편향 상한 적용(부록 A-1 "노출 가중치").</summary>
    private static float GetWeight(in CardOfferContext _ctx, CardDefinitionSO _card)
    {
        RoguelikeBalanceSO balance = _ctx.balance;
        float baseWeight = Mathf.Max(0.0001f, balance.baseWeight);
        float weight = baseWeight * balance.GetKindWeight(_card.kind);

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
            // 이미 고른 조합, 배타 그룹으로 막힌 조합(더는 얻을 수 없다)은 파트너를 유도하지 않는다.
            if (null == prism || ECardKind.Prism != prism.kind || _ctx.build.IsOwned(prism) || _ctx.build.IsExcluded(prism)) continue;
            if (false == prism.IsComboMaterial(_card)) continue;

            CardDefinitionSO other = prism.comboMaterialA == _card ? prism.comboMaterialB : prism.comboMaterialA;
            if (null != other && _ctx.build.GetLevel(other) >= startLevel) return true;
        }

        return false;
    }

    // // 리롤: 진짜 랜덤

    /// <summary>제외만 거친 후보에서 가중치·보장 없이 균등하게 뽑는다(기획서 v2.5 4-2 "리롤").</summary>
    private void FillUniformRandom(in CardOfferContext _ctx, int _slots, List<CardOffer> _results)
    {
        while (_results.Count < _slots && candidates.Count > 0)
        {
            int pick = rng.Next(candidates.Count);
            CardDefinitionSO card = candidates[pick];
            candidates.RemoveAt(pick);
            _results.Add(CreateOffer(in _ctx, card, false));
        }
    }

    // // 공통

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
