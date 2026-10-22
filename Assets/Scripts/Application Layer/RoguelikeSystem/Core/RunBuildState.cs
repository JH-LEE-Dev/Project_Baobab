using System.Collections.Generic;

/// <summary>
/// 한 판 동안의 빌드 상태(보유 카드와 레벨, 배타 그룹). 판이 끝나면 통째로 버린다.
/// 영구 진행 데이터(본편 능력 트리·세이브)와는 아무 연결이 없다(기획서 5-1 "런 단위 스킬 부여").
/// </summary>
public sealed class RunBuildState
{
    private const int InitialCapacity = 64;

    // 보유 카드 → 현재 레벨(1부터). 보유하지 않으면 키가 없다.
    private readonly Dictionary<CardDefinitionSO, int> levels = new Dictionary<CardDefinitionSO, int>(InitialCapacity);
    // 처음 고른 순서. 결과 화면/기록이 이 순서로 보여준다.
    private readonly List<CardDefinitionSO> ownedOrder = new List<CardDefinitionSO>(InitialCapacity);
    // 이미 한 장을 골라 잠긴 배타 그룹.
    private readonly HashSet<int> lockedExclusiveGroups = new HashSet<int>();
    // 이번 판에서 한 번이라도 제시된 카드(자물쇠 연출 판단용).
    private readonly HashSet<CardDefinitionSO> seenCards = new HashSet<CardDefinitionSO>();

    /// <summary>이번 판에서 카드를 고른 횟수(선택 횟수가 빌드 규모를 정한다, 기획서 v2.4 4장).</summary>
    public int PickCount { get; private set; }

    /// <summary>처음 고른 순서대로의 보유 카드.</summary>
    public IReadOnlyList<CardDefinitionSO> OwnedCards => ownedOrder;

    public void Reset()
    {
        levels.Clear();
        ownedOrder.Clear();
        lockedExclusiveGroups.Clear();
        seenCards.Clear();
        PickCount = 0;
    }

    public int GetLevel(CardDefinitionSO _card)
    {
        if (null == _card) return 0;
        return levels.TryGetValue(_card, out int level) ? level : 0;
    }

    public bool IsOwned(CardDefinitionSO _card) => GetLevel(_card) > 0;

    public bool IsMax(CardDefinitionSO _card)
    {
        if (null == _card) return false;
        return GetLevel(_card) >= _card.EffectiveMaxLevel;
    }

    /// <summary>
    /// 같은 배타 그룹의 "다른" 카드, 또는 재료가 겹치는 다른 조합 카드를 이미 보유해서 이 카드를 더 이상 고를 수 없는지.
    /// 그룹을 잠근 카드 자신은 제외되지 않는다(업그레이드 가능한 종류라면 계속 강화할 수 있다).
    /// </summary>
    public bool IsExcluded(CardDefinitionSO _card)
    {
        if (null == _card || IsOwned(_card)) return false;
        if (0 != _card.exclusiveGroup && lockedExclusiveGroups.Contains(_card.exclusiveGroup)) return true;
        if (ECardKind.Prism != _card.kind) return false;

        for (int i = 0; i < ownedOrder.Count; i++)
        {
            if (ECardKind.Prism == ownedOrder[i].kind && _card.ConflictsWith(ownedOrder[i])) return true;
        }

        return false;
    }

    public bool HasSeen(CardDefinitionSO _card) => null != _card && seenCards.Contains(_card);

    public void MarkSeen(CardDefinitionSO _card)
    {
        if (null != _card) seenCards.Add(_card);
    }

    /// <summary>카드를 한 단계 획득한다. 이미 MAX면 false.</summary>
    public bool Acquire(CardDefinitionSO _card)
    {
        if (null == _card || IsMax(_card)) return false;

        int level = GetLevel(_card);
        if (0 == level)
        {
            ownedOrder.Add(_card);
        }

        levels[_card] = level + 1;
        PickCount++;

        if (0 != _card.exclusiveGroup)
        {
            lockedExclusiveGroups.Add(_card.exclusiveGroup);
        }

        return true;
    }
}
