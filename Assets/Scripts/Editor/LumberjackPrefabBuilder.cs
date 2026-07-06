#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class LumberjackPrefabBuilder
{
    // 방향별 Workman 스프라이트 시트 경로와, CharacterAnimator 필드 이름에 쓰이는 방향 접미사.
    // 각 시트는 4열x3행(12프레임)이며, 팔이 안 보이는 맨 아랫줄(인덱스 8~11)만 사용한다.
    // 인덱스 11(맨 마지막)이 진짜 서있는(정지) 포즈라 Idle에 쓰고, 8~11 전체를 걷기 사이클(Run)로 쓴다.
    private static readonly (string spriteNamePrefix, string assetPath, string animSuffix)[] WorkmanDirections = new[]
    {
        ("Workman_R_D", "Assets/Graphics/Character/Workman/Workman_R_D.png", "R"),
        ("Workm_B_D", "Assets/Graphics/Character/Workman/Workm_B_D.png", "D"),
        ("Workman_RB_D", "Assets/Graphics/Character/Workman/Workman_RB_D.png", "RD"),
        ("Workman_RT_D", "Assets/Graphics/Character/Workman/Workman_RT_D.png", "RU"),
        ("Workman_T_D", "Assets/Graphics/Character/Workman/Workman_T_D.png", "U"),
    };

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

        // 제거되기 전에 Character의 아이템 감지 레이어 값을 그대로 이어받음
        LayerMask itemLayer = default;
        Character oldCharacter = instance.GetComponent<Character>();
        if (oldCharacter != null)
        {
            SerializedObject soOldCharacter = new SerializedObject(oldCharacter);
            itemLayer = soOldCharacter.FindProperty("itemLayer").intValue;
        }

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
        LumberjackInventoryComponent inventoryComp = instance.AddComponent<LumberjackInventoryComponent>();
        // LumberjackStatComponent는 개별 NPC가 아니라 InDungeonUnitSpawner가 공용으로 들고 있다가
        // Initialize()로 주입해주므로, 여기서 프리팹에 붙이지 않는다.

        // Get existing CharacterVisualComponent instead of creating LumberjackVisualComponent
        var visualComp = instance.GetComponentInChildren<CharacterVisualComponent>(true);
        {
            SerializedObject so = new SerializedObject(npcMain);
            if (visualComp != null) so.FindProperty("visualComponent").objectReferenceValue = visualComp;
            so.FindProperty("inventoryComponent").objectReferenceValue = inventoryComp;
            so.FindProperty("itemLayer").intValue = itemLayer.value;
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

        // 5. Workman 스프라이트를 CharacterAnimator에 바인딩 (Character 프리팹을 복제해왔으므로
        // 이 시점엔 아직 플레이어 스프라이트가 물려있다 - 여기서 덮어써야 한다)
        ApplyWorkmanSprites(instance);

        // 6. Save as new Prefab
        PrefabUtility.SaveAsPrefabAsset(instance, newPrefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log("Lumberjack NPC Prefab created successfully at " + newPrefabPath);
    }

    private static void ApplyWorkmanSprites(GameObject _instance)
    {
        CharacterAnimator animator = _instance.GetComponentInChildren<CharacterAnimator>(true);
        if (animator == null)
        {
            Debug.LogWarning("LumberjackPrefabBuilder: CharacterAnimator를 찾지 못해 Workman 스프라이트를 바인딩하지 못했습니다.");
            return;
        }

        SerializedObject so = new SerializedObject(animator);

        foreach (var (spriteNamePrefix, assetPath, animSuffix) in WorkmanDirections)
        {
            Dictionary<string, Sprite> sprites = LoadSpritesByName(assetPath);

            Sprite idleFrame = GetSprite(sprites, spriteNamePrefix, 11, assetPath);
            List<Sprite> runFrames = new List<Sprite>(4);
            for (int i = 8; i <= 11; i++)
            {
                runFrames.Add(GetSprite(sprites, spriteNamePrefix, i, assetPath));
            }

            List<Sprite> idleList = idleFrame != null ? new List<Sprite> { idleFrame } : new List<Sprite>();

            SetSpriteListProperty(so, "base_Idle" + animSuffix, idleList);
            SetSpriteListProperty(so, "base_Run" + animSuffix, runFrames);
            SetSpriteListProperty(so, "InDungeon_base_Idle" + animSuffix, idleList);
            SetSpriteListProperty(so, "InDungeon_base_Run" + animSuffix, runFrames);
        }

        so.ApplyModifiedProperties();

        // 얼굴/눈깜빡임 레이어는 캐릭터 것을 그대로 사용하므로 건드리지 않는다(Face* 관련 sprite 필드 및
        // 활성화 상태는 Character 프리팹에서 복제된 그대로 유지).
    }

    private static Dictionary<string, Sprite> LoadSpritesByName(string _assetPath)
    {
        Dictionary<string, Sprite> result = new Dictionary<string, Sprite>();
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(_assetPath);
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset is Sprite sprite)
            {
                result[sprite.name] = sprite;
            }
        }
        return result;
    }

    private static Sprite GetSprite(Dictionary<string, Sprite> _sprites, string _namePrefix, int _index, string _assetPath)
    {
        string spriteName = _namePrefix + "_" + _index;
        if (_sprites.TryGetValue(spriteName, out Sprite sprite))
        {
            return sprite;
        }

        Debug.LogWarning($"LumberjackPrefabBuilder: '{spriteName}' 스프라이트를 {_assetPath}에서 찾지 못했습니다. " +
            "시트가 4x3(12프레임) 그리드로 슬라이스되어 있는지, 이름이 '<prefix>_<index>' 형식인지 확인하세요.");
        return null;
    }

    private static void SetSpriteListProperty(SerializedObject _so, string _propertyName, List<Sprite> _sprites)
    {
        SerializedProperty prop = _so.FindProperty(_propertyName);
        if (prop == null)
        {
            Debug.LogWarning($"LumberjackPrefabBuilder: CharacterAnimator에 '{_propertyName}' 필드가 없습니다.");
            return;
        }

        prop.ClearArray();
        for (int i = 0; i < _sprites.Count; i++)
        {
            prop.InsertArrayElementAtIndex(i);
            prop.GetArrayElementAtIndex(i).objectReferenceValue = _sprites[i];
        }
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
