using System;
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
///   - 미공개 원석 재화 아이콘 -> UI_BlastFurnaceStatus 프리팹(용광로 HUD)
///                              + CurrencyCounterHUD 프리팹(재화 카운터)의 iconEntries
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
/// [정식판 기능 에셋]
/// 드론·부메랑·과열·회전 베기·별자리처럼 스킬로 여는 기능, 포자·열기처럼 2~4스테이지에만 있는
/// 기능의 프리팹·효과음·VFX도 끊습니다(StripFullOnlyFeatures). 코드는 남지만 데모에서 켜질 수 없는
/// 기능이고, 에셋은 파일을 뜯으면 그대로 보이기 때문입니다. "데모에서 켜질 수 있는가"는
/// 하드코딩하지 않고 데모 스킬 DB(AbilityBuildVariantData.demoSkillDataBase)의 스킬 명령과
/// 데모 최대 맵으로 판정합니다. 데모에 그 기능을 넣으면 자동으로 다시 남습니다.
/// 끊는 필드는 모두 코드가 null을 견디거나(풀 생성 시 null 검사 등) 데모에서 도달하지 않는 경로뿐임을
/// 2026-10-05에 하나씩 확인했습니다. 필드를 새로 추가할 때도 같은 확인을 거치십시오.
///
/// Graphics/VFX/Resources 폴더는 이름이 Resources라 참조와 무관하게 통째로 빌드에 실립니다.
/// 그 안을 Resources.Load로 읽는 코드는 없으므로(전부 인스펙터 참조), 데모 빌드 동안만 폴더 이름을
/// 바꿔 "참조된 것만" 실리게 합니다. 정식판 그림(드론 레이저, 발현 낙인, 별 표식 등)이 이 폴더에 있습니다.
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
    private const string FURNACE_HUD_PREFAB_PATH = "Assets/Prefabs/UI/WorldPopup/BlastFurnaceUI/UI_BlastFurnaceStatus.prefab";
    private const string CURRENCY_HUD_PREFAB_PATH = "Assets/Prefabs/UI/HUD/Common/CurrencyCounterHUD.prefab";
    private const string CHARACTER_PREFAB_PATH = "Assets/Prefabs/Objects/Character/Character.prefab";
    private const string TREE_PREFAB_PATH = "Assets/Prefabs/Objects/Trees/Tree.prefab";
    private const string OFFROAD_PREFAB_PATH = "Assets/Prefabs/Objects/OffRoadVehicle/OffRoadVehicle.prefab";
    private const string TENT_UI_PREFAB_PATH = "Assets/Prefabs/UI/TentUI/TentUI.prefab";
    private const string LOG_ITEM_DB_PATH = "Assets/Scriptable Obj/ItemData/LogItemData/LogItemTypeDataBase.asset";
    private const string TREE_STAT_DB_PATH = "Assets/Scriptable Obj/TreeStatData/Tree Stat Data Base.asset";
    private const string ABILITY_VARIANT_PATH = "Assets/Scriptable Obj/SkillData/AbilityBuildVariantData.asset";

    /// <summary>데모 빌드 동안 이름을 바꿔 두는 폴더입니다. 바꾼 이름은 Resources가 아니면 무엇이든 됩니다.</summary>
    private const string VFX_RESOURCES_FOLDER = "Assets/Graphics/VFX/Resources";
    private const string VFX_RESOURCES_HOLD_NAME = "_DemoBuildHold_VFX";
    private const string FOLDER_MOVE_MANIFEST = "_folder_moves.txt";

    private const string BACKUP_FOLDER = "DemoContentStripBackup";

    /// <summary>
    /// 백업/복원이 추적하는 에셋 전부. 새 대상을 추가하면 <b>여기에도 반드시 넣어야</b>
    /// 복원이 걸립니다(안 넣으면 그 에셋은 잘린 채로 남습니다).
    ///
    /// 백업 파일명이 원본의 <b>파일명</b>이라, 이 목록에 같은 파일명이 둘 생기면 백업이 서로
    /// 덮어써지고 복원도 엉뚱한 경로로 갑니다. 경로 전체를 키로 쓰지 않는 이유는, 그렇게 바꾸면
    /// 중단된 빌드가 남긴 <b>기존 백업을 못 찾아 복원이 통째로 건너뛰어지기</b> 때문입니다.
    /// 대신 아래 AssertNoDuplicateFileNames()가 빌드 시작 시 충돌을 잡아 즉시 멈춥니다.
    /// </summary>
    private static readonly string[] TRACKED_ASSET_PATHS =
    {
        AUDIO_DB_PATH,
        TREE_VISUAL_DB_PATH,
        INSTALLER_PREFAB_PATH,
        FURNACE_HUD_PREFAB_PATH,
        CURRENCY_HUD_PREFAB_PATH,
        CHARACTER_PREFAB_PATH,
        TREE_PREFAB_PATH,
        OFFROAD_PREFAB_PATH,
        TENT_UI_PREFAB_PATH,
        LOG_ITEM_DB_PATH,
        TREE_STAT_DB_PATH,
    };

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

        // 백업을 한 장이라도 뜨기 전에 확인한다. 뜬 뒤에 터뜨리면 이미 덮어써진 뒤다.
        AssertNoDuplicateFileNames();

        Directory.CreateDirectory(BackupDirectory);

        int _removedBgm = StripAudio(_maxPlayableMap);
        int _removedTree = StripTreeVisuals(_maxPlayableMap);
        int _removedGemOre = StripGemOreSprites(_maxPlayableMap, out int _removedFurnaceRecipe, out HashSet<GemOreType> _keepGemOre);

        // 원석 그림과 같은 판정으로 재화 아이콘도 끊는다. 판정을 못 내린 경우(_keepGemOre == null)는 건너뛴다.
        int _removedIcon = 0;
        if (null != _keepGemOre)
        {
            _removedIcon += StripFurnaceHudIcons(_keepGemOre);
            _removedIcon += StripCurrencyHudIcons(_keepGemOre);
        }

        string _fullOnly = StripFullOnlyFeatures(_maxPlayableMap);

        AssetDatabase.SaveAssets();

        // 저장이 끝난 뒤에 옮긴다. 폴더 이동이 저장되지 않은 수정분과 섞이지 않게 하기 위해서다.
        bool _heldVfx = HoldVfxResourcesFolder();

        Debug.Log($"[DemoStrip] 데모 빌드 - 미공개 콘텐츠 제외 (최대 플레이 맵: {_maxPlayableMap})\n" +
                  $"  BGM {_removedBgm}곡, 나무 비주얼 {_removedTree}종, 원석 그림 {_removedGemOre}종, 용광로 레시피 {_removedFurnaceRecipe}종, 재화 아이콘 {_removedIcon}개\n" +
                  $"  정식판 기능: {_fullOnly}\n" +
                  $"  VFX Resources 폴더 제외: {(true == _heldVfx ? "적용" : "건너뜀")}\n" +
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
    private static int StripGemOreSprites(MapType _maxPlayableMap, out int _removedFurnaceRecipes, out HashSet<GemOreType> _keepOut)
    {
        _removedFurnaceRecipes = 0;
        _keepOut = null;

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
        _keepOut = _keep;

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

    /// <summary>
    /// 용광로 HUD(UI_BlastFurnaceStatus)의 등급별 재화 아이콘에서 제외 대상 원석의 참조를 끊습니다.
    /// 필드가 배열이 아니라 등급마다 하나씩(goldIcon/diamondIcon/prismIcon)이라 짝을 직접 적어둡니다.
    /// 이 프리팹은 자기 파일이므로 백업도 따로 뜨고, 끊을 것이 있을 때만 백업하고 적용합니다.
    /// </summary>
    private static int StripFurnaceHudIcons(HashSet<GemOreType> _keep)
    {
        GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FURNACE_HUD_PREFAB_PATH);
        if (null == _prefab) return 0;

        UI_BlastFurnaceStatus _hud = _prefab.GetComponentInChildren<UI_BlastFurnaceStatus>(true);

        // 용광로 HUD가 아직 없는 브랜치일 수 있다. 빠뜨렸다고 빌드를 막을 일은 아니다.
        if (null == _hud) return 0;

        SerializedObject _so = new SerializedObject(_hud);
        int _removed = 0;

        if (false == _keep.Contains(GemOreType.Gold) && ClearSpriteRef(_so, "goldIcon")) _removed++;
        if (false == _keep.Contains(GemOreType.Diamond) && ClearSpriteRef(_so, "diamondIcon")) _removed++;
        if (false == _keep.Contains(GemOreType.Prism) && ClearSpriteRef(_so, "prismIcon")) _removed++;

        if (0 == _removed) return 0;

        Backup(FURNACE_HUD_PREFAB_PATH);

        _so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(_hud);

        return _removed;
    }

    /// <summary>
    /// 재화 카운터(CurrencyCounterHUD)의 iconEntries에서 제외 대상 원석 재화의 아이콘 참조를 끊습니다.
    /// 항목은 MoneyType으로 적혀 있으므로 GemOreType으로 되짚어 판정합니다. 원석이 아닌 재화(코인/당근 등)는 건드리지 않습니다.
    /// </summary>
    private static int StripCurrencyHudIcons(HashSet<GemOreType> _keep)
    {
        GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CURRENCY_HUD_PREFAB_PATH);
        if (null == _prefab) return 0;

        PresentationLayer.UISystem.CustomNumber.CurrencyCounterHUD _hud =
            _prefab.GetComponentInChildren<PresentationLayer.UISystem.CustomNumber.CurrencyCounterHUD>(true);
        if (null == _hud) return 0;

        SerializedObject _so = new SerializedObject(_hud);

        SerializedProperty _entries = _so.FindProperty("iconEntries");
        if (null == _entries) return 0;

        int _removed = 0;

        for (int i = 0; i < _entries.arraySize; i++)
        {
            SerializedProperty _entry = _entries.GetArrayElementAtIndex(i);

            GemOreType _ore = MoneyTypeToGemOreType((MoneyType)_entry.FindPropertyRelative("moneyType").enumValueIndex);
            if (GemOreType.None == _ore) continue;          // 원석 재화가 아니다
            if (true == _keep.Contains(_ore)) continue;

            if (true == ClearSpriteRef(_entry, "icon")) _removed++;
        }

        if (0 == _removed) return 0;

        Backup(CURRENCY_HUD_PREFAB_PATH);

        _so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(_hud);

        return _removed;
    }

    /// <summary>InventoryManager.GemOreTypeToMoneyType의 역방향. 원석 재화가 아니면 None.</summary>
    private static GemOreType MoneyTypeToGemOreType(MoneyType _moneyType)
    {
        switch (_moneyType)
        {
            case MoneyType.GoldOre: return GemOreType.Gold;
            case MoneyType.DiamondOre: return GemOreType.Diamond;
            case MoneyType.PrismOre: return GemOreType.Prism;
            default: return GemOreType.None;
        }
    }

    /// <summary>SerializedObject 최상위 필드용 오버로드. 참조가 있었으면 끊고 true를 돌려줍니다.</summary>
    private static bool ClearSpriteRef(SerializedObject _so, string _fieldName)
    {
        SerializedProperty _sprite = _so.FindProperty(_fieldName);
        if (null == _sprite || null == _sprite.objectReferenceValue) return false;

        _sprite.objectReferenceValue = null;
        return true;
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

#region 정식판 기능 에셋 제외

    /// <summary>
    /// 정식판 기능 중 데모에서 켜질 수 있는 것을 판정한 결과입니다.
    /// true 인 기능은 건드리지 않습니다.
    /// </summary>
    private sealed class DemoFeatureScope
    {
        public MapType maxMap;
        public HashSet<SkillType> skills = new HashSet<SkillType>();
        public HashSet<SkillCommandType> commands = new HashSet<SkillCommandType>();
        public HashSet<TreeType> trees = new HashSet<TreeType>();

        public bool boomerang;
        public bool drone;
        public bool overheat;
        public bool whirlwind;
        public bool constellation;

        /// <summary>포자막 나무는 2스테이지(FluffySporeForest)부터 나옵니다.</summary>
        public bool spore;

        /// <summary>나무 열기 분출은 4스테이지(MagmaForest) 데이터에만 있습니다(StageTileDataSO.treeHeatStaminaDamage).</summary>
        public bool heat;

        /// <summary>나무 화상은 과열 충격파(스킬) 또는 4스테이지 열기에서만 생깁니다.</summary>
        public bool burn;

        public bool MapReachable(int _mapType) => _mapType <= (int)maxMap;
    }

    /// <summary>
    /// 정식판 기능의 에셋 참조를 끊고, 무엇을 끊었는지 한 줄로 돌려줍니다.
    /// 판정 근거(데모 스킬 DB)를 못 읽으면 아무것도 끊지 않습니다. 잘못 끊으면 데모가 깨지지만,
    /// 덜 끊으면 용량과 유출만 늘기 때문입니다.
    /// </summary>
    private static string StripFullOnlyFeatures(MapType _maxPlayableMap)
    {
        DemoFeatureScope _scope = ReadDemoFeatureScope(_maxPlayableMap);

        if (null == _scope)
        {
            Debug.LogWarning($"[DemoStrip] 데모 스킬 DB를 읽지 못해 정식판 기능 에셋 제외를 건너뜁니다({ABILITY_VARIANT_PATH}).");
            return "건너뜀 (데모 스킬 DB 없음)";
        }

        int _sfx = StripFullOnlySfx(_scope);
        int _installer = StripInstallerFullOnly(_scope);
        int _character = StripCharacterFullOnly(_scope);
        int _tree = StripTreePrefabFullOnly(_scope);
        int _offroad = StripOffroadFullOnly(_scope);
        int _icons = StripTentAbilityIcons(_scope);
        int _logIcons = StripLogItemIcons(_scope);
        int _regen = StripTreeRegenStrategies(_scope);

        return $"효과음 {_sfx}개, 설치 프리팹 {_installer}곳, 캐릭터 {_character}곳, 나무 {_tree}곳, 차량 {_offroad}곳, " +
               $"스킬트리 아이콘 {_icons}개, 원목 아이콘 {_logIcons}개, 실드 회복 {_regen}개";
    }

    private static DemoFeatureScope ReadDemoFeatureScope(MapType _maxPlayableMap)
    {
        ScriptableObject _variant = AssetDatabase.LoadAssetAtPath<ScriptableObject>(ABILITY_VARIANT_PATH);
        if (null == _variant) return null;

        SerializedProperty _dbRef = new SerializedObject(_variant).FindProperty("demoSkillDataBase");
        if (null == _dbRef || null == _dbRef.objectReferenceValue) return null;

        SerializedProperty _skills = new SerializedObject(_dbRef.objectReferenceValue).FindProperty("skills");
        if (null == _skills || false == _skills.isArray || 0 == _skills.arraySize) return null;

        DemoFeatureScope _scope = new DemoFeatureScope { maxMap = _maxPlayableMap };

        for (int i = 0; i < _skills.arraySize; i++)
        {
            SerializedProperty _skill = _skills.GetArrayElementAtIndex(i);
            _scope.skills.Add((SkillType)_skill.FindPropertyRelative("skillType").intValue);

            SerializedProperty _effects = _skill.FindPropertyRelative("skillTypes");
            for (int j = 0; null != _effects && j < _effects.arraySize; j++)
            {
                _scope.commands.Add((SkillCommandType)_effects.GetArrayElementAtIndex(j).FindPropertyRelative("skillCommandType").intValue);
            }
        }

        foreach (TreeType _tree in CollectDemoTreeTypes(_maxPlayableMap)) _scope.trees.Add(_tree);

        HashSet<SkillCommandType> _c = _scope.commands;
        _scope.boomerang = _c.Contains(SkillCommandType.Boomerang) || _c.Contains(SkillCommandType.LumberjackNPCBoomerang);
        _scope.drone = _c.Contains(SkillCommandType.Drone);
        _scope.overheat = _c.Contains(SkillCommandType.Overheat) || _c.Contains(SkillCommandType.OverheatPermanent);
        _scope.whirlwind = _c.Contains(SkillCommandType.WhirlWind);
        _scope.constellation = _c.Contains(SkillCommandType.ConstellationManifestUnlock) || _c.Contains(SkillCommandType.StarGazeUnlock) ||
                               _c.Contains(SkillCommandType.StarMarkDamage) || _c.Contains(SkillCommandType.StarPathSpeedBoost) ||
                               _c.Contains(SkillCommandType.ConstellationDamage) || _c.Contains(SkillCommandType.ManifestationBrand);
        _scope.spore = _scope.MapReachable((int)MapType.FluffySporeForest);
        _scope.heat = _scope.MapReachable((int)MapType.MagmaForest);
        _scope.burn = _scope.overheat || _scope.heat;

        return _scope;
    }

    /// <summary>
    /// 기능별 효과음입니다. AudioManager는 없는 ID를 경고만 남기고 넘어가며(PlayInternal),
    /// 아래 소리는 모두 해당 기능 코드(Boomerang/Drone/TreeObj 실드·화상/별자리)에서만 울립니다.
    /// 데모 나무는 실드(sp)가 0이라 포자막 소리도 울리지 않습니다(2026-10-05 Tree Stat Data Base 확인).
    /// </summary>
    private static int StripFullOnlySfx(DemoFeatureScope _s)
    {
        HashSet<SoundID> _drop = new HashSet<SoundID>();

        if (false == _s.boomerang) { _drop.Add(SoundID.SpinStart); _drop.Add(SoundID.SpinLoop); }
        if (false == _s.drone) { _drop.Add(SoundID.SFXBeamFire); _drop.Add(SoundID.SFXChargeUp); _drop.Add(SoundID.SFXPunchImpact); _drop.Add(SoundID.SFXVoltageImpact); }
        if (false == _s.spore) { _drop.Add(SoundID.SporeHit); _drop.Add(SoundID.SporeExplosion); _drop.Add(SoundID.SporeShieldBreak); }
        if (false == _s.constellation) { _drop.Add(SoundID.Starappear); _drop.Add(SoundID.Stardisappear); _drop.Add(SoundID.StarExplosion); }
        if (false == _s.burn) { _drop.Add(SoundID.FireStart); _drop.Add(SoundID.FireLoop); }
        if (false == _s.heat) { _drop.Add(SoundID.TreeFireExplosion); }

        if (0 == _drop.Count) return 0;

        AudioDatabase _db = AssetDatabase.LoadAssetAtPath<AudioDatabase>(AUDIO_DB_PATH);
        if (null == _db || null == _db.sounds) return 0;

        Backup(AUDIO_DB_PATH);

        int _removed = _db.sounds.RemoveAll(_sound => null != _sound && _drop.Contains(_sound.id));
        if (_removed > 0) EditorUtility.SetDirty(_db);

        return _removed;
    }

    /// <summary>
    /// GameInstaller에 걸린 정식판 기능의 프리팹·데이터입니다.
    /// - VFX 풀: InDungeonVFXManager·ConstellationPixelLaserCreator는 프리팹이 null이면 풀을 만들지 않습니다.
    /// - VFXComponent: 프리팹이 null인 항목은 초기화에서 건너뛰고, 없는 태그는 Play가 null을 돌려줍니다.
    /// - 맵별 목록: 데모 최대 맵보다 뒤의 맵 항목만 비웁니다. 그 맵으로는 데모에서 이동할 수 없습니다.
    /// - 스킬 명령: SkillDispatcher는 null 항목을 건너뛰고 없는 명령을 TryGetValue로 찾습니다.
    ///   데모 스킬이 쓰는 명령은 남기고, 나머지만 끊습니다.
    /// </summary>
    private static int StripInstallerFullOnly(DemoFeatureScope _s)
    {
        GameObject _root = AssetDatabase.LoadAssetAtPath<GameObject>(INSTALLER_PREFAB_PATH);
        if (null == _root) return 0;

        int _n = 0;

        _n += EditComponents(_root, "InDungeonVFXManager", INSTALLER_PREFAB_PATH, _so =>
        {
            int _c = 0;
            if (false == _s.constellation)
            {
                _c += ClearRef(_so, "constellationDottedLinePrefab");
                _c += ClearRef(_so, "treeStarMarkGroundPrefab");
                _c += ClearRef(_so, "brandStarWrapPrefab");
                _c += ClearRef(_so, "shootingStarVfxPrefab");
                _c += ClearRef(_so, "starAppearAuraPrefab");
            }
            if (false == _s.spore) _c += ClearRef(_so, "sporeExplosionVfxPrefab");
            if (false == _s.heat) _c += ClearRef(_so, "treeHeatIndicatorMaterial");
            return _c;
        });

        if (false == _s.constellation) _n += EditComponents(_root, "ConstellationPixelLaserCreator", INSTALLER_PREFAB_PATH, _so => ClearRef(_so, "laserPrefab"));
        if (false == _s.boomerang) _n += EditComponents(_root, "BoomerangCreator", INSTALLER_PREFAB_PATH, _so => ClearRef(_so, "boomerangPrefab"));

        HashSet<string> _dropTags = new HashSet<string>();
        if (false == _s.spore) { _dropTags.Add("SporeShieldBrokenEffect"); _dropTags.Add("SporeShieldBrokenEffect_Bellpine"); }
        if (false == _s.constellation) _dropTags.Add("ManifestationBrandStampEffect");
        if (false == _s.heat) _dropTags.Add("TreeHeatEmitEffect");
        _n += EditComponents(_root, "VFXComponent", INSTALLER_PREFAB_PATH, _so => ClearVfxPoolEntries(_so, _dropTags));

        _n += EditComponents(_root, "TileMapGenerator", INSTALLER_PREFAB_PATH, _so => ClearUnreachableMapEntries(_so, "mapTypeTileDatas", "tileData", _s));
        _n += EditComponents(_root, "InDungeonObjectManager", INSTALLER_PREFAB_PATH, _so => ClearUnreachableMapEntries(_so, "mapTypeTreeGenerationDatas", "strategy", _s));
        _n += EditComponents(_root, "EnvironmentParticleSystem", INSTALLER_PREFAB_PATH, _so => ClearUnreachableMapEntries(_so, "mapParticleMappings", "particlePrefab", _s));

        _n += EditComponents(_root, "SkillDispatcher", INSTALLER_PREFAB_PATH, _so =>
        {
            SerializedProperty _list = _so.FindProperty("skillCommands");
            int _c = 0;
            for (int i = 0; null != _list && i < _list.arraySize; i++)
            {
                SerializedProperty _e = _list.GetArrayElementAtIndex(i);
                SkillCommand _cmd = _e.objectReferenceValue as SkillCommand;
                if (null == _cmd || true == _s.commands.Contains(_cmd.skillCommandType)) continue;

                _e.objectReferenceValue = null;
                _c++;
            }
            return _c;
        });

        return _n;
    }

    /// <summary>
    /// 캐릭터에 걸린 정식판 무기·과열입니다.
    /// 부메랑·드론 풀은 생성자에서 미리 만들지 않고, 개수가 0이면 Get을 부르지 않습니다
    /// (기본 개수 0은 CharacterStatBuildGuard가 보장). 과열 충격파는 null이면 풀을 만들지 않습니다.
    /// 회전 베기 프레임은 회전 베기 발동 때만 읽힙니다.
    /// </summary>
    private static int StripCharacterFullOnly(DemoFeatureScope _s)
    {
        GameObject _root = AssetDatabase.LoadAssetAtPath<GameObject>(CHARACTER_PREFAB_PATH);
        if (null == _root) return 0;

        int _n = 0;

        if (false == _s.boomerang) _n += EditComponents(_root, "BoomerangCreator", CHARACTER_PREFAB_PATH, _so => ClearRef(_so, "boomerangPrefab"));
        if (false == _s.drone) _n += EditComponents(_root, "DroneCreator", CHARACTER_PREFAB_PATH, _so => ClearRef(_so, "dronePrefab"));
        if (false == _s.whirlwind) _n += EditComponents(_root, "AttackComponent", CHARACTER_PREFAB_PATH, _so => ClearArray(_so, "whirlwindFrames"));

        if (false == _s.overheat)
        {
            _n += EditComponents(_root, "AxeExtraAttackCreator", CHARACTER_PREFAB_PATH, _so => ClearRef(_so, "overheatShockWavePrefab"));
            _n += EditComponents(_root, "VFXComponent", CHARACTER_PREFAB_PATH, _so => ClearVfxPoolEntries(_so, new HashSet<string> { "OverheatLoopEffect" }));
        }

        return _n;
    }

    /// <summary>
    /// 모든 나무에 붙은 별 표식(별자리)과 화상 이펙트입니다.
    /// TreeStarMarkAnimator는 프레임이 비면 아무것도 하지 않고, 화상 머티리얼은 화상 때만 씁니다.
    /// </summary>
    private static int StripTreePrefabFullOnly(DemoFeatureScope _s)
    {
        GameObject _root = AssetDatabase.LoadAssetAtPath<GameObject>(TREE_PREFAB_PATH);
        if (null == _root) return 0;

        int _n = 0;

        if (false == _s.constellation)
        {
            _n += EditComponents(_root, "TreeStarMarkAnimator", TREE_PREFAB_PATH, _so => ClearArray(_so, "frames"));

            // 별 표식 오브젝트의 기본 스프라이트도 같은 그림이다.
            Component[] _all = _root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < _all.Length; i++)
            {
                if (null == _all[i] || "TreeStarMarkAnimator" != _all[i].GetType().Name) continue;

                SpriteRenderer _sr = _all[i].GetComponent<SpriteRenderer>();
                if (null == _sr || null == _sr.sprite) continue;

                Backup(TREE_PREFAB_PATH);
                SerializedObject _so = new SerializedObject(_sr);
                _n += ClearRef(_so, "m_Sprite");
                _so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(_sr);
            }
        }

        if (false == _s.burn) _n += EditComponents(_root, "VFX_TreeBurn", TREE_PREFAB_PATH, _so => ClearRef(_so, "burnMaterial"));

        return _n;
    }

    /// <summary>원정 차량의 3·4스테이지 외형입니다. 해당 맵에서만 쓰고 null이면 기본 외형을 유지합니다.</summary>
    private static int StripOffroadFullOnly(DemoFeatureScope _s)
    {
        GameObject _root = AssetDatabase.LoadAssetAtPath<GameObject>(OFFROAD_PREFAB_PATH);
        if (null == _root) return 0;

        return EditComponents(_root, "OffroadVehicleObj", OFFROAD_PREFAB_PATH, _so =>
        {
            int _c = 0;
            if (false == _s.MapReachable((int)MapType.StarrootForest)) { _c += ClearRef(_so, "darkBaseSprite"); _c += ClearRef(_so, "darkWheelSprite"); }
            if (false == _s.MapReachable((int)MapType.MagmaForest)) { _c += ClearRef(_so, "cinderBaseSprite"); _c += ClearRef(_so, "cinderWheelSprite"); }
            return _c;
        });
    }

    /// <summary>
    /// 스킬 트리의 스킬별 아이콘입니다. 데모 트리는 데모 스킬만 그리고, 아이콘 캐시는 null을 건너뜁니다.
    /// </summary>
    private static int StripTentAbilityIcons(DemoFeatureScope _s)
    {
        GameObject _root = AssetDatabase.LoadAssetAtPath<GameObject>(TENT_UI_PREFAB_PATH);
        if (null == _root) return 0;

        return EditComponents(_root, "UI_TentAbilityComponent", TENT_UI_PREFAB_PATH, _so =>
        {
            SerializedProperty _list = _so.FindProperty("pictureBindings");
            int _c = 0;
            for (int i = 0; null != _list && i < _list.arraySize; i++)
            {
                SerializedProperty _e = _list.GetArrayElementAtIndex(i);
                SkillType _type = (SkillType)_e.FindPropertyRelative("skillType").intValue;
                if (SkillType.None == _type || true == _s.skills.Contains(_type)) continue;

                _c += ClearRelativeRef(_e, "sprite");
            }
            return _c;
        });
    }

    /// <summary>미공개 나무의 원목 아이콘입니다. 그 나무가 데모에 나오지 않으므로 그 원목도 얻을 수 없습니다.</summary>
    private static int StripLogItemIcons(DemoFeatureScope _s)
    {
        ScriptableObject _db = AssetDatabase.LoadAssetAtPath<ScriptableObject>(LOG_ITEM_DB_PATH);
        if (null == _db) return 0;

        SerializedObject _so = new SerializedObject(_db);
        SerializedProperty _list = _so.FindProperty("datas");
        int _c = 0;

        for (int i = 0; null != _list && i < _list.arraySize; i++)
        {
            SerializedProperty _e = _list.GetArrayElementAtIndex(i);
            if (true == _s.trees.Contains((TreeType)_e.FindPropertyRelative("treeType").intValue)) continue;

            _c += ClearRelativeRef(_e, "timberSprite");

            SerializedProperty _states = _e.FindPropertyRelative("stateSprites");
            for (int j = 0; null != _states && j < _states.arraySize; j++) _c += ClearRelativeRef(_states.GetArrayElementAtIndex(j), "timberSprite");
        }

        return ApplyIfChanged(_so, _db, LOG_ITEM_DB_PATH, _c);
    }

    /// <summary>미공개 나무(포자막)의 실드 회복 규칙입니다. EHealthComponent는 null이면 회복하지 않습니다.</summary>
    private static int StripTreeRegenStrategies(DemoFeatureScope _s)
    {
        ScriptableObject _db = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TREE_STAT_DB_PATH);
        if (null == _db) return 0;

        SerializedObject _so = new SerializedObject(_db);
        SerializedProperty _list = _so.FindProperty("treeStatDatas");
        int _c = 0;

        for (int i = 0; null != _list && i < _list.arraySize; i++)
        {
            SerializedProperty _e = _list.GetArrayElementAtIndex(i);
            if (true == _s.trees.Contains((TreeType)_e.FindPropertyRelative("treeType").intValue)) continue;

            _c += ClearRelativeRef(_e, "regenStrategy");
        }

        return ApplyIfChanged(_so, _db, TREE_STAT_DB_PATH, _c);
    }

#endregion

#region 정식판 기능 에셋 제외 - 도우미

    /// <summary>
    /// 프리팹 안에서 이름이 같은 컴포넌트를 전부 찾아 고칩니다. 타입을 이름으로 찾는 이유는
    /// 네임스페이스·어셈블리가 다른 런타임 타입을 이 에디터 스크립트가 직접 참조하지 않기 위해서입니다.
    /// 컴포넌트가 없으면(브랜치에 기능이 아직 없음) 조용히 0을 돌려줍니다.
    /// </summary>
    private static int EditComponents(GameObject _root, string _typeName, string _assetPath, Func<SerializedObject, int> _edit)
    {
        Component[] _all = _root.GetComponentsInChildren<Component>(true);
        int _total = 0;

        for (int i = 0; i < _all.Length; i++)
        {
            if (null == _all[i] || _typeName != _all[i].GetType().Name) continue;

            SerializedObject _so = new SerializedObject(_all[i]);
            int _changed = _edit(_so);
            if (0 == _changed) continue;

            Backup(_assetPath);
            _so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(_all[i]);
            _total += _changed;
        }

        return _total;
    }

    private static int ApplyIfChanged(SerializedObject _so, UnityEngine.Object _target, string _assetPath, int _changed)
    {
        if (0 == _changed) return 0;

        Backup(_assetPath);
        _so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(_target);
        return _changed;
    }

    private static int ClearRef(SerializedObject _so, string _field)
    {
        SerializedProperty _p = _so.FindProperty(_field);
        if (null == _p || SerializedPropertyType.ObjectReference != _p.propertyType || null == _p.objectReferenceValue) return 0;

        _p.objectReferenceValue = null;
        return 1;
    }

    private static int ClearRelativeRef(SerializedProperty _entry, string _field)
    {
        SerializedProperty _p = _entry.FindPropertyRelative(_field);
        if (null == _p || SerializedPropertyType.ObjectReference != _p.propertyType || null == _p.objectReferenceValue) return 0;

        _p.objectReferenceValue = null;
        return 1;
    }

    private static int ClearArray(SerializedObject _so, string _field)
    {
        SerializedProperty _p = _so.FindProperty(_field);
        if (null == _p || false == _p.isArray || 0 == _p.arraySize) return 0;

        _p.arraySize = 0;
        return 1;
    }

    /// <summary>VFXComponent.vfxPoolDataList 에서 태그가 맞는 항목의 프리팹만 끊습니다(항목 자체는 남김).</summary>
    private static int ClearVfxPoolEntries(SerializedObject _so, HashSet<string> _tags)
    {
        if (0 == _tags.Count) return 0;

        SerializedProperty _list = _so.FindProperty("vfxPoolDataList");
        int _c = 0;

        for (int i = 0; null != _list && i < _list.arraySize; i++)
        {
            SerializedProperty _e = _list.GetArrayElementAtIndex(i);
            SerializedProperty _tag = _e.FindPropertyRelative("vfxTag");
            if (null == _tag || false == _tags.Contains(_tag.stringValue)) continue;

            _c += ClearRelativeRef(_e, "effectPrefab");
        }

        return _c;
    }

    /// <summary>맵별 목록에서 데모 최대 맵보다 뒤의 맵 항목의 참조만 끊습니다.</summary>
    private static int ClearUnreachableMapEntries(SerializedObject _so, string _listField, string _refField, DemoFeatureScope _s)
    {
        SerializedProperty _list = _so.FindProperty(_listField);
        int _c = 0;

        for (int i = 0; null != _list && i < _list.arraySize; i++)
        {
            SerializedProperty _e = _list.GetArrayElementAtIndex(i);
            SerializedProperty _map = _e.FindPropertyRelative("mapType");
            if (null == _map || true == _s.MapReachable(_map.intValue)) continue;

            _c += ClearRelativeRef(_e, _refField);
        }

        return _c;
    }

    /// <summary>
    /// Graphics/VFX/Resources 폴더의 이름을 잠시 바꿉니다. 기록을 먼저 남기고 옮기므로, 옮긴 직후
    /// 에디터가 죽어도 다음 실행 때 RestoreHeldFolders가 되돌립니다.
    /// </summary>
    private static bool HoldVfxResourcesFolder()
    {
        if (false == AssetDatabase.IsValidFolder(VFX_RESOURCES_FOLDER)) return false;

        string _held = Path.GetDirectoryName(VFX_RESOURCES_FOLDER).Replace('\\', '/') + "/" + VFX_RESOURCES_HOLD_NAME;

        if (true == AssetDatabase.IsValidFolder(_held))
        {
            Debug.LogWarning($"[DemoStrip] {_held} 가 이미 있어 VFX Resources 폴더 제외를 건너뜁니다. 이전 빌드의 잔재인지 확인하십시오.");
            return false;
        }

        File.AppendAllText(Path.Combine(BackupDirectory, FOLDER_MOVE_MANIFEST), _held + "\t" + VFX_RESOURCES_FOLDER + "\n");

        string _error = AssetDatabase.RenameAsset(VFX_RESOURCES_FOLDER, VFX_RESOURCES_HOLD_NAME);
        if (false == string.IsNullOrEmpty(_error))
        {
            Debug.LogWarning($"[DemoStrip] VFX Resources 폴더 이름을 바꾸지 못했습니다: {_error}");
            return false;
        }

        return true;
    }

    private static List<string> RestoreHeldFolders(string _backupDir)
    {
        List<string> _restored = new List<string>();
        string _manifest = Path.Combine(_backupDir, FOLDER_MOVE_MANIFEST);

        if (false == File.Exists(_manifest)) return _restored;

        string[] _lines = File.ReadAllLines(_manifest);

        for (int i = 0; i < _lines.Length; i++)
        {
            string[] _pair = _lines[i].Split('\t');
            if (2 != _pair.Length) continue;

            string _held = _pair[0];
            string _original = _pair[1];

            if (false == AssetDatabase.IsValidFolder(_held) || true == AssetDatabase.IsValidFolder(_original)) continue;

            string _error = AssetDatabase.RenameAsset(_held, Path.GetFileName(_original));
            if (true == string.IsNullOrEmpty(_error))
            {
                _restored.Add(_original);
            }
            else
            {
                Debug.LogError($"[DemoStrip] {_held} 를 {_original} 로 되돌리지 못했습니다: {_error}\n직접 이름을 Resources 로 바꾸십시오.");
            }
        }

        return _restored;
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

    /// <summary>
    /// 원본을 한 번만 백업합니다. 같은 에셋을 여러 단계가 고치므로(GameInstaller, AudioDatabase),
    /// 두 번째 백업이 첫 단계의 수정본을 "원본"으로 덮어쓰지 않게 합니다. 백업 폴더는 매 빌드 시작 때
    /// 복구와 함께 비워지므로 이전 빌드의 파일이 남아 있을 일은 없습니다.
    /// </summary>
    private static void Backup(string _assetPath)
    {
        string _dest = Path.Combine(BackupDirectory, Path.GetFileName(_assetPath));
        if (true == File.Exists(_dest)) return;

        File.Copy(ToAbsolute(_assetPath), _dest, true);
    }

    private static string FindAssetPathByFileName(string _fileName)
    {
        for (int i = 0; i < TRACKED_ASSET_PATHS.Length; i++)
        {
            if (Path.GetFileName(TRACKED_ASSET_PATHS[i]) == _fileName) return TRACKED_ASSET_PATHS[i];
        }

        return null;
    }

    /// <summary>
    /// 추적 대상에 같은 파일명이 둘 이상이면 빌드를 멈춥니다.
    ///
    /// 백업 키가 파일명이라, 충돌하면 한쪽 백업이 다른 쪽에 덮어써지고 복원 시 원본이 뒤바뀝니다.
    /// 그 결과는 "빌드는 성공했는데 에셋이 조용히 잘린 채로 남는 것"이라 늦게 발견됩니다.
    /// 여기서 터뜨리는 편이 훨씬 싸므로 예외로 올립니다.
    /// </summary>
    private static void AssertNoDuplicateFileNames()
    {
        for (int i = 0; i < TRACKED_ASSET_PATHS.Length; i++)
        {
            for (int j = i + 1; j < TRACKED_ASSET_PATHS.Length; j++)
            {
                if (Path.GetFileName(TRACKED_ASSET_PATHS[i]) != Path.GetFileName(TRACKED_ASSET_PATHS[j])) continue;

                throw new BuildFailedException(
                    $"[DemoStrip] 백업 대상 파일명이 겹칩니다: {TRACKED_ASSET_PATHS[i]} / {TRACKED_ASSET_PATHS[j]}. " +
                    "백업 키가 파일명이라 한쪽이 덮어써지고 복원이 어긋납니다. 둘 중 하나의 파일명을 바꾸십시오.");
            }
        }
    }

    /// <summary>
    /// 백업이 남아 있으면 원본으로 되돌립니다. 백업 폴더의 존재 자체가 "아직 복구 안 됨" 표시입니다.
    /// </summary>
    public static void RestoreIfNeeded(bool _isEditorStartup)
    {
        string _dir = BackupDirectory;
        if (false == Directory.Exists(_dir)) return;

        // 이름을 바꿔 둔 폴더부터 되돌린다. 아래 파일 복구와 무관하지만, 기록 파일이 백업 폴더 안에 있어
        // 폴더를 지우기 전에 읽어야 한다.
        List<string> _restored = RestoreHeldFolders(_dir);

        // .asset 뿐 아니라 .prefab도 백업되므로 전부 훑는다.
        // 이름이 복구 대상 표에 없는 파일은 아래에서 건너뛴다.
        string[] _files = Directory.GetFiles(_dir);

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
