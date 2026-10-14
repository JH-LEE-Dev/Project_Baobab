using UnityEngine;

/// <summary>
/// 카드 가격과 리롤 비용 계산. 기획서 3-3 "가격 규칙", "리롤".
///
/// [바겐 세일 적용 범위] 기획서 미정 항목("반값이 업그레이드 가격과 프리즘에도 적용되는지")은
/// 현재 "모든 구매에 적용"으로 구현했다. CardPricePercent가 첫 구매·업그레이드·프리즘 가격 전체에 곱해진다.
/// </summary>
public static class CardPricing
{
    /// <summary>
    /// _card를 _levelAfterPurchase레벨로 만드는 구매의 가격.
    /// 첫 구매가 × (1 + step × (구매 후 레벨 - 1)) × (1 + 가격 보정% / 100).
    /// </summary>
    public static int GetPrice(CardDefinitionSO _card, int _levelAfterPurchase, RunModifiers _modifiers, RoguelikeBalanceSO _balance)
    {
        if (null == _card || null == _balance) return 0;

        int basePrice = _card.basePriceOverride > 0 ? _card.basePriceOverride : _balance.GetGradeBasePrice(_card.PriceGrade);
        float price = basePrice * (1f + _balance.upgradePriceStep * Mathf.Max(0, _levelAfterPurchase - 1));

        if (null != _modifiers)
        {
            price *= Mathf.Max(0f, 1f + _modifiers.Get(ERunEffect.CardPricePercent) / 100f);
        }

        return Mathf.Max(0, Mathf.RoundToInt(price));
    }

    /// <summary>지금 리롤하면 드는 비용. _rerollCount는 이번 판에서 이미 한 리롤 횟수.</summary>
    public static int GetRerollCost(int _rerollCount, RunModifiers _modifiers, RoguelikeBalanceSO _balance)
    {
        if (null == _balance) return 0;
        if (null != _modifiers && _modifiers.IsOn(ERunEffect.RerollFree)) return 0;

        float cost;
        if (RoguelikeBalanceSO.ERerollCostMode.Multiply == _balance.rerollCostMode)
        {
            cost = _balance.rerollBaseCost * Mathf.Pow(Mathf.Max(1f, _balance.rerollCostMultiplier), _rerollCount);
        }
        else
        {
            cost = _balance.rerollBaseCost + (float)_balance.rerollCostStep * _rerollCount;
        }

        int result = Mathf.Max(0, Mathf.RoundToInt(cost));
        if (_balance.rerollCostCap > 0) result = Mathf.Min(result, _balance.rerollCostCap);

        return result;
    }

    /// <summary>리롤 자체가 허용되는지. 첫 무료 선택과 리롤 금지 효과(바겐 세일)에서는 불가.</summary>
    public static bool CanReroll(bool _bFirstPick, RunModifiers _modifiers)
    {
        if (_bFirstPick) return false;
        if (null != _modifiers && _modifiers.IsOn(ERunEffect.RerollDisabled)) return false;
        return true;
    }

    /// <summary>카드 UI가 그릴 표시 등급. 사면 MAX가 되는 카드는 에메랄드(기획서 3-3 "카드 등급").</summary>
    public static ECardDisplayGrade GetDisplayGrade(CardDefinitionSO _card, int _levelAfterPurchase)
    {
        if (ECardKind.Prism == _card.kind) return ECardDisplayGrade.Prism;
        if (ECardKind.Ruby == _card.kind || ECardGrade.Ruby == _card.grade) return ECardDisplayGrade.Ruby;

        int maxLevel = _card.EffectiveMaxLevel;
        if (maxLevel > 1 && _levelAfterPurchase >= maxLevel) return ECardDisplayGrade.Emerald;

        switch (_card.grade)
        {
            case ECardGrade.Silver: return ECardDisplayGrade.Silver;
            case ECardGrade.Gold: return ECardDisplayGrade.Gold;
            default: return ECardDisplayGrade.Bronze;
        }
    }
}
