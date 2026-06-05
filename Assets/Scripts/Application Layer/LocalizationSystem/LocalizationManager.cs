using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class LocalizationManager
{
    // //내부 의존성
    private Dictionary<int, string> masterTable;
    private Dictionary<int, int> enumToKeyMap;
    private List<string> loadedJsons;
    private StringBuilder stringBuilder;
    private Language currentLanguage = Language.KR;

    public event Action OnLanguageChanged;

    // //퍼블릭 초기화 및 제어 메서드
    public void Initialize(int _initialCapacity = 512)
    {
        masterTable = new Dictionary<int, string>(_initialCapacity);
        enumToKeyMap = new Dictionary<int, int>(128);
        loadedJsons = new List<string>(4);
        stringBuilder = new StringBuilder(128);
    }

    public void LoadMappingData(LocalizationMapping _mappingData)
    {
        if (_mappingData == null) return;

        enumToKeyMap.Clear();
        for (int i = 0; i < _mappingData.mappings.Count; i++)
        {
            var entry = _mappingData.mappings[i];
            enumToKeyMap[entry.enumIntValue] = entry.compositeKey;
        }
    }

    public string GetText<T>(T _enumValue) where T : struct, Enum
    {
        int enumInt = System.Runtime.CompilerServices.Unsafe.As<T, int>(ref _enumValue);

        if (enumToKeyMap.TryGetValue(enumInt, out int compositeKey))
        {
            return GetText(compositeKey);
        }
        return string.Empty;
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

    public string GetText(int _jsonId, int _entryId)
    {
        int compositeKey = GenerateKey(_jsonId, _entryId);
        return GetText(compositeKey);
    }

    public string GetFormatText(int _compositeKey, params object[] _args)
    {
        string format = GetText(_compositeKey);
        if (string.IsNullOrEmpty(format)) return string.Empty;

        stringBuilder.Clear();
        stringBuilder.AppendFormat(format, _args);
        return stringBuilder.ToString();
    }

    public string GetFormatText(int _jsonId, int _entryId, params object[] _args)
    {
        int compositeKey = GenerateKey(_jsonId, _entryId);
        return GetFormatText(compositeKey, _args);
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
