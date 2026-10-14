using System.Collections.Generic;

/// <summary>
/// 기록 저장 규칙. 기획서 3-6 "런 기록"과 3-7 "상황별 기록 저장" 표를 그대로 옮겼다.
///
///   상황                 | 최근 기록 | 클리어 랭킹                     | 언리밋 랭킹
///   30웨이브 전 게임오버  | O         | O (도달 웨이브, 게임오버까지 시간) | -
///   30웨이브 클리어      | -         | O (클리어 직후 즉시, 클리어 시간)  | -
///   언리밋 중 게임오버    | O         | - (클리어 때 이미 저장)           | O
///   언리밋 중 포기       | O         | -                               | O (마지막으로 클리어한 Unlimit 웨이브)
///   30웨이브 전 포기     | O         | - (포기한 판은 제외)              | -
///
/// 파일 읽기/쓰기는 하지 않는다. RoguelikeRecordRepository가 이 결과를 저장한다.
/// </summary>
public static class RunRecordBook
{
    public const int RankingSize = 5;

    /// <summary>30웨이브를 클리어한 순간. 클리어 랭킹에 바로 넣는다. 들어간 순위(0부터)를 돌려주며, 밖이면 -1.</summary>
    public static int SubmitClear(RoguelikeRecordData _data, RunRecordEntry _clearSnapshot)
    {
        _data.EnsureValid();
        return InsertRanked(_data.clearRanking, _clearSnapshot.Clone(), CompareClear);
    }

    /// <summary>
    /// 판이 끝난 순간(게임오버 또는 포기). 최근 기록은 항상 갱신하고, 표의 규칙대로 랭킹에 넣는다.
    /// _clearRank/_unlimitRank는 이번 호출로 들어간 순위(0부터), 넣지 않았거나 밖이면 -1.
    /// </summary>
    public static void SubmitRunEnd(RoguelikeRecordData _data, RunRecordEntry _entry, ERunEndReason _reason,
        out int _clearRank, out int _unlimitRank)
    {
        _data.EnsureValid();
        _clearRank = -1;
        _unlimitRank = -1;

        _data.recent = _entry.Clone();
        _data.bHasRecent = true;

        if (_entry.bCleared)
        {
            // 클리어 랭킹은 클리어 순간 SubmitClear가 이미 넣었다. 여기서 또 넣으면 같은 판이 두 줄이 된다.
            _unlimitRank = InsertRanked(_data.unlimitRanking, _entry.Clone(), CompareUnlimit);
            return;
        }

        if (ERunEndReason.GameOver == _reason)
        {
            _clearRank = InsertRanked(_data.clearRanking, _entry.Clone(), CompareClear);
        }
    }

    /// <summary>클리어 랭킹 정렬: 도달 웨이브 높은 순 → 같으면 시간 짧은 순.</summary>
    public static int CompareClear(RunRecordEntry _a, RunRecordEntry _b)
    {
        if (_a.reachedWave != _b.reachedWave) return _b.reachedWave.CompareTo(_a.reachedWave);
        return _a.ClearRankingTime.CompareTo(_b.ClearRankingTime);
    }

    /// <summary>언리밋 랭킹 정렬: 도달 Unlimit 웨이브 높은 순 → 같으면 시간 짧은 순.</summary>
    public static int CompareUnlimit(RunRecordEntry _a, RunRecordEntry _b)
    {
        if (_a.reachedUnlimitWave != _b.reachedUnlimitWave) return _b.reachedUnlimitWave.CompareTo(_a.reachedUnlimitWave);
        return _a.playTimeSeconds.CompareTo(_b.playTimeSeconds);
    }

    /// <summary>정렬 위치에 넣고 RankingSize를 넘는 꼬리를 자른다. 같은 성적이면 먼저 들어온 기록이 위에 남는다.</summary>
    private static int InsertRanked(List<RunRecordEntry> _ranking, RunRecordEntry _entry, System.Comparison<RunRecordEntry> _compare)
    {
        int insertAt = _ranking.Count;
        for (int i = 0; i < _ranking.Count; i++)
        {
            if (_compare(_entry, _ranking[i]) < 0)
            {
                insertAt = i;
                break;
            }
        }

        if (insertAt >= RankingSize) return -1;

        _ranking.Insert(insertAt, _entry);

        while (_ranking.Count > RankingSize)
        {
            _ranking.RemoveAt(_ranking.Count - 1);
        }

        return insertAt;
    }
}
