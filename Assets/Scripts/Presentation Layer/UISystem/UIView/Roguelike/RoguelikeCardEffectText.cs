using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 설명 패널에 보여 줄 "이 카드를 고르면 어떻게 작용하는가" 문구를 카드 데이터(CardDefinitionSO.levels)로 만든다(기획서 v2.5 10-2).
/// 수치 · 부호 · 단위 · 화살표는 보여 주지 않는다. 어떤 효과가 바뀌는지는 효과 이름(카테고리)으로만 말하고, 이로운지 해로운지는 색으로 구분한다
/// (이로우면 버프 줄 = 금색, 해로우면 디메리트 줄 = 적색. 패널이 색을 입힌다).
///
/// [무엇을 보여 주나] 고른 뒤 레벨(levelAfterPurchase)에서 바뀌는 효과만, 효과 종류(ERunEffect)별로 한 줄씩.
///   스킬 획득(UnlockSkill)은 줄을 만들지 않는다 - 기본 설명이 이미 말한다.
/// [문구 키] 템플릿은 RoguelikeUI.json의 Roguelike_Effect_&lt;ERunEffect&gt;[_&lt;ERoguelikeSkill&gt;]. 템플릿 안의 {0}(수치 자리)은 지우고 효과 이름만 쓴다.
///   스킬별 문구가 있으면 그쪽이 우선이다(예: CountAdd_Drone = "드론 수 {0}").
///   템플릿으로 표현되지 않는 카드(조합 등)는 Roguelike_Card_&lt;cardId&gt;_L&lt;레벨&gt; 문구가 있으면 그것을 대신 쓴다.
/// [레벨별 추가 문구] 효과 줄 아래에 Roguelike_Card_&lt;cardId&gt;_X&lt;레벨&gt; 문구가 있으면 그 레벨에서만 한 줄 더 보여 준다(예: 회전베기 "4번째 공격마다 발동한다.").
/// 기본 설명에는 레벨에 따라 달라지는 내용을 쓰지 않는다.
/// [좋고 나쁨] 모든 카드가 같은 기준(IsBeneficial)으로 버프 줄 / 디메리트 줄을 나눈다. 좋고 나쁨은 효과마다 다르다(나무 생성 빈도 증가·타격 수 증가 등).
/// 힙 할당 없이 재사용 버퍼로 계산한다(키 문자열은 처음 쓸 때 한 번만 만든다).
/// </summary>
public static class RoguelikeCardEffectText
{
    private const string EffectKeyPrefix = "Roguelike_Effect_";
    private const string Placeholder = "{0}";
    private const int SlotCapacity = 24;
    private const int SkillCount = 8;

    private struct EffectSlot
    {
        public ERunEffect effect;
        public ERoguelikeSkill skill;
        public float after;
        public bool bChanged;
    }

    private static readonly EffectSlot[] slots = new EffectSlot[SlotCapacity];
    private static readonly string[,] effectKeys = new string[(int)ERunEffect.Count, SkillCount];
    private static readonly string[] genericKeys = new string[(int)ERunEffect.Count];
    private static readonly Dictionary<string, string[]> cardKeys = new Dictionary<string, string[]>(32);
    private static readonly Dictionary<string, string[]> extraKeys = new Dictionary<string, string[]>(32);
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

        for (int i = 0; i < slotCount; i++)
        {
            ref EffectSlot slot = ref slots[i];
            if (false == slot.bChanged || 0f == slot.after || ERunEffect.UnlockSkill == slot.effect) continue;

            string template = GetTemplate(_localization, slot.effect, slot.skill);
            if (string.IsNullOrEmpty(template)) continue;

            StringBuilder target = false == IsBeneficial(slot.effect, slot.after) ? _demerit : _benefit;
            if (0 < target.Length) target.Append('\n');

            AppendLine(target, template);
        }

        string extraText = _localization.GetText(GetExtraKey(card.cardId, level));
        if (false == string.IsNullOrEmpty(extraText))
        {
            if (0 < _benefit.Length) _benefit.Append('\n');
            _benefit.Append(extraText);
        }
    }

    // // 수치 모으기

    /// <summary>1레벨부터 _level까지의 효과를 (효과, 스킬)별 누적으로 모은다. after = 이 레벨까지의 합, bChanged = 이 레벨에서 바뀜.</summary>
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

    /// <summary>템플릿에서 수치 자리({0})를 지우고 효과 이름만 붙인다. 자리 앞뒤의 공백은 한 칸만 남긴다.</summary>
    private static void AppendLine(StringBuilder _target, string _template)
    {
        int at = _template.IndexOf(Placeholder, System.StringComparison.Ordinal);
        if (at < 0)
        {
            _target.Append(_template);
            return;
        }

        int headEnd = at;
        while (0 < headEnd && char.IsWhiteSpace(_template[headEnd - 1])) headEnd--;

        int tailStart = at + Placeholder.Length;
        while (tailStart < _template.Length && char.IsWhiteSpace(_template[tailStart])) tailStart++;

        _target.Append(_template, 0, headEnd);
        if (0 < headEnd && tailStart < _template.Length) _target.Append(' ');
        _target.Append(_template, tailStart, _template.Length - tailStart);
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
