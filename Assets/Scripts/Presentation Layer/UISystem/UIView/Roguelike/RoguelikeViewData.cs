using System.Collections.Generic;
using UnityEngine;

// 로그라이크 UIView가 받는 표시용 데이터. 뷰는 게임 규칙 객체(CardShop, RunBuildState 등)를 직접 보지 않고
// 이 구조체만 받는다. 판단(살 수 있는지, 에메랄드인지, 처음 보는 카드인지)은 시스템이 끝내서 넘긴다.

/// <summary>카드 선택 화면의 카드 한 장. 기획서 5-2, 5-3.</summary>
public struct RoguelikeCardViewData
{
    public string cardId;
    public string nameKey;
    public string descriptionKey;
    /// <summary>카드 데이터. 설명 패널이 레벨별 효과 수치(levels)를 읽어 "선택 후 효과" 문구를 만든다(RoguelikeCardEffectText).</summary>
    public CardDefinitionSO card;
    public Sprite icon;
    public ECardKind kind;
    /// <summary>희귀 셰이더를 고르는 등급. 사면 MAX가 되는 카드는 Emerald.</summary>
    public ECardDisplayGrade displayGrade;
    /// <summary>구매 후 레벨(1~5). 이미지 아래 I, II, III, IV, MAX 표기. 루비는 레벨 대신 ± 표기(bRuby).</summary>
    public int levelAfterPurchase;
    public int maxLevel;
    public bool bRuby;
    /// <summary>보유하지 않은 카드 → 자물쇠가 풀리는 등장 연출(기획서 5-3 "첫 등장 카드").</summary>
    public bool bNewCard;
    /// <summary>이번 판에서 처음 제시된 카드.</summary>
    public bool bFirstSeen;
    /// <summary>희귀 셰이더 연출 대상(MAX가 되는 에메랄드·프리즘·계약).</summary>
    public bool bRare;
}

/// <summary>보유 카드 확인 UI의 카드 한 장. 기획서 5-2 "보유 카드 확인".</summary>
public struct RoguelikeOwnedCardViewData
{
    public string cardId;
    public string nameKey;
    public string descriptionKey;
    /// <summary>카드 데이터(현재 레벨 효과 문구용).</summary>
    public CardDefinitionSO card;
    public Sprite icon;
    public ECardKind kind;
    public ECardDisplayGrade displayGrade;
    /// <summary>현재 레벨. 루비는 1.</summary>
    public int level;
    public int maxLevel;
    public bool bRuby;
}

/// <summary>구매 결과(구매 연출용).</summary>
public struct RoguelikeCardPurchaseViewData
{
    public int offerIndex;
    public int newLevel;
    public bool bNewCard;
    public ECardDisplayGrade displayGrade;
}

/// <summary>결과 화면 한 판. 기획서 3-7 "결과 화면 항목".</summary>
public struct RoguelikeResultViewData
{
    /// <summary>일반 구간 도달 웨이브(1~30).</summary>
    public int reachedWave;
    /// <summary>언리밋이면 Unlimit-N 표기.</summary>
    public bool bUnlimit;
    public int unlimitWave;
    public bool bAbandoned;
    public float playTimeSeconds;
    public int treesFelled;
    public long coinsCollected;
    public List<RoguelikeOwnedCardViewData> cards;
    /// <summary>이번 판이 들어간 랭킹 순위(0부터). 못 들어갔으면 -1. "신기록" 연출용.</summary>
    public int clearRank;
    public int unlimitRank;
}

/// <summary>순위표 한 줄. 기획서 3-6 "세 탭 공통 표시 정보".</summary>
public struct RoguelikeRecordViewData
{
    public bool bValid;
    public int reachedWave;
    public bool bCleared;
    public int unlimitWave;
    public float playTimeSeconds;
    /// <summary>클리어 랭킹 비교 시간(클리어한 판은 30웨이브 클리어 시점).</summary>
    public float rankingTimeSeconds;
    public bool bAbandoned;
    public long recordedAtUtcTicks;
    public List<RoguelikeOwnedCardViewData> cards;
}
