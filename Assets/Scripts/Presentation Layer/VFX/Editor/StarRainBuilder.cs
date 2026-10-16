#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 별 비 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_StarRain.mat: 정점 알파를 발광 세기로 쓰는 BrandWrapGlow 머티리얼(별자리 노바와 같은 GlowMin 1.2 / GlowMax 6)
///  2) VFX_StarRain.prefab: 별자리 노바 프리팹의 풀 규약 구조(루트 ParticleSystem + 공중 메쉬 + 바닥 메쉬)를 복제해 VFX_StarRain 컴포넌트로 교체
/// </summary>
public static class StarRainBuilder
{
    private const string MaterialPath = "Assets/Graphics/VFX/Materials/M_StarRain.mat";
    private const string ShaderName = "Custom/VFX/BrandWrapGlow";
    private const string SourcePrefabPath = "Assets/Prefabs/VFX/Constellation/VFX_ConstellationNova.prefab";
    private const string PrefabFolder = "Assets/Prefabs/VFX/StarRain";
    private const string PrefabPath = "Assets/Prefabs/VFX/StarRain/VFX_StarRain.prefab";

    // 루트 ParticleSystem 수명은 이펙트 최대 길이(지속시간 8초 + 낙하 연출 + 여유)보다 길어야 풀이 너무 일찍 회수하지 않는다
    private const float RootLifetime = 10.0f;

    [MenuItem("LumberBoy/VFX/Build Star Rain")]
    public static void Build()
    {
        Material material = BuildMaterial();
        if (null == material) return;

        BuildPrefab(material);
        AssetDatabase.SaveAssets();
        Debug.Log("[StarRainBuilder] 완료: " + MaterialPath + " / " + PrefabPath);
    }

    private static Material BuildMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (null == shader)
        {
            Debug.LogError("[StarRainBuilder] 셰이더를 찾지 못했습니다: " + ShaderName);
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (null == material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetFloat("_GlowMin", 1.2f);
        material.SetFloat("_GlowMax", 6.0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildPrefab(Material _material)
    {
        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
        {
            if (false == AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/VFX", "StarRain");
            }

            if (false == AssetDatabase.CopyAsset(SourcePrefabPath, PrefabPath))
            {
                Debug.LogError("[StarRainBuilder] 프리팹 복제 실패: " + SourcePrefabPath);
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            root.name = "VFX_StarRain";

            // 공중 메쉬 자식(원래 NovaMesh): 별자리 노바 컴포넌트를 제거하고 별 비 컴포넌트를 붙인다
            VFX_ConstellationNova nova = root.GetComponentInChildren<VFX_ConstellationNova>(true);
            Transform airTransform = null != nova ? nova.transform : root.transform.Find("RainMesh");

            if (null == airTransform)
            {
                Debug.LogError("[StarRainBuilder] 공중 메쉬 자식을 찾지 못했습니다.");
                return;
            }

            if (null != nova) Object.DestroyImmediate(nova);

            airTransform.name = "RainMesh";
            VFX_StarRain effect = airTransform.GetComponent<VFX_StarRain>();
            if (null == effect) effect = airTransform.gameObject.AddComponent<VFX_StarRain>();

            Transform groundTransform = root.transform.Find("GroundMesh");
            if (null == groundTransform)
            {
                Debug.LogError("[StarRainBuilder] GroundMesh 자식을 찾지 못했습니다.");
                return;
            }

            MeshRenderer airRenderer = airTransform.GetComponent<MeshRenderer>();
            MeshRenderer groundRenderer = groundTransform.GetComponent<MeshRenderer>();
            airRenderer.sharedMaterial = _material;
            groundRenderer.sharedMaterial = _material;

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("defaultTier").intValue = 2;
            so.FindProperty("defaultDuration").floatValue = 4.0f;
            so.FindProperty("defaultRadius").floatValue = 2.4f;
            so.FindProperty("quadBudget").intValue = 2400;
            so.FindProperty("sortingOrderOffset").intValue = 0;
            so.FindProperty("groundMeshFilter").objectReferenceValue = groundTransform.GetComponent<MeshFilter>();
            so.FindProperty("groundMeshRenderer").objectReferenceValue = groundRenderer;
            so.FindProperty("groundSortingLayerName").stringValue = "ShockWave";
            so.FindProperty("groundSortingOrder").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 풀 규약: 루트 ParticleSystem이 이펙트보다 오래 살아 있다가 끝나야 한다
            ParticleSystem ps = root.GetComponent<ParticleSystem>();
            if (null != ps)
            {
                ParticleSystem.MainModule main = ps.main;
                main.duration = RootLifetime;
                main.startLifetime = RootLifetime;
            }

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
