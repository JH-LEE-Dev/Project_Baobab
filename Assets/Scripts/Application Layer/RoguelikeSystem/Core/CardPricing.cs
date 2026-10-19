using UnityEngine;

/// <summary>
/// 카드 가격·리롤 비용·감정비 계산. 기획서 5-3 "가격 규칙", 4-1 "리롤", 4-4 "카드 감정", 부록 A-6.
///
/// [바겐 세일·VIP 적용 범위] CardPricePercent는 유료 구매 전체(첫 구매·업그레이드)에 곱해지고 소수점은 올린다.
/// 무료 카드(에메랄드·프리즘·첫 선택)와 감정비에는 적용하지 않는다.
/// </summary>
public static class CardPricing
{
    /// <summary>
    /// _card를 _levelAfterPurchase레벨로 만드는 구매의 가격.
    /// 레벨 I 가격 × 레벨 배율 × (1 + 가격 보정% / 100), 소수점 올림. 사면 MAX가 되는 구매와 프리즘은 무료.
    /// </summary>
    public static int GetPrice(CardDefinitionSO _card, int _levelAfterPurchase, RunModifiers _modifiers, RoguelikeBalanceSO _balance)
    {
        int listPrice = GetListPrice(_card, _levelAfterPurchase, _balance);
        if (listPrice <= 0) return 0;

        float percent = null != _modifiers ? _modifiers.Get(ERunEffect.CardPricePercent) : 0f;
        if (Mathf.Approximately(percent, 0f)) return listPrice;

        // 부동소수 오차로 정수 결과가 한 단계 올라가지 않게 아주 작은 값을 빼고 올린다.
        double price = listPrice * System.Math.Max(0.0, 1.0 + percent / 100.0);
        return System.Math.Max(0, (int)System.Math.Ceiling(price - 1e-6));
    }

    /// <summary>루비 가격 보정을 뺀 정가. 감정비가 이 값을 쓴다.</summary>
    public static int GetListPrice(CardDefinitionSO _card, int _levelAfterPurchase, RoguelikeBalanceSO _balance)
    {
        if (null == _card || null == _balance) return 0;
        if (ECardKind.Prism == _card.kind) return 0;

        int maxLevel = _card.EffectiveMaxLevel;
        if (_balance.bMaxLevelFree && maxLevel > 1 && _levelAfterPurchase >= maxLevel) return 0;

        int basePrice = GetBasePrice(_card, _balance);
        return Mathf.Max(0, RoundHalfUp(basePrice * Exact(_balance.GetLevelPriceMultiplier(Mathf.Max(1, _levelAfterPurchase)))));
    }

    /// <summary>레벨 I 가격(기준 가격 P × 등급 비율, 또는 카드별 덮어쓰기).</summary>
    public static int GetBasePrice(CardDefinitionSO _card, RoguelikeBalanceSO _balance)
    {
        if (null == _card || null == _balance) return 0;
        return _card.basePriceOverride > 0 ? _card.basePriceOverride : _balance.GetGradeBasePrice(_card.PriceGrade);
    }

    /// <summary>
    /// 보유 레벨 _currentLevel 카드의 감정비. I~III은 다음 레벨 정가 × 배율, IV는 레벨 I 가격 × 배율(올라갈 곳이 MAX뿐).
    /// 루비 가격 보정은 받지 않는다. 감정 대상이 아니면 0.
    /// </summary>
    public static int GetAppraisalFee(CardDefinitionSO _card, int _currentLevel, RoguelikeBalanceSO _balance)
    {
        if (null == _card || null == _balance || _currentLevel < 1) return 0;

        int maxLevel = _card.EffectiveMaxLevel;
        if (_currentLevel >= maxLevel) return 0;

        if (_currentLevel == maxLevel - 1)
        {
            return RoundHalfUp(GetBasePrice(_card, _balance) * Exact(_balance.appraisalMaxChallengeFeeMultiplier));
        }

        int nextLevelPrice = RoundHalfUp(GetBasePrice(_card, _balance) * Exact(_balance.GetLevelPriceMultiplier(_currentLevel + 1)));
        return RoundHalfUp(nextLevelPrice * Exact(_balance.appraisalNextLevelFeeMultiplier));
    }

    /// <summary>
    /// 지금 리롤하면 드는 비용. _rerollCount는 이번 판에서 이미 한 유료 리롤 횟수.
    /// 첫 선택의 무료 리롤은 RerollFree와 같이 0을 돌려주도록 호출부(CardShop)가 처리한다.
    /// </summary>
    public static int GetRerollCost(int _rerollCount, RunModifiers _modifiers, RoguelikeBalanceSO _balance)
    {
        if (null == _balance) return 0;
        if (null != _modifiers && _modifiers.IsOn(ERunEffect.RerollFree)) return 0;

        double cost;
        if (RoguelikeBalanceSO.ERerollCostMode.Multiply == _balance.rerollCostMode)
        {
            cost = _balance.rerollBaseCost * System.Math.Pow(Exact(Mathf.Max(1f, _balance.rerollCostMultiplier)), _rerollCount);
        }
        else
        {
            cost = _balance.rerollBaseCost + (double)_balance.rerollCostStep * _rerollCount;
        }

        int result = Mathf.Max(0, RoundHalfUp(cost));
        if (_balance.rerollCostCap > 0) result = Mathf.Min(result, _balance.rerollCostCap);

        return result;
    }

    /// <summary>리롤 금지 효과(바겐 세일)가 없는지. 첫 선택의 무료 리롤 횟수는 CardShop이 따로 본다.</summary>
    public static bool CanReroll(RunModifiers _modifiers)
    {
        return null == _modifiers || false == _modifiers.IsOn(ERunEffect.RerollDisabled);
    }

    /// <summary>카드 UI가 그릴 표시 등급. 사면 MAX가 되는 카드는 에메랄드(기획서 5-3 "카드 등급").</summary>
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

    /// <summary>
    /// 레어 카드인지(기획서 v2.2 10-2 "레어의 기준"): MAX(에메랄드)로 완성되는 카드, 프리즘, 루비. 레벨 I~IV는 실버·골드여도 일반이다.
    /// 레어 등장 연출과 보관함의 쇼케이스(아크릴 케이스) / 바인더(비닐 슬리브)를 가른다.
    /// </summary>
    public static bool IsRare(ECardDisplayGrade _displayGrade)
    {
        return ECardDisplayGrade.Emerald == _displayGrade || ECardDisplayGrade.Prism == _displayGrade || ECardDisplayGrade.Ruby == _displayGrade;
    }

    /// <summary>
    /// 인스펙터에 적힌 배율(1.15 등)을 그 십진값 그대로 double로 옮긴다. 1.15f는 실제로 1.1499999…라
    /// 그대로 곱하면 390 × 1.15 = 448.5가 448.49999…가 되어 반올림이 한 칸 내려간다(감정비 표 449).
    /// </summary>
    private static double Exact(float _value)
    {
        return (double)(decimal)_value;
    }

    /// <summary>0.5를 올리는 반올림. 448.5 같은 값이 짝수 쪽(448)으로 내려가지 않게 하고, 배율의 부동소수 오차를 흡수한다.</summary>
    private static int RoundHalfUp(double _value)
    {
        return (int)System.Math.Floor(_value + 0.5 + 1e-6);
    }
}
