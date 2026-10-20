using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크에 등장하는 카드 전체 목록. 제시 풀은 이 목록에서만 뽑는다.
/// 목록 순서는 동률일 때의 정렬 기준으로만 쓰이며 의미가 없다.
/// </summary>
[CreateAssetMenu(fileName = "RoguelikeCardDatabase", menuName = "Baobab/Roguelike/Card Database")]
public class CardDatabaseSO : ScriptableObject
{
    public List<CardDefinitionSO> cards = new List<CardDefinitionSO>();

    public int Count => null == cards ? 0 : cards.Count;

    public CardDefinitionSO Get(int _index) => cards[_index];

    /// <summary>기록 파일의 cardId로 카드를 찾는다. 없으면 null(삭제된 카드).</summary>
    public CardDefinitionSO FindById(string _cardId)
    {
        if (null == cards || string.IsNullOrEmpty(_cardId)) return null;

        for (int i = 0; i < cards.Count; i++)
        {
            if (null != cards[i] && cards[i].cardId == _cardId) return cards[i];
        }

        return null;
    }

#if UNITY_EDITOR
    /// <summary>에디터에서 데이터 실수를 잡는다. 빌드에는 포함되지 않는다.</summary>
    private void OnValidate()
    {
        if (null == cards) return;

        for (int i = 0; i < cards.Count; i++)
        {
            CardDefinitionSO a = cards[i];
            if (null == a) continue;

            if (string.IsNullOrEmpty(a.cardId))
            {
                Debug.LogWarning($"[CardDatabaseSO] '{a.name}'의 cardId가 비어 있습니다. 기록 저장에 쓰이므로 반드시 채워 주세요.", a);
            }

            for (int j = i + 1; j < cards.Count; j++)
            {
                CardDefinitionSO b = cards[j];
                if (null == b || a == b) continue;

                if (false == string.IsNullOrEmpty(a.cardId) && a.cardId == b.cardId)
                {
                    Debug.LogError($"[CardDatabaseSO] cardId '{a.cardId}'가 '{a.name}'과 '{b.name}'에 중복되어 있습니다.", this);
                }
            }

            if (ECardKind.Prism == a.kind && (null == a.comboMaterialA || null == a.comboMaterialB))
            {
                Debug.LogWarning($"[CardDatabaseSO] 프리즘 카드 '{a.name}'의 재료 카드가 비어 있습니다.", a);
            }

            if (0 != a.exclusiveGroup && IsUsedAsComboMaterial(a))
            {
                Debug.LogWarning($"[CardDatabaseSO] '{a.name}'은 조합 재료인데 배타 그룹({a.exclusiveGroup})에 들어 있습니다. " +
                    "다른 카드를 고르는 순간 조합이 막힐 수 있습니다(기획서 5-4 '조합 재료 주의').", a);
            }
        }
    }

    private bool IsUsedAsComboMaterial(CardDefinitionSO _card)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            CardDefinitionSO prism = cards[i];
            if (null == prism || ECardKind.Prism != prism.kind) continue;
            if (prism.IsComboMaterial(_card)) return true;
        }

        return false;
    }
#endif
}
