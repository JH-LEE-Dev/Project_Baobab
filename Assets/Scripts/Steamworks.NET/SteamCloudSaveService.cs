using System;
using UnityEngine;
using Steamworks;

public static class SteamCloudSaveService
{
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
}
