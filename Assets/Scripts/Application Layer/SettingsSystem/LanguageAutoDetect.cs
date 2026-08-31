using UnityEngine;

/// <summary>
/// 설정 파일이 없는 첫 실행에서 어떤 언어로 시작할지 정합니다.
///
/// [왜 필요한가]
/// SettingsData.CreateDefault()의 language는 한국어 고정입니다. 그 값을 그대로 쓰면 해외 유저는
/// 첫 화면부터 읽을 수 없는 글자를 보고, 옵션이 어디 있는지도 모르는 채로 언어를 찾아야 합니다.
/// 그래서 "저장된 선택이 아직 없을 때"에 한해 환경에서 언어를 추론합니다.
///
/// [우선순위] Steam 지정 언어 → OS 언어 → 영어
/// Steam을 맨 앞에 두는 이유는 SteamLanguageService 주석을 참고하세요. 영어를 최종 기본값으로
/// 두는 이유는, 어느 쪽으로도 판별되지 않았다는 것은 곧 우리가 번역하지 않은 언어권이라는 뜻이고
/// 그 경우 한국어보다 영어가 읽힐 확률이 훨씬 높기 때문입니다.
///
/// [저장하지 않는 이유]
/// 여기서 정한 값은 파일에 기록하지 않습니다(SettingsManager.Load의 isDirty를 건드리지 않습니다).
/// 덕분에 유저가 옵션에서 언어를 직접 고르기 전까지는 매 실행 다시 추론하며, Steam 언어를 바꾸면
/// 게임도 따라옵니다. 한 번 직접 고르면 그때 파일이 생겨 이 로직은 더 이상 관여하지 않습니다.
///
/// [언어를 추가할 때]
/// SettingsManager 상단의 체크리스트를 먼저 처리한 뒤, 아래 두 매핑에 항목을 추가하세요.
/// 매핑을 빠뜨리면 그 언어권 유저는 조용히 영어로 시작합니다.
/// </summary>
public static class LanguageAutoDetect
{
    /// <summary>어느 쪽으로도 판별되지 않았을 때 쓰는 값입니다.</summary>
    private const EOptionLanguage FALLBACK_LANGUAGE = EOptionLanguage.English;

    /// <summary>첫 실행에 적용할 언어를 정합니다.</summary>
    public static EOptionLanguage Resolve()
    {
        if (true == SteamLanguageService.TryGetGameLanguage(out string _steamCode)
            && true == TryFromSteamCode(_steamCode, out EOptionLanguage _fromSteam))
        {
            return _fromSteam;
        }

        if (true == TryFromSystemLanguage(Application.systemLanguage, out EOptionLanguage _fromSystem))
        {
            return _fromSystem;
        }

        return FALLBACK_LANGUAGE;
    }

    /// <summary>
    /// Steam API 언어 코드를 게임의 언어 항목으로 옮깁니다.
    ///
    /// Steam은 소문자로 내려주지만, 대소문자를 무시해 두면 나중에 표기가 달라져도 조용히
    /// 영어로 떨어지는 사고를 막을 수 있습니다.
    /// </summary>
    private static bool TryFromSteamCode(string _code, out EOptionLanguage _language)
    {
        _language = FALLBACK_LANGUAGE;

        if (true == string.IsNullOrEmpty(_code)) return false;

        EOptionLanguage _mapped;

        if (Matches(_code, "koreana")) _mapped = EOptionLanguage.Korean;
        else if (Matches(_code, "english")) _mapped = EOptionLanguage.English;
        else if (Matches(_code, "schinese")) _mapped = EOptionLanguage.ChineseSimplified;
        else if (Matches(_code, "tchinese")) _mapped = EOptionLanguage.ChineseTraditional;
        else if (Matches(_code, "japanese")) _mapped = EOptionLanguage.Japanese;
        else return false;

        return Accept(_mapped, out _language);
    }

    /// <summary>OS 언어를 게임의 언어 항목으로 옮깁니다.</summary>
    private static bool TryFromSystemLanguage(SystemLanguage _system, out EOptionLanguage _language)
    {
        EOptionLanguage _mapped;

        switch (_system)
        {
            case SystemLanguage.Korean:
                _mapped = EOptionLanguage.Korean;
                break;

            case SystemLanguage.English:
                _mapped = EOptionLanguage.English;
                break;

            // SystemLanguage.Chinese는 간체/번체를 구분하지 않던 시절의 값이라 어느 쪽인지 알 수 없다.
            // 요즘 플랫폼은 ChineseSimplified/ChineseTraditional을 돌려주지만, 이 값이 올 수도 있으므로
            // 사용자 수가 훨씬 많은 간체로 본다. (틀렸다면 유저가 옵션에서 한 번 바꾸면 된다)
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                _mapped = EOptionLanguage.ChineseSimplified;
                break;

            case SystemLanguage.ChineseTraditional:
                _mapped = EOptionLanguage.ChineseTraditional;
                break;

            case SystemLanguage.Japanese:
                _mapped = EOptionLanguage.Japanese;
                break;

            default:
                _language = FALLBACK_LANGUAGE;
                return false;
        }

        return Accept(_mapped, out _language);
    }

    /// <summary>
    /// 매핑 결과가 지금 실제로 지원되는 언어일 때만 통과시킵니다.
    ///
    /// EOptionLanguage에는 아직 지원하지 않는 항목(Russian)이 선언되어 있고, 앞으로도 번역보다
    /// enum이 먼저 늘어날 수 있습니다. 이 관문이 없으면 "선택기에는 없는 언어로 게임이 시작되는"
    /// 상태가 되고, SettingsData.Validate가 그걸 한국어로 되돌려 원인을 찾기 어려워집니다.
    /// (지원 언어가 enum 앞쪽에 연속으로 온다는 전제는 Validate·CycleLanguage와 동일합니다)
    /// </summary>
    private static bool Accept(EOptionLanguage _candidate, out EOptionLanguage _language)
    {
        if ((int)_candidate < 0 || (int)_candidate >= SettingsData.SUPPORTED_LANGUAGE_COUNT)
        {
            _language = FALLBACK_LANGUAGE;
            return false;
        }

        _language = _candidate;
        return true;
    }

    private static bool Matches(string _code, string _expected)
    {
        return string.Equals(_code, _expected, System.StringComparison.OrdinalIgnoreCase);
    }
}
