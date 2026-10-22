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
        CardDefinitionSO sporeDrone = factory.CreateCard("sporeDrone", ECardKind.Prism, _exclusiveGroup: 3);
        CardDefinitionSO sporeOffroad = factory.CreateCard("sporeOffroad", ECardKind.Prism, _exclusiveGroup: 3);

        RunBuildState build = new RunBuildState();
        build.Acquire(sporeDrone);

        Assert.IsTrue(build.IsExcluded(sporeOffroad));
        Assert.IsFalse(build.IsExcluded(sporeDrone));
        Assert.IsTrue(build.IsMax(sporeDrone));
    }

    [Test]
    public void Build_PrismsSharingMaterialAreExclusive_EvenAcrossGroups()
    {
        // 기획서 v2.5 6-2: 재료가 겹치는 조합은 한 판에 하나만. 포자 수류탄 + 드론(포자 조합)과 스텔라 + 드론(그룹 없음)은 드론이 겹친다.
        CardDefinitionSO spore = factory.CreateCard("spore", ECardKind.Active, ERoguelikeSkill.SporeBomb);
        CardDefinitionSO drone = factory.CreateCard("drone", ECardKind.Active, ERoguelikeSkill.Drone);
        CardDefinitionSO stella = factory.CreateCard("stella", ECardKind.Active, ERoguelikeSkill.Stella);
        CardDefinitionSO spin = factory.CreateCard("spin", ECardKind.Active, ERoguelikeSkill.Spin);
        CardDefinitionSO boomerang = factory.CreateCard("boomerang", ECardKind.Active, ERoguelikeSkill.Boomerang);
        CardDefinitionSO sporeDrone = factory.CreatePrism("sporeDrone", spore, drone, ERoguelikeCombo.SporeDrone, ERoguelikeSkill.SporeBomb);
        CardDefinitionSO stellaDrone = factory.CreatePrism("stellaDrone", stella, drone, ERoguelikeCombo.StellaDrone);
        CardDefinitionSO spinBoomerang = factory.CreatePrism("spinBoomerang", spin, boomerang, ERoguelikeCombo.SpinBoomerang);
        sporeDrone.exclusiveGroup = 3;

        RunBuildState build = new RunBuildState();
        build.Acquire(sporeDrone);

        Assert.IsTrue(build.IsExcluded(stellaDrone));
        Assert.IsFalse(build.IsExcluded(spinBoomerang), "재료가 겹치지 않는 조합은 함께 가질 수 있다");
        Assert.IsFalse(build.IsExcluded(drone), "재료 카드 자체는 막지 않는다");
        Assert.IsTrue(sporeDrone.ConflictsWith(stellaDrone));
        Assert.IsFalse(sporeDrone.ConflictsWith(spinBoomerang));
    }

    [Test]
    public void Resolver_SporeComboSuppressesSporeThrowButKeepsPartner()
    {
        CardDefinitionSO spore = factory.CreateCard("spore", ECardKind.Active, ERoguelikeSkill.SporeBomb);
        CardDefinitionSO drone = factory.CreateCard("drone", ECardKind.Active, ERoguelikeSkill.Drone);
        CardDefinitionSO sporeDrone = factory.CreatePrism("sporeDrone", spore, drone, ERoguelikeCombo.SporeDrone, ERoguelikeSkill.SporeBomb);

        RunBuildState build = new RunBuildState();
        build.Acquire(spore);
        build.Acquire(drone);
        build.Acquire(sporeDrone);

        RunModifiers mods = new RunModifiers();
        RunEffectResolver.Resolve(build, mods, new List<CardEffectEntry>());

        Assert.IsTrue(mods.IsComboActive(ERoguelikeCombo.SporeDrone));
        Assert.IsFalse(mods.IsSkillActive(ERoguelikeSkill.SporeBomb));
        Assert.IsTrue(mods.IsSkillActive(ERoguelikeSkill.Drone));
        Assert.IsFalse(mods.IsComboActive(ERoguelikeCombo.SporeShockwave));
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
        Assert.AreEqual(20, WaveSchedule.Get(balance, 1).killTarget);

        // 기획서 v2.7 부록 A "언리밋 곡선": Unlimit-1은 4티어 최소 수치(HP 380, 묶음 21그루 / 2.5초)에서 시작하고,
        // 웨이브마다 HP ×1.10 · 묶음 +1그루 · 묶음 주기 −0.02초가 누적된다. 목표 처치 수는 960 고정.
        Assert.IsTrue(w31.bUnlimit);
        Assert.AreEqual(1, w31.unlimitIndex);
        Assert.AreEqual(960, w31.killTarget);
        Assert.AreEqual(380f, w31.treeHp, 0.001f);
        Assert.AreEqual(21, w31.batchSize);
        Assert.AreEqual(2.5f, w31.batchSeconds, 0.0001f);
        Assert.AreEqual(380f * 1.1f, w32.treeHp, 0.001f);
        Assert.AreEqual(22, w32.batchSize);
        Assert.AreEqual(2.48f, w32.batchSeconds, 0.0001f);

        // Unlimit-30은 묶음 50그루 / 1.92초, 묶음 주기는 하한 1.0초에서 멈춘다(Unlimit-100).
        WaveSpec w60 = WaveSchedule.Get(balance, 60);
        Assert.AreEqual(50, w60.batchSize);
        Assert.AreEqual(1.92f, w60.batchSeconds, 0.0001f);
        Assert.AreEqual(1f, WaveSchedule.Get(balance, 130).batchSeconds, 0.0001f);
    }

    [Test]
    public void WaveSchedule_BatchSizeAndPeriodFollowAppendixA2()
    {
        // 기획서 v2.7 부록 A-2: 티어별 정수 묶음. 1티어 3~7그루 / 3초, 2티어 7~14그루 / 2.5초, 3티어 14~21그루 / 3초, 4티어 21~30그루 / 2.5초.
        int[] size = { 3, 4, 4, 5, 6, 6, 7, 7, 8, 9, 10, 11, 12, 13, 14, 14, 15, 16, 18, 19, 20, 21, 21, 22, 24, 25, 26, 28, 29, 30 };
        for (int wave = 1; wave <= 30; wave++)
        {
            WaveSpec spec = WaveSchedule.Get(balance, wave);
            float period = wave <= 7 ? 3f : wave <= 15 ? 2.5f : wave <= 22 ? 3f : 2.5f;
            Assert.AreEqual(size[wave - 1], spec.batchSize, "묶음 크기 " + wave);
            Assert.AreEqual(period, spec.batchSeconds, 0.0001f, "묶음 주기 " + wave);
            Assert.AreEqual(period / size[wave - 1], spec.spawnInterval, 0.0001f, "그루당 주기 " + wave);
        }

        Assert.AreEqual(960, WaveSchedule.Get(balance, 30).killTarget);
    }

    [Test]
    public void WaveSchedule_KillTargetsFollowAppendixA2_AndNeverDecrease()
    {
        // 기획서 v2.7 부록 A-2: 1웨이브 20 ~ 30웨이브 960, 16 · 17웨이브는 단조 보정 280 / 285.
        int[] targets = { 20, 30, 30, 45, 55, 60, 75, 95, 115, 140, 165, 190, 215, 245, 275, 280, 285, 295, 345, 375, 410, 445, 550, 595, 670, 715, 765, 850, 905, 960 };
        for (int wave = 1; wave <= 30; wave++)
        {
            Assert.AreEqual(targets[wave - 1], WaveSchedule.Get(balance, wave).killTarget, "웨이브 " + wave);
            if (wave > 1) Assert.GreaterOrEqual(WaveSchedule.Get(balance, wave).killTarget, WaveSchedule.Get(balance, wave - 1).killTarget, "단조 " + wave);
        }
    }

    [Test]
    public void WaveSchedule_AbsoluteHpAndDamageFollowAppendixA2()
    {
        // 기획서 v2.7 부록 A-2: 3티어 150 / 160 / 175, 4티어 380 / 500 / 580.
        float[] hp = { 20, 20, 30, 30, 30, 40, 40, 45, 50, 50, 55, 55, 55, 55, 55, 150, 150, 160, 160, 160, 175, 175, 380, 380, 500, 500, 500, 580, 580, 580 };
        for (int wave = 1; wave <= 30; wave++)
        {
            Assert.AreEqual(hp[wave - 1], WaveSchedule.Get(balance, wave).treeHp, 0.001f, "웨이브 " + wave);
        }

        Assert.AreEqual(3f, WaveSchedule.Get(balance, 1).staminaDamage);
        Assert.AreEqual(15f, WaveSchedule.Get(balance, 30).staminaDamage);
        Assert.AreEqual(1f, WaveSchedule.Get(balance, 1).spawnInterval, 0.001f);
        // 기획서 v2.6: 스폰 상한 80 고정(임계치 50과 무관), 유예 시간 = 피해 간격 4초(v2.7).
        Assert.AreEqual(80, balance.SpawnCap);
        Assert.AreEqual(50, balance.threshold);
        Assert.AreEqual(4f, balance.graceTime, 0.001f);
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
