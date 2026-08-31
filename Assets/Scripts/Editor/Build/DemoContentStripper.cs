using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 데모 빌드에서 미공개 스테이지의 콘텐츠를 빼냅니다.
///
/// [왜 필요한가]
/// 데모는 Town과 WideGreenForest까지만 갈 수 있지만(HUD_PopupNav_Main), 던전은 씬 하나를
/// 데이터로 갈아끼우는 구조라 나머지 숲의 에셋까지 전부 빌드에 실립니다. 플레이로는 닿을 수
/// 없어도 파일을 뜯으면 그대로 보이므로, 아직 공개하지 않은 BGM과 나무 그림이 새어 나갑니다.
///
/// [어떻게 빼는가]
/// 이 에셋들은 데이터베이스 ScriptableObject 한 곳에서만 참조됩니다.
///   - Stage2/3/4 BGM  -> AudioDatabase.sounds
///   - 미공개 나무 그림 -> TreeVisualDataBase.treeVisualDatas
/// 그래서 빌드 직전에 이 두 에셋에서 해당 항목만 지우면, 스프라이트와 오디오 클립이 아무에게도
/// 참조되지 않아 빌드에서 자연히 빠집니다. 빌드가 끝나면 원본으로 되돌립니다.
///
/// 씬을 건드리는 IProcessSceneWithReport 쪽이 더 안전해 보이지만 쓸 수 없습니다. 나무는
/// Tree.prefab이 데이터베이스를 직접 들고 있고, 프리팹은 씬이 아니라 에셋으로 실리기 때문에
/// 씬 콜백에서는 손이 닿지 않습니다. 원본을 고쳤다 되돌리는 방식이 유일한 방법입니다.
///
/// [무엇이 안전한가]
/// 지울 대상은 DensityDataBase에서 "데모에서 갈 수 있는 맵이 쓰는 나무"를 읽어 그 여집합으로
/// 정합니다. 하드코딩이 아니라 실제 배치 데이터를 따르므로, 나무를 옮기거나 추가해도 따라옵니다.
/// 설령 잘못 지워도 그 대상은 데모에서 도달할 수 없는 맵 전용이라 데모 플레이에는 영향이 없습니다.
///
/// [빌드가 도중에 죽으면]
/// 원본은 Library 아래에 바이트 그대로 백업해 둡니다. 빌드 후 복구가 원칙이고, 빌드가 비정상
/// 종료돼 백업이 남아 있으면 다음에 에디터가 켜질 때 자동으로 되돌립니다.
/// </summary>
public class DemoContentStripper : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    private const string AUDIO_DB_PATH = "Assets/Scriptable Obj/Audio/AudioDatabase.asset";
    private const string TREE_VISUAL_DB_PATH = "Assets/Scriptable Obj/TreeVisualData/Tree Visual Data Base.asset";
    private const string DENSITY_DB_PATH = "Assets/Scriptable Obj/DensityData/Density Data Base.asset";
    private const string NAV_PREFAB_PATH = "Assets/Prefabs/UI/MenuPopup/Map/NewNav/HUD_PopupNav_Main.prefab";

    private const string BACKUP_FOLDER = "DemoContentStripBackup";

    public void OnPreprocessBuild(BuildReport _report)
    {
        // 이전 빌드가 비정상 종료돼 백업이 남아 있을 수 있다. 무엇을 하든 먼저 원본으로 맞춘다.
        RestoreIfNeeded(false);

        if (true == BuildInfo.IsFullRelease) return;

        MapType _maxPlayableMap = ReadMaxPlayableMapTypeInDemo();

        if (MapType.None == _maxPlayableMap)
        {
            Debug.LogWarning($"[DemoStrip] 데모 최대 플레이 맵을 읽지 못했습니다({NAV_PREFAB_PATH}). " +
                             "미공개 콘텐츠 제외를 건너뜁니다. 빌드는 그대로 진행됩니다.");
            return;
        }

        Directory.CreateDirectory(BackupDirectory);

        int _removedBgm = StripAudio(_maxPlayableMap);
        int _removedTree = StripTreeVisuals(_maxPlayableMap);

        AssetDatabase.SaveAssets();

        Debug.Log($"[DemoStrip] 데모 빌드 - 미공개 콘텐츠 제외 (최대 플레이 맵: {_maxPlayableMap})\n" +
                  $"  BGM {_removedBgm}곡, 나무 비주얼 {_removedTree}종\n" +
                  "  빌드가 끝나면 원본으로 자동 복구됩니다.");
    }

    public void OnPostprocessBuild(BuildReport _report)
    {
        RestoreIfNeeded(false);
    }

#region 제외 대상 판정

    /// <summary>
    /// 데모에서 갈 수 있는 맵들이 실제로 쓰는 나무 종류를 DensityDataBase에서 모읍니다.
    /// 여기 없는 나무가 제외 대상입니다.
    /// </summary>
    private static HashSet<TreeType> CollectDemoTreeTypes(MapType _maxPlayableMap)
    {
        HashSet<TreeType> _used = new HashSet<TreeType>();

        MapDensityDataBase _density = AssetDatabase.LoadAssetAtPath<MapDensityDataBase>(DENSITY_DB_PATH);

        if (null == _density || null == _density.densityDatas) return _used;

        for (int m = 0; m < _density.densityDatas.Count; m++)
        {
            MapDensityData _map = _density.densityDatas[m];

            if (_map.mapType > _maxPlayableMap) continue;
            if (null == _map.densityData) continue;

            for (int f = 0; f < _map.densityData.Count; f++)
            {
                List<TreeDensityData> _trees = _map.densityData[f].spawnTreeTypes;
                if (null == _trees) continue;

                for (int t = 0; t < _trees.Count; t++)
                {
                    _used.Add(_trees[t].treeType);
                }
            }
        }

        return _used;
    }

    /// <summary>맵에 딸린 스테이지 BGM입니다. 맵과 곡이 1:1이라 표로 둡니다.</summary>
    private static SoundID GetStageBgm(MapType _mapType)
    {
        switch (_mapType)
        {
            case MapType.WideGreenForest: return SoundID.Stage1BGM;
            case MapType.FluffySporeForest: return SoundID.Stage2BGM;
            case MapType.StarrootForest: return SoundID.Stage3BGM;
            case MapType.MagmaForest: return SoundID.Stage4BGM;
            default: return SoundID.None;
        }
    }

    /// <summary>
    /// 데모 제한 기준값은 HUD_PopupNav_Main 프리팹이 들고 있습니다. 여기서 상수로 따로 두면
    /// 둘이 어긋날 수 있으므로, 런타임이 보는 그 값을 그대로 읽습니다.
    /// </summary>
    private static MapType ReadMaxPlayableMapTypeInDemo()
    {
        GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NAV_PREFAB_PATH);
        if (null == _prefab) return MapType.None;

        HUD_PopupNav_Main _nav = _prefab.GetComponentInChildren<HUD_PopupNav_Main>(true);
        if (null == _nav) return MapType.None;

        return _nav.MaxPlayableMapTypeInDemo;
    }

#endregion

#region 제외 실행

    private static int StripAudio(MapType _maxPlayableMap)
    {
        AudioDatabase _db = AssetDatabase.LoadAssetAtPath<AudioDatabase>(AUDIO_DB_PATH);

        if (null == _db || null == _db.sounds)
        {
            Debug.LogWarning($"[DemoStrip] AudioDatabase를 읽지 못했습니다: {AUDIO_DB_PATH}");
            return 0;
        }

        HashSet<SoundID> _drop = new HashSet<SoundID>();

        for (MapType _map = MapType.Town; _map <= MapType.MagmaForest; _map++)
        {
            if (_map <= _maxPlayableMap) continue;

            SoundID _bgm = GetStageBgm(_map);
            if (SoundID.None != _bgm) _drop.Add(_bgm);
        }

        if (0 == _drop.Count) return 0;

        Backup(AUDIO_DB_PATH);

        int _removed = _db.sounds.RemoveAll(_sound => null != _sound && _drop.Contains(_sound.id));

        if (_removed > 0) EditorUtility.SetDirty(_db);

        return _removed;
    }

    private static int StripTreeVisuals(MapType _maxPlayableMap)
    {
        TreeVisualDataBase _db = AssetDatabase.LoadAssetAtPath<TreeVisualDataBase>(TREE_VISUAL_DB_PATH);

        if (null == _db || null == _db.treeVisualDatas)
        {
            Debug.LogWarning($"[DemoStrip] TreeVisualDataBase를 읽지 못했습니다: {TREE_VISUAL_DB_PATH}");
            return 0;
        }

        HashSet<TreeType> _keep = CollectDemoTreeTypes(_maxPlayableMap);

        if (0 == _keep.Count)
        {
            // 배치 데이터를 못 읽은 상황이다. 여기서 그냥 지우면 데모에 쓰이는 나무까지 날아간다.
            Debug.LogWarning("[DemoStrip] 데모에서 쓰이는 나무 종류를 찾지 못해 나무 비주얼 제외를 건너뜁니다.");
            return 0;
        }

        Backup(TREE_VISUAL_DB_PATH);

        int _removed = _db.treeVisualDatas.RemoveAll(
            _data => TreeType.None != _data.treeType && false == _keep.Contains(_data.treeType));

        if (_removed > 0) EditorUtility.SetDirty(_db);

        return _removed;
    }

#endregion

#region 백업 / 복구

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    private static string BackupDirectory => Path.Combine(ProjectRoot, "Library", BACKUP_FOLDER);

    private static string ToAbsolute(string _assetPath)
    {
        return Path.Combine(ProjectRoot, _assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void Backup(string _assetPath)
    {
        File.Copy(ToAbsolute(_assetPath), Path.Combine(BackupDirectory, Path.GetFileName(_assetPath)), true);
    }

    private static string FindAssetPathByFileName(string _fileName)
    {
        if (Path.GetFileName(AUDIO_DB_PATH) == _fileName) return AUDIO_DB_PATH;
        if (Path.GetFileName(TREE_VISUAL_DB_PATH) == _fileName) return TREE_VISUAL_DB_PATH;

        return null;
    }

    /// <summary>
    /// 백업이 남아 있으면 원본으로 되돌립니다. 백업 폴더의 존재 자체가 "아직 복구 안 됨" 표시입니다.
    /// </summary>
    public static void RestoreIfNeeded(bool _isEditorStartup)
    {
        string _dir = BackupDirectory;
        if (false == Directory.Exists(_dir)) return;

        string[] _files = Directory.GetFiles(_dir, "*.asset");
        List<string> _restored = new List<string>(_files.Length);

        for (int i = 0; i < _files.Length; i++)
        {
            string _target = FindAssetPathByFileName(Path.GetFileName(_files[i]));
            if (null == _target) continue;

            File.Copy(_files[i], ToAbsolute(_target), true);
            AssetDatabase.ImportAsset(_target, ImportAssetOptions.ForceUpdate);

            _restored.Add(_target);
        }

        Directory.Delete(_dir, true);

        if (0 == _restored.Count) return;

        string _message = "[DemoStrip] 원본 데이터베이스를 복구했습니다:\n  " + string.Join("\n  ", _restored.ToArray());

        if (true == _isEditorStartup)
        {
            Debug.LogWarning(_message + "\n이전 빌드가 정상적으로 끝나지 않은 것 같습니다. 내용이 맞는지 한 번 확인하세요.");
        }
        else
        {
            Debug.Log(_message);
        }
    }

#endregion
}

/// <summary>
/// 빌드가 도중에 죽어 백업이 남은 경우를 대비해, 에디터가 켜질 때 한 번 복구를 시도합니다.
/// </summary>
[InitializeOnLoad]
internal static class DemoContentStripRecovery
{
    static DemoContentStripRecovery()
    {
        // 에셋 임포트가 가능한 시점까지 미룬다.
        EditorApplication.delayCall += () => DemoContentStripper.RestoreIfNeeded(true);
    }
}
