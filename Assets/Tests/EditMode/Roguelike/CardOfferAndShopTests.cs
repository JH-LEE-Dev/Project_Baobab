using System.Collections.Generic;
using NUnit.Framework;

/// <summary>제시 파이프라인과 선택·리롤(기획서 v2.4 4장 "카드 선택").</summary>
public class CardOfferAndShopTests
{
    private RoguelikeTestFactory factory;
    private RoguelikeBalanceSO balance;
    private RunBuildState build;
    private RunModifiers modifiers;
    private CardShop shop;

    [SetUp]
    public void SetUp()
    {
        factory = new RoguelikeTestFactory();
        balance = factory.CreateBalance();
        build = new RunBuildState();
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
        shop.Initialize(_db, balance, build, modifiers);
        shop.ResetRun(_seed);
    }

    [Test]
    public void FirstPick_OnlyActives_WithOneReroll()
    {
        CardDefinitionSO p1 = factory.CreateCard("p1", ECardKind.Passive);
        CardDefinitionSO a1 = factory.CreateCard("a1", ECardKind.Active, ERoguelikeSkill.Spin);
        CardDefinitionSO a2 = factory.CreateCard("a2", ECardKind.Active, ERoguelikeSkill.Drone);
        CardDefinitionSO r1 = factory.CreateCard("r1", ECardKind.Ruby);
        InitShop(factory.CreateDatabase(p1, a1, a2, r1));

        shop.Open(true);

        Assert.AreEqual(2, shop.Offers.Count);
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            Assert.AreEqual(ECardKind.Active, shop.Offers[i].card.kind);
        }

        // 첫 선택에도 리롤 1회(기획서 4-1 "첫 선택").
        Assert.IsTrue(shop.CanReroll);
        Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
        Assert.IsFalse(shop.CanReroll);
        Assert.AreEqual(ECardShopResult.RerollNotAllowed, shop.TryReroll());

        Assert.AreEqual(ECardShopResult.Success, shop.TryBuy(0, out _, null));
        Assert.AreEqual(1, build.PickCount);
    }

    [Test]
    public void Reroll_OncePerSelection_NotCarriedOver()
    {
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 12; i++) cards.Add(factory.CreateCard("c" + i, ECardKind.Passive));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        // 리롤을 쓰지 않고 고른 다음 선택도 1회뿐이다(이월하지 않는다).
        shop.Open(false);
        Assert.AreEqual(1, shop.RerollsLeft);
        Assert.AreEqual(ECardShopResult.Success, shop.TryBuy(0, out _, null));

        shop.Open(false);
        Assert.AreEqual(1, shop.RerollsLeft);
        Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
        Assert.AreEqual(ECardShopResult.RerollNotAllowed, shop.TryReroll());
        Assert.AreEqual(4, shop.Offers.Count);
        Assert.AreEqual(ECardShopResult.Success, shop.TryBuy(0, out _, null));

        // 다음 선택에서는 다시 1회.
        shop.Open(false);
        Assert.IsTrue(shop.CanReroll);
    }

    [Test]
    public void Pick_IsFree_ClosesShop_AndReportsDiscardedExclusive()
    {
        CardDefinitionSO contractA = factory.CreateCard("contractA", ECardKind.Ruby, _exclusiveGroup: 7);
        CardDefinitionSO contractB = factory.CreateCard("contractB", ECardKind.Ruby, _exclusiveGroup: 7);
        CardDefinitionSO other = factory.CreateCard("other", ECardKind.Passive);
        balance.kindWeights = new List<float> { 1f, 1f, 1f, 1f };
        InitShop(factory.CreateDatabase(contractA, contractB, other));

        shop.Open(false);
        int contractIdx = -1;
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (shop.Offers[i].card == contractA) contractIdx = i;
        }

        Assert.GreaterOrEqual(contractIdx, 0);

        List<int> discarded = new List<int>();
        Assert.AreEqual(ECardShopResult.Success, shop.TryBuy(contractIdx, out CardPurchaseResult result, discarded));
        Assert.IsFalse(shop.IsOpen);
        Assert.AreEqual(1, discarded.Count);
        Assert.AreEqual(contractB, shop.Offers[discarded[0]].card);
        Assert.AreEqual(1, result.newLevel);
        Assert.AreEqual(ECardDisplayGrade.Ruby, result.displayGrade);

        // 다음 선택부터 같은 그룹의 다른 카드는 조용히 빠진다.
        shop.Open(false);
        for (int i = 0; i < shop.Offers.Count; i++) Assert.AreNotEqual(contractB, shop.Offers[i].card);
    }

    [Test]
    public void KindWeight_ContractsAppearMuchLessThanPassives()
    {
        // 종류 비중: 액티브 1.0 = 공통 패시브 1.0 > 계약 0.3(기획서 v2.5 부록 A-1). 패시브 2장 보장이 더해져 계약은 랜덤 칸에서만 나온다.
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 10; i++) cards.Add(factory.CreateCard("p" + i, ECardKind.Passive));
        for (int i = 0; i < 10; i++) cards.Add(factory.CreateCard("r" + i, ECardKind.Ruby));
        InitShop(factory.CreateDatabase(cards.ToArray()), 7);

        int passives = 0;
        int contracts = 0;
        for (int round = 0; round < 400; round++)
        {
            shop.Open(false);
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (ECardKind.Ruby == shop.Offers[i].card.kind) contracts++;
                else passives++;
            }

            shop.Close();
        }

        Assert.Greater(contracts, 0, "계약도 낮은 비중으로 노출된다");
        Assert.Less(contracts * 3, passives, "계약은 공통 패시브보다 훨씬 덜 노출된다");
    }

    [Test]
    public void ForcedPartner_ShownWhenMaterialIsMax()
    {
        CardDefinitionSO spin = factory.CreateCard("spin", ECardKind.Active, ERoguelikeSkill.Spin);
        CardDefinitionSO shock = factory.CreateCard("shock", ECardKind.Active, ERoguelikeSkill.Shockwave);
        CardDefinitionSO prism = factory.CreatePrism("prism", spin, shock, ERoguelikeCombo.SpinShockwave);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { spin, shock, prism };
        for (int i = 0; i < 20; i++) cards.Add(factory.CreateCard("p" + i, ECardKind.Passive));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        for (int i = 0; i < 7; i++) shop.GrantDirect(spin);

        for (int round = 0; round < 20; round++)
        {
            shop.Open(false);

            bool bFound = false;
            for (int i = 0; i < shop.Offers.Count; i++) bFound |= shop.Offers[i].card == shock;
            Assert.IsTrue(bFound, "조합 재료 한 장이 MAX면 파트너가 한 선택에 1장 확실히 나온다");

            shop.Close();
        }
    }

    [Test]
    public void ForcedPartner_NotShownForExcludedPrism()
    {
        // 포자 수류탄 + 오프로드를 고르면 같은 포자 조합 그룹의 포자 수류탄 + 드론은 더는 얻을 수 없다. 포자 수류탄이 MAX여도 드론을 파트너로 밀어주지 않는다.
        CardDefinitionSO spore = factory.CreateCard("spore", ECardKind.Active, ERoguelikeSkill.SporeBomb);
        CardDefinitionSO offroad = factory.CreateCard("offroad", ECardKind.Active, ERoguelikeSkill.Offroad);
        CardDefinitionSO drone = factory.CreateCard("drone", ECardKind.Active, ERoguelikeSkill.Drone);
        CardDefinitionSO sporeOffroad = factory.CreatePrism("sporeOffroad", spore, offroad, ERoguelikeCombo.SporeOffroad);
        CardDefinitionSO sporeDrone = factory.CreatePrism("sporeDrone", spore, drone, ERoguelikeCombo.SporeDrone);
        sporeOffroad.exclusiveGroup = 3;
        sporeDrone.exclusiveGroup = 3;
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { spore, offroad, drone, sporeOffroad, sporeDrone };
        for (int i = 0; i < 40; i++) cards.Add(factory.CreateCard("p" + i, ECardKind.Passive));
        // 액티브 최소 1장 보장(v2.6)이 노출 가능한 액티브 중에서 고르므로, 드론만 남지 않게 다른 액티브를 섞는다.
        for (int i = 0; i < 10; i++) cards.Add(factory.CreateCard("a" + i, ECardKind.Active, ERoguelikeSkill.Spin));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        for (int i = 0; i < 7; i++)
        {
            shop.GrantDirect(spore);
            shop.GrantDirect(offroad);
        }

        shop.GrantDirect(sporeOffroad);

        int droneShown = 0;
        for (int round = 0; round < 40; round++)
        {
            shop.Open(false);
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (shop.Offers[i].card == drone) droneShown++;
            }

            shop.Close();
        }

        // 확정 노출이면 40번 모두 나온다. 일반 가중치로는 40장 패시브 사이에서 가끔만 나온다.
        Assert.Less(droneShown, 20);
    }

    [Test]
    public void ExclusivePrisms_ShownTogether_BeyondSlotCap()
    {
        // 포자 수류탄 + 드론과 스텔라 + 드론은 배타 그룹이 달라도 재료(드론)가 겹쳐 양자택일이다.
        CardDefinitionSO stella = factory.CreateCard("stella", ECardKind.Active, ERoguelikeSkill.Stella);
        CardDefinitionSO spore = factory.CreateCard("spore", ECardKind.Active, ERoguelikeSkill.SporeBomb);
        CardDefinitionSO drone = factory.CreateCard("drone", ECardKind.Active, ERoguelikeSkill.Drone);
        CardDefinitionSO prismA = factory.CreatePrism("prismA", spore, drone, ERoguelikeCombo.SporeDrone);
        CardDefinitionSO prismB = factory.CreatePrism("prismB", stella, drone, ERoguelikeCombo.StellaDrone);
        prismA.exclusiveGroup = 3;
        CardDefinitionSO emerald = factory.CreateCard("emerald", ECardKind.Passive);
        InitShop(factory.CreateDatabase(stella, spore, drone, prismA, prismB, emerald));
        // 상한 1이어도 배타 프리즘 두 장은 함께 나와야 한다.
        balance.guaranteedSlotCap = 1;

        for (int i = 0; i < 7; i++)
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
    public void Guaranteed_CapTwo_ThirdCarriesOverToNextSelection()
    {
        List<CardDefinitionSO> emeralds = new List<CardDefinitionSO>();
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 3; i++)
        {
            CardDefinitionSO card = factory.CreateCard("e" + i, ECardKind.Passive);
            emeralds.Add(card);
            cards.Add(card);
        }

        for (int i = 0; i < 10; i++) cards.Add(factory.CreateCard("f" + i, ECardKind.Passive));
        InitShop(factory.CreateDatabase(cards.ToArray()));
        for (int i = 0; i < emeralds.Count; i++)
        {
            for (int j = 0; j < 4; j++) shop.GrantDirect(emeralds[i]);
        }

        shop.Open(false);

        HashSet<CardDefinitionSO> shown = new HashSet<CardDefinitionSO>();
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (shop.Offers[i].bGuaranteed) shown.Add(shop.Offers[i].card);
        }

        // 프리즘 · MAX는 합쳐 한 번에 2장까지(기획서 v2.5 4-2).
        Assert.AreEqual(2, shown.Count);

        // 고르지 않고 닫으면 다음 선택에는 이월된 세 번째 에메랄드가 보장으로 나온다.
        shop.Close();
        shop.Open(false);
        bool bThirdShown = false;
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (shop.Offers[i].bGuaranteed && false == shown.Contains(shop.Offers[i].card)) bThirdShown = true;
        }

        Assert.IsTrue(bThirdShown);
    }

    [Test]
    public void TwoPassivesGuaranteed_AfterFirstPick()
    {
        // 액티브가 훨씬 많아도 공통 패시브 2장은 무조건 나온다(기획서 v2.5 4-2 ③).
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 30; i++) cards.Add(factory.CreateCard("a" + i, ECardKind.Active, ERoguelikeSkill.Spin));
        for (int i = 0; i < 3; i++) cards.Add(factory.CreateCard("p" + i, ECardKind.Passive));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        for (int round = 0; round < 30; round++)
        {
            shop.Open(false);
            Assert.AreEqual(4, shop.Offers.Count);

            int passives = 0;
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                if (ECardKind.Passive == shop.Offers[i].card.kind) passives++;
            }

            Assert.GreaterOrEqual(passives, 2);
            shop.Close();
        }
    }

    [Test]
    public void Reroll_IsTrueRandom_NoGuaranteedSlots()
    {
        // 리롤은 우선순위(프리즘 · MAX · 패시브 보장)를 쓰지 않는다. 에메랄드도 보장으로 나오지 않고 랜덤으로만 섞인다.
        CardDefinitionSO target = factory.CreateCard("target", ECardKind.Active, ERoguelikeSkill.Boomerang);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { target };
        for (int i = 0; i < 30; i++) cards.Add(factory.CreateCard("a" + i, ECardKind.Active, ERoguelikeSkill.Spin));
        InitShop(factory.CreateDatabase(cards.ToArray()));
        for (int i = 0; i < 6; i++) shop.GrantDirect(target);

        int targetMissing = 0;
        for (int round = 0; round < 40; round++)
        {
            shop.Open(false);
            Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());

            bool bFound = false;
            HashSet<CardDefinitionSO> seen = new HashSet<CardDefinitionSO>();
            for (int i = 0; i < shop.Offers.Count; i++)
            {
                Assert.IsFalse(shop.Offers[i].bGuaranteed);
                Assert.IsTrue(seen.Add(shop.Offers[i].card), "한 선택에 같은 카드는 한 번만");
                bFound |= shop.Offers[i].card == target;
            }

            if (false == bFound) targetMissing++;
            shop.Close();
        }

        Assert.Greater(targetMissing, 0, "리롤에서는 에메랄드가 보장되지 않는다");

        // 리롤로 빠졌어도 다음 선택에는 다시 보장으로 나온다.
        shop.Open(false);
        bool bGuaranteedBack = false;
        for (int i = 0; i < shop.Offers.Count; i++) bGuaranteedBack |= shop.Offers[i].card == target && shop.Offers[i].bGuaranteed;
        Assert.IsTrue(bGuaranteedBack);
    }

    [Test]
    public void ActiveAtLeastOne_EveryOfferAndReroll()
    {
        // 기획서 v2.6 4-2 ⑤: 패시브 · 계약이 훨씬 많아도 제시와 리롤 모두 액티브가 최소 1장 나온다.
        CardDefinitionSO active = factory.CreateCard("active", ECardKind.Active, ERoguelikeSkill.Spin);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { active };
        for (int i = 0; i < 30; i++) cards.Add(factory.CreateCard("p" + i, ECardKind.Passive));
        for (int i = 0; i < 30; i++) cards.Add(factory.CreateCard("r" + i, ECardKind.Ruby));
        balance.kindWeights = new List<float> { 1f, 1f, 1f, 1f };
        InitShop(factory.CreateDatabase(cards.ToArray()), 3);

        for (int round = 0; round < 40; round++)
        {
            shop.Open(false);
            Assert.AreEqual(4, shop.Offers.Count);
            Assert.AreEqual(1, CountKind(ECardKind.Active), "제시 " + round);
            Assert.GreaterOrEqual(CountKind(ECardKind.Passive), 2, "패시브 2장 보장은 유지 " + round);

            Assert.AreEqual(ECardShopResult.Success, shop.TryReroll());
            Assert.AreEqual(1, CountKind(ECardKind.Active), "리롤 " + round);
            shop.Close();
        }
    }

    [Test]
    public void ActiveAtLeastOne_ReplacesPassiveSlotWhenNoRandomSlot()
    {
        // 프리즘 · MAX 2장 + 패시브 2장으로 4칸이 찼는데 액티브가 없으면 패시브 보장 1칸을 액티브로 바꾼다. 보장 칸은 건드리지 않는다.
        List<CardDefinitionSO> emeralds = new List<CardDefinitionSO>();
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 2; i++)
        {
            CardDefinitionSO card = factory.CreateCard("e" + i, ECardKind.Passive);
            emeralds.Add(card);
            cards.Add(card);
        }

        for (int i = 0; i < 10; i++) cards.Add(factory.CreateCard("p" + i, ECardKind.Passive));
        CardDefinitionSO active = factory.CreateCard("active", ECardKind.Active, ERoguelikeSkill.Boomerang);
        cards.Add(active);
        InitShop(factory.CreateDatabase(cards.ToArray()));
        for (int i = 0; i < emeralds.Count; i++)
        {
            for (int j = 0; j < 4; j++) shop.GrantDirect(emeralds[i]);
        }

        shop.Open(false);

        int guaranteed = 0;
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (shop.Offers[i].bGuaranteed) guaranteed++;
        }

        Assert.AreEqual(4, shop.Offers.Count);
        Assert.AreEqual(2, guaranteed);
        Assert.AreEqual(1, CountKind(ECardKind.Active));
        Assert.AreEqual(3, CountKind(ECardKind.Passive));
    }

    private int CountKind(ECardKind _kind)
    {
        int count = 0;
        for (int i = 0; i < shop.Offers.Count; i++)
        {
            if (_kind == shop.Offers[i].card.kind) count++;
        }

        return count;
    }

    [Test]
    public void Offers_NeverDuplicate_AndRespectOfferCount()
    {
        List<CardDefinitionSO> cards = new List<CardDefinitionSO>();
        for (int i = 0; i < 10; i++) cards.Add(factory.CreateCard("c" + i, ECardKind.Passive));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        for (int round = 0; round < 50; round++)
        {
            shop.Open(false);
            Assert.AreEqual(4, shop.Offers.Count);

            HashSet<CardDefinitionSO> seen = new HashSet<CardDefinitionSO>();
            for (int i = 0; i < shop.Offers.Count; i++) Assert.IsTrue(seen.Add(shop.Offers[i].card));

            shop.Close();
        }
    }

    [Test]
    public void Emerald_IsGuaranteedEveryOffer_UntilPicked()
    {
        // 액티브는 VI를 보유하면 MAX(VII) 카드가 에메랄드로 보장 노출된다.
        CardDefinitionSO target = factory.CreateCard("target", ECardKind.Active, ERoguelikeSkill.Boomerang);
        List<CardDefinitionSO> cards = new List<CardDefinitionSO> { target };
        for (int i = 0; i < 12; i++) cards.Add(factory.CreateCard("f" + i, ECardKind.Passive));
        InitShop(factory.CreateDatabase(cards.ToArray()));

        for (int i = 0; i < 6; i++) shop.GrantDirect(target);

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
                Assert.AreEqual(7, shop.Offers[i].levelAfterPurchase);
            }

            Assert.IsTrue(bFound, "에메랄드 조건 카드가 제시되지 않았다");
            shop.Close();
        }
    }

    [Test]
    public void Prism_AppearsOnlyAfterBothMaterialsMax()
    {
        CardDefinitionSO a = factory.CreateCard("a", ECardKind.Active, ERoguelikeSkill.Spin);
        CardDefinitionSO b = factory.CreateCard("b", ECardKind.Active, ERoguelikeSkill.Shockwave);
        CardDefinitionSO prism = factory.CreatePrism("prism", a, b, ERoguelikeCombo.SpinShockwave);
        CardDefinitionSO filler = factory.CreateCard("filler", ECardKind.Passive);
        InitShop(factory.CreateDatabase(a, b, prism, filler));

        for (int i = 0; i < 7; i++) shop.GrantDirect(a);
        for (int i = 0; i < 6; i++) shop.GrantDirect(b);

        shop.Open(false);
        for (int i = 0; i < shop.Offers.Count; i++) Assert.AreNotEqual(prism, shop.Offers[i].card);
        shop.Close();

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
    public void AllCardsMax_NoOffers()
    {
        CardDefinitionSO a = factory.CreateCard("a", ECardKind.Passive);
        CardDefinitionSO r = factory.CreateCard("r", ECardKind.Ruby);
        InitShop(factory.CreateDatabase(a, r));
        for (int i = 0; i < 5; i++) shop.GrantDirect(a);
        shop.GrantDirect(r);

        // 노출 가능한 카드가 0장이면 제시도 0장이다. MAX·보유 계약으로 칸을 채우지 않는다(기획서 4-2 "카드 풀 예외 처리").
        // 이때 런 흐름은 안내 신호를 보내고 확인 입력 없이 웨이브를 시작한다(RoguelikeSystem.CardSelectRoutine).
        shop.Open(false);
        Assert.AreEqual(0, shop.Offers.Count);
    }

    [Test]
    public void Pick_RejectedWhenClosedOrInvalidIndex_StateUnchanged()
    {
        CardDefinitionSO p = factory.CreateCard("p", ECardKind.Passive);
        InitShop(factory.CreateDatabase(p));

        Assert.AreEqual(ECardShopResult.NotOpen, shop.TryBuy(0, out _, null));

        shop.Open(false);
        Assert.AreEqual(ECardShopResult.InvalidIndex, shop.TryBuy(5, out _, null));
        Assert.IsTrue(shop.IsOpen);
        Assert.IsFalse(build.IsOwned(p));
    }
}
