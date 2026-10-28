#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 수리 스파크 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_RepairSpark.mat: 정점 색(RGB) + 정점 알파(발광 세기)를 그리는 BrandWrapGlow 머티리얼(GlowMin 1.2 / GlowMax 6, 스텔라 폭발과 같은 기준)
///  2) VFX_RepairSpark.prefab: 스텔라 범위 프리팹의 풀 규약 구조(루트 ParticleSystem + 메쉬 자식)를 복제해 VFX_RepairSpark 컴포넌트로 교체
///  3) OffRoadVehicle.prefab: 수리함(RepairBox)의 VFXComponent 풀 목록에 RepairSpark 태그를 등록하고, 스파크 위치(RepairSparkPoint)를 모루 윗면으로 맞춘다
/// </summary>
public static class RepairSparkBuilder
{
    private const string MaterialPath = "Assets/Graphics/VFX/Materials/M_RepairSpark.mat";
    private const string ShaderName = "Custom/VFX/BrandWrapGlow";
    private const string SourcePrefabPath = "Assets/Prefabs/VFX/Roguelike/VFX_StellaRange.prefab";
    private const string PrefabPath = "Assets/Prefabs/VFX/Repair/VFX_RepairSpark.prefab";
    private const string VehiclePrefabPath = "Assets/Prefabs/Objects/OffRoadVehicle/OffRoadVehicle.prefab";

    // 루트 ParticleSystem 수명은 이펙트 전체 길이(1.2초)보다 길어야 풀이 너무 일찍 회수하지 않는다
    private const float RootLifetime = 1.8f;

    private const string SparkTag = "RepairSpark";

    // 모루 스프라이트(32x32, 중심 피벗) 윗면 타격 지점 = 중심에서 왼쪽 5px, 위쪽 7px. 높이는 기존 RepairSparkPoint(0.65625)와 같다.
    private const float SparkPointLocalX = -5.0f / 32.0f;

    [MenuItem("LumberBoy/VFX/Build Repair Spark")]
    public static void Build()
    {
        Material material = BuildMaterial();
        if (null == material) return;

        ParticleSystem prefabRoot = BuildPrefab(material);
        if (null == prefabRoot) return;

        AssetDatabase.SaveAssets();
        WireVehicle(prefabRoot);
        AssetDatabase.SaveAssets();
        Debug.Log("[RepairSparkBuilder] 완료: " + MaterialPath + " / " + PrefabPath + " / " + VehiclePrefabPath);
    }

    private static Material BuildMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (null == shader)
        {
            Debug.LogError("[RepairSparkBuilder] 셰이더를 찾지 못했습니다: " + ShaderName);
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

    private static ParticleSystem BuildPrefab(Material _material)
    {
        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
        {
            string folder = System.IO.Path.GetDirectoryName(PrefabPath).Replace('\\', '/');
            if (false == AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/VFX", "Repair");
            }

            if (false == AssetDatabase.CopyAsset(SourcePrefabPath, PrefabPath))
            {
                Debug.LogError("[RepairSparkBuilder] 프리팹 복제 실패: " + SourcePrefabPath);
                return null;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            root.name = "VFX_RepairSpark";

            // 메쉬 자식(원래 RangeMesh): 스텔라 범위 컴포넌트를 제거하고 수리 스파크 컴포넌트를 붙인다
            VFX_StellaRange range = root.GetComponentInChildren<VFX_StellaRange>(true);
            VFX_RepairSpark effect = root.GetComponentInChildren<VFX_RepairSpark>(true);
            Transform meshTransform = null != effect ? effect.transform : (null != range ? range.transform : null);

            if (null == meshTransform)
            {
                Debug.LogError("[RepairSparkBuilder] 메쉬 자식을 찾지 못했습니다.");
                return null;
            }

            if (null != range) Object.DestroyImmediate(range, true);

            meshTransform.name = "SparkMesh";
            if (null == effect) effect = meshTransform.gameObject.AddComponent<VFX_RepairSpark>();

            MeshRenderer meshRenderer = meshTransform.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = _material;

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("density").floatValue = 1.0f;
            so.FindProperty("glowScale").floatValue = 1.0f;
            so.FindProperty("sortingOrderOffset").intValue = 0;
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

        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<ParticleSystem>();
    }

    private static void WireVehicle(ParticleSystem _effectPrefab)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(VehiclePrefabPath);

        try
        {
            RepairBox repairBox = root.GetComponentInChildren<RepairBox>(true);
            if (null == repairBox)
            {
                Debug.LogError("[RepairSparkBuilder] OffRoadVehicle에서 RepairBox를 찾지 못했습니다.");
                return;
            }

            SerializedObject boxSo = new SerializedObject(repairBox);
            VFXComponent vfx = boxSo.FindProperty("repairVfx").objectReferenceValue as VFXComponent;
            Transform sparkPoint = boxSo.FindProperty("repairSparkPoint").objectReferenceValue as Transform;

            if (null == vfx)
            {
                Debug.LogError("[RepairSparkBuilder] RepairBox.repairVfx가 비어 있습니다.");
                return;
            }

            SerializedObject vfxSo = new SerializedObject(vfx);
            SerializedProperty list = vfxSo.FindProperty("vfxPoolDataList");

            SerializedProperty entry = null;
            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty candidate = list.GetArrayElementAtIndex(i);
                if (SparkTag == candidate.FindPropertyRelative("vfxTag").stringValue)
                {
                    entry = candidate;
                    break;
                }
            }

            if (null == entry)
            {
                list.InsertArrayElementAtIndex(list.arraySize);
                entry = list.GetArrayElementAtIndex(list.arraySize - 1);
            }

            entry.FindPropertyRelative("vfxTag").stringValue = SparkTag;
            entry.FindPropertyRelative("effectPrefab").objectReferenceValue = _effectPrefab;
            entry.FindPropertyRelative("initialPoolSize").intValue = 1;
            entry.FindPropertyRelative("allowDynamicExpansion").boolValue = true;
            entry.FindPropertyRelative("maxPoolSize").intValue = 2;
            entry.FindPropertyRelative("uiParticleScale").floatValue = 0.0f;
            vfxSo.ApplyModifiedPropertiesWithoutUndo();

            if (null != sparkPoint)
            {
                Vector3 local = sparkPoint.localPosition;
                local.x = SparkPointLocalX;
                sparkPoint.localPosition = local;
            }

            PrefabUtility.SaveAsPrefabAsset(root, VehiclePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
