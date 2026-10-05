using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 이 프로젝트에는 전역 네임스페이스에 자체 SceneManager 클래스가 있어서 이름이 가려진다.
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace LocalizationQA
{
    // //키 수동 지정 (프리팹 텍스트 ↔ 로컬라이징 키)
    [Serializable]
    internal sealed class LocQABinding
    {
        public string prefabGuid;
        public string objectPath;
        public string entryId;
        public bool ignore;
    }

    [Serializable]
    internal sealed class LocQABindingFile
    {
        public List<LocQABinding> bindings = new List<LocQABinding>();
    }

    /// <summary>
    /// 프리팹 텍스트에 어떤 키가 들어가는지 사람이 지정한 목록.
    /// 텍스트는 전부 코드에서 채워지므로 프리팹만 봐서는 키를 알 수 없다. 기본 문구로 자동 추론이
    /// 안 되는 텍스트만 여기에 적는다. 팀이 공유하도록 저장소에 커밋되는 JSON으로 둔다.
    /// </summary>
    internal sealed class LocQABindingSet
    {
        public const string FILE_PATH = "Assets/Scripts/Editor/Localization/LocalizationQA/LocalizationQABindings.json";

        private LocQABindingFile data = new LocQABindingFile();
        private readonly Dictionary<string, LocQABinding> map = new Dictionary<string, LocQABinding>(StringComparer.Ordinal);

        public static LocQABindingSet Load()
        {
            LocQABindingSet _set = new LocQABindingSet();
            if (true == File.Exists(FILE_PATH))
            {
                try
                {
                    LocQABindingFile _file = JsonUtility.FromJson<LocQABindingFile>(File.ReadAllText(FILE_PATH, Encoding.UTF8));
                    if (null != _file && null != _file.bindings) _set.data = _file;
                }
                catch (Exception _e)
                {
                    Debug.LogWarning($"[LocalizationQA] {FILE_PATH} 읽기 실패: {_e.Message}");
                }
            }

            for (int i = 0; i < _set.data.bindings.Count; i++)
            {
                LocQABinding _b = _set.data.bindings[i];
                _set.map[Key(_b.prefabGuid, _b.objectPath)] = _b;
            }
            return _set;
        }

        public LocQABinding Get(string _guid, string _path)
        {
            map.TryGetValue(Key(_guid, _path), out LocQABinding _b);
            return _b;
        }

        /// <summary>entryId가 비어 있고 ignore도 아니면 지정을 지운다.</summary>
        public void Set(string _guid, string _path, string _entryId, bool _ignore)
        {
            string _key = Key(_guid, _path);
            if (true == map.TryGetValue(_key, out LocQABinding _existing))
            {
                data.bindings.Remove(_existing);
                map.Remove(_key);
            }

            if (true == string.IsNullOrEmpty(_entryId) && false == _ignore) return;

            LocQABinding _b = new LocQABinding { prefabGuid = _guid, objectPath = _path, entryId = _entryId ?? string.Empty, ignore = _ignore };
            data.bindings.Add(_b);
            map[_key] = _b;
        }

        public void Save()
        {
            // 정렬해 두어야 여러 사람이 고쳐도 diff가 작다.
            data.bindings.Sort((a, b) =>
            {
                int _c = string.CompareOrdinal(a.prefabGuid, b.prefabGuid);
                return 0 != _c ? _c : string.CompareOrdinal(a.objectPath, b.objectPath);
            });

            Directory.CreateDirectory(Path.GetDirectoryName(FILE_PATH));
            File.WriteAllText(FILE_PATH, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(FILE_PATH);
        }

        private static string Key(string _guid, string _path) => _guid + "|" + _path;
    }

    // //검사·미리보기용 임시 무대
    /// <summary>
    /// 프리팹을 띄울 캔버스가 있는 프리뷰 씬. 열린 씬을 건드리지 않고, 쓰고 나면 통째로 버린다.
    /// 캔버스 크기는 고른 해상도에서 게임 UI 캔버스가 실제로 갖는 크기다. (LocQAResolution 참고)
    /// </summary>
    internal sealed class LocQAStage : IDisposable
    {
        public readonly float width;
        public readonly float height;
        private readonly int renderScale;

        private UnityEngine.SceneManagement.Scene scene;
        private readonly Camera camera;
        private RenderTexture renderTexture;

        public readonly RectTransform canvasRoot;

        public LocQAStage(bool _withCamera, Vector2 _canvasSize, int _renderScale = 2)
        {
            renderScale = Mathf.Max(1, _renderScale);
            width = Mathf.Max(1f, _canvasSize.x);
            height = Mathf.Max(1f, _canvasSize.y);
            scene = EditorSceneManager.NewPreviewScene();

            GameObject _canvasGo = new GameObject("LocalizationQA Canvas", typeof(RectTransform));
            UnitySceneManager.MoveGameObjectToScene(_canvasGo, scene);
            Canvas _canvas = _canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.referencePixelsPerUnit = 32f;   // 게임 CanvasScaler의 Reference Pixels Per Unit

            canvasRoot = (RectTransform)_canvasGo.transform;
            canvasRoot.sizeDelta = new Vector2(width, height);
            canvasRoot.position = Vector3.zero;

            if (false == _withCamera) return;

            GameObject _camGo = new GameObject("LocalizationQA Camera");
            UnitySceneManager.MoveGameObjectToScene(_camGo, scene);
            camera = _camGo.AddComponent<Camera>();
            camera.enabled = false;
            camera.scene = scene;
            camera.orthographic = true;
            camera.orthographicSize = height * 0.5f;
            camera.transform.position = new Vector3(0f, 0f, -100f);
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1000f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.19f, 1f);
            _canvas.worldCamera = camera;
        }

        /// <summary>
        /// 프리팹 연결 없는 복사본으로 띄운다. 게임 코드(등장 연출 등)가 부모를 바꾸는 등 실제 게임처럼 자유롭게
        /// 움직일 수 있어야 하기 때문이다. (프리팹 인스턴스는 계층 변경이 막혀 있다)
        /// 복사본의 계층 경로는 원본 프리팹과 같으므로, 원본이 필요하면 경로로 에셋에서 찾는다.
        /// 등장 연출을 끝내는 훅(LocQALayoutHooks.RunSetup)은 오브젝트를 옮길 수 있으므로, 호출하는 쪽이
        /// 칸 경로를 먼저 기록한 뒤에 부른다.
        /// </summary>
        public GameObject Spawn(GameObject _prefab)
        {
            // 부모를 지정해 바로 프리뷰 씬 안에 만든다. (열린 씬에 잠깐이라도 생기면 그 씬이 수정됨으로 표시된다)
            GameObject _instance = UnityEngine.Object.Instantiate(_prefab, canvasRoot, false);
            _instance.name = _prefab.name;
            return _instance;
        }

        public Texture2D Render()
        {
            if (null == camera) return null;

            int _w = Mathf.CeilToInt(width * renderScale);
            int _h = Mathf.CeilToInt(height * renderScale);
            if (null == renderTexture)
            {
                renderTexture = new RenderTexture(_w, _h, 24, RenderTextureFormat.ARGB32);
                renderTexture.filterMode = FilterMode.Point;
            }

            Canvas.ForceUpdateCanvases();
            camera.targetTexture = renderTexture;
            camera.Render();

            RenderTexture _prev = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Texture2D _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            _tex.hideFlags = HideFlags.HideAndDontSave;
            _tex.filterMode = FilterMode.Point;
            _tex.ReadPixels(new Rect(0, 0, _w, _h), 0, 0);
            _tex.Apply();
            RenderTexture.active = _prev;
            camera.targetTexture = null;
            return _tex;
        }

        /// <summary>월드 좌표 네 점 → 렌더 이미지 안의 0~1 사각형 (좌하단 원점)</summary>
        public Rect WorldToUv(Vector3[] _world)
        {
            float _xMin = float.MaxValue, _yMin = float.MaxValue, _xMax = float.MinValue, _yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 _local = canvasRoot.InverseTransformPoint(_world[i]);
                float _u = (_local.x + width * 0.5f) / width;
                float _v = (_local.y + height * 0.5f) / height;
                _xMin = Mathf.Min(_xMin, _u);
                _yMin = Mathf.Min(_yMin, _v);
                _xMax = Mathf.Max(_xMax, _u);
                _yMax = Mathf.Max(_yMax, _v);
            }
            return Rect.MinMaxRect(_xMin, _yMin, _xMax, _yMax);
        }

        public void Dispose()
        {
            if (null != renderTexture)
            {
                renderTexture.Release();
                UnityEngine.Object.DestroyImmediate(renderTexture);
                renderTexture = null;
            }
            if (true == scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // //프리팹 정적 검사
    internal sealed class LocQATextRecord
    {
        public TMP_Text text;
        public string path;
        public LocQAEntry entry;
        public string bindingNote;
        public bool ignored;
        public TMP_FontAsset originalFont;
        public Material originalMaterial;
        public GameObject[] inactiveChain;
        public string chainKey;
    }

    /// <summary>
    /// UI 프리팹을 하나씩 640×360 캔버스에 띄우고, 텍스트마다 모든 언어 문구를 넣어 가며 잰다.
    ///
    /// 텍스트에 들어갈 키는 (1) 사람이 지정한 목록 → (2) 프리팹에 적힌 기본 문구와 같은 항목 순으로 정한다.
    /// 둘 다 없으면 "미연결"로 모아 창에서 지정할 수 있게 한다.
    ///
    /// 비활성 패널 안의 텍스트도 재야 하므로, 텍스트마다 꺼져 있는 조상만 잠깐 켜고 레이아웃을 다시 계산한다.
    /// 모든 오브젝트를 한꺼번에 켜면 서로 배타적인 형제(탭 내용 등)가 레이아웃 그룹 공간을 나눠 가져
    /// 실제와 다른 크기가 나오기 때문이다.
    /// </summary>
    internal static class LocQAPrefabScanner
    {
        private const string LOG_TAG = "[LocalizationQA]";

        public static void ScanAll(LocQASettings _settings)
        {
            string _folder = string.IsNullOrEmpty(_settings.prefabFolder) ? "Assets/Prefabs/UI" : _settings.prefabFolder.TrimEnd('/');
            if (false == AssetDatabase.IsValidFolder(_folder))
            {
                EditorUtility.DisplayDialog("Localization QA", $"폴더를 찾을 수 없습니다: {_folder}", "확인");
                return;
            }

            string[] _guids = AssetDatabase.FindAssets("t:Prefab", new[] { _folder });
            List<string> _paths = new List<string>(_guids.Length);
            for (int i = 0; i < _guids.Length; i++) _paths.Add(AssetDatabase.GUIDToAssetPath(_guids[i]));
            _paths.Sort(StringComparer.Ordinal);

            Scan(_settings, _paths, true);
        }

        /// <summary>지정한 프리팹들만 다시 검사한다. (키를 지정한 직후 해당 프리팹만 갱신할 때)</summary>
        public static void Scan(LocQASettings _settings, List<string> _prefabPaths, bool _isFullScan)
        {
            LocQAStore _store = LocQAStore.instance;
            HashSet<string> _targets = new HashSet<string>(_prefabPaths, StringComparer.Ordinal);

            _store.issues.RemoveAll(i => LocQASource.Prefab == i.source && (_isFullScan || _targets.Contains(i.location)));
            _store.unbound.RemoveAll(u => _isFullScan || _targets.Contains(u.prefabPath));

            LocQAStringTable _table = LocQAStringTable.Load();
            LocQABindingSet _bindings = LocQABindingSet.Load();

            // 원문과 비교해야 "로컬라이징 때문에 생긴 문제"를 가려낼 수 있으므로 한국어는 항상 잰다.
            List<Language> _langs = _settings.SelectedLanguages();
            _langs.Remove(LocQALanguages.SOURCE);
            _langs.Insert(0, LocQALanguages.SOURCE);

            int _prefabCount = 0, _textCount = 0, _boundCount = 0, _unboundCount = 0, _ignoredCount = 0, _issueCount = 0;
            bool _canceled = false;

            using (LocQAFontResolver _fonts = LocQAFontResolver.Load())
            using (LocQAStage _stage = new LocQAStage(false, _settings.CanvasSize))
            {
                try
                {
                    for (int p = 0; p < _prefabPaths.Count; p++)
                    {
                        string _path = _prefabPaths[p];
                        if (true == EditorUtility.DisplayCancelableProgressBar("Localization QA · 프리팹 검사",
                            $"{Path.GetFileNameWithoutExtension(_path)} ({p + 1}/{_prefabPaths.Count})", (float)p / Mathf.Max(1, _prefabPaths.Count)))
                        {
                            _canceled = true;
                            break;
                        }

                        GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                        if (null == _prefab || null == _prefab.GetComponentInChildren<TMP_Text>(true)) continue;

                        GameObject _instance = null;
                        try
                        {
                            _instance = _stage.Spawn(_prefab);
                            string _guid = AssetDatabase.AssetPathToGUID(_path);
                            List<LocQATextRecord> _records = BuildRecords(_instance, _guid, _prefab.name, _table, _bindings);
                            LocQALayoutHooks.RunSetup(_instance);

                            _prefabCount++;
                            _textCount += _records.Count;

                            for (int r = 0; r < _records.Count; r++)
                            {
                                LocQATextRecord _rec = _records[r];
                                if (null != _rec.entry)
                                {
                                    _boundCount++;
                                    _store.Cover(_rec.entry.id);
                                    continue;
                                }

                                if (true == _rec.ignored) _ignoredCount++;
                                else _unboundCount++;

                                _store.unbound.Add(new LocQAUnbound
                                {
                                    prefabPath = _path,
                                    prefabGuid = _guid,
                                    objectPath = _rec.path,
                                    text = _rec.text.text,
                                    ignored = _rec.ignored
                                });
                            }

                            _issueCount += MeasurePrefab(_instance, _path, _records, _langs, _settings, _fonts, _stage, _store);
                        }
                        catch (Exception _e)
                        {
                            Debug.LogError($"{LOG_TAG} {_path} 검사 중 오류: {_e}");
                        }
                        finally
                        {
                            if (null != _instance) UnityEngine.Object.DestroyImmediate(_instance);
                        }
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }
            }

            if (true == _isFullScan)
            {
                _store.prefabSummary = $"{DateTime.Now:HH:mm:ss} · {_settings.Resolution.label} · 프리팹 {_prefabCount}개 · 텍스트 {_textCount}개 " +
                    $"(키 연결 {_boundCount} / 미연결 {_unboundCount} / 제외 {_ignoredCount}) · 문제 {_issueCount}건" +
                    (_canceled ? " · 중간에 취소됨" : string.Empty);
            }
            _store.NotifyChanged();
        }

        private static int MeasurePrefab(GameObject _instance, string _prefabPath, List<LocQATextRecord> _records, List<Language> _langs,
            LocQASettings _settings, LocQAFontResolver _fonts, LocQAStage _stage, LocQAStore _store)
        {
            // 꺼져 있는 조상 묶음이 같은 텍스트끼리 한 번에 잰다. (레이아웃 재계산 횟수를 줄인다)
            Dictionary<string, List<LocQATextRecord>> _groups = new Dictionary<string, List<LocQATextRecord>>(StringComparer.Ordinal);
            for (int i = 0; i < _records.Count; i++)
            {
                if (null == _records[i].entry) continue;
                if (false == _groups.TryGetValue(_records[i].chainKey, out List<LocQATextRecord> _list))
                {
                    _list = new List<LocQATextRecord>(4);
                    _groups.Add(_records[i].chainKey, _list);
                }
                _list.Add(_records[i]);
            }
            if (0 == _groups.Count) return 0;

            int _issues = 0;
            for (int l = 0; l < _langs.Count; l++)
            {
                Language _lang = _langs[l];
                ApplyLanguage(_records, _lang, _fonts, _settings);

                foreach (KeyValuePair<string, List<LocQATextRecord>> _group in _groups)
                {
                    GameObject[] _chain = _group.Value[0].inactiveChain;
                    SetActive(_chain, true);
                    try
                    {
                        RebuildLayout(_instance);

                        for (int i = 0; i < _group.Value.Count; i++)
                        {
                            LocQATextRecord _rec = _group.Value[i];
                            LocQAMeasurement _m = LocQATextAnalyzer.Measure(_rec.text, _settings, true, false);

                            for (int f = 0; f < _m.findings.Count; f++)
                            {
                                _store.issues.Add(new LocQAIssue
                                {
                                    source = LocQASource.Prefab,
                                    language = _lang,
                                    kind = _m.findings[f].Key,
                                    location = _prefabPath,
                                    objectPath = _rec.path,
                                    text = _rec.text.text,
                                    key = _rec.entry.id,
                                    detail = _m.findings[f].Value + (string.IsNullOrEmpty(_rec.bindingNote) ? string.Empty : " · " + _rec.bindingNote),
                                    lineCount = _m.lineCount,
                                    hasUv = true,
                                    boxUv = _stage.WorldToUv(_m.boxWorld),
                                    glyphUv = _stage.WorldToUv(_m.glyphWorld)
                                });
                                _issues++;
                            }
                        }
                    }
                    finally
                    {
                        SetActive(_chain, false);
                    }
                }
            }
            return _issues;
        }

        public static List<LocQATextRecord> BuildRecords(GameObject _instance, string _prefabGuid, string _prefabName, LocQAStringTable _table, LocQABindingSet _bindings)
        {
            TMP_Text[] _texts = _instance.GetComponentsInChildren<TMP_Text>(true);
            List<LocQATextRecord> _records = new List<LocQATextRecord>(_texts.Length);
            List<GameObject> _chain = new List<GameObject>(4);
            StringBuilder _chainKey = new StringBuilder(64);

            for (int i = 0; i < _texts.Length; i++)
            {
                TMP_Text _text = _texts[i];
                LocQATextRecord _rec = new LocQATextRecord
                {
                    text = _text,
                    path = LocQAPaths.GetPath(_text.transform, _instance.transform),
                    originalFont = _text.font,
                    originalMaterial = _text.fontSharedMaterial
                };

                LocQABinding _binding = _bindings.Get(_prefabGuid, _rec.path);
                if (null != _binding)
                {
                    if (true == _binding.ignore)
                    {
                        _rec.ignored = true;
                    }
                    else
                    {
                        _rec.entry = _table.Find(_binding.entryId);
                        _rec.bindingNote = null != _rec.entry ? string.Empty : $"지정된 키 '{_binding.entryId}'가 없어 자동 추론";
                    }
                }

                if (null == _rec.entry && false == _rec.ignored)
                {
                    List<LocQAEntry> _candidates = _table.MatchAnyLanguage(_text.text);
                    if (_candidates.Count > 0)
                    {
                        _rec.entry = PickCandidate(_candidates, _text.name, _prefabName);
                        string _note = _candidates.Count > 1 ? $"키 자동 추론(후보 {_candidates.Count}개)" : "키 자동 추론";
                        _rec.bindingNote = string.IsNullOrEmpty(_rec.bindingNote) ? _note : _rec.bindingNote;
                    }
                }

                _chain.Clear();
                _chainKey.Clear();
                for (Transform _t = _text.transform; null != _t; _t = _t.parent)
                {
                    if (false == _t.gameObject.activeSelf)
                    {
                        _chain.Add(_t.gameObject);
                        _chainKey.Append(_t.gameObject.GetHashCode()).Append(',');
                    }
                    if (_t == _instance.transform) break;
                }
                _rec.inactiveChain = _chain.ToArray();
                _rec.chainKey = _chainKey.ToString();

                _records.Add(_rec);
            }
            return _records;
        }

        /// <summary>같은 문구를 가진 항목이 여럿이면 오브젝트·프리팹 이름과 가장 닮은 키를 고른다.</summary>
        private static LocQAEntry PickCandidate(List<LocQAEntry> _candidates, string _objectName, string _prefabName)
        {
            if (1 == _candidates.Count) return _candidates[0];

            string _obj = Simplify(_objectName);
            string _prefab = Simplify(_prefabName);
            LocQAEntry _best = _candidates[0];
            int _bestScore = int.MinValue;

            for (int i = 0; i < _candidates.Count; i++)
            {
                LocQAEntry _c = _candidates[i];
                int _score = 0;
                string _key = Simplify(_c.data.key);
                if (_obj.Length > 1 && _key.Length > 1 && (_obj.Contains(_key) || _key.Contains(_obj))) _score += 2;

                string _file = Simplify(_c.file).Replace("ui", string.Empty).Replace("hud", string.Empty);
                if (_file.Length > 2 && _prefab.Contains(_file)) _score += 1;

                if (_score > _bestScore)
                {
                    _bestScore = _score;
                    _best = _c;
                }
            }
            return _best;
        }

        private static string Simplify(string _s)
        {
            if (string.IsNullOrEmpty(_s)) return string.Empty;
            StringBuilder _sb = new StringBuilder(_s.Length);
            string _lower = _s.ToLowerInvariant().Replace("tmp", string.Empty).Replace("text", string.Empty).Replace("txt", string.Empty);
            for (int i = 0; i < _lower.Length; i++)
            {
                if (true == char.IsLetterOrDigit(_lower[i])) _sb.Append(_lower[i]);
            }
            return _sb.ToString();
        }

        /// <summary>언어에 맞는 폰트(FontLocalizer와 같은 규칙)와 문구를 넣는다. 키가 없는 텍스트도 폰트는 바꾼다.</summary>
        public static void ApplyLanguage(List<LocQATextRecord> _records, Language _lang, LocQAFontResolver _fonts, LocQASettings _settings)
        {
            for (int i = 0; i < _records.Count; i++)
            {
                LocQATextRecord _rec = _records[i];
                TMP_FontAsset _target = _fonts.Resolve(_rec.originalFont, _rec.originalMaterial, _lang);
                if (_rec.text.font != _target)
                {
                    _rec.text.font = _target;
                    Material _mat = _fonts.ResolveMaterial(_rec.originalMaterial, _target, _rec.originalFont);
                    if (null != _mat) _rec.text.fontSharedMaterial = _mat;
                }

                if (null != _rec.entry)
                {
                    _rec.text.text = LocQAStringTable.FillFormat(LocQALanguages.Resolve(_rec.entry.data, _lang), _settings.formatSample);
                }
            }
        }

        private static readonly List<ILayoutController> controllerBuffer = new List<ILayoutController>(32);
        private static readonly List<KeyValuePair<int, RectTransform>> layoutRootBuffer = new List<KeyValuePair<int, RectTransform>>(32);

        /// <summary>
        /// 문구를 바꾼 뒤 레이아웃(LayoutGroup·ContentSizeFitter)을 다시 계산한다.
        ///
        /// 루트에 대고 ForceRebuildLayoutImmediate를 한 번 부르는 것으로는 부족하다. 레이아웃 컴포넌트가 없는
        /// 오브젝트를 만나면 그 아래 하위 트리를 통째로 건너뛰기 때문이다. (버튼 안의 ContentSizeFitter가
        /// 계산되지 않아 글자 칸 폭이 0이 되고, 한 글자씩 줄바꿈된 것으로 잘못 잡힌다)
        /// 그래서 캔버스 갱신 큐를 먼저 비우고, 레이아웃 컴포넌트가 붙은 곳을 깊은 것부터 직접 재계산한다.
        /// 중첩된 ContentSizeFitter는 한 번으로 수렴하지 않으므로 두 번 돈다.
        /// </summary>
        internal static void RebuildLayout(GameObject _instance)
        {
            Canvas.ForceUpdateCanvases();

            _instance.GetComponentsInChildren(false, controllerBuffer);
            layoutRootBuffer.Clear();
            for (int i = 0; i < controllerBuffer.Count; i++)
            {
                RectTransform _rt = (controllerBuffer[i] as Component)?.transform as RectTransform;
                if (null == _rt) continue;

                bool _exists = false;
                for (int j = 0; j < layoutRootBuffer.Count && false == _exists; j++) _exists = layoutRootBuffer[j].Value == _rt;
                if (true == _exists) continue;

                int _depth = 0;
                for (Transform _t = _rt; null != _t; _t = _t.parent) _depth++;
                layoutRootBuffer.Add(new KeyValuePair<int, RectTransform>(_depth, _rt));
            }
            controllerBuffer.Clear();
            layoutRootBuffer.Sort((a, b) => b.Key.CompareTo(a.Key));

            for (int _pass = 0; _pass < 2; _pass++)
            {
                for (int i = 0; i < layoutRootBuffer.Count; i++)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRootBuffer[i].Value);
                }
            }

            // 문구 길이로 크기를 직접 맞추는 UI는 게임 코드의 그 메서드를 그대로 불러 맞춘 뒤 한 번 더 계산한다.
            if (true == LocQALayoutHooks.Run(_instance))
            {
                for (int i = 0; i < layoutRootBuffer.Count; i++)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRootBuffer[i].Value);
                }
            }
            layoutRootBuffer.Clear();

            Canvas.ForceUpdateCanvases();
        }

        /// <summary>
        /// 칸이 화면에 보이도록 경로를 연다. 꺼진 조상은 켜고, 팝업이 나타나는 연출의 시작 상태
        /// (CanvasGroup 투명도 0, 크기 0)는 연출이 끝난 상태로 맞춘다. 첫 실행 팝업처럼 서서히 나타나는 UI용이다.
        /// </summary>
        internal static void RevealChain(Transform _target, Transform _root)
        {
            for (Transform _t = _target; null != _t; _t = _t.parent)
            {
                if (false == _t.gameObject.activeSelf) _t.gameObject.SetActive(true);
                if (true == _t.TryGetComponent(out CanvasGroup _group) && _group.alpha < 0.99f) _group.alpha = 1f;
                if (_t.localScale.sqrMagnitude < 0.0001f) _t.localScale = Vector3.one;
                if (_t == _root) break;
            }
        }

        private static void SetActive(GameObject[] _chain, bool _active)
        {
            for (int i = 0; i < _chain.Length; i++)
            {
                if (null != _chain[i]) _chain[i].SetActive(_active);
            }
        }

        // //미리보기
        /// <summary>프리팹의 해당 텍스트를 그 언어로 띄운 화면을 렌더링한다. 검사와 똑같은 과정으로 배치한다.</summary>
        public static Texture2D RenderPreview(string _prefabPath, string _objectPath, Language _lang, LocQASettings _settings,
            out Rect _boxUv, out Rect _glyphUv, out string _error)
        {
            _boxUv = default;
            _glyphUv = default;
            _error = null;

            GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (null == _prefab)
            {
                _error = "프리팹을 찾을 수 없습니다.";
                return null;
            }

            LocQAStringTable _table = LocQAStringTable.Load();
            LocQABindingSet _bindings = LocQABindingSet.Load();

            using (LocQAFontResolver _fonts = LocQAFontResolver.Load())
            using (LocQAStage _stage = new LocQAStage(true, _settings.CanvasSize))
            {
                GameObject _instance = _stage.Spawn(_prefab);
                List<LocQATextRecord> _records = BuildRecords(_instance, AssetDatabase.AssetPathToGUID(_prefabPath), _prefab.name, _table, _bindings);
                ApplyLanguage(_records, _lang, _fonts, _settings);

                Transform _target = LocQAPaths.Find(_instance.transform, _objectPath);
                LocQALayoutHooks.RunSetup(_instance);
                if (null == _target)
                {
                    _error = "프리팹에서 해당 오브젝트를 찾지 못했습니다. (프리팹 구조가 바뀌었을 수 있습니다)";
                    return _stage.Render();
                }

                RevealChain(_target, _instance.transform);
                RebuildLayout(_instance);

                TMP_Text _text = _target.GetComponent<TMP_Text>();
                if (null != _text)
                {
                    LocQAMeasurement _m = LocQATextAnalyzer.Measure(_text, _settings, true, false);
                    _boxUv = _stage.WorldToUv(_m.boxWorld);
                    _glyphUv = true == _m.hasGlyphs ? _stage.WorldToUv(_m.glyphWorld) : _boxUv;
                }
                return _stage.Render();
            }
        }
    }

    // //문구 전수 검사 (UI와 무관하게 모든 키 × 모든 언어)
    internal static class LocQAStringScanner
    {
        public static void Scan(LocQASettings _settings)
        {
            LocQAStore _store = LocQAStore.instance;
            _store.issues.RemoveAll(i => LocQASource.String == i.source);

            LocQAStringTable _table = LocQAStringTable.Load();
            List<Language> _langs = _settings.SelectedLanguages();
            TMP_FontAsset _uiFont = LoadUiFont(_settings);
            List<string> _missing = new List<string>(8);

            int _empty = 0, _fallback = 0, _glyph = 0;

            using (LocQAFontResolver _fonts = LocQAFontResolver.Load())
            {
                for (int i = 0; i < _table.Entries.Count; i++)
                {
                    LocQAEntry _entry = _table.Entries[i];
                    string _objectPath = string.IsNullOrEmpty(_entry.data.key) ? "#" + _entry.data.id : _entry.data.key;

                    for (int l = 0; l < _langs.Count; l++)
                    {
                        Language _lang = _langs[l];
                        string _text = LocQALanguages.Resolve(_entry.data, _lang);

                        if (true == string.IsNullOrWhiteSpace(_text))
                        {
                            Add(_store, _entry, _objectPath, _lang, LocQAKind.EmptyText, string.Empty,
                                LocQALanguages.SOURCE == _lang ? "원문(kr) 열이 비어 있습니다." : "번역도, 폴백할 영어(en)도 비어 있어 화면에 아무것도 안 나옵니다.");
                            _empty++;
                            continue;
                        }

                        if (true == LocQALanguages.IsFallback(_entry.data, _lang))
                        {
                            string _shownAs = (Language.ES_LATAM == _lang && false == string.IsNullOrEmpty(_entry.data.es)) ? "스페인어(es)" : "영어(en)";
                            Add(_store, _entry, _objectPath, _lang, LocQAKind.Untranslated, _text, $"번역이 없어 {_shownAs} 문구로 표시됩니다.");
                            _fallback++;
                        }

                        if (true == _settings.checkGlyph && null != _uiFont)
                        {
                            TMP_FontAsset _font = _fonts.Resolve(_uiFont, null, _lang);
                            _missing.Clear();
                            LocQAFontResolver.CollectMissing(_font, LocQAStringTable.StripForGlyphCheck(_text), _missing);
                            if (_missing.Count > 0)
                            {
                                Add(_store, _entry, _objectPath, _lang, LocQAKind.MissingGlyph, _text,
                                    $"'{_font.name}'(폴백 포함)에 없는 글자: {string.Join(" ", _missing)} — Tools/Localization/Generate Character Sets and Bake Atlases 필요");
                                _glyph++;
                            }
                        }
                    }
                }
            }

            _store.stringSummary = $"{DateTime.Now:HH:mm:ss} · 문구 {_table.Entries.Count}개 × {_langs.Count}개 언어 · 빈 문구 {_empty} / 글리프 없음 {_glyph} / 번역 없음 {_fallback}";
            _store.NotifyChanged();
        }

        public static TMP_FontAsset LoadUiFont(LocQASettings _settings)
        {
            if (false == string.IsNullOrEmpty(_settings.defaultUiFontGuid))
            {
                TMP_FontAsset _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(_settings.defaultUiFontGuid));
                if (null != _font) return _font;
            }
            return LocQAFontResolver.FindDefaultUiFont();
        }

        private static void Add(LocQAStore _store, LocQAEntry _entry, string _objectPath, Language _lang, LocQAKind _kind, string _text, string _detail)
        {
            _store.issues.Add(new LocQAIssue
            {
                source = LocQASource.String,
                language = _lang,
                kind = _kind,
                location = _entry.file + ".json",
                objectPath = _objectPath,
                text = _text,
                key = _entry.id,
                detail = _detail
            });
        }
    }
}
