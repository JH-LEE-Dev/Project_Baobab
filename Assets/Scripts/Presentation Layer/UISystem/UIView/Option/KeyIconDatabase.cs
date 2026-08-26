using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "KeyIconDatabase", menuName = "UI/Key Icon Database")]
public class KeyIconDatabase : ScriptableObject
{
    [Serializable]
    public struct KeyIconEntry
    {
        [Tooltip("Input System 바인딩 경로 (예: <Keyboard>/w, <Gamepad>/buttonSouth)")]
        public string bindingPath;
        public Sprite icon;
    }

    [Header("Keyboard / Mouse / Shared Entries")]
    [SerializeField] private KeyIconEntry[] entries;

    [Header("Xbox Gamepad Entries")]
    [SerializeField] private KeyIconEntry[] xboxEntries;

    [Header("PlayStation Gamepad Entries")]
    [SerializeField] private KeyIconEntry[] playStationEntries;

    // 런타임 조회용 딕셔너리
    private Dictionary<string, Sprite> lookupCache;
    private Dictionary<string, Sprite> xboxCache;
    private Dictionary<string, Sprite> playStationCache;

    /// <summary>
    /// 바인딩 경로와 아이콘 세트에 매칭되는 스프라이트를 반환합니다. 없으면 null을 반환합니다.
    /// </summary>
    public Sprite GetIcon(string _bindingPath, EGamepadIconSet _iconSet = EGamepadIconSet.Xbox)
    {
        if (true == string.IsNullOrEmpty(_bindingPath)) return null;

        if (null == lookupCache || null == xboxCache || null == playStationCache)
        {
            BuildCache();
        }

        Sprite _result = null;

        // 패드 경로인 경우 해당 벤더 캐시 우선 조회
        if (true == _bindingPath.StartsWith("<Gamepad>/"))
        {
            if (EGamepadIconSet.PlayStation == _iconSet)
            {
                if (true == playStationCache.TryGetValue(_bindingPath, out _result))
                {
                    return _result;
                }
            }
            else
            {
                if (true == xboxCache.TryGetValue(_bindingPath, out _result))
                {
                    return _result;
                }
            }
        }

        // 공통/키보드/마우스 캐시 조회
        if (true == lookupCache.TryGetValue(_bindingPath, out _result))
        {
            return _result;
        }

        // 폴백: Xbox 캐시 재조회
        if (true == xboxCache.TryGetValue(_bindingPath, out _result))
        {
            return _result;
        }

        return null;
    }

    public Sprite GetIcon(string _bindingPath)
    {
        return GetIcon(_bindingPath, EGamepadIconSet.Xbox);
    }

    private void BuildCache()
    {
        lookupCache = new Dictionary<string, Sprite>();
        xboxCache = new Dictionary<string, Sprite>();
        playStationCache = new Dictionary<string, Sprite>();

        PopulateDictionary(entries, lookupCache);
        PopulateDictionary(xboxEntries, xboxCache);
        PopulateDictionary(playStationEntries, playStationCache);
    }

    private static void PopulateDictionary(KeyIconEntry[] _source, Dictionary<string, Sprite> _target)
    {
        if (null == _source) return;

        for (int i = 0; i < _source.Length; i++)
        {
            if (false == string.IsNullOrEmpty(_source[i].bindingPath)
                && null != _source[i].icon
                && false == _target.ContainsKey(_source[i].bindingPath))
            {
                _target.Add(_source[i].bindingPath, _source[i].icon);
            }
        }
    }

#if UNITY_EDITOR
    public void SetEntriesForEditor(KeyIconEntry[] _entries, KeyIconEntry[] _xboxEntries, KeyIconEntry[] _playStationEntries)
    {
        entries = _entries;
        xboxEntries = _xboxEntries;
        playStationEntries = _playStationEntries;
    }
#endif
}
