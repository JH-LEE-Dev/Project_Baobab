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
// 솔트를 섞은 SHA-256 해시로 축약해서 태깅한다 (동일 유저의 재발 빈도는 구분하되 원본 ID는 복원할 수 없게 한다).
public static class SentryUserContextTagger
{
#if !DISABLESTEAMWORKS
    /// <summary>
    /// 해시에 섞는 고정 솔트입니다.
    ///
    /// 솔트가 없으면 익명화가 되지 않습니다. SteamID64는 76561197960265728 + accountID 구조라
    /// 실제 경우의 수가 약 43억뿐이고, 전수 대입으로 몇 분이면 원본이 복원됩니다. 즉 솔트 없는
    /// 해시는 익명 데이터가 아니라 가명 처리일 뿐이고, 개인정보로 취급됩니다.
    ///
    /// 이 값을 섞으면 Sentry 쪽 데이터만 손에 넣은 사람은 원본 SteamID를 되돌릴 수 없습니다.
    /// 클라이언트 바이너리를 뜯으면 상수 자체는 찾을 수 있으므로, 완전한 익명화가 아니라
    /// "Sentry 유출이 곧 SteamID 유출이 되지는 않게" 막는 장치입니다.
    ///
    /// 이 값을 바꾸면 기존 유저의 Sentry User.Id가 전부 다른 값으로 갈립니다. 같은 유저가 반복해서
    /// 겪는 오류인지 추적하던 것이 그 시점에서 끊기므로 함부로 바꾸지 마십시오.
    /// </summary>
    private const string USER_ID_SALT = "becb6dc823667a956016a307b64b4511";
#endif

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
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(USER_ID_SALT + steamId64.ToString()));

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
