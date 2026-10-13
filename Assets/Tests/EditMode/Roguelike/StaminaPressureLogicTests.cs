using NUnit.Framework;

/// <summary>기획서 3-2 "피해 판정 흐름" 표의 각 줄을 검증한다.</summary>
public class StaminaPressureLogicTests
{
    private StaminaPressureLogic logic;

    [SetUp]
    public void SetUp()
    {
        logic = new StaminaPressureLogic();
        logic.Configure(40, 5f, 3f);
    }

    [Test]
    public void BelowThreshold_StaysSafe_NoDamage()
    {
        Assert.AreEqual(0, logic.Tick(10f, 39));
        Assert.AreEqual(EPressureState.Safe, logic.State);
    }

    [Test]
    public void AtThreshold_EntersGrace_FirstHitAfterGraceTime()
    {
        Assert.AreEqual(0, logic.Tick(4.5f, 40));
        Assert.AreEqual(EPressureState.Grace, logic.State);

        Assert.AreEqual(1, logic.Tick(0.5f, 40));
        Assert.AreEqual(EPressureState.Damaging, logic.State);
    }

    [Test]
    public void Damaging_RepeatsEveryCooldown()
    {
        logic.Tick(5f, 50);
        Assert.AreEqual(0, logic.Tick(2.5f, 50));
        Assert.AreEqual(1, logic.Tick(0.5f, 50));
        Assert.AreEqual(1, logic.Tick(3f, 50));
    }

    [Test]
    public void DroppingBelowThreshold_DuringGrace_ResetsTimer()
    {
        logic.Tick(4f, 45);
        logic.Tick(0.1f, 30);
        Assert.AreEqual(EPressureState.Safe, logic.State);

        // 다시 T 이상: 유예가 처음부터. 4초 지나도 피해 없음.
        Assert.AreEqual(0, logic.Tick(4f, 45));
    }

    [Test]
    public void DroppingBelowThreshold_DuringDamaging_RestartsFromGrace()
    {
        logic.Tick(5f, 45);
        logic.Tick(2f, 45);
        logic.Tick(0.1f, 10);

        // 이전 쿨다운(2초 진행)과 무관하게 유예 5초가 다시 지나야 피해.
        Assert.AreEqual(0, logic.Tick(1.5f, 45));
        Assert.AreEqual(EPressureState.Grace, logic.State);
        Assert.AreEqual(0, logic.Tick(3f, 45));
        Assert.AreEqual(1, logic.Tick(0.5f, 45));
    }

    [Test]
    public void LargeDelta_ReturnsMultipleHits()
    {
        // 유예 5초 + 쿨다운 3초 두 번 = 11초에 3회.
        Assert.AreEqual(3, logic.Tick(11f, 60));
    }

    [Test]
    public void GraceRatio_ReportsProgress()
    {
        logic.Tick(2.5f, 40);
        Assert.AreEqual(0.5f, logic.GraceRatio, 0.0001f);
    }
}
