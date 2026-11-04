#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 희귀 전리품 등장 이펙트(VFX_RareLootAppear) 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) 부드러운 빛 텍스처 4장(64x64, 쌍선형): 오라 바깥, 오라 심, 빛기둥(가우시안 + 위로 사라짐, 아래 중심 피벗), 렌즈 줄
///  2) 머티리얼 4개: 반투명 빛(SpriteGlowTint), 가산 빛(SpriteGlowAdditive), 가산 픽셀(PixelQuadAdditive), 입자(BrandWrapGlow 1.3/4.2)
///  3) RareLootVfxData: LootItemTypeDataBase에 등록된 전리품 스프라이트마다 실루엣·경계와 실루엣 블러 둘레 빛 텍스처(T_RareLootRim_*)를 구워 둔다(색은 이펙트 인스펙터의 황금빛 공통값)
///  4) VFX_RareLootAppear.prefab: 부드러운 빛 스프라이트 36개(둘레 빛·이음부 빛·아우라 심 포함) + 픽셀 메쉬 3개(PixelAddFront, PixelGlow, PixelTrailLine)
///  5) LootItem.prefab: VFX(vfxRoot) 아래에 이펙트를 넣고 참조를 채운 뒤, 기존 VFX_Beam/VFX_Orbit을 끄고 LootItem의 참조를 비운다(코드는 null을 처리한다).
///     아웃라인·스텐실 머티리얼을 본체(NoSnap)와 짝인 Outline_LogItem_NoSnap / OutlineStencilWriter_LogItem_NoSnap으로 바꾼다
/// </summary>
public static class RareLootAppearBuilder
{
    private const string TextureFolder = "Assets/Graphics/VFX/RareLoot";
    private const string MaterialFolder = "Assets/Graphics/VFX/Materials";
    private const string DataPath = "Assets/Scriptable Obj/LootData/RareLootVfxData.asset";
    private const string LootTypeDataPath = "Assets/Scriptable Obj/LootData/LootItemTypeDataBase.asset";
    private const string PrefabPath = "Assets/Prefabs/VFX/Item/VFX_RareLootAppear.prefab";
    private const string LootItemPrefabPath = "Assets/Prefabs/Objects/Item/LootItem.prefab";
    private const string OutlineNoSnapPath = "Assets/Materials/Outline/Outline_LogItem_NoSnap.mat";
    private const string OutlineStencilNoSnapPath = "Assets/Materials/Outline/OutlineStencilWriter_LogItem_NoSnap.mat";

    private const string GlowSoftName = "T_RareLootGlowSoft";
    private const string GlowCoreName = "T_RareLootGlowCore";
    private const string BeamName = "T_RareLootBeam";
    private const string StreakName = "T_RareLootStreak";

    private const int TextureSize = 64;
    // 빛기둥: 위쪽 기둥은 +-2.6 시그마, 밑동은 BeamFlare배까지 넓어지는 나팔 모양. 텍스처 가로는 밑동 기준(+-2.6 x BeamFlare 시그마)이라
    // VFX_RareLootAppear.BeamTextureSpan(= BeamSpan x BeamFlare)과 같아야 한다. 밑동은 아이템에 붙으므로 아래 끝을 흐리게 하지 않는다.
    private const float BeamSpan = 2.6f;
    private const float BeamFlare = 1.8f;
    private const float BeamFlareHeight = 0.3f;   // 밑동부터 이 높이 비율까지 좁아진다
    // 빛기둥 아래 끝은 아이템 중심에서 수평으로 잘리지 않도록, 그 아래로 둥근 밑동(BeamFootDepthPx)을 붙여 폭·밝기가 함께 줄며 사라지게 한다.
    // 텍스처 피벗은 밑동 위(= 아이템 중심 자리). VFX_RareLootAppear.BeamFootDepth / BeamHeight와 같은 값이어야 한다.
    private const float BeamFootDepthPx = 14.0f;
    private const float BeamHeightPx = 150.0f * 1.3f;
    private const float BeamFootFrac = BeamFootDepthPx / (BeamHeightPx + BeamFootDepthPx);

    // 둘레 빛 글로우: 실루엣 둘레 여백(px), 블러 시그마(px), 번진 알파에 곱하는 배율
    private const int RimGlowPad = 16;
    private const float RimGlowSigma = 1.8f;
    private const float RimGlowGain = 1.7f;

    private const float SoftGlow = 1.1f;
    private const float AdditiveGlow = 2.0f;

    private static readonly string[] SoftNames =
    {
        "PoolOuter", "PoolCore", "BeamOuter", "BeamCore", "AuraOuter", "AuraCore", "HoldAura",
        "StreakH", "StreakV", "LandStreak", "Glint0", "Glint1", "Glint2", "Glint3",
        "TrailGlow0", "TrailGlow1", "TrailGlow2", "TrailGlow3", "TrailGlow4", "TrailGlow5", "TrailGlow6", "TrailGlow7",
        "ImpactFlash",
        "StarH0", "StarH1", "StarH2", "StarH3", "StarH4", "StarV0", "StarV1", "StarV2", "StarV3", "StarV4",
        "RimGlow", "BeamJoint", "ItemCore",
    };

    // 슬롯별 (텍스처, 가산 여부). RimGlow의 스프라이트는 이펙트가 재생 시작 때 아이템별 둘레 빛으로 바꾼다.
    private static readonly int[] SoftTexture = { 0, 1, 2, 2, 0, 1, 0, 3, 3, 3, 3, 3, 3, 3, 0, 0, 0, 0, 0, 0, 0, 0, 1, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 0, 1, 1 };
    private static readonly bool[] SoftAdditive = { false, true, false, true, false, true, false, true, true, true, true, true, true, true, false, false, false, false, false, false, false, false, true, true, true, true, true, true, true, true, true, true, true, false, true, true };

    [MenuItem("LumberBoy/VFX/Build Rare Loot Appear")]
    public static void Build()
    {
        EnsureFolder(TextureFolder);

        Sprite[] textures =
        {
            BuildTexture(GlowSoftName, 0, SpriteAlignment.Center, Vector2.zero),
            BuildTexture(GlowCoreName, 1, SpriteAlignment.Center, Vector2.zero),
            BuildTexture(BeamName, 2, SpriteAlignment.Custom, new Vector2(0.5f, BeamFootFrac)),
            BuildTexture(StreakName, 3, SpriteAlignment.Center, Vector2.zero),
        };

        for (int i = 0; i < textures.Length; i++)
        {
            if (null == textures[i])
            {
                Debug.LogError("[RareLootAppearBuilder] 텍스처 생성 실패");
                return;
            }
        }

        Material soft = BuildMaterial("M_RareLootSoft", "Custom/VFX/SpriteGlowTint", SoftGlow, -1.0f, -1.0f);
        Material additive = BuildMaterial("M_RareLootAdd", "Custom/VFX/SpriteGlowAdditive", AdditiveGlow, -1.0f, -1.0f);
        Material addPixel = BuildMaterial("M_RareLootAddPixel", "Custom/VFX/PixelQuadAdditive", -1.0f, -1.0f, -1.0f);
        Material particles = BuildMaterial("M_RareLootParticles", "Custom/VFX/BrandWrapGlow", -1.0f, 1.3f, 4.2f);
        if (null == soft || null == additive || null == addPixel || null == particles) return;

        RareLootVfxData data = BuildData();
        if (null == data) return;

        AssetDatabase.SaveAssets();

        GameObject prefab = BuildPrefab(textures, soft, additive, addPixel, particles, data);
        if (null == prefab) return;

        WireLootItem(prefab, data);
        AssetDatabase.SaveAssets();
        Debug.Log("[RareLootAppearBuilder] 완료: " + PrefabPath + " / " + DataPath + " / " + LootItemPrefabPath);
    }

    // ---------- 텍스처 ----------

    // _pivot은 _alignment가 Custom일 때만 쓴다
    private static Sprite BuildTexture(string _name, int _kind, SpriteAlignment _alignment, Vector2 _pivot)
    {
        string path = TextureFolder + "/" + _name + ".png";
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false, true);
        Color32[] pixels = new Color32[TextureSize * TextureSize];

        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float nx = (x + 0.5f) / TextureSize * 2.0f - 1.0f;
                float ny = (y + 0.5f) / TextureSize * 2.0f - 1.0f;
                float v = (y + 0.5f) / TextureSize;
                float alpha;

                switch (_kind)
                {
                    case 0:
                        alpha = Mathf.Pow(Mathf.Max(0.0f, 1.0f - Mathf.Sqrt(nx * nx + ny * ny)), 1.3f);
                        break;
                    case 1:
                        float d = Mathf.Max(0.0f, 1.0f - Mathf.Sqrt(nx * nx + ny * ny));
                        alpha = d * d;
                        break;
                    case 2:
                        if (BeamFootFrac > v)
                        {
                            // 둥근 밑동: 피벗에서 아래로 갈수록 폭이 타원처럼 줄고 밝기도 부드럽게 0이 된다
                            float f = v / BeamFootFrac;                       // 0 = 맨 아래, 1 = 피벗
                            float round = Mathf.Sqrt(Mathf.Max(0.0001f, 1.0f - (1.0f - f) * (1.0f - f)));
                            float uf = nx * BeamSpan / round;
                            alpha = Mathf.Exp(-uf * uf) * f * f * (3.0f - 2.0f * f);
                        }
                        else
                        {
                            float vb = (v - BeamFootFrac) / (1.0f - BeamFootFrac); // 0 = 피벗, 1 = 꼭대기
                            float s = Mathf.Clamp01(vb / BeamFlareHeight);
                            float widen = 1.0f + (BeamFlare - 1.0f) * (1.0f - s * s * (3.0f - 2.0f * s));
                            float u = nx * BeamSpan * BeamFlare / widen;
                            alpha = Mathf.Exp(-u * u) * Mathf.Pow(Mathf.Max(0.0f, 1.0f - vb), 1.4f);
                        }
                        break;
                    default:
                        float a = Mathf.Max(0.0f, 1.0f - Mathf.Abs(nx));
                        float b = Mathf.Max(0.0f, 1.0f - Mathf.Abs(ny));
                        alpha = a * a * b;
                        break;
                }

                pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255.0f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (null == importer) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = TextureSize;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)_alignment;
        if (SpriteAlignment.Custom == _alignment) settings.spritePivot = _pivot;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // ---------- 머티리얼 ----------

    private static Material BuildMaterial(string _name, string _shaderName, float _glow, float _glowMin, float _glowMax)
    {
        Shader shader = Shader.Find(_shaderName);
        if (null == shader)
        {
            Debug.LogError("[RareLootAppearBuilder] 셰이더를 찾지 못했습니다: " + _shaderName);
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

        if (0.0f <= _glow) material.SetFloat("_Glow", _glow);
        if (0.0f <= _glowMin) material.SetFloat("_GlowMin", _glowMin);
        if (0.0f <= _glowMax) material.SetFloat("_GlowMax", _glowMax);
        EditorUtility.SetDirty(material);
        return material;
    }

    // ---------- 데이터 ----------

    private static RareLootVfxData BuildData()
    {
        ScriptableObject typeData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(LootTypeDataPath);
        if (null == typeData)
        {
            Debug.LogError("[RareLootAppearBuilder] 전리품 종류 데이터를 찾지 못했습니다: " + LootTypeDataPath);
            return null;
        }

        List<Sprite> sprites = new List<Sprite>(8);
        SerializedObject so = new SerializedObject(typeData);
        SerializedProperty list = so.FindProperty("datas");
        for (int i = 0; null != list && i < list.arraySize; i++)
        {
            Sprite sprite = list.GetArrayElementAtIndex(i).FindPropertyRelative("sprite").objectReferenceValue as Sprite;
            if (null != sprite && false == sprites.Contains(sprite)) sprites.Add(sprite);
        }

        List<RareLootVfxData.Entry> entries = new List<RareLootVfxData.Entry>(sprites.Count);
        for (int i = 0; i < sprites.Count; i++)
        {
            if (true == TryBakeEntry(sprites[i], out RareLootVfxData.Entry entry)) entries.Add(entry);
        }

        RareLootVfxData data = AssetDatabase.LoadAssetAtPath<RareLootVfxData>(DataPath);
        if (null == data)
        {
            data = ScriptableObject.CreateInstance<RareLootVfxData>();
            AssetDatabase.CreateAsset(data, DataPath);
        }

        data.SetEditorEntries(entries.ToArray());
        EditorUtility.SetDirty(data);
        Debug.Log("[RareLootAppearBuilder] 스프라이트 " + entries.Count + "개 구움");
        return data;
    }

    // 텍스처 원본 파일을 직접 읽어(임포트 설정의 읽기 허용과 무관하게) 스프라이트 영역의 실루엣·경계·둘레 빛을 만든다
    private static bool TryBakeEntry(Sprite _sprite, out RareLootVfxData.Entry _entry)
    {
        _entry = default;
        string path = AssetDatabase.GetAssetPath(_sprite.texture);
        if (string.IsNullOrEmpty(path) || false == File.Exists(path)) return false;

        Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (false == source.LoadImage(File.ReadAllBytes(path)))
        {
            UnityEngine.Object.DestroyImmediate(source);
            return false;
        }

        Rect rect = _sprite.rect;
        int rx = Mathf.RoundToInt(rect.x);
        int ry = Mathf.RoundToInt(rect.y);
        int w = Mathf.RoundToInt(rect.width);
        int h = Mathf.RoundToInt(rect.height);
        int pivotX = Mathf.RoundToInt(_sprite.pivot.x);
        int pivotY = Mathf.RoundToInt(_sprite.pivot.y);

        Color32[] all = source.GetPixels32();
        int stride = source.width;
        UnityEngine.Object.DestroyImmediate(source);

        bool[] solid = new bool[w * h];
        List<Vector2Int> silhouette = new List<Vector2Int>(w * h);
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color32 c = all[(ry + y) * stride + rx + x];
                if (0 == c.a) continue;

                solid[y * w + x] = true;
                int ox = x - pivotX;
                int oy = y - pivotY;
                silhouette.Add(new Vector2Int(ox, oy));
                if (ox < minX) minX = ox;
                if (oy < minY) minY = oy;
                if (ox > maxX) maxX = ox;
                if (oy > maxY) maxY = oy;
            }
        }

        if (0 == silhouette.Count) return false;

        _entry.sprite = _sprite;
        _entry.silhouette = silhouette.ToArray();
        _entry.rimGlow = BuildRimGlow(_sprite, solid, w, h);
        _entry.bounds = new Vector4(minX, minY, maxX, maxY);
        return null != _entry.rimGlow;
    }

    // 둘레 빛: 실루엣 마스크를 가우시안으로 두 번 번지게 한 글로우 텍스처를 구워 스프라이트로 만든다.
    // 계단 없이 아이템 모양을 따라 부드럽게 번지고, 피벗을 원본 스프라이트 피벗에 맞춰 같은 자리에 겹친다.
    private static Sprite BuildRimGlow(Sprite _sprite, bool[] _solid, int _w, int _h)
    {
        int size = Mathf.NextPowerOfTwo(Mathf.Max(_w, _h) + RimGlowPad * 2);
        int padX = (size - _w) / 2;
        int padY = (size - _h) / 2;

        float[] mask = new float[size * size];
        for (int y = 0; y < _h; y++)
        {
            for (int x = 0; x < _w; x++)
            {
                if (true == _solid[y * _w + x]) mask[(y + padY) * size + x + padX] = 1.0f;
            }
        }

        float[] blurred = GaussianBlur(GaussianBlur(mask, size, RimGlowSigma), size, RimGlowSigma);

        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            float alpha = Mathf.Clamp01(blurred[i] * RimGlowGain);
            pixels[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255.0f));
        }

        string path = TextureFolder + "/T_RareLootRim_" + _sprite.name + ".png";
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.SetPixels32(pixels);
        texture.Apply(false);
        File.WriteAllBytes(path, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (null == importer) return null;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = size;   // 1유닛 = 텍스처 전체(이펙트가 크기를 px로 지정한다)
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = new Vector2((_sprite.pivot.x + padX) / size, (_sprite.pivot.y + padY) / size);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 분리형 가우시안 블러(가로 -> 세로). 반경은 3시그마.
    private static float[] GaussianBlur(float[] _source, int _size, float _sigma)
    {
        int radius = Mathf.CeilToInt(_sigma * 3.0f);
        float[] kernel = new float[radius * 2 + 1];
        float sum = 0.0f;
        for (int i = -radius; i <= radius; i++)
        {
            kernel[i + radius] = Mathf.Exp(-(i * i) / (2.0f * _sigma * _sigma));
            sum += kernel[i + radius];
        }
        for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;

        float[] temp = new float[_source.Length];
        float[] result = new float[_source.Length];

        for (int y = 0; y < _size; y++)
        {
            for (int x = 0; x < _size; x++)
            {
                float v = 0.0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int sx = Mathf.Clamp(x + k, 0, _size - 1);
                    v += _source[y * _size + sx] * kernel[k + radius];
                }
                temp[y * _size + x] = v;
            }
        }

        for (int y = 0; y < _size; y++)
        {
            for (int x = 0; x < _size; x++)
            {
                float v = 0.0f;
                for (int k = -radius; k <= radius; k++)
                {
                    int sy = Mathf.Clamp(y + k, 0, _size - 1);
                    v += temp[sy * _size + x] * kernel[k + radius];
                }
                result[y * _size + x] = v;
            }
        }

        return result;
    }

    // ---------- 프리팹 ----------

    private static GameObject BuildPrefab(Sprite[] _textures, Material _soft, Material _additive, Material _addPixel, Material _particles, RareLootVfxData _data)
    {
        GameObject root = new GameObject("VFX_RareLootAppear");

        try
        {
            VFX_RareLootAppear effect = root.AddComponent<VFX_RareLootAppear>();

            SpriteRenderer[] softRenderers = new SpriteRenderer[SoftNames.Length];
            for (int i = 0; i < SoftNames.Length; i++)
            {
                GameObject child = new GameObject(SoftNames[i]);
                child.transform.SetParent(root.transform, false);
                SpriteRenderer sr = child.AddComponent<SpriteRenderer>();
                sr.sprite = _textures[SoftTexture[i]];
                sr.sharedMaterial = true == SoftAdditive[i] ? _additive : _soft;
                sr.enabled = false;
                softRenderers[i] = sr;
            }

            MeshRenderer addFrontRenderer = CreateMeshChild(root.transform, "PixelAddFront", _addPixel);
            MeshRenderer glowRenderer = CreateMeshChild(root.transform, "PixelGlow", _particles);
            MeshRenderer trailLineRenderer = CreateMeshChild(root.transform, "PixelTrailLine", _addPixel);

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("data").objectReferenceValue = _data;
            SerializedProperty softProp = so.FindProperty("softRenderers");
            softProp.arraySize = softRenderers.Length;
            for (int i = 0; i < softRenderers.Length; i++) softProp.GetArrayElementAtIndex(i).objectReferenceValue = softRenderers[i];
            so.FindProperty("addFrontRenderer").objectReferenceValue = addFrontRenderer;
            so.FindProperty("glowRenderer").objectReferenceValue = glowRenderer;
            so.FindProperty("trailLineRenderer").objectReferenceValue = trailLineRenderer;
            so.FindProperty("glowScale").floatValue = 1.0f;
            so.FindProperty("additiveGlow").floatValue = AdditiveGlow;
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
        renderer.enabled = false;
        return renderer;
    }

    // ---------- LootItem 연결 ----------

    private static void WireLootItem(GameObject _effectPrefab, RareLootVfxData _data)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(LootItemPrefabPath);

        try
        {
            LootItem lootItem = root.GetComponent<LootItem>();
            if (null == lootItem)
            {
                Debug.LogError("[RareLootAppearBuilder] LootItem 컴포넌트를 찾지 못했습니다.");
                return;
            }

            SerializedObject lootSo = new SerializedObject(lootItem);
            GameObject vfxRoot = lootSo.FindProperty("vfxRoot").objectReferenceValue as GameObject;
            SpriteRenderer outline = lootSo.FindProperty("outlineSR").objectReferenceValue as SpriteRenderer;
            Transform animator = root.transform.Find("Animator");
            SpriteRenderer item = null != animator ? animator.GetComponent<SpriteRenderer>() : null;

            if (null == vfxRoot || null == item)
            {
                Debug.LogError("[RareLootAppearBuilder] LootItem의 VFX 루트 또는 본체 스프라이트를 찾지 못했습니다.");
                return;
            }

            // 기존 등장 연출 끄기: 오브젝트를 끄고 LootItem 참조를 비운다(LootItem은 null이면 건너뛴다)
            UnityEngine.Object beam = lootSo.FindProperty("vfxBeam").objectReferenceValue;
            UnityEngine.Object orbit = lootSo.FindProperty("vfxOrbit").objectReferenceValue;
            if (beam is Component beamComponent) beamComponent.gameObject.SetActive(false);
            if (orbit is Component orbitComponent) orbitComponent.gameObject.SetActive(false);
            lootSo.FindProperty("vfxBeam").objectReferenceValue = null;
            lootSo.FindProperty("vfxOrbit").objectReferenceValue = null;
            lootSo.ApplyModifiedPropertiesWithoutUndo();

            // 아웃라인: 본체가 스냅 없는 Custom-Sprite-Default-NoSnap이므로 아웃라인·스텐실도 스냅 없는 짝으로 맞춘다.
            // 스냅하는 Outline_LogItem은 아웃라인을 월드 픽셀 격자에 붙여, 소수 위치로 움직이는 본체와 0~1px 어긋나 떨린다.
            Material outlineNoSnap = AssetDatabase.LoadAssetAtPath<Material>(OutlineNoSnapPath);
            Material stencilNoSnap = AssetDatabase.LoadAssetAtPath<Material>(OutlineStencilNoSnapPath);
            SpriteRenderer stencil = lootSo.FindProperty("outlineStencilSR").objectReferenceValue as SpriteRenderer;
            if (null == outlineNoSnap || null == stencilNoSnap || null == outline || null == stencil)
            {
                Debug.LogError("[RareLootAppearBuilder] NoSnap 아웃라인 머티리얼 또는 LootItem 아웃라인 렌더러를 찾지 못했습니다.");
            }
            else
            {
                outline.sharedMaterial = outlineNoSnap;
                stencil.sharedMaterial = stencilNoSnap;
            }

            // 이전 실행에서 넣은 인스턴스는 지우고 새로 넣는다
            Transform old = vfxRoot.transform.Find("VFX_RareLootAppear");
            if (null != old) UnityEngine.Object.DestroyImmediate(old.gameObject);

            GameObject instance = PrefabUtility.InstantiatePrefab(_effectPrefab, vfxRoot.transform) as GameObject;
            instance.name = "VFX_RareLootAppear";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            VFX_RareLootAppear effect = instance.GetComponent<VFX_RareLootAppear>();
            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("data").objectReferenceValue = _data;
            so.FindProperty("itemRenderer").objectReferenceValue = item;
            so.FindProperty("outlineRenderer").objectReferenceValue = outline;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, LootItemPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureFolder(string _path)
    {
        if (true == AssetDatabase.IsValidFolder(_path)) return;

        string parent = Path.GetDirectoryName(_path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(_path));
    }
}
#endif
