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
    public void Price_MatchesAppendixA6Table()
    {
        CardDefinitionSO bronze = factory.CreateCard("b", ECardKind.Passive, ECardGrade.Bronze);
        CardDefinitionSO silver = factory.CreateCard("s", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Spin);
        CardDefinitionSO gold = factory.CreateCard("g", ECardKind.Passive, ECardGrade.Gold);
        CardDefinitionSO ruby = factory.CreateCard("r", ECardKind.Ruby, ECardGrade.Ruby);

        int[] bronzePrices = { 60, 78, 96, 120, 0 };
        int[] silverPrices = { 150, 195, 240, 300, 0 };
        int[] goldPrices = { 300, 390, 480, 600, 0 };
        for (int level = 1; level <= 5; level++)
        {
            Assert.AreEqual(bronzePrices[level - 1], CardPricing.GetPrice(bronze, level, null, balance), "브론즈 " + level);
            Assert.AreEqual(silverPrices[level - 1], CardPricing.GetPrice(silver, level, null, balance), "실버 " + level);
            Assert.AreEqual(goldPrices[level - 1], CardPricing.GetPrice(gold, level, null, balance), "골드 " + level);
        }

        Assert.AreEqual(240, CardPricing.GetPrice(ruby, 1, null, balance));
    }

    [Test]
    public void Price_BargainHalvesAndRoundsUp_FreeCardsStayFree()
    {
        CardDefinitionSO bronze = factory.CreateCard("b", ECardKind.Passive, ECardGrade.Bronze);
        RunModifiers mods = new RunModifiers();
        mods.Add(new CardEffectEntry(ERunEffect.CardPricePercent, ERoguelikeSkill.None, -50f));

        Assert.AreEqual(30, CardPricing.GetPrice(bronze, 1, mods, balance));
        Assert.AreEqual(39, CardPricing.GetPrice(bronze, 2, mods, balance));
        Assert.AreEqual(0, CardPricing.GetPrice(bronze, 5, mods, balance));

        // 홀수 정가는 반값에서 올림(기획서 6-4 "소수점은 올림").
        bronze.basePriceOverride = 75;
        Assert.AreEqual(38, CardPricing.GetPrice(bronze, 1, mods, balance));
    }

    [Test]
    public void AppraisalFee_MatchesSection44Table()
    {
        CardDefinitionSO bronze = factory.CreateCard("b", ECardKind.Passive, ECardGrade.Bronze);
        CardDefinitionSO silver = factory.CreateCard("s", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Spin);
        CardDefinitionSO gold = factory.CreateCard("g", ECardKind.Passive, ECardGrade.Gold);

        int[] bronzeFees = { 90, 110, 138, 66 };
        int[] silverFees = { 224, 276, 345, 165 };
        int[] goldFees = { 449, 552, 690, 330 };
        for (int level = 1; level <= 4; level++)
        {
            Assert.AreEqual(bronzeFees[level - 1], CardPricing.GetAppraisalFee(bronze, level, balance), "브론즈 " + level);
            Assert.AreEqual(silverFees[level - 1], CardPricing.GetAppraisalFee(silver, level, balance), "실버 " + level);
            Assert.AreEqual(goldFees[level - 1], CardPricing.GetAppraisalFee(gold, level, balance), "골드 " + level);
        }

        Assert.AreEqual(0, CardPricing.GetAppraisalFee(bronze, 5, balance));
    }

    [Test]
    public void RerollCost_MultipliesBy1Point2_AndFreeOverride()
    {
        Assert.AreEqual(20, CardPricing.GetRerollCost(0, null, balance));
        Assert.AreEqual(24, CardPricing.GetRerollCost(1, null, balance));
        Assert.AreEqual(29, CardPricing.GetRerollCost(2, null, balance));
        Assert.AreEqual(35, CardPricing.GetRerollCost(3, null, balance));

        RunModifiers mods = new RunModifiers();
        mods.Add(new CardEffectEntry(ERunEffect.RerollFree, ERoguelikeSkill.None, 1f));
        Assert.AreEqual(0, CardPricing.GetRerollCost(5, mods, balance));
    }

    [Test]
    public void Reroll_NotAllowedWhenDisabled()
    {
        RunModifiers mods = new RunModifiers();
        mods.Add(new CardEffectEntry(ERunEffect.RerollDisabled, ERoguelikeSkill.None, 1f));
        Assert.IsFalse(CardPricing.CanReroll(mods));
        Assert.IsTrue(CardPricing.CanReroll(null));
    }

    [Test]
    public void Modifiers_ConditionalBonusAddsToGlobal_AndSurvivesResolve()
    {
        CardDefinitionSO atk = factory.CreateCard("atk", ECardKind.Passive, ECardGrade.Bronze);
        RoguelikeTestFactory.AddLevelEffect(atk, 1, ERunEffect.DamagePercent, ERoguelikeSkill.None, 12f);

        RunBuildState build = new RunBuildState();
        build.Acquire(atk);

        RunModifiers mods = new RunModifiers();
        Assert.IsTrue(mods.SetConditional(20f, 10f));
        Assert.IsFalse(mods.SetConditional(20f, 10f), "같은 값이면 바뀐 것이 아니다");

        // 카드 합계를 다시 만들어도 전투 상태 보너스는 그대로다(RoguelikeSystem이 따로 맞춘다).
        RunEffectResolver.Resolve(build, mods, new List<CardEffectEntry>());
        Assert.AreEqual(32f, mods.GlobalDamagePercent, 0.0001f);
        Assert.AreEqual(10f, mods.GlobalAttackSpeedPercent, 0.0001f);

        Assert.IsTrue(mods.SetConditional(0f, 0f));
        Assert.AreEqual(12f, mods.GlobalDamagePercent, 0.0001f);
    }

    [Test]
    public void DisplayGrade_PurchaseReachingMaxIsEmerald()
    {
        CardDefinitionSO gold = factory.CreateCard("g", ECardKind.Passive, ECardGrade.Gold);

        Assert.AreEqual(ECardDisplayGrade.Gold, CardPricing.GetDisplayGrade(gold, 4));
        Assert.AreEqual(ECardDisplayGrade.Emerald, CardPricing.GetDisplayGrade(gold, 5));
    }

    [Test]
    public void IsRare_OnlyMaxPrismAndRuby()
    {
        // 기획서 v2.2 10-2 "레어의 기준": MAX(에메랄드)로 완성되는 카드 · 프리즘 · 루비. 레벨 I~IV는 실버·골드여도 일반이다.
        CardDefinitionSO silver = factory.CreateCard("s", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Spin);
        CardDefinitionSO gold = factory.CreateCard("g", ECardKind.Passive, ECardGrade.Gold);
        CardDefinitionSO prism = factory.CreateCard("p", ECardKind.Prism, ECardGrade.Gold);
        CardDefinitionSO ruby = factory.CreateCard("r", ECardKind.Ruby, ECardGrade.Ruby);

        for (int level = 1; level <= 4; level++)
        {
            Assert.IsFalse(CardPricing.IsRare(CardPricing.GetDisplayGrade(silver, level)), "실버 " + level);
            Assert.IsFalse(CardPricing.IsRare(CardPricing.GetDisplayGrade(gold, level)), "골드 " + level);
        }

        Assert.IsTrue(CardPricing.IsRare(CardPricing.GetDisplayGrade(silver, 5)), "실버 MAX");
        Assert.IsTrue(CardPricing.IsRare(CardPricing.GetDisplayGrade(gold, 5)), "골드 MAX");
        Assert.IsTrue(CardPricing.IsRare(CardPricing.GetDisplayGrade(prism, 1)), "프리즘");
        Assert.IsTrue(CardPricing.IsRare(CardPricing.GetDisplayGrade(ruby, 1)), "루비");
        Assert.IsFalse(CardPricing.IsRare(ECardDisplayGrade.Bronze), "브론즈");
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
        Assert.AreEqual(30, WaveSchedule.Get(balance, 1).killTarget);

        Assert.IsTrue(w31.bUnlimit);
        Assert.AreEqual(1, w31.unlimitIndex);
        Assert.AreEqual(0f, w31.goldPerTree);
        Assert.AreEqual(145, w31.killTarget);
        Assert.AreEqual(420f * 1.1f, w31.treeHp, 0.001f);
        Assert.AreEqual(420f * 1.2f, w32.treeHp, 0.001f);
    }

    [Test]
    public void WaveSchedule_AbsoluteHpGoldAndDamageFollowAppendixA2()
    {
        float[] hp = { 20, 20, 30, 30, 30, 40, 40, 45, 45, 50, 50, 50, 55, 55, 55, 100, 100, 115, 115, 115, 130, 130, 280, 280, 340, 340, 340, 420, 420, 420 };
        for (int wave = 1; wave <= 30; wave++)
        {
            Assert.AreEqual(hp[wave - 1], WaveSchedule.Get(balance, wave).treeHp, 0.001f, "웨이브 " + wave);
        }

        Assert.AreEqual(2f, WaveSchedule.Get(balance, 1).goldPerTree);
        Assert.AreEqual(1.75f, WaveSchedule.Get(balance, 8).goldPerTree);
        Assert.AreEqual(1f, WaveSchedule.Get(balance, 23).goldPerTree);
        Assert.AreEqual(15f, WaveSchedule.Get(balance, 30).staminaDamage);
        Assert.AreEqual(2f, WaveSchedule.Get(balance, 1).spawnInterval, 0.001f);
        Assert.AreEqual(52, balance.SpawnCap);
    }

    [Test]
    public void WaveSchedule_TreeKindChangesWithinTier_KeepPreviousKindShare()
    {
        WaveSpec w3 = WaveSchedule.Get(balance, 3);
        WaveSpec w4 = WaveSchedule.Get(balance, 4);
        WaveSpec w8 = WaveSchedule.Get(balance, 8);
        WaveSpec w28 = WaveSchedule.Get(balance, 28);

        Assert.AreEqual(1, w3.treeKind);
        Assert.AreEqual(0, w3.previousTreeKind);
        Assert.AreEqual(0.3f, w3.previousTreeKindShare, 0.0001f);

        Assert.AreEqual(-1, w4.previousTreeKind);

        // 티어가 바뀌는 웨이브는 다른 숲의 나무를 섞지 않는다.
        Assert.AreEqual(0, w8.treeKind);
        Assert.AreEqual(-1, w8.previousTreeKind);

        Assert.AreEqual(2, w28.treeKind);
        Assert.AreEqual(1, w28.previousTreeKind);
    }
}
