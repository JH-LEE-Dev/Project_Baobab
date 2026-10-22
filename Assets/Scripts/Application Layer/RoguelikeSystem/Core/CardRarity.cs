/// <summary>
/// 카드의 표시 프레임과 레어 여부. 기획서 v2.4 5-3 "카드 분류와 레어".
/// v2.4에서 골드·가격·등급이 없어져, 카드는 종류(액티브 · 공통 패시브 · 조합 · 계약)와 MAX 여부로만 구분한다.
/// </summary>
public static class CardRarity
{
    /// <summary>
    /// 카드 UI가 그릴 프레임. 프리즘 → Prism, 계약 → Ruby, 고르면 MAX가 되는 카드 → Emerald, 그 밖의 일반 카드 → Bronze(일반 프레임).
    /// </summary>
    public static ECardDisplayGrade GetDisplayGrade(CardDefinitionSO _card, int _levelAfterPick)
    {
        if (null == _card) return ECardDisplayGrade.Bronze;
        if (ECardKind.Prism == _card.kind) return ECardDisplayGrade.Prism;
        if (ECardKind.Ruby == _card.kind) return ECardDisplayGrade.Ruby;

        int maxLevel = _card.EffectiveMaxLevel;
        if (maxLevel > 1 && _levelAfterPick >= maxLevel) return ECardDisplayGrade.Emerald;

        return ECardDisplayGrade.Bronze;
    }

    /// <summary>
    /// 레어 카드인지(기획서 v2.4 5-3 "레어 / 일반"): MAX(에메랄드)로 완성되는 카드, 프리즘, 계약. 나머지는 일반이다.
    /// 레어 등장 연출과 보관함의 쇼케이스(아크릴 케이스) / 바인더(비닐 슬리브)를 가른다.
    /// </summary>
    public static bool IsRare(ECardDisplayGrade _displayGrade)
    {
        return ECardDisplayGrade.Emerald == _displayGrade || ECardDisplayGrade.Prism == _displayGrade || ECardDisplayGrade.Ruby == _displayGrade;
    }
}
