using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace LocalizationQA
{
    /// <summary>
    /// 번역 파일에서 지웠거나 이름을 바꾼 키를 가리키는 연결을 찾고, 새 키 후보를 고른다.
    ///
    /// 후보 점수 (같은 파일 안에서)
    ///  - 판정할 때의 문구와 지금 문구가 같은 언어마다 +10 (이름만 바뀐 키)
    ///  - 키 이름: 단계 꼬리(_t1, _T2 …)를 뗀 이름이 같으면 +6, 한쪽이 다른 쪽을 품으면 +4, 거의 같으면(편집 거리 25% 이하) +3
    ///  - 칸 이름에 후보 키의 첫 마디(name · desc 등)가 들어 있으면 +1
    /// 3점 이상만 후보로 낸다. 가장 높은 점수가 여럿이면(단계별로 나뉜 키) "모두로 바꾸기"를 함께 낸다.
    /// </summary>
    internal static class LocQAStaleKeys
    {
        private static readonly Regex TierSuffix = new Regex(@"_t\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal sealed class Candidate
        {
            public LocQAEntry entry;
            public int score;
        }

        public static List<Candidate> Find(string _entryId, string _slotPath, List<KeyValuePair<Language, LocQAReviewMark>> _marks, LocQAStringTable _table, ICollection<string> _exclude)
        {
            List<Candidate> _result = new List<Candidate>();
            if (string.IsNullOrEmpty(_entryId)) return _result;

            int _slash = _entryId.IndexOf('/');
            string _file = _slash > 0 ? _entryId.Substring(0, _slash) : string.Empty;
            string _oldKey = (_slash >= 0 ? _entryId.Substring(_slash + 1) : _entryId).ToLowerInvariant();
            string _oldCore = TierSuffix.Replace(_oldKey, string.Empty);
            string _slot = (_slotPath ?? string.Empty).ToLowerInvariant();

            for (int i = 0; i < _table.Entries.Count; i++)
            {
                LocQAEntry _entry = _table.Entries[i];
                if (_entry.file != _file) continue;
                if (null != _exclude && true == _exclude.Contains(_entry.id)) continue;

                int _score = 0;
                if (null != _marks)
                {
                    for (int m = 0; m < _marks.Count; m++)
                    {
                        if (false == string.IsNullOrEmpty(_marks[m].Value.hash) && _marks[m].Value.hash == LocQAReviewData.Hash(LocQALanguages.Resolve(_entry.data, _marks[m].Key))) _score += 10;
                    }
                }

                string _key = _entry.id.Substring(_entry.id.IndexOf('/') + 1).ToLowerInvariant();
                string _core = TierSuffix.Replace(_key, string.Empty);
                if (_core == _oldCore) _score += 6;
                else if ((_core.Length >= 4 && _oldCore.Contains(_core)) || (_oldCore.Length >= 4 && _core.Contains(_oldCore))) _score += 4;
                else if (Distance(_core, _oldCore) <= Mathf.Max(1, Mathf.Max(_core.Length, _oldCore.Length) / 4)) _score += 3;

                if (_score <= 0) continue;
                int _head = _key.IndexOf('_');
                if (_head >= 2 && true == _slot.Contains(_key.Substring(0, _head))) _score += 1;
                if (_score >= 3) _result.Add(new Candidate { entry = _entry, score = _score });
            }

            _result.Sort((a, b) => 0 != b.score - a.score ? b.score - a.score : string.CompareOrdinal(a.entry.id, b.entry.id));
            return _result;
        }

        /// <summary>가장 높은 점수를 받은 후보들 (단계별로 나뉜 키면 여럿)</summary>
        public static List<string> Best(List<Candidate> _candidates)
        {
            List<string> _ids = new List<string>();
            for (int i = 0; i < _candidates.Count && _candidates[i].score == _candidates[0].score; i++) _ids.Add(_candidates[i].entry.id);
            return _ids;
        }

        private static int Distance(string _a, string _b)
        {
            int[] _prev = new int[_b.Length + 1];
            int[] _cur = new int[_b.Length + 1];
            for (int j = 0; j <= _b.Length; j++) _prev[j] = j;
            for (int i = 1; i <= _a.Length; i++)
            {
                _cur[0] = i;
                for (int j = 1; j <= _b.Length; j++)
                {
                    int _cost = _a[i - 1] == _b[j - 1] ? 0 : 1;
                    _cur[j] = Mathf.Min(Mathf.Min(_cur[j - 1] + 1, _prev[j] + 1), _prev[j - 1] + _cost);
                }
                int[] _swap = _prev;
                _prev = _cur;
                _cur = _swap;
            }
            return _prev[_b.Length];
        }
    }

    /// <summary>없어진 키를 가리키는 연결 · 직접 지정을 새 키로 바꾸거나 지우는 창</summary>
    internal sealed class LocQAStaleKeysWindow : EditorWindow
    {
        private sealed class ItemRow
        {
            public LocQAReviewItem item;
            public string label;
            public int marks;
            public List<LocQAStaleKeys.Candidate> candidates;
        }

        private sealed class BindingRow
        {
            public LocQABinding binding;
            public string label;
            public List<LocQAStaleKeys.Candidate> candidates;
        }

        private LocQAStringTable table;
        private LocQABindingSet bindings;
        private readonly List<ItemRow> items = new List<ItemRow>();
        private readonly List<BindingRow> bindingRows = new List<BindingRow>();
        private int staleNotes;
        private int builtVersion = -1;
        private Vector2 scroll;

        [MenuItem("Tools/Localization/Localization QA/없어진 키 정리", false, 22)]
        public static void Open()
        {
            LocQAStaleKeysWindow _window = GetWindow<LocQAStaleKeysWindow>("없어진 키 정리");
            _window.minSize = new Vector2(720f, 360f);
            _window.table = null;
            _window.builtVersion = -1;
            _window.Show();
        }

        /// <summary>번역 파일에 없는 키를 가리키는 검수 연결 · 직접 지정 · 미사용/뺀 연결 기록 수</summary>
        public static int Count(LocQAStringTable _table)
        {
            LocQAReviewData _data = LocQAReviewData.Shared;
            int _count = _data.StaleItems(_table).Count + _data.StaleNotes(_table, false);
            List<LocQABinding> _bindings = LocQABindingSet.Load().All;
            for (int i = 0; i < _bindings.Count; i++)
            {
                if (false == string.IsNullOrEmpty(_bindings[i].entryId) && null == _table.Find(_bindings[i].entryId)) _count++;
            }
            return _count;
        }

        private void OnEnable()
        {
            LocQAReviewData.Changed += OnChanged;
            builtVersion = -1;
        }

        private void OnDisable()
        {
            LocQAReviewData.Changed -= OnChanged;
        }

        private void OnChanged()
        {
            builtVersion = -1;
            Repaint();
        }

        private void Build()
        {
            LocQAReviewData _data = LocQAReviewData.Shared;
            if (null != table && builtVersion == _data.Version) return;
            builtVersion = _data.Version;
            if (null == table) table = LocQAStringTable.Load();
            bindings = LocQABindingSet.Load();

            items.Clear();
            List<LocQAReviewItem> _stale = _data.StaleItems(table);
            for (int i = 0; i < _stale.Count; i++)
            {
                LocQAReviewItem _item = _stale[i];
                HashSet<string> _bound = new HashSet<string>(StringComparer.Ordinal);
                for (int k = 0; k < _data.Items.Count; k++)
                {
                    if (_data.Items[k].SlotKey == _item.SlotKey) _bound.Add(_data.Items[k].entryId);
                }
                List<KeyValuePair<Language, LocQAReviewMark>> _marks = _data.MarksOf(_item);
                items.Add(new ItemRow
                {
                    item = _item,
                    label = $"{PrefabName(_item.slotGuid)} / {_item.slotPath}",
                    marks = _marks.Count,
                    candidates = LocQAStaleKeys.Find(_item.entryId, _item.slotPath, _marks, table, _bound),
                });
            }
            items.Sort((a, b) => string.CompareOrdinal(a.item.entryId, b.item.entryId));

            bindingRows.Clear();
            List<LocQABinding> _all = bindings.All;
            for (int i = 0; i < _all.Count; i++)
            {
                LocQABinding _b = _all[i];
                if (string.IsNullOrEmpty(_b.entryId) || null != table.Find(_b.entryId)) continue;
                bindingRows.Add(new BindingRow
                {
                    binding = _b,
                    label = $"{PrefabName(_b.prefabGuid)} / {_b.objectPath}",
                    candidates = LocQAStaleKeys.Find(_b.entryId, _b.objectPath, null, table, null),
                });
            }

            staleNotes = _data.StaleNotes(table, false);
        }

        private void OnGUI()
        {
            Build();
            LocQAReviewData _data = LocQAReviewData.Shared;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label($"검수 연결 {items.Count}개 · 직접 지정 {bindingRows.Count}개 · 미사용/뺀 연결 기록 {staleNotes}개", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("다시 찾기", EditorStyles.toolbarButton))
                {
                    table = null;
                    builtVersion = -1;
                    GUIUtility.ExitGUI();
                }
            }

            if (0 == items.Count && 0 == bindingRows.Count && 0 == staleNotes)
            {
                EditorGUILayout.HelpBox("번역 파일에 없는 키를 가리키는 연결이 없습니다.", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < items.Count; i++)
            {
                ItemRow _row = items[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(_row.item.entryId, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (_row.marks > 0) GUILayout.Label($"판정 {_row.marks}개", EditorStyles.miniLabel, GUILayout.Width(60f));
                        if (GUILayout.Button("지우기", GUILayout.Width(60f)))
                        {
                            string _warn = _row.marks > 0 ? $"\n판정 {_row.marks}개도 함께 지워집니다." : string.Empty;
                            if (true == EditorUtility.DisplayDialog("없어진 키 정리", $"{_row.item.entryId}\n{_row.label}\n\n이 연결을 지울까요?{_warn}", "지우기", "취소"))
                            {
                                _data.Forget(_row.item);
                                _data.SaveNow();
                            }
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.LabelField(_row.label, EditorStyles.miniLabel);
                    DrawCandidates(_row.candidates, _ids =>
                    {
                        _data.Retarget(_row.item, _ids);
                        _data.SaveNow();
                    });
                }
            }

            for (int i = 0; i < bindingRows.Count; i++)
            {
                BindingRow _row = bindingRows[i];
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField("직접 지정 · " + _row.binding.entryId, EditorStyles.boldLabel);
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("지우기", GUILayout.Width(60f)))
                        {
                            bindings.Set(_row.binding.prefabGuid, _row.binding.objectPath, null, false);
                            bindings.Save();
                            builtVersion = -1;
                            GUIUtility.ExitGUI();
                        }
                    }
                    EditorGUILayout.LabelField(_row.label, EditorStyles.miniLabel);
                    // 직접 지정은 칸 하나에 키 하나라 한 개만 고른다.
                    DrawCandidates(_row.candidates, _ids =>
                    {
                        bindings.Set(_row.binding.prefabGuid, _row.binding.objectPath, _ids[0], false);
                        bindings.Save();
                        builtVersion = -1;
                    }, false);
                }
            }

            if (staleNotes > 0)
            {
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"미사용 표시 · 뺀 연결 기록 중 없는 키 {staleNotes}개 (판정 없음)");
                    if (GUILayout.Button("기록에서 지우기", GUILayout.Width(110f)))
                    {
                        _data.StaleNotes(table, true);
                        _data.SaveNow();
                        GUIUtility.ExitGUI();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawCandidates(List<LocQAStaleKeys.Candidate> _candidates, Action<List<string>> _apply, bool _allowMany = true)
        {
            if (0 == _candidates.Count)
            {
                EditorGUILayout.LabelField("비슷한 새 키 없음 — 키를 지운 것이면 '지우기'", EditorStyles.miniLabel);
                return;
            }

            List<string> _best = LocQAStaleKeys.Best(_candidates);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("새 키 후보", EditorStyles.miniLabel, GUILayout.Width(60f));
                if (true == _allowMany && _best.Count > 1 && GUILayout.Button($"가장 비슷한 {_best.Count}개 모두로", GUILayout.Width(150f)))
                {
                    _apply(_best);
                    GUIUtility.ExitGUI();
                }
                GUILayout.FlexibleSpace();
            }

            int _shown = Mathf.Min(8, _candidates.Count);
            for (int i = 0; i < _shown; i++)
            {
                LocQAStaleKeys.Candidate _c = _candidates[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(64f);
                    if (GUILayout.Button("→ " + _c.entry.id, EditorStyles.miniButtonLeft, GUILayout.Width(300f)))
                    {
                        _apply(new List<string> { _c.entry.id });
                        GUIUtility.ExitGUI();
                    }
                    GUILayout.Label(Preview(_c.entry), EditorStyles.miniLabel);
                }
            }
        }

        private static string Preview(LocQAEntry _entry)
        {
            string _text = LocQAStringTable.StripForGlyphCheck(LocQALanguages.Resolve(_entry.data, Language.KR)).Replace("\n", " ");
            return _text.Length > 60 ? _text.Substring(0, 60) + "…" : _text;
        }

        private static string PrefabName(string _guid)
        {
            string _path = AssetDatabase.GUIDToAssetPath(_guid);
            return string.IsNullOrEmpty(_path) || false == File.Exists(_path) ? "(지워진 프리팹)" : Path.GetFileNameWithoutExtension(_path);
        }
    }
}
