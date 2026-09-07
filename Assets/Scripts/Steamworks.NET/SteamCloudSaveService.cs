#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// 스팀 클라우드(Remote Storage)에 세이브 파일 하나를 올리고 내립니다.
///
/// [DISABLESTEAMWORKS 가드]
/// 위 두 줄은 Steamworks.NET의 모든 파일이 쓰는 관례입니다. 지원되지 않는 플랫폼에서 자동으로
/// 켜지지만, **Player Settings의 Scripting Define Symbols에 직접 넣어서 켤 수도 있습니다.**
/// 스팀을 거치지 않는 배포(스토브 등)는 그 방법으로 Steamworks 전체를 통째로 컴파일에서 빼냅니다.
///
/// 가드 없이는 그게 불가능합니다. using Steamworks 한 줄이 남아 있으면 디파인을 켜는 순간
/// 패키지가 사라진 자리를 참조해 컴파일이 깨집니다.
///
/// [스텁이 값을 돌려주는 규칙]
/// 호출부(SaveManager)는 이 클래스가 무슨 플랫폼 위에 있는지 몰라야 합니다. 그래서 스텁도
/// 예외를 던지거나 하지 않고, "클라우드가 없는 상태"를 그대로 표현하는 값을 돌려줍니다.
/// SaveManager 쪽에는 #if가 한 줄도 들어가지 않습니다.
/// </summary>
public static class SteamCloudSaveService
{
#if !DISABLESTEAMWORKS
    private const string CloudFileName = "SaveData.dat"; // GamePaths의 GAME_SAVE_FILE_NAME과 동일 문자열 유지

    public static bool IsAvailable =>
        SteamManager.Initialized
        && SteamRemoteStorage.IsCloudEnabledForAccount()
        && SteamRemoteStorage.IsCloudEnabledForApp();

    /// <summary>클라우드에 세이브를 올린다. 실제로 기록에 성공했을 때만 true.</summary>
    public static bool Upload(byte[] _data)
    {
        if (!SteamManager.Initialized)
        {
            Debug.Log("[SteamCloudSaveService] Cloud upload skipped: SteamManager not initialized.");
            return false;
        }

        if (!SteamRemoteStorage.IsCloudEnabledForAccount())
        {
            Debug.Log("[SteamCloudSaveService] Cloud upload skipped: Cloud disabled for this Steam account.");
            return false;
        }

        if (!SteamRemoteStorage.IsCloudEnabledForApp())
        {
            Debug.Log("[SteamCloudSaveService] Cloud upload skipped: Cloud disabled for this App ID.");
            return false;
        }

        if (SteamRemoteStorage.FileWrite(CloudFileName, _data, _data.Length))
        {
            Debug.Log($"[SteamCloudSaveService] Cloud upload succeeded ({_data.Length} bytes).");
            return true;
        }

        Debug.LogWarning("[SteamCloudSaveService] Cloud upload failed.");
        return false;
    }

    public static bool TryDownload(out byte[] _data)
    {
        _data = null;
        if (!IsAvailable || !SteamRemoteStorage.FileExists(CloudFileName)) return false;

        int size = SteamRemoteStorage.GetFileSize(CloudFileName);
        byte[] buffer = new byte[size];
        int read = SteamRemoteStorage.FileRead(CloudFileName, buffer, size);

        if (read != size) return false;

        _data = buffer;
        return true;
    }

    public static bool TryGetCloudTimestampUtc(out DateTime _utc)
    {
        _utc = default;
        if (!IsAvailable || !SteamRemoteStorage.FileExists(CloudFileName)) return false;

        long unixSeconds = SteamRemoteStorage.GetFileTimestamp(CloudFileName);
        _utc = DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        return true;
    }

    /// <summary>
    /// 클라우드 세이브를 삭제한다. 호출 후 클라우드에 파일이 남아있지 않음이 확실할 때만 true를 돌려준다
    /// (애초에 없었던 경우 포함). 클라우드를 쓸 수 없거나 삭제가 실패하면 false이며, 이때 호출부는
    /// 삭제 표식(tombstone)을 남겨 다음 실행에서 다시 정리해야 한다.
    /// </summary>
    public static bool Delete()
    {
        if (!IsAvailable)
        {
            Debug.Log("[SteamCloudSaveService] Cloud delete skipped: cloud unavailable.");
            return false;
        }

        // 지울 대상이 없으면 목적은 이미 달성된 상태다.
        if (!SteamRemoteStorage.FileExists(CloudFileName)) return true;

        if (SteamRemoteStorage.FileDelete(CloudFileName))
        {
            Debug.Log("[SteamCloudSaveService] Cloud save deleted.");
            return true;
        }

        Debug.LogWarning("[SteamCloudSaveService] Cloud save delete failed.");
        return false;
    }
#else
    /// <summary>스팀이 빠진 빌드에는 클라우드가 없다.</summary>
    public static bool IsAvailable => false;

    /// <summary>올릴 곳이 없으므로 아무것도 기록되지 않았다.</summary>
    public static bool Upload(byte[] _data) => false;

    public static bool TryDownload(out byte[] _data)
    {
        _data = null;
        return false;
    }

    public static bool TryGetCloudTimestampUtc(out DateTime _utc)
    {
        _utc = default;
        return false;
    }

    /// <summary>
    /// **false가 아니라 true입니다.** 이 계약은 "삭제에 성공했는가"가 아니라
    /// "호출 후 클라우드에 파일이 남아있지 않음이 확실한가"이며, 클라우드 자체가 없는 빌드에서는
    /// 언제나 확실합니다.
    ///
    /// false로 두면 SaveManager.DeleteSaveData가 매번 삭제 표식(tombstone) 파일을 남기는데,
    /// SyncCloudSaveIfNewer는 IsAvailable이 false라 시작하자마자 빠져나가므로 그 표식을
    /// 정리해 줄 사람이 아무도 없습니다. "새로하기"를 할 때마다 지워지지 않는 파일이 하나씩
    /// 남는 셈입니다.
    /// </summary>
    public static bool Delete() => true;
#endif // !DISABLESTEAMWORKS
}
