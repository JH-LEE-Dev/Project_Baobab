using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace PresentationLayer.UISystem.CustomNumber
{
    [ExecuteAlways]
    public class CurrencyFontHUD : MonoBehaviour
    {
        private const int GlyphPixelSize = 12;
        private const int FixedGlyphSlotCount = 7;
        private const string MainGlyphPrefix = "CurrencyGlyph_";
        private const string DeltaGlyphPrefix = "CurrencyDeltaGlyph_";

        private static readonly ulong[] SuffixDivisors =
        {
            1000UL,
            1000000UL,
            1000000000UL,
            1000000000000UL,
            1000000000000000UL,
        };

        private static readonly char[] SuffixChars = { 'K', 'M', 'B', 'T', 'Q' };

        [Header("Resources")]
        [SerializeField] private Sprite currency0;
        [SerializeField] private Sprite currency1;
        [SerializeField] private Sprite currency2;
        [SerializeField] private Sprite currency3;
        [SerializeField] private Sprite currency4;
        [SerializeField] private Sprite currency5;
        [SerializeField] private Sprite currency6;
        [SerializeField] private Sprite currency7;
        [SerializeField] private Sprite currency8;
        [SerializeField] private Sprite currency9;
        [SerializeField] private Sprite currencyDot;
        [SerializeField] private Sprite currencyK;
        [SerializeField] private Sprite currencyM;
        [SerializeField] private Sprite currencyB;
        [SerializeField] private Sprite currencyT;
        [SerializeField] private Sprite currencyQ;
        [SerializeField] private Sprite currencyPlus;
        [SerializeField] private Sprite currencyMinus;
        [SerializeField] private Sprite currencyComma;

        [Header("Settings")]
        [SerializeField] private float pixelScale = 1.0f;
        [SerializeField] private float characterSpacing = 0.0f;
        [SerializeField] private float numberLetterSpacingOffset = -1.0f;

        [Header("Value Interpolation Motion")]
        [SerializeField] private float valueTweenDuration = 1.2f;
        [SerializeField] private Ease valueTweenEase = Ease.OutExpo;
        [SerializeField] private float colorTweenDuration = 1.2f;
        [SerializeField] private Ease colorTweenEase = Ease.InExpo;
        [SerializeField] private Color increaseColor = new Color(0.35f, 1.0f, 0.45f, 1.0f);
        [SerializeField] private Color decreaseColor = new Color(1.0f, 0.32f, 0.28f, 1.0f);
        [SerializeField] private Color normalGlyphColor = Color.white;

        [Header("Glyph Wave Motion")]
        [SerializeField] private float glyphWaveDelay = 0.025f;
        [SerializeField] private float glyphMotionDuration = 0.25f;
        [SerializeField] private float glyphBounceDistance = 7.0f;
        [SerializeField] private Vector2 glyphPreSquashScale = new Vector2(1.1f, 0.75f);
        [SerializeField] private Vector2 glyphBounceSquashScale = new Vector2(0.7f, 1.4f);
        [SerializeField] private Ease glyphPreSquashEase = Ease.OutBack;
        [SerializeField] private Ease glyphBounceMoveEase = Ease.OutBack;
        [SerializeField] private Ease glyphReturnMoveEase = Ease.OutBack;
        [SerializeField] private Ease glyphReturnScaleEase = Ease.OutBack;
        [SerializeField] private float glyphReturnOvershoot = 3.5f;
        [SerializeField] private float glyphPreSquashRatio = 0.18f;
        [SerializeField] private float glyphBounceRatio = 0.28f;

        [Header("Delta Amount Motion")]
        [SerializeField] private float deltaStartSpacing = 0.0f;
        [SerializeField] private float deltaGlyphDelay = 0.03f;
        [SerializeField] private float deltaGlyphShowDuration = 0.15f;
        [SerializeField] private float deltaVisibleHoldDuration = 0.2f;
        [SerializeField] private float deltaGlyphHideDuration = 0.15f;
        [SerializeField] private Ease deltaGlyphShowEase = Ease.OutBack;
        [SerializeField] private float deltaGlyphShowOvershoot = 3.5f;
        [SerializeField] private Ease deltaGlyphHideEase = Ease.InBack;
        [SerializeField] private float deltaGlyphHideOvershoot = 3.5f;
        [SerializeField] private Color deltaIncreaseColor = new Color(0.35f, 1.0f, 0.45f, 1.0f);
        [SerializeField] private Color deltaDecreaseColor = new Color(1.0f, 0.32f, 0.28f, 1.0f);

        [Header("Editor Preview")]
        [SerializeField] private bool previewInEditor = true;
        [SerializeField] private long previewValue = 1123;

        private readonly List<RawImage> glyphPool = new List<RawImage>(8);
        private readonly List<bool> glyphVisibility = new List<bool>(FixedGlyphSlotCount);
        private readonly List<RawImage> deltaGlyphPool = new List<RawImage>(16);
        private readonly char[] textBuffer = new char[16];
        private readonly char[] deltaTextBuffer = new char[32];
        private readonly Sprite[] sourceSprites = new Sprite[19];

        private RectTransform rectTransform;
        private long lastDisplayedValue = long.MinValue;
        private float mainLayoutWidth;
        private bool initialized;
        private readonly List<GlyphMotionState> glyphMotionStates = new List<GlyphMotionState>(8);
        private readonly List<Sequence> glyphMotionTweens = new List<Sequence>(8);
        private Tween numberTween;
        private Tween colorTween;
        private Sequence deltaMotionSequence;
        private Color currentGlyphColor = Color.white;

        public void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            rectTransform = GetComponent<RectTransform>();
            if (null != rectTransform)
                rectTransform.pivot = new Vector2(0.0f, 0.5f);

            CacheSprites();
#if UNITY_EDITOR
            if (false == Application.isPlaying)
                CleanupLegacyEditorGlyphs();
#endif
            CollectPool();
            EnsurePoolSize(GetSlotCount());
            EnsureGlyphVisibilitySize(glyphPool.Count);
            HideRemainingGlyphs(0);
        }

        private void Awake()
        {
            if (Application.isPlaying)
                Initialize();
        }

        private void OnValidate()
        {
            if (Application.isPlaying || false == previewInEditor)
                return;

#if UNITY_EDITOR
            if (false == CanUpdateEditorPreview())
                return;

            EditorApplication.delayCall -= RefreshEditorPreview;
            EditorApplication.delayCall += RefreshEditorPreview;
#endif
        }

#if UNITY_EDITOR
        private void RefreshEditorPreview()
        {
            EditorApplication.delayCall -= RefreshEditorPreview;

            if (null == this || Application.isPlaying || false == previewInEditor)
                return;

            if (false == CanUpdateEditorPreview())
                return;

            initialized = false;
            lastDisplayedValue = long.MinValue;
            Initialize();
            SetValue(previewValue);
        }

        private bool CanUpdateEditorPreview()
        {
            if (null == gameObject || EditorUtility.IsPersistent(gameObject))
                return false;

            if (false == gameObject.scene.IsValid())
                return false;

            PrefabStage _prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (null != _prefabStage)
                return _prefabStage.IsPartOfPrefabContents(gameObject);

            return gameObject.scene.isLoaded;
        }
#endif

        private void OnDestroy()
        {
            StopNumberTweens();
            StopGlyphMotion();
            StopDeltaAmountMotion();
        }

        public void SetNumber(long _value)
        {
            Initialize();

            if (lastDisplayedValue == _value &&
                false == HasActiveNumberTween() &&
                false == HasActiveGlyphMotion() &&
                currentGlyphColor == normalGlyphColor)
            {
                return;
            }

            StopNumberTweens();
            StopDeltaAmountMotion();
            SetValue(_value, true);
            SetGlyphColor(normalGlyphColor);
        }

        private void SetValue(long _value)
        {
            SetValue(_value, true);
        }

        public void SetNumberAnimated(long _value)
        {
            SetNumberAnimatedInternal(_value, null);
        }

        public void SetNumberAnimated(long _value, long _deltaAmount)
        {
            SetNumberAnimatedInternal(_value, _deltaAmount);
        }

        private void SetNumberAnimatedInternal(long _value, long? _overrideDeltaAmount)
        {
            Initialize();

            bool _hasDisplayedValue = lastDisplayedValue != long.MinValue;
            long _previousValue = _hasDisplayedValue ? lastDisplayedValue : 0L;
            if (_previousValue == _value)
            {
                if (false == _hasDisplayedValue)
                    SetNumber(_value);

                return;
            }

            StopNumberTweens();

            if (false == _hasDisplayedValue)
                SetValue(_previousValue, true);

            if (_value > _previousValue)
            {
                PlayIncreaseMotion();
                PlayGlyphColorTween(increaseColor);
            }
            else
            {
                PlayDecreaseMotion();
                PlayGlyphColorTween(decreaseColor);
            }

            PlayDeltaAmountMotion(_overrideDeltaAmount ?? (_value - _previousValue));
            PlayNumberTween(_previousValue, _value);
        }

        public void PlayDeltaAmountMotion(long _amount)
        {
            Initialize();

            if (0L == _amount)
                return;

            StopDeltaAmountMotion();

            int _length = BuildDeltaText(_amount);
            UpdateDeltaGlyphs(_length, _amount > 0L ? deltaIncreaseColor : deltaDecreaseColor);

            deltaMotionSequence = DOTween.Sequence();

            for (int i = 0; i < _length; i++)
            {
                RawImage _glyph = deltaGlyphPool[i];
                if (null == _glyph)
                    continue;

                RectTransform _glyphRect = (RectTransform)_glyph.transform;
                _glyph.gameObject.SetActive(true);
                _glyphRect.localScale = Vector3.zero;

                deltaMotionSequence.Insert(
                    deltaGlyphDelay * i,
                    _glyphRect.DOScale(Vector3.one, deltaGlyphShowDuration).SetEase(deltaGlyphShowEase, deltaGlyphShowOvershoot));
            }

            float _hideStartTime = (deltaGlyphDelay * Mathf.Max(0, _length - 1)) + deltaGlyphShowDuration + deltaVisibleHoldDuration;
            for (int i = 0; i < _length; i++)
            {
                RawImage _glyph = deltaGlyphPool[i];
                if (null == _glyph)
                    continue;

                RectTransform _glyphRect = (RectTransform)_glyph.transform;
                deltaMotionSequence.Insert(
                    _hideStartTime + (deltaGlyphDelay * i),
                    _glyphRect.DOScale(Vector3.zero, deltaGlyphHideDuration).SetEase(deltaGlyphHideEase, deltaGlyphHideOvershoot));
            }

            deltaMotionSequence.OnKill(HideDeltaGlyphs);
            deltaMotionSequence.OnComplete(HideDeltaGlyphs);
        }

        private void SetValue(long _value, bool _stopMotion)
        {
            if (lastDisplayedValue == _value)
                return;

            Initialize();

            if (_stopMotion)
                StopGlyphMotion(true);

            lastDisplayedValue = _value;

            ulong _displayValue = _value < 0 ? 0UL : (ulong)_value;
            int _length = BuildDisplayText(_displayValue);
            UpdateGlyphs(_length);
        }

        public void PlayIncreaseMotion()
        {
            PlayValueChangeMotion(true);
        }

        public void PlayDecreaseMotion()
        {
            PlayValueChangeMotion(false);
        }

        public void PlayValueChangeMotion(bool _isIncrease)
        {
            Initialize();
            StopGlyphMotion(true);

            CacheGlyphMotionStates();

            int _slotCount = GetSlotCount();
            for (int i = 0; i < _slotCount; i++)
            {
                RawImage _glyph = glyphPool[i];
                if (null == _glyph || false == _glyph.gameObject.activeSelf)
                    continue;

                GlyphMotionState _state = GetGlyphMotionState(_glyph);
                if (null == _state || null == _state.RectTransform)
                    continue;

                float _delay = glyphWaveDelay * i;
                float _squashDuration = glyphMotionDuration * Mathf.Clamp01(glyphPreSquashRatio);
                float _bounceDuration = glyphMotionDuration * Mathf.Clamp01(glyphBounceRatio);
                float _returnDuration = Mathf.Max(0.01f, glyphMotionDuration - _squashDuration - _bounceDuration);

                Sequence _sequence = DOTween.Sequence();
                if (0.0f < _delay)
                    _sequence.AppendInterval(_delay);

                _sequence.Append(DOTween.To(
                    () => _state.MotionScale,
                    _value =>
                    {
                        _state.MotionScale = _value;
                        ApplyGlyphMotionState(_state);
                    },
                    new Vector3(glyphPreSquashScale.x, glyphPreSquashScale.y, 1.0f),
                    _squashDuration).SetEase(glyphPreSquashEase));
                _sequence.Append(DOTween.To(
                    () => _state.MotionOffset,
                    _value =>
                    {
                        _state.MotionOffset = _value;
                        ApplyGlyphMotionState(_state);
                    },
                    new Vector2(glyphBounceDistance, 0.0f),
                    _bounceDuration).SetEase(glyphBounceMoveEase));
                _sequence.Join(DOTween.To(
                    () => _state.MotionScale,
                    _value =>
                    {
                        _state.MotionScale = _value;
                        ApplyGlyphMotionState(_state);
                    },
                    new Vector3(glyphBounceSquashScale.x, glyphBounceSquashScale.y, 1.0f),
                    _bounceDuration).SetEase(glyphBounceMoveEase));
                _sequence.Append(DOTween.To(
                    () => _state.MotionOffset,
                    _value =>
                    {
                        _state.MotionOffset = _value;
                        ApplyGlyphMotionState(_state);
                    },
                    Vector2.zero,
                    _returnDuration).SetEase(glyphReturnMoveEase, glyphReturnOvershoot));
                _sequence.Join(DOTween.To(
                    () => _state.MotionScale,
                    _value =>
                    {
                        _state.MotionScale = _value;
                        ApplyGlyphMotionState(_state);
                    },
                    Vector3.one,
                    _returnDuration).SetEase(glyphReturnScaleEase, glyphReturnOvershoot));
                _sequence.OnKill(() =>
                {
                    RestoreGlyphMotionState(_state);
                });

                glyphMotionTweens.Add(_sequence);
            }
        }

        public void SetGlyphColor(Color _color)
        {
            Initialize();
            currentGlyphColor = _color;

            for (int i = 0; i < glyphPool.Count; i++)
            {
                if (null != glyphPool[i])
                    ApplyGlyphColor(i);
            }
        }

        public Color GetFirstActiveGlyphColor()
        {
            Initialize();

            for (int i = 0; i < glyphPool.Count; i++)
            {
                if (null != glyphPool[i] && IsGlyphVisible(i))
                    return glyphPool[i].color;
            }

            return Color.white;
        }

        public Color GetIncreaseColor()
        {
            return increaseColor;
        }

        public Color GetDecreaseColor()
        {
            return decreaseColor;
        }

        private void PlayNumberTween(long _previousValue, long _targetValue)
        {
            double _displayedValue = _previousValue;
            long _displayedLongValue = (long)Math.Round(_displayedValue);

            numberTween = DOTween.To(
                    () => _displayedValue,
                    _value =>
                    {
                        _displayedValue = _value;
                        long _nextDisplayValue = (long)Math.Round(_displayedValue);

                        if (_displayedLongValue == _nextDisplayValue)
                            return;

                        _displayedLongValue = _nextDisplayValue;
                        SetValue(_displayedLongValue, false);
                    },
                    _targetValue,
                    Mathf.Max(0.01f, valueTweenDuration))
                .SetEase(valueTweenEase)
                .OnComplete(() => SetValue(_targetValue, false));
        }

        private void PlayGlyphColorTween(Color _motionColor)
        {
            if (null != colorTween && colorTween.IsActive())
                colorTween.Kill();

            Color _currentColor = _motionColor;
            SetGlyphColor(_currentColor);

            colorTween = DOTween.To(
                    () => _currentColor,
                    _value =>
                    {
                        _currentColor = _value;
                        SetGlyphColor(_currentColor);
                    },
                    normalGlyphColor,
                    Mathf.Max(0.01f, colorTweenDuration))
                .SetEase(colorTweenEase);
        }

        private void StopNumberTweens()
        {
            if (null != numberTween && numberTween.IsActive())
                numberTween.Kill();

            if (null != colorTween && colorTween.IsActive())
                colorTween.Kill();
        }

        private void StopDeltaAmountMotion()
        {
            if (null != deltaMotionSequence && deltaMotionSequence.IsActive())
                deltaMotionSequence.Kill();

            HideDeltaGlyphs();
        }

        private bool HasActiveNumberTween()
        {
            return (null != numberTween && numberTween.IsActive()) ||
                   (null != colorTween && colorTween.IsActive());
        }

        private bool HasActiveGlyphMotion()
        {
            for (int i = 0; i < glyphMotionTweens.Count; i++)
            {
                if (null != glyphMotionTweens[i] && glyphMotionTweens[i].IsActive())
                    return true;
            }

            return false;
        }

        private void StopGlyphMotion(bool _restoreState = false)
        {
            for (int i = 0; i < glyphMotionTweens.Count; i++)
            {
                if (null != glyphMotionTweens[i] && glyphMotionTweens[i].IsActive())
                    glyphMotionTweens[i].Kill();
            }

            glyphMotionTweens.Clear();

            if (_restoreState)
                RestoreGlyphMotionStates();

            glyphMotionStates.Clear();
        }

        private void CacheGlyphMotionStates()
        {
            glyphMotionStates.Clear();

            int _slotCount = GetSlotCount();
            for (int i = 0; i < _slotCount; i++)
            {
                RawImage _glyph = glyphPool[i];
                if (null == _glyph || false == _glyph.gameObject.activeSelf)
                    continue;

                RectTransform _glyphRect = (RectTransform)_glyph.transform;
                glyphMotionStates.Add(new GlyphMotionState(_glyph, _glyphRect, _glyphRect.anchoredPosition, _glyphRect.localScale));
            }
        }

        private GlyphMotionState GetGlyphMotionState(RawImage _glyph)
        {
            for (int i = 0; i < glyphMotionStates.Count; i++)
            {
                if (glyphMotionStates[i].Glyph == _glyph)
                    return glyphMotionStates[i];
            }

            return null;
        }

        private void RestoreGlyphMotionStates()
        {
            for (int i = 0; i < glyphMotionStates.Count; i++)
                RestoreGlyphMotionState(glyphMotionStates[i]);
        }

        private void RestoreGlyphMotionState(GlyphMotionState _state)
        {
            if (null == _state || null == _state.Glyph || null == _state.RectTransform)
                return;

            _state.MotionOffset = Vector2.zero;
            _state.MotionScale = Vector3.one;
            ApplyGlyphMotionState(_state);
        }

        private void ApplyGlyphMotionState(GlyphMotionState _state)
        {
            if (null == _state || null == _state.RectTransform)
                return;

            _state.RectTransform.anchoredPosition = _state.InitialPosition + _state.MotionOffset;
            _state.RectTransform.localScale = new Vector3(
                _state.InitialScale.x * _state.MotionScale.x,
                _state.InitialScale.y * _state.MotionScale.y,
                _state.InitialScale.z * _state.MotionScale.z);
        }

        private void UpdateGlyphMotionBase(RawImage _glyph, Vector2 _basePosition)
        {
            GlyphMotionState _state = GetGlyphMotionState(_glyph);
            if (null == _state)
                return;

            _state.InitialPosition = _basePosition;
            _state.InitialScale = Vector3.one;
            ApplyGlyphMotionState(_state);
        }

        private int BuildDisplayText(ulong _value)
        {
            if (_value < 1000UL)
                return WriteUnsigned(_value, 0);

            int _suffixIndex = 0;
            for (int i = SuffixDivisors.Length - 1; i >= 0; i--)
            {
                if (_value >= SuffixDivisors[i])
                {
                    _suffixIndex = i;
                    break;
                }
            }

            ulong _divisor = SuffixDivisors[_suffixIndex];
            ulong _whole = _value / _divisor;
            ulong _fraction = ((_value % _divisor) * 100UL) / _divisor;

            int _index = WriteUnsigned(_whole, 0);

            if (0UL < _fraction)
            {
                textBuffer[_index++] = '.';

                if (_fraction < 10UL)
                {
                    textBuffer[_index++] = '0';
                    textBuffer[_index++] = (char)('0' + _fraction);
                }
                else
                {
                    textBuffer[_index++] = (char)('0' + (_fraction / 10UL));

                    ulong _secondDigit = _fraction % 10UL;
                    if (0UL < _secondDigit)
                        textBuffer[_index++] = (char)('0' + _secondDigit);
                }
            }

            textBuffer[_index++] = SuffixChars[_suffixIndex];
            return _index;
        }

        private int BuildDeltaText(long _amount)
        {
            ulong _value = _amount < 0L ? (ulong)(-_amount) : (ulong)_amount;
            int _digitCount = CountDigits(_value);
            int _commaCount = (_digitCount - 1) / 3;
            int _length = 1 + _digitCount + _commaCount;
            int _index = _length - 1;
            int _digitGroupCount = 0;

            deltaTextBuffer[0] = _amount < 0L ? '-' : '+';

            do
            {
                if (3 == _digitGroupCount)
                {
                    deltaTextBuffer[_index--] = ',';
                    _digitGroupCount = 0;
                }

                deltaTextBuffer[_index--] = (char)('0' + (_value % 10UL));
                _value /= 10UL;
                _digitGroupCount++;
            }
            while (0UL < _value && 0 < _index);

            return Mathf.Min(_length, deltaTextBuffer.Length);
        }

        private int CountDigits(ulong _value)
        {
            if (0UL == _value)
                return 1;

            int _count = 0;
            while (0UL < _value)
            {
                _value /= 10UL;
                _count++;
            }

            return _count;
        }

        private int WriteUnsigned(ulong _value, int _startIndex)
        {
            if (0UL == _value)
            {
                textBuffer[_startIndex] = '0';
                return _startIndex + 1;
            }

            int _index = _startIndex;
            ulong _temp = _value;

            while (0UL < _temp && _index < textBuffer.Length)
            {
                textBuffer[_index++] = (char)('0' + (_temp % 10UL));
                _temp /= 10UL;
            }

            int _left = _startIndex;
            int _right = _index - 1;
            while (_left < _right)
            {
                char _swap = textBuffer[_left];
                textBuffer[_left] = textBuffer[_right];
                textBuffer[_right] = _swap;
                _left++;
                _right--;
            }

            return _index;
        }

        private void UpdateGlyphs(int _length)
        {
            int _slotCount = GetSlotCount();
            EnsurePoolSize(_slotCount);
            EnsureGlyphVisibilitySize(glyphPool.Count);

            float _cursor = 0.0f;
            float _scaledGlyphSize = GlyphPixelSize * pixelScale;

            int _visibleLength = Mathf.Min(_length, _slotCount);
            for (int i = 0; i < _slotCount; i++)
            {
                bool _isVisible = i < _visibleLength;
                char _char = _isVisible ? textBuffer[i] : '0';
                GlyphMetrics _metrics = GetMetrics(_char);
                RawImage _image = glyphPool[i];
                RectTransform _imageRect = (RectTransform)_image.transform;

                _image.texture = _isVisible ? GetTexture(_char) : GetTexture('0');
                _image.raycastTarget = false;
                _imageRect.sizeDelta = new Vector2(_scaledGlyphSize, _scaledGlyphSize);

                Vector2 _basePosition = new Vector2(
                    _cursor - (_metrics.LeftPadding * pixelScale) + (_scaledGlyphSize * 0.5f),
                    0.0f);
                _imageRect.anchoredPosition = _basePosition;
                UpdateGlyphMotionBase(_image, _basePosition);

                if (false == _image.gameObject.activeSelf)
                    _image.gameObject.SetActive(true);

                SetGlyphVisible(i, _isVisible);

                _cursor += _metrics.InkWidth * pixelScale;

                if (i < _slotCount - 1)
                {
                    char _nextChar = i + 1 < _visibleLength ? textBuffer[i + 1] : '0';
                    _cursor += GetSpacing(_char, _nextChar);
                }
            }

            if (null != rectTransform)
            {
                mainLayoutWidth = Mathf.Max(0.0f, _cursor);
                rectTransform.sizeDelta = new Vector2(mainLayoutWidth, _scaledGlyphSize);
            }

            HideRemainingGlyphs(_slotCount);
        }

        private void UpdateDeltaGlyphs(int _length, Color _color)
        {
            EnsureDeltaPoolSize(_length);

            float _cursor = mainLayoutWidth + (deltaStartSpacing * pixelScale);
            float _scaledGlyphSize = GlyphPixelSize * pixelScale;

            for (int i = 0; i < _length; i++)
            {
                char _char = deltaTextBuffer[i];
                GlyphMetrics _metrics = GetMetrics(_char);
                RawImage _image = deltaGlyphPool[i];
                RectTransform _imageRect = (RectTransform)_image.transform;

                _image.texture = GetTexture(_char);
                _image.raycastTarget = false;
                _image.color = _color;
                _imageRect.sizeDelta = new Vector2(_scaledGlyphSize, _scaledGlyphSize);
                _imageRect.anchoredPosition = new Vector2(
                    _cursor - (_metrics.LeftPadding * pixelScale) + (_scaledGlyphSize * 0.5f),
                    0.0f);

                _cursor += _metrics.InkWidth * pixelScale;

                if (i < _length - 1)
                    _cursor += GetSpacing(_char, deltaTextBuffer[i + 1]);
            }

            for (int i = _length; i < deltaGlyphPool.Count; i++)
            {
                if (null != deltaGlyphPool[i])
                    deltaGlyphPool[i].gameObject.SetActive(false);
            }

            if (null != rectTransform)
                rectTransform.sizeDelta = new Vector2(Mathf.Max(mainLayoutWidth, _cursor), _scaledGlyphSize);
        }

        private void CacheSprites()
        {
            sourceSprites[0] = currency0;
            sourceSprites[1] = currency1;
            sourceSprites[2] = currency2;
            sourceSprites[3] = currency3;
            sourceSprites[4] = currency4;
            sourceSprites[5] = currency5;
            sourceSprites[6] = currency6;
            sourceSprites[7] = currency7;
            sourceSprites[8] = currency8;
            sourceSprites[9] = currency9;
            sourceSprites[10] = currencyDot;
            sourceSprites[11] = currencyK;
            sourceSprites[12] = currencyM;
            sourceSprites[13] = currencyB;
            sourceSprites[14] = currencyT;
            sourceSprites[15] = currencyQ;
            sourceSprites[16] = currencyPlus;
            sourceSprites[17] = currencyMinus;
            sourceSprites[18] = currencyComma;

        }

        private void CollectPool()
        {
            glyphPool.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform _child = transform.GetChild(i);
                if (false == _child.name.StartsWith(MainGlyphPrefix))
                    continue;

                RawImage _image = _child.GetComponent<RawImage>();
                if (null != _image)
                    glyphPool.Add(_image);
            }

            deltaGlyphPool.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform _child = transform.GetChild(i);
                if (false == _child.name.StartsWith(DeltaGlyphPrefix))
                    continue;

                RawImage _image = _child.GetComponent<RawImage>();
                if (null != _image)
                    deltaGlyphPool.Add(_image);
            }
        }

#if UNITY_EDITOR
        private void CleanupLegacyEditorGlyphs()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform _child = transform.GetChild(i);
                if (false == _child.name.StartsWith(MainGlyphPrefix) &&
                    false == _child.name.StartsWith(DeltaGlyphPrefix))
                    continue;

                if (null != _child.GetComponent<RawImage>())
                    continue;

                DestroyImmediate(_child.gameObject);
            }
        }
#endif

        private void EnsurePoolSize(int _count)
        {
            while (glyphPool.Count < _count)
            {
                RawImage _image = CreateGlyphImage(glyphPool.Count);
                if (null == _image)
                    return;

                glyphPool.Add(_image);
            }

            EnsureGlyphVisibilitySize(glyphPool.Count);
        }

        private RawImage CreateGlyphImage(int _index)
        {
#if UNITY_EDITOR
            if (false == Application.isPlaying && false == CanUpdateEditorPreview())
                return null;
#endif

            return CreateRawGlyphImage($"{MainGlyphPrefix}{_index}");
        }

        private void EnsureDeltaPoolSize(int _count)
        {
            while (deltaGlyphPool.Count < _count)
            {
                RawImage _image = CreateRawGlyphImage($"{DeltaGlyphPrefix}{deltaGlyphPool.Count}");
                if (null == _image)
                    return;

                deltaGlyphPool.Add(_image);
            }
        }

        private RawImage CreateRawGlyphImage(string _name)
        {
#if UNITY_EDITOR
            if (false == Application.isPlaying && false == CanUpdateEditorPreview())
                return null;
#endif

            GameObject _glyphObject = new GameObject(_name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
#if UNITY_EDITOR
            if (false == Application.isPlaying)
                _glyphObject.hideFlags = HideFlags.DontSaveInEditor;
#endif

            _glyphObject.layer = gameObject.layer;
            _glyphObject.transform.SetParent(transform, false);

            RectTransform _glyphRect = (RectTransform)_glyphObject.transform;
            _glyphRect.anchorMin = new Vector2(0.0f, 0.5f);
            _glyphRect.anchorMax = new Vector2(0.0f, 0.5f);
            _glyphRect.pivot = new Vector2(0.5f, 0.5f);

            RawImage _image = _glyphObject.GetComponent<RawImage>();
            _image.raycastTarget = false;

            return _image;
        }

        private void HideDeltaGlyphs()
        {
            for (int i = 0; i < deltaGlyphPool.Count; i++)
            {
                if (null == deltaGlyphPool[i])
                    continue;

                deltaGlyphPool[i].gameObject.SetActive(false);
                deltaGlyphPool[i].transform.localScale = Vector3.zero;
            }

            if (null != rectTransform)
                rectTransform.sizeDelta = new Vector2(mainLayoutWidth, GlyphPixelSize * pixelScale);
        }

        private void HideRemainingGlyphs(int _activeCount)
        {
            for (int i = _activeCount; i < glyphPool.Count; i++)
            {
                if (null == glyphPool[i])
                    continue;

                if (i < FixedGlyphSlotCount)
                {
                    SetGlyphVisible(i, false);
                    if (false == glyphPool[i].gameObject.activeSelf)
                        glyphPool[i].gameObject.SetActive(true);
                }
                else if (true == glyphPool[i].gameObject.activeSelf)
                {
                    glyphPool[i].gameObject.SetActive(false);
                }
            }
        }

        private int GetSlotCount()
        {
            return FixedGlyphSlotCount;
        }

        private void EnsureGlyphVisibilitySize(int _count)
        {
            while (glyphVisibility.Count < _count)
                glyphVisibility.Add(false);
        }

        private void SetGlyphVisible(int _index, bool _isVisible)
        {
            EnsureGlyphVisibilitySize(_index + 1);
            glyphVisibility[_index] = _isVisible;
            ApplyGlyphColor(_index);
        }

        private bool IsGlyphVisible(int _index)
        {
            return 0 <= _index && _index < glyphVisibility.Count && glyphVisibility[_index];
        }

        private void ApplyGlyphColor(int _index)
        {
            if (_index < 0 || _index >= glyphPool.Count || null == glyphPool[_index])
                return;

            Color _color = currentGlyphColor;
            if (false == IsGlyphVisible(_index))
                _color.a = 0.0f;

            glyphPool[_index].color = _color;
        }

        private Texture GetTexture(char _char)
        {
            if ('0' <= _char && _char <= '9')
                return GetTexture(sourceSprites[_char - '0']);

            switch (_char)
            {
                case '.':
                    return GetTexture(sourceSprites[10]);
                case 'K':
                    return GetTexture(sourceSprites[11]);
                case 'M':
                    return GetTexture(sourceSprites[12]);
                case 'B':
                    return GetTexture(sourceSprites[13]);
                case 'T':
                    return GetTexture(sourceSprites[14]);
                case 'Q':
                    return GetTexture(sourceSprites[15]);
                case '+':
                    return GetTexture(sourceSprites[16]);
                case '-':
                    return GetTexture(sourceSprites[17]);
                case ',':
                    return GetTexture(sourceSprites[18]);
                default:
                    return null;
            }
        }

        private Texture GetTexture(Sprite _sprite)
        {
            return null == _sprite ? null : _sprite.texture;
        }

        private float GetSpacing(char _left, char _right)
        {
            float _spacing = characterSpacing;

            if (IsNumberOrLetter(_left) && IsNumberOrLetter(_right))
                _spacing += numberLetterSpacingOffset;

            return _spacing * pixelScale;
        }

        private bool IsNumberOrLetter(char _char)
        {
            if ('0' <= _char && _char <= '9')
                return true;

            return 'K' == _char || 'M' == _char || 'B' == _char || 'T' == _char || 'Q' == _char;
        }

        private GlyphMetrics GetMetrics(char _char)
        {
            switch (_char)
            {
                case '1':
                    return new GlyphMetrics(4, 4);
                case '4':
                    return new GlyphMetrics(2, 8);
                case '.':
                    return new GlyphMetrics(5, 3);
                case ',':
                    return new GlyphMetrics(5, 3);
                case '+':
                    return new GlyphMetrics(3, 7);
                case '-':
                    return new GlyphMetrics(3, 6);
                default:
                    return new GlyphMetrics(3, 7);
            }
        }

        private readonly struct GlyphMetrics
        {
            public readonly int LeftPadding;
            public readonly int InkWidth;

            public GlyphMetrics(int _leftPadding, int _inkWidth)
            {
                LeftPadding = _leftPadding;
                InkWidth = _inkWidth;
            }
        }

        private sealed class GlyphMotionState
        {
            public readonly RawImage Glyph;
            public readonly RectTransform RectTransform;
            public Vector2 InitialPosition;
            public Vector3 InitialScale;
            public Vector2 MotionOffset;
            public Vector3 MotionScale;

            public GlyphMotionState(
                RawImage _glyph,
                RectTransform _rectTransform,
                Vector2 _initialPosition,
                Vector3 _initialScale)
            {
                Glyph = _glyph;
                RectTransform = _rectTransform;
                InitialPosition = _initialPosition;
                InitialScale = _initialScale;
                MotionOffset = Vector2.zero;
                MotionScale = Vector3.one;
            }
        }
    }
}
