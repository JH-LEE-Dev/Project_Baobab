using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

using UnityObject = UnityEngine.Object;

namespace LocalizationQA
{
    /// <summary>
    /// 플레이 모드에서 화면에 떠 있는 모든 TMP 텍스트를 실측한다.
    ///
    /// 이 게임은 문구를 전부 코드에서 넣기 때문에, 실제 문구와 실제 박스 크기(ContentSizeFitter·코드로 맞춘
    /// 말풍선 등)는 플레이 중에만 정확하다. 그래서 이 검사가 주력이고 프리팹 검사는 보조다.
    ///
    /// MonoBehaviour가 아니라 EditorApplication.update로 돈다. 에디터 폴더의 스크립트는 게임 오브젝트에
    /// 붙일 수 없기 때문이다. 대신 빌드에는 절대 포함되지 않는다.
    /// 화면 표시용 오버레이 캔버스만 게임 쪽에 만들고(표준 UI 컴포넌트뿐이다) 플레이가 끝나면 함께 사라진다.
    ///
    /// 언어 순회는 SettingsManager.SetLanguage를 거친다. 옵션 화면의 언어 표기처럼 SettingsManager 이벤트를
    /// 듣는 UI까지 실제와 똑같이 갱신되게 하기 위해서다. 설정 파일에도 기록되므로 끝나면 반드시 원래 언어로 되돌린다.
    /// (플레이를 도중에 멈춰도 ExitingPlayMode에서 되돌린다)
    /// </summary>
    internal sealed class LocQARuntimeRunner
    {
        public const string CAPTURE_ROOT = "LocalizationQA/Captures";
        private const string OVERLAY_NAME = "[LocalizationQA Overlay]";

        private const float LINE_THICKNESS = 2f;
        private const float CAPTURE_TIMEOUT = 3f;

        public static LocQARuntimeRunner Current { get; private set; }

        public bool IsCycling => null != cycle;
        public bool AutoCollect { get; set; }
        public string Status { get; private set; } = string.Empty;
        public float Progress { get; private set; }

        private readonly LocQAStringTable table;
        private LocQASettings settings;
        private IEnumerator cycle;
        private bool stopRequested;
        private bool restorePending;
        private Language originalLanguage;
        private float nextAutoScan;
        private int rescanFrame;

        private GameObject root;
        private Canvas overlay;
        private readonly List<Image> linePool = new List<Image>(64);
        private int linesUsed;

        // //생성·정리
        [InitializeOnLoadMethod]
        private static void RegisterLeakCleanup()
        {
            EditorApplication.playModeStateChanged -= CleanupLeakedOverlays;
            EditorApplication.playModeStateChanged += CleanupLeakedOverlays;
        }

        /// <summary>
        /// 플레이를 끝냈는데도 남은 오버레이(정리 도중 스크립트가 다시 컴파일되는 등)를 지운다.
        /// 씬에 속하지 않고 에셋도 아닌, 이 툴이 만든 이름의 최상위 오브젝트만 대상이다.
        /// </summary>
        private static void CleanupLeakedOverlays(PlayModeStateChange _change)
        {
            if (PlayModeStateChange.EnteredEditMode != _change) return;

            GameObject[] _all = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < _all.Length; i++)
            {
                GameObject _go = _all[i];
                if (null == _go || OVERLAY_NAME != _go.name || null != _go.transform.parent) continue;
                if (true == _go.scene.IsValid() || true == EditorUtility.IsPersistent(_go)) continue;
                UnityObject.DestroyImmediate(_go);
            }
        }

        public static LocQARuntimeRunner Ensure()
        {
            if (false == Application.isPlaying) return null;
            if (null != Current && null != Current.root) return Current;

            Current?.Dispose();
            Current = new LocQARuntimeRunner();
            return Current;
        }

        private LocQARuntimeRunner()
        {
            table = LocQAStringTable.Load();
            settings = LocQASettings.Load();
            BuildOverlay();

            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private void Dispose()
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;

            if (null != root)
            {
                if (true == Application.isPlaying) UnityObject.Destroy(root);
                else UnityObject.DestroyImmediate(root);
            }
            root = null;
            cycle = null;
            if (this == Current) Current = null;
        }

        private void OnPlayModeChanged(PlayModeStateChange _change)
        {
            if (PlayModeStateChange.ExitingPlayMode != _change) return;

            // 순회 도중 플레이를 멈추면 설정 파일에 엉뚱한 언어가 남는다. 게임 오브젝트가 아직 살아 있을 때 되돌린다.
            RestoreLanguageIfNeeded();
            RestoreResolutionIfNeeded();
            Dispose();
        }

        private void Tick()
        {
            if (false == Application.isPlaying || null == root)
            {
                Dispose();
                return;
            }

            if (null != cycle)
            {
                bool _running;
                try
                {
                    _running = cycle.MoveNext();
                }
                catch (Exception _e)
                {
                    Debug.LogException(_e);
                    _running = false;
                }
                if (false == _running) FinishCycle();
                return;
            }

            if (rescanFrame > 0 && Time.frameCount >= rescanFrame)
            {
                rescanFrame = 0;
                ScanNow(null);
                return;
            }

            if (false == AutoCollect || Time.realtimeSinceStartup < nextAutoScan) return;
            nextAutoScan = Time.realtimeSinceStartup + Mathf.Max(0.1f, settings.autoScanInterval);
            ScanNow(null);
        }

        public void ReloadSettings()
        {
            settings = LocQASettings.Load();
            if (false == settings.showOverlay) ClearOverlay();
        }

        // //언어
        public static Language GetCurrentLanguage()
        {
            LocalizationManager _manager = UnityObject.FindAnyObjectByType<LocalizationManager>(FindObjectsInactive.Include);
            return null != _manager ? _manager.CurrentLanguage : FontLocalizer.CurrentLanguage;
        }

        public static void SetLanguage(Language _lang)
        {
            if (true == SettingsManager.HasInstance)
            {
                SettingsManager.Instance.SetLanguage(LocQALanguages.ToOption(_lang));
                return;
            }

            LocalizationManager _manager = UnityObject.FindAnyObjectByType<LocalizationManager>(FindObjectsInactive.Include);
            if (null != _manager) _manager.SetLanguage(_lang);
        }

        private void RestoreLanguageIfNeeded()
        {
            if (false == restorePending) return;
            restorePending = false;
            SetLanguage(originalLanguage);
        }

        // //검사
        /// <summary>현재 언어로 화면의 모든 텍스트를 잰다. 같은 오브젝트의 이전 결과는 새 결과로 바꾼다.</summary>
        public int ScanNow(string _capturePath)
        {
            Language _lang = GetCurrentLanguage();
            Canvas.ForceUpdateCanvases();

            LocQAStore _store = LocQAStore.instance;
            TMP_Text[] _texts = UnityObject.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude);
            HashSet<string> _scanned = new HashSet<string>(StringComparer.Ordinal);
            List<LocQAIssue> _found = new List<LocQAIssue>(32);
            Vector2 _screenSize = new Vector2(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height));

            BeginOverlay();

            for (int i = 0; i < _texts.Length; i++)
            {
                TMP_Text _text = _texts[i];
                if (null == _text || false == _text.isActiveAndEnabled) continue;
                if (true == string.IsNullOrWhiteSpace(_text.text)) continue;
                if (true == settings.onlyVisible && false == IsVisible(_text)) continue;

                // 같은 오브젝트라도 해상도마다 배치가 다르므로 해상도별로 따로 기록한다.
                string _location = _text.gameObject.scene.name + " @" + Screen.width + "×" + Screen.height;
                string _path = LocQAPaths.GetPath(_text.transform, null);
                _scanned.Add(_location + "|" + _path);

                LocQAEntry _entry = table.MatchDisplayed(_text.text, _lang);
                string _template = null;
                if (null == _entry) _entry = table.MatchContained(_text.text, _lang, out _template);
                if (null != _entry)
                {
                    _store.Cover(_entry.id);
                    // 사람 검수 목록에도 "이 칸에 이 문구가 나온다"를 기록한다. (숫자·태그를 붙인 모양까지)
                    LocQAReviewCollector.Observe(_text, _entry, settings.prefabFolder, _template);
                }

                LocQAMeasurement _m = LocQATextAnalyzer.Measure(_text, settings, false, true);
                if (0 == _m.findings.Count) continue;

                bool _hasUv = LocQATextAnalyzer.TryGetScreenRect(_text, _m.boxWorld, out Rect _boxScreen);
                Rect _glyphScreen = _boxScreen;
                if (true == _hasUv && true == _m.hasGlyphs) LocQATextAnalyzer.TryGetScreenRect(_text, _m.glyphWorld, out _glyphScreen);

                for (int f = 0; f < _m.findings.Count; f++)
                {
                    _found.Add(new LocQAIssue
                    {
                        source = LocQASource.Play,
                        language = _lang,
                        kind = _m.findings[f].Key,
                        location = _location,
                        objectPath = _path,
                        text = _text.text,
                        key = null != _entry ? _entry.id : string.Empty,
                        detail = _m.findings[f].Value,
                        lineCount = _m.lineCount,
                        target = _text,
                        capturePath = _capturePath,
                        hasUv = _hasUv,
                        boxUv = ToUv(_boxScreen, _screenSize),
                        glyphUv = ToUv(_glyphScreen, _screenSize)
                    });
                }

                LocQASeverity _worst = _m.WorstSeverity;
                if (true == _hasUv && LocQASeverity.Info != _worst && true == settings.showOverlay)
                {
                    DrawRect(_boxScreen, new Color(1f, 0.92f, 0.2f, 0.9f), 1f);
                    DrawRect(_glyphScreen, LocQAKinds.SeverityColor(_worst), LINE_THICKNESS);
                }
            }

            EndOverlay();

            _store.issues.RemoveAll(i => LocQASource.Play == i.source && i.language == _lang && _scanned.Contains(i.ObjectKey));
            _store.issues.AddRange(_found);
            _store.playSummary = $"{DateTime.Now:HH:mm:ss} · {LocQALanguages.Name(_lang)} · 텍스트 {_scanned.Count}개 검사 · 문제 {_found.Count}건";
            _store.NotifyChanged();
            return _found.Count;
        }

        private static bool IsVisible(TMP_Text _text)
        {
            if (_text.color.a <= 0.01f) return false;

            Canvas _canvas = _text.canvas;
            if (null == _canvas || false == _canvas.isActiveAndEnabled) return false;

            // CanvasGroup 알파까지 반영된 값. 페이드아웃된 패널은 보이지 않는 것으로 본다.
            CanvasRenderer _renderer = _text.canvasRenderer;
            if (null != _renderer && _renderer.GetInheritedAlpha() <= 0.01f) return false;

            return true;
        }

        private static Rect ToUv(Rect _screen, Vector2 _size)
        {
            return Rect.MinMaxRect(_screen.xMin / _size.x, _screen.yMin / _size.y, _screen.xMax / _size.x, _screen.yMax / _size.y);
        }

        // //언어 순회
        /// <param name="_resolutions">순회할 해상도(LocQAResolution 인덱스). null이면 지금 Game 뷰 해상도 하나로만 검사한다.</param>
        public void StartCycle(List<Language> _languages, bool _capture, List<int> _resolutions = null)
        {
            if (true == IsCycling || null == _languages || 0 == _languages.Count) return;

            settings = LocQASettings.Load();
            stopRequested = false;
            totalFound = 0;
            captureSet = null;
            cycle = CycleRoutine(new List<Language>(_languages), _capture, null != _resolutions ? new List<int>(_resolutions) : null);
        }

        /// <summary>Game 뷰를 그 해상도로 바꾼다. 게임 UI 캔버스는 다음 프레임에 새 화면 크기로 다시 맞춰진다.</summary>
        public static void SetGameViewResolution(int _width, int _height)
        {
            PlayModeWindow.SetCustomRenderingResolution((uint)_width, (uint)_height, $"LocQA {_width}x{_height}");
        }

        public void StopCycle()
        {
            stopRequested = true;
        }

        private int totalFound;
        private LocQACapture captureSet;

        /// <summary>
        /// EditorApplication.update 한 번에 한 걸음씩 진행된다. (yield return null = 다음 에디터 틱)
        /// 프레임 단위로 기다려야 하는 곳은 Time.frameCount로 센다.
        /// </summary>
        private IEnumerator CycleRoutine(List<Language> _languages, bool _capture, List<int> _resolutions)
        {
            originalLanguage = GetCurrentLanguage();
            restorePending = true;

            if (null != _resolutions)
            {
                PlayModeWindow.GetRenderingResolution(out uint _ow, out uint _oh);
                originalResolution = new Vector2Int((int)_ow, (int)_oh);
                restoreResolution = true;
            }

            List<int> _resList = null != _resolutions && _resolutions.Count > 0 ? _resolutions : new List<int> { -1 };
            int _total = _resList.Count * _languages.Count;
            int _step = 0;
            string _stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            for (int r = 0; r < _resList.Count; r++)
            {
                if (true == stopRequested) yield break;

                string _resLabel = null;
                if (_resList[r] >= 0)
                {
                    LocQAResolution.Preset _preset = LocQAResolution.Get(_resList[r]);
                    _resLabel = _preset.label;
                    SetGameViewResolution(_preset.width, _preset.height);

                    // 화면 크기가 바뀌고, PixelPerfectCanvasScaleApplier가 Update에서 캔버스 배율을 다시 맞출 때까지 기다린다.
                    int _resFrame = Time.frameCount + 3;
                    float _resTimeout = Time.realtimeSinceStartup + 3f;
                    while ((Time.frameCount < _resFrame || Screen.width != _preset.width || Screen.height != _preset.height) && Time.realtimeSinceStartup < _resTimeout)
                    {
                        yield return null;
                    }
                }

                if (true == _capture)
                {
                    // 해상도마다 스크린샷 묶음을 따로 둔다. (검사 창에서 언어별로 넘겨 볼 때 같은 해상도끼리 비교하도록)
                    string _sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                    string _folder = Path.Combine(CAPTURE_ROOT, $"{_stamp}_{_sceneName}_{Screen.width}x{Screen.height}").Replace('\\', '/');
                    Directory.CreateDirectory(_folder);
                    captureSet = new LocQACapture { folder = _folder, label = $"{DateTime.Now:HH:mm:ss} {_sceneName} {Screen.width}×{Screen.height}" };
                    LocQAStore.instance.captures.Add(captureSet);
                }

            for (int i = 0; i < _languages.Count; i++)
            {
                if (true == stopRequested) yield break;

                Language _lang = _languages[i];
                Progress = (float)_step / _total;
                _step++;
                Status = $"{_step}/{_total} {(null != _resLabel ? _resLabel + " · " : string.Empty)}{LocQALanguages.Name(_lang)} 검사 중…";

                SetLanguage(_lang);

                // 언어 변경 이벤트로 문구가 바뀌고, FontLocalizer가 LateUpdate에서 늦게 잡은 텍스트까지
                // 반영될 때까지 몇 프레임과 설정한 시간만큼 기다린다. (타자 효과 같은 연출 포함)
                int _frame = Time.frameCount + 3;
                float _until = Time.realtimeSinceStartup + Mathf.Max(0f, settings.captureDelay);
                while (Time.frameCount < _frame || Time.realtimeSinceStartup < _until) yield return null;

                string _file = null != captureSet ? Path.Combine(captureSet.folder, $"{i:00}_{_lang}.png").Replace('\\', '/') : null;
                totalFound += ScanNow(_file);

                if (null != captureSet)
                {
                    // 방금 그린 문제 표시까지 담기도록, 다음에 그려지는 프레임을 파일로 남긴다.
                    string _fullPath = Path.GetFullPath(_file);
                    if (true == File.Exists(_fullPath)) File.Delete(_fullPath);
                    ScreenCapture.CaptureScreenshot(_fullPath);

                    int _captureFrame = Time.frameCount + 2;
                    float _timeout = Time.realtimeSinceStartup + CAPTURE_TIMEOUT;
                    while ((Time.frameCount < _captureFrame || false == File.Exists(_fullPath)) && Time.realtimeSinceStartup < _timeout)
                    {
                        yield return null;
                    }

                    if (true == File.Exists(_fullPath))
                    {
                        captureSet.languages.Add((int)_lang);
                        captureSet.files.Add(_file);
                    }
                    else
                    {
                        Debug.LogWarning("[LocalizationQA] 스크린샷을 찍지 못했습니다. Game 뷰가 화면에 보이는 상태인지 확인하세요.");
                    }
                }
            }
            }
        }

        private bool restoreResolution;
        private Vector2Int originalResolution;

        private void RestoreResolutionIfNeeded()
        {
            if (false == restoreResolution) return;
            restoreResolution = false;
            if (originalResolution.x > 0 && originalResolution.y > 0) SetGameViewResolution(originalResolution.x, originalResolution.y);
        }

        private void FinishCycle()
        {
            cycle = null;
            RestoreLanguageIfNeeded();
            RestoreResolutionIfNeeded();
            Progress = 1f;
            Status = (stopRequested ? "중지됨" : "완료") + $" · 문제 {totalFound}건"
                + (null != captureSet ? $" · 스크린샷 {captureSet.files.Count}장 ({captureSet.folder})" : string.Empty);
            LocQAStore.instance.NotifyChanged();

            // 원래 언어로 돌아온 화면 기준으로 표시를 다시 그린다. (문구가 바뀔 시간을 준다)
            if (true == settings.showOverlay) rescanFrame = Time.frameCount + 3;
            else ClearOverlay();
        }

        // //화면 표시 (게임 뷰 위에 문제 위치를 박스로 그린다)
        private void BuildOverlay()
        {
            // HideFlags.DontSave를 붙이면 플레이가 끝나도 Unity가 지우지 않아 에디터 Game 뷰에 박스가 남는다.
            // 평범한 런타임 오브젝트로 만들어 플레이 종료와 함께 사라지게 한다.
            root = new GameObject(OVERLAY_NAME, typeof(RectTransform));
            UnityObject.DontDestroyOnLoad(root);

            overlay = root.AddComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = short.MaxValue;
            overlay.pixelPerfect = false;
        }

        public void ClearOverlay()
        {
            BeginOverlay();
            EndOverlay();
        }

        private void BeginOverlay()
        {
            linesUsed = 0;
        }

        private void EndOverlay()
        {
            for (int i = linesUsed; i < linePool.Count; i++)
            {
                if (null != linePool[i] && true == linePool[i].gameObject.activeSelf) linePool[i].gameObject.SetActive(false);
            }
        }

        private void DrawRect(Rect _r, Color _color, float _thickness)
        {
            // 캔버스 스케일러가 없는 오버레이 캔버스라 1 단위 = 1 화면 픽셀이다.
            DrawLine(new Rect(_r.xMin - _thickness, _r.yMax, _r.width + _thickness * 2f, _thickness), _color);
            DrawLine(new Rect(_r.xMin - _thickness, _r.yMin - _thickness, _r.width + _thickness * 2f, _thickness), _color);
            DrawLine(new Rect(_r.xMin - _thickness, _r.yMin, _thickness, _r.height), _color);
            DrawLine(new Rect(_r.xMax, _r.yMin, _thickness, _r.height), _color);
        }

        private void DrawLine(Rect _r, Color _color)
        {
            if (null == overlay) return;

            Image _line;
            if (linesUsed < linePool.Count && null != linePool[linesUsed])
            {
                _line = linePool[linesUsed];
            }
            else
            {
                GameObject _go = new GameObject("Line", typeof(RectTransform), typeof(Image));
                _go.transform.SetParent(overlay.transform, false);
                _line = _go.GetComponent<Image>();
                _line.raycastTarget = false;
                RectTransform _rt = _line.rectTransform;
                _rt.anchorMin = Vector2.zero;
                _rt.anchorMax = Vector2.zero;
                _rt.pivot = Vector2.zero;

                if (linesUsed < linePool.Count) linePool[linesUsed] = _line;
                else linePool.Add(_line);
            }
            linesUsed++;

            if (false == _line.gameObject.activeSelf) _line.gameObject.SetActive(true);
            _line.color = _color;
            _line.rectTransform.anchoredPosition = _r.position;
            _line.rectTransform.sizeDelta = _r.size;
        }
    }
}
