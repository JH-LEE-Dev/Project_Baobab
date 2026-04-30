using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class LocalizationManager
{
    // //내부 의존성
    private Dictionary<int, string> masterTable;
    private List<string> loadedJsons;
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

    public void LoadLocalizationJson(string _jsonText)
    {
        if (string.IsNullOrEmpty(_jsonText)) return;
        
        if (!loadedJsons.Contains(_jsonText))
        {
            loadedJsons.Add(_jsonText);
        }

        ParseJson(_jsonText);
    }

    private void ParseJson(string _jsonText)
    {
        var data = JsonUtility.FromJson<LocalizationDataJson>(_jsonText);
        if (data == null || data.entries == null) return;

        int jsonId = data.jsonId;

        for (int i = 0; i < data.entries.Length; i++)
        {
            var entry = data.entries[i];
            if (entry.id == 0) continue;
            
            int compositeKey = GenerateKey(jsonId, entry.id);
            masterTable[compositeKey] = (currentLanguage == Language.KR) ? entry.kr : entry.en;
        }
    }

    public string GetText(int _compositeKey)
    {
        if (masterTable.TryGetValue(_compositeKey, out string _value)) return _value;
        return string.Empty;
    }

    public string GetFormatText(int _compositeKey, params object[] _args)
    {
        string format = GetText(_compositeKey);
        if (string.IsNullOrEmpty(format)) return string.Empty;

        stringBuilder.Clear();
        stringBuilder.AppendFormat(format, _args);
        return stringBuilder.ToString();
    }

    public void SetLanguage(Language _lang)
    {
        if (currentLanguage == _lang) return;
        currentLanguage = _lang;

        masterTable.Clear();
        for (int i = 0; i < loadedJsons.Count; i++)
        {
            ParseJson(loadedJsons[i]);
        }

        OnLanguageChanged?.Invoke();
    }

    // 비트 연산을 통한 고유 키 생성
    // jsonId: 10bit (0~1023), entryId: 21bit (0~2,097,151)
    public int GenerateKey(int _jsonId, int _entryId)
    {
        return (_jsonId << 21) | (_entryId & 0x1FFFFF);
    }
}
