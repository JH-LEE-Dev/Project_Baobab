using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PresentationLayer.UISystem
{
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPInlineStyleAnimator : MonoBehaviour, ITextPreprocessor
    {
        private const int MaxColorCount = 5;

        private struct ColorStyle
        {
            public bool enabled;
            public Color32[] colors;
        }

        private struct CharacterStyle
        {
            public ColorStyle colorStyle;
            public bool shake;
            public bool characterShake;
            public bool wave;
        }

        [SerializeField] private TMP_Text tmpText;
        [SerializeField] private bool animateInEditMode = true;

        [Header("Color")]
        [SerializeField] private float colorCycleDuration = 0.75f;

        [Header("Pixel Snap")]
        [Tooltip("Shake/Char_shake/Wave 오프셋을 360p 기준 1픽셀(캔버스 1유닛) 단위로 반올림한다. 고해상도에서도 저해상도의 픽셀 단위 움직임으로 보인다.")]
        [SerializeField] private bool snapToReferencePixel = true;

        [Header("Shake")]
        [SerializeField] private float shakeAmplitude = 1.2f;
        [SerializeField] private float shakeFrequency = 34.0f;

        [Header("Wave")]
        [SerializeField] private float waveAmplitude = 2.0f;
        [SerializeField] private float waveFrequency = 5.0f;
        [SerializeField] private float waveCharacterOffset = 0.45f;

        [Header("Reveal Bounce")]
        [SerializeField] private float revealTotalDuration = 0.3f;
        [SerializeField] private float revealCharacterDuration = 0.1f;
        [SerializeField] private Vector2 revealStartScale = new Vector2(1.08f, 0.68f);
        [SerializeField] private AnimationCurve revealScaleCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve revealAlphaCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private readonly List<CharacterStyle> characterStyles = new List<CharacterStyle>(128);
        private readonly Stack<ColorStyle> colorStack = new Stack<ColorStyle>(4);
        private readonly StringBuilder cleanTextBuilder = new StringBuilder(128);

        private ITextPreprocessor previousTextPreprocessor;
        private bool hasAnyStyle;
        private bool hasAnimatedStyle;
        private bool isRevealPlaying;
        private float revealStartTime;
        private int revealCharacterCount;
        private int nextRevealCallbackCharacterIndex;
        private Action revealCharacterAppearedCallback;
        // 등장 연출을 시작할 때의 언어. 연출 도중 언어가 바뀌었는지 판별하는 데 쓴다.
        private Language revealLanguage;

        public string PreprocessText(string _text)
        {
            string source = previousTextPreprocessor != null ? previousTextPreprocessor.PreprocessText(_text) : _text;
            ParseSourceText(source ?? string.Empty);
            return cleanTextBuilder.ToString();
        }

        public void PlayRevealBounce(Action _characterAppearedCallback = null)
        {
            if (Application.isPlaying == false)
                return;

            BindReferencesIfNeeded();

            if (tmpText == null)
                return;

            isRevealPlaying = true;
            revealStartTime = Time.time;
            nextRevealCallbackCharacterIndex = 0;
            revealCharacterAppearedCallback = _characterAppearedCallback;
            revealLanguage = FontLocalizer.CurrentLanguage;

            tmpText.ForceMeshUpdate(false, true);
            revealCharacterCount = Mathf.Max(0, tmpText.textInfo.characterCount);

            if (revealCharacterCount == 0)
            {
                isRevealPlaying = false;
                revealCharacterAppearedCallback = null;
                return;
            }

            tmpText.SetVerticesDirty();
        }

        public void StopRevealBounce(bool showImmediately = true)
        {
            isRevealPlaying = false;
            revealCharacterAppearedCallback = null;

            if (tmpText == null)
                return;

            if (showImmediately)
                tmpText.SetVerticesDirty();
        }

        private void Awake()
        {
            BindReferencesIfNeeded();
        }

        private void OnEnable()
        {
            BindReferencesIfNeeded();

            if (tmpText == null)
                return;

            previousTextPreprocessor = ReferenceEquals(tmpText.textPreprocessor, this) ? null : tmpText.textPreprocessor;
            tmpText.textPreprocessor = this;
            tmpText.OnPreRenderText += HandlePreRenderText;
            tmpText.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (tmpText != null)
            {
                tmpText.OnPreRenderText -= HandlePreRenderText;

                if (ReferenceEquals(tmpText.textPreprocessor, this))
                    tmpText.textPreprocessor = previousTextPreprocessor;

                tmpText.SetVerticesDirty();
            }

            previousTextPreprocessor = null;
        }

        private void OnValidate()
        {
            BindReferencesIfNeeded();

            if (isActiveAndEnabled && tmpText != null)
                tmpText.SetVerticesDirty();
        }

        private void Update()
        {
            EndRevealIfLanguageChanged();

            if ((hasAnimatedStyle == false && isRevealPlaying == false) || tmpText == null)
                return;

            if (Application.isPlaying == false && animateInEditMode == false)
                return;

            if (isRevealPlaying)
            {
                DispatchRevealCharacterCallbacks();

                if (IsRevealComplete())
                {
                    isRevealPlaying = false;
                    revealCharacterAppearedCallback = null;
                }
            }

            tmpText.ForceMeshUpdate(false, false);

#if UNITY_EDITOR
            if (Application.isPlaying == false)
                SceneView.RepaintAll();
#endif
        }

        private void ParseSourceText(string _source)
        {
            characterStyles.Clear();
            colorStack.Clear();
            cleanTextBuilder.Clear();
            hasAnyStyle = false;
            hasAnimatedStyle = false;

            ColorStyle currentColorStyle = default;
            int shakeDepth = 0;
            int characterShakeDepth = 0;
            int waveDepth = 0;

            int index = 0;
            while (index < _source.Length)
            {
                if (_source[index] == '<')
                {
                    int tagEndIndex = _source.IndexOf('>', index);
                    if (tagEndIndex >= 0)
                    {
                        string tag = _source.Substring(index + 1, tagEndIndex - index - 1);
                        if (HandleTag(tag, ref currentColorStyle, ref shakeDepth, ref characterShakeDepth, ref waveDepth))
                        {
                            index = tagEndIndex + 1;
                            continue;
                        }
                        else
                        {
                            // Standard TextMeshPro rich text tags (<b>, </i>, <size=...>, etc.)
                            // Pass through to cleanTextBuilder for TMP to format, but do not add dummy entries to characterStyles.
                            cleanTextBuilder.Append(_source, index, tagEndIndex - index + 1);
                            index = tagEndIndex + 1;
                            continue;
                        }
                    }
                }

                cleanTextBuilder.Append(_source[index]);
                characterStyles.Add(new CharacterStyle
                {
                    colorStyle = currentColorStyle,
                    shake = shakeDepth > 0,
                    characterShake = characterShakeDepth > 0,
                    wave = waveDepth > 0
                });
                index++;
            }
        }

        private bool HandleTag(string _tag, ref ColorStyle _currentColorStyle, ref int _shakeDepth, ref int _characterShakeDepth, ref int _waveDepth)
        {
            if (string.IsNullOrEmpty(_tag))
                return false;

            string trimmedTag = _tag.Trim();
            string upperTag = trimmedTag.ToUpperInvariant();

            if (upperTag.StartsWith("COLOR="))
            {
                colorStack.Push(_currentColorStyle);
                _currentColorStyle = BuildColorStyle(trimmedTag.Substring(6));
                hasAnyStyle |= _currentColorStyle.enabled;
                hasAnimatedStyle |= _currentColorStyle.enabled && _currentColorStyle.colors != null && _currentColorStyle.colors.Length > 1;
                return true;
            }

            if (upperTag == "/COLOR")
            {
                _currentColorStyle = colorStack.Count > 0 ? colorStack.Pop() : default;
                return true;
            }

            if (upperTag == "SHAKE")
            {
                _shakeDepth++;
                hasAnyStyle = true;
                hasAnimatedStyle = true;
                return true;
            }

            if (upperTag == "/SHAKE")
            {
                _shakeDepth = Mathf.Max(0, _shakeDepth - 1);
                return true;
            }

            if (upperTag == "CHAR_SHAKE")
            {
                _characterShakeDepth++;
                hasAnyStyle = true;
                hasAnimatedStyle = true;
                return true;
            }

            if (upperTag == "/CHAR_SHAKE")
            {
                _characterShakeDepth = Mathf.Max(0, _characterShakeDepth - 1);
                return true;
            }

            if (upperTag == "WAVE")
            {
                _waveDepth++;
                hasAnyStyle = true;
                hasAnimatedStyle = true;
                return true;
            }

            if (upperTag == "/WAVE")
            {
                _waveDepth = Mathf.Max(0, _waveDepth - 1);
                return true;
            }

            return false;
        }

        private ColorStyle BuildColorStyle(string _value)
        {
            string[] colorTexts = _value.Split(',');
            List<Color32> colors = new List<Color32>(MaxColorCount);

            for (int i = 0; i < colorTexts.Length && colors.Count < MaxColorCount; i++)
            {
                if (TryParseColor(colorTexts[i], out Color32 color))
                    colors.Add(color);
            }

            if (colors.Count == 0)
                return default;

            return new ColorStyle
            {
                enabled = true,
                colors = colors.ToArray()
            };
        }

        private bool TryParseColor(string _text, out Color32 _color)
        {
            _color = Color.white;

            if (string.IsNullOrEmpty(_text))
                return false;

            string colorText = _text.Trim();
            if (colorText.StartsWith("#"))
                colorText = colorText.Substring(1);

            if (colorText.Length == 6)
                colorText += "FF";

            if (colorText.Length != 8)
                return false;

            if (uint.TryParse(colorText, System.Globalization.NumberStyles.HexNumber, null, out uint rgba) == false)
                return false;

            _color = new Color32(
                (byte)((rgba >> 24) & 0xFF),
                (byte)((rgba >> 16) & 0xFF),
                (byte)((rgba >> 8) & 0xFF),
                (byte)(rgba & 0xFF));
            return true;
        }

        /// <summary>
        /// 등장 연출 도중 언어가 바뀌었으면 연출을 끝내고 새 문구를 그대로 보여준다.
        ///
        /// 연출은 시작 시점의 글자 수로 글자별 시작 시간을 잡아두기 때문에, 언어 변경으로 문구가
        /// 바뀐 채 이어가면 새 문구의 글자들이 엉뚱한 타이밍에 나타나거나 연출이 일찍 끝나 뒷글자가
        /// 한꺼번에 튀어나온다. 특히 일시정지(timeScale = 0) 중에는 Time.time이 멈춰 있어, 반쯤
        /// 찌그러지거나 투명한 글자가 재개할 때까지 그대로 남는다.
        ///
        /// "문구가 바뀌면"이 아니라 "언어가 바뀌면"으로 판별하는 이유: 결과창 처치 수처럼 등장 연출과
        /// 숫자 카운트업이 동시에 돌며 문구를 계속 바꾸는 UI가 있어, 문구 변경으로 판별하면 평소에도
        /// 연출이 시작하자마자 끊긴다.
        ///
        /// 메시 생성 도중(HandlePreRenderText)에도 불리므로 플래그만 내린다. 이어서 그리는 메시가 곧
        /// 원래 모습이 되고, 그 경로를 타지 않는 경우(Update)에는 다시 그리도록 표시한다.
        /// </summary>
        private void EndRevealIfLanguageChanged(bool _isRebuildingMesh = false)
        {
            if (false == isRevealPlaying || FontLocalizer.CurrentLanguage == revealLanguage)
                return;

            isRevealPlaying = false;
            revealCharacterAppearedCallback = null;

            if (false == _isRebuildingMesh && tmpText != null)
                tmpText.SetVerticesDirty();
        }

        private void HandlePreRenderText(TMP_TextInfo _textInfo)
        {
            EndRevealIfLanguageChanged(true);

            if (hasAnyStyle == false && isRevealPlaying == false || _textInfo == null)
                return;

            float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
            float pixelStep = true == snapToReferencePixel ? GetReferencePixelStep() : 0f;

            for (int i = 0; i < _textInfo.characterCount; i++)
            {
                bool hasStyle = i < characterStyles.Count;

                TMP_CharacterInfo characterInfo = _textInfo.characterInfo[i];
                if (characterInfo.isVisible == false)
                    continue;

                int materialIndex = characterInfo.materialReferenceIndex;
                int vertexIndex = characterInfo.vertexIndex;
                if (materialIndex < 0 || materialIndex >= _textInfo.meshInfo.Length)
                    continue;

                TMP_MeshInfo meshInfo = _textInfo.meshInfo[materialIndex];
                if (meshInfo.vertices == null || meshInfo.colors32 == null || vertexIndex + 3 >= meshInfo.vertices.Length)
                    continue;

                CharacterStyle style = hasStyle ? characterStyles[i] : default;
                Vector3 offset = hasStyle ? GetCharacterOffset(i, time, style) : Vector3.zero;
                if (0f < pixelStep)
                    offset = SnapOffsetToStep(offset, pixelStep);
                Color32 color = hasStyle ? GetCharacterColor(time, style) : Color.white;

                for (int j = 0; j < 4; j++)
                {
                    meshInfo.vertices[vertexIndex + j] += offset;

                    if (hasStyle && style.colorStyle.enabled)
                        meshInfo.colors32[vertexIndex + j] = color;
                }

                if (isRevealPlaying)
                    ApplyRevealBounce(_textInfo, characterInfo, meshInfo, vertexIndex, i, time);
            }
        }

        private void ApplyRevealBounce(TMP_TextInfo textInfo, TMP_CharacterInfo characterInfo, TMP_MeshInfo meshInfo, int vertexIndex, int characterIndex, float time)
        {
            float characterStartTime = revealStartTime + GetRevealInterval() * characterIndex;
            float elapsed = time - characterStartTime;

            if (elapsed < 0f)
            {
                SetCharacterAlpha(meshInfo, vertexIndex, 0f);
                return;
            }

            float progress = revealCharacterDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / revealCharacterDuration);
            float scaleProgress = Mathf.Clamp01(revealScaleCurve.Evaluate(progress));
            float alphaProgress = Mathf.Clamp01(revealAlphaCurve.Evaluate(progress));
            Vector2 scale = Vector2.Lerp(revealStartScale, Vector2.one, scaleProgress);

            Vector3 center = (characterInfo.bottomLeft + characterInfo.topRight) * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                int currentVertexIndex = vertexIndex + i;
                Vector3 vertex = meshInfo.vertices[currentVertexIndex];
                vertex -= center;
                vertex.x *= scale.x;
                vertex.y *= scale.y;
                meshInfo.vertices[currentVertexIndex] = vertex + center;
            }

            SetCharacterAlpha(meshInfo, vertexIndex, alphaProgress);
        }

        private void SetCharacterAlpha(TMP_MeshInfo meshInfo, int vertexIndex, float alpha)
        {
            byte alphaByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f);

            for (int i = 0; i < 4; i++)
            {
                Color32 color = meshInfo.colors32[vertexIndex + i];
                color.a = alphaByte;
                meshInfo.colors32[vertexIndex + i] = color;
            }
        }

        private bool IsRevealComplete()
        {
            if (revealCharacterCount <= 0)
                return true;

            float lastCharacterStartTime = revealStartTime + GetRevealInterval() * (revealCharacterCount - 1);
            return Time.time >= lastCharacterStartTime + revealCharacterDuration;
        }

        private void DispatchRevealCharacterCallbacks()
        {
            if (revealCharacterAppearedCallback == null || tmpText == null)
                return;

            float revealInterval = GetRevealInterval();
            while (nextRevealCallbackCharacterIndex < revealCharacterCount)
            {
                int characterIndex = nextRevealCallbackCharacterIndex;
                float characterStartTime = revealStartTime + (revealInterval * characterIndex);
                if (Time.time < characterStartTime)
                    break;

                nextRevealCallbackCharacterIndex++;

                if (characterIndex >= tmpText.textInfo.characterCount ||
                    tmpText.textInfo.characterInfo[characterIndex].isVisible == false)
                {
                    continue;
                }

                revealCharacterAppearedCallback.Invoke();
            }
        }

        private float GetRevealInterval()
        {
            if (revealCharacterCount <= 0 || revealTotalDuration <= 0f)
                return 0f;

            return revealTotalDuration / revealCharacterCount;
        }

        private Vector3 GetCharacterOffset(int _characterIndex, float _time, CharacterStyle _style)
        {
            Vector3 offset = Vector3.zero;

            if (_style.wave)
                offset.y += Mathf.Sin((_time * waveFrequency) + (_characterIndex * waveCharacterOffset)) * waveAmplitude;

            if (_style.shake)
            {
                const float seed = 13.73f;
                offset.x += Mathf.Sin((_time * shakeFrequency) + seed) * shakeAmplitude;
                offset.y += Mathf.Cos((_time * shakeFrequency * 1.17f) + seed) * shakeAmplitude;
            }

            if (_style.characterShake)
            {
                float seed = (_characterIndex + 1) * 37.719f;
                float xPhase = Mathf.Repeat(seed * 0.754f, 97.0f);
                float yPhase = Mathf.Repeat(seed * 1.327f, 113.0f);
                float xFrequency = shakeFrequency * Mathf.Lerp(0.78f, 1.35f, GetStable01(seed));
                float yFrequency = shakeFrequency * Mathf.Lerp(0.84f, 1.42f, GetStable01(seed + 17.0f));
                float amplitude = shakeAmplitude * Mathf.Lerp(0.65f, 1.25f, GetStable01(seed + 31.0f));

                offset.x += Mathf.Sin((_time * xFrequency) + xPhase) * amplitude;
                offset.y += Mathf.Cos((_time * yFrequency) + yPhase) * amplitude;
            }

            return offset;
        }

        /// <summary>
        /// 360p 기준 1픽셀이 이 텍스트의 로컬 좌표(정점 단위)로 얼마인지 돌려준다.
        ///
        /// 스크린 캔버스는 PixelPerfectCanvasScaleApplier가 정수 배율로 고정하고, 월드 캔버스는 스케일이 1/32(PPU 32)라서
        /// 어느 쪽이든 캔버스 1유닛이 360p 기준 1픽셀이다. 텍스트와 루트 캔버스 사이의 중간 스케일(텍스트를 키운 경우 등)은
        /// lossyScale 비율로 환산한다. 캔버스를 못 찾으면 로컬 1유닛을 1픽셀로 본다.
        /// </summary>
        private float GetReferencePixelStep()
        {
            Canvas canvas = tmpText.canvas;
            if (null == canvas)
                return 1.0f;

            float canvasScale = canvas.rootCanvas.transform.lossyScale.x;
            float textScale = tmpText.transform.lossyScale.x;
            if (0.0f >= canvasScale || 0.0f >= textScale)
                return 1.0f;

            return canvasScale / textScale;
        }

        private static Vector3 SnapOffsetToStep(Vector3 _offset, float _step)
        {
            _offset.x = Mathf.Round(_offset.x / _step) * _step;
            _offset.y = Mathf.Round(_offset.y / _step) * _step;
            return _offset;
        }

        private float GetStable01(float _seed)
        {
            return Mathf.Repeat(Mathf.Sin(_seed * 12.9898f) * 43758.5453f, 1.0f);
        }

        private Color32 GetCharacterColor(float _time, CharacterStyle _style)
        {
            if (_style.colorStyle.enabled == false || _style.colorStyle.colors == null || _style.colorStyle.colors.Length == 0)
                return Color.white;

            Color32[] colors = _style.colorStyle.colors;
            if (colors.Length == 1 || colorCycleDuration <= 0.0f)
                return colors[0];

            float progress = Mathf.Repeat(_time / colorCycleDuration, 1.0f);
            float scaledProgress = progress * colors.Length;
            int startIndex = Mathf.FloorToInt(scaledProgress) % colors.Length;
            int endIndex = (startIndex + 1) % colors.Length;
            float lerp = scaledProgress - Mathf.Floor(scaledProgress);

            return Color32.Lerp(colors[startIndex], colors[endIndex], lerp);
        }

        private void BindReferencesIfNeeded()
        {
            if (tmpText == null)
                tmpText = GetComponent<TMP_Text>();
        }
    }
}
