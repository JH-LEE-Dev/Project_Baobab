#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using PresentationLayer.VFX;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 도전과제 창(시안 B 리본, Docs/Roguelike/_Work/ChallengeUI_B_Ribbon_Plan.md) 18개 언어 검수 · 리본 확대 이미지.
///   LumberBoy/UI/Validate Roguelike Challenges   18개 언어 x (도전과제 제목 20 · 팁 9종 · 머리줄 문구)를 그 언어의 실제 TMP 폰트 에셋으로 재어
///       inkMax(기준선 위 + 아래 잉크 높이) · ribbonH(= 1 + 2 + inkMax + 2 + 1) · 최대 제목 폭 · 리본 끝 x(넘침) · 팁 줄 수 · 수치 아래 줄 칸 수 · 머리줄 잉크 · 아틀라스에 없는 글자 수를 콘솔 표로 출력한다.
///       리본 높이 15 · 17 · 19 리본을 실제 UI_ChallengeRibbon으로 그려 8배 확대 PNG로 Docs/Roguelike/_Work/ChallengeUI_Mockup/에 저장한다.
/// 폰트 크기 · 잉크 측정은 런타임(UIView_RoguelikeChallenges.RefreshInkMetrics)과 같은 함수(UI_RoguelikeChallengeRow.MeasureInk)를 쓴다.
/// RenderCanvas는 에디터 모드에서 UI를 640x360 같은 RenderTexture로 그려 Texture2D로 돌려준다(캔버스 referencePixelsPerUnit 32, ConstantPixelSize 배율 1).
/// </summary>
public static class RoguelikeChallengeListValidator
{
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Galmuri11_Optimum.asset";
    private const string FontTablePath = "Assets/Scriptable Obj/Localization/LocalizationFontTable.asset";
    private const string JsonPath = "Assets/Resources/Localization/RoguelikeUI.json";
    private const string OutputDir = "Docs/Roguelike/_Work/ChallengeUI_Mockup/";

    // 칸 · 리본 규격(UI_RoguelikeChallengeRow와 같은 값)
    private const int RowWidth = 340;
    private const int RibbonFoldX = 5;
    private const int RibbonTextPadding = 7;
    private const int RibbonLimitX = 332;
    private const int TitleX = 16;
    private const int ProgressRight = 8;
    private const int ProgressMinGap = 8;
    private const int TipWidth = 300;
    private const int TipLineRecommended = 3;
    private const int HeaderTextAreaHeight = 18;
    private const int HeaderWidth = 340;
    private const int InkPadding = 3;

    private const string ClearedLabelKey = "Roguelike_Challenges_ClearedLabel";

    private static readonly string[] PrismCardIds =
    {
        "combo_spin_shockwave", "combo_spin_boomerang", "combo_spore_drone", "combo_spore_offroad", "combo_spore_shockwave", "combo_stella_drone",
    };

    private static readonly string[] TipKeys =
    {
        "Roguelike_Challenge_PrismAll_Tip", "Roguelike_Challenge_PrismCard_Tip", "Roguelike_Challenge_CardMaxAll_Tip", "Roguelike_Challenge_RubyAll_Tip",
        "Roguelike_Challenge_TreeN_Tip", "Roguelike_Challenge_UnlimitN_Tip", "Roguelike_Challenge_TierAll_Tip", "Roguelike_Challenge_Wave30_Tip",
        "Roguelike_Challenge_CollectionAll_Tip",
    };

    // 도전과제 20종(RoguelikeChallenges.definitions와 같은 구성): 제목 키, {0} 인수(숫자 또는 카드 이름 키), 목표값(미달성 진행 수치 "n / 목표", 1 이하면 수치 없음)
    private struct Spec
    {
        public string titleKey;
        public string numberArg;
        public string cardId;
        public int goal;
    }

    private static readonly Spec[] Specs = BuildSpecs();

    private static Spec[] BuildSpecs()
    {
        List<Spec> list = new List<Spec>(20);
        list.Add(new Spec { titleKey = "Roguelike_Challenge_PrismAll_Title", goal = 6 });
        for (int i = 0; PrismCardIds.Length > i; i++) list.Add(new Spec { titleKey = "Roguelike_Challenge_PrismCard_Title", cardId = PrismCardIds[i], goal = 0 });
        list.Add(new Spec { titleKey = "Roguelike_Challenge_CardMaxAll_Title", goal = 17 });
        list.Add(new Spec { titleKey = "Roguelike_Challenge_RubyAll_Title", goal = 3 });
        list.Add(new Spec { titleKey = "Roguelike_Challenge_CollectionAll_Title", goal = 0 });
        int[] trees = { 1000, 5000, 10000 };
        for (int i = 0; trees.Length > i; i++) list.Add(new Spec { titleKey = "Roguelike_Challenge_TreeN_Title", numberArg = trees[i].ToString("N0", CultureInfo.InvariantCulture), goal = trees[i] });
        int[] unlimits = { 5, 10, 15, 20, 25 };
        for (int i = 0; unlimits.Length > i; i++) list.Add(new Spec { titleKey = "Roguelike_Challenge_UnlimitN_Title", numberArg = unlimits[i].ToString(CultureInfo.InvariantCulture), goal = unlimits[i] });
        list.Add(new Spec { titleKey = "Roguelike_Challenge_TierAll_Title", goal = 4 });
        list.Add(new Spec { titleKey = "Roguelike_Challenge_Wave30_Title", goal = 0 });
        return list.ToArray();
    }

    [MenuItem("LumberBoy/UI/Validate Roguelike Challenges")]
    public static void Validate()
    {
        TMP_FontAsset baseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        LocalizationFontTable table = AssetDatabase.LoadAssetAtPath<LocalizationFontTable>(FontTablePath);
        if (null == baseFont || null == table)
        {
            Debug.LogError("[RoguelikeChallengeListValidator] 폰트 또는 폰트 표를 찾지 못했습니다: " + FontPath + " / " + FontTablePath);
            return;
        }

        Dictionary<string, LocalizationEntry> entries = LoadEntries();
        if (null == entries) return;

        float fontSize = baseFont.faceInfo.pointSize;
        GameObject canvasObject = new GameObject("ChallengeValidateCanvas", typeof(Canvas));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        GameObject textObject = new GameObject("ChallengeValidateText", typeof(RectTransform));
        textObject.hideFlags = HideFlags.HideAndDontSave;
        textObject.transform.SetParent(canvasObject.transform, false);
        TextMeshProUGUI measure = textObject.AddComponent<TextMeshProUGUI>();
        measure.fontSize = fontSize;
        measure.overflowMode = TextOverflowModes.Overflow;
        measure.richText = true;
        measure.enableAutoSizing = false;

        StringBuilder summary = new StringBuilder(8192);
        StringBuilder detail = new StringBuilder(4096);
        int overflowLanguages = 0;
        int missingLanguages = 0;
        summary.AppendLine("[도전과제 창 검수] 폰트 크기 " + fontSize + ", 리본 끝 한도 x " + RibbonLimitX + ", 팁 폭 " + TipWidth + " (최대 " + TipLineRecommended + "줄 권장), 머리줄 글자 영역 " + HeaderTextAreaHeight + "px");
        summary.AppendLine("언어 | 폰트 | 위/아래 | inkMax | ribbonH | 칸 최소높이 | 최대 제목 폭(도전과제) | 리본 끝 x | 리본 | 팁 최대 줄 | 팁 4줄+ | 수치 아래줄 칸 | 머리줄 잉크/폭 | 개별 최대 잉크 | 없는 글자");
        detail.AppendLine("[도전과제 창 검수 - 제목 폭 20종] 언어 | " + "PrismAll | PrismCard x7 | CardMaxAll | RubyAll | Tree x3 | Unlimit x5 | TierAll | Wave30");

        Array languages = Enum.GetValues(typeof(Language));
        for (int li = 0; languages.Length > li; li++)
        {
            Language language = (Language)languages.GetValue(li);
            TMP_FontAsset font = table.GetFont(language);
            if (null == font) font = baseFont;
            measure.font = font;

            string label = Resolve(entries, ClearedLabelKey, language);
            string countSample = "20 / 20";
            string percentSample = "100%";

            float top = float.MinValue;
            float bottom = float.MaxValue;
            float singleInkMax = 0f;
            float titleMaxWidth = 0f;
            string titleMaxName = string.Empty;
            int missing = 0;
            int belowCount = 0;

            string[] titles = new string[Specs.Length];
            detail.Append(language).Append(" |");
            for (int i = 0; Specs.Length > i; i++)
            {
                Spec spec = Specs[i];
                string title = Resolve(entries, spec.titleKey, language);
                if (null != spec.numberArg) title = title.Replace("{0}", spec.numberArg);
                else if (null != spec.cardId) title = title.Replace("{0}", Resolve(entries, "Roguelike_Card_" + spec.cardId, language));
                titles[i] = title;

                float width = MeasureWidth(measure, title);
                detail.Append(' ').Append(Mathf.CeilToInt(width)).Append(" |");
                if (width > titleMaxWidth)
                {
                    titleMaxWidth = width;
                    titleMaxName = spec.titleKey.Replace("Roguelike_Challenge_", string.Empty).Replace("_Title", string.Empty) + (null != spec.numberArg ? spec.numberArg : (null != spec.cardId ? spec.cardId : string.Empty));
                }

                float single = InkHeight(font, fontSize, title);
                singleInkMax = Mathf.Max(singleInkMax, single);
                UI_RoguelikeChallengeRow.MeasureInk(font, fontSize, title, ref top, ref bottom);
                missing += CountMissing(font, title);
            }
            detail.AppendLine();

            // 머리줄 문구(라벨 · 수치 · 백분율): 리본 높이 계산에도 합치고, 게이지 위 18px 안에 들어가는지 따로 본다.
            float headerTop = float.MinValue;
            float headerBottom = float.MaxValue;
            UI_RoguelikeChallengeRow.MeasureInk(font, fontSize, label, ref headerTop, ref headerBottom);
            UI_RoguelikeChallengeRow.MeasureInk(font, fontSize, countSample, ref headerTop, ref headerBottom);
            UI_RoguelikeChallengeRow.MeasureInk(font, fontSize, percentSample, ref headerTop, ref headerBottom);
            UI_RoguelikeChallengeRow.MeasureInk(font, fontSize, label, ref top, ref bottom);
            UI_RoguelikeChallengeRow.MeasureInk(font, fontSize, countSample, ref top, ref bottom);
            UI_RoguelikeChallengeRow.MeasureInk(font, fontSize, percentSample, ref top, ref bottom);
            missing += CountMissing(font, label);

            int above = Mathf.Max(0, Mathf.CeilToInt(top));
            int below = Mathf.Max(0, Mathf.CeilToInt(-bottom));
            int inkMax = above + below;
            int ribbonH = InkPadding * 2 + inkMax;
            int tail = UI_ChallengeRibbon.TailLength(ribbonH);
            int ribbonEnd = RibbonFoldX + UI_ChallengeRibbon.FoldWidth + Mathf.CeilToInt(titleMaxWidth) + RibbonTextPadding * 2 + tail;
            bool bRibbonOver = RibbonLimitX < ribbonEnd;

            int headerInk = headerTop < headerBottom ? 0 : Mathf.CeilToInt(headerTop) + Mathf.Max(0, Mathf.CeilToInt(-headerBottom));
            float labelWidth = MeasureWidth(measure, label);
            float countWidth = MeasureWidth(measure, countSample);
            float percentWidth = MeasureWidth(measure, percentSample);
            bool bHeaderOver = HeaderTextAreaHeight < headerInk || 2f + labelWidth + 6f + countWidth + 6f > HeaderWidth - 2f - percentWidth;

            // 팁: 폭 300에서 줄바꿈한 줄 수와 높이
            int tipLinesMax = 0;
            float tipHeightMax = 0f;
            int tipOver = 0;
            for (int i = 0; TipKeys.Length > i; i++)
            {
                string tip = Resolve(entries, TipKeys[i], language);
                MeasureWrapped(measure, tip, out int lines, out float height);
                tipLinesMax = Mathf.Max(tipLinesMax, lines);
                tipHeightMax = Mathf.Max(tipHeightMax, height);
                if (TipLineRecommended < lines) tipOver++;
                missing += CountMissing(font, tip);
            }

            // 미달성 칸 제목 + 수치("n / 목표", 진행값은 목표 - 1로 가장 넓은 쪽 근사) 간격
            for (int i = 0; Specs.Length > i; i++)
            {
                if (1 >= Specs[i].goal) continue;

                string progress = Mathf.Max(0, Specs[i].goal - 1).ToString("N0", CultureInfo.InvariantCulture) + " / " + Specs[i].goal.ToString("N0", CultureInfo.InvariantCulture);
                float progressWidth = Mathf.Ceil(MeasureWidth(measure, progress));
                if (RowWidth - ProgressRight - progressWidth - (TitleX + Mathf.Ceil(MeasureWidth(measure, titles[i]))) < ProgressMinGap) belowCount++;
            }

            float lineHeight = 0f < font.faceInfo.pointSize ? (font.faceInfo.ascentLine - font.faceInfo.descentLine) * fontSize / font.faceInfo.pointSize * font.faceInfo.scale : 0f;
            int rowMin = Mathf.Max(42, 6 + ribbonH + 4 + Mathf.CeilToInt(tipHeightMax) + 7);

            string verdict = "OK";
            if (true == bRibbonOver) verdict = "넘침";
            else if (true == bHeaderOver) verdict = "머리줄주의";
            if (0 < tipOver) verdict += " / 팁4줄+";
            if (true == bRibbonOver || 0 < tipOver) overflowLanguages++;
            if (0 < missing)
            {
                missingLanguages++;
                Debug.LogError("[RoguelikeChallengeListValidator] " + language + ": 아틀라스에 없는 글자 " + missing + "개");
            }

            summary.Append(language).Append(" | ").Append(font.name).Append(" | ").Append(above).Append('/').Append(below).Append(" | ")
                .Append(inkMax).Append(" | ").Append(ribbonH).Append(" | ").Append(rowMin).Append(" (줄높이 ").Append(Mathf.CeilToInt(lineHeight)).Append(") | ")
                .Append(Mathf.CeilToInt(titleMaxWidth)).Append(" (").Append(titleMaxName).Append(") | ").Append(ribbonEnd).Append(" | ")
                .Append(verdict).Append(" | ").Append(tipLinesMax).Append(" | ").Append(tipOver).Append(" | ").Append(belowCount).Append(" | ")
                .Append(headerInk).Append('/').Append(Mathf.CeilToInt(2f + labelWidth + 6f + countWidth)).Append(" | ").Append(Mathf.CeilToInt(singleInkMax)).Append(" | ")
                .Append(missing).AppendLine();
        }

        UnityEngine.Object.DestroyImmediate(canvasObject);

        summary.AppendLine("리본 넘침 · 팁 4줄 이상 언어 " + overflowLanguages + "개, 없는 글자 있는 언어 " + missingLanguages + "개");
        Debug.Log(summary.ToString());
        Debug.Log(detail.ToString());

        SaveRibbonZoom();
    }

    // // 창 렌더

    private const string PrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeChallenges.prefab";
    private const BindingFlags PrivateFlags = BindingFlags.Instance | BindingFlags.NonPublic;

    // 렌더 견본 순서(Specs 번호): 가장 넓은 제목(조합 카드)부터 달성 / 미달성이 번갈아 나오게 한다.
    private static readonly int[] RenderOrder = { 6, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 0, 1, 2, 3, 4, 5, 7 };
    private static readonly int[] RenderClearedSpecs = { 6, 9, 11, 13, 16 };
    private static readonly Language[] RenderLanguages = { Language.KR, Language.EN, Language.DE, Language.RU, Language.JA };

    /// <summary>
    /// 도전과제 창 프리팹을 한국어 · 영어 · 독일어 · 러시아어 · 일본어로 에디터 모드에서 640x360으로 그려(진행이 끝난 상태: 리본 펼침 · 게이지 가득) 3배 확대 PNG로 저장한다.
    /// 뷰 컨텍스트 없이 쓰므로 번역은 JSON에서 직접 넣고, 화면 스크립트의 private 메서드(ApplyHeaderValues · RefreshRows · LayoutRows)는 리플렉션으로 부른다.
    /// </summary>
    [MenuItem("LumberBoy/UI/Render Roguelike Challenges Window")]
    public static void RenderWindows()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        TMP_FontAsset baseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        LocalizationFontTable table = AssetDatabase.LoadAssetAtPath<LocalizationFontTable>(FontTablePath);
        Dictionary<string, LocalizationEntry> entries = LoadEntries();
        if (null == prefab || null == baseFont || null == table || null == entries)
        {
            Debug.LogError("[RoguelikeChallengeListValidator] 프리팹 · 폰트 · 문구를 찾지 못했습니다.");
            return;
        }

        for (int li = 0; RenderLanguages.Length > li; li++)
        {
            Language language = RenderLanguages[li];
            TMP_FontAsset font = table.GetFont(language);
            if (null == font) font = baseFont;

            Texture2D source = RenderCanvas(640, 360, new Color32(0x16, 0x0D, 0x09, 255), delegate (RectTransform _root)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, _root);
                instance.hideFlags = HideFlags.HideAndDontSave;
                instance.SetActive(true);
                BuildWindow(instance, language, font, entries);
            });

            if (null == source) continue;

            Texture2D zoom = Upscale(source, 3);
            string path = OutputDir + "Window_" + language + ".png";
            File.WriteAllBytes(path, zoom.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(zoom);
            Debug.Log("[RoguelikeChallengeListValidator] 창 렌더 저장: " + path + " (" + font.name + ")");
        }
    }

    private static void BuildWindow(GameObject _instance, Language _language, TMP_FontAsset _font, Dictionary<string, LocalizationEntry> _entries)
    {
        RectTransform rootRect = (RectTransform)_instance.transform;
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        TMP_Text[] texts = _instance.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; texts.Length > i; i++) texts[i].font = _font;

        CanvasGroup[] groups = _instance.GetComponentsInChildren<CanvasGroup>(true);
        for (int i = 0; groups.Length > i; i++) groups[i].alpha = 1f;

        UIView_RoguelikeChallenges view = _instance.GetComponent<UIView_RoguelikeChallenges>();
        Type viewType = typeof(UIView_RoguelikeChallenges);
        SerializedObject so = new SerializedObject(view);
        TMP_Text titleText = so.FindProperty("titleText").objectReferenceValue as TMP_Text;
        TMP_Text headerLabel = so.FindProperty("headerLabel").objectReferenceValue as TMP_Text;
        if (null != titleText) titleText.text = Resolve(_entries, "Roguelike_Menu_Challenges", _language);
        if (null != headerLabel) headerLabel.text = Resolve(_entries, ClearedLabelKey, _language);
        viewType.GetMethod("LayoutTitlePlate", PrivateFlags).Invoke(view, null);

        List<RoguelikeChallengeViewData> list = (List<RoguelikeChallengeViewData>)viewType.GetField("challenges", PrivateFlags).GetValue(view);
        for (int i = 0; RenderOrder.Length > i; i++)
        {
            Spec spec = Specs[RenderOrder[i]];
            string argument = null != spec.numberArg ? spec.numberArg : (null != spec.cardId ? Resolve(_entries, "Roguelike_Card_" + spec.cardId, _language) : null);
            string title = Resolve(_entries, spec.titleKey, _language);
            string tip = Resolve(_entries, spec.titleKey.Replace("_Title", "_Tip"), _language);
            if (null != argument)
            {
                title = title.Replace("{0}", argument);
                tip = tip.Replace("{0}", argument);
            }

            bool bCleared = Array.IndexOf(RenderClearedSpecs, RenderOrder[i]) >= 0;
            list.Add(new RoguelikeChallengeViewData { title = title, tip = tip, bCleared = bCleared, goal = spec.goal, progress = 1 < spec.goal ? spec.goal * 2 / 3 : 0 });
        }

        viewType.GetMethod("ApplyHeaderValues", PrivateFlags).Invoke(view, null);
        viewType.GetMethod("RefreshRows", PrivateFlags).Invoke(view, null);

        List<UI_RoguelikeChallengeRow> rows = (List<UI_RoguelikeChallengeRow>)viewType.GetField("rows", PrivateFlags).GetValue(view);
        int order = 0;
        for (int i = 0; rows.Count > i; i++)
        {
            if (false == rows[i].IsCleared) continue;

            rows[i].PlayClearGlow(0f, order, false);
            rows[i].TickClearGlow(0.1f);
            order++;
        }

        viewType.GetField("openElapsed", PrivateFlags).SetValue(view, 2f);
        viewType.GetField("lastGaugeTick", PrivateFlags).SetValue(view, int.MinValue);
        viewType.GetMethod("TickHeaderGauge", PrivateFlags).Invoke(view, new object[] { 0f });
    }

    // // 리본 확대 이미지

    /// <summary>리본 높이 15 · 17 · 19를 실제 UI_ChallengeRibbon으로 그려(몸통 폭 120, 완전히 편 상태) 8배 확대 PNG로 저장한다.</summary>
    private static void SaveRibbonZoom()
    {
        int[] heights = { 15, 17, 19 };
        const int bodyWidth = 120;
        const int margin = 4;
        const int gap = 4;
        int width = margin * 2 + UI_ChallengeRibbon.TotalWidth(bodyWidth, 19);
        int height = margin * 2 + gap * (heights.Length - 1);
        for (int i = 0; heights.Length > i; i++) height += heights[i];

        Texture2D source = RenderCanvas(width, height, new Color32(0x3A, 0x26, 0x1C, 255), delegate (RectTransform _root)
        {
            int y = margin;
            for (int i = 0; heights.Length > i; i++)
            {
                GameObject go = new GameObject("Ribbon" + heights[i], typeof(RectTransform));
                go.layer = LayerMask.NameToLayer("UI");
                go.hideFlags = HideFlags.HideAndDontSave;
                RectTransform rect = (RectTransform)go.transform;
                rect.SetParent(_root, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                UI_ChallengeRibbon ribbon = go.AddComponent<UI_ChallengeRibbon>();
                ribbon.raycastTarget = false;
                ribbon.SetSize(bodyWidth, heights[i]);
                ribbon.SetReveal(1f);
                rect.anchoredPosition = new Vector2(margin, -y);
                y += heights[i] + gap;
            }
        });

        if (null == source) return;

        Texture2D zoom = Upscale(source, 8);
        string path = OutputDir + "Ribbon_Zoom_H15_17_19.png";
        File.WriteAllBytes(path, zoom.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(source);
        UnityEngine.Object.DestroyImmediate(zoom);
        Debug.Log("[RoguelikeChallengeListValidator] 리본 확대 이미지 저장: " + path + " (높이 15 · 17 · 19, 위에서부터)");
    }

    /// <summary>
    /// 에디터 모드에서 UI를 _width x _height RenderTexture로 한 장 그려 Texture2D로 돌려준다(호출한 쪽이 파괴한다). _build가 캔버스 루트(왼쪽 위 기준 _width x _height 사각형) 아래에 UI를 만든다.
    /// 캔버스는 ScreenSpaceCamera + ConstantPixelSize 배율 1 + referencePixelsPerUnit 32(게임 캔버스와 같음)라서 1 캔버스 단위 = 1 픽셀이다.
    /// </summary>
    public static Texture2D RenderCanvas(int _width, int _height, Color32 _background, Action<RectTransform> _build)
    {
        int layer = LayerMask.NameToLayer("UI");
        GameObject cameraObject = new GameObject("ChallengeRenderCamera");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = _height * 0.5f / 32f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = _background;
        camera.cullingMask = 1 << layer;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 1000f;
        camera.enabled = false;

        RenderTexture target = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32);
        camera.targetTexture = target;

        GameObject canvasObject = new GameObject("ChallengeRenderCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        canvasObject.layer = layer;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = camera;
        canvas.planeDistance = 100f;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 32f;

        Texture2D result = null;
        try
        {
            RectTransform root = (RectTransform)canvasObject.transform;
            _build(root);
            Canvas.ForceUpdateCanvases();
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            result = new Texture2D(_width, _height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0f, 0f, _width, _height), 0, 0);
            result.Apply();
            RenderTexture.active = previous;
        }
        finally
        {
            camera.targetTexture = null;
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(canvasObject);
            UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        return result;
    }

    /// <summary>가장 가까운 점 확대(nearest). 픽셀 규칙 검수용이라 보간하지 않는다.</summary>
    public static Texture2D Upscale(Texture2D _source, int _factor)
    {
        int width = _source.width * _factor;
        int height = _source.height * _factor;
        Color32[] src = _source.GetPixels32();
        Color32[] dst = new Color32[width * height];
        for (int y = 0; height > y; y++)
        {
            int sy = y / _factor;
            for (int x = 0; width > x; x++) dst[y * width + x] = src[sy * _source.width + x / _factor];
        }

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.SetPixels32(dst);
        result.Apply();
        return result;
    }

    // // 측정 도우미

    // 폭(1줄, 줄바꿈 없음)
    private static float MeasureWidth(TextMeshProUGUI _measure, string _text)
    {
        _measure.textWrappingMode = TextWrappingModes.NoWrap;
        return _measure.GetPreferredValues(_text, 10000f, 10000f).x;
    }

    // 팁: 글자 폭 300에서 줄바꿈한 줄 수와 높이
    private static void MeasureWrapped(TextMeshProUGUI _measure, string _text, out int _lines, out float _height)
    {
        _measure.textWrappingMode = TextWrappingModes.Normal;
        _measure.rectTransform.sizeDelta = new Vector2(TipWidth, 1000f);
        _measure.text = _text;
        _measure.ForceMeshUpdate();
        _lines = Mathf.Max(1, _measure.textInfo.lineCount);
        _height = _measure.GetPreferredValues(_text, TipWidth, 10000f).y;
    }

    // 한 문구의 잉크 높이(기준선 위 + 아래). 공백 · 태그는 건너뛴다.
    private static float InkHeight(TMP_FontAsset _font, float _fontSize, string _text)
    {
        float top = float.MinValue;
        float bottom = float.MaxValue;
        UI_RoguelikeChallengeRow.MeasureInk(_font, _fontSize, _text, ref top, ref bottom);
        return top > bottom ? top - bottom : 0f;
    }

    // 폰트(폴백 포함)에 없는 글자 수(태그 제외, 공백 제외)
    private static int CountMissing(TMP_FontAsset _font, string _text)
    {
        int missing = 0;
        bool bInTag = false;
        for (int i = 0; _text.Length > i; i++)
        {
            char c = _text[i];
            if ('<' == c) { bInTag = true; continue; }
            if ('>' == c && true == bInTag) { bInTag = false; continue; }
            if (true == bInTag || true == char.IsWhiteSpace(c)) continue;

            if (false == _font.HasCharacter(c, true, false)) missing++;
        }

        return missing;
    }

    // // 문구

    private static Dictionary<string, LocalizationEntry> LoadEntries()
    {
        if (false == File.Exists(JsonPath))
        {
            Debug.LogError("[RoguelikeChallengeListValidator] 현지화 JSON을 찾지 못했습니다: " + JsonPath);
            return null;
        }

        LocalizationDataJson data = JsonUtility.FromJson<LocalizationDataJson>(File.ReadAllText(JsonPath));
        Dictionary<string, LocalizationEntry> entries = new Dictionary<string, LocalizationEntry>(256);
        if (null == data || null == data.entries) return entries;

        for (int i = 0; data.entries.Length > i; i++)
        {
            if (false == string.IsNullOrEmpty(data.entries[i].key)) entries[data.entries[i].key] = data.entries[i];
        }

        return entries;
    }

    // LocalizationManager.ResolveText와 같은 폴백(비면 영어, 중남미 스페인어는 스페인어)
    private static string Resolve(Dictionary<string, LocalizationEntry> _entries, string _key, Language _language)
    {
        if (null == _entries || false == _entries.TryGetValue(_key, out LocalizationEntry entry)) return string.Empty;

        string text;
        switch (_language)
        {
            case Language.KR: text = entry.kr; break;
            case Language.ZH_HANS: text = string.IsNullOrEmpty(entry.zhHans) ? entry.en : entry.zhHans; break;
            case Language.ZH_HANT: text = string.IsNullOrEmpty(entry.zhHant) ? entry.en : entry.zhHant; break;
            case Language.JA: text = string.IsNullOrEmpty(entry.ja) ? entry.en : entry.ja; break;
            case Language.DE: text = string.IsNullOrEmpty(entry.de) ? entry.en : entry.de; break;
            case Language.FR: text = string.IsNullOrEmpty(entry.fr) ? entry.en : entry.fr; break;
            case Language.PT: text = string.IsNullOrEmpty(entry.pt) ? entry.en : entry.pt; break;
            case Language.ES: text = string.IsNullOrEmpty(entry.es) ? entry.en : entry.es; break;
            case Language.RU: text = string.IsNullOrEmpty(entry.ru) ? entry.en : entry.ru; break;
            case Language.PL: text = string.IsNullOrEmpty(entry.pl) ? entry.en : entry.pl; break;
            case Language.TR: text = string.IsNullOrEmpty(entry.tr) ? entry.en : entry.tr; break;
            case Language.ES_LATAM: text = false == string.IsNullOrEmpty(entry.esLatam) ? entry.esLatam : (string.IsNullOrEmpty(entry.es) ? entry.en : entry.es); break;
            case Language.IT: text = string.IsNullOrEmpty(entry.it) ? entry.en : entry.it; break;
            case Language.UK: text = string.IsNullOrEmpty(entry.uk) ? entry.en : entry.uk; break;
            case Language.CS: text = string.IsNullOrEmpty(entry.cs) ? entry.en : entry.cs; break;
            case Language.ID: text = string.IsNullOrEmpty(entry.ind) ? entry.en : entry.ind; break;
            case Language.VI: text = string.IsNullOrEmpty(entry.vi) ? entry.en : entry.vi; break;
            default: text = entry.en; break;
        }

        return null != text ? text : string.Empty;
    }
}
#endif
