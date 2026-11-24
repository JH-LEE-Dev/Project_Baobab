using NUnit.Framework;

/// <summary>기획서 v3.3 3-2 "스태미너 손실 판정 흐름"을 검증한다(안전 / 손실 중 2상태, 1초 펄스, 감소 합 상한).</summary>
public class StaminaPressureLogicTests
{
    private StaminaPressureLogic logic;

    [SetUp]
    public void SetUp()
    {
        logic = new StaminaPressureLogic();
        logic.Configure(40, 2f);
    }

    [Test]
    public void BelowThreshold_StaysSafe_NoLoss_NoPulse()
    {
        float loss = logic.Tick(10f, 39, out bool bPulse);

        Assert.AreEqual(0f, loss);
        Assert.IsFalse(bPulse);
        Assert.AreEqual(EPressureState.Safe, logic.State);
    }

    [Test]
    public void AtThreshold_EntersLosing_ImmediatePulse_LossIsRateTimesDelta()
    {
        float loss = logic.Tick(0.5f, 40, out bool bPulse);

        Assert.AreEqual(EPressureState.Losing, logic.State);
        Assert.IsTrue(bPulse);
        Assert.AreEqual(2f * 0.5f, loss, 0.0001f);
    }

    [Test]
    public void DroppingBelowThreshold_StopsImmediately_ReenteringLosesAndPulsesAgain()
    {
        logic.Tick(0.3f, 45, out bool _);
        float stopped = logic.Tick(0.1f, 39, out bool bStopPulse);

        Assert.AreEqual(0f, stopped);
        Assert.IsFalse(bStopPulse);
        Assert.AreEqual(EPressureState.Safe, logic.State);

        // 다시 T 이상: 기다림 없이 바로 손실 + 첫 펄스.
        float loss = logic.Tick(0.1f, 45, out bool bPulse);
        Assert.AreEqual(EPressureState.Losing, logic.State);
        Assert.IsTrue(bPulse);
        Assert.AreEqual(0.2f, loss, 0.0001f);
    }

    [Test]
    public void Losing_PulsesOncePerSecond()
    {
        logic.Tick(0.016f, 50, out bool bFirst);
        Assert.IsTrue(bFirst);

        // 0.5초 + 0.6초 = 1.1초가 지나야 두 번째 펄스(한 번).
        logic.Tick(0.5f, 50, out bool bHalf);
        Assert.IsFalse(bHalf);
        logic.Tick(0.6f, 50, out bool bSecond);
        Assert.IsTrue(bSecond);

        // 남은 0.1초 + 0.5초는 아직 1초 미만.
        logic.Tick(0.5f, 50, out bool bNone);
        Assert.IsFalse(bNone);
    }

    [Test]
    public void LargeDelta_PulsesOnlyOnce()
    {
        logic.Tick(0.016f, 50, out bool _);

        float loss = logic.Tick(3f, 50, out bool bPulse);
        Assert.IsTrue(bPulse);
        Assert.AreEqual(6f, loss, 0.0001f);

        // 큰 프레임 직후에도 펄스가 몰려 나오지 않는다.
        logic.Tick(0.016f, 50, out bool bNext);
        Assert.IsFalse(bNext);
    }

    [Test]
    public void Configure_NegativeLoss_ClampsToZero()
    {
        logic.Configure(40, -1f);

        Assert.AreEqual(0f, logic.LossPerSecond);
        Assert.AreEqual(0f, logic.Tick(1f, 50, out bool _));
    }

    [Test]
    public void ReducedLoss_SumIsCappedAtMaxReduce()
    {
        // 공통 패시브 MAX 35 + 인내의 시간 50 = 85 → 상한 70으로 잘려 손실 배율 0.3.
        Assert.AreEqual(0.6f * 0.3f, StaminaPressureLogic.ReducedLoss(0.6f, 35f + 50f, 70f), 0.0001f);
        // 상한 안쪽이면 그대로, 음수 감소는 0으로.
        Assert.AreEqual(2f * 0.91f, StaminaPressureLogic.ReducedLoss(2f, 9f, 70f), 0.0001f);
        Assert.AreEqual(2f, StaminaPressureLogic.ReducedLoss(2f, -10f, 70f), 0.0001f);
    }
}
