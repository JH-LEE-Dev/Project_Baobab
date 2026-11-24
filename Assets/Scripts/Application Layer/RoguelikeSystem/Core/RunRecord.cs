using System;
using System.Collections.Generic;

/// <summary>기록에 남는 카드 한 장. 카드 에셋이 아니라 cardId로 저장해 에셋이 바뀌어도 기록이 깨지지 않게 한다.</summary>
[Serializable]
public struct RunRecordCard
{
    public string cardId;
    /// <summary>판이 끝났을 때의 레벨. 계약·프리즘은 1.</summary>
    public int level;

    public RunRecordCard(string _cardId, int _level)
    {
        cardId = _cardId;
        level = _level;
    }
}

/// <summary>
/// 한 판의 기록. 기획서 3-6 "세 탭 공통 표시 정보"와 3-7 "결과 화면 항목"을 모두 담는다.
/// JsonUtility로 직렬화되므로 필드 이름을 바꾸면 기존 기록이 읽히지 않는다.
/// </summary>
[Serializable]
public class RunRecordEntry
{
    /// <summary>기록 시각(UTC, DateTime.Ticks).</summary>
    public long recordedAtUtcTicks;

    /// <summary>
    /// 일반 구간(1~30) 기준 도달 웨이브. 게임오버면 그때 싸우던 웨이브, 포기면 마지막으로 클리어한 웨이브.
    /// 30웨이브를 클리어한 판은 30.
    /// </summary>
    public int reachedWave;
    /// <summary>30웨이브를 클리어했는지(= 언리밋에 진입했는지).</summary>
    public bool bCleared;
    /// <summary>언리밋 도달 번호(Unlimit-N의 N). 언리밋에 못 갔으면 0.</summary>
    public int reachedUnlimitWave;

    /// <summary>그 판의 총 플레이 시간(초).</summary>
    public float playTimeSeconds;
    /// <summary>30웨이브를 클리어한 시점까지의 시간(초). 클리어하지 못했으면 0. 클리어 랭킹 비교용(기획서 3-6).</summary>
    public float clearTimeSeconds;

    public bool bAbandoned;

    public int treesFelled;

    /// <summary>고른 카드 + 해금한 히든(프리즘) 카드. 처음 고른 순서.</summary>
    public List<RunRecordCard> cards = new List<RunRecordCard>();

    /// <summary>클리어 랭킹 비교 시간. 클리어한 판은 클리어 시점, 아니면 플레이 시간(기획서 3-6).</summary>
    public float ClearRankingTime => bCleared ? clearTimeSeconds : playTimeSeconds;

    public RunRecordEntry Clone()
    {
        RunRecordEntry copy = (RunRecordEntry)MemberwiseClone();
        copy.cards = null == cards ? new List<RunRecordCard>() : new List<RunRecordCard>(cards);
        return copy;
    }
}

/// <summary>로그라이크 기록 파일 전체.</summary>
[Serializable]
public class RoguelikeRecordData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;

    /// <summary>가장 최근 1판(클리어 여부·포기 여부 무관).</summary>
    public RunRecordEntry recent;
    /// <summary>JsonUtility는 null 객체 필드를 기본값으로 채워 버리므로, 최근 기록 유무를 따로 둔다.</summary>
    public bool bHasRecent;

    /// <summary>클리어 랭킹 1~5위. 클리어한 판과 게임오버된 판(포기 제외), 일반 구간 기준.</summary>
    public List<RunRecordEntry> clearRanking = new List<RunRecordEntry>();
    /// <summary>언리밋 랭킹 1~5위. 언리밋에 도달한 판만.</summary>
    public List<RunRecordEntry> unlimitRanking = new List<RunRecordEntry>();

    // // 도전과제 누적 진행(기획서 v3.1 8-3). 여러 판에 걸쳐 쌓이고, 한 번 달성한 도전과제는 취소되지 않는다.
    // JsonUtility는 HashSet · Dictionary를 저장하지 못해 List와 int로 둔다. 이전 기록 파일에는 없는 필드라 EnsureValid가 채운다.

    /// <summary>달성한 도전과제 ID(RoguelikeChallenges 정의의 id).</summary>
    public List<string> challengeClearedIds = new List<string>();
    /// <summary>한 번이라도 MAX를 찍은 레벨 있는 카드(액티브 · 공통 패시브)의 cardId.</summary>
    public List<string> challengeMaxedCardIds = new List<string>();
    /// <summary>한 번이라도 고른 루비(계약) 카드의 cardId.</summary>
    public List<string> challengeRubyCardIds = new List<string>();
    /// <summary>한 번이라도 얻은 프리즘(조합) 카드의 cardId.</summary>
    public List<string> challengePrismCardIds = new List<string>();
    /// <summary>여러 판에 걸쳐 쓰러뜨린 나무 수.</summary>
    public int challengeTreesFelled;
    /// <summary>도달한 가장 높은 티어 번호(1부터, 0은 기록 없음).</summary>
    public int challengeBestTier;
    /// <summary>도달한 가장 높은 언리밋 번호(Unlimit-N의 N, 0은 기록 없음).</summary>
    public int challengeBestUnlimit;

    // // 카드 도감(기획서 v3.3 8-4, 부록 C-3 G-10). 도전과제 필드와 같이 없는 필드는 EnsureValid가 채우므로 기록 파일 버전은 올리지 않는다.

    /// <summary>한 번이라도 고른 카드의 cardId(레벨 · 종류와 무관). 도감 칸의 발견 판정에 쓴다.</summary>
    public List<string> collectionCardIds = new List<string>();
    /// <summary>도감에서 앞면으로 보여 준 카드의 cardId(NEW 판정용). collectionCardIds에는 있고 여기에는 없으면 NEW다.</summary>
    public List<string> collectionSeenCardIds = new List<string>();
    /// <summary>최초 도감 완성 축하(E9)를 이미 보여 줬는지.</summary>
    public bool collectionCompleteCelebrated;

    /// <summary>JsonUtility 역직렬화 직후 비어 있을 수 있는 리스트를 채운다.</summary>
    public void EnsureValid()
    {
        if (null == clearRanking) clearRanking = new List<RunRecordEntry>();
        if (null == unlimitRanking) unlimitRanking = new List<RunRecordEntry>();
        if (null == recent) bHasRecent = false;
        if (null == challengeClearedIds) challengeClearedIds = new List<string>();
        if (null == challengeMaxedCardIds) challengeMaxedCardIds = new List<string>();
        if (null == challengeRubyCardIds) challengeRubyCardIds = new List<string>();
        if (null == challengePrismCardIds) challengePrismCardIds = new List<string>();
        if (null == collectionCardIds) collectionCardIds = new List<string>();
        if (null == collectionSeenCardIds) collectionSeenCardIds = new List<string>();
        if (challengeTreesFelled < 0) challengeTreesFelled = 0;
    }

    /// <summary>
    /// 도감 기록이 생기기 전 기록 파일의 소급. collectionCardIds가 비어 있을 때만, 도전과제 목록 · 최근 기록 · 랭킹의 카드 중
    /// _collectionTargets(도감 정본 목록)에 있는 것을 중복 없이 넣고 본 카드(collectionSeenCardIds)에도 넣는다(옛 카드에 NEW가 무더기로 붙지 않게).
    /// 이 폴더(Baobab.Roguelike)는 도감 정본 목록(RoguelikeCollection)을 볼 수 없어 목록을 인자로 받는다. EnsureValid 뒤에 부른다.
    /// 무언가 채웠으면 true.
    /// </summary>
    public bool BackfillCollection(IReadOnlyList<string> _collectionTargets)
    {
        if (null == _collectionTargets || 0 < collectionCardIds.Count) return false;

        BackfillFrom(challengeMaxedCardIds, _collectionTargets);
        BackfillFrom(challengeRubyCardIds, _collectionTargets);
        BackfillFrom(challengePrismCardIds, _collectionTargets);
        if (true == bHasRecent) BackfillFromEntry(recent, _collectionTargets);
        for (int i = 0; i < clearRanking.Count; i++) BackfillFromEntry(clearRanking[i], _collectionTargets);
        for (int i = 0; i < unlimitRanking.Count; i++) BackfillFromEntry(unlimitRanking[i], _collectionTargets);

        for (int i = 0; i < collectionCardIds.Count; i++)
        {
            if (false == collectionSeenCardIds.Contains(collectionCardIds[i])) collectionSeenCardIds.Add(collectionCardIds[i]);
        }

        return 0 < collectionCardIds.Count;
    }

    private void BackfillFrom(List<string> _source, IReadOnlyList<string> _targets)
    {
        if (null == _source) return;

        for (int i = 0; i < _source.Count; i++) BackfillCard(_source[i], _targets);
    }

    private void BackfillFromEntry(RunRecordEntry _entry, IReadOnlyList<string> _targets)
    {
        if (null == _entry || null == _entry.cards) return;

        for (int i = 0; i < _entry.cards.Count; i++) BackfillCard(_entry.cards[i].cardId, _targets);
    }

    private void BackfillCard(string _cardId, IReadOnlyList<string> _targets)
    {
        if (string.IsNullOrEmpty(_cardId) || collectionCardIds.Contains(_cardId)) return;

        for (int i = 0; i < _targets.Count; i++)
        {
            if (_targets[i] != _cardId) continue;

            collectionCardIds.Add(_cardId);
            return;
        }
    }
}
