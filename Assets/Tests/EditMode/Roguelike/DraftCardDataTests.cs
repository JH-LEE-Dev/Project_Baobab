using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

/// <summary>Draft 카드 에셋의 레벨 효과가 기획서 v2.4 6-1·부록 A-4(액티브 7레벨)·A-5(공통 패시브 5레벨)의 누적 값과 맞는지.</summary>
public class DraftCardDataTests
{
    private const string DraftFolder = "Assets/Data/Roguelike/Cards/Draft/";

    private static CardDefinitionSO Load(string _cardName)
    {
        CardDefinitionSO card = AssetDatabase.LoadAssetAtPath<CardDefinitionSO>(DraftFolder + _cardName + ".asset");
        Assert.IsNotNull(card, _cardName);
        return card;
    }

    private static RunModifiers ResolveAtLevel(string _cardName, int _level)
    {
        CardDefinitionSO card = Load(_cardName);

        RunBuildState build = new RunBuildState();
        for (int i = 0; i < _level; i++) Assert.IsTrue(build.Acquire(card), _cardName + " " + (i + 1));

        RunModifiers mods = new RunModifiers();
        RunEffectResolver.Resolve(build, mods, new List<CardEffectEntry>());
        return mods;
    }

    [Test]
    public void DraftDatabase_Has25CardsInPlannedKinds()
    {
        // 기획서 v2.4: 액티브 7 · 조합 5 · 공통 패시브 10 · 계약 3.
        CardDatabaseSO db = AssetDatabase.LoadAssetAtPath<CardDatabaseSO>("Assets/Data/Roguelike/RoguelikeCardDatabase_Draft.asset");
        int[] counts = new int[4];
        for (int i = 0; i < db.Count; i++) counts[(int)db.Get(i).kind]++;

        Assert.AreEqual(25, db.Count);
        Assert.AreEqual(7, counts[(int)ECardKind.Active]);
        Assert.AreEqual(10, counts[(int)ECardKind.Passive]);
        Assert.AreEqual(3, counts[(int)ECardKind.Ruby]);
        Assert.AreEqual(5, counts[(int)ECardKind.Prism]);
    }

    [Test]
    public void Actives_HaveSevenLevelEntries()
    {
        foreach (string name in new[] { "active_boomerang", "active_drone", "active_offroad", "active_shockwave", "active_spin", "active_sporebomb", "active_stella" })
        {
            CardDefinitionSO card = Load(name);
            Assert.AreEqual(7, card.EffectiveMaxLevel, name);
            Assert.AreEqual(7, card.levels.Count, name);
        }
    }

    [Test]
    public void Prisms_HaveExclusiveGroupsFromSection54()
    {
        // 스텔라 조합(스텔라+포자폭탄, 스텔라+드론)과 삼각 조합(회전베기+충격파, 부메랑+충격파, 회전베기+부메랑)은 각각 한 판에 한 장.
        Assert.AreEqual(Load("combo_stella_spore").exclusiveGroup, Load("combo_stella_drone").exclusiveGroup);
        Assert.AreEqual(Load("combo_spin_shockwave").exclusiveGroup, Load("combo_boomerang_shockwave").exclusiveGroup);
        Assert.AreEqual(Load("combo_spin_shockwave").exclusiveGroup, Load("combo_spin_boomerang").exclusiveGroup);
        Assert.AreNotEqual(0, Load("combo_stella_spore").exclusiveGroup);
        Assert.AreNotEqual(0, Load("combo_spin_shockwave").exclusiveGroup);
        Assert.AreNotEqual(Load("combo_stella_spore").exclusiveGroup, Load("combo_spin_shockwave").exclusiveGroup);
    }

    [Test]
    public void PassiveMaxConditionalEffects_OnlyAtV()
    {
        Assert.AreEqual(0f, ResolveAtLevel("passive_damage", 4).Get(ERunEffect.DamagePercentAboveThreshold));
        Assert.AreEqual(20f, ResolveAtLevel("passive_damage", 5).Get(ERunEffect.DamagePercentAboveThreshold));
        Assert.AreEqual(0.3f, ResolveAtLevel("passive_duration", 5).Get(ERunEffect.DurationExtendOnDotKill), 0.0001f);
        Assert.AreEqual(10f, ResolveAtLevel("passive_movespeed", 5).Get(ERunEffect.AttackSpeedPercentWhileMoving));
    }

    [Test]
    public void ContractMoreTheMerrier_OneMoreOfEverything()
    {
        RunModifiers mods = ResolveAtLevel("ruby_more_the_merrier", 1);
        Assert.AreEqual(1f, mods.Get(ERunEffect.AxeExtraStrikeAdd));
        Assert.AreEqual(1f, mods.GetForSkill(ERunEffect.ShockwaveRepeatAdd, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(1f, mods.GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Boomerang));
        Assert.Greater(mods.Get(ERunEffect.TreeSpawnRatePercent), 0f);
    }

    [Test]
    public void Offroad_CarsOneTwoFourSix_IntervalAndSpeed()
    {
        // 1 → 2(III) → 4(V) → 6(MAX)대.
        Assert.AreEqual(0f, ResolveAtLevel("active_offroad", 2).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Offroad));
        Assert.AreEqual(1f, ResolveAtLevel("active_offroad", 3).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Offroad));
        Assert.AreEqual(3f, ResolveAtLevel("active_offroad", 5).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Offroad));
        Assert.AreEqual(5f, ResolveAtLevel("active_offroad", 7).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Offroad));

        // 주기 6 → 5.5(II) → 5.0(VI)초, 피해 30 → 33(IV) → 34(VI), 돌진 속도 +20%(IV).
        Assert.AreEqual(5.5f, 6f / (1f + ResolveAtLevel("active_offroad", 2).GetForSkill(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Offroad) / 100f), 0.01f);
        Assert.AreEqual(5f, 6f / (1f + ResolveAtLevel("active_offroad", 6).GetForSkill(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Offroad) / 100f), 0.01f);
        Assert.AreEqual(33f, 30f * (1f + ResolveAtLevel("active_offroad", 4).GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Offroad) / 100f), 0.01f);
        Assert.AreEqual(34f, 30f * (1f + ResolveAtLevel("active_offroad", 6).GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Offroad) / 100f), 0.01f);
        Assert.AreEqual(20f, ResolveAtLevel("active_offroad", 4).Get(ERunEffect.MoveSpeedPercent, ERoguelikeSkill.Offroad), 0.01f);
        Assert.AreEqual(0f, ResolveAtLevel("active_offroad", 7).Get(ERunEffect.MoveSpeedPercent), "오프로드 돌진 속도는 플레이어 이동속도가 아니다");
    }

    [Test]
    public void Spin_FrequencyLadderThreeTwoOneAlways_NoDoubleRotation()
    {
        int[] required = { 3, 3, 2, 2, 1, 1, 0 };
        for (int level = 1; level <= 7; level++)
        {
            float add = ResolveAtLevel("active_spin", level).GetForSkill(ERunEffect.TriggerHitCountAdd, ERoguelikeSkill.Spin);
            Assert.AreEqual(required[level - 1], 3 + (int)add, "레벨 " + level);
        }

        RunModifiers vi = ResolveAtLevel("active_spin", 6);
        Assert.AreEqual(70f, vi.GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Spin), 0.01f);   // 회전 피해 ×1.7
        Assert.AreEqual(50f, vi.GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Spin), 0.01f);    // 범위 +50%
        Assert.AreEqual(50f, ResolveAtLevel("active_spin", 4).GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Spin), 0.01f); // ×1.5
    }

    [Test]
    public void Shockwave_Chance50To75To100_DamageAndDoubleAtMax()
    {
        Assert.AreEqual(50f, ResolveAtLevel("active_shockwave", 1).GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(75f, ResolveAtLevel("active_shockwave", 3).GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(100f, ResolveAtLevel("active_shockwave", 5).GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Shockwave));

        Assert.AreEqual(9f, 8f * (1f + ResolveAtLevel("active_shockwave", 2).GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Shockwave) / 100f), 0.01f);
        Assert.AreEqual(12f, 8f * (1f + ResolveAtLevel("active_shockwave", 4).GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Shockwave) / 100f), 0.01f);
        Assert.AreEqual(14f, 8f * (1f + ResolveAtLevel("active_shockwave", 6).GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Shockwave) / 100f), 0.01f);

        Assert.AreEqual(0f, ResolveAtLevel("active_shockwave", 6).GetForSkill(ERunEffect.ShockwaveRepeatAdd, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(1f, ResolveAtLevel("active_shockwave", 7).GetForSkill(ERunEffect.ShockwaveRepeatAdd, ERoguelikeSkill.Shockwave));
    }

    [Test]
    public void Drone_CountChainFromVAndInterval()
    {
        Assert.AreEqual(0f, ResolveAtLevel("active_drone", 4).GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone), "체인은 V부터");
        Assert.AreEqual(1f, ResolveAtLevel("active_drone", 3).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Drone));     // 2기
        Assert.AreEqual(1f, ResolveAtLevel("active_drone", 5).GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone)); // 1연쇄
        Assert.AreEqual(2f, ResolveAtLevel("active_drone", 6).GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone)); // 2연쇄

        RunModifiers max = ResolveAtLevel("active_drone", 7);
        Assert.AreEqual(2f, max.GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Drone));      // 3기
        Assert.AreEqual(3f, max.GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone)); // 3연쇄

        float[] intervals = { 1f, 0.8f, 0.8f, 0.7f, 0.7f, 0.6f, 0.6f };
        float[] damages = { 7f, 9f, 9f, 12f, 12f, 12f, 12f };
        for (int level = 1; level <= 7; level++)
        {
            RunModifiers mods = ResolveAtLevel("active_drone", level);
            Assert.AreEqual(intervals[level - 1], 1f / (1f + mods.GetForSkill(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Drone) / 100f), 0.005f, "주기 " + level);
            Assert.AreEqual(damages[level - 1], 7f * (1f + mods.GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Drone) / 100f), 0.01f, "피해 " + level);
        }
    }

    [Test]
    public void Boomerang_TwoAtIII_FasterTicksAtV_ThreeAtMax()
    {
        Assert.AreEqual(1f, ResolveAtLevel("active_boomerang", 3).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Boomerang));
        Assert.AreEqual(5f, 4f * (1f + ResolveAtLevel("active_boomerang", 4).GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Boomerang) / 100f), 0.01f);
        Assert.AreEqual(0.25f, 0.3f / (1f + ResolveAtLevel("active_boomerang", 5).GetForSkill(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.Boomerang) / 100f), 0.001f);
        Assert.AreEqual(2f, ResolveAtLevel("active_boomerang", 7).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Boomerang));
    }

    [Test]
    public void SporeBomb_ContaminationDurationBombsIntervalAndStacks()
    {
        float[] durations = { 4f, 5f, 5f, 5f, 5f, 6f, 6f };
        for (int level = 1; level <= 7; level++)
        {
            float duration = 4f * (1f + ResolveAtLevel("active_sporebomb", level).GetForSkill(ERunEffect.DurationPercent, ERoguelikeSkill.SporeBomb) / 100f);
            Assert.AreEqual(durations[level - 1], duration, 0.001f, "오염 유지 " + level);
        }

        Assert.AreEqual(1f, ResolveAtLevel("active_sporebomb", 3).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.SporeBomb)); // 2개 투하

        RunModifiers iv = ResolveAtLevel("active_sporebomb", 4);
        Assert.AreEqual(5f, 4f * (1f + iv.GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.SporeBomb) / 100f), 0.01f);     // 틱 5
        Assert.AreEqual(4f, 5f / (1f + iv.GetForSkill(ERunEffect.AttackSpeedPercent, ERoguelikeSkill.SporeBomb) / 100f), 0.01f); // 투하 4초

        Assert.AreEqual(1f, 1f + ResolveAtLevel("active_sporebomb", 4).GetForSkill(ERunEffect.SporeStackAdd, ERoguelikeSkill.SporeBomb));
        Assert.AreEqual(2f, 1f + ResolveAtLevel("active_sporebomb", 5).GetForSkill(ERunEffect.SporeStackAdd, ERoguelikeSkill.SporeBomb));
        Assert.AreEqual(3f, 1f + ResolveAtLevel("active_sporebomb", 7).GetForSkill(ERunEffect.SporeStackAdd, ERoguelikeSkill.SporeBomb));
    }

    [Test]
    public void Stella_ChanceOtherActivesMarkDurationMarkDamageAndDoubleBlast()
    {
        float[] chances = { 5f, 8f, 8f, 8f, 10f, 12f, 12f };
        for (int level = 1; level <= 7; level++)
        {
            Assert.AreEqual(chances[level - 1], 5f + ResolveAtLevel("active_stella", level).GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Stella), 0.001f, "확률 " + level);
        }

        Assert.AreEqual(0f, ResolveAtLevel("active_stella", 2).GetForSkill(ERunEffect.StarHitByActives, ERoguelikeSkill.Stella));
        Assert.Greater(ResolveAtLevel("active_stella", 3).GetForSkill(ERunEffect.StarHitByActives, ERoguelikeSkill.Stella), 0f);

        RunModifiers iv = ResolveAtLevel("active_stella", 4);
        Assert.AreEqual(9f, 6f + iv.GetForSkill(ERunEffect.StarMarkDurationAdd, ERoguelikeSkill.Stella));
        Assert.AreEqual(115f, 100f * (1f + iv.GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Stella) / 100f), 0.01f);

        // 표식 나무의 받는 피해 +30% → +45%(V).
        Assert.AreEqual(30f, 30f + ResolveAtLevel("active_stella", 4).GetForSkill(ERunEffect.StarMarkDamageTakenAdd, ERoguelikeSkill.Stella));
        Assert.AreEqual(45f, 30f + ResolveAtLevel("active_stella", 5).GetForSkill(ERunEffect.StarMarkDamageTakenAdd, ERoguelikeSkill.Stella));

        Assert.AreEqual(0f, ResolveAtLevel("active_stella", 6).GetForSkill(ERunEffect.StarBlastRepeatAdd, ERoguelikeSkill.Stella));
        Assert.AreEqual(1f, ResolveAtLevel("active_stella", 7).GetForSkill(ERunEffect.StarBlastRepeatAdd, ERoguelikeSkill.Stella));
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
    }
}
