using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 설명 패널에 보여 줄 "이 카드를 고르면 어떻게 작용하는가" 문구를 카드 데이터(CardDefinitionSO.levels)의 수치로 만든다(기획서 v2.5 10-2).
/// 수치를 문구에 직접 쓰지 않으므로 밸런스 값이 바뀌어도 카드 에셋만 고치면 패널이 따라간다.
///
/// [무엇을 보여 주나] 고른 뒤 레벨(levelAfterPurchase)에서 올라가는 효과만, 효과 종류(ERunEffect)별로 한 줄씩.
///   처음 고름(그 효과가 처음 생김)  "피해 +12%"
///   업그레이드                       "피해 +12% → +27%"  (누적 수치 현재 → 다음)
///   스킬 획득(UnlockSkill)은 줄을 만들지 않는다 - 기본 설명이 이미 말한다.
/// [문구 키] 템플릿은 RoguelikeUI.json의 Roguelike_Effect_&lt;ERunEffect&gt;[_&lt;ERoguelikeSkill&gt;] ({0} 자리에 수치). 스킬별 문구가 있으면 그쪽이 우선이다
///   (예: CountAdd_Drone = "드론 수 {0}"). 초 단위 효과의 단위는 Roguelike_EffectUnit_Seconds. 퍼센트는 "%"를 그대로 쓴다.
///   템플릿으로 표현되지 않는 카드(조합 등)는 Roguelike_Card_&lt;cardId&gt;_L&lt;레벨&gt; 문구가 있으면 그것을 대신 쓴다.
/// [계약] 좋은 효과는 버프 줄, 나쁜 효과(디메리트)는 별도 줄(붉은색)로 나눈다. 좋고 나쁨은 효과마다 다르다(나무 생성 빈도 증가·임계치 감소·타격 수 감소 등).
/// 힙 할당 없이 재사용 버퍼로 계산한다(키 문자열은 처음 쓸 때 한 번만 만든다).
/// </summary>
public static class RoguelikeCardEffectText
{
    private const string EffectKeyPrefix = "Roguelike_Effect_";
    private const string SecondsUnitKey = "Roguelike_EffectUnit_Seconds";
    private const string Arrow = " → ";
    private const string Placeholder = "{0}";
    private const int SlotCapacity = 24;
    private const int SkillCount = 8;

    private struct EffectSlot
    {
        public ERunEffect effect;
        public ERoguelikeSkill skill;
        public float before;
        public float after;
        public bool bChanged;
    }

    private static readonly EffectSlot[] slots = new EffectSlot[SlotCapacity];
    private static readonly string[,] effectKeys = new string[(int)ERunEffect.Count, SkillCount];
    private static readonly string[] genericKeys = new string[(int)ERunEffect.Count];
    private static readonly Dictionary<string, string[]> cardKeys = new Dictionary<string, string[]>(32);
    private static int slotCount;

    /// <summary>
    /// _benefit에 버프 줄, _demerit에 디메리트 줄(계약만)을 채운다(둘 다 먼저 비운다). 줄 사이는 줄바꿈이다.
    /// 카드 데이터가 없거나 보여 줄 효과가 없으면 둘 다 비어 있다.
    /// </summary>
    public static void Build(in RoguelikeCardViewData _data, LocalizationManager _localization, StringBuilder _benefit, StringBuilder _demerit)
    {
        _benefit.Clear();
        _demerit.Clear();

        CardDefinitionSO card = _data.card;
        if (null == card || null == _localization || null == card.levels || 0 == card.levels.Count) return;

        int level = Mathf.Clamp(_data.levelAfterPurchase, 1, card.levels.Count);

        string overrideText = _localization.GetText(GetCardKey(card.cardId, level));
        if (false == string.IsNullOrEmpty(overrideText))
        {
            _benefit.Append(overrideText);
            return;
        }

        Collect(card, level);
        bool bSplit = ECardKind.Ruby == card.kind;
        string seconds = _localization.GetText(SecondsUnitKey);

        for (int i = 0; i < slotCount; i++)
        {
            ref EffectSlot slot = ref slots[i];
            if (false == slot.bChanged || ERunEffect.UnlockSkill == slot.effect) continue;

            string template = GetTemplate(_localization, slot.effect, slot.skill);
            if (string.IsNullOrEmpty(template)) continue;

            StringBuilder target = bSplit && false == IsBeneficial(slot.effect, slot.after) ? _demerit : _benefit;
            if (0 < target.Length) target.Append('\n');

            AppendLine(target, template, in slot, GetUnit(slot.effect, seconds));
        }
    }

    // // 수치 모으기

    /// <summary>1레벨부터 _level까지의 효과를 (효과, 스킬)별 누적으로 모은다. before = 직전 레벨까지, after = 이 레벨까지, bChanged = 이 레벨에서 바뀜.</summary>
    private static void Collect(CardDefinitionSO _card, int _level)
    {
        slotCount = 0;

        for (int i = 0; i < _level; i++)
        {
            CardLevelData levelData = _card.levels[i];
            if (null == levelData || null == levelData.effects) continue;

            bool bThisLevel = i == _level - 1;
            for (int j = 0; j < levelData.effects.Count; j++)
            {
                CardEffectEntry entry = levelData.effects[j];
                int index = FindSlot(entry.effect, entry.skill);
                if (index < 0) continue;

                ref EffectSlot slot = ref slots[index];
                slot.after += entry.value;
                if (bThisLevel) slot.bChanged = true;
                else slot.before += entry.value;
            }
        }
    }

    private static int FindSlot(ERunEffect _effect, ERoguelikeSkill _skill)
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i].effect == _effect && slots[i].skill == _skill) return i;
        }

        if (SlotCapacity <= slotCount) return -1;

        slots[slotCount] = new EffectSlot { effect = _effect, skill = _skill };
        return slotCount++;
    }

    // // 문구 조립

    private static void AppendLine(StringBuilder _target, string _template, in EffectSlot _slot, string _unit)
    {
        int at = _template.IndexOf(Placeholder, System.StringComparison.Ordinal);
        if (at < 0)
        {
            _target.Append(_template);
            return;
        }

        _target.Append(_template, 0, at);

        // 이 레벨에서 처음 생긴 효과는 "+after", 이미 있던 효과가 오르면 "before → after".
        if (0f != _slot.before)
        {
            AppendValue(_target, _slot.before, _unit);
            _target.Append(Arrow);
        }

        AppendValue(_target, _slot.after, _unit);
        _target.Append(_template, at + Placeholder.Length, _template.Length - at - Placeholder.Length);
    }

    /// <summary>부호를 붙인 수치. 10 이상은 정수, 그 아래는 소수 첫째 자리까지(정수면 정수). 단위가 있으면 뒤에 붙인다.</summary>
    private static void AppendValue(StringBuilder _target, float _value, string _unit)
    {
        // 10 이상은 정수로(밸런스 환산값 28.5714 같은 꼬리를 숨긴다), 10 미만은 소수 첫째 자리까지.
        float rounded = Mathf.Abs(_value) >= 10f ? Mathf.Round(_value) : Mathf.Round(_value * 10f) / 10f;
        _target.Append(rounded < 0f ? '-' : '+');

        float abs = Mathf.Abs(rounded);
        int whole = (int)abs;
        int tenth = Mathf.RoundToInt((abs - whole) * 10f);
        if (10 <= tenth)
        {
            whole++;
            tenth = 0;
        }

        AppendInt(_target, whole);
        if (0 < tenth)
        {
            _target.Append('.');
            _target.Append((char)('0' + tenth));
        }

        _target.Append(_unit);
    }

    private static void AppendInt(StringBuilder _target, int _value)
    {
        if (_value >= 10) AppendInt(_target, _value / 10);
        _target.Append((char)('0' + _value % 10));
    }

    /// <summary>효과의 단위: 퍼센트 효과는 "%", 초 효과는 로컬라이즈된 초, 개수·고정 수치는 없음.</summary>
    private static string GetUnit(ERunEffect _effect, string _seconds)
    {
        switch (_effect)
        {
            case ERunEffect.DamagePercent:
            case ERunEffect.AttackSpeedPercent:
            case ERunEffect.RangePercent:
            case ERunEffect.DurationPercent:
            case ERunEffect.DotDamagePercent:
            case ERunEffect.ChancePercent:
            case ERunEffect.CooldownReductionPercent:
            case ERunEffect.MoveSpeedPercent:
            case ERunEffect.MaxStaminaPercent:
            case ERunEffect.TreeSpawnRatePercent:
            case ERunEffect.DamagePercentAboveThreshold:
            case ERunEffect.AttackSpeedPercentWhileMoving:
            case ERunEffect.StarMarkDamageTakenAdd:
            case ERunEffect.DotTickIntervalReducePercent:
            case ERunEffect.DotTickIntervalReduceAtMaxStackPercent:
                return "%";

            case ERunEffect.GraceTimeAdd:
            case ERunEffect.DamageCooldownAdd:
            case ERunEffect.StarMarkDurationAdd:
            case ERunEffect.DurationExtendOnDotKill:
            case ERunEffect.DotTickIntervalAdd:
                return _seconds;

            default:
                return string.Empty;
        }
    }

    /// <summary>이 효과가 이 값일 때 플레이어에게 이로운가. 계약의 버프/디메리트를 나누는 기준이다.</summary>
    private static bool IsBeneficial(ERunEffect _effect, float _value)
    {
        switch (_effect)
        {
            case ERunEffect.TreeSpawnRatePercent:
            case ERunEffect.TriggerHitCountAdd:
                return _value < 0f;

            default:
                return 0f < _value;
        }
    }

    // // 키

    private static string GetTemplate(LocalizationManager _localization, ERunEffect _effect, ERoguelikeSkill _skill)
    {
        int effectIndex = (int)_effect;
        int skillIndex = (int)_skill;
        if (effectIndex < 0 || effectIndex >= effectKeys.GetLength(0) || skillIndex < 0 || skillIndex >= SkillCount) return string.Empty;

        if (ERoguelikeSkill.None != _skill)
        {
            string skillKey = effectKeys[effectIndex, skillIndex];
            if (null == skillKey)
            {
                skillKey = EffectKeyPrefix + _effect + "_" + _skill;
                effectKeys[effectIndex, skillIndex] = skillKey;
            }

            string skillText = _localization.GetText(skillKey);
            if (false == string.IsNullOrEmpty(skillText)) return skillText;
        }

        string genericKey = genericKeys[effectIndex];
        if (null == genericKey)
        {
            genericKey = EffectKeyPrefix + _effect;
            genericKeys[effectIndex] = genericKey;
        }

        return _localization.GetText(genericKey);
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
