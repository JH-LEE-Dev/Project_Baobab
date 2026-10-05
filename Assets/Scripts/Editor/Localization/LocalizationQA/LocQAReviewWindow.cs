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
    /// 사람이 모든 UI의 모든 문구를 언어별로 직접 보고 판정하는 검수 창.
    ///
    ///  왼쪽  : 화면(UIView) 목록과 진행률. 화면이 쓰는 부품(툴팁·키 가이드 등)까지 한 화면으로 묶는다.
    ///  가운데: 그 화면의 텍스트 칸 × 들어갈 수 있는 문구 목록
    ///  오른쪽: 보기 방식에 따라
    ///    · 문구별 비교 : 고른 문구 하나를 모든 언어로 나란히 (번역 간 비교, 레이아웃 담당자용)
    ///    · 언어별 검수 : 고른 언어 하나로 화면의 모든 문구를 바둑판으로 (언어 담당 검수자용)
    ///
    /// 판정은 저장소의 JSON(언어별 파일)에 남고, 번역이 바뀌면 그 칸은 "번역 바뀜"으로 돌아가 다시 보게 된다.
    /// </summary>
    internal sealed class LocQAReviewWindow : EditorWindow
    {
        private const float LEFT_WIDTH = 230f;
        private const float MIDDLE_WIDTH = 340f;
        private const float TOOLBAR_HEIGHT = 42f;
        private const float DETAIL_HEIGHT = 178f;
        private const double RENDER_BUDGET = 0.04;
        private const int LANGUAGE_CACHE_LIMIT = 600;

        private static readonly Color ColorTodo = new Color(0.45f, 0.45f, 0.45f);
        private static readonly Color ColorOk = new Color(0.30f, 0.78f, 0.38f);
        private static readonly Color ColorNg = new Color(0.93f, 0.26f, 0.22f);
        private static readonly Color ColorChanged = new Color(0.98f, 0.62f, 0.12f);
        private static readonly Color ColorSelected = new Color(0.25f, 0.55f, 1f);

        private enum EFilter
        {
            All,
            Todo,
            Ng
        }

        private enum EMode
        {
            Compare,
            Language
        }

        private static readonly string[] FilterLabels = { "전체", "할 일 남은 것", "NG 있는 것" };

        private enum ELayout
        {
            Grid,
            Horizontal,
            Vertical
        }

        private static readonly GUIContent[] LayoutLabels =
        {
            new GUIContent("바둑판", "여러 칸을 바둑판처럼 나란히 봅니다."),
            new GUIContent("가로 스크롤", "칸을 화면 높이에 꽉 차게 키워 가로로 넘겨 봅니다. ('전체 화면'과 함께 쓰면 UI 전체를 크게 볼 수 있습니다)"),
            new GUIContent("세로 스크롤", "칸을 화면 폭에 꽉 차게 키워 세로로 넘겨 봅니다. ('전체 화면'과 함께 쓰면 UI 전체를 크게 볼 수 있습니다)")
        };
        private static readonly GUIContent[] ModeLabels =
        {
            new GUIContent("문구별 비교", "문구 하나를 모든 언어로 나란히 봅니다."),
            new GUIContent("언어별 검수", "언어 하나로 화면의 모든 문구를 바둑판으로 넘겨 봅니다. (언어 담당 검수자용)")
        };

        [MenuItem("Tools/Localization/Localization QA/사람 검수 (UI 문구)", false, 19)]
        public static void Open()
        {
            LocQAReviewWindow _window = GetWindow<LocQAReviewWindow>("UI 문구 검수", true, typeof(LocQAWindow));
            _window.titleContent = new GUIContent("UI 문구 검수", EditorGUIUtility.IconContent("d_FilterSelectedOnly").image);
            _window.minSize = new Vector2(1100f, 620f);
            _window.Show();
        }

        /// <summary>다른 창(자동 검사 등)에서 특정 항목을 바로 열 때 쓴다.</summary>
        public static void FocusItem(string _itemKey, Language _language)
        {
            Open();
            LocQAReviewWindow _window = GetWindow<LocQAReviewWindow>();
            LocQAReviewItem _item = LocQAReviewData.Shared.Find(_itemKey);
            if (null == _item) return;

            _window.mode = EMode.Compare;
            _window.selectedContext = _window.FirstScreen(_item.contextGuid, _item.slotGuid);
            _window.filter = EFilter.All;
            _window.rowSearch = string.Empty;
            _window.selectedRowKey = _item.Key;
            _window.builtSignature = null;
            _window.pendingCellLanguage = _language;
            _window.Repaint();
        }

        /// <summary>진행 현황판에서 화면·언어를 골랐을 때 언어별 검수로 연다.</summary>
        public static void OpenLanguage(string _screenGuid, Language _language)
        {
            Open();
            LocQAReviewWindow _window = GetWindow<LocQAReviewWindow>();
            _window.mode = EMode.Language;
            _window.reviewLanguage = (int)_language;
            _window.selectedContext = _screenGuid;
            _window.selectedRowKey = null;
            _window.builtSignature = null;
            _window.Repaint();
        }

        // //창 상태
        [SerializeField] private EMode mode = EMode.Compare;
        [SerializeField] private int reviewLanguage = (int)Language.EN;
        [SerializeField] private string selectedContext;
        [SerializeField] private string selectedRowKey;
        [SerializeField] private int selectedCell;
        [SerializeField] private EFilter filter = EFilter.Todo;
        [SerializeField] private string contextSearch = string.Empty;
        [SerializeField] private string rowSearch = string.Empty;
        [SerializeField] private bool fullView;
        [SerializeField] private Vector2 contextScroll;
        [SerializeField] private Vector2 rowScroll;
        [SerializeField] private Vector2 gridScroll;
        [SerializeField] private float cellWidth = 230f;   // 바둑판 칸 폭 (툴바 슬라이더)
        [SerializeField] private ELayout layout = ELayout.Grid;

        private sealed class ContextRow
        {
            public string guid;
            public string name;
            public int rows;
            public int unconnected;
            public int done;
            public int total;
            public int ng;
        }

        private sealed class Row
        {
            public string key;
            public LocQAReviewItem item;
            public LocQAReviewSlot slot;
            public LocQAEntry entry;
            public string label;
            public string sub;
            public string group;
        }

        private LocQASettings settings;
        private List<Language> languages = new List<Language>();
        private LocQAStringTable table;
        private readonly Dictionary<string, string[]> hashCache = new Dictionary<string, string[]>(StringComparer.Ordinal);
        private readonly List<ContextRow> contexts = new List<ContextRow>(64);
        private readonly List<Row> rows = new List<Row>(256);
        [NonSerialized] private string builtSignature;
        [NonSerialized] private int unconnectedEntries;
        [NonSerialized] private Language? pendingCellLanguage;

        // 문구별 비교: 고른 문구 하나의 언어별 그림
        [NonSerialized] private string renderedKey;
        [NonSerialized] private List<LocQAReviewCell> cells;
        [NonSerialized] private string renderError;
        [NonSerialized] private string renderNote;

        // 언어별 검수: 문구마다 고른 언어 하나의 그림 (차례로 조금씩 그린다)
        private readonly Dictionary<string, LocQAReviewCell> languageCells = new Dictionary<string, LocQAReviewCell>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> languageErrors = new Dictionary<string, string>(StringComparer.Ordinal);
        [NonSerialized] private LocQARenderSession session;
        [NonSerialized] private Vector2 sessionCanvas;
        [NonSerialized] private int gridColumns = 1;
        [NonSerialized] private float gridCellHeight = 100f;
        [NonSerialized] private bool scrollToSelection;
        [NonSerialized] private int firstVisibleRow;

        private GUIStyle rowStyle;
        private GUIStyle rowSelectedStyle;
        private GUIStyle smallWrap;
        private GUIStyle titleStyle;
        private GUIStyle cellHeader;

        private Language ReviewLanguage => (Language)Mathf.Clamp(reviewLanguage, 0, LocQALanguages.All.Length - 1);

        // //유니티 이벤트
        private void OnEnable()
        {
            settings = LocQASettings.Load();
            table = LocQAStringTable.Load();
            hashCache.Clear();
            LocQAReviewData.Changed += OnDataChanged;
            builtSignature = null;
        }

        private void OnDisable()
        {
            LocQAReviewData.Changed -= OnDataChanged;
            LocQAReviewData.Shared.SaveNow();
            ReleaseCells();
            ReleaseLanguageCells();
            DisposeSession();
        }

        private void OnFocus()
        {
            // 설정 창에서 언어·해상도를 바꿨을 수 있다.
            settings = LocQASettings.Load();
            builtSignature = null;
        }

        private void OnDataChanged()
        {
            builtSignature = null;
            Repaint();
        }

        private void Update()
        {
            if (EMode.Language != mode || EditorApplication.isCompiling) return;
            if (true == RenderLanguageCellsStep()) Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            RebuildIfNeeded();
            HandleKeyboard();

            // 그림은 Layout 이벤트 맨 앞에서 바로 그린다. (미리보기 창들이 쓰는 방식)
            // 지연 호출로 미루면 에디터가 백그라운드일 때 처리되지 않아 "그리는 중"에 멈출 수 있다.
            if (EventType.Layout == Event.current.type && EMode.Compare == mode) RenderCompareIfNeeded();

            float _h = position.height - TOOLBAR_HEIGHT;
            GUILayout.BeginArea(new Rect(0f, 0f, position.width, TOOLBAR_HEIGHT));
            DrawToolbar();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(0f, TOOLBAR_HEIGHT, LEFT_WIDTH, _h), EditorStyles.helpBox);
            DrawContexts();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(LEFT_WIDTH, TOOLBAR_HEIGHT, MIDDLE_WIDTH, _h), EditorStyles.helpBox);
            DrawRows();
            GUILayout.EndArea();

            Rect _right = new Rect(LEFT_WIDTH + MIDDLE_WIDTH, TOOLBAR_HEIGHT, position.width - LEFT_WIDTH - MIDDLE_WIDTH, _h);
            GUILayout.BeginArea(_right);
            if (EMode.Compare == mode) DrawCompare(_right.width);
            else DrawLanguageGrid(_right.width);
            GUILayout.EndArea();
        }

        // //상단
        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    if (GUILayout.Button(new GUIContent("목록 갱신", "UI 프리팹을 훑어 텍스트 칸과 문구를 모읍니다. 기존 연결과 판정은 그대로 유지됩니다."), EditorStyles.toolbarButton))
                    {
                        LocQAScreenIndex.Invalidate();
                        LocQAReviewCollector.CollectFromPrefabs(settings);
                        ResetCaches();
                        GUIUtility.ExitGUI();
                    }
                }
                if (GUILayout.Button(new GUIContent("다시 읽기", "git pull 등으로 다른 사람의 판정을 받은 뒤, 또는 번역 JSON이 바뀐 뒤 누르세요."), EditorStyles.toolbarButton))
                {
                    LocQAReviewData.Reload();
                    LocQAScreenIndex.Invalidate();
                    ResetCaches();
                }

                GUILayout.Space(10f);
                EditorGUI.BeginChangeCheck();
                EMode _mode = (EMode)GUILayout.Toolbar((int)mode, ModeLabels, EditorStyles.toolbarButton, GUILayout.Width(200f));
                if (EditorGUI.EndChangeCheck())
                {
                    mode = _mode;
                    builtSignature = null;
                    gridScroll = Vector2.zero;
                    scrollToSelection = true;
                }

                if (EMode.Language == mode)
                {
                    EditorGUI.BeginChangeCheck();
                    int _lang = EditorGUILayout.Popup(reviewLanguage, LanguageOptions(), EditorStyles.toolbarPopup, GUILayout.Width(150f));
                    if (EditorGUI.EndChangeCheck())
                    {
                        reviewLanguage = _lang;
                        builtSignature = null;
                    }
                }

                GUILayout.Space(10f);
                EditorGUI.BeginChangeCheck();
                EFilter _filter = (EFilter)EditorGUILayout.Popup((int)filter, FilterLabels, EditorStyles.toolbarPopup, GUILayout.Width(110f));
                if (EditorGUI.EndChangeCheck())
                {
                    filter = _filter;
                    builtSignature = null;
                }

                GUILayout.FlexibleSpace();
                EditorGUI.BeginChangeCheck();
                int _resolution = EditorGUILayout.Popup(settings.ResolutionIndex, LocQAResolution.Labels, EditorStyles.toolbarPopup, GUILayout.Width(280f));
                if (EditorGUI.EndChangeCheck())
                {
                    settings.resolutionIndex = _resolution;
                    settings.Save();
                    builtSignature = null;
                }
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button(new GUIContent("진행 현황", "언어별·화면별 검수 진행률과 NG 수를 한눈에 봅니다."), EditorStyles.toolbarButton))
                {
                    LocQAProgressWindow.Open();
                }
                if (GUILayout.Button(new GUIContent($"어느 칸에도 연결 안 된 문구 {unconnectedEntries}개", "어떤 UI 칸에서도 검수 대상이 아닌 문구입니다. (게임에서 쓰지 않는 문구로 표시한 것은 제외) 칸을 고른 뒤 이 목록에서 연결하세요."), EditorStyles.toolbarButton))
                {
                    OpenKeyPicker(true);
                }

                GUILayout.Space(10f);
                GUILayout.Label($"비교 언어 {languages.Count}개", EditorStyles.miniLabel, GUILayout.Width(70f));
                if (GUILayout.Button(new GUIContent("언어 고르기", "문구별 비교에 나올 언어는 자동 검사 창 '설정' 탭과 같은 값을 씁니다."), EditorStyles.toolbarButton))
                {
                    LocQAWindow.Open();
                }

                GUILayout.Space(10f);
                GUILayout.Label(new GUIContent("칸 크기", "바둑판 칸을 크게 하면 그림이 커집니다. (화면 폭에 들어가는 칸 수가 줄어듭니다)"), EditorStyles.miniLabel, GUILayout.Width(40f));
                using (new EditorGUI.DisabledScope(ELayout.Grid != layout))
                {
                    cellWidth = GUILayout.HorizontalSlider(cellWidth, 180f, 900f, GUILayout.Width(110f));
                }

                GUILayout.Space(6f);
                EditorGUI.BeginChangeCheck();
                ELayout _layout = (ELayout)EditorGUILayout.Popup((int)layout, LayoutLabels, EditorStyles.toolbarPopup, GUILayout.Width(92f));
                if (EditorGUI.EndChangeCheck())
                {
                    layout = _layout;
                    gridScroll = Vector2.zero;
                    scrollToSelection = true;
                }
                bool _maximized = GUILayout.Toggle(maximized, new GUIContent("창 최대화", "검수 창을 에디터 전체 크기로 키웁니다. 다시 누르면 원래대로 돌아옵니다."), EditorStyles.toolbarButton, GUILayout.Width(64f));
                if (_maximized != maximized) maximized = _maximized;

                GUILayout.FlexibleSpace();
                GUILayout.Label("검수자", EditorStyles.miniLabel, GUILayout.Width(38f));
                string _reviewer = EditorGUILayout.DelayedTextField(LocQAReviewData.Reviewer, EditorStyles.toolbarTextField, GUILayout.Width(110f));
                if (_reviewer != LocQAReviewData.Reviewer) LocQAReviewData.Reviewer = _reviewer;

                if (GUILayout.Button(new GUIContent("NG 목록 CSV", "NG로 판정한 칸을 사유와 함께 CSV로 내보냅니다."), EditorStyles.toolbarButton)) ExportNgCsv();
                if (GUILayout.Button(new GUIContent("자동 검사 창", "칸 넘침 등을 자동으로 찾는 창"), EditorStyles.toolbarButton)) LocQAWindow.Open();
            }
        }

        private void ResetCaches()
        {
            table = LocQAStringTable.Load();
            hashCache.Clear();
            ReleaseCells();
            ReleaseLanguageCells();
            DisposeSession();
            builtSignature = null;
        }

        // //왼쪽: 화면 목록
        private void DrawContexts()
        {
            EditorGUILayout.LabelField(new GUIContent("화면 목록", "UIView 화면 단위입니다. 화면이 쓰는 부품(툴팁·키 가이드·옵션 행 등)의 문구도 그 화면에 함께 나옵니다."), EditorStyles.boldLabel);
            if (EMode.Language == mode) EditorGUILayout.LabelField($"진행률: {LocQALanguages.Name(ReviewLanguage)} 기준", EditorStyles.miniLabel);
            contextSearch = EditorGUILayout.TextField(contextSearch, EditorStyles.toolbarSearchField);

            if (0 == contexts.Count)
            {
                EditorGUILayout.HelpBox("검수 목록이 비어 있습니다.\n위의 '목록 갱신'을 누르면 UI 프리팹의 텍스트 칸과 문구를 모읍니다.", MessageType.Info);
                return;
            }

            contextScroll = EditorGUILayout.BeginScrollView(contextScroll);
            for (int i = 0; i < contexts.Count; i++)
            {
                ContextRow _c = contexts[i];
                if (false == string.IsNullOrEmpty(contextSearch) && _c.name.IndexOf(contextSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;

                bool _selected = _c.guid == selectedContext;
                Rect _r = GUILayoutUtility.GetRect(LEFT_WIDTH - 30f, 38f);
                if (EventType.Repaint == Event.current.type)
                {
                    if (_selected) EditorGUI.DrawRect(_r, new Color(0.24f, 0.48f, 0.9f, 0.35f));
                    float _ratio = _c.total > 0 ? (float)_c.done / _c.total : 0f;
                    Rect _bar = new Rect(_r.x + 4f, _r.yMax - 8f, _r.width - 8f, 4f);
                    EditorGUI.DrawRect(_bar, new Color(0f, 0f, 0f, 0.3f));
                    EditorGUI.DrawRect(new Rect(_bar.x, _bar.y, _bar.width * _ratio, _bar.height), _c.ng > 0 ? ColorNg : ColorOk);
                }

                GUI.Label(new Rect(_r.x + 4f, _r.y + 1f, _r.width - 8f, 16f), _c.name, _selected ? EditorStyles.whiteBoldLabel : EditorStyles.boldLabel);
                string _info = $"문구 {_c.rows}개 · {_c.done}/{_c.total}" + (_c.ng > 0 ? $" · NG {_c.ng}" : string.Empty) + (_c.unconnected > 0 ? $" · 미연결 칸 {_c.unconnected}" : string.Empty);
                GUI.Label(new Rect(_r.x + 4f, _r.y + 16f, _r.width - 8f, 14f), _info, EditorStyles.miniLabel);

                if (EventType.MouseDown == Event.current.type && _r.Contains(Event.current.mousePosition))
                {
                    SelectContext(_c.guid);
                    Event.current.Use();
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void SelectContext(string _guid)
        {
            selectedContext = _guid;
            selectedRowKey = null;
            rowScroll = Vector2.zero;
            gridScroll = Vector2.zero;
            builtSignature = null;
            GUIUtility.keyboardControl = 0;
        }

        // //가운데: 문구 목록
        private void DrawRows()
        {
            if (string.IsNullOrEmpty(selectedContext) && false == contexts.Exists(c => c.guid == selectedContext))
            {
                EditorGUILayout.HelpBox("왼쪽에서 화면을 고르세요.", MessageType.None);
                return;
            }

            ContextRow _context = contexts.Find(c => c.guid == selectedContext);
            EditorGUILayout.LabelField(null != _context ? _context.name : "(화면)", EditorStyles.boldLabel);

            string _search = EditorGUILayout.TextField(rowSearch, EditorStyles.toolbarSearchField);
            if (_search != rowSearch)
            {
                rowSearch = _search;
                builtSignature = null;
            }

            rowScroll = EditorGUILayout.BeginScrollView(rowScroll);
            string _lastGroup = null;
            for (int i = 0; i < rows.Count; i++)
            {
                Row _row = rows[i];
                if (_row.group != _lastGroup)
                {
                    _lastGroup = _row.group;
                    EditorGUILayout.LabelField(new GUIContent("▸ " + _row.group, SlotTooltip(_row)), EditorStyles.miniBoldLabel);
                }

                bool _selected = _row.key == selectedRowKey;
                Rect _r = GUILayoutUtility.GetRect(MIDDLE_WIDTH - 30f, 34f);
                if (EventType.Repaint == Event.current.type && _selected) EditorGUI.DrawRect(_r, new Color(0.24f, 0.48f, 0.9f, 0.35f));

                GUI.Label(new Rect(_r.x + 10f, _r.y, _r.width - 14f, 16f), new GUIContent(_row.label, _row.label), _selected ? rowSelectedStyle : rowStyle);
                GUI.Label(new Rect(_r.x + 10f, _r.y + 15f, _r.width * 0.55f, 16f), new GUIContent(_row.sub, _row.sub), EditorStyles.miniLabel);

                if (EventType.Repaint == Event.current.type)
                {
                    if (null != _row.item)
                    {
                        string[] _hashes = Hashes(_row.entry);
                        if (EMode.Compare == mode)
                        {
                            // 언어별 판정 상태를 작은 칸으로 보여준다.
                            float _sq = 7f;
                            float _x = _r.xMax - (_sq + 1f) * languages.Count - 4f;
                            for (int l = 0; l < languages.Count; l++)
                            {
                                Color _c = StateColor(LocQAReviewData.Shared.GetEffective(_row.item, languages[l], _hashes[(int)languages[l]]));
                                EditorGUI.DrawRect(new Rect(_x + l * (_sq + 1f), _r.y + 20f, _sq, _sq), _c);
                            }
                        }
                        else
                        {
                            LocQAReviewData.EffectiveState _state = LocQAReviewData.Shared.GetEffective(_row.item, ReviewLanguage, _hashes[(int)ReviewLanguage]);
                            Color _prev = GUI.contentColor;
                            GUI.contentColor = StateColor(_state);
                            GUI.Label(new Rect(_r.xMax - 70f, _r.y + 15f, 66f, 16f), StateLabel(_state), EditorStyles.miniBoldLabel);
                            GUI.contentColor = _prev;
                        }
                    }
                    else if (null != _row.slot)
                    {
                        GUI.Label(new Rect(_r.xMax - 120f, _r.y + 15f, 116f, 16f), true == _row.slot.ignored ? "번역 대상 아님" : "문구 연결 필요", EditorStyles.miniBoldLabel);
                    }
                }

                if (EventType.MouseDown == Event.current.type && _r.Contains(Event.current.mousePosition))
                {
                    SelectRow(_row.key);
                    scrollToSelection = true;
                    Event.current.Use();
                }
            }
            if (0 == rows.Count) EditorGUILayout.LabelField("표시할 문구가 없습니다. (필터를 '전체'로 바꿔 보세요)", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void SelectRow(string _key)
        {
            if (selectedRowKey == _key) return;
            selectedRowKey = _key;
            selectedCell = 0;
            if (EMode.Compare == mode) gridScroll = Vector2.zero;
            GUIUtility.keyboardControl = 0;
            Repaint();
        }

        // //오른쪽 (문구별 비교)
        private void DrawCompare(float _width)
        {
            Row _row = CurrentRow();
            if (null == _row)
            {
                EditorGUILayout.HelpBox(
                    "가운데 목록에서 문구를 고르면, 그 문구를 실제 UI 칸에 넣은 화면이 언어별로 나란히 나옵니다.\n" +
                    "언어 하나로 화면 전체를 넘겨 보려면 위의 '언어별 검수'를 누르세요.\n\n" +
                    "단축키 (목록·화면을 클릭한 상태에서)\n" +
                    "  ←→↑↓ : 칸 이동\n" +
                    "  1 또는 O : OK      2 또는 X : NG      0 : 판정 지우기\n" +
                    "  Space : OK 후 다음 칸\n" +
                    "  Enter : 아직 안 본 언어를 모두 OK로 하고 다음 문구\n" +
                    "  PageDown / PageUp : 다음 / 이전 문구\n" +
                    "  더블클릭 : 크게 보기",
                    MessageType.Info);
                return;
            }

            DrawItemHeader(_row);

            if (false == string.IsNullOrEmpty(renderError)) EditorGUILayout.HelpBox(renderError, MessageType.Warning);
            if (false == string.IsNullOrEmpty(renderNote)) EditorGUILayout.LabelField("※ " + renderNote, EditorStyles.wordWrappedMiniLabel);
            if (null == cells || renderedKey != CompareKey(_row))
            {
                EditorGUILayout.LabelField("화면을 그리는 중…", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            if (true == pendingCellLanguage.HasValue)
            {
                int _index = cells.FindIndex(c => c.language == pendingCellLanguage.Value);
                if (_index >= 0) selectedCell = _index;
                pendingCellLanguage = null;
            }

            float _viewHeight = Mathf.Max(140f, position.height - TOOLBAR_HEIGHT - DETAIL_HEIGHT - 120f);
            DrawCompareGrid(_row, _width - 4f, _viewHeight);

            selectedCell = Mathf.Clamp(selectedCell, 0, Mathf.Max(0, cells.Count - 1));
            if (cells.Count > 0) DrawCellDetail(_row, cells[selectedCell]);
        }

        private void DrawItemHeader(Row _row)
        {
            if (null != _row.item)
            {
                EditorGUILayout.LabelField(_row.item.entryId, titleStyle);
                EditorGUILayout.LabelField($"칸: {SlotLabel(_row)}   ·   그리는 화면: {ContextName(_row)}   ·   출처: {_row.item.source}   ·   {settings.Resolution.label}", EditorStyles.miniLabel);
            }
            else
            {
                EditorGUILayout.LabelField($"(문구 미연결) \"{OneLine(_row.slot.defaultText, 60)}\"", titleStyle);
                EditorGUILayout.LabelField($"칸: {SlotLabel(_row)}   ·   이 칸에 들어갈 문구를 연결해야 검수할 수 있습니다. 번역하지 않는 텍스트(숫자·이름·고정 영문)라면 '번역 대상 아님'으로 표시하세요.", EditorStyles.wordWrappedMiniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawRowButtons(_row);
                GUILayout.FlexibleSpace();
                fullView = GUILayout.Toggle(fullView, new GUIContent("전체 화면", "칸 주변만 잘라 보지 않고 UI 전체를 봅니다."), "Button", GUILayout.Width(70f));

                using (new EditorGUI.DisabledScope(null == _row.item || null == cells))
                {
                    if (GUILayout.Button(new GUIContent("남은 언어 모두 OK", "아직 판정하지 않은(또는 번역이 바뀐) 언어를 모두 OK로 표시합니다. (Enter)"), GUILayout.Width(120f)))
                    {
                        MarkRemainingOk(_row);
                    }
                }
                if (GUILayout.Button("◀", GUILayout.Width(28f))) StepRow(-1);
                if (GUILayout.Button("▶", GUILayout.Width(28f))) StepRow(+1);
            }
        }

        private void DrawRowButtons(Row _row)
        {
            if (GUILayout.Button(new GUIContent(null != _row.item ? "이 칸에 문구 추가…" : "문구 연결…", "이 텍스트 칸에 들어갈 수 있는 문구를 검색해서 여러 개 한꺼번에 추가합니다."), GUILayout.Width(120f)))
            {
                OpenKeyPicker(false);
            }

            if (null != _row.slot)
            {
                if (GUILayout.Button(true == _row.slot.ignored ? "번역 대상 아님 해제" : "번역 대상 아님", GUILayout.Width(120f)))
                {
                    LocQAReviewData.Shared.SetSlotIgnored(_row.slot, false == _row.slot.ignored);
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent("목록에서 빼기", "이 칸에 실제로는 나오지 않는 문구라면 검수 목록에서 뺍니다. (판정 기록도 지워집니다)"), GUILayout.Width(90f)))
                {
                    if (EditorUtility.DisplayDialog("검수 목록에서 빼기", $"'{_row.item.entryId}'를 이 칸의 검수 목록에서 뺄까요?\n이 항목의 판정 기록도 함께 지워집니다.", "빼기", "취소"))
                    {
                        LocQAReviewData.Shared.Remove(_row.item);
                        selectedRowKey = null;
                        GUIUtility.ExitGUI();
                    }
                }
            }

            if (GUILayout.Button("프리팹 열기", GUILayout.Width(76f))) OpenContextPrefab(_row);
        }

        private void DrawCompareGrid(Row _row, float _width, float _viewHeight)
        {
            Texture2D _sample = null;
            for (int i = 0; i < cells.Count && null == _sample; i++) _sample = fullView ? cells[i].full : cells[i].crop;
            float _aspect = null != _sample ? (float)_sample.height / Mathf.Max(1, _sample.width) : 0.5625f;

            CellLayout _l = ComputeLayout(cells.Count, _width - 16f, _viewHeight, _aspect, 46f);
            gridColumns = ELayout.Grid == layout ? _l.columns : 1;
            string[] _hashes = null != _row.item ? Hashes(_row.entry) : null;

            Rect _view = GUILayoutUtility.GetRect(_width, _viewHeight, GUILayout.ExpandWidth(true));
            if (true == scrollToSelection)
            {
                ScrollTo(_l, selectedCell, new Vector2(_view.width - 16f, _view.height - 16f));
                scrollToSelection = false;
            }
            HandleHorizontalWheel(_view);

            gridScroll = GUI.BeginScrollView(_view, gridScroll, new Rect(0f, 0f, _l.contentW, _l.contentH));
            for (int _index = 0; _index < cells.Count; _index++)
            {
                Rect _r = CellRect(_l, _index);
                if (false == IsInView(_r, _view)) continue;

                LocQAReviewCell _cell = cells[_index];
                LocQAReviewData.EffectiveState _state = null != _row.item
                    ? LocQAReviewData.Shared.GetEffective(_row.item, _cell.language, _hashes[(int)_cell.language])
                    : LocQAReviewData.EffectiveState.Todo;

                string _title = $"{_cell.language}  {LocQALanguages.Name(_cell.language)}";
                if (true == DrawCell(_r, _l.imageH, _title, _cell, _state, _index == selectedCell, null != _row.item, out LocQAReviewState _clicked))
                {
                    selectedCell = _index;
                    if (LocQAReviewState.None != _clicked) SetState(_row, _cell.language, _clicked);
                }

                if (true == DoubleClicked(_r) && null != _cell.full)
                {
                    LocQAImageWindow.Show(_cell.full, true, _cell.fullBoxUv, _cell.fullGlyphUv, $"{LocQALanguages.Name(_cell.language)} · {_row.label}");
                }
            }
            GUI.EndScrollView();
        }

        // //칸 배치 (바둑판 / 가로 스크롤 / 세로 스크롤)
        private struct CellLayout
        {
            public int columns;
            public float cellW;
            public float cellH;
            public float imageH;
            public float contentW;
            public float contentH;
        }

        /// <summary>
        /// 칸 크기와 전체 내용 크기를 정한다. 가로 스크롤은 칸 높이를 보이는 높이에, 세로 스크롤은 칸 폭을 보이는 폭에 꽉 채운다.
        /// _extra는 그림 위아래의 제목·버튼 줄 높이다.
        /// </summary>
        private CellLayout ComputeLayout(int _count, float _width, float _viewHeight, float _aspect, float _extra)
        {
            CellLayout _l = new CellLayout();
            _aspect = Mathf.Max(0.05f, _aspect);
            switch (layout)
            {
                case ELayout.Horizontal:
                    _l.columns = Mathf.Max(1, _count);
                    _l.imageH = Mathf.Max(60f, _viewHeight - _extra - 22f);
                    _l.cellW = Mathf.Max(220f, _l.imageH / _aspect + 10f);
                    _l.cellH = _l.imageH + _extra;
                    _l.contentW = _count * _l.cellW;
                    _l.contentH = _l.cellH;
                    break;
                case ELayout.Vertical:
                    _l.columns = 1;
                    _l.cellW = Mathf.Max(220f, _width);
                    _l.imageH = Mathf.Clamp((_l.cellW - 10f) * _aspect, 60f, Mathf.Max(60f, _viewHeight - _extra - 6f));
                    _l.cellH = _l.imageH + _extra;
                    _l.contentW = _l.cellW;
                    _l.contentH = _count * _l.cellH;
                    break;
                default:
                    _l.columns = Mathf.Clamp((int)(_width / cellWidth), 1, 8);
                    _l.cellW = _width / _l.columns;
                    _l.imageH = Mathf.Clamp((_l.cellW - 10f) * _aspect, 50f, 1400f);
                    _l.cellH = _l.imageH + _extra;
                    _l.contentW = _width;
                    _l.contentH = Mathf.Ceil((float)_count / _l.columns) * _l.cellH;
                    break;
            }
            return _l;
        }

        private static Rect CellRect(CellLayout _l, int _index)
        {
            int _column = _index % _l.columns;
            int _line = _index / _l.columns;
            return new Rect(_column * _l.cellW + 3f, _line * _l.cellH + 3f, _l.cellW - 6f, _l.cellH - 6f);
        }

        /// <summary>스크롤 영역에 보이는 칸인지. (보이지 않는 칸은 그리지 않아 수백 개 문구에서도 가볍게)</summary>
        private bool IsInView(Rect _cell, Rect _view)
        {
            return _cell.yMax >= gridScroll.y - 4f && _cell.y <= gridScroll.y + _view.height + 4f
                && _cell.xMax >= gridScroll.x - 4f && _cell.x <= gridScroll.x + _view.width + 4f;
        }

        private void ScrollTo(CellLayout _l, int _index, Vector2 _viewSize)
        {
            if (_index < 0) return;
            Rect _r = CellRect(_l, _index);
            if (_r.y < gridScroll.y) gridScroll.y = _r.y - 3f;
            else if (_r.yMax > gridScroll.y + _viewSize.y) gridScroll.y = _r.yMax - _viewSize.y + 3f;
            if (_r.x < gridScroll.x) gridScroll.x = _r.x - 3f;
            else if (_r.xMax > gridScroll.x + _viewSize.x) gridScroll.x = _r.xMax - _viewSize.x + 3f;
            gridScroll = Vector2.Max(gridScroll, Vector2.zero);
        }

        /// <summary>가로 스크롤 배치에서는 마우스 휠을 가로 이동으로 쓴다.</summary>
        private void HandleHorizontalWheel(Rect _view)
        {
            Event _e = Event.current;
            if (ELayout.Horizontal != layout || EventType.ScrollWheel != _e.type || false == _view.Contains(_e.mousePosition)) return;
            gridScroll.x = Mathf.Max(0f, gridScroll.x + (_e.delta.y + _e.delta.x) * 24f);
            _e.Use();
            Repaint();
        }

        // //오른쪽 (언어별 검수)
        private void DrawLanguageGrid(float _width)
        {
            Language _lang = ReviewLanguage;
            if (0 == rows.Count)
            {
                EditorGUILayout.HelpBox(string.IsNullOrEmpty(selectedContext) && false == contexts.Exists(c => c.guid == selectedContext)
                    ? "왼쪽에서 화면을 고르면 그 화면의 모든 문구가 " + LocQALanguages.Name(_lang) + "로 바둑판처럼 나옵니다."
                    : "표시할 문구가 없습니다. (필터를 '전체'로 바꿔 보세요)", MessageType.Info);
                return;
            }

            int _todo = 0, _ok = 0, _ng = 0, _rendered = 0;
            for (int i = 0; i < rows.Count; i++)
            {
                if (true == languageCells.ContainsKey(LanguageKey(rows[i], _lang)) || true == languageErrors.ContainsKey(LanguageKey(rows[i], _lang))) _rendered++;
                if (null == rows[i].item) continue;
                LocQAReviewData.EffectiveState _s = LocQAReviewData.Shared.GetEffective(rows[i].item, _lang, Hashes(rows[i].entry)[(int)_lang]);
                if (LocQAReviewData.EffectiveState.Ok == _s) _ok++;
                else if (LocQAReviewData.EffectiveState.Ng == _s) _ng++;
                else _todo++;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField($"{LocQALanguages.Name(_lang)} · 문구 {rows.Count}개 · OK {_ok} · NG {_ng} · 남음 {_todo}" + (_rendered < rows.Count ? $"   (그리는 중 {_rendered}/{rows.Count})" : string.Empty), EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("◀ 이전 언어", "PageUp"), GUILayout.Width(80f))) StepLanguage(-1);
                if (GUILayout.Button(new GUIContent("다음 언어 ▶", "PageDown"), GUILayout.Width(80f))) StepLanguage(+1);
            }
            EditorGUILayout.LabelField("단축키: ←→↑↓ 칸 이동 · 1/O OK · 2/X NG · 0 지우기 · Space/Enter OK 후 다음 칸 · PageUp/PageDown 이전/다음 언어 · 더블클릭 크게 보기", EditorStyles.miniLabel);

            float _viewHeight = Mathf.Max(140f, position.height - TOOLBAR_HEIGHT - DETAIL_HEIGHT - 60f);
            float _gridWidth = _width - 4f;

            Texture2D _sample = null;
            foreach (KeyValuePair<string, LocQAReviewCell> _pair in languageCells)
            {
                _sample = fullView && null != _pair.Value.full ? _pair.Value.full : _pair.Value.crop;
                if (null != _sample) break;
            }
            float _aspect = null != _sample ? (float)_sample.height / Mathf.Max(1, _sample.width) : 0.42f;
            if (ELayout.Grid == layout && false == fullView) _aspect = 0.42f;   // 바둑판에서는 칸 높이를 고르게 맞춘다

            CellLayout _l = ComputeLayout(rows.Count, _gridWidth - 16f, _viewHeight, _aspect, 62f);
            gridColumns = ELayout.Grid == layout ? _l.columns : 1;
            gridCellHeight = _l.cellH;

            Rect _view = GUILayoutUtility.GetRect(_gridWidth, _viewHeight, GUILayout.ExpandWidth(true));
            int _selectedIndex = rows.FindIndex(r => r.key == selectedRowKey);
            if (true == scrollToSelection && _selectedIndex >= 0)
            {
                ScrollTo(_l, _selectedIndex, new Vector2(_view.width - 16f, _view.height - 16f));
                scrollToSelection = false;
            }
            HandleHorizontalWheel(_view);
            firstVisibleRow = Mathf.Clamp(ELayout.Horizontal == layout ? (int)(gridScroll.x / Mathf.Max(1f, _l.cellW)) : (int)(gridScroll.y / Mathf.Max(1f, _l.cellH)) * Mathf.Max(1, _l.columns), 0, Mathf.Max(0, rows.Count - 1));

            gridScroll = GUI.BeginScrollView(_view, gridScroll, new Rect(0f, 0f, _l.contentW, _l.contentH));
            for (int _index = 0; _index < rows.Count; _index++)
            {
                Rect _r = CellRect(_l, _index);
                if (false == IsInView(_r, _view)) continue;

                Row _row = rows[_index];
                string _key = LanguageKey(_row, _lang);
                languageCells.TryGetValue(_key, out LocQAReviewCell _cell);
                LocQAReviewData.EffectiveState _state = null != _row.item
                    ? LocQAReviewData.Shared.GetEffective(_row.item, _lang, Hashes(_row.entry)[(int)_lang])
                    : LocQAReviewData.EffectiveState.Todo;

                if (null == _cell)
                {
                    if (EventType.Repaint == Event.current.type)
                    {
                        EditorGUI.DrawRect(_r, new Color(0f, 0f, 0f, 0.2f));
                        DrawOutline(_r, _row.key == selectedRowKey ? ColorSelected : StateColor(_state), _row.key == selectedRowKey ? 3f : 1f);
                    }
                    languageErrors.TryGetValue(_key, out string _error);
                    GUI.Label(new Rect(_r.x + 6f, _r.y + 3f, _r.width - 12f, 16f), new GUIContent(_row.label, _row.label), cellHeader);
                    GUI.Label(new Rect(_r.x + 6f, _r.y + 24f, _r.width - 12f, 40f), _error ?? "그리는 중…", EditorStyles.wordWrappedMiniLabel);
                    GUI.Label(new Rect(_r.x + 6f, _r.y + 20f + _l.imageH + 2f, _r.width - 12f, 14f), new GUIContent(_row.group, SlotTooltip(_row)), EditorStyles.miniLabel);
                    if (true == Clicked(_r)) SelectRow(_row.key);
                    continue;
                }

                if (true == DrawCell(_r, _l.imageH, _row.label, _cell, _state, _row.key == selectedRowKey, null != _row.item, out LocQAReviewState _clicked))
                {
                    SelectRow(_row.key);
                    if (LocQAReviewState.None != _clicked) SetState(_row, _lang, _clicked);
                }
                // 어느 화면·칸의 문구인지. (칸 배경을 그린 뒤에 써야 가려지지 않는다)
                GUI.Label(new Rect(_r.x + 6f, _r.y + 20f + _l.imageH + 2f, _r.width - 12f, 14f), new GUIContent(_row.group, SlotTooltip(_row)), EditorStyles.miniLabel);

                if (true == DoubleClicked(_r)) ShowLarge(_row, _lang);
            }
            GUI.EndScrollView();

            Row _current = CurrentRow();
            if (null != _current)
            {
                languageCells.TryGetValue(LanguageKey(_current, _lang), out LocQAReviewCell _currentCell);
                DrawCellDetail(_current, _currentCell, _lang);
            }
        }

        /// <summary>언어별 검수에서 화면에 보이는(앞쪽) 문구부터 조금씩 그린다. 그렸으면 true.</summary>
        private bool RenderLanguageCellsStep()
        {
            if (0 == rows.Count) return false;

            Language _lang = ReviewLanguage;
            if (null != session && sessionCanvas != settings.CanvasSize) DisposeSession();

            // 지금 보이는 줄부터 그리고, 그다음 나머지를 차례로.
            int _first = Mathf.Clamp(firstVisibleRow, 0, rows.Count - 1);
            double _start = EditorApplication.timeSinceStartup;
            bool _any = false;

            for (int n = 0; n < rows.Count; n++)
            {
                Row _row = rows[(_first + n) % rows.Count];
                string _key = LanguageKey(_row, _lang);
                if (true == languageCells.ContainsKey(_key) || true == languageErrors.ContainsKey(_key)) continue;

                if (null == session)
                {
                    session = new LocQARenderSession(settings);
                    sessionCanvas = settings.CanvasSize;
                }

                try
                {
                    LocQARenderRequest _request = null != _row.item ? LocQARenderRequest.From(_row.item, _row.entry) : LocQARenderRequest.From(_row.slot);
                    List<LocQAReviewCell> _result = session.Render(_request, new List<Language> { _lang }, false, fullView, out string _error, out string _note);
                    if (_result.Count > 0) languageCells[_key] = _result[0];
                    else languageErrors[_key] = _error ?? "그리지 못했습니다.";
                }
                catch (Exception _e)
                {
                    Debug.LogException(_e);
                    languageErrors[_key] = "오류: " + _e.Message;
                    DisposeSession();
                }
                _any = true;

                if (EditorApplication.timeSinceStartup - _start > RENDER_BUDGET) break;
            }

            if (languageCells.Count > LANGUAGE_CACHE_LIMIT) TrimLanguageCells(_lang);
            return _any;
        }

        private void TrimLanguageCells(Language _lang)
        {
            HashSet<string> _keep = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rows.Count; i++) _keep.Add(LanguageKey(rows[i], _lang));

            List<string> _remove = new List<string>();
            foreach (KeyValuePair<string, LocQAReviewCell> _pair in languageCells)
            {
                if (false == _keep.Contains(_pair.Key)) _remove.Add(_pair.Key);
            }
            for (int i = 0; i < _remove.Count; i++)
            {
                languageCells[_remove[i]].Release();
                languageCells.Remove(_remove[i]);
            }
        }

        private void ShowLarge(Row _row, Language _lang)
        {
            LocQARenderRequest _request = null != _row.item ? LocQARenderRequest.From(_row.item, _row.entry) : LocQARenderRequest.From(_row.slot);
            List<LocQAReviewCell> _result = LocQAReviewRenderer.Render(_request, new List<Language> { _lang }, settings, out string _error, out string _note);
            if (_result.Count > 0 && null != _result[0].full)
            {
                LocQAImageWindow.Show(_result[0].full, true, _result[0].fullBoxUv, _result[0].fullGlyphUv, $"{LocQALanguages.Name(_lang)} · {_row.label}");
            }
            for (int i = 0; i < _result.Count; i++) _result[i].Release();
        }

        private void StepLanguage(int _delta)
        {
            int _count = LocQALanguages.All.Length;
            reviewLanguage = ((reviewLanguage + _delta) % _count + _count) % _count;
            builtSignature = null;
            Repaint();
        }

        private string LanguageKey(Row _row, Language _lang)
        {
            StringBuilder _sb = new StringBuilder(_row.key.Length + 48);
            _sb.Append(_row.key).Append('#').Append((int)_lang).Append('#').Append(settings.ResolutionIndex).Append(true == fullView ? "#F" : string.Empty);
            if (null != _row.item) _sb.Append('#').Append(_row.item.contextGuid).Append(_row.item.contextPath).Append('#').Append(_row.item.template).Append('#').Append(_row.item.samples);
            return _sb.ToString();
        }

        // //칸 그리기 (두 보기 공용)
        /// <returns>칸을 클릭했으면 true. OK/NG 버튼을 눌렀으면 _clicked에 그 판정이 담긴다.</returns>
        private bool DrawCell(Rect _r, float _imageHeight, string _title, LocQAReviewCell _cell, LocQAReviewData.EffectiveState _state, bool _selected, bool _canMark, out LocQAReviewState _clicked)
        {
            _clicked = LocQAReviewState.None;
            Event _e = Event.current;

            if (EventType.Repaint == _e.type)
            {
                EditorGUI.DrawRect(_r, new Color(0f, 0f, 0f, 0.25f));
                DrawOutline(_r, true == _selected ? ColorSelected : StateColor(_state), true == _selected ? 3f : 2f);
            }

            GUI.Label(new Rect(_r.x + 6f, _r.y + 3f, _r.width - 80f, 16f), new GUIContent(_title, _title), cellHeader);
            Color _prevColor = GUI.contentColor;
            GUI.contentColor = StateColor(_state);
            GUI.Label(new Rect(_r.xMax - 70f, _r.y + 3f, 64f, 16f), StateLabel(_state), EditorStyles.miniBoldLabel);
            GUI.contentColor = _prevColor;

            if (true == _cell.fallback)
            {
                GUI.contentColor = ColorChanged;
                GUI.Label(new Rect(_r.xMax - 140f, _r.y + 3f, 66f, 16f), new GUIContent("번역 없음", "이 언어 번역이 비어 있어 영어(중남미 스페인어는 스페인어) 문구가 대신 나옵니다."), EditorStyles.miniBoldLabel);
                GUI.contentColor = _prevColor;
            }

            Rect _imageArea = new Rect(_r.x + 4f, _r.y + 20f, _r.width - 8f, _imageHeight);
            Texture2D _tex = fullView && null != _cell.full ? _cell.full : _cell.crop;
            Rect _box = fullView && null != _cell.full ? _cell.fullBoxUv : _cell.cropBoxUv;
            Rect _glyph = fullView && null != _cell.full ? _cell.fullGlyphUv : _cell.cropGlyphUv;
            if (null != _tex)
            {
                Rect _drawn = DrawImage(_imageArea, _tex);
                if (EventType.Repaint == _e.type)
                {
                    DrawOutline(UvToGui(_drawn, _box), new Color(1f, 0.92f, 0.2f, 0.55f), 1f);
                    if (true == HasProblem(_cell)) DrawOutline(UvToGui(_drawn, _glyph), ColorNg, 2f);
                }
            }

            Rect _footer = new Rect(_r.x + 4f, _r.yMax - 22f, _r.width - 8f, 18f);
            string _auto = AutoSummary(_cell);
            if (false == string.IsNullOrEmpty(_auto))
            {
                GUI.contentColor = ColorChanged;
                GUI.Label(new Rect(_footer.x, _footer.y, _footer.width - 92f, 18f), new GUIContent("자동: " + _auto, _auto), EditorStyles.miniLabel);
                GUI.contentColor = _prevColor;
            }

            if (true == _canMark)
            {
                if (GUI.Button(new Rect(_footer.xMax - 88f, _footer.y, 42f, 18f), "OK", EditorStyles.miniButtonLeft)) _clicked = LocQAReviewState.Ok;
                if (GUI.Button(new Rect(_footer.xMax - 46f, _footer.y, 42f, 18f), "NG", EditorStyles.miniButtonRight)) _clicked = LocQAReviewState.Ng;
                if (LocQAReviewState.None != _clicked) return true;
            }

            if (EventType.MouseDown == _e.type && 1 == _e.clickCount && _r.Contains(_e.mousePosition) && false == _footer.Contains(_e.mousePosition))
            {
                GUIUtility.keyboardControl = 0;
                _e.Use();
                Repaint();
                return true;
            }
            return false;
        }

        private static bool Clicked(Rect _r)
        {
            Event _e = Event.current;
            if (EventType.MouseDown != _e.type || false == _r.Contains(_e.mousePosition)) return false;
            GUIUtility.keyboardControl = 0;
            _e.Use();
            return true;
        }

        private static bool DoubleClicked(Rect _r)
        {
            Event _e = Event.current;
            if (EventType.MouseDown != _e.type || 2 != _e.clickCount || false == _r.Contains(_e.mousePosition)) return false;
            _e.Use();
            return true;
        }

        private void DrawCellDetail(Row _row, LocQAReviewCell _cell, Language? _languageOverride = null)
        {
            Language _lang = _languageOverride ?? (null != _cell ? _cell.language : ReviewLanguage);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(DETAIL_HEIGHT)))
            {
                string _title = $"{LocQALanguages.Name(_lang)} ({_lang})" + (null != _row.item ? "  ·  " + _row.item.entryId : "  ·  (문구 미연결)");
                if (null != _cell && true == _cell.fallback) _title += "  — 번역이 없어 대신 표시되는 문구";
                EditorGUILayout.LabelField(_title, EditorStyles.boldLabel);

                string _text = null != _cell ? _cell.text : (null != _row.entry ? LocQALanguages.Resolve(_row.entry.data, _lang) : _row.slot?.defaultText);
                EditorGUILayout.SelectableLabel(_text ?? string.Empty, smallWrap, GUILayout.Height(Mathf.Min(44f, smallWrap.CalcHeight(new GUIContent(_text ?? string.Empty), position.width - LEFT_WIDTH - MIDDLE_WIDTH - 40f) + 4f)));

                if (EMode.Language == mode && null != _row.entry && Language.KR != _lang)
                {
                    EditorGUILayout.LabelField(new GUIContent("원문: " + OneLine(_row.entry.data.kr, 200), _row.entry.data.kr), EditorStyles.miniLabel);
                }

                if (null != _cell)
                {
                    if (true == _cell.invisible)
                    {
                        EditorGUILayout.HelpBox("화면에 안 보임: 스크롤·가림막 처리를 해도 이 칸의 글자가 그려지지 않았습니다. 이 그림으로는 판정할 수 없으니 게임에서 직접 확인하세요.", MessageType.Warning);
                    }
                    for (int i = 0; i < _cell.findings.Count; i++)
                    {
                        if (LocQASeverity.Info == LocQAKinds.Severity(_cell.findings[i].Key)) continue;
                        EditorGUILayout.LabelField($"자동 검사 · {LocQAKinds.Name(_cell.findings[i].Key)}: {_cell.findings[i].Value}", EditorStyles.wordWrappedMiniLabel);
                    }
                }

                if (EMode.Language == mode)
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        DrawRowButtons(_row);
                        GUILayout.FlexibleSpace();
                    }
                }

                if (null == _row.item) return;

                using (new EditorGUILayout.HorizontalScope())
                {
                    string _template = _row.item.template ?? string.Empty;
                    EditorGUILayout.LabelField(new GUIContent("표시 형식", "코드가 문구에 숫자·태그를 붙여 보여주는 칸이면 그 모양을 적습니다. {text} 자리에 문구가 들어갑니다. 예) {text} : 3 / 5   비우면 문구만 넣습니다."), GUILayout.Width(56f));
                    string _newTemplate = EditorGUILayout.DelayedTextField(_template);
                    if (_newTemplate != _template) LocQAReviewData.Shared.SetTemplate(_row.item, _newTemplate);

                    string _samples = _row.item.samples ?? string.Empty;
                    EditorGUILayout.LabelField(new GUIContent("견본 값", "문구의 {0}, {1} … 자리에 넣을 값입니다. '|'로 구분합니다. 예) 5   또는   12|30|42   비우면 설정의 기본 견본 값을 넣습니다."), GUILayout.Width(44f));
                    string _newSamples = EditorGUILayout.DelayedTextField(_samples, GUILayout.Width(110f));
                    if (_newSamples != _samples) LocQAReviewData.Shared.SetSamples(_row.item, _newSamples);
                }

                LocQAReviewMark _mark = LocQAReviewData.Shared.GetMark(_row.item, _lang);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("NG 사유", GUILayout.Width(56f));
                    string _memo = null != _mark ? _mark.memo : string.Empty;
                    EditorGUI.BeginChangeCheck();
                    string _newMemo = EditorGUILayout.DelayedTextField(_memo ?? string.Empty);
                    if (EditorGUI.EndChangeCheck())
                    {
                        LocQAReviewState _state = null != _mark ? (LocQAReviewState)_mark.state : LocQAReviewState.None;
                        if (false == string.IsNullOrEmpty(_newMemo)) _state = LocQAReviewState.Ng;
                        LocQAReviewData.Shared.SetMark(_row.item, _lang, _state, Hashes(_row.entry)[(int)_lang], _newMemo);
                    }
                    if (null != _mark && false == string.IsNullOrEmpty(_mark.by))
                    {
                        GUILayout.Label($"{_mark.by} · {_mark.at}", EditorStyles.miniLabel, GUILayout.Width(170f));
                    }
                }
            }
        }

        // //판정
        private void SetState(Row _row, Language _lang, LocQAReviewState _state)
        {
            if (null == _row || null == _row.item) return;
            LocQAReviewMark _mark = LocQAReviewData.Shared.GetMark(_row.item, _lang);
            string _memo = LocQAReviewState.Ok == _state ? string.Empty : (null != _mark ? _mark.memo : string.Empty);
            LocQAReviewData.Shared.SetMark(_row.item, _lang, _state, Hashes(_row.entry)[(int)_lang], _memo);
            Repaint();
        }

        private void MarkRemainingOk(Row _row)
        {
            if (null == _row || null == _row.item || null == cells) return;
            string[] _hashes = Hashes(_row.entry);
            for (int i = 0; i < cells.Count; i++)
            {
                Language _lang = cells[i].language;
                LocQAReviewData.EffectiveState _state = LocQAReviewData.Shared.GetEffective(_row.item, _lang, _hashes[(int)_lang]);
                if (LocQAReviewData.EffectiveState.Todo == _state || LocQAReviewData.EffectiveState.Changed == _state)
                {
                    LocQAReviewData.Shared.SetMark(_row.item, _lang, LocQAReviewState.Ok, _hashes[(int)_lang], string.Empty);
                }
            }
        }

        private void HandleKeyboard()
        {
            Event _e = Event.current;
            if (EventType.KeyDown != _e.type || true == EditorGUIUtility.editingTextField) return;
            if (0 != GUIUtility.keyboardControl) return;

            if (EMode.Language == mode)
            {
                if (true == HandleLanguageKeys(_e.keyCode))
                {
                    _e.Use();
                    Repaint();
                }
                return;
            }

            Row _row = CurrentRow();
            int _count = null != cells ? cells.Count : 0;
            int _columns = Mathf.Max(1, gridColumns);

            switch (_e.keyCode)
            {
                case KeyCode.LeftArrow: selectedCell = Mathf.Max(0, selectedCell - 1); break;
                case KeyCode.RightArrow: selectedCell = Mathf.Min(Mathf.Max(0, _count - 1), selectedCell + 1); break;
                case KeyCode.UpArrow: selectedCell = Mathf.Max(0, selectedCell - _columns); break;
                case KeyCode.DownArrow: selectedCell = Mathf.Min(Mathf.Max(0, _count - 1), selectedCell + _columns); break;
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                case KeyCode.O:
                    if (_count > 0) SetState(_row, cells[selectedCell].language, LocQAReviewState.Ok);
                    break;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                case KeyCode.X:
                    if (_count > 0) SetState(_row, cells[selectedCell].language, LocQAReviewState.Ng);
                    break;
                case KeyCode.Alpha0:
                case KeyCode.Keypad0:
                case KeyCode.Delete:
                    if (_count > 0) SetState(_row, cells[selectedCell].language, LocQAReviewState.None);
                    break;
                case KeyCode.Space:
                    if (_count > 0)
                    {
                        SetState(_row, cells[selectedCell].language, LocQAReviewState.Ok);
                        selectedCell = Mathf.Min(_count - 1, selectedCell + 1);
                    }
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    MarkRemainingOk(_row);
                    StepRow(+1);
                    break;
                case KeyCode.PageDown: StepRow(+1); break;
                case KeyCode.PageUp: StepRow(-1); break;
                default: return;
            }
            scrollToSelection = true;
            _e.Use();
            Repaint();
        }

        private bool HandleLanguageKeys(KeyCode _key)
        {
            int _index = rows.FindIndex(r => r.key == selectedRowKey);
            int _columns = Mathf.Max(1, gridColumns);
            Row _row = _index >= 0 ? rows[_index] : null;

            switch (_key)
            {
                case KeyCode.LeftArrow: MoveTo(_index - 1); return true;
                case KeyCode.RightArrow: MoveTo(_index + 1); return true;
                case KeyCode.UpArrow: MoveTo(_index - _columns); return true;
                case KeyCode.DownArrow: MoveTo(_index + _columns); return true;
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                case KeyCode.O:
                    SetState(_row, ReviewLanguage, LocQAReviewState.Ok);
                    return true;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                case KeyCode.X:
                    SetState(_row, ReviewLanguage, LocQAReviewState.Ng);
                    return true;
                case KeyCode.Alpha0:
                case KeyCode.Keypad0:
                case KeyCode.Delete:
                    SetState(_row, ReviewLanguage, LocQAReviewState.None);
                    return true;
                case KeyCode.Space:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    SetState(_row, ReviewLanguage, LocQAReviewState.Ok);
                    MoveTo(_index + 1);
                    return true;
                case KeyCode.PageDown: StepLanguage(+1); return true;
                case KeyCode.PageUp: StepLanguage(-1); return true;
                default: return false;
            }
        }

        private void MoveTo(int _index)
        {
            if (0 == rows.Count) return;
            _index = Mathf.Clamp(_index, 0, rows.Count - 1);
            selectedRowKey = rows[_index].key;
            scrollToSelection = true;
        }

        private void StepRow(int _delta)
        {
            if (0 == rows.Count) return;
            int _index = rows.FindIndex(r => r.key == selectedRowKey);
            int _next = Mathf.Clamp(_index + _delta, 0, rows.Count - 1);
            if (_index < 0) _next = 0;
            SelectRow(rows[_next].key);
        }

        // //렌더링 (문구별 비교)
        private void RenderCompareIfNeeded()
        {
            Row _current = CurrentRow();
            if (null == _current) return;

            string _key = CompareKey(_current);
            if (_key == renderedKey) return;

            ReleaseCells();
            renderedKey = _key;
            LocQARenderRequest _request = null != _current.item ? LocQARenderRequest.From(_current.item, _current.entry) : LocQARenderRequest.From(_current.slot);

            try
            {
                cells = LocQAReviewRenderer.Render(_request, languages, settings, out renderError, out renderNote);
            }
            catch (Exception _ex)
            {
                Debug.LogException(_ex);
                renderError = "화면을 그리다 오류가 났습니다: " + _ex.Message;
                cells = new List<LocQAReviewCell>();
            }
        }

        private string CompareKey(Row _row)
        {
            StringBuilder _sb = new StringBuilder(_row.key.Length + 64);
            _sb.Append(_row.key).Append('#');
            for (int i = 0; i < languages.Count; i++) _sb.Append((int)languages[i]).Append(',');
            if (null != _row.item) _sb.Append(_row.item.contextGuid).Append(_row.item.contextPath).Append('#').Append(_row.item.template).Append('#').Append(_row.item.samples);
            _sb.Append('#').Append(settings.ResolutionIndex);
            return _sb.ToString();
        }

        private void ReleaseCells()
        {
            if (null != cells)
            {
                for (int i = 0; i < cells.Count; i++) cells[i].Release();
            }
            cells = null;
            renderedKey = null;
            renderError = null;
            renderNote = null;
        }

        private void ReleaseLanguageCells()
        {
            foreach (KeyValuePair<string, LocQAReviewCell> _pair in languageCells) _pair.Value.Release();
            languageCells.Clear();
            languageErrors.Clear();
        }

        private void DisposeSession()
        {
            if (null == session) return;
            try { session.Dispose(); }
            catch (Exception _e) { Debug.LogException(_e); }
            session = null;
        }

        // //목록 구성
        private void RebuildIfNeeded()
        {
            LocQAReviewData _data = LocQAReviewData.Shared;
            languages = settings.SelectedLanguages();
            string _signature = $"{_data.Version}|{_data.Items.Count}|{_data.Slots.Count}|{settings.languageMask}|{filter}|{mode}|{reviewLanguage}|{selectedContext}|{rowSearch}|{selectedRowKey}";
            if (_signature == builtSignature) return;

            // 해상도나 데이터 구성이 바뀌어 기존 그림이 맞지 않을 수 있으면 다시 그린다.
            if (null != session && sessionCanvas != settings.CanvasSize)
            {
                ReleaseLanguageCells();
                DisposeSession();
            }
            builtSignature = _signature;

            Dictionary<string, ContextRow> _contexts = new Dictionary<string, ContextRow>(StringComparer.Ordinal);
            HashSet<string> _referenced = new HashSet<string>(StringComparer.Ordinal);
            List<Language> _progressLanguages = EMode.Language == mode ? new List<Language> { ReviewLanguage } : languages;

            for (int i = 0; i < _data.Items.Count; i++)
            {
                LocQAReviewItem _item = _data.Items[i];
                _referenced.Add(_item.entryId);

                string[] _hashes = Hashes(table.Find(_item.entryId));
                int _done = 0;
                int _ng = 0;
                for (int l = 0; l < _progressLanguages.Count; l++)
                {
                    LocQAReviewData.EffectiveState _state = _data.GetEffective(_item, _progressLanguages[l], _hashes[(int)_progressLanguages[l]]);
                    if (LocQAReviewData.EffectiveState.Ok == _state) _done++;
                    else if (LocQAReviewData.EffectiveState.Ng == _state) { _done++; _ng++; }
                }

                // 부품(툴팁 등)은 그것을 쓰는 화면마다 보인다. 판정은 공유되므로 어느 화면에서 해도 같다.
                List<string> _screens = LocQAScreenIndex.ScreensOfSlot(_item.contextGuid, _item.slotGuid, settings.prefabFolder);
                for (int s = 0; s < _screens.Count; s++)
                {
                    ContextRow _c = GetContext(_contexts, _screens[s]);
                    _c.rows++;
                    _c.total += _progressLanguages.Count;
                    _c.done += _done;
                    _c.ng += _ng;
                }
            }

            for (int i = 0; i < _data.Slots.Count; i++)
            {
                LocQAReviewSlot _slot = _data.Slots[i];
                if (true == _data.IsSlotFilled(_slot.SlotKey)) continue;
                List<string> _screens = LocQAScreenIndex.ScreensOfSlot(_slot.contextGuid, _slot.slotGuid, settings.prefabFolder);
                for (int s = 0; s < _screens.Count; s++)
                {
                    ContextRow _c = GetContext(_contexts, _screens[s]);
                    if (false == _slot.ignored) _c.unconnected++;
                }
            }

            contexts.Clear();
            foreach (ContextRow _c in _contexts.Values)
            {
                // 검수할 문구도, 연결이 필요한 칸도 없는 묶음은 보여줄 필요가 없다. (번역 대상 아님 칸만 있는 경우 등)
                if (_c.rows > 0 || _c.unconnected > 0) contexts.Add(_c);
            }
            contexts.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

            unconnectedEntries = 0;
            for (int i = 0; i < table.Entries.Count; i++)
            {
                string _id = table.Entries[i].id;
                if (false == _referenced.Contains(_id) && null == _data.UnusedReason(_id)) unconnectedEntries++;
            }

            BuildRows(_data);
        }

        private void BuildRows(LocQAReviewData _data)
        {
            rows.Clear();
            if (null == selectedContext) return;

            for (int i = 0; i < _data.Items.Count; i++)
            {
                LocQAReviewItem _item = _data.Items[i];
                if (false == LocQAScreenIndex.ScreensOfSlot(_item.contextGuid, _item.slotGuid, settings.prefabFolder).Contains(selectedContext)) continue;

                LocQAEntry _entry = table.Find(_item.entryId);
                Row _row = new Row
                {
                    key = _item.Key,
                    item = _item,
                    entry = _entry,
                    label = _item.entryId,
                    sub = null != _entry ? OneLine(_entry.data.kr, 50) : "(JSON에 없는 키)"
                };
                _row.group = GroupLabel(_row);
                if (false == PassesFilter(_data, _row)) continue;
                rows.Add(_row);
            }

            for (int i = 0; i < _data.Slots.Count; i++)
            {
                LocQAReviewSlot _slot = _data.Slots[i];
                if (true == _data.IsSlotFilled(_slot.SlotKey) || false == LocQAScreenIndex.ScreensOfSlot(_slot.contextGuid, _slot.slotGuid, settings.prefabFolder).Contains(selectedContext)) continue;

                Row _row = new Row
                {
                    key = "slot:" + _slot.SlotKey,
                    slot = _slot,
                    label = "(문구 미연결)",
                    sub = OneLine(_slot.defaultText, 50)
                };
                _row.group = GroupLabel(_row);
                if (false == PassesFilter(_data, _row)) continue;
                rows.Add(_row);
            }

            rows.Sort((a, b) =>
            {
                int _c = string.CompareOrdinal(a.group, b.group);
                return 0 != _c ? _c : string.CompareOrdinal(a.label, b.label);
            });

            // 언어별 검수에서는 고른 칸이 없으면 첫 칸을 고른다. (바로 단축키로 판정할 수 있게)
            if (EMode.Language == mode && rows.Count > 0 && false == rows.Exists(r => r.key == selectedRowKey))
            {
                selectedRowKey = rows[0].key;
            }
        }

        private bool PassesFilter(LocQAReviewData _data, Row _row)
        {
            // 지금 보고 있는 항목은 판정이 끝나도 목록에서 사라지지 않게 한다. (다음 항목으로 넘어가는 기준점)
            if (_row.key == selectedRowKey) return true;

            if (false == string.IsNullOrEmpty(rowSearch)
                && _row.label.IndexOf(rowSearch, StringComparison.OrdinalIgnoreCase) < 0
                && (_row.sub ?? string.Empty).IndexOf(rowSearch, StringComparison.OrdinalIgnoreCase) < 0
                && _row.group.IndexOf(rowSearch, StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (EFilter.All == filter) return true;

            if (null != _row.slot) return EFilter.Todo == filter && false == _row.slot.ignored;

            string[] _hashes = Hashes(_row.entry);
            List<Language> _langs = EMode.Language == mode ? new List<Language> { ReviewLanguage } : languages;
            bool _todo = false;
            bool _ng = false;
            for (int l = 0; l < _langs.Count; l++)
            {
                LocQAReviewData.EffectiveState _state = _data.GetEffective(_row.item, _langs[l], _hashes[(int)_langs[l]]);
                if (LocQAReviewData.EffectiveState.Todo == _state || LocQAReviewData.EffectiveState.Changed == _state) _todo = true;
                if (LocQAReviewData.EffectiveState.Ng == _state) _ng = true;
            }
            return EFilter.Todo == filter ? _todo : _ng;
        }

        private ContextRow GetContext(Dictionary<string, ContextRow> _map, string _guid)
        {
            if (false == _map.TryGetValue(_guid, out ContextRow _c))
            {
                _c = new ContextRow { guid = _guid, name = ScreenName(_guid) };
                _map.Add(_guid, _c);
            }
            return _c;
        }

        public static string ScreenName(string _guid)
        {
            if (string.IsNullOrEmpty(_guid)) return "(화면 미확인 부품)";
            string _path = AssetDatabase.GUIDToAssetPath(_guid);
            return string.IsNullOrEmpty(_path) ? "(사라진 프리팹)" : Path.GetFileNameWithoutExtension(_path);
        }

        private string FirstScreen(string _contextGuid, string _slotGuid)
        {
            List<string> _screens = LocQAScreenIndex.ScreensOfSlot(_contextGuid, _slotGuid, settings.prefabFolder);
            return _screens.Count > 0 ? _screens[0] : LocQAScreenIndex.UNKNOWN_SCREEN;
        }

        private Row CurrentRow()
        {
            if (string.IsNullOrEmpty(selectedRowKey)) return null;
            Row _row = rows.Find(r => r.key == selectedRowKey);
            if (null != _row) return _row;

            LocQAReviewItem _item = LocQAReviewData.Shared.Find(selectedRowKey);
            if (null == _item) return null;
            LocQAEntry _entry = table.Find(_item.entryId);
            _row = new Row { key = _item.Key, item = _item, entry = _entry, label = _item.entryId, sub = null != _entry ? OneLine(_entry.data.kr, 50) : string.Empty };
            _row.group = GroupLabel(_row);
            return _row;
        }

        /// <summary>문구의 언어별 해시 (Language 값을 인덱스로). 판정 당시 문구와 지금 문구가 같은지 볼 때 쓴다.</summary>
        private string[] Hashes(LocQAEntry _entry)
        {
            string _key = null != _entry ? _entry.id : string.Empty;
            if (true == hashCache.TryGetValue(_key, out string[] _cached)) return _cached;

            string[] _hashes = new string[LocQALanguages.All.Length];
            for (int i = 0; i < LocQALanguages.All.Length; i++)
            {
                _hashes[(int)LocQALanguages.All[i]] = null != _entry ? LocQAReviewData.Hash(LocQALanguages.Resolve(_entry.data, LocQALanguages.All[i])) : string.Empty;
            }
            hashCache[_key] = _hashes;
            return _hashes;
        }

        // //문구 추가
        private void OpenKeyPicker(bool _unconnectedOnly)
        {
            Row _row = CurrentRow();
            string _contextName = null != _row ? ContextName(_row) : string.Empty;

            HashSet<string> _referenced = new HashSet<string>(StringComparer.Ordinal);
            List<LocQAReviewItem> _items = LocQAReviewData.Shared.Items;
            for (int i = 0; i < _items.Count; i++) _referenced.Add(_items[i].entryId);

            string _target = null != _row ? $"{_contextName} › {SlotLabel(_row)}" : null;
            LocQAKeyPickerWindow.Show(table, _referenced, _unconnectedOnly, _contextName, _target, _entries =>
            {
                Row _current = CurrentRow();
                if (null == _current) return;

                string _slotGuid = null != _current.item ? _current.item.slotGuid : _current.slot.slotGuid;
                string _slotPath = null != _current.item ? _current.item.slotPath : _current.slot.slotPath;
                string _contextGuid = null != _current.item ? _current.item.contextGuid : _current.slot.contextGuid;
                string _contextPath = null != _current.item ? _current.item.contextPath : _current.slot.contextPath;
                int _depth = null != _current.item ? _current.item.contextDepth : _current.slot.contextDepth;

                string _firstKey = null;
                for (int i = 0; i < _entries.Count; i++)
                {
                    LocQAReviewData.Shared.AddOrImprove(_slotGuid, _slotPath, _contextGuid, _contextPath, _depth, _entries[i].id, "수동");
                    if (null == _firstKey) _firstKey = _slotGuid + "|" + _slotPath + "|" + _entries[i].id;
                }

                // 미연결 칸에 문구를 붙였으면 방금 붙인 첫 문구로 넘어간다.
                if (null != _current.slot && null != _firstKey) SelectRow(_firstKey);
                builtSignature = null;
                Repaint();
            });
        }

        // //기타
        private void OpenContextPrefab(Row _row)
        {
            string _guid = null != _row.item ? _row.item.contextGuid : _row.slot.contextGuid;
            string _path = null != _row.item ? _row.item.contextPath : _row.slot.contextPath;
            GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(_guid));
            if (null == _prefab) return;

            AssetDatabase.OpenAsset(_prefab);
            PrefabStage _stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (null == _stage) return;
            Transform _target = LocQAPaths.Find(_stage.prefabContentsRoot.transform, _path);
            if (null == _target) return;
            Selection.activeGameObject = _target.gameObject;
            EditorGUIUtility.PingObject(_target.gameObject);
        }

        private void ExportNgCsv()
        {
            string _csv = BuildNgCsv(table, out int _count);
            if (0 == _count)
            {
                EditorUtility.DisplayDialog("UI 문구 검수", "NG로 판정한 칸이 없습니다.", "확인");
                return;
            }

            string _file = EditorUtility.SaveFilePanel("NG 목록 내보내기", Path.GetFullPath("."), $"LocalizationReview_NG_{DateTime.Now:yyyyMMdd_HHmm}.csv", "csv");
            if (string.IsNullOrEmpty(_file)) return;
            File.WriteAllText(_file, _csv, new UTF8Encoding(true));
            EditorUtility.RevealInFinder(_file);
        }

        /// <summary>NG 판정 목록 CSV 내용. (엑셀용 BOM은 저장할 때 붙인다)</summary>
        public static string BuildNgCsv(LocQAStringTable _table, out int _count)
        {
            LocQAReviewData _data = LocQAReviewData.Shared;
            StringBuilder _sb = new StringBuilder(4096);
            _sb.AppendLine("화면,칸,키,언어,문구,NG 사유,검수자,시각,판정 뒤 번역이 바뀜");
            _count = 0;

            for (int i = 0; i < _data.Items.Count; i++)
            {
                LocQAReviewItem _item = _data.Items[i];
                LocQAEntry _entry = _table.Find(_item.entryId);
                for (int l = 0; l < LocQALanguages.All.Length; l++)
                {
                    Language _lang = LocQALanguages.All[l];
                    LocQAReviewMark _mark = _data.GetMark(_item, _lang);
                    if (null == _mark || LocQAReviewState.Ng != (LocQAReviewState)_mark.state) continue;

                    string _text = null != _entry ? LocQALanguages.Resolve(_entry.data, _lang) : string.Empty;
                    _sb.Append(Csv(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(_item.contextGuid)))).Append(',')
                        .Append(Csv(_item.contextPath)).Append(',')
                        .Append(Csv(_item.entryId)).Append(',')
                        .Append(Csv(LocQALanguages.Name(_lang))).Append(',')
                        .Append(Csv(_text)).Append(',')
                        .Append(Csv(_mark.memo)).Append(',')
                        .Append(Csv(_mark.by)).Append(',')
                        .Append(Csv(_mark.at)).Append(',')
                        .Append(_mark.hash != LocQAReviewData.Hash(_text) ? "Y" : string.Empty).AppendLine();
                    _count++;
                }
            }
            return _sb.ToString();
        }

        private static string Csv(string _value)
        {
            if (string.IsNullOrEmpty(_value)) return string.Empty;
            return "\"" + _value.Replace("\"", "\"\"") + "\"";
        }

        private static string SlotLabel(Row _row)
        {
            string _guid = null != _row.item ? _row.item.slotGuid : _row.slot.slotGuid;
            string _path = null != _row.item ? _row.item.slotPath : _row.slot.slotPath;
            string _prefab = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(_guid));
            int _slash = string.IsNullOrEmpty(_path) ? -1 : _path.LastIndexOf('/');
            string _leaf = _slash >= 0 ? _path.Substring(_slash + 1) : (string.IsNullOrEmpty(_path) ? "(루트)" : _path);
            string _parent = _slash > 0 ? _path.Substring(0, _slash) : string.Empty;
            int _parentSlash = _parent.LastIndexOf('/');
            if (_parentSlash >= 0) _parent = _parent.Substring(_parentSlash + 1);
            return string.IsNullOrEmpty(_parent) ? $"{_prefab} / {_leaf}" : $"{_prefab} / …{_parent}/{_leaf}";
        }

        private static string ContextName(Row _row)
        {
            string _context = null != _row.item ? _row.item.contextGuid : _row.slot.contextGuid;
            return Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(_context));
        }

        /// <summary>가운데 목록의 묶음 제목: [그리는 화면] 칸</summary>
        private static string GroupLabel(Row _row)
        {
            return $"[{ContextName(_row)}] {SlotLabel(_row)}";
        }

        private static string SlotTooltip(Row _row)
        {
            string _guid = null != _row.item ? _row.item.slotGuid : _row.slot.slotGuid;
            string _path = null != _row.item ? _row.item.slotPath : _row.slot.slotPath;
            return AssetDatabase.GUIDToAssetPath(_guid) + "\n" + _path;
        }

        private static bool HasProblem(LocQAReviewCell _cell)
        {
            for (int i = 0; i < _cell.findings.Count; i++)
            {
                if (LocQASeverity.Info != LocQAKinds.Severity(_cell.findings[i].Key)) return true;
            }
            return false;
        }

        private static string AutoSummary(LocQAReviewCell _cell)
        {
            // 글자가 그림에 나타나지 않은 칸은 검수할 수 없으므로 가장 먼저 알린다.
            string _summary = true == _cell.invisible ? "화면에 안 보임" : null;
            for (int i = 0; i < _cell.findings.Count; i++)
            {
                LocQAKind _kind = _cell.findings[i].Key;
                if (LocQASeverity.Info == LocQAKinds.Severity(_kind) && LocQAKind.AutoWrap != _kind) continue;
                string _name = LocQAKinds.Name(_kind);
                _summary = null == _summary ? _name : _summary + ", " + _name;
            }
            return _summary;
        }

        public static Color StateColor(LocQAReviewData.EffectiveState _state)
        {
            switch (_state)
            {
                case LocQAReviewData.EffectiveState.Ok: return ColorOk;
                case LocQAReviewData.EffectiveState.Ng: return ColorNg;
                case LocQAReviewData.EffectiveState.Changed: return ColorChanged;
                default: return ColorTodo;
            }
        }

        private static string StateLabel(LocQAReviewData.EffectiveState _state)
        {
            switch (_state)
            {
                case LocQAReviewData.EffectiveState.Ok: return "OK";
                case LocQAReviewData.EffectiveState.Ng: return "NG";
                case LocQAReviewData.EffectiveState.Changed: return "번역 바뀜";
                default: return "미검토";
            }
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

        private static Rect DrawImage(Rect _area, Texture _texture)
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
            return _image;
        }

        private static Rect UvToGui(Rect _image, Rect _uv)
        {
            return new Rect(_image.x + _uv.xMin * _image.width, _image.y + (1f - _uv.yMax) * _image.height, _uv.width * _image.width, _uv.height * _image.height);
        }

        private static void DrawOutline(Rect _r, Color _color, float _t)
        {
            EditorGUI.DrawRect(new Rect(_r.xMin, _r.yMin, _r.width, _t), _color);
            EditorGUI.DrawRect(new Rect(_r.xMin, _r.yMax - _t, _r.width, _t), _color);
            EditorGUI.DrawRect(new Rect(_r.xMin, _r.yMin, _t, _r.height), _color);
            EditorGUI.DrawRect(new Rect(_r.xMax - _t, _r.yMin, _t, _r.height), _color);
        }

        private static readonly System.Text.RegularExpressions.Regex TagRegex = new System.Text.RegularExpressions.Regex(@"<[^>]+>", System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>목록·제목에 쓰는 한 줄 요약. 리치 텍스트 태그는 걷어낸다. (색 태그가 그대로 보이면 읽기 어렵다)</summary>
        private static string OneLine(string _text, int _max)
        {
            if (string.IsNullOrEmpty(_text)) return string.Empty;
            string _line = TagRegex.Replace(_text, string.Empty).Replace("\r\n", " ⏎ ").Replace("\n", " ⏎ ").Replace("\\n", " ⏎ ");
            return _line.Length > _max ? _line.Substring(0, _max) + "…" : _line;
        }

        private void EnsureStyles()
        {
            if (null != rowStyle) return;
            rowStyle = new GUIStyle(EditorStyles.label) { clipping = TextClipping.Clip };
            rowSelectedStyle = new GUIStyle(EditorStyles.boldLabel) { clipping = TextClipping.Clip };
            smallWrap = new GUIStyle(EditorStyles.label) { wordWrap = true, richText = false };
            titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            cellHeader = new GUIStyle(EditorStyles.miniBoldLabel) { clipping = TextClipping.Clip };
        }
    }

    /// <summary>
    /// 텍스트 칸에 들어갈 문구를 검색해 여러 개 고르는 창.
    /// 프리팹 이름과 닮은 JSON(예: HUD_Message ↔ MessageHUD)을 먼저 보여준다.
    /// </summary>
    internal sealed class LocQAKeyPickerWindow : EditorWindow
    {
        private LocQAStringTable table;
        private HashSet<string> referenced;
        private Action<List<LocQAEntry>> onAdd;
        private string targetLabel;
        private string search = string.Empty;
        private int fileIndex;
        private bool unconnectedOnly;
        private string[] files;
        private readonly HashSet<string> picked = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<LocQAEntry> visible = new List<LocQAEntry>(512);
        private Vector2 scroll;

        public static LocQAKeyPickerWindow Show(LocQAStringTable _table, HashSet<string> _referenced, bool _unconnectedOnly, string _contextName, string _targetLabel, Action<List<LocQAEntry>> _onAdd)
        {
            LocQAKeyPickerWindow _window = CreateInstance<LocQAKeyPickerWindow>();
            _window.titleContent = new GUIContent("문구 고르기");
            _window.table = _table;
            _window.referenced = _referenced;
            _window.onAdd = _onAdd;
            _window.targetLabel = _targetLabel;
            _window.unconnectedOnly = _unconnectedOnly;

            List<string> _files = new List<string> { "(모든 파일)" };
            for (int i = 0; i < _table.Entries.Count; i++)
            {
                if (false == _files.Contains(_table.Entries[i].file)) _files.Add(_table.Entries[i].file);
            }
            _window.files = _files.ToArray();
            _window.fileIndex = SuggestFile(_window.files, _contextName);

            _window.minSize = new Vector2(620f, 480f);
            _window.ShowUtility();
            return _window;
        }

        /// <summary>프리팹 이름과 단어가 가장 많이 겹치는 JSON 파일. (HUD_Message ↔ MessageHUD, UI_Option ↔ OptionUI)</summary>
        private static int SuggestFile(string[] _files, string _contextName)
        {
            if (string.IsNullOrEmpty(_contextName)) return 0;
            HashSet<string> _words = Words(_contextName);
            int _best = 0;
            int _bestScore = 0;
            for (int i = 1; i < _files.Length; i++)
            {
                int _score = 0;
                foreach (string _w in Words(_files[i]))
                {
                    if (true == _words.Contains(_w)) _score += ("ui" == _w || "hud" == _w) ? 1 : 3;
                }
                if (_score > _bestScore)
                {
                    _bestScore = _score;
                    _best = i;
                }
            }
            return _bestScore >= 3 ? _best : 0;
        }

        private static HashSet<string> Words(string _name)
        {
            HashSet<string> _set = new HashSet<string>(StringComparer.Ordinal);
            StringBuilder _sb = new StringBuilder(16);
            for (int i = 0; i < _name.Length; i++)
            {
                char _c = _name[i];
                bool _boundary = false == char.IsLetterOrDigit(_c)
                    || (char.IsUpper(_c) && _sb.Length > 0 && i + 1 < _name.Length && char.IsLower(_name[i + 1]));
                if (true == _boundary && _sb.Length > 0)
                {
                    _set.Add(_sb.ToString().ToLowerInvariant());
                    _sb.Clear();
                }
                if (true == char.IsLetterOrDigit(_c)) _sb.Append(_c);
            }
            if (_sb.Length > 0) _set.Add(_sb.ToString().ToLowerInvariant());
            return _set;
        }

        /// <summary>현재 조건에 보이는 문구 목록 (검색·파일·연결 여부 필터 적용)</summary>
        public List<LocQAEntry> Visible
        {
            get
            {
                visible.Clear();
                for (int i = 0; i < table.Entries.Count; i++)
                {
                    LocQAEntry _e = table.Entries[i];
                    if (fileIndex > 0 && _e.file != files[fileIndex]) continue;
                    if (true == unconnectedOnly && (true == referenced.Contains(_e.id) || null != LocQAReviewData.Shared.UnusedReason(_e.id))) continue;
                    if (false == string.IsNullOrEmpty(search)
                        && _e.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                        && (_e.data.kr ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                        && (_e.data.en ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0
                        && (_e.data.enumValue ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    visible.Add(_e);
                }
                return visible;
            }
        }

        private void OnGUI()
        {
            if (null == table)
            {
                Close();
                return;
            }

            EditorGUILayout.LabelField(string.IsNullOrEmpty(targetLabel) ? "먼저 검수 창에서 텍스트 칸(문구)을 고르면 그 칸에 추가할 수 있습니다." : "추가할 칸: " + targetLabel, EditorStyles.wordWrappedLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                fileIndex = EditorGUILayout.Popup(fileIndex, files, GUILayout.Width(180f));
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            }
            unconnectedOnly = EditorGUILayout.ToggleLeft("아직 어느 칸에도 연결 안 된 문구만 (게임에서 쓰지 않는 문구 제외)", unconnectedOnly);

            List<LocQAEntry> _visible = Visible;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button($"보이는 {_visible.Count}개 전부 선택", GUILayout.Width(170f)))
                {
                    for (int i = 0; i < _visible.Count; i++) picked.Add(_visible[i].id);
                }
                if (GUILayout.Button("선택 해제", GUILayout.Width(80f))) picked.Clear();
                GUILayout.FlexibleSpace();
                GUILayout.Label($"선택 {picked.Count}개", EditorStyles.miniBoldLabel);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < _visible.Count; i++)
            {
                LocQAEntry _e = _visible[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool _on = picked.Contains(_e.id);
                    bool _now = EditorGUILayout.Toggle(_on, GUILayout.Width(18f));
                    if (_now != _on)
                    {
                        if (_now) picked.Add(_e.id);
                        else picked.Remove(_e.id);
                    }
                    GUILayout.Label(new GUIContent(_e.id, _e.id), EditorStyles.miniLabel, GUILayout.Width(250f));
                    string _kr = (_e.data.kr ?? string.Empty).Replace("\n", " ⏎ ");
                    GUILayout.Label(new GUIContent(_kr, _e.data.kr), EditorStyles.miniLabel);
                    if (true == referenced.Contains(_e.id)) GUILayout.Label("연결됨", EditorStyles.miniBoldLabel, GUILayout.Width(40f));
                    string _unused = LocQAReviewData.Shared.UnusedReason(_e.id);
                    if (null != _unused) GUILayout.Label(new GUIContent("미사용", _unused), EditorStyles.miniBoldLabel, GUILayout.Width(40f));
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(0 == picked.Count || string.IsNullOrEmpty(targetLabel)))
            {
                if (GUILayout.Button($"이 칸에 {picked.Count}개 추가", GUILayout.Height(28f)))
                {
                    Commit();
                    GUIUtility.ExitGUI();
                }
            }
        }

        /// <summary>선택한 문구를 칸에 추가하고 창을 닫는다.</summary>
        public void Commit()
        {
            List<LocQAEntry> _result = new List<LocQAEntry>(picked.Count);
            for (int i = 0; i < table.Entries.Count; i++)
            {
                if (true == picked.Contains(table.Entries[i].id)) _result.Add(table.Entries[i]);
            }
            onAdd?.Invoke(_result);
            Close();
        }

        public void SetSearch(string _search, bool _unconnectedOnly)
        {
            search = _search ?? string.Empty;
            unconnectedOnly = _unconnectedOnly;
            fileIndex = 0;
        }

        public void PickVisible()
        {
            List<LocQAEntry> _visible = Visible;
            for (int i = 0; i < _visible.Count; i++) picked.Add(_visible[i].id);
        }
    }

    /// <summary>
    /// 검수 진행 현황판. 언어별 진행률과, 화면 × 언어 진행률 표를 보여준다.
    /// 표의 칸을 누르면 그 화면을 그 언어로 언어별 검수 창에서 연다.
    /// </summary>
    internal sealed class LocQAProgressWindow : EditorWindow
    {
        private Vector2 scroll;
        private int builtVersion = -1;
        private readonly List<string> screens = new List<string>(32);
        private int[,] ok;
        private int[,] ng;
        private int[,] changed;
        private int[,] total;
        private int[] langOk;
        private int[] langNg;
        private int[] langChanged;
        private int[] langTotal;
        private LocQAStringTable table;

        [MenuItem("Tools/Localization/Localization QA/검수 진행 현황", false, 21)]
        public static void Open()
        {
            LocQAProgressWindow _window = GetWindow<LocQAProgressWindow>("검수 진행 현황");
            _window.minSize = new Vector2(760f, 360f);
            _window.Show();
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
            if (builtVersion == _data.Version && null != ok) return;
            builtVersion = _data.Version;

            if (null == table) table = LocQAStringTable.Load();
            LocQASettings _settings = LocQASettings.Load();
            int _langCount = LocQALanguages.All.Length;

            screens.Clear();
            Dictionary<string, int> _screenIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < _data.Items.Count; i++)
            {
                List<string> _s = LocQAScreenIndex.ScreensOfSlot(_data.Items[i].contextGuid, _data.Items[i].slotGuid, _settings.prefabFolder);
                for (int k = 0; k < _s.Count; k++)
                {
                    if (true == _screenIndex.ContainsKey(_s[k])) continue;
                    _screenIndex.Add(_s[k], screens.Count);
                    screens.Add(_s[k]);
                }
            }
            screens.Sort((a, b) => string.Compare(LocQAReviewWindow.ScreenName(a), LocQAReviewWindow.ScreenName(b), StringComparison.OrdinalIgnoreCase));
            _screenIndex.Clear();
            for (int i = 0; i < screens.Count; i++) _screenIndex[screens[i]] = i;

            ok = new int[screens.Count, _langCount];
            ng = new int[screens.Count, _langCount];
            changed = new int[screens.Count, _langCount];
            total = new int[screens.Count, _langCount];
            langOk = new int[_langCount];
            langNg = new int[_langCount];
            langChanged = new int[_langCount];
            langTotal = new int[_langCount];

            for (int i = 0; i < _data.Items.Count; i++)
            {
                LocQAReviewItem _item = _data.Items[i];
                LocQAEntry _entry = table.Find(_item.entryId);
                List<string> _s = LocQAScreenIndex.ScreensOfSlot(_item.contextGuid, _item.slotGuid, _settings.prefabFolder);

                for (int l = 0; l < _langCount; l++)
                {
                    Language _lang = LocQALanguages.All[l];
                    string _hash = null != _entry ? LocQAReviewData.Hash(LocQALanguages.Resolve(_entry.data, _lang)) : string.Empty;
                    LocQAReviewData.EffectiveState _state = _data.GetEffective(_item, _lang, _hash);

                    langTotal[l]++;
                    if (LocQAReviewData.EffectiveState.Ok == _state) langOk[l]++;
                    else if (LocQAReviewData.EffectiveState.Ng == _state) langNg[l]++;
                    else if (LocQAReviewData.EffectiveState.Changed == _state) langChanged[l]++;

                    for (int k = 0; k < _s.Count; k++)
                    {
                        int _si = _screenIndex[_s[k]];
                        total[_si, l]++;
                        if (LocQAReviewData.EffectiveState.Ok == _state) ok[_si, l]++;
                        else if (LocQAReviewData.EffectiveState.Ng == _state) ng[_si, l]++;
                        else if (LocQAReviewData.EffectiveState.Changed == _state) changed[_si, l]++;
                    }
                }
            }
        }

        private void OnGUI()
        {
            Build();
            int _langCount = LocQALanguages.All.Length;

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("판정은 문구×언어 단위입니다. 칸을 누르면 그 화면을 그 언어로 '언어별 검수'에서 엽니다.", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("새로고침", EditorStyles.toolbarButton))
                {
                    table = LocQAStringTable.Load();
                    builtVersion = -1;
                }
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("언어별 진행률", EditorStyles.boldLabel);
            for (int l = 0; l < _langCount; l++)
            {
                Language _lang = LocQALanguages.All[l];
                int _done = langOk[l] + langNg[l];
                float _ratio = langTotal[l] > 0 ? (float)_done / langTotal[l] : 0f;
                Rect _r = EditorGUILayout.GetControlRect(false, 18f);
                Rect _label = new Rect(_r.x, _r.y, 150f, _r.height);
                Rect _bar = new Rect(_r.x + 154f, _r.y + 2f, Mathf.Max(60f, _r.width - 460f), _r.height - 4f);
                Rect _info = new Rect(_bar.xMax + 8f, _r.y, 300f, _r.height);

                GUI.Label(_label, $"{LocQALanguages.Name(_lang)} ({_lang})");
                EditorGUI.DrawRect(_bar, new Color(0f, 0f, 0f, 0.3f));
                EditorGUI.DrawRect(new Rect(_bar.x, _bar.y, _bar.width * _ratio, _bar.height), langNg[l] > 0 ? new Color(0.93f, 0.55f, 0.22f) : new Color(0.30f, 0.78f, 0.38f));
                GUI.Label(_info, $"{_ratio * 100f:0}%  ·  OK {langOk[l]}  NG {langNg[l]}  번역 바뀜 {langChanged[l]}  남음 {langTotal[l] - _done - langChanged[l]}", EditorStyles.miniLabel);

                if (EventType.MouseDown == Event.current.type && _r.Contains(Event.current.mousePosition) && screens.Count > 0)
                {
                    LocQAReviewWindow.OpenLanguage(screens[0], _lang);
                    Event.current.Use();
                }
            }

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("화면 × 언어 (완료율 %, 빨강 = NG 있음)", EditorStyles.boldLabel);

            const float NAME_W = 170f;
            const float CELL_W = 52f;
            Rect _header = EditorGUILayout.GetControlRect(false, 18f, GUILayout.Width(NAME_W + CELL_W * _langCount));
            for (int l = 0; l < _langCount; l++)
            {
                GUI.Label(new Rect(_header.x + NAME_W + l * CELL_W, _header.y, CELL_W, _header.height), LocQALanguages.All[l].ToString(), EditorStyles.centeredGreyMiniLabel);
            }

            for (int s = 0; s < screens.Count; s++)
            {
                Rect _r = EditorGUILayout.GetControlRect(false, 18f, GUILayout.Width(NAME_W + CELL_W * _langCount));
                GUI.Label(new Rect(_r.x, _r.y, NAME_W, _r.height), LocQAReviewWindow.ScreenName(screens[s]), EditorStyles.miniLabel);
                for (int l = 0; l < _langCount; l++)
                {
                    Rect _cell = new Rect(_r.x + NAME_W + l * CELL_W + 1f, _r.y + 1f, CELL_W - 2f, _r.height - 2f);
                    int _t = total[s, l];
                    float _ratio = _t > 0 ? (float)(ok[s, l] + ng[s, l]) / _t : 0f;
                    Color _color = ng[s, l] > 0 ? new Color(0.93f, 0.26f, 0.22f, 0.25f + 0.6f * _ratio) : new Color(0.30f, 0.78f, 0.38f, 0.1f + 0.7f * _ratio);
                    EditorGUI.DrawRect(_cell, _color);
                    GUI.Label(_cell, new GUIContent($"{_ratio * 100f:0}", $"{LocQAReviewWindow.ScreenName(screens[s])} · {LocQALanguages.Name(LocQALanguages.All[l])}\nOK {ok[s, l]} · NG {ng[s, l]} · 번역 바뀜 {changed[s, l]} · 전체 {_t}"), EditorStyles.centeredGreyMiniLabel);

                    if (EventType.MouseDown == Event.current.type && _cell.Contains(Event.current.mousePosition))
                    {
                        LocQAReviewWindow.OpenLanguage(screens[s], LocQALanguages.All[l]);
                        Event.current.Use();
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
