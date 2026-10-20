using System.Collections.Generic;
using NUnit.Framework;

/// <summary>레벨 수·표시 프레임·효과 합산·조합 억제·웨이브 표 규칙(기획서 v2.4 5장, 부록 A-2).</summary>
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
    public void MaxLevel_FollowsKind_Active7Passive5ContractAndPrism1()
    {
        // 기획서 v2.4 5-2: 액티브 7레벨, 공통 패시브 5레벨, 계약·프리즘은 레벨이 없다.
        Assert.AreEqual(7, factory.CreateCard("a", ECardKind.Active, ERoguelikeSkill.Spin).EffectiveMaxLevel);
        Assert.AreEqual(5, factory.CreateCard("p", ECardKind.Passive).EffectiveMaxLevel);
        Assert.AreEqual(1, factory.CreateCard("r", ECardKind.Ruby).EffectiveMaxLevel);
        Assert.AreEqual(1, factory.CreateCard("x", ECardKind.Prism).EffectiveMaxLevel);
    }

    [Test]
    public void Build_ActiveNeedsSevenPicksToMax()
    {
        CardDefinitionSO active = factory.CreateCard("a", ECardKind.Active, ERoguelikeSkill.Drone);
        RunBuildState build = new RunBuildState();

        for (int i = 0; i < 6; i++) Assert.IsTrue(build.Acquire(active));
        Assert.IsFalse(build.IsMax(active));

        Assert.IsTrue(build.Acquire(active));
        Assert.IsTrue(build.IsMax(active));
        Assert.IsFalse(build.Acquire(active), "MAX 이후에는 더 오르지 않는다");
        Assert.AreEqual(7, build.PickCount);
    }

    [Test]
    public void Modifiers_ConditionalBonusAddsToGlobal_AndSurvivesResolve()
    {
        CardDefinitionSO atk = factory.CreateCard("atk", ECardKind.Passive);
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
    public void DisplayGrade_PickReachingMaxIsEmerald_OtherwiseNormal()
    {
        CardDefinitionSO active = factory.CreateCard("a", ECardKind.Active, ERoguelikeSkill.Spin);
        CardDefinitionSO passive = factory.CreateCard("p", ECardKind.Passive);

        Assert.AreEqual(ECardDisplayGrade.Bronze, CardRarity.GetDisplayGrade(active, 6));
        Assert.AreEqual(ECardDisplayGrade.Emerald, CardRarity.GetDisplayGrade(active, 7));
        Assert.AreEqual(ECardDisplayGrade.Bronze, CardRarity.GetDisplayGrade(passive, 4));
        Assert.AreEqual(ECardDisplayGrade.Emerald, CardRarity.GetDisplayGrade(passive, 5));
    }

    [Test]
    public void IsRare_OnlyMaxPrismAndContract()
    {
        // 기획서 v2.4 5-3 "레어 / 일반": MAX(에메랄드)로 완성되는 카드 · 프리즘 · 계약. 나머지는 일반이다.
        CardDefinitionSO active = factory.CreateCard("a", ECardKind.Active, ERoguelikeSkill.Spin);
        CardDefinitionSO passive = factory.CreateCard("p", ECardKind.Passive);
        CardDefinitionSO prism = factory.CreateCard("x", ECardKind.Prism);
        CardDefinitionSO contract = factory.CreateCard("r", ECardKind.Ruby);

        for (int level = 1; level <= 6; level++)
        {
            Assert.IsFalse(CardRarity.IsRare(CardRarity.GetDisplayGrade(active, level)), "액티브 " + level);
        }

        for (int level = 1; level <= 4; level++)
        {
            Assert.IsFalse(CardRarity.IsRare(CardRarity.GetDisplayGrade(passive, level)), "패시브 " + level);
        }

        Assert.IsTrue(CardRarity.IsRare(CardRarity.GetDisplayGrade(active, 7)), "액티브 MAX");
        Assert.IsTrue(CardRarity.IsRare(CardRarity.GetDisplayGrade(passive, 5)), "패시브 MAX");
        Assert.IsTrue(CardRarity.IsRare(CardRarity.GetDisplayGrade(prism, 1)), "프리즘");
        Assert.IsTrue(CardRarity.IsRare(CardRarity.GetDisplayGrade(contract, 1)), "계약");
    }

    [Test]
    public void Resolver_SumsLevelEffectsCumulatively()
    {
        CardDefinitionSO atk = factory.CreateCard("atk", ECardKind.Passive);
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
        CardDefinitionSO spin = factory.CreateCard("spin", ECardKind.Active, ERoguelikeSkill.Spin);
        CardDefinitionSO shock = factory.CreateCard("shock", ECardKind.Active, ERoguelikeSkill.Shockwave);
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
        CardDefinitionSO stellaSpore = factory.CreateCard("stellaSpore", ECardKind.Prism, _exclusiveGroup: 1);
        CardDefinitionSO stellaDrone = factory.CreateCard("stellaDrone", ECardKind.Prism, _exclusiveGroup: 1);

        RunBuildState build = new RunBuildState();
        build.Acquire(stellaSpore);

        Assert.IsTrue(build.IsExcluded(stellaDrone));
        Assert.IsFalse(build.IsExcluded(stellaSpore));
        Assert.IsTrue(build.IsMax(stellaSpore));
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

        // 언리밋 기준 HP 520(기획서 v2.4 부록 A-1), 웨이브마다 +10% 누적.
        Assert.IsTrue(w31.bUnlimit);
        Assert.AreEqual(1, w31.unlimitIndex);
        Assert.AreEqual(145, w31.killTarget);
        Assert.AreEqual(520f * 1.1f, w31.treeHp, 0.001f);
        Assert.AreEqual(520f * 1.2f, w32.treeHp, 0.001f);
    }

    [Test]
    public void WaveSchedule_AbsoluteHpAndDamageFollowAppendixA2()
    {
        // 기획서 v2.4 부록 A-2: 3티어 160 / 185 / 210, 4티어 380 / 450 / 520.
        float[] hp = { 20, 20, 30, 30, 30, 40, 40, 45, 45, 50, 50, 50, 55, 55, 55, 160, 160, 185, 185, 185, 210, 210, 380, 380, 450, 450, 450, 520, 520, 520 };
        for (int wave = 1; wave <= 30; wave++)
        {
            Assert.AreEqual(hp[wave - 1], WaveSchedule.Get(balance, wave).treeHp, 0.001f, "웨이브 " + wave);
        }

        Assert.AreEqual(3f, WaveSchedule.Get(balance, 1).staminaDamage);
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
