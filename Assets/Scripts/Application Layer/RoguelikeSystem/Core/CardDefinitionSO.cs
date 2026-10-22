using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 한 장의 기획 데이터. 기획서 v2.4 부록 B "카드 기획 시트 양식"을 그대로 옮겼다.
///
/// [레벨 효과는 누적이다]
/// levels[0]은 1레벨을 고를 때 받는 효과, levels[1]은 2레벨로 올릴 때 "추가로" 받는 효과다.
/// 3레벨 카드의 효과는 levels[0] + levels[1] + levels[2]의 합이다. 예를 들어 "레벨당 공격력 +10%"는
/// 모든 레벨 칸에 DamagePercent 10을 넣으면 되고, "1레벨: 회전베기 자동 발동 / 2레벨: 범위 증가"는
/// 1레벨 칸에 UnlockSkill, 2레벨 칸에 RangePercent를 넣는다.
///
/// 계약(ECardKind.Ruby)과 프리즘은 레벨이 없으므로 levels[0]만 쓴다. 계약은 버프와 디메리트를 같은 칸에 함께 넣는다.
///
/// [레벨 수는 종류가 정한다] 액티브 7레벨(I~VI, MAX), 공통 패시브 5레벨(I~IV, MAX), 계약·프리즘 1(기획서 v2.4 5-2).
/// 카드에는 가격도 등급도 없다(5-3). 카드의 강약은 MAX까지 필요한 레벨 수로 정해진다.
/// </summary>
[CreateAssetMenu(fileName = "Card_", menuName = "Baobab/Roguelike/Card Definition")]
public class CardDefinitionSO : ScriptableObject
{
    /// <summary>액티브 카드의 레벨 수(VII = MAX). 기획서 v2.4 5-2.</summary>
    public const int ActiveMaxLevel = 7;
    /// <summary>공통 패시브 카드의 레벨 수(V = MAX). 기획서 v2.4 5-2.</summary>
    public const int PassiveMaxLevel = 5;

    [Header("기본")]
    [Tooltip("기록 파일에 저장되는 고유 ID입니다. 한 번 정하면 바꾸지 마십시오(기존 기록의 카드가 사라집니다).")]
    public string cardId;
    [Tooltip("카드 이름 로컬라이즈 키. 이름에 스킬 이름을 넣지 않는다(기획서 5-1 태그 규칙).")]
    public string nameKey;
    public string descriptionKey;
    public Sprite icon;

    [Header("분류")]
    [Tooltip("액티브 · 공통 패시브 · 계약(Ruby) · 조합(Prism). 레벨 수와 노출 비중이 종류로 정해집니다.")]
    public ECardKind kind = ECardKind.Passive;
    [Tooltip("액티브 카드가 가리키는 스킬. 공통 패시브·계약은 None.")]
    public ERoguelikeSkill skillTag = ERoguelikeSkill.None;
    [Tooltip("같은 값을 가진 카드끼리는 한 판에 한 장만 보유합니다. 0은 배타 없음. 조합 재료 카드에는 넣지 않습니다.")]
    public int exclusiveGroup = 0;

    [Header("레벨")]
    [Tooltip("레벨별 '추가' 효과. [0]=1레벨 선택 시, [1]=2레벨로 올릴 때 ... 레벨 수는 종류가 정합니다(액티브 7, 공통 패시브 5).")]
    public List<CardLevelData> levels = new List<CardLevelData>();

    [Header("프리즘 전용")]
    [Tooltip("두 카드가 모두 MAX가 되면 이 조합 카드가 열립니다.")]
    public CardDefinitionSO comboMaterialA;
    public CardDefinitionSO comboMaterialB;
    public ERoguelikeCombo comboType = ERoguelikeCombo.None;
    [Tooltip("이 조합을 고르면 기본 능력이 꺼지는 재료 스킬. 조합마다 다르다(기획서 6-2 조합 표 '재료의 기존 능력').")]
    public List<ERoguelikeSkill> suppressedSkills = new List<ERoguelikeSkill>();

    [Header("메모")]
    [TextArea] public string designerNote;

    /// <summary>레벨 상한(마지막 레벨이 MAX). 액티브 7, 공통 패시브 5, 계약·프리즘 1.</summary>
    public int EffectiveMaxLevel
    {
        get
        {
            switch (kind)
            {
                case ECardKind.Active: return ActiveMaxLevel;
                case ECardKind.Passive: return PassiveMaxLevel;
                default: return 1;
            }
        }
    }

    public bool IsComboMaterial(CardDefinitionSO _card)
    {
        return null != _card && (comboMaterialA == _card || comboMaterialB == _card);
    }

    /// <summary>
    /// 한 판에 함께 가질 수 없는 카드인지. 같은 배타 그룹(0 제외)이거나, 둘 다 조합 카드이고 재료가 한 장이라도 겹친다
    /// (기획서 v2.5 6-2 "재료가 겹치는 조합은 한 판에 하나만": 포자 수류탄 + 충격파와 삼각 조합, 포자 수류탄 + 드론과 스텔라 + 드론).
    /// </summary>
    public bool ConflictsWith(CardDefinitionSO _other)
    {
        if (null == _other || this == _other) return false;
        if (0 != exclusiveGroup && exclusiveGroup == _other.exclusiveGroup) return true;
        if (ECardKind.Prism != kind || ECardKind.Prism != _other.kind) return false;

        return (null != comboMaterialA && _other.IsComboMaterial(comboMaterialA)) || (null != comboMaterialB && _other.IsComboMaterial(comboMaterialB));
    }

    /// <summary>_level레벨까지 누적된 효과를 _results에 더한다(지우지 않는다).</summary>
    public void CollectEffects(int _level, List<CardEffectEntry> _results)
    {
        if (null == levels) return;

        int last = Mathf.Min(_level, levels.Count);
        for (int i = 0; i < last; i++)
        {
            CardLevelData levelData = levels[i];
            if (null == levelData || null == levelData.effects) continue;

            for (int j = 0; j < levelData.effects.Count; j++)
            {
                _results.Add(levelData.effects[j]);
            }
        }
    }
}

[Serializable]
public class CardLevelData
{
    public List<CardEffectEntry> effects = new List<CardEffectEntry>();
}

[Serializable]
public struct CardEffectEntry
{
    public ERunEffect effect;
    [Tooltip("None이면 전역. 특정 스킬이면 그 스킬에만 적용.")]
    public ERoguelikeSkill skill;
    public float value;

    public CardEffectEntry(ERunEffect _effect, ERoguelikeSkill _skill, float _value)
    {
        effect = _effect;
        skill = _skill;
        value = _value;
    }
}
