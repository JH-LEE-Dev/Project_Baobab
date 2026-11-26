using System.Collections.Generic;
using UnityEngine;

// 로그라이크 UIView가 받는 표시용 데이터. 뷰는 게임 규칙 객체(CardShop, RunBuildState 등)를 직접 보지 않고
// 이 구조체만 받는다. 판단(선택할 수 있는지, 에메랄드인지 등)은 시스템이 끝내서 넘긴다.

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
    /// <summary>[삭제 예정] 뷰가 읽지 않는다(코디네이터가 값만 넣는다). Application Layer 정리 후 삭제 예정(기획서 v3.2 C-2).</summary>
    public bool bNewCard;
    /// <summary>[삭제 예정] 뷰가 읽지 않는다(코디네이터가 값만 넣는다). Application Layer 정리 후 삭제 예정(기획서 v3.2 C-2).</summary>
    public bool bFirstSeen;
    /// <summary>[삭제 예정] 뷰가 읽지 않는다(희귀 판정은 displayGrade로 한다). Application Layer 정리 후 삭제 예정(기획서 v3.2 C-2).</summary>
    public bool bRare;
    /// <summary>이 칸을 아직 리롤할 수 있는가(카드 칸마다 1회, 기획서 4-1). 칸별 모드에서만 읽는다. 코디네이터가 CanRerollSlot(i)로 채운다(G-12).</summary>
    public bool bCanReroll;
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
    /// <summary>[삭제 예정] 뷰가 읽지 않는다(코디네이터가 값만 넣는다). Application Layer 정리 후 삭제 예정(기획서 v3.2 C-2).</summary>
    public int newLevel;
    /// <summary>[삭제 예정] 뷰가 읽지 않는다(코디네이터가 값만 넣는다). Application Layer 정리 후 삭제 예정(기획서 v3.2 C-2).</summary>
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
    /// <summary>그 판에 부순 나무 수(저장 데이터 RunRecordEntry.treesFelled).</summary>
    public int treesFelled;
    public List<RoguelikeOwnedCardViewData> cards;
}

/// <summary>카드 도감의 칸 한 장. 도감 순서(UIView_RoguelikeCollection.CollectionCardIds)대로 RoguelikeCollectionViewBuilder가 만든다.</summary>
public struct RoguelikeCollectionViewData
{
    public string cardId;
    public string nameKey;
    public string descKey;
    public CardDefinitionSO card;
    public Sprite icon;
    public ECardKind kind;
    /// <summary>한 번이라도 고른 카드(앞면). false면 뒷면 칸이다(히든 조합 규칙: 이름 · 재료를 보이지 않는다).</summary>
    public bool bFound;
    /// <summary>한 번이라도 MAX를 찍은 레벨 있는 카드(금 칸 + MAX 배지).</summary>
    public bool bMaxed;
    /// <summary>지난번 도감을 닫은 뒤 새로 고른 카드(뒤집힘 연출 + NEW 배지). 발견했고 collectionSeenCardIds에 없는 카드다.</summary>
    public bool bNew;
}

