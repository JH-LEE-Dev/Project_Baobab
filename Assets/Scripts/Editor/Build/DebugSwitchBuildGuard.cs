using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 디버그·테스트 스위치가 배포 빌드에 섞이지 않게 막습니다.
///
/// [원칙] 디버그·테스트용 인스펙터 필드는 #if UNITY_EDITOR (또는 UNITY_EDITOR || DEVELOPMENT_BUILD) 로 감쌉니다.
/// 감싼 필드는 빌드에서 코드째 빠지므로 프리팹에 켜진 채 커밋돼도 배포판에 아무 영향이 없습니다.
/// 2026-10-05 에 맵 전체 해금(DensityManager, HUD_PopupNav_Main), ` 키 디버그 사운드(AudioManager),
/// 조명·그림자 디버그 값, 디버그 로그, FPS 표시 등을 모두 이 원칙대로 감쌌습니다.
///
/// [두 겹으로 봅니다]
/// 1) 소스 검사 - 런타임 스크립트(Assets/Scripts, Editor 폴더 제외)에서 이름이 debug/test/cheat 류인 직렬화 필드가
///    에디터 전용 블록 밖에 있으면 막습니다. 새로 추가된 디버그 스위치가 감싸지지 않은 채 들어오는 것을 잡습니다.
///    이름만 그럴 뿐 실제 게임 로직인 필드는 RUNTIME_FIELDS 에 등록합니다.
/// 2) 값 검사 - RUNTIME_FIELDS 중 배포 값이 정해진 스위치(꺼져 있어야 하는 것, 켜져 있어야 하는 것)를 프리팹 전체와
///    빌드 씬(직접 배치·오버라이드)에서 읽어 다르면 막습니다. 씬은 빌드 중 열린 씬을 바꾸지 않으려고 YAML로 읽습니다.
///
/// [Development Build]
/// 개발 빌드에서는 경고만 남깁니다(CharacterStatBuildGuard 와 같은 정책).
/// </summary>
public class DebugSwitchBuildGuard : IPreprocessBuildWithReport
{
    private const string MENU_CHECK = "Tools/빌드/디버그 스위치 검사";
    private const string TAG = "[DebugSwitchGuard]";
    private const string SCRIPTS_ROOT = "Assets/Scripts";

    private struct RuntimeField
    {
        public Type type;
        public string field;
        public bool checkValue;
        public bool expected;
        public string reason;

        /// <summary>배포 빌드에서 반드시 _expected 여야 하는 스위치입니다.</summary>
        public static RuntimeField Must(Type _type, string _field, bool _expected, string _reason)
        {
            return new RuntimeField { type = _type, field = _field, checkValue = true, expected = _expected, reason = _reason };
        }

        /// <summary>이름만 디버그 같을 뿐 실제 로직이라 소스 검사에서만 빼는 필드입니다. 값은 보지 않습니다.</summary>
        public static RuntimeField Allow(Type _type, string _field, string _reason)
        {
            return new RuntimeField { type = _type, field = _field, checkValue = false, reason = _reason };
        }

        public string ExpectedText => true == expected ? "켜짐" : "꺼짐";
        public string WrongText => true == expected ? "꺼짐" : "켜짐";
        public string WrongYaml => true == expected ? "0" : "1";
    }

    /// <summary>
    /// 빌드에 남는 스위치 중 배포 값이 정해진 것(Must)과, 이름만 디버그 같은 실제 로직(Allow)입니다.
    /// Must 는 프리팹·씬에서 값까지 봅니다.
    /// 필드 이름이 바뀌어 찾지 못하면 "검사 안 함"이 아니라 오류로 막습니다.
    /// </summary>
    private static readonly RuntimeField[] RUNTIME_FIELDS =
    {
        // 용광로가 SetRunning 으로 아지랑이를 직접 켜고 끄는 "수동 제어" 상태다. 이름만 Test 다.
        // 초기값이 켜져 있으면 용광로가 처음 신호를 주기 전까지 실제 가동과 무관하게 보이므로 꺼져 있어야 한다.
        RuntimeField.Must(typeof(BlastFurnaceHeatHaze), "isTestMode", false, "용광로 열기 효과가 실제 가동과 무관하게 표시됩니다"),
        RuntimeField.Allow(typeof(BlastFurnaceHeatHaze), "isTestRunning", "수동 제어 상태의 가동 여부"),

        // 부트스트랩(MainMenuScene)의 빌드 단위 스위치. 테스트하려고 바꾼 채 커밋되면 배포판 흐름이 달라진다.
        RuntimeField.Must(typeof(BootStrap), "isTempScene", false, "메인 메뉴로 넘어가지 않고 임시 씬 모드로 멈춥니다"),
        RuntimeField.Must(typeof(BootStrap), "enableTutorial", true, "새 게임이 튜토리얼 없이 바로 마을에서 시작합니다"),
        RuntimeField.Must(typeof(BootStrap), "enableSentry", true, "크래시 리포트가 수집되지 않습니다"),
        RuntimeField.Must(typeof(BootStrap), "enableGameAnalytics", true, "지표가 수집되지 않습니다"),
    };

    // 카멜 표기 경계로 본다. "latest" 처럼 단어 안에 우연히 들어간 test 는 잡지 않는다.
    private static readonly Regex DEBUG_NAME = new Regex(@"^(debug|test|cheat)|Debug|Test|Cheat");

    // 한 줄짜리 필드 선언: [특성] 접근자 타입 이름 (= 초기값);
    private static readonly Regex FIELD_DECL = new Regex(
        @"^\s*(?<attrs>(\[[^\]]*\]\s*)*)(?<mods>((public|private|protected|internal|static|readonly|const|new|volatile)\s+)*)(?<type>[\w<>\[\],\.\? ]+?)\s+(?<name>\w+)\s*(=(?!>)[^;]*)?;\s*(//.*)?$");

    /// <summary>
    /// 빌드를 실패시키는 검사라 에셋을 고치는 스트리퍼보다 먼저 돌아야 합니다.
    /// 캐릭터 스탯 가드(-925)와 플랫폼 정합성 가드(-900) 사이에 둡니다.
    /// </summary>
    public int callbackOrder => -920;

    public void OnPreprocessBuild(BuildReport _report)
    {
        List<string> _errors = Validate();

        if (0 == _errors.Count) return;

        string _detail = "  " + string.Join("\n  ", _errors);
        // 촬영용 빌드(BuildRunner.RunTrailer)는 배포하지 않지만 화면 표시 때문에 개발 빌드로 만들 수 없다. 개발 빌드처럼 경고만 남긴다.
        bool _isDevelopmentBuild = 0 != (_report.summary.options & BuildOptions.Development) || true == BuildRunner.IsTrailerBuildInProgress;

        if (true == _isDevelopmentBuild)
        {
            Debug.LogWarning($"{TAG} 디버그 스위치 문제가 있습니다. 개발·촬영용 빌드라 그대로 진행합니다.\n{_detail}");
            return;
        }

        throw new BuildFailedException(
            $"{TAG} 빌드를 중단했습니다. 디버그 스위치가 배포판에 섞일 수 있습니다.\n" +
            $"{_detail}\n" +
            "\n" +
            "- [소스] 디버그·테스트 필드는 #if UNITY_EDITOR 로 감싸십시오. 실제 게임 로직이면 이 가드의 RUNTIME_FIELDS 에 등록하십시오.\n" +
            "- [값] 해당 프리팹/씬의 인스펙터에서 배포 값으로 바꾸고 저장한 뒤 다시 빌드하십시오.");
    }

    /// <summary>
    /// 검사만 하고 결과를 돌려줍니다. 빌드 전처리, 검사 메뉴, 자동화가 같은 경로를 씁니다.
    /// </summary>
    public static List<string> Validate()
    {
        List<string> _errors = new List<string>();
        List<RuntimeField> _valueRules = new List<RuntimeField>();
        HashSet<string> _allowed = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < RUNTIME_FIELDS.Length; i++)
        {
            RuntimeField _rule = RUNTIME_FIELDS[i];

            if (false == HasField(_rule))
            {
                _errors.Add($"[목록] {_rule.type.Name}.{_rule.field} 필드를 찾지 못했습니다. 이름이 바뀌었다면 이 가드의 목록도 고치십시오.");
                continue;
            }

            _allowed.Add(_rule.type.Name + "." + _rule.field);
            if (true == _rule.checkValue) _valueRules.Add(_rule);
        }

        CheckTrailerDefineNotInProject(_errors);
        CheckSources(_allowed, _errors);
        CheckPrefabs(_valueRules, _errors);
        CheckScenes(_valueRules, _errors);

        return _errors;
    }

#region 소스 검사

    /// <summary>
    /// 촬영용 디파인이 프로젝트 설정에 들어가면 모든 빌드에 지도 전체 해금 같은 촬영 기능이 실립니다.
    /// 이 디파인은 BuildRunner.RunTrailer 가 그 빌드에만 붙이는 것이라, 프로젝트 설정에 있으면 안 됩니다.
    /// </summary>
    private static void CheckTrailerDefineNotInProject(List<string> _errors)
    {
        PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone, out string[] _defines);

        for (int i = 0; i < _defines.Length; i++)
        {
            if (BuildRunner.TRAILER_DEFINE != _defines[i]) continue;

            _errors.Add($"[디파인] {BuildRunner.TRAILER_DEFINE} 가 프로젝트 설정(Standalone)에 들어 있습니다. 촬영용 빌드 메뉴만 붙이는 디파인이니 Player Settings 에서 지우십시오.");
        }
    }

    private static void CheckSources(HashSet<string> _allowed, List<string> _errors)
    {
        string _root = ToAbsolute(SCRIPTS_ROOT);

        if (false == Directory.Exists(_root))
        {
            _errors.Add($"[소스] 스크립트 폴더를 찾지 못했습니다: {SCRIPTS_ROOT}");
            return;
        }

        string[] _files = Directory.GetFiles(_root, "*.cs", SearchOption.AllDirectories);

        for (int i = 0; i < _files.Length; i++)
        {
            string _relative = _files[i].Substring(_root.Length).Replace('\\', '/');

            // Editor 폴더는 빌드에 안 들어가고, Steamworks.NET 은 외부 코드다.
            if (true == _relative.Contains("/Editor/") || true == _relative.Contains("/Steamworks.NET/")) continue;

            ScanSource(SCRIPTS_ROOT + _relative, File.ReadAllLines(_files[i], Encoding.UTF8), _allowed, _errors);
        }
    }

    /// <summary>
    /// 전처리 블록을 따라가며, 배포 빌드에 컴파일되는 직렬화 필드 중 디버그 이름인 것을 찾습니다.
    /// 한 줄 선언만 봅니다. 이 프로젝트의 직렬화 필드는 모두 한 줄 선언입니다.
    /// </summary>
    private static void ScanSource(string _path, string[] _lines, HashSet<string> _allowed, List<string> _errors)
    {
        // 각 #if 블록이 "배포 빌드에서 빠지는가"를 쌓는다. 하나라도 빠지는 블록 안이면 안전하다.
        List<bool> _excluded = new List<bool>();
        List<bool> _elseExcluded = new List<bool>();
        bool _pendingSerialize = false;
        bool _pendingNonSerialized = false;
        bool _inBlockComment = false;
        string _className = Path.GetFileNameWithoutExtension(_path);

        for (int i = 0; i < _lines.Length; i++)
        {
            string _line = _lines[i];
            string _trim = _line.Trim();

            if (true == _inBlockComment)
            {
                if (true == _trim.Contains("*/")) _inBlockComment = false;
                continue;
            }

            if (true == _trim.StartsWith("/*", StringComparison.Ordinal))
            {
                _inBlockComment = false == _trim.Contains("*/");
                continue;
            }

            if (true == _trim.StartsWith("#if", StringComparison.Ordinal))
            {
                string _condition = _trim.Substring(3);
                _excluded.Add(IsExcludedFromRelease(_condition));
                _elseExcluded.Add(IsNegatedEditor(_condition));
                continue;
            }

            if (true == _trim.StartsWith("#elif", StringComparison.Ordinal))
            {
                if (0 < _excluded.Count)
                {
                    string _condition = _trim.Substring(5);
                    _excluded[_excluded.Count - 1] = IsExcludedFromRelease(_condition);
                    _elseExcluded[_elseExcluded.Count - 1] = false;
                }
                continue;
            }

            if (true == _trim.StartsWith("#else", StringComparison.Ordinal))
            {
                if (0 < _excluded.Count) _excluded[_excluded.Count - 1] = _elseExcluded[_elseExcluded.Count - 1];
                continue;
            }

            if (true == _trim.StartsWith("#endif", StringComparison.Ordinal))
            {
                if (0 < _excluded.Count)
                {
                    _excluded.RemoveAt(_excluded.Count - 1);
                    _elseExcluded.RemoveAt(_elseExcluded.Count - 1);
                }
                continue;
            }

            if (0 == _trim.Length || true == _trim.StartsWith("//", StringComparison.Ordinal)) continue;

            // 특성만 있는 줄([SerializeField] 등)은 다음 줄 선언에 붙는다.
            if (true == _trim.StartsWith("[", StringComparison.Ordinal) && true == _trim.EndsWith("]", StringComparison.Ordinal))
            {
                if (true == _trim.Contains("SerializeField") || true == _trim.Contains("SerializeReference")) _pendingSerialize = true;
                if (true == _trim.Contains("NonSerialized")) _pendingNonSerialized = true;
                continue;
            }

            bool _serializeAttr = _pendingSerialize;
            bool _nonSerialized = _pendingNonSerialized;
            _pendingSerialize = false;
            _pendingNonSerialized = false;

            if (true == _excluded.Contains(true)) continue;

            Match _match = FIELD_DECL.Match(_line);
            if (false == _match.Success) continue;

            string _name = _match.Groups["name"].Value;
            string _mods = _match.Groups["mods"].Value;
            string _attrs = _match.Groups["attrs"].Value;
            string _type = _match.Groups["type"].Value.Trim();

            if (false == DEBUG_NAME.IsMatch(_name)) continue;
            if ("return" == _type || "using" == _type) continue;

            bool _isStaticOrConst = _mods.Contains("static") || _mods.Contains("const") || _mods.Contains("readonly");
            bool _hasSerializeField = _serializeAttr || _attrs.Contains("SerializeField") || _attrs.Contains("SerializeReference");
            bool _isPublic = _mods.Contains("public");
            bool _isNonSerialized = _nonSerialized || _attrs.Contains("NonSerialized");

            bool _serialized = false == _isStaticOrConst && false == _isNonSerialized && (_hasSerializeField || _isPublic);
            if (false == _serialized) continue;

            if (true == _allowed.Contains(_className + "." + _name)) continue;

            _errors.Add($"[소스] {_className}.{_name} 가 배포 빌드에 남습니다 - {_path}:{i + 1}  (#if UNITY_EDITOR 로 감싸십시오)");
        }
    }

    /// <summary>
    /// #if 조건이 배포(비개발) 빌드에서 거짓인지 봅니다. || 로 묶인 모든 항이 UNITY_EDITOR 나 DEVELOPMENT_BUILD 여야 합니다.
    /// 예) UNITY_EDITOR, UNITY_EDITOR || DEVELOPMENT_BUILD, UNITY_EDITOR &amp;&amp; X 는 빠짐. UNITY_EDITOR || UNITY_STANDALONE 은 남음.
    /// </summary>
    private static bool IsExcludedFromRelease(string _condition)
    {
        string[] _terms = _condition.Split(new[] { "||" }, StringSplitOptions.None);

        for (int i = 0; i < _terms.Length; i++)
        {
            string _term = _terms[i].Trim().Trim('(', ')').Trim();

            if (true == _term.Contains("!")) return false;
            // BAOBAB_TRAILER 는 촬영용 빌드에만 extraScriptingDefines 로 붙는다. 프로젝트 설정에 들어가 있으면 Validate 가 따로 막는다.
            if (false == _term.Contains("UNITY_EDITOR") && false == _term.Contains("DEVELOPMENT_BUILD") && false == _term.Contains(BuildRunner.TRAILER_DEFINE)) return false;
        }

        return true;
    }

    /// <summary>
    /// #if !UNITY_EDITOR ... #else 의 #else 쪽은 에디터 전용입니다.
    /// </summary>
    private static bool IsNegatedEditor(string _condition)
    {
        return "!UNITY_EDITOR" == _condition.Replace(" ", "").Trim('(', ')');
    }

#endregion

#region 값 검사

    private static bool HasField(RuntimeField _rule)
    {
        return null != _rule.type.GetField(_rule.field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }

    private static void CheckPrefabs(List<RuntimeField> _rules, List<string> _errors)
    {
        if (0 == _rules.Count) return;

        string[] _guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });

        for (int i = 0; i < _guids.Length; i++)
        {
            string _path = AssetDatabase.GUIDToAssetPath(_guids[i]);
            string _text = ReadText(_path);

            if (null == _text) continue;

            GameObject _prefab = null;

            for (int r = 0; r < _rules.Count; r++)
            {
                // 필드 이름이 파일에 없으면 그 컴포넌트가 없거나 값이 기본값(false)이다. 프리팹을 다 열지 않으려는 거름망이다.
                // 중첩 프리팹 안쪽 값은 그 안쪽 프리팹 파일을 검사할 때 잡히고, 바깥에서 덮어쓴 값은 이 파일에 이름이 남는다.
                if (false == _text.Contains(_rules[r].field)) continue;

                if (null == _prefab) _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
                if (null == _prefab) break;

                Component[] _components = _prefab.GetComponentsInChildren(_rules[r].type, true);

                for (int c = 0; c < _components.Length; c++)
                {
                    if (true == IsWrong(_components[c], _rules[r]))
                    {
                        _errors.Add($"[값] {_rules[r].type.Name}.{_rules[r].field} {_rules[r].WrongText} (배포 값 {_rules[r].ExpectedText}) - {_path} ({HierarchyPath(_components[c].transform)})  → {_rules[r].reason}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 씬은 열지 않고 YAML로 봅니다. 씬에 직접 붙은 컴포넌트는 m_Script 의 스크립트 GUID로,
    /// 씬에서 프리팹 값을 덮어쓴 것은 propertyPath 와 원본 프리팹이 그 컴포넌트를 가졌는지로 판단합니다.
    /// 덮어쓰지 않은 프리팹 값은 CheckPrefabs 에서 이미 봤습니다.
    /// </summary>
    private static void CheckScenes(List<RuntimeField> _rules, List<string> _errors)
    {
        if (0 == _rules.Count) return;

        Dictionary<Type, string> _scriptGuids = new Dictionary<Type, string>();

        for (int r = 0; r < _rules.Count; r++)
        {
            if (true == _scriptGuids.ContainsKey(_rules[r].type)) continue;

            string _guid = FindScriptGuid(_rules[r].type);

            if (null == _guid) _errors.Add($"[값] {_rules[r].type.Name} 스크립트 파일을 찾지 못했습니다. 씬 검사를 할 수 없습니다.");
            _scriptGuids[_rules[r].type] = _guid;
        }

        EditorBuildSettingsScene[] _scenes = EditorBuildSettings.scenes;

        for (int s = 0; s < _scenes.Length; s++)
        {
            if (false == _scenes[s].enabled) continue;

            string _path = _scenes[s].path;
            string _text = ReadText(_path);

            if (null == _text)
            {
                _errors.Add($"[값] 빌드 씬 파일을 읽지 못했습니다: {_path}");
                continue;
            }

            string[] _lines = _text.Replace("\r\n", "\n").Split('\n');

            CheckSceneComponents(_path, _lines, _rules, _scriptGuids, _errors);
            CheckSceneOverrides(_path, _lines, _rules, _errors);
        }
    }

    private static void CheckSceneComponents(string _scenePath, string[] _lines, List<RuntimeField> _rules, Dictionary<Type, string> _scriptGuids, List<string> _errors)
    {
        string _blockScriptGuid = null;

        for (int i = 0; i < _lines.Length; i++)
        {
            string _line = _lines[i];

            if (true == _line.StartsWith("--- ", StringComparison.Ordinal))
            {
                _blockScriptGuid = null;
                continue;
            }

            if (true == _line.StartsWith("  m_Script: ", StringComparison.Ordinal))
            {
                _blockScriptGuid = ExtractGuid(_line);
                continue;
            }

            if (null == _blockScriptGuid) continue;

            for (int r = 0; r < _rules.Count; r++)
            {
                // 최상위 필드는 들여쓰기 두 칸이다. 더 깊으면 다른 구조체 안의 같은 이름 필드다.
                if (_blockScriptGuid != _scriptGuids[_rules[r].type]) continue;
                if (_line != "  " + _rules[r].field + ": " + _rules[r].WrongYaml) continue;

                _errors.Add($"[값] {_rules[r].type.Name}.{_rules[r].field} {_rules[r].WrongText} (배포 값 {_rules[r].ExpectedText}) - {_scenePath} (씬에 직접 배치, {i + 1}행)  → {_rules[r].reason}");
            }
        }
    }

    private static void CheckSceneOverrides(string _scenePath, string[] _lines, List<RuntimeField> _rules, List<string> _errors)
    {
        string _targetGuid = null;

        for (int i = 0; i < _lines.Length; i++)
        {
            string _line = _lines[i].Trim();

            if (true == _line.StartsWith("- target: ", StringComparison.Ordinal))
            {
                _targetGuid = ExtractGuid(_line);
                continue;
            }

            if (false == _line.StartsWith("propertyPath: ", StringComparison.Ordinal)) continue;
            if (i + 1 >= _lines.Length) continue;

            string _property = _line.Substring("propertyPath: ".Length);
            string _value = _lines[i + 1].Trim();

            for (int r = 0; r < _rules.Count; r++)
            {
                if (_property != _rules[r].field) continue;
                if ("value: " + _rules[r].WrongYaml != _value) continue;
                if (false == PrefabHasComponent(_targetGuid, _rules[r].type)) continue;

                _errors.Add($"[값] {_rules[r].type.Name}.{_rules[r].field} {_rules[r].WrongText} (배포 값 {_rules[r].ExpectedText}) - {_scenePath} (씬에서 프리팹 값 덮어씀, {i + 1}행)  → {_rules[r].reason}");
            }
        }
    }

#endregion

#region 도우미

    private static bool IsWrong(Component _component, RuntimeField _rule)
    {
        SerializedObject _so = new SerializedObject(_component);
        SerializedProperty _property = _so.FindProperty(_rule.field);

        return null != _property && SerializedPropertyType.Boolean == _property.propertyType && _rule.expected != _property.boolValue;
    }

    private static bool PrefabHasComponent(string _guid, Type _type)
    {
        if (null == _guid) return false;

        string _path = AssetDatabase.GUIDToAssetPath(_guid);
        if (false == _path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) return false;

        GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_path);

        return null != _prefab && 0 < _prefab.GetComponentsInChildren(_type, true).Length;
    }

    private static string FindScriptGuid(Type _type)
    {
        string[] _guids = AssetDatabase.FindAssets($"t:MonoScript {_type.Name}");

        for (int i = 0; i < _guids.Length; i++)
        {
            MonoScript _script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(_guids[i]));

            if (null != _script && _type == _script.GetClass()) return _guids[i];
        }

        return null;
    }

    /// <summary>
    /// "{fileID: 11500000, guid: 0123..., type: 3}" 에서 guid 값을 꺼냅니다.
    /// </summary>
    private static string ExtractGuid(string _line)
    {
        int _start = _line.IndexOf("guid: ", StringComparison.Ordinal);
        if (0 > _start) return null;

        _start += "guid: ".Length;
        int _end = _line.IndexOfAny(new[] { ',', '}' }, _start);

        return 0 > _end ? _line.Substring(_start).Trim() : _line.Substring(_start, _end - _start).Trim();
    }

    private static string ReadText(string _assetPath)
    {
        string _path = ToAbsolute(_assetPath);

        return true == File.Exists(_path) ? File.ReadAllText(_path, Encoding.UTF8) : null;
    }

    private static string ToAbsolute(string _assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", _assetPath));
    }

    private static string HierarchyPath(Transform _transform)
    {
        string _path = _transform.name;

        for (Transform _parent = _transform.parent; null != _parent; _parent = _parent.parent)
        {
            _path = _parent.name + "/" + _path;
        }

        return _path;
    }

#endregion

#region 메뉴

    [MenuItem(MENU_CHECK, false, 68)]
    private static void CheckFromMenu()
    {
        List<string> _errors = Validate();

        if (0 == _errors.Count)
        {
            EditorUtility.DisplayDialog("디버그 스위치 검사", "배포 빌드에 남는 디버그 스위치가 없습니다. 이대로 빌드할 수 있습니다.", "확인");
            return;
        }

        Debug.LogError($"{TAG} 디버그 스위치 검사 실패\n  " + string.Join("\n  ", _errors));
        EditorUtility.DisplayDialog("디버그 스위치 검사", $"문제 {_errors.Count}건. 자세한 내용은 콘솔을 보십시오.\n\n" +
                                    string.Join("\n", _errors.GetRange(0, Math.Min(8, _errors.Count))), "확인");
    }

#endregion
}
