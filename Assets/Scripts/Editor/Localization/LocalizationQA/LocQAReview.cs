using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LocalizationQA
{
    // //검수 데이터
    internal enum LocQAReviewState
    {
        None = 0,
        Ok = 1,
        Ng = 2
    }

    /// <summary>
    /// 검수 항목 하나 = "UI 텍스트 칸(슬롯)" × "그 칸에 들어가는 문구(키)".
    ///
    /// 슬롯은 텍스트가 원래 정의된 프리팹 기준으로 잡는다. 옵션 행(OPT_Selector)처럼 여러 화면에
    /// 중첩되어 쓰이는 프리팹의 텍스트를 화면마다 따로 검수하지 않게 하기 위해서다.
    /// 그리는 화면(컨텍스트)은 그 문구가 실제로 쓰이는 화면으로 둔다.
    /// (같은 "FPS" 제목이라도 옵션 창 안에 넣어 그려야 실제와 같은 폭으로 보인다)
    /// contextDepth는 그 화면의 적합도 점수로, 낮을수록 좋다. UIView가 붙은 화면 프리팹이면 0점부터,
    /// 부품 프리팹이면 1000점부터 시작하고 중첩이 한 단계 깊을 때마다 1점씩 더한다.
    /// </summary>
    [Serializable]
    internal sealed class LocQAReviewItem
    {
        public string slotGuid;
        public string slotPath;
        public string contextGuid;
        public string contextPath;
        public int contextDepth;
        public string entryId;
        public string source;   // 자동 / 플레이 / 수동 / 코드분석

        // 코드가 문구에 숫자·태그를 붙여 표시하는 칸의 모양. "{text}" 자리에 문구가 들어간다.
        // 예) "{text} : 1 / 5" (툴팁 레벨), "<COLOR=FFD800><WAVE>{text}</WAVE></COLOR>" (최대 레벨 비용)
        // 비어 있으면 문구만 그대로 넣는다.
        public string template;

        // 문구의 {0}, {1} … 자리에 넣을 견본 값. "|"로 구분한다. 예) "5" (처치 수 1~10 구간), "12|30|42"
        // 비어 있으면 설정의 기본 견본 값(999)을 넣는다.
        public string samples;

        public string Key => slotGuid + "|" + slotPath + "|" + entryId;
        public string SlotKey => slotGuid + "|" + slotPath;
    }

    /// <summary>문구가 하나도 연결되지 않은 텍스트 칸. 사람이 문구를 연결하거나 "번역 대상 아님"으로 표시해야 한다.</summary>
    [Serializable]
    internal sealed class LocQAReviewSlot
    {
        public string slotGuid;
        public string slotPath;
        public string contextGuid;
        public string contextPath;
        public int contextDepth;
        public string defaultText;
        public bool ignored;

        public string SlotKey => slotGuid + "|" + slotPath;
    }

    /// <summary>게임에서 실제로 쓰이지 않는 문구. 검수할 칸이 없으므로 "연결 안 된 문구" 집계에서 뺀다.</summary>
    [Serializable]
    internal sealed class LocQAUnusedEntry
    {
        public string entryId;
        public string reason;
    }

    [Serializable]
    internal sealed class LocQAReviewItemsFile
    {
        public List<LocQAReviewItem> items = new List<LocQAReviewItem>();
        public List<LocQAReviewSlot> slots = new List<LocQAReviewSlot>();
        // 사람이나 코드 분석이 "이 칸에는 이 문구가 안 나온다"고 뺀 항목. 자동 추론이 같은 연결을 되살리지 않게 기억한다.
        public List<string> rejected = new List<string>();
        public List<LocQAUnusedEntry> unused = new List<LocQAUnusedEntry>();
    }

    /// <summary>없어진 프리팹·칸 정리 결과. (LocQAReviewData.PruneMissing)</summary>
    internal sealed class LocQAPruneReport
    {
        public readonly List<string> removedItems = new List<string>(); // "프리팹 / 칸 경로 · 키"
        public int removedSlots;    // 문구가 연결되지 않은 칸 기록
        public int removedMarks;    // 지워지는 연결에 걸려 있던 판정(언어별) 수
        public int moved;           // 그리던 화면에서 빠져 원본 프리팹에서 그리게 된 칸

        public bool HasRemovals => removedItems.Count > 0 || removedSlots > 0;
    }

    [Serializable]
    internal sealed class LocQAReviewMark
    {
        public string item;
        public int state;
        public string hash;     // 판정할 때의 문구. 번역이 바뀌면 다시 검수해야 한다.
        public string memo;
        public string by;
        public string at;
    }

    [Serializable]
    internal sealed class LocQAReviewMarksFile
    {
        public string language;
        public List<LocQAReviewMark> marks = new List<LocQAReviewMark>();
    }

    /// <summary>
    /// 검수 목록과 판정 기록. 저장소에 커밋되는 JSON이다.
    /// 판정은 언어별 파일로 나눈다. 언어마다 검수자가 다른 경우가 많아, 한 파일에 모으면 병합 충돌이 잦기 때문이다.
    /// </summary>
    internal sealed class LocQAReviewData
    {
        public const string FOLDER = "Assets/Scripts/Editor/Localization/LocalizationQA/Review";
        private const string ITEMS_FILE = "Items.json";
        private const string REVIEWER_PREF = "LumberBoy.LocalizationQA.Reviewer";
        private const double AUTOSAVE_DELAY = 1.0;

        private static LocQAReviewData shared;

        public static LocQAReviewData Shared
        {
            get
            {
                if (null == shared) shared = Load();
                return shared;
            }
        }

        public static event Action Changed;

        private LocQAReviewItemsFile itemsFile = new LocQAReviewItemsFile();
        private readonly Dictionary<string, LocQAReviewItem> itemsByKey = new Dictionary<string, LocQAReviewItem>(1024, StringComparer.Ordinal);
        private readonly Dictionary<string, LocQAReviewSlot> slotsByKey = new Dictionary<string, LocQAReviewSlot>(256, StringComparer.Ordinal);
        private readonly HashSet<string> filledSlots = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> rejected = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> unusedReasons = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<Language, LocQAReviewMarksFile> markFiles = new Dictionary<Language, LocQAReviewMarksFile>();
        private readonly Dictionary<Language, Dictionary<string, LocQAReviewMark>> marks = new Dictionary<Language, Dictionary<string, LocQAReviewMark>>();
        private readonly HashSet<Language> dirtyLanguages = new HashSet<Language>();
        private bool itemsDirty;
        private double lastChange;

        public int Version { get; private set; }
        public List<LocQAReviewItem> Items => itemsFile.items;
        public List<LocQAReviewSlot> Slots => itemsFile.slots;

        public static string Reviewer
        {
            get
            {
                string _name = EditorPrefs.GetString(REVIEWER_PREF, string.Empty);
                if (false == string.IsNullOrEmpty(_name)) return _name;
                _name = CloudProjectSettings.userName;
                if (string.IsNullOrEmpty(_name) || "anonymous" == _name) _name = Environment.UserName;
                return _name;
            }
            set => EditorPrefs.SetString(REVIEWER_PREF, value ?? string.Empty);
        }

        // //읽기·쓰기
        private static LocQAReviewData Load()
        {
            LocQAReviewData _data = new LocQAReviewData();

            string _itemsPath = Path.Combine(FOLDER, ITEMS_FILE);
            if (true == File.Exists(_itemsPath))
            {
                try
                {
                    LocQAReviewItemsFile _file = JsonUtility.FromJson<LocQAReviewItemsFile>(File.ReadAllText(_itemsPath, Encoding.UTF8));
                    if (null != _file) _data.itemsFile = _file;
                }
                catch (Exception _e)
                {
                    Debug.LogError($"[LocalizationQA] {_itemsPath} 읽기 실패: {_e.Message}");
                }
            }
            if (null == _data.itemsFile.items) _data.itemsFile.items = new List<LocQAReviewItem>();
            if (null == _data.itemsFile.slots) _data.itemsFile.slots = new List<LocQAReviewSlot>();
            if (null == _data.itemsFile.rejected) _data.itemsFile.rejected = new List<string>();
            if (null == _data.itemsFile.unused) _data.itemsFile.unused = new List<LocQAUnusedEntry>();
            _data.RebuildIndex();

            for (int i = 0; i < LocQALanguages.All.Length; i++)
            {
                Language _lang = LocQALanguages.All[i];
                LocQAReviewMarksFile _marks = null;
                string _path = MarksPath(_lang);
                if (true == File.Exists(_path))
                {
                    try { _marks = JsonUtility.FromJson<LocQAReviewMarksFile>(File.ReadAllText(_path, Encoding.UTF8)); }
                    catch (Exception _e) { Debug.LogError($"[LocalizationQA] {_path} 읽기 실패: {_e.Message}"); }
                }
                if (null == _marks) _marks = new LocQAReviewMarksFile();
                if (null == _marks.marks) _marks.marks = new List<LocQAReviewMark>();
                _marks.language = _lang.ToString();

                Dictionary<string, LocQAReviewMark> _map = new Dictionary<string, LocQAReviewMark>(_marks.marks.Count, StringComparer.Ordinal);
                for (int m = 0; m < _marks.marks.Count; m++) _map[_marks.marks[m].item] = _marks.marks[m];

                _data.markFiles[_lang] = _marks;
                _data.marks[_lang] = _map;
            }

            EditorApplication.update -= AutoSave;
            EditorApplication.update += AutoSave;
            AssemblyReloadEvents.beforeAssemblyReload -= SaveShared;
            AssemblyReloadEvents.beforeAssemblyReload += SaveShared;
            EditorApplication.quitting -= SaveShared;
            EditorApplication.quitting += SaveShared;
            return _data;
        }

        private static string MarksPath(Language _lang) => Path.Combine(FOLDER, "Marks_" + _lang + ".json");

        private static void AutoSave()
        {
            if (null == shared) return;
            if (false == shared.itemsDirty && 0 == shared.dirtyLanguages.Count) return;
            if (EditorApplication.timeSinceStartup - shared.lastChange < AUTOSAVE_DELAY) return;
            shared.SaveNow();
        }

        private static void SaveShared()
        {
            shared?.SaveNow();
        }

        public void SaveNow()
        {
            if (false == itemsDirty && 0 == dirtyLanguages.Count) return;
            Directory.CreateDirectory(FOLDER);
            UTF8Encoding _utf8 = new UTF8Encoding(false);

            if (true == itemsDirty)
            {
                itemsFile.items.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                itemsFile.slots.Sort((a, b) => string.CompareOrdinal(a.SlotKey, b.SlotKey));
                File.WriteAllText(Path.Combine(FOLDER, ITEMS_FILE), JsonUtility.ToJson(itemsFile, true), _utf8);
                itemsDirty = false;
            }

            foreach (Language _lang in dirtyLanguages)
            {
                LocQAReviewMarksFile _file = markFiles[_lang];
                _file.marks.Sort((a, b) => string.CompareOrdinal(a.item, b.item));
                File.WriteAllText(MarksPath(_lang), JsonUtility.ToJson(_file, true), _utf8);
            }
            dirtyLanguages.Clear();
        }

        /// <summary>디스크에서 다시 읽는다. (git pull로 다른 사람의 판정을 받은 뒤)</summary>
        public static void Reload()
        {
            shared?.SaveNow();
            shared = null;
            Changed?.Invoke();
        }

        private void RebuildIndex()
        {
            itemsByKey.Clear();
            filledSlots.Clear();
            for (int i = 0; i < itemsFile.items.Count; i++)
            {
                itemsByKey[itemsFile.items[i].Key] = itemsFile.items[i];
                filledSlots.Add(itemsFile.items[i].SlotKey);
            }
            slotsByKey.Clear();
            for (int i = 0; i < itemsFile.slots.Count; i++) slotsByKey[itemsFile.slots[i].SlotKey] = itemsFile.slots[i];

            rejected.Clear();
            for (int i = 0; i < itemsFile.rejected.Count; i++) rejected.Add(itemsFile.rejected[i]);

            unusedReasons.Clear();
            for (int i = 0; i < itemsFile.unused.Count; i++) unusedReasons[itemsFile.unused[i].entryId] = itemsFile.unused[i].reason;
        }

        /// <summary>게임에서 쓰이지 않는 문구면 그 사유, 아니면 null</summary>
        public string UnusedReason(string _entryId)
        {
            if (string.IsNullOrEmpty(_entryId)) return null;
            unusedReasons.TryGetValue(_entryId, out string _reason);
            return _reason;
        }

        public void SetUnused(string _entryId, string _reason)
        {
            itemsFile.unused.RemoveAll(u => u.entryId == _entryId);
            unusedReasons.Remove(_entryId);
            if (null != _reason)
            {
                itemsFile.unused.Add(new LocQAUnusedEntry { entryId = _entryId, reason = _reason });
                unusedReasons[_entryId] = _reason;
            }
            Touch(true);
        }

        private void Touch(bool _items)
        {
            if (true == _items) itemsDirty = true;
            lastChange = EditorApplication.timeSinceStartup;
            Version++;
            Changed?.Invoke();
        }

        // //항목
        public LocQAReviewItem Find(string _key)
        {
            if (string.IsNullOrEmpty(_key)) return null;
            itemsByKey.TryGetValue(_key, out LocQAReviewItem _item);
            return _item;
        }

        public bool IsSlotFilled(string _slotKey) => filledSlots.Contains(_slotKey);

        /// <summary>
        /// 항목을 추가한다. 이미 있으면 더 가까운 화면(중첩이 얕은 프리팹)에서 발견된 경우에만 그릴 화면을 바꾼다.
        /// 사람이 직접 추가한 항목의 화면은 건드리지 않는다.
        /// </summary>
        public bool AddOrImprove(string _slotGuid, string _slotPath, string _contextGuid, string _contextPath, int _depth, string _entryId, string _source, string _template = null)
        {
            if (string.IsNullOrEmpty(_entryId)) return false;

            string _key = _slotGuid + "|" + _slotPath + "|" + _entryId;
            bool _isGuess = "자동" == _source || "플레이" == _source;

            // 추론(자동·플레이)은 사람이나 코드 분석이 뺀 연결, 번역 대상 아님으로 표시한 칸을 되살리지 않는다.
            if (true == _isGuess && false == itemsByKey.ContainsKey(_key))
            {
                if (true == rejected.Contains(_key)) return false;
                if (true == slotsByKey.TryGetValue(_slotGuid + "|" + _slotPath, out LocQAReviewSlot _ignoredSlot) && true == _ignoredSlot.ignored) return false;
            }
            if (false == _isGuess && true == rejected.Remove(_key)) itemsFile.rejected.Remove(_key);

            if (true == itemsByKey.TryGetValue(_key, out LocQAReviewItem _existing))
            {
                bool _changed = false;

                // 플레이에서 실제로 본 위치는 점수가 같아도 우선한다. (같은 부품이 화면에 여러 개 있을 때 실제 자리)
                bool _better = _depth < _existing.contextDepth
                    || ("플레이" == _source && "플레이" != _existing.source && _depth <= _existing.contextDepth);
                if ("수동" != _existing.source && true == _better)
                {
                    _existing.contextGuid = _contextGuid;
                    _existing.contextPath = _contextPath;
                    _existing.contextDepth = _depth;
                    _changed = true;
                }
                if (false == string.IsNullOrEmpty(_template) && string.IsNullOrEmpty(_existing.template))
                {
                    _existing.template = _template;
                    _changed = true;
                }

                if (true == _changed) Touch(true);
                return _changed;
            }

            // 같은 칸이 더 알맞은 화면 안에서 발견된 적이 있으면 그 화면에서 그린다. (부품 단독보다 실제 화면 안)
            if (true == slotsByKey.TryGetValue(_slotGuid + "|" + _slotPath, out LocQAReviewSlot _slotRecord) && _slotRecord.contextDepth < _depth)
            {
                _contextGuid = _slotRecord.contextGuid;
                _contextPath = _slotRecord.contextPath;
                _depth = _slotRecord.contextDepth;
            }

            LocQAReviewItem _item = new LocQAReviewItem
            {
                slotGuid = _slotGuid,
                slotPath = _slotPath,
                contextGuid = _contextGuid,
                contextPath = _contextPath,
                contextDepth = _depth,
                entryId = _entryId,
                source = _source,
                template = _template ?? string.Empty
            };
            itemsFile.items.Add(_item);
            itemsByKey[_key] = _item;
            filledSlots.Add(_item.SlotKey);
            Touch(true);
            return true;
        }

        /// <summary>사람이 목록에서 뺀 연결. 자동 추론이 되살리지 않도록 기억해 둔다.</summary>
        public void Remove(LocQAReviewItem _item)
        {
            RemoveInternal(_item, true);
        }

        private void RemoveInternal(LocQAReviewItem _item, bool _reject)
        {
            if (null == _item || false == itemsFile.items.Remove(_item)) return;
            itemsByKey.Remove(_item.Key);
            if (true == _reject && true == rejected.Add(_item.Key)) itemsFile.rejected.Add(_item.Key);

            filledSlots.Clear();
            for (int i = 0; i < itemsFile.items.Count; i++) filledSlots.Add(itemsFile.items[i].SlotKey);

            foreach (KeyValuePair<Language, Dictionary<string, LocQAReviewMark>> _pair in marks)
            {
                if (false == _pair.Value.TryGetValue(_item.Key, out LocQAReviewMark _mark)) continue;
                _pair.Value.Remove(_item.Key);
                markFiles[_pair.Key].marks.Remove(_mark);
                dirtyLanguages.Add(_pair.Key);
            }
            Touch(true);
        }

        /// <summary>
        /// 지워졌거나 구조가 바뀐 프리팹을 기록에 반영한다. (목록 갱신·다시 읽기 때)
        ///  - 칸이 원래 정의된 프리팹이나 그 안의 텍스트 오브젝트가 없어졌으면 그 칸의 문구 연결(과 판정)·칸 기록을 지운다.
        ///  - 칸은 그대로인데 그리던 화면에서 빠졌으면 칸의 원본 프리팹에서 그리도록 옮긴다. (목록 갱신이 더 알맞은 화면을 다시 찾는다)
        /// </summary>
        /// <param name="_apply">false면 바꾸지 않고 무엇이 바뀔지만 알려준다.</param>
        /// <param name="_remove">false면 지우는 것은 빼고 화면 옮기기만 한다.</param>
        public LocQAPruneReport PruneMissing(bool _apply, bool _remove = true)
        {
            LocQAPruneReport _report = new LocQAPruneReport();
            Dictionary<string, GameObject> _prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            bool _changed = false;

            for (int i = itemsFile.items.Count - 1; i >= 0; i--)
            {
                LocQAReviewItem _item = itemsFile.items[i];
                TMP_Text _slotText = TextAt(_prefabs, _item.slotGuid, _item.slotPath);
                if (null == _slotText)
                {
                    _report.removedItems.Add($"{PrefabName(_item.slotGuid)} / {_item.slotPath} · {_item.entryId}");
                    foreach (Dictionary<string, LocQAReviewMark> _map in marks.Values)
                    {
                        if (true == _map.ContainsKey(_item.Key)) _report.removedMarks++;
                    }
                    if (true == _apply && true == _remove)
                    {
                        RemoveInternal(_item, false);
                        _changed = true;
                    }
                    continue;
                }

                if (null != TextAt(_prefabs, _item.contextGuid, _item.contextPath)) continue;
                _report.moved++;
                if (false == _apply) continue;
                _item.contextGuid = _item.slotGuid;
                _item.contextPath = _item.slotPath;
                _item.contextDepth = LocQAReviewCollector.ContextScore(_slotText.transform.root.gameObject, _slotText.transform);
                _changed = true;
            }

            for (int i = itemsFile.slots.Count - 1; i >= 0; i--)
            {
                LocQAReviewSlot _slot = itemsFile.slots[i];
                TMP_Text _slotText = TextAt(_prefabs, _slot.slotGuid, _slot.slotPath);
                if (null == _slotText)
                {
                    _report.removedSlots++;
                    if (true == _apply && true == _remove)
                    {
                        itemsFile.slots.RemoveAt(i);
                        slotsByKey.Remove(_slot.SlotKey);
                        _changed = true;
                    }
                    continue;
                }

                if (null != TextAt(_prefabs, _slot.contextGuid, _slot.contextPath)) continue;
                _report.moved++;
                if (false == _apply) continue;
                _slot.contextGuid = _slot.slotGuid;
                _slot.contextPath = _slot.slotPath;
                _slot.contextDepth = LocQAReviewCollector.ContextScore(_slotText.transform.root.gameObject, _slotText.transform);
                _changed = true;
            }

            if (true == _changed) Touch(true);
            return _report;
        }

        private static TMP_Text TextAt(Dictionary<string, GameObject> _cache, string _guid, string _path)
        {
            if (string.IsNullOrEmpty(_guid)) return null;
            if (false == _cache.TryGetValue(_guid, out GameObject _prefab))
            {
                string _assetPath = AssetDatabase.GUIDToAssetPath(_guid);
                _prefab = string.IsNullOrEmpty(_assetPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(_assetPath);
                _cache.Add(_guid, _prefab);
            }
            if (null == _prefab) return null;

            Transform _target = LocQAPaths.Find(_prefab.transform, _path);
            return null != _target ? _target.GetComponent<TMP_Text>() : null;
        }

        private static string PrefabName(string _guid)
        {
            string _assetPath = AssetDatabase.GUIDToAssetPath(_guid);
            return string.IsNullOrEmpty(_assetPath) || false == File.Exists(_assetPath) ? "(지워진 프리팹)" : Path.GetFileNameWithoutExtension(_assetPath);
        }

        public void AddOrUpdateSlot(string _slotGuid, string _slotPath, string _contextGuid, string _contextPath, int _depth, string _defaultText)
        {
            string _key = _slotGuid + "|" + _slotPath;
            if (true == slotsByKey.TryGetValue(_key, out LocQAReviewSlot _existing))
            {
                if (_depth >= _existing.contextDepth) return;
                _existing.contextGuid = _contextGuid;
                _existing.contextPath = _contextPath;
                _existing.contextDepth = _depth;
                _existing.defaultText = _defaultText;
                PropagateSlotContext(_existing);
                Touch(true);
                return;
            }

            LocQAReviewSlot _slot = new LocQAReviewSlot
            {
                slotGuid = _slotGuid,
                slotPath = _slotPath,
                contextGuid = _contextGuid,
                contextPath = _contextPath,
                contextDepth = _depth,
                defaultText = _defaultText
            };
            itemsFile.slots.Add(_slot);
            slotsByKey[_key] = _slot;
            PropagateSlotContext(_slot);
            Touch(true);
        }

        /// <summary>칸이 더 알맞은 화면에서 발견되면, 이미 있는 그 칸의 문구들도 그 화면에서 그리도록 옮긴다. (사람이 정한 것은 제외)</summary>
        private void PropagateSlotContext(LocQAReviewSlot _slot)
        {
            for (int i = 0; i < itemsFile.items.Count; i++)
            {
                LocQAReviewItem _item = itemsFile.items[i];
                if (_item.SlotKey != _slot.SlotKey || "수동" == _item.source || _item.contextDepth <= _slot.contextDepth) continue;
                _item.contextGuid = _slot.contextGuid;
                _item.contextPath = _slot.contextPath;
                _item.contextDepth = _slot.contextDepth;
            }
        }

        public LocQAReviewSlot FindSlot(string _slotKey)
        {
            slotsByKey.TryGetValue(_slotKey, out LocQAReviewSlot _slot);
            return _slot;
        }

        public void SetSamples(LocQAReviewItem _item, string _samples)
        {
            if (null == _item) return;
            string _value = _samples ?? string.Empty;
            if (_item.samples == _value) return;
            _item.samples = _value;
            Touch(true);
        }

        public void SetTemplate(LocQAReviewItem _item, string _template)
        {
            if (null == _item) return;
            string _value = _template ?? string.Empty;
            if (_item.template == _value) return;
            _item.template = _value;
            Touch(true);
        }

        public void SetSlotIgnored(LocQAReviewSlot _slot, bool _ignored)
        {
            if (null == _slot || _slot.ignored == _ignored) return;
            _slot.ignored = _ignored;
            Touch(true);
        }

        // //판정
        public LocQAReviewMark GetMark(LocQAReviewItem _item, Language _lang)
        {
            if (null == _item) return null;
            marks[_lang].TryGetValue(_item.Key, out LocQAReviewMark _mark);
            return _mark;
        }

        public void SetMark(LocQAReviewItem _item, Language _lang, LocQAReviewState _state, string _hash, string _memo)
        {
            if (null == _item) return;

            Dictionary<string, LocQAReviewMark> _map = marks[_lang];
            _map.TryGetValue(_item.Key, out LocQAReviewMark _mark);

            if (LocQAReviewState.None == _state && string.IsNullOrEmpty(_memo))
            {
                if (null == _mark) return;
                _map.Remove(_item.Key);
                markFiles[_lang].marks.Remove(_mark);
            }
            else
            {
                if (null == _mark)
                {
                    _mark = new LocQAReviewMark { item = _item.Key };
                    _map[_item.Key] = _mark;
                    markFiles[_lang].marks.Add(_mark);
                }
                _mark.state = (int)_state;
                _mark.hash = _hash;
                _mark.memo = _memo ?? string.Empty;
                _mark.by = Reviewer;
                _mark.at = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            }

            dirtyLanguages.Add(_lang);
            Touch(false);
        }

        /// <summary>판정이 지금 문구에 대해 유효한지까지 따진 상태. 번역이 바뀌었으면 "변경됨"으로 본다.</summary>
        public EffectiveState GetEffective(LocQAReviewItem _item, Language _lang, string _currentHash)
        {
            LocQAReviewMark _mark = GetMark(_item, _lang);
            if (null == _mark || LocQAReviewState.None == (LocQAReviewState)_mark.state) return EffectiveState.Todo;
            if (_mark.hash != _currentHash) return EffectiveState.Changed;
            return LocQAReviewState.Ok == (LocQAReviewState)_mark.state ? EffectiveState.Ok : EffectiveState.Ng;
        }

        public enum EffectiveState
        {
            Todo,
            Ok,
            Ng,
            Changed
        }

        public static string Hash(string _text)
        {
            string _s = LocQAStringTable.Normalize(_text);
            uint _h = 2166136261;
            for (int i = 0; i < _s.Length; i++)
            {
                _h ^= _s[i];
                _h *= 16777619;
            }
            return _h.ToString("x8");
        }
    }

    // //검수 목록 수집
    internal static class LocQAReviewCollector
    {
        private static Dictionary<string, List<string>> prefabIndex;
        private sealed class RuntimeSlot
        {
            public string contextGuid;
            public string contextPath;
            public int depth;
            public string slotGuid;
            public string slotPath;
            public readonly HashSet<string> recorded = new HashSet<string>(StringComparer.Ordinal);
        }

        // 자동 수집은 0.5초마다 화면 전체를 훑으므로, 텍스트별 원본 위치 찾기 결과를 기억해 둔다. (못 찾은 것도 null로)
        private static readonly Dictionary<TMP_Text, RuntimeSlot> runtimeCache = new Dictionary<TMP_Text, RuntimeSlot>();

        /// <summary>
        /// UI 프리팹을 훑어 텍스트 칸과 거기 들어갈 문구를 모은다. (프리팹 기본 문구로 추론되는 것 + 사람이 지정한 것)
        /// 프리팹을 수정하지 않고 에셋을 읽기만 한다.
        /// </summary>
        public static void CollectFromPrefabs(LocQASettings _settings)
        {
            string _folder = string.IsNullOrEmpty(_settings.prefabFolder) ? "Assets/Prefabs/UI" : _settings.prefabFolder.TrimEnd('/');
            if (false == AssetDatabase.IsValidFolder(_folder))
            {
                EditorUtility.DisplayDialog("Localization QA", $"폴더를 찾을 수 없습니다: {_folder}", "확인");
                return;
            }

            LocQAReviewData _data = LocQAReviewData.Shared;
            LocQAStringTable _table = LocQAStringTable.Load();
            LocQABindingSet _bindings = LocQABindingSet.Load();
            string[] _guids = AssetDatabase.FindAssets("t:Prefab", new[] { _folder });

            try
            {
                for (int p = 0; p < _guids.Length; p++)
                {
                    string _path = AssetDatabase.GUIDToAssetPath(_guids[p]);
                    if (true == EditorUtility.DisplayCancelableProgressBar("Localization QA · 검수 목록 갱신", _path, (float)p / _guids.Length)) break;

                    GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                    if (null == _prefab || null == _prefab.GetComponentInChildren<TMP_Text>(true)) continue;

                    // 에셋을 읽기만 하므로 인스턴스를 만들지 않는다.
                    List<LocQATextRecord> _records = LocQAPrefabScanner.BuildRecords(_prefab, _guids[p], _prefab.name, _table, _bindings);
                    for (int r = 0; r < _records.Count; r++)
                    {
                        LocQATextRecord _rec = _records[r];
                        if (false == GetSlot(_rec.text, out string _slotGuid, out string _slotPath)) continue;
                        int _depth = ContextScore(_prefab, _rec.text.transform);

                        if (true == _rec.ignored) continue;

                        // 모든 칸을 기록해 둔다. (문구가 연결된 칸은 목록에서 숨겨지고, 연결이 모두 빠지면 다시 "연결 필요"로 드러난다)
                        _data.AddOrUpdateSlot(_slotGuid, _slotPath, _guids[p], _rec.path, _depth, _rec.text.text);
                        if (null != _rec.entry)
                        {
                            _data.AddOrImprove(_slotGuid, _slotPath, _guids[p], _rec.path, _depth, _rec.entry.id, "자동");
                        }
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            _data.SaveNow();
        }

        /// <summary>텍스트가 원래 정의된 프리팹과 그 안의 경로. (중첩 프리팹이면 가장 안쪽 원본)</summary>
        public static bool GetSlot(TMP_Text _text, out string _guid, out string _path)
        {
            _guid = null;
            _path = null;
            if (null == _text) return false;

            TMP_Text _source = null;
            if (true == PrefabUtility.IsPartOfPrefabInstance(_text)) _source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(_text);
            if (null == _source) _source = _text;

            string _assetPath = AssetDatabase.GetAssetPath(_source);
            if (string.IsNullOrEmpty(_assetPath)) return false;

            _guid = AssetDatabase.AssetPathToGUID(_assetPath);
            _path = LocQAPaths.GetPath(_source.transform, _source.transform.root);
            return true;
        }

        /// <summary>그 화면에서 그리기에 얼마나 알맞은지. 낮을수록 좋다. (UIView 화면 우선, 그다음 중첩이 얕은 순)</summary>
        public static int ContextScore(GameObject _contextRoot, Transform _text)
        {
            int _score = null != _contextRoot.GetComponent<UIView>() ? 0 : 1000;
            return _score + NestingDepth(_text, _contextRoot.transform);
        }

        private static int NestingDepth(Transform _from, Transform _root)
        {
            int _depth = 0;
            for (Transform _t = _from; null != _t && _t != _root; _t = _t.parent)
            {
                if (true == PrefabUtility.IsAnyPrefabInstanceRoot(_t.gameObject)) _depth++;
            }
            return _depth;
        }

        /// <summary>
        /// 플레이 중 화면에 나온 문구를 그 텍스트 칸의 검수 항목으로 기록한다.
        /// 런타임에 생성된 오브젝트는 프리팹 연결이 없으므로, 조상 오브젝트 이름으로 원본 프리팹을 찾아 경로를 맞춘다.
        /// </summary>
        public static void Observe(TMP_Text _text, LocQAEntry _entry, string _prefabFolder, string _template = null)
        {
            if (null == _text || null == _entry) return;

            if (false == runtimeCache.TryGetValue(_text, out RuntimeSlot _slot))
            {
                _slot = null;
                if (true == ResolveRuntime(_text, _prefabFolder, out string _contextGuid, out string _contextPath, out TMP_Text _assetText)
                    && true == GetSlot(_assetText, out string _slotGuid, out string _slotPath))
                {
                    _slot = new RuntimeSlot
                    {
                        contextGuid = _contextGuid,
                        contextPath = _contextPath,
                        depth = ContextScore(_assetText.transform.root.gameObject, _assetText.transform),
                        slotGuid = _slotGuid,
                        slotPath = _slotPath
                    };
                }
                runtimeCache[_text] = _slot;
            }

            if (null == _slot || false == _slot.recorded.Add(_entry.id)) return;
            LocQAReviewData.Shared.AddOrImprove(_slot.slotGuid, _slot.slotPath, _slot.contextGuid, _slot.contextPath, _slot.depth, _entry.id, "플레이", _template);
        }

        private static bool ResolveRuntime(TMP_Text _text, string _prefabFolder, out string _contextGuid, out string _contextPath, out TMP_Text _assetText)
        {
            _contextGuid = null;
            _contextPath = null;
            _assetText = null;

            if (null == prefabIndex) BuildPrefabIndex(_prefabFolder);

            // 조상 중 이름이 프리팹과 같은 곳을 모두 따져, 가장 알맞은 화면(UIView 화면 우선)을 고른다.
            // 키 가이드 줄이면 KeyGuideRow 단독보다 그것이 들어 있는 TentUI 화면이 낫다.
            int _bestScore = int.MaxValue;
            for (Transform _ancestor = _text.transform; null != _ancestor; _ancestor = _ancestor.parent)
            {
                string _name = CleanName(_ancestor.name);
                if (false == prefabIndex.TryGetValue(_name, out List<string> _candidates)) continue;

                string _relative = LocQAPaths.GetPath(_text.transform, _ancestor);
                for (int i = 0; i < _candidates.Count; i++)
                {
                    GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_candidates[i]);
                    if (null == _prefab) continue;

                    Transform _found = LocQAPaths.Find(_prefab.transform, _relative);
                    if (null == _found || false == _found.TryGetComponent(out TMP_Text _foundText)) continue;

                    int _score = ContextScore(_prefab, _found);
                    if (_score >= _bestScore) continue;

                    _bestScore = _score;
                    _contextGuid = AssetDatabase.AssetPathToGUID(_candidates[i]);
                    _contextPath = _relative;
                    _assetText = _foundText;
                }
            }
            return null != _assetText;
        }

        private static string CleanName(string _name)
        {
            string _clean = _name.Replace("(Clone)", string.Empty).Trim();
            if (_clean.EndsWith("_Instance", StringComparison.Ordinal)) _clean = _clean.Substring(0, _clean.Length - 9);
            return _clean;
        }

        private static void BuildPrefabIndex(string _prefabFolder)
        {
            prefabIndex = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            string _folder = string.IsNullOrEmpty(_prefabFolder) ? "Assets/Prefabs" : _prefabFolder.TrimEnd('/');
            if (false == AssetDatabase.IsValidFolder(_folder)) return;

            string[] _guids = AssetDatabase.FindAssets("t:Prefab", new[] { _folder });
            for (int i = 0; i < _guids.Length; i++)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guids[i]);
                string _name = Path.GetFileNameWithoutExtension(_path);
                if (false == prefabIndex.TryGetValue(_name, out List<string> _list))
                {
                    _list = new List<string>(1);
                    prefabIndex.Add(_name, _list);
                }
                _list.Add(_path);
            }
        }

        [InitializeOnLoadMethod]
        private static void ResetOnPlayModeChange()
        {
            EditorApplication.playModeStateChanged += _ =>
            {
                prefabIndex = null;
                runtimeCache.Clear();
            };
        }
    }

    // //화면(UIView) 묶음
    /// <summary>
    /// 부품 프리팹이 어느 화면(UIView가 붙은 프리팹)에 속하는지. 화면 프리팹이 참조하는 프리팹(중첩·직렬화 필드 모두)을
    /// 그 화면의 부품으로 본다. 예) TentUI 화면 → ToolTip, KeyGuideRow, MoneyPanel
    /// </summary>
    internal static class LocQAScreenIndex
    {
        public const string UNKNOWN_SCREEN = "";

        private static Dictionary<string, List<string>> screensOf;
        private static readonly List<string> Unknown = new List<string> { UNKNOWN_SCREEN };

        public static void Invalidate()
        {
            screensOf = null;
            itemScreensCache.Clear();
        }

        private static readonly Dictionary<string, List<string>> itemScreensCache = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>
        /// 칸이 보이는 모든 화면. 그리는 화면뿐 아니라 칸이 원래 정의된 프리팹을 쓰는 화면도 합친다.
        /// (옵션 창은 ESC 메뉴와 메인 메뉴 양쪽에 들어 있으므로 두 화면 모두에서 보여야 한다)
        /// </summary>
        public static List<string> ScreensOfSlot(string _contextGuid, string _slotGuid, string _prefabFolder)
        {
            if (null == screensOf) itemScreensCache.Clear();
            string _key = _contextGuid + "|" + _slotGuid;
            if (true == itemScreensCache.TryGetValue(_key, out List<string> _cached) && null != screensOf) return _cached;

            List<string> _result = new List<string>(ScreensOf(_contextGuid, _prefabFolder));
            List<string> _fromSlot = ScreensOf(_slotGuid, _prefabFolder);
            for (int i = 0; i < _fromSlot.Count; i++)
            {
                if (false == _result.Contains(_fromSlot[i])) _result.Add(_fromSlot[i]);
            }
            // 어느 화면인지 알 수 있으면 "미확인" 묶음에는 넣지 않는다.
            if (_result.Count > 1) _result.Remove(UNKNOWN_SCREEN);

            itemScreensCache[_key] = _result;
            return _result;
        }

        public static List<string> ScreensOf(string _contextGuid, string _prefabFolder)
        {
            if (null == screensOf) Build(_prefabFolder);
            return true == screensOf.TryGetValue(_contextGuid, out List<string> _list) && _list.Count > 0 ? _list : Unknown;
        }

        private static void Build(string _prefabFolder)
        {
            screensOf = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            string _folder = string.IsNullOrEmpty(_prefabFolder) ? "Assets/Prefabs/UI" : _prefabFolder.TrimEnd('/');
            if (false == AssetDatabase.IsValidFolder(_folder)) return;

            string[] _guids = AssetDatabase.FindAssets("t:Prefab", new[] { _folder });
            for (int i = 0; i < _guids.Length; i++)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guids[i]);
                GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                if (null == _prefab || null == _prefab.GetComponent<UIView>()) continue;

                Add(_guids[i], _guids[i]);
                string[] _deps = AssetDatabase.GetDependencies(_path, true);
                for (int d = 0; d < _deps.Length; d++)
                {
                    if (false == _deps[d].EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || _deps[d] == _path) continue;
                    Add(AssetDatabase.AssetPathToGUID(_deps[d]), _guids[i]);
                }
            }
        }

        private static void Add(string _component, string _screen)
        {
            if (false == screensOf.TryGetValue(_component, out List<string> _list))
            {
                _list = new List<string>(2);
                screensOf.Add(_component, _list);
            }
            if (false == _list.Contains(_screen)) _list.Add(_screen);
        }
    }

    // //언어별 화면 렌더링
    internal sealed class LocQAReviewCell
    {
        public Language language;
        public string text;
        public string hash;
        public bool fallback;       // 번역이 없어 다른 언어(영어·스페인어) 문구가 대신 나오는 칸
        public bool invisible;      // 그렸지만 글자가 화면에 나타나지 않은 칸 (가려짐·잘림 등). 검수할 수 없으므로 표시한다.
        public Texture2D full;
        public Texture2D crop;
        public Rect fullBoxUv;
        public Rect fullGlyphUv;
        public Rect cropBoxUv;
        public Rect cropGlyphUv;
        public readonly List<KeyValuePair<LocQAKind, string>> findings = new List<KeyValuePair<LocQAKind, string>>(2);

        public void Release()
        {
            if (null != full) UnityEngine.Object.DestroyImmediate(full);
            if (null != crop) UnityEngine.Object.DestroyImmediate(crop);
            full = null;
            crop = null;
        }
    }

    /// <summary>검수 화면 하나를 그려 달라는 요청. (어느 화면의 어느 칸에 어떤 문구를 어떤 모양으로)</summary>
    internal struct LocQARenderRequest
    {
        public string contextGuid;
        public string contextPath;
        public LocQAEntry entry;        // null이면 프리팹 기본 문구 그대로(폰트만 언어별로)
        public string template;
        public string samples;

        public static LocQARenderRequest From(LocQAReviewItem _item, LocQAEntry _entry)
        {
            return new LocQARenderRequest
            {
                contextGuid = _item.contextGuid,
                contextPath = _item.contextPath,
                entry = _entry,
                template = _item.template,
                samples = _item.samples
            };
        }

        public static LocQARenderRequest From(LocQAReviewSlot _slot)
        {
            return new LocQARenderRequest { contextGuid = _slot.contextGuid, contextPath = _slot.contextPath };
        }
    }

    /// <summary>
    /// 검수 화면을 그리는 작업 단위. 화면 프리팹을 프리뷰 씬에 한 번 띄워 두고, 칸·문구·언어만 바꿔 가며 계속 찍는다.
    /// 언어별 검수에서 같은 화면(툴팁 등)의 문구 수백 개를 차례로 그릴 때, 매번 프리팹을 새로 띄우지 않기 위해서다.
    /// 한 번 그릴 때 바꾼 상태(켠 오브젝트, 투명도, 위치)는 다음 그리기 전에 모두 되돌린다.
    /// </summary>
    internal sealed class LocQARenderSession : IDisposable
    {
        private const float CROP_PAD = 24f;
        private const float MIN_CROP_W = 200f;
        private const float MIN_CROP_H = 80f;

        private readonly LocQASettings settings;
        private readonly LocQAStringTable table;
        private readonly LocQABindingSet bindings;
        private readonly LocQAFontResolver fonts;
        private readonly LocQAStage stage;
        // 검수 대상이 아닌 주변 칸을 채울 문구 후보
        private readonly Dictionary<string, List<string>> entriesAtPath = new Dictionary<string, List<string>>(StringComparer.Ordinal); // 화면|경로 → 그 자리에 기록된 문구들
        private readonly Dictionary<string, List<string>> entriesOfSlot = new Dictionary<string, List<string>>(StringComparer.Ordinal); // 칸 → 연결된 문구들

        private string contextGuid;
        private GameObject instance;
        // 등장 연출 훅이 오브젝트를 옮기기 전의 경로 → 오브젝트. 칸 경로는 프리팹 기준이므로 이것으로 찾는다.
        private readonly Dictionary<string, Transform> pathMap = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private Vector3 instanceOrigin;
        private List<LocQATextRecord> records;
        private LocQAEntry[] baseEntries;

        // 게임이 실행 중에 붙이는 부품(툴팁·키 설정 행 등)의 칸에 넣을 문구 (경로 → 키). LocQAHosting.Attach가 채운다.
        private readonly Dictionary<string, string> hostFills = new Dictionary<string, string>(StringComparer.Ordinal);
        // 지금 그리는 칸의 부품 내용·위치 맞추기 (툴팁 내용, 말풍선 꼬리 등). 없으면 null
        private LocQAHostPlan hostPlan;

        // 언어 이름이 들어가는 주변 칸(옵션의 언어 행 값 등) → 언어별로 넣을 문구.
        // 게임은 그 칸에 항상 "현재 언어의 이름"을 보여주므로, 중국어로 그릴 때는 简体中文을 넣는다. (English로 고정하면 게임과 다르다)
        private readonly Dictionary<int, Dictionary<Language, LocQAEntry>> ownLanguageFill = new Dictionary<int, Dictionary<Language, LocQAEntry>>();

        // 되돌리기 기록
        private readonly List<GameObject> activated = new List<GameObject>(8);
        private readonly List<KeyValuePair<CanvasGroup, float>> alphas = new List<KeyValuePair<CanvasGroup, float>>(4);
        private readonly List<KeyValuePair<Transform, Vector3>> scales = new List<KeyValuePair<Transform, Vector3>>(4);
        private readonly List<KeyValuePair<RectTransform, Vector2>> scrolled = new List<KeyValuePair<RectTransform, Vector2>>(2);
        private readonly List<GameObject> hiddenOccluders = new List<GameObject>(4);

        public Vector2 CanvasSize { get; }

        public LocQARenderSession(LocQASettings _settings)
        {
            settings = _settings;
            table = LocQAStringTable.Load();
            bindings = LocQABindingSet.Load();
            fonts = LocQAFontResolver.Load();
            CanvasSize = _settings.CanvasSize;
            stage = new LocQAStage(true, CanvasSize, 1);

            // 검수 대상이 아닌 주변 칸도 실제 화면에 가깝게 채우기 위한 후보를 모아 둔다. (FillEntry 참고)
            List<LocQAReviewItem> _items = LocQAReviewData.Shared.Items;
            for (int i = 0; i < _items.Count; i++)
            {
                LocQAReviewItem _item = _items[i];
                string _pathKey = _item.contextGuid + "|" + _item.contextPath;
                if (false == entriesAtPath.TryGetValue(_pathKey, out List<string> _atPath))
                {
                    _atPath = new List<string>(2);
                    entriesAtPath.Add(_pathKey, _atPath);
                }
                _atPath.Add(_item.entryId);

                if (false == entriesOfSlot.TryGetValue(_item.SlotKey, out List<string> _list))
                {
                    _list = new List<string>(4);
                    entriesOfSlot.Add(_item.SlotKey, _list);
                }
                _list.Add(_item.entryId);
            }
        }

        public void Dispose()
        {
            fonts.Dispose();
            stage.Dispose();
        }

        /// <summary>
        /// 요청한 칸에 문구를 넣고 언어마다 찍는다.
        /// _sharedCrop이면 모든 언어를 같은 영역으로 잘라 나란히 비교할 수 있게 하고, 아니면 칸마다 따로 자른다.
        /// _keepFull이 false면 잘라낸 그림만 남기고 전체 화면 그림은 버린다. (언어별 검수에서 메모리를 아끼려고)
        /// </summary>
        public List<LocQAReviewCell> Render(LocQARenderRequest _request, List<Language> _langs, bool _sharedCrop, bool _keepFull, out string _error, out string _note)
        {
            _error = null;
            _note = null;
            List<LocQAReviewCell> _cells = new List<LocQAReviewCell>(_langs.Count);

            // 게임이 다른 화면에 붙이는 부품이면 그 화면 안의 그 자리에서 그린다. (LocQAHosting)
            string _contextGuid = _request.contextGuid;
            string _contextPath = _request.contextPath;
            if (true == settings.InGameView && true == LocQAHosting.TryRedirect(_contextGuid, _contextPath, null != _request.entry ? _request.entry.id : null, out string _hostGuid, out string _hostPath))
            {
                _contextGuid = _hostGuid;
                _contextPath = _hostPath;
            }

            if (false == Prepare(_contextGuid, out _error)) return _cells;
            Restore();

            pathMap.TryGetValue(_contextPath ?? string.Empty, out Transform _target);
            if (null == _target) _target = LocQAPaths.Find(instance.transform, _contextPath);
            TMP_Text _text = null != _target ? _target.GetComponent<TMP_Text>() : null;
            if (null == _text)
            {
                _error = "화면 프리팹에서 텍스트 칸을 찾지 못했습니다. (프리팹이 지워졌거나 구조가 바뀌었습니다. '다시 읽기'를 누르면 정리됩니다)";
                return _cells;
            }

            for (int i = 0; i < records.Count; i++)
            {
                records[i].entry = records[i].text == _text ? _request.entry : baseEntries[i];
            }

            // 그 칸이 보일 때의 화면 상태(고른 탭, 툴팁 내용 등)로 맞춘다.
            hostPlan = true == settings.InGameView ? LocQAHosting.Begin(instance, _target, _request.entry, table) : null;

            Reveal(_target);

            // 칸이 있는 창이 열려 있을 때 게임이 감추는 다른 창을 감춘다. (ESC 메뉴 위에 옵션 창을 연 경우 등)
            List<GameObject> _covered = LocQALayoutHooks.CoveredWindows(_target, instance.transform);
            for (int i = 0; i < _covered.Count; i++)
            {
                if (false == _covered[i].activeSelf) continue;
                _covered[i].SetActive(false);
                hiddenOccluders.Add(_covered[i]);
            }

            // 연출로 화면 밖에서 들어오는 UI(던전 상태 배너 등)는 프리팹 배치 그대로면 캔버스 밖에 있다.
            // 첫 언어로 배치를 잡은 뒤, 칸이 화면 밖이면 UI 전체를 옮겨 칸이 가운데 오게 한다.
            if (_langs.Count > 0)
            {
                Apply(_request, _text, _langs[0]);

                // 스크롤 목록 안의 칸이면, 게임에서 그 항목으로 이동했을 때처럼 스크롤해서 보이게 한다.
                ScrollIntoView(_text);
                if (true == BringIntoView(_text)) _note = "원래 화면 밖에 배치된 UI라(연출로 들어오는 UI 등) 칸이 가운데 오도록 옮겨서 그렸습니다.";

                // 그래도 글자가 안 보이면, 그 위에 덮여 그려지는 다른 패널(겹쳐 있는 다른 탭 등)을 숨긴다.
                // 게임에서는 한 번에 한 패널만 보이지만 프리팹에는 여러 패널이 켜진 채 겹쳐 저장된 경우다.
                if (false == ProbeVisible(_text))
                {
                    if (true == HideOccluders(_text)) _note = (null == _note ? string.Empty : _note + " ") + "칸을 가리는 다른 패널을 숨기고 그렸습니다.";
                }
            }

            Rect _union = default;
            bool _hasUnion = false;
            List<Rect> _regions = new List<Rect>(_langs.Count);

            for (int l = 0; l < _langs.Count; l++)
            {
                Language _lang = _langs[l];
                Apply(_request, _text, _lang);

                LocQAMeasurement _m = LocQATextAnalyzer.Measure(_text, settings, true, false);

                LocQAReviewCell _cell = new LocQAReviewCell
                {
                    language = _lang,
                    text = null != _request.entry ? LocQALanguages.Resolve(_request.entry.data, _lang) : _text.text,
                    full = stage.Render(),
                    fullBoxUv = stage.WorldToUv(_m.boxWorld),
                    fullGlyphUv = true == _m.hasGlyphs ? stage.WorldToUv(_m.glyphWorld) : stage.WorldToUv(_m.boxWorld)
                };
                _cell.hash = LocQAReviewData.Hash(_cell.text);
                _cell.fallback = null != _request.entry && true == LocQALanguages.IsFallback(_request.entry.data, _lang);
                _cell.findings.AddRange(_m.findings);
                _cell.invisible = false == string.IsNullOrWhiteSpace(_text.text) && false == IsDrawn(_text, _cell.full, _cell.fullGlyphUv);
                _cells.Add(_cell);

                Rect _region = Union(_cell.fullBoxUv, _cell.fullGlyphUv);
                RectTransform _background = LocQATextAnalyzer.FindBackground(_text.rectTransform);
                if (null != _background)
                {
                    Vector3[] _corners = new Vector3[4];
                    _background.GetWorldCorners(_corners);
                    Rect _bg = stage.WorldToUv(_corners);
                    // 화면 전체를 덮는 배경이면 잘라낼 의미가 없으므로 넣지 않는다.
                    if (_bg.width < 0.9f || _bg.height < 0.9f) _region = Union(_region, _bg);
                }
                _regions.Add(_region);
                _union = true == _hasUnion ? Union(_union, _region) : _region;
                _hasUnion = true;
            }

            for (int i = 0; i < _cells.Count; i++)
            {
                LocQAReviewCell _cell = _cells[i];
                Rect _crop = ExpandCrop(true == _sharedCrop ? _union : _regions[i]);
                _cell.crop = Crop(_cell.full, _crop);
                _cell.cropBoxUv = ToCropSpace(_cell.fullBoxUv, _crop);
                _cell.cropGlyphUv = ToCropSpace(_cell.fullGlyphUv, _crop);
                if (false == _keepFull)
                {
                    UnityEngine.Object.DestroyImmediate(_cell.full);
                    _cell.full = null;
                }
            }
            return _cells;
        }

        // //내부 로직
        /// <summary>
        /// 검수 대상이 아닌 주변 칸 중, 그 자리에 기록된 문구도 프리팹 문구로 찾은 키도 없는 칸에 넣을 문구.
        /// 실제 화면과 다른 문구로 채우지 않도록 확실한 것만 고른다.
        ///  1) 그 칸에 연결된 문구가 하나뿐이면 그것
        ///  2) 그 칸에 연결된 문구 중 행 이름과 맞는 것 (옵션 행 OPT_WindowMode ↔ WindowMode)
        ///  3) 없으면 null — 프리팹에 적힌 원래 문구를 그대로 둔다.
        /// (같은 행 프리팹을 여러 번 재사용하는 칸에서 아무 문구나 채우면 "창모드" 행에 다른 제목이 나오는 식으로 틀린다)
        /// </summary>
        private LocQAEntry FillEntry(GameObject _prefab, LocQATextRecord _rec)
        {
            // 띄운 화면은 프리팹 연결이 없는 복사본이라, 같은 경로의 원본 에셋 텍스트로 칸을 찾는다.
            Transform _source = LocQAPaths.Find(_prefab.transform, _rec.path);
            TMP_Text _sourceText = null != _source ? _source.GetComponent<TMP_Text>() : null;
            if (false == LocQAReviewCollector.GetSlot(_sourceText, out string _g, out string _p)) return null;
            if (false == entriesOfSlot.TryGetValue(_g + "|" + _p, out List<string> _candidates)) return null;
            if (1 == _candidates.Count) return table.Find(_candidates[0]);

            // 행 이름(조상 오브젝트 이름)과 키가 맞는 문구
            string _best = null;
            int _bestLength = 0;
            int _depth = 0;
            for (Transform _t = _rec.text.transform; null != _t && _depth < 4; _t = _t.parent, _depth++)
            {
                string _name = SimplifyName(_t.name);
                if (_name.Length < 3) continue;
                for (int i = 0; i < _candidates.Count; i++)
                {
                    int _slash = _candidates[i].LastIndexOf('/');
                    string _key = SimplifyName(_slash >= 0 ? _candidates[i].Substring(_slash + 1) : _candidates[i]);
                    if (_key.Length < 3 || _key.Length <= _bestLength) continue;
                    if (true == _name.Contains(_key) || true == _key.Contains(_name))
                    {
                        _best = _candidates[i];
                        _bestLength = _key.Length;
                    }
                }
                if (null != _best) break;
            }
            return null != _best ? table.Find(_best) : null;
        }

        /// <summary>
        /// 주변 칸 중 언어 이름이 들어가는 칸을 찾아, 그릴 언어마다 넣을 "그 언어의 이름" 문구를 정해 둔다.
        /// 그 자리에 연결된 언어 이름 문구가 있으면 그중에서(옵션 행은 짧은 이름 ...Short), 없으면 같은 종류(짧은/긴 이름)에서 고른다.
        /// </summary>
        private void BuildOwnLanguageFill(string _contextGuid)
        {
            ownLanguageFill.Clear();
            for (int i = 0; i < records.Count; i++)
            {
                LocQAEntry _base = baseEntries[i];
                if (null == _base || false == LocQALanguages.TryGetOwnLanguage(_base.id, out _)) continue;

                List<string> _candidates = new List<string>(20);
                if (true == entriesAtPath.TryGetValue(_contextGuid + "|" + records[i].path, out List<string> _atPath))
                {
                    for (int c = 0; c < _atPath.Count; c++)
                    {
                        if (true == LocQALanguages.TryGetOwnLanguage(_atPath[c], out _)) _candidates.Add(_atPath[c]);
                    }
                }
                if (_candidates.Count < 2) _candidates = LocQALanguages.OwnLanguageEntryIds(_base.id.EndsWith("Short", StringComparison.Ordinal));

                Dictionary<Language, LocQAEntry> _map = new Dictionary<Language, LocQAEntry>();
                for (int c = 0; c < _candidates.Count; c++)
                {
                    if (false == LocQALanguages.TryGetOwnLanguage(_candidates[c], out Language _lang) || true == _map.ContainsKey(_lang)) continue;
                    LocQAEntry _entry = table.Find(_candidates[c]);
                    if (null != _entry) _map.Add(_lang, _entry);
                }
                if (_map.Count > 0) ownLanguageFill.Add(i, _map);
            }
        }

        private static readonly Regex TemplateKey = new Regex(@"\{key:([^}]+)\}", RegexOptions.Compiled);

        /// <summary>
        /// 표시 형식 안의 "{key:파일/키}"를 그 언어의 문구로 바꾼다. 코드가 다른 로컬라이징 문구를 덧붙이는 칸용.
        /// 예) 게임패드 자동: "{text} ({key:OptionUI/GamepadIconGeneric})" → 독일어면 "Automatisch (Standard)"
        /// </summary>
        private string FillTemplateKeys(string _template, Language _lang)
        {
            if (_template.IndexOf("{key:", StringComparison.Ordinal) < 0) return _template;
            return TemplateKey.Replace(_template, _m =>
            {
                LocQAEntry _entry = table.Find(_m.Groups[1].Value.Trim());
                return null != _entry ? LocQALanguages.Resolve(_entry.data, _lang) : _m.Value;
            });
        }

        private static readonly string[] NameNoise = { "opt", "row", "tmp", "txt", "text", "visuals", "title", "value", "btn", "label" };

        private static string SimplifyName(string _name)
        {
            StringBuilder _sb = new StringBuilder(_name.Length);
            string _lower = _name.ToLowerInvariant();
            for (int i = 0; i < _lower.Length; i++)
            {
                if (true == char.IsLetterOrDigit(_lower[i])) _sb.Append(_lower[i]);
            }
            string _result = _sb.ToString();
            for (int i = 0; i < NameNoise.Length; i++) _result = _result.Replace(NameNoise[i], string.Empty);
            return _result;
        }

        private bool Prepare(string _contextGuid, out string _error)
        {
            _error = null;
            if (null != instance && _contextGuid == contextGuid) return true;

            if (null != instance) UnityEngine.Object.DestroyImmediate(instance);
            instance = null;
            activated.Clear();
            alphas.Clear();
            scales.Clear();

            GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(_contextGuid));
            if (null == _prefab)
            {
                _error = "화면 프리팹을 찾을 수 없습니다. (지워졌다면 '다시 읽기'를 누르면 정리됩니다)";
                return false;
            }

            contextGuid = _contextGuid;
            instance = stage.Spawn(_prefab);
            instanceOrigin = instance.transform.position;

            // 게임이 실행 중에 이 화면에 붙이는 부품을 먼저 붙인다. (그 텍스트도 함께 모으도록)
            hostFills.Clear();
            if (true == settings.InGameView) LocQAHosting.Attach(instance, _contextGuid, hostFills);
            records = LocQAPrefabScanner.BuildRecords(instance, _contextGuid, _prefab.name, table, bindings);

            baseEntries = new LocQAEntry[records.Count];
            for (int i = 0; i < records.Count; i++)
            {
                LocQATextRecord _rec = records[i];

                // 붙인 부품의 칸은 게임 코드가 넣는 그 문구 (키 설정 행 제목 = 그 행의 동작 이름)
                if (true == hostFills.TryGetValue(_rec.path, out string _hostFill))
                {
                    baseEntries[i] = table.Find(_hostFill);
                    if (null != baseEntries[i]) continue;
                }

                // 이 자리에 연결된 문구가 있으면 그것이 우선이다. 스캐너가 프리팹 임시 문구로 찾은 키는 추측이라
                // 틀릴 수 있다. (옵션 행들은 프리팹에 다른 행의 문구 "FPS", "Master Volume"이 적힌 채 저장되어 있다)
                // 여러 개면 프리팹 문구와 같은 것을 고른다. (언어 행 값이 항상 "简体中文"로 나와 □로 보이는 일이 없게, "English"를 쓴다)
                if (true == entriesAtPath.TryGetValue(_contextGuid + "|" + _rec.path, out List<string> _atPath))
                {
                    string _pick = (null != _rec.entry && true == _atPath.Contains(_rec.entry.id)) ? _rec.entry.id : _atPath[0];
                    baseEntries[i] = table.Find(_pick);
                    if (null != baseEntries[i]) continue;
                }

                baseEntries[i] = _rec.entry;
                if (null != _rec.entry) continue;

                baseEntries[i] = FillEntry(_prefab, _rec);
            }
            BuildOwnLanguageFill(_contextGuid);

            pathMap.Clear();
            Transform[] _all = instance.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < _all.Length; i++) pathMap[LocQAPaths.GetPath(_all[i], instance.transform)] = _all[i];

            // 경로를 다 기록한 뒤 등장 연출(가림막 등)을 끝난 상태로 맞춘다. 게임 코드의 그 메서드를 그대로 실행한다.
            LocQALayoutHooks.RunSetup(instance);
            instanceOrigin = instance.transform.position;
            return true;
        }

        private void Apply(LocQARenderRequest _request, TMP_Text _text, Language _lang)
        {
            foreach (KeyValuePair<int, Dictionary<Language, LocQAEntry>> _pair in ownLanguageFill)
            {
                if (records[_pair.Key].text == _text) continue;
                records[_pair.Key].entry = true == _pair.Value.TryGetValue(_lang, out LocQAEntry _own) ? _own : baseEntries[_pair.Key];
            }
            LocQAPrefabScanner.ApplyLanguage(records, _lang, fonts, settings);
            LocQALayoutHooks.RunLanguage(instance, _lang);
            hostPlan?.ApplyLanguage(_lang);

            if (null != _request.entry)
            {
                // 문구별 견본 값과, 코드가 붙이는 숫자·태그(표시 형식)까지 실제 화면과 같은 모양으로 넣는다.
                string _filled = LocQAStringTable.FillFormat(LocQALanguages.Resolve(_request.entry.data, _lang), _request.samples, settings.formatSample);
                bool _hasTemplate = false == string.IsNullOrEmpty(_request.template) && _request.template.Contains("{text}");
                _text.text = true == _hasTemplate ? FillTemplateKeys(_request.template, _lang).Replace("{text}", _filled) : _filled;
            }
            LocQAPrefabScanner.RebuildLayout(instance);

            // 크기에 따라 정해지는 위치 (툴팁은 폭에 따라 노드 옆 위치가 달라진다)
            if (null != hostPlan)
            {
                hostPlan.AfterLayout();
                Canvas.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// 칸이 화면에 보이도록 경로를 연다. 꺼진 조상은 켜고, 팝업이 나타나는 연출의 시작 상태
        /// (CanvasGroup 투명도 0, 크기 0)는 연출이 끝난 상태로 맞춘다. 바꾼 것은 기록해 두었다가 Restore에서 되돌린다.
        /// </summary>
        private void Reveal(Transform _target)
        {
            for (Transform _t = _target; null != _t; _t = _t.parent)
            {
                if (false == _t.gameObject.activeSelf)
                {
                    _t.gameObject.SetActive(true);
                    activated.Add(_t.gameObject);
                }
                if (true == _t.TryGetComponent(out CanvasGroup _group) && _group.alpha < 0.99f)
                {
                    alphas.Add(new KeyValuePair<CanvasGroup, float>(_group, _group.alpha));
                    _group.alpha = 1f;
                }
                if (_t.localScale.sqrMagnitude < 0.0001f)
                {
                    scales.Add(new KeyValuePair<Transform, Vector3>(_t, _t.localScale));
                    _t.localScale = Vector3.one;
                }
                if (_t == instance.transform) break;
            }
        }

        private void Restore()
        {
            for (int i = activated.Count - 1; i >= 0; i--)
            {
                if (null != activated[i]) activated[i].SetActive(false);
            }
            for (int i = 0; i < alphas.Count; i++)
            {
                if (null != alphas[i].Key) alphas[i].Key.alpha = alphas[i].Value;
            }
            for (int i = 0; i < scales.Count; i++)
            {
                if (null != scales[i].Key) scales[i].Key.localScale = scales[i].Value;
            }
            activated.Clear();
            alphas.Clear();
            scales.Clear();
            for (int i = 0; i < scrolled.Count; i++)
            {
                if (null != scrolled[i].Key) scrolled[i].Key.anchoredPosition = scrolled[i].Value;
            }
            for (int i = 0; i < hiddenOccluders.Count; i++)
            {
                if (null != hiddenOccluders[i]) hiddenOccluders[i].SetActive(true);
            }
            scrolled.Clear();
            hiddenOccluders.Clear();
            if (null != instance) instance.transform.position = instanceOrigin;
        }

        private static readonly Vector3[] cornerBuffer = new Vector3[4];

        /// <summary>
        /// 칸이 스크롤 목록의 보이는 영역 밖이면 목록을 스크롤해 보이게 한다. 안쪽 스크롤부터 차례로 맞춘다.
        /// (옵션 창 아래쪽 항목처럼 프리팹 상태로는 스크롤 아래에 숨어 있는 칸)
        ///
        /// Unity ScrollRect뿐 아니라 마스크(Mask·RectMask2D)로 잘라 보여주는 모든 영역을 스크롤로 본다.
        /// 게임의 옵션 창은 자체 스크롤(UI_CustomScroll)이 마스크 안의 내용 위치를 옮기는 방식이라서다.
        /// 마스크 바로 아래에서 칸으로 이어지는 자식을 "내용"으로 보고 그 위치를 옮긴다.
        /// </summary>
        private void ScrollIntoView(TMP_Text _text)
        {
            bool _moved = false;
            Transform _child = _text.transform;
            for (Transform _t = _text.transform.parent; null != _t && _t != instance.transform; _child = _t, _t = _t.parent)
            {
                RectTransform _viewport = null;
                RectTransform _content = null;
                bool _vertical = true;
                bool _horizontal = true;

                if (true == _t.TryGetComponent(out ScrollRect _scroll) && null != _scroll.content)
                {
                    _viewport = null != _scroll.viewport ? _scroll.viewport : (RectTransform)_scroll.transform;
                    _content = _scroll.content;
                    _vertical = _scroll.vertical;
                    _horizontal = _scroll.horizontal;
                }
                else if ((true == _t.TryGetComponent(out Mask _mask) && true == _mask.enabled)
                    || (true == _t.TryGetComponent(out RectMask2D _rectMask) && true == _rectMask.enabled))
                {
                    _viewport = _t as RectTransform;
                    _content = _child as RectTransform;
                }
                if (null == _viewport || null == _content) continue;

                Rect _target = LocalRect(_viewport, _text.rectTransform);
                Rect _view = _viewport.rect;

                Vector2 _delta = Vector2.zero;
                if (true == _vertical)
                {
                    if (_target.yMin < _view.yMin) _delta.y = _view.yMin - _target.yMin + 4f;
                    else if (_target.yMax > _view.yMax) _delta.y = _view.yMax - _target.yMax - 4f;
                }
                if (true == _horizontal)
                {
                    if (_target.xMin < _view.xMin) _delta.x = _view.xMin - _target.xMin + 4f;
                    else if (_target.xMax > _view.xMax) _delta.x = _view.xMax - _target.xMax - 4f;
                }
                if (Vector2.zero == _delta) continue;

                // 뷰포트 기준 이동량을 내용 부모 기준으로 바꿔 적용한다.
                Vector3 _world = _viewport.TransformVector(_delta);
                Vector3 _local = null != _content.parent ? _content.parent.InverseTransformVector(_world) : _world;
                scrolled.Add(new KeyValuePair<RectTransform, Vector2>(_content, _content.anchoredPosition));
                _content.anchoredPosition += new Vector2(_local.x, _local.y);
                _moved = true;
            }
            if (true == _moved) Canvas.ForceUpdateCanvases();
        }

        /// <summary>지금 상태로 그렸을 때 글자가 보이는지. (가림 처리 전에 한 번 확인하는 용도)</summary>
        private bool ProbeVisible(TMP_Text _text)
        {
            LocQAMeasurement _m = LocQATextAnalyzer.Measure(_text, settings, true, false);
            if (false == _m.hasGlyphs) return true;
            Texture2D _with = stage.Render();
            bool _drawn = IsDrawn(_text, _with, stage.WorldToUv(_m.glyphWorld));
            UnityEngine.Object.DestroyImmediate(_with);
            return _drawn;
        }

        /// <summary>
        /// 글자가 실제로 그려졌는지 픽셀로 확인한다. 글자만 투명하게 한 그림과 비교해, 글자 영역에서 바뀐 픽셀이
        /// 거의 없으면 다른 것에 가려졌거나 마스크 밖이라 안 보이는 것이다.
        /// </summary>
        private bool IsDrawn(TMP_Text _text, Texture2D _with, Rect _glyphUv)
        {
            if (null == _with) return true;
            int _w = _with.width;
            int _h = _with.height;
            int _x0 = Mathf.Clamp(Mathf.FloorToInt(_glyphUv.xMin * _w) - 1, 0, _w);
            int _x1 = Mathf.Clamp(Mathf.CeilToInt(_glyphUv.xMax * _w) + 1, 0, _w);
            int _y0 = Mathf.Clamp(Mathf.FloorToInt(_glyphUv.yMin * _h) - 1, 0, _h);
            int _y1 = Mathf.Clamp(Mathf.CeilToInt(_glyphUv.yMax * _h) + 1, 0, _h);
            if (_x1 <= _x0 || _y1 <= _y0) return false;   // 화면 밖

            // 글자(와 폴백 폰트용 하위 메시)만 투명하게 해서 한 번 더 찍는다.
            CanvasRenderer[] _renderers = _text.GetComponentsInChildren<CanvasRenderer>(true);
            float[] _alphas = new float[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _alphas[i] = _renderers[i].GetAlpha();
                _renderers[i].SetAlpha(0f);
            }
            Texture2D _without = stage.Render();
            for (int i = 0; i < _renderers.Length; i++) _renderers[i].SetAlpha(_alphas[i]);

            Color[] _a = _with.GetPixels(_x0, _y0, _x1 - _x0, _y1 - _y0);
            Color[] _b = _without.GetPixels(_x0, _y0, _x1 - _x0, _y1 - _y0);
            UnityEngine.Object.DestroyImmediate(_without);

            int _changed = 0;
            for (int i = 0; i < _a.Length; i++)
            {
                if (Mathf.Abs(_a[i].r - _b[i].r) + Mathf.Abs(_a[i].g - _b[i].g) + Mathf.Abs(_a[i].b - _b[i].b) > 0.08f) _changed++;
            }
            return _changed >= Mathf.Max(3, (int)(_a.Length * 0.005f));
        }

        /// <summary>
        /// 칸보다 나중에 그려지면서 글자 위를 덮는 그래픽이 속한 갈래(칸과 공통 조상 바로 아래의 자식)를 숨긴다.
        /// 겹쳐 저장된 다른 탭 패널 같은 것이다. 칸의 조상·자손은 건드리지 않는다. 숨겼으면 true.
        /// </summary>
        private bool HideOccluders(TMP_Text _text)
        {
            LocQAMeasurement _m = LocQATextAnalyzer.Measure(_text, settings, true, false);
            if (false == _m.hasGlyphs) return false;
            Rect _glyph = WorldRect(_m.glyphWorld);

            Graphic[] _all = instance.GetComponentsInChildren<Graphic>(false);
            int _textIndex = Array.IndexOf(_all, _text);
            if (_textIndex < 0) return false;

            bool _hidAny = false;
            for (int i = _textIndex + 1; i < _all.Length; i++)
            {
                Graphic _g = _all[i];
                if (null == _g || false == _g.isActiveAndEnabled || _g.color.a < 0.3f) continue;
                if (true == _g.transform.IsChildOf(_text.transform)) continue;
                if (_g.canvasRenderer.GetInheritedAlpha() < 0.3f) continue;

                _g.rectTransform.GetWorldCorners(cornerBuffer);
                if (false == WorldRect(cornerBuffer).Overlaps(_glyph)) continue;

                // 칸과 갈라지는 지점의 갈래를 찾는다.
                Transform _branch = _g.transform;
                while (null != _branch.parent && false == _text.transform.IsChildOf(_branch.parent)) _branch = _branch.parent;
                if (null == _branch.parent || false == _branch.gameObject.activeSelf) continue;

                _branch.gameObject.SetActive(false);
                hiddenOccluders.Add(_branch.gameObject);
                _hidAny = true;
            }

            if (true == _hidAny)
            {
                LocQAPrefabScanner.RebuildLayout(instance);
                Canvas.ForceUpdateCanvases();
            }
            return _hidAny;
        }

        private static Rect WorldRect(Vector3[] _corners)
        {
            float _xMin = float.MaxValue, _yMin = float.MaxValue, _xMax = float.MinValue, _yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                _xMin = Mathf.Min(_xMin, _corners[i].x);
                _yMin = Mathf.Min(_yMin, _corners[i].y);
                _xMax = Mathf.Max(_xMax, _corners[i].x);
                _yMax = Mathf.Max(_yMax, _corners[i].y);
            }
            return Rect.MinMaxRect(_xMin, _yMin, _xMax, _yMax);
        }

        /// <summary>칸(과 그 배경)이 캔버스 밖에 있으면 UI 전체를 옮겨 칸을 가운데 둔다. 옮겼으면 true.</summary>
        private bool BringIntoView(TMP_Text _text)
        {
            Rect _region = LocalRect(stage.canvasRoot, _text.rectTransform);
            RectTransform _background = LocQATextAnalyzer.FindBackground(_text.rectTransform);
            if (null != _background)
            {
                Rect _bg = LocalRect(stage.canvasRoot, _background);
                if (_bg.width < stage.width * 0.9f || _bg.height < stage.height * 0.9f) _region = Union(_region, _bg);
            }

            // 화면에 조금이라도 걸쳐 있으면 옮기지 않는다. 게임에서도 그 자리라 화면 밖으로 잘리는 것까지 그대로 보여야 한다.
            // (넓은 툴팁이 화면 오른쪽에서 잘리는 것 등) 완전히 밖에 있는 것은 등장 연출로 들어오는 UI다.
            // (UI 집중 보기에서는 조금이라도 나가면 옮겨 전체가 보이게 한다)
            Rect _canvas = stage.canvasRoot.rect;
            bool _inside = _region.xMin >= _canvas.xMin && _region.xMax <= _canvas.xMax && _region.yMin >= _canvas.yMin && _region.yMax <= _canvas.yMax;
            if (true == _inside || (true == settings.InGameView && true == _region.Overlaps(_canvas))) return false;

            Vector3 _delta = stage.canvasRoot.TransformVector(new Vector3(_canvas.center.x - _region.center.x, _canvas.center.y - _region.center.y, 0f));
            instance.transform.position += _delta;
            Canvas.ForceUpdateCanvases();
            return true;
        }

        private static Rect LocalRect(RectTransform _space, RectTransform _target)
        {
            _target.GetWorldCorners(cornerBuffer);
            float _xMin = float.MaxValue, _yMin = float.MaxValue, _xMax = float.MinValue, _yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 _p = _space.InverseTransformPoint(cornerBuffer[i]);
                _xMin = Mathf.Min(_xMin, _p.x);
                _yMin = Mathf.Min(_yMin, _p.y);
                _xMax = Mathf.Max(_xMax, _p.x);
                _yMax = Mathf.Max(_yMax, _p.y);
            }
            return Rect.MinMaxRect(_xMin, _yMin, _xMax, _yMax);
        }

        private static Rect Union(Rect _a, Rect _b)
        {
            return Rect.MinMaxRect(Mathf.Min(_a.xMin, _b.xMin), Mathf.Min(_a.yMin, _b.yMin), Mathf.Max(_a.xMax, _b.xMax), Mathf.Max(_a.yMax, _b.yMax));
        }

        private Rect ExpandCrop(Rect _uv)
        {
            float _padU = CROP_PAD / stage.width;
            float _padV = CROP_PAD / stage.height;
            Rect _r = Rect.MinMaxRect(_uv.xMin - _padU, _uv.yMin - _padV, _uv.xMax + _padU, _uv.yMax + _padV);

            float _minW = MIN_CROP_W / stage.width;
            float _minH = MIN_CROP_H / stage.height;
            if (_r.width < _minW) _r = new Rect(_r.center.x - _minW * 0.5f, _r.y, _minW, _r.height);
            if (_r.height < _minH) _r = new Rect(_r.x, _r.center.y - _minH * 0.5f, _r.width, _minH);

            return Rect.MinMaxRect(Mathf.Clamp01(_r.xMin), Mathf.Clamp01(_r.yMin), Mathf.Clamp01(_r.xMax), Mathf.Clamp01(_r.yMax));
        }

        private static Rect ToCropSpace(Rect _uv, Rect _crop)
        {
            float _w = Mathf.Max(0.0001f, _crop.width);
            float _h = Mathf.Max(0.0001f, _crop.height);
            return new Rect((_uv.x - _crop.x) / _w, (_uv.y - _crop.y) / _h, _uv.width / _w, _uv.height / _h);
        }

        private static Texture2D Crop(Texture2D _source, Rect _uv)
        {
            if (null == _source) return null;
            int _x = Mathf.Clamp(Mathf.FloorToInt(_uv.xMin * _source.width), 0, _source.width - 1);
            int _y = Mathf.Clamp(Mathf.FloorToInt(_uv.yMin * _source.height), 0, _source.height - 1);
            int _w = Mathf.Clamp(Mathf.CeilToInt(_uv.width * _source.width), 1, _source.width - _x);
            int _h = Mathf.Clamp(Mathf.CeilToInt(_uv.height * _source.height), 1, _source.height - _y);

            Texture2D _crop = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            _crop.hideFlags = HideFlags.HideAndDontSave;
            _crop.filterMode = FilterMode.Point;
            _crop.SetPixels(_source.GetPixels(_x, _y, _w, _h));
            _crop.Apply();
            return _crop;
        }
    }

    internal static class LocQAReviewRenderer
    {
        /// <summary>문구 하나를 여러 언어로 한 번에 그린다. (모든 언어를 같은 영역으로 잘라 나란히 비교)</summary>
        public static List<LocQAReviewCell> Render(LocQARenderRequest _request, List<Language> _langs, LocQASettings _settings, out string _error, out string _note)
        {
            using (LocQARenderSession _session = new LocQARenderSession(_settings))
            {
                return _session.Render(_request, _langs, true, true, out _error, out _note);
            }
        }
    }
}
