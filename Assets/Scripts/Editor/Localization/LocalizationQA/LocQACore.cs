using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LocalizationQA
{
    // //문제 분류
    internal enum LocQAKind
    {
        Overflow = 0,
        SlightOverflow = 1,
        Truncated = 2,
        MissingGlyph = 3,
        WordBreak = 4,
        ParentOverflow = 5,
        OffScreen = 6,
        AutoWrap = 7,
        Untranslated = 8,
        EmptyText = 9
    }

    internal enum LocQASeverity
    {
        Error,
        Warning,
        Info
    }

    internal enum LocQASource
    {
        Play,
        Prefab,
        String
    }

    internal static class LocQAKinds
    {
        // LocQAKind 순서와 반드시 같아야 한다. (MaskField 비트 위치로도 쓰인다)
        public static readonly string[] Names =
        {
            "칸 초과",
            "글자 살짝 넘침",
            "잘림·말줄임",
            "글리프 없음(□)",
            "단어 중간 끊김",
            "배경 영역 초과",
            "화면 밖",
            "자동 줄바꿈",
            "번역 없음(폴백)",
            "빈 문구"
        };

        public static readonly string[] SourceNames = { "플레이", "프리팹", "문구" };

        public static int Count => Names.Length;

        /// <summary>번역 누락은 수천 건이 나올 수 있어 기본으로는 숨긴다.</summary>
        public static int DefaultMask => ((1 << Count) - 1) & ~(1 << (int)LocQAKind.Untranslated);

        public static string Name(LocQAKind _kind) => Names[(int)_kind];

        public static LocQASeverity Severity(LocQAKind _kind)
        {
            switch (_kind)
            {
                case LocQAKind.Overflow:
                case LocQAKind.Truncated:
                case LocQAKind.MissingGlyph:
                case LocQAKind.EmptyText:
                    return LocQASeverity.Error;
                case LocQAKind.AutoWrap:
                case LocQAKind.Untranslated:
                    return LocQASeverity.Info;
                default:
                    return LocQASeverity.Warning;
            }
        }

        public static Color SeverityColor(LocQASeverity _severity)
        {
            switch (_severity)
            {
                case LocQASeverity.Error: return new Color(0.93f, 0.26f, 0.22f);
                case LocQASeverity.Warning: return new Color(0.98f, 0.62f, 0.12f);
                default: return new Color(0.35f, 0.62f, 0.95f);
            }
        }
    }

    // //언어 (LocalizationManager.ResolveText와 같은 규칙)
    internal static class LocQALanguages
    {
        public static readonly Language[] All = (Language[])Enum.GetValues(typeof(Language));

        /// <summary>원문 언어. "원본에서도 생기는 문제"를 가릴 때 기준이 된다.</summary>
        public const Language SOURCE = Language.KR;

        public static string Name(Language _lang)
        {
            switch (_lang)
            {
                case Language.KR: return "한국어";
                case Language.EN: return "영어";
                case Language.ZH_HANS: return "중국어 간체";
                case Language.ZH_HANT: return "중국어 번체";
                case Language.JA: return "일본어";
                case Language.DE: return "독일어";
                case Language.FR: return "프랑스어";
                case Language.PT: return "포르투갈어";
                case Language.ES: return "스페인어";
                case Language.RU: return "러시아어";
                case Language.PL: return "폴란드어";
                case Language.TR: return "튀르키예어";
                case Language.ES_LATAM: return "중남미 스페인어";
                case Language.IT: return "이탈리아어";
                case Language.UK: return "우크라이나어";
                case Language.CS: return "체코어";
                case Language.ID: return "인도네시아어";
                case Language.VI: return "베트남어";
                default: return _lang.ToString();
            }
        }

        /// <summary>폴백 없이 해당 언어 열의 값을 그대로 돌려준다.</summary>
        public static string Raw(in LocalizationEntry _entry, Language _lang)
        {
            switch (_lang)
            {
                case Language.KR: return _entry.kr;
                case Language.EN: return _entry.en;
                case Language.ZH_HANS: return _entry.zhHans;
                case Language.ZH_HANT: return _entry.zhHant;
                case Language.JA: return _entry.ja;
                case Language.DE: return _entry.de;
                case Language.FR: return _entry.fr;
                case Language.PT: return _entry.pt;
                case Language.ES: return _entry.es;
                case Language.RU: return _entry.ru;
                case Language.PL: return _entry.pl;
                case Language.TR: return _entry.tr;
                case Language.ES_LATAM: return _entry.esLatam;
                case Language.IT: return _entry.it;
                case Language.UK: return _entry.uk;
                case Language.CS: return _entry.cs;
                case Language.ID: return _entry.ind;
                case Language.VI: return _entry.vi;
                default: return _entry.en;
            }
        }

        /// <summary>
        /// 게임이 실제로 띄우는 문자열. LocalizationManager.ResolveText와 같은 폴백 규칙을 따른다.
        /// (KR은 폴백 없음, ES_LATAM은 es → en, 나머지는 en)
        /// </summary>
        public static string Resolve(in LocalizationEntry _entry, Language _lang)
        {
            switch (_lang)
            {
                case Language.KR: return _entry.kr;
                case Language.EN: return _entry.en;
                case Language.ES_LATAM:
                    if (false == string.IsNullOrEmpty(_entry.esLatam)) return _entry.esLatam;
                    return string.IsNullOrEmpty(_entry.es) ? _entry.en : _entry.es;
                default:
                    string _raw = Raw(_entry, _lang);
                    return string.IsNullOrEmpty(_raw) ? _entry.en : _raw;
            }
        }

        public static bool IsFallback(in LocalizationEntry _entry, Language _lang)
        {
            if (Language.KR == _lang || Language.EN == _lang) return false;
            return string.IsNullOrEmpty(Raw(_entry, _lang));
        }

        public static EOptionLanguage ToOption(Language _lang)
        {
            switch (_lang)
            {
                case Language.KR: return EOptionLanguage.Korean;
                case Language.EN: return EOptionLanguage.English;
                case Language.ZH_HANS: return EOptionLanguage.ChineseSimplified;
                case Language.ZH_HANT: return EOptionLanguage.ChineseTraditional;
                case Language.JA: return EOptionLanguage.Japanese;
                case Language.DE: return EOptionLanguage.German;
                case Language.FR: return EOptionLanguage.French;
                case Language.PT: return EOptionLanguage.Portuguese;
                case Language.ES: return EOptionLanguage.Spanish;
                case Language.RU: return EOptionLanguage.Russian;
                case Language.PL: return EOptionLanguage.Polish;
                case Language.TR: return EOptionLanguage.Turkish;
                case Language.ES_LATAM: return EOptionLanguage.SpanishLatAm;
                case Language.IT: return EOptionLanguage.Italian;
                case Language.UK: return EOptionLanguage.Ukrainian;
                case Language.CS: return EOptionLanguage.Czech;
                case Language.ID: return EOptionLanguage.Indonesian;
                case Language.VI: return EOptionLanguage.Vietnamese;
                default: return EOptionLanguage.English;
            }
        }
    }

    // //문자열 테이블
    internal sealed class LocQAEntry
    {
        public string file;
        public int jsonId;
        public LocalizationEntry data;
        public string id;          // "파일명/키". 키가 비어 있으면 "파일명/#항목id"
        public bool hasFormat;
    }

    internal sealed class LocQAStringTable
    {
        public const string JSON_FOLDER = "Assets/Resources/Localization";

        private static readonly Regex FormatRegex = new Regex(@"\{\d+(?::[^{}]*)?\}", RegexOptions.Compiled);
        private static readonly Regex TagRegex = new Regex(@"<[^>]+>", RegexOptions.Compiled);

        public readonly List<LocQAEntry> Entries = new List<LocQAEntry>(512);

        private readonly Dictionary<string, LocQAEntry> byId = new Dictionary<string, LocQAEntry>(512, StringComparer.Ordinal);
        private readonly Dictionary<string, List<LocQAEntry>> byAnyText = new Dictionary<string, List<LocQAEntry>>(2048, StringComparer.Ordinal);
        private readonly Dictionary<Language, Dictionary<string, LocQAEntry>> byLangText = new Dictionary<Language, Dictionary<string, LocQAEntry>>();
        private readonly Dictionary<Language, List<KeyValuePair<Regex, LocQAEntry>>> byLangFormat = new Dictionary<Language, List<KeyValuePair<Regex, LocQAEntry>>>();

        private static readonly List<LocQAEntry> EmptyList = new List<LocQAEntry>(0);

        public static LocQAStringTable Load()
        {
            LocQAStringTable _table = new LocQAStringTable();

            string[] _guids = AssetDatabase.FindAssets("t:TextAsset", new[] { JSON_FOLDER });
            List<string> _paths = new List<string>(_guids.Length);
            for (int i = 0; i < _guids.Length; i++)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_guids[i]);
                if (_path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) _paths.Add(_path);
            }
            _paths.Sort(StringComparer.Ordinal);

            for (int i = 0; i < _paths.Count; i++)
            {
                TextAsset _asset = AssetDatabase.LoadAssetAtPath<TextAsset>(_paths[i]);
                if (null == _asset) continue;

                LocalizationDataJson _data;
                try
                {
                    _data = JsonUtility.FromJson<LocalizationDataJson>(_asset.text);
                }
                catch (Exception _e)
                {
                    Debug.LogWarning($"[LocalizationQA] {_paths[i]} 파싱 실패: {_e.Message}");
                    continue;
                }
                if (null == _data || null == _data.entries) continue;

                for (int j = 0; j < _data.entries.Length; j++)
                {
                    LocalizationEntry _e = _data.entries[j];
                    if (0 == _e.id) continue;   // 런타임도 id 0은 건너뛴다.

                    LocQAEntry _entry = new LocQAEntry
                    {
                        file = _asset.name,
                        jsonId = _data.jsonId,
                        data = _e,
                        id = _asset.name + "/" + (string.IsNullOrEmpty(_e.key) ? "#" + _e.id : _e.key),
                    };
                    _entry.hasFormat = FormatRegex.IsMatch(_e.kr ?? string.Empty) || FormatRegex.IsMatch(_e.en ?? string.Empty);

                    if (true == _table.byId.ContainsKey(_entry.id)) continue;
                    _table.byId.Add(_entry.id, _entry);
                    _table.Entries.Add(_entry);

                    for (int k = 0; k < LocQALanguages.All.Length; k++)
                    {
                        string _text = Normalize(LocQALanguages.Raw(_e, LocQALanguages.All[k]));
                        if (0 == _text.Length) continue;

                        if (false == _table.byAnyText.TryGetValue(_text, out List<LocQAEntry> _list))
                        {
                            _list = new List<LocQAEntry>(2);
                            _table.byAnyText.Add(_text, _list);
                        }
                        if (false == _list.Contains(_entry)) _list.Add(_entry);
                    }
                }
            }

            return _table;
        }

        public LocQAEntry Find(string _id)
        {
            if (string.IsNullOrEmpty(_id)) return null;
            byId.TryGetValue(_id, out LocQAEntry _entry);
            return _entry;
        }

        /// <summary>어느 언어로든 이 문구와 정확히 같은 항목들. (프리팹에 적힌 기본 문구로 키를 추론할 때 쓴다)</summary>
        public List<LocQAEntry> MatchAnyLanguage(string _text)
        {
            string _key = Normalize(_text);
            if (0 == _key.Length) return EmptyList;
            return byAnyText.TryGetValue(_key, out List<LocQAEntry> _list) ? _list : EmptyList;
        }

        /// <summary>
        /// 화면에 표시된 문구가 어떤 항목인지 역추적한다. 정확히 같은 문구를 먼저 찾고,
        /// 없으면 {0} 같은 자리표시자가 있는 항목을 패턴으로 대조한다.
        /// </summary>
        public LocQAEntry MatchDisplayed(string _text, Language _lang)
        {
            string _key = Normalize(_text);
            if (0 == _key.Length) return null;

            EnsureLanguageIndex(_lang);

            if (true == byLangText[_lang].TryGetValue(_key, out LocQAEntry _entry)) return _entry;

            List<KeyValuePair<Regex, LocQAEntry>> _patterns = byLangFormat[_lang];
            for (int i = 0; i < _patterns.Count; i++)
            {
                if (true == _patterns[i].Key.IsMatch(_key)) return _patterns[i].Value;
            }
            return null;
        }

        /// <summary>
        /// 코드가 문구 앞뒤에 숫자·기호·태그만 붙여 표시한 경우(예: "레벨 : 1 / 5", 색 태그로 감싼 "무료")의 원래 항목.
        /// 붙은 부분에 글자가 하나라도 있으면 다른 문구와 섞인 것이라 보고 맞추지 않는다.
        /// 찾으면 그 모양을 "{text}" 자리표시자로 돌려준다.
        /// </summary>
        public LocQAEntry MatchContained(string _text, Language _lang, out string _template)
        {
            _template = null;
            string _key = Normalize(_text);
            if (_key.Length < 2) return null;

            EnsureLanguageIndex(_lang);

            LocQAEntry _best = null;
            string _bestText = null;
            int _bestIndex = -1;
            foreach (KeyValuePair<string, LocQAEntry> _pair in byLangText[_lang])
            {
                string _candidate = _pair.Key;
                if (_candidate.Length < 2 || _candidate.Length >= _key.Length) continue;
                if (null != _bestText && _candidate.Length <= _bestText.Length) continue;

                int _index = _key.IndexOf(_candidate, StringComparison.Ordinal);
                if (_index < 0) continue;

                string _rest = TagRegex.Replace(_key.Remove(_index, _candidate.Length), string.Empty);
                bool _hasLetter = false;
                for (int i = 0; i < _rest.Length && false == _hasLetter; i++) _hasLetter = char.IsLetter(_rest[i]);
                if (true == _hasLetter) continue;

                _best = _pair.Value;
                _bestText = _candidate;
                _bestIndex = _index;
            }

            if (null == _best) return null;
            _template = _key.Substring(0, _bestIndex) + "{text}" + _key.Substring(_bestIndex + _bestText.Length);
            return _best;
        }

        private void EnsureLanguageIndex(Language _lang)
        {
            if (true == byLangText.ContainsKey(_lang)) return;

            Dictionary<string, LocQAEntry> _texts = new Dictionary<string, LocQAEntry>(Entries.Count, StringComparer.Ordinal);
            List<KeyValuePair<Regex, LocQAEntry>> _patterns = new List<KeyValuePair<Regex, LocQAEntry>>(32);

            for (int i = 0; i < Entries.Count; i++)
            {
                LocQAEntry _entry = Entries[i];
                string _text = Normalize(LocQALanguages.Resolve(_entry.data, _lang));
                if (0 == _text.Length) continue;

                if (true == FormatRegex.IsMatch(_text))
                {
                    string[] _parts = FormatRegex.Split(_text);
                    for (int p = 0; p < _parts.Length; p++) _parts[p] = Regex.Escape(_parts[p]);
                    Regex _regex = new Regex("^" + string.Join("(.*?)", _parts) + "$", RegexOptions.Singleline);
                    _patterns.Add(new KeyValuePair<Regex, LocQAEntry>(_regex, _entry));
                }
                else if (false == _texts.ContainsKey(_text))
                {
                    _texts.Add(_text, _entry);
                }
            }

            byLangText[_lang] = _texts;
            byLangFormat[_lang] = _patterns;
        }

        /// <summary>줄바꿈 표기 차이(\r\n, 문자 그대로의 \n)와 앞뒤 공백을 없앤 비교용 문자열.</summary>
        public static string Normalize(string _text)
        {
            if (string.IsNullOrEmpty(_text)) return string.Empty;
            return _text.Replace("\r\n", "\n").Replace("\\n", "\n").Trim();
        }

        /// <summary>{0}, {1:N0} 같은 자리표시자를 견본 값으로 채운다. 실제 수치가 들어간 길이로 재기 위해서다.</summary>
        public static string FillFormat(string _text, string _sample)
        {
            if (string.IsNullOrEmpty(_text)) return _text ?? string.Empty;
            return FormatRegex.Replace(_text, _sample ?? string.Empty);
        }

        /// <summary>{0}, {1} … 자리에 문구별 견본 값("|" 구분)을 넣는다. 값이 모자란 자리는 기본 견본 값으로 채운다.</summary>
        public static string FillFormat(string _text, string _samples, string _fallback)
        {
            if (string.IsNullOrEmpty(_samples)) return FillFormat(_text, _fallback);
            if (string.IsNullOrEmpty(_text)) return _text ?? string.Empty;

            string[] _values = _samples.Split('|');
            return FormatRegex.Replace(_text, _m =>
            {
                string _token = _m.Value;
                int _end = _token.IndexOf(':');
                if (_end < 0) _end = _token.Length - 1;
                if (true == int.TryParse(_token.Substring(1, _end - 1), out int _index) && _index >= 0 && _index < _values.Length)
                {
                    return _values[_index];
                }
                return _fallback ?? string.Empty;
            });
        }

        /// <summary>글리프 검사용. 리치 텍스트 태그와 자리표시자를 걷어낸다.</summary>
        public static string StripForGlyphCheck(string _text)
        {
            if (string.IsNullOrEmpty(_text)) return string.Empty;
            string _stripped = TagRegex.Replace(_text.Replace("\\n", "\n").Replace("\\t", "\t"), string.Empty);
            return FormatRegex.Replace(_stripped, string.Empty);
        }
    }

    // //폰트 (FontLocalizer와 같은 규칙으로 언어별 폰트를 고른다)
    internal sealed class LocQAFontResolver : IDisposable
    {
        private static readonly int ID_MainTex = Shader.PropertyToID("_MainTex");
        private static readonly int ID_GradientScale = Shader.PropertyToID("_GradientScale");

        private readonly LocalizationFontTable table;
        private readonly Dictionary<(Material, TMP_FontAsset), Material> derivedMaterials = new Dictionary<(Material, TMP_FontAsset), Material>();

        private LocQAFontResolver(LocalizationFontTable _table)
        {
            table = _table;
        }

        public LocalizationFontTable Table => table;

        public static LocQAFontResolver Load()
        {
            LocalizationFontTable _table = null;
            string[] _guids = AssetDatabase.FindAssets("t:LocalizationFontTable");
            if (_guids.Length > 0)
            {
                _table = AssetDatabase.LoadAssetAtPath<LocalizationFontTable>(AssetDatabase.GUIDToAssetPath(_guids[0]));
            }
            if (null == _table)
            {
                Debug.LogWarning("[LocalizationQA] LocalizationFontTable 에셋을 찾지 못해 언어별 폰트 교체 없이 검사합니다.");
            }
            return new LocQAFontResolver(_table);
        }

        /// <summary>해당 언어에서 이 텍스트가 실제로 쓰게 될 폰트. (FontLocalizer.Apply와 같은 판단)</summary>
        public TMP_FontAsset Resolve(TMP_FontAsset _original, Material _originalMaterial, Language _lang)
        {
            if (null == _original || null == table) return _original;
            if (true == table.IsExcluded(_original)) return _original;

            TMP_FontAsset _target = table.GetFont(_lang);
            if (null == _target) return _original;

            // 렌더 모드가 다르면 런타임도 교체를 포기하고 원본을 유지한다.
            if (null != _originalMaterial && null != _target.material
                && _originalMaterial.HasProperty(ID_GradientScale) != _target.material.HasProperty(ID_GradientScale))
            {
                return _original;
            }
            return _target;
        }

        /// <summary>미리보기 렌더링용. 원본 머티리얼 설정은 두고 아틀라스만 바꾼 머티리얼.</summary>
        public Material ResolveMaterial(Material _source, TMP_FontAsset _target, TMP_FontAsset _original)
        {
            if (_target == _original) return _source;
            if (null == _target) return _source;
            if (null == _source || null == _target.atlasTexture) return _target.material;

            if (true == derivedMaterials.TryGetValue((_source, _target), out Material _cached) && null != _cached) return _cached;

            Material _derived = new Material(_source);
            _derived.hideFlags = HideFlags.HideAndDontSave;
            _derived.SetTexture(ID_MainTex, _target.atlasTexture);
            derivedMaterials[(_source, _target)] = _derived;
            return _derived;
        }

        public void Dispose()
        {
            foreach (KeyValuePair<(Material, TMP_FontAsset), Material> _pair in derivedMaterials)
            {
                if (null != _pair.Value) UnityEngine.Object.DestroyImmediate(_pair.Value);
            }
            derivedMaterials.Clear();
        }

        /// <summary>
        /// 이 폰트(폴백 포함)로 그릴 수 없는 글자들을 모은다.
        /// 동적 아틀라스는 아직 굽지 않은 글자도 원본 TTF에 있으면 런타임에 채워지므로 있는 것으로 본다.
        /// </summary>
        public static void CollectMissing(TMP_FontAsset _font, string _text, List<string> _missing)
        {
            if (null == _font || string.IsNullOrEmpty(_text)) return;

            HashSet<TMP_FontAsset> _visited = new HashSet<TMP_FontAsset>();

            for (int i = 0; i < _text.Length; i++)
            {
                char _c = _text[i];
                uint _codePoint = _c;

                if (true == char.IsHighSurrogate(_c) && i + 1 < _text.Length && true == char.IsLowSurrogate(_text[i + 1]))
                {
                    _codePoint = (uint)char.ConvertToUtf32(_c, _text[i + 1]);
                    i++;
                }
                else if (true == char.IsWhiteSpace(_c) || true == char.IsControl(_c) || 0x200B == _c || 0xFEFF == _c || 0xAD == _c)
                {
                    continue;
                }

                _visited.Clear();
                if (true == HasGlyph(_font, _codePoint, _visited)) continue;

                bool _foundInGlobal = false;
                List<TMP_FontAsset> _global = TMP_Settings.fallbackFontAssets;
                if (null != _global)
                {
                    for (int g = 0; g < _global.Count && false == _foundInGlobal; g++)
                    {
                        _foundInGlobal = HasGlyph(_global[g], _codePoint, _visited);
                    }
                }
                if (true == _foundInGlobal) continue;

                string _str = char.ConvertFromUtf32((int)_codePoint);
                if (false == _missing.Contains(_str)) _missing.Add(_str);
            }
        }

        private static bool HasGlyph(TMP_FontAsset _font, uint _codePoint, HashSet<TMP_FontAsset> _visited)
        {
            if (null == _font || false == _visited.Add(_font)) return false;

            Dictionary<uint, TMP_Character> _lookup = _font.characterLookupTable;
            if (null != _lookup && true == _lookup.ContainsKey(_codePoint)) return true;

            if (AtlasPopulationMode.Static != _font.atlasPopulationMode && null != _font.sourceFontFile
                && _codePoint <= 0xFFFF && true == _font.sourceFontFile.HasCharacter((char)_codePoint))
            {
                return true;
            }

            List<TMP_FontAsset> _fallbacks = _font.fallbackFontAssetTable;
            if (null != _fallbacks)
            {
                for (int i = 0; i < _fallbacks.Count; i++)
                {
                    if (true == HasGlyph(_fallbacks[i], _codePoint, _visited)) return true;
                }
            }
            return false;
        }

        /// <summary>문구 검사에서 "원본 폰트"로 쓸 UI 기본 폰트. 프로젝트의 모든 UI가 Galmuri11_Optimum으로 제작되어 있다.</summary>
        public static TMP_FontAsset FindDefaultUiFont()
        {
            string[] _guids = AssetDatabase.FindAssets("Galmuri11_Optimum t:TMP_FontAsset");
            for (int i = 0; i < _guids.Length; i++)
            {
                TMP_FontAsset _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(_guids[i]));
                if (null != _font && "Galmuri11_Optimum" == _font.name) return _font;
            }
            return TMP_Settings.defaultFontAsset;
        }
    }

    // //설정
    [Serializable]
    internal sealed class LocQASettings
    {
        private const string PREF_KEY = "LumberBoy.LocalizationQA.Settings";

        public int languageMask = -1;
        public float tolerance = 1f;
        public string formatSample = "999";
        public bool checkParent = true;
        public bool checkScreen = true;
        public bool checkAutoWrap = true;
        public bool checkWordBreak = true;
        public bool checkGlyph = true;
        public bool onlyVisible = true;
        public bool showOverlay = true;
        public bool captureScreenshots = true;
        public float captureDelay = 0.25f;
        public float autoScanInterval = 0.5f;
        public string prefabFolder = "Assets/Prefabs/UI";
        public string defaultUiFontGuid = "";
        public int resolutionIndex = -1;   // LocQAResolution.All의 인덱스. -1이면 기본(1920×1080)

        public int ResolutionIndex => resolutionIndex < 0 ? LocQAResolution.DefaultIndex : Mathf.Clamp(resolutionIndex, 0, LocQAResolution.All.Count - 1);
        public LocQAResolution.Preset Resolution => LocQAResolution.Get(ResolutionIndex);
        public Vector2 CanvasSize => Resolution.CanvasSize;

        public bool IsOn(Language _lang) => 0 != (languageMask & (1 << (int)_lang));

        public void SetOn(Language _lang, bool _on)
        {
            if (true == _on) languageMask |= 1 << (int)_lang;
            else languageMask &= ~(1 << (int)_lang);
        }

        public List<Language> SelectedLanguages()
        {
            List<Language> _list = new List<Language>(LocQALanguages.All.Length);
            for (int i = 0; i < LocQALanguages.All.Length; i++)
            {
                if (true == IsOn(LocQALanguages.All[i])) _list.Add(LocQALanguages.All[i]);
            }
            return _list;
        }

        public static LocQASettings Load()
        {
            LocQASettings _settings = new LocQASettings();
            string _json = EditorPrefs.GetString(PREF_KEY, string.Empty);
            if (false == string.IsNullOrEmpty(_json))
            {
                try { JsonUtility.FromJsonOverwrite(_json, _settings); }
                catch (Exception) { /* 손상된 값이면 기본값으로 시작한다 */ }
            }
            return _settings;
        }

        public void Save()
        {
            EditorPrefs.SetString(PREF_KEY, JsonUtility.ToJson(this));
        }
    }

    // //결과
    [Serializable]
    internal sealed class LocQAIssue
    {
        public LocQASource source;
        public Language language;
        public LocQAKind kind;
        public string location;     // 씬 이름 / 프리팹 경로 / JSON 파일
        public string objectPath;   // 계층 경로 (문구 검사는 키)
        public string text;
        public string key;
        public string detail;
        public int lineCount;
        public UnityEngine.Object target;   // 플레이 검사에서만. 플레이가 끝나면 null이 된다.
        public string capturePath;
        public bool hasUv;
        public Rect boxUv;          // 0~1, 좌하단 원점
        public Rect glyphUv;

        public LocQASeverity Severity => LocQAKinds.Severity(kind);
        public string ObjectKey => location + "|" + objectPath;
    }

    [Serializable]
    internal sealed class LocQAUnbound
    {
        public string prefabPath;
        public string prefabGuid;
        public string objectPath;
        public string text;
        public bool ignored;
    }

    [Serializable]
    internal sealed class LocQACapture
    {
        public string folder;
        public string label;
        public List<int> languages = new List<int>();
        public List<string> files = new List<string>();

        public string FileFor(Language _lang)
        {
            int _index = languages.IndexOf((int)_lang);
            return _index >= 0 ? files[_index] : null;
        }
    }

    /// <summary>
    /// 검사 결과 저장소. 플레이 모드 진입 시의 도메인 리로드에도 살아남아야 해서
    /// ScriptableSingleton으로 둔다. (디스크에는 쓰지 않는다)
    /// </summary>
    internal sealed class LocQAStore : ScriptableSingleton<LocQAStore>
    {
        public List<LocQAIssue> issues = new List<LocQAIssue>(1024);
        public List<LocQAUnbound> unbound = new List<LocQAUnbound>(256);
        public List<string> coveredKeys = new List<string>(512);
        public List<LocQACapture> captures = new List<LocQACapture>();
        public string prefabSummary = string.Empty;
        public string stringSummary = string.Empty;
        public string playSummary = string.Empty;

        [NonSerialized] private HashSet<string> coveredSet;
        [NonSerialized] private Dictionary<string, int> sourceIssueLines;
        [NonSerialized] private int version;

        public static event Action Changed;

        public int Version => version;
        public int CoveredCount => CoveredSet.Count;

        private HashSet<string> CoveredSet
        {
            get
            {
                if (null == coveredSet) coveredSet = new HashSet<string>(coveredKeys, StringComparer.Ordinal);
                return coveredSet;
            }
        }

        public void NotifyChanged()
        {
            version++;
            sourceIssueLines = null;
            Changed?.Invoke();
        }

        public void Cover(string _id)
        {
            if (string.IsNullOrEmpty(_id)) return;
            if (true == CoveredSet.Add(_id)) coveredKeys.Add(_id);
        }

        public bool IsCovered(string _id) => CoveredSet.Contains(_id);

        public void ClearCoverage()
        {
            coveredKeys.Clear();
            coveredSet = null;
        }

        /// <summary>
        /// 같은 오브젝트가 원문(한국어)에서도 같은 종류의 문제를 갖는지.
        /// 자동 줄바꿈은 원문보다 줄 수가 늘어난 경우만 로컬라이징 문제로 본다.
        /// </summary>
        public bool IsAlsoInSource(LocQAIssue _issue)
        {
            if (LocQALanguages.SOURCE == _issue.language || LocQASource.String == _issue.source) return false;

            if (null == sourceIssueLines)
            {
                sourceIssueLines = new Dictionary<string, int>(256, StringComparer.Ordinal);
                for (int i = 0; i < issues.Count; i++)
                {
                    LocQAIssue _src = issues[i];
                    if (LocQALanguages.SOURCE != _src.language) continue;

                    string _key = SourceKey(_src);
                    sourceIssueLines.TryGetValue(_key, out int _lines);
                    sourceIssueLines[_key] = Mathf.Max(_lines, _src.lineCount);
                }
            }

            if (false == sourceIssueLines.TryGetValue(SourceKey(_issue), out int _sourceLines)) return false;
            if (LocQAKind.AutoWrap == _issue.kind) return _issue.lineCount <= _sourceLines;
            return true;
        }

        private static string SourceKey(LocQAIssue _issue) => (int)_issue.source + "|" + (int)_issue.kind + "|" + _issue.ObjectKey;

        public void ClearAll()
        {
            issues.Clear();
            unbound.Clear();
            captures.Clear();
            ClearCoverage();
            prefabSummary = stringSummary = playSummary = string.Empty;
            NotifyChanged();
        }

        public void Clear(LocQASource _source)
        {
            issues.RemoveAll(i => i.source == _source);
            if (LocQASource.Prefab == _source)
            {
                unbound.Clear();
                prefabSummary = string.Empty;
            }
            else if (LocQASource.Play == _source)
            {
                captures.Clear();
                playSummary = string.Empty;
            }
            else
            {
                stringSummary = string.Empty;
            }
            NotifyChanged();
        }
    }

    // //계층 경로
    internal static class LocQAPaths
    {
        private static readonly Regex IndexedSegment = new Regex(@"^(.*)\[(\d+)\]$", RegexOptions.Compiled);

        /// <summary>root 아래 경로. root가 null이면 씬 최상위부터. 같은 이름의 형제가 있으면 "이름[n]"으로 구분한다.</summary>
        public static string GetPath(Transform _target, Transform _root)
        {
            List<string> _parts = new List<string>(8);
            for (Transform _cur = _target; null != _cur && _cur != _root; _cur = _cur.parent)
            {
                _parts.Add(Segment(_cur));
            }
            _parts.Reverse();
            return string.Join("/", _parts);
        }

        private static string Segment(Transform _t)
        {
            Transform _parent = _t.parent;
            if (null == _parent) return _t.name;

            int _same = 0;
            int _index = 0;
            for (int i = 0; i < _parent.childCount; i++)
            {
                Transform _child = _parent.GetChild(i);
                if (_child.name != _t.name) continue;
                if (_child == _t) _index = _same;
                _same++;
            }
            return _same > 1 ? _t.name + "[" + _index + "]" : _t.name;
        }

        public static Transform Find(Transform _root, string _path)
        {
            if (null == _root) return null;
            if (string.IsNullOrEmpty(_path)) return _root;

            Transform _cur = _root;
            string[] _parts = _path.Split('/');
            for (int p = 0; p < _parts.Length && null != _cur; p++)
            {
                string _name = _parts[p];
                int _wanted = 0;
                Match _match = IndexedSegment.Match(_name);
                if (true == _match.Success)
                {
                    _name = _match.Groups[1].Value;
                    _wanted = int.Parse(_match.Groups[2].Value);
                }

                Transform _next = null;
                int _seen = 0;
                for (int i = 0; i < _cur.childCount; i++)
                {
                    Transform _child = _cur.GetChild(i);
                    if (_child.name != _name) continue;
                    if (_seen == _wanted) { _next = _child; break; }
                    _seen++;
                }

                // "[n]"이 실제 이름의 일부였던 경우를 위해 원래 이름으로 한 번 더 찾는다.
                if (null == _next && true == _match.Success) _next = _cur.Find(_parts[p]);
                _cur = _next;
            }
            return _cur;
        }

        public static string ShortLocation(string _location)
        {
            if (string.IsNullOrEmpty(_location)) return string.Empty;
            int _slash = _location.LastIndexOf('/');
            string _name = _slash >= 0 ? _location.Substring(_slash + 1) : _location;
            if (_name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) _name = _name.Substring(0, _name.Length - 7);
            return _name;
        }
    }
}
