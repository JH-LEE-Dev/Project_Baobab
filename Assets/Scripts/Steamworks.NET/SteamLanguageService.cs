using Steamworks;

/// <summary>
/// Steam이 이 게임에 대해 지정한 언어를 읽습니다.
///
/// 유저는 라이브러리에서 게임마다 언어를 따로 고를 수 있고(속성 > 언어), 따로 고르지 않았으면
/// Steam 클라이언트 언어가 그대로 내려옵니다. 첫 실행 언어를 정할 때 OS 언어보다 이쪽을
/// 우선하는 이유는, 이 값이 유저가 "이 게임을 무슨 말로 볼지" 이미 밝혀둔 답이기 때문입니다.
/// (한국에서 영어 윈도우를 쓰거나 그 반대인 경우처럼, OS 언어와 실제 읽는 언어는 자주 다릅니다)
///
/// 얇게 감싸기만 하는 이유는 SteamCloudSaveService와 같습니다. Steamworks 타입이 설정 시스템까지
/// 번지지 않도록 여기서 문자열로 끊습니다.
/// </summary>
public static class SteamLanguageService
{
    /// <summary>
    /// Steam API 언어 코드를 돌려줍니다. (예: "koreana", "english", "schinese")
    /// Steam을 쓸 수 없으면 false이며, 이때 호출부는 OS 언어로 넘어가야 합니다.
    ///
    /// 코드 목록: https://partner.steamgames.com/doc/store/localization/languages
    /// 한국어가 "korean"이 아니라 "koreana"인 것에 주의하세요. Steam의 역사적인 표기입니다.
    /// </summary>
    public static bool TryGetGameLanguage(out string _apiLanguageCode)
    {
        _apiLanguageCode = null;

        // SteamManager.Initialized는 접근만으로 SteamManager를 만들어 SteamAPI를 초기화한다.
        // (SteamCloudSaveService와 같은 경로다. 부팅 중 어느 쪽이 먼저 불리든 결과는 같다)
        if (false == SteamManager.Initialized) return false;

        string _code = SteamApps.GetCurrentGameLanguage();

        if (true == string.IsNullOrEmpty(_code)) return false;

        _apiLanguageCode = _code;
        return true;
    }
}
