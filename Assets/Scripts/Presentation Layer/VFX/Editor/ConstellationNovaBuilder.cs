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
