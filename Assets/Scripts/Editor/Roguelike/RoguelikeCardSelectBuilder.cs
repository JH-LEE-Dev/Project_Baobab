#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 선택 화면 프리팹 구성기. 여러 번 실행해도 같은 결과로 덮어쓴다. 카드 프리팹(RoguelikeCardBuilder)을 먼저 다시 만든다.
///
///   UI_RoguelikeCardSlot.prefab  카드 1칸: 카드 + 판정 박스(Button + 포커스 중계)
///   UIView_RoguelikeCardSelect.prefab  화면: 딤 → Content(제목 명판 · 슬롯 4칸 · 리롤 버튼 · 설명 패널) · WAVE 배지(왼쪽 위) · 패드 키 안내(왼쪽 아래)
///
/// 폴리싱 시안 v4 - 화면 배치(Content 중심 기준, 정수 픽셀)는 제목 명판 → 카드 줄 → 바로 아래 가운데 리롤 글자 버튼 → 설명 패널.
///   바탕 그림은 보관함 구성기가 만든 Title_Plate · Panel_Info와 HUD 기본 버튼(BasicButton)을 9-슬라이스로 쓴다.
/// 문구(리롤·MAX·카드 이름/설명)는 프리팹에 박지 않는다 - Resources/Localization/RoguelikeUI.json의 키를
/// 화면 스크립트가 열릴 때 채운다. 버튼 폭·설명 패널 높이도 글자 길이에 맞춰 스크립트가 다시 잡는다(언어마다 길이가 크게 다르다).
/// </summary>
public static class RoguelikeCardSelectBuilder
{
    private const string SlotPrefabPath = "Assets/Prefabs/Roguelike/UI/UI_RoguelikeCardSlot.prefab";
    private const string ViewPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeCardSelect.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Galmuri11_Optimum.asset";
    private const string IconRerollPath = "Assets/Graphics/Roguelike/card/Icon_Reroll.png";

    // 임시 아이콘(디자인 확정 전)은 EnsureIconSprite가 만든다. 같은 경로에 이미 파일이 있으면 덮어쓰지 않는다 - 스프라이트만 바꾸면 된다.

    private const string OwnedDir = "Assets/Graphics/Roguelike/owned/";
    private const string BasicButtonPath = "Assets/Graphics/HUD/Button/BasicButton.png";
    private const string KeyIconDatabasePath = "Assets/Prefabs/UI/Option/KeyIconDatabase.asset";

    private const int SlotCount = 4;
    private const int CardRowY = 6;
    private const int DescWidth = 190;
    private const int DescHeight = 48;
    private const int ContentOffsetY = -10;
    // 줄 높이(px). 가장 큰 글자 높이(Lorem 계열 위 17 + 아래 4) 기준에 여백을 더한 값(결과 · 기록 화면과 같은 규격).
    private const float PlateHeight = 25f;
    private const float RerollHeight = 28f;
    private const float GuideHeight = 18f;
    private const float KeyIconSize = 16f;
    private const float EdgeMargin = 12f;
    private const float WaveBadgeWidth = 104f;

    private static readonly Color DimColor = new Color(0.02f, 0.03f, 0.06f, 0.85f);
    private static readonly Color CreamColor = new Color(0.95f, 0.89f, 0.77f, 1f);
    private static readonly Color GoldColor = new Color(1f, 0.83f, 0.35f, 1f);
    private static readonly Color ShadowColor = new Color(0.16f, 0.1f, 0.06f, 1f);
    private static readonly Color DarkTextColor = new Color(0.23f, 0.14f, 0.09f, 1f);

    [MenuItem("LumberBoy/UI/Build Roguelike Card Select")]
    public static void Build()
    {
        RoguelikeCardBuilder.Build();

        Sprite rerollIcon = EnsureIconSprite(IconRerollPath, BuildRerollRows());
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        GameObject cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoguelikeCardBuilder.PrefabPath);

        if (null == font || null == cardPrefab || null == rerollIcon)
        {
            Debug.LogError("[RoguelikeCardSelectBuilder] 폰트·카드 프리팹·리롤 아이콘 중 찾지 못한 것이 있습니다.");
            return;
        }

        BuildSlotPrefab(cardPrefab);
        GameObject slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPrefabPath);
        BuildViewPrefab(slotPrefab, font, rerollIcon);

        AssetDatabase.SaveAssets();
        Debug.Log("[RoguelikeCardSelectBuilder] 완료: " + SlotPrefabPath + " / " + ViewPrefabPath);
    }

    // // 슬롯

    private static void BuildSlotPrefab(GameObject _cardPrefab)
    {
        int w = RoguelikeCardBuilder.FrameWidth;
        int h = RoguelikeCardBuilder.FrameHeight;
        int lift = UI_RoguelikeCard.HoverLiftPixels;

        GameObject root = new GameObject("UI_RoguelikeCardSlot", typeof(RectTransform), typeof(CanvasGroup));
        root.layer = LayerMask.NameToLayer("UI");
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = Vector2.zero;
        rootRect.sizeDelta = new Vector2(w, h);

        // 카드 (중첩 프리팹). 슬롯 왼쪽 아래에 붙인다.
        GameObject cardObject = (GameObject)PrefabUtility.InstantiatePrefab(_cardPrefab, rootRect);
        RectTransform cardRect = (RectTransform)cardObject.transform;
        cardRect.anchorMin = Vector2.zero;
        cardRect.anchorMax = Vector2.zero;
        cardRect.pivot = Vector2.zero;
        cardRect.anchoredPosition = Vector2.zero;
        UI_RoguelikeCard card = cardObject.GetComponent<UI_RoguelikeCard>();

        // 카드 프리팹 단독용 판정 박스는 슬롯의 판정 박스(아래)와 겹치므로 슬롯 안에서는 끈다(raycast 대상이 두 겹이 되지 않게).
        Transform cardHit = cardObject.transform.Find("HitBox");
        if (null != cardHit) cardHit.GetComponent<Image>().raycastTarget = false;

        // 판정 박스: 카드(호버로 올라간 범위 포함)를 덮고 움직이지 않는다. 가장 위 형제라 마우스를 먼저 받는다.
        Image hitImage = RoguelikeCardBuilder.CreateImage("HitBox", rootRect, null, Vector2Int.zero, new Vector2(w, h + lift));
        hitImage.color = new Color(1f, 1f, 1f, 0f);
        hitImage.raycastTarget = true;

        Button button = hitImage.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = hitImage;

        UI_RoguelikeSelectRelay relay = hitImage.gameObject.AddComponent<UI_RoguelikeSelectRelay>();
        SerializedObject relaySo = new SerializedObject(relay);
        relaySo.FindProperty("cursorTarget").objectReferenceValue = card.VisualRect;
        relaySo.ApplyModifiedPropertiesWithoutUndo();

        UI_RoguelikeCardSlot slot = root.AddComponent<UI_RoguelikeCardSlot>();
        SerializedObject so = new SerializedObject(slot);
        so.FindProperty("card").objectReferenceValue = card;
        so.FindProperty("relay").objectReferenceValue = relay;
        so.FindProperty("button").objectReferenceValue = button;
        so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, SlotPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    // // 화면

    private static void BuildViewPrefab(GameObject _slotPrefab, TMP_FontAsset _font, Sprite _rerollIcon)
    {
        Sprite titlePlateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Title_Plate.png");
        Sprite infoPanelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OwnedDir + "Panel_Info.png");
        Sprite basicButtonSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BasicButtonPath);
        KeyIconDatabase keyIcons = AssetDatabase.LoadAssetAtPath<KeyIconDatabase>(KeyIconDatabasePath);
        if (null == titlePlateSprite || null == infoPanelSprite || null == basicButtonSprite || null == keyIcons)
        {
            Debug.LogError("[RoguelikeCardSelectBuilder] 명판 · 정보 패널 · 기본 버튼 그림 또는 키 아이콘 DB를 찾지 못했습니다. 보관함 구성기(Build Roguelike Owned Cards)를 먼저 실행하십시오.");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(ViewPrefabPath);
        RectTransform rootRect = (RectTransform)root.transform;

        for (int i = rootRect.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(rootRect.GetChild(i).gameObject);

        // 딤: 화면 전체를 어둡게 덮고 뒤쪽 클릭을 막는다(Linear 색 공간이라 알파가 실제보다 옅게 보여 0.85로 둔다).
        Image dim = CreateStretchImage("Dim", rootRect, DimColor);
        dim.raycastTarget = true;

        // Content: 화면 정중앙 기준 좌표계(앵커·피벗 가운데).
        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.layer = LayerMask.NameToLayer("UI");
        RectTransform content = (RectTransform)contentObject.transform;
        content.SetParent(rootRect, false);
        content.anchorMin = new Vector2(0.5f, 0.5f);
        content.anchorMax = new Vector2(0.5f, 0.5f);
        content.pivot = new Vector2(0.5f, 0.5f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = new Vector2(0f, ContentOffsetY);

        // 제목 명판: 카드 줄 위 가운데. 피벗이 아래라 스크립트가 카드 위쪽 + 간격에 바닥을 맞춘다. 폭도 스크립트가 문구에 맞춘다.
        RectTransform titlePlate = AnchoredRect("TitlePlate", content, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, PlateHeight));
        titlePlate.pivot = new Vector2(0.5f, 0f);
        Image titleImage = titlePlate.gameObject.AddComponent<Image>();
        titleImage.sprite = titlePlateSprite;
        titleImage.type = Image.Type.Sliced;
        titleImage.raycastTarget = false;
        TMP_Text titleText = CreateText("Title", titlePlate, _font, string.Empty, TextAlignmentOptions.Midline, CreamColor, false, TextOverflowModes.Overflow);
        StretchRect(titleText.rectTransform);
        titleText.rectTransform.offsetMin = new Vector2(0f, 1f);
        AddShadow(titleText);

        UI_RoguelikeCardSlot[] slots = new UI_RoguelikeCardSlot[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            GameObject slotObject = (GameObject)PrefabUtility.InstantiatePrefab(_slotPrefab, content);
            slotObject.name = "Slot_" + i;
            slots[i] = slotObject.GetComponent<UI_RoguelikeCardSlot>();
        }

        // 설명 패널: 카드 줄 바로 아래(위쪽 고정, 아래로 자란다). 9-슬라이스 바탕 + 높이 연출 중 글자가 바탕 밖으로 새지 않게 RectMask2D.
        RectTransform desc = RoguelikeCardBuilder.CreateRect("DescPanel", content, Vector2Int.zero, new Vector2(DescWidth, DescHeight));
        desc.anchorMin = new Vector2(0.5f, 0.5f);
        desc.anchorMax = new Vector2(0.5f, 0.5f);
        desc.pivot = new Vector2(0.5f, 1f);
        desc.anchoredPosition = new Vector2(0f, CardRowY - 10);
        Image descBg = desc.gameObject.AddComponent<Image>();
        descBg.sprite = infoPanelSprite;
        descBg.type = Image.Type.Sliced;
        descBg.raycastTarget = false;
        desc.gameObject.AddComponent<RectMask2D>();

        // 세 글자 영역의 위치·크기는 UI_RoguelikeCardDescPanel이 글자에 맞춰 잡는다.
        TMP_Text nameText = CreateText("Name", desc, _font, string.Empty, TextAlignmentOptions.TopLeft, CreamColor, true, TextOverflowModes.Overflow);
        TMP_Text levelText = CreateText("Level", desc, _font, string.Empty, TextAlignmentOptions.TopRight, GoldColor, false, TextOverflowModes.Overflow);
        TMP_Text descText = CreateText("Description", desc, _font, string.Empty, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.82f, 0.76f, 1f), true, TextOverflowModes.Overflow);

        UI_RoguelikeCardDescPanel descPanel = desc.gameObject.AddComponent<UI_RoguelikeCardDescPanel>();
        SerializedObject descSo = new SerializedObject(descPanel);
        descSo.FindProperty("nameText").objectReferenceValue = nameText;
        descSo.FindProperty("levelText").objectReferenceValue = levelText;
        descSo.FindProperty("descriptionText").objectReferenceValue = descText;
        descSo.FindProperty("bCardSelectMode").boolValue = true;
        descSo.ApplyModifiedPropertiesWithoutUndo();

        // WAVE 배지: 화면 왼쪽 위. 시스템이 SetWave를 부르기 전에는 숨긴다.
        RectTransform waveBadge = AnchoredRect("WaveBadge", rootRect, new Vector2(0f, 1f), new Vector2(EdgeMargin, -EdgeMargin), new Vector2(WaveBadgeWidth, PlateHeight));
        Image waveImage = waveBadge.gameObject.AddComponent<Image>();
        waveImage.sprite = titlePlateSprite;
        waveImage.type = Image.Type.Sliced;
        waveImage.raycastTarget = false;
        TMP_Text waveText = CreateText("WaveText", waveBadge, _font, "WAVE 1", TextAlignmentOptions.Midline, GoldColor, false, TextOverflowModes.Overflow);
        StretchRect(waveText.rectTransform);
        waveText.rectTransform.offsetMin = new Vector2(0f, 1f);
        AddShadow(waveText);
        waveBadge.gameObject.SetActive(false);

        // 리롤 버튼: 카드 줄 바로 아래 가운데(Content 기준 - 위치는 스크립트가 카드 줄에 맞춰 잡는다). 판정(투명) → 기본 버튼 바탕 → 아이콘 · 글자 · 남은 횟수 배지.
        // 포커스 표시는 공용 커서 박스만 쓴다(별도 테두리 없음). 폭은 스크립트가 18개 언어 중 가장 긴 문구 기준 최소 폭과 현재 문구 폭 중 큰 값으로 잡는다.
        RectTransform rerollRect = AnchoredRect("RerollButton", content, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140f, RerollHeight));
        Image rerollHit = rerollRect.gameObject.AddComponent<Image>();
        rerollHit.color = new Color(1f, 1f, 1f, 0f);
        rerollHit.raycastTarget = true;
        Button rerollButton = rerollRect.gameObject.AddComponent<Button>();
        rerollButton.transition = Selectable.Transition.None;
        rerollButton.targetGraphic = rerollHit;
        CanvasGroup rerollGroup = rerollRect.gameObject.AddComponent<CanvasGroup>();
        UI_RoguelikeSelectRelay rerollRelay = rerollRect.gameObject.AddComponent<UI_RoguelikeSelectRelay>();

        Image rerollBg = CreateStretchImage("Background", rerollRect, Color.white);
        rerollBg.sprite = basicButtonSprite;
        rerollBg.type = Image.Type.Sliced;
        rerollBg.raycastTarget = false;

        Image rerollIcon = RoguelikeCardBuilder.CreateImage("Icon", rerollRect, _rerollIcon, Vector2Int.zero, _rerollIcon.rect.size);
        rerollIcon.raycastTarget = false;
        RectTransform rerollIconRect = rerollIcon.rectTransform;
        rerollIconRect.anchorMin = new Vector2(0f, 0.5f);
        rerollIconRect.anchorMax = new Vector2(0f, 0.5f);
        rerollIconRect.pivot = new Vector2(0f, 0.5f);
        // 리롤 그림(13x13)은 위쪽에 투명 1행이 붙어 14x14다. 피벗이 세로 가운데라 그림 아래 모서리를 예전 자리(가운데 - 6.5)에 두려면 0.5 올린다.
        rerollIconRect.anchoredPosition = new Vector2(9f, 0.5f);

        TMP_Text rerollLabel = CreateText("Label", rerollRect, _font, string.Empty, TextAlignmentOptions.MidlineLeft, DarkTextColor, false, TextOverflowModes.Overflow);
        StretchRect(rerollLabel.rectTransform);
        rerollLabel.rectTransform.offsetMin = new Vector2(26f, 1f);
        rerollLabel.rectTransform.offsetMax = new Vector2(-26f, 0f);

        RectTransform countBadge = AnchoredRect("CountBadge", rerollRect, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(14f, 14f));
        Image countImage = countBadge.gameObject.AddComponent<Image>();
        countImage.color = DarkTextColor;
        countImage.raycastTarget = false;
        TMP_Text rerollCount = CreateText("Count", countBadge, _font, "1", TextAlignmentOptions.Midline, GoldColor, false, TextOverflowModes.Overflow);
        StretchRect(rerollCount.rectTransform);
        rerollCount.rectTransform.offsetMin = new Vector2(0f, 1f);

        // 키 안내: 화면 왼쪽 아래. 패드 모드에서만 스크립트가 켠다(키보드 · 마우스 모드는 마우스 GUI 조작).
        RectTransform guide = AnchoredRect("PadGuide", rootRect, Vector2.zero, new Vector2(EdgeMargin, EdgeMargin + 5f), new Vector2(0f, GuideHeight));
        HorizontalLayoutGroup layout = guide.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        ContentSizeFitter guideFitter = guide.gameObject.AddComponent<ContentSizeFitter>();
        guideFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        UI_KeyboardImage moveKey = CreateKeyImage("MoveKey", guide, keyIcons, EOtherKey.None, EGamepadButton.LeftStick);
        TMP_Text hintMove = CreateGuideText("HintMove", guide, _font);
        UI_KeyboardImage selectKey = CreateKeyImage("SelectKey", guide, keyIcons, EOtherKey.Enter, EGamepadButton.Button_A_Cross);
        TMP_Text hintSelect = CreateGuideText("HintSelect", guide, _font);
        guide.gameObject.SetActive(false);

        // 화면 컴포넌트 연결. UIView 기본 설정(layer 등)은 기존 프리팹 값을 그대로 둔다.
        UIView_RoguelikeCardSelect view = root.GetComponent<UIView_RoguelikeCardSelect>();
        SerializedObject viewSo = new SerializedObject(view);

        SerializedProperty slotsProperty = viewSo.FindProperty("slots");
        slotsProperty.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++) slotsProperty.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

        viewSo.FindProperty("contentRoot").objectReferenceValue = content;
        viewSo.FindProperty("cardRowY").floatValue = CardRowY;
        viewSo.FindProperty("slotGap").floatValue = 14f;
        viewSo.FindProperty("descPanel").objectReferenceValue = descPanel;
        viewSo.FindProperty("rerollButton").objectReferenceValue = rerollButton;
        viewSo.FindProperty("rerollGroup").objectReferenceValue = rerollGroup;
        viewSo.FindProperty("rerollRelay").objectReferenceValue = rerollRelay;
        viewSo.FindProperty("rerollIconRect").objectReferenceValue = rerollIconRect;
        viewSo.FindProperty("rerollBackground").objectReferenceValue = rerollBg;
        viewSo.FindProperty("rerollLabel").objectReferenceValue = rerollLabel;
        viewSo.FindProperty("rerollCountText").objectReferenceValue = rerollCount;
        viewSo.FindProperty("titlePlate").objectReferenceValue = titlePlate;
        viewSo.FindProperty("titleText").objectReferenceValue = titleText;
        viewSo.FindProperty("waveBadge").objectReferenceValue = waveBadge;
        viewSo.FindProperty("waveText").objectReferenceValue = waveText;
        viewSo.FindProperty("padGuideRow").objectReferenceValue = guide;
        viewSo.FindProperty("hintMoveText").objectReferenceValue = hintMove;
        viewSo.FindProperty("hintSelectText").objectReferenceValue = hintSelect;
        SerializedProperty guideImages = viewSo.FindProperty("padGuideImages");
        guideImages.arraySize = 2;
        guideImages.GetArrayElementAtIndex(0).objectReferenceValue = moveKey;
        guideImages.GetArrayElementAtIndex(1).objectReferenceValue = selectKey;
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, ViewPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    /// <summary>앵커 · 피벗이 같은 지점(_corner)인 사각형. _position은 그 지점에서의 거리.</summary>
    private static RectTransform AnchoredRect(string _name, RectTransform _parent, Vector2 _corner, Vector2 _position, Vector2 _size)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        rect.anchorMin = _corner;
        rect.anchorMax = _corner;
        rect.pivot = _corner;
        rect.sizeDelta = _size;
        rect.anchoredPosition = _position;
        return rect;
    }

    /// <summary>글자 아래 1px 그림자(글자를 복제해 어두운 색으로 깔고 UI_TextShadowFollower가 원본을 따라가게 한다).</summary>
    private static void AddShadow(TMP_Text _text)
    {
        TMP_Text shadow = UnityEngine.Object.Instantiate(_text.gameObject, _text.transform.parent).GetComponent<TMP_Text>();
        shadow.name = _text.name + "Shadow";
        shadow.color = ShadowColor;
        shadow.transform.SetSiblingIndex(_text.transform.GetSiblingIndex());
        UI_TextShadowFollower follower = shadow.gameObject.AddComponent<UI_TextShadowFollower>();
        SerializedObject so = new SerializedObject(follower);
        so.FindProperty("source").objectReferenceValue = _text;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static TMP_Text CreateGuideText(string _name, RectTransform _parent, TMP_FontAsset _font)
    {
        TMP_Text text = CreateText(_name, _parent, _font, string.Empty, TextAlignmentOptions.MidlineLeft, Color.white, false, TextOverflowModes.Overflow);
        ContentSizeFitter fitter = text.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        LayoutElement element = text.gameObject.AddComponent<LayoutElement>();
        element.minHeight = GuideHeight;
        element.preferredHeight = GuideHeight;
        return text;
    }

    private static UI_KeyboardImage CreateKeyImage(string _name, RectTransform _parent, KeyIconDatabase _database, EOtherKey _key, EGamepadButton _button)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        Image image = go.AddComponent<Image>();
        image.raycastTarget = false;
        RoguelikeHUDBuilder.CopyInventoryKeyEffect(go);

        rect.sizeDelta = new Vector2(KeyIconSize, KeyIconSize);
        LayoutElement element = go.AddComponent<LayoutElement>();
        element.minWidth = KeyIconSize;
        element.minHeight = KeyIconSize;
        element.preferredWidth = KeyIconSize;
        element.preferredHeight = KeyIconSize;

        UI_KeyboardImage keyImage = go.AddComponent<UI_KeyboardImage>();
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

    private static string[] BuildRerollRows()
    {
        // 위쪽이 열린 둥근 화살표(새로 뽑기). 11줄 x 11칸.
        return new[]
        {
            "...####....",
            "..#....##..",
            ".#.....###.",
            "#.......#..",
            "#..........",
            "#..........",
            "#..........",
            ".#.......#.",
            "..#.....#..",
            "...#####...",
            "...........",
        };
    }

    /// <summary>
    /// ASCII 글리프로 임시 아이콘 PNG를 만든다(흰 글리프 + 1px 검은 외곽선, 가장자리 여백 1px 포함). 파일이 이미 있으면 그대로 쓴다.
    /// 픽셀 아트 규칙에 맞게 Point 필터·비압축·Single 스프라이트로 가져온다.
    /// </summary>
    private static Sprite EnsureIconSprite(string _path, string[] _rows)
    {
        int gw = _rows[0].Length;
        int gh = _rows.Length;
        int w = gw + 2;
        int h = gh + 2;
        Color32[] pixels = new Color32[w * h];

        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                if ('#' != _rows[y][x]) continue;

                int px = x + 1;
                int py = gh - 1 - y + 1; // 텍스처는 아래에서 위
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int idx = (py + dy) * w + px + dx;
                        if (0 == pixels[idx].a) pixels[idx] = new Color32(0, 0, 0, 255);
                    }
                }
            }
        }

        for (int y = 0; y < gh; y++)
        {
            for (int x = 0; x < gw; x++)
            {
                if ('#' != _rows[y][x]) continue;

                pixels[(gh - 1 - y + 1) * w + x + 1] = new Color32(255, 255, 255, 255);
            }
        }

        return RoguelikeCardBuilder.EnsureSpriteFromPixels(_path, pixels, w, h);
    }

    private static Image CreateStretchImage(string _name, RectTransform _parent, Color _color)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        StretchRect(rect);

        Image image = go.AddComponent<Image>();
        image.color = _color;
        return image;
    }

    private static TMP_Text CreateText(string _name, RectTransform _parent, TMP_FontAsset _font, string _text, TextAlignmentOptions _alignment, Color _color, bool _bWrap, TextOverflowModes _overflow)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(_parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = _font.faceInfo.pointSize;
        text.text = _text;
        text.alignment = _alignment;
        text.color = _color;
        text.textWrappingMode = _bWrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = _overflow;
        text.raycastTarget = false;

        // 언어별 폰트 교체 시스템이 Instantiate 직후부터 원본 폰트를 알 수 있게 미리 붙인다.
        go.AddComponent<LocalizedFontTracker>();
        return text;
    }

    private static void StretchRect(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }
}
#endif
