using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

public static class LocalizationFontCharacterSetGenerator
{
    private const string JsonPath = "Assets/Resources/Localization";
    private const string ExportDirectory = "Assets/TextMesh Pro/Font Character Sets";

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private static readonly Regex RichTextTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex CompositeFormatRegex = new Regex(@"\{\d+(?:[^{}]*)\}", RegexOptions.Compiled);

    // 폰트 하나당 한 줄이다. 여러 언어가 한 폰트를 공유하면 열을 나란히 적어 합집합을 굽는다.
    // (LocalizationFontTable에서 그 언어들이 어떤 폰트를 가리키는지와 반드시 맞춰야 한다)
    private static readonly LanguageDefinition[] Languages =
    {
        new LanguageDefinition(
            "JA",
            "FusionPixel_JA_Characters.txt",
            "、。！？「」『』（）［］【】・ー〜…※",
            entry => entry.ja),
        new LanguageDefinition(
            "ZH_HANS",
            "FusionPixel_ZH_HANS_Characters.txt",
            "，。！？：；（）【】《》、“”‘’…—·",
            entry => entry.zhHans),
        new LanguageDefinition(
            "ZH_HANT",
            "FusionPixel_ZH_HANT_Characters.txt",
            "，。！？：；（）【】《》、「」『』“”‘’…—·",
            entry => entry.zhHant),

        // 독일어·프랑스어·포르투갈어·스페인어·러시아어는 Lorem 하나로 처리하므로 다섯 열을 합쳐 굽는다.
        // 안전 문자는 스페인어의 여는 물음표·느낌표와 러시아어·독일어의 인용부호다. 번역문에
        // 아직 안 나타났더라도 번역이 들어오는 순간 쓰이는데, 그때 굽기를 잊으면 그 글자만 깨진다.
        new LanguageDefinition(
            "LATIN_CYRILLIC",
            "Lorem_Characters.txt",
            "¿¡«»‹›„“”‘’–—…·",
            entry => entry.de,
            entry => entry.fr,
            entry => entry.pt,
            entry => entry.es,
            entry => entry.ru)
    };

    [MenuItem("Tools/Localization/Generate Font Character Sets", false, 1)]
    public static void GenerateFromMenu()
    {
        GenerateAll(true);
    }

    [MenuItem("Tools/Localization/Bake All Font Atlases", false, 2)]
    public static void BakeAllFontAtlasesFromMenu()
    {
        BakeAllFontAtlases();
    }

    [MenuItem("Tools/Localization/Generate Character Sets and Bake Atlases", false, 0)]
    public static void GenerateAndBakeFromMenu()
    {
        GenerateAll(false);
        BakeAllFontAtlases();
    }

    public static void BakeAllFontAtlases()
    {
        // 세 배열은 같은 순서로 짝을 이룬다. 폰트를 늘릴 때 한 줄만 빠뜨리면 엉뚱한 TTF로
        // 구워지므로, 위 Languages의 FileName과 여기 문자셋 경로가 맞는지 함께 확인할 것.
        string[] _fontAssetPaths = new string[]
        {
            "Assets/TextMesh Pro/Fonts/FusionPixel_JA.asset",
            "Assets/TextMesh Pro/Fonts/FusionPixel_zh_hans.asset",
            "Assets/TextMesh Pro/Fonts/FusionPixel_zh_hant.asset",
            "Assets/TextMesh Pro/Fonts/Lorem_Optimum.asset"
        };

        string[] _ttfPaths = new string[]
        {
            "Assets/TextMesh Pro/Fonts/fusion-pixel-12px-proportional-ja.ttf",
            "Assets/TextMesh Pro/Fonts/fusion-pixel-12px-proportional-zh_hans.ttf",
            "Assets/TextMesh Pro/Fonts/fusion-pixel-12px-proportional-zh_hant.ttf",
            "Assets/TextMesh Pro/Fonts/Lorem.ttf"
        };

        string[] _charSetPaths = new string[]
        {
            Path.Combine(ExportDirectory, "FusionPixel_JA_Characters.txt").Replace('\\', '/'),
            Path.Combine(ExportDirectory, "FusionPixel_ZH_HANS_Characters.txt").Replace('\\', '/'),
            Path.Combine(ExportDirectory, "FusionPixel_ZH_HANT_Characters.txt").Replace('\\', '/'),
            Path.Combine(ExportDirectory, "Lorem_Characters.txt").Replace('\\', '/')
        };

        for (int i = 0; i < _fontAssetPaths.Length; i++)
        {
            BakeFontAsset(_fontAssetPaths[i], _ttfPaths[i], _charSetPaths[i]);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[LocalizationFontCharacterSetGenerator] 모든 다국어 폰트 아틀라스 베이킹이 성공적으로 완료되었습니다.");
    }

    private static void BakeFontAsset(string _fontPath, string _ttfPath, string _charSetPath)
    {
        TMP_FontAsset _existingFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(_fontPath);
        Font _ttf = AssetDatabase.LoadAssetAtPath<Font>(_ttfPath);

        if (null == _existingFont || null == _ttf || false == File.Exists(_charSetPath))
        {
            Debug.LogError($"[LocalizationFontCharacterSetGenerator] 폰트 베이킹 실패: {_fontPath} 또는 {_ttfPath} 에셋을 찾을 수 없습니다.");
            return;
        }

        string _chars = File.ReadAllText(_charSetPath, Encoding.UTF8);

        // 1. 깨끗한 신규 Dynamic 폰트 에셋을 메모리에 생성하여 대상 문자 전체 베이킹
        TMP_FontAsset _newFont = TMP_FontAsset.CreateFontAsset(
            _ttf,
            12,
            2,
            GlyphRenderMode.RASTER_HINTED,
            2048,
            2048,
            AtlasPopulationMode.Dynamic,
            true);

        if (null == _newFont)
        {
            Debug.LogError($"[LocalizationFontCharacterSetGenerator] CreateFontAsset 실패: {_fontPath}");
            return;
        }

        string _missing;
        _newFont.TryAddCharacters(_chars, out _missing);

        if (false == string.IsNullOrEmpty(_missing))
        {
            Debug.LogWarning($"[LocalizationFontCharacterSetGenerator] {_existingFont.name} 미지원 문자: {_missing}");
        }

        // 2. 기존 폰트 에셋의 테이블을 신규 베이킹된 테이블로 동기화
        _existingFont.faceInfo = _newFont.faceInfo;
        _existingFont.glyphTable.Clear();
        _existingFont.glyphTable.AddRange(_newFont.glyphTable);
        _existingFont.characterTable.Clear();
        _existingFont.characterTable.AddRange(_newFont.characterTable);

        // 3. 아틀라스 텍스처 픽셀 복사 및 플러시
        Texture2D _srcTex = _newFont.atlasTexture;
        Texture2D _dstTex = _existingFont.atlasTexture;

        if (null != _srcTex && null != _dstTex)
        {
            Graphics.CopyTexture(_srcTex, _dstTex);
            _dstTex.Apply(false, false);
            EditorUtility.SetDirty(_dstTex);
        }

        _existingFont.ReadFontAssetDefinition();
        EditorUtility.SetDirty(_existingFont);

        Debug.Log($"[LocalizationFontCharacterSetGenerator] {_existingFont.name} 베이킹 완료: 문자수={_existingFont.characterTable.Count}, 글리프수={_existingFont.glyphTable.Count}");
    }

    public static bool GenerateAll(bool _refreshAssetDatabase)
    {
        if (!Directory.Exists(JsonPath))
        {
            Debug.LogError($"[LocalizationFontCharacterSetGenerator] Path not found: {JsonPath}");
            return false;
        }

        string[] jsonFiles = Directory.GetFiles(JsonPath, "*.json", SearchOption.TopDirectoryOnly);
        Array.Sort(jsonFiles, StringComparer.OrdinalIgnoreCase);

        List<LocalizationDataJson> localizationData = new List<LocalizationDataJson>(jsonFiles.Length);
        List<string> errors = new List<string>();

        for (int i = 0; i < jsonFiles.Length; i++)
        {
            string filePath = jsonFiles[i];

            try
            {
                string jsonText = File.ReadAllText(filePath, Encoding.UTF8);
                LocalizationDataJson data = JsonUtility.FromJson<LocalizationDataJson>(jsonText);

                if (data == null || data.entries == null)
                {
                    errors.Add($"{filePath}: entries is missing.");
                    continue;
                }

                localizationData.Add(data);
            }
            catch (Exception exception)
            {
                errors.Add($"{filePath}: {exception.Message}");
            }
        }

        if (errors.Count > 0)
        {
            Debug.LogError(
                "[LocalizationFontCharacterSetGenerator] Generation cancelled because localization JSON could not be parsed.\n" +
                string.Join("\n", errors));
            return false;
        }

        if (!Directory.Exists(ExportDirectory))
        {
            Directory.CreateDirectory(ExportDirectory);
        }

        bool anyFileChanged = false;
        StringBuilder summary = new StringBuilder();
        summary.Append($"[LocalizationFontCharacterSetGenerator] Scanned {jsonFiles.Length} JSON files.");

        for (int languageIndex = 0; languageIndex < Languages.Length; languageIndex++)
        {
            LanguageDefinition language = Languages[languageIndex];
            SortedSet<int> codePoints = new SortedSet<int>();
            int localizedEntryCount = 0;
            int englishFallbackEntryCount = 0;

            AddBasicLatin(codePoints);
            AddVisibleCharacters(language.SafetyCharacters, codePoints);

            for (int dataIndex = 0; dataIndex < localizationData.Count; dataIndex++)
            {
                LocalizationEntry[] entries = localizationData[dataIndex].entries;

                for (int entryIndex = 0; entryIndex < entries.Length; entryIndex++)
                {
                    LocalizationEntry entry = entries[entryIndex];

                    // 이 폰트를 쓰는 언어를 모두 돌면서, 실제로 화면에 나갈 문자열을 모은다.
                    // 번역이 비어 있으면 런타임이 영어로 폴백하므로(LocalizationManager.ResolveText)
                    // 여기서도 똑같이 영어를 집어넣어야 한다. 안 그러면 아직 번역하지 않은 항목이
                    // 그 언어에서 통째로 두부가 된다.
                    for (int columnIndex = 0; columnIndex < language.SelectTexts.Length; columnIndex++)
                    {
                        string localizedText = language.SelectTexts[columnIndex](entry);
                        string runtimeText;

                        if (!string.IsNullOrWhiteSpace(localizedText))
                        {
                            runtimeText = localizedText;
                            localizedEntryCount++;
                        }
                        else
                        {
                            runtimeText = entry.en;
                            if (!string.IsNullOrWhiteSpace(runtimeText)) englishFallbackEntryCount++;
                        }

                        AddVisibleCharacters(runtimeText, codePoints);
                    }
                }
            }

            string outputPath = Path.Combine(ExportDirectory, language.FileName).Replace('\\', '/');
            string outputText = BuildCharacterText(codePoints);
            bool fileChanged = WriteIfChanged(outputPath, outputText);
            anyFileChanged |= fileChanged;

            summary.Append(
                $"\n- {language.Label}: {codePoints.Count} characters " +
                $"({localizedEntryCount} localized, {englishFallbackEntryCount} English fallback)" +
                (fileChanged ? " [updated]" : " [unchanged]"));
        }

        if (_refreshAssetDatabase && anyFileChanged)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        Debug.Log(summary.ToString());
        return true;
    }

    private static void AddBasicLatin(SortedSet<int> _codePoints)
    {
        for (int codePoint = 0x20; codePoint <= 0x7E; codePoint++)
        {
            _codePoints.Add(codePoint);
        }
    }

    private static void AddVisibleCharacters(string _text, SortedSet<int> _codePoints)
    {
        if (string.IsNullOrEmpty(_text)) return;

        string visibleText = RichTextTagRegex.Replace(_text, string.Empty);
        visibleText = CompositeFormatRegex.Replace(visibleText, string.Empty);

        for (int index = 0; index < visibleText.Length; index++)
        {
            int codePoint = char.ConvertToUtf32(visibleText, index);
            if (char.IsHighSurrogate(visibleText[index])) index++;

            string character = char.ConvertFromUtf32(codePoint);
            UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character, 0);

            if (category == UnicodeCategory.Control ||
                category == UnicodeCategory.Format ||
                category == UnicodeCategory.LineSeparator ||
                category == UnicodeCategory.ParagraphSeparator ||
                category == UnicodeCategory.Surrogate)
            {
                continue;
            }

            _codePoints.Add(codePoint);
        }
    }

    private static string BuildCharacterText(SortedSet<int> _codePoints)
    {
        StringBuilder builder = new StringBuilder(_codePoints.Count);

        foreach (int codePoint in _codePoints)
        {
            builder.Append(char.ConvertFromUtf32(codePoint));
        }

        return builder.ToString();
    }

    private static bool WriteIfChanged(string _path, string _contents)
    {
        if (File.Exists(_path))
        {
            string previousContents = File.ReadAllText(_path, Encoding.UTF8);
            if (string.Equals(previousContents, _contents, StringComparison.Ordinal)) return false;
        }

        File.WriteAllText(_path, _contents, Utf8WithoutBom);
        return true;
    }

    private sealed class LanguageDefinition
    {
        public readonly string Label;
        public readonly string FileName;
        public readonly string SafetyCharacters;

        /// <summary>이 폰트가 담당하는 언어 열들입니다. 여러 언어가 한 폰트를 쓰면 둘 이상이 됩니다.</summary>
        public readonly Func<LocalizationEntry, string>[] SelectTexts;

        public LanguageDefinition(
            string _label,
            string _fileName,
            string _safetyCharacters,
            params Func<LocalizationEntry, string>[] _selectTexts)
        {
            Label = _label;
            FileName = _fileName;
            SafetyCharacters = _safetyCharacters;
            SelectTexts = _selectTexts;
        }
    }
}
