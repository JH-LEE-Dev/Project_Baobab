#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// 게임 쪽에서 쓰는 도전과제 목록입니다. Steamworks 타입이 게임 코드로 번지지 않도록 여기서 끊습니다.
/// </summary>
public enum AchievementID
{
    EnterStage1,
    EnterStage2,
    EnterStage3,
    EnterStage4,
    EndingStarted,
}

/// <summary>
/// 스팀 도전과제를 해금합니다.
///
/// 얇게 감싸기만 하는 이유와 DISABLESTEAMWORKS 가드의 의미는 SteamCloudSaveService와 같습니다.
/// 스팀이 빠진 빌드(STOVE·itch)에서는 아무 일도 하지 않으며, 호출부에는 #if가 한 줄도 들어가지 않습니다.
///
/// [여러 번 불러도 되는 이유]
/// 이미 해금된 도전과제는 GetAchievement로 확인하고 건너뛰므로, 호출부는 "처음인지"를 따로 기억할 필요 없이
/// 해당 순간마다 Unlock을 부르면 됩니다. 기록은 스팀 계정에 남으므로 세이브와도 무관합니다.
///
/// [통계 동기화]
/// 이 프로젝트의 Steamworks SDK(1.61 이후)는 SteamAPI.Init에서 현재 통계를 자동으로 받아오므로
/// RequestCurrentStats가 없습니다. 해금은 SetAchievement 뒤 StoreStats로 서버에 올립니다.
/// </summary>
public static class SteamAchievementService
{
    /// <summary>
    /// Steamworks 웹 관리 화면(App Admin > Stats &amp; Achievements)에 등록한 "API 이름"과 글자 그대로 같아야 합니다.
    /// 데모와 정식은 서로 다른 스팀 앱이라, 등록되지 않은 앱에서는 SetAchievement가 실패만 하고 끝납니다.
    /// </summary>
    private static string GetApiName(AchievementID _id)
    {
        switch (_id)
        {
            case AchievementID.EnterStage1: return "ENTER_STAGE_1";
            case AchievementID.EnterStage2: return "ENTER_STAGE_2";
            case AchievementID.EnterStage3: return "ENTER_STAGE_3";
            case AchievementID.EnterStage4: return "ENTER_STAGE_4";
            case AchievementID.EndingStarted: return "ENDING_STARTED";
        }

        return null;
    }

    /// <summary>
    /// 숲 종류에 맞는 "N스테이지 첫 입장" 도전과제를 해금합니다. 숲이 아니면 아무 일도 없습니다.
    /// </summary>
    public static void UnlockStageEnter(MapType _mapType)
    {
        switch (_mapType)
        {
            case MapType.WideGreenForest: Unlock(AchievementID.EnterStage1); break;
            case MapType.FluffySporeForest: Unlock(AchievementID.EnterStage2); break;
            case MapType.StarrootForest: Unlock(AchievementID.EnterStage3); break;
            case MapType.MagmaForest: Unlock(AchievementID.EnterStage4); break;
        }
    }

#if !DISABLESTEAMWORKS
    // SetAchievement는 성공했는데 StoreStats가 실패한 상태. 로컬에는 이미 해금으로 찍혀 아래 GetAchievement가
    // true를 돌려주므로, 이 표시 없이는 같은 도전과제를 다시 불러도 서버에 영영 올라가지 않는다.
    private static bool bStorePending = false;

#if UNITY_2019_3_OR_NEWER
    // 도메인 리로드를 끈 에디터에서 이전 플레이의 값이 남지 않게 한다. (SteamManager.InitOnPlayMode와 같은 이유)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitOnPlayMode()
    {
        bStorePending = false;
    }
#endif

    private static bool TryStoreStats(string _apiName)
    {
        if (true == SteamUserStats.StoreStats())
        {
            bStorePending = false;
            return true;
        }

        bStorePending = true;
        Debug.LogWarning($"[SteamAchievementService] 도전과제 서버 저장 요청 실패: {_apiName} (다음 해금 시도 때 다시 올림)");
        return false;
    }
#endif

    public static void Unlock(AchievementID _id)
    {
#if !DISABLESTEAMWORKS
        string _apiName = GetApiName(_id);
        if (true == string.IsNullOrEmpty(_apiName)) return;

        // SteamManager.Initialized는 접근만으로 SteamManager를 만들어 SteamAPI를 초기화한다. (SteamLanguageService와 같은 경로)
        if (false == SteamManager.Initialized) return;

        // 앞서 서버 저장에 실패한 해금이 있으면 이번 도전과제와 상관없이 먼저 다시 올린다.
        if (true == bStorePending) TryStoreStats("(이전 실패분)");

        // 이미 해금됐으면 다시 올리지 않는다. 조회 자체가 실패하면(API 이름 미등록 등) 해금도 실패하므로 여기서 끝낸다.
        if (false == SteamUserStats.GetAchievement(_apiName, out bool _bAchieved))
        {
            Debug.LogWarning($"[SteamAchievementService] 도전과제 조회 실패: {_apiName} (Steamworks에 등록·게시됐는지 확인)");
            return;
        }

        if (true == _bAchieved) return;

        if (false == SteamUserStats.SetAchievement(_apiName))
        {
            Debug.LogWarning($"[SteamAchievementService] 도전과제 해금 실패: {_apiName}");
            return;
        }

        if (false == TryStoreStats(_apiName)) return;

        Debug.Log($"[SteamAchievementService] 도전과제 해금: {_apiName}");
#endif
    }
}
