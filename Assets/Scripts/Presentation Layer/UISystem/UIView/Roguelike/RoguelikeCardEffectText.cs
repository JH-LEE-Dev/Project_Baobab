using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 설명 패널에 보여 줄 카드 옵션 수치 문구를 카드 데이터(CardDefinitionSO.levels)로 만든다.
///
/// - 카드 선택 화면(Build): 구매 후 레벨에서 "이번에 추가되는 효과"만 조립한다.
///   기본 설명(descriptionKey)에 이미 단일/고정 수치가 적힌 루비·균일 패시브의 기본 스탯은 중복을 피하고,
///   확률(ChancePercent) 등 비공개 대상은 생략한다.
/// - 보유 카드 화면(BuildOwned): 1레벨부터 현재 보유 레벨까지 "누적된 효과 합계"를 조립한다.
/// - 템플릿은 RoguelikeUI.json의 Roguelike_Effect_&lt;ERunEffect&gt;[_&lt;ERoguelikeSkill&gt;]이며, {0} 자리에 포맷된 수치를 채운다.
///   힙 할당 없이 재사용 버퍼로 계산한다(키 문자열은 처음 쓸 때 한 번만 만든다).
/// </summary>
public static class RoguelikeCardEffectText
{
    private const string EffectKeyPrefix = "Roguelike_Effect_";
    private const string Placeholder = "{0}";
    private const string ShockwaveCardId = "active_shockwave";
    private const string SpinCardId = "active_spin";
    private const string SpinEveryKey = "Roguelike_Card_active_spin_Desc_Every";
    private const int SpinBaseRequiredSwings = 3;
    private const string PassiveRangeCardId = "passive_range";
    private const string PassiveDamageCardId = "passive_damage";
    private const string PassiveAttackSpeedCardId = "passive_attackspeed";
    private const string PassiveDurationCardId = "passive_duration";
    private const string PassiveDotCardId = "passive_dot";
    private const string PassiveDotSpeedCardId = "passive_dot_speed";
    private const string PassiveMoveSpeedCardId = "passive_movespeed";
    private const string PassiveStaminaRecoverCardId = "passive_stamina_recover";
    private const string PassiveMaxStaminaCardId = "passive_max_stamina";
    private const string PassiveGraceCardId = "passive_grace";
    private const int SlotCapacity = 24;
    private const int SkillCount = 8;

    private struct EffectSlot
    {
        public ERunEffect effect;
        public ERoguelikeSkill skill;
        public float value;
    }

    private static readonly EffectSlot[] slots = new EffectSlot[SlotCapacity];
    private static readonly string[,] effectKeys = new string[(int)ERunEffect.Count, SkillCount];
    private static readonly string[] genericKeys = new string[(int)ERunEffect.Count];
    private static readonly Dictionary<string, string[]> cardKeys = new Dictionary<string, string[]>(32);
    private static readonly Dictionary<string, string[]> extraKeys = new Dictionary<string, string[]>(32);
    private static readonly StringBuilder valueBuilder = new StringBuilder(32);
    private static readonly StringBuilder descBuilder = new StringBuilder(192);
    private static int slotCount;

    /// <summary>
    /// 기본 설명(_description)에 {0} 플레이스홀더가 있을 때 해당 레벨의 주 효과 수치를 채워 돌려준다.
    /// 회전베기는 항상 누적 발동 주기(4→3→2→매 공격마다)를, 차등 패시브는 _bCumulative에 따라 단일 또는 누적 수치를 사용한다.
    /// </summary>
    public static string FormatDescription(CardDefinitionSO _card, int _rawLevel, bool _bCumulative, LocalizationManager _localization, string _description)
    {
        if (true == string.IsNullOrEmpty(_description)) return _description;
        if (null == _card || null == _card.levels || 0 == _card.levels.Count) return _description;

        int level = Mathf.Clamp(_rawLevel, 1, _card.levels.Count);
        if (SpinCardId == _card.cardId)
        {
            return FormatSpinDescription(_card, level, _localization, _description);
        }

        int at = _description.IndexOf(Placeholder, StringComparison.Ordinal);
        if (0 > at) return _description;

        Collect(_card, level, false == _bCumulative);

        for (int i = 0; slotCount > i; i++)
        {
            ref EffectSlot slot = ref slots[i];
            if (false == IsVariablePassivePrimaryEffect(_card.cardId, slot.effect)) continue;

            FormatUnsignedNumeric(slot.effect, slot.value, valueBuilder);
            return ReplacePlaceholder(_description, at, valueBuilder);
        }

        return _description;
    }

    private static string FormatSpinDescription(CardDefinitionSO _card, int _level, LocalizationManager _localization, string _description)
    {
        Collect(_card, _level, false);
        int requiredSwings = SpinBaseRequiredSwings;
        for (int i = 0; slotCount > i; i++)
        {
            if (ERunEffect.TriggerHitCountAdd == slots[i].effect)
            {
                requiredSwings += Mathf.RoundToInt(slots[i].value);
            }
        }

        int interval = Mathf.Max(1, requiredSwings + 1);
        if (1 >= interval && null != _localization)
        {
            string everyText = _localization.GetText(SpinEveryKey);
            if (false == string.IsNullOrEmpty(everyText)) return everyText;
        }

        int at = _description.IndexOf(Placeholder, StringComparison.Ordinal);
        if (0 > at) return _description;

        valueBuilder.Clear();
        valueBuilder.Append(interval);
        return ReplacePlaceholder(_description, at, valueBuilder);
    }

    private static string ReplacePlaceholder(string _template, int _at, StringBuilder _valueText)
    {
        descBuilder.Clear();
        descBuilder.Append(_template, 0, _at);
        descBuilder.Append(_valueText);
        int tailStart = _at + Placeholder.Length;
        if (_template.Length > tailStart)
        {
            descBuilder.Append(_template, tailStart, _template.Length - tailStart);
        }
        return descBuilder.ToString();
    }

    /// <summary>
    /// 카드 선택 화면용: 구매 후 레벨(levelAfterPurchase)에서 이번에 오르는 효과 줄을 _benefit / _demerit에 채운다.
    /// </summary>
    public static void Build(in RoguelikeCardViewData _data, LocalizationManager _localization, StringBuilder _benefit, StringBuilder _demerit, string _separator = "\n")
    {
        BuildCore(_data.card, _data.levelAfterPurchase, _localization, _benefit, _demerit, _separator, true);
    }

    /// <summary>
    /// 보유 카드 화면용: 1레벨부터 현재 레벨(_data.level)까지의 누적 효과 합계를 _benefit / _demerit에 채운다.
    /// </summary>
    public static void BuildOwned(in RoguelikeOwnedCardViewData _data, LocalizationManager _localization, StringBuilder _benefit, StringBuilder _demerit, string _separator = "\n")
    {
        BuildCore(_data.card, _data.level, _localization, _benefit, _demerit, _separator, false);
    }

    private static void BuildCore(
        CardDefinitionSO _card,
        int _rawLevel,
        LocalizationManager _localization,
        StringBuilder _benefit,
        StringBuilder _demerit,
        string _separator,
        bool _bDeltaOnly)
    {
        _benefit.Clear();
        _demerit.Clear();

        if (null == _card || null == _localization || null == _card.levels || 0 == _card.levels.Count) return;

        int level = Mathf.Clamp(_rawLevel, 1, _card.levels.Count);
        string sep = false == string.IsNullOrEmpty(_separator) ? _separator : "\n";

        if (true == _bDeltaOnly)
        {
            string overrideText = _localization.GetText(GetCardKey(_card.cardId, level));
            if (false == string.IsNullOrEmpty(overrideText))
            {
                _benefit.Append(overrideText);
                return;
            }
        }

        if (ECardKind.Ruby == _card.kind || ECardKind.Prism == _card.kind) return;

        string extraText = SpinCardId == _card.cardId ? string.Empty : _localization.GetText(GetExtraKey(_card.cardId, level));
        Collect(_card, level, _bDeltaOnly);

        bool bHasExtra = true == _bDeltaOnly && false == string.IsNullOrEmpty(extraText);
        if (true == bHasExtra && 1 == level)
        {
            _benefit.Append(extraText);
        }

        for (int i = 0; slotCount > i; i++)
        {
            ref EffectSlot slot = ref slots[i];
            if (true == ShouldSkipEffect(_card, slot.effect, slot.value, extraText, _bDeltaOnly)) continue;

            string template = GetTemplate(_localization, slot.effect, slot.skill);
            if (true == string.IsNullOrEmpty(template)) continue;

            StringBuilder target = false == IsBeneficial(slot.effect, slot.value) ? _demerit : _benefit;
            if (0 < target.Length) target.Append(sep);

            FormatValue(slot.effect, slot.skill, slot.value, _localization, valueBuilder);
            AppendFormattedLine(target, template, valueBuilder);
        }

        if (true == bHasExtra && 1 < level)
        {
            if (0 < _benefit.Length) _benefit.Append(sep);
            _benefit.Append(extraText);
        }
    }

    // // 수치 모으기

    /// <summary>_bDeltaOnly가 true면 _level 한 칸만, false면 1~_level 누적 합을 모은다.</summary>
    private static void Collect(CardDefinitionSO _card, int _level, bool _bDeltaOnly)
    {
        slotCount = 0;
        int start = true == _bDeltaOnly ? _level - 1 : 0;

        for (int i = start; _level > i; i++)
        {
            CardLevelData levelData = _card.levels[i];
            if (null == levelData || null == levelData.effects) continue;

            for (int j = 0; levelData.effects.Count > j; j++)
            {
                CardEffectEntry entry = levelData.effects[j];
                int index = FindSlot(entry.effect, entry.skill);
                if (0 > index) continue;

                slots[index].value += entry.value;
            }
        }
    }

    private static int FindSlot(ERunEffect _effect, ERoguelikeSkill _skill)
    {
        for (int i = 0; slotCount > i; i++)
        {
            if (slots[i].effect == _effect && slots[i].skill == _skill) return i;
        }

        if (SlotCapacity <= slotCount) return -1;

        slots[slotCount] = new EffectSlot { effect = _effect, skill = _skill };
        return slotCount++;
    }

    private static bool ShouldSkipEffect(CardDefinitionSO _card, ERunEffect _effect, float _value, string _extraText, bool _bDeltaOnly)
    {
        if (0f == _value || ERunEffect.UnlockSkill == _effect || ERunEffect.ChancePercent == _effect) return true;

        // 레벨별 차등 패시브의 주 효과와 회전베기 타격 주기 감소는 기본 설명(_Desc) 본문에 직접 치환되므로 하단 줄에서 반복하지 않는다.
        if (true == IsVariablePassivePrimaryEffect(_card.cardId, _effect)) return true;
        if (SpinCardId == _card.cardId && ERunEffect.TriggerHitCountAdd == _effect) return true;

        if (ERunEffect.TriggerHitCountAdd == _effect && false == string.IsNullOrEmpty(_extraText)) return true;

        if (false == _bDeltaOnly) return false;

        // 충격파 VII는 2타 분할로 인해 ShockwaveDamageScalePercent 델타가 음수(-381.25)로 기록되어 있어 원시 음수를 숨긴다.
        if (ShockwaveCardId == _card.cardId && ERunEffect.ShockwaveDamageScalePercent == _effect && 0f > _value) return true;

        // 균일 패시브의 주 효과는 기본 설명(_Desc)에 이미 고정 수치가 적혀 있어 카드 선택 화면에서 반복하지 않는다.
        return IsUniformPassivePrimaryEffect(_card.cardId, _effect);
    }

    private static bool IsUniformPassivePrimaryEffect(string _cardId, ERunEffect _effect)
    {
        if (PassiveRangeCardId == _cardId && ERunEffect.RangePercent == _effect) return true;
        if (PassiveDamageCardId == _cardId && ERunEffect.DamagePercent == _effect) return true;
        if (PassiveAttackSpeedCardId == _cardId && ERunEffect.AttackSpeedPercent == _effect) return true;
        if (PassiveDurationCardId == _cardId && ERunEffect.DurationPercent == _effect) return true;
        if (PassiveDotCardId == _cardId && ERunEffect.DotDamagePercent == _effect) return true;
        if (PassiveDotSpeedCardId == _cardId && ERunEffect.DotTickIntervalReducePercent == _effect) return true;
        if (PassiveMoveSpeedCardId == _cardId && ERunEffect.MoveSpeedPercent == _effect) return true;
        return false;
    }

    private static bool IsVariablePassivePrimaryEffect(string _cardId, ERunEffect _effect)
    {
        if (PassiveStaminaRecoverCardId == _cardId && ERunEffect.StaminaRecoverPerWave == _effect) return true;
        if (PassiveMaxStaminaCardId == _cardId && ERunEffect.MaxStaminaAdd == _effect) return true;
        if (PassiveGraceCardId == _cardId && ERunEffect.StaminaLossReducePercent == _effect) return true;
        return false;
    }

    // // 수치 포맷 · 문구 조립

    private static void FormatUnsignedNumeric(ERunEffect _effect, float _value, StringBuilder _out)
    {
        _out.Clear();
        AppendNumericValue(_effect, Mathf.Abs(_value), _out);
    }

    private static void FormatValue(ERunEffect _effect, ERoguelikeSkill _skill, float _value, LocalizationManager _localization, StringBuilder _out)
    {
        _out.Clear();

        if (0f < _value && false == IsCountWithoutSign(_effect, _skill)) _out.Append('+');

        AppendNumericValue(_effect, _value, _out);

        if (true == IsSecondsEffect(_effect))
        {
            string unit = _localization.GetText(LocKeys.RoguelikeUI.roguelikeEffectUnitSeconds);
            _out.Append(false == string.IsNullOrEmpty(unit) ? unit : "s");
            return;
        }

        if (true == IsPercentEffect(_effect))
        {
            _out.Append('%');
        }
    }

    private static bool IsCountWithoutSign(ERunEffect _effect, ERoguelikeSkill _skill)
    {
        if (ERunEffect.ShockwaveRepeatAdd == _effect || ERunEffect.StarBlastRepeatAdd == _effect) return true;
        return ERunEffect.CountAdd == _effect && ERoguelikeSkill.None != _skill;
    }

    private static void AppendNumericValue(ERunEffect _effect, float _value, StringBuilder _out)
    {
        if (true == IsSecondsEffect(_effect))
        {
            AppendTenths(_out, _value);
            return;
        }

        if (true == IsPercentEffect(_effect))
        {
            int twice = Mathf.RoundToInt(_value * 2f);
            if (0 != (twice & 1) && 0.05f >= Mathf.Abs(_value * 2f - twice))
            {
                AppendHalves(_out, twice);
            }
            else
            {
                _out.Append(Mathf.RoundToInt(_value));
            }
            return;
        }

        _out.Append(Mathf.RoundToInt(_value));
    }

    private static void AppendTenths(StringBuilder _out, float _value)
    {
        int tenths = Mathf.RoundToInt(_value * 10f);
        if (0 == tenths % 10)
        {
            _out.Append(tenths / 10);
            return;
        }

        if (0 > tenths)
        {
            _out.Append('-');
            tenths = -tenths;
        }

        _out.Append(tenths / 10).Append('.').Append(tenths % 10);
    }

    private static void AppendHalves(StringBuilder _out, int _twice)
    {
        if (0 > _twice)
        {
            _out.Append('-');
            _twice = -_twice;
        }

        _out.Append(_twice / 2).Append(".5");
    }

    private static bool IsPercentEffect(ERunEffect _effect)
    {
        switch (_effect)
        {
            case ERunEffect.DamagePercent:
            case ERunEffect.ShockwaveDamageScalePercent:
            case ERunEffect.AttackSpeedPercent:
            case ERunEffect.RangePercent:
            case ERunEffect.DurationPercent:
            case ERunEffect.DotDamagePercent:
            case ERunEffect.CooldownReductionPercent:
            case ERunEffect.MoveSpeedPercent:
            case ERunEffect.MaxStaminaPercent:
            case ERunEffect.TreeSpawnRatePercent:
            case ERunEffect.DamagePercentAboveThreshold:
            case ERunEffect.AttackSpeedPercentWhileMoving:
            case ERunEffect.StarMarkDamageTakenAdd:
            case ERunEffect.DotTickIntervalReducePercent:
            case ERunEffect.DotTickIntervalReduceAtMaxStackPercent:
            case ERunEffect.StaminaLossReducePercent:
                return true;

            default:
                return false;
        }
    }

    private static bool IsSecondsEffect(ERunEffect _effect)
    {
        switch (_effect)
        {
            case ERunEffect.StarMarkDurationAdd:
            case ERunEffect.DurationExtendOnDotKill:
            case ERunEffect.DotTickIntervalAdd:
                return true;

            default:
                return false;
        }
    }

    /// <summary>템플릿의 {0} 자리에 포맷된 수치를 채워 붙인다.</summary>
    private static void AppendFormattedLine(StringBuilder _target, string _template, StringBuilder _valueText)
    {
        int at = _template.IndexOf(Placeholder, StringComparison.Ordinal);
        if (0 > at)
        {
            _target.Append(_template);
            return;
        }

        _target.Append(_template, 0, at);
        _target.Append(_valueText);
        int tailStart = at + Placeholder.Length;
        if (_template.Length > tailStart)
        {
            _target.Append(_template, tailStart, _template.Length - tailStart);
        }
    }

    /// <summary>이 효과가 이 값일 때 플레이어에게 이로운가. 계약의 버프/디메리트를 나누는 기준이다.</summary>
    private static bool IsBeneficial(ERunEffect _effect, float _value)
    {
        switch (_effect)
        {
            case ERunEffect.TreeSpawnRatePercent:
            case ERunEffect.TriggerHitCountAdd:
            case ERunEffect.DotTickIntervalAdd:
                return 0f > _value;

            default:
                return 0f < _value;
        }
    }

    // // 키

    private static string GetTemplate(LocalizationManager _localization, ERunEffect _effect, ERoguelikeSkill _skill)
    {
        ERunEffect templateEffect = ERunEffect.ShockwaveDamageScalePercent == _effect ? ERunEffect.DamagePercent : _effect;
        int effectIndex = (int)templateEffect;
        int skillIndex = (int)_skill;
        if (0 > effectIndex || effectKeys.GetLength(0) <= effectIndex || 0 > skillIndex || SkillCount <= skillIndex) return string.Empty;

        if (ERoguelikeSkill.None != _skill)
        {
            string skillKey = effectKeys[effectIndex, skillIndex];
            if (null == skillKey)
            {
                skillKey = EffectKeyPrefix + templateEffect + "_" + _skill;
                effectKeys[effectIndex, skillIndex] = skillKey;
            }

            string skillText = _localization.GetText(skillKey);
            if (false == string.IsNullOrEmpty(skillText)) return skillText;
        }

        string genericKey = genericKeys[effectIndex];
        if (null == genericKey)
        {
            genericKey = EffectKeyPrefix + templateEffect;
            genericKeys[effectIndex] = genericKey;
        }

        return _localization.GetText(genericKey);
    }

    /// <summary>수치 줄 아래에 덧붙는 레벨별 한 줄(Roguelike_Card_&lt;cardId&gt;_X&lt;레벨&gt;). 수치로 말할 수 없는 그 레벨만의 변화(발동 방식, 대쉬 해금 등)를 쓴다.</summary>
    private static string GetExtraKey(string _cardId, int _level)
    {
        if (false == extraKeys.TryGetValue(_cardId, out string[] keys))
        {
            keys = new string[CardDefinitionSO.ActiveMaxLevel + 1];
            extraKeys.Add(_cardId, keys);
        }

        int index = Mathf.Clamp(_level, 0, keys.Length - 1);
        if (null == keys[index]) keys[index] = "Roguelike_Card_" + _cardId + "_X" + _level;
        return keys[index];
    }

    private static string GetCardKey(string _cardId, int _level)
    {
        if (false == cardKeys.TryGetValue(_cardId, out string[] keys))
        {
            keys = new string[CardDefinitionSO.ActiveMaxLevel + 1];
            cardKeys.Add(_cardId, keys);
        }

        int index = Mathf.Clamp(_level, 0, keys.Length - 1);
        if (null == keys[index]) keys[index] = "Roguelike_Card_" + _cardId + "_L" + _level;
        return keys[index];
    }
}
