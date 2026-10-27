#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보유 카드 보관함 프리팹 구성기. 여러 번 실행해도 같은 결과로 덮어쓴다. 메뉴: LumberBoy/UI/Build Roguelike Owned Cards
///
///   UI_RoguelikeOwnedCardCell.prefab  카드 한 칸: 카드 선택 화면과 같은 카드(UI_RoguelikeCard 중첩 프리팹) + 판정 박스(Button + 포커스 중계)
///   UIView_RoguelikeOwnedCards.prefab  화면: 딤(바깥을 누르면 닫힘) → Content(제목 줄 · 카드 목록(왼쪽) · 설명(오른쪽) · 키 안내 줄). 기존 프리팹의 UIView 설정(ESC 닫기 등)은 그대로 둔다.
///
/// [그림] 카드는 카드 선택 화면의 카드를 그대로 쓴다. 배경(패널 틀 · 파인 칸 · 설명 칸 · 제목 명판 · 스크롤바)은 코드가 그린 픽셀 9-슬라이스 PNG다(Assets/Graphics/Roguelike/owned/).
///   디자인이 정해지면 같은 이름으로 파일을 바꿔 넣으면 된다(파일이 이미 있으면 다시 돌려도 덮어쓰지 않는다. 9-슬라이스 테두리 값은 이 구성기가 정한다).
/// [글자 없는 조작] 닫기 버튼 같은 글자가 든 버튼은 두지 않는다(언어마다 글자 폭이 달라 크기가 흔들린다).
///   키 안내는 UI_KeyboardImage(옵션 · 특성 UI와 같은 키/패드 아이콘 스크립트)가 현재 입력 장치에 맞는 아이콘을 보여 준다.
///
/// 글자 위치 · 패널 크기는 화면 스크립트가 열릴 때 화면 크기에 맞춰 다시 잡는다(언어마다 길이가 다르다). 프리팹에 박힌 값은 첫 모양일 뿐이다.
/// 문구는 프리팹에 박지 않는다 - Resources/Localization/RoguelikeUI.json의 Roguelike_OwnedCards_* 키를 스크립트가 채운다.
/// </summary>
public static class RoguelikeOwnedCardsBuilder
{
    private const string CellPrefabPath = "Assets/Prefabs/Roguelike/UI/UI_RoguelikeOwnedCardCell.prefab";
    private const string ViewPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeOwnedCards.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Galmuri11_Optimum.asset";
    private const string KeyIconDatabasePath = "Assets/Prefabs/UI/Option/KeyIconDatabase.asset";
    private const string SpriteDir = "Assets/Graphics/Roguelike/owned/";
    private const float KeyIconSize = 16f;
    // 9-슬라이스 그림은 캔버스의 Reference Pixels Per Unit(32)과 같은 PPU로 만들어야 테두리가 그린 크기 그대로(카드와 같은 픽셀 밀도) 나온다.
    private const float SpritePixelsPerUnit = 32f;
    // 틀은 카드 프레임(CardFrame)과 같은 구조다: 바깥 윤곽 · 면 · 안쪽 윤곽 3겹 + 안쪽 그림자 1칸 = 테두리 4칸. 모서리는 4칸 안에서 계단식 대각선으로 깎는다.
    private const int PanelSize = 10;
    private const int PanelBorder = 4;
    private const int PlateSize = 10;
    private const int PlateBorder = 4;

    private class BackdropSprites
    {
        public Sprite bookPanel;
        public Sprite infoPanel;
        public Sprite titlePlate;
        public Sprite scrollTrack;
        public Sprite scrollThumb;
    }

    // 카드 프레임(Assets/Graphics/Roguelike/card/CardFrame.png)에서 가져온 색.
    private static readonly Color32 FrameOutline = new Color32(102, 57, 49, 255);
    private static readonly Color32 FrameLight = new Color32(202, 143, 115, 255);
    private static readonly Color32 FrameShade = new Color32(143, 86, 59, 255);

    // 프로젝트가 Linear 색 공간이라 알파 0.9의 검정은 화면 밝기 약 35%가 남는다. 0.97이면 약 20%.
    private static readonly Color DimColor = new Color(0.03f, 0.02f, 0.01f, 0.97f);
    private static readonly Color TitleColor = new Color(0.95f, 0.88f, 0.7f, 1f);
    private static readonly Color MutedColor = new Color(0.71f, 0.61f, 0.48f, 1f);

    [MenuItem("LumberBoy/UI/Build Roguelike Owned Cards")]
    public static void Build()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        KeyIconDatabase keyIconDatabase = AssetDatabase.LoadAssetAtPath<KeyIconDatabase>(KeyIconDatabasePath);
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoguelikeCardBuilder.PrefabPath);
        if (null == cardPrefab)
        {
            RoguelikeCardBuilder.Build();
            cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoguelikeCardBuilder.PrefabPath);
        }

        if (null == font || null == cardPrefab || null == keyIconDatabase)
        {
            Debug.LogError("[RoguelikeOwnedCardsBuilder] 폰트 · 카드 프리팹 · 키 아이콘 데이터베이스를 찾지 못했습니다.");
            return;
        }

        if (false == AssetDatabase.IsValidFolder("Assets/Graphics/Roguelike/owned"))
        {
            AssetDatabase.CreateFolder("Assets/Graphics/Roguelike", "owned");
        }

        BackdropSprites sprites = new BackdropSprites
        {
            bookPanel = MakeSlicedSprite("Panel_Book", BuildFrame(PanelSize, PanelSize, C(30, 21, 14), C(20, 14, 9)), PanelSize, PanelSize, new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder)),
            infoPanel = MakeSlicedSprite("Panel_Info", BuildFrame(PanelSize, PanelSize, C(38, 27, 19), C(25, 18, 12)), PanelSize, PanelSize, new Vector4(PanelBorder, PanelBorder, PanelBorder, PanelBorder)),
            titlePlate = MakeSlicedSprite("Title_Plate", BuildFrame(PlateSize, PlateSize, C(92, 60, 40), C(92, 60, 40)), PlateSize, PlateSize, new Vector4(PlateBorder, PlateBorder, PlateBorder, PlateBorder)),
            scrollTrack = MakeSlicedSprite("Scroll_Track", BuildScrollTrack(), 3, 6, new Vector4(0f, 2f, 0f, 2f)),
            scrollThumb = MakeSlicedSprite("Scroll_Thumb", BuildScrollThumb(), 3, 6, new Vector4(0f, 2f, 0f, 2f)),
        };

        UI_RoguelikeOwnedCardCell cellPrefab = BuildCellPrefab(cardPrefab);
        BuildViewPrefab(cellPrefab, font, keyIconDatabase, sprites);

        AssetDatabase.SaveAssets();
        Debug.Log("[RoguelikeOwnedCardsBuilder] 완료: " + CellPrefabPath + " / " + ViewPrefabPath);
    }

    // // 칸

    private static UI_RoguelikeOwnedCardCell BuildCellPrefab(GameObject _cardPrefab)
    {
        int w = UI_RoguelikeOwnedCardCell.CardWidth;
        int h = UI_RoguelikeOwnedCardCell.CardHeight;

        GameObject root = new GameObject("UI_RoguelikeOwnedCardCell", typeof(RectTransform));
        root.layer = LayerMask.NameToLayer("UI");
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0f, 1f);
        rootRect.anchorMax = new Vector2(0f, 1f);
        rootRect.pivot = new Vector2(0f, 1f);
        rootRect.sizeDelta = new Vector2(w, h);

        // 카드 (중첩 프리팹). 칸 왼쪽 아래에 붙인다.
        GameObject cardObject = (GameObject)PrefabUtility.InstantiatePrefab(_cardPrefab, rootRect);
        RectTransform cardRect = (RectTransform)cardObject.transform;
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.zero;
        cardRect.pivot = Vector2.zero;
        cardRect.anchoredPosition = Vector2.zero;
        UI_RoguelikeCard card = cardObject.GetComponent<UI_RoguelikeCard>();

        // 카드 프리팹 단독용 판정 박스는 칸의 판정 박스(아래)와 겹치므로 칸 안에서는 끈다(raycast 대상이 두 겹이 되지 않게).
        Transform cardHit = cardObject.transform.Find("HitBox");
        if (null != cardHit) cardHit.GetComponent<Image>().raycastTarget = false;

        // 판정 박스: 카드(호버로 올라간 범위 포함)를 덮고 움직이지 않는다. 가장 위 형제라 마우스를 먼저 받는다.
        Image hit = RoguelikeCardBuilder.CreateImage("HitBox", rootRect, null, Vector2Int.zero, new Vector2(w, h + UI_RoguelikeOwnedCardCell.HoverLiftPixels));
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        Button button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hit;

        UI_RoguelikeSelectRelay relay = hit.gameObject.AddComponent<UI_RoguelikeSelectRelay>();
        SerializedObject relaySo = new SerializedObject(relay);
        relaySo.FindProperty("cursorTarget").objectReferenceValue = card.VisualRect;
        relaySo.ApplyModifiedPropertiesWithoutUndo();

        UI_RoguelikeOwnedCardCell cell = root.AddComponent<UI_RoguelikeOwnedCardCell>();
        SerializedObject so = new SerializedObject(cell);
        so.FindProperty("card").objectReferenceValue = card;
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("relay").objectReferenceValue = relay;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, CellPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return AssetDatabase.LoadAssetAtPath<UI_RoguelikeOwnedCardCell>(CellPrefabPath);
    }

    // // 화면

    private static void BuildViewPrefab(UI_RoguelikeOwnedCardCell _cellPrefab, TMP_FontAsset _font, KeyIconDatabase _keyIconDatabase, BackdropSprites _sprites)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(ViewPrefabPath);

        try
        {
            RectTransform rootRect = (RectTransform)root.transform;
            for (int i = rootRect.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(rootRect.GetChild(i).gameObject);

            UIView_RoguelikeOwnedCards view = root.GetComponent<UIView_RoguelikeOwnedCards>();
            if (null == view)
            {
                Debug.LogError("[RoguelikeOwnedCardsBuilder] UIView_RoguelikeOwnedCards 컴포넌트를 찾지 못했습니다.");
                return;
            }

            // 딤: 화면 전체를 어둡게 덮고 뒤쪽 클릭을 막는다. 누르면 닫힌다(창 바깥 클릭).
            Image dim = AddStretchImage("Dim", rootRect, DimColor, 0f);
            dim.raycastTarget = true;
            Button dimButton = dim.gameObject.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.targetGraphic = dim;
            Navigation noNavigation = new Navigation { mode = Navigation.Mode.None };
            dimButton.navigation = noNavigation;

            RectTransform content = RoguelikeCardBuilder.CreateRect("Content", rootRect, Vector2Int.zero, new Vector2(624f, 330f));
            content.anchorMin = new Vector2(0.5f, 0.5f);
            content.anchorMax = new Vector2(0.5f, 0.5f);
            content.pivot = new Vector2(0.5f, 0.5f);

            // 열기 · 닫기 연출 중에는 카드 · 설명이 마우스를 받지 않게 막는다(딤은 Content 밖이라 연출 중에도 눌린다).
            CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

            // ---- 제목 줄: 명판 위에 제목 · 장수(명판 폭은 글자 폭에 맞춰 스크립트가 정한다). 열기 · 닫기 연출에서 한 묶음으로 투명도를 바꾸려고 TitleBar(Content 왼쪽 위 모서리)에 담는다.
            RectTransform titleBar = TopLeftRect("TitleBar", content, 1f, 1f);
            CanvasGroup titleGroup = titleBar.gameObject.AddComponent<CanvasGroup>();
            RectTransform titlePlate = CreateSlicedPanel("TitlePlate", titleBar, _sprites.titlePlate, 120f, 20f, false);
            TMP_Text titleText = CreateText("Title", titleBar, _font, TextAlignmentOptions.MidlineLeft, TitleColor);
            TMP_Text countText = CreateText("Count", titleBar, _font, TextAlignmentOptions.MidlineLeft, MutedColor);

            // ---- 카드 목록(왼쪽): 마스크 영역 안에서 스크롤하는 내용 + 스크롤바. 패널 바탕은 마우스를 받아 딤(닫기)으로 새지 않게 한다.
            RectTransform book = CreateSlicedPanel("Book", content, _sprites.bookPanel, 288f, 300f, true);

            // 열기 · 닫기 연출 중 책이 트레이 칸 크기로 줄어들 때 안의 내용이 삐져나오지 않게 하는 마스크. 평소에는 꺼 둔다(스크립트가 연출 동안만 켠다).
            RectMask2D bookMask = book.gameObject.AddComponent<RectMask2D>();
            bookMask.enabled = false;

            RectTransform viewport = TopLeftRect("Viewport", book, 266f, 284f);
            viewport.gameObject.AddComponent<RectMask2D>();
            Image viewportHit = viewport.gameObject.AddComponent<Image>();
            viewportHit.color = new Color(1f, 1f, 1f, 0f);
            viewportHit.raycastTarget = true;

            RectTransform scrollContent = TopLeftRect("ScrollContent", viewport, 266f, 284f);
            TMP_Text emptyText = CreateText("EmptyText", scrollContent, _font, TextAlignmentOptions.Center, MutedColor);

            // 스크롤바: 얇은 홈 + 막대. 내용이 넘칠 때만 스크립트가 켠다.
            RectTransform track = TopLeftRect("ScrollTrack", book, 3f, 284f);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = _sprites.scrollTrack;
            trackImage.type = Image.Type.Sliced;
            trackImage.raycastTarget = false;
            Image thumb = RoguelikeCardBuilder.CreateImage("ScrollThumb", track, _sprites.scrollThumb, Vector2Int.zero, new Vector2(3f, 12f));
            thumb.type = Image.Type.Sliced;

            ScrollRect scroll = book.gameObject.AddComponent<ScrollRect>();
            scroll.content = scrollContent;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;

            // ---- 설명(오른쪽): 설명 패널(이름 · 레벨 · 구분선 · 설명 · 현재 효과). 카드 그림은 목록의 카드가 이미 보여 준다. 배경 상자 없이 패널 위에 바로 놓는다.
            RectTransform info = CreateSlicedPanel("Info", content, _sprites.infoPanel, 224f, 300f, true);
            CanvasGroup infoGroup = info.gameObject.AddComponent<CanvasGroup>();

            RectTransform desc = TopLeftRect("DescPanel", info, 216f, 48f);

            TMP_Text nameText = CreateText("Name", desc, _font, TextAlignmentOptions.TopLeft, Color.white);
            TMP_Text levelText = CreateText("Level", desc, _font, TextAlignmentOptions.TopRight, new Color(1f, 0.8f, 0.25f, 1f));
            TMP_Text descText = CreateText("Description", desc, _font, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.85f, 0.85f, 1f));

            // 이름 줄 아래 1칸 구분선: 카드 프레임의 윤곽색.
            Image divider = RoguelikeCardBuilder.CreateImage("Divider", desc, null, Vector2Int.zero, new Vector2(100f, 1f));
            divider.color = FrameOutline;
            divider.raycastTarget = false;
            nameText.textWrappingMode = TextWrappingModes.Normal;
            descText.textWrappingMode = TextWrappingModes.Normal;

            UI_RoguelikeCardDescPanel descPanel = desc.gameObject.AddComponent<UI_RoguelikeCardDescPanel>();
            SerializedObject descSo = new SerializedObject(descPanel);
            descSo.FindProperty("nameText").objectReferenceValue = nameText;
            descSo.FindProperty("levelText").objectReferenceValue = levelText;
            descSo.FindProperty("descriptionText").objectReferenceValue = descText;
            descSo.FindProperty("divider").objectReferenceValue = divider.rectTransform;
            descSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 키 안내 줄: 키보드 = [Esc] 닫기 / 패드 = [스틱] 이동  [B] 닫기. 이동 안내(MoveRow)는 패드 모드에서만 켜진다(키보드/마우스는 마우스 전용).
            // 아이콘은 UI_KeyboardImage가 입력 장치에 맞춰 바꾼다. 아이콘과 문구 모두 인벤토리 키 아이콘과 같은 흰 외곽선(UIEffect)을 쓴다.
            RectTransform keyGuide = TopLeftRect("KeyGuide", content, 624f, 18f);
            CanvasGroup keyGuideGroup = keyGuide.gameObject.AddComponent<CanvasGroup>();
            HorizontalLayoutGroup guideLayout = keyGuide.gameObject.AddComponent<HorizontalLayoutGroup>();
            guideLayout.childAlignment = TextAnchor.MiddleCenter;
            guideLayout.spacing = 16f;
            guideLayout.childControlWidth = true;
            guideLayout.childControlHeight = true;
            guideLayout.childForceExpandWidth = false;
            guideLayout.childForceExpandHeight = false;

            RectTransform moveRow = CreateGuideRow("MoveRow", keyGuide);
            UI_KeyboardImage moveImage = CreateKeyImage("Key_Move", moveRow, _keyIconDatabase, EOtherKey.None, EGamepadButton.LeftStick);
            TMP_Text hintMoveText = CreateText("MoveText", moveRow, _font, TextAlignmentOptions.MidlineLeft, Color.black);
            RoguelikeHUDBuilder.CopyInventoryKeyEffect(hintMoveText.gameObject);

            RectTransform closeRow = CreateGuideRow("CloseRow", keyGuide);
            UI_KeyboardImage closeImage = CreateKeyImage("Key_Close", closeRow, _keyIconDatabase, EOtherKey.Escape, EGamepadButton.Button_B_Circle);
            TMP_Text hintCloseText = CreateText("CloseText", closeRow, _font, TextAlignmentOptions.MidlineLeft, Color.black);
            RoguelikeHUDBuilder.CopyInventoryKeyEffect(hintCloseText.gameObject);

            // ---- 트레이 레이어: 창이 열려 있는 동안 HUD 트레이를 올려 두는 화면 전체 레이어. 가장 마지막 형제라 딤 · 내용 위에 그려진다. 그래픽이 없어 마우스를 받지 않는다.
            RectTransform trayLayer = RoguelikeCardBuilder.CreateRect("TrayLayer", rootRect, Vector2Int.zero, Vector2.zero);
            trayLayer.anchorMin = Vector2.zero;
            trayLayer.anchorMax = Vector2.one;
            trayLayer.pivot = new Vector2(0.5f, 0.5f);
            trayLayer.offsetMin = Vector2.zero;
            trayLayer.offsetMax = Vector2.zero;

            // ---- 화면 컴포넌트 연결. UIView 기본 설정(layer · ESC 닫기 등)은 기존 프리팹 값을 그대로 둔다.
            SerializedObject viewSo = new SerializedObject(view);
            viewSo.FindProperty("contentRoot").objectReferenceValue = content;
            viewSo.FindProperty("dimButton").objectReferenceValue = dimButton;
            viewSo.FindProperty("titlePlate").objectReferenceValue = titlePlate;
            viewSo.FindProperty("titleText").objectReferenceValue = titleText;
            viewSo.FindProperty("countText").objectReferenceValue = countText;
            viewSo.FindProperty("bookRoot").objectReferenceValue = book;
            viewSo.FindProperty("viewport").objectReferenceValue = viewport;
            viewSo.FindProperty("scrollContent").objectReferenceValue = scrollContent;
            viewSo.FindProperty("scrollRect").objectReferenceValue = scroll;
            viewSo.FindProperty("scrollTrack").objectReferenceValue = track;
            viewSo.FindProperty("scrollThumb").objectReferenceValue = thumb.rectTransform;
            viewSo.FindProperty("cellPrefab").objectReferenceValue = _cellPrefab;
            viewSo.FindProperty("emptyText").objectReferenceValue = emptyText;
            viewSo.FindProperty("infoRoot").objectReferenceValue = info;
            viewSo.FindProperty("descPanel").objectReferenceValue = descPanel;
            viewSo.FindProperty("keyGuideRoot").objectReferenceValue = keyGuide;
            viewSo.FindProperty("moveGuideRow").objectReferenceValue = moveRow;
            viewSo.FindProperty("hintMoveText").objectReferenceValue = hintMoveText;
            viewSo.FindProperty("hintCloseText").objectReferenceValue = hintCloseText;
            viewSo.FindProperty("bookMask").objectReferenceValue = bookMask;
            viewSo.FindProperty("contentGroup").objectReferenceValue = contentGroup;
            viewSo.FindProperty("titleGroup").objectReferenceValue = titleGroup;
            viewSo.FindProperty("infoGroup").objectReferenceValue = infoGroup;
            viewSo.FindProperty("keyGuideGroup").objectReferenceValue = keyGuideGroup;
            viewSo.FindProperty("trayLayer").objectReferenceValue = trayLayer;

            SerializedProperty imageProperty = viewSo.FindProperty("keyGuideImages");
            imageProperty.arraySize = 2;
            imageProperty.GetArrayElementAtIndex(0).objectReferenceValue = moveImage;
            imageProperty.GetArrayElementAtIndex(1).objectReferenceValue = closeImage;

            viewSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ViewPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // // 오브젝트

    /// <summary>왼쪽 위 앵커 · 피벗의 빈 사각형. 위치와 크기는 화면 스크립트가 다시 잡는다.</summary>
    private static RectTransform TopLeftRect(string _name, RectTransform _parent, float _width, float _height)
    {
        RectTransform rect = RoguelikeCardBuilder.CreateRect(_name, _parent, Vector2Int.zero, new Vector2(_width, _height));
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }

    /// <summary>9-슬라이스 스프라이트 패널(틀 + 파인 칸). _bRaycast면 마우스를 받는다(뒤쪽 딤으로 클릭이 새지 않게).</summary>
    private static RectTransform CreateSlicedPanel(string _name, RectTransform _parent, Sprite _sprite, float _width, float _height, bool _bRaycast)
    {
        RectTransform rect = TopLeftRect(_name, _parent, _width, _height);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = _sprite;
        image.type = Image.Type.Sliced;
        image.raycastTarget = _bRaycast;
        return rect;
    }

    private static Image AddStretchImage(string _name, RectTransform _parent, Color _color, float _inset)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(_inset, _inset);
        rect.offsetMax = new Vector2(-_inset, -_inset);

        Image image = go.AddComponent<Image>();
        image.color = _color;
        image.raycastTarget = false;
        return image;
    }

    /// <summary>키 안내 한 묶음(아이콘들 + 문구): 가로로 이어 붙이고 내용 크기에 맞춘다.</summary>
    private static RectTransform CreateGuideRow(string _name, RectTransform _parent)
    {
        RectTransform row = TopLeftRect(_name, _parent, 0f, 0f);
        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return row;
    }

    /// <summary>키/패드 아이콘 한 개. 키보드 모드에서는 _key, 패드 모드에서는 _button의 아이콘을 보인다(없으면 숨는다).</summary>
    private static UI_KeyboardImage CreateKeyImage(string _name, RectTransform _parent, KeyIconDatabase _database, EOtherKey _key, EGamepadButton _button)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);

        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;

        // 흰 외곽선: 인벤토리의 키 아이콘(UI_Inventory/Keyboard)의 UIEffect를 그대로 복사한다(HUD 트레이의 키 아이콘과 같다).
        RoguelikeHUDBuilder.CopyInventoryKeyEffect(go);

        // 키 아이콘은 16x16 원본 크기 그대로 보인다(Image 기본 선호 크기는 PPU 기준 환산이라 50x50으로 부풀어 오른다).
        rect.sizeDelta = new Vector2(KeyIconSize, KeyIconSize);
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.minWidth = KeyIconSize;
        element.minHeight = KeyIconSize;
        element.preferredWidth = KeyIconSize;
        element.preferredHeight = KeyIconSize;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;

        UI_KeyboardImage keyImage = go.AddComponent<UI_KeyboardImage>();
        SerializedObject so = new SerializedObject(keyImage);
        so.FindProperty("keyIconDatabase").objectReferenceValue = _database;
        so.FindProperty("keyIconMode").enumValueIndex = (int)EKeyIconMode.Other;
        so.FindProperty("otherKey").enumValueIndex = (int)_key;
        so.FindProperty("otherGamepadButton").enumValueIndex = (int)_button;
        so.FindProperty("targetImage").objectReferenceValue = image;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 첫 모양(키보드 아이콘)을 미리 채워 둔다. 열릴 때 UI_KeyboardImage.Initialize가 입력 장치에 맞춰 다시 정한다.
        keyImage.RefreshIcon();
        return keyImage;
    }

    private static TMP_Text CreateText(string _name, RectTransform _parent, TMP_FontAsset _font, TextAlignmentOptions _alignment, Color _color)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(100f, 14f);
        rect.anchoredPosition = Vector2.zero;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = _font.faceInfo.pointSize;
        text.text = string.Empty;
        text.alignment = _alignment;
        text.color = _color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        // 언어별 폰트 교체 시스템이 Instantiate 직후부터 원본 폰트를 알 수 있게 미리 붙인다.
        go.AddComponent<LocalizedFontTracker>();
        return text;
    }

    // // 배경 그림(코드가 그린 임시 픽셀 9-슬라이스)

    private static Color32 C(int _r, int _g, int _b)
    {
        return new Color32((byte)_r, (byte)_g, (byte)_b, 255);
    }

    /// <summary>9-슬라이스 스프라이트. 파일이 방금 만들어졌을 때만(테두리가 비어 있을 때) 테두리를 넣는다 - 디자이너가 바꿔 넣은 그림의 설정을 건드리지 않는다.</summary>
    private static Sprite MakeSlicedSprite(string _name, Color32[] _pixels, int _width, int _height, Vector4 _border)
    {
        string path = SpriteDir + _name + ".png";
        Sprite sprite = RoguelikeCardBuilder.EnsureSpriteFromPixels(path, _pixels, _width, _height, SpritePixelsPerUnit);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (null != importer && Vector4.zero == importer.spriteBorder)
        {
            importer.spriteBorder = _border;
            importer.SaveAndReimport();
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        return sprite;
    }

    /// <summary>
    /// 카드 프레임 같은 틀(10x10, 테두리 4): 바깥 윤곽 · 면(위왼쪽이 밝고 아래오른쪽이 중간 갈색) · 안쪽 윤곽 · 안쪽 그림자(위왼쪽만). 가운데는 _center.
    /// 모서리는 둥글리지 않고 4칸 안에서 계단식 대각선으로 깎는다. 안쪽 그림자가 필요 없으면 _shadow를 _center와 같게 준다. 텍스처는 아래에서 위 순서다(y=0이 맨 아래).
    /// </summary>
    private static Color32[] BuildFrame(int _width, int _height, Color32 _center, Color32 _shadow)
    {
        Color32[] pixels = new Color32[_width * _height];

        for (int y = 0; y < _height; y++)
        {
            for (int x = 0; x < _width; x++)
            {
                int sy = _height - 1 - y;
                int layer = FrameLayer(x, sy, _width, _height);
                if (layer < 0) continue;

                bool bLit = Mathf.Min(x, sy) <= Mathf.Min(_width - 1 - x, y);

                Color32 color;
                switch (layer)
                {
                    case 0: color = FrameOutline; break;
                    case 1: color = bLit ? FrameLight : FrameShade; break;
                    case 2: color = FrameOutline; break;
                    case 3: color = bLit ? _shadow : _center; break;
                    default: color = _center; break;
                }

                pixels[y * _width + x] = color;
            }
        }

        return pixels;
    }

    /// <summary>
    /// 틀의 한 칸이 어느 층인지: 0 바깥 윤곽, 1 면, 2 안쪽 윤곽, 3 안쪽 그림자 자리, 4 가운데, -1 비어 있음(깎인 모서리).
    /// 변 근처는 가장자리까지의 거리가 층이고, 모서리 4x4에서는 두 거리의 합(3 윤곽 · 4~5 면 · 6 안쪽 윤곽)이 계단식 대각선 층이 된다 - 카드 프레임의 모서리와 같은 모양이다.
    /// </summary>
    private static int FrameLayer(int _x, int _y, int _width, int _height)
    {
        int edgeX = Mathf.Min(_x, _width - 1 - _x);
        int edgeY = Mathf.Min(_y, _height - 1 - _y);

        if (3 >= edgeX && 3 >= edgeY)
        {
            int sum = edgeX + edgeY;
            if (3 > sum) return -1;
            if (3 == sum) return 0;
            if (5 >= sum) return 1;
            return 2;
        }

        return Mathf.Min(Mathf.Min(edgeX, edgeY), 4);
    }

    /// <summary>스크롤바 홈 3x6(세로 테두리 2): 왼쪽이 어두운 파인 홈. 위아래 끝은 윤곽.</summary>
    private static Color32[] BuildScrollTrack()
    {
        const int Width = 3;
        const int Height = 6;
        Color32[] pixels = new Color32[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (0 == y || Height - 1 == y) pixels[y * Width + x] = C(10, 7, 4);
                else if (0 == x) pixels[y * Width + x] = C(10, 7, 4);
                else if (1 == x) pixels[y * Width + x] = C(20, 14, 9);
                else pixels[y * Width + x] = C(40, 29, 19);
            }
        }

        return pixels;
    }

    /// <summary>스크롤바 막대 3x6(세로 테두리 2): 왼쪽 밝은 줄 · 가운데 · 오른쪽 윤곽색 줄(카드 프레임 팔레트), 위아래 끝은 윤곽.</summary>
    private static Color32[] BuildScrollThumb()
    {
        const int Width = 3;
        const int Height = 6;
        Color32[] pixels = new Color32[Width * Height];

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (0 == y || Height - 1 == y) pixels[y * Width + x] = C(57, 34, 23);
                else if (0 == x) pixels[y * Width + x] = FrameLight;
                else if (1 == x) pixels[y * Width + x] = FrameShade;
                else pixels[y * Width + x] = FrameOutline;
            }
        }

        return pixels;
    }
}
#endif
