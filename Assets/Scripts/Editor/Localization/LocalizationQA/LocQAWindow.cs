using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LocalizationQA
{
    /// <summary>
    /// 모든 UI·모든 문구의 로컬라이징 상태(칸 넘침, 줄바꿈, 잘림, 글리프 누락)를 한 곳에서 검사하는 창.
    ///
    ///  · 플레이 검사 : 게임에 띄운 화면을 언어별로 순회하며 실측하고 스크린샷에 문제 위치를 표시한다. (가장 정확)
    ///  · 프리팹 검사 : 플레이 없이 UI 프리팹 전부를 모든 언어로 띄워 잰다. (전체를 빠르게 훑기)
    ///  · 문구 검사   : UI와 무관하게 모든 키 × 모든 언어의 빈 문구·번역 누락·글리프 누락을 본다.
    /// 결과는 하나의 목록에 모이고, 고르면 해당 언어 화면을 미리보기로 바로 보여준다.
    /// </summary>
    internal sealed class LocQAWindow : EditorWindow
    {
        private const float ROW_HEIGHT = 20f;
        private const float LANG_BUTTON_WIDTH = 92f;

        private enum ETab
        {
            Play,
            Prefab,
            Strings,
            Settings
        }

        private static readonly GUIContent[] TabLabels =
        {
            new GUIContent("플레이 검사", "게임 화면을 언어별로 실측 (가장 정확)"),
            new GUIContent("프리팹 검사", "플레이 없이 모든 UI 프리팹을 모든 언어로 검사"),
            new GUIContent("문구 검사", "모든 키 × 모든 언어: 빈 문구·번역 누락·글리프 누락·확인 범위"),
            new GUIContent("설정")
        };

        [MenuItem("Tools/Localization/Localization QA/자동 검사 (넘침·잘림·글리프)", false, 20)]
        public static void Open()
        {
            LocQAWindow _window = GetWindow<LocQAWindow>("Localization QA", true, typeof(LocQAReviewWindow));
            _window.titleContent = new GUIContent("Localization QA", EditorGUIUtility.IconContent("console.warnicon.sml").image);
            _window.minSize = new Vector2(820f, 560f);
            _window.Show();
        }

        // //창 상태 (도메인 리로드에도 유지)
        [SerializeField] private ETab tab = ETab.Play;
        [SerializeField] private int sourceFilter = -1;
        [SerializeField] private int languageFilter = -1;
        [SerializeField] private int kindMask = -1;
        [SerializeField] private bool kindMaskInitialized;
        [SerializeField] private bool hideSourceIssues = true;
        [SerializeField] private string search = string.Empty;
        [SerializeField] private bool showIgnoredUnbound;
        [SerializeField] private bool showUnbound = true;
        [SerializeField] private bool showUncovered;
        [SerializeField] private string uncoveredSearch = string.Empty;
        [SerializeField] private float detailHeight = 260f;
        [SerializeField] private Vector2 listScroll;
        [SerializeField] private Vector2 panelScroll;
        [SerializeField] private Vector2 detailTextScroll;

        private LocQASettings settings;
        private readonly List<LocQAIssue> filtered = new List<LocQAIssue>(1024);
        private readonly int[] languageCounts = new int[32];
        private readonly int[] languageErrors = new int[32];
        private int totalCount;
        [NonSerialized] private string filterSignature;
        [NonSerialized] private LocQAIssue selected;
        [NonSerialized] private bool listHasFocus;
        [NonSerialized] private bool resizingDetail;

        // 미리보기
        [NonSerialized] private Language previewLanguage;
        [NonSerialized] private LocQAIssue previewIssue;
        [NonSerialized] private Language previewIssueLanguage;
        [NonSerialized] private Texture2D previewTexture;
        [NonSerialized] private bool previewOwned;
        [NonSerialized] private bool previewHasRect;
        [NonSerialized] private Rect previewBox;
        [NonSerialized] private Rect previewGlyph;
        [NonSerialized] private string previewMessage;
        [NonSerialized] private bool previewPending;
        private readonly Dictionary<string, Texture2D> captureCache = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        // 문구 목록 (키 지정 메뉴·미확인 문구 목록용)
        [NonSerialized] private LocQAStringTable tableCache;
        [NonSerialized] private double lastRepaint;

        private GUIStyle rowStyle;
        private GUIStyle rowSelectedStyle;
        private GUIStyle wrapStyle;
        private GUIStyle headerStyle;
        private GUIStyle titleStyle;

        // //유니티 이벤트
        private void OnEnable()
        {
            settings = LocQASettings.Load();
            if (false == kindMaskInitialized)
            {
                kindMask = LocQAKinds.DefaultMask;
                kindMaskInitialized = true;
            }
            LocQAStore.Changed += OnStoreChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            filterSignature = null;
        }

        private void OnDisable()
        {
            LocQAStore.Changed -= OnStoreChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            ReleasePreview();
            foreach (KeyValuePair<string, Texture2D> _pair in captureCache)
            {
                if (null != _pair.Value) DestroyImmediate(_pair.Value);
            }
            captureCache.Clear();
        }

        private void OnStoreChanged()
        {
            filterSignature = null;
            Repaint();
        }

        private void OnPlayModeChanged(PlayModeStateChange _change)
        {
            filterSignature = null;
            Repaint();
        }

        private void Update()
        {
            LocQARuntimeRunner _runner = LocQARuntimeRunner.Current;
            if (null != _runner && true == _runner.IsCycling && EditorApplication.timeSinceStartup - lastRepaint > 0.2)
            {
                lastRepaint = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnGUI()
        {
            EnsureStyles();
            RebuildFilterIfNeeded();
            HandleKeyboard();

            DrawToolbar();

            panelScroll = EditorGUILayout.BeginScrollView(panelScroll, GUILayout.MaxHeight(Mathf.Max(150f, position.height * 0.42f)));
            switch (tab)
            {
                case ETab.Play: DrawPlayTab(); break;
                case ETab.Prefab: DrawPrefabTab(); break;
                case ETab.Strings: DrawStringsTab(); break;
                case ETab.Settings: DrawSettingsTab(); break;
            }
            EditorGUILayout.EndScrollView();

            DrawSeparator();
            DrawLanguageBar();
            DrawFilterBar();

            Rect _listRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true), GUILayout.MinHeight(90f));
            DrawList(_listRect);

            DrawDetailSplitter();
            DrawDetail();
        }

        // //상단
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                tab = (ETab)GUILayout.Toolbar((int)tab, TabLabels, EditorStyles.toolbarButton, GUILayout.Width(440f));
                GUILayout.FlexibleSpace();

                if (GUILayout.Button(new GUIContent("사람 검수 창 열기", "모든 UI의 모든 문구를 언어별로 직접 보고 OK/NG를 기록하는 창"), EditorStyles.toolbarButton))
                {
                    LocQAReviewWindow.Open();
                }

                if (GUILayout.Button(new GUIContent("CSV 내보내기", "현재 필터에 보이는 결과를 CSV로 저장 (엑셀에서 바로 열림)"), EditorStyles.toolbarButton))
                {
                    ExportCsv();
                }

                if (EditorGUILayout.DropdownButton(new GUIContent("결과 지우기"), FocusType.Passive, EditorStyles.toolbarDropDown))
                {
                    GenericMenu _menu = new GenericMenu();
                    _menu.AddItem(new GUIContent("플레이 검사 결과"), false, () => LocQAStore.instance.Clear(LocQASource.Play));
                    _menu.AddItem(new GUIContent("프리팹 검사 결과"), false, () => LocQAStore.instance.Clear(LocQASource.Prefab));
                    _menu.AddItem(new GUIContent("문구 검사 결과"), false, () => LocQAStore.instance.Clear(LocQASource.String));
                    _menu.AddSeparator(string.Empty);
                    _menu.AddItem(new GUIContent("전부 (확인 범위 기록 포함)"), false, () => LocQAStore.instance.ClearAll());
                    _menu.ShowAsContext();
                }
            }
        }

        // //플레이 검사
        private void DrawPlayTab()
        {
            if (false == EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "플레이 모드에서 쓰는 검사입니다. 가장 정확합니다.\n" +
                    "1) 플레이를 시작하고 확인할 UI(옵션 창, 툴팁, 팝업 등)를 게임에 띄웁니다.\n" +
                    "2) '모든 언어 순회 검사'를 누르면 언어를 하나씩 바꿔 가며 화면의 모든 텍스트를 재고, 언어별 스크린샷에 문제 위치를 그려 저장합니다.\n" +
                    "3) 아래 목록에서 항목을 고르면 그 언어의 스크린샷이 바로 보입니다. ◀ ▶로 다른 언어와 비교할 수 있습니다.\n" +
                    "'자동 수집'을 켜 두면 게임을 돌아다니는 동안 지금 언어로 보이는 텍스트를 계속 검사해 모아 줍니다.",
                    MessageType.Info);

                if (GUILayout.Button("▶  플레이 시작", GUILayout.Height(28f))) EditorApplication.isPlaying = true;
                DrawSummaryLine(LocQAStore.instance.playSummary);
                return;
            }

            LocQARuntimeRunner _runner = LocQARuntimeRunner.Ensure();
            if (null == _runner) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                Language _current = LocQARuntimeRunner.GetCurrentLanguage();
                EditorGUILayout.LabelField("현재 게임 언어", GUILayout.Width(90f));

                using (new EditorGUI.DisabledScope(_runner.IsCycling))
                {
                    if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(26f))) LocQARuntimeRunner.SetLanguage(StepLanguage(_current, -1));

                    int _index = Array.IndexOf(LocQALanguages.All, _current);
                    int _picked = EditorGUILayout.Popup(_index, LanguageOptions(), GUILayout.Width(150f));
                    if (_picked != _index && _picked >= 0) LocQARuntimeRunner.SetLanguage(LocQALanguages.All[_picked]);

                    if (GUILayout.Button("▶", EditorStyles.miniButtonRight, GUILayout.Width(26f))) LocQARuntimeRunner.SetLanguage(StepLanguage(_current, +1));
                }
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(4f);

            if (true == _runner.IsCycling)
            {
                Rect _bar = EditorGUILayout.GetControlRect(false, 22f);
                EditorGUI.ProgressBar(_bar, _runner.Progress, _runner.Status);
                if (GUILayout.Button("중지", GUILayout.Height(24f))) _runner.StopCycle();
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                PlayModeWindow.GetRenderingResolution(out uint _gw, out uint _gh);
                EditorGUILayout.LabelField(new GUIContent($"Game 뷰 {_gw}×{_gh}", "플레이 검사는 Game 뷰 해상도로 진행됩니다. 아래에서 게임이 제공하는 해상도로 바꿀 수 있습니다."), GUILayout.Width(130f));
                using (new EditorGUI.DisabledScope(_runner.IsCycling))
                {
                    int _picked = EditorGUILayout.Popup(-1, LocQAResolution.Labels, GUILayout.Width(300f));
                    if (_picked >= 0)
                    {
                        LocQAResolution.Preset _preset = LocQAResolution.Get(_picked);
                        LocQARuntimeRunner.SetGameViewResolution(_preset.width, _preset.height);
                    }
                }
                GUILayout.FlexibleSpace();
            }

            List<Language> _langs = settings.SelectedLanguages();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("현재 화면 검사", "지금 언어로 화면의 모든 텍스트를 한 번 검사합니다."), GUILayout.Height(30f)))
                {
                    _runner.ReloadSettings();
                    _runner.ScanNow(null);
                }

                using (new EditorGUI.DisabledScope(0 == _langs.Count))
                {
                    string _label = $"모든 언어 순회 검사 ({_langs.Count}개 언어{(settings.captureScreenshots ? " + 스크린샷" : string.Empty)})";
                    if (GUILayout.Button(new GUIContent(_label, "언어를 하나씩 바꿔 가며 지금 화면을 검사합니다. 끝나면 원래 언어로 돌아옵니다. 대상 언어는 '설정' 탭에서 고릅니다."), GUILayout.Height(30f)))
                    {
                        _runner.ReloadSettings();
                        _runner.StartCycle(_langs, settings.captureScreenshots);
                    }

                    int _resCount = LocQAResolution.All.Count;
                    if (GUILayout.Button(new GUIContent($"모든 해상도 × 언어 순회 ({_resCount}×{_langs.Count})", "게임이 제공하는 모든 해상도(창모드 + 흔한 전체화면 모니터)로 Game 뷰를 바꿔 가며, 해상도마다 모든 언어를 검사하고 스크린샷을 남깁니다. 끝나면 원래 해상도·언어로 돌아옵니다."), GUILayout.Height(30f)))
                    {
                        List<int> _all = new List<int>(_resCount);
                        for (int i = 0; i < _resCount; i++) _all.Add(i);
                        _runner.ReloadSettings();
                        _runner.StartCycle(_langs, settings.captureScreenshots, _all);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                bool _auto = EditorGUILayout.ToggleLeft(new GUIContent("자동 수집", $"{settings.autoScanInterval:0.##}초마다 지금 언어로 보이는 텍스트를 계속 검사해 모읍니다."), _runner.AutoCollect, GUILayout.Width(90f));
                if (_auto != _runner.AutoCollect)
                {
                    _runner.ReloadSettings();
                    _runner.AutoCollect = _auto;
                }

                EditorGUI.BeginChangeCheck();
                settings.showOverlay = EditorGUILayout.ToggleLeft(new GUIContent("게임 화면에 문제 표시", "문제가 있는 텍스트를 게임 뷰 위에 박스로 그립니다. (스크린샷에도 찍힙니다)"), settings.showOverlay, GUILayout.Width(150f));
                settings.captureScreenshots = EditorGUILayout.ToggleLeft("순회 시 스크린샷 저장", settings.captureScreenshots, GUILayout.Width(150f));
                if (EditorGUI.EndChangeCheck())
                {
                    settings.Save();
                    _runner.ReloadSettings();
                    if (true == settings.showOverlay) _runner.ScanNow(null);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("스크린샷 폴더 열기", EditorStyles.miniButton))
                {
                    Directory.CreateDirectory(LocQARuntimeRunner.CAPTURE_ROOT);
                    EditorUtility.RevealInFinder(Path.GetFullPath(LocQARuntimeRunner.CAPTURE_ROOT));
                }
            }

            if (false == string.IsNullOrEmpty(_runner.Status)) EditorGUILayout.LabelField(_runner.Status, EditorStyles.miniLabel);
            DrawSummaryLine(LocQAStore.instance.playSummary);
            EditorGUILayout.LabelField("※ 스크린샷은 Game 뷰가 화면에 보이는 상태여야 찍힙니다. 박스 색: 노랑=텍스트 칸, 빨강=오류, 주황=경고", EditorStyles.miniLabel);
        }

        private static Language StepLanguage(Language _current, int _delta)
        {
            int _index = Array.IndexOf(LocQALanguages.All, _current);
            int _count = LocQALanguages.All.Length;
            return LocQALanguages.All[((_index + _delta) % _count + _count) % _count];
        }

        // //프리팹 검사
        private void DrawPrefabTab()
        {
            EditorGUILayout.HelpBox(
                "플레이 없이 폴더 안의 UI 프리팹을 전부 고른 해상도의 실제 캔버스 크기로 띄우고, 텍스트마다 설정한 모든 언어 문구·폰트를 넣어 잽니다.\n" +
                "이 게임은 문구를 코드에서 넣기 때문에, 프리팹에 적힌 기본 문구로 키를 자동 추론합니다. 추론이 안 되는 텍스트는 아래 '키 미연결' 목록에서 직접 지정하세요. (지정 내용은 저장소의 JSON에 기록되어 팀이 공유합니다)",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                settings.prefabFolder = EditorGUILayout.TextField("검사 폴더", settings.prefabFolder);
                if (GUILayout.Button("…", GUILayout.Width(26f)))
                {
                    string _picked = EditorUtility.OpenFolderPanel("UI 프리팹 폴더", settings.prefabFolder, string.Empty);
                    string _root = Path.GetFullPath(".").Replace('\\', '/') + "/";
                    if (false == string.IsNullOrEmpty(_picked) && _picked.Replace('\\', '/').StartsWith(_root, StringComparison.OrdinalIgnoreCase))
                    {
                        settings.prefabFolder = _picked.Replace('\\', '/').Substring(_root.Length);
                    }
                }
                if (EditorGUI.EndChangeCheck()) settings.Save();
            }

            EditorGUI.BeginChangeCheck();
            settings.resolutionIndex = EditorGUILayout.Popup(new GUIContent("해상도", "그 해상도에서 게임 UI 캔버스가 갖는 실제 크기로 검사합니다."), settings.ResolutionIndex, LocQAResolution.Labels);
            if (EditorGUI.EndChangeCheck()) settings.Save();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button($"모든 UI 프리팹 검사 ({settings.SelectedLanguages().Count}개 언어)", GUILayout.Height(30f)))
                {
                    LocQAPrefabScanner.ScanAll(settings);
                    sourceFilter = (int)LocQASource.Prefab;
                    filterSignature = null;
                    GUIUtility.ExitGUI();
                }
            }

            DrawSummaryLine(LocQAStore.instance.prefabSummary);
            DrawUnboundList();
        }

        private void DrawUnboundList()
        {
            List<LocQAUnbound> _unbound = LocQAStore.instance.unbound;
            if (0 == _unbound.Count) return;

            int _pending = 0;
            for (int i = 0; i < _unbound.Count; i++)
            {
                if (false == _unbound[i].ignored) _pending++;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                showUnbound = EditorGUILayout.Foldout(showUnbound, $"키 미연결 텍스트 {_pending}개 (검사에서 빠짐)", true);
                GUILayout.FlexibleSpace();
                showIgnoredUnbound = EditorGUILayout.ToggleLeft("제외한 것도 보기", showIgnoredUnbound, GUILayout.Width(120f));
            }
            if (false == showUnbound) return;

            EditorGUILayout.LabelField("숫자·이름처럼 실행 중에만 정해지는 텍스트는 '제외'하세요. 키를 지정하면 그 프리팹만 바로 다시 검사합니다.", EditorStyles.miniLabel);

            for (int i = 0; i < _unbound.Count; i++)
            {
                LocQAUnbound _u = _unbound[i];
                if (true == _u.ignored && false == showIgnoredUnbound) continue;

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(new GUIContent(LocQAPaths.ShortLocation(_u.prefabPath), _u.prefabPath), EditorStyles.miniLabel, GUILayout.Width(150f));
                    GUILayout.Label(new GUIContent(LastSegment(_u.objectPath), _u.objectPath), EditorStyles.miniLabel, GUILayout.Width(150f));
                    GUILayout.Label(new GUIContent("\"" + OneLine(_u.text, 60) + "\"", _u.text), EditorStyles.miniLabel, GUILayout.MinWidth(80f));

                    if (GUILayout.Button("보기", EditorStyles.miniButtonLeft, GUILayout.Width(40f))) OpenPrefabAndSelect(_u.prefabPath, _u.objectPath);

                    if (true == _u.ignored)
                    {
                        if (GUILayout.Button("제외 해제", EditorStyles.miniButtonRight, GUILayout.Width(70f))) SetBinding(_u, null, false);
                    }
                    else
                    {
                        if (EditorGUILayout.DropdownButton(new GUIContent("키 지정"), FocusType.Passive, EditorStyles.miniButtonMid, GUILayout.Width(64f)))
                        {
                            ShowKeyMenu(_u);
                        }
                        if (GUILayout.Button("제외", EditorStyles.miniButtonRight, GUILayout.Width(44f))) SetBinding(_u, null, true);
                    }
                }
            }
        }

        private void ShowKeyMenu(LocQAUnbound _u)
        {
            LocQAStringTable _table = GetTable();
            GenericMenu _menu = new GenericMenu();
            for (int i = 0; i < _table.Entries.Count; i++)
            {
                LocQAEntry _entry = _table.Entries[i];
                string _key = string.IsNullOrEmpty(_entry.data.key) ? "#" + _entry.data.id : _entry.data.key;
                string _label = $"{MenuSafe(_entry.file)}/{MenuSafe(_key)}   ·   {MenuSafe(OneLine(_entry.data.kr, 28))}";
                LocQAEntry _captured = _entry;
                _menu.AddItem(new GUIContent(_label), false, () => SetBinding(_u, _captured.id, false));
            }
            _menu.ShowAsContext();
        }

        private void SetBinding(LocQAUnbound _u, string _entryId, bool _ignore)
        {
            string _guid = _u.prefabGuid;
            string _objectPath = _u.objectPath;
            string _prefabPath = _u.prefabPath;

            // 목록을 그리는 도중(버튼)이나 메뉴 콜백에서 불리므로, 목록을 바꾸는 재검사는 GUI 처리가 끝난 뒤에 한다.
            EditorApplication.delayCall += () =>
            {
                LocQABindingSet _set = LocQABindingSet.Load();
                _set.Set(_guid, _objectPath, _entryId, _ignore);
                _set.Save();

                // 해당 프리팹만 다시 검사해 목록과 결과를 바로 갱신한다.
                LocQAPrefabScanner.Scan(settings, new List<string> { _prefabPath }, false);
                filterSignature = null;
                Repaint();
            };
        }

        private static LocQAUnbound ToBindingTarget(LocQAIssue _issue)
        {
            return new LocQAUnbound
            {
                prefabPath = _issue.location,
                prefabGuid = AssetDatabase.AssetPathToGUID(_issue.location),
                objectPath = _issue.objectPath,
                text = _issue.text
            };
        }

        // //문구 검사
        private void DrawStringsTab()
        {
            EditorGUILayout.HelpBox(
                "UI와 상관없이 모든 로컬라이징 키 × 설정한 모든 언어를 검사합니다: 빈 문구, 번역 누락(영어로 폴백), 해당 언어 폰트에 없는 글자(□로 나옴).\n" +
                "아래 '확인 범위'는 플레이·프리팹 검사에서 실제 화면에 나타나 크기 검사를 거친 문구의 비율입니다. 남은 문구 목록을 보고 아직 안 띄워 본 UI를 찾으세요.",
                MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"모든 문구 검사 ({settings.SelectedLanguages().Count}개 언어)", GUILayout.Height(30f)))
                {
                    LocQAStringScanner.Scan(settings);
                    tableCache = null;
                    GUIUtility.ExitGUI();
                }
            }
            DrawSummaryLine(LocQAStore.instance.stringSummary);

            LocQAStringTable _table = GetTable();
            LocQAStore _store = LocQAStore.instance;
            int _covered = 0;
            for (int i = 0; i < _table.Entries.Count; i++)
            {
                if (true == _store.IsCovered(_table.Entries[i].id)) _covered++;
            }
            int _total = Mathf.Max(1, _table.Entries.Count);

            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                Rect _bar = EditorGUILayout.GetControlRect(false, 20f);
                EditorGUI.ProgressBar(_bar, (float)_covered / _total, $"확인 범위: 화면에서 검사된 문구 {_covered} / {_table.Entries.Count} ({100f * _covered / _total:0}%)");
                if (GUILayout.Button("기록 초기화", GUILayout.Width(80f)))
                {
                    _store.ClearCoverage();
                    _store.NotifyChanged();
                }
            }

            showUncovered = EditorGUILayout.Foldout(showUncovered, $"아직 화면에서 확인 안 된 문구 {_table.Entries.Count - _covered}개", true);
            if (false == showUncovered) return;

            uncoveredSearch = EditorGUILayout.TextField("검색", uncoveredSearch);
            string _currentFile = null;
            int _shown = 0;
            for (int i = 0; i < _table.Entries.Count && _shown < 400; i++)
            {
                LocQAEntry _entry = _table.Entries[i];
                if (true == _store.IsCovered(_entry.id)) continue;
                if (false == string.IsNullOrEmpty(uncoveredSearch)
                    && _entry.id.IndexOf(uncoveredSearch, StringComparison.OrdinalIgnoreCase) < 0
                    && (_entry.data.kr ?? string.Empty).IndexOf(uncoveredSearch, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (_currentFile != _entry.file)
                {
                    _currentFile = _entry.file;
                    EditorGUILayout.LabelField(_entry.file, EditorStyles.boldLabel);
                }
                EditorGUILayout.LabelField(new GUIContent($"    {(string.IsNullOrEmpty(_entry.data.key) ? "#" + _entry.data.id : _entry.data.key)}  ·  {OneLine(_entry.data.kr, 70)}", _entry.data.kr), EditorStyles.miniLabel);
                _shown++;
            }
            if (_shown >= 400) EditorGUILayout.LabelField("… (검색으로 좁혀 보세요)", EditorStyles.miniLabel);
        }

        // //설정
        private void DrawSettingsTab()
        {
            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("검사할 언어", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("전체", EditorStyles.miniButtonLeft, GUILayout.Width(60f))) settings.languageMask = -1;
                if (GUILayout.Button("없음", EditorStyles.miniButtonMid, GUILayout.Width(60f))) settings.languageMask = 0;
                if (GUILayout.Button("한·영", EditorStyles.miniButtonMid, GUILayout.Width(60f))) SetLanguages(Language.KR, Language.EN);
                if (GUILayout.Button("한중일", EditorStyles.miniButtonMid, GUILayout.Width(60f))) SetLanguages(Language.KR, Language.JA, Language.ZH_HANS, Language.ZH_HANT);
                if (GUILayout.Button("긴 언어 위주", EditorStyles.miniButtonRight, GUILayout.Width(90f))) SetLanguages(Language.KR, Language.DE, Language.FR, Language.RU, Language.PT, Language.ES, Language.PL);
                GUILayout.FlexibleSpace();
            }

            int _perRow = Mathf.Max(1, (int)((position.width - 30f) / 150f));
            for (int i = 0; i < LocQALanguages.All.Length; i += _perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(i + _perRow, LocQALanguages.All.Length); j++)
                    {
                        Language _lang = LocQALanguages.All[j];
                        bool _on = EditorGUILayout.ToggleLeft($"{LocQALanguages.Name(_lang)} ({_lang})", settings.IsOn(_lang), GUILayout.Width(146f));
                        settings.SetOn(_lang, _on);
                    }
                }
            }

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("해상도", EditorStyles.boldLabel);
            settings.resolutionIndex = EditorGUILayout.Popup(new GUIContent("프리팹 검사·검수 해상도", "게임 옵션의 창모드 해상도와 흔한 전체화면 모니터 해상도입니다. 그 해상도에서 게임 UI 캔버스가 갖는 실제 크기로 UI를 띄웁니다. (플레이 검사는 Game 뷰 해상도를 따릅니다)"), settings.ResolutionIndex, LocQAResolution.Labels);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("판정 기준", EditorStyles.boldLabel);
            settings.tolerance = EditorGUILayout.Slider(new GUIContent("허용 오차 (UI 픽셀)", "이만큼 이하로 삐져나온 것은 넘친 것으로 보지 않습니다. 640×360 캔버스 단위입니다."), settings.tolerance, 0f, 6f);
            settings.formatSample = EditorGUILayout.TextField(new GUIContent("{0} 자리에 넣을 견본", "프리팹 검사에서 {0}, {1:N0} 같은 자리표시자를 이 값으로 채워 잽니다. 실제로 나올 수 있는 가장 긴 값으로 두면 안전합니다."), settings.formatSample);
            settings.checkParent = EditorGUILayout.ToggleLeft(new GUIContent("배경(부모 이미지) 밖으로 나간 글자 검사", "버튼·패널 배경보다 글자가 큰 경우를 찾습니다."), settings.checkParent);
            settings.checkScreen = EditorGUILayout.ToggleLeft(new GUIContent("화면 가장자리 밖으로 나간 글자 검사 (플레이 검사만)", "프리팹은 게임처럼 배치되지 않아(코드가 위치를 잡는 팝업, 스크롤 등) 프리팹 검사에서는 재지 않습니다."), settings.checkScreen);
            settings.checkWordBreak = EditorGUILayout.ToggleLeft(new GUIContent("단어 중간 줄바꿈 검사", "라틴·키릴 문자 단어가 칸이 좁아 둘로 쪼개진 경우. 한중일 글자는 제외합니다."), settings.checkWordBreak);
            settings.checkAutoWrap = EditorGUILayout.ToggleLeft(new GUIContent("자동 줄바꿈 기록 (참고)", "칸 안에 들어가더라도 원문보다 줄이 늘어난 텍스트를 알려 줍니다."), settings.checkAutoWrap);
            settings.checkGlyph = EditorGUILayout.ToggleLeft("폰트에 없는 글자(□) 검사", settings.checkGlyph);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("플레이 검사", EditorStyles.boldLabel);
            settings.onlyVisible = EditorGUILayout.ToggleLeft(new GUIContent("보이는 텍스트만 (투명·페이드아웃 제외)", "CanvasGroup 알파가 0인 패널 등은 건너뜁니다."), settings.onlyVisible);
            settings.captureDelay = EditorGUILayout.Slider(new GUIContent("언어 전환 후 대기 (초)", "연출(타자 효과 등)이 끝나기를 기다릴 시간입니다."), settings.captureDelay, 0f, 3f);
            settings.autoScanInterval = EditorGUILayout.Slider("자동 수집 간격 (초)", settings.autoScanInterval, 0.1f, 5f);

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("문구 검사", EditorStyles.boldLabel);
            TMP_FontAssetField();

            if (EditorGUI.EndChangeCheck())
            {
                settings.Save();
                if (null != LocQARuntimeRunner.Current) LocQARuntimeRunner.Current.ReloadSettings();
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("기본값으로", GUILayout.Width(100f)))
            {
                settings = new LocQASettings();
                settings.Save();
            }
        }

        private void TMP_FontAssetField()
        {
            TMPro.TMP_FontAsset _current = LocQAStringScanner.LoadUiFont(settings);
            TMPro.TMP_FontAsset _picked = (TMPro.TMP_FontAsset)EditorGUILayout.ObjectField(
                new GUIContent("UI 원본 폰트", "교체 폰트가 없는 언어(한국어·영어·베트남어)에서 쓰는 폰트입니다."), _current, typeof(TMPro.TMP_FontAsset), false);
            if (_picked != _current)
            {
                settings.defaultUiFontGuid = null != _picked ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_picked)) : string.Empty;
            }
        }

        private void SetLanguages(params Language[] _langs)
        {
            settings.languageMask = 0;
            for (int i = 0; i < _langs.Length; i++) settings.SetOn(_langs[i], true);
        }

        // //언어 요약 막대
        private void DrawLanguageBar()
        {
            float _width = position.width - 8f;
            int _perRow = Mathf.Max(1, (int)(_width / LANG_BUTTON_WIDTH));
            int _total = LocQALanguages.All.Length + 1;

            for (int i = 0; i < _total; i += _perRow)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (int j = i; j < Mathf.Min(i + _perRow, _total); j++)
                    {
                        int _langIndex = j - 1;
                        bool _isAll = _langIndex < 0;
                        int _count = _isAll ? totalCount : languageCounts[(int)LocQALanguages.All[_langIndex]];
                        int _errors = _isAll ? 0 : languageErrors[(int)LocQALanguages.All[_langIndex]];
                        string _label = _isAll ? $"전체 {_count}" : $"{LocQALanguages.All[_langIndex]} {_count}";
                        string _tip = _isAll ? "모든 언어" : $"{LocQALanguages.Name(LocQALanguages.All[_langIndex])} · 오류 {_errors}건";
                        bool _active = _isAll ? languageFilter < 0 : languageFilter == (int)LocQALanguages.All[_langIndex];

                        Color _prev = GUI.backgroundColor;
                        if (false == _isAll && _errors > 0) GUI.backgroundColor = new Color(1f, 0.55f, 0.5f);
                        else if (false == _isAll && 0 == _count) GUI.backgroundColor = new Color(0.6f, 0.85f, 0.6f);

                        bool _clicked = GUILayout.Toggle(_active, new GUIContent(_label, _tip), EditorStyles.miniButton, GUILayout.Width(LANG_BUTTON_WIDTH - 4f));
                        GUI.backgroundColor = _prev;

                        if (_clicked && false == _active)
                        {
                            languageFilter = _isAll ? -1 : (int)LocQALanguages.All[_langIndex];
                            filterSignature = null;
                        }
                    }
                }
            }
        }

        private void DrawFilterBar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string[] _sources = { "모든 출처", "플레이", "프리팹", "문구" };
                int _source = EditorGUILayout.Popup(sourceFilter + 1, _sources, EditorStyles.toolbarPopup, GUILayout.Width(80f)) - 1;
                if (_source != sourceFilter)
                {
                    sourceFilter = _source;
                    filterSignature = null;
                }

                int _mask = EditorGUILayout.MaskField(kindMask, LocQAKinds.Names, EditorStyles.toolbarPopup, GUILayout.Width(130f));
                if (_mask != kindMask)
                {
                    kindMask = _mask;
                    filterSignature = null;
                }

                bool _hide = GUILayout.Toggle(hideSourceIssues, new GUIContent("원문(한국어)에도 있는 문제 숨기기", "한국어에서도 똑같이 생기는 문제는 로컬라이징 탓이 아니므로 숨깁니다. 자동 줄바꿈은 한국어보다 줄이 늘어난 경우만 남깁니다."), EditorStyles.toolbarButton);
                if (_hide != hideSourceIssues)
                {
                    hideSourceIssues = _hide;
                    filterSignature = null;
                }

                GUILayout.FlexibleSpace();
                string _search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(200f));
                if (_search != search)
                {
                    search = _search;
                    filterSignature = null;
                }
                GUILayout.Label($"{filtered.Count}건", EditorStyles.miniLabel, GUILayout.Width(52f));
            }
        }

        // //목록
        private void RebuildFilterIfNeeded()
        {
            LocQAStore _store = LocQAStore.instance;
            string _signature = $"{_store.Version}|{_store.issues.Count}|{sourceFilter}|{languageFilter}|{kindMask}|{hideSourceIssues}|{search}";
            if (_signature == filterSignature) return;
            filterSignature = _signature;

            filtered.Clear();
            Array.Clear(languageCounts, 0, languageCounts.Length);
            Array.Clear(languageErrors, 0, languageErrors.Length);
            totalCount = 0;

            List<LocQAIssue> _issues = _store.issues;
            for (int i = 0; i < _issues.Count; i++)
            {
                LocQAIssue _issue = _issues[i];
                if (sourceFilter >= 0 && (int)_issue.source != sourceFilter) continue;
                if (0 == (kindMask & (1 << (int)_issue.kind))) continue;
                if (true == hideSourceIssues && true == _store.IsAlsoInSource(_issue)) continue;
                if (false == string.IsNullOrEmpty(search) && false == Matches(_issue, search)) continue;

                totalCount++;
                languageCounts[(int)_issue.language]++;
                if (LocQASeverity.Error == _issue.Severity) languageErrors[(int)_issue.language]++;

                if (languageFilter >= 0 && (int)_issue.language != languageFilter) continue;
                filtered.Add(_issue);
            }

            filtered.Sort((a, b) =>
            {
                int _c = a.Severity.CompareTo(b.Severity);
                if (0 != _c) return _c;
                _c = string.CompareOrdinal(a.location, b.location);
                if (0 != _c) return _c;
                _c = string.CompareOrdinal(a.objectPath, b.objectPath);
                if (0 != _c) return _c;
                _c = a.kind.CompareTo(b.kind);
                return 0 != _c ? _c : a.language.CompareTo(b.language);
            });

            if (null != selected && false == filtered.Contains(selected)) selected = null;
        }

        private static bool Matches(LocQAIssue _issue, string _search)
        {
            return Contains(_issue.text, _search) || Contains(_issue.key, _search) || Contains(_issue.location, _search)
                || Contains(_issue.objectPath, _search) || Contains(_issue.detail, _search);
        }

        private static bool Contains(string _haystack, string _needle)
        {
            return false == string.IsNullOrEmpty(_haystack) && _haystack.IndexOf(_needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawList(Rect _rect)
        {
            Rect _header = new Rect(_rect.x, _rect.y, _rect.width, ROW_HEIGHT);
            GUI.Box(_header, GUIContent.none, EditorStyles.toolbar);
            DrawColumns(_header, "", "언어", "종류", "위치", "문구", headerStyle);

            Rect _body = new Rect(_rect.x, _rect.y + ROW_HEIGHT, _rect.width, _rect.height - ROW_HEIGHT);
            if (0 == filtered.Count)
            {
                string _msg = 0 == LocQAStore.instance.issues.Count
                    ? "아직 결과가 없습니다. 위 탭에서 검사를 실행하세요."
                    : "현재 필터에 맞는 문제가 없습니다.";
                GUI.Label(new Rect(_body.x + 8f, _body.y + 8f, _body.width - 16f, 40f), _msg, EditorStyles.wordWrappedMiniLabel);
                return;
            }

            Rect _view = new Rect(0f, 0f, _body.width - 16f, filtered.Count * ROW_HEIGHT);
            listScroll = GUI.BeginScrollView(_body, listScroll, _view);

            int _first = Mathf.Max(0, (int)(listScroll.y / ROW_HEIGHT));
            int _last = Mathf.Min(filtered.Count - 1, _first + (int)(_body.height / ROW_HEIGHT) + 1);
            Event _e = Event.current;

            for (int i = _first; i <= _last; i++)
            {
                LocQAIssue _issue = filtered[i];
                Rect _row = new Rect(0f, i * ROW_HEIGHT, _view.width, ROW_HEIGHT);
                bool _isSelected = _issue == selected;

                if (Event.current.type == EventType.Repaint)
                {
                    if (_isSelected) EditorGUI.DrawRect(_row, new Color(0.24f, 0.48f, 0.9f, 0.45f));
                    else if (0 == (i & 1)) EditorGUI.DrawRect(_row, new Color(0f, 0f, 0f, 0.06f));
                    EditorGUI.DrawRect(new Rect(_row.x, _row.y + 2f, 4f, _row.height - 4f), LocQAKinds.SeverityColor(_issue.Severity));
                }

                string _location = LocQAPaths.ShortLocation(_issue.location) + " › " + LastSegment(_issue.objectPath);
                DrawColumns(_row, string.Empty, _issue.language.ToString(), LocQAKinds.Name(_issue.kind), _location,
                    OneLine(_issue.text, 200), _isSelected ? rowSelectedStyle : rowStyle);

                if (EventType.MouseDown == _e.type && _row.Contains(_e.mousePosition))
                {
                    Select(_issue);
                    listHasFocus = true;
                    GUIUtility.keyboardControl = 0;   // 검색창 포커스를 풀어야 방향키 이동이 먹는다.
                    if (2 == _e.clickCount) PingTarget(_issue);
                    _e.Use();
                }
            }

            GUI.EndScrollView();

            if (EventType.MouseDown == _e.type && false == _body.Contains(_e.mousePosition)) listHasFocus = false;
        }

        private static void DrawColumns(Rect _row, string _sev, string _lang, string _kind, string _location, string _text, GUIStyle _style)
        {
            float _x = _row.x + 8f;
            GUI.Label(new Rect(_x, _row.y, 72f, _row.height), _lang, _style);
            _x += 74f;
            GUI.Label(new Rect(_x, _row.y, 110f, _row.height), _kind, _style);
            _x += 112f;
            float _locWidth = Mathf.Clamp(_row.width * 0.3f, 160f, 340f);
            GUI.Label(new Rect(_x, _row.y, _locWidth, _row.height), _location, _style);
            _x += _locWidth + 4f;
            GUI.Label(new Rect(_x, _row.y, Mathf.Max(10f, _row.xMax - _x - 4f), _row.height), _text, _style);
        }

        private void HandleKeyboard()
        {
            Event _e = Event.current;
            if (EventType.KeyDown != _e.type || false == listHasFocus || 0 == filtered.Count) return;
            if (GUIUtility.keyboardControl != 0) return;   // 텍스트 필드 입력 중에는 건드리지 않는다.

            int _index = null != selected ? filtered.IndexOf(selected) : -1;
            int _next = _index;
            if (KeyCode.DownArrow == _e.keyCode) _next = Mathf.Min(filtered.Count - 1, _index + 1);
            else if (KeyCode.UpArrow == _e.keyCode) _next = Mathf.Max(0, _index - 1);
            else if (KeyCode.LeftArrow == _e.keyCode && null != selected) { StepPreviewLanguage(-1); _e.Use(); return; }
            else if (KeyCode.RightArrow == _e.keyCode && null != selected) { StepPreviewLanguage(+1); _e.Use(); return; }
            else if ((KeyCode.Return == _e.keyCode || KeyCode.KeypadEnter == _e.keyCode) && null != selected) { PingTarget(selected); _e.Use(); return; }
            else return;

            if (_next != _index && _next >= 0)
            {
                Select(filtered[_next]);
                float _y = _next * ROW_HEIGHT;
                if (_y < listScroll.y) listScroll.y = _y;
                float _visible = Mathf.Max(ROW_HEIGHT, position.height - detailHeight - 300f);
                if (_y + ROW_HEIGHT > listScroll.y + _visible) listScroll.y = _y + ROW_HEIGHT - _visible;
            }
            _e.Use();
            Repaint();
        }

        private void Select(LocQAIssue _issue)
        {
            if (selected == _issue) return;
            selected = _issue;
            previewLanguage = _issue.language;
            detailTextScroll = Vector2.zero;
            RequestPreview();
        }

        // //상세
        private void DrawDetailSplitter()
        {
            Rect _split = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(5f), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(new Rect(_split.x, _split.y + 2f, _split.width, 1f), new Color(0f, 0f, 0f, 0.35f));
            EditorGUIUtility.AddCursorRect(_split, MouseCursor.ResizeVertical);

            Event _e = Event.current;
            if (EventType.MouseDown == _e.type && _split.Contains(_e.mousePosition))
            {
                resizingDetail = true;
                _e.Use();
            }
            else if (true == resizingDetail && EventType.MouseDrag == _e.type)
            {
                detailHeight = Mathf.Clamp(detailHeight - _e.delta.y, 140f, position.height * 0.7f);
                _e.Use();
                Repaint();
            }
            else if (EventType.MouseUp == _e.type)
            {
                resizingDetail = false;
            }
        }

        private void DrawDetail()
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(detailHeight)))
            {
                if (null == selected)
                {
                    EditorGUILayout.HelpBox("목록에서 항목을 고르면 상세 내용과 그 언어의 화면이 여기에 나옵니다.\n↑↓: 항목 이동 · ←→: 미리보기 언어 바꾸기 · Enter/더블클릭: 오브젝트로 이동", MessageType.None);
                    return;
                }

                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(300f, position.width * 0.42f))))
                {
                    DrawDetailText(selected);
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    DrawPreview();
                }
            }
        }

        private void DrawDetailText(LocQAIssue _issue)
        {
            Color _prev = GUI.contentColor;
            GUI.contentColor = LocQAKinds.SeverityColor(_issue.Severity);
            EditorGUILayout.LabelField($"[{LocQALanguages.Name(_issue.language)}] {LocQAKinds.Name(_issue.kind)}", titleStyle);
            GUI.contentColor = _prev;

            detailTextScroll = EditorGUILayout.BeginScrollView(detailTextScroll);
            EditorGUILayout.LabelField(_issue.detail, wrapStyle);
            if (true == LocQAStore.instance.IsAlsoInSource(_issue))
            {
                EditorGUILayout.LabelField("※ 원문(한국어)에서도 같은 문제가 있습니다.", EditorStyles.miniBoldLabel);
            }

            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("문구", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(_issue.text ?? string.Empty, wrapStyle, GUILayout.MinHeight(wrapStyle.CalcHeight(new GUIContent(_issue.text ?? string.Empty), position.width * 0.4f)));

            EditorGUILayout.LabelField("키", string.IsNullOrEmpty(_issue.key) ? "(알 수 없음 — 코드에서 조합한 문구)" : _issue.key, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("위치", _issue.location, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("경로", _issue.objectPath, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                switch (_issue.source)
                {
                    case LocQASource.Play:
                        using (new EditorGUI.DisabledScope(null == _issue.target))
                        {
                            if (GUILayout.Button(new GUIContent("오브젝트 선택", null == _issue.target ? "플레이가 끝나 오브젝트가 사라졌습니다." : string.Empty))) PingTarget(_issue);
                        }
                        break;
                    case LocQASource.Prefab:
                        if (GUILayout.Button("프리팹 열고 선택")) PingTarget(_issue);
                        if (EditorGUILayout.DropdownButton(new GUIContent("키 다시 지정", "자동 추론된 키가 틀렸을 때 올바른 키를 지정합니다."), FocusType.Passive, GUILayout.Width(90f)))
                        {
                            ShowKeyMenu(ToBindingTarget(_issue));
                        }
                        if (GUILayout.Button(new GUIContent("검사 제외", "로컬라이징 문구가 들어가지 않는 텍스트(숫자·이름·고정 영문 등)라면 프리팹 검사에서 뺍니다."), GUILayout.Width(70f)))
                        {
                            SetBinding(ToBindingTarget(_issue), null, true);
                        }
                        break;
                    case LocQASource.String:
                        if (GUILayout.Button("JSON 파일 선택")) PingTarget(_issue);
                        break;
                }

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_issue.key)))
                {
                    if (GUILayout.Button(new GUIContent("사람 검수 창에서 보기", "이 문구가 들어가는 칸을 사람 검수 창에서 그 언어로 엽니다."), GUILayout.Width(130f)))
                    {
                        OpenInReview(_issue);
                    }
                }

                if (false == string.IsNullOrEmpty(_issue.text) && GUILayout.Button("문구 복사", GUILayout.Width(70f)))
                {
                    EditorGUIUtility.systemCopyBuffer = _issue.text;
                }
            }
        }

        private void DrawPreview()
        {
            if (null == selected) return;

            if (LocQASource.String == selected.source)
            {
                EditorGUILayout.HelpBox("문구 검사 결과는 화면이 없습니다. 같은 키가 화면에 나오는지는 플레이·프리팹 검사로 확인하세요.", MessageType.None);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("◀", EditorStyles.miniButtonLeft, GUILayout.Width(26f))) StepPreviewLanguage(-1);
                int _index = Array.IndexOf(LocQALanguages.All, previewLanguage);
                int _picked = EditorGUILayout.Popup(_index, LanguageOptions(), GUILayout.Width(150f));
                if (_picked != _index && _picked >= 0)
                {
                    previewLanguage = LocQALanguages.All[_picked];
                    RequestPreview();
                }
                if (GUILayout.Button("▶", EditorStyles.miniButtonRight, GUILayout.Width(26f))) StepPreviewLanguage(+1);

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(null == previewTexture))
                {
                    if (GUILayout.Button("크게 보기", EditorStyles.miniButton)) LocQAImageWindow.Show(previewTexture, previewHasRect, previewBox, previewGlyph, $"{LocQALanguages.Name(previewLanguage)} · {LastSegment(selected.objectPath)}");
                }
            }

            if (true == previewPending && EventType.Layout == Event.current.type) ResolvePreview();

            Rect _area = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(_area, new Color(0f, 0f, 0f, 0.25f));

            if (null == previewTexture)
            {
                GUI.Label(new Rect(_area.x + 8f, _area.y + 8f, _area.width - 16f, _area.height - 16f), previewMessage ?? string.Empty, EditorStyles.wordWrappedMiniLabel);
                return;
            }

            Rect _image = DrawImageWithHighlight(_area, previewTexture, previewHasRect, previewBox, previewGlyph);
            if (false == string.IsNullOrEmpty(previewMessage))
            {
                GUI.Label(new Rect(_image.x + 4f, _image.yMax - 18f, _image.width - 8f, 16f), previewMessage, EditorStyles.whiteMiniLabel);
            }

            if (EventType.MouseDown == Event.current.type && 2 == Event.current.clickCount && _image.Contains(Event.current.mousePosition))
            {
                LocQAImageWindow.Show(previewTexture, previewHasRect, previewBox, previewGlyph, $"{LocQALanguages.Name(previewLanguage)} · {LastSegment(selected.objectPath)}");
                Event.current.Use();
            }
        }

        public static Rect DrawImageWithHighlight(Rect _area, Texture _texture, bool _hasRect, Rect _box, Rect _glyph)
        {
            float _aspect = (float)_texture.width / Mathf.Max(1, _texture.height);
            float _w = _area.width;
            float _h = _w / _aspect;
            if (_h > _area.height)
            {
                _h = _area.height;
                _w = _h * _aspect;
            }
            Rect _image = new Rect(_area.x + (_area.width - _w) * 0.5f, _area.y + (_area.height - _h) * 0.5f, _w, _h);
            GUI.DrawTexture(_image, _texture, ScaleMode.StretchToFill);

            if (true == _hasRect)
            {
                DrawOutline(UvToGui(_image, _box), new Color(1f, 0.92f, 0.2f, 0.9f), 1f);
                DrawOutline(UvToGui(_image, _glyph), new Color(1f, 0.2f, 0.25f, 1f), 2f);
            }
            return _image;
        }

        private static Rect UvToGui(Rect _image, Rect _uv)
        {
            return new Rect(_image.x + _uv.xMin * _image.width, _image.y + (1f - _uv.yMax) * _image.height, _uv.width * _image.width, _uv.height * _image.height);
        }

        private static void DrawOutline(Rect _r, Color _color, float _t)
        {
            EditorGUI.DrawRect(new Rect(_r.xMin - _t, _r.yMin - _t, _r.width + _t * 2f, _t), _color);
            EditorGUI.DrawRect(new Rect(_r.xMin - _t, _r.yMax, _r.width + _t * 2f, _t), _color);
            EditorGUI.DrawRect(new Rect(_r.xMin - _t, _r.yMin, _t, _r.height), _color);
            EditorGUI.DrawRect(new Rect(_r.xMax, _r.yMin, _t, _r.height), _color);
        }

        private void StepPreviewLanguage(int _delta)
        {
            if (null == selected) return;
            previewLanguage = StepLanguage(previewLanguage, _delta);
            RequestPreview();
            Repaint();
        }

        private void RequestPreview()
        {
            previewPending = true;
            Repaint();
        }

        private void ResolvePreview()
        {
            previewPending = false;
            if (null == selected) return;
            if (previewIssue == selected && previewIssueLanguage == previewLanguage && (null != previewTexture || null != previewMessage)) return;

            ReleasePreview();
            previewIssue = selected;
            previewIssueLanguage = previewLanguage;

            if (LocQASource.Prefab == selected.source)
            {
                // 지연 호출로 미루면 에디터가 백그라운드일 때 처리되지 않아 "그리는 중"에 멈출 수 있어 바로 그린다.
                RenderPrefabPreview(selected, previewLanguage);
                return;
            }

            ResolveCapturePreview();
        }

        private void RenderPrefabPreview(LocQAIssue _issue, Language _lang)
        {
            if (this == null || previewIssue != _issue || previewIssueLanguage != _lang) return;

            Texture2D _tex = LocQAPrefabScanner.RenderPreview(_issue.location, _issue.objectPath, _lang, settings, out Rect _box, out Rect _glyph, out string _error);
            if (previewIssue != _issue || previewIssueLanguage != _lang)
            {
                if (null != _tex) DestroyImmediate(_tex);
                return;
            }

            previewTexture = _tex;
            previewOwned = true;
            previewHasRect = null != _tex && null == _error;
            previewBox = _box;
            previewGlyph = _glyph;
            previewMessage = _error ?? (null == _tex ? "미리보기를 만들지 못했습니다." : "프리팹 미리보기 (640×360)");
            Repaint();
        }

        private void ResolveCapturePreview()
        {
            LocQAIssue _issue = selected;
            string _file = null;
            LocQACapture _set = FindCaptureSet(_issue.capturePath);

            if (previewLanguage == _issue.language) _file = _issue.capturePath;
            else if (null != _set) _file = _set.FileFor(previewLanguage);

            if (string.IsNullOrEmpty(_file) || false == File.Exists(_file))
            {
                previewMessage = string.IsNullOrEmpty(_issue.capturePath)
                    ? "스크린샷이 없습니다. 플레이 검사에서 '순회 시 스크린샷 저장'을 켜고 '모든 언어 순회 검사'를 하면 여기에 언어별 화면이 나옵니다."
                    : $"이 순회에서 {LocQALanguages.Name(previewLanguage)} 스크린샷은 찍지 않았습니다.";
                return;
            }

            previewTexture = LoadCapture(_file);
            previewOwned = false;

            // 다른 언어 화면이면 같은 오브젝트가 그 언어에서 잡힌 위치를 쓴다. (그 언어에선 문제가 없으면 표시 없음)
            LocQAIssue _match = _issue;
            if (previewLanguage != _issue.language)
            {
                _match = null;
                List<LocQAIssue> _issues = LocQAStore.instance.issues;
                for (int i = 0; i < _issues.Count; i++)
                {
                    LocQAIssue _other = _issues[i];
                    if (_other.capturePath == _file && _other.ObjectKey == _issue.ObjectKey && true == _other.hasUv)
                    {
                        _match = _other;
                        break;
                    }
                }
            }

            previewHasRect = null != _match && true == _match.hasUv;
            if (true == previewHasRect)
            {
                previewBox = _match.boxUv;
                previewGlyph = _match.glyphUv;
            }
            previewMessage = null == _match ? $"{LocQALanguages.Name(previewLanguage)}: 이 텍스트는 문제 없음" : $"{LocQALanguages.Name(previewLanguage)} 스크린샷";
        }

        private static LocQACapture FindCaptureSet(string _file)
        {
            if (string.IsNullOrEmpty(_file)) return null;
            List<LocQACapture> _captures = LocQAStore.instance.captures;
            for (int i = 0; i < _captures.Count; i++)
            {
                if (true == _captures[i].files.Contains(_file)) return _captures[i];
            }
            return null;
        }

        private Texture2D LoadCapture(string _file)
        {
            if (true == captureCache.TryGetValue(_file, out Texture2D _cached) && null != _cached) return _cached;

            Texture2D _tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            _tex.hideFlags = HideFlags.HideAndDontSave;
            _tex.filterMode = FilterMode.Point;
            if (false == _tex.LoadImage(File.ReadAllBytes(_file)))
            {
                DestroyImmediate(_tex);
                return null;
            }
            captureCache[_file] = _tex;
            return _tex;
        }

        private void ReleasePreview()
        {
            if (true == previewOwned && null != previewTexture) DestroyImmediate(previewTexture);
            previewTexture = null;
            previewOwned = false;
            previewHasRect = false;
            previewMessage = null;
            previewIssue = null;
        }

        // //이동
        private void PingTarget(LocQAIssue _issue)
        {
            switch (_issue.source)
            {
                case LocQASource.Play:
                    if (null != _issue.target)
                    {
                        Selection.activeObject = _issue.target;
                        EditorGUIUtility.PingObject(_issue.target);
                    }
                    break;
                case LocQASource.Prefab:
                    OpenPrefabAndSelect(_issue.location, _issue.objectPath);
                    break;
                case LocQASource.String:
                    UnityEngine.Object _json = AssetDatabase.LoadAssetAtPath<TextAsset>(LocQAStringTable.JSON_FOLDER + "/" + _issue.location);
                    if (null != _json)
                    {
                        Selection.activeObject = _json;
                        EditorGUIUtility.PingObject(_json);
                    }
                    break;
            }
        }

        /// <summary>자동 검사 결과의 문구를 사람 검수 창에서 연다. 같은 칸의 항목을 먼저 찾고, 없으면 같은 문구의 첫 항목.</summary>
        private static void OpenInReview(LocQAIssue _issue)
        {
            string _slotKey = null;
            if (LocQASource.Prefab == _issue.source)
            {
                GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_issue.location);
                Transform _t = null != _prefab ? LocQAPaths.Find(_prefab.transform, _issue.objectPath) : null;
                TMPro.TMP_Text _text = null != _t ? _t.GetComponent<TMPro.TMP_Text>() : null;
                if (null != _text && true == LocQAReviewCollector.GetSlot(_text, out string _g, out string _p)) _slotKey = _g + "|" + _p;
            }

            List<LocQAReviewItem> _items = LocQAReviewData.Shared.Items;
            LocQAReviewItem _found = null;
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].entryId != _issue.key) continue;
                if (null == _found) _found = _items[i];
                if (null != _slotKey && _items[i].SlotKey == _slotKey)
                {
                    _found = _items[i];
                    break;
                }
            }

            if (null == _found)
            {
                EditorUtility.DisplayDialog("Localization QA", $"'{_issue.key}' 문구가 사람 검수 목록의 어느 칸에도 연결되어 있지 않습니다.\n검수 창에서 '목록 갱신'을 하거나 칸에 연결하세요.", "확인");
                return;
            }
            LocQAReviewWindow.FocusItem(_found.Key, _issue.language);
        }

        private static void OpenPrefabAndSelect(string _prefabPath, string _objectPath)
        {
            GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (null == _prefab) return;

            AssetDatabase.OpenAsset(_prefab);
            PrefabStage _stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (null == _stage) return;

            Transform _target = LocQAPaths.Find(_stage.prefabContentsRoot.transform, _objectPath);
            if (null == _target) return;

            Selection.activeGameObject = _target.gameObject;
            EditorGUIUtility.PingObject(_target.gameObject);
        }

        // //CSV
        private void ExportCsv()
        {
            if (0 == filtered.Count)
            {
                EditorUtility.DisplayDialog("Localization QA", "내보낼 결과가 없습니다.", "확인");
                return;
            }

            string _path = EditorUtility.SaveFilePanel("CSV 내보내기", Path.GetFullPath("LocalizationQA"), $"LocalizationQA_{DateTime.Now:yyyyMMdd_HHmm}.csv", "csv");
            if (string.IsNullOrEmpty(_path)) return;

            LocQAStore _store = LocQAStore.instance;
            StringBuilder _sb = new StringBuilder(filtered.Count * 160);
            _sb.AppendLine("출처,언어,심각도,종류,위치,오브젝트 경로,키,문구,상세,원문에도 있음,스크린샷");
            for (int i = 0; i < filtered.Count; i++)
            {
                LocQAIssue _issue = filtered[i];
                _sb.Append(Csv(LocQAKinds.SourceNames[(int)_issue.source])).Append(',')
                    .Append(Csv(LocQALanguages.Name(_issue.language))).Append(',')
                    .Append(Csv(_issue.Severity.ToString())).Append(',')
                    .Append(Csv(LocQAKinds.Name(_issue.kind))).Append(',')
                    .Append(Csv(_issue.location)).Append(',')
                    .Append(Csv(_issue.objectPath)).Append(',')
                    .Append(Csv(_issue.key)).Append(',')
                    .Append(Csv(_issue.text)).Append(',')
                    .Append(Csv(_issue.detail)).Append(',')
                    .Append(_store.IsAlsoInSource(_issue) ? "Y" : string.Empty).Append(',')
                    .Append(Csv(_issue.capturePath)).AppendLine();
            }

            // 엑셀이 한글을 바로 읽도록 BOM을 붙인다.
            File.WriteAllText(_path, _sb.ToString(), new UTF8Encoding(true));
            EditorUtility.RevealInFinder(_path);
        }

        private static string Csv(string _value)
        {
            if (string.IsNullOrEmpty(_value)) return string.Empty;
            return "\"" + _value.Replace("\"", "\"\"") + "\"";
        }

        // //공용
        private LocQAStringTable GetTable()
        {
            if (null == tableCache) tableCache = LocQAStringTable.Load();
            return tableCache;
        }

        private static GUIContent[] languageOptions;

        private static GUIContent[] LanguageOptions()
        {
            if (null == languageOptions)
            {
                languageOptions = new GUIContent[LocQALanguages.All.Length];
                for (int i = 0; i < languageOptions.Length; i++)
                {
                    languageOptions[i] = new GUIContent($"{LocQALanguages.Name(LocQALanguages.All[i])} ({LocQALanguages.All[i]})");
                }
            }
            return languageOptions;
        }

        private void DrawSummaryLine(string _summary)
        {
            if (false == string.IsNullOrEmpty(_summary)) EditorGUILayout.LabelField("마지막 결과: " + _summary, EditorStyles.miniLabel);
        }

        private static void DrawSeparator()
        {
            Rect _r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(6f), GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(new Rect(_r.x, _r.y + 3f, _r.width, 1f), new Color(0f, 0f, 0f, 0.3f));
        }

        private static string LastSegment(string _path)
        {
            if (string.IsNullOrEmpty(_path)) return "(루트)";
            int _slash = _path.LastIndexOf('/');
            return _slash >= 0 ? _path.Substring(_slash + 1) : _path;
        }

        private static string OneLine(string _text, int _max)
        {
            if (string.IsNullOrEmpty(_text)) return string.Empty;
            string _line = _text.Replace("\r\n", " ⏎ ").Replace("\n", " ⏎ ").Replace("\\n", " ⏎ ");
            return _line.Length > _max ? _line.Substring(0, _max) + "…" : _line;
        }

        private static string MenuSafe(string _text)
        {
            if (string.IsNullOrEmpty(_text)) return string.Empty;
            // GenericMenu는 '/'를 하위 메뉴로, 끝의 %#&를 단축키로 해석한다.
            return _text.Replace('/', '∕').Replace('%', '％').Replace('#', '＃').Replace('&', '＆');
        }

        private void EnsureStyles()
        {
            if (null != rowStyle) return;

            rowStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip, alignment = TextAnchor.MiddleLeft, richText = false };
            rowSelectedStyle = new GUIStyle(rowStyle);
            rowSelectedStyle.normal.textColor = Color.white;
            headerStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft };
            wrapStyle = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        }
    }

    /// <summary>미리보기를 크게 띄우는 창. 텍스처는 복사해서 따로 갖는다. (원래 창의 선택이 바뀌어도 남도록)</summary>
    internal sealed class LocQAImageWindow : EditorWindow
    {
        private Texture2D texture;
        private bool hasRect;
        private Rect box;
        private Rect glyph;

        public static void Show(Texture2D _source, bool _hasRect, Rect _box, Rect _glyph, string _title)
        {
            if (null == _source) return;

            LocQAImageWindow _window = CreateInstance<LocQAImageWindow>();
            _window.titleContent = new GUIContent(_title);
            _window.texture = Instantiate(_source);
            _window.texture.hideFlags = HideFlags.HideAndDontSave;
            _window.texture.filterMode = FilterMode.Point;
            _window.hasRect = _hasRect;
            _window.box = _box;
            _window.glyph = _glyph;
            _window.minSize = new Vector2(480f, 300f);
            _window.position = new Rect(120f, 120f, Mathf.Min(1300f, _source.width + 20f), Mathf.Min(760f, _source.height + 20f));
            _window.Show();
        }

        private void OnDisable()
        {
            if (null != texture) DestroyImmediate(texture);
        }

        private void OnGUI()
        {
            if (null == texture)
            {
                Close();
                return;
            }
            Rect _area = new Rect(0f, 0f, position.width, position.height);
            EditorGUI.DrawRect(_area, new Color(0.1f, 0.1f, 0.1f));
            LocQAWindow.DrawImageWithHighlight(_area, texture, hasRect, box, glyph);
        }
    }
}
