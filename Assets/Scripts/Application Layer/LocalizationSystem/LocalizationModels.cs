using System;

[Serializable]
public struct LocalizationEntry
{
    public int id;      // String ID (JSON 내부 식별자, 파일 내 고유해야 함)
    public string key;  // 개발 식별자 (C# 상수 생성용 키)
    public string kr;
    public string en;
    public string enumType;  // 연결하고자 하는 Enum의 이름 (예: "ForestType")
    public string enumValue; // 연결하고자 하는 Enum 값의 이름 (예: "DeepForest")
}

[Serializable]
public class LocalizationDataJson
{
    public int jsonId; // JSON 파일 고유 식별자
    public LocalizationEntry[] entries;
}

public enum Language
{
    KR,
    EN
}
