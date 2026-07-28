using UnityEngine;

[CreateAssetMenu(fileName = "KeyIconDatabase", menuName = "UI/Key Icon Database")]
public class KeyIconDatabase : ScriptableObject
{
    [System.Serializable]
    public struct KeyIconEntry
    {
        [Tooltip("Input System 바인딩 경로 (예: <Keyboard>/w, <Keyboard>/space)")]
        public string bindingPath;
        public Sprite icon;
    }

    [SerializeField] private KeyIconEntry[] entries;

    // 런타임 조회용 딕셔너리 (Awake 시 빌드)
    private System.Collections.Generic.Dictionary<string, Sprite> lookupCache;

    /// <summary>
    /// 바인딩 경로에 매칭되는 스프라이트를 반환합니다. 없으면 null을 반환합니다.
    /// </summary>
    public Sprite GetIcon(string _bindingPath)
    {
        if (true == string.IsNullOrEmpty(_bindingPath)) return null;

        // 캐시가 없으면 빌드
        if (null == lookupCache)
        {
            BuildCache();
        }

        Sprite _result = null;
        lookupCache.TryGetValue(_bindingPath, out _result);
        return _result;
    }

    private void BuildCache()
    {
        lookupCache = new System.Collections.Generic.Dictionary<string, Sprite>();
        if (null == entries) return;

        for (int i = 0; i < entries.Length; i++)
        {
            if (false == string.IsNullOrEmpty(entries[i].bindingPath)
                && null != entries[i].icon
                && false == lookupCache.ContainsKey(entries[i].bindingPath))
            {
                lookupCache.Add(entries[i].bindingPath, entries[i].icon);
            }
        }
    }
}
