#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 별자리 노바(픽셀 각인 마법진) 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_ConstellationNovaSoft.mat: 부드러운 빛(NovaSoftGlow, 가산)
///  2) M_ConstellationNovaPixel.mat: 마법진 픽셀(BrandWrapGlow, 시안과 같은 발광 배율 1.3 ~ 4.2). 다른 이펙트가 쓰는 M_BrandStampBurst는 건드리지 않는다.
///  3) VFX_ConstellationNova.prefab: SoftMesh 자식 추가, 머티리얼 연결, 새 직렬화 필드를 SerializedObject로 명시 기록(프리팹 캐시 함정 대응)
/// </summary>
public static class ConstellationNovaBuilder
{
    private const string SoftShaderPath = "Assets/Shaders/VFX/NovaSoftGlow.shader";
    private const string PixelShaderName = "Custom/VFX/BrandWrapGlow";
    private const string SoftMaterialPath = "Assets/Graphics/VFX/Materials/M_ConstellationNovaSoft.mat";
    private const string PixelMaterialPath = "Assets/Graphics/VFX/Materials/M_ConstellationNovaPixel.mat";
    private const string PrefabPath = "Assets/Prefabs/VFX/Constellation/VFX_ConstellationNova.prefab";
    private const string ArenaPrefabPath = "Assets/Prefabs/VFX/Constellation/VFX_RoguelikeArenaNova.prefab";
    private const string InstallerPrefabPath = "Assets/Prefabs/Installer/Installer/GameInstaller.prefab";
    private const string NovaTag = "ConstellationNovaEffect";
    private const string ArenaTag = "RoguelikeArenaNovaEffect";

    // 시안의 발광 배율: 정점 알파 a -> 1.3 + 2.9 * a
    private const float PixelGlowMin = 1.3f;
    private const float PixelGlowMax = 4.2f;

    [MenuItem("LumberBoy/VFX/Build Constellation Nova")]
    public static void Build()
    {
        if (true == EditorApplication.isPlaying)
        {
            Debug.LogError("[ConstellationNovaBuilder] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        AssetDatabase.Refresh();

        Material softMaterial = BuildSoftMaterial();
        Material pixelMaterial = BuildPixelMaterial();
        if (null == softMaterial || null == pixelMaterial) return;

        BuildPrefab(softMaterial, pixelMaterial);
        AssetDatabase.SaveAssets();
        Debug.Log("[ConstellationNovaBuilder] 완료: " + SoftMaterialPath + " / " + PixelMaterialPath + " / " + PrefabPath);
    }

    /// <summary>
    /// 로그라이크 아레나 사각형 노바: 별자리 노바 프리팹 복제 + shape = ArenaDiamond(기본 반폭 9.5 x 반높이 4.75), GameInstaller 풀에 RoguelikeArenaNovaEffect 등록.
    /// 먼저 "Build Constellation Nova"가 실행되어 원본 프리팹이 최신이어야 한다(없으면 자동 실행). 여러 번 실행해도 같은 결과로 맞춘다.
    /// </summary>
    [MenuItem("LumberBoy/VFX/Build Roguelike Arena Nova")]
    public static void BuildArena()
    {
        if (true == EditorApplication.isPlaying)
        {
            Debug.LogError("[ConstellationNovaBuilder] 플레이 모드에서는 실행할 수 없습니다.");
            return;
        }

        Build();

        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath))
        {
            if (false == AssetDatabase.CopyAsset(PrefabPath, ArenaPrefabPath))
            {
                Debug.LogError("[ConstellationNovaBuilder] 프리팹 복제에 실패했습니다: " + ArenaPrefabPath);
                return;
            }
        }

        if (false == ConfigureArenaPrefab()) return;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (false == RegisterArenaPool()) return;
        AssetDatabase.SaveAssets();
        Debug.Log("[ConstellationNovaBuilder] 완료: " + ArenaPrefabPath + " / " + InstallerPrefabPath + " (" + ArenaTag + ")");
    }

    private static bool ConfigureArenaPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ArenaPrefabPath);
        try
        {
            VFX_ConstellationNova nova = root.GetComponentInChildren<VFX_ConstellationNova>(true);
            if (null == nova)
            {
                Debug.LogError("[ConstellationNovaBuilder] 아레나 프리팹에서 VFX_ConstellationNova를 찾지 못했습니다.");
                return false;
            }

            root.name = "VFX_RoguelikeArenaNova";
            SerializedObject so = new SerializedObject(nova);
            so.FindProperty("shape").enumValueIndex = (int)VFX_ConstellationNova.ENovaShape.ArenaDiamond;
            so.FindProperty("arenaHalfWidth").floatValue = 9.5f;
            so.FindProperty("arenaHalfHeight").floatValue = 4.75f;
            so.FindProperty("arenaCurtainHeightPx").floatValue = 46.0f;
            // 로그라이크 섬 타일의 보이는 윗면 중심은 칸 중심보다 0.25유닛(8px) 위
            so.FindProperty("arenaCenterOffsetY").floatValue = 0.25f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 기존 노바 풀 항목(ConstellationNovaEffect)이 있는 VFXComponent에 같은 규격(초기/최대 크기)으로 아레나 항목을 추가한다.
    private static bool RegisterArenaPool()
    {
        GameObject arenaAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
        ParticleSystem arenaRoot = null != arenaAsset ? arenaAsset.GetComponent<ParticleSystem>() : null;
        if (null == arenaRoot)
        {
            Debug.LogError("[ConstellationNovaBuilder] 아레나 프리팹 루트에 ParticleSystem이 없습니다.");
            return false;
        }

        GameObject installer = PrefabUtility.LoadPrefabContents(InstallerPrefabPath);
        try
        {
            VFXComponent[] components = installer.GetComponentsInChildren<VFXComponent>(true);
            for (int c = 0; c < components.Length; c++)
            {
                SerializedObject so = new SerializedObject(components[c]);
                SerializedProperty list = so.FindProperty("vfxPoolDataList");
                if (null == list) continue;

                SerializedProperty novaEntry = null;
                SerializedProperty arenaEntry = null;
                for (int i = 0; i < list.arraySize; i++)
                {
                    SerializedProperty candidate = list.GetArrayElementAtIndex(i);
                    string tag = candidate.FindPropertyRelative("vfxTag").stringValue;
                    if (NovaTag == tag) novaEntry = candidate;
                    else if (ArenaTag == tag) arenaEntry = candidate;
                }

                if (null == novaEntry) continue;

                if (null == arenaEntry)
                {
                    list.InsertArrayElementAtIndex(list.arraySize);
                    arenaEntry = list.GetArrayElementAtIndex(list.arraySize - 1);
                    arenaEntry.FindPropertyRelative("initialPoolSize").intValue = novaEntry.FindPropertyRelative("initialPoolSize").intValue;
                    arenaEntry.FindPropertyRelative("allowDynamicExpansion").boolValue = novaEntry.FindPropertyRelative("allowDynamicExpansion").boolValue;
                    arenaEntry.FindPropertyRelative("maxPoolSize").intValue = novaEntry.FindPropertyRelative("maxPoolSize").intValue;
                    arenaEntry.FindPropertyRelative("uiParticleScale").floatValue = novaEntry.FindPropertyRelative("uiParticleScale").floatValue;
                }

                arenaEntry.FindPropertyRelative("vfxTag").stringValue = ArenaTag;
                arenaEntry.FindPropertyRelative("effectPrefab").objectReferenceValue = arenaRoot;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(installer, InstallerPrefabPath);
                return true;
            }

            Debug.LogError("[ConstellationNovaBuilder] GameInstaller에서 " + NovaTag + " 풀 항목을 찾지 못했습니다.");
            return false;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(installer);
        }
    }

    private static Material BuildSoftMaterial()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(SoftShaderPath);
        if (null == shader)
        {
            Debug.LogError("[ConstellationNovaBuilder] 셰이더를 찾지 못했습니다: " + SoftShaderPath);
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(SoftMaterialPath);
        if (null == material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, SoftMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetFloat("_Glow", VFX_ConstellationNova.SoftGlowMultiplier);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material BuildPixelMaterial()
    {
        Shader shader = Shader.Find(PixelShaderName);
        if (null == shader)
        {
            Debug.LogError("[ConstellationNovaBuilder] 셰이더를 찾지 못했습니다: " + PixelShaderName);
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(PixelMaterialPath);
        if (null == material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, PixelMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetFloat("_GlowMin", PixelGlowMin);
        material.SetFloat("_GlowMax", PixelGlowMax);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildPrefab(Material _softMaterial, Material _pixelMaterial)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            VFX_ConstellationNova nova = root.GetComponentInChildren<VFX_ConstellationNova>(true);
            if (null == nova)
            {
                Debug.LogError("[ConstellationNovaBuilder] VFX_ConstellationNova 컴포넌트를 찾지 못했습니다.");
                return;
            }

            Transform groundTransform = root.transform.Find("GroundMesh");
            if (null == groundTransform)
            {
                Debug.LogError("[ConstellationNovaBuilder] GroundMesh 자식을 찾지 못했습니다.");
                return;
            }

            // 소프트 메쉬: 바닥 메쉬와 같은 구성(MeshFilter + MeshRenderer) 자식
            Transform softTransform = root.transform.Find("SoftMesh");
            if (null == softTransform)
            {
                GameObject soft = new GameObject("SoftMesh", typeof(MeshFilter), typeof(MeshRenderer));
                soft.layer = root.layer;
                softTransform = soft.transform;
                softTransform.SetParent(root.transform, false);
            }

            MeshRenderer airRenderer = nova.GetComponent<MeshRenderer>();
            MeshRenderer groundRenderer = groundTransform.GetComponent<MeshRenderer>();
            MeshRenderer softRenderer = softTransform.GetComponent<MeshRenderer>();
            airRenderer.sharedMaterial = _pixelMaterial;
            groundRenderer.sharedMaterial = _pixelMaterial;
            softRenderer.sharedMaterial = _softMaterial;

            // 소프트 메쉬는 공중 메쉬와 같은 레이어, 오더 +1(런타임에도 루트 렌더러 기준으로 다시 맞춘다)
            softRenderer.sortingLayerID = airRenderer.sortingLayerID;
            softRenderer.sortingOrder = airRenderer.sortingOrder + 1;
            softRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            softRenderer.receiveShadows = false;

            SerializedObject so = new SerializedObject(nova);
            so.FindProperty("maxRadius").floatValue = 4.0f;
            so.FindProperty("palette").enumValueIndex = (int)VFX_ConstellationNova.NovaPalette.Star;
            so.FindProperty("bSoftLight").boolValue = true;
            so.FindProperty("curtainHeightPx").floatValue = 74.0f;
            so.FindProperty("pillarHeightPx").floatValue = 130.0f;
            so.FindProperty("sortingOrderOffset").intValue = 0;
            so.FindProperty("groundMeshFilter").objectReferenceValue = groundTransform.GetComponent<MeshFilter>();
            so.FindProperty("groundMeshRenderer").objectReferenceValue = groundRenderer;
            so.FindProperty("groundSortingLayerName").stringValue = "ShockWave";
            so.FindProperty("groundSortingOrder").intValue = 0;
            so.FindProperty("softMeshFilter").objectReferenceValue = softTransform.GetComponent<MeshFilter>();
            so.FindProperty("softMeshRenderer").objectReferenceValue = softRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
