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
///   - 미공개 원석/주괴 그림 -> GameInstaller 프리팹의 GemOreItemController.gemOreTypeDatas
///                              + 같은 프리팹의 BlastFurnaceManager.recipes
/// 그래서 빌드 직전에 이 에셋들에서 해당 항목만 지우면, 스프라이트와 오디오 클립이 아무에게도
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
/// 원석도 같은 방식입니다. 어떤 원석이 실제로 떨어질 수 있는지는 GemOreItemController의
/// 드랍 테이블(gemOreDropDatas)이 정하므로, "데모에서 갈 수 있는 맵의 나무"에 대한 줄이 하나도
/// 없는 원석 종류는 데모에서 나올 수 없습니다. 그 종류의 그림만 참조를 끊습니다.
/// 나중에 다이아/프리즘 줄을 채우면 그 그림은 자동으로 다시 남습니다.
///
/// 원석 그림은 용광로 레시피(BlastFurnaceManager.recipes)에도 한 번 더 걸려 있습니다. 한쪽만
/// 끊으면 다른 쪽이 붙잡고 있어 그림이 그대로 빌드에 실리므로 둘을 함께 끊습니다. 주괴 그림은
/// 레시피에만 있어 여기서만 걸러집니다. 용광로 본체 그림은 세 대가 같은 것을 쓰므로(원석 종류와
/// 무관) 제외 대상이 아닙니다.
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
    private const string INSTALLER_PREFAB_PATH = "Assets/Prefabs/Installer/Installer/GameInstaller.prefab";

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
        int _removedGemOre = StripGemOreSprites(_maxPlayableMap, out int _removedFurnaceRecipe);

        AssetDatabase.SaveAssets();

        Debug.Log($"[DemoStrip] 데모 빌드 - 미공개 콘텐츠 제외 (최대 플레이 맵: {_maxPlayableMap})\n" +
                  $"  BGM {_removedBgm}곡, 나무 비주얼 {_removedTree}종, 원석 그림 {_removedGemOre}종, 용광로 레시피 {_removedFurnaceRecipe}종\n" +
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

    /// <summary>
    /// 데모에서 나올 수 없는 원석 종류의 그림 참조를 끊습니다.
    ///
    /// 나올 수 있는 종류는 드랍 테이블에서 역산합니다 - "데모에서 갈 수 있는 맵의 나무"에 대한
    /// 줄이 하나라도 있으면 그 원석은 데모에서 떨어질 수 있습니다. 값이 비어 있는 줄(IsEmpty)은
    /// 드랍 자체가 일어나지 않으므로 세지 않습니다.
    ///
    /// 같은 그림을 참조하는 곳이 둘이라 두 군데를 함께 끊습니다 - 원석이 떨어질 때 쓰는
    /// GemOreItemController.gemOreTypeDatas와, 용광로가 원석/주괴를 날릴 때 쓰는
    /// BlastFurnaceManager.recipes입니다. 한쪽만 끊으면 다른 쪽이 붙잡고 있어 그림이 빌드에
    /// 그대로 실립니다. 반환값은 앞쪽 개수이고, 뒤쪽은 _removedFurnaceRecipes로 나갑니다.
    ///
    /// 원석 프리팹(GemOreItem.prefab) 자체는 건드리지 않습니다. 그림자/머티리얼을 원목과 공유할 뿐
    /// 자기 그림은 런타임에 이 테이블에서 받아 쓰므로, 참조를 끊으면 그림만 빌드에서 빠집니다.
    /// </summary>
    private static int StripGemOreSprites(MapType _maxPlayableMap, out int _removedFurnaceRecipes)
    {
        _removedFurnaceRecipes = 0;

        GameObject _installer = AssetDatabase.LoadAssetAtPath<GameObject>(INSTALLER_PREFAB_PATH);

        if (null == _installer)
        {
            Debug.LogWarning($"[DemoStrip] GameInstaller 프리팹을 읽지 못했습니다: {INSTALLER_PREFAB_PATH}");
            return 0;
        }

        GemOreItemController _controller = _installer.GetComponentInChildren<GemOreItemController>(true);

        if (null == _controller)
        {
            // 원석 기능이 아직 없는 브랜치일 수 있다. 빠뜨렸다고 빌드를 막을 일은 아니다.
            // 드랍 테이블이 여기 있으므로, 없으면 용광로 쪽도 무엇을 남길지 정할 수 없다.
            return 0;
        }

        HashSet<TreeType> _demoTrees = CollectDemoTreeTypes(_maxPlayableMap);

        if (0 == _demoTrees.Count)
        {
            // 배치 데이터를 못 읽은 상황이다. 여기서 그냥 지우면 데모에 쓰이는 원석까지 날아간다.
            Debug.LogWarning("[DemoStrip] 데모에서 쓰이는 나무 종류를 찾지 못해 원석 그림 제외를 건너뜁니다.");
            return 0;
        }

        SerializedObject _controllerSo = new SerializedObject(_controller);

        HashSet<GemOreType> _keep = CollectDemoGemOreTypes(_controllerSo, _demoTrees);

        // SerializedProperty에 값을 넣는 것만으로는 에셋이 바뀌지 않는다(Apply를 해야 반영된다).
        // 그래서 두 곳을 먼저 다 훑어 끊을 것을 정해두고, 백업을 뜬 뒤에 한꺼번에 적용한다.
        int _removed = ClearGemOreTypeDataSprites(_controllerSo, _keep);

        SerializedObject _furnaceSo = PrepareBlastFurnaceRecipeStrip(_installer, _keep, out _removedFurnaceRecipes);

        if (0 == _removed && 0 == _removedFurnaceRecipes) return 0;

        Backup(INSTALLER_PREFAB_PATH);

        if (_removed > 0)
        {
            _controllerSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_controller);
        }

        if (null != _furnaceSo)
        {
            _furnaceSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_furnaceSo.targetObject);
        }

        return _removed;
    }

    /// <summary>
    /// 원석이 떨어질 때 쓰는 그림(GemOreItemController.gemOreTypeDatas)에서 제외 대상의 참조를 끊습니다.
    /// 아직 Apply하지 않으므로 이 호출만으로는 에셋이 바뀌지 않습니다.
    /// </summary>
    private static int ClearGemOreTypeDataSprites(SerializedObject _controllerSo, HashSet<GemOreType> _keep)
    {
        SerializedProperty _typeDatas = _controllerSo.FindProperty("gemOreTypeDatas");
        if (null == _typeDatas) return 0;

        int _removed = 0;

        for (int i = 0; i < _typeDatas.arraySize; i++)
        {
            SerializedProperty _entry = _typeDatas.GetArrayElementAtIndex(i);
            GemOreType _type = (GemOreType)_entry.FindPropertyRelative("gemOreType").enumValueIndex;

            if (true == _keep.Contains(_type)) continue;

            bool _hadAny = false;
            _hadAny |= ClearSpriteRef(_entry, "smallSprite");
            _hadAny |= ClearSpriteRef(_entry, "mediumSprite");
            _hadAny |= ClearSpriteRef(_entry, "largeSprite");

            if (true == _hadAny) _removed++;
        }

        return _removed;
    }

    /// <summary>
    /// 용광로 레시피(BlastFurnaceManager.recipes)에서 제외 대상 원석의 원석/주괴 그림 참조를 끊습니다.
    ///
    /// 여기를 끊지 않으면 gemOreTypeDatas에서 끊어도 레시피가 같은 그림을 붙잡고 있어 빌드에 그대로
    /// 실립니다. 주괴 그림(ingotSprite)은 레시피에만 있어 이 함수가 유일한 통로입니다.
    ///
    /// 용광로 프리팹 자체(본체 그림)는 건드리지 않습니다. 세 대가 같은 그림을 쓰므로 원석 종류를
    /// 가릴 수 있는 정보가 아니고, 레시피는 런타임에 여기서 그림을 받아 쓰기 때문에 참조만 끊으면
    /// 그림 파일만 빌드에서 빠집니다.
    ///
    /// <b>아직 Apply하지 않은</b> SerializedObject를 돌려줍니다. 백업(Backup)을 뜨기 전에 에셋이
    /// 바뀌면 안 되므로 적용 시점은 호출부가 정합니다. 끊을 것이 없으면 null입니다.
    /// </summary>
    private static SerializedObject PrepareBlastFurnaceRecipeStrip(GameObject _installer, HashSet<GemOreType> _keep, out int _removed)
    {
        _removed = 0;

        BlastFurnaceManager _manager = _installer.GetComponentInChildren<BlastFurnaceManager>(true);

        // 용광로가 아직 없는 브랜치일 수 있다. 빠뜨렸다고 빌드를 막을 일은 아니다.
        if (null == _manager) return null;

        SerializedObject _so = new SerializedObject(_manager);

        SerializedProperty _recipes = _so.FindProperty("recipes");
        if (null == _recipes) return null;

        for (int i = 0; i < _recipes.arraySize; i++)
        {
            SerializedProperty _entry = _recipes.GetArrayElementAtIndex(i);
            GemOreType _type = (GemOreType)_entry.FindPropertyRelative("gemOreType").enumValueIndex;

            if (true == _keep.Contains(_type)) continue;

            bool _hadAny = false;
            _hadAny |= ClearSpriteRef(_entry, "oreSprite");
            _hadAny |= ClearSpriteRef(_entry, "ingotSprite");

            if (true == _hadAny) _removed++;
        }

        return 0 == _removed ? null : _so;
    }

    /// <summary>드랍 테이블을 읽어 데모에서 실제로 떨어질 수 있는 원석 종류를 모읍니다.</summary>
    private static HashSet<GemOreType> CollectDemoGemOreTypes(SerializedObject _controllerSo, HashSet<TreeType> _demoTrees)
    {
        HashSet<GemOreType> _reachable = new HashSet<GemOreType>();

        SerializedProperty _drops = _controllerSo.FindProperty("gemOreDropDatas");
        if (null == _drops) return _reachable;

        for (int i = 0; i < _drops.arraySize; i++)
        {
            SerializedProperty _row = _drops.GetArrayElementAtIndex(i);

            TreeType _tree = (TreeType)_row.FindPropertyRelative("treeType").enumValueIndex;
            if (false == _demoTrees.Contains(_tree)) continue;

            // 값이 하나도 없는 줄은 드랍이 일어나지 않으므로 "나올 수 있다"고 보지 않는다.
            if (true == IsEmptyDropRow(_row)) continue;

            _reachable.Add((GemOreType)_row.FindPropertyRelative("gemOreType").enumValueIndex);
        }

        return _reachable;
    }

    /// <summary>GemOreDropData.IsEmpty와 같은 판정입니다(직렬화 접근이라 여기서 다시 계산합니다).</summary>
    private static bool IsEmptyDropRow(SerializedProperty _row)
    {
        return _row.FindPropertyRelative("maxTotalCurrency").intValue <= 0
            && _row.FindPropertyRelative("maxSmallCnt").intValue <= 0
            && _row.FindPropertyRelative("maxMediumCnt").intValue <= 0
            && _row.FindPropertyRelative("maxLargeCnt").intValue <= 0;
    }

    /// <summary>참조가 있었으면 끊고 true를 돌려줍니다.</summary>
    private static bool ClearSpriteRef(SerializedProperty _entry, string _fieldName)
    {
        SerializedProperty _sprite = _entry.FindPropertyRelative(_fieldName);
        if (null == _sprite || null == _sprite.objectReferenceValue) return false;

        _sprite.objectReferenceValue = null;
        return true;
    }

#endregion

#region 백업 / 복구

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

    /// <summary>
    /// 백업 위치입니다. <b>Library 아래는 .gitignore 대상(/[Ll]ibrary/)입니다.</b>
    ///
    /// 빌드가 비정상 종료돼 백업이 남은 상태에서 Library를 지우면(유니티 문제 해결의 기본 수순)
    /// 여기서의 복구는 불가능해지고, 원본 에셋은 <b>수정된 채</b> 남습니다.
    /// 다만 대상 에셋은 전부 버전 관리 대상이라 그때는 git 쪽에서 되돌리면 됩니다 —
    /// 빌드가 중간에 죽었다면 Library를 지우기 전에 git status를 먼저 보십시오.
    ///
    /// 백업을 Assets 아래로 옮기지 마십시오. 그 순간 백업 파일 자체가 빌드에 실립니다.
    /// </summary>
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
        if (Path.GetFileName(INSTALLER_PREFAB_PATH) == _fileName) return INSTALLER_PREFAB_PATH;

        return null;
    }

    /// <summary>
    /// 백업이 남아 있으면 원본으로 되돌립니다. 백업 폴더의 존재 자체가 "아직 복구 안 됨" 표시입니다.
    /// </summary>
    public static void RestoreIfNeeded(bool _isEditorStartup)
    {
        string _dir = BackupDirectory;
        if (false == Directory.Exists(_dir)) return;

        // .asset 뿐 아니라 .prefab도 백업되므로 전부 훑는다.
        // 이름이 복구 대상 표에 없는 파일은 아래에서 건너뛴다.
        string[] _files = Directory.GetFiles(_dir);
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
