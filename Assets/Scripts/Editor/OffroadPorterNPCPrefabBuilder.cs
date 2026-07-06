#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class OffroadPorterNPCPrefabBuilder
{
    private const string SPRITE_DIR = "Assets/Graphics/Character/Assistant";

    [MenuItem("Tools/Create Offroad Porter NPC Prefab")]
    public static void CreatePrefab()
    {
        string characterPrefabPath = "Assets/Prefabs/Objects/Character/Character.prefab";
        string newPrefabDir = "Assets/Prefabs/Objects/NPC";
        string newPrefabPath = newPrefabDir + "/OffroadPorterNPC.prefab";

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
        instance.name = "OffroadPorterNPC";

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

        // 이 NPC는 도끼질/전투가 필요 없으므로 팔(ArmComponent) 오브젝트 자체를 통째로 제거한다.
        var oldArm = instance.GetComponentInChildren<ArmComponent>(true);
        if (oldArm != null)
        {
            Object.DestroyImmediate(oldArm.gameObject);
        }

        // Remove old ArmAnimTrigger (혹시 ArmComponent 바깥에 남아있는 경우 대비)
        var armTriggers = instance.GetComponentsInChildren<ArmAnimTrigger>(true);
        foreach (var t in armTriggers) Object.DestroyImmediate(t);

        // 4. Add new components
        OffroadPorterNPC npcMain = instance.AddComponent<OffroadPorterNPC>();
        instance.AddComponent<PathFindComponent>();
        LumberjackInventoryComponent inventoryComp = instance.AddComponent<LumberjackInventoryComponent>();

        // Get existing CharacterVisualComponent
        var visualComp = instance.GetComponentInChildren<CharacterVisualComponent>(true);
        {
            SerializedObject so = new SerializedObject(npcMain);
            if (visualComp != null) so.FindProperty("visualComponent").objectReferenceValue = visualComp;
            so.FindProperty("inventoryComponent").objectReferenceValue = inventoryComp;
            so.ApplyModifiedProperties();
        }

        // 4-1. Assistant 전용 스프라이트 적용
        ApplyAssistantSprites(instance);

        // 5. Save as new Prefab
        PrefabUtility.SaveAsPrefabAsset(instance, newPrefabPath);
        Object.DestroyImmediate(instance);

        Debug.Log("Offroad Porter NPC Prefab created successfully at " + newPrefabPath);
    }

    private static void RemoveComponent<T>(GameObject obj) where T : Component
    {
        T comp = obj.GetComponent<T>();
        if (comp != null)
        {
            Object.DestroyImmediate(comp);
        }
    }

    // Character.prefab의 12프레임 시트(Character_R_D 등)를 fileID 기준으로 직접 대조해서 확인한
    // 실제 인덱스 배치. Assistant도 동일하게 12프레임(4x3) 시트이므로 그대로 따른다.
    //   0,1,2   : Idle(마을)  -> [0,1,2,1,0] 왕복 5프레임
    //   3,4,5,6 : Run(마을)   -> [3,4,5,6] 그대로 4프레임
    //   7       : Idle(던전)  -> 정지 프레임 1개
    //   8,9,10,11: Run(던전)  -> [8,9,10,11] 그대로 4프레임

    /// <summary>
    /// Assets/Graphics/Character/Assistant의 방향별 스프라이트 시트를 CharacterAnimator에 채워 넣는다.
    /// 얼굴은 별도로 그려 넣을 예정이라 Face/Blink 오브젝트는 그대로 둔다.
    /// </summary>
    private static void ApplyAssistantSprites(GameObject _instance)
    {
        CharacterAnimator animator = _instance.GetComponentInChildren<CharacterAnimator>(true);
        if (animator == null)
        {
            Debug.LogWarning("[OffroadPorterNPCPrefabBuilder] CharacterAnimator를 찾지 못해 스프라이트를 적용하지 못했습니다.");
            return;
        }

        List<Sprite> right = LoadSpriteSheet(SPRITE_DIR + "/Assistant_R_D.png");
        List<Sprite> rightUp = LoadSpriteSheet(SPRITE_DIR + "/Assistant_RT_D.png");
        List<Sprite> rightDown = LoadSpriteSheet(SPRITE_DIR + "/Assistant_RB_D.png");
        List<Sprite> up = LoadSpriteSheet(SPRITE_DIR + "/Assistant_T_D.png");
        List<Sprite> down = LoadSpriteSheet(SPRITE_DIR + "/Assistant_B_D.png");

        SerializedObject soAnim = new SerializedObject(animator);
        AssignSpriteList(soAnim, "base_IdleR", BuildIdleFrames(right));
        AssignSpriteList(soAnim, "base_RunR", BuildRunFrames(right));
        AssignSpriteList(soAnim, "InDungeon_base_IdleR", BuildInDungeonIdleFrames(right));
        AssignSpriteList(soAnim, "InDungeon_base_RunR", BuildInDungeonRunFrames(right));

        AssignSpriteList(soAnim, "base_IdleRU", BuildIdleFrames(rightUp));
        AssignSpriteList(soAnim, "base_RunRU", BuildRunFrames(rightUp));
        AssignSpriteList(soAnim, "InDungeon_base_IdleRU", BuildInDungeonIdleFrames(rightUp));
        AssignSpriteList(soAnim, "InDungeon_base_RunRU", BuildInDungeonRunFrames(rightUp));

        AssignSpriteList(soAnim, "base_IdleRD", BuildIdleFrames(rightDown));
        AssignSpriteList(soAnim, "base_RunRD", BuildRunFrames(rightDown));
        AssignSpriteList(soAnim, "InDungeon_base_IdleRD", BuildInDungeonIdleFrames(rightDown));
        AssignSpriteList(soAnim, "InDungeon_base_RunRD", BuildInDungeonRunFrames(rightDown));

        AssignSpriteList(soAnim, "base_IdleU", BuildIdleFrames(up));
        AssignSpriteList(soAnim, "base_RunU", BuildRunFrames(up));
        AssignSpriteList(soAnim, "InDungeon_base_IdleU", BuildInDungeonIdleFrames(up));
        AssignSpriteList(soAnim, "InDungeon_base_RunU", BuildInDungeonRunFrames(up));

        AssignSpriteList(soAnim, "base_IdleD", BuildIdleFrames(down));
        AssignSpriteList(soAnim, "base_RunD", BuildRunFrames(down));
        AssignSpriteList(soAnim, "InDungeon_base_IdleD", BuildInDungeonIdleFrames(down));
        AssignSpriteList(soAnim, "InDungeon_base_RunD", BuildInDungeonRunFrames(down));
        soAnim.ApplyModifiedProperties();
    }

    private static List<Sprite> BuildIdleFrames(List<Sprite> _all)
    {
        if (_all.Count < 3) return new List<Sprite>(_all);
        return new List<Sprite> { _all[0], _all[1], _all[2], _all[1], _all[0] };
    }

    private static List<Sprite> BuildRunFrames(List<Sprite> _all)
    {
        if (_all.Count < 7) return new List<Sprite>();
        return new List<Sprite> { _all[3], _all[4], _all[5], _all[6] };
    }

    private static List<Sprite> BuildInDungeonIdleFrames(List<Sprite> _all)
    {
        if (_all.Count < 8) return new List<Sprite>();
        return new List<Sprite> { _all[7] };
    }

    private static List<Sprite> BuildInDungeonRunFrames(List<Sprite> _all)
    {
        if (_all.Count < 12) return new List<Sprite>();
        return new List<Sprite> { _all[8], _all[9], _all[10], _all[11] };
    }

    private static List<Sprite> LoadSpriteSheet(string _path)
    {
        List<Sprite> sprites = new List<Sprite>();
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(_path);
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite != null) sprites.Add(sprite);
        }

        sprites.Sort((a, b) => GetTrailingIndex(a.name).CompareTo(GetTrailingIndex(b.name)));
        return sprites;
    }

    private static int GetTrailingIndex(string _name)
    {
        int lastUnderscore = _name.LastIndexOf('_');
        if (lastUnderscore >= 0 && int.TryParse(_name.Substring(lastUnderscore + 1), out int index))
        {
            return index;
        }
        return 0;
    }

    private static void AssignSpriteList(SerializedObject _so, string _propertyName, List<Sprite> _sprites)
    {
        SerializedProperty prop = _so.FindProperty(_propertyName);
        if (prop == null) return;

        prop.ClearArray();
        for (int i = 0; i < _sprites.Count; i++)
        {
            prop.InsertArrayElementAtIndex(i);
            prop.GetArrayElementAtIndex(i).objectReferenceValue = _sprites[i];
        }
    }
}
#endif
