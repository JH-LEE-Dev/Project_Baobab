#if UNITY_EDITOR
using PresentationLayer.VFX;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 폐지(시안 v3)에 따른 UI 프리팹 구성기. 여러 번 실행해도 같은 결과로 덮어쓴다.
///   LumberBoy/UI/Build Roguelike Result     UIView_RoguelikeResult.prefab  결과 화면(딤 · 판 · 제목 명판 · 웨이브 · 행 · 카드 칸 · 버튼 2개 · 키 안내)
///   LumberBoy/UI/Build Roguelike Records    UIView_RoguelikeRecords.prefab 기록 창 - 나이테 기록판(딤 · 판 · 탭 명판(옵션 화살표 · 패드 LB/RB) · 점 3개 · 스포트라이트 · 순위 줄 견본 · 카드 보관함 · 입자 · 뒤로 버튼). 그림은 Graphics/Roguelike/records/
///   LumberBoy/UI/Build Roguelike Challenges UIView_RoguelikeChallenges.prefab 도전과제 창(기록 창과 같은 판 · 명판 · 스크롤바 · 뒤로 버튼 + 본문 위쪽 머리줄 30 + 칸 목록 뷰포트 220, 칸 템플릿 1개 = 시안 B 리본). 프리팹이 없으면 새로 만든다.
///   LumberBoy/UI/Build Roguelike Collection UIView_RoguelikeCollection.prefab 카드 도감 창(RoguelikeLobbyUIBuilder.Collection.cs)
///   LumberBoy/UI/Build Roguelike Main Menu  UI_MainMenu.prefab에 로그라이크 하위 메뉴(명판 + 버튼 6개 + 최고 기록판), UIView_MainMenu.prefab에 기록 창 · 도전과제 창 · 카드 도감 창 · 카드 DB 연결
///   LumberBoy/UI/Build Roguelike Lobby UI (All)  위 셋을 차례로
///
/// [그림] 보관함과 같은 9-슬라이스(Assets/Graphics/Roguelike/owned/Panel_Book · Panel_Info · Title_Plate), 버튼 바탕은 본편 BasicButton,
///   아이콘은 HUD 아이콘(Assets/Graphics/Roguelike/hud/). 기존 프리팹의 UIView 설정(레이어 · ESC 닫기 등)은 그대로 둔다.
/// [글자] 문구는 프리팹에 박지 않는다(RoguelikeUI.json 키를 스크립트가 채운다). 글자 오브젝트에는 LocalizedFontTracker를 붙인다.
///   숫자 · WAVE 표기처럼 언어와 무관한 글자도 같은 폰트(Galmuri11)로 만들고, 언어별 폰트 교체를 따른다.
/// [크기 - 18개 언어 규격] 줄 높이는 언어별 폰트 중 가장 큰 글자 높이 + 위아래 여백 2px 기준이다(시안 v3 측정). 폭은 화면 스크립트가 열릴 때
///   18개 언어 최소 폭(Spec*)과 현재 언어 폭 중 큰 값으로 다시 잡는다. 검수는 Localization QA 툴(목록 갱신 → 자동 검사).
/// </summary>
public static partial class RoguelikeLobbyUIBuilder
{
    private const string ResultPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeResult.prefab";
    private const string RecordsPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeRecords.prefab";
    private const string ChallengesPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeChallenges.prefab";
    private const string MainMenuPrefabPath = "Assets/Prefabs/UI/MainMenu/UI_MainMenu.prefab";
    private const string MainMenuViewPrefabPath = "Assets/Prefabs/UI/MainMenu/UIView_MainMenu.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Galmuri11_Optimum.asset";
    // 기록 창 숫자 값 전용(언어별 폰트 교체 제외 - LocalizationFontTable.excludedFonts). 정적 아틀라스: 0~9 : , + × W -
    private const string OptionPrefabPath = "Assets/Prefabs/UI/Option/UI_Option.prefab";
    private const string KeyIconDatabasePath = "Assets/Prefabs/UI/Option/KeyIconDatabase.asset";
    private const string CardDatabasePath = "Assets/Data/Roguelike/RoguelikeCardDatabase_Draft.asset";
    private const string OwnedDir = "Assets/Graphics/Roguelike/owned/";
    private const string HudDir = "Assets/Graphics/Roguelike/hud/";
    private const string RecordsDir = "Assets/Graphics/Roguelike/records/";
    private const string BasicButtonPath = "Assets/Graphics/HUD/Button/BasicButton.png";
    // 옵션 창 선택기(UI_OptionSelector)와 같은 화살표 그림 - 기록 창 탭 명판의 ◀ ▶를 이것으로 통일한다.
    private const string OptionArrowLeftPath = "Assets/Graphics/HUD/Option/Chevron-Arrow-Left-export.png";
    private const string OptionArrowRightPath = "Assets/Graphics/HUD/Option/Chevron-Arrow-Right-export.png";
    private const float ArrowIconSize = 24f;
    // 칸 레벨 배지 그림 이름(레벨 1~6)
    private static readonly string[] SlotLevelNames = { "I", "II", "III", "IV", "V", "VI" };
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

    // 스크롤바: 판 색감의 단색(그림 없음) - 홈 #3A261C, 막대 #CA8F73(누르는 동안 #8F563B는 UI_RoguelikeScrollbar가 칠한다). 굵기 3.
    private static readonly Color ScrollTrackColor = new Color32(0x3A, 0x26, 0x1C, 255);
    private static readonly Color ScrollThumbColor = new Color32(0xCA, 0x8F, 0x73, 255);
    private const float ScrollbarThickness = 3f;

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
        public Sprite infoPanelPrism;
        public Sprite infoPanelRuby;
        public Sprite cardSlotNormal;
        public Sprite cardSlotMax;
        public Sprite cardSlotPrism;
        public Sprite cardSlotRuby;
        public Sprite[] slotLevels;
        public Sprite slotLevelMax;
        public Sprite slotLevelRuby;
        public Sprite titlePlate;
        public Sprite basicButton;
        public Sprite arrowLeft;
        public Sprite arrowRight;
        public Sprite infoPanelGold;
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
        BuildChallenges();
        BuildCollection();
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
            UI_RoguelikeCardIconSlot slotTemplate = CreateIconSlot(slotRoot, a, false);

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

    // // 기록 창 (나이테 기록판 - Docs/Roguelike/_Work/RecordUI_Redesign/RecordUI_Implementation_Plan.md)

    [MenuItem("LumberBoy/UI/Build Roguelike Records")]
    public static void BuildRecords()
    {
        Assets a = LoadAssets();
        if (null == a) return;

        RecordArt r = LoadRecordArt();
        if (null == r) return;

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

            // 판 360 x 282: 탭 명판(위 가장자리에 걸침) · 점 3개 · 본문 340 x 249(스포트라이트 109 + 간격 4 + 줄 5개 24 · 간격 4, 계획서 11장).
            const float panelWidth = 360f;
            const float panelHeight = UIView_RoguelikeRecords.PanelHeight;
            RectTransform panel = Centered("Panel", rootRect, panelWidth, panelHeight);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = a.bookPanel;
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = true;
            panel.anchoredPosition = new Vector2(0f, 5f); // 위 가장자리 고정(이전 304 높이, 중심 -6과 같은 위치)

            RectTransform tabPlate = SlicedChild("TabPlate", panel, a.titlePlate, 272f, PlateHeight);
            TMP_Text tabText = Text("TabName", tabPlate, a.font, TextAlignmentOptions.Midline, CreamColor);
            AddShadow(tabText);
            StretchText(tabText);
            Button prev = ArrowButton("Prev", tabPlate, a.arrowLeft, true, out RectTransform prevArrow);
            Button next = ArrowButton("Next", tabPlate, a.arrowRight, false, out RectTransform nextArrow);

            // 패드 전용 LB · RB 아이콘: 화살표(폭 22) 바로 안쪽에 간격 1을 두고 붙인다(세로 중앙). 키보드 키는 없다(EOtherKey.None) - 패드 모드에서만 켠다.
            UI_KeyboardImage prevKey = KeyImage("TabPrevKey", tabPlate, a.keyIcons, EOtherKey.None, EGamepadButton.LeftShoulder_LB_L1);
            PlaceKeyIcon(prevKey.GetComponent<RectTransform>(), true, 22f + 1f);
            UI_KeyboardImage nextKey = KeyImage("TabNextKey", tabPlate, a.keyIcons, EOtherKey.None, EGamepadButton.RightShoulder_RB_R1);
            PlaceKeyIcon(nextKey.GetComponent<RectTransform>(), false, 22f + 1f);

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

            // 본문: 판 안쪽(테두리 4 안, 명판 아래)을 스텐실 마스크로 잘라 탭 전환 때 12px 밀려도 판 밖으로 나가지 않게 한다. 본문 원점은 판 (10, 25).
            const float bodyWidth = panelWidth - 20f;
            const float bodyHeight = 249f;
            float bodyTop = Mathf.Ceil(PlateHeight * 0.5f) + 12f;
            RectTransform bodyClip = TopLeft("BodyClip", panel, panelWidth - 8f, panelHeight - 4f - (bodyTop - 4f));
            bodyClip.anchoredPosition = new Vector2(4f, -(bodyTop - 4f));
            AddStencilMask(bodyClip);
            RectTransform content = TopLeft("Body", bodyClip, bodyWidth, bodyHeight);
            content.anchoredPosition = new Vector2(6f, -4f);
            CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

            UI_RoguelikeRecordSpotlight spotlight = CreateSpotlight(content, a, r, out RectTransform strip, out UI_RoguelikeStripScroll stripScroll,
                out RectTransform stripViewport, out RectTransform stripContent, out UI_RoguelikeCardIconSlot stripSlotTemplate, out Image stripArrowLeft, out Image stripArrowRight);

            RectTransform rowRoot = TopLeft("Rows", content, bodyWidth, bodyHeight);
            UI_RoguelikeRecordRow rowTemplate = CreateRecordRow(rowRoot, a, r, bodyWidth);

            UI_RoguelikeRecordCardGrid cardGrid = CreateRecordCardGrid(content, a, r);
            UI_RoguelikeRecordFx fx = CreateRecordFx(content, r, bodyWidth, bodyHeight);
            SetSerialized(spotlight, "fx", fx);

            // 뒤로 버튼: 옵션 창 BTN_Close를 그대로 복제(스프라이트 · 32x32 · UI_OptionButton 설정). 판 아래 테두리 바깥, 판 왼쪽 끝에 맞춘다.
            UI_OptionButton backButton = CreateBackButton(panel, a, out UI_KeyboardImage backKey);
            if (null == backButton) return;

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
            Set(so, "contentRoot", content);
            Set(so, "contentGroup", contentGroup);
            Set(so, "spotlight", spotlight);
            Set(so, "rowRoot", rowRoot);
            Set(so, "rowTemplate", rowTemplate);
            Set(so, "cardGrid", cardGrid);
            Set(so, "fx", fx);
            Set(so, "stripRoot", strip);
            Set(so, "stripScroll", stripScroll);
            Set(so, "stripViewport", stripViewport);
            Set(so, "stripContent", stripContent);
            Set(so, "stripSlotTemplate", stripSlotTemplate);
            Set(so, "stripArrowLeft", stripArrowLeft);
            Set(so, "stripArrowRight", stripArrowRight);
            Set(so, "backButton", backButton);
            SetArray(so, "padOnlyItems", prevKey.transform, nextKey.transform, backKey.transform);
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

    /// <summary>
    /// 스텐실 마스크(Mask + 보이지 않는 흰 Image). RectMask2D는 UI 셰이더가 잘린 경계를 화면 1픽셀만큼 부드럽게 섞어(안티에일리어싱)
    /// 그림 가장자리와 겹치면 반투명 줄(서브 픽셀 섞임)이 생긴다. 스텐실은 정점 경계 그대로 칼같이 자른다.
    /// 마스크 그림의 알파가 0이면 전부 잘리므로 흰색(알파 1)으로 두고 showMaskGraphic만 끈다.
    /// </summary>
    private static Image AddStencilMask(RectTransform _rect)
    {
        Image image = _rect.GetComponent<Image>();
        if (null == image) image = _rect.gameObject.AddComponent<Image>();
        image.sprite = null;
        image.color = Color.white;
        image.raycastTarget = false;
        Mask mask = _rect.gameObject.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        return image;
    }

    /// <summary>기록 창 전용 그림(Assets/Graphics/Roguelike/records/, 계획서 5-2 · 11장). 모두 Point · PPU 32 · 압축 없음.</summary>
    private class RecordArt
    {
        public Sprite[] frames;          // 금 · 은 · 동 · 나무 · 빈칸 (Panel_Info 계열 9-슬라이스)
        public Sprite tile;
        public Sprite spotRings;
        public Sprite[] medals28;
        public Sprite medal28Empty;
        public Sprite medal28Stump;
        public Sprite[] medals19;
        public Sprite medal19Empty;
        public Sprite[] keyIcons;        // 금 깃발 · 빨강 깃발 · 흰 깃발 · ∞ (16)
        public Sprite hudTree;
        public Sprite clock16;
        public Sprite clock8;
        public Sprite card16;
        public Sprite card8;
        public Sprite tree8;
        public Sprite[] smallKeys;       // 금 · 빨강 · 흰 깃발 10 · ∞ 민트 10x8
        public Sprite slotMax;
        public Sprite badgeNew;
        public Sprite[] stamps;          // CLEAR · GAME OVER · RETIRE
        public Sprite[] pips8;           // off · gold · red · white · head (8x7)
        public Sprite[] pips8Tall;       // off · gold · red · white · head (8x9)
        public Sprite[] pips4;           // off · gold · red · white · mint (4x5)
        public Sprite track;
        public Sprite shutterTile;
        public Sprite[] digits;          // 큰 숫자: 금 · 흰색 · 민트 · 빨강 x 12
        public Sprite[] smallDigits;     // 작은 숫자: 크림 · 금 · 빨강 · 민트 · 흐린 크림 x 14
        public Sprite[] fxShapes;
        public Sprite arrowLeft;
        public Sprite arrowRight;
    }

    private static RecordArt LoadRecordArt()
    {
        RecordArt r = new RecordArt
        {
            frames = new[] { AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Panel_Info_Gold.png"), R("Panel_Info_Silver"), R("Panel_Info_Bronze"),
                AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Panel_Info.png"), R("Panel_Info_Empty") },
            tile = R("Panel_Info_Tile"),
            spotRings = R("SpotRings"),
            medals28 = new[] { R("Medal28_1"), R("Medal28_2"), R("Medal28_3"), R("Medal28_4"), R("Medal28_5") },
            medal28Empty = R("Medal28_Empty"),
            medal28Stump = R("Medal28_Stump"),
            medals19 = new[] { R("Medal19_1"), R("Medal19_2"), R("Medal19_3"), R("Medal19_4"), R("Medal19_5") },
            medal19Empty = R("Medal19_Empty"),
            keyIcons = new[] { AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "HudIconFlagGold.png"), AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "HudIconFlag.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "HudIconFlagWhite.png"), AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "HudIconInfinity.png") },
            hudTree = AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "HudIconTree.png"),
            clock16 = R("IconClock16"),
            clock8 = R("IconClock8"),
            card16 = R("IconCard16"),
            card8 = R("IconCard8"),
            tree8 = R("IconTree8_Green"),
            smallKeys = new[] { R("IconFlag10_Gold"), R("IconFlag10_Red"), R("IconFlag10_White"), R("IconInfinity10_Mint") },
            slotMax = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "SlotLevel_MAX.png"),
            badgeNew = R("BadgeNew"),
            stamps = new[] { R("Stamp_Clear"), R("Stamp_GameOver"), R("Stamp_Retire") },
            pips8 = new[] { R("Pip8_off"), R("Pip8_gold"), R("Pip8_red"), R("Pip8_white"), R("Pip8_head") },
            pips8Tall = new[] { R("Pip8Tall_off"), R("Pip8Tall_gold"), R("Pip8Tall_red"), R("Pip8Tall_white"), R("Pip8Tall_head") },
            pips4 = new[] { R("Pip4_off"), R("Pip4_gold"), R("Pip4_red"), R("Pip4_white"), R("Pip4_mint") },
            track = R("Track9"),
            shutterTile = R("ShutterTile"),
            digits = new Sprite[UI_RoguelikeBigNumber.GlyphsPerPalette * 4],
            smallDigits = new Sprite[UI_RoguelikeSmallNumber.GlyphsPerPalette * 5],
            fxShapes = new[] { R("Fx_Dot1"), R("Fx_Dot2"), R("Fx_Plus3"), R("Fx_X3"), R("Fx_Diamond3") },
            arrowLeft = R("StripArrow_L"),
            arrowRight = R("StripArrow_R"),
        };

        // 큰 숫자: 팔레트(금 · 흰색 · 민트 · 빨강)마다 12칸(0~9, colon, comma)으로 자른 스프라이트
        LoadSheet("BigDigits_", new[] { "gold", "white", "mint", "red" },
            new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "colon", "comma" }, r.digits);
        // 작은 숫자: 팔레트(크림 · 금 · 빨강 · 민트 · 흐린 크림)마다 14칸(0~9, colon, comma, times, W)
        LoadSheet("SmallDigits_", new[] { "cream", "gold", "red", "mint", "muted" },
            new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "colon", "comma", "times", "W" }, r.smallDigits);

        bool bMissing = 0 <= System.Array.IndexOf(r.frames, null) || 0 <= System.Array.IndexOf(r.medals28, null) || 0 <= System.Array.IndexOf(r.medals19, null)
            || 0 <= System.Array.IndexOf(r.keyIcons, null) || 0 <= System.Array.IndexOf(r.smallKeys, null) || 0 <= System.Array.IndexOf(r.stamps, null)
            || 0 <= System.Array.IndexOf(r.pips8, null) || 0 <= System.Array.IndexOf(r.pips8Tall, null) || 0 <= System.Array.IndexOf(r.pips4, null)
            || 0 <= System.Array.IndexOf(r.digits, null) || 0 <= System.Array.IndexOf(r.smallDigits, null) || 0 <= System.Array.IndexOf(r.fxShapes, null)
            || null == r.tile || null == r.spotRings || null == r.medal28Empty || null == r.medal28Stump || null == r.medal19Empty || null == r.hudTree
            || null == r.clock16 || null == r.clock8 || null == r.card16 || null == r.card8 || null == r.tree8 || null == r.slotMax || null == r.badgeNew
            || null == r.track || null == r.shutterTile || null == r.arrowLeft || null == r.arrowRight;
        if (true == bMissing)
        {
            Debug.LogError("[RoguelikeLobbyUIBuilder] 기록 창 그림을 찾지 못했습니다: " + RecordsDir + " (Docs/Roguelike/_Work/RecordUI_Redesign/art/art_v2.py로 만든 그림)");
            return null;
        }
        return r;
    }

    /// <summary>여러 장으로 자른 글자 시트(_prefix + 팔레트 + .png, 하위 이름 _prefix + 팔레트 + _ + 글자)를 팔레트 순 x 글자 순으로 _into에 채운다.</summary>
    private static void LoadSheet(string _prefix, string[] _palettes, string[] _glyphs, Sprite[] _into)
    {
        for (int p = 0; _palettes.Length > p; p++)
        {
            UnityEngine.Object[] subs = AssetDatabase.LoadAllAssetRepresentationsAtPath(RecordsDir + _prefix + _palettes[p] + ".png");
            for (int g = 0; _glyphs.Length > g; g++)
            {
                string name = _prefix + _palettes[p] + "_" + _glyphs[g];
                for (int s = 0; subs.Length > s; s++)
                {
                    if (subs[s] is Sprite sprite && sprite.name == name) _into[p * _glyphs.Length + g] = sprite;
                }
            }
        }
    }

    private static Sprite R(string _name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(RecordsDir + _name + ".png");
    }

    /// <summary>원본 크기 그대로 왼쪽 위 기준에 놓는 그림(팻 픽셀 방지: 크기 = 스프라이트 원본).</summary>
    private static Image PixelImage(string _name, RectTransform _parent, Sprite _sprite, float _x, float _y)
    {
        RectTransform rect = TopLeft(_name, _parent, null != _sprite ? _sprite.rect.width : 1f, null != _sprite ? _sprite.rect.height : 1f);
        rect.anchoredPosition = new Vector2(_x, -_y);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.raycastTarget = false;
        image.preserveAspect = false;
        return image;
    }

    /// <summary>9-슬라이스 그림(테두리 원본 크기, 가운데만 늘어난다).</summary>
    private static Image SlicedImage(string _name, RectTransform _parent, Sprite _sprite, float _x, float _y, float _width, float _height)
    {
        RectTransform rect = TopLeft(_name, _parent, _width, _height);
        rect.anchoredPosition = new Vector2(_x, -_y);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>
    /// 문구 글자(12px, 세로 가운데 = _centerY, 왼쪽 정렬). 언어별 폰트 교체를 따른다(빈 기록 안내 문구 전용 - 숫자는 모두 작은 숫자 그림 글꼴).
    /// 세로는 위쪽 정렬 + UI_RoguelikePixelText가 기준선을 정수 픽셀에 맞춘다(Midline 정렬은 폰트 메트릭 평균이라 반 픽셀이 될 수 있다).
    /// </summary>
    private static TMP_Text ValueText(string _name, RectTransform _parent, TMP_FontAsset _font, Color _color, float _x, float _centerY, float _width)
    {
        TMP_Text text = Text(_name, _parent, _font, TextAlignmentOptions.TopLeft, _color);
        text.rectTransform.sizeDelta = new Vector2(_width, SmallLineHeight);
        text.rectTransform.anchoredPosition = new Vector2(_x, -(_centerY - SmallLineHeight * 0.5f));
        UI_RoguelikePixelText pixel = text.gameObject.AddComponent<UI_RoguelikePixelText>();
        SerializedObject so = new SerializedObject(pixel);
        Set(so, "text", text);
        so.FindProperty("centerY").floatValue = _centerY;
        so.ApplyModifiedPropertiesWithoutUndo();
        pixel.Apply();
        return text;
    }

    /// <summary>
    /// 작은 숫자(그림 글꼴 칸 7x9): 상자 (_x, _y) 폭 _width x 높이 9, 글자 Image 10개(크기 · 위치는 스크립트가 정수로 정한다).
    /// _bRight가 참이면 상자 오른쪽 끝(_x + _width)에 외곽선 끝을 맞춘다.
    /// </summary>
    private static UI_RoguelikeSmallNumber CreateSmallNumber(string _name, RectTransform _parent, RecordArt _r, float _x, float _y, float _width, bool _bRight)
    {
        RectTransform root = TopLeft(_name, _parent, _width, 9f);
        root.anchoredPosition = new Vector2(_x, -_y);
        Image[] glyphs = new Image[UI_RoguelikeSmallNumber.MaxChars];
        for (int i = 0; UI_RoguelikeSmallNumber.MaxChars > i; i++)
        {
            glyphs[i] = PixelImage("G" + i, root, _r.smallDigits[0], 0f, 0f);
            glyphs[i].gameObject.SetActive(false);
        }

        UI_RoguelikeSmallNumber comp = root.gameObject.AddComponent<UI_RoguelikeSmallNumber>();
        SerializedObject so = new SerializedObject(comp);
        SetArray(so, "glyphs", glyphs);
        SetArray(so, "sprites", _r.smallDigits);
        so.FindProperty("bRightAlign").boolValue = _bRight;
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    private static void SetSerialized(UnityEngine.Object _target, string _property, UnityEngine.Object _value)
    {
        SerializedObject so = new SerializedObject(_target);
        Set(so, _property, _value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>
    /// 스포트라이트(340x109, 최근 탭은 스크립트가 65로 줄인다 - 계획서 11장): 틀 · 나이테 무늬 · 메달 · 기준 아이콘 + 큰 숫자 · 칩 2x2 · 큰 길 · 끝 깃발 · ∞+n · 카드 띠 · 넘침 화살표 · 도장 · 빈 기록 문구 · 덧문.
    /// 가로 안쪽 여백 12(내용 x 12~328), 위 여백 12. 좌표는 모두 스포트라이트 왼쪽 위 기준 정수 픽셀.
    /// 틀 · 아래 덧문은 높이가 바뀌어도 따라가도록 늘어남 / 아래 붙음 앵커를 쓴다.
    /// </summary>
    private static UI_RoguelikeRecordSpotlight CreateSpotlight(RectTransform _parent, Assets _a, RecordArt _r, out RectTransform _strip, out UI_RoguelikeStripScroll _stripScroll,
        out RectTransform _stripViewport, out RectTransform _stripContent, out UI_RoguelikeCardIconSlot _stripSlotTemplate, out Image _arrowLeft, out Image _arrowRight)
    {
        const float height = UI_RoguelikeRecordSpotlight.RankingHeight;
        RectTransform spot = TopLeft("Spotlight", _parent, 340f, height);
        CanvasGroup group = spot.gameObject.AddComponent<CanvasGroup>();
        Image frame = SlicedImage("Frame", spot, _r.frames[0], 0f, 0f, 340f, height);
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = Vector2.zero;
        frameRect.anchorMax = Vector2.one;
        frameRect.offsetMin = Vector2.zero;
        frameRect.offsetMax = Vector2.zero;
        Image rings = PixelImage("Rings", spot, _r.spotRings, 150f, 5f);

        RectTransform medal = TopLeft("Medal", spot, 28f, 28f);
        medal.anchoredPosition = new Vector2(12f, -14f);
        Image medalImage = medal.gameObject.AddComponent<Image>();
        medalImage.sprite = _r.medals28[0];
        medalImage.raycastTarget = false;

        // 머리 줄(y 12~44): 기준 아이콘 16 (46, 20) + 큰 숫자 (65, 20) - 아이콘 끝 62에서 3px.
        RectTransform key = TopLeft("Key", spot, 200f, 44f);
        CanvasGroup keyGroup = key.gameObject.AddComponent<CanvasGroup>();
        Image keyIcon = PixelImage("KeyIcon", key, _r.keyIcons[0], 46f, 20f);
        UI_RoguelikeBigNumber bigNumber = CreateBigNumber(key, _r, 65f, 20f);

        // 칩 2x2: 열 x 212 / 276, 줄 y 12 / 28. 아이콘 16 칸 + 3 + 값(왼쪽 정렬, 세로 y+3).
        RectTransform[] chipRects = new RectTransform[UI_RoguelikeRecordSpotlight.ChipCount];
        CanvasGroup[] chipGroups = new CanvasGroup[UI_RoguelikeRecordSpotlight.ChipCount];
        Image[] chipIcons = new Image[UI_RoguelikeRecordSpotlight.ChipCount];
        UI_RoguelikeSmallNumber[] chipNumbers = new UI_RoguelikeSmallNumber[UI_RoguelikeRecordSpotlight.ChipCount];
        for (int i = 0; UI_RoguelikeRecordSpotlight.ChipCount > i; i++)
        {
            RectTransform chip = TopLeft("Chip" + i, spot, 60f, 16f);
            chip.anchoredPosition = new Vector2(212f + 64f * (i % 2), -(12f + 16f * (i / 2)));
            chipRects[i] = chip;
            chipGroups[i] = chip.gameObject.AddComponent<CanvasGroup>();
            chipIcons[i] = PixelImage("Icon", chip, _r.hudTree, 0f, 0f);
            chipNumbers[i] = CreateSmallNumber("Value", chip, _r, 19f, 3f, 41f, false);
        }

        // 웨이브 길(y 47~57): 칸 8x7 간격 1(x 12~280) + 끝 깃발 10 (284, 47).
        UI_RoguelikeWaveTrail trail = CreateWaveTrail(spot, _r, 12f, 50f, true);
        Image flag = PixelImage("EndFlag", spot, _r.smallKeys[0], 284f, 47f);
        CanvasGroup flagGroup = flag.gameObject.AddComponent<CanvasGroup>();

        // ∞ + 단계: 숫자 상자 오른쪽 끝 329(외곽선 끝 = 내용 끝선), ∞ 위치는 스크립트가 숫자 폭으로 정한다.
        RectTransform unlimit = TopLeft("Unlimit", spot, 340f, 60f);
        CanvasGroup unlimitGroup = unlimit.gameObject.AddComponent<CanvasGroup>();
        Image unlimitIcon = PixelImage("Icon", unlimit, _r.smallKeys[3], 300f, 50f);
        UI_RoguelikeSmallNumber unlimitNumber = CreateSmallNumber("Value", unlimit, _r, 269f, 49f, 60f, true);

        // 카드 띠(랭킹 탭): (12, 61) 316x36 = 8칸 x 40 - 4. 가로 스크롤 + 스텐실 마스크 + 칸 견본(선택 대상 + CanvasGroup).
        _strip = TopLeft("Strip", spot, 316f, 36f);
        _strip.anchoredPosition = new Vector2(12f, -61f);
        _stripViewport = TopLeft("Viewport", _strip, 316f, 36f);
        Image stripCatch = AddStencilMask(_stripViewport);
        stripCatch.raycastTarget = true;
        _stripContent = TopLeft("Content", _stripViewport, 316f, 36f);
        _stripSlotTemplate = CreateIconSlot(_stripContent, _a, true);
        _stripSlotTemplate.name = "StripSlotTemplate";
        _stripSlotTemplate.gameObject.AddComponent<CanvasGroup>();
        _stripScroll = _strip.gameObject.AddComponent<UI_RoguelikeStripScroll>();
        _stripScroll.viewport = _stripViewport;
        _stripScroll.content = _stripContent;
        _stripScroll.horizontal = true;
        _stripScroll.vertical = false;
        _stripScroll.movementType = ScrollRect.MovementType.Clamped;
        _stripScroll.inertia = true;
        _strip.gameObject.SetActive(false);
        // 넘침 화살표 3x5: 띠 양옆 여백(4 ~ 12, 328 ~ 336)의 가운데, 띠 세로 가운데(61 + 18 - 3).
        _arrowLeft = PixelImage("StripArrowL", spot, _r.arrowLeft, 6f, 76f);
        _arrowRight = PixelImage("StripArrowR", spot, _r.arrowRight, 331f, 76f);
        _arrowLeft.gameObject.SetActive(false);
        _arrowRight.gameObject.SetActive(false);

        // 도장: 위치는 스크립트가 큰 숫자 최종 폭으로 정한다.
        Image stamp = PixelImage("Stamp", spot, _r.stamps[0], 120f, 17f);
        stamp.gameObject.SetActive(false);

        TMP_Text empty = ValueText("Empty", spot, _a.font, MutedColor, 0f, 28f, 340f);
        empty.gameObject.SetActive(false);

        // 덧문: 고정 크기 타일(332x51 = 안쪽 101의 위쪽 절반)을 스텐실 마스크 안에 두고 스크립트가 마스크 높이만 정수로 바꾼다(무늬가 밀리지 않는다).
        const float shutterTileHeight = 51f;
        RectTransform shutterTop = TopLeft("ShutterTop", spot, 332f, 0f);
        shutterTop.anchoredPosition = new Vector2(4f, -4f);
        AddStencilMask(shutterTop);
        Image topTile = TiledImage("Tile", shutterTop, _r.shutterTile, 332f, shutterTileHeight);
        topTile.rectTransform.anchoredPosition = Vector2.zero;
        RectTransform shutterBottom = NewRect("ShutterBottom", spot);
        shutterBottom.anchorMin = Vector2.zero;
        shutterBottom.anchorMax = Vector2.zero;
        shutterBottom.pivot = Vector2.zero;
        shutterBottom.sizeDelta = new Vector2(332f, 0f);
        shutterBottom.anchoredPosition = new Vector2(4f, 4f);
        AddStencilMask(shutterBottom);
        Image bottomTile = TiledImage("Tile", shutterBottom, _r.shutterTile, 332f, shutterTileHeight);
        RectTransform bottomTileRect = bottomTile.rectTransform;
        bottomTileRect.anchorMin = Vector2.zero;
        bottomTileRect.anchorMax = Vector2.zero;
        bottomTileRect.pivot = Vector2.zero;
        bottomTileRect.anchoredPosition = Vector2.zero;
        shutterTop.gameObject.SetActive(false);
        shutterBottom.gameObject.SetActive(false);
        RectTransform seamTop = TopLeft("SeamTop", spot, 332f, 1f);
        Image seamTopImage = seamTop.gameObject.AddComponent<Image>();
        seamTopImage.raycastTarget = false;
        seamTop.gameObject.SetActive(false);
        RectTransform seamBottom = TopLeft("SeamBottom", spot, 332f, 1f);
        Image seamBottomImage = seamBottom.gameObject.AddComponent<Image>();
        seamBottomImage.raycastTarget = false;
        seamBottom.gameObject.SetActive(false);

        UI_RoguelikeRecordSpotlight comp = spot.gameObject.AddComponent<UI_RoguelikeRecordSpotlight>();
        SerializedObject so = new SerializedObject(comp);
        Set(so, "group", group);
        Set(so, "frameImage", frame);
        SetArray(so, "frameSprites", _r.frames);
        Set(so, "ringPattern", rings.gameObject);
        Set(so, "medalRect", medal);
        Set(so, "medalImage", medalImage);
        SetArray(so, "medalSprites", _r.medals28);
        Set(so, "medalEmptySprite", _r.medal28Empty);
        Set(so, "medalStumpSprite", _r.medal28Stump);
        Set(so, "keyGroup", keyGroup);
        Set(so, "keyIconImage", keyIcon);
        SetArray(so, "keyIconSprites", _r.keyIcons);
        Set(so, "bigNumber", bigNumber);
        Set(so, "stampRect", stamp.rectTransform);
        Set(so, "stampImage", stamp);
        SetArray(so, "stampSprites", _r.stamps);
        SetArray(so, "chipRects", chipRects);
        SetArray(so, "chipGroups", chipGroups);
        SetArray(so, "chipIcons", chipIcons);
        SetArray(so, "chipNumbers", chipNumbers);
        Set(so, "treeSprite", _r.hudTree);
        Set(so, "cardSprite", _r.card16);
        Set(so, "clockSprite", _r.clock16);
        Set(so, "clearTimeSprite", _r.keyIcons[0]);
        Set(so, "maxSprite", _r.slotMax);
        Set(so, "trail", trail);
        Set(so, "flagRect", flag.rectTransform);
        Set(so, "flagGroup", flagGroup);
        Set(so, "flagImage", flag);
        SetArray(so, "flagSprites", _r.smallKeys[0], _r.smallKeys[1], _r.smallKeys[2]);
        Set(so, "unlimitGroup", unlimitGroup);
        Set(so, "unlimitIcon", unlimitIcon.rectTransform);
        Set(so, "unlimitNumber", unlimitNumber);
        Set(so, "shutterTop", shutterTop);
        Set(so, "shutterBottom", shutterBottom);
        Set(so, "seamTop", seamTopImage);
        Set(so, "seamBottom", seamBottomImage);
        Set(so, "emptyText", empty);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    private static Image TiledImage(string _name, RectTransform _parent, Sprite _sprite, float _width, float _height)
    {
        RectTransform rect = TopLeft(_name, _parent, _width, _height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.type = Image.Type.Tiled;
        image.pixelsPerUnitMultiplier = 1f;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>큰 숫자 자리 8칸: 자리마다 스텐실 마스크(10x15) 안에 지금 숫자 · 다음 숫자 Image. 위치 · 크기는 스크립트가 정수로 정한다.</summary>
    private static UI_RoguelikeBigNumber CreateBigNumber(RectTransform _parent, RecordArt _r, float _x, float _y)
    {
        RectTransform root = TopLeft("BigNumber", _parent, 80f, 15f);
        root.anchoredPosition = new Vector2(_x, -_y);
        RectTransform[] cells = new RectTransform[UI_RoguelikeBigNumber.MaxChars];
        Image[] glyphs = new Image[UI_RoguelikeBigNumber.MaxChars];
        Image[] nexts = new Image[UI_RoguelikeBigNumber.MaxChars];
        for (int i = 0; UI_RoguelikeBigNumber.MaxChars > i; i++)
        {
            cells[i] = TopLeft("Cell" + i, root, 10f, 15f);
            AddStencilMask(cells[i]);
            glyphs[i] = PixelImage("Glyph", cells[i], _r.digits[0], 0f, 0f);
            nexts[i] = PixelImage("Next", cells[i], _r.digits[1], 0f, 14f);
            nexts[i].gameObject.SetActive(false);
            cells[i].gameObject.SetActive(false);
        }

        UI_RoguelikeBigNumber comp = root.gameObject.AddComponent<UI_RoguelikeBigNumber>();
        SerializedObject so = new SerializedObject(comp);
        SetArray(so, "cells", cells);
        SetArray(so, "glyphs", glyphs);
        SetArray(so, "nextGlyphs", nexts);
        SetArray(so, "sprites", _r.digits);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    /// <summary>
    /// 웨이브 길 30칸(계획서 11장). 큰 길: 칸 8x7 · 간격 1(폭 269), 10단위 칸 8x9(위로 2), 바탕 홈(Track9 9-슬라이스 271x9) + 10단위 칸 위 홈 조각 10x3.
    /// 작은 길: 칸 4x5 · 간격 1(폭 149), 홈 151x7. 위치는 부모 왼쪽 위 기준 (_x, _y) 정수 픽셀.
    /// </summary>
    private static UI_RoguelikeWaveTrail CreateWaveTrail(RectTransform _parent, RecordArt _r, float _x, float _y, bool _bBig)
    {
        int pipWidth = true == _bBig ? 8 : 4;
        int pipHeight = true == _bBig ? 7 : 5;
        int step = pipWidth + 1;
        int total = UI_RoguelikeWaveTrail.PipCount * step - 1;

        RectTransform root = TopLeft("Trail", _parent, total, pipHeight);
        root.anchoredPosition = new Vector2(_x, -_y);
        CanvasGroup group = root.gameObject.AddComponent<CanvasGroup>();
        SlicedImage("Track", root, _r.track, -1f, -1f, total + 2f, pipHeight + 2f);

        Sprite[] pips = true == _bBig ? _r.pips8 : _r.pips4;
        Image[] images = new Image[UI_RoguelikeWaveTrail.PipCount];
        for (int i = 0; UI_RoguelikeWaveTrail.PipCount > i; i++)
        {
            bool bTall = true == _bBig && 9 == i % 10;
            if (true == bTall) SlicedImage("TrackBump" + i, root, _r.track, i * step - 1f, -3f, pipWidth + 2f, 3f);
            images[i] = PixelImage("Pip" + i, root, true == bTall ? _r.pips8Tall[0] : pips[0], i * step, true == bTall ? -2f : 0f);
        }

        UI_RoguelikeWaveTrail comp = root.gameObject.AddComponent<UI_RoguelikeWaveTrail>();
        SerializedObject so = new SerializedObject(comp);
        SetArray(so, "pips", images);
        Set(so, "offSprite", pips[0]);
        Set(so, "goldSprite", pips[1]);
        Set(so, "redSprite", pips[2]);
        Set(so, "whiteSprite", pips[3]);
        Set(so, "mintSprite", true == _bBig ? _r.pips8[1] : _r.pips4[4]);
        Set(so, "headSprite", true == _bBig ? _r.pips8[4] : null);
        Set(so, "offTallSprite", true == _bBig ? _r.pips8Tall[0] : null);
        Set(so, "goldTallSprite", true == _bBig ? _r.pips8Tall[1] : null);
        Set(so, "redTallSprite", true == _bBig ? _r.pips8Tall[2] : null);
        Set(so, "whiteTallSprite", true == _bBig ? _r.pips8Tall[3] : null);
        Set(so, "headTallSprite", true == _bBig ? _r.pips8Tall[4] : null);
        Set(so, "group", group);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    /// <summary>
    /// 순위 줄 견본(340x24, 계획서 11장): 바탕 Panel_Info · 메달 19 (12, 2) · 결과 깃발 10 / ∞ (34) + 기준값 (47) · 작은 길 30칸 (91, 9) ·
    /// 카드 아이콘 (251, 8) + 장수(오른쪽 끝 273) · 나무 / 시계 아이콘 (282) + 값(오른쪽 끝 329) · NEW 딱지 (22, -4). 숫자는 모두 작은 숫자 그림 글꼴.
    /// 줄 전체가 선택 · 클릭 대상이다(탐색은 화면 스크립트가 기록이 있는 줄끼리 위 / 아래로 Explicit으로 잇는다). 포커스 표시는 커서 상자가 하므로 색 전환은 없다.
    /// </summary>
    private static UI_RoguelikeRecordRow CreateRecordRow(RectTransform _parent, Assets _a, RecordArt _r, float _width)
    {
        RectTransform row = TopLeft("RowTemplate", _parent, _width, 24f);
        Image bg = row.gameObject.AddComponent<Image>();
        bg.sprite = _a.infoPanel;
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = true;
        CanvasGroup group = row.gameObject.AddComponent<CanvasGroup>();

        Button button = row.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = bg;
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit };

        Image medal = PixelImage("Medal", row, _r.medals19[0], 12f, 2f);
        Image keyIcon = PixelImage("KeyIcon", row, _r.smallKeys[0], 34f, 7f);
        UI_RoguelikeSmallNumber keyNumber = CreateSmallNumber("KeyNumber", row, _r, 47f, 7f, 40f, false);
        UI_RoguelikeWaveTrail trail = CreateWaveTrail(row, _r, 91f, 9f, false);
        Image cardIcon = PixelImage("CardIcon", row, _r.card8, 251f, 8f);
        UI_RoguelikeSmallNumber cardCount = CreateSmallNumber("CardCount", row, _r, 260f, 7f, 13f, true);
        Image rightIcon = PixelImage("RightIcon", row, _r.tree8, 282f, 8f);
        UI_RoguelikeSmallNumber rightNumber = CreateSmallNumber("RightNumber", row, _r, 292f, 7f, 37f, true);
        Image badge = PixelImage("New", row, _r.badgeNew, 22f, -4f);
        badge.gameObject.SetActive(false);

        UI_RoguelikeRecordRow comp = row.gameObject.AddComponent<UI_RoguelikeRecordRow>();
        SerializedObject so = new SerializedObject(comp);
        Set(so, "button", button);
        Set(so, "background", bg);
        Set(so, "openSprite", _a.infoPanelGold);
        Set(so, "emptySprite", _r.frames[4]);
        Set(so, "group", group);
        Set(so, "medalImage", medal);
        SetArray(so, "medalSprites", _r.medals19);
        Set(so, "medalEmptySprite", _r.medal19Empty);
        Set(so, "keyIconImage", keyIcon);
        Set(so, "flagClearSprite", _r.smallKeys[0]);
        Set(so, "flagOverSprite", _r.smallKeys[1]);
        Set(so, "flagRetireSprite", _r.smallKeys[2]);
        Set(so, "unlimitSprite", _r.smallKeys[3]);
        Set(so, "keyNumber", keyNumber);
        Set(so, "trail", trail);
        Set(so, "cardIcon", cardIcon.gameObject);
        Set(so, "cardCountNumber", cardCount);
        Set(so, "rightIconImage", rightIcon);
        Set(so, "treeSprite", _r.tree8);
        Set(so, "clockSprite", _r.clock8);
        Set(so, "rightNumber", rightNumber);
        Set(so, "newBadge", badge.rectTransform);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    /// <summary>
    /// 최근 기록 탭의 카드 보관함(계획서 11장): 받침(0, 69) 340x180 = 스포트라이트(65) 아래 4px, 빈 홈 32개 + 카드 칸 32개(선택 대상 + CanvasGroup),
    /// 칸 (12 + 40 · 열, 81 + 40 · 줄) - 받침 안쪽 여백 사방 12, 칸 간격 4.
    /// </summary>
    private static UI_RoguelikeRecordCardGrid CreateRecordCardGrid(RectTransform _parent, Assets _a, RecordArt _r)
    {
        RectTransform root = TopLeft("CardGrid", _parent, 340f, 249f);
        RectTransform tray = TopLeft("Tray", root, 340f, 249f);
        CanvasGroup trayGroup = tray.gameObject.AddComponent<CanvasGroup>();
        SlicedImage("Base", tray, _r.tile, 0f, 69f, 340f, 180f);

        UI_RoguelikeCardIconSlot[] slots = new UI_RoguelikeCardIconSlot[UI_RoguelikeRecordCardGrid.Capacity];
        for (int i = 0; UI_RoguelikeRecordCardGrid.Capacity > i; i++)
        {
            float x = 12f + 40f * (i % UI_RoguelikeRecordCardGrid.Columns);
            float y = 81f + 40f * (i / UI_RoguelikeRecordCardGrid.Columns);
            SlicedImage("Socket" + i, tray, _r.frames[4], x, y, UI_RoguelikeCardIconSlot.SlotSize, UI_RoguelikeCardIconSlot.SlotSize);
        }
        for (int i = 0; UI_RoguelikeRecordCardGrid.Capacity > i; i++)
        {
            slots[i] = CreateIconSlot(root, _a, true);
            slots[i].name = "Slot" + i;
            slots[i].gameObject.AddComponent<CanvasGroup>();
            slots[i].Rect.anchoredPosition = new Vector2(12f + 40f * (i % UI_RoguelikeRecordCardGrid.Columns), -(81f + 40f * (i / UI_RoguelikeRecordCardGrid.Columns)));
            slots[i].gameObject.SetActive(false);
        }

        UI_RoguelikeRecordCardGrid comp = root.gameObject.AddComponent<UI_RoguelikeRecordCardGrid>();
        SerializedObject so = new SerializedObject(comp);
        SetArray(so, "slots", slots);
        Set(so, "trayGroup", trayGroup);
        so.ApplyModifiedPropertiesWithoutUndo();
        root.gameObject.SetActive(false);
        return comp;
    }

    /// <summary>입자 풀(24개, 본문 맨 위에 그린다). 크기 · 위치 · 색은 스크립트가 정한다.</summary>
    private static UI_RoguelikeRecordFx CreateRecordFx(RectTransform _parent, RecordArt _r, float _width, float _height)
    {
        RectTransform root = TopLeft("Fx", _parent, _width, _height);
        Image[] images = new Image[UI_RoguelikeRecordFx.PoolSize];
        for (int i = 0; UI_RoguelikeRecordFx.PoolSize > i; i++)
        {
            images[i] = PixelImage("P" + i, root, _r.fxShapes[0], 0f, 0f);
            images[i].gameObject.SetActive(false);
        }

        UI_RoguelikeRecordFx comp = root.gameObject.AddComponent<UI_RoguelikeRecordFx>();
        SerializedObject so = new SerializedObject(comp);
        SetArray(so, "images", images);
        SetArray(so, "shapeSprites", _r.fxShapes);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    // // 도전과제 창

    [MenuItem("LumberBoy/UI/Build Roguelike Challenges")]
    public static void BuildChallenges()
    {
        Assets a = LoadAssets();
        if (null == a) return;

        // 프리팹이 없으면 빈 루트(UIView_RoguelikeChallenges)로 새로 만들고, UIView 설정(레이어 · ESC 닫기 · 오버레이)은 기록 창 프리팹과 같게 맞춘다.
        bool bNew = null == AssetDatabase.LoadAssetAtPath<GameObject>(ChallengesPrefabPath);
        GameObject root;
        if (true == bNew)
        {
            root = new GameObject("UIView_RoguelikeChallenges", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
            root.AddComponent<UIView_RoguelikeChallenges>();
        }
        else
        {
            root = PrefabUtility.LoadPrefabContents(ChallengesPrefabPath);
        }

        try
        {
            UIView_RoguelikeChallenges view = root.GetComponent<UIView_RoguelikeChallenges>();
            if (null == view) { Debug.LogError("[RoguelikeLobbyUIBuilder] UIView_RoguelikeChallenges 컴포넌트가 없습니다."); return; }

            Material clearMaterial = GetOrCreateClearGlowMaterial();
            if (null == clearMaterial) return;

            RectTransform rootRect = ClearChildren(root);

            // 달성 칸 파티클이 30fps로 메쉬를 다시 만들어도 메인메뉴 전체 UI가 다시 배칭되지 않도록 창 루트를 자체 Canvas로 격리한다(다른 로그라이크 창 프리팹에는 선례가 없어 overrideSorting 없이 부모 Canvas를 따른다).
            if (null == root.GetComponent<Canvas>())
            {
                Canvas rootCanvas = root.AddComponent<Canvas>();
                rootCanvas.overrideSorting = false;
            }

            if (null == root.GetComponent<GraphicRaycaster>()) root.AddComponent<GraphicRaycaster>();

            if (true == bNew)
            {
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                UIView_RoguelikeRecords records = AssetDatabase.LoadAssetAtPath<UIView_RoguelikeRecords>(RecordsPrefabPath);
                if (null != records)
                {
                    SerializedObject source = new SerializedObject(records);
                    SerializedObject target = new SerializedObject(view);
                    target.FindProperty("layer").intValue = source.FindProperty("layer").intValue;
                    target.FindProperty("bCloseableByESC").boolValue = source.FindProperty("bCloseableByESC").boolValue;
                    target.FindProperty("bOverlay").boolValue = source.FindProperty("bOverlay").boolValue;
                    target.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Image dim = Stretch("Dim", rootRect, DimColor);
            dim.raycastTarget = true;
            CanvasGroup dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.navigation = new Navigation { mode = Navigation.Mode.None };

            // 판 360 x 282, 명판 272, 본문 340 x 250, 스크롤바, 뒤로 버튼은 기록 창과 같은 수치다. 탭 화살표 · LB/RB · 점 · 카드 띠는 없다.
            const float panelWidth = 360f;
            const float panelHeight = UIView_RoguelikeRecords.PanelHeight;
            RectTransform panel = Centered("Panel", rootRect, panelWidth, panelHeight);
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = a.bookPanel;
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = true;
            panel.anchoredPosition = new Vector2(0f, 5f);

            RectTransform plate = SlicedChild("TitlePlate", panel, a.titlePlate, 272f, PlateHeight);
            TMP_Text plateText = Text("Title", plate, a.font, TextAlignmentOptions.Midline, CreamColor);
            AddShadow(plateText);
            StretchText(plateText);

            // 본문 340 x 250 = 위쪽 머리줄 30(달성 라벨 · 수치 · 백분율 · 게이지) + 칸 목록 뷰포트 220. 스크롤바 홈은 뷰포트와 같은 세로 범위(y · 길이)로 맞춘다.
            const float bodyTop = 12f;
            float bodyY = -(Mathf.Ceil(PlateHeight * 0.5f) + bodyTop);
            const float bodyHeight = 220f;
            CreateChallengeHeader(panel, a, panelWidth - 20f, new Vector2(10f, bodyY), out RectTransform headerRootRect, out TMP_Text headerLabelText, out TMP_Text headerCountText, out TMP_Text headerPercentText, out UI_LootGlowParticles headerGaugeGraphic);
            RectTransform bodyViewport = TopLeft("Body", panel, panelWidth - 20f, bodyHeight);
            bodyViewport.anchoredPosition = new Vector2(10f, bodyY - ChallengeHeaderHeight);
            bodyViewport.gameObject.AddComponent<RectMask2D>();
            Image bodyCatch = bodyViewport.gameObject.AddComponent<Image>();
            bodyCatch.color = new Color(0f, 0f, 0f, 0f);
            bodyCatch.raycastTarget = true;
            RectTransform rowRoot = TopLeft("Rows", bodyViewport, panelWidth - 20f, bodyHeight);
            rowRoot.anchoredPosition = Vector2.zero;

            UI_RoguelikeChallengeRow rowTemplate = CreateChallengeRow(rowRoot, a, panelWidth - 20f, clearMaterial);

            ScrollRect bodyScroll = bodyViewport.gameObject.AddComponent<ScrollRect>();
            bodyScroll.viewport = bodyViewport;
            bodyScroll.content = rowRoot;
            bodyScroll.horizontal = false;
            bodyScroll.vertical = true;
            bodyScroll.movementType = ScrollRect.MovementType.Clamped;
            bodyScroll.inertia = true;

            UI_RoguelikeScrollbar bodyBar = CreateScrollbar("ScrollTrack", panel, ScrollbarThickness, bodyHeight, false);
            ((RectTransform)bodyBar.transform).anchoredPosition = new Vector2(10f + (panelWidth - 20f) + 1f, bodyY - ChallengeHeaderHeight);

            TMP_Text empty = Text("Empty", panel, a.font, TextAlignmentOptions.Midline, MutedColor);
            SetTop(empty.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(panelWidth - 20f, SmallLineHeight));

            UI_OptionButton backButton = CreateBackButton(panel, a, out UI_KeyboardImage backKey);
            if (null == backButton) return;

            // 튀는 조각 공용 층: 판 전체를 덮고 본문 RectMask2D 밖(Body보다 뒤 형제 = 위에 그려짐)이라 칸 밖으로는 튀되, 본문 뷰포트 밖은 UI_ChallengeClearShards가 코드로 잘라낸다. 마우스를 받지 않는다.
            UI_LootGlowParticles shardsGraphic = CreateGlowGraphic("ClearShards", panel, clearMaterial);
            UI_ChallengeClearShards clearShards = shardsGraphic.gameObject.AddComponent<UI_ChallengeClearShards>();
            SerializedObject shardsSo = new SerializedObject(clearShards);
            Set(shardsSo, "graphic", shardsGraphic);
            shardsSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject so = new SerializedObject(view);
            Set(so, "dimGroup", dimGroup);
            Set(so, "dimButton", dimButton);
            Set(so, "panelRoot", panel);
            Set(so, "titlePlate", plate);
            Set(so, "titleText", plateText);
            Set(so, "rowRoot", rowRoot);
            Set(so, "rowTemplate", rowTemplate);
            Set(so, "emptyText", empty);
            Set(so, "headerRoot", headerRootRect);
            Set(so, "headerLabel", headerLabelText);
            Set(so, "headerCount", headerCountText);
            Set(so, "headerPercent", headerPercentText);
            Set(so, "headerGauge", headerGaugeGraphic);
            Set(so, "clearShards", clearShards);
            Set(so, "bodyScroll", bodyScroll);
            Set(so, "bodyViewport", bodyViewport);
            Set(so, "bodyBar", bodyBar);
            Set(so, "backButton", backButton);
            SetArray(so, "padOnlyItems", backKey.transform);
            SetArray(so, "keyGuideImages", backKey);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ChallengesPrefabPath);
            Debug.Log("[RoguelikeLobbyUIBuilder] 완료: " + ChallengesPrefabPath);
        }
        finally
        {
            if (true == bNew) UnityEngine.Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 도전과제 칸(시안 B 리본, Docs/Roguelike/_Work/ChallengeUI_B_Ribbon_Plan.md): 상자 없이 투명 바탕 + 왼쪽 강조 줄 + 아래 구분선, 제목은 리본(달성) 위 / CREAM(미달성), 아래 팁, 오른쪽에 진행 수치(미달성). 달성 깃발은 없다(리본이 달성 표시).
    // 여기 수치는 프리팹의 기본값이고, 실제 배치(제목 · 팁 · 리본 위치와 칸 높이)는 런타임에 UI_RoguelikeChallengeRow.FitHeight가 언어별 잉크 높이로 다시 잡는다(칸 높이 최소 42).
    private const float ChallengeRowHeight = 42f;
    private const float ChallengeHeaderHeight = 30f;
    private const float ChallengeTipWidth = 300f;
    private const float ChallengeTitleHeight = 14f;
    private const float ChallengeTipHeight = 15f;
    private const float ChallengeTipX = 16f;
    private const float ChallengeTitleX = 16f;
    private const float ChallengeHeaderTextHeight = 14f;
    private static readonly Color InkColor = new Color32(0x3A, 0x1F, 0x10, 255);
    private static readonly Color ClearedTipColor = new Color32(0xCD, 0xB5, 0x8E, 255);

    // 달성 칸 파티클 머티리얼: UIGlow 가산(_Mode 1, SrcAlpha / One), 발광 배율 1(메인메뉴 캔버스는 Overlay라 블룸이 없다). 있으면 그대로 재사용한다.
    private const string ClearGlowMaterialPath = "Assets/Graphics/VFX/Materials/M_UIChallengeClearAdd.mat";

    private static Material GetOrCreateClearGlowMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ClearGlowMaterialPath);
        if (null != material) return material;

        Shader shader = Shader.Find("Custom/UI/UIGlow");
        if (null == shader)
        {
            Debug.LogError("[RoguelikeLobbyUIBuilder] 셰이더를 찾지 못했습니다: Custom/UI/UIGlow");
            return null;
        }

        material = new Material(shader);
        material.SetFloat("_Mode", 1f);
        material.SetFloat("_GlowMax", 1f);
        material.SetFloat("_TintScale", 1f);
        material.SetFloat("_SrcBlend", 5f);
        material.SetFloat("_DstBlend", 1f);
        AssetDatabase.CreateAsset(material, ClearGlowMaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    /// <summary>_parent 전체를 덮는 입자 그래픽 자식. 피벗을 왼쪽 아래로 두어 UI_LootGlowParticles 좌표의 원점이 왼쪽 아래가 된다. 마우스를 받지 않는다(maskable은 기본값 유지).</summary>
    private static UI_LootGlowParticles CreateGlowGraphic(string _name, RectTransform _parent, Material _material)
    {
        RectTransform rect = StretchRect(_name, _parent);
        rect.pivot = Vector2.zero;
        UI_LootGlowParticles graphic = rect.gameObject.AddComponent<UI_LootGlowParticles>();
        graphic.material = _material;
        graphic.raycastTarget = false;
        return graphic;
    }

    /// <summary>
    /// 본문 위쪽 머리줄(_width x 30): "달성" 라벨(MUTED) · "n / 총수"(GOLD) · 백분율(GOLD, 오른쪽 끝 정렬) · 게이지 그래픽(기본 UI 머티리얼, 원점 = 머리줄 왼쪽 아래).
    /// 글자 위치와 게이지 그림은 런타임(UIView_RoguelikeChallenges)이 언어 · 시간에 맞춰 다시 잡는다. 글자 오브젝트에는 LocalizedFontTracker가 붙는다.
    /// </summary>
    private static void CreateChallengeHeader(RectTransform _panel, Assets _a, float _width, Vector2 _position, out RectTransform _root, out TMP_Text _label, out TMP_Text _count, out TMP_Text _percent, out UI_LootGlowParticles _gauge)
    {
        _root = TopLeft("Header", _panel, _width, ChallengeHeaderHeight);
        _root.anchoredPosition = _position;

        _gauge = CreateGlowGraphic("Gauge", _root, null);

        _label = Text("Label", _root, _a.font, TextAlignmentOptions.TopLeft, MutedColor);
        _label.rectTransform.sizeDelta = new Vector2(60f, ChallengeHeaderTextHeight);

        _count = Text("Count", _root, _a.font, TextAlignmentOptions.TopLeft, GoldColor);
        _count.rectTransform.sizeDelta = new Vector2(60f, ChallengeHeaderTextHeight);

        _percent = Text("Percent", _root, _a.font, TextAlignmentOptions.TopRight, GoldColor);
        RectTransform percentRect = _percent.rectTransform;
        percentRect.anchorMin = Vector2.one;
        percentRect.anchorMax = Vector2.one;
        percentRect.pivot = Vector2.one;
        percentRect.sizeDelta = new Vector2(40f, ChallengeHeaderTextHeight);
        percentRect.anchoredPosition = Vector2.zero;
    }

    private static UI_RoguelikeChallengeRow CreateChallengeRow(RectTransform _parent, Assets _a, float _width, Material _clearMaterial)
    {
        RectTransform row = TopLeft("RowTemplate", _parent, _width, ChallengeRowHeight);

        // 칸 바탕은 없다: 알파 0 Image가 마우스 · 패드 선택(레이캐스트)만 받는다.
        Image bg = row.gameObject.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);
        bg.raycastTarget = true;

        // 칸 전체가 패드 선택 대상이다(눌러도 하는 일은 없다). 탐색은 화면 스크립트가 보이는 칸끼리 위 / 아래로 Explicit으로 잇는다. 포커스 표시는 커서 상자가 한다.
        Button button = row.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = bg;
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit };

        // 보이는 것 전부의 부모: 등장 연출이 이 오브젝트의 위치(5px 아래에서 올라옴)와 알파만 움직이고 칸 자신(레이캐스트 · 커서 대상)은 제자리에 둔다.
        RectTransform content = StretchRect("Content", row);
        CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();
        contentGroup.blocksRaycasts = false;
        contentGroup.interactable = false;

        // 강조 줄 · 구분선 · 달성 틴트: 맨 아래 형제(다른 것 뒤)에서 칸 전체를 덮는다. 기본 UI 머티리얼(불투명 색).
        UI_LootGlowParticles decor = CreateGlowGraphic("Decor", content, null);

        // 달성 리본: 폭 · 높이 · 위치는 런타임이 정한다. 기본은 꺼 둔다.
        RectTransform ribbonRect = TopLeft("Ribbon", content, 100f, 15f);
        UI_ChallengeRibbon ribbon = ribbonRect.gameObject.AddComponent<UI_ChallengeRibbon>();
        ribbon.raycastTarget = false;
        ribbonRect.gameObject.SetActive(false);

        TMP_Text title = Text("Title", content, _a.font, TextAlignmentOptions.TopLeft, CreamColor);
        title.rectTransform.sizeDelta = new Vector2(_width - ChallengeTitleX, ChallengeTitleHeight);
        title.rectTransform.anchoredPosition = new Vector2(ChallengeTitleX, 0f);

        // 미달성 진행 수치: 제목 줄 오른쪽 끝 정렬(x 332). 기본은 꺼 둔다.
        TMP_Text progress = Text("Progress", content, _a.font, TextAlignmentOptions.TopRight, MutedColor);
        RectTransform progressRect = progress.rectTransform;
        progressRect.anchorMin = Vector2.one;
        progressRect.anchorMax = Vector2.one;
        progressRect.pivot = Vector2.one;
        progressRect.sizeDelta = new Vector2(60f, ChallengeTitleHeight);
        progressRect.anchoredPosition = Vector2.zero;
        progress.gameObject.SetActive(false);

        TMP_Text tip = Text("Tip", content, _a.font, TextAlignmentOptions.TopLeft, MutedColor);
        tip.textWrappingMode = TextWrappingModes.Normal;
        tip.rectTransform.sizeDelta = new Vector2(ChallengeTipWidth, ChallengeTipHeight);
        tip.rectTransform.anchoredPosition = new Vector2(ChallengeTipX, 0f);

        // 달성 칸 파티클 그래픽: 맨 위 형제(글자보다 위)에서 칸 전체를 덮는다. 칸 루트의 UI_ChallengeClearGlow가 그린다.
        UI_LootGlowParticles clearGraphic = CreateGlowGraphic("ClearGlow", content, _clearMaterial);
        UI_ChallengeClearGlow clearGlow = row.gameObject.AddComponent<UI_ChallengeClearGlow>();
        SerializedObject glowSo = new SerializedObject(clearGlow);
        Set(glowSo, "graphic", clearGraphic);
        Set(glowSo, "ribbon", ribbon);
        Set(glowSo, "titleText", title);
        glowSo.ApplyModifiedPropertiesWithoutUndo();

        UI_RoguelikeChallengeRow comp = row.gameObject.AddComponent<UI_RoguelikeChallengeRow>();
        SerializedObject so = new SerializedObject(comp);
        Set(so, "clearGlow", clearGlow);
        so.FindProperty("clearedTitleColor").colorValue = InkColor;
        so.FindProperty("clearedTipColor").colorValue = ClearedTipColor;
        Set(so, "titleText", title);
        Set(so, "tipText", tip);
        Set(so, "button", button);
        Set(so, "background", bg);
        Set(so, "content", content);
        Set(so, "contentGroup", contentGroup);
        Set(so, "decor", decor);
        Set(so, "ribbon", ribbon);
        Set(so, "progressText", progress);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    /// <summary>명판 안 패드 아이콘 배치:명판 한쪽 끝에서 _inset 안쪽, 세로 중앙, 16x16.</summary>
    private static void PlaceKeyIcon(RectTransform _rect, bool _bLeft, float _inset)
    {
        _rect.anchorMin = new Vector2(_bLeft ? 0f : 1f, 0.5f);
        _rect.anchorMax = _rect.anchorMin;
        _rect.pivot = new Vector2(_bLeft ? 0f : 1f, 0.5f);
        _rect.sizeDelta = new Vector2(KeyIconSize, KeyIconSize);
        _rect.anchoredPosition = new Vector2(_bLeft ? _inset : -_inset, 0f);
    }

    /// <summary>
    /// 옵션 프리팹의 BTN_Close를 복제해 BTN_Back을 만든다(같은 스프라이트 · 크기 · UI_OptionButton 설정).
    /// 앵커 (0,0) 피벗 (0,1) 위치 (0,-4): 판 왼쪽 끝에 맞추고 아래 테두리에서 4px 아래. 패드 선택은 잡지 않는다(navigation None).
    /// 옵션의 IMG_ShortcutIcon 자리에는 UI_KeyboardImage(B)인 BackKey를 둔다.
    /// </summary>
    private static UI_OptionButton CreateBackButton(RectTransform _panel, Assets _a, out UI_KeyboardImage _backKey)
    {
        _backKey = null;
        GameObject optionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptionPrefabPath);
        UI_OptionButton source = null;
        if (null != optionPrefab)
        {
            UI_OptionButton[] candidates = optionPrefab.GetComponentsInChildren<UI_OptionButton>(true);
            for (int i = 0; candidates.Length > i; i++)
            {
                if ("BTN_Close" == candidates[i].name) { source = candidates[i]; break; }
            }
        }
        if (null == source) { Debug.LogError("[RoguelikeLobbyUIBuilder] 옵션 프리팹에서 BTN_Close를 찾지 못했습니다: " + OptionPrefabPath); return null; }

        GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, _panel);
        clone.name = "BTN_Back";
        clone.SetActive(true);
        RectTransform rect = (RectTransform)clone.transform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(32f, 32f);
        rect.anchoredPosition = new Vector2(0f, -4f);

        // 옵션 전용 단축키 아이콘 자식은 BackKey로 교체한다.
        UI_OptionButton button = clone.GetComponent<UI_OptionButton>();
        Transform oldIcon = rect.Find("IMG_ShortcutIcon");
        if (null != oldIcon) UnityEngine.Object.DestroyImmediate(oldIcon.gameObject);
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        SerializedObject buttonSo = new SerializedObject(button);
        SerializedProperty shortcut = buttonSo.FindProperty("shortcutIconImage");
        if (null != shortcut) shortcut.objectReferenceValue = null;
        buttonSo.ApplyModifiedPropertiesWithoutUndo();

        _backKey = KeyImage("BackKey", rect, _a.keyIcons, EOtherKey.None, EGamepadButton.Button_B_Circle);
        RectTransform keyRect = _backKey.GetComponent<RectTransform>();
        keyRect.anchorMin = new Vector2(0f, 0.5f);
        keyRect.anchorMax = new Vector2(0f, 0.5f);
        keyRect.pivot = new Vector2(1f, 0.5f);
        keyRect.sizeDelta = new Vector2(KeyIconSize, KeyIconSize);
        keyRect.anchoredPosition = new Vector2(48f, 0f);
        return button;
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
            // 도끼 그림(7x7)은 오른쪽 열 · 위쪽 행에 투명 1px이 붙은 8x8이다. 피벗이 세로 가운데라 그림 아래 모서리를 예전 자리(가운데 - 3.5)에 두려면 0.5 올린다.
            RectTransform axe = IconRect("Icon", plate, a.iconAxe, 8f, 8f);
            axe.anchorMin = new Vector2(0f, 0.5f);
            axe.anchorMax = new Vector2(0f, 0.5f);
            axe.pivot = new Vector2(0f, 0.5f);
            axe.anchoredPosition = new Vector2(9f, 0.5f);
            TMP_Text plateText = Text("Label", plate, a.font, TextAlignmentOptions.MidlineLeft, CreamColor);
            AddShadow(plateText);
            plateText.rectTransform.anchorMin = Vector2.zero;
            plateText.rectTransform.anchorMax = Vector2.one;
            plateText.rectTransform.offsetMin = new Vector2(20f, 0f);
            plateText.rectTransform.offsetMax = new Vector2(-8f, 0f);

            UI_MainMenuButton start = CloneMenuButton(sourceButton, menuRoot, "BTN_RL_Start");
            UI_MainMenuButton records = CloneMenuButton(sourceButton, menuRoot, "BTN_RL_Records");
            UI_MainMenuButton challenges = CloneMenuButton(sourceButton, menuRoot, "BTN_RL_Challenges");
            UI_MainMenuButton collection = CloneMenuButton(sourceButton, menuRoot, "BTN_RL_Collection");
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
                // 칸 크기는 스프라이트 크기 그대로이고 위치는 화면 스크립트(UI_RoguelikeMenuRecordPanel.PlaceIcon)가 잡는다.
                icons[i] = IconRect("Icon" + i, panel, iconSprites[i], iconSprites[i].rect.width, iconSprites[i].rect.height);
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
            Set(ms, "roguelikeChallengesButton", challenges);
            Set(ms, "roguelikeCollectionButton", collection);
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
                UIView_RoguelikeChallenges challengesPrefab = AssetDatabase.LoadAssetAtPath<UIView_RoguelikeChallenges>(ChallengesPrefabPath);
                if (null == challengesPrefab) Debug.LogWarning("[RoguelikeLobbyUIBuilder] 도전과제 창 프리팹이 없습니다. Build Roguelike Challenges를 먼저 실행하십시오: " + ChallengesPrefabPath);
                Set(vs, "roguelikeChallengesPrefab", challengesPrefab);
                UIView_RoguelikeCollection collectionPrefab = AssetDatabase.LoadAssetAtPath<UIView_RoguelikeCollection>(CollectionPrefabPath);
                if (null == collectionPrefab) Debug.LogWarning("[RoguelikeLobbyUIBuilder] 카드 도감 창 프리팹이 없습니다. Build Roguelike Collection을 먼저 실행하십시오: " + CollectionPrefabPath);
                Set(vs, "roguelikeCollectionPrefab", collectionPrefab);
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

    /// <summary>
    /// 카드 한 칸 견본. _bSelectable이 참이면(기록 창 카드 띠) 패드로 고를 수 있는 Selectable과 마우스 판정(끌어서 스크롤)을 붙인다.
    /// 결과 화면 · 기록 줄의 칸은 거짓이라 선택 대상이 아니고 마우스를 받지 않는다.
    /// </summary>
    private static UI_RoguelikeCardIconSlot CreateIconSlot(RectTransform _parent, Assets _a, bool _bSelectable)
    {
        RectTransform slot = TopLeft("SlotTemplate", _parent, UI_RoguelikeCardIconSlot.SlotSize, UI_RoguelikeCardIconSlot.SlotSize);
        Image bg = slot.gameObject.AddComponent<Image>();
        bg.sprite = _a.cardSlotNormal;
        bg.type = Image.Type.Sliced;
        bg.raycastTarget = _bSelectable;

        Selectable selectable = null;
        if (true == _bSelectable)
        {
            selectable = slot.gameObject.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;
            selectable.targetGraphic = bg;
            selectable.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        }

        // 아이콘 · 레벨 배지는 화면 스크립트(UI_RoguelikeCardIconSlot.SetCard)가 스프라이트 크기와 위치를 정한다.
        RectTransform icon = IconRect("Icon", slot, null, 32f, 32f);
        RectTransform level = IconRect("Level", slot, null, 15f, 7f);

        UI_RoguelikeCardIconSlot comp = slot.gameObject.AddComponent<UI_RoguelikeCardIconSlot>();
        SerializedObject so = new SerializedObject(comp);
        Set(so, "frameImage", bg);
        Set(so, "iconImage", icon.GetComponent<Image>());
        Set(so, "levelImage", level.GetComponent<Image>());
        SetArray(so, "levelSprites", _a.slotLevels);
        Set(so, "maxLevelSprite", _a.slotLevelMax);
        Set(so, "rubyLevelSprite", _a.slotLevelRuby);
        Set(so, "prismFrameSprite", _a.cardSlotPrism);
        Set(so, "rubyFrameSprite", _a.cardSlotRuby);
        Set(so, "selectable", selectable);
        so.ApplyModifiedPropertiesWithoutUndo();
        return comp;
    }

    /// <summary>
    /// 스크롤바(홈 + 막대): 판 색감의 단색(홈 #3A261C, 막대 #CA8F73, 스프라이트 없음). 홈은 왼쪽 위 기준 _width x _height이고 마우스를 받는다(막대 끌기).
    /// 내용이 넘칠 때만 스크립트가 켠다(홈은 처음에 꺼 둔다).
    /// </summary>
    private static UI_RoguelikeScrollbar CreateScrollbar(string _name, RectTransform _parent, float _width, float _height, bool _bHorizontal)
    {
        RectTransform track = TopLeft(_name, _parent, _width, _height);
        Image trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = ScrollTrackColor;
        trackImage.raycastTarget = true;

        RectTransform thumb = TopLeft("Thumb", track, _bHorizontal ? 12f : _width, _bHorizontal ? _height : 12f);
        Image thumbImage = thumb.gameObject.AddComponent<Image>();
        thumbImage.color = ScrollThumbColor;
        thumbImage.raycastTarget = false;

        UI_RoguelikeScrollbar scrollbar = track.gameObject.AddComponent<UI_RoguelikeScrollbar>();
        SerializedObject so = new SerializedObject(scrollbar);
        Set(so, "track", track);
        Set(so, "thumb", thumb);
        so.FindProperty("bHorizontal").boolValue = _bHorizontal;
        so.ApplyModifiedPropertiesWithoutUndo();
        track.gameObject.SetActive(false);
        return scrollbar;
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
            infoPanelPrism = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Panel_Info_Prism.png"),
            infoPanelRuby = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Panel_Info_Ruby.png"),
            cardSlotNormal = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "CardSlot_Normal.png"),
            cardSlotMax = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "CardSlot_Max.png"),
            cardSlotPrism = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "CardSlot_Prism.png"),
            cardSlotRuby = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "CardSlot_Ruby.png"),
            slotLevels = new Sprite[SlotLevelNames.Length],
            slotLevelMax = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "SlotLevel_MAX.png"),
            slotLevelRuby = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "SlotLevel_Ruby.png"),
            titlePlate = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Title_Plate.png"),
            basicButton = AssetDatabase.LoadAssetAtPath<Sprite>(BasicButtonPath),
            arrowLeft = AssetDatabase.LoadAssetAtPath<Sprite>(OptionArrowLeftPath),
            arrowRight = AssetDatabase.LoadAssetAtPath<Sprite>(OptionArrowRightPath),
            infoPanelGold = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Panel_Info_Gold.png"),
            iconFlag = AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "IconFlag.png"),
            iconInfinity = AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "IconInfinity.png"),
            iconTree = AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "IconTree.png"),
            iconAxe = AssetDatabase.LoadAssetAtPath<Sprite>(HudDir + "IconAxe.png"),
        };

        for (int i = 0; SlotLevelNames.Length > i; i++) a.slotLevels[i] = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "SlotLevel_" + SlotLevelNames[i] + ".png");

        if (null == a.infoPanelPrism || null == a.infoPanelRuby || null == a.slotLevelMax || null == a.slotLevelRuby || 0 <= System.Array.IndexOf(a.slotLevels, null))
        {
            Debug.LogError("[RoguelikeLobbyUIBuilder] 칸 그림(Panel_Info_Prism · Panel_Info_Ruby · SlotLevel_*)을 찾지 못했습니다: " + OwnedDir);
            return null;
        }

        if (null == a.cardSlotNormal || null == a.cardSlotMax || null == a.cardSlotPrism || null == a.cardSlotRuby)
        {
            Debug.LogError("[RoguelikeLobbyUIBuilder] 카드 칸 그림(CardSlot_Normal · CardSlot_Max · CardSlot_Prism · CardSlot_Ruby)을 찾지 못했습니다: " + OwnedDir);
            return null;
        }

        if (null == a.infoPanelGold)
        {
            Debug.LogError("[RoguelikeLobbyUIBuilder] 금색 상자 그림(Panel_Info_Gold)을 찾지 못했습니다: " + OwnedDir);
            return null;
        }

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
