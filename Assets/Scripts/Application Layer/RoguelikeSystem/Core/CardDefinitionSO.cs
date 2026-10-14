using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카드 한 장의 기획 데이터. 기획서 8장 "카드 기획 시트 양식"을 그대로 옮겼다.
///
/// [레벨 효과는 누적이다]
/// levels[0]은 1레벨을 살 때 받는 효과, levels[1]은 2레벨로 올릴 때 "추가로" 받는 효과다.
/// 3레벨 카드의 효과는 levels[0] + levels[1] + levels[2]의 합이다. 예를 들어 "레벨당 공격력 +10%"는
/// 모든 레벨 칸에 DamagePercent 10을 넣으면 되고, "1레벨: 회전베기 자동 발동 / 2레벨: 범위 증가"는
/// 1레벨 칸에 UnlockSkill, 2레벨 칸에 RangePercent를 넣는다.
///
/// 루비와 프리즘은 레벨이 없으므로 levels[0]만 쓴다. 루비는 버프와 디메리트를 같은 칸에 함께 넣는다.
/// </summary>
[CreateAssetMenu(fileName = "Card_", menuName = "Baobab/Roguelike/Card Definition")]
public class CardDefinitionSO : ScriptableObject
{
    public const int DefaultMaxLevel = 5;

    [Header("기본")]
    [Tooltip("기록 파일에 저장되는 고유 ID입니다. 한 번 정하면 바꾸지 마십시오(기존 기록의 카드가 사라집니다).")]
    public string cardId;
    [Tooltip("카드 이름 로컬라이즈 키. 이름에 스킬 이름을 넣지 않는다(기획서 3-3 태그 규칙).")]
    public string nameKey;
    public string descriptionKey;
    public Sprite icon;

    [Header("분류")]
    public ECardKind kind = ECardKind.Passive;
    [Tooltip("브론즈/실버/골드/루비만 직접 지정합니다. 에메랄드는 자동이고, 프리즘은 kind로 판별합니다.")]
    public ECardGrade grade = ECardGrade.Bronze;
    [Tooltip("액티브 카드가 가리키는 스킬. 공통 패시브·루비는 None.")]
    public ERoguelikeSkill skillTag = ERoguelikeSkill.None;
    [Tooltip("같은 값을 가진 카드끼리는 한 판에 한 장만 보유합니다. 0은 배타 없음. 조합 재료 카드에는 넣지 않습니다.")]
    public int exclusiveGroup = 0;

    [Header("레벨")]
    [Tooltip("모든 카드 5로 통일(5레벨 = MAX). 루비·프리즘은 무시되고 항상 1입니다.")]
    public int maxLevel = DefaultMaxLevel;
    [Tooltip("레벨별 '추가' 효과. [0]=1레벨 구매 시, [1]=2레벨로 올릴 때 ...")]
    public List<CardLevelData> levels = new List<CardLevelData>();

    [Header("가격")]
    [Tooltip("0이면 등급별 단가(RoguelikeBalanceSO)를 씁니다. 0보다 크면 이 카드만 첫 구매가를 덮어씁니다.")]
    public int basePriceOverride = 0;

    [Header("프리즘 전용")]
    [Tooltip("두 카드가 모두 MAX가 되면 이 조합 카드가 열립니다.")]
    public CardDefinitionSO comboMaterialA;
    public CardDefinitionSO comboMaterialB;
    public ERoguelikeCombo comboType = ERoguelikeCombo.None;
    [Tooltip("이 조합을 사면 기본 능력이 꺼지는 재료 스킬. 조합마다 다르다(기획서 3-3 조합 표 '재료의 기존 능력').")]
    public List<ERoguelikeSkill> suppressedSkills = new List<ERoguelikeSkill>();

    [Header("메모")]
    [TextArea] public string designerNote;

    /// <summary>실제로 적용되는 레벨 상한. 루비·프리즘은 1.</summary>
    public int EffectiveMaxLevel
    {
        get
        {
            if (ECardKind.Ruby == kind || ECardKind.Prism == kind) return 1;
            return Mathf.Max(1, maxLevel);
        }
    }

    /// <summary>가격표 조회에 쓰는 등급. 프리즘은 kind가 우선한다.</summary>
    public ECardGrade PriceGrade => ECardKind.Prism == kind ? ECardGrade.Prism : grade;

    public bool IsComboMaterial(CardDefinitionSO _card)
    {
        return null != _card && (comboMaterialA == _card || comboMaterialB == _card);
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
