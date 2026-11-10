#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 3스테이지 배리어 연출 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_TreeBarrier.mat: 껍질 셰이더(Custom/VFX/TreeBarrier). 껍질 모양 값은 VFX_TreeBarrier 상수와 같게 맞춘다.
///  2) M_TreeBarrierParticle.mat: 스파크/파편용 BrandWrapGlow(GlowMin 1.2 / GlowMax 6, 수리 스파크와 같은 기준)
///  3) VFX_TreeBarrierSpark.prefab / VFX_TreeBarrierShatter.prefab: 수리 스파크 프리팹의 풀 규약 구조(루트 ParticleSystem + 메쉬 자식)를 복제해 컴포넌트를 바꾼다
///  4) Tree.prefab: BaseVisual(SortingGroup) 아래에 비활성 자식 VFX_TreeBarrier를 두고 TreeObj.barrierVfx에 연결한다
///  5) GameInstaller.prefab: InDungeonVFXManager의 VFXComponent 풀 목록에 두 태그를 등록한다
/// </summary>
public static class TreeBarrierBuilder
{
    private const string ShellMaterialPath = "Assets/Graphics/VFX/Materials/M_TreeBarrier.mat";
    private const string ParticleMaterialPath = "Assets/Graphics/VFX/Materials/M_TreeBarrierParticle.mat";
    private const string ShellShaderName = "Custom/VFX/TreeBarrier";
    private const string ParticleShaderName = "Custom/VFX/BrandWrapGlow";

    private const string SourcePrefabPath = "Assets/Prefabs/VFX/Repair/VFX_RepairSpark.prefab";
    private const string PrefabFolder = "Assets/Prefabs/VFX/Barrier";
    private const string SparkPrefabPath = PrefabFolder + "/VFX_TreeBarrierSpark.prefab";
    private const string ShatterPrefabPath = PrefabFolder + "/VFX_TreeBarrierShatter.prefab";
    private const string TreePrefabPath = "Assets/Prefabs/Objects/Trees/Tree.prefab";
    private const string InstallerPrefabPath = "Assets/Prefabs/Installer/Installer/GameInstaller.prefab";

    public const string SparkTag = "TreeBarrierSparkEffect";
    public const string ShatterTag = "TreeBarrierShatterEffect";

    private const string BarrierChildName = "VFX_TreeBarrier";

    [MenuItem("LumberBoy/VFX/Build Tree Barrier")]
    public static void Build()
    {
        Material shell = BuildShellMaterial();
        Material particle = BuildParticleMaterial();
        if (null == shell || null == particle) return;

        ParticleSystem spark = BuildEffectPrefab<VFX_BarrierSpark>(SparkPrefabPath, "VFX_TreeBarrierSpark", "SparkMesh", particle, 0.9f);
        ParticleSystem shatter = BuildEffectPrefab<VFX_BarrierShatter>(ShatterPrefabPath, "VFX_TreeBarrierShatter", "ShatterMesh", particle, 1.5f);
        if (null == spark || null == shatter) return;

        AssetDatabase.SaveAssets();
        WireTreePrefab(shell);
        WireInstaller(spark, shatter);
        AssetDatabase.SaveAssets();
        Debug.Log("[TreeBarrierBuilder] 완료: " + ShellMaterialPath + " / " + SparkPrefabPath + " / " + ShatterPrefabPath + " / " + TreePrefabPath + " / " + InstallerPrefabPath);
    }

    private static Material LoadOrCreateMaterial(string _path, string _shaderName)
    {
        Shader shader = Shader.Find(_shaderName);
        if (null == shader)
        {
            Debug.LogError("[TreeBarrierBuilder] 셰이더를 찾지 못했습니다: " + _shaderName);
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

        return material;
    }

    private static Material BuildShellMaterial()
    {
        Material material = LoadOrCreateMaterial(ShellMaterialPath, ShellShaderName);
        if (null == material) return null;

        material.SetFloat("_CenterY", VFX_TreeBarrier.ShellCenterYPx);
        material.SetFloat("_RadiusX", VFX_TreeBarrier.ShellRadiusXPx);
        material.SetFloat("_RadiusY", VFX_TreeBarrier.ShellRadiusYPx);
        material.SetFloat("_HitAge", 10.0f);
        material.SetFloat("_BreakAge", -1.0f);
        material.enableInstancing = false;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material BuildParticleMaterial()
    {
        Material material = LoadOrCreateMaterial(ParticleMaterialPath, ParticleShaderName);
        if (null == material) return null;

        material.SetFloat("_GlowMin", 1.2f);
        material.SetFloat("_GlowMax", 6.0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static ParticleSystem BuildEffectPrefab<T>(string _path, string _rootName, string _meshName, Material _material, float _rootLifetime) where T : MonoBehaviour
    {
        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(_path))
        {
            if (false == AssetDatabase.IsValidFolder(PrefabFolder))
            {
                AssetDatabase.CreateFolder("Assets/Prefabs/VFX", "Barrier");
            }

            if (false == AssetDatabase.CopyAsset(SourcePrefabPath, _path))
            {
                Debug.LogError("[TreeBarrierBuilder] 프리팹 복제 실패: " + SourcePrefabPath);
                return null;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(_path);

        try
        {
            root.name = _rootName;

            VFX_RepairSpark source = root.GetComponentInChildren<VFX_RepairSpark>(true);
            T effect = root.GetComponentInChildren<T>(true);
            Transform meshTransform = null != effect ? effect.transform : (null != source ? source.transform : null);
            if (null == meshTransform)
            {
                Debug.LogError("[TreeBarrierBuilder] 메쉬 자식을 찾지 못했습니다: " + _path);
                return null;
            }

            if (null != source) Object.DestroyImmediate(source, true);

            meshTransform.name = _meshName;
            if (null == effect) effect = meshTransform.gameObject.AddComponent<T>();

            meshTransform.GetComponent<MeshRenderer>().sharedMaterial = _material;

            ParticleSystem ps = root.GetComponent<ParticleSystem>();
            if (null != ps)
            {
                ParticleSystem.MainModule main = ps.main;
                main.duration = _rootLifetime;
                main.startLifetime = _rootLifetime;
            }

            PrefabUtility.SaveAsPrefabAsset(root, _path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(_path).GetComponent<ParticleSystem>();
    }

    private static void WireTreePrefab(Material _shellMaterial)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(TreePrefabPath);

        try
        {
            TreeObj tree = root.GetComponent<TreeObj>();
            Transform baseVisual = FindChildRecursive(root.transform, "BaseVisual");
            if (null == tree || null == baseVisual)
            {
                Debug.LogError("[TreeBarrierBuilder] Tree.prefab에서 TreeObj 또는 BaseVisual을 찾지 못했습니다.");
                return;
            }

            Transform child = baseVisual.Find(BarrierChildName);
            if (null == child)
            {
                child = new GameObject(BarrierChildName).transform;
                child.SetParent(baseVisual, false);
            }

            child.localPosition = Vector3.zero;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;

            if (null == child.GetComponent<MeshFilter>()) child.gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            if (null == renderer) renderer = child.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _shellMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingLayerName = "Objects";

            VFX_TreeBarrier barrier = child.GetComponent<VFX_TreeBarrier>();
            if (null == barrier) barrier = child.gameObject.AddComponent<VFX_TreeBarrier>();
            barrier.enabled = false;
            child.gameObject.SetActive(false);

            SerializedObject treeSo = new SerializedObject(tree);
            treeSo.FindProperty("barrierVfx").objectReferenceValue = barrier;
            treeSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, TreePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void WireInstaller(ParticleSystem _spark, ParticleSystem _shatter)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(InstallerPrefabPath);

        try
        {
            InDungeonVFXManager manager = root.GetComponentInChildren<InDungeonVFXManager>(true);
            if (null == manager)
            {
                Debug.LogError("[TreeBarrierBuilder] GameInstaller에서 InDungeonVFXManager를 찾지 못했습니다.");
                return;
            }

            VFXComponent vfx = new SerializedObject(manager).FindProperty("vfxComponent").objectReferenceValue as VFXComponent;
            if (null == vfx)
            {
                Debug.LogError("[TreeBarrierBuilder] InDungeonVFXManager.vfxComponent가 비어 있습니다.");
                return;
            }

            SerializedObject vfxSo = new SerializedObject(vfx);
            SerializedProperty list = vfxSo.FindProperty("vfxPoolDataList");
            // 스파크는 충격파 한 번에 여러 그루가 막을 수 있어 넉넉히 둔다(부족하면 풀이 늘어난다).
            SetPoolEntry(list, SparkTag, _spark, 6, 24);
            SetPoolEntry(list, ShatterTag, _shatter, 4, 16);
            vfxSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, InstallerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void SetPoolEntry(SerializedProperty _list, string _tag, ParticleSystem _prefab, int _initial, int _max)
    {
        SerializedProperty entry = null;
        for (int i = 0; i < _list.arraySize; i++)
        {
            SerializedProperty candidate = _list.GetArrayElementAtIndex(i);
            if (_tag == candidate.FindPropertyRelative("vfxTag").stringValue)
            {
                entry = candidate;
                break;
            }
        }

        if (null == entry)
        {
            _list.InsertArrayElementAtIndex(_list.arraySize);
            entry = _list.GetArrayElementAtIndex(_list.arraySize - 1);
        }

        entry.FindPropertyRelative("vfxTag").stringValue = _tag;
        entry.FindPropertyRelative("effectPrefab").objectReferenceValue = _prefab;
        entry.FindPropertyRelative("initialPoolSize").intValue = _initial;
        entry.FindPropertyRelative("allowDynamicExpansion").boolValue = true;
        entry.FindPropertyRelative("maxPoolSize").intValue = _max;
        entry.FindPropertyRelative("uiParticleScale").floatValue = 0.0f;
    }

    private static Transform FindChildRecursive(Transform _parent, string _name)
    {
        for (int i = 0; i < _parent.childCount; i++)
        {
            Transform child = _parent.GetChild(i);
            if (_name == child.name) return child;

            Transform found = FindChildRecursive(child, _name);
            if (null != found) return found;
        }

        return null;
    }
}
#endif
