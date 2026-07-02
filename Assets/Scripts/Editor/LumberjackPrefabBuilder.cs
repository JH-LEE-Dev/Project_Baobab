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
        RemoveComponent<CustomSortable>(instance);
        RemoveComponent<Rigidbody2D>(instance);
        RemoveComponent<CircleCollider2D>(instance);

        // Remove AttackComponent, HealthComponent, StatComponent which might be on children
        foreach (var attack in instance.GetComponentsInChildren<AttackComponent>(true)) Object.DestroyImmediate(attack);
        foreach (var health in instance.GetComponentsInChildren<PHealthComponent>(true)) Object.DestroyImmediate(health);
        foreach (var stat in instance.GetComponentsInChildren<StatComponent>(true)) Object.DestroyImmediate(stat);
        
        // Remove old ArmComponent but keep the GameObject and AxeComponent
        var oldArm = instance.GetComponentInChildren<ArmComponent>(true);
        GameObject armObj = oldArm != null ? oldArm.gameObject : null;
        if (oldArm != null) Object.DestroyImmediate(oldArm);

        // Remove old CharacterVisualComponent
        var oldVisual = instance.GetComponentInChildren<CharacterVisualComponent>(true);
        GameObject visualObj = oldVisual != null ? oldVisual.gameObject : null;
        if (oldVisual != null) Object.DestroyImmediate(oldVisual);

        // 4. Add new components
        LumberjackNPC npcMain = instance.AddComponent<LumberjackNPC>();
        PathFindComponent pathFind = instance.AddComponent<PathFindComponent>();
        
        if (visualObj != null)
        {
            LumberjackVisualComponent visComp = visualObj.AddComponent<LumberjackVisualComponent>();
            // Use serialized object to assign private fields
            SerializedObject so = new SerializedObject(npcMain);
            so.FindProperty("visualComponent").objectReferenceValue = visComp;
            so.ApplyModifiedProperties();
        }

        if (armObj != null)
        {
            LumberjackArmComponent armComp = armObj.AddComponent<LumberjackArmComponent>();
            
            // Assign AxeAnimation to armComp
            var axeAnim = armObj.GetComponentInChildren<AxeAnimation>(true);
            if (axeAnim != null)
            {
                SerializedObject soArm = new SerializedObject(armComp);
                soArm.FindProperty("axeAnimation").objectReferenceValue = axeAnim;
                // Also find SpriteRenderer on the Axe
                var sr = axeAnim.GetComponent<SpriteRenderer>();
                if (sr != null) soArm.FindProperty("axeSpriteRenderer").objectReferenceValue = sr;
                soArm.ApplyModifiedProperties();
            }

            SerializedObject so = new SerializedObject(npcMain);
            so.FindProperty("armComponent").objectReferenceValue = armComp;
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
