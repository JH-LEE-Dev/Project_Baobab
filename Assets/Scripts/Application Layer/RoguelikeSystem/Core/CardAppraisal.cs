using System.Collections.Generic;
using UnityEngine;

/// <summary>감정 한 번의 결과. 결과는 감정비를 낸 순간 확정되고, 스티커를 뜯는 조작은 확인 연출일 뿐이다(기획서 4-4).</summary>
public struct CardAppraisalResult
{
    public CardDefinitionSO card;
    public int fromLevel;
    /// <summary>감정 뒤 레벨. 결과가 현재 레벨보다 낮거나 같으면 fromLevel과 같다(하락 없음).</summary>
    public int toLevel;
    public int feePaid;
    /// <summary>승격으로 제시 중이던 같은 카드 칸의 레벨·가격이 바뀌었거나 다른 카드로 바뀌었는지(UI가 제시 목록을 다시 그릴지).</summary>
    public bool bOffersChanged;

    public bool bPromoted => toLevel > fromLevel;
}

public enum ECardAppraisalResult
{
    Success = 0,
    NotAvailable = 1,
    NotEligible = 2,
    NotEnoughGold = 3,
    LimitReached = 4,
}

/// <summary>
/// 카드 감정(한 판 한 번의 도박) 규칙. 기획서 4-4.
///   - 런 시작 때 appraiserWaveMin~Max 중 한 웨이브를 정하고, 그 웨이브의 카드 선택 단계에만 감정사가 있다.
///   - 보유한 레벨 I~IV 액티브·공통 패시브를 서로 다른 카드로 최대 appraisalMaxCards장까지 맡긴다. MAX·루비·프리즘은 대상이 아니다.
///   - 결과 분포는 보유 레벨별로 다르고, 하락은 없다.
///   - 감정사가 있는 선택 단계가 끝나면 남은 기회는 사라진다(다음 선택으로 이월하지 않는다).
/// 골드 차감과 레벨 반영은 CardShop이 한다. 이 클래스는 "누가, 얼마에, 무엇이 나오는가"만 정한다.
/// </summary>
public sealed class CardAppraisal
{
    private readonly HashSet<CardDefinitionSO> appraisedCards = new HashSet<CardDefinitionSO>();
    private System.Random rng = new System.Random();

    /// <summary>이번 판에 감정사가 등장하는 웨이브. 0이면 등장하지 않는다(밸런스 범위가 비었을 때).</summary>
    public int AppraiserWave { get; private set; }
    public int AppraisedCount => appraisedCards.Count;

    public void ResetRun(int _seed, RoguelikeBalanceSO _balance)
    {
        rng = new System.Random(_seed);
        appraisedCards.Clear();
        AppraiserWave = 0;

        if (null == _balance) return;

        int min = Mathf.Max(1, _balance.appraiserWaveMin);
        int max = Mathf.Max(min, _balance.appraiserWaveMax);
        if (_balance.appraisalMaxCards > 0) AppraiserWave = rng.Next(min, max + 1);
    }

    public bool IsAppraiserWave(int _waveNumber) => AppraiserWave > 0 && _waveNumber == AppraiserWave;

    public bool HasRemaining(RoguelikeBalanceSO _balance) => null != _balance && appraisedCards.Count < _balance.appraisalMaxCards;

    public bool WasAppraised(CardDefinitionSO _card) => null != _card && appraisedCards.Contains(_card);

    /// <summary>_card가 감정 대상인지(골드와 무관). 보유 레벨 I~IV의 액티브·공통 패시브이고 이번 판에 아직 맡기지 않은 카드.</summary>
    public bool IsEligible(CardDefinitionSO _card, RunBuildState _build)
    {
        if (null == _card || null == _build) return false;
        if (ECardKind.Active != _card.kind && ECardKind.Passive != _card.kind) return false;
        if (appraisedCards.Contains(_card)) return false;

        int level = _build.GetLevel(_card);
        return level >= 1 && level < _card.EffectiveMaxLevel;
    }

    /// <summary>결과 레벨을 뽑는다. 결과가 MAX를 넘으면 MAX, 현재보다 낮을 일은 없다.</summary>
    public int RollResultLevel(int _currentLevel, int _maxLevel, RoguelikeBalanceSO _balance)
    {
        RoguelikeBalanceSO.AppraisalOutcome outcome = _balance.GetAppraisalOutcome(_currentLevel);
        float stay = Mathf.Max(0f, outcome.stay);
        float plusOne = Mathf.Max(0f, outcome.plusOne);
        float plusTwo = Mathf.Max(0f, outcome.plusTwo);
        float max = Mathf.Max(0f, outcome.max);

        float total = stay + plusOne + plusTwo + max;
        if (total <= 0f) return _currentLevel;

        double roll = rng.NextDouble() * total;

        int result;
        if (roll < stay) result = _currentLevel;
        else if (roll < stay + plusOne) result = _currentLevel + 1;
        else if (roll < stay + plusOne + plusTwo) result = _currentLevel + 2;
        else result = _maxLevel;

        return Mathf.Clamp(result, _currentLevel, _maxLevel);
    }

    public void MarkAppraised(CardDefinitionSO _card)
    {
        if (null != _card) appraisedCards.Add(_card);
    }
}
