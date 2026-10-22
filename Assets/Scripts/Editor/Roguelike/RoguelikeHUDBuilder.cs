#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD(UIView_RoguelikeHUD 프리팹)를 만든다. 여러 번 실행해도 같은 결과로 덮어쓴다.
/// 모든 막대는 스태미너 바(MainBar_Base / MainBar_Fill)의 미니 버전이다: 같은 캡슐 프레임 · 채움 광택 · 아래 그림자를 그대로 쓰되,
/// 가운데 줄을 덜어내(확대 · 축소 없이 행만 삭제) 높이를 줄이고 폭은 가운데를 덜어내 120으로 줄였다. 채움 색만 색조를 돌린다.
/// 글자 라벨 없이 아이콘 + 작은 숫자(버프 HUD와 같은 8px 글리프)만 쓴다. 유예 타이머에는 숫자가 없다. 웨이브는 배경 없이 깃발 + 숫자만 있다.
///
/// 배치(하단 중앙 기준, 스프라이트 아래 모서리 y. 스태미너 바 몸통 y 12~27, 오른쪽 대쉬 게이지 x 42~와 겹치지 않게 x -80~40, 폭 120):
///   ThresholdBar  목표치 바(처치) 120x14(몸통 12 + 그림자 2), 위치 (-20, 28), 분홍적색. 도끼 아이콘 + 현재/목표   UI_RoguelikeThresholdBar
///    ├ Background  미니 프레임   ├ Fill 분홍적색 채움(Image Filled)   ├ Head 젤리 머리(RawImage, CPU 픽셀)   └ AxeIcon + NumberAnchor/Number
///   GraceTimer    유예 타이머 120x4(숫자 없음), 위치 (-20, 44). 보라(유예 중, 차오름) / 장미주황(쿨다운, 줄어듦)   UI_RoguelikeGraceTimer
///    └ Background(프레임 4행) + Fill + Flash
///   PressureStrip 임계치 바 120x14, 위치 (-20, 48) - 유예 타이머 바로 위. 호박색 + T 눈금, 초과 진한 적색. 나무 아이콘 + 나무 수/T   UI_RoguelikePressureStrip
///    └ Background + OverFill(적색, 뒤) + BaseFill(호박색, 앞) + Tick + Flash(흰 실루엣) + TreeIcon + NumberAnchor/Number
///   WaveLabel     웨이브 68x14, 화면 상단 중앙(위에서 9px). 배경 없음. 깃발 + 현재/30, 언리밋은 금색 깃발 + 무한대 + 번호   UI_RoguelikeWaveLabel
/// 스프라이트는 Assets/Graphics/Roguelike/hud/에 만든다.
/// </summary>
public static class RoguelikeHUDBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeHUD.prefab";
    private const string BarDir = "Assets/Graphics/HUD/Bar/CharacterBar/";
    private const string HudDir = "Assets/Graphics/Roguelike/hud/";
    private const string NumberSheetPath = "Assets/Graphics/Font/SmallCurrencyNum.png";
    private const int SourceWidth = 160;
    private const int BarWidth = 120;
    private const int SpriteHeight = 14;
    private const int BodyBottom = 2;
    private const int GraceHeight = 4;
    private const int WaveWidth = 68;
    private const int WaveHeight = 14;
    private const int BarX = -20;
    private const int KillY = 28;
    private const int GraceY = 44;
    private const int TreeY = 48;
    private const int WaveTop = 9;
    private const int IconBottom = BodyBottom + 2;

    private static readonly Color32 Clear = new Color32(0, 0, 0, 0);
    private static readonly Color FrameDark = new Color(0.231f, 0.161f, 0.122f, 1f);

    // 스태미너 바 스프라이트(위에서 아래 행)에서 가져올 행. 몸통 16행 중 가운데 4행을 덜어 12행으로, 아래 그림자 2행은 그대로.
    private static readonly int[] MiniRows = { 0, 1, 2, 3, 4, 5, 10, 11, 12, 13, 14, 15, 16, 17 };
    // 유예 타이머: 위 윤곽, 안쪽 그림자, 안쪽 바탕, 아래 윤곽.
    private static readonly int[] GraceRows = { 0, 1, 14, 15 };

    private static readonly string[] AxeRows = { "..##...", ".###...", "####...", "..##...", "..#....", "..#....", "..#...." };
    private static readonly string[] TreeRows = { "...#...", "..###..", ".#####.", "..###..", ".#####.", "...#...", "...#..." };
    private static readonly string[] FlagRows = { "#......", "###....", "#####..", "####...", "#......", "#......", "#......" };
    private static readonly string[] InfinityRows = { ".##.##.", "#..#..#", "#..#..#", "#..#..#", ".##.##." };

    [MenuItem("LumberBoy/UI/Build Roguelike HUD")]
    public static void Build()
    {
        if (false == System.IO.File.Exists(BarDir + "MainBar_Base.png") || false == System.IO.File.Exists(BarDir + "MainBar_Fill.png"))
        {
            Debug.LogError("[RoguelikeHUDBuilder] MainBar_Base / MainBar_Fill 스프라이트를 찾지 못했습니다.");
            return;
        }

        if (false == AssetDatabase.IsValidFolder("Assets/Graphics/Roguelike/hud"))
        {
            AssetDatabase.CreateFolder("Assets/Graphics/Roguelike", "hud");
        }

        Sprite[] digits = LoadDigitSprites();
        if (null == digits) return;

        // 옛 스프라이트와 이번에 다시 만드는 스프라이트를 지운다(디자인이 바뀌어도 낡은 그림이 남지 않게).
        string[] stale = { "ThresholdBase", "ThresholdFill", "ThresholdStripBase", "ThresholdTree", "BarFrame12", "KillFill12", "GraceFrame", "BadgeFrame", "IconAxe", "IconTree", "IconFlag", "IconInfinity",
            "MiniFrame", "MiniFillPink", "MiniFillAmber", "MiniFillRed", "MiniFillWhite", "MiniGraceFrame", "MiniGraceFill" };
        for (int i = 0; i < stale.Length; i++) AssetDatabase.DeleteAsset(HudDir + stale[i] + ".png");

        Color32[] fullBase = ReadPixels(BarDir + "MainBar_Base.png");
        Color32[] fullFill = ReadPixels(BarDir + "MainBar_Fill.png");
        float baseHue = AverageHue(fullFill);

        Color32[] frameMini = CompressRows(CropWidth(fullBase), BarWidth, MiniRows);
        Color32[] fillMini = CompressRows(CropWidth(fullFill), BarWidth, MiniRows);
        Color32[] graceMini = CompressRows(CropWidth(fullBase), BarWidth, GraceRows);

        Color32[] pinkPixels = ShiftHue(fillMini, baseHue, 0.995f, 1.0f, 1.25f);
        Color32[] amberPixels = ShiftHue(fillMini, baseHue, 0.115f, 1.25f, 1.1f);
        Color32[] redPixels = ShiftHue(fillMini, baseHue, 0.0f, 1.25f, 0.85f);
        Color32[] whitePixels = Silhouette(fillMini);
        Color32[] graceFillPixels = GraceFillPixels(graceMini, BarWidth, GraceHeight);

        Sprite frame = Make("MiniFrame", frameMini, BarWidth, SpriteHeight);
        Sprite graceFrame = Make("MiniGraceFrame", graceMini, BarWidth, GraceHeight);
        Sprite pinkFill = Make("MiniFillPink", pinkPixels, BarWidth, SpriteHeight);
        Sprite amberFill = Make("MiniFillAmber", amberPixels, BarWidth, SpriteHeight);
        Sprite redFill = Make("MiniFillRed", redPixels, BarWidth, SpriteHeight);
        Sprite whiteFill = Make("MiniFillWhite", whitePixels, BarWidth, SpriteHeight);
        Sprite graceFillSprite = Make("MiniGraceFill", graceFillPixels, BarWidth, GraceHeight);
        Sprite axeIcon = Make("IconAxe", IconPixels(AxeRows, false), 7, 7);
        Sprite treeIcon = Make("IconTree", IconPixels(TreeRows, false), 7, 7);
        Sprite flagIcon = Make("IconFlag", IconPixels(FlagRows, true), 9, 9);
        Sprite infinityIcon = Make("IconInfinity", IconPixels(InfinityRows, true), 9, 7);

        // 젤리 머리는 컴포넌트가 런타임에 그린다. 채움의 행별 평균색과 윤곽(안쪽 여부), 바 프레임 색을 컴포넌트에 넘긴다.
        Color[] rowColors = RowAverageColors(pinkPixels, BarWidth, SpriteHeight);
        Color[] amberRowColors = RowAverageColors(amberPixels, BarWidth, SpriteHeight);
        Color[] redRowColors = RowAverageColors(redPixels, BarWidth, SpriteHeight);
        bool[] insideMask = new bool[pinkPixels.Length];
        for (int i = 0; i < pinkPixels.Length; i++) insideMask[i] = 127 < pinkPixels[i].a;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            UIView_RoguelikeHUD view = root.GetComponent<UIView_RoguelikeHUD>();
            if (null == view)
            {
                Debug.LogError("[RoguelikeHUDBuilder] UIView_RoguelikeHUD 컴포넌트를 찾지 못했습니다.");
                return;
            }

            string[] oldNames = { "ThresholdBar", "PressureStrip", "WaveLabel", "GraceTimer" };
            for (int i = 0; i < oldNames.Length; i++)
            {
                Transform existing = root.transform.Find(oldNames[i]);
                if (null != existing) Object.DestroyImmediate(existing.gameObject);
            }

            RectTransform rootRect = (RectTransform)root.transform;

            // ---- 목표치 바(처치)
            RectTransform killRect = NewBottomCenter("ThresholdBar", rootRect, BarX, KillY, BarWidth, SpriteHeight);
            Image killBackground = RoguelikeCardBuilder.CreateImage("Background", killRect, frame, Vector2Int.zero, frame.rect.size);
            Image fill = RoguelikeCardBuilder.CreateImage("Fill", killRect, pinkFill, Vector2Int.zero, pinkFill.rect.size);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            RectTransform headRect = RoguelikeCardBuilder.CreateRect("Head", killRect, Vector2Int.zero, new Vector2(BarWidth, SpriteHeight));
            RawImage head = headRect.gameObject.AddComponent<RawImage>();
            head.raycastTarget = false;
            head.enabled = false;
            NewIcon("AxeIcon", killRect, axeIcon, 4, IconBottom, new Color(0.42f, 0.1f, 0.18f, 1f));
            UI_RoguelikeIconNumber killNumber = NewNumber(killRect, 66, IconBottom, digits);

            UI_RoguelikeThresholdBar bar = killRect.gameObject.AddComponent<UI_RoguelikeThresholdBar>();
            SerializedObject barSo = new SerializedObject(bar);
            barSo.FindProperty("fillImage").objectReferenceValue = fill;
            barSo.FindProperty("headSurface").objectReferenceValue = head;
            barSo.FindProperty("borderColor").colorValue = FrameDark;
            barSo.FindProperty("iconNumber").objectReferenceValue = killNumber;
            SerializedProperty rowProperty = barSo.FindProperty("rowColors");
            rowProperty.arraySize = rowColors.Length;
            for (int i = 0; i < rowColors.Length; i++) rowProperty.GetArrayElementAtIndex(i).colorValue = rowColors[i];
            SerializedProperty insideProperty = barSo.FindProperty("insideMask");
            insideProperty.arraySize = insideMask.Length;
            for (int i = 0; i < insideMask.Length; i++) insideProperty.GetArrayElementAtIndex(i).boolValue = insideMask[i];
            barSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 유예 타이머(숫자 없음)
            RectTransform graceRect = NewBottomCenter("GraceTimer", rootRect, BarX, GraceY, BarWidth, GraceHeight);
            Image graceBackground = RoguelikeCardBuilder.CreateImage("Background", graceRect, graceFrame, Vector2Int.zero, graceFrame.rect.size);
            Image graceFillImage = NewFilled("Fill", graceRect, graceFillSprite);
            graceFillImage.enabled = false;
            Image graceFlash = RoguelikeCardBuilder.CreateImage("Flash", graceRect, graceFillSprite, Vector2Int.zero, graceFillSprite.rect.size);
            graceFlash.color = Color.white;
            graceFlash.enabled = false;

            UI_RoguelikeGraceTimer grace = graceRect.gameObject.AddComponent<UI_RoguelikeGraceTimer>();
            SerializedObject graceSo = new SerializedObject(grace);
            graceSo.FindProperty("timerRoot").objectReferenceValue = graceRect;
            graceSo.FindProperty("fillImage").objectReferenceValue = graceFillImage;
            graceSo.FindProperty("flashImage").objectReferenceValue = graceFlash;
            graceSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 임계치 바(나무): 적색 채움이 뒤, 호박색 채움이 앞
            RectTransform treeRect = NewBottomCenter("PressureStrip", rootRect, BarX, TreeY, BarWidth, SpriteHeight);
            Image treeBackground = RoguelikeCardBuilder.CreateImage("Background", treeRect, frame, Vector2Int.zero, frame.rect.size);
            Image overFill = NewFilled("OverFill", treeRect, redFill);
            Image baseFill = NewFilled("BaseFill", treeRect, amberFill);
            overFill.enabled = false;
            baseFill.enabled = false;
            RectTransform treeHeadRect = RoguelikeCardBuilder.CreateRect("Head", treeRect, Vector2Int.zero, new Vector2(BarWidth, SpriteHeight));
            RawImage treeHead = treeHeadRect.gameObject.AddComponent<RawImage>();
            treeHead.raycastTarget = false;
            treeHead.enabled = false;
            Image tick = NewSolid("Tick", treeRect, 1, 1, 1, SpriteHeight, new Color32(255, 241, 201, 255));
            Image treeFlash = RoguelikeCardBuilder.CreateImage("Flash", treeRect, whiteFill, Vector2Int.zero, whiteFill.rect.size);
            treeFlash.color = Color.white;
            treeFlash.enabled = false;
            NewIcon("TreeIcon", treeRect, treeIcon, 4, IconBottom, new Color(0.2f, 0.36f, 0.14f, 1f));
            UI_RoguelikeIconNumber treeNumber = NewNumber(treeRect, 66, IconBottom, digits);

            UI_RoguelikePressureStrip strip = treeRect.gameObject.AddComponent<UI_RoguelikePressureStrip>();
            SerializedObject stripSo = new SerializedObject(strip);
            stripSo.FindProperty("stripRoot").objectReferenceValue = treeRect;
            stripSo.FindProperty("baseFill").objectReferenceValue = baseFill;
            stripSo.FindProperty("overFill").objectReferenceValue = overFill;
            stripSo.FindProperty("tick").objectReferenceValue = tick.rectTransform;
            stripSo.FindProperty("flashImage").objectReferenceValue = treeFlash;
            stripSo.FindProperty("iconNumber").objectReferenceValue = treeNumber;
            stripSo.FindProperty("headSurface").objectReferenceValue = treeHead;
            stripSo.FindProperty("borderColor").colorValue = FrameDark;
            SerializedProperty amberProperty = stripSo.FindProperty("amberRowColors");
            amberProperty.arraySize = amberRowColors.Length;
            for (int i = 0; i < amberRowColors.Length; i++) amberProperty.GetArrayElementAtIndex(i).colorValue = amberRowColors[i];
            SerializedProperty redProperty = stripSo.FindProperty("redRowColors");
            redProperty.arraySize = redRowColors.Length;
            for (int i = 0; i < redRowColors.Length; i++) redProperty.GetArrayElementAtIndex(i).colorValue = redRowColors[i];
            SerializedProperty stripInsideProperty = stripSo.FindProperty("insideMask");
            stripInsideProperty.arraySize = insideMask.Length;
            for (int i = 0; i < insideMask.Length; i++) stripInsideProperty.GetArrayElementAtIndex(i).boolValue = insideMask[i];
            stripSo.ApplyModifiedPropertiesWithoutUndo();

            // ---- 웨이브(상단 중앙, 배경 없이 깃발 + 숫자)
            RectTransform waveRect = RoguelikeCardBuilder.CreateRect("WaveLabel", rootRect, Vector2Int.zero, new Vector2(WaveWidth, WaveHeight));
            waveRect.anchorMin = new Vector2(0.5f, 1f);
            waveRect.anchorMax = new Vector2(0.5f, 1f);
            waveRect.pivot = new Vector2(0.5f, 1f);
            waveRect.anchoredPosition = new Vector2(0f, -WaveTop);
            Image flag = NewIcon("Flag", waveRect, flagIcon, 15, 2, new Color(0.95f, 0.45f, 0.4f, 1f));
            Image infinity = NewIcon("Infinity", waveRect, infinityIcon, 25, 3, new Color(1f, 0.85f, 0.4f, 1f));
            infinity.enabled = false;
            UI_RoguelikeIconNumber waveNumber = NewNumber(waveRect, 42, 3, digits);
            // 웨이브는 배지 전체가 따로 튀는 연출이 있어 숫자의 변경 연출은 끈다(등장 · 퇴장만 쓴다).
            SerializedObject waveNumberSo = new SerializedObject(waveNumber);
            waveNumberSo.FindProperty("bPlayChange").boolValue = false;
            waveNumberSo.ApplyModifiedPropertiesWithoutUndo();

            UI_RoguelikeWaveLabel wave = waveRect.gameObject.AddComponent<UI_RoguelikeWaveLabel>();
            SerializedObject waveSo = new SerializedObject(wave);
            waveSo.FindProperty("labelRoot").objectReferenceValue = waveRect;
            waveSo.FindProperty("iconNumber").objectReferenceValue = waveNumber;
            waveSo.FindProperty("flagImage").objectReferenceValue = flag;
            waveSo.FindProperty("infinityImage").objectReferenceValue = infinity;
            waveSo.FindProperty("flagColor").colorValue = new Color(0.95f, 0.45f, 0.4f, 1f);
            waveSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject viewSo = new SerializedObject(view);
            viewSo.FindProperty("thresholdBar").objectReferenceValue = bar;
            viewSo.FindProperty("waveLabel").objectReferenceValue = wave;
            viewSo.FindProperty("pressureStrip").objectReferenceValue = strip;
            viewSo.FindProperty("graceTimer").objectReferenceValue = grace;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // 배경은 레이캐스트를 받지 않는다(HUD가 마우스를 가로채지 않게).
            killBackground.raycastTarget = false;
            graceBackground.raycastTarget = false;
            treeBackground.raycastTarget = false;
            fill.raycastTarget = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[RoguelikeHUDBuilder] 완료: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ---------------------------------------------------------------- 오브젝트

    private static RectTransform NewBottomCenter(string _name, RectTransform _parent, int _x, int _y, int _w, int _h)
    {
        RectTransform rect = RoguelikeCardBuilder.CreateRect(_name, _parent, Vector2Int.zero, new Vector2(_w, _h));
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(_x, _y);
        return rect;
    }

    private static Image NewSolid(string _name, RectTransform _parent, int _x, int _y, int _w, int _h, Color _color)
    {
        RectTransform rect = RoguelikeCardBuilder.CreateRect(_name, _parent, new Vector2Int(_x, _y), new Vector2(_w, _h));
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = _color;
        image.raycastTarget = false;
        return image;
    }

    private static Image NewFilled(string _name, RectTransform _parent, Sprite _sprite)
    {
        Image image = RoguelikeCardBuilder.CreateImage(_name, _parent, _sprite, Vector2Int.zero, _sprite.rect.size);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 0f;
        return image;
    }

    private static Image NewIcon(string _name, RectTransform _parent, Sprite _sprite, int _x, int _y, Color _color)
    {
        Image image = RoguelikeCardBuilder.CreateImage(_name, _parent, _sprite, new Vector2Int(_x, _y), _sprite.rect.size);
        image.color = _color;
        return image;
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

    private static Sprite Make(string _name, Color32[] _pixels, int _w, int _h)
    {
        return RoguelikeCardBuilder.EnsureSpriteFromPixels(HudDir + _name + ".png", _pixels, _w, _h);
    }

    /// <summary>PNG를 그대로 읽는다(아래에서 위 순서, 가져오기 설정과 무관한 원본 픽셀).</summary>
    private static Color32[] ReadPixels(string _path)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(System.IO.File.ReadAllBytes(_path));
        Color32[] pixels = texture.GetPixels32();
        Object.DestroyImmediate(texture);
        return pixels;
    }

    /// <summary>160폭 픽셀을 120폭으로 줄인다: 왼쪽 60열 + 오른쪽 60열(가운데 40열은 같은 무늬의 반복이라 덜어낸다. 확대 · 축소 없음).</summary>
    private static Color32[] CropWidth(Color32[] _full)
    {
        const int Height = 18;
        Color32[] result = new Color32[BarWidth * Height];
        int half = BarWidth / 2;
        int cut = SourceWidth - BarWidth;

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < BarWidth; x++)
            {
                int sx = x < half ? x : x + cut;
                result[y * BarWidth + x] = _full[y * SourceWidth + sx];
            }
        }

        return result;
    }

    /// <summary>원본(18행)에서 _rowsTopDown(위에서 아래 순서의 원본 행 번호)만 골라 새 스프라이트 픽셀을 만든다. 행만 덜어내므로 확대 · 축소가 없다.</summary>
    private static Color32[] CompressRows(Color32[] _source, int _w, int[] _rowsTopDown)
    {
        const int SourceHeight = 18;
        int n = _rowsTopDown.Length;
        Color32[] result = new Color32[_w * n];

        for (int i = 0; i < n; i++)
        {
            int srcY = SourceHeight - 1 - _rowsTopDown[i];
            int dstY = n - 1 - i;
            for (int x = 0; x < _w; x++) result[dstY * _w + x] = _source[srcY * _w + x];
        }

        return result;
    }

    /// <summary>채움의 색조를 돌려 다른 색 채움을 만든다. 명암은 그대로라 스태미너 바와 같은 질감이 된다. 투명 픽셀은 그대로 둔다.</summary>
    private static Color32[] ShiftHue(Color32[] _source, float _baseHue, float _targetHue, float _saturationMultiplier, float _valueMultiplier)
    {
        Color32[] result = new Color32[_source.Length];

        for (int i = 0; i < _source.Length; i++)
        {
            Color32 c = _source[i];
            if (0 == c.a)
            {
                result[i] = c;
                continue;
            }

            Color.RGBToHSV(c, out float h, out float s, out float v);
            float delta = Mathf.Repeat(h - _baseHue + 0.5f, 1f) - 0.5f;
            Color shifted = Color.HSVToRGB(Mathf.Repeat(_targetHue + delta, 1f), Mathf.Clamp01(s * _saturationMultiplier), Mathf.Clamp01(v * _valueMultiplier));
            result[i] = new Color32((byte)Mathf.RoundToInt(shifted.r * 255f), (byte)Mathf.RoundToInt(shifted.g * 255f), (byte)Mathf.RoundToInt(shifted.b * 255f), c.a);
        }

        return result;
    }

    /// <summary>채움 모양 그대로의 흰색 실루엣(피해 섬광용). Image 색은 스프라이트 색에 곱해지므로 색 있는 채움을 그대로 쓰면 흰색이 되지 않는다.</summary>
    private static Color32[] Silhouette(Color32[] _source)
    {
        Color32[] result = new Color32[_source.Length];
        for (int i = 0; i < _source.Length; i++) result[i] = 0 < _source[i].a ? new Color32(255, 255, 255, _source[i].a) : Clear;
        return result;
    }

    /// <summary>채움 평균 색조(스태미너 초록 채움의 기준 색조).</summary>
    private static float AverageHue(Color32[] _pixels)
    {
        float sumX = 0f;
        float sumY = 0f;
        int count = 0;

        for (int i = 0; i < _pixels.Length; i++)
        {
            if (_pixels[i].a < 255) continue;

            Color.RGBToHSV(_pixels[i], out float h, out float s, out float v);
            if (s < 0.2f) continue;

            sumX += Mathf.Cos(h * 6.2832f);
            sumY += Mathf.Sin(h * 6.2832f);
            count++;
        }

        if (0 == count) return 0.25f;

        return Mathf.Repeat(Mathf.Atan2(sumY, sumX) / 6.2832f, 1f);
    }

    /// <summary>유예 타이머 채움: 프레임 4행 중 안쪽 두 행에서 윤곽(어두운 칸)이 아닌 칸만 흰색으로. 프레임의 둥근 끝 안쪽에 딱 맞는다.</summary>
    private static Color32[] GraceFillPixels(Color32[] _frame, int _w, int _h)
    {
        Color32[] result = new Color32[_w * _h];
        Color32 dark = new Color32(59, 41, 31, 255);

        // 위에서 아래 행 1, 2가 안쪽(아래에서 위 인덱스로 _h - 2, _h - 3).
        for (int row = 1; row <= 2; row++)
        {
            int y = _h - 1 - row;
            for (int x = 0; x < _w; x++)
            {
                Color32 p = _frame[y * _w + x];
                bool bInside = 255 == p.a && (p.r != dark.r || p.g != dark.g || p.b != dark.b);
                if (true == bInside) result[y * _w + x] = new Color32(255, 255, 255, 255);
            }
        }

        return result;
    }

    /// <summary>글자 모양 문자열(위에서 아래) 하나를 흰색 아이콘으로. _bOutline이면 8방향 1px 검은 외곽선을 둘러 크기가 가로세로 2씩 커진다. 색은 Image 색으로 입힌다(외곽선은 검정 그대로).</summary>
    private static Color32[] IconPixels(string[] _rows, bool _bOutline)
    {
        int h = _rows.Length;
        int w = _rows[0].Length;
        int pad = true == _bOutline ? 1 : 0;
        int ow = w + pad * 2;
        int oh = h + pad * 2;
        Color32[] pixels = new Color32[ow * oh];
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 black = new Color32(0, 0, 0, 255);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if ('#' != _rows[y][x]) continue;
                pixels[(oh - 1 - (y + pad)) * ow + (x + pad)] = white;
            }
        }

        if (false == _bOutline) return pixels;

        Color32[] result = (Color32[])pixels.Clone();
        for (int y = 0; y < oh; y++)
        {
            for (int x = 0; x < ow; x++)
            {
                if (255 == pixels[y * ow + x].a) continue;

                bool bNear = false;
                for (int dy = -1; dy <= 1 && false == bNear; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx;
                        int yy = y + dy;
                        if (xx < 0 || ow <= xx || yy < 0 || oh <= yy) continue;
                        if (255 != pixels[yy * ow + xx].a) continue;

                        bNear = true;
                        break;
                    }
                }

                if (true == bNear) result[y * ow + x] = black;
            }
        }

        return result;
    }

    /// <summary>행(아래부터)별 평균색. 불투명 픽셀이 없는 행은 전체 평균색을 쓴다.</summary>
    private static Color[] RowAverageColors(Color32[] _pixels, int _w, int _h)
    {
        Color[] rows = new Color[_h];
        Color total = Color.black;
        int totalCount = 0;

        for (int y = 0; y < _h; y++)
        {
            Color sum = Color.black;
            int count = 0;

            for (int x = 0; x < _w; x++)
            {
                Color32 pixel = _pixels[y * _w + x];
                if (pixel.a < 127) continue;

                sum += (Color)pixel;
                count++;
            }

            if (0 < count) rows[y] = new Color(sum.r / count, sum.g / count, sum.b / count, 1f);
            else rows[y] = Color.clear;

            total += sum;
            totalCount += count;
        }

        Color average = 0 < totalCount ? new Color(total.r / totalCount, total.g / totalCount, total.b / totalCount, 1f) : Color.gray;
        for (int y = 0; y < _h; y++)
        {
            if (0f == rows[y].a) rows[y] = average;
        }

        return rows;
    }
}
#endif
