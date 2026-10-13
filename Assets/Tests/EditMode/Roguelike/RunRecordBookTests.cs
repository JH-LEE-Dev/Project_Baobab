using NUnit.Framework;

/// <summary>기록 저장 규칙(기획서 3-6, 3-7).</summary>
public class RunRecordBookTests
{
    private static RunRecordEntry Entry(int _wave, float _time, bool _bCleared = false, int _unlimit = 0, float _clearTime = 0f)
    {
        return new RunRecordEntry
        {
            reachedWave = _wave,
            playTimeSeconds = _time,
            bCleared = _bCleared,
            reachedUnlimitWave = _unlimit,
            clearTimeSeconds = _clearTime,
        };
    }

    [Test]
    public void GameOverBeforeClear_GoesToRecentAndClearRanking()
    {
        RoguelikeRecordData data = new RoguelikeRecordData();
        RunRecordBook.SubmitRunEnd(data, Entry(12, 300f), ERunEndReason.GameOver, out int clearRank, out int unlimitRank);

        Assert.IsTrue(data.bHasRecent);
        Assert.AreEqual(12, data.recent.reachedWave);
        Assert.AreEqual(0, clearRank);
        Assert.AreEqual(-1, unlimitRank);
        Assert.AreEqual(1, data.clearRanking.Count);
    }

    [Test]
    public void AbandonBeforeClear_OnlyRecent()
    {
        RoguelikeRecordData data = new RoguelikeRecordData();
        RunRecordBook.SubmitRunEnd(data, Entry(5, 100f), ERunEndReason.Abandoned, out int clearRank, out _);

        Assert.IsTrue(data.bHasRecent);
        Assert.AreEqual(-1, clearRank);
        Assert.AreEqual(0, data.clearRanking.Count);
    }

    [Test]
    public void ClearThenUnlimitGameOver_ClearRankingOnce_UnlimitRankingAdded()
    {
        RoguelikeRecordData data = new RoguelikeRecordData();
        RunRecordBook.SubmitClear(data, Entry(30, 900f, true, 0, 900f));
        RunRecordBook.SubmitRunEnd(data, Entry(30, 1200f, true, 4, 900f), ERunEndReason.GameOver, out int clearRank, out int unlimitRank);

        Assert.AreEqual(1, data.clearRanking.Count);
        Assert.AreEqual(-1, clearRank);
        Assert.AreEqual(0, unlimitRank);
        Assert.AreEqual(4, data.unlimitRanking[0].reachedUnlimitWave);
    }

    [Test]
    public void ClearRanking_SortsByWaveThenTime_AndKeepsTopFive()
    {
        RoguelikeRecordData data = new RoguelikeRecordData();
        RunRecordBook.SubmitRunEnd(data, Entry(10, 500f), ERunEndReason.GameOver, out _, out _);
        RunRecordBook.SubmitRunEnd(data, Entry(10, 400f), ERunEndReason.GameOver, out _, out _);
        RunRecordBook.SubmitRunEnd(data, Entry(20, 999f), ERunEndReason.GameOver, out _, out _);
        RunRecordBook.SubmitClear(data, Entry(30, 2000f, true, 0, 800f));
        RunRecordBook.SubmitRunEnd(data, Entry(3, 10f), ERunEndReason.GameOver, out _, out _);
        RunRecordBook.SubmitRunEnd(data, Entry(2, 10f), ERunEndReason.GameOver, out _, out _);
        RunRecordBook.SubmitRunEnd(data, Entry(1, 10f), ERunEndReason.GameOver, out int lastRank, out _);

        Assert.AreEqual(5, data.clearRanking.Count);
        Assert.AreEqual(30, data.clearRanking[0].reachedWave);
        Assert.AreEqual(20, data.clearRanking[1].reachedWave);
        Assert.AreEqual(400f, data.clearRanking[2].playTimeSeconds);
        Assert.AreEqual(500f, data.clearRanking[3].playTimeSeconds);
        Assert.AreEqual(3, data.clearRanking[4].reachedWave);
        Assert.AreEqual(-1, lastRank);
    }
}
