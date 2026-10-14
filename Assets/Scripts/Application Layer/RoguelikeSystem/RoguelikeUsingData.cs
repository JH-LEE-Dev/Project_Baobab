using System;
using UnityEngine;

/// <summary>로비에서 타격으로 실행하는 동작(기획서 4-2).</summary>
public enum ERoguelikeLobbyAction
{
    StartRun = 0,
    OpenRecords = 1,
}

/// <summary>
/// 시스템이 "끝날 때까지 기다리는" 연출 지점. 이펙트 담당은 IRoguelikeProductionHandler로 여기에 연출을 꽂는다.
/// 핸들러가 없으면 RoguelikeSystem의 대체 대기 시간만큼 기다린 뒤 그냥 진행한다(연출 없이도 루프가 끝까지 돈다).
/// </summary>
public enum ERoguelikeProduction
{
    /// <summary>로비 진입: 디스토션 → 암전 → 바닥 타일 파도 → 배경 조립 → 플레이어 낙하(기획서 4-1). 지형은 이미 깔린 상태로 시작한다.</summary>
    LobbyEntry = 0,
    /// <summary>티어 전환: 바닥 타일 파도(기획서 3-4). 로비 진입의 타일 파도와 같은 연출을 공용으로 쓴다.</summary>
    TierTransition = 1,
    /// <summary>웨이브 클리어: 남은 나무가 묘목으로 돌아가며 사라짐 + 코인 일괄 흡수(동시에 진행, 기획서 3-3 골드 획득 4단계).</summary>
    WaveClear = 2,
    /// <summary>30웨이브 마지막 나무 줌인 + 슬로우 부서짐 → 줌아웃(기획서 3-5). 이 연출이 끝나면 언리밋으로 넘어간다.</summary>
    RunClear = 3,
    /// <summary>결과 화면 확인 직후 디스토션(기획서 3-7 종료 흐름 5). 이어서 LobbyEntry가 재생된다.</summary>
    ResultToLobby = 4,
    /// <summary>로비 → 런 시작 전환. 기획서에 정의되지 않은 연출이라 기본은 대기 없이 바로 넘어간다.</summary>
    LobbyToRun = 5,
}

/// <summary>연출 하나에 넘겨주는 정보.</summary>
public struct RoguelikeProductionRequest
{
    public ERoguelikeProduction type;
    /// <summary>LobbyEntry: 결과 화면에서 돌아오는 중인지(디스토션을 메인메뉴가 아니라 결과 화면에 건다).</summary>
    public bool bFromResult;
    /// <summary>RunClear: 마지막 나무 위치.</summary>
    public Vector3 position;
    /// <summary>TierTransition: 새 티어.</summary>
    public int tierIndex;
    public MapType mapType;
    public Transform characterTransform;
}

/// <summary>
/// 이펙트 담당이 구현하는 연출 핸들러. RoguelikeSystem 프리팹의 자식에 붙여 두거나 RoguelikeProductionGate.Register로 등록한다.
/// _onComplete는 연출이 끝났을 때 반드시 한 번 불러야 한다. 부르지 않으면 대기 상한(maxWaitSeconds) 뒤 시스템이 강제로 진행한다.
/// </summary>
public interface IRoguelikeProductionHandler
{
    bool CanHandle(ERoguelikeProduction _type);
    void Play(in RoguelikeProductionRequest _request, Action _onComplete);
}
