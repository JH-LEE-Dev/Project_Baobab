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
/// 게임의 모든 스탯(캐릭터·무기·나무·동물·아이템·설치물·스킬·능력 노드·밀도·가격 데이터)이
/// 사람이 승인한 기준값과 같을 때만 배포 빌드를 통과시킵니다.
///
/// [왜 필요한가]
/// 2026-10-01, 드론 VFX를 확인하려고 기본 캐릭터의 droneCount를 1로 켠 채 커밋됐습니다(c5c56508).
/// 2026-10-06, 트레일러 촬영을 위해 충격파 4스테이지 수치(30000 → 50000)와 영상용 스킬 노드가 정식 스킬 DB에 커밋됐습니다(74d3eeb9, 343cc202).
/// 데이터 수치는 커밋 diff에서 한 줄로만 보여 리뷰로 잡기 어렵고, 테스트·촬영용 값이 그대로 배포물에 실릴 수 있습니다.
///
/// [두 겹으로 봅니다]
/// 1) 고정 규칙 - 모든 배포 빌드
///    스킬로만 얻는 능력(드론·부메랑 개수, 과열·회전 베기 같은 해금 스위치)은 기본 캐릭터에서
///    반드시 "획득 전" 값(0 / 꺼짐)이어야 합니다. 시작 돈 덮어쓰기(치트)도 꺼져 있어야 합니다.
///    이 규칙에 정답이 있으므로 사람이 승인할 여지 없이 막습니다.
/// 2) 승인 기준값 대조 - 데모·정식 배포 빌드 각각
///    밸런스 수치는 정답이 없으므로, 사람이 승인한 기준값 파일(StatBaseline_Demo.txt / StatBaseline_Full.txt)과
///    하나라도 다르면 막습니다. 배포물에는 "의도해서 바꾼 것"만 나가야 하기 때문입니다. 의도한 변경이면
///    Tools > 빌드 > 스탯 기준값 승인 (데모|정식) 으로 기준값을 갱신하고 그 파일을 함께 커밋합니다.
///
/// [어디를 보는가] (ReadCurrent)
/// - 프리팹: Assets/Prefabs/Objects 아래 게임 오브젝트 전부(장식·카메라·조명·날씨·타일맵 등 PREFAB_EXCLUDED_FOLDERS 제외)와
///   GameInstaller. 프리팹 안의 게임 스크립트(Assets/Scripts) 컴포넌트가 가진 숫자·불·열거형·벡터 값 전부.
///   캐릭터·충격파·부메랑·드론·나무·토끼·아이템·용광로·상점 등이 여기에 들어갑니다.
///   씬에 직접 놓인 인스턴스는 없습니다(GameInstaller 는 BootStrap 이 프리팹을 생성하고, TownScene 의 Tree 는 마을 장식). 2026-10-07 확인.
/// - 데이터: Assets/Scriptable Obj 아래 ScriptableObject 전부(소리·번역·입력·연출·타일·나무 외형 제외).
///   나무 체력, 원목 가격, 밀도, 던전 스태미나, 스킬 커맨드 등.
/// - 배포별 데이터: AbilityBuildVariantData 가 가리키는 그 배포의 스킬 DB(SkillDataBase_Demo|Full)와
///   능력 노드 DB(AbilityNodeDatabase_Demo|Full.json). 노드 DB는 화면 배치(gridX/Y, 선 경로)는 빼고
///   노드 존재·선행 노드·필요 프레스티지 레벨만 봅니다.
///
/// [Development Build / 촬영용 빌드]
/// 테스트 중에는 일부러 수치를 바꾸고 빌드할 수 있어야 하므로, 개발·촬영용 빌드에서는 경고만 남기고 통과합니다.
/// </summary>
public class GameStatBuildGuard : IPreprocessBuildWithReport
{
    public const string CHARACTER_PREFAB_PATH = "Assets/Prefabs/Objects/Character/Character.prefab";
    private const string BASELINE_DIR = "Assets/Scripts/Editor/Build/";
    private const string VARIANT_DATA_PATH = "Assets/Scriptable Obj/SkillData/AbilityBuildVariantData.asset";

    private const string PREFAB_ROOT = "Assets/Prefabs/Objects/";
    private const string DATA_ROOT = "Assets/Scriptable Obj/";
    private const string GAME_SCRIPT_ROOT = "Assets/Scripts/";

    private const string MENU_CHECK = "Tools/빌드/스탯 검사";
    private const string MENU_APPROVE_DEMO = "Tools/빌드/스탯 기준값 승인 (데모)";
    private const string MENU_APPROVE_FULL = "Tools/빌드/스탯 기준값 승인 (정식)";
    private const string TAG = "[StatGuard]";

    /// <summary>빌드 실패 메시지에 담을 최대 줄 수입니다. 전체 목록은 스탯 검사 메뉴가 콘솔에 남깁니다.</summary>
    private const int MAX_REPORT_LINES = 40;

    /// <summary>
    /// Assets/Prefabs/Objects 바로 아래에서 스탯이 없는 폴더입니다. 화면 연출만 있는 오브젝트라 빼서 승인 잡음을 줄입니다.
    /// 여기 없는 새 폴더는 자동으로 검사 대상에 들어갑니다(새로 생긴 값은 승인 전까지 빌드를 막습니다).
    /// Bullet 은 총이 빠진 뒤 쓰지 않는 코드입니다.
    /// </summary>
    private static readonly HashSet<string> PREFAB_EXCLUDED_FOLDERS = new HashSet<string>
    {
        "BirdShadow", "Bullet", "Camera", "Cloud", "Deco", "Lamp", "Light", "NoticeBoard", "QuestIndicator", "TileMap", "Weather",
    };

    /// <summary>Assets/Prefabs/Objects 밖에 있는 스탯 프리팹입니다.</summary>
    private static readonly string[] EXTRA_PREFABS =
    {
        "Assets/Prefabs/Installer/Installer/GameInstaller.prefab",
    };

    /// <summary>
    /// Assets/Scriptable Obj 바로 아래에서 스탯이 없는 폴더입니다.
    /// SkillData 의 배포별 DB는 AbilityBuildVariantData 로 따로 고릅니다.
    /// </summary>
    private static readonly HashSet<string> DATA_EXCLUDED_FOLDERS = new HashSet<string>
    {
        "Audio", "DOTweenMotionData", "Input", "Localization", "TileData", "TreeVisualData",
    };

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
    /// 먼저 돌아야 합니다. 원본 에셋을 그대로 읽어야 기준값과 비교할 수 있습니다.
    /// 자동저장 가드(-950)와 플랫폼 정합성 가드(-900) 사이에 둡니다.
    /// </summary>
    public int callbackOrder => -925;

    public void OnPreprocessBuild(BuildReport _report)
    {
        BuildRelease _release = PlatformBuildModeSwitcher.CurrentRelease;
        List<string> _errors = Validate(_release);

        if (0 == _errors.Count) return;

        string _detail = "  " + string.Join("\n  ", Head(_errors, MAX_REPORT_LINES));
        // 촬영용 빌드(BuildRunner.RunTrailer)는 배포하지 않지만 화면 표시 때문에 개발 빌드로 만들 수 없다. 개발 빌드처럼 경고만 남긴다.
        bool _isDevelopmentBuild = 0 != (_report.summary.options & BuildOptions.Development) || true == BuildRunner.IsTrailerBuildInProgress;

        if (true == _isDevelopmentBuild)
        {
            Debug.LogWarning($"{TAG} 스탯이 배포 기준과 다릅니다 ({_errors.Count}건). 개발·촬영용 빌드라 그대로 진행합니다.\n{_detail}");
            return;
        }

        throw new BuildFailedException(
            $"{TAG} 빌드를 중단했습니다. 스탯이 승인된 기준과 다릅니다 ({_errors.Count}건).\n" +
            $"{_detail}\n" +
            "\n" +
            "- [고정 규칙] 항목은 기본 캐릭터에 테스트 값이 남은 것입니다. 프리팹을 고친 뒤 다시 빌드하십시오.\n" +
            "- [기준값] 항목은 승인된 밸런스와 다른 것입니다. 테스트·촬영용 값이면 되돌리고, 의도한 변경이면\n" +
            $"  {ApproveMenu(_release)} 로 기준값을 갱신하고 그 파일을 함께 커밋하십시오.\n" +
            $"- 전체 목록은 {MENU_CHECK} 메뉴가 콘솔에 남깁니다.");
    }

    public static string BaselinePath(BuildRelease _release)
    {
        return BASELINE_DIR + (BuildRelease.Full == _release ? "StatBaseline_Full.txt" : "StatBaseline_Demo.txt");
    }

    /// <summary>
    /// 검사만 하고 결과를 돌려줍니다. 빌드 전처리, 검사 메뉴, 자동화가 같은 경로를 씁니다.
    /// </summary>
    public static List<string> Validate(BuildRelease _release)
    {
        List<string> _errors = new List<string>();

        Dictionary<string, string> _character = ReadCharacterStat(_errors);
        if (null != _character) CheckRequiredValues(_character, _errors);

        Dictionary<string, string> _current = ReadCurrent(_release, _errors);
        if (null != _current) CheckBaseline(_release, _current, _errors);

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

    private static void CheckBaseline(BuildRelease _release, Dictionary<string, string> _current, List<string> _errors)
    {
        Dictionary<string, string> _baseline = ReadBaseline(_release);

        if (null == _baseline)
        {
            _errors.Add($"[기준값] 승인된 기준값 파일이 없습니다: {BaselinePath(_release)}  ({ApproveMenu(_release)} 로 만드십시오)");
            return;
        }

        List<string> _keys = new List<string>(_baseline.Keys);
        _keys.Sort(StringComparer.Ordinal);

        for (int i = 0; i < _keys.Count; i++)
        {
            string _key = _keys[i];
            string _expected = _baseline[_key];

            if (false == _current.TryGetValue(_key, out string _actual))
            {
                _errors.Add($"[기준값] {_key} 가 사라졌습니다 (승인된 값 {_expected})");
            }
            else if (false == SameValue(_actual, _expected))
            {
                _errors.Add($"[기준값] {_key} = {_actual} (승인된 값 {_expected})");
            }
        }

        _keys = new List<string>(_current.Keys);
        _keys.Sort(StringComparer.Ordinal);

        for (int i = 0; i < _keys.Count; i++)
        {
            if (false == _baseline.ContainsKey(_keys[i]))
            {
                _errors.Add($"[기준값] {_keys[i]} = {_current[_keys[i]]} 는 새로 생긴 값이라 아직 승인되지 않았습니다");
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

#region 현재 값 읽기

    /// <summary>
    /// 고정 규칙용으로 캐릭터 StatComponent 의 최상위 숫자·불 필드만 읽습니다.
    /// 프리팹 구조가 바뀌어도 규칙이 따라가도록 기준값 키와 따로 둡니다.
    /// </summary>
    private static Dictionary<string, string> ReadCharacterStat(List<string> _errors)
    {
        GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CHARACTER_PREFAB_PATH);

        if (null == _prefab)
        {
            _errors.Add($"캐릭터 프리팹을 찾지 못했습니다: {CHARACTER_PREFAB_PATH}");
            return null;
        }

        StatComponent _stat = _prefab.GetComponentInChildren<StatComponent>(true);

        if (null == _stat)
        {
            _errors.Add($"캐릭터 프리팹에서 StatComponent를 찾지 못했습니다: {CHARACTER_PREFAB_PATH}");
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

        return _values;
    }

    /// <summary>
    /// 그 배포에 실리는 스탯을 전부 읽습니다. 키는 "출처|오브젝트|필드 경로" 입니다.
    /// 배열 원소는 xxxType / xxxState 필드가 있으면 그 값으로 이름 붙여, 순서가 바뀌어도 같은 키가 되게 합니다.
    /// </summary>
    private static Dictionary<string, string> ReadCurrent(BuildRelease _release, List<string> _errors)
    {
        AbilityBuildVariantData _variant = AssetDatabase.LoadAssetAtPath<AbilityBuildVariantData>(VARIANT_DATA_PATH);

        if (null == _variant)
        {
            _errors.Add($"배포별 스킬 데이터 설정을 찾지 못했습니다: {VARIANT_DATA_PATH}");
            return null;
        }

        bool _full = BuildRelease.Full == _release;
        SkillDataBase _skillDb = true == _full ? _variant.FullSkillDataBase : _variant.DemoSkillDataBase;
        TextAsset _nodeDb = true == _full ? _variant.FullAbilityNodeDatabase : _variant.DemoAbilityNodeDatabase;

        if (null == _skillDb || null == _nodeDb)
        {
            _errors.Add($"AbilityBuildVariantData 에 {(true == _full ? "정식" : "데모")} 스킬 DB 또는 능력 노드 DB가 비어 있습니다.");
            return null;
        }

        // 배포별 DB는 아래에서 그 배포 것만 읽는다. 폴더 순회에서는 양쪽 모두 뺀다.
        HashSet<string> _variantAssets = new HashSet<string>(StringComparer.Ordinal)
        {
            VARIANT_DATA_PATH,
            AssetDatabase.GetAssetPath(_variant.DemoSkillDataBase),
            AssetDatabase.GetAssetPath(_variant.FullSkillDataBase),
        };

        Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);

        // 프리팹
        List<string> _prefabs = FindAssets("t:Prefab", PREFAB_ROOT, PREFAB_EXCLUDED_FOLDERS);
        _prefabs.AddRange(EXTRA_PREFABS);

        for (int i = 0; i < _prefabs.Count; i++)
        {
            ReadPrefab(_prefabs[i], _values, _errors);
        }

        // 공용 데이터
        List<string> _data = FindAssets("t:ScriptableObject", DATA_ROOT, DATA_EXCLUDED_FOLDERS);

        for (int i = 0; i < _data.Count; i++)
        {
            if (true == _variantAssets.Contains(_data[i])) continue;
            ReadDataAsset(_data[i], _values);
        }

        // 배포별 데이터
        ReadDataAsset(AssetDatabase.GetAssetPath(_skillDb), _values);
        ReadNodeDatabase(_nodeDb, _values, _errors);

        return _values;
    }

    /// <summary>폴더 아래 에셋을 찾되, 그 폴더 바로 아래 하위 폴더 이름이 제외 목록에 있으면 뺍니다. 결과는 경로 순입니다.</summary>
    private static List<string> FindAssets(string _filter, string _root, HashSet<string> _excludedFolders)
    {
        string[] _guids = AssetDatabase.FindAssets(_filter, new[] { _root.TrimEnd('/') });
        HashSet<string> _paths = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < _guids.Length; i++)
        {
            string _path = AssetDatabase.GUIDToAssetPath(_guids[i]);
            string _relative = _path.Substring(_root.Length);
            int _slash = _relative.IndexOf('/');

            if (0 < _slash && true == _excludedFolders.Contains(_relative.Substring(0, _slash))) continue;

            _paths.Add(_path);
        }

        List<string> _result = new List<string>(_paths);
        _result.Sort(StringComparer.Ordinal);
        return _result;
    }

    private static void ReadPrefab(string _path, Dictionary<string, string> _values, List<string> _errors)
    {
        GameObject _root = AssetDatabase.LoadAssetAtPath<GameObject>(_path);

        if (null == _root)
        {
            _errors.Add($"[기준값] 스탯 프리팹을 찾지 못했습니다: {_path}");
            return;
        }

        string _label = Label(_path);
        MonoBehaviour[] _components = _root.GetComponentsInChildren<MonoBehaviour>(true);
        Dictionary<string, int> _seen = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < _components.Length; i++)
        {
            MonoBehaviour _component = _components[i];

            if (null == _component) continue; // 스크립트가 빠진 컴포넌트
            if (false == IsGameScript(_component)) continue;

            string _object = HierarchyPath(_component.transform, _root.transform) + ":" + _component.GetType().Name;
            _seen.TryGetValue(_object, out int _count);
            _seen[_object] = _count + 1;

            if (0 < _count) _object += "#" + _count;

            ReadObject(_component, _label + "|" + _object + "|", _values);
        }
    }

    private static void ReadDataAsset(string _path, Dictionary<string, string> _values)
    {
        UnityEngine.Object[] _assets = AssetDatabase.LoadAllAssetsAtPath(_path);
        string _label = Label(_path);

        for (int i = 0; i < _assets.Length; i++)
        {
            ScriptableObject _so = _assets[i] as ScriptableObject;

            if (null == _so) continue;
            if (false == IsGameScript(_so)) continue;

            string _object = AssetDatabase.IsMainAsset(_so) ? _so.GetType().Name : _so.GetType().Name + "(" + _so.name + ")";
            ReadObject(_so, _label + "|" + _object + "|", _values);
        }
    }

    /// <summary>
    /// 능력 노드 DB에서 게임 규칙만 읽습니다: 어떤 노드가 있는지, 선행 노드, 필요 프레스티지 레벨.
    /// 화면 배치(gridX/Y, 선 경로 pivot)와 표시용 값(nameLocId, levelBadge)은 밸런스가 아니라 뺍니다.
    /// </summary>
    private static void ReadNodeDatabase(TextAsset _json, Dictionary<string, string> _values, List<string> _errors)
    {
        string _label = Label(AssetDatabase.GetAssetPath(_json));
        AbilityNodeDatabaseJson _db;

        try
        {
            _db = JsonUtility.FromJson<AbilityNodeDatabaseJson>(_json.text);
        }
        catch (Exception _e)
        {
            _errors.Add($"[기준값] 능력 노드 DB를 읽지 못했습니다: {_label} ({_e.Message})");
            return;
        }

        if (null == _db || null == _db.nodes)
        {
            _errors.Add($"[기준값] 능력 노드 DB가 비어 있습니다: {_label}");
            return;
        }

        HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < _db.nodes.Length; i++)
        {
            AbilityNodeDefinitionJson _node = _db.nodes[i];
            if (null == _node) continue;

            string _name = "node[" + _node.skillType + "]";
            if (false == _seen.Add(_name)) _name = "node[" + _node.skillType + "#" + i + "]";

            List<string> _parents = new List<string>(_node.GetParentSkillTypeNames());
            _parents.Sort(StringComparer.Ordinal);

            string _prefix = _label + "|" + _name + "|";
            _values[_prefix + "requiredPrestigeLevel"] = _node.requiredPrestigeLevel.ToString(CultureInfo.InvariantCulture);
            _values[_prefix + "parents"] = 0 == _parents.Count ? "-" : string.Join(",", _parents);
        }
    }

    /// <summary>
    /// 컴포넌트·데이터 하나의 직렬화된 값을 전부 읽습니다. 숨김 필드도 포함하려고 Next를 씁니다.
    /// m_ 로 시작하는 Unity 내부 필드(m_Enabled, m_Script 등)는 밸런스 수치가 아니라 뺍니다.
    /// </summary>
    private static void ReadObject(UnityEngine.Object _target, string _prefix, Dictionary<string, string> _values)
    {
        SerializedObject _so = new SerializedObject(_target);
        SerializedProperty _it = _so.GetIterator();

        if (false == _it.Next(true)) return;

        do
        {
            if (true == _it.name.StartsWith("m_", StringComparison.Ordinal)) continue;

            ReadProperty(_it.Copy(), _prefix + _it.name, _values);
        }
        while (true == _it.Next(false));
    }

    /// <summary>
    /// 숫자·불·열거형·벡터는 값으로 남기고, 배열과 구조체는 안으로 들어갑니다.
    /// 오브젝트 참조, 문자열, 색, 커브는 밸런스 수치가 아니라 뺍니다.
    /// </summary>
    private static void ReadProperty(SerializedProperty _p, string _key, Dictionary<string, string> _values)
    {
        switch (_p.propertyType)
        {
            case SerializedPropertyType.Integer:
                _values[_key] = _p.longValue.ToString(CultureInfo.InvariantCulture);
                return;
            case SerializedPropertyType.Float:
                _values[_key] = _p.floatValue.ToString("R", CultureInfo.InvariantCulture);
                return;
            case SerializedPropertyType.Boolean:
                _values[_key] = true == _p.boolValue ? "1" : "0";
                return;
            case SerializedPropertyType.Enum:
                _values[_key] = EnumText(_p);
                return;
            case SerializedPropertyType.Vector2:
                _values[_key] = Vector(_p.vector2Value.x, _p.vector2Value.y);
                return;
            case SerializedPropertyType.Vector3:
                _values[_key] = Vector(_p.vector3Value.x, _p.vector3Value.y, _p.vector3Value.z);
                return;
            case SerializedPropertyType.Vector2Int:
                _values[_key] = Vector(_p.vector2IntValue.x, _p.vector2IntValue.y);
                return;
            case SerializedPropertyType.Vector3Int:
                _values[_key] = Vector(_p.vector3IntValue.x, _p.vector3IntValue.y, _p.vector3IntValue.z);
                return;
            case SerializedPropertyType.Generic:
                break;
            default:
                return;
        }

        if (true == _p.isArray)
        {
            HashSet<string> _labels = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < _p.arraySize; i++)
            {
                SerializedProperty _element = _p.GetArrayElementAtIndex(i);
                string _name = ElementLabel(_element, i);

                if (false == _labels.Add(_name)) _name += "#" + i;

                ReadProperty(_element, _key + "[" + _name + "]", _values);
            }

            return;
        }

        SerializedProperty _child = _p.Copy();
        SerializedProperty _end = _p.GetEndProperty();

        if (false == _child.Next(true)) return;

        while (false == SerializedProperty.EqualContents(_child, _end))
        {
            // 그라디언트·파티클 색 같은 Unity 내부 구조체(m_Mode 등)는 연출 값이라 들어가지 않는다.
            if (false == _child.name.StartsWith("m_", StringComparison.Ordinal))
            {
                ReadProperty(_child.Copy(), _key + "." + _child.name, _values);
            }

            if (false == _child.Next(false)) break;
        }
    }

    /// <summary>
    /// 배열 원소 이름입니다. 원소가 xxxType / xxxState 필드를 가지면 "treeType=Tree4" 처럼 그 값을 쓰고, 없으면 순번을 씁니다.
    /// </summary>
    private static string ElementLabel(SerializedProperty _element, int _index)
    {
        if (SerializedPropertyType.Generic != _element.propertyType || true == _element.isArray)
        {
            return _index.ToString(CultureInfo.InvariantCulture);
        }

        // 원목 데이터처럼 itemType 은 모두 같고 treeType 으로 갈리는 경우가 있어, 해당하는 필드를 모두 이어 붙인다.
        List<string> _ids = new List<string>();
        SerializedProperty _child = _element.Copy();
        SerializedProperty _end = _element.GetEndProperty();

        if (true == _child.Next(true))
        {
            while (false == SerializedProperty.EqualContents(_child, _end))
            {
                bool _isId = SerializedPropertyType.Enum == _child.propertyType || SerializedPropertyType.Integer == _child.propertyType;

                if (true == _isId && (true == _child.name.EndsWith("Type", StringComparison.Ordinal) || true == _child.name.EndsWith("State", StringComparison.Ordinal)))
                {
                    string _value = SerializedPropertyType.Enum == _child.propertyType
                        ? EnumText(_child)
                        : _child.longValue.ToString(CultureInfo.InvariantCulture);

                    _ids.Add(_child.name + "=" + _value);
                }

                if (false == _child.Next(false)) break;
            }
        }

        return 0 < _ids.Count ? string.Join(",", _ids) : _index.ToString(CultureInfo.InvariantCulture);
    }

    private static string EnumText(SerializedProperty _p)
    {
        int _index = _p.enumValueIndex;
        string[] _names = _p.enumNames;

        if (0 <= _index && _index < _names.Length) return _names[_index];

        return _p.intValue.ToString(CultureInfo.InvariantCulture);
    }

    private static string Vector(params float[] _v)
    {
        string[] _parts = new string[_v.Length];

        for (int i = 0; i < _v.Length; i++) _parts[i] = _v[i].ToString("R", CultureInfo.InvariantCulture);

        return "(" + string.Join(", ", _parts) + ")";
    }

    private static bool IsGameScript(MonoBehaviour _component)
    {
        MonoScript _script = MonoScript.FromMonoBehaviour(_component);
        return null != _script && true == AssetDatabase.GetAssetPath(_script).StartsWith(GAME_SCRIPT_ROOT, StringComparison.Ordinal);
    }

    private static bool IsGameScript(ScriptableObject _asset)
    {
        MonoScript _script = MonoScript.FromScriptableObject(_asset);
        return null != _script && true == AssetDatabase.GetAssetPath(_script).StartsWith(GAME_SCRIPT_ROOT, StringComparison.Ordinal);
    }

    private static string HierarchyPath(Transform _t, Transform _root)
    {
        string _path = _t.name;

        while (_t != _root && null != _t.parent)
        {
            _t = _t.parent;
            _path = _t.name + "/" + _path;
        }

        return _path;
    }

    /// <summary>"Assets/Prefabs/Objects/Trees/Tree.prefab" → "Prefabs/Objects/Trees/Tree"</summary>
    private static string Label(string _assetPath)
    {
        string _label = _assetPath.StartsWith("Assets/", StringComparison.Ordinal) ? _assetPath.Substring(7) : _assetPath;
        string _extension = Path.GetExtension(_label);

        return 0 < _extension.Length ? _label.Substring(0, _label.Length - _extension.Length) : _label;
    }

#endregion

#region 기준값 파일

    private static Dictionary<string, string> ReadBaseline(BuildRelease _release)
    {
        string _path = ToAbsolute(BaselinePath(_release));

        if (false == File.Exists(_path)) return null;

        Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] _lines = File.ReadAllLines(_path, Encoding.UTF8);

        for (int i = 0; i < _lines.Length; i++)
        {
            string _line = _lines[i].Trim();

            if (0 == _line.Length || '#' == _line[0]) continue;

            // 키에 '=' 가 들어갈 수 있으므로(배열 원소 이름 treeType=Tree4) 마지막 " = " 로 나눈다.
            int _eq = _line.LastIndexOf(" = ", StringComparison.Ordinal);
            if (0 >= _eq) continue;

            _values[_line.Substring(0, _eq)] = _line.Substring(_eq + 3);
        }

        return _values;
    }

    /// <summary>
    /// 현재 값을 그 배포의 기준값으로 저장합니다. 고정 규칙을 어긴 상태로는 승인하지 않습니다.
    /// 테스트 값이 기준값으로 굳어 버리면 이 가드가 거꾸로 그 값을 지키게 되기 때문입니다.
    /// </summary>
    public static bool ApproveBaseline(BuildRelease _release, out string _message)
    {
        List<string> _errors = new List<string>();

        Dictionary<string, string> _character = ReadCharacterStat(_errors);
        if (null != _character) CheckRequiredValues(_character, _errors);

        Dictionary<string, string> _current = ReadCurrent(_release, _errors);

        if (0 != _errors.Count || null == _current)
        {
            _message = "승인할 수 없습니다. 먼저 아래 문제를 고치십시오.\n  " + string.Join("\n  ", _errors);
            return false;
        }

        List<string> _keys = new List<string>(_current.Keys);
        _keys.Sort(StringComparer.Ordinal);

        string _name = BuildRelease.Full == _release ? "정식" : "데모";
        StringBuilder _sb = new StringBuilder();
        _sb.Append($"# {_name} 배포 빌드의 스탯 승인 기준값입니다. GameStatBuildGuard 가 이 값과 대조합니다.").Append('\n');
        _sb.Append($"# 직접 고치지 말고 {ApproveMenu(_release).Replace("/", " > ")} 로 갱신한 뒤, 바뀐 에셋과 함께 커밋하십시오.").Append('\n');
        _sb.Append("# 형식: 출처|오브젝트|필드 경로 = 값").Append('\n');
        _sb.Append($"# 승인: {DateTime.Now:yyyy-MM-dd HH:mm}").Append('\n');

        for (int i = 0; i < _keys.Count; i++)
        {
            _sb.Append(_keys[i]).Append(" = ").Append(_current[_keys[i]]).Append('\n');
        }

        File.WriteAllText(ToAbsolute(BaselinePath(_release)), _sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(BaselinePath(_release));

        _message = $"{_name} 기준값 {_keys.Count}개를 저장했습니다: {BaselinePath(_release)}";
        return true;
    }

    private static string ApproveMenu(BuildRelease _release)
    {
        return BuildRelease.Full == _release ? MENU_APPROVE_FULL : MENU_APPROVE_DEMO;
    }

    private static string ToAbsolute(string _assetPath)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", _assetPath));
    }

    private static List<string> Head(List<string> _lines, int _max)
    {
        if (_lines.Count <= _max) return _lines;

        List<string> _head = _lines.GetRange(0, _max);
        _head.Add($"... 외 {_lines.Count - _max}건");
        return _head;
    }

#endregion

#region 메뉴

    [MenuItem(MENU_CHECK, false, 66)]
    private static void CheckFromMenu()
    {
        BuildRelease _release = PlatformBuildModeSwitcher.CurrentRelease;
        List<string> _errors = Validate(_release);
        string _mode = BuildRelease.Demo == _release ? "데모" : "정식";

        if (0 == _errors.Count)
        {
            EditorUtility.DisplayDialog("스탯 검사", $"{_mode} (고정 규칙 + 기준값)\n\n문제 없습니다. 이대로 빌드할 수 있습니다.", "확인");
            return;
        }

        Debug.LogError($"{TAG} 스탯 검사 실패 ({_mode}, {_errors.Count}건)\n  " + string.Join("\n  ", _errors));
        EditorUtility.DisplayDialog("스탯 검사", $"{_mode} (고정 규칙 + 기준값)\n\n문제 {_errors.Count}건. 전체 목록은 콘솔을 보십시오.\n\n" +
                                    string.Join("\n", Head(_errors, 8)), "확인");
    }

    [MenuItem(MENU_APPROVE_DEMO, false, 67)]
    private static void ApproveDemoFromMenu()
    {
        ApproveFromMenu(BuildRelease.Demo);
    }

    [MenuItem(MENU_APPROVE_FULL, false, 68)]
    private static void ApproveFullFromMenu()
    {
        ApproveFromMenu(BuildRelease.Full);
    }

    private static void ApproveFromMenu(BuildRelease _release)
    {
        string _title = BuildRelease.Full == _release ? "스탯 기준값 승인 (정식)" : "스탯 기준값 승인 (데모)";
        List<string> _diff = new List<string>();
        Dictionary<string, string> _current = ReadCurrent(_release, _diff);

        if (null != _current) CheckBaseline(_release, _current, _diff);

        if (0 != _diff.Count)
        {
            Debug.Log($"{TAG} {_title} 대상 {_diff.Count}건\n  " + string.Join("\n  ", _diff));
        }

        string _summary = 0 == _diff.Count
            ? "기준값과 현재 값이 같습니다. 다시 저장만 합니다."
            : $"아래 {_diff.Count}건을 승인합니다. 전체 목록은 콘솔을 보십시오.\n\n" + string.Join("\n", Head(_diff, 12));

        bool _ok = EditorUtility.DisplayDialog(_title,
            _summary + "\n\n테스트·촬영용 값이 아니라 의도한 밸런스 변경이 맞습니까?", "승인", "취소");

        if (false == _ok) return;

        bool _saved = ApproveBaseline(_release, out string _message);

        if (true == _saved) Debug.Log($"{TAG} {_message}");
        else Debug.LogError($"{TAG} {_message}");

        EditorUtility.DisplayDialog(_title, _message, "확인");
    }

#endregion
}
