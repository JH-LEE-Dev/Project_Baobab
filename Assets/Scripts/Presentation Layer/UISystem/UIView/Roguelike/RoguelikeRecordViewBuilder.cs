using System.Collections.Generic;

/// <summary>
/// 메인메뉴(로그라이크 씬 밖)에서 저장된 로그라이크 기록을 기록 창용 표시 데이터로 바꾼다.
/// 로그라이크 씬 안에서는 RoguelikeUICoordinator가 같은 일을 한다(카드 → 보유 카드 표시 데이터 규칙이 같다: ToOwnedView).
/// 저장 파일은 읽기만 한다(RoguelikeRecordRepository.Load). 카드 아이콘은 카드 데이터베이스(CardDatabaseSO)에서 찾는다.
/// </summary>
public static class RoguelikeRecordViewBuilder
{
    /// <summary>기록 창 세 탭 데이터를 채운다. 리스트는 호출한 쪽이 재사용한다.</summary>
    public static void Build(RoguelikeRecordData _records, CardDatabaseSO _database, out RoguelikeRecordViewData _recent,
        List<RoguelikeRecordViewData> _clearRanking, List<RoguelikeRecordViewData> _unlimitRanking)
    {
        _recent = default;
        _clearRanking.Clear();
        _unlimitRanking.Clear();

        if (null == _records) return;

        if (true == _records.bHasRecent) _recent = ToRecordView(_records.recent, _database);
        for (int i = 0; _records.clearRanking.Count > i; i++) _clearRanking.Add(ToRecordView(_records.clearRanking[i], _database));
        for (int i = 0; _records.unlimitRanking.Count > i; i++) _unlimitRanking.Add(ToRecordView(_records.unlimitRanking[i], _database));
    }

    /// <summary>최고 도달 웨이브(클리어 랭킹 · 최근 기록 중 가장 높은 값). 기록이 없으면 0.</summary>
    public static int GetBestWave(RoguelikeRecordData _records)
    {
        int best = 0;
        if (null == _records) return best;

        for (int i = 0; _records.clearRanking.Count > i; i++)
        {
            RunRecordEntry entry = _records.clearRanking[i];
            if (null != entry && best < entry.reachedWave) best = entry.reachedWave;
        }

        if (true == _records.bHasRecent && null != _records.recent && best < _records.recent.reachedWave) best = _records.recent.reachedWave;
        return best;
    }

    private static RoguelikeRecordViewData ToRecordView(RunRecordEntry _entry, CardDatabaseSO _database)
    {
        if (null == _entry) return default;

        int count = null != _entry.cards ? _entry.cards.Count : 0;
        List<RoguelikeOwnedCardViewData> cards = new List<RoguelikeOwnedCardViewData>(count);

        for (int i = 0; count > i; i++)
        {
            RunRecordCard recordCard = _entry.cards[i];
            CardDefinitionSO card = null != _database ? _database.FindById(recordCard.cardId) : null;

            if (null != card) cards.Add(ToOwnedView(card, recordCard.level));
            else cards.Add(new RoguelikeOwnedCardViewData { cardId = recordCard.cardId, level = recordCard.level });
        }

        return new RoguelikeRecordViewData
        {
            bValid = true,
            reachedWave = _entry.reachedWave,
            bCleared = _entry.bCleared,
            unlimitWave = _entry.reachedUnlimitWave,
            playTimeSeconds = _entry.playTimeSeconds,
            rankingTimeSeconds = _entry.ClearRankingTime,
            bAbandoned = _entry.bAbandoned,
            recordedAtUtcTicks = _entry.recordedAtUtcTicks,
            treesFelled = _entry.treesFelled,
            cards = cards,
        };
    }

    private static RoguelikeOwnedCardViewData ToOwnedView(CardDefinitionSO _card, int _level)
    {
        return new RoguelikeOwnedCardViewData
        {
            cardId = _card.cardId,
            nameKey = _card.nameKey,
            descriptionKey = _card.descriptionKey,
            card = _card,
            icon = _card.icon,
            kind = _card.kind,
            displayGrade = CardRarity.GetDisplayGrade(_card, _level),
            level = _level,
            maxLevel = _card.EffectiveMaxLevel,
            bRuby = ECardKind.Ruby == _card.kind,
        };
    }
}
