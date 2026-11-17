using System.Collections.Generic;
using System.Globalization;

/// <summary>도전과제 종류(기획서 v3.1 8-3). 문구 키는 종류마다 하나이고, 나무 · 언리밋은 제목의 {0}에 목표값을 넣는다.</summary>
public enum ERoguelikeChallengeKind
{
    PrismAll = 0,
    CardMaxAll = 1,
    RubyAll = 2,
    TreeN = 3,
    UnlimitN = 4,
    TierAll = 5,
    Wave30 = 6,
}

/// <summary>도전과제 한 칸의 정의. id는 기록 파일에 저장되므로 한 번 정하면 바꾸지 않는다.</summary>
public readonly struct RoguelikeChallengeDefinition
{
    public readonly string id;
    public readonly ERoguelikeChallengeKind kind;
    /// <summary>나무 수(TreeN) · 언리밋 번호(UnlimitN). 나머지 종류는 쓰지 않는다(전체 수는 카드 DB · 밸런스에서 센다).</summary>
    public readonly int target;
    public readonly int titleKey;
    public readonly int tipKey;

    public RoguelikeChallengeDefinition(string _id, ERoguelikeChallengeKind _kind, int _target, int _titleKey, int _tipKey)
    {
        id = _id;
        kind = _kind;
        target = _target;
        titleKey = _titleKey;
        tipKey = _tipKey;
    }
}

/// <summary>
/// 로그라이크 도전과제 13칸의 정의와 표시용 문구 조립. 기획서 v3.1 8-3 목록 순서 그대로이며, 목록 창도 이 순서로 그린다.
/// 달성 판정 · 누적은 RoguelikeChallengeTracker가, 저장은 RoguelikeRecordData(RoguelikeRecords.json)가 맡는다.
/// </summary>
public static class RoguelikeChallenges
{
    private static readonly RoguelikeChallengeDefinition[] definitions =
    {
        new RoguelikeChallengeDefinition("PrismAll", ERoguelikeChallengeKind.PrismAll, 0, LocKeys.RoguelikeUI.roguelikeChallengePrismAllTitle, LocKeys.RoguelikeUI.roguelikeChallengePrismAllTip),
        new RoguelikeChallengeDefinition("CardMaxAll", ERoguelikeChallengeKind.CardMaxAll, 0, LocKeys.RoguelikeUI.roguelikeChallengeCardMaxAllTitle, LocKeys.RoguelikeUI.roguelikeChallengeCardMaxAllTip),
        new RoguelikeChallengeDefinition("RubyAll", ERoguelikeChallengeKind.RubyAll, 0, LocKeys.RoguelikeUI.roguelikeChallengeRubyAllTitle, LocKeys.RoguelikeUI.roguelikeChallengeRubyAllTip),
        new RoguelikeChallengeDefinition("Tree1000", ERoguelikeChallengeKind.TreeN, 1000, LocKeys.RoguelikeUI.roguelikeChallengeTreeNTitle, LocKeys.RoguelikeUI.roguelikeChallengeTreeNTip),
        new RoguelikeChallengeDefinition("Tree5000", ERoguelikeChallengeKind.TreeN, 5000, LocKeys.RoguelikeUI.roguelikeChallengeTreeNTitle, LocKeys.RoguelikeUI.roguelikeChallengeTreeNTip),
        new RoguelikeChallengeDefinition("Tree10000", ERoguelikeChallengeKind.TreeN, 10000, LocKeys.RoguelikeUI.roguelikeChallengeTreeNTitle, LocKeys.RoguelikeUI.roguelikeChallengeTreeNTip),
        new RoguelikeChallengeDefinition("Unlimit5", ERoguelikeChallengeKind.UnlimitN, 5, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTitle, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTip),
        new RoguelikeChallengeDefinition("Unlimit10", ERoguelikeChallengeKind.UnlimitN, 10, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTitle, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTip),
        new RoguelikeChallengeDefinition("Unlimit15", ERoguelikeChallengeKind.UnlimitN, 15, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTitle, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTip),
        new RoguelikeChallengeDefinition("Unlimit20", ERoguelikeChallengeKind.UnlimitN, 20, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTitle, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTip),
        new RoguelikeChallengeDefinition("Unlimit25", ERoguelikeChallengeKind.UnlimitN, 25, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTitle, LocKeys.RoguelikeUI.roguelikeChallengeUnlimitNTip),
        new RoguelikeChallengeDefinition("TierAll", ERoguelikeChallengeKind.TierAll, 0, LocKeys.RoguelikeUI.roguelikeChallengeTierAllTitle, LocKeys.RoguelikeUI.roguelikeChallengeTierAllTip),
        new RoguelikeChallengeDefinition("Wave30", ERoguelikeChallengeKind.Wave30, 0, LocKeys.RoguelikeUI.roguelikeChallengeWave30Title, LocKeys.RoguelikeUI.roguelikeChallengeWave30Tip),
    };

    public static int Count => definitions.Length;

    public static RoguelikeChallengeDefinition Get(int _index) => definitions[_index];

    /// <summary>현재 언어로 번역한 제목. 나무 · 언리밋은 {0}에 목표값을 넣는다(나무는 천 단위 콤마, 검수 메뉴의 견본 값 "10,000"과 같은 표기).</summary>
    public static string BuildTitle(int _index, LocalizationManager _localization)
    {
        if (null == _localization || _index < 0 || _index >= definitions.Length) return string.Empty;

        RoguelikeChallengeDefinition def = definitions[_index];
        switch (def.kind)
        {
            case ERoguelikeChallengeKind.TreeN:
                return _localization.GetFormatText(def.titleKey, def.target.ToString("N0", CultureInfo.InvariantCulture));
            case ERoguelikeChallengeKind.UnlimitN:
                return _localization.GetFormatText(def.titleKey, def.target);
            default:
                return _localization.GetText(def.titleKey);
        }
    }

    public static string BuildTip(int _index, LocalizationManager _localization)
    {
        if (null == _localization || _index < 0 || _index >= definitions.Length) return string.Empty;
        return _localization.GetText(definitions[_index].tipKey);
    }

    /// <summary>
    /// 목록 창용 뷰 데이터를 정의 순서대로 _results에 더한다(지우지 않는다). 메인메뉴 도전과제 창을 열 때 한 번 부른다.
    /// _records가 null이면 모두 미달성으로 그린다.
    /// </summary>
    public static void FillViewData(RoguelikeRecordData _records, LocalizationManager _localization, List<RoguelikeChallengeViewData> _results)
    {
        if (null == _results) return;

        List<string> cleared = null != _records ? _records.challengeClearedIds : null;
        for (int i = 0; i < definitions.Length; i++)
        {
            _results.Add(new RoguelikeChallengeViewData
            {
                title = BuildTitle(i, _localization),
                tip = BuildTip(i, _localization),
                bCleared = null != cleared && cleared.Contains(definitions[i].id),
            });
        }
    }
}

/// <summary>
/// 도전과제 달성 판정(기획서 v3.1 8-3 "판정 시점", 부록 C-3 4-20 ⑧). RoguelikeSystem이 판정 지점마다 부르고,
/// 새로 달성한 칸이 있으면 NewlyCleared를 읽어 저장 · 달성 신호를 낸다. 진행은 같은 RoguelikeRecordData에 바로 쌓이므로
/// 게임오버 · 포기로 끝난 판도 런 종료 저장(FinishRunRoutine)에 함께 남는다.
///
/// 전체 수(프리즘 · 레벨 있는 카드 · 루비)는 카드 DB에서, 마지막 티어는 밸런스의 티어 수에서 센다(하드코딩하지 않는다).
/// 판정 지점에서 매번 할당하지 않는다(달성 플래그 배열 · 결과 리스트를 재사용, 문자열 추가는 진행이 처음 생길 때만).
/// </summary>
public sealed class RoguelikeChallengeTracker
{
    private RoguelikeRecordData records;
    private int prismTotal;
    private int levelCardTotal;
    private int rubyTotal;
    private int tierTotal;

    private readonly bool[] clearedFlags = new bool[RoguelikeChallenges.Count];
    private readonly List<int> newlyCleared = new List<int>(RoguelikeChallenges.Count);

    /// <summary>직전 판정 호출에서 새로 달성한 도전과제 인덱스(정의 순서).</summary>
    public IReadOnlyList<int> NewlyCleared => newlyCleared;

    public void Initialize(RoguelikeRecordData _records, CardDatabaseSO _cardDatabase, int _tierCount)
    {
        records = _records;
        tierTotal = _tierCount;
        prismTotal = 0;
        levelCardTotal = 0;
        rubyTotal = 0;

        if (null != _cardDatabase && null != _cardDatabase.cards)
        {
            for (int i = 0; i < _cardDatabase.cards.Count; i++)
            {
                CardDefinitionSO card = _cardDatabase.cards[i];
                if (null == card) continue;

                switch (card.kind)
                {
                    case ECardKind.Active:
                    case ECardKind.Passive: levelCardTotal++; break;
                    case ECardKind.Ruby: rubyTotal++; break;
                    case ECardKind.Prism: prismTotal++; break;
                }
            }
        }

        for (int i = 0; i < clearedFlags.Length; i++)
        {
            clearedFlags[i] = null != records && records.challengeClearedIds.Contains(RoguelikeChallenges.Get(i).id);
        }

        newlyCleared.Clear();
    }

    /// <summary>카드를 고른 순간(CardShop.TryBuy 성공). 프리즘 해제 · MAX 도달 · 루비 선택을 센다.</summary>
    public void OnCardPurchased(in CardPurchaseResult _purchase)
    {
        newlyCleared.Clear();
        if (null == records || null == _purchase.card || string.IsNullOrEmpty(_purchase.card.cardId)) return;

        CardDefinitionSO card = _purchase.card;
        switch (card.kind)
        {
            case ECardKind.Active:
            case ECardKind.Passive:
                if (_purchase.newLevel >= card.EffectiveMaxLevel && AddUnique(records.challengeMaxedCardIds, card.cardId))
                {
                    Evaluate(ERoguelikeChallengeKind.CardMaxAll);
                }
                break;
            case ECardKind.Ruby:
                if (AddUnique(records.challengeRubyCardIds, card.cardId)) Evaluate(ERoguelikeChallengeKind.RubyAll);
                break;
            case ECardKind.Prism:
                if (AddUnique(records.challengePrismCardIds, card.cardId)) Evaluate(ERoguelikeChallengeKind.PrismAll);
                break;
        }
    }

    /// <summary>나무를 쓰러뜨릴 때마다(플레이 중 실제 나무만, 디버그 웨이브 클리어는 세지 않는다).</summary>
    public void OnTreeFelled()
    {
        newlyCleared.Clear();
        if (null == records) return;

        if (records.challengeTreesFelled < int.MaxValue) records.challengeTreesFelled++;
        Evaluate(ERoguelikeChallengeKind.TreeN);
    }

    /// <summary>웨이브 전투가 시작될 때. 티어(마지막 티어 첫 웨이브)와 언리밋(Unlimit-N 시작) 도달을 센다.</summary>
    public void OnWaveStarted(in WaveSpec _spec)
    {
        newlyCleared.Clear();
        if (null == records) return;

        int tier = _spec.tierIndex + 1;
        if (tier > records.challengeBestTier)
        {
            records.challengeBestTier = tier;
            Evaluate(ERoguelikeChallengeKind.TierAll);
        }

        if (_spec.bUnlimit && _spec.unlimitIndex > records.challengeBestUnlimit)
        {
            records.challengeBestUnlimit = _spec.unlimitIndex;
            Evaluate(ERoguelikeChallengeKind.UnlimitN);
        }
    }

    /// <summary>30웨이브를 클리어한 순간(런 클리어).</summary>
    public void OnRunCleared()
    {
        newlyCleared.Clear();
        if (null == records) return;

        Evaluate(ERoguelikeChallengeKind.Wave30);
    }

    /// <summary>_kind 종류의 칸 중 아직 달성하지 않았고 조건을 채운 칸을 달성 처리한다.</summary>
    private void Evaluate(ERoguelikeChallengeKind _kind)
    {
        for (int i = 0; i < clearedFlags.Length; i++)
        {
            if (clearedFlags[i]) continue;

            RoguelikeChallengeDefinition def = RoguelikeChallenges.Get(i);
            if (def.kind != _kind || false == IsMet(in def)) continue;

            clearedFlags[i] = true;
            AddUnique(records.challengeClearedIds, def.id);
            newlyCleared.Add(i);
        }
    }

    private bool IsMet(in RoguelikeChallengeDefinition _def)
    {
        switch (_def.kind)
        {
            case ERoguelikeChallengeKind.PrismAll: return prismTotal > 0 && records.challengePrismCardIds.Count >= prismTotal;
            case ERoguelikeChallengeKind.CardMaxAll: return levelCardTotal > 0 && records.challengeMaxedCardIds.Count >= levelCardTotal;
            case ERoguelikeChallengeKind.RubyAll: return rubyTotal > 0 && records.challengeRubyCardIds.Count >= rubyTotal;
            case ERoguelikeChallengeKind.TreeN: return records.challengeTreesFelled >= _def.target;
            case ERoguelikeChallengeKind.UnlimitN: return records.challengeBestUnlimit >= _def.target;
            case ERoguelikeChallengeKind.TierAll: return tierTotal > 0 && records.challengeBestTier >= tierTotal;
            case ERoguelikeChallengeKind.Wave30: return true;
            default: return false;
        }
    }

    private static bool AddUnique(List<string> _list, string _value)
    {
        if (_list.Contains(_value)) return false;

        _list.Add(_value);
        return true;
    }
}
