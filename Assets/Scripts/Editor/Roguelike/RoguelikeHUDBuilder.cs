#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD(UIView_RoguelikeHUD 프리팹)에 목표치 바 · 웨이브 표시 · 임계치 띠를 만든다. 여러 번 실행해도 같은 결과로 덮어쓴다.
///
/// 배치(하단 중앙 기준, 아래 모서리 y. 스태미너 바 y 10~28, 오른쪽 대쉬 게이지 x 42~와 겹치지 않게 x -80~40, 폭 120):
///   ThresholdBar   목표치 바(현재 처치 수 / 목표 처치 수) 120x18, 위치 (-20, 30)   UI_RoguelikeThresholdBar
///    ├ Background  MainBar_Base  스태미너 바와 같은 바탕
///    ├ Fill        파스텔 적색 채움 한 가지(Image Filled - 정수 픽셀 폭). 상태별 색 구분·눈금은 없다
///    ├ Head        채움 앞쪽 끝의 젤리 머리(RawImage 120x18. 스태미너 바처럼 컴포넌트가 CPU로 픽셀을 그려 넣는 오버레이)
///    └ Value_0~4   "처치 수/목표 처치 수" 숫자(Galmuri11). 0~3 = 1px 짙은 갈색 외곽선(네 방향), 4 = 밝은 크림색 본문
///   PressureStrip  임계치 띠 + 숫자 줄 120x18, 위치 (-20, 50) - 목표치 바 2px 위   UI_RoguelikePressureStrip
///    ├ Background  띠 프레임 120x4 (ThresholdStripBase.png)
///    ├ BaseFill    T까지의 파스텔 적색 채움(폭만 바뀌는 단색 사각형, 안쪽 118x2)
///    ├ OverFill    T를 넘은 구간의 진한 적색 채움(눈금 오른쪽에서 시작)
///    ├ Tick        T 위치의 크림색 눈금(1x6)
///    ├ Flash       피해 때 번갈아 하얗게 번쩍이는 층(평소 꺼짐)
///    └ NumberRow   띠 위 12px 줄: Icon(나무 5x5, ThresholdTree.png) + Value_0~4 "나무 수/T"(오른쪽 정렬)
///   WaveLabel      웨이브 표시 60x12, 위치 (-80, 55) - 목표치 바 왼쪽 끝 위. Value_0~4 "웨이브 12/30"(왼쪽 정렬)   UI_RoguelikeWaveLabel
/// 채움 스프라이트는 MainBar_Fill(초록)의 색조만 돌려 만든다(밝기·명암은 그대로라 스태미너 바와 같은 질감): Assets/Graphics/Roguelike/hud/ThresholdFill.png.
/// </summary>
public static class RoguelikeHUDBuilder
{
    private const string PrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeHUD.prefab";
    private const string BarDir = "Assets/Graphics/HUD/Bar/CharacterBar/";
    private const string HudDir = "Assets/Graphics/Roguelike/hud/";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Galmuri11_Optimum.asset";
    private const string BarName = "ThresholdBar";
    private const int SourceWidth = 160;
    private const int BarWidth = 120;
    private const int BarHeight = 18;
    private const float PastelHue = 0.995f;
    private const float PastelSaturation = 1.0f;
    private const float PastelValue = 1.25f;
    private const int BarX = -20;
    private const int BarY = 30;
    // Galmuri11 숫자는 줄 높이 기준 가운데 정렬이라 글리프가 시각적 중앙보다 아래에 앉는다. 숫자 사각형을 이만큼 올려 위아래 여백을 맞춘다.
    private const int ValueOffsetY = 2;
    private const int StripY = 50;
    private const int StripHeight = 4;
    private const int RowY = 55;
    private const int RowHeight = 12;
    private const int WaveLabelWidth = 60;

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

        Color32[] fullSource = ReadPixels(BarDir + "MainBar_Fill.png");
        float baseHue = AverageHue(fullSource);
        Color32[] source = CropWidth(fullSource);

        // 바탕도 같은 방식으로 폭 120으로 줄인다(왼쪽 끝 60열 + 오른쪽 끝 60열).
        Sprite baseSprite = RoguelikeCardBuilder.EnsureSpriteFromPixels(HudDir + "ThresholdBase.png", CropWidth(ReadPixels(BarDir + "MainBar_Base.png")), BarWidth, BarHeight);

        // 파스텔 적색: 밝은 산호빛 붉은색. 채도는 그대로 두고 명도를 올려 갈색 프레임·바탕과 대비시킨다.
        Color32[] burgundyPixels = ShiftHue(source, baseHue, PastelHue, PastelSaturation, PastelValue);
        Sprite fillBurgundy = RoguelikeCardBuilder.EnsureSpriteFromPixels(HudDir + "ThresholdFill.png", burgundyPixels, BarWidth, BarHeight);

        // 젤리 머리는 컴포넌트가 런타임에 그린다. 채움의 행별 평균색과 윤곽(안쪽 여부), 바 프레임 색을 컴포넌트에 넘긴다.
        Color[] rowColors = RowAverageColors(burgundyPixels);
        bool[] insideMask = new bool[burgundyPixels.Length];
        for (int i = 0; i < burgundyPixels.Length; i++) insideMask[i] = 127 < burgundyPixels[i].a;
        Color borderColor = DarkestOpaqueColor(ReadPixels(HudDir + "ThresholdBase.png"));

        // 임계치 띠 프레임(120x4: 1px 테두리, 모서리 투명, 안쪽 바탕색)과 나무 아이콘(5x5 흰색 - 색은 Image 색으로 입힌다).
        Color32[] basePixels = ReadPixels(HudDir + "ThresholdBase.png");
        Color32 stripInner = basePixels[(BarHeight / 2) * BarWidth + BarWidth / 2];
        Sprite stripSprite = RoguelikeCardBuilder.EnsureSpriteFromPixels(HudDir + "ThresholdStripBase.png", BuildStripPixels((Color32)borderColor, stripInner), BarWidth, StripHeight);
        Sprite treeSprite = RoguelikeCardBuilder.EnsureSpriteFromPixels(HudDir + "ThresholdTree.png", BuildTreePixels(), 5, 5);
        Color pastelColor = rowColors[BarHeight / 2];
        Color deepColor = new Color(0.79f, 0.227f, 0.282f, 1f);

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            UIView_RoguelikeHUD view = root.GetComponent<UIView_RoguelikeHUD>();
            if (null == view)
            {
                Debug.LogError("[RoguelikeHUDBuilder] UIView_RoguelikeHUD 컴포넌트를 찾지 못했습니다.");
                return;
            }

            Transform existing = root.transform.Find(BarName);
            if (null != existing) Object.DestroyImmediate(existing.gameObject);

            GameObject barGo = new GameObject(BarName, typeof(RectTransform));
            barGo.layer = LayerMask.NameToLayer("UI");
            RectTransform barRect = (RectTransform)barGo.transform;
            barRect.SetParent(root.transform, false);
            barRect.anchorMin = new Vector2(0.5f, 0f);
            barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(BarWidth, BarHeight);
            barRect.anchoredPosition = new Vector2(BarX, BarY);

            Image background = RoguelikeCardBuilder.CreateImage("Background", barRect, baseSprite, Vector2Int.zero, baseSprite.rect.size);

            Image fill = RoguelikeCardBuilder.CreateImage("Fill", barRect, fillBurgundy, Vector2Int.zero, fillBurgundy.rect.size);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            RectTransform headRect = RoguelikeCardBuilder.CreateRect("Head", barRect, Vector2Int.zero, new Vector2(BarWidth, BarHeight));
            RawImage head = headRect.gameObject.AddComponent<RawImage>();
            head.raycastTarget = false;
            head.enabled = false;

            // 숫자: 외곽선 네 장(±1px) + 본문 한 장. 바 전체를 덮는 사각형에 가운데 정렬(정수 픽셀 위치).
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Vector2Int[] offsets = { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1), Vector2Int.zero };
            Color outline = new Color(0.16f, 0.09f, 0.06f, 1f);
            Color cream = new Color(1f, 0.945f, 0.79f, 1f);
            TMP_Text[] valueTexts = new TMP_Text[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                valueTexts[i] = CreateValueText("Value_" + i, barRect, font, offsets[i], i == offsets.Length - 1 ? cream : outline, TextAlignmentOptions.Midline);
            }

            UI_RoguelikeThresholdBar bar = barGo.AddComponent<UI_RoguelikeThresholdBar>();
            SerializedObject barSo = new SerializedObject(bar);
            barSo.FindProperty("fillImage").objectReferenceValue = fill;
            barSo.FindProperty("headSurface").objectReferenceValue = head;
            barSo.FindProperty("borderColor").colorValue = borderColor;
            SerializedProperty rowProperty = barSo.FindProperty("rowColors");
            rowProperty.arraySize = rowColors.Length;
            for (int i = 0; i < rowColors.Length; i++) rowProperty.GetArrayElementAtIndex(i).colorValue = rowColors[i];
            SerializedProperty insideProperty = barSo.FindProperty("insideMask");
            insideProperty.arraySize = insideMask.Length;
            for (int i = 0; i < insideMask.Length; i++) insideProperty.GetArrayElementAtIndex(i).boolValue = insideMask[i];
            SerializedProperty textProperty = barSo.FindProperty("valueTexts");
            textProperty.arraySize = valueTexts.Length;
            for (int i = 0; i < valueTexts.Length; i++) textProperty.GetArrayElementAtIndex(i).objectReferenceValue = valueTexts[i];
            barSo.ApplyModifiedPropertiesWithoutUndo();

            UI_RoguelikeWaveLabel waveLabel = BuildWaveLabel(root, font, offsets, outline, cream);
            UI_RoguelikePressureStrip pressureStrip = BuildPressureStrip(root, stripSprite, treeSprite, pastelColor, deepColor, font, offsets, outline, cream);

            SerializedObject viewSo = new SerializedObject(view);
            viewSo.FindProperty("thresholdBar").objectReferenceValue = bar;
            viewSo.FindProperty("waveLabel").objectReferenceValue = waveLabel;
            viewSo.FindProperty("pressureStrip").objectReferenceValue = pressureStrip;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // 배경은 레이캐스트를 받지 않는다(HUD가 마우스를 가로채지 않게).
            background.raycastTarget = false;
            fill.raycastTarget = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[RoguelikeHUDBuilder] 완료: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>웨이브 표시(WaveLabel)를 만든다. 목표치 바 왼쪽 끝 위, 왼쪽 정렬.</summary>
    private static UI_RoguelikeWaveLabel BuildWaveLabel(GameObject _root, TMP_FontAsset _font, Vector2Int[] _offsets, Color _outline, Color _cream)
    {
        Transform existing = _root.transform.Find("WaveLabel");
        if (null != existing) Object.DestroyImmediate(existing.gameObject);

        RectTransform rect = RoguelikeCardBuilder.CreateRect("WaveLabel", (RectTransform)_root.transform, Vector2Int.zero, new Vector2(WaveLabelWidth, RowHeight));
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(BarX - BarWidth / 2, RowY);

        TMP_Text[] texts = new TMP_Text[_offsets.Length];
        for (int i = 0; i < _offsets.Length; i++)
        {
            texts[i] = CreateValueText("Value_" + i, rect, _font, _offsets[i], i == _offsets.Length - 1 ? _cream : _outline, TextAlignmentOptions.MidlineLeft);
            texts[i].text = string.Empty;
        }

        UI_RoguelikeWaveLabel label = rect.gameObject.AddComponent<UI_RoguelikeWaveLabel>();
        SerializedObject so = new SerializedObject(label);
        so.FindProperty("labelRoot").objectReferenceValue = rect;
        SerializedProperty textProperty = so.FindProperty("texts");
        textProperty.arraySize = texts.Length;
        for (int i = 0; i < texts.Length; i++) textProperty.GetArrayElementAtIndex(i).objectReferenceValue = texts[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        return label;
    }

    /// <summary>임계치 띠 + 숫자 줄(PressureStrip)을 만든다. 목표치 바 2px 위.</summary>
    private static UI_RoguelikePressureStrip BuildPressureStrip(GameObject _root, Sprite _stripSprite, Sprite _treeSprite, Color _pastel, Color _deep,
        TMP_FontAsset _font, Vector2Int[] _offsets, Color _outline, Color _cream)
    {
        Transform existing = _root.transform.Find("PressureStrip");
        if (null != existing) Object.DestroyImmediate(existing.gameObject);

        RectTransform rect = RoguelikeCardBuilder.CreateRect("PressureStrip", (RectTransform)_root.transform, Vector2Int.zero, new Vector2(BarWidth, BarHeight));
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(BarX, StripY);

        Image background = RoguelikeCardBuilder.CreateImage("Background", rect, _stripSprite, Vector2Int.zero, _stripSprite.rect.size);
        RectTransform baseFill = CreateSolid("BaseFill", rect, new Vector2Int(1, 1), new Vector2(0f, StripHeight - 2), _pastel);
        RectTransform overFill = CreateSolid("OverFill", rect, new Vector2Int(1, 1), new Vector2(0f, StripHeight - 2), _deep);
        RectTransform tick = CreateSolid("Tick", rect, new Vector2Int(1, -1), new Vector2(1f, StripHeight + 2), _cream);
        Image flash = RoguelikeCardBuilder.CreateRect("Flash", rect, new Vector2Int(1, 1), new Vector2(BarWidth - 2, StripHeight - 2)).gameObject.AddComponent<Image>();
        flash.color = Color.white;
        flash.raycastTarget = false;
        flash.enabled = false;

        RectTransform row = RoguelikeCardBuilder.CreateRect("NumberRow", rect, new Vector2Int(0, StripHeight + 1), new Vector2(BarWidth, RowHeight));
        RectTransform iconRect = RoguelikeCardBuilder.CreateRect("Icon", row, new Vector2Int(0, 4), _treeSprite.rect.size);
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = _treeSprite;
        icon.color = _cream;
        icon.raycastTarget = false;

        TMP_Text[] texts = new TMP_Text[_offsets.Length];
        for (int i = 0; i < _offsets.Length; i++)
        {
            texts[i] = CreateValueText("Value_" + i, row, _font, _offsets[i], i == _offsets.Length - 1 ? _cream : _outline, TextAlignmentOptions.MidlineRight);
            texts[i].text = "0/0";
        }

        background.raycastTarget = false;

        UI_RoguelikePressureStrip strip = rect.gameObject.AddComponent<UI_RoguelikePressureStrip>();
        SerializedObject so = new SerializedObject(strip);
        so.FindProperty("stripRoot").objectReferenceValue = rect;
        so.FindProperty("baseFill").objectReferenceValue = baseFill;
        so.FindProperty("overFill").objectReferenceValue = overFill;
        so.FindProperty("tick").objectReferenceValue = tick;
        so.FindProperty("flashImage").objectReferenceValue = flash;
        so.FindProperty("iconRect").objectReferenceValue = iconRect;
        so.FindProperty("iconImage").objectReferenceValue = icon;
        SerializedProperty textProperty = so.FindProperty("valueTexts");
        textProperty.arraySize = texts.Length;
        for (int i = 0; i < texts.Length; i++) textProperty.GetArrayElementAtIndex(i).objectReferenceValue = texts[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        baseFill.gameObject.SetActive(false);
        overFill.gameObject.SetActive(false);
        return strip;
    }

    /// <summary>스프라이트 없는 단색 사각형(폭만 바뀌는 채움·눈금).</summary>
    private static RectTransform CreateSolid(string _name, RectTransform _parent, Vector2Int _position, Vector2 _size, Color _color)
    {
        RectTransform rect = RoguelikeCardBuilder.CreateRect(_name, _parent, _position, _size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = _color;
        image.raycastTarget = false;
        return rect;
    }

    /// <summary>띠 프레임 픽셀(아래 행부터): 1px 테두리, 네 모서리 투명, 안쪽은 바탕색.</summary>
    private static Color32[] BuildStripPixels(Color32 _border, Color32 _inner)
    {
        Color32[] pixels = new Color32[BarWidth * StripHeight];

        for (int y = 0; y < StripHeight; y++)
        {
            for (int x = 0; x < BarWidth; x++)
            {
                bool bEdgeX = 0 == x || BarWidth - 1 == x;
                bool bEdgeY = 0 == y || StripHeight - 1 == y;
                if (true == bEdgeX && true == bEdgeY) pixels[y * BarWidth + x] = new Color32(0, 0, 0, 0);
                else pixels[y * BarWidth + x] = true == bEdgeX || true == bEdgeY ? _border : _inner;
            }
        }

        return pixels;
    }

    /// <summary>나무 아이콘 5x5(아래 행부터): 위 세 줄은 둥근 수관, 아래 두 줄은 줄기. 흰색이라 Image 색으로 입힌다.</summary>
    private static Color32[] BuildTreePixels()
    {
        string[] rows = { "..#..", "..#..", "#####", "#####", ".###." };
        Color32[] pixels = new Color32[25];

        for (int y = 0; y < 5; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                pixels[y * 5 + x] = '#' == rows[y][x] ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
        }

        return pixels;
    }

    /// <summary>_rect를 덮고 _offset만큼 어긋난 숫자 텍스트(외곽선 복사본은 _offset이 있고, 본문은 (0, 0)).</summary>
    private static TMP_Text CreateValueText(string _name, RectTransform _parent, TMP_FontAsset _font, Vector2Int _offset, Color _color, TextAlignmentOptions _alignment)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(_offset.x, _offset.y + ValueOffsetY);
        rect.offsetMax = new Vector2(_offset.x, _offset.y + ValueOffsetY);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = _font.faceInfo.pointSize;
        text.text = "0/0";
        text.alignment = _alignment;
        text.color = _color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>행(아래부터)별 평균색. 불투명 픽셀이 없는 행은 전체 평균색을 쓴다.</summary>
    private static Color[] RowAverageColors(Color32[] _pixels)
    {
        Color[] rows = new Color[BarHeight];
        Color total = Color.black;
        int totalCount = 0;

        for (int y = 0; y < BarHeight; y++)
        {
            Color sum = Color.black;
            int count = 0;

            for (int x = 0; x < BarWidth; x++)
            {
                Color32 pixel = _pixels[y * BarWidth + x];
                if (pixel.a < 127) continue;

                sum += (Color)pixel;
                count++;
            }

            if (0 < count) rows[y] = new Color(sum.r / count, sum.g / count, sum.b / count, 1f);
            total += sum;
            totalCount += count;
            if (0 == count) rows[y] = Color.clear;
        }

        Color average = 0 < totalCount ? new Color(total.r / totalCount, total.g / totalCount, total.b / totalCount, 1f) : Color.gray;
        for (int y = 0; y < BarHeight; y++)
        {
            if (0f == rows[y].a) rows[y] = average;
        }

        return rows;
    }

    /// <summary>불투명 픽셀 중 가장 어두운 색(바 프레임 색).</summary>
    private static Color DarkestOpaqueColor(Color32[] _pixels)
    {
        Color darkest = Color.black;
        float best = float.MaxValue;

        for (int i = 0; i < _pixels.Length; i++)
        {
            if (_pixels[i].a < 255) continue;

            Color color = _pixels[i];
            float brightness = color.r + color.g + color.b;
            if (best <= brightness) continue;

            best = brightness;
            darkest = color;
        }

        darkest.a = 1f;
        return darkest;
    }

    /// <summary>160폭 픽셀을 120폭으로 줄인다: 왼쪽 60열 + 오른쪽 60열(가운데 40열은 같은 무늬의 반복이라 버린다).</summary>
    private static Color32[] CropWidth(Color32[] _full)
    {
        Color32[] result = new Color32[BarWidth * BarHeight];
        int half = BarWidth / 2;
        int cut = SourceWidth - BarWidth;

        for (int y = 0; y < BarHeight; y++)
        {
            for (int x = 0; x < BarWidth; x++)
            {
                int sx = x < half ? x : x + cut;
                result[y * BarWidth + x] = _full[y * SourceWidth + sx];
            }
        }

        return result;
    }

    /// <summary>PNG를 그대로 읽는다(아래에서 위 순서, 가져오기 설정과 무관한 원본 픽셀).</summary>
    private static Color32[] ReadPixels(string _path)
    {
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(System.IO.File.ReadAllBytes(_path));
        Color32[] pixels = texture.GetPixels32();
        UnityEngine.Object.DestroyImmediate(texture);
        return pixels;
    }

    /// <summary>채도가 있는 불투명 픽셀의 평균 색조(초록 채움의 기준 색조).</summary>
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

    /// <summary>채움의 색조를 돌려 상태 색을 만든다. 명암(밝기 차이)은 그대로라 같은 질감의 다른 색이 된다. 투명·회색 픽셀은 그대로 둔다.</summary>
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
}
#endif
