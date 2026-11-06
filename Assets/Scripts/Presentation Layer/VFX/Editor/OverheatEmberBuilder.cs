#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 4스테이지 불씨 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_OverheatEmber.mat: 정점 색(RGB) + 정점 알파(발광 세기)를 그리는 BrandWrapGlow 머티리얼. 바닥 붉은 빛은 모닥불의 가산 머티리얼을 같이 쓴다.
///  2) VFX_OverheatEmberAcquire / VFX_OverheatEmberExpire.prefab: 스텔라 범위 프리팹의 풀 규약 구조(루트 ParticleSystem + 메쉬 자식)를 복제해
///     VFX_OverheatEmberOneShot으로 교체
///  3) OverheatEmber.prefab: Visual 아래에 EmberOrb(VFX_OverheatEmber) > EmberGlow(바닥 빛), EmberTrail(드랍 빛 꼬리)을 붙이고
///     Visual의 임시 마름모 그림을 지운다(SpriteRenderer는 정렬 기준이라 남긴다)
///  5) M_OverheatEmberTrail.mat: 드랍 빛 꼬리용 EmberTrailAdditive 머티리얼(_Intensity 1)
///  4) GameInstaller.prefab: EmberItemController의 VFXComponent 풀에 두 태그를 등록하고 획득·소멸 태그 칸을 채운다(프리팹 값만, 코드 수정 없음)
/// </summary>
public static class OverheatEmberBuilder
{
    private const string MaterialPath = "Assets/Graphics/VFX/Materials/M_OverheatEmber.mat";
    private const string GlowMaterialPath = "Assets/Graphics/VFX/Materials/M_CampfireGroundGlow.mat";
    private const string ShaderName = "Custom/VFX/BrandWrapGlow";
    private const string TrailMaterialPath = "Assets/Graphics/VFX/Materials/M_OverheatEmberTrail.mat";
    private const string TrailShaderName = "Custom/VFX/EmberTrailAdditive";
    private const string SourcePrefabPath = "Assets/Prefabs/VFX/Roguelike/VFX_StellaRange.prefab";
    private const string PrefabFolder = "Assets/Prefabs/VFX/Item/OverheatEmber";
    private const string AcquirePrefabPath = PrefabFolder + "/VFX_OverheatEmberAcquire.prefab";
    private const string ExpirePrefabPath = PrefabFolder + "/VFX_OverheatEmberExpire.prefab";
    private const string EmberPrefabPath = "Assets/Prefabs/Objects/Item/OverheatEmber.prefab";
    private const string InstallerPrefabPath = "Assets/Prefabs/Installer/Installer/GameInstaller.prefab";

    private const string AcquireTag = "OverheatEmberAcquire";
    private const string ExpireTag = "OverheatEmberExpire";
    private const string ControllerTypeName = "EmberItemController";

    // 루트 ParticleSystem 수명은 이펙트 전체 길이보다 길어야 풀이 너무 일찍 회수하지 않는다
    private const float RootLifetimeMargin = 0.4f;

    [MenuItem("LumberBoy/VFX/Build Overheat Ember")]
    public static void Build()
    {
        Material material = BuildMaterial();
        Material glowMaterial = AssetDatabase.LoadAssetAtPath<Material>(GlowMaterialPath);
        Material trailMaterial = BuildTrailMaterial();
        if (null == material || null == glowMaterial || null == trailMaterial)
        {
            Debug.LogError("[OverheatEmberBuilder] 머티리얼을 준비하지 못했습니다: " + MaterialPath + " / " + GlowMaterialPath + " / " + TrailMaterialPath);
            return;
        }

        EnsureFolder();
        ParticleSystem acquire = BuildOneShotPrefab(AcquirePrefabPath, "VFX_OverheatEmberAcquire", VFX_OverheatEmberOneShot.EKind.Acquire, material);
        ParticleSystem expire = BuildOneShotPrefab(ExpirePrefabPath, "VFX_OverheatEmberExpire", VFX_OverheatEmberOneShot.EKind.Expire, material);
        if (null == acquire || null == expire) return;

        AssetDatabase.SaveAssets();
        if (false == WireEmberPrefab(material, glowMaterial, trailMaterial)) return;
        if (false == WireInstaller(acquire, expire)) return;

        AssetDatabase.SaveAssets();
        Debug.Log("[OverheatEmberBuilder] 완료: " + MaterialPath + " / " + AcquirePrefabPath + " / " + ExpirePrefabPath + " / " + EmberPrefabPath + " / " + InstallerPrefabPath);
    }

    private static Material BuildMaterial()
    {
        Shader shader = Shader.Find(ShaderName);
        if (null == shader)
        {
            Debug.LogError("[OverheatEmberBuilder] 셰이더를 찾지 못했습니다: " + ShaderName);
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

        // 몸통은 정점 알파 0(발광 1배)이라 색 단계가 그대로 남고, 불티·번쩍임만 블룸을 받는다
        material.SetFloat("_GlowMin", 1.0f);
        material.SetFloat("_GlowMax", 2.4f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material BuildTrailMaterial()
    {
        Shader shader = Shader.Find(TrailShaderName);
        if (null == shader)
        {
            Debug.LogError("[OverheatEmberBuilder] 셰이더를 찾지 못했습니다: " + TrailShaderName);
            return null;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(TrailMaterialPath);
        if (null == material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, TrailMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        // 정점 알파 1 = 세기 1. 시안의 원판 세기(최대 0.7)를 그대로 정점 알파로 넣는다
        material.SetFloat("_Intensity", 1.0f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureFolder()
    {
        if (true == AssetDatabase.IsValidFolder(PrefabFolder)) return;
        AssetDatabase.CreateFolder("Assets/Prefabs/VFX/Item", "OverheatEmber");
    }

    private static ParticleSystem BuildOneShotPrefab(string _path, string _name, VFX_OverheatEmberOneShot.EKind _kind, Material _material)
    {
        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(_path))
        {
            if (false == AssetDatabase.CopyAsset(SourcePrefabPath, _path))
            {
                Debug.LogError("[OverheatEmberBuilder] 프리팹 복제 실패: " + SourcePrefabPath + " -> " + _path);
                return null;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(_path);

        try
        {
            root.name = _name;

            // 메쉬 자식(원래 RangeMesh): 스텔라 범위 컴포넌트를 제거하고 불씨 단발 컴포넌트를 붙인다
            VFX_StellaRange range = root.GetComponentInChildren<VFX_StellaRange>(true);
            VFX_OverheatEmberOneShot effect = root.GetComponentInChildren<VFX_OverheatEmberOneShot>(true);
            Transform meshTransform = null != effect ? effect.transform : (null != range ? range.transform : null);

            if (null == meshTransform)
            {
                Debug.LogError("[OverheatEmberBuilder] 메쉬 자식을 찾지 못했습니다: " + _path);
                return null;
            }

            if (null != range) Object.DestroyImmediate(range, true);

            meshTransform.name = "EmberMesh";
            if (null == effect) effect = meshTransform.gameObject.AddComponent<VFX_OverheatEmberOneShot>();

            meshTransform.GetComponent<MeshRenderer>().sharedMaterial = _material;

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("kind").enumValueIndex = (int)_kind;
            so.FindProperty("glowScale").floatValue = 1.0f;
            so.FindProperty("sortingOrderOffset").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 풀 규약: 루트 ParticleSystem이 이펙트보다 오래 살아 있다가 끝나야 한다
            ParticleSystem ps = root.GetComponent<ParticleSystem>();
            if (null != ps)
            {
                float lifetime = VFX_OverheatEmberOneShot.DurationOf(_kind) + RootLifetimeMargin;
                ParticleSystem.MainModule main = ps.main;
                main.duration = lifetime;
                main.startLifetime = lifetime;
            }

            PrefabUtility.SaveAsPrefabAsset(root, _path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return AssetDatabase.LoadAssetAtPath<GameObject>(_path).GetComponent<ParticleSystem>();
    }

    private static bool WireEmberPrefab(Material _material, Material _glowMaterial, Material _trailMaterial)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(EmberPrefabPath);

        try
        {
            SpriteRenderer sprite = root.GetComponentInChildren<SpriteRenderer>(true);
            if (null == sprite)
            {
                Debug.LogError("[OverheatEmberBuilder] 불씨 프리팹에서 Visual SpriteRenderer를 찾지 못했습니다.");
                return false;
            }

            // 임시 마름모 그림은 지우고 정렬 기준으로만 남긴다
            sprite.sprite = null;

            Transform orbTransform = FindOrCreateChild(sprite.transform, "EmberOrb");
            MeshRenderer orbRenderer = EnsureMeshComponents(orbTransform.gameObject);
            orbRenderer.sharedMaterial = _material;

            Transform glowTransform = FindOrCreateChild(orbTransform, "EmberGlow");
            MeshRenderer glowRenderer = EnsureMeshComponents(glowTransform.gameObject);
            glowRenderer.sharedMaterial = _glowMaterial;

            Transform trailTransform = FindOrCreateChild(orbTransform, "EmberTrail");
            MeshRenderer trailRenderer = EnsureMeshComponents(trailTransform.gameObject);
            trailRenderer.sharedMaterial = _trailMaterial;

            VFX_OverheatEmber effect = orbTransform.GetComponent<VFX_OverheatEmber>();
            if (null == effect) effect = orbTransform.gameObject.AddComponent<VFX_OverheatEmber>();

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("groundAnchor").objectReferenceValue = root.transform;
            so.FindProperty("sortingSource").objectReferenceValue = sprite;
            so.FindProperty("glowRenderer").objectReferenceValue = glowRenderer;
            so.FindProperty("trailRenderer").objectReferenceValue = trailRenderer;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, EmberPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return true;
    }

    private static Transform FindOrCreateChild(Transform _parent, string _name)
    {
        Transform child = _parent.Find(_name);
        if (null == child)
        {
            child = new GameObject(_name).transform;
            child.SetParent(_parent, false);
        }

        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        child.gameObject.layer = _parent.gameObject.layer;
        return child;
    }

    private static MeshRenderer EnsureMeshComponents(GameObject _go)
    {
        if (null == _go.GetComponent<MeshFilter>()) _go.AddComponent<MeshFilter>();

        MeshRenderer renderer = _go.GetComponent<MeshRenderer>();
        if (null == renderer) renderer = _go.AddComponent<MeshRenderer>();

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private static bool WireInstaller(ParticleSystem _acquire, ParticleSystem _expire)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(InstallerPrefabPath);

        try
        {
            MonoBehaviour controller = null;
            MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (null != behaviours[i] && ControllerTypeName == behaviours[i].GetType().Name)
                {
                    controller = behaviours[i];
                    break;
                }
            }

            if (null == controller)
            {
                Debug.LogError("[OverheatEmberBuilder] GameInstaller에서 " + ControllerTypeName + "를 찾지 못했습니다.");
                return false;
            }

            VFXComponent vfx = controller.GetComponent<VFXComponent>();
            if (null == vfx)
            {
                Debug.LogError("[OverheatEmberBuilder] " + ControllerTypeName + " 오브젝트에 VFXComponent가 없습니다.");
                return false;
            }

            SerializedObject vfxSo = new SerializedObject(vfx);
            SerializedProperty list = vfxSo.FindProperty("vfxPoolDataList");
            SetPoolEntry(list, AcquireTag, _acquire, 4, 16);
            SetPoolEntry(list, ExpireTag, _expire, 2, 8);
            vfxSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerSo = new SerializedObject(controller);
            controllerSo.FindProperty("acquireVfxTag").stringValue = AcquireTag;
            controllerSo.FindProperty("expireVfxTag").stringValue = ExpireTag;
            controllerSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, InstallerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return true;
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
}
#endif
