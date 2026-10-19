using System.Collections.Generic;
using NUnit.Framework;

/// <summary>제시 파이프라인과 구매·리롤·건너뛰기(기획서 3-3).</summary>
public class CardOfferAndShopTests
{
    private RoguelikeTestFactory factory;
    private RoguelikeBalanceSO balance;
    private RunBuildState build;
    private RunWallet wallet;
    private RunModifiers modifiers;
    private CardShop shop;

    [SetUp]
    public void SetUp()
    {
        factory = new RoguelikeTestFactory();
        balance = factory.CreateBalance();
        build = new RunBuildState();
        wallet = new RunWallet();
        modifiers = new RunModifiers();
        shop = new CardShop();
    }

    [TearDown]
    public void TearDown()
    {
        factory.DestroyAll();
    }

    private void InitShop(CardDatabaseSO _db, int _seed = 1)
    {
        shop.Initialize(_db, balance, build, wallet, modifiers);
        shop.ResetRun(_seed);
    }

    [Test]
    public void FirstPick_OnlyActives_FreeWithOneFreeReroll()
    {
        CardDefinitionSO p1 = factory.CreateCard("p1", ECardKind.Passive, ECardGrade.Bronze);
        CardDefinitionSO a1 = factory.CreateCard("a1", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Spin);
        CardDefinitionSO a2 = factory.CreateCard("a2", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Drone);
        CardDefinitionSO r1 = factory.CreateCard("r1", ECardKind.Ruby, ECardGrade.Ruby);
        InitShop(factory.CreateDatabase(p1, a1, a2, r1));

        shop.Open(true);

        Assert.AreEqual(2, shop.Offers.Count);
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            Assert.AreEqual(ECardKind.Active, shop.Offers[i].card.kind);
            Assert.AreEqual(0, shop.Offers[i].price);
        }

        // 무료 리롤 1회. 리롤 비용 상승 횟수에 들어가지 않는다.
        Assert.IsTrue(shop.CanReroll);
        Assert.AreEqual(0, shop.RerollCost);
        Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
        Assert.AreEqual(0, build.RerollCount);
        Assert.IsFalse(shop.CanReroll);
        Assert.AreEqual(ECardShopResult.RerollNotAllowed, shop.TryReroll());

        Assert.AreEqual(ECardShopResult.SkipNotAllowed, shop.TrySkip());
        Assert.AreEqual(ECardShopResult.Success, shop.TryBuy(0, out _, null));
        Assert.AreEqual(0, wallet.Gold);
    }

    [Test]
    public void ActiveGuarantee_AtLeastOneActive_EvenWhenUnaffordable()
    {
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 12; i++) cards.Add(factory.CreateCard("p" + i, ECardKind.Passive, ECardGrade.Bronze));
        CardDefinitionSO active = factory.CreateCard("active", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Offroad);
        cards.Add(active);
        InitShop(factory.CreateDatabase(cards.ToArray()));
        wallet.Add(100); // 실버 150은 못 산다.

        for (int round = 0; round < 30; round++)
        {
            shop.Open(false);

            int actives = 0;
            int affordable = 0;
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (ECardKind.Active == shop.Offers[i].card.kind) actives++;
                if (shop.Offers[i].IsAffordable(wallet.Gold)) affordable++;
            }

            Assert.AreEqual(1, actives);
            Assert.AreEqual(2, affordable);
            shop.TrySkip();
        }
    }

    [Test]
    public void ActiveGuarantee_ReleasedWhenThreeActivesOwned()
    {
        CardDefinitionSO a1 = factory.CreateCard("a1", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Spin);
        CardDefinitionSO a2 = factory.CreateCard("a2", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Drone);
        CardDefinitionSO a3 = factory.CreateCard("a3", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Offroad);
        CardDefinitionSO a4 = factory.CreateCard("a4", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Stella);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { a1, a2, a3, a4 };
        for (int i = 0; i < 30; i++) cards.Add(factory.CreateCard("p" + i, ECardKind.Passive, ECardGrade.Bronze));
        InitShop(factory.CreateDatabase(cards.ToArray()));
        wallet.Add(10000);

        shop.GrantDirect(a1);
        shop.GrantDirect(a2);
        shop.GrantDirect(a3);
        // a1~a3 업그레이드와 a4가 모두 후보지만 보장이 풀려 액티브가 없는 제시도 나와야 한다.
        bool bSawNoActive = false;
        for (int round = 0; round < 60 && false == bSawNoActive; round++)
        {
            shop.Open(false);

            int actives = 0;
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (ECardKind.Active == shop.Offers[i].card.kind) actives++;
            }

            bSawNoActive = 0 == actives;
            shop.TrySkip();
        }

        Assert.IsTrue(bSawNoActive);
    }

    [Test]
    public void AffordableGuarantee_PrefersBronzeWhenActiveUnaffordable()
    {
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 6; i++) cards.Add(factory.CreateCard("b" + i, ECardKind.Passive, ECardGrade.Bronze));
        for (int i = 0; i < 20; i++)
        {
            CardDefinitionSO silver = factory.CreateCard("s" + i, ECardKind.Passive, ECardGrade.Silver);
            silver.basePriceOverride = 50; // 실버지만 브론즈보다 싸서 구매 가능
            cards.Add(silver);
        }

        cards.Add(factory.CreateCard("active", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Drone));
        InitShop(factory.CreateDatabase(cards.ToArray()));
        wallet.Add(100);

        for (int round = 0; round < 30; round++)
        {
            shop.Open(false);

            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (ECardKind.Active == shop.Offers[i].card.kind) continue;
                Assert.AreEqual(ECardGrade.Bronze, shop.Offers[i].card.grade);
            }

            shop.TrySkip();
        }
    }

    [Test]
    public void ExclusivePrisms_ShownTogether_BeyondSlotCap()
    {
        CardDefinitionSO stella = factory.CreateCard("stella", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Stella);
        CardDefinitionSO spore = factory.CreateCard("spore", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.SporeBomb);
        CardDefinitionSO drone = factory.CreateCard("drone", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Drone);
        CardDefinitionSO prismA = factory.CreatePrism("prismA", stella, spore, ERoguelikeCombo.StellaSpore);
        CardDefinitionSO prismB = factory.CreatePrism("prismB", stella, drone, ERoguelikeCombo.StellaDrone);
        prismA.exclusiveGroup = 3;
        prismB.exclusiveGroup = 3;
        CardDefinitionSO emerald = factory.CreateCard("emerald", ECardKind.Passive, ECardGrade.Bronze);
        InitShop(factory.CreateDatabase(stella, spore, drone, prismA, prismB, emerald));
        // 상한 1이어도 배타 프리즘 두 장은 함께 나와야 한다.
        balance.guaranteedSlotCap = 1;

        for (int i = 0; i < 5; i++)
        {
            shop.GrantDirect(stella);
            shop.GrantDirect(spore);
            shop.GrantDirect(drone);
        }

        for (int i = 0; i < 4; i++) shop.GrantDirect(emerald);

        shop.Open(false);

        Assert.AreEqual(3, shop.Offers.Count);
        int prisms = 0;
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (ECardKind.Prism == shop.Offers[i].card.kind) prisms++;
        }

        Assert.AreEqual(2, prisms);
    }

    [Test]
    public void Reroll_UnshownGuaranteedCardComesFirst()
    {
        List<CardDefinitionSO> emeralds = new List<CardDefinitionSO>();
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 3; i++)
        {
            CardDefinitionSO card = factory.CreateCard("e" + i, ECardKind.Passive, ECardGrade.Bronze);
            emeralds.Add(card);
            cards.Add(card);
        }

        for (int i = 0; i < 10; i++) cards.Add(factory.CreateCard("f" + i, ECardKind.Passive, ECardGrade.Bronze));
        InitShop(factory.CreateDatabase(cards.ToArray()));
        for (int i = 0; i < emeralds.Count; i++)
        {
            for (int j = 0; j < 4; j++) shop.GrantDirect(emeralds[i]);
        }

        wallet.Add(10000);
        shop.Open(false);

        HashSet<CardDefinitionSO> shown = new HashSet<CardDefinitionSO>();
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (shop.Offers[i].bGuaranteed) shown.Add(shop.Offers[i].card);
        }

        Assert.AreEqual(2, shown.Count);

        // 리롤하면 아직 노출되지 않은 세 번째 에메랄드가 반드시 나온다.
        Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
        bool bThirdShown = false;
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (shop.Offers[i].bGuaranteed && false == shown.Contains(shop.Offers[i].card)) bThirdShown = true;
        }

        Assert.IsTrue(bThirdShown);
    }

    [Test]
    public void Appraisal_LockedWithoutAppraiserOrGold_PromotesWithoutDowngrade()
    {
        CardDefinitionSO bronze = factory.CreateCard("bronze", ECardKind.Passive, ECardGrade.Bronze);
        CardDefinitionSO ruby = factory.CreateCard("ruby", ECardKind.Ruby, ECardGrade.Ruby);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { bronze, ruby };
        for (int i = 0; i < 6; i++) cards.Add(factory.CreateCard("f" + i, ECardKind.Passive, ECardGrade.Bronze));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        Assert.GreaterOrEqual(shop.AppraiserWave, balance.appraiserWaveMin);
        Assert.LessOrEqual(shop.AppraiserWave, balance.appraiserWaveMax);

        shop.GrantDirect(bronze);
        shop.GrantDirect(ruby);

        // 감정사가 없으면 잠김.
        shop.Open(false, false);
        Assert.IsFalse(shop.CanAppraise);
        Assert.AreEqual(ECardAppraisalResult.NotAvailable, shop.TryAppraise(bronze, out _));
        shop.TrySkip();

        // 감정사가 있어도 골드가 가장 싼 감정비(브론즈 I = 90)보다 적으면 잠김.
        wallet.Add(89);
        shop.Open(false, true);
        Assert.IsFalse(shop.CanAppraise);
        Assert.AreEqual(ECardAppraisalResult.NotEnoughGold, shop.TryAppraise(bronze, out _));
        Assert.AreEqual(89, wallet.Gold);

        // 루비는 대상이 아니다.
        Assert.IsFalse(shop.IsAppraisable(ruby));
        Assert.AreEqual(ECardAppraisalResult.NotEligible, shop.TryAppraise(ruby, out _));

        wallet.Add(1);
        Assert.IsTrue(shop.CanAppraise);
        Assert.AreEqual(ECardAppraisalResult.Success, shop.TryAppraise(bronze, out CardAppraisalResult result));
        Assert.AreEqual(0, wallet.Gold);
        Assert.AreEqual(1, result.fromLevel);
        Assert.GreaterOrEqual(result.toLevel, 1);
        Assert.AreEqual(result.toLevel, build.GetLevel(bronze));

        // 선택 기회를 쓰지 않는다. 같은 카드는 다시 맡길 수 없다.
        Assert.IsTrue(shop.IsOpen);
        wallet.Add(1000);
        Assert.AreEqual(ECardAppraisalResult.NotEligible, shop.TryAppraise(bronze, out _));
        shop.TrySkip();

        // 선택이 끝나면 감정사도 떠난다.
        shop.Open(false, false);
        Assert.IsFalse(shop.IsAppraiserPresent);
    }

    [Test]
    public void Appraisal_ToMax_ReplacesThatOfferSlot_AndRefreshesFee()
    {
        CardDefinitionSO target = factory.CreateCard("target", ECardKind.Passive, ECardGrade.Bronze);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { target };
        for (int i = 0; i < 6; i++) cards.Add(factory.CreateCard("f" + i, ECardKind.Passive, ECardGrade.Bronze));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        // 레벨 IV면 에메랄드로 보장 노출된다. IV 감정 결과는 65% 유지 / 35% MAX라 MAX가 나올 때까지 판을 바꿔 시도한다.
        for (int i = 0; i < 4; i++) shop.GrantDirect(target);
        wallet.Add(100000);

        bool bPromotedToMax = false;
        for (int seed = 1; seed < 200 && false == bPromotedToMax; seed++)
        {
            build.Reset();
            wallet.Reset();
            wallet.Add(100000);
            shop.ResetRun(seed);
            for (int i = 0; i < 4; i++) shop.GrantDirect(target);

            shop.Open(false, true);
            Assert.IsTrue(shop.HasAppraisableCards);
            int feeBefore = shop.CheapestAppraisalFee;

            bool bOffered = false;
            for (int i = 0; i < shop.Offers.Count; i++) bOffered |= shop.Offers[i].card == target;
            Assert.IsTrue(bOffered, "레벨 IV 카드는 에메랄드로 보장 노출된다");

            Assert.AreEqual(ECardAppraisalResult.Success, shop.TryAppraise(target, out CardAppraisalResult result));
            if (result.toLevel < 5)
            {
                shop.TrySkip();
                continue;
            }

            bPromotedToMax = true;

            // MAX가 된 카드는 제시에서 빠지고 다른 카드로 바뀐다. 같은 카드가 두 번 나오지 않는다.
            HashSet<CardDefinitionSO> seen = new HashSet<CardDefinitionSO>();
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                Assert.AreNotEqual(target, shop.Offers[i].card);
                Assert.IsTrue(seen.Add(shop.Offers[i].card));
            }

            // 맡길 수 있는 카드가 없으니 감정비 캐시도 갱신되어 -1, 잠금 상태다.
            Assert.Greater(feeBefore, 0);
            Assert.AreEqual(-1, shop.CheapestAppraisalFee);
            Assert.IsFalse(shop.HasAppraisableCards);
            Assert.IsFalse(shop.CanAppraise);
        }

        Assert.IsTrue(bPromotedToMax, "200판 안에 MAX 결과가 한 번은 나와야 한다");
    }

    [Test]
    public void Appraisal_ToMax_ReplacementKeepsAffordableGuarantee()
    {
        CardDefinitionSO target = factory.CreateCard("target", ECardKind.Passive, ECardGrade.Bronze);
        CardDefinitionSO active = factory.CreateCard("active", ECardKind.Active, ECardGrade.Silver, ERoguelikeSkill.Offroad);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { target, active };
        for (int i = 0; i < 3; i++) cards.Add(factory.CreateCard("cheap" + i, ECardKind.Passive, ECardGrade.Bronze));
        for (int i = 0; i < 20; i++) cards.Add(factory.CreateCard("gold" + i, ECardKind.Passive, ECardGrade.Gold));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        bool bPromotedToMax = false;
        for (int seed = 1; seed < 200 && false == bPromotedToMax; seed++)
        {
            build.Reset();
            wallet.Reset();
            wallet.Add(136); // 감정비(브론즈 IV = 66)를 내고 70이 남아 브론즈(60)만 살 수 있다.
            shop.ResetRun(seed);
            for (int i = 0; i < 4; i++) shop.GrantDirect(target);

            shop.Open(false, true);
            Assert.AreEqual(ECardAppraisalResult.Success, shop.TryAppraise(target, out CardAppraisalResult result));
            if (result.toLevel < 5)
            {
                shop.TrySkip();
                continue;
            }

            bPromotedToMax = true;
            Assert.IsTrue(result.bOffersChanged);

            int affordable = 0;
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (shop.Offers[i].IsAffordable(wallet.Gold)) affordable++;
            }

            Assert.GreaterOrEqual(affordable, 2, "MAX가 된 칸을 바꿔도 구매 가능 카드 2장 보장(슬롯 3)은 유지된다");
        }

        Assert.IsTrue(bPromotedToMax);
    }

    [Test]
    public void Appraisal_ResultDistributionNeverDowngrades()
    {
        CardAppraisal appraisal = new CardAppraisal();
        appraisal.ResetRun(42, balance);

        int[] counts = new int[6];
        for (int i = 0; i < 4000; i++)
        {
            int result = appraisal.RollResultLevel(3, 5, balance);
            Assert.GreaterOrEqual(result, 3);
            Assert.LessOrEqual(result, 5);
            counts[result]++;
        }

        // 레벨 III은 +2와 MAX가 같은 결과(V)라 IV(+1)와 V만 오른다: 45 / 28 / 27.
        Assert.AreEqual(0.45f, counts[3] / 4000f, 0.04f);
        Assert.AreEqual(0.28f, counts[4] / 4000f, 0.04f);
        Assert.AreEqual(0.27f, counts[5] / 4000f, 0.04f);
    }

    [Test]
    public void Offers_NeverDuplicate_AndRespectOfferCount()
    {
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 10; i++) cards.Add(factory.CreateCard("c" + i, ECardKind.Passive, ECardGrade.Bronze));
        InitShop(factory.CreateDatabase(cards.ToArray()));
        wallet.Add(1000);

        for (int round = 0; round < 50; round++)
        {
            shop.Open(false);
            Assert.AreEqual(3, shop.Offers.Count);

            HashSet<CardDefinitionSO> seen = new HashSet<CardDefinitionSO>();
            for (int i = 0; i < shop.Offers.Count; i++) Assert.IsTrue(seen.Add(shop.Offers[i].card));
        }
    }

    [Test]
    public void AffordableGuarantee_AtLeastTwoAffordableWhenPossible()
    {
        CardDefinitionSO cheap1 = factory.CreateCard("cheap1", ECardKind.Passive, ECardGrade.Bronze);
        CardDefinitionSO cheap2 = factory.CreateCard("cheap2", ECardKind.Passive, ECardGrade.Bronze);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { cheap1, cheap2 };
        for (int i = 0; i < 12; i++) cards.Add(factory.CreateCard("gold" + i, ECardKind.Passive, ECardGrade.Gold));
        InitShop(factory.CreateDatabase(cards.ToArray()));
        wallet.Add(70); // 브론즈 60만 살 수 있다.

        for (int round = 0; round < 30; round++)
        {
            shop.Open(false);

            int affordable = 0;
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (shop.Offers[i].IsAffordable(wallet.Gold)) affordable++;
            }

            Assert.GreaterOrEqual(affordable, 2);
        }
    }

    [Test]
    public void Emerald_IsGuaranteedEveryOffer_UntilBought()
    {
        CardDefinitionSO target = factory.CreateCard("target", ECardKind.Passive, ECardGrade.Bronze);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { target };
        for (int i = 0; i < 12; i++) cards.Add(factory.CreateCard("f" + i, ECardKind.Passive, ECardGrade.Bronze));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        for (int i = 0; i < 4; i++) shop.GrantDirect(target);
        wallet.Add(10000);

        for (int round = 0; round < 10; round++)
        {
            shop.Open(false);

            bool bFound = false;
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (shop.Offers[i].card != target) continue;
                bFound = true;
                Assert.AreEqual(ECardDisplayGrade.Emerald, shop.Offers[i].displayGrade);
                Assert.IsTrue(shop.Offers[i].bGuaranteed);
                Assert.AreEqual(5, shop.Offers[i].levelAfterPurchase);
            }

            Assert.IsTrue(bFound, "에메랄드 조건 카드가 제시되지 않았다");
            shop.TrySkip();
        }
    }

    [Test]
    public void Prism_AppearsOnlyAfterBothMaterialsMax()
    {
        CardDefinitionSO a = factory.CreateCard("a", ECardKind.Active, ECardGrade.Bronze, ERoguelikeSkill.Spin);
        CardDefinitionSO b = factory.CreateCard("b", ECardKind.Active, ECardGrade.Bronze, ERoguelikeSkill.Shockwave);
        CardDefinitionSO prism = factory.CreatePrism("prism", a, b, ERoguelikeCombo.SpinShockwave);
        CardDefinitionSO filler = factory.CreateCard("filler", ECardKind.Passive, ECardGrade.Bronze);
        InitShop(factory.CreateDatabase(a, b, prism, filler));
        wallet.Add(100000);

        for (int i = 0; i < 5; i++) shop.GrantDirect(a);
        for (int i = 0; i < 4; i++) shop.GrantDirect(b);

        shop.Open(false);
        for (int i = 0; i < shop.Offers.Count; i++) Assert.AreNotEqual(prism, shop.Offers[i].card);
        shop.TrySkip();

        shop.GrantDirect(b);
        shop.Open(false);

        bool bFound = false;
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (shop.Offers[i].card == prism)
            {
                bFound = true;
                Assert.AreEqual(ECardDisplayGrade.Prism, shop.Offers[i].displayGrade);
            }
        }

        Assert.IsTrue(bFound);
    }

    [Test]
    public void Buy_DeductsGold_ClosesShop_AndReportsDiscardedExclusive()
    {
        CardDefinitionSO bargain = factory.CreateCard("bargain", ECardKind.Ruby, ECardGrade.Ruby, _exclusiveGroup: 7);
        CardDefinitionSO vip = factory.CreateCard("vip", ECardKind.Ruby, ECardGrade.Ruby, _exclusiveGroup: 7);
        CardDefinitionSO other = factory.CreateCard("other", ECardKind.Passive, ECardGrade.Bronze);
        InitShop(factory.CreateDatabase(bargain, vip, other));
        wallet.Add(1000);

        shop.Open(false);
        int bargainIdx = -1;
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (shop.Offers[i].card == bargain) bargainIdx = i;
        }

        Assert.GreaterOrEqual(bargainIdx, 0);

        List<int> discarded = new List<int>();
        Assert.AreEqual(ECardShopResult.Success, shop.TryBuy(bargainIdx, out CardPurchaseResult result, discarded));
        Assert.AreEqual(1000 - 240, wallet.Gold);
        Assert.IsFalse(shop.IsOpen);
        Assert.AreEqual(1, discarded.Count);
        Assert.AreEqual(vip, shop.Offers[discarded[0]].card);
        Assert.AreEqual(1, result.newLevel);

        // 다음 선택부터 vip는 조용히 빠진다.
        shop.Open(false);
        for (int i = 0; i < shop.Offers.Count; i++) Assert.AreNotEqual(vip, shop.Offers[i].card);
    }

    [Test]
    public void Reroll_ChargesIncreasingCost()
    {
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 8; i++) cards.Add(factory.CreateCard("c" + i, ECardKind.Passive, ECardGrade.Bronze));
        InitShop(factory.CreateDatabase(cards.ToArray()));
        wallet.Add(100);

        shop.Open(false);
        Assert.AreEqual(20, shop.RerollCost);
        Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
        Assert.AreEqual(80, wallet.Gold);
        Assert.AreEqual(24, shop.RerollCost);
        Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
        Assert.AreEqual(56, wallet.Gold);
        Assert.AreEqual(29, shop.RerollCost);
        Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
        Assert.AreEqual(27, wallet.Gold);
        Assert.AreEqual(35, shop.RerollCost);
        Assert.AreEqual(ECardShopResult.NotEnoughGold, shop.TryReroll());
    }

    [Test]
    public void AllCardsMax_NoOffers()
    {
        CardDefinitionSO a = factory.CreateCard("a", ECardKind.Passive, ECardGrade.Bronze);
        CardDefinitionSO r = factory.CreateCard("r", ECardKind.Ruby, ECardGrade.Ruby);
        InitShop(factory.CreateDatabase(a, r));
        for (int i = 0; i < 5; i++) shop.GrantDirect(a);
        shop.GrantDirect(r);

        // 노출 가능한 카드가 0장이면 제시도 0장이다. MAX·보유 루비로 칸을 채우지 않는다(기획서 4-3 "카드 풀 예외 처리").
        // 이때 런 흐름은 안내 신호를 보내고 확인 입력 없이 웨이브를 시작한다(RoguelikeSystem.CardSelectRoutine).
        shop.Open(false);
        Assert.AreEqual(0, shop.Offers.Count);
    }

    [Test]
    public void NotEnoughGold_PurchaseFails_StateUnchanged()
    {
        CardDefinitionSO gold = factory.CreateCard("gold", ECardKind.Passive, ECardGrade.Gold);
        InitShop(factory.CreateDatabase(gold));
        wallet.Add(10);

        shop.Open(false);
        Assert.AreEqual(ECardShopResult.NotEnoughGold, shop.TryBuy(0, out _, null));
        Assert.AreEqual(10, wallet.Gold);
        Assert.IsTrue(shop.IsOpen);
        Assert.IsFalse(build.IsOwned(gold));
    }
}
