using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using PresentationLayer.UISystem;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 빌드에도 그대로 남는 디버그 스위치(인스펙터 체크박스)가 켜져 있으면 배포 빌드를 막습니다.
///
/// [왜 필요한가]
/// #if UNITY_EDITOR 로 감싼 디버그 필드(예: UIView_MainMenu.bForceShowInitialSetupPopup)는 빌드에서 코드째 빠지므로
/// 프리팹에 켜져 있어도 상관없습니다. 하지만 아래 목록의 스위치는 감싸져 있지 않아, 누가 테스트하려고 켠 채 커밋하면
/// 배포판에서 실제로 동작합니다. 특히 맵 전체 해금 두 개는 데모에서 정식판 맵을 전부 열어 버립니다.
/// 프리팹 체크박스 하나는 커밋 diff에서 "0 → 1" 한 줄로만 보여 리뷰로 잡기 어렵습니다(캐릭터 droneCount 사고와 같은 구조).
///
/// [어디를 보는가]
/// - Assets 아래 모든 프리팹: 프리팹 에셋을 열어 해당 컴포넌트의 값을 읽습니다(중첩 프리팹·변형 프리팹 값까지 반영됨).
/// - 빌드에 들어가는 씬: 씬에 직접 붙은 컴포넌트와, 씬에서 프리팹 값을 덮어쓴 경우(오버라이드)를 YAML에서 읽습니다.
///   씬을 열지 않는 이유는 빌드 중에 현재 열린 씬을 바꾸면 안 되기 때문입니다.
/// 빌드에 안 쓰는 프리팹에 켜져 있어도 막습니다. 어느 프리팹이 실리는지 따지다 놓치는 것보다, 꺼 두는 쪽이 싸기 때문입니다.
///
/// [Development Build]
/// 개발 빌드에서 일부러 켜고 확인할 수 있도록 경고만 남깁니다(CharacterStatBuildGuard 와 같은 정책).
///
/// [스위치를 새로 만들 때]
/// 가능하면 #if UNITY_EDITOR 로 감싸 빌드에서 빼는 것이 가장 안전합니다. 빌드에도 남아야 하는 스위치라면 아래 목록에 추가하십시오.
/// </summary>
public class DebugSwitchBuildGuard : IPreprocessBuildWithReport
{
    private const string MENU_CHECK = "Tools/빌드/디버그 스위치 검사";
    private const string TAG = "[DebugSwitchGuard]";

    private struct Rule
    {
        public Type type;
        public string field;
        public string effect;

        public Rule(Type _type, string _field, string _effect)
        {
            type = _type;
            field = _field;
            effect = _effect;
        }
    }

    /// <summary>
    /// 배포 빌드에서 반드시 꺼져 있어야 하는 bool 스위치입니다.
    /// 필드 이름이 바뀌면 "검사 안 함"이 아니라 오류로 막습니다. 조용히 빠지면 이 가드가 있다는 믿음만 남기 때문입니다.
    /// </summary>
    private static readonly Rule[] RULES =
    {
        // 게임 진행에 영향
        new Rule(typeof(DensityManager), "debugUnlockAllMaps", "모든 맵이 해금됩니다"),
        new Rule(typeof(HUD_PopupNav_Main), "debugForceUnlockAll", "지도를 열면 모든 맵이 해금됩니다"),
        new Rule(typeof(AudioManager), "enableDebugSound", "` 키로 디버그 사운드가 재생됩니다"),

        // 연출이 테스트 값으로 고정됨
        new Rule(typeof(BlastFurnaceHeatHaze), "isTestMode", "용광로 열기 효과가 실제 가동과 무관하게 테스트 상태를 따릅니다"),
        new Rule(typeof(GlobalSpriteDirectionalLight), "debugUseManualLighting", "시간대 조명 대신 수동 조명이 쓰입니다"),
        new Rule(typeof(OffsetShadow), "useDebugValues", "그림자가 디버그 각도·길이로 고정됩니다"),
        new Rule(typeof(RaymarchingShadow), "useDebugValues", "그림자가 디버그 각도·길이로 고정됩니다"),

        // 디버그 로그
        new Rule(typeof(LogContainer), "bDebug", "인벤토리 디버그 로그가 출력됩니다"),
        new Rule(typeof(TMPAnimation), "isDebug", "텍스트 애니메이션 디버그 로그가 출력됩니다"),
        new Rule(typeof(InDungeonUnitSpawner), "enableLJDebugLog", "벌목 NPC 디버그 로그가 출력됩니다"),
    };

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
        bool _isDevelopmentBuild = 0 != (_report.summary.options & BuildOptions.Development);

        if (true == _isDevelopmentBuild)
        {
            Debug.LogWarning($"{TAG} 디버그 스위치가 켜져 있습니다. Development Build라 그대로 진행합니다.\n{_detail}");
            return;
        }

        throw new BuildFailedException(
            $"{TAG} 빌드를 중단했습니다. 디버그 스위치가 켜져 있습니다.\n" +
            $"{_detail}\n" +
            "\n" +
            "해당 프리팹/씬의 인스펙터에서 체크를 끄고 저장한 뒤 다시 빌드하십시오.");
    }

    /// <summary>
    /// 검사만 하고 결과를 돌려줍니다. 빌드 전처리, 검사 메뉴, 자동화가 같은 경로를 씁니다.
    /// </summary>
    public static List<string> Validate()
    {
        List<string> _errors = new List<string>();
        List<Rule> _rules = new List<Rule>();

        for (int i = 0; i < RULES.Length; i++)
        {
            if (true == HasBoolField(RULES[i])) _rules.Add(RULES[i]);
            else _errors.Add($"{RULES[i].type.Name}.{RULES[i].field} 필드를 찾지 못했습니다. 이름이 바뀌었다면 이 가드의 목록도 고치십시오.");
        }

        CheckPrefabs(_rules, _errors);
        CheckScenes(_rules, _errors);

        return _errors;
    }

#region 검사

    private static bool HasBoolField(Rule _rule)
    {
        FieldInfo _info = _rule.type.GetField(_rule.field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return null != _info && typeof(bool) == _info.FieldType;
    }

    private static void CheckPrefabs(List<Rule> _rules, List<string> _errors)
    {
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
                    if (true == IsOn(_components[c], _rules[r].field))
                    {
                        _errors.Add($"{_rules[r].type.Name}.{_rules[r].field} 켜짐 - {_path} ({HierarchyPath(_components[c].transform)})  → {_rules[r].effect}");
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
    private static void CheckScenes(List<Rule> _rules, List<string> _errors)
    {
        Dictionary<Type, string> _scriptGuids = new Dictionary<Type, string>();

        for (int r = 0; r < _rules.Count; r++)
        {
            if (true == _scriptGuids.ContainsKey(_rules[r].type)) continue;

            string _guid = FindScriptGuid(_rules[r].type);

            if (null == _guid) _errors.Add($"{_rules[r].type.Name} 스크립트 파일을 찾지 못했습니다. 씬 검사를 할 수 없습니다.");
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
                _errors.Add($"빌드 씬 파일을 읽지 못했습니다: {_path}");
                continue;
            }

            string[] _lines = _text.Replace("\r\n", "\n").Split('\n');

            CheckSceneComponents(_path, _lines, _rules, _scriptGuids, _errors);
            CheckSceneOverrides(_path, _lines, _rules, _errors);
        }
    }

    private static void CheckSceneComponents(string _scenePath, string[] _lines, List<Rule> _rules, Dictionary<Type, string> _scriptGuids, List<string> _errors)
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
                if (_line != "  " + _rules[r].field + ": 1") continue;

                _errors.Add($"{_rules[r].type.Name}.{_rules[r].field} 켜짐 - {_scenePath} (씬에 직접 배치, {i + 1}행)  → {_rules[r].effect}");
            }
        }
    }

    private static void CheckSceneOverrides(string _scenePath, string[] _lines, List<Rule> _rules, List<string> _errors)
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
            if (i + 1 >= _lines.Length || "value: 1" != _lines[i + 1].Trim()) continue;

            string _property = _line.Substring("propertyPath: ".Length);

            for (int r = 0; r < _rules.Count; r++)
            {
                if (_property != _rules[r].field) continue;
                if (false == PrefabHasComponent(_targetGuid, _rules[r].type)) continue;

                _errors.Add($"{_rules[r].type.Name}.{_rules[r].field} 켜짐 - {_scenePath} (씬에서 프리팹 값 덮어씀, {i + 1}행)  → {_rules[r].effect}");
            }
        }
    }

#endregion

#region 도우미

    private static bool IsOn(Component _component, string _field)
    {
        SerializedObject _so = new SerializedObject(_component);
        SerializedProperty _property = _so.FindProperty(_field);

        return null != _property && SerializedPropertyType.Boolean == _property.propertyType && true == _property.boolValue;
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
        string _path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", _assetPath));

        return true == File.Exists(_path) ? File.ReadAllText(_path, Encoding.UTF8) : null;
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
            EditorUtility.DisplayDialog("디버그 스위치 검사", $"스위치 {RULES.Length}종 모두 꺼져 있습니다. 이대로 빌드할 수 있습니다.", "확인");
            return;
        }

        Debug.LogError($"{TAG} 디버그 스위치 검사 실패\n  " + string.Join("\n  ", _errors));
        EditorUtility.DisplayDialog("디버그 스위치 검사", $"문제 {_errors.Count}건. 자세한 내용은 콘솔을 보십시오.\n\n" +
                                    string.Join("\n", _errors.GetRange(0, Math.Min(8, _errors.Count))), "확인");
    }

#endregion
}
