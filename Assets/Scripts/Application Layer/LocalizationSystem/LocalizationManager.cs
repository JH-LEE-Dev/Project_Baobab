using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class LocalizationManager
{
    // //내부 의존성
    private Dictionary<int, string> masterTable;
    private List<string> loadedJsons; // 언어 변경 시 재파싱을 위해 원본 JSON 보관
    private StringBuilder stringBuilder;
    private Language currentLanguage = Language.KR;

    public event Action OnLanguageChanged;

    // //퍼블릭 초기화 및 제어 메서드
    public void Initialize(int _initialCapacity = 512)
    {
        masterTable = new Dictionary<int, string>(_initialCapacity);
        loadedJsons = new List<string>(4);
        stringBuilder = new StringBuilder(128);
    }

    /// <summary>
    /// JSON 텍스트를 파싱하여 ID 기반 마스터 테이블에 등록하고, 원본을 보관합니다.
    /// </summary>
    public void LoadLocalizationJson(string _jsonText)
    {
        if (string.IsNullOrEmpty(_jsonText)) return;
        
        // 중복 로드 방지 및 보관
        if (!loadedJsons.Contains(_jsonText))
        {
            loadedJsons.Add(_jsonText);
        }

        ParseJson(_jsonText);
    }

    private void ParseJson(string _jsonText)
    {
        var data = JsonUtility.FromJson<LocalizationDataJson>(_jsonText);
        if (data == null || data.sections == null) return;

        for (int i = 0; i < data.sections.Length; i++)
        {
            var section = data.sections[i];
            for (int j = 0; j < section.entries.Length; j++)
            {
                var entry = section.entries[j];
                if (entry.id == 0) continue;
                
                masterTable[entry.id] = (currentLanguage == Language.KR) ? entry.kr : entry.en;
            }
        }
    }

    public string GetText(int _id)
    {
        if (masterTable.TryGetValue(_id, out string _value)) return _value;
        return string.Empty;
    }

    public string GetFormatText(int _id, int _arg0)
    {
        string format = GetText(_id);
        if (string.IsNullOrEmpty(format)) return string.Empty;

        stringBuilder.Clear();
        stringBuilder.AppendFormat(format, _arg0);
        return stringBuilder.ToString();
    }

    public void SetLanguage(Language _lang)
    {
        if (currentLanguage == _lang) return;
        currentLanguage = _lang;

        // 언어 변경 시 테이블을 비우고 보관된 모든 JSON을 다시 파싱합니다.
        masterTable.Clear();
        for (int i = 0; i < loadedJsons.Count; i++)
        {
            ParseJson(loadedJsons[i]);
        }

        OnLanguageChanged?.Invoke();
    }
}
