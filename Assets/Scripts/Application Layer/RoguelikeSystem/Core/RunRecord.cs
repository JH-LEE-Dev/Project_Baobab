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

    /// <summary>JsonUtility 역직렬화 직후 비어 있을 수 있는 리스트를 채운다.</summary>
    public void EnsureValid()
    {
        if (null == clearRanking) clearRanking = new List<RunRecordEntry>();
        if (null == unlimitRanking) unlimitRanking = new List<RunRecordEntry>();
        if (null == recent) bHasRecent = false;
    }
}
