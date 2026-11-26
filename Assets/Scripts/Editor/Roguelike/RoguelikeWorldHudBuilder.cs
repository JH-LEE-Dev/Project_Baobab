#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 로그라이크 월드 HUD(WorldHUD_Plan.md)의 에디터 구성기. 여러 번 실행해도 같은 결과로 덮어쓴다.
///   LumberBoy/UI/Build Roguelike World HUD  재색 시트 임포트 설정(Read/Write 켬, Point, 압축 없음, 밉맵 끔)을 맞추고 RoguelikeWorldHud.prefab을 만든다.
///   프리팹 = 루트(RoguelikeWorldHud) + 자식 SpriteRenderer 8개. 띠 2개는 고정 Sorting Layer TreeShadow / Order -32768(short.MinValue)(Y 정렬 없음: 나무 · 액터 · 이펙트 · 물 장식은 모두 띠 위, 물 · 지형만 아래),
///   숫자 4개는 WorldUI / Order 0, 연출 입자 2개(10-4)는 WorldUI / Order -1(숫자보다 아래)이고
///   Unlit 스프라이트 재질(조명 영향 없음)을 쓴다. 렌더러는 꺼진 채로 시작한다(RoguelikeWorldHud가 켠다).
/// 시트는 Docs/Roguelike/_Work/WorldHUD/make_world_hud_sprites.py가 Assets/Graphics/Roguelike/hud/world/에 만든다.
/// </summary>
public static class RoguelikeWorldHudBuilder
{
    public const string SheetDir = "Assets/Graphics/Roguelike/hud/world/";
    public const string PrefabPath = "Assets/Prefabs/Roguelike/UI/RoguelikeWorldHud.prefab";

    private const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    private const string BandSortingLayer = "TreeShadow";
    private const int BandSortingOrder = short.MinValue;
    private const string LabelSortingLayer = "WorldUI";
    private const int LabelSortingOrder = 0;
    private const int FxSortingOrder = -1;

    private static readonly string[] ElementNames = { "ThresholdBand", "StaminaBand", "WaveLabel", "ThresholdLabel", "StaminaLabel", "KillLabel", "FxBand", "FxKill" };

    [MenuItem("LumberBoy/UI/Build Roguelike World HUD")]
    public static void Build()
    {
        EnsureSheetImportSettings();

        RoguelikeWorldHudSheets sheets = LoadSheets();
        if (null == sheets) return;

        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
        if (null == unlit)
        {
            Debug.LogError("[RoguelikeWorldHudBuilder] Unlit 스프라이트 재질을 찾지 못했습니다: " + UnlitMaterialPath);
            return;
        }

        if (false == SortingLayerExists(BandSortingLayer) || false == SortingLayerExists(LabelSortingLayer))
        {
            Debug.LogError("[RoguelikeWorldHudBuilder] Sorting Layer가 없습니다: " + BandSortingLayer + " / " + LabelSortingLayer + " (TagManager는 바꾸지 않는다 - 보고 필요)");
            return;
        }

        GameObject root = new GameObject("RoguelikeWorldHud");

        try
        {
            RoguelikeWorldHud hud = root.AddComponent<RoguelikeWorldHud>();
            SpriteRenderer[] renderers = new SpriteRenderer[ElementNames.Length];

            for (int i = 0; ElementNames.Length > i; i++)
            {
                GameObject child = new GameObject(ElementNames[i]);
                child.transform.SetParent(root.transform, false);

                SpriteRenderer spriteRenderer = child.AddComponent<SpriteRenderer>();
                spriteRenderer.sharedMaterial = unlit;
                spriteRenderer.sortingLayerName = 2 > i ? BandSortingLayer : LabelSortingLayer;
                spriteRenderer.sortingOrder = 2 > i ? BandSortingOrder : (RoguelikeWorldHudPresenter.FxBandIndex > i ? LabelSortingOrder : FxSortingOrder);
                spriteRenderer.enabled = false;
                renderers[i] = spriteRenderer;
            }

            SerializedObject so = new SerializedObject(hud);
            SerializedProperty rendererProperty = so.FindProperty("renderers");
            rendererProperty.arraySize = renderers.Length;
            for (int i = 0; renderers.Length > i; i++) rendererProperty.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];

            SerializedProperty sheetProperty = so.FindProperty("sheets");
            sheetProperty.FindPropertyRelative("bigDigits").objectReferenceValue = sheets.bigDigits;
            sheetProperty.FindPropertyRelative("bigDigitsWarn").objectReferenceValue = sheets.bigDigitsWarn;
            sheetProperty.FindPropertyRelative("bigDigitsAmber").objectReferenceValue = sheets.bigDigitsAmber;
            sheetProperty.FindPropertyRelative("bigDigitsGold").objectReferenceValue = sheets.bigDigitsGold;
            sheetProperty.FindPropertyRelative("smallDigits").objectReferenceValue = sheets.smallDigits;
            sheetProperty.FindPropertyRelative("slash").objectReferenceValue = sheets.slash;
            sheetProperty.FindPropertyRelative("iconTree").objectReferenceValue = sheets.iconTree;
            sheetProperty.FindPropertyRelative("iconBolt").objectReferenceValue = sheets.iconBolt;
            sheetProperty.FindPropertyRelative("iconAxe").objectReferenceValue = sheets.iconAxe;
            sheetProperty.FindPropertyRelative("iconFlag").objectReferenceValue = sheets.iconFlag;
            sheetProperty.FindPropertyRelative("iconDash").objectReferenceValue = sheets.iconDash;
            sheetProperty.FindPropertyRelative("iconInfinity").objectReferenceValue = sheets.iconInfinity;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[RoguelikeWorldHudBuilder] 완료: " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static bool SortingLayerExists(string _name)
    {
        SortingLayer[] layers = SortingLayer.layers;

        for (int i = 0; layers.Length > i; i++)
        {
            if (_name == layers[i].name) return true;
        }

        return false;
    }

    /// <summary>시트 12장의 임포트 설정을 규칙대로 맞춘다(Default 텍스처, Read/Write, Point, 압축 없음, 밉맵 끔, Clamp, 알파 투명). 바뀐 파일만 다시 임포트한다.</summary>
    public static void EnsureSheetImportSettings()
    {
        string[] names =
        {
            "WorldBigDigits", "WorldBigDigits_Warn", "WorldBigDigits_Amber", "WorldBigDigits_Gold", "WorldSmallDigits", "WorldSlash",
            "WorldIconTree", "WorldIconBolt", "WorldIconAxe", "WorldIconFlag", "WorldIconDash", "WorldIconInfinity",
        };

        for (int i = 0; names.Length > i; i++)
        {
            string path = SheetDir + names[i] + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (null == importer)
            {
                Debug.LogError("[RoguelikeWorldHudBuilder] 시트를 찾지 못했습니다(make_world_hud_sprites.py 실행 필요): " + path);
                continue;
            }

            bool bOk = TextureImporterType.Default == importer.textureType
                && true == importer.isReadable
                && FilterMode.Point == importer.filterMode
                && TextureImporterCompression.Uncompressed == importer.textureCompression
                && false == importer.mipmapEnabled
                && TextureWrapMode.Clamp == importer.wrapMode
                && true == importer.alphaIsTransparency
                && TextureImporterNPOTScale.None == importer.npotScale;
            if (true == bOk) continue;

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
        }
    }

    /// <summary>시트 12장을 읽어 페인터 입력 묶음을 만든다. 하나라도 없으면 null.</summary>
    public static RoguelikeWorldHudSheets LoadSheets()
    {
        RoguelikeWorldHudSheets sheets = new RoguelikeWorldHudSheets
        {
            bigDigits = LoadSheet("WorldBigDigits"),
            bigDigitsWarn = LoadSheet("WorldBigDigits_Warn"),
            bigDigitsAmber = LoadSheet("WorldBigDigits_Amber"),
            bigDigitsGold = LoadSheet("WorldBigDigits_Gold"),
            smallDigits = LoadSheet("WorldSmallDigits"),
            slash = LoadSheet("WorldSlash"),
            iconTree = LoadSheet("WorldIconTree"),
            iconBolt = LoadSheet("WorldIconBolt"),
            iconAxe = LoadSheet("WorldIconAxe"),
            iconFlag = LoadSheet("WorldIconFlag"),
            iconDash = LoadSheet("WorldIconDash"),
            iconInfinity = LoadSheet("WorldIconInfinity"),
        };

        if (null == sheets.bigDigits || null == sheets.bigDigitsWarn || null == sheets.bigDigitsAmber || null == sheets.bigDigitsGold || null == sheets.smallDigits || null == sheets.slash
            || null == sheets.iconTree || null == sheets.iconBolt || null == sheets.iconAxe || null == sheets.iconFlag || null == sheets.iconDash || null == sheets.iconInfinity)
        {
            return null;
        }

        return sheets;
    }

    private static Texture2D LoadSheet(string _name)
    {
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetDir + _name + ".png");
        if (null == texture) Debug.LogError("[RoguelikeWorldHudBuilder] 시트를 찾지 못했습니다: " + SheetDir + _name + ".png");
        return texture;
    }
}
#endif
