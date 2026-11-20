#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 도감 창(UIView_RoguelikeCollection) 18개 언어 폭 검수.
///   LumberBoy/UI/Validate Roguelike Collection   18개 언어 x (제목판 · 진행도 줄 · 분류 머리줄 4종 · 상세 종류/이름/설명 · MAX 달성 라벨 · 미발견 문구)를
///   그 언어의 실제 TMP 폰트 에셋으로 재어, 창이 정한 폭(UIView_RoguelikeCollection의 상수와 같은 값)을 넘는 언어를 콘솔 표로 출력한다.
/// 글자 크기는 프리팹의 각 TMP 글자 크기를 그대로 쓴다. 카드 이름 · 설명은 카드 데이터베이스의 키를 RoguelikeUI.json에서 찾는다.
/// </summary>
public static class RoguelikeCollectionValidator
{
    private const string PrefabPath = "Assets/Prefabs/Roguelike/UI/UIView_RoguelikeCollection.prefab";
    private const string FontPath = "Assets/TextMesh Pro/Fonts/Galmuri11_Optimum.asset";
    private const string FontTablePath = "Assets/Scriptable Obj/Localization/LocalizationFontTable.asset";
    private const string JsonPath = "Assets/Resources/Localization/RoguelikeUI.json";

    // UIView_RoguelikeCollection의 배치 상수(같은 값)
    private const int PanelWidth = 528;
    private const int SpecPlateWidth = 272;
    private const int PlatePadding = 24;
    private const int GridX = 12;
    private const int ProgressRight = PanelWidth - 12;
    private const int ProgressGap = 8;
    private const int HeaderRight = 7 * 40 - 4;
    private const int HeaderLineGap = 8;
    private const int DetailWidth = 218;
    private const int DetailHeight = 250;
    private const int DetailMaskTop = 112;
    private const int DetailMaskWidth = DetailWidth - 20;
    private const int DetailMaskHeight = DetailHeight - DetailMaskTop;
    private const int DetailDescTop = 41;
    private const int DetailBadgeTop = DetailHeight - 16 - DetailMaskTop;
    private const int DetailBadgeGap = 4;
    private const float DetailDescWidth = 182f;
    private const int MaxNameLines = 2;
    private const int MaxDescLines = 5;
    private const float TextWidthSlack = 2f;
    private const float Unbounded = 10000f;

    // 진행 막대가 이보다 짧으면 주의(시안의 가로 막대가 사라질 정도)
    private const int BarMinWidth = 60;
    // 머리줄 선이 이보다 짧으면 주의
    private const int HeaderLineMinWidth = 8;

    private const string TitleKey = "Roguelike_Collection_Title";
    private const string CompleteTitleKey = "Roguelike_Collection_CompleteTitle";
    private const string FoundKey = "Roguelike_Collection_Found";
    private const string CompleteKey = "Roguelike_Collection_Complete";
    private const string MaxReachedKey = "Roguelike_Collection_MaxReached";
    private const string UnfoundKey = "Roguelike_Collection_Unfound";

    private static readonly string[] KindKeys =
    {
        "Roguelike_Collection_KindActive", "Roguelike_Collection_KindPassive", "Roguelike_Collection_KindPrism", "Roguelike_Collection_KindRuby",
    };

    // 도감 때문에 추가한 창 밖 문구(글자 누락 검사만 한다)
    private static readonly string[] ExtraKeys =
    {
        "Roguelike_Menu_Collection", "Roguelike_Challenge_CollectionAll_Title", "Roguelike_Challenge_CollectionAll_Tip",
    };

    // 분류별 칸 수 최대(액티브 7 · 패시브 10 · 프리즘 6 · 루비 3): 머리줄 수치 "n/n"의 가장 넓은 모양 근사
    private static readonly string[] CountSamples = { "7/7", "10/10", "6/6", "3/3" };

    [MenuItem("LumberBoy/UI/Validate Roguelike Collection")]
    public static void Validate()
    {
        string report = BuildReport(out int overflowLanguages, out int missingLanguages);
        if (null == report) return;

        Debug.Log(report);
        if (0 < missingLanguages) Debug.LogError("[RoguelikeCollectionValidator] 아틀라스에 없는 글자가 있는 언어 " + missingLanguages + "개");
        if (0 < overflowLanguages) Debug.LogWarning("[RoguelikeCollectionValidator] 폭 · 높이를 넘는 언어 " + overflowLanguages + "개");
    }

    /// <summary>검수 표 문자열. 프리팹 · 폰트 · 문구를 찾지 못하면 null.</summary>
    public static string BuildReport(out int _overflowLanguages, out int _missingLanguages)
    {
        _overflowLanguages = 0;
        _missingLanguages = 0;

        UIView_RoguelikeCollection view = AssetDatabase.LoadAssetAtPath<UIView_RoguelikeCollection>(PrefabPath);
        TMP_FontAsset baseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        LocalizationFontTable table = AssetDatabase.LoadAssetAtPath<LocalizationFontTable>(FontTablePath);
        string[] databaseGuids = AssetDatabase.FindAssets("t:CardDatabaseSO");
        if (null == view || null == baseFont || null == table || 0 == databaseGuids.Length)
        {
            Debug.LogError("[RoguelikeCollectionValidator] 프리팹 · 폰트 · 폰트 표 · 카드 데이터베이스를 찾지 못했습니다.");
            return null;
        }

        // 카드 데이터베이스가 여러 개(정식 · 초안)라 도감 순서 카드가 처음 찾아지는 쪽을 쓴다.
        List<CardDatabaseSO> databases = new List<CardDatabaseSO>(databaseGuids.Length);
        for (int i = 0; databaseGuids.Length > i; i++)
        {
            CardDatabaseSO loaded = AssetDatabase.LoadAssetAtPath<CardDatabaseSO>(AssetDatabase.GUIDToAssetPath(databaseGuids[i]));
            if (null != loaded) databases.Add(loaded);
        }

        Dictionary<string, LocalizationEntry> entries = LoadEntries();
        if (0 == databases.Count || null == entries) return null;

        SerializedObject so = new SerializedObject(view);
        TMP_Text plateText = so.FindProperty("plateText").objectReferenceValue as TMP_Text;
        TMP_Text progressLabel = so.FindProperty("progressLabel").objectReferenceValue as TMP_Text;
        TMP_Text progressCount = so.FindProperty("progressCount").objectReferenceValue as TMP_Text;
        TMP_Text progressPercent = so.FindProperty("progressPercent").objectReferenceValue as TMP_Text;
        TMP_Text headerTitle = so.FindProperty("headers.Array.data[0].title").objectReferenceValue as TMP_Text;
        TMP_Text headerCount = so.FindProperty("headers.Array.data[0].count").objectReferenceValue as TMP_Text;
        TMP_Text detailKind = so.FindProperty("detailKind").objectReferenceValue as TMP_Text;
        TMP_Text detailName = so.FindProperty("detailName").objectReferenceValue as TMP_Text;
        TMP_Text detailDesc = so.FindProperty("detailDesc").objectReferenceValue as TMP_Text;
        TMP_Text detailMaxLabel = so.FindProperty("detailMaxLabel").objectReferenceValue as TMP_Text;
        Image detailMaxBadge = so.FindProperty("detailMaxBadge").objectReferenceValue as Image;
        SerializedProperty idsProperty = so.FindProperty("collectionCardIds");
        if (null == plateText || null == progressLabel || null == progressCount || null == progressPercent || null == headerTitle || null == headerCount
            || null == detailKind || null == detailName || null == detailDesc || null == detailMaxLabel || null == idsProperty)
        {
            Debug.LogError("[RoguelikeCollectionValidator] 프리팹의 글자 참조가 비어 있습니다.");
            return null;
        }

        float badgeWidth = null != detailMaxBadge && null != detailMaxBadge.sprite ? detailMaxBadge.sprite.rect.size.x : 14f;

        // 도감 카드 이름 · 설명 키(분류와 최대 레벨 배지 유무)
        int cardCount = idsProperty.arraySize;
        List<string> nameKeys = new List<string>(cardCount);
        List<string> descKeys = new List<string>(cardCount);
        List<bool> hasBadge = new List<bool>(cardCount);
        for (int i = 0; cardCount > i; i++)
        {
            string cardId = idsProperty.GetArrayElementAtIndex(i).stringValue;
            CardDefinitionSO card = null;
            for (int d = 0; null == card && databases.Count > d; d++) card = databases[d].FindById(cardId);
            if (null == card) continue;

            nameKeys.Add(card.nameKey);
            descKeys.Add(card.descriptionKey);
            hasBadge.Add(ECardKind.Active == card.kind || ECardKind.Passive == card.kind);
        }

        GameObject canvasObject = new GameObject("CollectionValidateCanvas", typeof(Canvas));
        canvasObject.hideFlags = HideFlags.HideAndDontSave;
        GameObject textObject = new GameObject("CollectionValidateText", typeof(RectTransform));
        textObject.hideFlags = HideFlags.HideAndDontSave;
        textObject.transform.SetParent(canvasObject.transform, false);
        TextMeshProUGUI measure = textObject.AddComponent<TextMeshProUGUI>();
        measure.overflowMode = TextOverflowModes.Overflow;
        measure.richText = true;
        measure.enableAutoSizing = false;

        StringBuilder report = new StringBuilder(8192);
        report.AppendLine("[카드 도감 검수] 카드 " + nameKeys.Count + "장, 글자 크기 제목판 " + plateText.fontSize + " / 진행도 " + progressLabel.fontSize + " / 머리줄 " + headerTitle.fontSize
            + " / 상세 종류 " + detailKind.fontSize + " 이름 " + detailName.fontSize + " 설명 " + detailDesc.fontSize + " / MAX 라벨 " + detailMaxLabel.fontSize);
        report.AppendLine("한도: 제목판 " + PanelWidth + ", 막대 최소 " + BarMinWidth + ", 머리줄 선 최소 " + HeaderLineMinWidth + ", 상세 종류 " + DetailMaskWidth + ", 이름 폭 " + DetailDescWidth + " 최대 " + MaxNameLines + "줄, 설명 폭 " + DetailDescWidth + " 최대 " + MaxDescLines
            + "줄 (설명 아래끝 한도: MAX 배지 있는 카드 " + (DetailBadgeTop - 2) + " · 그 밖 " + DetailMaskHeight + "), MAX 줄 폭 " + DetailMaskWidth);
        report.AppendLine("언어 | 폰트 | 제목판 폭 | 진행 막대 | 머리줄 선 최소 | 종류 최대 | 이름 줄 최대 | 설명 줄/아래끝 최대 | 미발견 높이 | MAX 줄 폭 | 없는 글자 | 판정");

        Array languages = Enum.GetValues(typeof(Language));
        for (int li = 0; languages.Length > li; li++)
        {
            Language language = (Language)languages.GetValue(li);
            TMP_FontAsset font = table.GetFont(language);
            if (null == font) font = baseFont;
            measure.font = font;

            int missing = 0;
            StringBuilder problems = new StringBuilder(64);

            // 도감이 쓰는 다른 문구(로그라이크 메뉴 버튼 · 도전과제 제목 · 팁)의 없는 글자
            for (int e = 0; ExtraKeys.Length > e; e++) missing += CountMissing(font, Resolve(entries, ExtraKeys[e], language));

            // 제목판
            string title = Resolve(entries, TitleKey, language);
            string completeTitle = Resolve(entries, CompleteTitleKey, language);
            float plateWidthText = Mathf.Max(MeasureWidth(measure, title, plateText.fontSize), MeasureWidth(measure, completeTitle, plateText.fontSize));
            int plateWidth = Mathf.Max(SpecPlateWidth, Mathf.CeilToInt(plateWidthText) + PlatePadding * 2);
            if (0 != (plateWidth & 1)) plateWidth++;
            if (PanelWidth < plateWidth) problems.Append("제목판 ");
            missing += CountMissing(font, title) + CountMissing(font, completeTitle);

            // 진행도 줄: 라벨 + 공백 + "26 / 26" + 간격 + 막대 + 간격 + 백분율(가장 넓은 "99%" · 완성 문구)
            string foundLabel = Resolve(entries, FoundKey, language);
            string completeLabel = Resolve(entries, CompleteKey, language);
            float labelWidth = MeasureWidth(measure, foundLabel, progressLabel.fontSize);
            float spaceWidth = MeasureWidth(measure, "a a", progressLabel.fontSize) - MeasureWidth(measure, "aa", progressLabel.fontSize);
            float numberWidth = MeasureWidth(measure, "26 / 26", progressCount.fontSize);
            float percentWidth = Mathf.Max(MeasureWidth(measure, "99%", progressPercent.fontSize), MeasureWidth(measure, completeLabel, progressPercent.fontSize));
            int barX = Mathf.RoundToInt(GridX + labelWidth + spaceWidth + numberWidth + ProgressGap);
            int barWidth = Mathf.RoundToInt(ProgressRight - percentWidth - ProgressGap - barX);
            if (BarMinWidth > barWidth) problems.Append("진행막대 ");
            missing += CountMissing(font, foundLabel) + CountMissing(font, completeLabel);

            // 분류 머리줄: 제목 + 수치 + 선(한 줄, 8px 여유)
            float headerLineMin = float.MaxValue;
            float kindMax = 0f;
            for (int k = 0; KindKeys.Length > k; k++)
            {
                string kind = Resolve(entries, KindKeys[k], language);
                float titleWidth = MeasureWidth(measure, kind, headerTitle.fontSize);
                float countWidth = MeasureWidth(measure, CountSamples[k], headerCount.fontSize);
                headerLineMin = Mathf.Min(headerLineMin, HeaderRight - titleWidth - countWidth - HeaderLineGap);
                kindMax = Mathf.Max(kindMax, MeasureWidth(measure, kind, detailKind.fontSize) + TextWidthSlack);
                missing += CountMissing(font, kind);
            }

            if (HeaderLineMinWidth > headerLineMin) problems.Append("머리줄 ");
            if (DetailMaskWidth < kindMax) problems.Append("종류 ");

            // 카드 이름(폭 182에서 최대 2줄, 늘어난 줄만큼 설명이 내려감) · 설명(폭 182에서 최대 5줄, 높이는 상세 아래 끝 또는 MAX 배지 윗변까지)
            MeasureWrapped(measure, "A", detailName.fontSize, DetailDescWidth, out _, out float singleNameHeight);
            int nameLinesMax = 1;
            int descLinesMax = 0;
            float descBottomMax = 0f;
            int missingKeys = 0;
            bool bNameOver = false;
            bool bDescLinesOver = false;
            bool bDescOver = false;
            for (int i = 0; nameKeys.Count > i; i++)
            {
                string name = Resolve(entries, nameKeys[i], language);
                string desc = Resolve(entries, descKeys[i], language);
                if (0 == name.Length || 0 == desc.Length) missingKeys++;
                desc = StripMarkers(desc);

                MeasureWrapped(measure, name, detailName.fontSize, DetailDescWidth, out int nameLines, out float nameHeight);
                MeasureWrapped(measure, desc, detailDesc.fontSize, DetailDescWidth, out int lines, out float height);
                nameLinesMax = Mathf.Max(nameLinesMax, nameLines);
                descLinesMax = Mathf.Max(descLinesMax, lines);
                float descTop = DetailDescTop + Mathf.Max(0f, nameHeight - singleNameHeight);
                float descBottom = descTop + height;
                descBottomMax = Mathf.Max(descBottomMax, descBottom);
                float limit = true == hasBadge[i] ? DetailBadgeTop - 2 : DetailMaskHeight;
                if (MaxNameLines < nameLines) bNameOver = true;
                if (MaxDescLines < lines) bDescLinesOver = true;
                if (limit < descBottom) bDescOver = true;
                missing += CountMissing(font, name) + CountMissing(font, desc);
            }

            if (true == bNameOver) problems.Append("이름줄 ");
            if (true == bDescLinesOver) problems.Append("설명줄 ");
            if (true == bDescOver) problems.Append("설명높이 ");
            if (0 < missingKeys) problems.Append("키없음" + missingKeys + " ");

            // 미발견 문구
            string unfound = Resolve(entries, UnfoundKey, language);
            MeasureWrapped(measure, unfound, detailDesc.fontSize, DetailDescWidth, out int unfoundLines, out float unfoundHeight);
            if (DetailMaskHeight - DetailDescTop < unfoundHeight) problems.Append("미발견 ");
            missing += CountMissing(font, unfound);

            // MAX 달성 줄: 배지 + 간격 + 라벨
            string maxLabel = Resolve(entries, MaxReachedKey, language);
            float maxLine = badgeWidth + DetailBadgeGap + MeasureWidth(measure, maxLabel, detailMaxLabel.fontSize) + TextWidthSlack;
            if (DetailMaskWidth < maxLine) problems.Append("MAX줄 ");
            missing += CountMissing(font, maxLabel);

            if (0 < missing) _missingLanguages++;
            if (0 < problems.Length) _overflowLanguages++;

            report.Append(language).Append(" | ").Append(font.name).Append(" | ").Append(plateWidth).Append(" | ").Append(barWidth).Append(" | ")
                .Append(Mathf.FloorToInt(headerLineMin)).Append(" | ").Append(Mathf.CeilToInt(kindMax)).Append(" | ").Append(nameLinesMax).Append(" | ")
                .Append(descLinesMax).Append('/').Append(Mathf.CeilToInt(descBottomMax)).Append(" | ").Append(unfoundLines).Append('/').Append(Mathf.CeilToInt(unfoundHeight)).Append(" | ")
                .Append(Mathf.CeilToInt(maxLine)).Append(" | ").Append(missing).Append(" | ")
                .Append(0 < problems.Length ? problems.ToString().Trim() : "OK").AppendLine();
        }

        UnityEngine.Object.DestroyImmediate(canvasObject);
        report.AppendLine("넘침 언어 " + _overflowLanguages + "개, 없는 글자 있는 언어 " + _missingLanguages + "개");
        return report.ToString();
    }

    // // 측정 도우미

    private static float MeasureWidth(TextMeshProUGUI _measure, string _text, float _fontSize)
    {
        _measure.fontSize = _fontSize;
        _measure.textWrappingMode = TextWrappingModes.NoWrap;
        return Mathf.Ceil(_measure.GetPreferredValues(_text, Unbounded, Unbounded).x);
    }

    private static void MeasureWrapped(TextMeshProUGUI _measure, string _text, float _fontSize, float _width, out int _lines, out float _height)
    {
        _measure.fontSize = _fontSize;
        _measure.textWrappingMode = TextWrappingModes.Normal;
        _measure.rectTransform.sizeDelta = new Vector2(_width, 1000f);
        _measure.text = _text;
        _measure.ForceMeshUpdate();
        _lines = Mathf.Max(1, _measure.textInfo.lineCount);
        _height = Mathf.Ceil(_measure.GetPreferredValues(_text, _width, Unbounded).y);
    }

    // 색 마커 {+} {-} {/}를 걷어 낸다(폭 계산에 영향 없는 리치텍스트 태그로 바뀌므로 글자만 남긴다).
    private static string StripMarkers(string _text)
    {
        if (string.IsNullOrEmpty(_text)) return string.Empty;

        return _text.Replace("{+}", string.Empty).Replace("{-}", string.Empty).Replace("{/}", string.Empty);
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
            Debug.LogError("[RoguelikeCollectionValidator] 현지화 JSON을 찾지 못했습니다: " + JsonPath);
            return null;
        }

        LocalizationDataJson data = JsonUtility.FromJson<LocalizationDataJson>(File.ReadAllText(JsonPath));
        Dictionary<string, LocalizationEntry> entries = new Dictionary<string, LocalizationEntry>(1024);
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
        if (null == _entries || string.IsNullOrEmpty(_key) || false == _entries.TryGetValue(_key, out LocalizationEntry entry)) return string.Empty;

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
