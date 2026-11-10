#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 폐지(시안 v3)에 따른 UI 프리팹 구성기. 여러 번 실행해도 같은 결과로 덮어쓴다.
///   LumberBoy/UI/Build Roguelike Result     UIView_RoguelikeResult.prefab  결과 화면(딤 · 판 · 제목 명판 · 웨이브 · 행 · 카드 칸 · 버튼 2개 · 키 안내)
///   LumberBoy/UI/Build Roguelike Records    UIView_RoguelikeRecords.prefab 기록 창(딤 · 판 · 화살표 · 탭 · 화살표 명판(옵션 화살표 그림) · 점 3개 · 행 템플릿 · 키 안내)
///   LumberBoy/UI/Build Roguelike Main Menu  UI_MainMenu.prefab에 로그라이크 하위 메뉴(명판 + 버튼 3개 + 최고 기록판), UIView_MainMenu.prefab에 기록 창 · 카드 DB 연결
///   LumberBoy/UI/Build Roguelike Lobby UI (All)  위 셋을 차례로
///
/// [그림] 보관함과 같은 9-슬라이스(Assets/Graphics/Roguelike/owned/Panel_Book · Panel_Info · Title_Plate), 버튼 바탕은 본편 BasicButton,
///   아이콘은 HUD 아이콘(Assets/Graphics/Roguelike/hud/). 기존 프리팹의 UIView 설정(레이어 · ESC 닫기 등)은 그대로 둔다.
/// [글자] 문구는 프리팹에 박지 않는다(RoguelikeUI.json 키를 스크립트가 채운다). 글자 오브젝트에는 LocalizedFontTracker를 붙인다.
///   숫자 · WAVE 표기처럼 언어와 무관한 글자도 같은 폰트(Galmuri11)로 만들고, 언어별 폰트 교체를 따른다.
/// [크기 - 18개 언어 규격] 줄 높이는 언어별 폰트 중 가장 큰 글자 높이 + 위아래 여백 2px 기준이다(시안 v3 측정). 폭은 화면 스크립트가 열릴 때
///   18개 언어 최소 폭(Spec*)과 현재 언어 폭 중 큰 값으로 다시 잡는다. 검수는 Localization QA 툴(목록 갱신 → 자동 검사).
/// </summary>
public static class RoguelikeLobbyUIBuilder
{
    private const string ResultPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeResult.prefab";
    private const string RecordsPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeRecords.prefab";
    private const string MainMenuPrefabPath = "Assets/Prefabs/UI/MainMenu/UI_MainMenu.prefab";
    private const string MainMenuViewPrefabPath = "Assets/Prefabs/UI/MainMenu/UIView_MainMenu.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Galmuri11_Optimum.asset";
    private const string KeyIconDatabasePath = "Assets/Prefabs/UI/Option/KeyIconDatabase.asset";
    private const string CardDatabasePath = "Assets/Data/Roguelike/RoguelikeCardDatabase_Draft.asset";
    private const string OwnedDir = "Assets/Graphics/Roguelike/owned/";
    private const string HudDir = "Assets/Graphics/Roguelike/hud/";
    private const string BasicButtonPath = "Assets/Graphics/HUD/Button/BasicButton.png";
    // 옵션 창 선택기(UI_OptionSelector)와 같은 화살표 그림 - 기록 창 탭 명판의 ◀ ▶를 이것으로 통일한다.
    private const string OptionArrowLeftPath = "Assets/Graphics/HUD/Option/Chevron-Arrow-Left-export.png";
    private const string OptionArrowRightPath = "Assets/Graphics/HUD/Option/Chevron-Arrow-Right-export.png";
    private const float ArrowIconSize = 24f;
    private static readonly Color ArrowHoverColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    private static readonly Color ArrowPressedColor = new Color(0.6f, 0.6f, 0.6f, 1f);

    // 줄 높이(px). 가장 큰 글자 높이(Lorem 계열 위 17 + 아래 4) 기준에 여백을 더한 값.
    private const float PlateHeight = 25f;
    private const float SmallLineHeight = 18f;
    private const float BigLineHeight = 30f;
    private const float RowHeight = 20f;
    private const float ButtonHeight = 28f;
    private const float GuideHeight = 18f;
    private const float KeyIconSize = 16f;

    // 로그라이크 메뉴 배치(B안 - 양쪽 끝 대칭). 640×360 기준.
    // 하위 메뉴 루트는 메인 버튼 묶음(BTNs) 위치에서 이만큼 옮긴다: 왼쪽 여백 11 → 16, 명판 윗변을 기록판 명판 윗변과 같은 줄로.
    private static readonly Vector2 RoguelikeMenuOffset = new Vector2(5f, 18f);
    // 기록판(오른쪽 위 기준): 오른쪽 여백 16, 판 윗변(= 명판 세로 중심) 162.
    private static readonly Vector2 RecordPanelPosition = new Vector2(-16f, -162f);

    private static readonly Color DimColor = new Color(0.02f, 0.03f, 0.06f, 0.6f);
    private static readonly Color CreamColor = new Color(0.95f, 0.89f, 0.77f, 1f);
    private static readonly Color MutedColor = new Color(0.73f, 0.65f, 0.54f, 1f);
    private static readonly Color GoldColor = new Color(1f, 0.83f, 0.35f, 1f);
    private static readonly Color ShadowColor = new Color(0.16f, 0.1f, 0.06f, 1f);
    private static readonly Color SeparatorColor = new Color(0.95f, 0.89f, 0.77f, 0.12f);

    private class Assets
    {
        public TMP_FontAsset font;
        public KeyIconDatabase keyIcons;
        public Sprite bookPanel;
        public Sprite infoPanel;
        public Sprite titlePlate;
        public Sprite basicButton;
        public Sprite arrowLeft;
        public Sprite arrowRight;
        public Sprite iconFlag;
        public Sprite iconInfinity;
        public Sprite iconTree;
        public Sprite iconAxe;
    }

    [MenuItem("LumberBoy/UI/Build Roguelike Lobby UI (All)")]
    public static void BuildAll()
    {
        BuildResult();
        BuildRecords();
        BuildMainMenu();
    }

    // // 결과 화면

    [MenuItem("LumberBoy/UI/Build Roguelike Result")]
    public static void BuildResult()
    {
        Assets a = LoadAssets();
        if (null == a) return;

        GameObject root = PrefabUtility.LoadPrefabContents(ResultPrefabPath);
        try
        {
            UIView_RoguelikeResult view = root.GetComponent<UIView_RoguelikeResult>();
            if (null == view) { Debug.LogError("[RoguelikeLobbyUIBuilder] UIView_RoguelikeResult 컴포넌트가 없습니다."); return; }

            RectTransform rootRect = ClearChildren(root);

            Image dim = Stretch("Dim", rootRect, DimColor);
            dim.raycastTarget = true;
            CanvasGroup dimGroup = dim.gameObject.AddComponent<CanvasGroup>();

            // 판: 위치(panelRoot) → 크기 연출(PanelVisual, 늘 1배로 돌아온다) → 9-슬라이스 바탕
            RectTransform panelRoot = TopCenter("Panel", rootRect, 420f, 280f, new Vector2(0.5f, 1f));
            RectTransform panelVisual = StretchRect("PanelVisual", panelRoot);
            Image panelImage = panelVisual.gameObject.AddComponent<Image>();
            panelImage.sprite = a.bookPanel;
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = true;

            RectTransform plate = SlicedChild("TitlePlate", panelVisual, a.titlePlate, 170f, PlateHeight);
            TMP_Text title = Text("Title", plate, a.font, TextAlignmentOptions.Midline, CreamColor);
            AddShadow(title);

            TMP_Text waveLabel = Text("WaveLabel", panelVisual, a.font, TextAlignmentOptions.Midline, MutedColor);
            TMP_Text waveValue = Text("WaveValue", panelVisual, a.font, TextAlignmentOptions.Midline, GoldColor);
            waveValue.fontSize = a.font.faceInfo.pointSize * 2f;
            TMP_Text best = Text("Best", panelVisual, a.font, TextAlignmentOptions.Midline, MutedColor);

            RectTransform rows = StretchRect("Rows", panelVisual);
            TMP_Text playLabel = Text("PlayTimeLabel", panelVisual, a.font, TextAlignmentOptions.MidlineLeft, CreamColor);
            TMP_Text playValue = Text("PlayTimeValue", panelVisual, a.font, TextAlignmentOptions.MidlineRight, GoldColor);
            TMP_Text treesLabel = Text("TreesLabel", panelVisual, a.font, TextAlignmentOptions.MidlineLeft, CreamColor);
            TMP_Text treesValue = Text("TreesValue", panelVisual, a.font, TextAlignmentOptions.MidlineRight, GoldColor);
            RectTransform sep0 = Solid("Separator0", panelVisual, SeparatorColor);
            RectTransform sep1 = Solid("Separator1", panelVisual, SeparatorColor);

            TMP_Text ownedLabel = Text("OwnedLabel", panelVisual, a.font, TextAlignmentOptions.MidlineLeft, MutedColor);
            RectTransform slotRoot = TopCenter("Slots", panelVisual, 380f, 36f, new Vector2(0.5f, 1f));
            UI_RoguelikeCardIconSlot slotTemplate = CreateIconSlot(slotRoot, a);

            UI_RoguelikeTextButton retry = CreateTextButton("RetryButton", panelVisual, a);
            UI_RoguelikeTextButton mainMenu = CreateTextButton("MainMenuButton", panelVisual, a);

            RectTransform guide = GuideRow("KeyGuide", panelVisual);
            UI_KeyboardImage moveKey = KeyImage("MoveKey", guide, a.keyIcons, EOtherKey.None, EGamepadButton.LeftStick);
            TMP_Text hintMove = GuideText("HintMove", guide, a.font);
            UI_KeyboardImage selectKey = KeyImage("SelectKey", guide, a.keyIcons, EOtherKey.Enter, EGamepadButton.Button_A_Cross);
            TMP_Text hintSelect = GuideText("HintSelect", guide, a.font);

            SerializedObject so = new SerializedObject(view);
            Set(so, "dimGroup", dimGroup);
            Set(so, "panelRoot", panelRoot);
            Set(so, "panelVisual", panelVisual);
            Set(so, "titlePlate", plate);
            Set(so, "titleText", title);
            Set(so, "waveLabelText", waveLabel);
            Set(so, "waveValueText", waveValue);
            Set(so, "bestText", best);
            Set(so, "rowsRoot", rows);
            Set(so, "playTimeLabel", playLabel);
            Set(so, "playTimeValue", playValue);
            Set(so, "treesLabel", treesLabel);
            Set(so, "treesValue", treesValue);
            SetArray(so, "rowSeparators", sep0, sep1);
            Set(so, "ownedLabel", ownedLabel);
            Set(so, "slotRoot", slotRoot);
            Set(so, "slotTemplate", slotTemplate);
            Set(so, "retryButton", retry);
            Set(so, "mainMenuButton", mainMenu);
            Set(so, "guideRow", guide);
            Set(so, "hintMoveText", hintMove);
            Set(so, "hintSelectText", hintSelect);
            SetArray(so, "keyGuideImages", moveKey, selectKey);
            so.FindProperty("plateHeight").floatValue = PlateHeight;
            so.FindProperty("smallLineHeight").floatValue = SmallLineHeight;
            so.FindProperty("bigLineHeight").floatValue = BigLineHeight;
            so.FindProperty("rowHeight").floatValue = RowHeight;
            so.FindProperty("buttonHeight").floatValue = ButtonHeight;
            so.FindProperty("guideHeight").floatValue = GuideHeight;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ResultPrefabPath);
            Debug.Log("[RoguelikeLobbyUIBuilder] 완료: " + ResultPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // // 기록 창

    [MenuItem("LumberBoy/UI/Build Roguelike Records")]
    public static void BuildRecords()
    {
        Assets a = LoadAssets();
        if (null == a) return;

        GameObject root = PrefabUtility.LoadPrefabContents(RecordsPrefabPath);
        try
        {
            UIView_RoguelikeRecords view = root.GetComponent<UIView_RoguelikeRecords>();
            if (null == view) { Debug.LogError("[RoguelikeLobbyUIBuilder] UIView_RoguelikeRecords 컴포넌트가 없습니다."); return; }

            RectTransform rootRect = ClearChildren(root);

            Image dim = Stretch("Dim", rootRect, DimColor);
            dim.raycastTarget = true;
            CanvasGroup dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.navigation = new Navigation { mode = Navigation.Mode.None };

            // 판 360 x 300: 탭 명판(위 가장자리에 걸침) · 점 3개 · 행 5줄(46 + 4) · 키 안내
            const float panelWidth = 360f;
            const float panelHeight = 304f;
            RectTransform panel = Centered("Panel", rootRect, panelWidth, panelHeight);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = a.bookPanel;
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = true;
            panel.anchoredPosition = new Vector2(0f, -6f);

            RectTransform tabPlate = SlicedChild("TabPlate", panel, a.titlePlate, 238f, PlateHeight);
            TMP_Text tabText = Text("TabName", tabPlate, a.font, TextAlignmentOptions.Midline, CreamColor);
            AddShadow(tabText);
            StretchText(tabText);
            Button prev = ArrowButton("Prev", tabPlate, a.arrowLeft, true, out RectTransform prevArrow);
            Button next = ArrowButton("Next", tabPlate, a.arrowRight, false, out RectTransform nextArrow);

            // 점 3개(3x3, 간격 3)와 5x5 커서 - 첫 실행 언어 설정 팝업(UI_InitialSetupPopup)의 페이지 점과 같은 모양 · 크기. 색은 화면 스크립트가 정한다.
            Image[] dots = new Image[3];
            for (int i = 0; 3 > i; i++)
            {
                RectTransform dot = Solid("Dot" + i, panel, Color.white);
                SetTop(dot, new Vector2(0.5f, 1f), new Vector2(-5.5f + i * 6f, -(Mathf.Ceil(PlateHeight * 0.5f) + 4.5f)), new Vector2(3f, 3f));
                dots[i] = dot.GetComponent<Image>();
            }
            RectTransform dotCursor = Solid("DotCursor", dots[0].rectTransform, Color.white);
            dotCursor.anchorMin = new Vector2(0.5f, 0.5f);
            dotCursor.anchorMax = new Vector2(0.5f, 0.5f);
            dotCursor.pivot = new Vector2(0.5f, 0.5f);
            dotCursor.sizeDelta = new Vector2(5f, 5f);
            dotCursor.anchoredPosition = Vector2.zero;

            RectTransform rowRoot = TopLeft("Rows", panel, panelWidth - 20f, 5f * 50f);
            rowRoot.anchoredPosition = new Vector2(10f, -(Mathf.Ceil(PlateHeight * 0.5f) + 12f));
            UI_RoguelikeRecordRow rowTemplate = CreateRecordRow(rowRoot, a, panelWidth - 20f);

            TMP_Text empty = Text("Empty", panel, a.font, TextAlignmentOptions.Midline, MutedColor);
            SetTop(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(panelWidth - 20f, SmallLineHeight));

            RectTransform guide = GuideRow("KeyGuide", panel);
            guide.anchorMin = new Vector2(0f, 0f);
            guide.anchorMax = new Vector2(0f, 0f);
            guide.pivot = new Vector2(0f, 0f);
            guide.anchoredPosition = new Vector2(10f, 8f);
            UI_KeyboardImage prevKey = KeyImage("TabPrevKey", guide, a.keyIcons, EOtherKey.PageUp, EGamepadButton.LeftShoulder_LB_L1);
            UI_KeyboardImage nextKey = KeyImage("TabNextKey", guide, a.keyIcons, EOtherKey.PageDown, EGamepadButton.RightShoulder_RB_R1);
            TMP_Text hintTab = GuideText("HintTab", guide, a.font);
            UI_KeyboardImage backKey = KeyImage("BackKey", guide, a.keyIcons, EOtherKey.Escape, EGamepadButton.Button_B_Circle);
            TMP_Text hintBack = GuideText("HintBack", guide, a.font);

            SerializedObject so = new SerializedObject(view);
            Set(so, "dimGroup", dimGroup);
            Set(so, "dimButton", dimButton);
            Set(so, "panelRoot", panel);
            Set(so, "tabPlate", tabPlate);
            Set(so, "tabText", tabText);
            Set(so, "prevTabButton", prev);
            Set(so, "nextTabButton", next);
            Set(so, "prevArrowIcon", prevArrow);
            Set(so, "nextArrowIcon", nextArrow);
            SetArray(so, "tabDots", dots[0], dots[1], dots[2]);
            Set(so, "tabDotCursor", dotCursor);
            Set(so, "rowRoot", rowRoot);
            Set(so, "rowTemplate", rowTemplate);
            Set(so, "emptyText", empty);
            SetArray(so, "tabGuideItems", prevKey.transform, nextKey.transform, hintTab.rectTransform);
            Set(so, "hintTabText", hintTab);
            Set(so, "hintBackText", hintBack);
            SetArray(so, "keyGuideImages", prevKey, nextKey, backKey);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, RecordsPrefabPath);
            Debug.Log("[RoguelikeLobbyUIBuilder] 완료: " + RecordsPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // // 메인메뉴 하위 메뉴

    [MenuItem("LumberBoy/UI/Build Roguelike Main Menu")]
    public static void BuildMainMenu()
    {
        Assets a = LoadAssets();
        if (null == a) return;

        GameObject root = PrefabUtility.LoadPrefabContents(MainMenuPrefabPath);
        try
        {
            UI_MainMenu menu = root.GetComponent<UI_MainMenu>();
            RectTransform rootRect = (RectTransform)root.transform;
            RectTransform buttons = rootRect.Find("BTNs") as RectTransform;
            Transform sourceButton = null != buttons ? buttons.Find("BTN_Roguelike") : null;
            if (null == menu || null == buttons || null == sourceButton) { Debug.LogError("[RoguelikeLobbyUIBuilder] UI_MainMenu · BTNs · BTN_Roguelike를 찾지 못했습니다."); return; }

            Transform old = rootRect.Find("RoguelikeMenu");
            if (null != old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            Transform oldPanel = rootRect.Find("RoguelikeRecordPanel");
            if (null != oldPanel) UnityEngine.Object.DestroyImmediate(oldPanel.gameObject);

            // 하위 메뉴 묶음: BTNs 자리(왼쪽 가운데 기준)에서 RoguelikeMenuOffset만큼 옮긴다(B안 - 왼쪽 여백 16, 위쪽 끝을 기록판과 정렬).
            RectTransform menuRoot = new GameObject("RoguelikeMenu", typeof(RectTransform)).GetComponent<RectTransform>();
            menuRoot.gameObject.layer = LayerMask.NameToLayer("UI");
            menuRoot.SetParent(rootRect, false);
            menuRoot.anchorMin = buttons.anchorMin;
            menuRoot.anchorMax = buttons.anchorMax;
            menuRoot.pivot = buttons.pivot;
            menuRoot.sizeDelta = buttons.sizeDelta;
            menuRoot.anchoredPosition = buttons.anchoredPosition + RoguelikeMenuOffset;
            menuRoot.SetSiblingIndex(buttons.GetSiblingIndex() + 1);

            // 명판: 버튼 글자 왼쪽 선(버튼 판정 박스 왼쪽)에 맞춘다.
            RectTransform plate = new GameObject("Plate", typeof(RectTransform)).GetComponent<RectTransform>();
            plate.gameObject.layer = LayerMask.NameToLayer("UI");
            plate.SetParent(menuRoot, false);
            plate.anchorMin = new Vector2(0.5f, 0.5f);
            plate.anchorMax = new Vector2(0.5f, 0.5f);
            plate.pivot = new Vector2(0f, 0.5f);
            plate.sizeDelta = new Vector2(116f, PlateHeight);
            plate.anchoredPosition = new Vector2(-105f, 0f);
            Image plateImage = plate.gameObject.AddComponent<Image>();
            plateImage.sprite = a.titlePlate;
            plateImage.type = Image.Type.Sliced;
            plateImage.raycastTarget = false;
            RectTransform axe = IconRect("Icon", plate, a.iconAxe, 7f, 7f);
            axe.anchorMin = new Vector2(0f, 0.5f);
            axe.anchorMax = new Vector2(0f, 0.5f);
            axe.pivot = new Vector2(0f, 0.5f);
            axe.anchoredPosition = new Vector2(9f, 0f);
            TMP_Text plateText = Text("Label", plate, a.font, TextAlignmentOptions.MidlineLeft, CreamColor);
            AddShadow(plateText);
            plateText.rectTransform.anchorMin = Vector2.zero;
            plateText.rectTransform.anchorMax = Vector2.one;
            plateText.rectTransform.offsetMin = new Vector2(20f, 0f);
            plateText.rectTransform.offsetMax = new Vector2(-8f, 0f);

            UI_MainMenuButton start = CloneMenuButton(sourceButton, menuRoot, "BTN_RL_Start");
            UI_MainMenuButton records = CloneMenuButton(sourceButton, menuRoot, "BTN_RL_Records");
            UI_MainMenuButton option = CloneMenuButton(sourceButton, menuRoot, "BTN_RL_Option");
            UI_MainMenuButton back = CloneMenuButton(sourceButton, menuRoot, "BTN_RL_Back");

            // 최고 기록판: 화면 오른쪽 위 기준(오른쪽 여백 16). 판 윗변 = 명판 세로 중심을 162에 두어
            // 왼쪽 하위 메뉴 명판과 윗변(149.5)을 같은 줄로 맞춘다(B안). 로고 아래끝(약 131)과 명판 윗변 사이 약 18px,
            // 판(높이 약 150) 아래끝 312는 버전 글자(위끝 약 338) 위다. 로그라이크 메뉴에서는 디스코드 아이콘이 화면 밖으로 빠진다.
            RectTransform panelRoot = new GameObject("RoguelikeRecordPanel", typeof(RectTransform)).GetComponent<RectTransform>();
            panelRoot.gameObject.layer = LayerMask.NameToLayer("UI");
            panelRoot.SetParent(rootRect, false);
            panelRoot.anchorMin = new Vector2(1f, 1f);
            panelRoot.anchorMax = new Vector2(1f, 1f);
            panelRoot.pivot = new Vector2(1f, 1f);
            panelRoot.sizeDelta = Vector2.zero;
            panelRoot.anchoredPosition = RecordPanelPosition;
            CanvasGroup group = panelRoot.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            RectTransform panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.gameObject.layer = LayerMask.NameToLayer("UI");
            panel.SetParent(panelRoot, false);
            panel.anchorMin = new Vector2(1f, 1f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.pivot = new Vector2(1f, 1f);
            panel.sizeDelta = new Vector2(272f, 150f);
            panel.anchoredPosition = Vector2.zero;
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = a.bookPanel;
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = false;

            RectTransform titlePlate = SlicedChild("TitlePlate", panel, a.titlePlate, 216f, PlateHeight);
            TMP_Text titleText = Text("Title", titlePlate, a.font, TextAlignmentOptions.Midline, CreamColor);
            AddShadow(titleText);
            StretchText(titleText);

            Sprite[] iconSprites = { a.iconFlag, a.iconInfinity, a.iconTree };
            RectTransform[] icons = new RectTransform[3];
            TMP_Text[] labels = new TMP_Text[3];
            TMP_Text[] values = new TMP_Text[3];
            for (int i = 0; 3 > i; i++)
            {
                icons[i] = IconRect("Icon" + i, panel, iconSprites[i], 9f, 9f);
                labels[i] = Text("Label" + i, panel, a.font, TextAlignmentOptions.MidlineLeft, MutedColor);
                values[i] = Text("Value" + i, panel, a.font, TextAlignmentOptions.MidlineLeft, GoldColor);
            }
            RectTransform sepA = Solid("Separator0", panel, SeparatorColor);
            RectTransform sepB = Solid("Separator1", panel, SeparatorColor);

            UI_RoguelikeMenuRecordPanel recordPanel = panelRoot.gameObject.AddComponent<UI_RoguelikeMenuRecordPanel>();
            SerializedObject ps = new SerializedObject(recordPanel);
            Set(ps, "group", group);
            Set(ps, "panel", panel);
            Set(ps, "titlePlate", titlePlate);
            Set(ps, "titleText", titleText);
            SetArray(ps, "icons", icons[0], icons[1], icons[2]);
            SetArray(ps, "labels", labels[0], labels[1], labels[2]);
            SetArray(ps, "values", values[0], values[1], values[2]);
            SetArray(ps, "separators", sepA, sepB);
            ps.FindProperty("plateHeight").floatValue = PlateHeight;
            ps.FindProperty("lineHeight").floatValue = SmallLineHeight;
            ps.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject ms = new SerializedObject(menu);
            Set(ms, "roguelikeMenuRoot", menuRoot);
            Set(ms, "roguelikePlate", plate);
            Set(ms, "roguelikePlateText", plateText);
            Set(ms, "roguelikeStartButton", start);
            Set(ms, "roguelikeRecordsButton", records);
            Set(ms, "roguelikeOptionButton", option);
            Set(ms, "roguelikeBackButton", back);
            Set(ms, "roguelikeRecordPanel", recordPanel);
            ms.FindProperty("roguelikeFirstButtonY").floatValue = -Mathf.Ceil(PlateHeight * 0.5f) - 18f;
            ms.ApplyModifiedPropertiesWithoutUndo();

            menuRoot.gameObject.SetActive(false);
            panelRoot.gameObject.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, MainMenuPrefabPath);
            Debug.Log("[RoguelikeLobbyUIBuilder] 완료: " + MainMenuPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // 메인메뉴 화면: 기록 창 프리팹 · 카드 DB 연결
        GameObject viewRoot = PrefabUtility.LoadPrefabContents(MainMenuViewPrefabPath);
        try
        {
            UIView_MainMenu view = viewRoot.GetComponent<UIView_MainMenu>();
            if (null != view)
            {
                SerializedObject vs = new SerializedObject(view);
                Set(vs, "roguelikeRecordsPrefab", AssetDatabase.LoadAssetAtPath<UIView_RoguelikeRecords>(RecordsPrefabPath));
                Set(vs, "roguelikeCardDatabase", AssetDatabase.LoadAssetAtPath<CardDatabaseSO>(CardDatabasePath));
                vs.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(viewRoot, MainMenuViewPrefabPath);
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(viewRoot);
        }

        AssetDatabase.SaveAssets();
    }

    // // 부품

    private static UI_MainMenuButton CloneMenuButton(Transform _source, RectTransform _parent, string _name)
    {
        GameObject clone = UnityEngine.Object.Instantiate(_source.gameObject, _parent);
        clone.name = _name;
        clone.SetActive(true);
        RectTransform rect = (RectTransform)clone.transform;
        rect.anchoredPosition = new Vector2(((RectTransform)_source).anchoredPosition.x, 0f);
        return clone.GetComponent<UI_MainMenuButton>();
    }

    private static UI_RoguelikeCardIconSlot CreateIconSlot(RectTransform _parent, Assets _a)
    {
        RectTransform slot = TopLeft("SlotTemplate", _parent, UI_RoguelikeCardIconSlot.SlotSize, UI_RoguelikeCardIconSlot.SlotSize);
        Image bg = slot.gameObject.AddComponent<Image>();
        bg.sprite = _a.infoPanel;
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;

        RectTransform icon = IconRect("Icon", slot, null, 32f, 32f);
        icon.anchorMin = new Vector2(0.5f, 0.5f);
        icon.anchorMax = new Vector2(0.5f, 0.5f);
        icon.pivot = new Vector2(0.5f, 0.5f);
        icon.anchoredPosition = Vector2.zero;

        TMP_Text shadow = Text("LevelShadow", slot, _a.font, TextAlignmentOptions.BottomRight, ShadowColor);
        TMP_Text level = Text("Level", slot, _a.font, TextAlignmentOptions.BottomRight, Color.white);
        SetCorner(shadow.rectTransform, new Vector2(1f, -1f));
        SetCorner(level.rectTransform, Vector2.zero);

        UI_RoguelikeCardIconSlot comp = slot.gameObject.AddComponent<UI_RoguelikeCardIconSlot>();
        SerializedObject so = new SerializedObject(comp);
        Set(so, "iconImage", icon.GetComponent<Image>());
        Set(so, "levelText", level);
        Set(so, "levelShadowText", shadow);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    private static UI_RoguelikeRecordRow CreateRecordRow(RectTransform _parent, Assets _a, float _width)
    {
        RectTransform row = TopLeft("RowTemplate", _parent, _width, 46f);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = _a.infoPanel;
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = false;

        TMP_Text rank = Text("Rank", row, _a.font, TextAlignmentOptions.Midline, GoldColor);
        SetLeft(rank.rectTransform, 8f, 20f);
        TMP_Text wave = Text("Wave", row, _a.font, TextAlignmentOptions.MidlineLeft, CreamColor);
        SetLeft(wave.rectTransform, 34f, 90f);
        TMP_Text time = Text("Time", row, _a.font, TextAlignmentOptions.MidlineLeft, MutedColor);
        SetLeft(time.rectTransform, 128f, 56f);

        UI_RoguelikeCardIconSlot[] slots = new UI_RoguelikeCardIconSlot[UI_RoguelikeRecordRow.SlotCount];
        for (int i = 0; UI_RoguelikeRecordRow.SlotCount > i; i++)
        {
            slots[i] = CreateIconSlot(row, _a);
            slots[i].name = "Slot" + i;
            RectTransform rect = slots[i].Rect;
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-(30f + (UI_RoguelikeRecordRow.SlotCount - 1 - i) * 38f), 0f);
        }

        TMP_Text more = Text("More", row, _a.font, TextAlignmentOptions.MidlineRight, MutedColor);
        more.rectTransform.anchorMin = new Vector2(1f, 0.5f);
        more.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        more.rectTransform.pivot = new Vector2(1f, 0.5f);
        more.rectTransform.sizeDelta = new Vector2(24f, SmallLineHeight);
        more.rectTransform.anchoredPosition = new Vector2(-4f, 0f);

        UI_RoguelikeRecordRow comp = row.gameObject.AddComponent<UI_RoguelikeRecordRow>();
        SerializedObject so = new SerializedObject(comp);
        Set(so, "rankText", rank);
        Set(so, "waveText", wave);
        Set(so, "timeText", time);
        Set(so, "moreText", more);
        SetArray(so, "slots", slots);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    private static UI_RoguelikeTextButton CreateTextButton(string _name, RectTransform _parent, Assets _a)
    {
        RectTransform root = TopCenter(_name, _parent, 186f, ButtonHeight, new Vector2(0.5f, 1f));
        Image hit = root.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        hit.raycastTarget = true;
        Selectable selectable = root.gameObject.AddComponent<Selectable>();
        selectable.transition = Selectable.Transition.None;
        selectable.targetGraphic = hit;

        RectTransform visual = StretchRect("Visual", root);

        Image bg = Stretch("Background", visual, Color.white);
        bg.sprite = _a.basicButton;
        bg.type = Image.Type.Sliced;

        TMP_Text label = Text("Label", visual, _a.font, TextAlignmentOptions.Midline, new Color(0.23f, 0.14f, 0.09f, 1f));
        StretchText(label);
        label.rectTransform.offsetMin = new Vector2(6f, 1f);
        label.rectTransform.offsetMax = new Vector2(-6f, 0f);

        UI_RoguelikeTextButton comp = root.gameObject.AddComponent<UI_RoguelikeTextButton>();
        SerializedObject so = new SerializedObject(comp);
        Set(so, "selectable", selectable);
        Set(so, "label", label);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    /// <summary>
    /// 탭 명판 끝의 화살표 버튼(판정 폭 22 · 명판 높이 전체). 그림은 옵션 창 선택기와 같은 화살표 스프라이트(24×24)이고
    /// 명판 가장자리에서 8px 안쪽에 온다(탭을 넘길 때 3px 밀려도 테두리 안쪽). 색 반응은 옵션 화살표와 같다(흰색 → 올림 0.8 → 누름 0.6, 0.1초).
    /// </summary>
    private static Button ArrowButton(string _name, RectTransform _parent, Sprite _arrow, bool _bLeft, out RectTransform _icon)
    {
        RectTransform rect = new GameObject(_name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.gameObject.layer = LayerMask.NameToLayer("UI");
        rect.SetParent(_parent, false);
        rect.anchorMin = new Vector2(_bLeft ? 0f : 1f, 0f);
        rect.anchorMax = new Vector2(_bLeft ? 0f : 1f, 1f);
        rect.pivot = new Vector2(_bLeft ? 0f : 1f, 0.5f);
        rect.sizeDelta = new Vector2(22f, 0f);
        rect.anchoredPosition = Vector2.zero;

        Image hit = rect.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        // 화살표 그림: 위쪽 기준으로 놓아 명판 높이(25)의 가운데에 정수 픽셀로 온다.
        _icon = NewRect("Arrow", rect);
        _icon.anchorMin = new Vector2(_bLeft ? 0f : 1f, 1f);
        _icon.anchorMax = _icon.anchorMin;
        _icon.pivot = _icon.anchorMin;
        _icon.sizeDelta = new Vector2(ArrowIconSize, ArrowIconSize);
        _icon.anchoredPosition = new Vector2(_bLeft ? 1f : -1f, -1f);
        Image arrowImage = _icon.gameObject.AddComponent<Image>();
        arrowImage.sprite = _arrow;
        arrowImage.raycastTarget = false;

        Button button = rect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = arrowImage;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = ArrowHoverColor;
        colors.pressedColor = ArrowPressedColor;
        colors.selectedColor = Color.white;
        colors.disabledColor = ArrowPressedColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        return button;
    }

    // // 기본 도우미

    private static Assets LoadAssets()
    {
        Assets a = new Assets
        {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath),
            keyIcons = AssetDatabase.LoadAssetAtPath<KeyIconDatabase>(KeyIconDatabasePath),
            bookPanel = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Panel_Book.png"),
            infoPanel = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Panel_Info.png"),
            titlePlate = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Title_Plate.png"),
            basicButton = AssetDatabase.LoadAssetAtPath<Sprite>(BasicButtonPath),
            arrowLeft = AssetDatabase.LoadAssetAtPath<Sprite>(OptionArrowLeftPath),
            arrowRight = AssetDatabase.LoadAssetAtPath<Sprite>(OptionArrowRightPath),
            iconFlag = AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "IconFlag.png"),
            iconInfinity = AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "IconInfinity.png"),
            iconTree = AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "IconTree.png"),
            iconAxe = AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "IconAxe.png"),
        };

        if (null == a.font || null == a.keyIcons || null == a.bookPanel || null == a.infoPanel || null == a.titlePlate || null == a.basicButton || null == a.arrowLeft || null == a.arrowRight)
        {
            Debug.LogError("[RoguelikeLobbyUIBuilder] 폰트 · 키 아이콘 DB · 9-슬라이스 그림을 찾지 못했습니다. 보관함 구성기(Build Roguelike Owned Cards)를 먼저 실행하십시오. (옵션 화살표 그림 경로도 확인)");
            return null;
        }
        return a;
    }

    private static RectTransform ClearChildren(GameObject _root)
    {
        RectTransform rect = (RectTransform)_root.transform;
        for (int i = rect.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(rect.GetChild(i).gameObject);
        return rect;
    }

    private static RectTransform NewRect(string _name, RectTransform _parent)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        return rect;
    }

    private static RectTransform StretchRect(string _name, RectTransform _parent)
    {
        RectTransform rect = NewRect(_name, _parent);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static Image Stretch(string _name, RectTransform _parent, Color _color)
    {
        RectTransform rect = StretchRect(_name, _parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = _color;
        image.raycastTarget = false;
        return image;
    }

    private static RectTransform Solid(string _name, RectTransform _parent, Color _color)
    {
        RectTransform rect = NewRect(_name, _parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = _color;
        image.raycastTarget = false;
        rect.sizeDelta = new Vector2(1f, 1f);
        return rect;
    }

    private static RectTransform TopLeft(string _name, RectTransform _parent, float _width, float _height)
    {
        RectTransform rect = NewRect(_name, _parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(_width, _height);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static RectTransform TopCenter(string _name, RectTransform _parent, float _width, float _height, Vector2 _pivot)
    {
        RectTransform rect = NewRect(_name, _parent);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = _pivot;
        rect.sizeDelta = new Vector2(_width, _height);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    private static RectTransform Centered(string _name, RectTransform _parent, float _width, float _height)
    {
        RectTransform rect = NewRect(_name, _parent);
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(_width, _height);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    /// <summary>판 위쪽 가장자리에 반쯤 걸치는 9-슬라이스 명판.</summary>
    private static RectTransform SlicedChild(string _name, RectTransform _parent, Sprite _sprite, float _width, float _height)
    {
        RectTransform rect = NewRect(_name, _parent);
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(_width, _height);
        rect.anchoredPosition = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return rect;
    }

    private static RectTransform IconRect(string _name, RectTransform _parent, Sprite _sprite, float _width, float _height)
    {
        RectTransform rect = NewRect(_name, _parent);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(_width, _height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.raycastTarget = false;
        image.preserveAspect = false;
        return rect;
    }

    private static TMP_Text Text(string _name, RectTransform _parent, TMP_FontAsset _font, TextAlignmentOptions _alignment, Color _color)
    {
        RectTransform rect = TopLeft(_name, _parent, 100f, SmallLineHeight);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = _font.faceInfo.pointSize;
        text.text = string.Empty;
        text.alignment = _alignment;
        text.color = _color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        rect.gameObject.AddComponent<LocalizedFontTracker>();
        return text;
    }

    private static TMP_Text GuideText(string _name, RectTransform _parent, TMP_FontAsset _font)
    {
        TMP_Text text = Text(_name, _parent, _font, TextAlignmentOptions.MidlineLeft, Color.white);
        ContentSizeFitter fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
        element.minHeight = GuideHeight;
        element.preferredHeight = GuideHeight;
        return text;
    }

    private static void StretchText(TMP_Text _text)
    {
        RectTransform rect = _text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    /// <summary>글자 아래 1px 그림자(글자를 복제해 어두운 색으로 1px 아래에 깐다).</summary>
    private static void AddShadow(TMP_Text _text)
    {
        TMP_Text shadow = UnityEngine.Object.Instantiate(_text.gameObject, _text.transform.parent).GetComponent<TMP_Text>();
        shadow.name = _text.name + "Shadow";
        shadow.color = ShadowColor;
        shadow.transform.SetSiblingIndex(_text.transform.GetSiblingIndex());
        UI_TextShadowFollower follower = shadow.gameObject.AddComponent<UI_TextShadowFollower>();
        SerializedObject so = new SerializedObject(follower);
        Set(so, "source", _text);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetCorner(RectTransform _rect, Vector2 _offset)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.pivot = new Vector2(1f, 0f);
        _rect.offsetMin = new Vector2(_offset.x, _offset.y);
        _rect.offsetMax = new Vector2(-1f + _offset.x, _offset.y);
    }

    private static void SetLeft(RectTransform _rect, float _x, float _width)
    {
        _rect.anchorMin = new Vector2(0f, 0.5f);
        _rect.anchorMax = new Vector2(0f, 0.5f);
        _rect.pivot = new Vector2(0f, 0.5f);
        _rect.sizeDelta = new Vector2(_width, SmallLineHeight);
        _rect.anchoredPosition = new Vector2(_x, 0f);
    }

    private static void SetTop(RectTransform _rect, Vector2 _anchor, Vector2 _position, Vector2 _size)
    {
        _rect.anchorMin = _anchor;
        _rect.anchorMax = _anchor;
        _rect.pivot = new Vector2(0.5f, 0.5f);
        _rect.sizeDelta = _size;
        _rect.anchoredPosition = _position;
    }

    private static RectTransform GuideRow(string _name, RectTransform _parent)
    {
        RectTransform row = TopLeft(_name, _parent, 0f, GuideHeight);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = row.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        return row;
    }

    private static UI_KeyboardImage KeyImage(string _name, RectTransform _parent, KeyIconDatabase _database, EOtherKey _key, EGamepadButton _button)
    {
        RectTransform rect = NewRect(_name, _parent);
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;
        RoguelikeHUDBuilder.CopyInventoryKeyEffect(rect.gameObject);

        rect.sizeDelta = new Vector2(KeyIconSize, KeyIconSize);
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.minWidth = KeyIconSize;
        element.minHeight = KeyIconSize;
        element.preferredWidth = KeyIconSize;
        element.preferredHeight = KeyIconSize;

        UI_KeyboardImage keyImage = rect.gameObject.AddComponent<UI_KeyboardImage>();
        SerializedObject so = new SerializedObject(keyImage);
        so.FindProperty("keyIconDatabase").objectReferenceValue = _database;
        so.FindProperty("keyIconMode").enumValueIndex = (int)EKeyIconMode.Other;
        so.FindProperty("otherKey").enumValueIndex = (int)_key;
        so.FindProperty("otherGamepadButton").enumValueIndex = (int)_button;
        so.FindProperty("targetImage").objectReferenceValue = image;
        so.ApplyModifiedPropertiesWithoutUndo();
        keyImage.RefreshIcon();
        return keyImage;
    }

    private static void Set(SerializedObject _so, string _property, UnityEngine.Object _value)
    {
        SerializedProperty property = _so.FindProperty(_property);
        if (null == property) { Debug.LogError("[RoguelikeLobbyUIBuilder] 필드를 찾지 못했습니다: " + _property); return; }
        property.objectReferenceValue = _value;
    }

    private static void SetArray(SerializedObject _so, string _property, params UnityEngine.Object[] _values)
    {
        SerializedProperty property = _so.FindProperty(_property);
        if (null == property) { Debug.LogError("[RoguelikeLobbyUIBuilder] 배열 필드를 찾지 못했습니다: " + _property); return; }
        property.arraySize = _values.Length;
        for (int i = 0; _values.Length > i; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = _values[i];
    }
}
#endif
