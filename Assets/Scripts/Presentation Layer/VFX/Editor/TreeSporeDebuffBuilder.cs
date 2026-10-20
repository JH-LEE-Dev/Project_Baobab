#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 나무 포자 디버프 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_TreeSporeDebuff.mat: 정점 색(알파) 무조명 머티리얼(SpriteGlow, 배율 1.0)
///  2) M_TreeSporeDebuffSprite.mat: 오버레이/바닥 링 스프라이트용 무조명 스프라이트 머티리얼
///  3) Resources/TreeSporeDebuff/TreeSporeDebuffVfxManager.prefab: 관리자(바닥 메쉬 + 상단 메쉬 자식). Resources에서 읽으므로 빌드에 포함된다.
/// </summary>
public static class TreeSporeDebuffBuilder
{
    private const string MeshMaterialPath = "Assets/Graphics/VFX/Materials/M_TreeSporeDebuff.mat";
    private const string SpriteMaterialPath = "Assets/Graphics/VFX/Materials/M_TreeSporeDebuffSprite.mat";
    private const string MeshShaderName = "Custom/VFX/SpriteGlow";
    private const string SpriteShaderName = "Universal Render Pipeline/2D/Sprite-Unlit-Default";
    private const string PrefabFolder = "Assets/Graphics/VFX/Resources/TreeSporeDebuff";
    private const string PrefabPath = PrefabFolder + "/TreeSporeDebuffVfxManager.prefab";

    [MenuItem("LumberBoy/VFX/Build Tree Spore Debuff")]
    public static void Build()
    {
        Material meshMaterial = BuildMaterial(MeshMaterialPath, MeshShaderName, true);
        Material spriteMaterial = BuildMaterial(SpriteMaterialPath, SpriteShaderName, false);
        if (null == meshMaterial || null == spriteMaterial) return;

        BuildPrefab(meshMaterial, spriteMaterial);
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

    private static void BuildPrefab(Material _meshMaterial, Material _spriteMaterial)
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
            top.GetComponent<MeshRenderer>().sharedMaterial = _meshMaterial;

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("groundMeshFilter").objectReferenceValue = ground.GetComponent<MeshFilter>();
            so.FindProperty("groundMeshRenderer").objectReferenceValue = ground.GetComponent<MeshRenderer>();
            so.FindProperty("topMeshFilter").objectReferenceValue = top.GetComponent<MeshFilter>();
            so.FindProperty("topMeshRenderer").objectReferenceValue = top.GetComponent<MeshRenderer>();
            so.FindProperty("spriteMaterial").objectReferenceValue = _spriteMaterial;
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
