#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

public class LumberjackPrefabBuilder
{
    [MenuItem("Tools/Create Lumberjack NPC Prefab")]
    public static void CreatePrefab()
    {
        string characterPrefabPath = "Assets/Prefabs/Objects/Character/Character.prefab";
        string newPrefabDir = "Assets/Prefabs/Objects/NPC";
        string newPrefabPath = newPrefabDir + "/LumberjackNPC.prefab";

        if (!System.IO.Directory.Exists(newPrefabDir))
        {
            System.IO.Directory.CreateDirectory(newPrefabDir);
        }

        // 1. Load Original
        GameObject characterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(characterPrefabPath);
        if (characterPrefab == null)
        {
            Debug.LogError("Character prefab not found at " + characterPrefabPath);
            return;
        }

        // 2. Instantiate and unpack
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        instance.name = "LumberjackNPC";

        // 3. Remove unwanted components from root
        RemoveComponent<Character>(instance);
        RemoveComponent<InputManager>(instance);
        RemoveComponent<Rigidbody2D>(instance);
        RemoveComponent<CircleCollider2D>(instance);

        // Remove AttackComponent GameObject completely (including RadiusIndicator)
        foreach (var attack in instance.GetComponentsInChildren<AttackComponent>(true)) 
        {
            Object.DestroyImmediate(attack.gameObject);
        }
        foreach (var health in instance.GetComponentsInChildren<PHealthComponent>(true)) Object.DestroyImmediate(health);
        foreach (var stat in instance.GetComponentsInChildren<StatComponent>(true)) Object.DestroyImmediate(stat);
        
        // Remove old ArmComponent but keep the GameObject and AxeComponent
        var oldArm = instance.GetComponentInChildren<ArmComponent>(true);
        GameObject armObj = oldArm != null ? oldArm.gameObject : null;

        // 기존 ArmComponent에서 튜닝된 값을 가져와 새 컴포넌트에 그대로 이어받음
        float oldSmoothSpeed = 10f;
        float oldMaxYOffset = 0.15f;
        if (oldArm != null)
        {
            SerializedObject soOldArm = new SerializedObject(oldArm);
            oldSmoothSpeed = soOldArm.FindProperty("smoothSpeed").floatValue;
            oldMaxYOffset = soOldArm.FindProperty("maxYOffset").floatValue;
        }

        if (oldArm != null) Object.DestroyImmediate(oldArm);

        // Remove old ArmAnimTrigger
        var armTriggers = instance.GetComponentsInChildren<ArmAnimTrigger>(true);
        foreach (var t in armTriggers) Object.DestroyImmediate(t);

        // 4. Add new components
        LumberjackNPC npcMain = instance.AddComponent<LumberjackNPC>();
        PathFindComponent pathFind = instance.AddComponent<PathFindComponent>();
        
        // Get existing CharacterVisualComponent instead of creating LumberjackVisualComponent
        var visualComp = instance.GetComponentInChildren<CharacterVisualComponent>(true);
        if (visualComp != null)
        {
            SerializedObject so = new SerializedObject(npcMain);
            so.FindProperty("visualComponent").objectReferenceValue = visualComp;
            so.ApplyModifiedProperties();
        }

        if (armObj != null)
        {
            // Remove Player-specific AxeComponent
            var axeComp = armObj.GetComponentInChildren<AxeComponent>(true);
            if (axeComp != null) Object.DestroyImmediate(axeComp);

            LumberjackArmComponent newArmComp = armObj.AddComponent<LumberjackArmComponent>();

            var axeAnim = armObj.GetComponentInChildren<AxeAnimation>(true);
            var axeSR = axeAnim != null ? axeAnim.GetComponent<SpriteRenderer>() : null;

            SerializedObject soArm = new SerializedObject(newArmComp);
            soArm.FindProperty("axeAnimation").objectReferenceValue = axeAnim;
            soArm.FindProperty("axeSpriteRenderer").objectReferenceValue = axeSR;
            soArm.FindProperty("smoothSpeed").floatValue = oldSmoothSpeed;
            soArm.FindProperty("maxYOffset").floatValue = oldMaxYOffset;
            soArm.ApplyModifiedProperties();

            SerializedObject so = new SerializedObject(npcMain);
            so.FindProperty("armComponent").objectReferenceValue = newArmComp;
            so.ApplyModifiedProperties();
            
            // Disable RifleComponent if it exists
            //var rifle = armObj.GetComponentInChildren<RifleComponent>(true);
            //if (rifle != null) rifle.gameObject.SetActive(false);
        }

        // 5. Save as new Prefab
        PrefabUtility.SaveAsPrefabAsset(instance, newPrefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log("Lumberjack NPC Prefab created successfully at " + newPrefabPath);
    }

    private static void RemoveComponent<T>(GameObject obj) where T : Component
    {
        T comp = obj.GetComponent<T>();
        if (comp != null)
        {
            Object.DestroyImmediate(comp);
        }
    }
}
#endif
