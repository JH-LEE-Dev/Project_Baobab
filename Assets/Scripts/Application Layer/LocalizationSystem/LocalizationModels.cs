using System;

[Serializable]
public struct LocalizationEntry
{
    public int id;      // String ID (JSON 내부 식별자, 파일 내 고유해야 함)
    public string kr;
    public string en;
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
