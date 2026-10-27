#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 모닥불 불꽃 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_CampfireFlame.mat: 정점 색(RGB) + 정점 알파(발광 세기)를 그리는 BrandWrapGlow 머티리얼(GlowMin 1.0 / GlowMax 2.2, 연기는 알파 0이라 색 그대로)
///  2) M_CampfireGroundGlow.mat: 정점 색에 정점 알파를 곱해 배경에 더하는 PixelQuadAdditive 머티리얼(바닥 불빛)
///  3) OffRoadVehicle.prefab: RestArea 아래에 CampfireFlame(몸통 메쉬) + GroundGlow(바닥 불빛 메쉬)를 만들고 RestAreaVisual.flameVfx에 연결한다
/// </summary>
public static class CampfireFlameBuilder
{
    private const string FlameMaterialPath = "Assets/Graphics/VFX/Materials/M_CampfireFlame.mat";
    private const string GlowMaterialPath = "Assets/Graphics/VFX/Materials/M_CampfireGroundGlow.mat";
    private const string FlameShaderName = "Custom/VFX/BrandWrapGlow";
    private const string GlowShaderName = "Custom/VFX/PixelQuadAdditive";
    private const string VehiclePrefabPath = "Assets/Prefabs/Objects/OffRoadVehicle/OffRoadVehicle.prefab";

    private const string FlameObjectName = "CampfireFlame";
    private const string GlowObjectName = "GroundGlow";

    [MenuItem("LumberBoy/VFX/Build Campfire Flame")]
    public static void Build()
    {
        Material flameMaterial = BuildMaterial(FlameMaterialPath, FlameShaderName, true);
        Material glowMaterial = BuildMaterial(GlowMaterialPath, GlowShaderName, false);
        if (null == flameMaterial || null == glowMaterial) return;

        AssetDatabase.SaveAssets();
        WireVehicle(flameMaterial, glowMaterial);
        AssetDatabase.SaveAssets();
        Debug.Log("[CampfireFlameBuilder] 완료: " + FlameMaterialPath + " / " + GlowMaterialPath + " / " + VehiclePrefabPath);
    }

    private static Material BuildMaterial(string _path, string _shaderName, bool _bGlowRange)
    {
        Shader shader = Shader.Find(_shaderName);
        if (null == shader)
        {
            Debug.LogError("[CampfireFlameBuilder] 셰이더를 찾지 못했습니다: " + _shaderName);
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

        if (true == _bGlowRange)
        {
            material.SetFloat("_GlowMin", 1.0f);
            material.SetFloat("_GlowMax", 2.2f);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform FindOrCreateChild(Transform _parent, string _name)
    {
        Transform child = _parent.Find(_name);
        if (null != child) return child;

        GameObject go = new GameObject(_name);
        go.transform.SetParent(_parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static void WireVehicle(Material _flameMaterial, Material _glowMaterial)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(VehiclePrefabPath);

        try
        {
            RestAreaVisual restArea = root.GetComponentInChildren<RestAreaVisual>(true);
            if (null == restArea)
            {
                Debug.LogError("[CampfireFlameBuilder] OffRoadVehicle에서 RestAreaVisual을 찾지 못했습니다.");
                return;
            }

            // 불꽃 몸통: 원점은 RestArea(= CampFire) 위치
            Transform flameTransform = FindOrCreateChild(restArea.transform, FlameObjectName);
            MeshFilter flameFilter = flameTransform.GetComponent<MeshFilter>();
            if (null == flameFilter) flameFilter = flameTransform.gameObject.AddComponent<MeshFilter>();
            MeshRenderer flameRenderer = flameTransform.GetComponent<MeshRenderer>();
            if (null == flameRenderer) flameRenderer = flameTransform.gameObject.AddComponent<MeshRenderer>();
            flameRenderer.sharedMaterial = _flameMaterial;

            VFX_CampfireFlame flame = flameTransform.GetComponent<VFX_CampfireFlame>();
            if (null == flame) flame = flameTransform.gameObject.AddComponent<VFX_CampfireFlame>();

            // 바닥 불빛
            Transform glowTransform = FindOrCreateChild(flameTransform, GlowObjectName);
            MeshFilter glowFilter = glowTransform.GetComponent<MeshFilter>();
            if (null == glowFilter) glowFilter = glowTransform.gameObject.AddComponent<MeshFilter>();
            MeshRenderer glowRenderer = glowTransform.GetComponent<MeshRenderer>();
            if (null == glowRenderer) glowRenderer = glowTransform.gameObject.AddComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = _glowMaterial;

            SerializedObject flameSo = new SerializedObject(flame);
            flameSo.FindProperty("glowScale").floatValue = 1.0f;
            flameSo.FindProperty("groundGlowStrength").floatValue = 0.22f;
            flameSo.FindProperty("sortingLayerName").stringValue = "Objects";
            flameSo.FindProperty("sortingOrderOffset").intValue = 1;
            flameSo.FindProperty("glowRenderer").objectReferenceValue = glowRenderer;
            flameSo.FindProperty("glowSortingLayerName").stringValue = "Indicators";
            flameSo.FindProperty("glowSortingOrder").intValue = 0;
            flameSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject restSo = new SerializedObject(restArea);
            restSo.FindProperty("flameVfx").objectReferenceValue = flame;
            restSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, VehiclePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
