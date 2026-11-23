#if UNITY_EDITOR
using Coffee.UIEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD(UIView_RoguelikeHUD 프리팹)를 만든다. 여러 번 실행해도 같은 결과로 덮어쓴다.
/// 웨이브 · 임계치 · 처치 · 스태미너 · 대쉬는 섬 둘레 월드 HUD(RoguelikeWorldHud.prefab, RoguelikeWorldHudBuilder)로 그리므로 이 프리팹에는 그림이 없다(WorldHUD_Plan.md).
///   StaminaBar   그림 없는 전달 컴포넌트(UI_RoguelikeStaminaBar). 월드 HUD는 UIView_RoguelikeHUD.Initialize가 worldHudPrefab으로 만들어 넘긴다.
///   DashCells    그림 없는 전달 컴포넌트(UI_RoguelikeDashCells).
///   OwnedTray    우상단 보관함 트레이(WorldHUD_Plan.md 11-1: 인벤토리 키 아이콘 위 끝 y 3, 오른쪽 여백 8). 모양 · 동작은 그대로이고 앵커만 오른쪽 위다.
/// 트레이 스프라이트는 Assets/Graphics/Roguelike/hud/에 만든다(파일이 없을 때만 만들며, 가로 세로가 홀수인 그림은 오른쪽 열 / 위쪽 행에 투명 1px을 붙여 짝수로 만든다).
/// </summary>
public static class RoguelikeHUDBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeHUD.prefab";
    private const string WorldHudPrefabPath = "Assets/Prefabs/Roguelike/UI/RoguelikeWorldHud.prefab";
    private const string HudDir = "Assets/Graphics/Roguelike/hud/";
    private const string KeyIconDatabasePath = "Assets/Prefabs/UI/Option/KeyIconDatabase.asset";
    private const string InventoryPrefabPath = "Assets/Prefabs/UI/Popup/UI_Inventory.prefab";
    private const string NumberSheetPath = "Assets/Graphics/Font/SmallCurrencyNum.png";

    // 보관함 트레이(우상단): 보유 카드 총량 숫자판 34x19 하나(겹친 카드 묶음 아이콘 11x11 + 합계 숫자) + 오른쪽 위 모서리에 걸친 인벤토리 키 아이콘(16x16).
    // 판 그림 높이가 홀수(19)라 아이콘(11) · 숫자(8)가 판 그림 기준으로 위아래 같은 여백에 놓인다. 스프라이트(높이 20)는 그림 위에 투명 1행이 붙은 것이다. 표시 전용이라 레이캐스트를 받는 그래픽이 없다.
    private const int TrayPlateWidth = UI_RoguelikeOwnedCardTray.PlateWidth;
    private const int TrayPlateArtHeight = UI_RoguelikeOwnedCardTray.PlateArtHeight;
    private const int TrayIconSize = 11;
    // 카드 그림 크기(7x9). 스프라이트는 오른쪽 열 · 위쪽 행에 투명 1px이 붙어 8x10이다.
    private const int TrayCardWidth = 7;
    private const int TrayCardHeight = 9;
    private const int TrayBackOffsetX = 4;
    private const int TrayIconX = 3;
    private const int TrayIconY = (TrayPlateArtHeight - TrayIconSize) / 2;
    private const int TrayContentY = 6;
    private const int TrayKeySize = 16;
    private const int TrayKeyDrop = 5;
    private const int TrayKeyShiftX = 3;
    private const int TrayNumberRight = TrayPlateWidth - 3;
    private const int TrayWidth = TrayPlateWidth;
    // 트레이 칸(보유 카드 창이 줄어들어 닿는 사각형)은 그림 크기다. 판 스프라이트(34x20)의 투명 윗줄은 칸 위로 한 줄 삐져나온다.
    private const int TrayHeight = TrayPlateArtHeight;
    private const int TrayMargin = 8;
    // 키 아이콘 위 끝이 화면 위에서 3px(위 가운데 · 좌상단 숫자와 같은 높이). 키 아이콘은 판 위로 (TrayKeySize - TrayKeyDrop)만큼 솟으므로 판 위 끝은 그만큼 아래다(y 14).
    private const int TrayKeyTopMargin = 3;
    private const int TrayTopMargin = TrayKeyTopMargin + TrayKeySize - TrayKeyDrop;

    [MenuItem("LumberBoy/UI/Build Roguelike HUD")]
    public static void Build()
    {
        if (false == AssetDatabase.IsValidFolder("Assets/Graphics/Roguelike/hud"))
        {
            AssetDatabase.CreateFolder("Assets/Graphics/Roguelike", "hud");
        }

        Sprite[] digits = LoadDigitSprites();
        if (null == digits) return;

        // 월드 HUD 프리팹이 없으면 먼저 만든다.
        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(WorldHudPrefabPath)) RoguelikeWorldHudBuilder.Build();

        RoguelikeWorldHud worldHudPrefab = AssetDatabase.LoadAssetAtPath<RoguelikeWorldHud>(WorldHudPrefabPath);
        if (null == worldHudPrefab)
        {
            Debug.LogError("[RoguelikeHUDBuilder] 월드 HUD 프리팹을 찾지 못했습니다: " + WorldHudPrefabPath);
            return;
        }

        Sprite[] trayPlates =
        {
            Make("TrayPlate", TrayPlatePixels(0), TrayPlateWidth, TrayPlateArtHeight),
            Make("TrayPlateLit1", TrayPlatePixels(1), TrayPlateWidth, TrayPlateArtHeight),
            Make("TrayPlateLit2", TrayPlatePixels(2), TrayPlateWidth, TrayPlateArtHeight),
        };
        Sprite trayCardBack = Make("TrayCardBack", TrayCardPixels(false), TrayCardWidth, TrayCardHeight);
        Sprite trayCardFront = Make("TrayCardFront", TrayCardPixels(true), TrayCardWidth, TrayCardHeight);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            UIView_RoguelikeHUD view = root.GetComponent<UIView_RoguelikeHUD>();
            if (null == view)
            {
                Debug.LogError("[RoguelikeHUDBuilder] UIView_RoguelikeHUD 컴포넌트를 찾지 못했습니다.");
                return;
            }

            string[] oldNames = { "ThresholdBar", "PressureStrip", "WaveLabel", "GraceTimer", "StaminaBar", "DashCells", "OwnedTray" };
            for (int i = 0; i < oldNames.Length; i++)
            {
                Transform existing = root.transform.Find(oldNames[i]);
                if (null != existing) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }

            RectTransform rootRect = (RectTransform)root.transform;

            // ---- 스태미너 · 대쉬 전달 컴포넌트(그림 없음). 본편 HUD가 정적 연결점(RoguelikeHUDLink)으로 값을 넘기고, 이 컴포넌트가 월드 HUD로 넘긴다.
            RectTransform staminaRect = RoguelikeCardBuilder.CreateRect("StaminaBar", rootRect, Vector2Int.zero, Vector2.zero);
            UI_RoguelikeStaminaBar staminaBar = staminaRect.gameObject.AddComponent<UI_RoguelikeStaminaBar>();

            RectTransform dashRect = RoguelikeCardBuilder.CreateRect("DashCells", rootRect, Vector2Int.zero, Vector2.zero);
            UI_RoguelikeDashCells dashCells = dashRect.gameObject.AddComponent<UI_RoguelikeDashCells>();

            // ---- 보관함 트레이(우상단): 보유 카드 총량 숫자판 하나. 표시 전용(클릭 없음) - 보유 카드 창은 키로 연다.
            RectTransform trayRect = RoguelikeCardBuilder.CreateRect("OwnedTray", rootRect, Vector2Int.zero, new Vector2(TrayWidth, TrayHeight));
            trayRect.anchorMin = Vector2.one;
            trayRect.anchorMax = Vector2.one;
            trayRect.pivot = Vector2.one;
            trayRect.anchoredPosition = new Vector2(-TrayMargin, -TrayTopMargin);

            RectTransform trayVisual = RoguelikeCardBuilder.CreateRect("Visual", trayRect, Vector2Int.zero, new Vector2(TrayWidth, TrayHeight));
            TrayParts trayParts = NewTrayPlate("CountPlate", trayVisual, trayPlates, trayCardBack, trayCardFront, digits);

            UI_KeyboardImage ownedKeyImage = NewTrayKeyIcon("Key_Inventory", trayVisual, AssetDatabase.LoadAssetAtPath<KeyIconDatabase>(KeyIconDatabasePath));

            UI_RoguelikeOwnedCardTray tray = trayRect.gameObject.AddComponent<UI_RoguelikeOwnedCardTray>();
            SerializedObject traySo = new SerializedObject(tray);
            traySo.FindProperty("totalNumber").objectReferenceValue = trayParts.number;
            traySo.FindProperty("plateImage").objectReferenceValue = trayParts.plate;
            SerializedProperty plateSpriteProperty = traySo.FindProperty("plateSprites");
            plateSpriteProperty.arraySize = trayPlates.Length;
            for (int i = 0; i < trayPlates.Length; i++) plateSpriteProperty.GetArrayElementAtIndex(i).objectReferenceValue = trayPlates[i];
            traySo.FindProperty("keyIcon").objectReferenceValue = ownedKeyImage.transform;
            traySo.FindProperty("frontCardIcon").objectReferenceValue = trayParts.frontIcon.rectTransform;
            traySo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject viewSo = new SerializedObject(view);
            viewSo.FindProperty("worldHudPrefab").objectReferenceValue = worldHudPrefab;
            viewSo.FindProperty("staminaBar").objectReferenceValue = staminaBar;
            viewSo.FindProperty("dashCells").objectReferenceValue = dashCells;
            viewSo.FindProperty("ownedTray").objectReferenceValue = tray;
            viewSo.FindProperty("ownedKeyImage").objectReferenceValue = ownedKeyImage;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[RoguelikeHUDBuilder] 완료: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- 오브젝트

    /// <summary>
    /// 작은 숫자 한 묶음. NumberAnchor(폭 0, 높이 8)가 중심 위치를 잡고, 그 자식 Number(RawImage)를 컴포넌트가 폭에 맞춰 정수 칸에 놓는다.
    /// _centerX는 부모 왼쪽 아래 기준 숫자의 가운데 x, _bottomY는 숫자 아래 모서리 y.
    /// </summary>
    private static UI_RoguelikeIconNumber NewNumber(RectTransform _parent, int _centerX, int _bottomY, Sprite[] _digits)
    {
        RectTransform anchor = RoguelikeCardBuilder.CreateRect("NumberAnchor", _parent, new Vector2Int(_centerX, _bottomY), new Vector2(0f, HUD_BuffNumberFont.GlyphHeight));

        RectTransform numberRect = RoguelikeCardBuilder.CreateRect("Number", anchor, Vector2Int.zero, new Vector2(8f, HUD_BuffNumberFont.GlyphHeight));
        numberRect.anchorMin = new Vector2(0.5f, 0.5f);
        numberRect.anchorMax = new Vector2(0.5f, 0.5f);
        numberRect.pivot = new Vector2(0f, 0.5f);
        numberRect.anchoredPosition = Vector2.zero;

        RawImage raw = numberRect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        raw.enabled = false;

        UI_RoguelikeIconNumber number = numberRect.gameObject.AddComponent<UI_RoguelikeIconNumber>();
        SerializedObject so = new SerializedObject(number);
        so.FindProperty("image").objectReferenceValue = raw;
        SerializedProperty digitProperty = so.FindProperty("digitSprites");
        digitProperty.arraySize = _digits.Length;
        for (int i = 0; i < _digits.Length; i++) digitProperty.GetArrayElementAtIndex(i).objectReferenceValue = _digits[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return number;
    }

    private static Image NewIcon(string _name, RectTransform _parent, Sprite _sprite, int _x, int _y, Color _color)
    {
        Image image = RoguelikeCardBuilder.CreateImage(_name, _parent, _sprite, new Vector2Int(_x, _y), _sprite.rect.size);
        image.color = _color;
        return image;
    }

    /// <summary>
    /// 인벤토리 키 아이콘(보유 카드 창을 여는 키). 리바인딩된 인벤토리 키를 따라가고 입력 장치에 맞춰 키보드 · 패드 아이콘이 바뀐다(인벤토리 UI와 같은 UI_KeyboardImage).
    /// 16x16 원본 크기, 트레이 오른쪽 위 모서리에 걸친다(아래쪽이 윗판에 겹친다). 흰 외곽선은 인벤토리의 키 아이콘(UI_Inventory/Keyboard)의 UIEffect를 그대로 복사한다.
    /// </summary>
    private static UI_KeyboardImage NewTrayKeyIcon(string _name, RectTransform _parent, KeyIconDatabase _database)
    {
        RectTransform rect = RoguelikeCardBuilder.CreateRect(_name, _parent, new Vector2Int(TrayWidth - TrayKeySize + TrayKeyShiftX, TrayPlateArtHeight - TrayKeyDrop), new Vector2(TrayKeySize, TrayKeySize));
        Image image = rect.gameObject.AddComponent<Image>();
        image.raycastTarget = false;

        CopyInventoryKeyEffect(rect.gameObject);

        UI_KeyboardImage keyImage = rect.gameObject.AddComponent<UI_KeyboardImage>();
        SerializedObject so = new SerializedObject(keyImage);
        so.FindProperty("keyIconDatabase").objectReferenceValue = _database;
        so.FindProperty("keyIconMode").enumValueIndex = (int)EKeyIconMode.RebindableAction;
        so.FindProperty("boundAction").enumValueIndex = System.Array.IndexOf(System.Enum.GetValues(typeof(ERebindableAction)), ERebindableAction.Inventory);
        so.FindProperty("targetImage").objectReferenceValue = image;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 첫 모양(키보드 아이콘)을 미리 채워 둔다. HUD가 열릴 때 UI_KeyboardImage.Initialize가 입력 장치에 맞춰 다시 정한다.
        keyImage.RefreshIcon();
        return keyImage;
    }

    /// <summary>인벤토리 키 아이콘의 UIEffect(흰 외곽선 Outline8)를 _target에 그대로 복사한다. 설정값을 따로 옮기지 않아 인벤토리와 같은 외곽선이 된다.</summary>
    internal static void CopyInventoryKeyEffect(GameObject _target)
    {
        GameObject inventoryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPrefabPath);
        UI_KeyboardImage sourceKey = null != inventoryPrefab ? inventoryPrefab.GetComponentInChildren<UI_KeyboardImage>(true) : null;
        UIEffect sourceEffect = null != sourceKey ? sourceKey.GetComponent<UIEffect>() : null;

        if (null == sourceEffect || false == UnityEditorInternal.ComponentUtility.CopyComponent(sourceEffect))
        {
            Debug.LogWarning("[RoguelikeHUDBuilder] 인벤토리 키 아이콘의 UIEffect를 찾지 못해 흰 외곽선을 넣지 못했습니다: " + InventoryPrefabPath);
            return;
        }

        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(_target);
    }

    private class TrayParts
    {
        public Image plate;
        public UI_RoguelikeIconNumber number;
        public Image frontIcon;
    }

    /// <summary>
    /// 트레이의 숫자판: 판 + 카드 아이콘(뒤 카드 · 앞 카드. 평소에는 고정이고 앞 카드만 보관 도착 반응에서 위로 튄다. 자체 색, 색 곱 없음) + 오른쪽 숫자(판 오른쪽 끝에 붙는 오른쪽 정렬이라 자릿수가 늘면 왼쪽으로 자란다).
    /// 아이콘 11x11 칸 안에서 뒤 카드는 오른쪽 위(왼쪽 4 · 위 0), 앞 카드는 왼쪽 아래(왼쪽 0 · 위 2)다. 아이콘 · 숫자는 판 세로 가운데.
    /// </summary>
    private static TrayParts NewTrayPlate(string _name, RectTransform _parent, Sprite[] _plates, Sprite _cardBack, Sprite _cardFront, Sprite[] _digits)
    {
        Image plate = RoguelikeCardBuilder.CreateImage(_name, _parent, _plates[0], Vector2Int.zero, _plates[0].rect.size);
        NewIcon("IconBack", plate.rectTransform, _cardBack, TrayIconX + TrayBackOffsetX, TrayIconY + TrayIconSize - TrayCardHeight, Color.white);
        Image frontIcon = NewIcon("IconFront", plate.rectTransform, _cardFront, TrayIconX, TrayIconY, Color.white);

        UI_RoguelikeIconNumber number = NewNumber(plate.rectTransform, TrayNumberRight, TrayContentY, _digits);

        SerializedObject so = new SerializedObject(number);
        so.FindProperty("bRightAlign").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        return new TrayParts { plate = plate, number = number, frontIcon = frontIcon };
    }

    /// <summary>버프 HUD와 같은 숫자 시트. 시트 순서가 1~9,0이라 _0~_8이 1~9, _9가 0이다.</summary>
    private static Sprite[] LoadDigitSprites()
    {
        UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(NumberSheetPath);
        Sprite[] digits = new Sprite[10];

        for (int i = 0; i < all.Length; i++)
        {
            Sprite sprite = all[i] as Sprite;
            if (null == sprite) continue;

            string name = sprite.name;
            int index = int.Parse(name.Substring(name.LastIndexOf('_') + 1));

            if (index <= 8) digits[index + 1] = sprite;
            else if (9 == index) digits[0] = sprite;
        }

        for (int i = 0; i < 10; i++)
        {
            if (null != digits[i]) continue;

            Debug.LogError("[RoguelikeHUDBuilder] 숫자 스프라이트를 찾지 못했습니다: " + i);
            return null;
        }

        return digits;
    }

    // ---------------------------------------------------------------- 스프라이트 만들기

    /// <summary>
    /// 아래에서 위 순서의 픽셀로 스프라이트를 만든다. 파일이 이미 있으면 읽기만 하고 다시 쓰지 않는다(디자이너 · 사용자가 바꾼 그림과 설정을 지키기 위해).
    /// 가로 세로가 홀수이면 투명 1px을 오른쪽 열 / 위쪽 행에 붙여 짝수로 만든다(RoguelikeCardBuilder.EnsureSpriteFromPixels).
    /// </summary>
    private static Sprite Make(string _name, Color32[] _pixels, int _w, int _h)
    {
        string path = HudDir + _name + ".png";
        Sprite sprite = RoguelikeCardBuilder.EnsureSpriteFromPixels(path, _pixels, _w, _h);
        EnsureImportSettings(path);
        return sprite;
    }

    /// <summary>
    /// 임포트 설정이 픽셀 아트 규칙(Single, Point, PPU 32, Full Rect, 밉맵 끔, 압축 없음, 알파 투명)이 아닐 때만 규칙대로 고친다.
    /// 사용자가 이미 맞춰 둔 설정은 건드리지 않는다(PPU · 메쉬 타입도 조건에 들어 있어 되돌리지 않는다).
    /// </summary>
    private static void EnsureImportSettings(string _path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(_path);
        if (null == importer || true == RoguelikeCardBuilder.IsPixelArtImportOk(importer, RoguelikeCardBuilder.PixelArtPixelsPerUnit)) return;

        RoguelikeCardBuilder.ApplyPixelArtImportSettings(importer, RoguelikeCardBuilder.PixelArtPixelsPerUnit);
        importer.SaveAndReimport();
    }

    /// <summary>
    /// 트레이 카드 한 장 7x9: 1칸 검은 윤곽(프레임 윤곽색과 같은 계열) + 채움. 뒤 카드는 어두운 갈색, 앞 카드는 밝은 갈색에 안쪽 맨 위 줄 · 맨 왼쪽 줄 하이라이트.
    /// 카드 프레임 팔레트: 윤곽 #663931, 밝음 #ca8f73, 어둠 #8f563b.
    /// </summary>
    private static Color32[] TrayCardPixels(bool _bFront)
    {
        Color32[] pixels = new Color32[TrayCardWidth * TrayCardHeight];
        Color32 black = new Color32(0, 0, 0, 255);
        Color32 fill = _bFront ? new Color32(143, 86, 59, 255) : new Color32(102, 57, 49, 255);
        Color32 light = new Color32(202, 143, 115, 255);

        for (int ty = 0; ty < TrayCardHeight; ty++)
        {
            for (int tx = 0; tx < TrayCardWidth; tx++)
            {
                bool bEdge = 0 == tx || TrayCardWidth - 1 == tx || 0 == ty || TrayCardHeight - 1 == ty;
                Color32 color = bEdge ? black : fill;
                if (false == bEdge && true == _bFront && (1 == ty || 1 == tx)) color = light;

                pixels[(TrayCardHeight - 1 - ty) * TrayCardWidth + tx] = color;
            }
        }

        return pixels;
    }

    /// <summary>
    /// 트레이 숫자판 그림 34x19(스프라이트는 위에 투명 1행이 붙은 34x20): 윤곽 + 어두운 바탕 + 위쪽 안쪽 하이라이트 한 줄. 네 모서리는 한 칸씩 비운다.
    /// _lit 0 = 기본, 1 = 약한 점멸(테두리가 살구색, 위 하이라이트가 한 단계 밝음), 2 = 강한 점멸(테두리가 거의 흰색, 위 하이라이트가 밝은 살구색). 바탕은 그대로다 - 판 전체가 아니라 테두리만 번쩍인다.
    /// 반투명 없이 판 스프라이트를 갈아 끼워 단계로 번쩍인다.
    /// </summary>
    private static Color32[] TrayPlatePixels(int _lit)
    {
        Color32[] pixels = new Color32[TrayPlateWidth * TrayPlateArtHeight];
        Color32 outline = new Color32(59, 41, 31, 255);
        Color32 fill = new Color32(35, 24, 16, 255);
        Color32 highlight = new Color32(90, 66, 40, 255);

        if (1 == _lit)
        {
            outline = new Color32(255, 217, 160, 255);
            highlight = new Color32(170, 128, 84, 255);
        }
        else if (2 == _lit)
        {
            outline = new Color32(255, 244, 214, 255);
            highlight = new Color32(255, 222, 168, 255);
        }

        for (int y = 0; y < TrayPlateArtHeight; y++)
        {
            for (int x = 0; x < TrayPlateWidth; x++)
            {
                bool bCorner = (0 == x || TrayPlateWidth - 1 == x) && (0 == y || TrayPlateArtHeight - 1 == y);
                if (true == bCorner) continue;

                bool bEdge = 0 == x || TrayPlateWidth - 1 == x || 0 == y || TrayPlateArtHeight - 1 == y;
                if (true == bEdge) pixels[y * TrayPlateWidth + x] = outline;
                else if (TrayPlateArtHeight - 2 == y && 2 <= x && x <= TrayPlateWidth - 3) pixels[y * TrayPlateWidth + x] = highlight;
                else pixels[y * TrayPlateWidth + x] = fill;
            }
        }

        return pixels;
    }
}
#endif
