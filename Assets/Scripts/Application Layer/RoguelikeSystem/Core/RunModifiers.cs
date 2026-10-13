using System;
using System.Collections.Generic;

/// <summary>
/// 현재 빌드가 주는 효과의 합계. 카드를 살 때마다 RunEffectResolver가 빌드 전체에서 처음부터 다시 계산한다.
///
/// "산 카드 효과를 하나씩 더하는" 방식이 아니라 "빌드 → 합계"를 매번 새로 만드는 이유는 조합 카드 때문이다.
/// 조합을 사면 재료 스킬의 기본 능력이 꺼지는데(기획서 3-3), 이것을 증분으로 처리하면 순서·되돌리기
/// 규칙이 복잡해진다. 합계를 새로 만들면 "보유 중이고, 그 스킬을 끄는 조합이 없으면 켜짐" 하나로 끝난다.
/// </summary>
public sealed class RunModifiers
{
    private static readonly int EffectCount = (int)ERunEffect.Count;
    private static readonly int SkillCount = Enum.GetValues(typeof(ERoguelikeSkill)).Length;
    private static readonly int ComboCount = Enum.GetValues(typeof(ERoguelikeCombo)).Length;

    // [effect * SkillCount + skill] = 합계. skill 0(None)은 전역 효과 칸이다.
    private readonly float[] values = new float[EffectCount * SkillCount];
    private readonly bool[] suppressedSkills = new bool[SkillCount];
    private readonly bool[] activeCombos = new bool[ComboCount];

    public void Clear()
    {
        Array.Clear(values, 0, values.Length);
        Array.Clear(suppressedSkills, 0, suppressedSkills.Length);
        Array.Clear(activeCombos, 0, activeCombos.Length);
    }

    public void CopyFrom(RunModifiers _other)
    {
        Array.Copy(_other.values, values, values.Length);
        Array.Copy(_other.suppressedSkills, suppressedSkills, suppressedSkills.Length);
        Array.Copy(_other.activeCombos, activeCombos, activeCombos.Length);
    }

    /// <summary>특정 칸의 합계. 전역 값은 _skill=None.</summary>
    public float Get(ERunEffect _effect, ERoguelikeSkill _skill = ERoguelikeSkill.None)
    {
        return values[Index(_effect, _skill)];
    }

    /// <summary>전역 값 + 그 스킬 전용 값. 스킬 하나에 실제로 걸리는 수치를 구할 때 쓴다.</summary>
    public float GetForSkill(ERunEffect _effect, ERoguelikeSkill _skill)
    {
        if (ERoguelikeSkill.None == _skill) return Get(_effect);
        return Get(_effect) + Get(_effect, _skill);
    }

    public bool IsOn(ERunEffect _effect) => Get(_effect) > 0f;

    /// <summary>스킬을 획득했고, 그 기본 능력을 끄는 조합 카드도 없다.</summary>
    public bool IsSkillActive(ERoguelikeSkill _skill)
    {
        if (ERoguelikeSkill.None == _skill) return false;
        return IsSkillUnlocked(_skill) && false == suppressedSkills[(int)_skill];
    }

    /// <summary>조합 때문에 기본 능력이 꺼졌더라도 스킬 자체는 보유 중인지.</summary>
    public bool IsSkillUnlocked(ERoguelikeSkill _skill)
    {
        if (ERoguelikeSkill.None == _skill) return false;
        return Get(ERunEffect.UnlockSkill, _skill) > 0f;
    }

    public bool IsSkillSuppressed(ERoguelikeSkill _skill) => suppressedSkills[(int)_skill];

    public bool IsComboActive(ERoguelikeCombo _combo)
    {
        if (ERoguelikeCombo.None == _combo) return false;
        return activeCombos[(int)_combo];
    }

    public void Add(in CardEffectEntry _entry)
    {
        values[Index(_entry.effect, _entry.skill)] += _entry.value;
    }

    public void SetSuppressed(ERoguelikeSkill _skill)
    {
        if (ERoguelikeSkill.None == _skill) return;
        suppressedSkills[(int)_skill] = true;
    }

    public void SetComboActive(ERoguelikeCombo _combo)
    {
        if (ERoguelikeCombo.None == _combo) return;
        activeCombos[(int)_combo] = true;
    }

    private static int Index(ERunEffect _effect, ERoguelikeSkill _skill)
    {
        return (int)_effect * SkillCount + (int)_skill;
    }
}

/// <summary>빌드 상태에서 RunModifiers를 만든다.</summary>
public static class RunEffectResolver
{
    public static void Resolve(RunBuildState _build, RunModifiers _result, List<CardEffectEntry> _scratch)
    {
        _result.Clear();

        IReadOnlyList<CardDefinitionSO> owned = _build.OwnedCards;
        for (int i = 0; i < owned.Count; i++)
        {
            CardDefinitionSO card = owned[i];
            if (null == card) continue;

            // 액티브 카드는 보유하는 것만으로 자기 스킬을 얻는다. 데이터에 UnlockSkill을 빠뜨려도 동작하도록 여기서 보장한다.
            if (ECardKind.Active == card.kind && ERoguelikeSkill.None != card.skillTag)
            {
                CardEffectEntry unlock = new CardEffectEntry(ERunEffect.UnlockSkill, card.skillTag, 1f);
                _result.Add(in unlock);
            }

            _scratch.Clear();
            card.CollectEffects(_build.GetLevel(card), _scratch);

            for (int j = 0; j < _scratch.Count; j++)
            {
                CardEffectEntry entry = _scratch[j];
                _result.Add(in entry);
            }

            if (ECardKind.Prism == card.kind)
            {
                _result.SetComboActive(card.comboType);

                if (null != card.suppressedSkills)
                {
                    for (int j = 0; j < card.suppressedSkills.Count; j++)
                    {
                        _result.SetSuppressed(card.suppressedSkills[j]);
                    }
                }
            }
        }

        _scratch.Clear();
    }
}
