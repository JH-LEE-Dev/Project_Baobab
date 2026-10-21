#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD(UIView_RoguelikeHUD 프리팹)에 임계점 바를 만든다. 여러 번 실행해도 같은 결과로 덮어쓴다.
///
/// 계층(ThresholdBar = 하단 중앙, 스태미너 바(y 10~28) 바로 위. 오른쪽 대쉬 게이지(x 42~)와 겹치지 않게 x -80~40, 폭 120):
///   ThresholdBar 120x18 (anchor 하단 중앙, 위치 (-20, 30))  UI_RoguelikeThresholdBar
///    ├ Background  MainBar_Base  스태미너 바와 같은 바탕
///    ├ Ghost       줄어든 직후 직전 값을 옅은 색으로 잠깐 남기는 층(Image Filled, 채움 뒤). 평소에는 채움에 가려진다
///    ├ Fill        파스텔 적색 채움 한 가지(Image Filled - 정수 픽셀 폭). 상태별 색 구분·눈금은 없다
///    ├ Head        채움 앞쪽 끝의 젤리 머리(Image Sliced 8x18, 테두리 왼 1 / 오른 5 - 폭을 정수로 늘리고 줄여도 둥근 끝이 안 깨진다)
///    ├ Flash       피해 때 번갈아 하얗게 번쩍이는 층(평소 꺼짐, 채움 모양의 흰색 실루엣)
///    └ Value_0~4   "처치 수/목표 처치 수" 숫자(Galmuri11). 0~3 = 1px 짙은 갈색 외곽선(네 방향), 4 = 밝은 크림색 본문. 섬광 위에 그려진다
/// 채움 스프라이트는 MainBar_Fill(초록)의 색조만 돌려 만든다(밝기·명암은 그대로라 스태미너 바와 같은 질감): Assets/Graphics/Roguelike/hud/ThresholdFill.png.
/// 머리(ThresholdHead.png)는 그 채움의 오른쪽 끝 8열, 고스트(ThresholdGhost.png)는 같은 채움의 밝은 붉은 색조다.
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

        // 고스트: 같은 채움을 밝은 붉은색으로(줄어든 직후 직전 값이 옅게 남아 보인다).
        Sprite ghostSprite = RoguelikeCardBuilder.EnsureSpriteFromPixels(HudDir + "ThresholdGhost.png", ShiftHue(source, baseHue, PastelHue, PastelSaturation * 0.45f, 1.45f), BarWidth, BarHeight);

        // 젤리 머리: 채움 오른쪽 끝 8열(왼쪽 1열 = 몸통에 이어지는 열, 가운데 2열 = 늘어나는 몸통, 오른쪽 5열 = 둥근 끝). 9-slice 테두리로 폭을 정수 칸 단위로 바꾼다.
        const int headWidth = 8;
        Color32[] headPixels = new Color32[headWidth * BarHeight];
        for (int y = 0; y < BarHeight; y++)
        {
            for (int x = 0; x < headWidth; x++) headPixels[y * headWidth + x] = burgundyPixels[y * BarWidth + (BarWidth - headWidth + x)];
        }

        Sprite headSprite = RoguelikeCardBuilder.EnsureSpriteFromPixels(HudDir + "ThresholdHead.png", headPixels, headWidth, BarHeight);
        TextureImporter headImporter = (TextureImporter)AssetImporter.GetAtPath(HudDir + "ThresholdHead.png");
        if (new Vector4(1f, 0f, 5f, 0f) != headImporter.spriteBorder)
        {
            headImporter.spriteBorder = new Vector4(1f, 0f, 5f, 0f);
            headImporter.SaveAndReimport();
            headSprite = RoguelikeCardBuilder.LoadSprite(HudDir + "ThresholdHead.png");
        }

        // 피해 섬광용 흰색 실루엣(채움 모양 그대로, 모든 불투명 픽셀이 흰색). Image 색은 스프라이트 색에 곱해지므로 초록 채움을 그대로 쓰면 흰색이 되지 않는다.
        Color32[] flashPixels = new Color32[source.Length];
        for (int i = 0; i < source.Length; i++) flashPixels[i] = 0 < source[i].a ? new Color32(255, 255, 255, source[i].a) : new Color32(0, 0, 0, 0);
        Sprite flashSprite = RoguelikeCardBuilder.EnsureSpriteFromPixels(HudDir + "ThresholdFlash.png", flashPixels, BarWidth, BarHeight);

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

            Image ghost = RoguelikeCardBuilder.CreateImage("Ghost", barRect, ghostSprite, Vector2Int.zero, ghostSprite.rect.size);
            ghost.type = Image.Type.Filled;
            ghost.fillMethod = Image.FillMethod.Horizontal;
            ghost.fillOrigin = (int)Image.OriginHorizontal.Left;
            ghost.fillAmount = 0f;
            ghost.enabled = false;

            Image fill = RoguelikeCardBuilder.CreateImage("Fill", barRect, fillBurgundy, Vector2Int.zero, fillBurgundy.rect.size);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            Image head = RoguelikeCardBuilder.CreateImage("Head", barRect, headSprite, Vector2Int.zero, headSprite.rect.size);
            head.type = Image.Type.Sliced;
            head.enabled = false;

            Image flash = RoguelikeCardBuilder.CreateImage("Flash", barRect, flashSprite, Vector2Int.zero, flashSprite.rect.size);
            flash.color = Color.white;
            flash.enabled = false;

            // 숫자: 외곽선 네 장(±1px) + 본문 한 장. 바 전체를 덮는 사각형에 가운데 정렬(정수 픽셀 위치).
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Vector2Int[] offsets = { new Vector2Int(-1, 0), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(0, 1), Vector2Int.zero };
            Color outline = new Color(0.16f, 0.09f, 0.06f, 1f);
            Color cream = new Color(1f, 0.945f, 0.79f, 1f);
            TMP_Text[] valueTexts = new TMP_Text[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                valueTexts[i] = CreateValueText("Value_" + i, barRect, font, offsets[i], i == offsets.Length - 1 ? cream : outline);
            }

            UI_RoguelikeThresholdBar bar = barGo.AddComponent<UI_RoguelikeThresholdBar>();
            SerializedObject barSo = new SerializedObject(bar);
            barSo.FindProperty("barRoot").objectReferenceValue = barRect;
            barSo.FindProperty("ghostImage").objectReferenceValue = ghost;
            barSo.FindProperty("fillImage").objectReferenceValue = fill;
            barSo.FindProperty("headRect").objectReferenceValue = head.rectTransform;
            barSo.FindProperty("headImage").objectReferenceValue = head;
            barSo.FindProperty("flashImage").objectReferenceValue = flash;
            SerializedProperty textProperty = barSo.FindProperty("valueTexts");
            textProperty.arraySize = valueTexts.Length;
            for (int i = 0; i < valueTexts.Length; i++) textProperty.GetArrayElementAtIndex(i).objectReferenceValue = valueTexts[i];
            barSo.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject viewSo = new SerializedObject(view);
            viewSo.FindProperty("thresholdBar").objectReferenceValue = bar;
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            // 배경은 레이캐스트를 받지 않는다(HUD가 마우스를 가로채지 않게).
            background.raycastTarget = false;
            fill.raycastTarget = false;
            ghost.raycastTarget = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[RoguelikeHUDBuilder] 완료: " + PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>바 전체를 덮고 _offset만큼 어긋난 가운데 정렬 숫자 텍스트.</summary>
    private static TMP_Text CreateValueText(string _name, RectTransform _parent, TMP_FontAsset _font, Vector2Int _offset, Color _color)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(_offset.x, _offset.y);
        rect.offsetMax = new Vector2(_offset.x, _offset.y);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = _font.faceInfo.pointSize;
        text.text = "0/0";
        text.alignment = TextAlignmentOptions.Midline;
        text.color = _color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
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
