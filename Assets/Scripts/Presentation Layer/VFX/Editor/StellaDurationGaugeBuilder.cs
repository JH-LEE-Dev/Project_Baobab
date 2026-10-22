#if UNITY_EDITOR
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 스텔라 유지시간 게이지 프리팹(VFX_StellaDurationGauge)을 만든다. 범위 링 프리팹(VFX_StellaRange)을 복제해
/// 루트 ParticleSystem(풀 수명 타이머), 자식 메쉬, 머티리얼을 그대로 쓰고 자식의 스크립트만 게이지로 바꾼다. 여러 번 실행해도 같은 결과로 덮어쓴다.
/// </summary>
public static class StellaDurationGaugeBuilder
{
    private const string SourcePath = "Assets/Prefabs/VFX/Roguelike/VFX_StellaRange.prefab";
    private const string TargetPath = "Assets/Prefabs/VFX/Roguelike/VFX_StellaDurationGauge.prefab";

    [MenuItem("LumberBoy/VFX/Build Stella Duration Gauge")]
    public static void Build()
    {
        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath))
        {
            Debug.LogError("[StellaDurationGaugeBuilder] 원본 프리팹을 찾지 못했습니다: " + SourcePath);
            return;
        }

        AssetDatabase.DeleteAsset(TargetPath);

        if (false == AssetDatabase.CopyAsset(SourcePath, TargetPath))
        {
            Debug.LogError("[StellaDurationGaugeBuilder] 프리팹 복제에 실패했습니다.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(TargetPath);

        try
        {
            root.name = "VFX_StellaDurationGauge";

            VFX_StellaRange range = root.GetComponentInChildren<VFX_StellaRange>(true);
            if (null == range)
            {
                Debug.LogError("[StellaDurationGaugeBuilder] 복제한 프리팹에서 VFX_StellaRange를 찾지 못했습니다.");
                return;
            }

            GameObject meshObject = range.gameObject;
            Object.DestroyImmediate(range, true);
            meshObject.name = "GaugeMesh";

            VFX_StellaDurationGauge gauge = meshObject.AddComponent<VFX_StellaDurationGauge>();
            SerializedObject so = new SerializedObject(gauge);
            so.FindProperty("sortingLayerName").stringValue = "FlyingItem";
            so.FindProperty("sortingOrder").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();

            MeshRenderer renderer = meshObject.GetComponent<MeshRenderer>();
            if (null != renderer)
            {
                renderer.sortingLayerName = "FlyingItem";
                renderer.sortingOrder = 0;
            }

            PrefabUtility.SaveAsPrefabAsset(root, TargetPath);
            Debug.Log("[StellaDurationGaugeBuilder] 완료: " + TargetPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
