using System.Collections.Generic;
using UnityEngine;

/// <summary>테스트용 카드·밸런스 에셋을 메모리에만 만든다. TearDown에서 DestroyAll로 정리한다.</summary>
public sealed class RoguelikeTestFactory
{
    private readonly List<Object> created = new List<Object>(32);

    public RoguelikeBalanceSO CreateBalance()
    {
        RoguelikeBalanceSO balance = ScriptableObject.CreateInstance<RoguelikeBalanceSO>();
        balance.FillTemporaryValues();
        created.Add(balance);
        return balance;
    }

    public CardDefinitionSO CreateCard(string _id, ECardKind _kind, ERoguelikeSkill _skill = ERoguelikeSkill.None, int _exclusiveGroup = 0)
    {
        CardDefinitionSO card = ScriptableObject.CreateInstance<CardDefinitionSO>();
        card.name = _id;
        card.cardId = _id;
        card.kind = _kind;
        card.skillTag = _skill;
        card.exclusiveGroup = _exclusiveGroup;
        card.levels = new List<CardLevelData>();
        created.Add(card);
        return card;
    }

    public CardDefinitionSO CreatePrism(string _id, CardDefinitionSO _a, CardDefinitionSO _b, ERoguelikeCombo _combo, params ERoguelikeSkill[] _suppressed)
    {
        CardDefinitionSO card = CreateCard(_id, ECardKind.Prism);
        card.comboMaterialA = _a;
        card.comboMaterialB = _b;
        card.comboType = _combo;
        card.suppressedSkills = new List<ERoguelikeSkill>(_suppressed);
        return card;
    }

    public CardDatabaseSO CreateDatabase(params CardDefinitionSO[] _cards)
    {
        CardDatabaseSO db = ScriptableObject.CreateInstance<CardDatabaseSO>();
        db.cards = new List<CardDefinitionSO>(_cards);
        created.Add(db);
        return db;
    }

    public static void AddLevelEffect(CardDefinitionSO _card, int _level, ERunEffect _effect, ERoguelikeSkill _skill, float _value)
    {
        while (_card.levels.Count < _level) _card.levels.Add(new CardLevelData());
        _card.levels[_level - 1].effects.Add(new CardEffectEntry(_effect, _skill, _value));
    }

    public void DestroyAll()
    {
        for (int i = 0; i < created.Count; i++)
        {
            if (null != created[i]) Object.DestroyImmediate(created[i]);
        }

        created.Clear();
    }
}
