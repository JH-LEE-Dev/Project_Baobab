using System;

[Serializable]
public enum Language { KR, EN }

[Serializable]
public struct LocalizationEntry
{
    public int id;      // 고유 식별자 (ID 기반 조회로 GC Alloc 제거)
    public string kr;
    public string en;
}

[Serializable]
public struct LocalizationSection
{
    public string uiName; // 기획 확인용 섹션 구분
    public LocalizationEntry[] entries;
}

[Serializable]
public class LocalizationDataJson
{
    public LocalizationSection[] sections;
}
