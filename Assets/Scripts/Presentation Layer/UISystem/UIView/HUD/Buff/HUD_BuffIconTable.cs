using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 버프 id(EBuffHudId) -> 아이콘 스프라이트 테이블. 윗단은 스프라이트를 몰라도 되고 아트 교체는 이 에셋에서만 한다.
/// 스프라이트 규격: 16x16 캔버스, 그림은 가운데 12x12(Point 필터).
/// </summary>
[CreateAssetMenu(fileName = "HUD_BuffIconTable", menuName = "UI/HUD Buff Icon Table")]
public class HUD_BuffIconTable : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public EBuffHudId id;
        public Sprite icon;
    }

    [SerializeField] private Entry[] entries;

    // 런타임 조회용 딕셔너리
    private Dictionary<int, Sprite> lookupCache;

    // id에 해당하는 아이콘을 반환한다. 없으면 null.
    public Sprite GetIcon(int _id)
    {
        if (null == lookupCache)
        {
            BuildCache();
        }

        lookupCache.TryGetValue(_id, out Sprite _icon);
        return _icon;
    }

    private void BuildCache()
    {
        int _count = null != entries ? entries.Length : 0;
        lookupCache = new Dictionary<int, Sprite>(Mathf.Max(8, _count));

        for (int i = 0; i < _count; i++)
        {
            int _key = (int)entries[i].id;

            if (null != entries[i].icon && false == lookupCache.ContainsKey(_key))
            {
                lookupCache.Add(_key, entries[i].icon);
            }
        }
    }

#if UNITY_EDITOR
    public void SetEntriesForEditor(Entry[] _entries)
    {
        entries = _entries;
        lookupCache = null;
    }

    private void OnValidate()
    {
        lookupCache = null;
    }
#endif
}
