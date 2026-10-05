using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;

namespace LocalizationQA
{
    internal sealed class LocQAMeasurement
    {
        public Rect box;                // 텍스트 RectTransform 로컬 좌표
        public Rect glyphs;             // 실제로 그려지는 글자들의 외곽 (로컬)
        public bool hasGlyphs;
        public int lineCount;
        public int explicitBreaks;
        public int autoBreaks;
        public readonly Vector3[] boxWorld = new Vector3[4];
        public readonly Vector3[] glyphWorld = new Vector3[4];
        public readonly List<KeyValuePair<LocQAKind, string>> findings = new List<KeyValuePair<LocQAKind, string>>(2);

        public LocQASeverity WorstSeverity
        {
            get
            {
                LocQASeverity _worst = LocQASeverity.Info;
                for (int i = 0; i < findings.Count; i++)
                {
                    LocQASeverity _s = LocQAKinds.Severity(findings[i].Key);
                    if (_s < _worst) _worst = _s;
                }
                return _worst;
            }
        }
    }

    /// <summary>
    /// TMP 텍스트 하나가 "칸 안에 제대로 들어가 있는지"를 잰다.
    ///
    /// 판정은 TMP가 계산해 둔 글자 배치(textInfo)를 그대로 쓴다. 그래서 측정 전에 메시가 최신이어야 한다.
    /// 글자 외곽은 정점 좌표가 아니라 글리프 메트릭으로 계산한다. 정점에는 아틀라스 패딩이 섞여 있어서
    /// 실제 글자보다 몇 픽셀 크게 잡히기 때문이다.
    /// </summary>
    internal static class LocQATextAnalyzer
    {
        private static readonly List<string> missingBuffer = new List<string>(8);
        private static readonly Vector3[] cornerBuffer = new Vector3[4];

        /// <param name="_checkScreen">
        /// 화면 가장자리 검사 여부. 프리팹 검사에서는 끈다. 프리팹은 게임에서처럼 배치되지 않아서
        /// (코드가 위치를 잡는 팝업, 스크롤되는 크레딧 등) 화면 밖 판정이 의미가 없기 때문이다.
        /// </param>
        public static LocQAMeasurement Measure(TMP_Text _text, LocQASettings _settings, bool _forceMeshUpdate, bool _checkScreen)
        {
            if (true == _forceMeshUpdate || true == _text.havePropertiesChanged)
            {
                _text.ForceMeshUpdate(true, true);
            }

            LocQAMeasurement _m = new LocQAMeasurement();
            RectTransform _rt = _text.rectTransform;
            _m.box = _rt.rect;

            TMP_TextInfo _info = _text.textInfo;
            int _count = null == _info ? 0 : Mathf.Min(_info.characterCount, _info.characterInfo.Length);

            float _xMin = float.MaxValue, _yMin = float.MaxValue, _xMax = float.MinValue, _yMax = float.MinValue;
            for (int i = 0; i < _count; i++)
            {
                TMP_CharacterInfo _ci = _info.characterInfo[i];
                if ('\n' == _ci.character) _m.explicitBreaks++;
                if (false == _ci.isVisible) continue;

                GlyphRect(_ci, out float _x0, out float _x1, out float _y0, out float _y1);
                if (_x0 < _xMin) _xMin = _x0;
                if (_x1 > _xMax) _xMax = _x1;
                if (_y0 < _yMin) _yMin = _y0;
                if (_y1 > _yMax) _yMax = _y1;
            }

            _m.hasGlyphs = _xMin <= _xMax && _yMin <= _yMax;
            if (true == _m.hasGlyphs) _m.glyphs = Rect.MinMaxRect(_xMin, _yMin, _xMax, _yMax);

            _m.lineCount = null == _info ? 0 : _info.lineCount;
            _m.autoBreaks = Mathf.Max(0, _m.lineCount - 1 - _m.explicitBreaks);

            ToWorld(_rt, _m.box, _m.boxWorld);
            if (true == _m.hasGlyphs) ToWorld(_rt, _m.glyphs, _m.glyphWorld);

            float _tol = Mathf.Max(0f, _settings.tolerance);
            TextOverflowModes _mode = _text.overflowMode;
            float _lineHeight = (null != _info && _info.lineCount > 0) ? _info.lineInfo[0].lineHeight : 0f;
            if (_lineHeight <= 0f) _lineHeight = _text.fontSize;

            // 1) 자기 칸을 벗어났는가
            //  · 세로: TMP 자신의 판정(줄 높이 기준으로 칸에 다 못 들어간 첫 글자)을 기준으로 하되,
            //    첫 줄만 걸린 경우는 글자가 실제로 반 줄 이상 나갔을 때만 넘침으로 본다.
            //    한 줄짜리 텍스트는 언어별 폰트의 줄 높이(어센더~디센더)가 칸보다 크기만 해도 TMP가 넘침으로
            //    판정하는데, 글자는 칸 안에 그대로 보이므로 문제가 아니다. 'g' 'q' 디센더도 마찬가지다.
            //  · 가로: 줄바꿈이 꺼진 텍스트나 한 단어가 칸보다 긴 경우라 글리프 외곽으로 잰다.
            //  폭이나 높이가 0인 칸은 "크기 제한 없음"으로 쓰는 경우라 그 축은 재지 않는다.
            int _overflowIndex = -1;
            if (true == _m.hasGlyphs)
            {
                float _dx = _m.box.width >= 1f ? Mathf.Max(0f, _m.box.xMin - _m.glyphs.xMin, _m.glyphs.xMax - _m.box.xMax) : 0f;
                float _dy = _m.box.height >= 1f ? Mathf.Max(0f, _m.box.yMin - _m.glyphs.yMin, _m.glyphs.yMax - _m.box.yMax) : 0f;

                int _overflowLine = 0;
                if (_m.box.height >= 1f && TextOverflowModes.Overflow == _mode && _text.firstOverflowCharacterIndex >= 0
                    && null != _info && _text.firstOverflowCharacterIndex < _count)
                {
                    _overflowLine = _info.characterInfo[_text.firstOverflowCharacterIndex].lineNumber + 1;
                }
                bool _verticalOverflow = _overflowLine >= 2 || (_overflowLine >= 1 && _dy >= Mathf.Max(_tol, _lineHeight * 0.5f));

                if (true == _verticalOverflow || _dx > _tol)
                {
                    bool _severe = true == _verticalOverflow || _dx > Mathf.Max(_tol, _text.fontSize * 0.5f);
                    string _where = true == _verticalOverflow ? $" · {_m.lineCount}줄 중 {_overflowLine}번째 줄부터 칸 밖" : string.Empty;

                    string _detail = $"박스 {F(_m.box.width)}×{F(_m.box.height)} · 글자 {F(_m.glyphs.width)}×{F(_m.glyphs.height)} · {_m.lineCount}줄 → {Excess(_dx, _dy)}{_where}";
                    _overflowIndex = _m.findings.Count;
                    _m.findings.Add(new KeyValuePair<LocQAKind, string>(_severe ? LocQAKind.Overflow : LocQAKind.SlightOverflow, _detail));
                }
            }

            // 2) 말줄임·잘라내기·마스킹으로 글자가 사라졌는가
            if ((TextOverflowModes.Ellipsis == _mode || TextOverflowModes.Truncate == _mode || TextOverflowModes.Masking == _mode)
                && (true == _text.isTextTruncated || _text.firstOverflowCharacterIndex >= 0))
            {
                _m.findings.Add(new KeyValuePair<LocQAKind, string>(LocQAKind.Truncated,
                    $"넘침 처리가 '{_mode}'라서 문구 일부가 잘려 보입니다 (박스 {F(_m.box.width)}×{F(_m.box.height)}, {_m.lineCount}줄)"));
            }

            // 3) 폰트에 없는 글자
            if (true == _settings.checkGlyph)
            {
                missingBuffer.Clear();
                LocQAFontResolver.CollectMissing(_text.font, LocQAStringTable.StripForGlyphCheck(_text.text), missingBuffer);
                if (missingBuffer.Count > 0)
                {
                    string _fontName = null != _text.font ? _text.font.name : "(없음)";
                    _m.findings.Add(new KeyValuePair<LocQAKind, string>(LocQAKind.MissingGlyph,
                        $"'{_fontName}'(폴백 포함)에 없는 글자 {missingBuffer.Count}개: {Describe(missingBuffer)}"));
                }
            }

            // 4) 단어가 줄 끝에서 둘로 쪼개졌는가 (라틴·키릴 등 띄어쓰기로 줄을 바꾸는 언어만)
            if (true == _settings.checkWordBreak && null != _info && _m.lineCount > 1)
            {
                string _broken = FindBrokenWord(_info, _count);
                if (null != _broken)
                {
                    _m.findings.Add(new KeyValuePair<LocQAKind, string>(LocQAKind.WordBreak, $"단어가 줄 끝에서 잘렸습니다: \"{_broken}\""));
                }
            }

            // 5) 배경(가장 가까운 부모 그래픽)이나 화면 밖으로 나갔는가
            if (true == _m.hasGlyphs && true == _settings.checkParent)
            {
                RectTransform _container = FindBackground(_rt);
                if (null != _container)
                {
                    Rect _g = ToLocalRect(_container, _m.glyphWorld);
                    Rect _c = _container.rect;
                    float _dx = _c.width >= 1f ? Mathf.Max(0f, _c.xMin - _g.xMin, _g.xMax - _c.xMax) : 0f;
                    float _dy = _c.height >= 1f ? Mathf.Max(0f, _c.yMin - _g.yMin, _g.yMax - _c.yMax) : 0f;

                    if (_dx > _tol || _dy > _tol)
                    {
                        string _detail = $"배경 '{_container.name}'({F(_c.width)}×{F(_c.height)}) 밖으로 {Excess(_dx, _dy)}";
                        if (_overflowIndex >= 0 && LocQAKind.Overflow == _m.findings[_overflowIndex].Key)
                        {
                            // 칸 초과와 같은 원인이므로 한 줄로 합친다.
                            _m.findings[_overflowIndex] = new KeyValuePair<LocQAKind, string>(LocQAKind.Overflow, _m.findings[_overflowIndex].Value + " / " + _detail);
                        }
                        else
                        {
                            _m.findings.Add(new KeyValuePair<LocQAKind, string>(LocQAKind.ParentOverflow, _detail));
                        }
                    }
                }
            }

            if (true == _m.hasGlyphs && true == _checkScreen && true == _settings.checkScreen)
            {
                CheckScreen(_text, _m, _tol);
            }

            // 6) 자동 줄바꿈 (넘치지 않아도 의도와 다르게 줄이 늘어난 것을 찾기 위한 참고 정보)
            if (true == _settings.checkAutoWrap && _m.autoBreaks > 0)
            {
                _m.findings.Add(new KeyValuePair<LocQAKind, string>(LocQAKind.AutoWrap,
                    $"자동 줄바꿈 {_m.autoBreaks}회 → {_m.lineCount}줄 (문구에 직접 넣은 줄바꿈 {_m.explicitBreaks}개)"));
            }

            return _m;
        }

        // //내부 로직
        private static void GlyphRect(in TMP_CharacterInfo _ci, out float _x0, out float _x1, out float _y0, out float _y1)
        {
            Glyph _glyph = (TMP_TextElementType.Character == _ci.elementType && null != _ci.textElement) ? _ci.textElement.glyph : null;

            if (null != _glyph && _glyph.metrics.width > 0f && _ci.scale > 0f)
            {
                GlyphMetrics _gm = _glyph.metrics;
                float _s = _ci.scale;
                _x0 = _ci.origin + _gm.horizontalBearingX * _s;
                _x1 = _x0 + _gm.width * _s;
                _y1 = _ci.baseLine + _gm.horizontalBearingY * _s;
                _y0 = _y1 - _gm.height * _s;
                return;
            }

            _x0 = Mathf.Min(_ci.bottomLeft.x, _ci.topLeft.x);
            _x1 = Mathf.Max(_ci.topRight.x, _ci.bottomRight.x);
            _y0 = Mathf.Min(_ci.bottomLeft.y, _ci.bottomRight.y);
            _y1 = Mathf.Max(_ci.topLeft.y, _ci.topRight.y);
        }

        private static string FindBrokenWord(TMP_TextInfo _info, int _count)
        {
            for (int l = 0; l < _info.lineCount - 1; l++)
            {
                int _last = _info.lineInfo[l].lastCharacterIndex;
                int _next = _info.lineInfo[l + 1].firstCharacterIndex;
                if (_last < 0 || _next != _last + 1 || _next >= _count) continue;

                char _a = _info.characterInfo[_last].character;
                char _b = _info.characterInfo[_next].character;
                if (false == IsSpacedWordChar(_a) || false == IsSpacedWordChar(_b)) continue;

                int _start = _last;
                while (_start > 0 && true == IsSpacedWordChar(_info.characterInfo[_start - 1].character)) _start--;
                int _end = _next;
                while (_end + 1 < _count && true == IsSpacedWordChar(_info.characterInfo[_end + 1].character)) _end++;

                System.Text.StringBuilder _sb = new System.Text.StringBuilder(_end - _start + 2);
                for (int i = _start; i <= _end; i++)
                {
                    _sb.Append(_info.characterInfo[i].character);
                    if (i == _last) _sb.Append('|');
                }
                return _sb.ToString();
            }
            return null;
        }

        /// <summary>띄어쓰기 단위로 줄을 바꾸는 문자. 한중일 글자는 글자 단위 줄바꿈이 정상이므로 제외한다.</summary>
        private static bool IsSpacedWordChar(char _c)
        {
            if (false == char.IsLetterOrDigit(_c)) return false;
            if (_c >= 0x1100 && _c <= 0x11FF) return false;   // 한글 자모
            if (_c >= 0x3040 && _c <= 0x30FF) return false;   // 히라가나·가타카나
            if (_c >= 0x3130 && _c <= 0x318F) return false;   // 한글 호환 자모
            if (_c >= 0x3400 && _c <= 0x9FFF) return false;   // 한자
            if (_c >= 0xAC00 && _c <= 0xD7A3) return false;   // 한글 음절
            if (_c >= 0xF900 && _c <= 0xFAFF) return false;   // 호환 한자
            if (_c >= 0xFF00 && _c <= 0xFFEF) return false;   // 전각
            return true;
        }

        /// <summary>글자 뒤에 깔린 가장 가까운 배경 그래픽(이미지·마스크). 캔버스 루트에서 멈춘다.</summary>
        internal static RectTransform FindBackground(RectTransform _rt)
        {
            for (Transform _t = _rt.parent; null != _t; _t = _t.parent)
            {
                if (true == _t.TryGetComponent(out Canvas _canvas) && true == _canvas.isRootCanvas) return null;

                RectTransform _candidate = _t as RectTransform;
                if (null == _candidate) continue;

                if (true == _t.TryGetComponent(out RectMask2D _mask) && true == _mask.enabled) return _candidate;

                if (true == _t.TryGetComponent(out Graphic _graphic) && true == _graphic.enabled
                    && false == (_graphic is TMP_Text) && _graphic.color.a > 0.01f)
                {
                    return _candidate;
                }
            }
            return null;
        }

        private static void CheckScreen(TMP_Text _text, LocQAMeasurement _m, float _tol)
        {
            if (false == TryGetScreenRect(_text, _m.glyphWorld, out Rect _g)) return;
            Rect _screen = new Rect(0f, 0f, Screen.width, Screen.height);
            // 화면 픽셀 기준이므로 허용치도 캔버스 배율(640×360 기준)에 맞춰 키운다.
            float _tolerance = _tol * Mathf.Max(1f, Screen.height / 360f);

            // 통째로 화면 밖에 있는 텍스트(멀리 있는 말풍선 등)는 로컬라이징 문제가 아니다.
            if (false == _g.Overlaps(_screen)) return;

            float _dx = Mathf.Max(0f, _screen.xMin - _g.xMin, _g.xMax - _screen.xMax);
            float _dy = Mathf.Max(0f, _screen.yMin - _g.yMin, _g.yMax - _screen.yMax);
            if (_dx > _tolerance || _dy > _tolerance)
            {
                _m.findings.Add(new KeyValuePair<LocQAKind, string>(LocQAKind.OffScreen, $"화면 가장자리 밖으로 {Excess(_dx, _dy)}"));
            }
        }

        /// <summary>월드 좌표 네 점을 화면 픽셀 사각형으로. (텍스트가 속한 캔버스의 카메라 기준)</summary>
        public static bool TryGetScreenRect(TMP_Text _text, Vector3[] _world, out Rect _screenRect)
        {
            _screenRect = default;
            Canvas _canvas = _text.canvas;
            if (null == _canvas) return false;

            Canvas _root = _canvas.rootCanvas;
            Camera _cam = null;
            if (RenderMode.ScreenSpaceOverlay != _root.renderMode)
            {
                _cam = null != _root.worldCamera ? _root.worldCamera : Camera.main;
                if (null == _cam) return false;
            }

            float _xMin = float.MaxValue, _yMin = float.MaxValue, _xMax = float.MinValue, _yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector2 _p = RectTransformUtility.WorldToScreenPoint(_cam, _world[i]);
                _xMin = Mathf.Min(_xMin, _p.x);
                _yMin = Mathf.Min(_yMin, _p.y);
                _xMax = Mathf.Max(_xMax, _p.x);
                _yMax = Mathf.Max(_yMax, _p.y);
            }
            _screenRect = Rect.MinMaxRect(_xMin, _yMin, _xMax, _yMax);
            return true;
        }

        private static void ToWorld(RectTransform _rt, Rect _local, Vector3[] _out)
        {
            _out[0] = _rt.TransformPoint(new Vector3(_local.xMin, _local.yMin, 0f));
            _out[1] = _rt.TransformPoint(new Vector3(_local.xMin, _local.yMax, 0f));
            _out[2] = _rt.TransformPoint(new Vector3(_local.xMax, _local.yMax, 0f));
            _out[3] = _rt.TransformPoint(new Vector3(_local.xMax, _local.yMin, 0f));
        }

        private static Rect ToLocalRect(RectTransform _space, Vector3[] _world)
        {
            for (int i = 0; i < 4; i++) cornerBuffer[i] = _space.InverseTransformPoint(_world[i]);

            float _xMin = float.MaxValue, _yMin = float.MaxValue, _xMax = float.MinValue, _yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                _xMin = Mathf.Min(_xMin, cornerBuffer[i].x);
                _yMin = Mathf.Min(_yMin, cornerBuffer[i].y);
                _xMax = Mathf.Max(_xMax, cornerBuffer[i].x);
                _yMax = Mathf.Max(_yMax, cornerBuffer[i].y);
            }
            return Rect.MinMaxRect(_xMin, _yMin, _xMax, _yMax);
        }

        private static string Excess(float _dx, float _dy)
        {
            if (_dx <= 0f && _dy <= 0f) return "줄 높이가 칸 높이보다 큼";
            if (_dx > 0f && _dy > 0f) return $"가로 +{F(_dx)}, 세로 +{F(_dy)} 넘침";
            if (_dx > 0f) return $"가로 +{F(_dx)} 넘침";
            return $"세로 +{F(_dy)} 넘침";
        }

        private static string Describe(List<string> _chars)
        {
            System.Text.StringBuilder _sb = new System.Text.StringBuilder(64);
            int _shown = Mathf.Min(_chars.Count, 12);
            for (int i = 0; i < _shown; i++)
            {
                if (i > 0) _sb.Append(' ');
                int _cp = char.ConvertToUtf32(_chars[i], 0);
                _sb.Append(_chars[i]).Append("(U+").Append(_cp.ToString("X4")).Append(')');
            }
            if (_chars.Count > _shown) _sb.Append(" 외 ").Append(_chars.Count - _shown).Append("개");
            return _sb.ToString();
        }

        private static string F(float _v) => _v.ToString("0.#", CultureInfo.InvariantCulture);
    }
}
