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
    public void FirstPick_OnlyBronze_FreeAndNoReroll()
    {
        CardDefinitionSO b1 = factory.CreateCard("b1", ECardKind.Passive, ECardGrade.Bronze);
        CardDefinitionSO b2 = factory.CreateCard("b2", ECardKind.Active, ECardGrade.Bronze, ERoguelikeSkill.Spin);
        CardDefinitionSO s1 = factory.CreateCard("s1", ECardKind.Passive, ECardGrade.Silver);
        CardDefinitionSO g1 = factory.CreateCard("g1", ECardKind.Passive, ECardGrade.Gold);
        InitShop(factory.CreateDatabase(b1, b2, s1, g1));

        shop.Open(true);

        Assert.AreEqual(2, shop.Offers.Count);
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            Assert.AreEqual(ECardGrade.Bronze, shop.Offers[i].card.grade);
            Assert.AreEqual(0, shop.Offers[i].price);
        }

        Assert.IsFalse(shop.CanReroll);
        Assert.AreEqual(ECardShopResult.SkipNotAllowed, shop.TrySkip());
        Assert.AreEqual(ECardShopResult.Success, shop.TryBuy(0, out _, null));
        Assert.AreEqual(0, wallet.Gold);
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
        wallet.Add(50); // 브론즈 40만 살 수 있다.

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
        Assert.AreEqual(1000 - 180, wallet.Gold);
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
        Assert.AreEqual(30, shop.RerollCost);
        Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
        Assert.AreEqual(50, wallet.Gold);
        Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
        Assert.AreEqual(10, wallet.Gold);
        Assert.AreEqual(ECardShopResult.NotEnoughGold, shop.TryReroll());
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
