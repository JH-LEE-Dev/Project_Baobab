#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 대시 박차기 먼지 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_DashKickoff.mat: 정점 색(흰색, 알파)을 그대로 그리는 무조명 알파 블렌드 머티리얼(SpriteGlow 셰이더, 배율 1.0 = 발광 없음)
///  2) VFX_DashKickoff.prefab: 별자리 노바 프리팹의 풀 규약 구조(루트 ParticleSystem + 앞/뒤/바닥 메쉬)를 복제해
///     VFX_DashKickoff 컴포넌트로 교체
/// </summary>
public static class DashKickoffBuilder
{
    private const string MaterialPath = "Assets/Graphics/VFX/Materials/M_DashKickoff.mat";
    private const string ShaderName = "Custom/VFX/SpriteGlow";
    private const string SourcePrefabPath = "Assets/Prefabs/VFX/Constellation/VFX_ConstellationNova.prefab";
    private const string PrefabPath = "Assets/Prefabs/VFX/Dash/VFX_DashKickoff.prefab";

    // 루트 ParticleSystem 수명은 이펙트 전체 길이(전체 0.20초)보다 길어야 풀이 너무 일찍 회수하지 않는다
    private const float RootLifetime = 0.5f;

    [MenuItem("LumberBoy/VFX/Build Dash Kickoff")]
    public static void Build()
    {
        Material material = BuildMaterial();
        if (null == material) return;

        BuildPrefab(material);
        AssetDatabase.SaveAssets();
        Debug.Log("[DashKickoffBuilder] 완료: " + MaterialPath + " / " + PrefabPath);
    }

    private static Material BuildMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (null == shader)
        {
            Debug.LogError("[DashKickoffBuilder] 셰이더를 찾지 못했습니다: " + ShaderName);
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

        material.SetFloat("_Glow", 1.0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildPrefab(Material _material)
    {
        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
        {
            string folder = System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
            if (false == AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/VFX", "Dash");
            }

            if (false == AssetDatabase.CopyAsset(SourcePrefabPath, PrefabPath))
            {
                Debug.LogError("[DashKickoffBuilder] 프리팹 복제 실패: " + SourcePrefabPath);
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            root.name = "VFX_DashKickoff";

            // 앞쪽 메쉬 자식(원래 NovaMesh): 별자리 노바 컴포넌트를 제거하고 대시 박차기 컴포넌트를 붙인다
            VFX_ConstellationNova nova = root.GetComponentInChildren<VFX_ConstellationNova>(true);
            Transform frontTransform = null != nova ? nova.transform : root.transform.Find("DashMesh");

            if (null == frontTransform)
            {
                Debug.LogError("[DashKickoffBuilder] 앞쪽 메쉬 자식을 찾지 못했습니다.");
                return;
            }

            if (null != nova) Object.DestroyImmediate(nova);

            frontTransform.name = "DashMesh";
            VFX_DashKickoff effect = frontTransform.GetComponent<VFX_DashKickoff>();
            if (null == effect) effect = frontTransform.gameObject.AddComponent<VFX_DashKickoff>();

            Transform groundTransform = root.transform.Find("GroundMesh");
            if (null == groundTransform)
            {
                Debug.LogError("[DashKickoffBuilder] GroundMesh 자식을 찾지 못했습니다.");
                return;
            }

            // 뒤쪽 메쉬: 바닥 메쉬와 같은 구성(MeshFilter + MeshRenderer)으로 하나 더 만든다
            Transform backTransform = root.transform.Find("BackMesh");
            if (null == backTransform)
            {
                GameObject back = new GameObject("BackMesh", typeof(MeshFilter), typeof(MeshRenderer));
                back.layer = root.layer;
                backTransform = back.transform;
                backTransform.SetParent(root.transform, false);
            }

            MeshRenderer frontRenderer = frontTransform.GetComponent<MeshRenderer>();
            MeshRenderer backRenderer = backTransform.GetComponent<MeshRenderer>();
            MeshRenderer groundRenderer = groundTransform.GetComponent<MeshRenderer>();
            frontRenderer.sharedMaterial = _material;
            backRenderer.sharedMaterial = _material;
            groundRenderer.sharedMaterial = _material;

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("density").floatValue = 1.0f;
            so.FindProperty("sizeScale").floatValue = 1.0f;
            so.FindProperty("defaultDistance").floatValue = 3.0f;
            so.FindProperty("defaultDuration").floatValue = 0.18f;
            so.FindProperty("backMeshFilter").objectReferenceValue = backTransform.GetComponent<MeshFilter>();
            so.FindProperty("backMeshRenderer").objectReferenceValue = backRenderer;
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
