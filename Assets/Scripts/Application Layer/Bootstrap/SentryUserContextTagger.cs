#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using Sentry;
using Sentry.Unity;
#if !DISABLESTEAMWORKS
using System.Security.Cryptography;
using System.Text;
using Steamworks;
#endif

// SteamID64를 그대로 Sentry에 보내면 스팀 프로필로 역추적 가능한 지속 식별자가 되므로,
// SHA-256 해시로 축약해서 태깅한다 (동일 유저의 재발 빈도는 구분하되 원본 ID는 노출하지 않음).
public static class SentryUserContextTagger
{
    public static void TagCurrentUser()
    {
#if !DISABLESTEAMWORKS
        if (!SteamManager.Initialized)
        {
            return;
        }

        ulong steamId64 = SteamUser.GetSteamID().m_SteamID;

        SentrySdk.ConfigureScope(scope =>
        {
            scope.User = new SentryUser { Id = HashSteamId(steamId64) };
        });
#endif
    }

#if !DISABLESTEAMWORKS
    private static string HashSteamId(ulong steamId64)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(steamId64.ToString()));

            StringBuilder sb = new StringBuilder(16);
            for (int i = 0; i < 8; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }

            return sb.ToString();
        }
    }
#endif
}
