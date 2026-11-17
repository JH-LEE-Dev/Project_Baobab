#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using PresentationLayer.VFX;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

/// <summary>
/// 도전과제 달성 팝업(B안, Docs/Roguelike/_Work/ChallengeToast_B_Plan.md) 구성기 · 미리 보기 · 18개 언어 검수.
///   LumberBoy/UI/Build Roguelike Challenge Toast     UI_RoguelikeChallengeToast.prefab(284x48)을 만들고 UIView_RoguelikeHUD.prefab 오른쪽 위(여백 8px)에 붙여 challengeToast 필드를 연결한다.
///                                                    HUD 프리팹은 LoadPrefabContents로 열어 기존 ChallengeToast 자식만 바꾼다(RoguelikeHUDBuilder는 실행하지도 고치지도 않는다).
///   LumberBoy/UI/Preview Roguelike Challenge Toast   플레이 모드에서 HUD를 찾아 현재 언어의 테스트 제목 2개를 연속 Show 한다.
///   LumberBoy/UI/Validate Roguelike Challenge Toast  18개 언어 x (머리말 1 + 제목 7종)을 그 언어의 실제 TMP 폰트 에셋으로 재어 최대 폭 · 잉크 높이 · 아틀라스에 없는 글자 수를 콘솔 표로 출력한다.
///                                                    판 안쪽 270px 초과나 없는 글자가 있으면 LogError. 도전과제 목록 창의 제목(1줄) 넘침 · 팁 4줄 이상 · 칸 높이도 같은 표에 넣는다.
/// 폰트는 언어와 무관하게 Galmuri11_Optimum(원본)이고 LocalizedFontTracker가 언어별 폰트로 바꾼다.
/// </summary>
public static class RoguelikeChallengeToastBuilder
{
    private const string ToastPrefabPath = "Assets/Prefabs/Roguelike/UI/UI_RoguelikeChallengeToast.prefab";
    private const string HudPrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeHUD.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Galmuri11_Optimum.asset";
    private const string FontTablePath = "Assets/Scriptable Obj/Localization/LocalizationFontTable.asset";
    private const string PanelPath = "Assets/Graphics/Roguelike/owned/Panel_Info.png";
    private const string FlagPath = "Assets/Graphics/Roguelike/hud/HudIconFlagGold.png";
    private const string JsonPath = "Assets/Resources/Localization/RoguelikeUI.json";
    private const string ToastObjectName = "ChallengeToast";
    private const float ScreenMargin = 8.0f;

    private const int PanelWidth = UI_RoguelikeChallengeToast.Width - 6;
    private const int PanelFullHeight = UI_RoguelikeChallengeToast.PanelVisibleHeight + 4;
    private const int TitleAreaHeight = 23;
    private const int HeaderAreaHeight = UI_RoguelikeChallengeToast.BandHeight - 2;
    private const int HeaderAreaWidth = 252;
    private const int FlagSize = 16;
    // 판 안쪽 폭(검수 기준)과 머리말이 깃발 옆에 들어가는 최대 폭(깃발 4 + 16 뒤부터 가운데 + 8을 기준으로 좌우 대칭)
    private const int InnerLimit = UI_RoguelikeChallengeToast.TitleAreaWidth;
    private const int HeaderFlagLimit = 260;
    // 도전과제 목록 창 칸: 글자 폭 = 340 - 10 - 30, 제목 1줄, 팁은 줄 수만큼(3줄까지 허용, 4줄 이상이면 넘침). 칸 높이 = 위 8 + 제목 + 팁 + 아래 9, 최소 62
    private const int ListTextWidth = 300;
    private const int ListTitleMinHeight = 14;
    private const int ListRowMinHeight = 62;
    private const int ListRowTopPadding = 8;
    private const int ListRowBottomPadding = 9;

    private static readonly Color BandGold = new Color32(0xFF, 0xD4, 0x59, 255);
    private static readonly Color BandAmber = new Color32(0xFF, 0x9D, 0x2E, 255);
    private static readonly Color DarkBrown = new Color32(0x3C, 0x22, 0x08, 255);
    private static readonly Color TitleCream = new Color32(0xFF, 0xF6, 0xD8, 255);

    private static readonly string[] TitleKeys =
    {
        "Roguelike_Challenge_PrismAll_Title", "Roguelike_Challenge_CardMaxAll_Title", "Roguelike_Challenge_RubyAll_Title", "Roguelike_Challenge_TreeN_Title",
        "Roguelike_Challenge_UnlimitN_Title", "Roguelike_Challenge_TierAll_Title", "Roguelike_Challenge_Wave30_Title",
    };

    private static readonly string[] TipKeys =
    {
        "Roguelike_Challenge_PrismAll_Tip", "Roguelike_Challenge_CardMaxAll_Tip", "Roguelike_Challenge_RubyAll_Tip", "Roguelike_Challenge_TreeN_Tip",
        "Roguelike_Challenge_UnlimitN_Tip", "Roguelike_Challenge_TierAll_Tip", "Roguelike_Challenge_Wave30_Tip",
    };

    private const string HeaderKey = "Roguelike_Challenge_ClearedToast";
    private const string ListTitleKey = "Roguelike_Menu_Challenges";

    // // 구성

    [MenuItem("LumberBoy/UI/Build Roguelike Challenge Toast")]
    public static void Build()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
        Sprite flagSprite = AssetDatabase.LoadAssetAtPath<Sprite>(FlagPath);
        if (null == font || null == panelSprite || null == flagSprite)
        {
            Debug.LogError("[RoguelikeChallengeToastBuilder] 폰트 · 판 그림 · 깃발 그림을 찾지 못했습니다: " + FontPath + " / " + PanelPath + " / " + FlagPath);
            return;
        }

        if (null == AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath))
        {
            Debug.LogError("[RoguelikeChallengeToastBuilder] HUD 프리팹이 없습니다: " + HudPrefabPath);
            return;
        }

        BuildToastPrefab(font, panelSprite, flagSprite);
        AttachToHud();
    }

    private static void BuildToastPrefab(TMP_FontAsset _font, Sprite _panelSprite, Sprite _flagSprite)
    {
        bool bNew = null == AssetDatabase.LoadAssetAtPath<GameObject>(ToastPrefabPath);
        GameObject root;
        if (true == bNew)
        {
            root = new GameObject("UI_RoguelikeChallengeToast", typeof(RectTransform));
            root.layer = LayerMask.NameToLayer("UI");
        }
        else
        {
            root = PrefabUtility.LoadPrefabContents(ToastPrefabPath);
        }

        try
        {
            RectTransform rootRect = (RectTransform)root.transform;
            for (int i = rootRect.childCount - 1; 0 <= i; i--) UnityEngine.Object.DestroyImmediate(rootRect.GetChild(i).gameObject);

            rootRect.anchorMin = Vector2.one;
            rootRect.anchorMax = Vector2.one;
            rootRect.pivot = Vector2.one;
            rootRect.sizeDelta = new Vector2(UI_RoguelikeChallengeToast.Width, UI_RoguelikeChallengeToast.BandHeight + UI_RoguelikeChallengeToast.PanelVisibleHeight);
            rootRect.anchoredPosition = new Vector2(-ScreenMargin, -ScreenMargin);

            UI_RoguelikeChallengeToast toast = root.GetComponent<UI_RoguelikeChallengeToast>();
            if (null == toast) toast = root.AddComponent<UI_RoguelikeChallengeToast>();
            CanvasGroup rootGroup = GetOrAdd<CanvasGroup>(root);
            rootGroup.alpha = 0.0f;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;

            RectTransform body = NewRect("Body", rootRect);
            Stretch(body);

            // 판(띠 뒤): 마스크가 띠 아래에서 높이만큼만 보여 주고, 판 그림은 마스크 아래 끝에 붙어 위로 4px 띠 뒤에 숨는다.
            RectTransform panelMask = NewRect("PanelMask", body);
            Anchor(panelMask, new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(3.0f, -UI_RoguelikeChallengeToast.BandHeight), new Vector2(PanelWidth, UI_RoguelikeChallengeToast.PanelVisibleHeight));
            panelMask.gameObject.AddComponent<RectMask2D>();

            RectTransform panel = NewRect("Panel", panelMask);
            Anchor(panel, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(PanelWidth, PanelFullHeight));
            Image panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.sprite = _panelSprite;
            panelImage.type = Image.Type.Sliced;
            panelImage.raycastTarget = false;

            RectTransform titleClipRect = NewRect("TitleClip", panel);
            Anchor(titleClipRect, new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(4.0f, -4.0f), new Vector2(InnerLimit, TitleAreaHeight));
            RectMask2D titleClip = titleClipRect.gameObject.AddComponent<RectMask2D>();

            TMP_Text title = NewText("Title", titleClipRect, _font, TitleCream);
            Anchor(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(InnerLimit, TitleAreaHeight));
            CanvasGroup titleGroup = NoRaycastGroup(title.gameObject);

            // 띠
            RectTransform band = NewRect("Band", body);
            Anchor(band, new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f), Vector2.zero, new Vector2(UI_RoguelikeChallengeToast.Width, UI_RoguelikeChallengeToast.BandHeight));
            Image bandImage = band.gameObject.AddComponent<Image>();
            bandImage.color = BandGold;
            bandImage.raycastTarget = false;

            RectTransform line = NewRect("Underline", band);
            Anchor(line, Vector2.zero, new Vector2(1.0f, 0.0f), Vector2.zero, new Vector2(0.0f, 2.0f));
            line.pivot = new Vector2(0.5f, 0.0f);
            Image lineImage = line.gameObject.AddComponent<Image>();
            lineImage.color = BandAmber;
            lineImage.raycastTarget = false;

            // 깃발: 띠 왼쪽 위 (4, 2), 16x16. 금색 깃발 그림을 어두운 갈색으로 칠한다.
            RectTransform flag = NewRect("Flag", body);
            Anchor(flag, new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(4.0f, -2.0f), new Vector2(FlagSize, FlagSize));
            Image flagImage = flag.gameObject.AddComponent<Image>();
            flagImage.sprite = _flagSprite;
            flagImage.color = DarkBrown;
            flagImage.raycastTarget = false;

            // 머리말: 띠 가운데에서 깃발 쪽으로 8px 치우친 가운데(깃발 16 + 간격), 밑줄 2px을 뺀 위 20px 안에서 세로 가운데
            TMP_Text header = NewText("Header", body, _font, DarkBrown);
            Anchor(header.rectTransform, new Vector2(0.5f, 1.0f), new Vector2(0.5f, 1.0f), new Vector2(8.0f, 0.0f), new Vector2(HeaderAreaWidth, HeaderAreaHeight));
            header.rectTransform.pivot = new Vector2(0.5f, 1.0f);
            CanvasGroup headerGroup = NoRaycastGroup(header.gameObject);

            // 광택 · 반짝임 · 조각 그래픽(맨 위). 원점 = 왼쪽 위. 기본 UI 머티리얼(불투명 색).
            RectTransform fxRect = NewRect("Fx", body);
            Stretch(fxRect);
            fxRect.pivot = new Vector2(0.0f, 1.0f);
            UI_LootGlowParticles fx = fxRect.gameObject.AddComponent<UI_LootGlowParticles>();
            fx.raycastTarget = false;

            SerializedObject so = new SerializedObject(toast);
            Set(so, "canvasGroup", rootGroup);
            Set(so, "body", body);
            Set(so, "bandRect", band);
            Set(so, "bandImage", bandImage);
            Set(so, "panelMask", panelMask);
            Set(so, "titleClip", titleClip);
            Set(so, "titleText", title);
            Set(so, "titleGroup", titleGroup);
            Set(so, "flagRect", flag);
            Set(so, "flagImage", flagImage);
            Set(so, "headerRect", header.rectTransform);
            Set(so, "headerText", header);
            Set(so, "headerGroup", headerGroup);
            Set(so, "fx", fx);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, ToastPrefabPath);
            Debug.Log("[RoguelikeChallengeToastBuilder] 완료: " + ToastPrefabPath);
        }
        finally
        {
            if (true == bNew) UnityEngine.Object.DestroyImmediate(root);
            else PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void AttachToHud()
    {
        GameObject toastPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ToastPrefabPath);
        if (null == toastPrefab)
        {
            Debug.LogError("[RoguelikeChallengeToastBuilder] 팝업 프리팹을 읽지 못했습니다: " + ToastPrefabPath);
            return;
        }

        GameObject hudRoot = PrefabUtility.LoadPrefabContents(HudPrefabPath);
        try
        {
            UIView_RoguelikeHUD view = hudRoot.GetComponent<UIView_RoguelikeHUD>();
            if (null == view)
            {
                Debug.LogError("[RoguelikeChallengeToastBuilder] UIView_RoguelikeHUD 컴포넌트를 찾지 못했습니다.");
                return;
            }

            Transform existing = hudRoot.transform.Find(ToastObjectName);
            if (null != existing) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(toastPrefab, hudRoot.transform);
            instance.name = ToastObjectName;
            RectTransform rect = (RectTransform)instance.transform;
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-ScreenMargin, -ScreenMargin);
            rect.SetAsLastSibling();

            SerializedObject viewSo = new SerializedObject(view);
            Set(viewSo, "challengeToast", instance.GetComponent<UI_RoguelikeChallengeToast>());
            viewSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(hudRoot, HudPrefabPath);
            Debug.Log("[RoguelikeChallengeToastBuilder] HUD에 연결 완료: " + HudPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(hudRoot);
        }
    }

    // // 미리 보기

    [MenuItem("LumberBoy/UI/Preview Roguelike Challenge Toast")]
    public static void Preview()
    {
        if (false == Application.isPlaying)
        {
            Debug.LogWarning("[RoguelikeChallengeToastBuilder] 플레이 모드에서만 미리 볼 수 있습니다.");
            return;
        }

        UIView_RoguelikeHUD hud = UnityEngine.Object.FindAnyObjectByType<UIView_RoguelikeHUD>(FindObjectsInactive.Include);
        if (null == hud)
        {
            Debug.LogWarning("[RoguelikeChallengeToastBuilder] 로그라이크 HUD를 찾지 못했습니다(로그라이크 런이 시작된 뒤에 실행하십시오).");
            return;
        }

        Dictionary<string, LocalizationEntry> entries = LoadEntries();
        Language language = FontLocalizer.CurrentLanguage;

        RoguelikeChallengeViewData first = new RoguelikeChallengeViewData { title = Resolve(entries, "Roguelike_Challenge_TreeN_Title", language), tip = string.Empty, bCleared = true };
        RoguelikeChallengeViewData second = new RoguelikeChallengeViewData { title = Resolve(entries, "Roguelike_Challenge_TierAll_Title", language), tip = string.Empty, bCleared = true };
        hud.ShowChallengeToast(in first);
        hud.ShowChallengeToast(in second);
    }

    // // 검수

    [MenuItem("LumberBoy/UI/Validate Roguelike Challenge Toast")]
    public static void Validate()
    {
        TMP_FontAsset baseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        LocalizationFontTable table = AssetDatabase.LoadAssetAtPath<LocalizationFontTable>(FontTablePath);
        if (null == baseFont || null == table)
        {
            Debug.LogError("[RoguelikeChallengeToastBuilder] 폰트 또는 폰트 표를 찾지 못했습니다: " + FontPath + " / " + FontTablePath);
            return;
        }

        Dictionary<string, LocalizationEntry> entries = LoadEntries();
        if (null == entries) return;

        float fontSize = baseFont.faceInfo.pointSize;
        GameObject canvasObject = new GameObject("ToastValidateCanvas", typeof(Canvas));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        GameObject textObject = new GameObject("ToastValidateText", typeof(RectTransform));
        textObject.hideFlags = HideFlags.HideAndDontSave;
        textObject.transform.SetParent(canvasObject.transform, false);
        TextMeshProUGUI measure = textObject.AddComponent<TextMeshProUGUI>();
        measure.fontSize = fontSize;
        measure.overflowMode = TextOverflowModes.Overflow;
        measure.richText = true;
        measure.enableAutoSizing = false;

        int toastErrors = 0;
        int listIssues = 0;
        StringBuilder summary = new StringBuilder(4096);
        StringBuilder detail = new StringBuilder(4096);
        summary.AppendLine("[도전과제 팝업 검수] 폰트 크기 " + fontSize + ", 판 안쪽 한도 " + InnerLimit + "px (머리말은 깃발 옆 한도 " + HeaderFlagLimit + "px), 잉크 높이는 제목 <= " + (TitleAreaHeight - 4) + " · 머리말 <= " + (HeaderAreaHeight - 4) + " 이어야 위아래 여백 2px 이상");
        summary.AppendLine("언어 | 폰트 | 머리말 폭/잉크높이 | 제목 최대폭(키) | 제목 최대 잉크높이 | 없는 글자 | 목록 제목 최대폭 | 목록 팁 최대 줄/칸 높이 | 판정");
        detail.AppendLine("[도전과제 팝업 검수 - 제목 7종 폭] 언어 | PrismAll | CardMaxAll | RubyAll | TreeN | UnlimitN | TierAll | Wave30");

        Array languages = Enum.GetValues(typeof(Language));
        for (int li = 0; languages.Length > li; li++)
        {
            Language language = (Language)languages.GetValue(li);
            TMP_FontAsset font = table.GetFont(language);
            if (null == font) font = baseFont;
            measure.font = font;

            string headerText = Resolve(entries, HeaderKey, language);
            float headerWidth = MeasureWidth(measure, headerText);
            float headerInk = InkHeight(font, fontSize, headerText);
            int missing = CountMissing(font, headerText);

            float titleMax = 0.0f;
            string titleMaxKey = string.Empty;
            float inkMax = 0.0f;
            float listTitleMax = 0.0f;
            detail.Append(language).Append(" |");
            for (int i = 0; TitleKeys.Length > i; i++)
            {
                string text = Resolve(entries, TitleKeys[i], language);
                float width = MeasureWidth(measure, text);
                detail.Append(' ').Append(Mathf.CeilToInt(width)).Append(" |");
                if (width > titleMax)
                {
                    titleMax = width;
                    titleMaxKey = TitleKeys[i].Replace("Roguelike_Challenge_", string.Empty).Replace("_Title", string.Empty);
                }

                inkMax = Mathf.Max(inkMax, InkHeight(font, fontSize, text));
                missing += CountMissing(font, text);
                listTitleMax = Mathf.Max(listTitleMax, width);
            }
            detail.AppendLine();

            // 목록 창: 제목 1줄(글자 폭 300), 팁은 글자 폭 300에서 줄바꿈
            int tipLinesMax = 0;
            float tipHeightMax = 0.0f;
            for (int i = 0; TipKeys.Length > i; i++)
            {
                string tip = Resolve(entries, TipKeys[i], language);
                MeasureWrapped(measure, tip, out int lines, out float height);
                tipLinesMax = Mathf.Max(tipLinesMax, lines);
                tipHeightMax = Mathf.Max(tipHeightMax, height);
                missing += CountMissing(font, tip);
            }

            string listTitle = Resolve(entries, ListTitleKey, language);
            missing += CountMissing(font, listTitle);

            bool bWidthOver = InnerLimit < Mathf.CeilToInt(titleMax) || InnerLimit < Mathf.CeilToInt(headerWidth);
            bool bHeaderFlagOver = HeaderFlagLimit < Mathf.CeilToInt(headerWidth);
            bool bInkOver = (TitleAreaHeight - 4) < Mathf.CeilToInt(inkMax) || (HeaderAreaHeight - 4) < Mathf.CeilToInt(headerInk);
            bool bListOver = ListTextWidth < Mathf.CeilToInt(listTitleMax) || 4 <= tipLinesMax;

            // 칸 높이(UI_RoguelikeChallengeRow.FitHeight와 같은 식): 위 8 + 제목 높이(그 폰트 줄 높이, 최소 14) + 팁 높이 + 아래 9, 최소 62
            float lineHeight = 0.0f < font.faceInfo.pointSize ? (font.faceInfo.ascentLine - font.faceInfo.descentLine) * fontSize / font.faceInfo.pointSize * font.faceInfo.scale : 0.0f;
            int titleHeight = Mathf.Max(ListTitleMinHeight, Mathf.CeilToInt(lineHeight));
            int rowHeightMax = Mathf.Max(ListRowMinHeight, ListRowTopPadding + titleHeight + Mathf.CeilToInt(tipHeightMax) + ListRowBottomPadding);

            string verdict = "OK";
            if (true == bWidthOver || 0 < missing) verdict = "ERROR";
            else if (true == bHeaderFlagOver || true == bInkOver) verdict = "여백주의";
            if (true == bListOver) verdict += " / 목록넘침";

            if (true == bWidthOver || 0 < missing)
            {
                toastErrors++;
                Debug.LogError("[RoguelikeChallengeToastBuilder] " + language + ": 폭 " + Mathf.CeilToInt(Mathf.Max(titleMax, headerWidth)) + "px(한도 " + InnerLimit + "), 없는 글자 " + missing + "개");
            }
            if (true == bListOver) listIssues++;

            summary.Append(language).Append(" | ").Append(font.name).Append(" | ")
                .Append(Mathf.CeilToInt(headerWidth)).Append('/').Append(Mathf.CeilToInt(headerInk)).Append(" | ")
                .Append(Mathf.CeilToInt(titleMax)).Append(" (").Append(titleMaxKey).Append(") | ")
                .Append(Mathf.CeilToInt(inkMax)).Append(" | ").Append(missing).Append(" | ")
                .Append(Mathf.CeilToInt(listTitleMax)).Append(" | ").Append(tipLinesMax).Append('/').Append(rowHeightMax).Append(" | ")
                .AppendLine(verdict);
        }

        UnityEngine.Object.DestroyImmediate(canvasObject);

        summary.AppendLine("팝업 오류 언어 " + toastErrors + "개, 목록 창 넘침 언어 " + listIssues + "개");
        Debug.Log(summary.ToString());
        Debug.Log(detail.ToString());
    }

    // 폭(1줄, 줄바꿈 없음)
    private static float MeasureWidth(TextMeshProUGUI _measure, string _text)
    {
        _measure.textWrappingMode = TextWrappingModes.NoWrap;
        return _measure.GetPreferredValues(_text, 10000.0f, 10000.0f).x;
    }

    // 목록 창 팁: 글자 폭 300에서 줄바꿈한 줄 수와 높이
    private static void MeasureWrapped(TextMeshProUGUI _measure, string _text, out int _lines, out float _height)
    {
        _measure.textWrappingMode = TextWrappingModes.Normal;
        _measure.rectTransform.sizeDelta = new Vector2(ListTextWidth, 1000.0f);
        _measure.text = _text;
        _measure.ForceMeshUpdate();
        _lines = Mathf.Max(1, _measure.textInfo.lineCount);
        _height = _measure.GetPreferredValues(_text, ListTextWidth, 10000.0f).y;
    }

    // 폰트(폴백 포함)에서 글자 하나의 글리프를 찾는다.
    private static bool TryGetGlyph(TMP_FontAsset _font, uint _unicode, int _depth, out TMP_Character _character, out TMP_FontAsset _owner)
    {
        _character = null;
        _owner = null;
        if (null == _font || 4 < _depth) return false;

        if (true == _font.characterLookupTable.TryGetValue(_unicode, out TMP_Character found) && null != found.glyph)
        {
            _character = found;
            _owner = _font;
            return true;
        }

        List<TMP_FontAsset> fallbacks = _font.fallbackFontAssetTable;
        if (null == fallbacks) return false;

        for (int i = 0; fallbacks.Count > i; i++)
        {
            if (true == TryGetGlyph(fallbacks[i], _unicode, _depth + 1, out _character, out _owner)) return true;
        }

        return false;
    }

    // 글자 잉크의 위아래 범위(글리프 베어링 기준, 폰트 크기 배율 적용). 공백 · 태그는 건너뛴다.
    private static float InkHeight(TMP_FontAsset _font, float _fontSize, string _text)
    {
        float top = float.MinValue;
        float bottom = float.MaxValue;
        bool bInTag = false;
        for (int i = 0; _text.Length > i; i++)
        {
            char c = _text[i];
            if ('<' == c) { bInTag = true; continue; }
            if ('>' == c && true == bInTag) { bInTag = false; continue; }
            if (true == bInTag || true == char.IsWhiteSpace(c)) continue;

            if (false == TryGetGlyph(_font, c, 0, out TMP_Character character, out TMP_FontAsset owner)) continue;

            float scale = _fontSize / owner.faceInfo.pointSize * owner.faceInfo.scale;
            GlyphMetrics metrics = character.glyph.metrics;
            if (0.0f >= metrics.height) continue;

            top = Mathf.Max(top, metrics.horizontalBearingY * scale);
            bottom = Mathf.Min(bottom, (metrics.horizontalBearingY - metrics.height) * scale);
        }

        return top > bottom ? top - bottom : 0.0f;
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
            Debug.LogError("[RoguelikeChallengeToastBuilder] 현지화 JSON을 찾지 못했습니다: " + JsonPath);
            return null;
        }

        LocalizationDataJson data = JsonUtility.FromJson<LocalizationDataJson>(File.ReadAllText(JsonPath));
        Dictionary<string, LocalizationEntry> entries = new Dictionary<string, LocalizationEntry>(128);
        if (null == data || null == data.entries) return entries;

        for (int i = 0; data.entries.Length > i; i++)
        {
            if (false == string.IsNullOrEmpty(data.entries[i].key)) entries[data.entries[i].key] = data.entries[i];
        }

        return entries;
    }

    // LocalizationManager.ResolveText와 같은 폴백 + {0}에 최대 견본 값(TreeN 10,000 · UnlimitN 25)을 넣는다.
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

        if (null == text) return string.Empty;
        if (_key.Contains("TreeN")) return text.Replace("{0}", "10,000");
        if (_key.Contains("UnlimitN")) return text.Replace("{0}", "25");
        return text;
    }

    // // 도우미

    private static RectTransform NewRect(string _name, RectTransform _parent)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(_parent, false);
        return rect;
    }

    private static void Stretch(RectTransform _rect)
    {
        _rect.anchorMin = Vector2.zero;
        _rect.anchorMax = Vector2.one;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }

    /// <summary>앵커 · 위치 · 크기를 정한다. 피벗은 앵커 점과 같은 쪽(최소 앵커)에 둔다(스트레치 앵커는 호출 측이 따로 맞춘다).</summary>
    private static void Anchor(RectTransform _rect, Vector2 _anchorMin, Vector2 _anchorMax, Vector2 _position, Vector2 _size)
    {
        _rect.anchorMin = _anchorMin;
        _rect.anchorMax = _anchorMax;
        _rect.pivot = _anchorMin;
        _rect.sizeDelta = _size;
        _rect.anchoredPosition = _position;
    }

    private static TMP_Text NewText(string _name, RectTransform _parent, TMP_FontAsset _font, Color _color)
    {
        RectTransform rect = NewRect(_name, _parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = _font;
        text.fontSize = _font.faceInfo.pointSize;
        text.text = string.Empty;
        text.alignment = TextAlignmentOptions.Midline;
        text.color = _color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        rect.gameObject.AddComponent<LocalizedFontTracker>();
        return text;
    }

    private static CanvasGroup NoRaycastGroup(GameObject _target)
    {
        CanvasGroup group = _target.AddComponent<CanvasGroup>();
        group.alpha = 1.0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        return group;
    }

    private static T GetOrAdd<T>(GameObject _target) where T : Component
    {
        T component = _target.GetComponent<T>();
        return null != component ? component : _target.AddComponent<T>();
    }

    private static void Set(SerializedObject _so, string _property, UnityEngine.Object _value)
    {
        SerializedProperty property = _so.FindProperty(_property);
        if (null == property)
        {
            Debug.LogError("[RoguelikeChallengeToastBuilder] 필드를 찾지 못했습니다: " + _property);
            return;
        }

        property.objectReferenceValue = _value;
    }
}
#endif
