using UnityEditor;
using UnityEngine;

/// <summary>
/// 엔딩 디버그 창(Tools/Ending/Debug Window). 특성 트리에 소풍 바구니 노드가 아직 없어 텐트에서 살 수 없으므로,
/// 플레이 중 마을에서 바구니만 켜 준다. 엔딩 출발은 실제 경로 그대로 차에 타서 시작한다.
/// 바구니는 이번 실행에만 켜지고 세이브에 남지 않는다. 엔딩을 보면 세이브에 '엔딩 봄'(과 조건이 되면 로그라이크 해금)이 기록된다.
/// </summary>
public class EndingDebugWindow : EditorWindow
{
    [MenuItem("Tools/Ending/Debug Window")]
    public static void Open()
    {
        GetWindow<EndingDebugWindow>("Ending Debug");
    }

    private void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        if (false == EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서 MainMenuScene으로 시작해 마을까지 들어간 뒤 사용하세요.", MessageType.Info);
            return;
        }

        BootStrap bootStrap = FindAnyObjectByType<BootStrap>();
        TownObjectManager townObjectManager = FindAnyObjectByType<TownObjectManager>();
        TownSystem townSystem = FindAnyObjectByType<TownSystem>();
        bool bInTown = null != bootStrap && SceneType.Town == bootStrap.CurrentSceneType && false == GameModeContext.IsRoguelike;

        EditorGUILayout.LabelField("정식판 빌드", BuildInfo.IsFullRelease ? "예" : "아니오 (엔딩 없음)");
        EditorGUILayout.LabelField("현재 씬", null != bootStrap ? bootStrap.CurrentSceneType.ToString() : "-");
        EditorGUILayout.LabelField("소풍 바구니", null != townObjectManager && townObjectManager.HasPicnicBasket ? "켜짐" : "꺼짐");
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(false == bInTown || null == townObjectManager))
        {
            if (GUILayout.Button("소풍 바구니 켜기 (이번 실행만)")) GivePicnicBasket(townObjectManager);

            // 이미 엔딩을 본 세이브는 다시 출발하지 않는다. 이번 실행에서만 조건을 풀어 준다(세이브의 기록은 그대로).
            if (GUILayout.Button("엔딩 다시 보기 허용 (이번 실행만)") && null != townSystem) townSystem.SetEndingSeen(false);
        }

        EditorGUILayout.HelpBox(
            "바구니를 켠 뒤 마을에서 차에 타면 숲 선택 없이 엔딩으로 출발합니다.\n" +
            "튜토리얼 도끼 강화가 남아 있으면 출발하지 않습니다.\n" +
            "엔딩을 보면 세이브에 '엔딩 봄'이 기록되니, 백업해 둔 세이브로 보는 걸 권합니다.",
            MessageType.None);
    }

    // SC_PicnicBasket.Apply와 같이 마을·던전 오브젝트 관리자에 함께 켠다.
    private static void GivePicnicBasket(TownObjectManager _townObjectManager)
    {
        _townObjectManager.SetPicnicBasket(true);

        InDungeonObjectManager inDungeonObjectManager = FindAnyObjectByType<InDungeonObjectManager>(FindObjectsInactive.Include);
        if (null != inDungeonObjectManager) inDungeonObjectManager.SetPicnicBasket(true);
    }
}
