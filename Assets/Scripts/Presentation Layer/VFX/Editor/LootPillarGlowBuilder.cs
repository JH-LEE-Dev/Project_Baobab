#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using PresentationLayer.Environment;
using PresentationLayer.VFX;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 마을 전리품 기둥 빛(VFX_LootPillarGlow) 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) 부드러운 빛 텍스처(쌍선형): 원반 4장(감쇠 지수 1.2 / 1.4 / 1.5 / 2.2), 빛 줄기 덮기·심 2장(피벗 = 기둥 윗면)
///  2) 후광 아틀라스 T_LootPillarHalo: LootGrowthVisualData의 모든 전시 프레임 실루엣을 가우시안(시그마 2.2px)으로 번지게 해 한 장에 굽고 조각으로 나눈다
///  3) 머티리얼 4개: 반투명 빛(SpriteGlowTint), 가산 빛(SpriteGlowAdditive), 빛 줄기 심(SpriteBeamFlow), 입자(BrandWrapGlow 0 ~ 2.5)
///  4) LootPillarGlowData: 전시 프레임마다 후광 조각·중심·경계
///  5) VFX_LootPillarGlow.prefab: Back(SortingGroup) 아래 부드러운 빛 7개 + 입자 메쉬, 반짝임 메쉬
///  6) LootPillar.prefab: 루트 아래 PillarGlow로 넣고 참조(LootDisplayObject.pillarGlow, VFX_LootEvolve.pillarGlow)를 채운 뒤
///     기존 중앙 글로우(ItemAuraOrbitController.useCenterGlow)를 끈다. 컴포넌트는 지우지 않는다.
/// </summary>
public static class LootPillarGlowBuilder
{
    private const string TextureFolder = "Assets/Graphics/VFX/LootPillar";
    private const string MaterialFolder = "Assets/Graphics/VFX/Materials";
    private const string DataPath = "Assets/Scriptable Obj/LootData/LootPillarGlowData.asset";
    private const string GrowthDataPath = "Assets/Scriptable Obj/LootData/LootGrowthVisualData.asset";
    private const string PrefabPath = "Assets/Prefabs/VFX/Item/VFX_LootPillarGlow.prefab";
    private const string PillarPrefabPath = "Assets/Prefabs/Objects/LootPillar/LootPillar.prefab";
    private const string HaloAtlasPath = TextureFolder + "/T_LootPillarHalo.png";
    private const string InstanceName = "PillarGlow";

    private const int DiscSize = 64;
    private const int BeamTextureWidth = 32;
    private const int BeamTextureHeight = 96;

    // 후광: 조각 둘레 여백(px), 블러 시그마·반경(px, 시뮬레이션과 같은 정규화 커널), 알파 지수
    private const int HaloPad = 8;
    private const float HaloSigma = 2.2f;
    private const int HaloRadius = 6;
    private const float HaloPower = 0.8f;
    private const int HaloColumns = 8;

    /// <summary>후광 아틀라스 굽기 설정(마을 기둥 = 전시 프레임, 상세 HUD = 확대 스프라이트가 같은 코드를 쓴다).</summary>
    internal struct HaloBakeSettings
    {
        public string atlasPath;
        public string dataPath;
        public string namePrefix;
        public int pad;
        public float sigma;
        public int radius;
        public int columns;
        public string label;
    }

    private static readonly HaloBakeSettings VillageHalo = new HaloBakeSettings
    {
        atlasPath = HaloAtlasPath, dataPath = DataPath, namePrefix = "LootPillarHalo_",
        pad = HaloPad, sigma = HaloSigma, radius = HaloRadius, columns = HaloColumns, label = "전시 프레임",
    };

    private static readonly string[] SoftNames = { "PoolTint", "PoolAdd", "BeamTint", "BeamCore", "CoreTint", "CoreAdd", "Halo" };

    [MenuItem("LumberBoy/VFX/Build Loot Pillar Glow")]
    public static void Build()
    {
        EnsureFolder(TextureFolder);

        Sprite discPool = BuildDisc("T_LootPillarDisc12", 1.2f);
        Sprite discPoolAdd = BuildDisc("T_LootPillarDisc14", 1.4f);
        Sprite discCore = BuildDisc("T_LootPillarDisc15", 1.5f);
        Sprite discCoreAdd = BuildDisc("T_LootPillarDisc22", 2.2f);
        Sprite beamTint = BuildBeam("T_LootPillarBeamTint", false);
        Sprite beamCore = BuildBeam("T_LootPillarBeamCore", true);
        if (null == discPool || null == discPoolAdd || null == discCore || null == discCoreAdd || null == beamTint || null == beamCore)
        {
            Debug.LogError("[LootPillarGlowBuilder] 텍스처 생성 실패");
            return;
        }

        Material tint = BuildMaterial("M_LootPillarTint", "Custom/VFX/SpriteGlowTint");
        Material additive = BuildMaterial("M_LootPillarAdd", "Custom/VFX/SpriteGlowAdditive");
        Material beamFlow = BuildMaterial("M_LootPillarBeamFlow", "Custom/VFX/SpriteBeamFlow");
        Material particles = BuildMaterial("M_LootPillarParticles", "Custom/VFX/BrandWrapGlow");
        if (null == tint || null == additive || null == beamFlow || null == particles) return;

        tint.SetFloat("_Glow", 1.0f);
        additive.SetFloat("_Glow", 1.0f);
        beamFlow.SetFloat("_Glow", 1.0f);
        beamFlow.SetFloat("_HeightPx", VFX_LootPillarGlow.BeamHeightPx);
        beamFlow.SetFloat("_BottomH", -VFX_LootPillarGlow.BeamBelowPx / VFX_LootPillarGlow.BeamHeightPx);
        particles.SetFloat("_GlowMin", 0.0f);
        particles.SetFloat("_GlowMax", VFX_LootPillarGlow.ParticleGlowMax);
        EditorUtility.SetDirty(tint);
        EditorUtility.SetDirty(additive);
        EditorUtility.SetDirty(beamFlow);
        EditorUtility.SetDirty(particles);

        LootPillarGlowData data = BuildData();
        if (null == data) return;

        AssetDatabase.SaveAssets();

        Sprite[] softSprites = { discPool, discPoolAdd, beamTint, beamCore, discCore, discCoreAdd, 0 < data.Count ? data.Get(0).halo : null };
        Material[] softMaterials = { tint, additive, tint, beamFlow, tint, additive, additive };

        GameObject prefab = BuildPrefab(softSprites, softMaterials, particles, data);
        if (null == prefab) return;

        WirePillar(prefab, data);
        AssetDatabase.SaveAssets();
        Debug.Log("[LootPillarGlowBuilder] 완료: " + PrefabPath + " / " + DataPath + " / " + PillarPrefabPath);
    }

    // ---------- 텍스처 ----------

    internal static float Smooth(float _e0, float _e1, float _x)
    {
        float t = Mathf.Clamp01((_x - _e0) / (_e1 - _e0));
        return t * t * (3.0f - 2.0f * t);
    }

    // 시뮬레이션 addGlow/tintGlow의 감쇠: (1 - 거리)^지수
    private static Sprite BuildDisc(string _name, float _power)
    {
        Color32[] pixels = new Color32[DiscSize * DiscSize];
        for (int y = 0; y < DiscSize; y++)
        {
            for (int x = 0; x < DiscSize; x++)
            {
                float nx = (x + 0.5f) / DiscSize * 2.0f - 1.0f;
                float ny = (y + 0.5f) / DiscSize * 2.0f - 1.0f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = 1.0f > d ? Mathf.Pow(1.0f - d, _power) : 0.0f;
                pixels[y * DiscSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255.0f));
            }
        }

        return WriteSprite(_name, DiscSize, DiscSize, pixels, new Vector2(0.5f, 0.5f), DiscSize);
    }

    // 시뮬레이션 drawBeam: h = 기둥 윗면 0 ~ 꼭대기 1, 위로 갈수록 폭이 (1 - 0.35h)로 좁아진다.
    // 덮기 = (1 - u²)^1.5 x vfade, 심 = (1 - |u|)^3 x vfade (흐르는 결은 셰이더가 곱한다). 피벗 = 기둥 윗면(h = 0).
    private static Sprite BuildBeam(string _name, bool _bCore)
    {
        float totalPx = VFX_LootPillarGlow.BeamHeightPx + VFX_LootPillarGlow.BeamBelowPx;
        float bottomH = -VFX_LootPillarGlow.BeamBelowPx / VFX_LootPillarGlow.BeamHeightPx;
        Color32[] pixels = new Color32[BeamTextureWidth * BeamTextureHeight];

        for (int y = 0; y < BeamTextureHeight; y++)
        {
            float h = Mathf.Lerp(bottomH, 1.0f, (y + 0.5f) / BeamTextureHeight);
            float vfade = Smooth(-0.05f, 0.08f, h) * Mathf.Pow(Mathf.Clamp01(1.0f - h), 1.6f);
            float width = 1.0f - 0.35f * Mathf.Clamp01(h);

            for (int x = 0; x < BeamTextureWidth; x++)
            {
                float u = ((x + 0.5f) / BeamTextureWidth * 2.0f - 1.0f) / width;
                float alpha = 0.0f;
                if (1.0f > Mathf.Abs(u))
                {
                    alpha = true == _bCore ? Mathf.Pow(1.0f - Mathf.Abs(u), 3.0f) * vfade : Mathf.Pow(1.0f - u * u, 1.5f) * vfade;
                }

                pixels[y * BeamTextureWidth + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255.0f));
            }
        }

        // 1유닛 = 텍스처 세로 전체가 되도록 PPU를 세로 크기로 둔다(이펙트가 크기를 px로 정한다)
        return WriteSprite(_name, BeamTextureWidth, BeamTextureHeight, pixels, new Vector2(0.5f, VFX_LootPillarGlow.BeamBelowPx / totalPx), BeamTextureHeight);
    }

    private static Sprite WriteSprite(string _name, int _width, int _height, Color32[] _pixels, Vector2 _pivot, float _pixelsPerUnit)
    {
        return WriteSprite(TextureFolder, _name, _width, _height, _pixels, _pivot, _pixelsPerUnit);
    }

    internal static Sprite WriteSprite(string _folder, string _name, int _width, int _height, Color32[] _pixels, Vector2 _pivot, float _pixelsPerUnit)
    {
        string path = _folder + "/" + _name + ".png";
        WritePng(path, _width, _height, _pixels);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (null == importer) return null;

        ApplyCommonImport(importer, _pixelsPerUnit);
        importer.spriteImportMode = SpriteImportMode.Single;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = _pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    internal static void WritePng(string _path, int _width, int _height, Color32[] _pixels)
    {
        Texture2D texture = new Texture2D(_width, _height, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(_pixels);
        texture.Apply(false);
        File.WriteAllBytes(_path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(_path, ImportAssetOptions.ForceUpdate);
    }

    internal static void ApplyCommonImport(TextureImporter _importer, float _pixelsPerUnit)
    {
        _importer.textureType = TextureImporterType.Sprite;
        _importer.spritePixelsPerUnit = _pixelsPerUnit;
        _importer.filterMode = FilterMode.Bilinear;
        _importer.wrapMode = TextureWrapMode.Clamp;
        _importer.mipmapEnabled = false;
        _importer.alphaIsTransparency = true;
        _importer.sRGBTexture = false;
        _importer.textureCompression = TextureImporterCompression.Uncompressed;
    }

    // ---------- 머티리얼 ----------

    internal static Material BuildMaterial(string _name, string _shaderName)
    {
        Shader shader = Shader.Find(_shaderName);
        if (null == shader)
        {
            Debug.LogError("[LootPillarGlowBuilder] 셰이더를 찾지 못했습니다: " + _shaderName);
            return null;
        }

        string path = MaterialFolder + "/" + _name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (null == material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        return material;
    }

    // ---------- 데이터(후광 아틀라스) ----------

    private struct FrameBake
    {
        public Sprite sprite;
        public float[] halo;      // (frame + 여백) 크기의 후광 알파
        public int cellWidth;
        public int cellHeight;
        public Vector2 pivot;     // 원본 프레임 피벗(px)
        public Vector2 centroid;
        public Vector4 bounds;
    }

    private static LootPillarGlowData BuildData()
    {
        LootGrowthVisualData growth = AssetDatabase.LoadAssetAtPath<LootGrowthVisualData>(GrowthDataPath);
        if (null == growth || null == growth.EditorTierSprites)
        {
            Debug.LogError("[LootPillarGlowBuilder] 성장 비주얼 데이터를 찾지 못했습니다: " + GrowthDataPath);
            return null;
        }

        List<Sprite> frames = new List<Sprite>(64);
        List<LootGrowthVisualData.TierSprite> tiers = growth.EditorTierSprites;
        for (int i = 0; i < tiers.Count; i++)
        {
            Sprite[] display = tiers[i].displayFrames;
            for (int f = 0; null != display && f < display.Length; f++)
            {
                if (null != display[f] && false == frames.Contains(display[f])) frames.Add(display[f]);
            }
        }

        return BakeHaloData(frames, VillageHalo);
    }

    /// <summary>스프라이트들의 실루엣 후광을 한 장의 아틀라스로 굽고 조각으로 나눠, 중심·경계와 함께 데이터 에셋에 담는다.</summary>
    internal static LootPillarGlowData BakeHaloData(List<Sprite> _frames, in HaloBakeSettings _settings)
    {
        List<Sprite> frames = _frames;
        Dictionary<string, Color32[]> sourceCache = new Dictionary<string, Color32[]>(16);
        Dictionary<string, int> strideCache = new Dictionary<string, int>(16);
        List<FrameBake> bakes = new List<FrameBake>(frames.Count);
        int cellWidth = 0;
        int cellHeight = 0;

        for (int i = 0; i < frames.Count; i++)
        {
            if (false == TryBakeFrame(frames[i], sourceCache, strideCache, _settings, out FrameBake bake)) continue;
            bakes.Add(bake);
            cellWidth = Mathf.Max(cellWidth, bake.cellWidth);
            cellHeight = Mathf.Max(cellHeight, bake.cellHeight);
        }

        if (0 == bakes.Count)
        {
            Debug.LogError("[LootPillarGlowBuilder] 구울 " + _settings.label + "이 없습니다.");
            return null;
        }

        // 아틀라스: 칸(cellWidth x cellHeight)을 설정의 열 수로 깐다. 위 행부터 채운다.
        int columns = _settings.columns;
        int rows = (bakes.Count + columns - 1) / columns;
        int atlasWidth = Mathf.NextPowerOfTwo(columns * cellWidth);
        int atlasHeight = Mathf.NextPowerOfTwo(rows * cellHeight);
        Color32[] atlas = new Color32[atlasWidth * atlasHeight];
        for (int i = 0; i < atlas.Length; i++) atlas[i] = new Color32(255, 255, 255, 0);

        Rect[] rects = new Rect[bakes.Count];
        for (int i = 0; i < bakes.Count; i++)
        {
            FrameBake bake = bakes[i];
            int ox = (i % columns) * cellWidth;
            int oy = atlasHeight - (i / columns + 1) * cellHeight;
            rects[i] = new Rect(ox, oy, bake.cellWidth, bake.cellHeight);

            for (int y = 0; y < bake.cellHeight; y++)
            {
                for (int x = 0; x < bake.cellWidth; x++)
                {
                    float v = bake.halo[y * bake.cellWidth + x];
                    float alpha = 0.01f < v ? Mathf.Pow(Mathf.Clamp01(v), HaloPower) : 0.0f;
                    atlas[(oy + y) * atlasWidth + ox + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255.0f));
                }
            }
        }

        WritePng(_settings.atlasPath, atlasWidth, atlasHeight, atlas);
        if (false == SliceHaloAtlas(bakes, rects, _settings)) return null;

        Dictionary<string, Sprite> haloSprites = new Dictionary<string, Sprite>(bakes.Count);
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(_settings.atlasPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite) haloSprites[sprite.name] = sprite;
        }

        LootPillarGlowData.Entry[] entries = new LootPillarGlowData.Entry[bakes.Count];
        for (int i = 0; i < bakes.Count; i++)
        {
            haloSprites.TryGetValue(HaloName(_settings, i), out Sprite halo);
            entries[i].sprite = bakes[i].sprite;
            entries[i].halo = halo;
            entries[i].centroid = bakes[i].centroid;
            entries[i].bounds = bakes[i].bounds;
            if (null == halo) Debug.LogWarning("[LootPillarGlowBuilder] 후광 조각을 찾지 못했습니다: " + HaloName(_settings, i));
        }

        LootPillarGlowData data = AssetDatabase.LoadAssetAtPath<LootPillarGlowData>(_settings.dataPath);
        if (null == data)
        {
            data = ScriptableObject.CreateInstance<LootPillarGlowData>();
            AssetDatabase.CreateAsset(data, _settings.dataPath);
        }

        data.SetEditorEntries(entries);
        EditorUtility.SetDirty(data);
        Debug.Log("[LootPillarGlowBuilder] " + _settings.label + " " + entries.Length + "개 구움 (아틀라스 " + atlasWidth + "x" + atlasHeight + ")");
        return data;
    }

    private static string HaloName(in HaloBakeSettings _settings, int _index)
    {
        return _settings.namePrefix + _index;
    }

    // 원본 시트 파일을 직접 읽어(임포트 설정의 읽기 허용과 무관하게) 프레임 실루엣·중심·경계·후광을 만든다
    private static bool TryBakeFrame(Sprite _sprite, Dictionary<string, Color32[]> _sourceCache, Dictionary<string, int> _strideCache, in HaloBakeSettings _settings, out FrameBake _bake)
    {
        _bake = default;
        string path = AssetDatabase.GetAssetPath(_sprite.texture);
        if (string.IsNullOrEmpty(path) || false == File.Exists(path)) return false;

        if (false == _sourceCache.TryGetValue(path, out Color32[] all))
        {
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (false == source.LoadImage(File.ReadAllBytes(path)))
            {
                UnityEngine.Object.DestroyImmediate(source);
                return false;
            }

            all = source.GetPixels32();
            _sourceCache[path] = all;
            _strideCache[path] = source.width;
            UnityEngine.Object.DestroyImmediate(source);
        }

        int stride = _strideCache[path];
        Rect rect = _sprite.rect;
        int rx = Mathf.RoundToInt(rect.x);
        int ry = Mathf.RoundToInt(rect.y);
        int w = Mathf.RoundToInt(rect.width);
        int h = Mathf.RoundToInt(rect.height);
        int pivotX = Mathf.RoundToInt(_sprite.pivot.x);
        int pivotY = Mathf.RoundToInt(_sprite.pivot.y);

        int pad = _settings.pad;
        int cw = w + pad * 2;
        int ch = h + pad * 2;
        float[] mask = new float[cw * ch];
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        double sumX = 0.0;
        double sumY = 0.0;
        int count = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (128 > all[(ry + y) * stride + rx + x].a) continue;   // 시뮬레이션과 같은 알파 0.5 기준

                mask[(y + pad) * cw + x + pad] = 1.0f;
                int ox = x - pivotX;
                int oy = y - pivotY;
                if (ox < minX) minX = ox;
                if (oy < minY) minY = oy;
                if (ox > maxX) maxX = ox;
                if (oy > maxY) maxY = oy;
                sumX += x + 0.5;
                sumY += y + 0.5;
                count++;
            }
        }

        if (0 == count) return false;

        _bake.sprite = _sprite;
        _bake.halo = Blur(Blur(mask, cw, ch, true, _settings), cw, ch, false, _settings);
        _bake.cellWidth = cw;
        _bake.cellHeight = ch;
        _bake.pivot = new Vector2(pivotX, pivotY);
        _bake.centroid = new Vector2((float)(sumX / count) - pivotX, (float)(sumY / count) - pivotY);
        _bake.bounds = new Vector4(minX, minY, maxX, maxY);
        return true;
    }

    // 시뮬레이션과 같은 정규화 가우시안(반경·시그마는 설정값). 바깥은 0으로 본다.
    private static float[] Blur(float[] _source, int _width, int _height, bool _bHorizontal, in HaloBakeSettings _settings)
    {
        int radius = _settings.radius;
        float sigma = _settings.sigma;
        float[] kernel = new float[radius * 2 + 1];
        float sum = 0.0f;
        for (int i = -radius; i <= radius; i++)
        {
            kernel[i + radius] = Mathf.Exp(-(i * i) / (2.0f * sigma * sigma));
            sum += kernel[i + radius];
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        float[] result = new float[_source.Length];
        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                float v = 0.0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int sx = true == _bHorizontal ? x + k : x;
                    int sy = true == _bHorizontal ? y : y + k;
                    if (0 > sx || 0 > sy || _width <= sx || _height <= sy) continue;
                    v += _source[sy * _width + sx] * kernel[k + radius];
                }
                result[y * _width + x] = v;
            }
        }

        return result;
    }

    // 아틀라스를 칸마다 조각으로 나눈다. 피벗은 원본 프레임 피벗 + 여백, PPU는 원본 프레임과 같다.
    // 이름이 같은 조각은 ID를 유지해 다시 구워도 참조가 끊기지 않는다.
    private static bool SliceHaloAtlas(List<FrameBake> _bakes, Rect[] _rects, in HaloBakeSettings _settings)
    {
        TextureImporter importer = AssetImporter.GetAtPath(_settings.atlasPath) as TextureImporter;
        if (null == importer) return false;

        ApplyCommonImport(importer, _bakes[0].sprite.pixelsPerUnit);
        importer.spriteImportMode = SpriteImportMode.Multiple;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        SpriteDataProviderFactories factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        SpriteRect[] existing = provider.GetSpriteRects();
        Dictionary<string, SpriteRect> byName = new Dictionary<string, SpriteRect>(existing.Length);
        for (int i = 0; i < existing.Length; i++) byName[existing[i].name] = existing[i];

        SpriteRect[] result = new SpriteRect[_bakes.Count];
        List<SpriteNameFileIdPair> pairs = new List<SpriteNameFileIdPair>(_bakes.Count);
        for (int i = 0; i < _bakes.Count; i++)
        {
            string name = HaloName(_settings, i);
            if (false == byName.TryGetValue(name, out SpriteRect sr)) sr = new SpriteRect { name = name, spriteID = GUID.Generate() };

            Rect rect = _rects[i];
            sr.rect = rect;
            sr.alignment = SpriteAlignment.Custom;
            sr.pivot = new Vector2((_bakes[i].pivot.x + _settings.pad) / rect.width, (_bakes[i].pivot.y + _settings.pad) / rect.height);
            sr.border = Vector4.zero;
            result[i] = sr;
            pairs.Add(new SpriteNameFileIdPair(sr.name, sr.spriteID));
        }

        provider.SetSpriteRects(result);
        ISpriteNameFileIdDataProvider nameFileId = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (null != nameFileId) nameFileId.SetNameFileIdPairs(pairs);
        provider.Apply();
        importer.SaveAndReimport();
        return true;
    }

    // ---------- 프리팹 ----------

    private static GameObject BuildPrefab(Sprite[] _softSprites, Material[] _softMaterials, Material _particles, LootPillarGlowData _data)
    {
        GameObject root = new GameObject("VFX_LootPillarGlow");

        try
        {
            VFX_LootPillarGlow effect = root.AddComponent<VFX_LootPillarGlow>();

            GameObject back = new GameObject("Back");
            back.transform.SetParent(root.transform, false);
            SortingGroup group = back.AddComponent<SortingGroup>();

            SpriteRenderer[] softRenderers = new SpriteRenderer[VFX_LootPillarGlow.SoftCount];
            for (int i = 0; i < VFX_LootPillarGlow.SoftCount; i++)
            {
                GameObject child = new GameObject(SoftNames[i]);
                child.transform.SetParent(back.transform, false);
                SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
                sr.sprite = _softSprites[i];
                sr.sharedMaterial = _softMaterials[i];
                sr.sortingOrder = i;
                sr.enabled = false;
                softRenderers[i] = sr;
            }

            MeshRenderer moteRenderer = CreateMeshChild(back.transform, "Motes", _particles);
            moteRenderer.sortingOrder = VFX_LootPillarGlow.MoteOrderInGroup;
            MeshRenderer twinkleRenderer = CreateMeshChild(root.transform, "Twinkle", _particles);

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("data").objectReferenceValue = _data;
            so.FindProperty("backGroup").objectReferenceValue = group;
            SerializedProperty softProp = so.FindProperty("softRenderers");
            softProp.arraySize = softRenderers.Length;
            for (int i = 0; i < softRenderers.Length; i++) softProp.GetArrayElementAtIndex(i).objectReferenceValue = softRenderers[i];
            so.FindProperty("moteRenderer").objectReferenceValue = moteRenderer;
            so.FindProperty("twinkleRenderer").objectReferenceValue = twinkleRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static MeshRenderer CreateMeshChild(Transform _parent, string _name, Material _material)
    {
        GameObject child = new GameObject(_name);
        child.transform.SetParent(_parent, false);
        child.AddComponent<MeshFilter>();
        MeshRenderer renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = _material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;
        return renderer;
    }

    // ---------- LootPillar 연결 ----------

    private static void WirePillar(GameObject _effectPrefab, LootPillarGlowData _data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PillarPrefabPath);

        try
        {
            LootDisplayObject display = root.GetComponent<LootDisplayObject>();
            if (null == display)
            {
                Debug.LogError("[LootPillarGlowBuilder] LootDisplayObject를 찾지 못했습니다.");
                return;
            }

            SerializedObject displaySo = new SerializedObject(display);
            SpriteRenderer item = displaySo.FindProperty("targetRenderer").objectReferenceValue as SpriteRenderer;
            SpriteRenderer pillar = displaySo.FindProperty("pillarRenderer").objectReferenceValue as SpriteRenderer;
            SpriteRenderer lootShadow = displaySo.FindProperty("lootShadowRenderer").objectReferenceValue as SpriteRenderer;
            if (null == item || null == pillar || null == lootShadow)
            {
                Debug.LogError("[LootPillarGlowBuilder] 전리품·기둥·전리품 그림자 렌더러 참조가 비어 있습니다.");
                return;
            }

            // 이전 실행에서 넣은 인스턴스는 지우고 새로 넣는다
            Transform old = root.transform.Find(InstanceName);
            if (null != old) UnityEngine.Object.DestroyImmediate(old.gameObject);

            GameObject instance = PrefabUtility.InstantiatePrefab(_effectPrefab, root.transform) as GameObject;
            instance.name = InstanceName;
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            VFX_LootPillarGlow effect = instance.GetComponent<VFX_LootPillarGlow>();
            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("data").objectReferenceValue = _data;
            so.FindProperty("itemRenderer").objectReferenceValue = item;
            so.FindProperty("pillarRenderer").objectReferenceValue = pillar;
            so.FindProperty("poolAnchor").objectReferenceValue = lootShadow.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            displaySo.FindProperty("pillarGlow").objectReferenceValue = effect;
            displaySo.ApplyModifiedPropertiesWithoutUndo();

            // 기존 '등'(중앙 글로우)을 끈다. 위성이 0개라 이것만 끄면 아우라 컨트롤러는 아무것도 그리지 않는다.
            UnityEngine.Object aura = displaySo.FindProperty("auraOrbitController").objectReferenceValue;
            if (null != aura)
            {
                SerializedObject auraSo = new SerializedObject(aura);
                auraSo.FindProperty("useCenterGlow").boolValue = false;
                auraSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // 진화 연출이 원반·후광도 같이 조였다가 터뜨리게 한다
            UnityEngine.Object evolve = displaySo.FindProperty("evolveEffect").objectReferenceValue;
            if (null != evolve)
            {
                SerializedObject evolveSo = new SerializedObject(evolve);
                evolveSo.FindProperty("pillarGlow").objectReferenceValue = effect;
                evolveSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, PillarPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    internal static void EnsureFolder(string _path)
    {
        if (true == AssetDatabase.IsValidFolder(_path)) return;

        string parent = Path.GetDirectoryName(_path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(_path));
    }
}
#endif
