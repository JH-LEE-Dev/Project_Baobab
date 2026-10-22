using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

/// <summary>Draft 카드 에셋의 레벨 효과가 기획서 v2.5 6-1·부록 A-4(액티브 7레벨)·A-5(공통 패시브 5레벨)의 누적 값과 맞는지.</summary>
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
    public void DraftDatabase_Has28CardsInPlannedKinds()
    {
        // 기획서 v2.5: 액티브 7 · 조합 7 · 공통 패시브 11 · 계약 3.
        CardDatabaseSO db = AssetDatabase.LoadAssetAtPath<CardDatabaseSO>("Assets/Data/Roguelike/RoguelikeCardDatabase_Draft.asset");
        int[] counts = new int[4];
        for (int i = 0; i < db.Count; i++) counts[(int)db.Get(i).kind]++;

        Assert.AreEqual(28, db.Count);
        Assert.AreEqual(7, counts[(int)ECardKind.Active]);
        Assert.AreEqual(11, counts[(int)ECardKind.Passive]);
        Assert.AreEqual(3, counts[(int)ECardKind.Ruby]);
        Assert.AreEqual(7, counts[(int)ECardKind.Prism]);
    }

    [Test]
    public void SporeCombos_ReplaceSporeThrow_WithMaterials()
    {
        CardDefinitionSO spore = Load("active_sporebomb");
        string[] names = { "combo_spore_drone", "combo_spore_offroad", "combo_spore_shockwave" };
        string[] partners = { "active_drone", "active_offroad", "active_shockwave" };
        ERoguelikeCombo[] combos = { ERoguelikeCombo.SporeDrone, ERoguelikeCombo.SporeOffroad, ERoguelikeCombo.SporeShockwave };

        for (int i = 0; i < names.Length; i++)
        {
            CardDefinitionSO prism = Load(names[i]);
            Assert.AreEqual(ECardKind.Prism, prism.kind, names[i]);
            Assert.AreEqual(combos[i], prism.comboType, names[i]);
            Assert.IsTrue(prism.IsComboMaterial(spore), names[i]);
            Assert.IsTrue(prism.IsComboMaterial(Load(partners[i])), names[i]);
            // 포자 던지기(투하 · 대폭발 · 오염)만 끄고 상대 재료의 기본 능력은 유지한다.
            CollectionAssert.AreEqual(new[] { ERoguelikeSkill.SporeBomb }, prism.suppressedSkills, names[i]);
        }
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
        // 포자 조합(포자 수류탄 + 드론 / 오프로드 / 충격파)과 삼각 조합(회전베기+충격파, 부메랑+충격파, 회전베기+부메랑)은 각각 한 판에 한 장.
        Assert.AreEqual(Load("combo_spore_drone").exclusiveGroup, Load("combo_spore_offroad").exclusiveGroup);
        Assert.AreEqual(Load("combo_spore_drone").exclusiveGroup, Load("combo_spore_shockwave").exclusiveGroup);
        Assert.AreEqual(Load("combo_spin_shockwave").exclusiveGroup, Load("combo_boomerang_shockwave").exclusiveGroup);
        Assert.AreEqual(Load("combo_spin_shockwave").exclusiveGroup, Load("combo_spin_boomerang").exclusiveGroup);
        Assert.AreNotEqual(0, Load("combo_spore_drone").exclusiveGroup);
        Assert.AreNotEqual(0, Load("combo_spin_shockwave").exclusiveGroup);
        Assert.AreNotEqual(Load("combo_spore_drone").exclusiveGroup, Load("combo_spin_shockwave").exclusiveGroup);
        // 스텔라 조합 그룹은 해소됐다. 재료가 겹치는 조합(포자 + 드론 ↔ 스텔라 + 드론, 포자 + 충격파 ↔ 삼각 조합)은 재료 겹침으로 막힌다.
        Assert.AreEqual(0, Load("combo_stella_drone").exclusiveGroup);
        Assert.IsTrue(Load("combo_spore_drone").ConflictsWith(Load("combo_stella_drone")));
        Assert.IsTrue(Load("combo_spore_shockwave").ConflictsWith(Load("combo_spin_shockwave")));
        Assert.IsFalse(Load("combo_spore_offroad").ConflictsWith(Load("combo_spin_boomerang")));
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

        // 기획서 v2.6 A-4: 피해 12(I) → 13(II) → 14(IV) → 15(VI), 크기 1.5(I) · 2.5(II) · 3.5(IV) · 5(VI) · 7(VII) = 범위 +10 / +30 / +50 / +80 / +120%
        // (크기 1 = 기본, 크기 +1 = 범위 +20%).
        float[] damages = { 12f, 13f, 13f, 14f, 14f, 15f, 15f };
        float[] ranges = { 10f, 30f, 30f, 50f, 50f, 80f, 120f };
        for (int level = 1; level <= 7; level++)
        {
            RunModifiers mods = ResolveAtLevel("active_shockwave", level);
            Assert.AreEqual(damages[level - 1], 12f * (1f + mods.GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.Shockwave) / 100f), 0.01f, "피해 " + level);
            Assert.AreEqual(ranges[level - 1], mods.GetForSkill(ERunEffect.RangePercent, ERoguelikeSkill.Shockwave), 0.01f, "범위 " + level);
        }

        Assert.AreEqual(0f, ResolveAtLevel("active_shockwave", 6).GetForSkill(ERunEffect.ShockwaveRepeatAdd, ERoguelikeSkill.Shockwave));
        Assert.AreEqual(1f, ResolveAtLevel("active_shockwave", 7).GetForSkill(ERunEffect.ShockwaveRepeatAdd, ERoguelikeSkill.Shockwave));

        // MAX: 허공을 휘둘러도 발동.
        Assert.AreEqual(0f, ResolveAtLevel("active_shockwave", 6).GetForSkill(ERunEffect.ShockwaveFireOnWhiff, ERoguelikeSkill.Shockwave));
        Assert.Greater(ResolveAtLevel("active_shockwave", 7).GetForSkill(ERunEffect.ShockwaveFireOnWhiff, ERoguelikeSkill.Shockwave), 0f);
    }

    [Test]
    public void Drone_CountChainFromVAndInterval()
    {
        Assert.AreEqual(0f, ResolveAtLevel("active_drone", 4).GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone), "체인은 V부터");
        Assert.AreEqual(1f, ResolveAtLevel("active_drone", 3).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Drone));     // 2기
        // 기획서 v2.5 A-4: 체인 타격 수 약 1.5배 - V 2연쇄(레이저당 3그루) · VI 4연쇄(5그루) · MAX 5연쇄(6그루).
        Assert.AreEqual(2f, ResolveAtLevel("active_drone", 5).GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone)); // 2연쇄
        Assert.AreEqual(4f, ResolveAtLevel("active_drone", 6).GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone)); // 4연쇄

        RunModifiers max = ResolveAtLevel("active_drone", 7);
        Assert.AreEqual(2f, max.GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Drone));      // 3기
        Assert.AreEqual(5f, max.GetForSkill(ERunEffect.ChainCountAdd, ERoguelikeSkill.Drone)); // 5연쇄

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
    public void SporeBomb_TickIntervalBombsDropIntervalAndStacks()
    {
        // 기획서 v2.5 A-4: 틱 주기 2.0 → 1.5(II) → 1.0(IV) → 0.5(VI)초, 틱 피해 4 고정, 오염 4초 고정.
        float[] tickIntervals = { 2f, 1.5f, 1.5f, 1f, 1f, 0.5f, 0.5f };
        for (int level = 1; level <= 7; level++)
        {
            RunModifiers mods = ResolveAtLevel("active_sporebomb", level);
            Assert.AreEqual(tickIntervals[level - 1], 2f + mods.GetForSkill(ERunEffect.DotTickIntervalAdd, ERoguelikeSkill.SporeBomb), 0.001f, "틱 주기 " + level);
            Assert.AreEqual(0f, mods.GetForSkill(ERunEffect.DamagePercent, ERoguelikeSkill.SporeBomb), "틱 피해 고정 " + level);
            Assert.AreEqual(0f, mods.GetForSkill(ERunEffect.DurationPercent, ERoguelikeSkill.SporeBomb), "오염 유지 고정 " + level);
        }

        Assert.AreEqual(1f, ResolveAtLevel("active_sporebomb", 3).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.SporeBomb)); // 2개 투하
        Assert.AreEqual(2f, ResolveAtLevel("active_sporebomb", 7).GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.SporeBomb)); // 3개 동시 투하

        RunModifiers iv = ResolveAtLevel("active_sporebomb", 4);
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
        // 기획서 v2.6 A-5: 퍼센트 계열은 MAX +200%(레벨마다 +40%), 이동속도는 MAX +60%(레벨마다 +12%).
        Assert.AreEqual(200f, ResolveAtLevel("passive_range", 5).Get(ERunEffect.RangePercent), 0.001f);
        Assert.AreEqual(200f, ResolveAtLevel("passive_damage", 5).Get(ERunEffect.DamagePercent), 0.001f);
        Assert.AreEqual(120f, ResolveAtLevel("passive_damage", 3).Get(ERunEffect.DamagePercent), 0.001f);
        Assert.AreEqual(200f, ResolveAtLevel("passive_attackspeed", 5).Get(ERunEffect.AttackSpeedPercent), 0.001f);
        Assert.AreEqual(200f, ResolveAtLevel("passive_dot", 5).Get(ERunEffect.DotDamagePercent), 0.001f);
        Assert.AreEqual(200f, ResolveAtLevel("passive_duration", 5).Get(ERunEffect.DurationPercent), 0.001f);
        Assert.AreEqual(60f, ResolveAtLevel("passive_movespeed", 5).Get(ERunEffect.MoveSpeedPercent), 0.001f);
        Assert.AreEqual(25f, ResolveAtLevel("passive_stamina_recover", 5).Get(ERunEffect.StaminaRecoverPerWave), 0.001f);
        Assert.AreEqual(50f, ResolveAtLevel("passive_max_stamina", 5).Get(ERunEffect.MaxStaminaAdd), 0.001f);
        Assert.AreEqual(4f, ResolveAtLevel("passive_grace", 5).Get(ERunEffect.GraceTimeAdd), 0.001f);
    }

    [Test]
    public void DotSpeed_TickIntervalReductionLadder_AndMaxStackBonusAtV()
    {
        // 기획서 v2.6 A-5 지속 피해 속도 증가: 틱 주기 −16 / −32 / −48 / −64 / −80%, MAX 조건부(중첩 최대 나무) 추가 −10%.
        float[] reduce = { 16f, 32f, 48f, 64f, 80f };
        for (int level = 1; level <= 5; level++)
        {
            Assert.AreEqual(reduce[level - 1], ResolveAtLevel("passive_dot_speed", level).Get(ERunEffect.DotTickIntervalReducePercent), 0.001f, "레벨 " + level);
        }

        Assert.AreEqual(0f, ResolveAtLevel("passive_dot_speed", 4).Get(ERunEffect.DotTickIntervalReduceAtMaxStackPercent));
        Assert.AreEqual(10f, ResolveAtLevel("passive_dot_speed", 5).Get(ERunEffect.DotTickIntervalReduceAtMaxStackPercent), 0.001f);
        Assert.AreEqual(5, Load("passive_dot_speed").EffectiveMaxLevel);
    }
}
