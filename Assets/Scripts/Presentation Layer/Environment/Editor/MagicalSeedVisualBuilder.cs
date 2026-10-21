#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// 마법 씨앗 전리품 리소스(Assets/Graphics/Magical Seed) 적용기. 여러 번 실행해도 같은 결과로 맞춘다.
///  - Loot  (StageNN_이름.png, 32x32)          → 드랍 스프라이트(LootItemTypeDataBase)
///  - Big   (StageNN-K_이름 Seed|Flower|Tree.png, 64x64) → 확대 스프라이트(LootGrowthVisualData.sprite)
///  - Small (StageNN-K_이름 Seed|Flower|Tree_S.png, 32x32 프레임 가로 시트) → 전시 프레임(LootGrowthVisualData.displayFrames)
/// 스테이지 번호 → 전리품은 StageLoots, 이름의 Seed/Flower/Tree → 단계 1/2/3.
/// 전시 재생 규칙(idle/action/fps)은 DisplaySpecs. 표에 없는 시트(예: 임시인 Stage04 Lava)는 프레임 전부를 순서대로 반복한다.
///
/// 메뉴 실행(Build)은 재생 규칙까지 표 기준으로 다시 쓴다. 파일을 교체/추가했을 때 MagicalSeedSheetPostprocessor가
/// 부르는 Refresh는 스프라이트·프레임만 다시 읽고, 이미 있는 항목의 재생 규칙(인스펙터에서 고친 값)은 그대로 둔다.
/// </summary>
public static class MagicalSeedVisualBuilder
{
    public const string RootPath = "Assets/Graphics/Magical Seed";
    public const string BigFolder = RootPath + "/Big";
    public const string SmallFolder = RootPath + "/Small";
    public const string LootFolder = RootPath + "/Loot";
    public const int FrameSize = 32;
    public const float PixelsPerUnit = 32f;

    private const string DataPath = "Assets/Scriptable Obj/LootData/LootGrowthVisualData.asset";
    private const string DbPath = "Assets/Scriptable Obj/LootData/LootItemTypeDataBase.asset";

    // Stage01 ~ Stage04
    private static readonly LootType[] StageLoots = { LootType.LostAndFoundBox, LootType.SporePotion, LootType.StarCompass, LootType.ObsidianCharm };

    private static readonly Vector2 ActionInterval = new Vector2(3f, 4f);

    private struct DisplaySpec
    {
        public int stage;
        public int tier;
        public int[] idle;
        public int[] action;
        public float fps;

        public DisplaySpec(int _stage, int _tier, int[] _idle, int[] _action, float _fps)
        {
            stage = _stage;
            tier = _tier;
            idle = _idle;
            action = _action;
            fps = _fps;
        }
    }

    private static int[] Seq(params int[] _frames) => _frames;
    private static readonly int[] None = new int[0];

    // action이 있으면 idle을 보이다가 3~4초 랜덤 간격으로 action을 한 번 재생하고 돌아온다. 없으면 idle을 계속 반복(Loop).
    private static readonly DisplaySpec[] DisplaySpecs =
    {
        // 바람개비
        new DisplaySpec(1, 1, Seq(0), Seq(0, 1, 2, 3, 4), 10f),
        new DisplaySpec(1, 2, Seq(0), Seq(0, 1, 2, 3, 4), 8f),
        new DisplaySpec(1, 3, Seq(0, 1, 2), None, 3f),
        // 몽글
        new DisplaySpec(2, 1, Seq(0, 1, 2, 3, 4), None, 8f),
        new DisplaySpec(2, 2, Seq(0), Seq(0, 1, 2, 3, 4, 5), 8f),
        new DisplaySpec(2, 3, Seq(0, 1), None, 2f),
        // 별비
        new DisplaySpec(3, 1, Seq(0), Seq(0, 1, 2, 1, 0), 10f),
        new DisplaySpec(3, 2, Seq(0), Seq(0, 1, 2, 3), 8f),
        new DisplaySpec(3, 3, Seq(0, 1), None, 2f),
    };

    // 표에 없는 시트의 기본 fps(씨앗/꽃/나무)
    private static readonly float[] DefaultFps = { 8f, 8f, 2f };

    private static readonly Regex StageTierRegex = new Regex(@"^Stage(\d+)-\d+_.*?(Seed|Flower|Tree)(_S)?$", RegexOptions.IgnoreCase);
    private static readonly Regex StageRegex = new Regex(@"^Stage(\d+)_", RegexOptions.IgnoreCase);

    [MenuItem("LumberBoy/Loot/Apply Magical Seed Resources")]
    public static void Build()
    {
        Apply(false);
    }

    /// <summary>파일 교체/추가 뒤 슬라이스·스프라이트·프레임만 다시 맞춘다(재생 규칙은 유지).</summary>
    public static void Refresh()
    {
        Apply(true);
    }

    private static void Apply(bool _bKeepAnimSettings)
    {
        // 시트 슬라이스부터 맞춘다(격자와 같으면 그대로. 재임포트는 동기라 아래에서 바로 새 프레임을 읽는다)
        foreach (string _path in FindPngs(SmallFolder).ToArray())
        {
            EnsureGridSlices(_path);
        }

        int _dropCount = ApplyDropSprites();
        LootGrowthVisualData _data = AssetDatabase.LoadAssetAtPath<LootGrowthVisualData>(DataPath);

        if (null == _data)
        {
            Debug.LogError("[MagicalSeedVisualBuilder] LootGrowthVisualData를 찾지 못했습니다: " + DataPath);
            return;
        }

        List<LootGrowthVisualData.TierSprite> _old = new List<LootGrowthVisualData.TierSprite>(_data.EditorTierSprites);
        List<LootGrowthVisualData.TierSprite> _entries = new List<LootGrowthVisualData.TierSprite>(12);

        foreach (string _path in FindPngs(BigFolder))
        {
            if (false == TryParseStageTier(_path, out LootType _loot, out int _stage, out int _tier)) continue;

            int _index = GetOrAddEntry(_entries, _old, _loot, _stage, _tier, _bKeepAnimSettings);
            LootGrowthVisualData.TierSprite _entry = _entries[_index];
            _entry.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(_path);
            if (Vector2.zero == _entry.spriteBase && null != _entry.sprite) _entry.spriteBase = EstimateBase(_path, _entry.sprite);
            _entries[_index] = _entry;
        }

        foreach (string _path in FindPngs(SmallFolder))
        {
            if (false == TryParseStageTier(_path, out LootType _loot, out int _stage, out int _tier)) continue;

            int _index = GetOrAddEntry(_entries, _old, _loot, _stage, _tier, _bKeepAnimSettings);
            LootGrowthVisualData.TierSprite _entry = _entries[_index];
            _entry.displayFrames = LoadFrames(_path);
            _entry.displayArtCenter = ComputeArtCenter(_path, _entry.displayFrames);
            if (Vector2.zero == _entry.displayBase && 0 < _entry.displayFrames.Length) _entry.displayBase = EstimateBase(_path, _entry.displayFrames[0]);
            _entries[_index] = _entry;
        }

        _entries.Sort((a, b) => a.lootType != b.lootType ? a.lootType.CompareTo(b.lootType) : a.tier.CompareTo(b.tier));

        if (true == IsSame(_old, _entries))
        {
            return;
        }

        _data.EditorTierSprites.Clear();
        _data.EditorTierSprites.AddRange(_entries);
        EditorUtility.SetDirty(_data);
        AssetDatabase.SaveAssetIfDirty(_data);

        Debug.Log("[MagicalSeedVisualBuilder] 적용 완료: 드랍 " + _dropCount + "개 / 성장 항목 " + _entries.Count + "개");
    }

    private static int GetOrAddEntry(List<LootGrowthVisualData.TierSprite> _entries, List<LootGrowthVisualData.TierSprite> _old,
        LootType _loot, int _stage, int _tier, bool _bKeepAnimSettings)
    {
        int _found = _entries.FindIndex(x => x.lootType == _loot && x.tier == _tier);

        if (0 <= _found)
        {
            return _found;
        }

        LootGrowthVisualData.TierSprite _entry = new LootGrowthVisualData.TierSprite();
        _entry.lootType = _loot;
        _entry.tier = _tier;
        _entry.displayFrames = new Sprite[0];

        int _oldIndex = _old.FindIndex(x => x.lootType == _loot && x.tier == _tier);

        // 밑둥은 그림을 보고 맞춘 값이라 메뉴 실행(Build)에서도 유지한다. 비어 있을 때만 픽셀에서 추정한다.
        if (0 <= _oldIndex)
        {
            _entry.displayBase = _old[_oldIndex].displayBase;
            _entry.spriteBase = _old[_oldIndex].spriteBase;
        }

        if (true == _bKeepAnimSettings && 0 <= _oldIndex && 0 < _old[_oldIndex].fps)
        {
            LootGrowthVisualData.TierSprite _prev = _old[_oldIndex];
            _entry.isPlaceholder = _prev.isPlaceholder;
            _entry.idleFrames = _prev.idleFrames;
            _entry.actionFrames = _prev.actionFrames;
            _entry.fps = _prev.fps;
            _entry.actionInterval = _prev.actionInterval;
        }
        else
        {
            int _specIndex = System.Array.FindIndex(DisplaySpecs, x => x.stage == _stage && x.tier == _tier);

            if (0 <= _specIndex)
            {
                _entry.idleFrames = (int[])DisplaySpecs[_specIndex].idle.Clone();
                _entry.actionFrames = (int[])DisplaySpecs[_specIndex].action.Clone();
                _entry.fps = DisplaySpecs[_specIndex].fps;
            }
            else
            {
                _entry.idleFrames = new int[0];
                _entry.actionFrames = new int[0];
                _entry.fps = DefaultFps[_tier - 1];
            }

            _entry.actionInterval = ActionInterval;
        }

        _entries.Add(_entry);
        return _entries.Count - 1;
    }

    // 드랍: Loot/StageNN_*.png → LootItemTypeDataBase
    private static int ApplyDropSprites()
    {
        LootItemTypeDataBase _db = AssetDatabase.LoadAssetAtPath<LootItemTypeDataBase>(DbPath);

        if (null == _db || null == _db.datas)
        {
            Debug.LogError("[MagicalSeedVisualBuilder] LootItemTypeDataBase를 찾지 못했습니다: " + DbPath);
            return 0;
        }

        int _count = 0;
        bool _bChanged = false;

        foreach (string _path in FindPngs(LootFolder))
        {
            Match _match = StageRegex.Match(Path.GetFileNameWithoutExtension(_path));

            if (false == _match.Success || false == TryGetStageLoot(int.Parse(_match.Groups[1].Value), out LootType _loot)) continue;

            LootItemTypeData _item = _db.Get(_loot);
            Sprite _sprite = AssetDatabase.LoadAssetAtPath<Sprite>(_path);

            if (null == _item || null == _sprite) continue;

            _count++;

            if (_item.sprite != _sprite)
            {
                _item.sprite = _sprite;
                _bChanged = true;
            }
        }

        if (true == _bChanged)
        {
            EditorUtility.SetDirty(_db);
            AssetDatabase.SaveAssetIfDirty(_db);
        }

        return _count;
    }

    private static bool TryParseStageTier(string _path, out LootType _loot, out int _stage, out int _tier)
    {
        _loot = LootType.None;
        _stage = 0;
        _tier = 0;

        Match _match = StageTierRegex.Match(Path.GetFileNameWithoutExtension(_path));

        if (false == _match.Success) return false;

        _stage = int.Parse(_match.Groups[1].Value);

        switch (_match.Groups[2].Value.ToLowerInvariant())
        {
            case "seed": _tier = 1; break;
            case "flower": _tier = 2; break;
            default: _tier = 3; break;
        }

        return TryGetStageLoot(_stage, out _loot);
    }

    private static bool TryGetStageLoot(int _stage, out LootType _loot)
    {
        _loot = LootType.None;

        if (_stage < 1 || StageLoots.Length < _stage) return false;

        _loot = StageLoots[_stage - 1];
        return true;
    }

    private static IEnumerable<string> FindPngs(string _folder)
    {
        if (false == AssetDatabase.IsValidFolder(_folder)) return Enumerable.Empty<string>();

        return AssetDatabase.FindAssets("t:Texture2D", new[] { _folder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(x => x.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) && Path.GetDirectoryName(x).Replace('\\', '/') == _folder)
            .OrderBy(x => x, System.StringComparer.Ordinal);
    }

    // 시트의 프레임: 위 줄부터, 왼쪽 → 오른쪽
    private static Sprite[] LoadFrames(string _path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(_path).OfType<Sprite>()
            .OrderByDescending(x => x.rect.y)
            .ThenBy(x => x.rect.x)
            .ToArray();
    }

    // 프레임마다 알파가 있는 픽셀 영역의 중심을 구해 평균 낸 값 - 프레임(스프라이트) 중심 기준, 유닛.
    // 텍스처가 읽기 불가라 원본 PNG를 따로 읽는다.
    private static Vector2 ComputeArtCenter(string _path, Sprite[] _frames)
    {
        if (null == _frames || 0 == _frames.Length) return Vector2.zero;

        Texture2D _tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        try
        {
            if (false == _tex.LoadImage(File.ReadAllBytes(_path))) return Vector2.zero;

            Color32[] _pixels = _tex.GetPixels32();
            float _sumX = 0f;
            float _sumY = 0f;
            int _found = 0;

            for (int f = 0; f < _frames.Length; f++)
            {
                Rect _rect = _frames[f].rect;
                int _minX = int.MaxValue, _minY = int.MaxValue, _maxX = int.MinValue, _maxY = int.MinValue;

                for (int y = (int)_rect.y; y < (int)_rect.yMax && y < _tex.height; y++)
                {
                    for (int x = (int)_rect.x; x < (int)_rect.xMax && x < _tex.width; x++)
                    {
                        if (0 == _pixels[y * _tex.width + x].a) continue;

                        if (x < _minX) _minX = x;
                        if (_maxX < x) _maxX = x;
                        if (y < _minY) _minY = y;
                        if (_maxY < y) _maxY = y;
                    }
                }

                if (_maxX < _minX) continue;

                // 프레임 중심 기준 픽셀 오프셋
                _sumX += (_minX + _maxX + 1) * 0.5f - _rect.center.x;
                _sumY += (_minY + _maxY + 1) * 0.5f - _rect.center.y;
                _found++;
            }

            if (0 == _found) return Vector2.zero;

            float _ppu = _frames[0].pixelsPerUnit;
            return new Vector2(_sumX / _found / _ppu, _sumY / _found / _ppu);
        }
        finally
        {
            Object.DestroyImmediate(_tex);
        }
    }

    // 밑둥 추정: 가장 아래 줄의 가로 중심, 높이는 그 줄에서 프레임 크기의 1/16만큼 위(바닥 면 중심 근사) - 스프라이트 중심 기준, 유닛.
    // 꽃처럼 잎이 옆으로 퍼진 그림은 맞지 않을 수 있으니 결과를 보고 데이터에서 고친다.
    private static Vector2 EstimateBase(string _path, Sprite _sprite)
    {
        Texture2D _tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        try
        {
            if (false == _tex.LoadImage(File.ReadAllBytes(_path))) return Vector2.zero;

            Color32[] _pixels = _tex.GetPixels32();
            Rect _rect = _sprite.rect;

            for (int y = (int)_rect.y; y < (int)_rect.yMax && y < _tex.height; y++)
            {
                int _minX = int.MaxValue, _maxX = int.MinValue;

                for (int x = (int)_rect.x; x < (int)_rect.xMax && x < _tex.width; x++)
                {
                    if (0 == _pixels[y * _tex.width + x].a) continue;

                    if (x < _minX) _minX = x;
                    if (_maxX < x) _maxX = x;
                }

                if (_maxX < _minX) continue;

                float _ppu = _sprite.pixelsPerUnit;
                float _baseX = (_minX + _maxX + 1) * 0.5f - _rect.center.x;
                float _baseY = y + _rect.height / 16f - _rect.center.y;
                return new Vector2(_baseX / _ppu, _baseY / _ppu);
            }

            return Vector2.zero;
        }
        finally
        {
            Object.DestroyImmediate(_tex);
        }
    }

    /// <summary>
    /// Small 시트를 32x32 격자로 슬라이스한다. 이미 격자와 같으면 아무것도 하지 않는다(false).
    /// 기존 조각은 이름/ID를 그대로 두고 위치만 맞추므로 기존 참조가 유지된다. 바꿨으면 재임포트까지 하고 true.
    /// </summary>
    public static bool EnsureGridSlices(string _path)
    {
        TextureImporter _importer = AssetImporter.GetAtPath(_path) as TextureImporter;

        if (null == _importer) return false;

        _importer.GetSourceTextureWidthAndHeight(out int _width, out int _height);

        int _cols = _width / FrameSize;
        int _rows = _height / FrameSize;

        if (_cols < 1 || _rows < 1) return false;

        bool _bModeChanged = SpriteImportMode.Multiple != _importer.spriteImportMode;

        if (true == _bModeChanged)
        {
            _importer.spriteImportMode = SpriteImportMode.Multiple;
        }

        SpriteDataProviderFactories _factory = new SpriteDataProviderFactories();
        _factory.Init();
        ISpriteEditorDataProvider _provider = _factory.GetSpriteEditorDataProviderFromObject(_importer);
        _provider.InitSpriteEditorDataProvider();

        List<SpriteRect> _rects = _provider.GetSpriteRects().OrderByDescending(x => x.rect.y).ThenBy(x => x.rect.x).ToList();
        int _count = _cols * _rows;
        bool _bChanged = _bModeChanged || _rects.Count != _count;

        string _baseName = Path.GetFileNameWithoutExtension(_path);
        SpriteRect[] _result = new SpriteRect[_count];

        for (int i = 0; i < _count; i++)
        {
            int _col = i % _cols;
            int _row = i / _cols;
            Rect _rect = new Rect(_col * FrameSize, _height - (_row + 1) * FrameSize, FrameSize, FrameSize);

            SpriteRect _sr = i < _rects.Count ? _rects[i] : new SpriteRect { name = _baseName + "_" + i, spriteID = GUID.Generate() };

            if (_sr.rect != _rect || SpriteAlignment.Center != _sr.alignment)
            {
                _sr.rect = _rect;
                _sr.alignment = SpriteAlignment.Center;
                _sr.pivot = new Vector2(0.5f, 0.5f);
                _bChanged = true;
            }

            _result[i] = _sr;
        }

        if (false == _bChanged) return false;

        _provider.SetSpriteRects(_result);

        ISpriteNameFileIdDataProvider _nameFileId = _provider.GetDataProvider<ISpriteNameFileIdDataProvider>();

        if (null != _nameFileId)
        {
            _nameFileId.SetNameFileIdPairs(_result.Select(x => new SpriteNameFileIdPair(x.name, x.spriteID)).ToList());
        }

        _provider.Apply();
        _importer.SaveAndReimport();

        Debug.Log("[MagicalSeedVisualBuilder] 32x32 격자로 다시 슬라이스: " + _path + " (" + _count + "프레임)");
        return true;
    }

    private static bool IsSame(List<LootGrowthVisualData.TierSprite> a, List<LootGrowthVisualData.TierSprite> b)
    {
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            LootGrowthVisualData.TierSprite x = a[i];
            LootGrowthVisualData.TierSprite y = b[i];

            if (x.lootType != y.lootType || x.tier != y.tier || x.sprite != y.sprite || x.isPlaceholder != y.isPlaceholder
                || x.fps != y.fps || x.actionInterval != y.actionInterval || x.displayArtCenter != y.displayArtCenter
                || x.displayBase != y.displayBase || x.spriteBase != y.spriteBase
                || false == SeqEqual(x.idleFrames, y.idleFrames) || false == SeqEqual(x.actionFrames, y.actionFrames)
                || false == SeqEqual(x.displayFrames, y.displayFrames))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SeqEqual<T>(T[] a, T[] b)
    {
        int _la = null != a ? a.Length : 0;
        int _lb = null != b ? b.Length : 0;

        if (_la != _lb) return false;

        for (int i = 0; i < _la; i++)
        {
            if (false == EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
        }

        return true;
    }
}

/// <summary>
/// Magical Seed 폴더의 PNG를 새로 넣거나 교체하면 알아서 맞춘다.
///  - 새 파일: 픽셀 아트 임포트 설정(PPU 32, Point, 무압축, Small은 Multiple)
///  - Small 시트: 32x32 격자로 슬라이스(프레임 수가 바뀌어도 그대로 따라감)
///  - LootGrowthVisualData / LootItemTypeDataBase: 스프라이트·프레임 다시 읽기(재생 규칙은 유지)
/// 그래서 임시 리소스(Stage04 Lava)는 같은 이름의 파일로 덮어쓰기만 하면 된다.
/// </summary>
public class MagicalSeedSheetPostprocessor : AssetPostprocessor
{
    private static bool bRefreshQueued;

    private void OnPreprocessTexture()
    {
        if (false == IsTarget(assetPath) || false == assetImporter.importSettingsMissing) return;

        TextureImporter _importer = (TextureImporter)assetImporter;
        _importer.textureType = TextureImporterType.Sprite;
        _importer.spriteImportMode = assetPath.StartsWith(MagicalSeedVisualBuilder.SmallFolder + "/") ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        _importer.spritePixelsPerUnit = MagicalSeedVisualBuilder.PixelsPerUnit;
        _importer.filterMode = FilterMode.Point;
        _importer.mipmapEnabled = false;
        _importer.alphaIsTransparency = true;
        _importer.textureCompression = TextureImporterCompression.Uncompressed;
    }

    private static void OnPostprocessAllAssets(string[] _imported, string[] _deleted, string[] _moved, string[] _movedFrom)
    {
        bool _bTouched = _imported.Any(IsTarget) || _deleted.Any(IsTarget) || _moved.Any(IsTarget) || _movedFrom.Any(IsTarget);

        if (false == _bTouched || true == bRefreshQueued) return;

        // 임포트 도중에 다른 에셋을 고치거나 재임포트하지 않도록 한 프레임 미룬다
        bRefreshQueued = true;
        EditorApplication.delayCall += () =>
        {
            bRefreshQueued = false;
            MagicalSeedVisualBuilder.Refresh();
        };
    }

    private static bool IsTarget(string _path)
    {
        return _path.StartsWith(MagicalSeedVisualBuilder.RootPath + "/") && _path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase);
    }
}
#endif
