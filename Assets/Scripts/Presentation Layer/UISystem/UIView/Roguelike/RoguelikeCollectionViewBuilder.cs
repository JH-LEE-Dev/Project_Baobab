using System.Collections.Generic;

/// <summary>
/// 메인메뉴(로그라이크 씬 밖)에서 저장된 로그라이크 기록을 카드 도감 칸 데이터로 바꾼다(RoguelikeRecordViewBuilder와 같은 위치 · 방식).
/// 저장 파일은 읽기만 한다. 카드 이름 · 아이콘 · 종류는 카드 데이터베이스(CardDatabaseSO)에서 찾는다.
///
/// 발견 = collectionCardIds(한 번이라도 고른 카드, 기획서 v3.3 8-4). 옛 기록 파일의 소급은 RoguelikeRecordRepository.Load가 이미 해 둔다.
/// MAX = challengeMaxedCardIds 포함.
/// NEW = 발견했고 collectionSeenCardIds(도감에서 앞면으로 보여 준 카드)에 없는 카드.
/// E9(최초 완성 축하) = 모든 칸이 발견 상태이고 collectionCompleteCelebrated가 아직 false.
/// 도감 창을 닫을 때 보여 준 카드 · 축하 여부는 RoguelikeRecordRepository.SaveCollectionSeen으로 저장한다(메인메뉴가 부른다).
/// </summary>
public static class RoguelikeCollectionViewBuilder
{
    private const int InitialIdCapacity = 64;

    // 분류 순서: 액티브 → 패시브 → 프리즘 → 루비.
    private static readonly ECardKind[] KindOrder = { ECardKind.Active, ECardKind.Passive, ECardKind.Prism, ECardKind.Ruby };

    // 매번 새로 만들지 않도록 재사용한다(열 때마다 비우고 채운다).
    private static readonly HashSet<string> foundIds = new HashSet<string>(InitialIdCapacity);
    private static readonly HashSet<string> seenIds = new HashSet<string>(InitialIdCapacity);
    private static readonly HashSet<string> maxedIds = new HashSet<string>(InitialIdCapacity);

    /// <summary>
    /// _order(도감 순서 cardId 목록)의 카드를 분류(액티브 → 패시브 → 프리즘 → 루비)별로 묶어 _output에 채운다. _output은 호출한 쪽이 재사용한다.
    /// 카드 데이터베이스에 없는 카드(삭제된 카드)는 건너뛴다. _bCelebrate = 이번에 최초 완성 축하(E9)를 재생해야 한다.
    /// </summary>
    public static void Build(RoguelikeRecordData _records, CardDatabaseSO _database, IReadOnlyList<string> _order, List<RoguelikeCollectionViewData> _output, out bool _bCelebrate)
    {
        _bCelebrate = false;
        _output.Clear();
        if (null == _database || null == _order) return;

        CollectIds(_records);

        int foundCount = 0;
        for (int k = 0; KindOrder.Length > k; k++)
        {
            for (int i = 0; _order.Count > i; i++)
            {
                CardDefinitionSO card = _database.FindById(_order[i]);
                if (null == card || KindOrder[k] != card.kind) continue;

                bool bFound = foundIds.Contains(card.cardId);
                if (true == bFound) foundCount++;

                _output.Add(new RoguelikeCollectionViewData
                {
                    cardId = card.cardId,
                    nameKey = card.nameKey,
                    descKey = card.descriptionKey,
                    card = card,
                    icon = card.icon,
                    kind = card.kind,
                    bFound = bFound,
                    bMaxed = maxedIds.Contains(card.cardId),
                    bNew = bFound && false == seenIds.Contains(card.cardId),
                });
            }
        }

        bool bAllFound = 0 < _output.Count && _output.Count == _order.Count && foundCount == _output.Count;
        _bCelebrate = bAllFound && null != _records && false == _records.collectionCompleteCelebrated;
    }

    private static void CollectIds(RoguelikeRecordData _records)
    {
        foundIds.Clear();
        seenIds.Clear();
        maxedIds.Clear();
        if (null == _records) return;

        AddIds(_records.collectionCardIds, foundIds);
        AddIds(_records.collectionSeenCardIds, seenIds);
        AddIds(_records.challengeMaxedCardIds, maxedIds);
    }

    private static void AddIds(List<string> _source, HashSet<string> _target)
    {
        if (null == _source) return;

        for (int i = 0; _source.Count > i; i++)
        {
            if (false == string.IsNullOrEmpty(_source[i])) _target.Add(_source[i]);
        }
    }
}
