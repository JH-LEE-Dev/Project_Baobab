using System.Collections.Generic;

/// <summary>
/// 한 판 동안의 빌드 상태(보유 카드와 레벨, 배타 그룹, 리롤 횟수). 판이 끝나면 통째로 버린다.
/// 영구 진행 데이터(본편 능력 트리·세이브)와는 아무 연결이 없다(기획서 3-3 "런 단위 스킬 부여").
/// </summary>
public sealed class RunBuildState
{
    private const int InitialCapacity = 64;

    // 보유 카드 → 현재 레벨(1부터). 보유하지 않으면 키가 없다.
    private readonly Dictionary<CardDefinitionSO, int> levels = new Dictionary<CardDefinitionSO, int>(InitialCapacity);
    // 처음 구매한 순서. 결과 화면/기록이 이 순서로 보여준다.
    private readonly List<CardDefinitionSO> ownedOrder = new List<CardDefinitionSO>(InitialCapacity);
    // 이미 한 장을 구매해 잠긴 배타 그룹.
    private readonly HashSet<int> lockedExclusiveGroups = new HashSet<int>();
    // 이번 판에서 한 번이라도 제시된 카드(자물쇠 연출 판단용).
    private readonly HashSet<CardDefinitionSO> seenCards = new HashSet<CardDefinitionSO>();

    public int RerollCount { get; private set; }
    public int PurchaseCount { get; private set; }

    /// <summary>처음 구매한 순서대로의 보유 카드.</summary>
    public IReadOnlyList<CardDefinitionSO> OwnedCards => ownedOrder;

    public void Reset()
    {
        levels.Clear();
        ownedOrder.Clear();
        lockedExclusiveGroups.Clear();
        seenCards.Clear();
        RerollCount = 0;
        PurchaseCount = 0;
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
    /// 같은 배타 그룹의 "다른" 카드를 이미 보유해서 이 카드를 더 이상 살 수 없는지.
    /// 그룹을 잠근 카드 자신은 제외되지 않는다(업그레이드 가능한 종류라면 계속 강화할 수 있다).
    /// </summary>
    public bool IsExcluded(CardDefinitionSO _card)
    {
        if (null == _card || 0 == _card.exclusiveGroup) return false;
        if (false == lockedExclusiveGroups.Contains(_card.exclusiveGroup)) return false;
        return false == IsOwned(_card);
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
        PurchaseCount++;

        if (0 != _card.exclusiveGroup)
        {
            lockedExclusiveGroups.Add(_card.exclusiveGroup);
        }

        return true;
    }

    /// <summary>
    /// 감정 승격. 보유 카드의 레벨을 _level로 바로 올린다(최대 MAX). 구매가 아니므로 구매 수는 늘지 않는다.
    /// 보유하지 않았거나 현재 레벨 이하면 false.
    /// </summary>
    public bool PromoteTo(CardDefinitionSO _card, int _level)
    {
        int current = GetLevel(_card);
        if (current <= 0) return false;

        int target = UnityEngine.Mathf.Min(_level, _card.EffectiveMaxLevel);
        if (target <= current) return false;

        levels[_card] = target;
        return true;
    }

    /// <summary>보유 중인 액티브 카드 수(액티브 보장 해제 판단용).</summary>
    public int OwnedActiveCount
    {
        get
        {
            int count = 0;
            for (int i = 0; i < ownedOrder.Count; i++)
            {
                if (null != ownedOrder[i] && ECardKind.Active == ownedOrder[i].kind) count++;
            }

            return count;
        }
    }

    public void IncreaseRerollCount()
    {
        RerollCount++;
    }
}
