using System.Collections.Generic;
using NUnit.Framework;

/// <summary>가격·표시 등급·효과 합산·조합 억제 규칙(기획서 3-3).</summary>
public class CardRulesTests
{
    private RoguelikeTestFactory factory;
    private RoguelikeBalanceSO balance;

    [SetUp]
    public void SetUp()
    {
        factory = new RoguelikeTestFactory();
        balance = factory.CreateBalance();
    }

    [TearDown]
    public void TearDown()
    {
        factory.DestroyAll();
    }

    [Test]
    public void Price_FirstPurchaseUsesGradePrice_UpgradeScalesByLevel()
    {
        CardDefinitionSO bronze = factory.CreateCard("b", ECardKind.Passive, ECardGrade.Bronze);

        Assert.AreEqual(40, CardPricing.GetPrice(bronze, 1, null, balance));
        Assert.AreEqual(60, CardPricing.GetPrice(bronze, 2, null, balance));
        Assert.AreEqual(100, CardPricing.GetPrice(bronze, 4, null, balance));
    }

    [Test]
    public void Price_CardPricePercentApplies()
    {
        CardDefinitionSO silver = factory.CreateCard("s", ECardKind.Passive, ECardGrade.Silver);
        RunModifiers mods = new RunModifiers();
        mods.Add(new CardEffectEntry(ERunEffect.CardPricePercent, ERoguelikeSkill.None, -50f));

        Assert.AreEqual(60, CardPricing.GetPrice(silver, 1, mods, balance));
    }

    [Test]
    public void RerollCost_LinearIncrease_AndFreeOverride()
    {
        Assert.AreEqual(20, CardPricing.GetRerollCost(0, null, balance));
        Assert.AreEqual(40, CardPricing.GetRerollCost(2, null, balance));

        RunModifiers mods = new RunModifiers();
        mods.Add(new CardEffectEntry(ERunEffect.RerollFree, ERoguelikeSkill.None, 1f));
        Assert.AreEqual(0, CardPricing.GetRerollCost(5, mods, balance));
    }

    [Test]
    public void Reroll_NotAllowedOnFirstPickOrWhenDisabled()
    {
        Assert.IsFalse(CardPricing.CanReroll(true, null));

        RunModifiers mods = new RunModifiers();
        mods.Add(new CardEffectEntry(ERunEffect.RerollDisabled, ERoguelikeSkill.None, 1f));
        Assert.IsFalse(CardPricing.CanReroll(false, mods));
        Assert.IsTrue(CardPricing.CanReroll(false, null));
    }

    [Test]
    public void DisplayGrade_PurchaseReachingMaxIsEmerald()
    {
        CardDefinitionSO gold = factory.CreateCard("g", ECardKind.Passive, ECardGrade.Gold);

        Assert.AreEqual(ECardDisplayGrade.Gold, CardPricing.GetDisplayGrade(gold, 4));
        Assert.AreEqual(ECardDisplayGrade.Emerald, CardPricing.GetDisplayGrade(gold, 5));
    }

    [Test]
    public void Resolver_SumsLevelEffectsCumulatively()
    {
        CardDefinitionSO atk = factory.CreateCard("atk", ECardKind.Passive, ECardGrade.Bronze);
        for (int level = 1; level <= 5; level++)
        {
            RoguelikeTestFactory.AddLevelEffect(atk, level, ERunEffect.DamagePercent, ERoguelikeSkill.None, 10f);
        }

        RunBuildState build = new RunBuildState();
        build.Acquire(atk);
        build.Acquire(atk);
        build.Acquire(atk);

        RunModifiers mods = new RunModifiers();
        RunEffectResolver.Resolve(build, mods, new List<CardEffectEntry>());

        Assert.AreEqual(30f, mods.Get(ERunEffect.DamagePercent), 0.0001f);
    }

    [Test]
    public void Resolver_ActiveCardUnlocksItsSkill_PrismSuppressesMaterials()
    {
        CardDefinitionSO spin = factory.CreateCard("spin", ECardKind.Active, ECardGrade.Bronze, ERoguelikeSkill.Spin);
        CardDefinitionSO shock = factory.CreateCard("shock", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Shockwave);
        CardDefinitionSO prism = factory.CreatePrism("p", spin, shock, ERoguelikeCombo.SpinShockwave, ERoguelikeSkill.Spin, ERoguelikeSkill.Shockwave);

        RunBuildState build = new RunBuildState();
        build.Acquire(spin);
        build.Acquire(shock);

        RunModifiers mods = new RunModifiers();
        List<CardEffectEntry> scratch = new List<CardEffectEntry>();
        RunEffectResolver.Resolve(build, mods, scratch);

        Assert.IsTrue(mods.IsSkillActive(ERoguelikeSkill.Spin));
        Assert.IsTrue(mods.IsSkillActive(ERoguelikeSkill.Shockwave));
        Assert.IsFalse(mods.IsComboActive(ERoguelikeCombo.SpinShockwave));

        build.Acquire(prism);
        RunEffectResolver.Resolve(build, mods, scratch);

        Assert.IsTrue(mods.IsComboActive(ERoguelikeCombo.SpinShockwave));
        Assert.IsFalse(mods.IsSkillActive(ERoguelikeSkill.Spin));
        Assert.IsTrue(mods.IsSkillUnlocked(ERoguelikeSkill.Spin));
        Assert.IsFalse(mods.IsSkillActive(ERoguelikeSkill.Shockwave));
    }

    [Test]
    public void Build_ExclusiveGroupLocksOtherCardsButNotTheOwner()
    {
        CardDefinitionSO bargain = factory.CreateCard("bargain", ECardKind.Ruby, ECardGrade.Ruby, _exclusiveGroup: 1);
        CardDefinitionSO vip = factory.CreateCard("vip", ECardKind.Ruby, ECardGrade.Ruby, _exclusiveGroup: 1);

        RunBuildState build = new RunBuildState();
        build.Acquire(bargain);

        Assert.IsTrue(build.IsExcluded(vip));
        Assert.IsFalse(build.IsExcluded(bargain));
        Assert.IsTrue(build.IsMax(bargain));
    }

    [Test]
    public void WaveSchedule_TierChangeAndUnlimit()
    {
        WaveSpec w7 = WaveSchedule.Get(balance, 7);
        WaveSpec w8 = WaveSchedule.Get(balance, 8);
        WaveSpec w31 = WaveSchedule.Get(balance, 31);
        WaveSpec w32 = WaveSchedule.Get(balance, 32);

        Assert.AreEqual(0, w7.tierIndex);
        Assert.IsFalse(w7.bTierChanged);
        Assert.AreEqual(1, w8.tierIndex);
        Assert.IsTrue(w8.bTierChanged);
        Assert.AreEqual(60, WaveSchedule.Get(balance, 1).killTarget);

        Assert.IsTrue(w31.bUnlimit);
        Assert.AreEqual(1, w31.unlimitIndex);
        Assert.AreEqual(0, w31.goldPerTree);
        Assert.AreEqual(350, w31.killTarget);
        Assert.AreEqual(12f * 1.1f, w31.treeHpMultiplier, 0.0001f);
        Assert.AreEqual(12f * 1.2f, w32.treeHpMultiplier, 0.0001f);
    }
}
