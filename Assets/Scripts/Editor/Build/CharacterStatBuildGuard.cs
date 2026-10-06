using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 캐릭터 프리팹(Character.prefab)의 StatComponent 수치가 정상일 때만 빌드를 통과시킵니다.
///
/// [왜 필요한가]
/// 2026-10-01, 드론 VFX를 확인하려고 기본 캐릭터의 droneCount를 1로, 과열 스위치 셋을 켠 채 커밋됐습니다(c5c56508).
/// 뒤의 머지에서 과열 스위치만 되돌리고 드론은 남아, "스킬을 찍지 않아도 처음부터 드론이 따라다니는" 상태가
/// 데모와 정식 모두에 실릴 뻔했습니다. 프리팹 수치는 커밋 diff에서 한 줄로만 보여 리뷰로 잡기 어렵습니다.
///
/// [두 겹으로 봅니다]
/// 1) 고정 규칙 - 모든 배포 빌드
///    스킬로만 얻는 능력(드론·부메랑 개수, 과열·회전 베기 같은 해금 스위치)은 기본 캐릭터에서
///    반드시 "획득 전" 값(0 / 꺼짐)이어야 합니다. 시작 돈 덮어쓰기(치트)도 꺼져 있어야 합니다.
///    이 규칙에 정답이 있으므로 사람이 승인할 여지 없이 막습니다.
/// 2) 승인 기준값 대조 - 데모 배포 빌드
///    데미지·쿨타임 같은 밸런스 수치는 정답이 없으므로, 사람이 승인한 기준값 파일
///    (CharacterStatBaseline_Demo.txt)과 하나라도 다르면 막습니다. 출시된 데모의 밸런스는
///    "의도해서 바꾼 것"만 나가야 하기 때문입니다. 의도한 변경이면
///    Tools > 빌드 > 캐릭터 스탯 기준값 승인 (데모) 로 기준값을 갱신하고 그 파일을 함께 커밋합니다.
///
/// [Development Build]
/// 테스트 중에는 일부러 드론을 켜고 빌드할 수 있어야 하므로, 개발 빌드에서는 경고만 남기고 통과합니다.
/// 개발 빌드는 BuildRunner(배포용)로 만들 수 없고 DemoContentStripper·BuildOutputSanitizer도 건너뛰므로
/// 배포물로 나갈 수 없습니다.
///
/// [어디를 보는가]
/// StatComponent는 Character.prefab에만 붙어 있고, 캐릭터는 GameInstaller가 이 프리팹 에셋을 생성합니다.
/// 씬에 직접 배치된 캐릭터(=씬 오버라이드)는 없으므로 프리팹 에셋 하나만 보면 됩니다(2026-10-05 확인).
/// </summary>
public class CharacterStatBuildGuard : IPreprocessBuildWithReport
{
    public const string PREFAB_PATH = "Assets/Prefabs/Objects/Character/Character.prefab";
    public const string BASELINE_PATH = "Assets/Scripts/Editor/Build/CharacterStatBaseline_Demo.txt";

    private const string MENU_APPROVE = "Tools/빌드/캐릭터 스탯 기준값 승인 (데모)";
    private const string MENU_CHECK = "Tools/빌드/캐릭터 스탯 검사";
    private const string TAG = "[CharacterStatGuard]";

    /// <summary>
    /// 스킬로만 얻는 능력입니다. 기본 캐릭터에서는 반드시 이 값이어야 합니다.
    /// 필드 이름이 바뀌어 프리팹에서 찾지 못하면 "검사 안 함"이 아니라 오류로 막습니다.
    /// 조용히 빠지면 이 가드가 있다는 믿음만 남기 때문입니다.
    /// </summary>
    private static readonly KeyValuePair<string, string>[] REQUIRED_VALUES =
    {
        // 개수: 0이면 해당 무기가 없다
        new KeyValuePair<string, string>("droneCount", "0"),
        new KeyValuePair<string, string>("droneChainCount", "0"),
        new KeyValuePair<string, string>("boomerangCount", "0"),
        new KeyValuePair<string, string>("ricochetCnt", "0"),
        new KeyValuePair<string, string>("shockWaveChance", "0"),

        // 해금 스위치
        new KeyValuePair<string, string>("bOverheat", "0"),
        new KeyValuePair<string, string>("bOverheatPermanent", "0"),
        new KeyValuePair<string, string>("bWhirlWind", "0"),
        new KeyValuePair<string, string>("bMultiAttack", "0"),
        new KeyValuePair<string, string>("bCanHunting", "0"),
        new KeyValuePair<string, string>("bShockWaveCritical", "0"),
        new KeyValuePair<string, string>("bShockWaveEnforcement", "0"),
        new KeyValuePair<string, string>("bShockWaveMastery", "0"),
        new KeyValuePair<string, string>("bShockWaveOverheatBoost", "0"),
        new KeyValuePair<string, string>("bBoomerangCritical", "0"),
        new KeyValuePair<string, string>("bBoomerangOverheatBoost", "0"),
        new KeyValuePair<string, string>("bDroneOverheatBoost", "0"),

        // 시작 돈 덮어쓰기는 개발용 치트다
        new KeyValuePair<string, string>("bOverrideStartMoney", "0"),
    };

    /// <summary>
    /// 빌드를 실패시키는 검사라 에셋을 고치는 스트리퍼(AbilityBuildVariantStripper -100, DemoContentStripper 0)보다
    /// 먼저 돌아야 합니다. 자동저장 가드(-950)와 플랫폼 정합성 가드(-900) 사이에 둡니다.
    /// </summary>
    public int callbackOrder => -925;

    public void OnPreprocessBuild(BuildReport _report)
    {
        BuildRelease _release = PlatformBuildModeSwitcher.CurrentRelease;
        List<string> _errors = Validate(_release);

        if (0 == _errors.Count) return;

        string _detail = "  " + string.Join("\n  ", _errors);
        // 촬영용 빌드(BuildRunner.RunTrailer)는 배포하지 않지만 화면 표시 때문에 개발 빌드로 만들 수 없다. 개발 빌드처럼 경고만 남긴다.
        bool _isDevelopmentBuild = 0 != (_report.summary.options & BuildOptions.Development) || true == BuildRunner.IsTrailerBuildInProgress;

        if (true == _isDevelopmentBuild)
        {
            Debug.LogWarning($"{TAG} 캐릭터 수치가 배포 기준과 다릅니다. 개발·촬영용 빌드라 그대로 진행합니다.\n{_detail}");
            return;
        }

        throw new BuildFailedException(
            $"{TAG} 빌드를 중단했습니다. 캐릭터 프리팹 수치가 정상이 아닙니다.\n" +
            $"{_detail}\n" +
            "\n" +
            $"대상: {PREFAB_PATH} 의 StatComponent\n" +
            "- [고정 규칙] 항목은 테스트 값이 남은 것입니다. 프리팹을 고친 뒤 다시 빌드하십시오.\n" +
            "- [기준값] 항목은 승인된 밸런스와 다른 것입니다. 의도한 변경이면\n" +
            $"  {MENU_APPROVE} 로 기준값을 갱신하고 그 파일을 함께 커밋하십시오.");
    }

    /// <summary>
    /// 검사만 하고 결과를 돌려줍니다. 빌드 전처리, 검사 메뉴, 자동화가 같은 경로를 씁니다.
    /// </summary>
    public static List<string> Validate(BuildRelease _release)
    {
        List<string> _errors = new List<string>();
        Dictionary<string, string> _current = ReadCurrent(_errors);

        if (null == _current) return _errors;

        CheckRequiredValues(_current, _errors);

        if (BuildRelease.Demo == _release)
        {
            CheckBaseline(_current, _errors);
        }

        return _errors;
    }

#region 검사

    private static void CheckRequiredValues(Dictionary<string, string> _current, List<string> _errors)
    {
        for (int i = 0; i < REQUIRED_VALUES.Length; i++)
        {
            string _name = REQUIRED_VALUES[i].Key;
            string _expected = REQUIRED_VALUES[i].Value;

            if (false == _current.TryGetValue(_name, out string _actual))
            {
                _errors.Add($"[고정 규칙] '{_name}' 필드를 찾지 못했습니다. 이름이 바뀌었다면 이 가드의 목록도 고치십시오.");
                continue;
            }

            if (false == SameValue(_actual, _expected))
            {
                _errors.Add($"[고정 규칙] {_name} = {_actual} (기본 캐릭터는 {_expected} 이어야 함. 스킬로만 얻는 능력입니다)");
            }
        }
    }

    private static void CheckBaseline(Dictionary<string, string> _current, List<string> _errors)
    {
        Dictionary<string, string> _baseline = ReadBaseline();

        if (null == _baseline)
        {
            _errors.Add($"[기준값] 승인된 기준값 파일이 없습니다: {BASELINE_PATH}  ({MENU_APPROVE} 로 만드십시오)");
            return;
        }

        foreach (KeyValuePair<string, string> _pair in _baseline)
        {
            if (false == _current.TryGetValue(_pair.Key, out string _actual))
            {
                _errors.Add($"[기준값] {_pair.Key} 가 프리팹에서 사라졌습니다 (기준 {_pair.Value})");
            }
            else if (false == SameValue(_actual, _pair.Value))
            {
                _errors.Add($"[기준값] {_pair.Key} = {_actual} (승인된 값 {_pair.Value})");
            }
        }

        foreach (KeyValuePair<string, string> _pair in _current)
        {
            if (false == _baseline.ContainsKey(_pair.Key))
            {
                _errors.Add($"[기준값] {_pair.Key} = {_pair.Value} 는 새로 생긴 수치라 아직 승인되지 않았습니다");
            }
        }
    }

    private static bool SameValue(string _a, string _b)
    {
        if (_a == _b) return true;

        bool _okA = double.TryParse(_a, NumberStyles.Float, CultureInfo.InvariantCulture, out double _da);
        bool _okB = double.TryParse(_b, NumberStyles.Float, CultureInfo.InvariantCulture, out double _db);

        return true == _okA && true == _okB && Math.Abs(_da - _db) < 1e-6;
    }

#endregion

#region 읽기 / 쓰기

    /// <summary>
    /// StatComponent의 직렬화된 숫자·불 필드를 전부 읽습니다. 숨김 필드도 포함하려고 Next를 씁니다.
    /// 오브젝트 참조와 구조체는 밸런스 수치가 아니므로 제외합니다.
    /// </summary>
    private static Dictionary<string, string> ReadCurrent(List<string> _errors)
    {
        GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);

        if (null == _prefab)
        {
            _errors.Add($"캐릭터 프리팹을 찾지 못했습니다: {PREFAB_PATH}");
            return null;
        }

        StatComponent _stat = _prefab.GetComponentInChildren<StatComponent>(true);

        if (null == _stat)
        {
            _errors.Add($"캐릭터 프리팹에서 StatComponent를 찾지 못했습니다: {PREFAB_PATH}");
            return null;
        }

        Dictionary<string, string> _values = new Dictionary<string, string>();
        SerializedObject _so = new SerializedObject(_stat);
        SerializedProperty _it = _so.GetIterator();
        bool _enterChildren = true;

        while (true == _it.Next(_enterChildren))
        {
            _enterChildren = false;

            switch (_it.propertyType)
            {
                case SerializedPropertyType.Float:
                    _values[_it.name] = _it.floatValue.ToString("R", CultureInfo.InvariantCulture);
                    break;
                case SerializedPropertyType.Integer:
                    _values[_it.name] = _it.longValue.ToString(CultureInfo.InvariantCulture);
                    break;
                case SerializedPropertyType.Boolean:
                    _values[_it.name] = true == _it.boolValue ? "1" : "0";
                    break;
            }
        }

        // m_ 로 시작하는 Unity 내부 필드(m_Enabled 등)는 밸런스 수치가 아니다.
        List<string> _internal = new List<string>();
        foreach (string _key in _values.Keys)
        {
            if (true == _key.StartsWith("m_", StringComparison.Ordinal)) _internal.Add(_key);
        }
        for (int i = 0; i < _internal.Count; i++) _values.Remove(_internal[i]);

        return _values;
    }

    private static Dictionary<string, string> ReadBaseline()
    {
        string _path = ToAbsolute(BASELINE_PATH);

        if (false == File.Exists(_path)) return null;

        Dictionary<string, string> _values = new Dictionary<string, string>();
        string[] _lines = File.ReadAllLines(_path, Encoding.UTF8);

        for (int i = 0; i < _lines.Length; i++)
        {
            string _line = _lines[i].Trim();

            if (0 == _line.Length || '#' == _line[0]) continue;

            int _eq = _line.IndexOf('=');
            if (0 >= _eq) continue;

            _values[_line.Substring(0, _eq).Trim()] = _line.Substring(_eq + 1).Trim();
        }

        return _values;
    }

    /// <summary>
    /// 현재 프리팹 값을 기준값으로 저장합니다. 고정 규칙을 어긴 상태로는 승인하지 않습니다.
    /// 테스트 값이 기준값으로 굳어 버리면 이 가드가 거꾸로 그 값을 지키게 되기 때문입니다.
    /// </summary>
    public static bool ApproveBaseline(out string _message)
    {
        List<string> _errors = new List<string>();
        Dictionary<string, string> _current = ReadCurrent(_errors);

        if (null != _current) CheckRequiredValues(_current, _errors);

        if (0 != _errors.Count)
        {
            _message = "고정 규칙을 어긴 값이 있어 승인할 수 없습니다. 먼저 프리팹을 고치십시오.\n  " + string.Join("\n  ", _errors);
            return false;
        }

        List<string> _keys = new List<string>(_current.Keys);
        _keys.Sort(StringComparer.Ordinal);

        StringBuilder _sb = new StringBuilder();
        _sb.AppendLine("# 데모 배포 빌드의 캐릭터 스탯 승인 기준값입니다. CharacterStatBuildGuard 가 이 값과 대조합니다.");
        _sb.AppendLine("# 직접 고치지 말고 Tools > 빌드 > 캐릭터 스탯 기준값 승인 (데모) 로 갱신한 뒤, 프리팹과 함께 커밋하십시오.");
        _sb.AppendLine($"# 대상: {PREFAB_PATH} (StatComponent)");
        _sb.AppendLine($"# 승인: {DateTime.Now:yyyy-MM-dd HH:mm}");

        for (int i = 0; i < _keys.Count; i++)
        {
            _sb.Append(_keys[i]).Append('=').AppendLine(_current[_keys[i]]);
        }

        File.WriteAllText(ToAbsolute(BASELINE_PATH), _sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(BASELINE_PATH);

        _message = $"기준값 {_keys.Count}개를 저장했습니다: {BASELINE_PATH}";
        return true;
    }

    private static string ToAbsolute(string _assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", _assetPath));
    }

#endregion

#region 메뉴

    [MenuItem(MENU_CHECK, false, 66)]
    private static void CheckFromMenu()
    {
        BuildRelease _release = PlatformBuildModeSwitcher.CurrentRelease;
        List<string> _errors = Validate(_release);
        string _mode = BuildRelease.Demo == _release ? "데모 (고정 규칙 + 기준값)" : "정식 (고정 규칙)";

        if (0 == _errors.Count)
        {
            EditorUtility.DisplayDialog("캐릭터 스탯 검사", $"{_mode}\n\n문제 없습니다. 이대로 빌드할 수 있습니다.", "확인");
            return;
        }

        Debug.LogError($"{TAG} 캐릭터 스탯 검사 실패 ({_mode})\n  " + string.Join("\n  ", _errors));
        EditorUtility.DisplayDialog("캐릭터 스탯 검사", $"{_mode}\n\n문제 {_errors.Count}건. 자세한 내용은 콘솔을 보십시오.\n\n" +
                                    string.Join("\n", _errors.GetRange(0, Math.Min(8, _errors.Count))), "확인");
    }

    [MenuItem(MENU_APPROVE, false, 67)]
    private static void ApproveFromMenu()
    {
        List<string> _diff = new List<string>();
        Dictionary<string, string> _current = ReadCurrent(_diff);

        if (null != _current) CheckBaseline(_current, _diff);

        string _summary = 0 == _diff.Count
            ? "기준값과 현재 값이 같습니다. 다시 저장만 합니다."
            : $"아래 {_diff.Count}건을 승인합니다.\n\n" + string.Join("\n", _diff.GetRange(0, Math.Min(12, _diff.Count)));

        bool _ok = EditorUtility.DisplayDialog("캐릭터 스탯 기준값 승인 (데모)",
            _summary + "\n\n의도한 밸런스 변경이 맞습니까?", "승인", "취소");

        if (false == _ok) return;

        bool _saved = ApproveBaseline(out string _message);

        if (true == _saved) Debug.Log($"{TAG} {_message}");
        else Debug.LogError($"{TAG} {_message}");

        EditorUtility.DisplayDialog("캐릭터 스탯 기준값 승인 (데모)", _message, "확인");
    }

#endregion
}
