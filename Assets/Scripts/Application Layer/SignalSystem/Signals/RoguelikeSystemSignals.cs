using UnityEngine;

// 로그라이크 모드 시그널. 로그라이크 세션에서만 발행된다(본편에서는 아무도 발행하지 않는다).
//
// [이펙트·UI 작업자용] 연출을 입힐 지점은 대부분 아래 상태 알림 시그널을 구독하면 된다.
// 시스템이 연출 완료를 "기다려야" 하는 지점은 시그널이 아니라 RoguelikeProductionGate를 쓴다
// (IRoguelikeProductionHandler 참고). 기다릴 필요가 없는 연출은 시그널만 듣고 재생하면 된다.

// // 상태 알림 (시스템 → 누구나)

/// <summary>런 상태가 바뀌었다. 기획서 7장의 상태 하나하나에 대응한다.</summary>
public struct RoguelikePhaseChangedSignal
{
    public ERunPhase phase;
    public ERunPhase prevPhase;
    public RoguelikePhaseChangedSignal(ERunPhase _phase, ERunPhase _prevPhase)
    {
        phase = _phase;
        prevPhase = _prevPhase;
    }
}

/// <summary>새 런이 시작됐다(골드 0, 보유 카드 없음, 리롤 비용 초기화).</summary>
public struct RoguelikeRunStartedSignal { }

/// <summary>웨이브 전투가 시작됐다.</summary>
public struct RoguelikeWaveStartedSignal
{
    public WaveSpec spec;
    public RoguelikeWaveStartedSignal(WaveSpec _spec)
    {
        spec = _spec;
    }
}

/// <summary>처치 수가 바뀌었다(웨이브 진행도).</summary>
public struct RoguelikeWaveProgressSignal
{
    public int killed;
    public int target;
    public RoguelikeWaveProgressSignal(int _killed, int _target)
    {
        killed = _killed;
        target = _target;
    }
}

/// <summary>목표 처치 수를 채웠다. 스폰이 멈추고 남은 나무가 묘목으로 돌아가기 시작한다.</summary>
public struct RoguelikeWaveClearedSignal
{
    public WaveSpec spec;
    public RoguelikeWaveClearedSignal(WaveSpec _spec)
    {
        spec = _spec;
    }
}

/// <summary>
/// 30웨이브 목표의 마지막 나무를 벤 순간(기획서 3-5 "런 클리어 연출"의 줌인 대상).
/// 일반 웨이브에서는 발행되지 않는다.
/// </summary>
public struct RoguelikeFinalTreeKilledSignal
{
    public Vector3 position;
    public RoguelikeFinalTreeKilledSignal(Vector3 _position)
    {
        position = _position;
    }
}

/// <summary>티어가 바뀌어 지형이 교체됐다(지형 전환 연출 직전).</summary>
public struct RoguelikeTierChangedSignal
{
    public int tierIndex;
    public MapType mapType;
    public RoguelikeTierChangedSignal(int _tierIndex, MapType _mapType)
    {
        tierIndex = _tierIndex;
        mapType = _mapType;
    }
}

/// <summary>
/// 바닥 타일 파도가 시작됐다. 두 경우에 나간다 - bEntry로 구분한다.
///   bEntry = true : 로비 진입(기획서 4-1 3~5단계). 물만 깔린 바다에서 섬 중앙부터 육지가 물결을 따라 깔리고, 이어서 해안선이 퍼즐처럼 조립된다.
///   bEntry = false: 티어 전환(기획서 3-4). 물결이 지나가며 칸마다 솟았다 가라앉고, 첫 마루에서 직전 지형이 새 지형으로 바뀐다.
/// 타일 한 칸 단위 장식은 RoguelikeSystem.TileWave.TileRevealedEvent를 쓴다(진입은 칸이 생기는 순간, 전환은 칸이 바뀌는 순간).
/// </summary>
public struct RoguelikeTileWaveStartedSignal
{
    public bool bEntry;
    public RoguelikeTileWaveStartedSignal(bool _bEntry)
    {
        bEntry = _bEntry;
    }
}

/// <summary>
/// 로비 진입 연출 1~2단계(디스토션 → 암전)가 끝나 화면이 검게 덮였다(기획서 4-1). 결과 화면 UI는 이때 닫는다 -
/// 디스토션이 결과 화면 위에 걸려야 하므로 확인 버튼을 누른 순간이 아니라 여기서 닫는다.
/// </summary>
public struct RoguelikeEntryScreenCoveredSignal { }

/// <summary>
/// 로비 진입 암전이 걷히기 시작했다(기획서 v2.2 9-1 3단계 페이드 아웃, 약 0.3초). 화면에는 물만 깔린 바다가 드러나고,
/// 같은 프레임에 바닥 타일 파도(RoguelikeTileWaveStartedSignal, bEntry = true)도 시작된다 - 걷힌 뒤가 아니라 걷히는 동안 파도가 돈다.
/// </summary>
public struct RoguelikeEntryScreenRevealStartedSignal { }

/// <summary>
/// 바닥 타일 파도가 끝났다(도중에 끊겨 바로 채운 경우 포함). 화면 안 지형이 전부 새 지형이다.
/// bEntry는 시작 신호와 같다(로비 진입 파도면 true, 티어 전환이면 false).
/// </summary>
public struct RoguelikeTileWaveCompletedSignal
{
    public bool bEntry;
    public RoguelikeTileWaveCompletedSignal(bool _bEntry)
    {
        bEntry = _bEntry;
    }
}

/// <summary>플레이어 낙하가 시작됐다(기획서 4-1 6단계). from에서 to로 duration초 동안 떨어진다.</summary>
public struct RoguelikeCharacterDropStartedSignal
{
    public Vector3 from;
    public Vector3 to;
    public float duration;
    public RoguelikeCharacterDropStartedSignal(Vector3 _from, Vector3 _to, float _duration)
    {
        from = _from;
        to = _to;
        duration = _duration;
    }
}

/// <summary>플레이어가 착지했다. 착지 먼지·효과음을 여기에 붙인다.</summary>
public struct RoguelikeCharacterLandedSignal
{
    public Vector3 position;
    public RoguelikeCharacterLandedSignal(Vector3 _position)
    {
        position = _position;
    }
}

/// <summary>30웨이브 클리어. 곧바로 언리밋으로 이어진다.</summary>
public struct RoguelikeRunClearedSignal { }

/// <summary>임계치 판정 상태가 바뀌었다(안전/유예/지속 피해).</summary>
public struct RoguelikePressureStateChangedSignal
{
    public EPressureState state;
    public RoguelikePressureStateChangedSignal(EPressureState _state)
    {
        state = _state;
    }
}

/// <summary>임계치 피해를 한 번 입었다.</summary>
public struct RoguelikePressureDamagedSignal
{
    public float damage;
    public RoguelikePressureDamagedSignal(float _damage)
    {
        damage = _damage;
    }
}

/// <summary>카드를 골랐다. 효과는 이미 적용된 상태다.</summary>
public struct RoguelikeCardPurchasedSignal
{
    public CardPurchaseResult result;
    public RoguelikeCardPurchasedSignal(CardPurchaseResult _result)
    {
        result = _result;
    }
}

/// <summary>빌드 효과 합계가 다시 계산됐다(카드 선택, 런 시작).</summary>
public struct RoguelikeModifiersChangedSignal { }

/// <summary>런이 끝났다(게임오버 또는 포기). 기록은 이미 저장된 상태다.</summary>
public struct RoguelikeRunEndedSignal
{
    public ERunEndReason reason;
    public RoguelikeRunEndedSignal(ERunEndReason _reason)
    {
        reason = _reason;
    }
}

/// <summary>[UnitSystem → 로그라이크] 캐릭터가 쓰러지는 연출이 끝났다(본편이라면 결과창을 띄우는 시점).</summary>
public struct RoguelikeCharacterDownCompletedSignal { }

// // 요청 (UI → 시스템)

public struct RoguelikeCardBuyRequestedSignal
{
    public int offerIndex;
    public RoguelikeCardBuyRequestedSignal(int _offerIndex)
    {
        offerIndex = _offerIndex;
    }
}

public struct RoguelikeCardRerollRequestedSignal { }

/// <summary>카드 UI가 선택·소멸 연출을 다 보여 줬다. 시스템은 이 신호를 받고서야 다음 단계로 넘어간다.</summary>
public struct RoguelikeCardSelectionPresentationDoneSignal { }

public struct RoguelikeResultConfirmedSignal { }

public struct RoguelikeRecordsClosedSignal { }

/// <summary>로비 오브젝트를 타격했다.</summary>
public struct RoguelikeLobbyActionSignal
{
    public ERoguelikeLobbyAction action;
    public RoguelikeLobbyActionSignal(ERoguelikeLobbyAction _action)
    {
        action = _action;
    }
}

// // 카드 선택·결과 화면 (시스템 → UI 코디네이터)

/// <summary>카드 선택 단계가 열렸다. 제시 카드는 IRoguelikeRunInfo.CurrentOffers.</summary>
public struct RoguelikeCardSelectionOpenedSignal { }

/// <summary>리롤로 제시 카드가 바뀌었다.</summary>
public struct RoguelikeOffersChangedSignal { }

/// <summary>
/// 카드를 고르지 않은 채 선택이 닫혔다(선택 화면이 열린 채 포기·게임오버로 런이 끊긴 경우). v2.4에는 건너뛰기가 없어
/// 플레이어 입력으로는 오지 않는다. UI는 열려 있던 선택 화면을 닫는다.
/// </summary>
public struct RoguelikeCardSelectionSkippedSignal { }

/// <summary>선택·리롤 요청이 거절됐다(리롤을 이미 씀, 고를 수 없는 칸 등).</summary>
public struct RoguelikeCardShopRejectedSignal
{
    public ECardShopResult reason;
    public int offerIndex;
    public RoguelikeCardShopRejectedSignal(ECardShopResult _reason, int _offerIndex)
    {
        reason = _reason;
        offerIndex = _offerIndex;
    }
}

/// <summary>
/// 노출 가능한 카드가 0장이라 선택 화면 대신 "선택할 수 있는 카드가 없습니다" 안내를 띄울 차례다(기획서 v2.4 4-2).
/// 안내가 끝나면 RoguelikeCardSelectionPresentationDoneSignal을 보낸다. 확인 입력 없이 웨이브가 시작된다.
/// </summary>
public struct RoguelikeNoCardsAvailableSignal { }

/// <summary>결과 화면을 띄울 차례다. 내용은 IRoguelikeRunInfo.LastRunRecord.</summary>
public struct RoguelikeResultReadySignal { }

/// <summary>순위표를 띄울 차례다. 내용은 IRoguelikeRunInfo.Records.</summary>
public struct RoguelikeRecordsRequestedSignal { }

// // 신규 스킬·조합 연출 (시스템 → 이펙트)

/// <summary>로그라이크 신규 스킬·조합 효과의 연출 지점. 판정과 피해는 이미 끝난 뒤에 발행된다.</summary>
public enum ERoguelikeSkillVfx
{
    /// <summary>오프로드가 필드 끝에 플래시되며 소환됨. position=출발점, position2=도착점, duration=예열 시간.</summary>
    OffroadWarmup = 0,
    /// <summary>오프로드 돌진 시작(예열이 끝나 출발하는 순간). position=출발점, position2=반대편 섬 끝 멈추는 점, duration=멈칫 없이 달렸을 때의 돌진 시간(대략).</summary>
    OffroadDash = 1,
    /// <summary>별 생성. id=별 번호, radius=이 별을 쳤을 때의 폭발 반경, duration=별의 수명(초). 스텔라+드론의 별은 쳐도 터지지 않고 노바 때까지 사라지지 않아 radius·duration이 0이다.</summary>
    StarSpawned = 2,
    /// <summary>별을 쳐서 단발 공격. radius=공격 반경.</summary>
    StarBlast = 3,
    /// <summary>별이 수명이 다해 사라짐.</summary>
    StarExpired = 4,
    /// <summary>포자 폭탄 투하(캐릭터 → 착지점). position=착지점, position2=던진 자리, radius=대폭발 범위, duration=날아가는 시간(초).</summary>
    SporeBombThrown = 5,
    /// <summary>360도 충격파(회전베기+충격파, 부메랑+충격파).</summary>
    Shockwave360 = 6,
    /// <summary>부메랑 타격에 붙은 회전베기(회전베기+부메랑). position=캐릭터, radius.</summary>
    SpinBoomerangStrike = 7,
    /// <summary>포자 폭발(본편 4연발). radius. v2.6부터 포자 조합 3종은 SporeBigExplosion(포자 수류탄 대폭발)을 써서 지금은 내는 곳이 없다.</summary>
    SporeExplosion = 8,
    /// <summary>별 이음선 빔(스텔라+드론). position→position2.</summary>
    StarLinkBeam = 9,
    /// <summary>노바(스텔라+드론). position=중심, radius.</summary>
    StarNova = 10,
    /// <summary>홀로 남은 별의 표식 공격(스텔라+드론). radius.</summary>
    LoneStarPulse = 11,
    /// <summary>별 이음선 구성이 바뀜(스텔라+드론). 연결선 그리기는 IRoguelikeStarLinkProvider로 읽는다.</summary>
    StarLinksChanged = 12,
    /// <summary>오프로드가 나무를 들이받음(충돌 먼지·파편 자리). position=차량 위치, radius=돌진 폭, id=같은 순간에 맞은 나무 수.</summary>
    OffroadHit = 13,
    /// <summary>오프로드가 반대편 섬 끝에 닿기 직전이라 달리는 채로 플래시되며 사라지기 시작함. position=그 순간 위치, duration=사라지기까지의 시간.</summary>
    OffroadExit = 14,
    // 15는 폐지된 포자 연막 연장(SporeZoneExtended) 자리다. v2.4에서 연막이 오염으로 바뀌었다.
    /// <summary>스텔라 V 별 폭발 2회 연속의 뒤이은 폭발. 별은 첫 폭발(StarBlast) 때 이미 사라졌다. position, radius, id=그 별 번호.</summary>
    StarBlastRepeat = 16,
    /// <summary>포자 폭탄이 떨어져 본편 포자 대폭발이 일어남. 범위 안 나무는 오염된다. position=폭발 중심, radius=대폭발 범위(등각 타원 장반경), id=포자폭탄 레벨 단계(1~3).</summary>
    SporeBigExplosion = 17,
    /// <summary>별이 도끼 범위(인디케이터) 안에 들어와 나무처럼 아웃라인으로 강조됨. position=별 위치, id=별 번호. 범위에서 빠지면 StarHighlightOff.</summary>
    StarHighlightOn = 18,
    /// <summary>별 강조가 꺼짐(도끼 범위에서 빠짐, 맞아서 터짐, 사라짐). position=별 위치, id=별 번호.</summary>
    StarHighlightOff = 19,
    /// <summary>스텔라 III: 드론 레이저가 사거리 안의 별을 맞힘. position=드론 총구, position2=별 위치, id=별 번호.</summary>
    DroneStarShot = 20,
}

public struct RoguelikeSkillVfxSignal
{
    public ERoguelikeSkillVfx type;
    public Vector3 position;
    public Vector3 position2;
    public float radius;
    public float duration;
    public int id;

    public RoguelikeSkillVfxSignal(ERoguelikeSkillVfx _type, Vector3 _position, Vector3 _position2, float _radius, float _duration, int _id)
    {
        type = _type;
        position = _position;
        position2 = _position2;
        radius = _radius;
        duration = _duration;
        id = _id;
    }
}
