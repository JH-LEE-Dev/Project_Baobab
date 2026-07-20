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

    public static void Upload(byte[] _data)
    {
        if (!SteamManager.Initialized)
        {
            Debug.Log("[SteamCloudSaveService] Cloud upload skipped: SteamManager not initialized.");
            return;
        }

        if (!SteamRemoteStorage.IsCloudEnabledForAccount())
        {
            Debug.Log("[SteamCloudSaveService] Cloud upload skipped: Cloud disabled for this Steam account.");
            return;
        }

        if (!SteamRemoteStorage.IsCloudEnabledForApp())
        {
            Debug.Log("[SteamCloudSaveService] Cloud upload skipped: Cloud disabled for this App ID.");
            return;
        }

        if (SteamRemoteStorage.FileWrite(CloudFileName, _data, _data.Length))
        {
            Debug.Log($"[SteamCloudSaveService] Cloud upload succeeded ({_data.Length} bytes).");
        }
        else
        {
            Debug.LogWarning("[SteamCloudSaveService] Cloud upload failed.");
        }
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
}
