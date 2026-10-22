#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 선택 화면 프리팹 구성기. 여러 번 실행해도 같은 결과로 덮어쓴다. 카드 프리팹(RoguelikeCardBuilder)을 먼저 다시 만든다.
///
///   UI_RoguelikeCardSlot.prefab  카드 1칸: 카드 + 판정 박스(Button + 포커스 중계)
///   UIView_RoguelikeCardSelect.prefab  화면: 딤 → Content(슬롯 4칸 · 리롤 아이콘 버튼 · 설명 패널)
///
/// v2.4 - 골드·가격·건너뛰기가 없다. 화면 배치(Content 중심 기준, 정수 픽셀)는 카드 줄 → 바로 아래 가운데 리롤 아이콘 버튼 → 설명 패널을 붙여 시선이 한곳에 모이게 한다.
///   카드 y 6~87 / 리롤 버튼 y -20~-2 / 설명 패널 위쪽 y -26 (아래로 자란다)
///
/// 리롤 버튼 바탕·설명 패널 바탕은 디자인이 정해지기 전의 자리표시자(단색 박스 + 프로젝트 기본 한글 TMP 폰트)다.
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

    private const int SlotCount = 4;
    private const int CardRowY = 6;
    private const int CardToButtonGap = 8;
    private const int ButtonHeight = 18;
    private const int RerollWidth = 26;
    private const int DescWidth = 190;
    private const int DescHeight = 48;
    private const int ContentOffsetY = -10;

    private static readonly Color ButtonColor = new Color(0.22f, 0.14f, 0.1f, 0.95f);
    private static readonly Color PanelColor = new Color(0.16f, 0.1f, 0.07f, 0.95f);
    private static readonly Color DimColor = new Color(0f, 0f, 0f, 0.7f);

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
        GameObject root = PrefabUtility.LoadPrefabContents(ViewPrefabPath);
        RectTransform rootRect = (RectTransform)root.transform;

        for (int i = rootRect.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(rootRect.GetChild(i).gameObject);

        // 딤: 화면 전체를 어둡게 덮고 뒤쪽 클릭을 막는다.
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

        UI_RoguelikeCardSlot[] slots = new UI_RoguelikeCardSlot[SlotCount];
        for (int i = 0; i < SlotCount; i++)
        {
            GameObject slotObject = (GameObject)PrefabUtility.InstantiatePrefab(_slotPrefab, content);
            slotObject.name = "Slot_" + i;
            slots[i] = slotObject.GetComponent<UI_RoguelikeCardSlot>();
        }

        // 리롤 버튼: 카드 줄 바로 아래 가운데. 아이콘만 있는 고정 크기 버튼이다(글자가 없어 언어에 따라 폭이 달라지지 않는다).
        // 이름·설명은 설명 패널이 보여 준다.
        int buttonY = CardRowY - CardToButtonGap - ButtonHeight;
        CreateButton("RerollButton", content, new Vector2Int(-RerollWidth / 2, buttonY), new Vector2Int(RerollWidth, ButtonHeight),
            out Button rerollButton, out CanvasGroup rerollGroup, out UI_RoguelikeSelectRelay rerollRelay, out RectTransform rerollRect);
        RectTransform rerollIconRect = PlaceIcon("Icon", rerollRect, _rerollIcon, RerollWidth, ButtonHeight);

        // 설명 패널: 버튼 바로 아래. 이름·레벨(첫 줄) + 설명.
        // 위쪽(피벗 위)을 버튼 아래 6px에 고정한다. 글자 길이에 따라 높이는 스크립트가 아래로 늘린다.
        RectTransform desc = RoguelikeCardBuilder.CreateRect("DescPanel", content, Vector2Int.zero, new Vector2(DescWidth, DescHeight));
        desc.anchorMin = new Vector2(0.5f, 0.5f);
        desc.anchorMax = new Vector2(0.5f, 0.5f);
        desc.pivot = new Vector2(0.5f, 1f);
        desc.anchoredPosition = new Vector2(0f, buttonY - 6);
        Image descBg = desc.gameObject.AddComponent<Image>();
        descBg.color = PanelColor;
        descBg.raycastTarget = false;

        // 세 글자 영역의 위치·크기는 UI_RoguelikeCardDescPanel이 글자에 맞춰 잡는다.
        TMP_Text nameText = CreateText("Name", desc, _font, string.Empty, TextAlignmentOptions.TopLeft, Color.white, true, TextOverflowModes.Overflow);
        TMP_Text levelText = CreateText("Level", desc, _font, string.Empty, TextAlignmentOptions.TopRight, new Color(1f, 0.8f, 0.25f, 1f), false, TextOverflowModes.Overflow);
        TMP_Text descText = CreateText("Description", desc, _font, string.Empty, TextAlignmentOptions.TopLeft, new Color(0.85f, 0.85f, 0.85f, 1f), true, TextOverflowModes.Overflow);

        UI_RoguelikeCardDescPanel descPanel = desc.gameObject.AddComponent<UI_RoguelikeCardDescPanel>();
        SerializedObject descSo = new SerializedObject(descPanel);
        descSo.FindProperty("nameText").objectReferenceValue = nameText;
        descSo.FindProperty("levelText").objectReferenceValue = levelText;
        descSo.FindProperty("descriptionText").objectReferenceValue = descText;
        descSo.ApplyModifiedPropertiesWithoutUndo();

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
        viewSo.FindProperty("rerollBackground").objectReferenceValue = rerollRect.GetComponent<Image>();
        viewSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, ViewPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    private static RectTransform PlaceIcon(string _name, RectTransform _parent, Sprite _sprite, int _buttonWidth, int _buttonHeight)
    {
        Vector2 size = _sprite.rect.size;
        Vector2Int position = new Vector2Int(Mathf.FloorToInt((_buttonWidth - size.x) * 0.5f), Mathf.FloorToInt((_buttonHeight - size.y) * 0.5f));
        Image icon = RoguelikeCardBuilder.CreateImage(_name, _parent, _sprite, position, size);
        return icon.rectTransform;
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

    private static void CreateButton(string _name, RectTransform _parent, Vector2Int _position, Vector2Int _size, out Button _button, out CanvasGroup _group,
        out UI_RoguelikeSelectRelay _relay, out RectTransform _rect)
    {
        _rect = RoguelikeCardBuilder.CreateRect(_name, _parent, _position, _size);
        _rect.anchorMin = new Vector2(0.5f, 0.5f);
        _rect.anchorMax = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = _position;

        Image image = _rect.gameObject.AddComponent<Image>();
        image.color = ButtonColor;
        image.raycastTarget = true;

        _button = _rect.gameObject.AddComponent<Button>();
        _button.transition = Selectable.Transition.None;
        _button.targetGraphic = image;

        _group = _rect.gameObject.AddComponent<CanvasGroup>();
        _relay = _rect.gameObject.AddComponent<UI_RoguelikeSelectRelay>();
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
