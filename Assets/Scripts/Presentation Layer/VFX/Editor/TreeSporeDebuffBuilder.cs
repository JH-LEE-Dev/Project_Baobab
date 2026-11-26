#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 나무 포자 디버프 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_TreeSporeDebuff.mat: 정점 색(알파) 무조명 머티리얼(SpriteGlow, 배율 1.0)
///  2) M_TreeSporeDebuffSprite.mat: 바닥 링 스프라이트용 무조명 스프라이트 머티리얼
///  3) M_TreeSporeDebuffSway.mat: 상단 메쉬용(나무 본체와 같이 바람에 흔들리는 정점 색 무조명, 배율 1.0)
///  4) M_TreeSporeDebuffOverlay.mat: 나무 오버레이 스프라이트용(나무 본체와 같이 바람에 흔들리는 무조명 스프라이트)
///  5) Resources/TreeSporeDebuff/TreeSporeDebuffVfxManager.prefab: 관리자(바닥 메쉬 + 상단 메쉬 자식). Resources에서 읽으므로 빌드에 포함된다.
/// </summary>
public static class TreeSporeDebuffBuilder
{
    private const string MeshMaterialPath = "Assets/Graphics/VFX/Materials/M_TreeSporeDebuff.mat";
    private const string SpriteMaterialPath = "Assets/Graphics/VFX/Materials/M_TreeSporeDebuffSprite.mat";
    private const string SwayMeshMaterialPath = "Assets/Graphics/VFX/Materials/M_TreeSporeDebuffSway.mat";
    private const string OverlayMaterialPath = "Assets/Graphics/VFX/Materials/M_TreeSporeDebuffOverlay.mat";
    private const string MeshShaderName = "Custom/VFX/SpriteGlow";
    private const string SwayMeshShaderName = "Custom/VFX/TreeSporeDebuffSway";
    private const string OverlayShaderName = "Custom/VFX/TreeSporeDebuffOverlay";
    private const string SpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    private const string PrefabFolder = "Assets/Graphics/VFX/Resources/TreeSporeDebuff";
    private const string PrefabPath = PrefabFolder + "/TreeSporeDebuffVfxManager.prefab";

    [MenuItem("LumberBoy/VFX/Build Tree Spore Debuff")]
    public static void Build()
    {
        Material meshMaterial = BuildMaterial(MeshMaterialPath, MeshShaderName, true);
        Material spriteMaterial = BuildMaterial(SpriteMaterialPath, SpriteShaderName, false);
        Material swayMeshMaterial = BuildMaterial(SwayMeshMaterialPath, SwayMeshShaderName, true);
        Material overlayMaterial = BuildMaterial(OverlayMaterialPath, OverlayShaderName, false);
        if (null == meshMaterial || null == spriteMaterial || null == swayMeshMaterial || null == overlayMaterial) return;

        // 오버레이는 GPU 인스턴싱으로 그려야 스프라이트 묶음 그리기로 합쳐지지 않아 렌더러마다 자기 피벗으로 흔들린다(셰이더 주석 참고).
        overlayMaterial.enableInstancing = true;
        EditorUtility.SetDirty(overlayMaterial);

        BuildPrefab(meshMaterial, spriteMaterial, swayMeshMaterial, overlayMaterial);
        AssetDatabase.SaveAssets();
        Debug.Log("[TreeSporeDebuffBuilder] 완료: " + PrefabPath);
    }

    private static Material BuildMaterial(string _path, string _shaderName, bool _bGlow)
    {
        Shader shader = Shader.Find(_shaderName);
        if (null == shader)
        {
            Debug.LogError("[TreeSporeDebuffBuilder] 셰이더를 찾지 못했습니다: " + _shaderName);
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(_path);
        if (null == material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, _path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        if (true == _bGlow) material.SetFloat("_Glow", 1.0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void BuildPrefab(Material _meshMaterial, Material _spriteMaterial, Material _swayMeshMaterial, Material _overlayMaterial)
    {
        if (false == AssetDatabase.IsValidFolder(PrefabFolder))
        {
            AssetDatabase.CreateFolder("Assets/Graphics/VFX/Resources", "TreeSporeDebuff");
        }

        GameObject root = new GameObject("TreeSporeDebuffVfxManager");

        try
        {
            TreeSporeDebuffVfxManager manager = root.AddComponent<TreeSporeDebuffVfxManager>();

            GameObject ground = new GameObject("GroundMesh", typeof(MeshFilter), typeof(MeshRenderer));
            ground.transform.SetParent(root.transform, false);
            GameObject top = new GameObject("TopMesh", typeof(MeshFilter), typeof(MeshRenderer));
            top.transform.SetParent(root.transform, false);

            ground.GetComponent<MeshRenderer>().sharedMaterial = _meshMaterial;
            top.GetComponent<MeshRenderer>().sharedMaterial = _swayMeshMaterial;

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("groundMeshFilter").objectReferenceValue = ground.GetComponent<MeshFilter>();
            so.FindProperty("groundMeshRenderer").objectReferenceValue = ground.GetComponent<MeshRenderer>();
            so.FindProperty("topMeshFilter").objectReferenceValue = top.GetComponent<MeshFilter>();
            so.FindProperty("topMeshRenderer").objectReferenceValue = top.GetComponent<MeshRenderer>();
            so.FindProperty("spriteMaterial").objectReferenceValue = _spriteMaterial;
            so.FindProperty("overlayMaterial").objectReferenceValue = _overlayMaterial;
            so.FindProperty("groundSortingLayerName").stringValue = "ShockWave";
            so.FindProperty("groundSpriteOrder").intValue = 0;
            so.FindProperty("groundMeshOrder").intValue = 1;
            so.FindProperty("topSortingLayerName").stringValue = "FlyingItem";
            so.FindProperty("topMeshOrder").intValue = 12;
            so.FindProperty("maxFullTrees").intValue = 16;
            so.FindProperty("cullMargin").floatValue = 4.0f;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
#endif
