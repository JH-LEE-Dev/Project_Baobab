using System.Collections.Generic;

/// <summary>
/// 메인메뉴(로그라이크 씬 밖)에서 저장된 로그라이크 기록을 카드 도감 칸 데이터로 바꾼다(RoguelikeRecordViewBuilder와 같은 위치 · 방식).
/// 저장 파일은 읽기만 한다. 카드 이름 · 아이콘 · 종류는 카드 데이터베이스(CardDatabaseSO)에서 찾는다.
///
/// 발견 판정(임시, G-10 Application Layer 필드가 들어오기 전): challengeMaxedCardIds ∪ challengeRubyCardIds ∪ challengePrismCardIds
///   ∪ recent.cards(bHasRecent일 때) ∪ clearRanking[].cards ∪ unlimitRanking[].cards.
/// MAX = challengeMaxedCardIds 포함. NEW = 항상 false(G-10 전, 에디터 시험 토글은 UIView_RoguelikeCollection이 가진다).
/// G-10 필드(collectionCardIds · collectionSeenCardIds)가 들어오면 이 파일의 발견 · NEW 판정만 새 필드로 바꾼다.
/// </summary>
public static class RoguelikeCollectionViewBuilder
{
    private const int InitialIdCapacity = 64;

    // 분류 순서: 액티브 → 패시브 → 프리즘 → 루비.
    private static readonly ECardKind[] KindOrder = { ECardKind.Active, ECardKind.Passive, ECardKind.Prism, ECardKind.Ruby };

    // 매번 새로 만들지 않도록 재사용한다(열 때마다 비우고 채운다).
    private static readonly HashSet<string> foundIds = new HashSet<string>(InitialIdCapacity);
    private static readonly HashSet<string> maxedIds = new HashSet<string>(InitialIdCapacity);

    /// <summary>
    /// _order(도감 순서 cardId 목록)의 카드를 분류(액티브 → 패시브 → 프리즘 → 루비)별로 묶어 _output에 채운다. _output은 호출한 쪽이 재사용한다.
    /// 카드 데이터베이스에 없는 카드(삭제된 카드)는 건너뛴다.
    /// </summary>
    public static void Build(RoguelikeRecordData _records, CardDatabaseSO _database, IReadOnlyList<string> _order, List<RoguelikeCollectionViewData> _output)
    {
        _output.Clear();
        if (null == _database || null == _order) return;

        CollectFoundIds(_records);

        for (int k = 0; KindOrder.Length > k; k++)
        {
            for (int i = 0; _order.Count > i; i++)
            {
                CardDefinitionSO card = _database.FindById(_order[i]);
                if (null == card || KindOrder[k] != card.kind) continue;

                bool bMaxed = maxedIds.Contains(card.cardId);
                _output.Add(new RoguelikeCollectionViewData
                {
                    cardId = card.cardId,
                    nameKey = card.nameKey,
                    descKey = card.descriptionKey,
                    icon = card.icon,
                    kind = card.kind,
                    bFound = foundIds.Contains(card.cardId) || bMaxed,
                    bMaxed = bMaxed,
                    bNew = false,
                });
            }
        }
    }

    private static void CollectFoundIds(RoguelikeRecordData _records)
    {
        foundIds.Clear();
        maxedIds.Clear();
        if (null == _records) return;

        AddIds(_records.challengeMaxedCardIds, maxedIds);
        AddIds(_records.challengeMaxedCardIds, foundIds);
        AddIds(_records.challengeRubyCardIds, foundIds);
        AddIds(_records.challengePrismCardIds, foundIds);

        if (true == _records.bHasRecent) AddEntryCards(_records.recent);
        for (int i = 0; null != _records.clearRanking && _records.clearRanking.Count > i; i++) AddEntryCards(_records.clearRanking[i]);
        for (int i = 0; null != _records.unlimitRanking && _records.unlimitRanking.Count > i; i++) AddEntryCards(_records.unlimitRanking[i]);
    }

    private static void AddIds(List<string> _source, HashSet<string> _target)
    {
        if (null == _source) return;

        for (int i = 0; _source.Count > i; i++)
        {
            if (false == string.IsNullOrEmpty(_source[i])) _target.Add(_source[i]);
        }
    }

    private static void AddEntryCards(RunRecordEntry _entry)
    {
        if (null == _entry || null == _entry.cards) return;

        for (int i = 0; _entry.cards.Count > i; i++)
        {
            string cardId = _entry.cards[i].cardId;
            if (false == string.IsNullOrEmpty(cardId)) foundIds.Add(cardId);
        }
    }
}
