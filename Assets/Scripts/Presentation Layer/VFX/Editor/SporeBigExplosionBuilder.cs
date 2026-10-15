#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 포자 대폭발 이펙트 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) M_SporeBigExplosion.mat: 열기 분출 머티리얼(BrandWrapGlow)을 복제하고 발광 범위를 낮춤(GlowMin 1.0 / GlowMax 2.5)
///  2) VFX_SporeBigExplosion.prefab: 별자리 노바 프리팹의 풀 규약 구조(루트 ParticleSystem + 공중/바닥 메쉬)를 복제해
///     VFX_SporeBigExplosion 컴포넌트로 교체
/// </summary>
public static class SporeBigExplosionBuilder
{
    private const string SourceMaterialPath = "Assets/Graphics/VFX/Materials/M_TreeHeatBurst.mat";
    private const string MaterialPath = "Assets/Graphics/VFX/Materials/M_SporeBigExplosion.mat";
    private const string SourcePrefabPath = "Assets/Prefabs/VFX/Constellation/VFX_ConstellationNova.prefab";
    private const string PrefabPath = "Assets/Prefabs/VFX/Spore/VFX_SporeBigExplosion.prefab";
    private const string PuffSourcePrefabPath = "Assets/Prefabs/VFX/Spore/SporeExplosionVFX.prefab";
    private const int PuffCount = 5;
    private const int SmokeCount = 2;
    private const string SmokeSheetPathFormat = "Assets/Graphics/VFX/Resources/Spore/SporeSmoke_Tier{0}.png";
    private const string TierSheetPathFormat = "Assets/Graphics/VFX/Resources/Spore/SporeExplosion_Tier{0}.png";

    // 루트 ParticleSystem 수명은 이펙트 전체 길이보다 길어야 풀이 너무 일찍 회수하지 않는다
    private const float RootLifetime = 1.6f;
    private const float GlowMin = 1.0f;
    private const float GlowMax = 3.0f;
    private const string PuffMaterialPath = "Assets/Graphics/VFX/Materials/M_SporeBigExplosionPuff.mat";
    private const string PuffShaderName = "Custom/VFX/SpriteGlow";
    private const float PuffGlow = 1.4f;

    [MenuItem("LumberBoy/VFX/Build Spore Big Explosion")]
    public static void Build()
    {
        Material material = BuildMaterial();
        if (null == material) return;

        BuildPrefab(material);
        AssetDatabase.SaveAssets();
        Debug.Log("[SporeBigExplosionBuilder] 완료: " + MaterialPath + " / " + PrefabPath);
    }

    private static Material BuildMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (null == material)
        {
            if (false == AssetDatabase.CopyAsset(SourceMaterialPath, MaterialPath))
            {
                Debug.LogError("[SporeBigExplosionBuilder] 머티리얼 복제 실패: " + SourceMaterialPath);
                return null;
            }

            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }

        // 옥색이 흰색으로 날아가지 않도록 발광 범위를 낮춘다(열기 분출은 1.3 ~ 4.2). 정점 알파 0이면 색 그대로(x1.0).
        material.SetFloat("_GlowMin", GlowMin);
        material.SetFloat("_GlowMax", GlowMax);
        EditorUtility.SetDirty(material);
        return material;
    }

    // 연막용 HDR 스프라이트 머티리얼: 원본 스프라이트 색 그대로 배율(_Glow)만 곱해 블룸을 받게 한다.
    // 셰이더를 못 찾으면 원본 머티리얼을 그대로 쓴다(블룸 없음).
    private static Material BuildPuffMaterial(Material _fallback)
    {
        Shader shader = Shader.Find(PuffShaderName);
        if (null == shader)
        {
            Debug.LogWarning("[SporeBigExplosionBuilder] " + PuffShaderName + " 셰이더를 찾지 못해 원본 연막 머티리얼을 씁니다.");
            return _fallback;
        }

        Material material = AssetDatabase.LoadAssetAtPath<Material>(PuffMaterialPath);
        if (null == material)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, PuffMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetFloat("_Glow", PuffGlow);
        EditorUtility.SetDirty(material);
        return material;
    }

    // 연막: 기존 포자 폭발(SporeExplosionVFX)의 스프라이트 프레임을 그대로 재생하는 SpriteRenderer 자식 N개를 만든다.
    private static void BuildPuffs(Transform _root, SerializedObject _effect)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(PuffSourcePrefabPath);
        if (null == source)
        {
            Debug.LogError("[SporeBigExplosionBuilder] 연막 원본 프리팹을 찾지 못했습니다: " + PuffSourcePrefabPath);
            return;
        }

        SporeExplosionVFX sourceEffect = source.GetComponent<SporeExplosionVFX>();
        SpriteRenderer sourceRenderer = source.GetComponent<SpriteRenderer>();
        if (null == sourceEffect || null == sourceRenderer)
        {
            Debug.LogError("[SporeBigExplosionBuilder] 연막 원본에서 SporeExplosionVFX/SpriteRenderer를 찾지 못했습니다.");
            return;
        }

        Material puffMaterial = BuildPuffMaterial(sourceRenderer.sharedMaterial);

        SerializedObject sourceSo = new SerializedObject(sourceEffect);
        SerializedProperty sourceFrames = sourceSo.FindProperty("frames");

        SerializedProperty frames = _effect.FindProperty("puffFrames");
        frames.arraySize = sourceFrames.arraySize;
        for (int i = 0; i < sourceFrames.arraySize; i++)
        {
            frames.GetArrayElementAtIndex(i).objectReferenceValue = sourceFrames.GetArrayElementAtIndex(i).objectReferenceValue;
        }

        // 단계별 연막 프레임(SporeExplosion_Tier1~3.png를 같은 순서로 슬라이스한 스프라이트)
        for (int tier = 1; tier <= 3; tier++)
        {
            string sheetPath = TierSheetPathFormat.Replace("{0}", tier.ToString());
            UnityEngine.Object[] sub = AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath);
            SerializedProperty tierFrames = _effect.FindProperty("puffFramesTier" + tier);
            tierFrames.arraySize = sourceFrames.arraySize;

            for (int i = 0; i < sourceFrames.arraySize; i++)
            {
                string spriteName = "SporeExplosionT" + tier + "_" + i;
                Sprite found = null;
                for (int k = 0; k < sub.Length; k++)
                {
                    if (sub[k] is Sprite sp && sp.name == spriteName)
                    {
                        found = sp;
                        break;
                    }
                }

                if (null == found) Debug.LogWarning("[SporeBigExplosionBuilder] 스프라이트를 찾지 못했습니다: " + sheetPath + " / " + spriteName);
                tierFrames.GetArrayElementAtIndex(i).objectReferenceValue = found;
            }
        }

        // 어두운 연막(스모크) 프레임: 단계 0~3(SporeSmoke_Tier0~3.png)
        for (int tier = 0; tier <= 3; tier++)
        {
            string sheetPath = SmokeSheetPathFormat.Replace("{0}", tier.ToString());
            UnityEngine.Object[] sub = AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath);
            SerializedProperty smokeFrames = _effect.FindProperty("smokeFramesTier" + tier);
            smokeFrames.arraySize = sourceFrames.arraySize;

            for (int i = 0; i < sourceFrames.arraySize; i++)
            {
                string spriteName = "SporeSmokeT" + tier + "_" + i;
                Sprite found = null;
                for (int k = 0; k < sub.Length; k++)
                {
                    if (sub[k] is Sprite sp && sp.name == spriteName)
                    {
                        found = sp;
                        break;
                    }
                }

                if (null == found) Debug.LogWarning("[SporeBigExplosionBuilder] 스모크 스프라이트를 찾지 못했습니다: " + sheetPath + " / " + spriteName);
                smokeFrames.GetArrayElementAtIndex(i).objectReferenceValue = found;
            }
        }

        _effect.FindProperty("smokeCount").intValue = SmokeCount;

        SerializedProperty renderers = _effect.FindProperty("puffRenderers");
        renderers.arraySize = PuffCount + SmokeCount;
        int firstSortingLayer = SortingLayer.NameToID("FlyingItem");

        for (int i = 0; i < PuffCount + SmokeCount; i++)
        {
            string childName = i < PuffCount ? "Puff_" + i : "Smoke_" + (i - PuffCount);
            Transform child = _root.Find(childName);
            if (null == child)
            {
                GameObject go = new GameObject(childName, typeof(SpriteRenderer));
                go.layer = _root.gameObject.layer;
                child = go.transform;
                child.SetParent(_root, false);
            }

            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = puffMaterial;
            sr.sprite = sourceFrames.arraySize > 1 ? sourceFrames.GetArrayElementAtIndex(1).objectReferenceValue as Sprite : null;
            sr.sortingLayerID = firstSortingLayer;
            sr.sortingOrder = 9;
            sr.enabled = false;

            renderers.GetArrayElementAtIndex(i).objectReferenceValue = sr;
        }
    }

    private static void BuildPrefab(Material _material)
    {
        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
        {
            if (false == AssetDatabase.CopyAsset(SourcePrefabPath, PrefabPath))
            {
                Debug.LogError("[SporeBigExplosionBuilder] 프리팹 복제 실패: " + SourcePrefabPath);
                return;
            }
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            root.name = "VFX_SporeBigExplosion";

            // 공중 메쉬 자식(원래 NovaMesh): 별자리 노바 컴포넌트를 제거하고 포자 대폭발 컴포넌트를 붙인다
            VFX_ConstellationNova nova = root.GetComponentInChildren<VFX_ConstellationNova>(true);
            Transform skyMeshTransform = null != nova ? nova.transform : root.transform.Find("SporeMesh");

            if (null == skyMeshTransform)
            {
                Debug.LogError("[SporeBigExplosionBuilder] 공중 메쉬 자식을 찾지 못했습니다.");
                return;
            }

            if (null != nova) Object.DestroyImmediate(nova);

            skyMeshTransform.name = "SporeMesh";
            VFX_SporeBigExplosion effect = skyMeshTransform.GetComponent<VFX_SporeBigExplosion>();
            if (null == effect) effect = skyMeshTransform.gameObject.AddComponent<VFX_SporeBigExplosion>();

            Transform groundTransform = root.transform.Find("GroundMesh");
            if (null == groundTransform)
            {
                Debug.LogError("[SporeBigExplosionBuilder] GroundMesh 자식을 찾지 못했습니다.");
                return;
            }

            MeshRenderer skyRenderer = skyMeshTransform.GetComponent<MeshRenderer>();
            MeshRenderer groundRenderer = groundTransform.GetComponent<MeshRenderer>();
            skyRenderer.sharedMaterial = _material;
            groundRenderer.sharedMaterial = _material;

            SerializedObject so = new SerializedObject(effect);
            so.FindProperty("groundMeshFilter").objectReferenceValue = groundTransform.GetComponent<MeshFilter>();
            so.FindProperty("groundMeshRenderer").objectReferenceValue = groundRenderer;
            so.FindProperty("groundSortingLayerName").stringValue = "ShockWave";
            so.FindProperty("groundSortingOrder").intValue = 0;
            so.FindProperty("rangeRadius").floatValue = 2.5f;
            so.FindProperty("density").floatValue = 1.0f;
            so.FindProperty("sortingOrderOffset").intValue = 0;
            so.FindProperty("puffCount").intValue = PuffCount;
            so.FindProperty("puffHeightPx").floatValue = 18.0f;

            BuildPuffs(root.transform, so);
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
