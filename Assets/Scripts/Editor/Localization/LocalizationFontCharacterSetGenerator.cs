using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class LocalizationFontCharacterSetGenerator
{
    private const string JsonPath = "Assets/Resources/Localization";
    private const string ExportDirectory = "Assets/TextMesh Pro/Font Character Sets";

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private static readonly Regex RichTextTagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex CompositeFormatRegex = new Regex(@"\{\d+(?:[^{}]*)\}", RegexOptions.Compiled);

    private static readonly LanguageDefinition[] Languages =
    {
        new LanguageDefinition(
            "JA",
            "FusionPixel_JA_Characters.txt",
            entry => entry.ja,
            "、。！？「」『』（）［］【】・ー〜…※"),
        new LanguageDefinition(
            "ZH_HANS",
            "FusionPixel_ZH_HANS_Characters.txt",
            entry => entry.zhHans,
            "，。！？：；（）【】《》、“”‘’…—·"),
        new LanguageDefinition(
            "ZH_HANT",
            "FusionPixel_ZH_HANT_Characters.txt",
            entry => entry.zhHant,
            "，。！？：；（）【】《》、「」『』“”‘’…—·")
    };

    [MenuItem("Tools/Localization/Generate Font Character Sets")]
    public static void GenerateFromMenu()
    {
        GenerateAll(true);
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
                    string localizedText = language.SelectText(entry);
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
        public readonly Func<LocalizationEntry, string> SelectText;
        public readonly string SafetyCharacters;

        public LanguageDefinition(
            string _label,
            string _fileName,
            Func<LocalizationEntry, string> _selectText,
            string _safetyCharacters)
        {
            Label = _label;
            FileName = _fileName;
            SelectText = _selectText;
            SafetyCharacters = _safetyCharacters;
        }
    }
}
