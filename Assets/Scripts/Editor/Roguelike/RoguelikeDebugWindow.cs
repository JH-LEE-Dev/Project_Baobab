using UnityEditor;
using UnityEngine;

/// <summary>
/// 로그라이크 디버그 창(Tools/Roguelike/Debug Window). 플레이 중 런 상태를 보고, UI 없이 카드 선택·결과 확인을 대신하며
/// 웨이브를 건너뛴다. 메인메뉴에서 바로 로그라이크로 들어가는 버튼도 있다(메인메뉴 버튼은 UI 담당이 붙인다).
/// </summary>
public class RoguelikeDebugWindow : EditorWindow
{
    private int nextWave = 8;
    private long goldToAdd = 500;
    private int treesToKill = 10;
    private float staminaDamage = 10f;
    private CardDefinitionSO cardToGrant;
    private Vector2 scroll;

    [MenuItem("Tools/Roguelike/Debug Window")]
    public static void Open()
    {
        GetWindow<RoguelikeDebugWindow>("Roguelike Debug");
    }

    private void OnInspectorUpdate()
    {
        if (EditorApplication.isPlaying) Repaint();
    }

    private void OnGUI()
    {
        if (false == EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("플레이 모드에서 MainMenuScene으로 시작한 뒤 사용하세요.", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);

        RoguelikeSystem system = FindAnyObjectByType<RoguelikeSystem>();
        if (null == system)
        {
            BootStrap bootStrap = FindAnyObjectByType<BootStrap>();
            EditorGUILayout.HelpBox("로그라이크 세션이 아닙니다.", MessageType.None);
            using (new EditorGUI.DisabledScope(null == bootStrap || SceneType.MainMenu != bootStrap.CurrentSceneType))
            {
                if (GUILayout.Button("메인메뉴 → 로그라이크 진입")) EnterRoguelikeLikeMainMenuButton(bootStrap);
            }

            EditorGUILayout.EndScrollView();
            return;
        }

        DrawState(system);
        EditorGUILayout.Space();
        DrawLobby(system);
        DrawCardSelect(system);
        DrawCombat(system);
        DrawResult(system);
        DrawCheats(system);

        EditorGUILayout.EndScrollView();
    }

    private static void DrawState(RoguelikeSystem _system)
    {
        WaveSpec wave = _system.CurrentWave;
        EditorGUILayout.LabelField("상태", _system.Phase.ToString(), EditorStyles.boldLabel);
        EditorGUILayout.LabelField("웨이브", wave.bUnlimit ? $"Unlimit-{wave.unlimitIndex}" : $"{wave.waveNumber} (티어 {wave.tierIndex + 1})");
        EditorGUILayout.LabelField("처치", $"{_system.WaveKills} / {wave.killTarget}");
        EditorGUILayout.LabelField("필드 나무", $"{_system.FieldTreeCount} / T {_system.Threshold} (상한 {_system.SpawnCap})");
        EditorGUILayout.LabelField("임계치 판정", $"{_system.PressureState}  유예 {_system.PressureGraceRatio:P0}  쿨다운 {_system.PressureCooldownRatio:P0}");
        EditorGUILayout.LabelField("골드", _system.Gold.ToString());
        EditorGUILayout.LabelField("플레이 시간", $"{_system.PlayTimeSeconds:F1}초");

        RunBuildState build = _system.Build;
        for (int i = 0; i < build.OwnedCards.Count; i++)
        {
            CardDefinitionSO card = build.OwnedCards[i];
            EditorGUILayout.LabelField("  보유", $"{card.name}  Lv.{build.GetLevel(card)}/{card.EffectiveMaxLevel}");
        }
    }

    private static void DrawLobby(RoguelikeSystem _system)
    {
        if (ERunPhase.Lobby != _system.Phase) return;

        EditorGUILayout.LabelField("로비", EditorStyles.boldLabel);
        if (GUILayout.Button("게임 시작 오브젝트 타격")) _system.DebugLobbyAction(ERoguelikeLobbyAction.StartRun);
        if (GUILayout.Button("순위표 오브젝트 타격")) _system.DebugLobbyAction(ERoguelikeLobbyAction.OpenRecords);
        if (GUILayout.Button("다음 티어 지형 전환 연출 (F6)")) _system.DebugPlayTierTransition();
    }

    /// <summary>
    /// 메인메뉴의 로그라이크 버튼과 같은 경로로 들어간다(UIView_MainMenu.OnRoguelikeButtonClicked → 디스토션·암전 → 메인메뉴 퇴장 정리 →
    /// BootStrap.GoToRoguelikeFromMainMenu). BootStrap을 바로 부르면 메인메뉴 버튼 UI가 켜진 채로 남아 입력을 계속 받는다.
    /// </summary>
    private static void EnterRoguelikeLikeMainMenuButton(BootStrap _bootStrap)
    {
        // 실제 버튼은 메인메뉴 버튼 목록(UI_MainMenu) 안에 있다. 그 목록이 아직 안 보이는 상태(스플래시·초기 설정 팝업 등)에서
        // 들어가면 열려 있던 팝업이 응답 없이 남아 있다가 메인메뉴로 돌아왔을 때 다시 보인다. 버튼을 누를 수 있을 때만 들어간다.
        UI_MainMenu mainMenuButtons = FindAnyObjectByType<UI_MainMenu>(FindObjectsInactive.Include);
        if (null != mainMenuButtons && false == mainMenuButtons.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[RoguelikeDebugWindow] 메인메뉴 버튼이 보이는 상태에서 눌러 주십시오(초기 설정 팝업·스플래시 중에는 들어가지 않습니다).");
            return;
        }

        UIView_MainMenu mainMenuView = FindAnyObjectByType<UIView_MainMenu>();
        if (null != mainMenuView)
        {
            Sound.PlayUI(SoundID.MainClick);
            mainMenuView.OnRoguelikeButtonClicked();
            return;
        }

        Debug.LogWarning("[RoguelikeDebugWindow] UIView_MainMenu를 찾지 못해 메인메뉴 퇴장 연출 없이 바로 전환합니다.");
        _bootStrap.GoToRoguelikeFromMainMenu();
    }

    private static void DrawCardSelect(RoguelikeSystem _system)
    {
        if (false == _system.IsCardSelectionOpen) return;

        EditorGUILayout.LabelField(_system.IsFirstPick ? "카드 선택 (첫 무료 선택)" : "카드 선택", EditorStyles.boldLabel);

        for (int i = 0; i < _system.CurrentOffers.Count; i++)
        {
            CardOffer offer = _system.CurrentOffers[i];
            string label = $"[{offer.displayGrade}] {offer.card.name}  →Lv.{offer.levelAfterPurchase}  {offer.price}G" +
                (offer.bGuaranteed ? "  (보장)" : string.Empty) + (offer.IsAffordable(_system.Gold) ? string.Empty : "  (골드 부족)");
            if (GUILayout.Button(label)) _system.DebugBuyOffer(i);
        }

        EditorGUILayout.BeginHorizontal();
        using (new EditorGUI.DisabledScope(false == _system.CanSkip))
        {
            if (GUILayout.Button("건너뛰기")) _system.DebugSkip();
        }
        using (new EditorGUI.DisabledScope(false == _system.CanReroll))
        {
            if (GUILayout.Button($"리롤 ({_system.RerollCost}G)")) _system.DebugReroll();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCombat(RoguelikeSystem _system)
    {
        if (ERunPhase.WaveCombat != _system.Phase) return;

        EditorGUILayout.LabelField("전투", EditorStyles.boldLabel);
        if (GUILayout.Button("웨이브 즉시 클리어")) _system.DebugCompleteWave();

        EditorGUILayout.BeginHorizontal();
        treesToKill = EditorGUILayout.IntField(treesToKill, GUILayout.Width(60));
        if (GUILayout.Button("나무 쓰러뜨리기")) _system.DebugKillTrees(treesToKill);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        staminaDamage = EditorGUILayout.FloatField(staminaDamage, GUILayout.Width(60));
        if (GUILayout.Button("스태미너 피해")) _system.DebugDamageStamina(staminaDamage);
        EditorGUILayout.EndHorizontal();

        // 오프로드 즉시 발동. 카드가 없어도 되며, 대수를 고르거나 지금 빌드의 대수로 낸다(게임 화면에서는 F7).
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("오프로드 발동 (지금 빌드, F7)")) _system.DebugFireOffroad(0);
        if (GUILayout.Button("1대", GUILayout.Width(40))) _system.DebugFireOffroad(1);
        if (GUILayout.Button("2대", GUILayout.Width(40))) _system.DebugFireOffroad(2);
        if (GUILayout.Button("3대", GUILayout.Width(40))) _system.DebugFireOffroad(3);
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawResult(RoguelikeSystem _system)
    {
        if (ERunPhase.Result != _system.Phase) return;

        EditorGUILayout.LabelField("결과", EditorStyles.boldLabel);
        RunRecordEntry entry = _system.LastRunRecord;
        if (null != entry)
        {
            EditorGUILayout.LabelField("도달", entry.bCleared ? $"30 클리어, Unlimit-{entry.reachedUnlimitWave}" : $"웨이브 {entry.reachedWave}");
            EditorGUILayout.LabelField("시간/나무/코인", $"{entry.playTimeSeconds:F1}초 / {entry.treesFelled} / {entry.coinsCollected}");
            EditorGUILayout.LabelField("순위", $"클리어 {_system.LastClearRank}  언리밋 {_system.LastUnlimitRank}");
        }

        if (GUILayout.Button("확인 (로비로)")) _system.DebugConfirmResult();
    }

    private void DrawCheats(RoguelikeSystem _system)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("치트", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        goldToAdd = EditorGUILayout.LongField(goldToAdd, GUILayout.Width(80));
        if (GUILayout.Button("골드 추가")) _system.DebugAddGold(goldToAdd);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        nextWave = EditorGUILayout.IntField(nextWave, GUILayout.Width(80));
        if (GUILayout.Button("다음 웨이브 번호 지정")) _system.DebugSetNextWave(nextWave);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        cardToGrant = (CardDefinitionSO)EditorGUILayout.ObjectField(cardToGrant, typeof(CardDefinitionSO), false);
        using (new EditorGUI.DisabledScope(null == cardToGrant))
        {
            if (GUILayout.Button("카드 1단계 지급", GUILayout.Width(110))) _system.DebugGrantCard(cardToGrant);
        }
        EditorGUILayout.EndHorizontal();
    }
}
