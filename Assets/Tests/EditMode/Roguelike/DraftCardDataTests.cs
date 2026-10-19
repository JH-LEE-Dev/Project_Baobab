using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

/// <summary>Draft 카드 에셋의 레벨 효과가 기획서 v2.1 부록 A-4(액티브)·A-5(공통 패시브)의 누적 값과 맞는지.</summary>
public class DraftCardDataTests
{
    private const string DraftFolder = "Assets/Data/Roguelike/Cards/Draft/";

    private static RunModifiers ResolveAtLevel(string _cardName, int _level)
    {
        CardDefinitionSO card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(DraftFolder + _cardName + ".asset");
        Assert.IsNotNull(card, _cardName);

        RunBuildState build = new RunBuildState();
        for (int i = 0; i < _level; i++) build.Acquire(card);

        RunModifiers mods = new RunModifiers();
        RunEffectResolver.Resolve(build, mods, new List<CardEffectEntry>());
        return mods;
    }

    [Test]
    public void DraftDatabase_Has28CardsInPlannedKinds()
    {
        CardDatabaseSO db = AssetDatabase.LoadAssetAtPath<CardDatabaseSO>("Assets/Data/Roguelike/RoguelikeCardDatabase_Draft.asset");
        int[] counts = new int[4];
        for (int i = 0; i < db.Count; i++) counts[(int)db.Get(i).kind]++;

        Assert.AreEqual(28, db.Count);
        Assert.AreEqual(7, counts[(int)ECardKind.Active]);
        Assert.AreEqual(11, counts[(int)ECardKind.Passive]);
        Assert.AreEqual(5, counts[(int)ECardKind.Ruby]);
        Assert.AreEqual(5, counts[(int)ECardKind.Prism]);
    }

    [Test]
    public void PassiveMaxConditionalEffects_OnlyAtV()
    {
        Assert.AreEqual(0f, ResolveAtLevel("passive_damage", 4).Get(ERunEffect.DamagePercentAboveThreshold));
        Assert.AreEqual(20f, ResolveAtLevel("passive_damage", 5).Get(ERunEffect.DamagePercentAboveThreshold));
        Assert.AreEqual(0.3f, ResolveAtLevel("passive_duration", 5).Get(ERunEffect.DurationExtendOnDotKill), 0.0001f);
        Assert.AreEqual(10f, ResolveAtLevel("passive_movespeed", 5).Get(ERunEffect.AttackSpeedPercentWhileMoving));
        Assert.AreEqual(25f, ResolveAtLevel("passive_gold", 5).Get(ERunEffect.GoldPerWaveClear));
        Assert.AreEqual(0f, ResolveAtLevel("passive_gold", 4).Get(ERunEffect.GoldPerWaveClear));
    }

    [Test]
    public void RubyMoreTheMerrier_OneMoreOfEverything()
    {
        RunModifiers mods = ResolveAtLevel("ruby_more_the_merrier", 1);
        Assert.AreEqual(1f, mods.Get(ERunEffect.AxeExtraStrikeAdd));
        Assert.AreEqual(1f, mods.GetForSkill(ERunEffect.ShockwaveRepeatAdd, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(1f, mods.GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Boomerang));
        Assert.Greater(mods.Get(ERunEffect.TreeSpawnRatePercent), 0f);
    }

    [Test]
    public void Actives_AreSilver()
    {
        foreach (string name in new[] { "active_boomerang", "active_drone", "active_offroad", "active_shockwave", "active_spin", "active_sporebomb", "active_stella" })
        {
            CardDefinitionSO card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(DraftFolder + name + ".asset");
            Assert.AreEqual(ECardGrade.Silver, card.grade, name);
        }
    }

    [Test]
    public void Offroad_IntervalTwoAndThreeCarsAndWiderPath()
    {
        Assert.AreEqual(4.5f, 6f / (1f + ResolveAtLevel("active_offroad", 2).GetForSkill(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Offroad) / 100f), 0.01f);
        Assert.AreEqual(1f, ResolveAtLevel("active_offroad", 3).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Offroad));  // 2대
        Assert.AreEqual(25f, ResolveAtLevel("active_offroad", 4).GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Offroad));
        Assert.AreEqual(2f, ResolveAtLevel("active_offroad", 5).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Offroad));  // 3대
    }

    [Test]
    public void Spin_TwoSwingsAtII_DoubleRotationAtIV_EverySwingAtV()
    {
        Assert.AreEqual(-1f, ResolveAtLevel("active_spin", 2).GetForSkill(ERunEffect.TriggerHitCountAdd, ERoguelikeSkill.Spin));
        Assert.AreEqual(0f, ResolveAtLevel("active_spin", 3).GetForSkill(ERunEffect.SpinRotationAdd, ERoguelikeSkill.Spin));
        Assert.AreEqual(1f, ResolveAtLevel("active_spin", 4).GetForSkill(ERunEffect.SpinRotationAdd, ERoguelikeSkill.Spin));

        RunModifiers max = ResolveAtLevel("active_spin", 5);
        Assert.AreEqual(-2f, max.GetForSkill(ERunEffect.TriggerHitCountAdd, ERoguelikeSkill.Spin)); // 3휘두름 - 2 = 매 휘두름
        Assert.AreEqual(25f, max.GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Spin));
    }

    [Test]
    public void Shockwave_Chance50Then100_Damage12AtIV_DoubleAtV()
    {
        Assert.AreEqual(50f, ResolveAtLevel("active_shockwave", 1).GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(100f, ResolveAtLevel("active_shockwave", 3).GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(50f, ResolveAtLevel("active_shockwave", 4).GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(0f, ResolveAtLevel("active_shockwave", 4).GetForSkill(ERunEffect.ShockwaveRepeatAdd, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(1f, ResolveAtLevel("active_shockwave", 5).GetForSkill(ERunEffect.ShockwaveRepeatAdd, ERoguelikeSkill.Shockwave));
    }

    [Test]
    public void Drone_CountChainAndInterval()
    {
        RunModifiers lv3 = ResolveAtLevel("active_drone", 3);
        Assert.AreEqual(1f, lv3.GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Drone));      // 2기
        Assert.AreEqual(1f, lv3.GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone)); // 총 2그루

        RunModifiers lv4 = ResolveAtLevel("active_drone", 4);
        Assert.AreEqual(2f, lv4.GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone));
        Assert.AreEqual(0.5f, 1f / (1f + lv4.GetForSkill(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Drone) / 100f), 0.001f);

        RunModifiers max = ResolveAtLevel("active_drone", 5);
        Assert.AreEqual(2f, max.GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Drone));      // 3기
        Assert.AreEqual(4f, max.GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone)); // 총 5그루
    }

    [Test]
    public void Boomerang_TickSixAtIII_ThreeAtV()
    {
        Assert.AreEqual(50f, ResolveAtLevel("active_boomerang", 3).GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Boomerang)); // 틱 4 -> 6
        Assert.AreEqual(1f, ResolveAtLevel("active_boomerang", 4).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Boomerang));
        Assert.AreEqual(2f, ResolveAtLevel("active_boomerang", 5).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Boomerang));
    }

    [Test]
    public void SporeBomb_FiveSecondsAtII_TwoBombsAtIV_StackThreeAtV()
    {
        Assert.AreEqual(5f, 4f * (1f + ResolveAtLevel("active_sporebomb", 2).GetForSkill(ERunEffect.DurationPercent, ERoguelikeSkill.SporeBomb) / 100f), 0.001f);
        Assert.AreEqual(1f, ResolveAtLevel("active_sporebomb", 4).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.SporeBomb));
        Assert.AreEqual(0f, ResolveAtLevel("active_sporebomb", 4).GetForSkill(ERunEffect.SporeStackAdd, ERoguelikeSkill.SporeBomb));
        Assert.AreEqual(3f, 1f + ResolveAtLevel("active_sporebomb", 5).GetForSkill(ERunEffect.SporeStackAdd, ERoguelikeSkill.SporeBomb));
    }

    [Test]
    public void Stella_ChanceOtherActivesMarkDurationAndDoubleBlast()
    {
        Assert.AreEqual(8f, 5f + ResolveAtLevel("active_stella", 2).GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Stella));
        Assert.AreEqual(0f, ResolveAtLevel("active_stella", 2).GetForSkill(ERunEffect.StarHitByActives, ERoguelikeSkill.Stella));

        RunModifiers lv3 = ResolveAtLevel("active_stella", 3);
        Assert.AreEqual(12f, 5f + lv3.GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Stella));
        Assert.Greater(lv3.GetForSkill(ERunEffect.StarHitByActives, ERoguelikeSkill.Stella), 0f);

        Assert.AreEqual(9f, 6f + ResolveAtLevel("active_stella", 4).GetForSkill(ERunEffect.StarMarkDurationAdd, ERoguelikeSkill.Stella));
        Assert.AreEqual(1f, ResolveAtLevel("active_stella", 5).GetForSkill(ERunEffect.StarBlastRepeatAdd, ERoguelikeSkill.Stella));
    }

    [Test]
    public void Passives_MaxCumulativeMatchesAppendixA5()
    {
        Assert.AreEqual(50f, ResolveAtLevel("passive_range", 5).Get(ERunEffect.RangePercent), 0.001f);
        Assert.AreEqual(100f, ResolveAtLevel("passive_damage", 5).Get(ERunEffect.DamagePercent), 0.001f);
        Assert.AreEqual(46f, ResolveAtLevel("passive_damage", 3).Get(ERunEffect.DamagePercent), 0.001f);
        Assert.AreEqual(60f, ResolveAtLevel("passive_attackspeed", 5).Get(ERunEffect.AttackSpeedPercent), 0.001f);
        Assert.AreEqual(30f, ResolveAtLevel("passive_movespeed", 5).Get(ERunEffect.MoveSpeedPercent), 0.001f);
        Assert.AreEqual(25f, ResolveAtLevel("passive_stamina_recover", 5).Get(ERunEffect.StaminaRecoverPerWave), 0.001f);
        Assert.AreEqual(50f, ResolveAtLevel("passive_max_stamina", 5).Get(ERunEffect.MaxStaminaAdd), 0.001f);
        Assert.AreEqual(4f, ResolveAtLevel("passive_grace", 5).Get(ERunEffect.GraceTimeAdd), 0.001f);
        Assert.AreEqual(3f, ResolveAtLevel("passive_damage_cooldown", 5).Get(ERunEffect.DamageCooldownAdd), 0.001f);
        Assert.AreEqual(52f, ResolveAtLevel("passive_gold", 5).Get(ERunEffect.GoldGainPercent), 0.001f);
    }
}
