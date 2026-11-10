#if UNITY_EDITOR
using Coffee.UIEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD(UIView_RoguelikeHUD 프리팹)를 만든다. 여러 번 실행해도 같은 결과로 덮어쓴다.
/// 확정 시안(Docs/Roguelike/HUD_Polish)과 같은 픽셀로 좌측 하단(화면 여백 8px)에 막대를 세로로 쌓는다. 640x360 캔버스 좌표(왼쪽 위 원점, 윤곽 포함 크기):
///   WaveLabel     (7, 296)  깃발 16x16(4, 295) + 웨이브 숫자(잉크 왼쪽 19) + 진행 점 30칸. 언리밋은 금색 깃발 + 무한대 16x16(17, 295) + 번호(잉크 왼쪽 32) + 10칸   UI_RoguelikeWaveLabel
///   PressureStrip (7, 310)  T 표식(5x3 그림, 6x4 스프라이트, 그림 y 310) + 나무 아이콘 16x16(4, 310) + 임계치 바 112x8(19, 313) + 숫자(135, 313)   UI_RoguelikePressureStrip
///   GraceTimer    (20, 321) 임계치 바에 붙은 2px 줄 110x3(안쪽 108x2, 스프라이트 110x4), 바 아래 윤곽(y 320)을 공유   UI_RoguelikeGraceTimer
///   ThresholdBar  (7, 328)  도끼 아이콘 16x16(4, 326) + 처치 바 112x8(19, 329) + 숫자(135, 329)   UI_RoguelikeThresholdBar
///   StaminaBar    (7, 342)  번개 아이콘 16x16(4, 340. 사용자 그림) + 스태미너 바 112x10(19, 342), 1/4 · 1/2 · 3/4 눈금   UI_RoguelikeStaminaBar
///   DashCells     (131, 340) 대쉬 아이콘 16x16(131, 340) + 칸 14x9 그림 둘(x 146, 161, 그림 y 343, 스프라이트 14x10)   UI_RoguelikeDashCells
///   OwnedTray     우하단(변경 없음)
/// HUD 아이콘 7종(HudIcon*.png)은 16x16 원본이고 구성기는 읽기만 한다(없으면 오류 로그). 나머지 스프라이트는 파일이 없을 때만 만들며, 가로 세로가 홀수인 그림은 오른쪽 열 / 위쪽 행에 투명 1px을 붙여 짝수로 만든다(원래 그림은 같은 픽셀에 남는다).
/// 막대 윤곽 + 홈은 스프라이트(BarFrame)이고 안쪽(홈 + 채움 + 눈금)은 각 컴포넌트가 CPU로 그리는 RawImage다(RoguelikeBarPainter).
/// 그림자는 HUD 실루엣을 왼쪽 아래(-1, -1)에 한 장만 복제한다: 실루엣 바깥을 이루는 그래픽(윤곽 · 아이콘 · 숫자 · 진행 점 · T 표식 · 유예 줄 윤곽)에만 UnityEngine.UI.Shadow를 붙이고
/// 막대 안쪽 그래픽(채움)에는 붙이지 않는다. 반투명 1px 픽셀은 이 그림자뿐이다.
/// 스프라이트는 Assets/Graphics/Roguelike/hud/에 만든다(매번 같은 이름으로 덮어쓴다).
/// </summary>
public static class RoguelikeHUDBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeHUD.prefab";
    private const string HudDir = "Assets/Graphics/Roguelike/hud/";
    private const string KeyIconDatabasePath = "Assets/Prefabs/UI/Option/KeyIconDatabase.asset";
    private const string InventoryPrefabPath = "Assets/Prefabs/UI/Popup/UI_Inventory.prefab";
    private const string NumberSheetPath = "Assets/Graphics/Font/SmallCurrencyNum.png";
    private const int CanvasHeight = 360;

    // 보관함 트레이(우하단): 보유 카드 총량 숫자판 34x19 하나(겹친 카드 묶음 아이콘 11x11 + 합계 숫자) + 오른쪽 위 모서리에 걸친 인벤토리 키 아이콘(16x16).
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

    private readonly struct Box
    {
        public readonly int x;
        public readonly int y;
        public readonly int w;
        public readonly int h;

        public Box(int _x, int _y, int _w, int _h)
        {
            x = _x;
            y = _y;
            w = _w;
            h = _h;
        }
    }

    [MenuItem("LumberBoy/UI/Build Roguelike HUD")]
    public static void Build()
    {
        if (false == AssetDatabase.IsValidFolder("Assets/Graphics/Roguelike/hud"))
        {
            AssetDatabase.CreateFolder("Assets/Graphics/Roguelike", "hud");
        }

        Sprite[] digits = LoadDigitSprites();
        if (null == digits) return;

        // 아이콘 7종은 16x16 원본 파일이다(번개는 사용자가 직접 그린 그림). 읽기만 하고 다시 쓰지 않으며, 파일이 없으면 오류를 내고 멈춘다.
        Sprite treeIcon = LoadIcon("HudIconTree");
        Sprite axeIcon = LoadIcon("HudIconAxe");
        Sprite flagIcon = LoadIcon("HudIconFlag");
        Sprite flagGoldIcon = LoadIcon("HudIconFlagGold");
        Sprite flagWhiteIcon = LoadIcon("HudIconFlagWhite");
        Sprite boltIcon = LoadIcon("HudIconBolt");
        Sprite dashIcon = LoadIcon("HudIconDash");
        Sprite infinityIcon = LoadIcon("HudIconInfinity");
        if (null == treeIcon || null == axeIcon || null == flagIcon || null == flagGoldIcon || null == flagWhiteIcon || null == boltIcon || null == dashIcon || null == infinityIcon) return;

        // 막대 윤곽 + 홈, T 표식, 유예 줄 윤곽(파일이 없을 때만 만든다. 홀수 크기는 투명 1px이 붙은 짝수로 만들어진다).
        Sprite frame112x8 = MakeFresh("BarFrame112x8", FramePixels(112, 8), 112, 8);
        Sprite frame112x10 = MakeFresh("BarFrame112x10", FramePixels(112, 10), 112, 10);
        Sprite frame14x9 = MakeFresh("BarFrame14x9", FramePixels(14, 9), 14, 9);
        Sprite graceFrame = MakeFresh("GraceFrame110x3", GraceFramePixels(), 110, 3);
        Sprite tMarker = MakeFresh("TMarker", TMarkerPixels(), 5, 3);

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
            Box canvasBox = new Box(0, 0, 640, CanvasHeight);

            // ---- 웨이브 줄: 깃발 + 숫자(잉크 왼쪽 19, 위 298) + 진행 점(x는 숫자 폭에 맞춰 컴포넌트가 옮긴다)
            Box waveBox = new Box(7, 296, 140, 9);
            RectTransform waveRect = NewBox("WaveLabel", rootRect, canvasBox, waveBox);
            Image flag = NewSpriteImage("Flag", waveRect, waveBox, flagIcon, 4, 295, true);
            Image infinity = NewSpriteImage("Infinity", waveRect, waveBox, infinityIcon, 17, 295, true);
            infinity.enabled = false;
            UI_RoguelikeIconNumber waveNumber = NewGridNumber(waveRect, waveBox, 19, 298, digits);
            RawImage pips = NewRaw("Pips", waveRect, waveBox, 41, 298, 97, 5, true);

            UI_RoguelikeWaveLabel wave = waveRect.gameObject.AddComponent<UI_RoguelikeWaveLabel>();
            SerializedObject waveSo = new SerializedObject(wave);
            waveSo.FindProperty("labelRoot").objectReferenceValue = waveRect;
            waveSo.FindProperty("iconNumber").objectReferenceValue = waveNumber;
            waveSo.FindProperty("numberAnchor").objectReferenceValue = waveNumber.transform.parent;
            waveSo.FindProperty("flagImage").objectReferenceValue = flag;
            waveSo.FindProperty("infinityImage").objectReferenceValue = infinity;
            waveSo.FindProperty("normalFlagSprite").objectReferenceValue = flagIcon;
            waveSo.FindProperty("unlimitFlagSprite").objectReferenceValue = flagGoldIcon;
            waveSo.FindProperty("flashFlagSprite").objectReferenceValue = flagWhiteIcon;
            waveSo.FindProperty("pipSurface").objectReferenceValue = pips;
            waveSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 임계치 바(나무): T 표식은 막대 윤곽보다 먼저 그려 표식 그림자가 막대 윗줄을 덮지 않게 한다
            Box treeBox = new Box(7, 310, 160, 14);
            RectTransform treeRect = NewBox("PressureStrip", rootRect, canvasBox, treeBox);
            // T 표식 그림(5x3)은 위에 투명 1행이 붙은 6x4라 스프라이트 위 모서리가 한 줄 위(y 309)다.
            Image tick = NewSpriteImage("Tick", treeRect, treeBox, tMarker, 18, 309, true);
            NewSpriteImage("Background", treeRect, treeBox, frame112x8, 19, 313, true);
            RawImage treeFill = NewRaw("Fill", treeRect, treeBox, 20, 314, 110, 6, false);
            NewSpriteImage("TreeIcon", treeRect, treeBox, treeIcon, 4, 310, true);
            UI_RoguelikeIconNumber treeNumber = NewGridNumber(treeRect, treeBox, 135, 313, digits);

            UI_RoguelikePressureStrip strip = treeRect.gameObject.AddComponent<UI_RoguelikePressureStrip>();
            SerializedObject stripSo = new SerializedObject(strip);
            stripSo.FindProperty("fillSurface").objectReferenceValue = treeFill;
            stripSo.FindProperty("tick").objectReferenceValue = tick.rectTransform;
            stripSo.FindProperty("tickImage").objectReferenceValue = tick;
            stripSo.FindProperty("iconNumber").objectReferenceValue = treeNumber;
            stripSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 유예 타이머(숫자 없음): 임계치 바 아래 윤곽(y 320)을 공유하는 2px 줄
            Box graceBox = new Box(20, 321, 110, 3);
            RectTransform graceRect = NewBox("GraceTimer", rootRect, canvasBox, graceBox);
            // 유예 줄 윤곽 그림(110x3)은 위에 투명 1행이 붙은 110x4라 스프라이트 위 모서리가 한 줄 위(y 320)다.
            NewSpriteImage("Background", graceRect, graceBox, graceFrame, 20, 320, true);
            RawImage graceFill = NewRaw("Fill", graceRect, graceBox, 21, 321, 108, 2, false);

            UI_RoguelikeGraceTimer grace = graceRect.gameObject.AddComponent<UI_RoguelikeGraceTimer>();
            SerializedObject graceSo = new SerializedObject(grace);
            graceSo.FindProperty("fillSurface").objectReferenceValue = graceFill;
            graceSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 목표치 바(처치)
            Box killBox = new Box(7, 328, 160, 10);
            RectTransform killRect = NewBox("ThresholdBar", rootRect, canvasBox, killBox);
            NewSpriteImage("AxeIcon", killRect, killBox, axeIcon, 4, 326, true);
            NewSpriteImage("Background", killRect, killBox, frame112x8, 19, 329, true);
            RawImage killFill = NewRaw("Fill", killRect, killBox, 20, 330, 110, 6, false);
            UI_RoguelikeIconNumber killNumber = NewGridNumber(killRect, killBox, 135, 329, digits);

            UI_RoguelikeThresholdBar bar = killRect.gameObject.AddComponent<UI_RoguelikeThresholdBar>();
            SerializedObject barSo = new SerializedObject(bar);
            barSo.FindProperty("fillSurface").objectReferenceValue = killFill;
            barSo.FindProperty("iconNumber").objectReferenceValue = killNumber;
            barSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 스태미너 바(+ 본편 HUD가 전달하는 값을 받는다)
            Box staminaBox = new Box(7, 342, 124, 10);
            RectTransform staminaRect = NewBox("StaminaBar", rootRect, canvasBox, staminaBox);
            NewSpriteImage("BoltIcon", staminaRect, staminaBox, boltIcon, 4, 340, true);
            NewSpriteImage("Background", staminaRect, staminaBox, frame112x10, 19, 342, true);
            RawImage staminaFill = NewRaw("Fill", staminaRect, staminaBox, 20, 343, 110, 8, false);

            UI_RoguelikeStaminaBar staminaBar = staminaRect.gameObject.AddComponent<UI_RoguelikeStaminaBar>();
            SerializedObject staminaSo = new SerializedObject(staminaBar);
            staminaSo.FindProperty("fillSurface").objectReferenceValue = staminaFill;
            staminaSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 대쉬 칸: 해금되기 전에는 아이콘 · 칸 모두 숨는다(Visual)
            Box dashBox = new Box(131, 340, 44, 16);
            RectTransform dashRect = NewBox("DashCells", rootRect, canvasBox, dashBox);
            RectTransform dashVisual = NewBox("Visual", dashRect, dashBox, dashBox);
            NewSpriteImage("DashIcon", dashVisual, dashBox, dashIcon, 131, 340, true);

            GameObject[] cellObjects = new GameObject[UI_RoguelikeDashCells.MaxCells];
            RawImage[] cellFills = new RawImage[UI_RoguelikeDashCells.MaxCells];
            int[] cellX = { 146, 161 };

            for (int i = 0; i < UI_RoguelikeDashCells.MaxCells; i++)
            {
                // 칸 그림(14x9, y 343)은 위에 투명 1행이 붙은 14x10이라 스프라이트 위 모서리가 한 줄 위(y 342)다.
                Box cellBox = new Box(cellX[i], 342, 14, 10);
                Image cell = NewSpriteImage("Cell" + i, dashVisual, dashBox, frame14x9, cellX[i], 342, true);
                cellObjects[i] = cell.gameObject;
                cellFills[i] = NewRaw("Fill", cell.rectTransform, cellBox, cellX[i] + 1, 344, 12, 7, false);
            }

            dashVisual.gameObject.SetActive(false);

            UI_RoguelikeDashCells dashCells = dashRect.gameObject.AddComponent<UI_RoguelikeDashCells>();
            SerializedObject dashSo = new SerializedObject(dashCells);
            dashSo.FindProperty("visualRoot").objectReferenceValue = dashVisual.gameObject;
            SerializedProperty cellProperty = dashSo.FindProperty("cellObjects");
            SerializedProperty fillProperty = dashSo.FindProperty("fillSurfaces");
            cellProperty.arraySize = cellObjects.Length;
            fillProperty.arraySize = cellFills.Length;
            for (int i = 0; i < cellObjects.Length; i++)
            {
                cellProperty.GetArrayElementAtIndex(i).objectReferenceValue = cellObjects[i];
                fillProperty.GetArrayElementAtIndex(i).objectReferenceValue = cellFills[i];
            }

            dashSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 보관함 트레이(우하단): 보유 카드 총량 숫자판 하나. 표시 전용(클릭 없음) - 보유 카드 창은 키로 연다.
            RectTransform trayRect = RoguelikeCardBuilder.CreateRect("OwnedTray", rootRect, Vector2Int.zero, new Vector2(TrayWidth, TrayHeight));
            trayRect.anchorMin = new Vector2(1f, 0f);
            trayRect.anchorMax = new Vector2(1f, 0f);
            trayRect.pivot = new Vector2(1f, 0f);
            trayRect.anchoredPosition = new Vector2(-TrayMargin, TrayMargin);

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
            traySo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject viewSo = new SerializedObject(view);
            viewSo.FindProperty("thresholdBar").objectReferenceValue = bar;
            viewSo.FindProperty("waveLabel").objectReferenceValue = wave;
            viewSo.FindProperty("pressureStrip").objectReferenceValue = strip;
            viewSo.FindProperty("graceTimer").objectReferenceValue = grace;
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
    /// 캔버스 좌표(왼쪽 위 원점)로 놓는 상자. 앵커 · 피벗은 왼쪽 아래 (0, 0)이라 anchoredPosition = (x - 부모 x, 부모 아래 y - (y + h))이다.
    /// _child가 캔버스 좌표의 자식 상자이고 _parentBox가 부모 상자다.
    /// </summary>
    private static RectTransform NewBox(string _name, RectTransform _parent, Box _parentBox, Box _child)
    {
        return RoguelikeCardBuilder.CreateRect(_name, _parent, ToLocal(_parentBox, _child.x, _child.y, _child.h), new Vector2(_child.w, _child.h));
    }

    private static Vector2Int ToLocal(Box _parentBox, int _x, int _y, int _h)
    {
        return new Vector2Int(_x - _parentBox.x, _parentBox.y + _parentBox.h - (_y + _h));
    }

    /// <summary>스프라이트 하나(크기는 스프라이트 그대로). _bShadow면 그림자를 붙인다.</summary>
    private static Image NewSpriteImage(string _name, RectTransform _parent, Box _parentBox, Sprite _sprite, int _x, int _y, bool _bShadow)
    {
        Vector2 size = _sprite.rect.size;
        Image image = RoguelikeCardBuilder.CreateImage(_name, _parent, _sprite, ToLocal(_parentBox, _x, _y, (int)size.y), size);
        if (true == _bShadow) AddShadow(image.gameObject);
        return image;
    }

    /// <summary>컴포넌트가 텍스처를 그려 넣는 RawImage. 막대 안쪽(채움)에는 그림자를 붙이지 않는다.</summary>
    private static RawImage NewRaw(string _name, RectTransform _parent, Box _parentBox, int _x, int _y, int _w, int _h, bool _bShadow)
    {
        RectTransform rect = RoguelikeCardBuilder.CreateRect(_name, _parent, ToLocal(_parentBox, _x, _y, _h), new Vector2(_w, _h));
        RawImage raw = rect.gameObject.AddComponent<RawImage>();
        raw.raycastTarget = false;
        if (true == _bShadow) AddShadow(raw.gameObject);
        return raw;
    }

    /// <summary>HUD 실루엣을 왼쪽 아래(-1, -1)에 한 장 복제하는 그림자. 색은 #1A0E08 알파 120, 그래픽 알파(투명 픽셀)를 따라간다.</summary>
    private static void AddShadow(GameObject _target)
    {
        UnityEngine.UI.Shadow shadow = _target.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = RoguelikeHudColors.ShadowColor;
        shadow.effectDistance = new Vector2(-1f, -1f);
        shadow.useGraphicAlpha = true;
    }

    /// <summary>
    /// 작은 숫자 한 묶음(4px 격자, 왼쪽 정렬, 그림자). _inkLeft는 잉크 왼쪽 x, _inkTop은 잉크 위 y(캔버스 좌표, 잉크 높이 7).
    /// NumberAnchor(폭 0, 높이 8)가 위치를 잡고 그 자식 Number(RawImage)를 컴포넌트가 폭에 맞춰 정수 칸에 놓는다.
    /// </summary>
    private static UI_RoguelikeIconNumber NewGridNumber(RectTransform _parent, Box _parentBox, int _inkLeft, int _inkTop, Sprite[] _digits)
    {
        // 숫자 이미지는 높이 8(아래 7행이 잉크)이라 잉크 아래 y = _inkTop + 7이 이미지 아래 모서리다.
        Vector2Int local = ToLocal(_parentBox, _inkLeft, _inkTop, 7);
        UI_RoguelikeIconNumber number = NewNumber(_parent, local.x, local.y, _digits);

        SerializedObject so = new SerializedObject(number);
        so.FindProperty("bLeftAlign").boolValue = true;
        so.FindProperty("bGridSpacing").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        AddShadow(number.gameObject);
        return number;
    }

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
    }

    /// <summary>
    /// 트레이의 숫자판: 판 + 카드 아이콘(뒤 카드 · 앞 카드. 연출 없이 고정이다. 자체 색, 색 곱 없음) + 오른쪽 숫자(판 오른쪽 끝에 붙는 오른쪽 정렬이라 자릿수가 늘면 왼쪽으로 자란다).
    /// 아이콘 11x11 칸 안에서 뒤 카드는 오른쪽 위(왼쪽 4 · 위 0), 앞 카드는 왼쪽 아래(왼쪽 0 · 위 2)다. 아이콘 · 숫자는 판 세로 가운데.
    /// </summary>
    private static TrayParts NewTrayPlate(string _name, RectTransform _parent, Sprite[] _plates, Sprite _cardBack, Sprite _cardFront, Sprite[] _digits)
    {
        Image plate = RoguelikeCardBuilder.CreateImage(_name, _parent, _plates[0], Vector2Int.zero, _plates[0].rect.size);
        NewIcon("IconBack", plate.rectTransform, _cardBack, TrayIconX + TrayBackOffsetX, TrayIconY + TrayIconSize - TrayCardHeight, Color.white);
        NewIcon("IconFront", plate.rectTransform, _cardFront, TrayIconX, TrayIconY, Color.white);

        UI_RoguelikeIconNumber number = NewNumber(plate.rectTransform, TrayNumberRight, TrayContentY, _digits);

        SerializedObject so = new SerializedObject(number);
        so.FindProperty("bRightAlign").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        return new TrayParts { plate = plate, number = number };
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
    /// 위에서 아래 순서의 픽셀(_topDown)로 스프라이트를 만든다. 파일이 이미 있으면 읽기만 하고 다시 쓰지 않는다. 막대 · 표식 스프라이트가 쓴다.
    /// </summary>
    private static Sprite MakeFresh(string _name, Color32[] _topDown, int _w, int _h)
    {
        Color32[] bottomUp = new Color32[_topDown.Length];
        for (int y = 0; y < _h; y++)
        {
            for (int x = 0; x < _w; x++) bottomUp[(_h - 1 - y) * _w + x] = _topDown[y * _w + x];
        }

        return Make(_name, bottomUp, _w, _h);
    }

    /// <summary>
    /// HUD 아이콘(16x16 원본 파일)을 읽는다. 다시 쓰지 않고 임포트 설정도 건드리지 않는다(번개는 사용자가 직접 그린 그림이다). 파일이 없으면 오류를 로그에 남기고 null이다.
    /// </summary>
    private static Sprite LoadIcon(string _name)
    {
        string path = HudDir + _name + ".png";
        Sprite sprite = System.IO.File.Exists(path) ? RoguelikeCardBuilder.LoadSprite(path) : null;

        if (null == sprite) Debug.LogError("[RoguelikeHUDBuilder] HUD 아이콘 파일을 찾지 못했습니다(16x16 PNG를 넣어 주십시오): " + path);
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
    /// 막대 윤곽 + 홈(RoguelikeHUD_Polish_Plan.md 3장): 윤곽 위 · 아래 행은 x+1 ~ x+w-2, 좌 · 우 열은 y+1 ~ y+h-2이고 네 모서리는 투명이다.
    /// 홈은 안쪽 전체가 TR, 맨 위 1행이 TR_D.
    /// </summary>
    private static Color32[] FramePixels(int _w, int _h)
    {
        Color32[] pixels = new Color32[_w * _h];

        for (int x = 1; x < _w - 1; x++)
        {
            pixels[x] = RoguelikeHudColors.Outline;
            pixels[(_h - 1) * _w + x] = RoguelikeHudColors.Outline;
        }

        for (int y = 1; y < _h - 1; y++)
        {
            pixels[y * _w] = RoguelikeHudColors.Outline;
            pixels[y * _w + _w - 1] = RoguelikeHudColors.Outline;

            for (int x = 1; x < _w - 1; x++) pixels[y * _w + x] = 1 == y ? RoguelikeHudColors.GrooveTop : RoguelikeHudColors.Groove;
        }

        return pixels;
    }

    /// <summary>유예 줄 윤곽 110x3: 옆 윤곽(0열 · 109열의 위 2행), 아래 윤곽(1~108열), 안쪽 108x2는 TR_D. 아래 두 모서리는 투명이다(위는 임계치 바 아래 윤곽을 공유).</summary>
    private static Color32[] GraceFramePixels()
    {
        const int W = 110;
        const int H = 3;
        Color32[] pixels = new Color32[W * H];

        for (int y = 0; y < 2; y++)
        {
            pixels[y * W] = RoguelikeHudColors.Outline;
            pixels[y * W + W - 1] = RoguelikeHudColors.Outline;

            for (int x = 1; x < W - 1; x++) pixels[y * W + x] = RoguelikeHudColors.GrooveTop;
        }

        for (int x = 1; x < W - 1; x++) pixels[2 * W + x] = RoguelikeHudColors.Outline;

        return pixels;
    }

    /// <summary>T 표식 5x3(5-3-1 역삼각): 윗줄 5칸 윤곽, 가운데 줄은 윤곽 · 흰색 · 윤곽, 아랫줄은 가운데 한 칸 윤곽. 모두 불투명.</summary>
    private static Color32[] TMarkerPixels()
    {
        Color32[] pixels = new Color32[5 * 3];

        for (int x = 0; x < 5; x++) pixels[x] = RoguelikeHudColors.Outline;

        pixels[5 + 1] = RoguelikeHudColors.Outline;
        pixels[5 + 2] = RoguelikeHudColors.White;
        pixels[5 + 3] = RoguelikeHudColors.Outline;
        pixels[10 + 2] = RoguelikeHudColors.Outline;
        return pixels;
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
