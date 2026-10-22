#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 메인메뉴 프리팹(UI_MainMenu)에 로그라이크 진입 버튼(BTN_Roguelike)을 추가한다.
/// 새로하기 버튼(BTN_NewGame)을 복제해서 만들고 UI_MainMenu.roguelikeButton에 연결한다. 여러 번 실행해도 이미 있으면 연결만 다시 확인한다(중복 생성 없음).
/// 위치는 런타임에 UI_MainMenu.UpdateButtonLayout이 정하므로(간격 buttonSpacingY) 여기서는 새로하기 버튼 자리에 겹쳐 둔다.
/// </summary>
public static class RoguelikeMainMenuButtonBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/MainMenu/UI_MainMenu.prefab";
    private const string SourceName = "BTN_NewGame";
    private const string TargetName = "BTN_Roguelike";

    [MenuItem("LumberBoy/UI/Add Roguelike MainMenu Button")]
    public static void Build()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            UI_MainMenu mainMenu = root.GetComponentInChildren<UI_MainMenu>(true);
            if (null == mainMenu)
            {
                Debug.LogError("[RoguelikeMainMenuButtonBuilder] UI_MainMenu를 찾지 못했습니다.");
                return;
            }

            Transform existing = FindChild(root.transform, TargetName);
            Transform source = FindChild(root.transform, SourceName);
            if (null == existing && null == source)
            {
                Debug.LogError("[RoguelikeMainMenuButtonBuilder] " + SourceName + " 를 찾지 못했습니다.");
                return;
            }

            Transform target = existing;
            if (null == target)
            {
                GameObject copy = Object.Instantiate(source.gameObject, source.parent);
                copy.name = TargetName;
                // 새로하기 바로 아래 형제 순서(등장 연출·뎁스는 UpdateButtonLayout이 다시 정한다).
                copy.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);
                target = copy.transform;
            }

            UI_MainMenuButton button = target.GetComponent<UI_MainMenuButton>();
            SerializedObject so = new SerializedObject(mainMenu);
            so.FindProperty("roguelikeButton").objectReferenceValue = button;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[RoguelikeMainMenuButtonBuilder] 완료: " + (null == existing ? "버튼 생성 + 연결" : "기존 버튼 연결 확인"));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Transform FindChild(Transform _root, string _name)
    {
        if (_root.name == _name) return _root;

        for (int i = 0; i < _root.childCount; i++)
        {
            Transform found = FindChild(_root.GetChild(i), _name);
            if (null != found) return found;
        }

        return null;
    }
}
#endif
