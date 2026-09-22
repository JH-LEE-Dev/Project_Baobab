using System;

[Serializable]
public struct LocalizationEntry
{
    public int id;      // String ID (JSON 내부 식별자, 파일 내 고유해야 함)
    public string key;  // 개발 식별자 (C# 상수 생성용 키)
    public string kr;
    public string en;
    public string zhHans; // 중국어 간체. 비어 있으면 en으로 폴백한다 (LocalizationManager.ParseJson)
    public string zhHant; // 중국어 번체. 비어 있으면 en으로 폴백한다 (LocalizationManager.ParseJson)
    public string ja;     // 일본어. 비어 있으면 en으로 폴백한다 (LocalizationManager.ParseJson)
    public string de;     // 독일어. 비어 있으면 en으로 폴백한다 (LocalizationManager.ParseJson)
    public string fr;     // 프랑스어. 비어 있으면 en으로 폴백한다 (LocalizationManager.ParseJson)
    public string pt;     // 포르투갈어. 비어 있으면 en으로 폴백한다 (LocalizationManager.ParseJson)
    public string es;     // 스페인어. 비어 있으면 en으로 폴백한다 (LocalizationManager.ParseJson)
    public string ru;     // 러시아어. 비어 있으면 en으로 폴백한다 (LocalizationManager.ParseJson)
    public string enumType;  // 연결하고자 하는 Enum의 이름 (예: "ForestType")
    public string enumValue; // 연결하고자 하는 Enum 값의 이름 (예: "DeepForest")
}

[Serializable]
public class LocalizationDataJson
{
    public int jsonId; // JSON 파일 고유 식별자
    public LocalizationEntry[] entries;
}

/// <summary>
/// LocalizationManager가 실제로 구분하는 언어입니다.
///
/// 유저가 옵션에서 고르는 항목은 <see cref="EOptionLanguage"/>이고, 이 enum은 그것을
/// 로컬라이징 데이터의 열(LocalizationEntry의 필드)로 옮긴 것입니다. 둘을 잇는 곳은
/// SettingsManager.ApplyLanguageToLocalization 한 곳뿐이므로, 항목을 늘릴 때는 그 매핑을
/// 반드시 함께 손봐야 합니다. (빠뜨리면 조용히 EN으로 떨어집니다)
/// </summary>
public enum Language
{
    KR,
    EN,
    ZH_HANS,
    ZH_HANT,
    JA,
    DE,
    FR,
    PT,
    ES,
    RU
}
