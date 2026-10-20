#if UNITY_EDITOR
using PresentationLayer.Environment;
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 전리품 진화 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_LootEvolve.mat: 정점 알파를 발광 세기로 쓰는 BrandWrapGlow 머티리얼(별자리 노바와 같은 GlowMin 1.2 / GlowMax 6)
///  2) LootPillar.prefab에 LootEvolve 자식(공중 메쉬 + HaloMesh + GroundMesh)을 만들고 LootDisplayObject.evolveEffect에 연결
///     (아우라 변조용으로 LootDisplayObject.auraOrbitController도 이펙트에 연결)
/// </summary>
public static class LootEvolveBuilder
{
    private const string MaterialPath = "Assets/Graphics/VFX/Materials/M_LootEvolve.mat";
    private const string ShaderName = "Custom/VFX/BrandWrapGlow";
    private const string PrefabPath = "Assets/Prefabs/Objects/LootPillar/LootPillar.prefab";

    [MenuItem("LumberBoy/VFX/Build Loot Evolve")]
    public static void Build()
    {
        Material material = BuildMaterial();
        if (null == material) return;

        BuildPrefab(material);
        AssetDatabase.SaveAssets();
        Debug.Log("[LootEvolveBuilder] 완료: " + MaterialPath + " / " + PrefabPath);
    }

    private static Material BuildMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (null == shader)
        {
            Debug.LogError("[LootEvolveBuilder] 셰이더를 찾지 못했습니다: " + ShaderName);
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

    private static Transform GetOrCreateChild(Transform _parent, string _name, bool _bWithMesh)
    {
        Transform child = _parent.Find(_name);
        if (null == child)
        {
            GameObject go = true == _bWithMesh
                ? new GameObject(_name, typeof(MeshFilter), typeof(MeshRenderer))
                : new GameObject(_name);
            go.layer = _parent.gameObject.layer;
            child = go.transform;
            child.SetParent(_parent, false);
        }
        else if (true == _bWithMesh)
        {
            if (null == child.GetComponent<MeshFilter>()) child.gameObject.AddComponent<MeshFilter>();
            if (null == child.GetComponent<MeshRenderer>()) child.gameObject.AddComponent<MeshRenderer>();
        }

        return child;
    }

    private static void BuildPrefab(Material _material)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            Transform loot = root.transform.Find("Loot");
            if (null == loot)
            {
                Debug.LogError("[LootEvolveBuilder] Loot 자식을 찾지 못했습니다.");
                return;
            }

            LootDisplayObject display = root.GetComponent<LootDisplayObject>();
            if (null == display)
            {
                Debug.LogError("[LootEvolveBuilder] LootDisplayObject를 찾지 못했습니다.");
                return;
            }

            // 공중 메쉬 + 컴포넌트가 붙는 루트 오브젝트: 전리품 중심과 같은 위치에 둔다
            Transform effectTransform = GetOrCreateChild(root.transform, "LootEvolve", true);
            effectTransform.localPosition = loot.localPosition;
            effectTransform.localRotation = Quaternion.identity;
            effectTransform.localScale = Vector3.one;

            VFX_LootEvolve effect = effectTransform.GetComponent<VFX_LootEvolve>();
            if (null == effect) effect = effectTransform.gameObject.AddComponent<VFX_LootEvolve>();

            Transform haloTransform = GetOrCreateChild(effectTransform, "HaloMesh", true);
            Transform groundTransform = GetOrCreateChild(effectTransform, "GroundMesh", true);
            haloTransform.localPosition = Vector3.zero;
            groundTransform.localPosition = Vector3.zero;

            effectTransform.GetComponent<MeshRenderer>().sharedMaterial = _material;
            haloTransform.GetComponent<MeshRenderer>().sharedMaterial = _material;
            groundTransform.GetComponent<MeshRenderer>().sharedMaterial = _material;

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("itemRenderer").objectReferenceValue = loot.GetComponent<SpriteRenderer>();
            so.FindProperty("itemTransform").objectReferenceValue = loot;

            // 아우라(전리품 뒤 중앙 글로우): LootDisplayObject가 이미 가진 컨트롤러를 그대로 연결한다
            SerializedObject displayRead = new SerializedObject(display);
            so.FindProperty("auraController").objectReferenceValue = displayRead.FindProperty("auraOrbitController").objectReferenceValue;
            so.FindProperty("quadBudget").intValue = 1800;
            so.FindProperty("airSortingLayerName").stringValue = "FlyingItem";
            so.FindProperty("airSortingOrder").intValue = 10;
            so.FindProperty("haloMeshFilter").objectReferenceValue = haloTransform.GetComponent<MeshFilter>();
            so.FindProperty("haloMeshRenderer").objectReferenceValue = haloTransform.GetComponent<MeshRenderer>();
            so.FindProperty("groundMeshFilter").objectReferenceValue = groundTransform.GetComponent<MeshFilter>();
            so.FindProperty("groundMeshRenderer").objectReferenceValue = groundTransform.GetComponent<MeshRenderer>();
            so.FindProperty("groundSortingLayerName").stringValue = "ShockWave";
            so.FindProperty("groundSortingOrder").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject displaySo = new SerializedObject(display);
            displaySo.FindProperty("evolveEffect").objectReferenceValue = effect;
            displaySo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
