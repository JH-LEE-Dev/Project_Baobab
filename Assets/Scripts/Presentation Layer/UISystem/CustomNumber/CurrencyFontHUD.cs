using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.CustomNumber
{
    public enum CurrencyFontAlignmentMode
    {
        Left,
        Center,
    }

    [ExecuteAlways]
    public class CurrencyFontHUD : MonoBehaviour
    {
        private const int GlyphPixelSize = 12;
        private const int FixedGlyphSlotCount = 7;
        private const string MainGlyphPrefix = "CurrencyGlyph_";
        private const string DeltaGlyphPrefix = "CurrencyDeltaGlyph_";
        private const string AmountPivotAName = "AmountPivot_A";
        private const string AmountPivotBName = "AmountPivot_B";
        private const string CenterPivotName = "CenterPivot";
        private const float FontPopInterval = 0.04f;

        private static float lastFontPopPlayedTime = float.NegativeInfinity;

        // LogCutter.GetSoundVolume()과 동일한 규칙: 마을이 아니면(=던전에 있는 동안 배경에서 계속
        // 진행되는 원격 입금 등으로 카운터가 갱신되는 상태) 폰트 팝 사운드도 재생하지 않는다.
        // 이 HUD는 여러 화면(인벤토리/텐트/트레이더)에 흩어져 있고 사운드 재생 자체가 static
        // 메서드이므로, 게이팅도 단일 static 값으로 관리해 GameplayUICoordinator 한 곳에서만 갱신한다.
        private static MapType currentGlobalMapType = MapType.Town;

        public static void SetGlobalMapType(MapType _mapType)
        {
            currentGlobalMapType = _mapType;
        }

        // 하늘 카메라 연출(마을↔던전 전환) 동안 폰트 팝에 곱해지는 볼륨 배율.
        // 3D 사운드는 거리 감쇠와 production3DVolumeFactor로 카메라가 멀어지면 알아서 죽지만,
        // AudioManager.ApplySourceVolume()은 그 연출 계수를 spatialBlend > 0인 소스에만 적용한다
        // (UI/2D 사운드까지 죽이면 연출 자체의 SkyUP/SkyDown/HUDDown이 같이 먹통이 되기 때문).
        // 폰트 팝은 Sound.PlayUI로 나가는 2D 사운드라 그 혜택을 못 받으므로, 연출을 실제로 구동하는
        // SkyCameraProductionManager가 카메라 높이에 맞춰 이 값을 직접 밀어준다.
        //
        // 기본값은 반드시 1f이어야 한다. 연출을 타지 않는 일반 플레이 구간(마을에 그냥 서 있을 때)이
        // 이 값을 그대로 쓰기 때문이다. 같은 이유로 SkyCameraProductionManager는 연출을 건너뛰는
        // 모든 예외 분기에서도 1f로 원복해, 사운드가 영영 무음으로 남지 않게 한다.
        private static float skyProductionVolumeFactor = 1f;

        public static void SetSkyProductionVolumeFactor(float _factor)
        {
            skyProductionVolumeFactor = Mathf.Clamp01(_factor);
        }

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
        [SerializeField] private CurrencyFontAlignmentMode alignmentMode = CurrencyFontAlignmentMode.Left;
        [SerializeField] private float centerModeWidth = 40.0f;

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
        [SerializeField] private float deltaStartSpacing = 4.0f;
        [SerializeField] private float deltaGlyphWaveDuration = 0.25f;
        [SerializeField] private float deltaGlyphShowDuration = 0.15f;
        [SerializeField] private float deltaVisibleHoldDuration = 0.2f;
        [SerializeField] private float deltaGlyphHideDuration = 0.15f;
        [SerializeField] private float deltaGlyphShowOvershoot = 3.5f;
        [SerializeField] private float deltaGlyphHideOvershoot = 3.5f;
        [SerializeField] private Color deltaIncreaseColor = new Color(0.35f, 1.0f, 0.45f, 1.0f);
        [SerializeField] private Color deltaDecreaseColor = new Color(1.0f, 0.32f, 0.28f, 1.0f);
        [SerializeField] private float deltaHoldShakeDuration = 0.12f;
        [SerializeField] private float deltaHoldShakeDistance = 2.0f;

        private readonly List<RawImage> glyphPool = new List<RawImage>(8);
        private readonly List<bool> glyphVisibility = new List<bool>(FixedGlyphSlotCount);
        private readonly List<RawImage> deltaGlyphPool = new List<RawImage>(16);
        private readonly char[] textBuffer = new char[16];
        private readonly char[] deltaTextBuffer = new char[32];
        private readonly Sprite[] sourceSprites = new Sprite[19];

        private RectTransform rectTransform;
        private RectTransform amountPivotA;
        private RectTransform amountPivotB;
        private RectTransform centerPivot;
        private long lastDisplayedValue = long.MinValue;
        private float mainLayoutWidth;
        private float mainVisibleLeftEdge;
        private float mainVisibleRightEdge;
        private float notifiedVisibleLeftEdge = float.NaN;
        private float notifiedVisibleWidth = float.NaN;
        private bool initialized;
        private readonly List<GlyphMotionState> glyphMotionStates = new List<GlyphMotionState>(8);
        private readonly List<Sequence> glyphMotionTweens = new List<Sequence>(8);
        private Tween numberTween;
        private Tween colorTween;
        private Sequence deltaMotionSequence;
        private DeltaAmountPhase deltaAmountPhase = DeltaAmountPhase.None;
        private long activeDeltaAmount;
        private bool activeDeltaUsesPivotB;
        private int activeDeltaLength;
        private float currentDeltaShowTime;
        private Color currentGlyphColor = Color.white;

        public event Action VisibleContentBoundsChanged;

        public float PixelUnit => Mathf.Max(0.0001f, pixelScale);
        public float VisibleContentLeftEdge => mainVisibleLeftEdge;
        public float VisibleContentWidth => Mathf.Max(0.0f, mainVisibleRightEdge - mainVisibleLeftEdge);

        public void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            rectTransform = GetComponent<RectTransform>();
            if (null != rectTransform)
                rectTransform.pivot = new Vector2(0.0f, 0.5f);

            CacheSprites();
            CacheAmountPivots();
            CacheCenterPivot();
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

        public void SetMode(CurrencyFontAlignmentMode _mode)
        {
            Initialize();

            if (alignmentMode == _mode)
                return;

            alignmentMode = _mode;
            if (lastDisplayedValue != long.MinValue)
            {
                long _displayedValue = lastDisplayedValue;
                lastDisplayedValue = long.MinValue;
                SetValue(_displayedValue, false);
            }
        }

        private void SetValue(long _value)
        {
            SetValue(_value, true);
        }

        public void SetNumberAnimated(long _value)
        {
            SetNumberAnimatedInternal(_value, null, false);
        }

        public void SetNumberAnimated(long _value, bool _useAmountPivotB = false)
        {
            SetNumberAnimatedInternal(_value, null, _useAmountPivotB);
        }

        public void SetNumberAnimated(long _value, long _deltaAmount, bool _useAmountPivotB = false)
        {
            SetNumberAnimatedInternal(_value, _deltaAmount, _useAmountPivotB);
        }

        private void SetNumberAnimatedInternal(long _value, long? _overrideDeltaAmount, bool _useAmountPivotB)
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

            PlayDeltaAmountMotion(_overrideDeltaAmount ?? (_value - _previousValue), _useAmountPivotB);
            PlayNumberTween(_previousValue, _value);
        }

        public void PlayDeltaAmountMotion(long _amount, bool _useAmountPivotB = false)
        {
            Initialize();

            if (0L == _amount)
                return;

            bool _canMergeWithActiveDelta = CanMergeWithActiveDelta(_amount);
            if (_canMergeWithActiveDelta && deltaAmountPhase == DeltaAmountPhase.Showing)
            {
                activeDeltaAmount += _amount;
                activeDeltaUsesPivotB = _useAmountPivotB;
                if (0L == activeDeltaAmount)
                {
                    StopDeltaAmountMotion();
                    return;
                }

                RebuildActiveDeltaGlyphs();
                ApplyDeltaShowState(currentDeltaShowTime);
                RestartDeltaShowMotion(currentDeltaShowTime);
                return;
            }

            if (_canMergeWithActiveDelta && deltaAmountPhase == DeltaAmountPhase.Holding)
            {
                activeDeltaAmount += _amount;
                activeDeltaUsesPivotB = _useAmountPivotB;
                if (0L == activeDeltaAmount)
                {
                    StopDeltaAmountMotion();
                    return;
                }

                RebuildActiveDeltaGlyphs();
                SetDeltaGlyphScales(Vector3.one);
                RestartDeltaHoldMotion(true);
                return;
            }

            StopDeltaAmountMotion();
            activeDeltaAmount = _amount;
            activeDeltaUsesPivotB = _useAmountPivotB;
            currentDeltaShowTime = 0.0f;

            RebuildActiveDeltaGlyphs();
            SetDeltaGlyphScales(Vector3.zero);
            RestartDeltaShowMotion(0.0f);
        }

        private bool CanMergeWithActiveDelta(long _amount)
        {
            if (deltaAmountPhase == DeltaAmountPhase.None ||
                deltaAmountPhase == DeltaAmountPhase.Hiding ||
                0L == activeDeltaAmount)
            {
                return false;
            }

            return (activeDeltaAmount > 0L && _amount > 0L) ||
                   (activeDeltaAmount < 0L && _amount < 0L);
        }

        private void RestartDeltaShowMotion(float _startTime)
        {
            KillDeltaSequenceOnly();

            float _showEndTime = GetDeltaShowEndTime(activeDeltaLength);
            float _duration = Mathf.Max(0.0f, _showEndTime - _startTime);
            deltaAmountPhase = DeltaAmountPhase.Showing;

            deltaMotionSequence = DOTween.Sequence();
            if (_duration > 0.0f)
            {
                deltaMotionSequence.Append(
                    DOVirtual.Float(_startTime, _showEndTime, _duration, _time =>
                    {
                        currentDeltaShowTime = _time;
                        ApplyDeltaShowState(_time);
                    }).SetEase(Ease.Linear));
            }
            else
            {
                currentDeltaShowTime = _showEndTime;
                ApplyDeltaShowState(_showEndTime);
            }

            deltaMotionSequence.AppendCallback(() =>
            {
                deltaAmountPhase = DeltaAmountPhase.Holding;
                SetDeltaGlyphScales(Vector3.one);
            });
            AppendDeltaHoldAndHide(deltaMotionSequence, false);
        }

        private void RestartDeltaHoldMotion(bool _playShake)
        {
            KillDeltaSequenceOnly();
            deltaAmountPhase = DeltaAmountPhase.Holding;

            deltaMotionSequence = DOTween.Sequence();
            AppendDeltaHoldAndHide(deltaMotionSequence, _playShake);
        }

        private void AppendDeltaHoldAndHide(Sequence _sequence, bool _playShake)
        {
            if (_playShake)
                _sequence.Append(CreateDeltaShakeTween());

            _sequence.AppendInterval(Mathf.Max(0.0f, deltaVisibleHoldDuration));
            _sequence.AppendCallback(() => deltaAmountPhase = DeltaAmountPhase.Hiding);

            float _hideEndTime = GetDeltaHideEndTime(activeDeltaLength);
            _sequence.Append(
                DOVirtual.Float(0.0f, _hideEndTime, _hideEndTime, ApplyDeltaHideState).SetEase(Ease.Linear));
            _sequence.OnComplete(HideDeltaGlyphs);
        }

        private Tween CreateDeltaShakeTween()
        {
            float _shakeDuration = Mathf.Max(0.01f, deltaHoldShakeDuration);
            float _shakeDistance = Mathf.Max(0.0f, deltaHoldShakeDistance);

            return DOVirtual.Float(0.0f, 1.0f, _shakeDuration, _progress =>
            {
                RebuildActiveDeltaGlyphs();
                float _offset = Mathf.Sin(_progress * Mathf.PI * 6.0f) * (1.0f - _progress) * _shakeDistance * pixelScale;
                for (int i = 0; i < activeDeltaLength; i++)
                {
                    RawImage _glyph = deltaGlyphPool[i];
                    if (null == _glyph)
                        continue;

                    RectTransform _glyphRect = (RectTransform)_glyph.transform;
                    Vector2 _position = _glyphRect.anchoredPosition;
                    _position.x += _offset;
                    _glyphRect.anchoredPosition = _position;
                }
            }).SetEase(Ease.Linear).OnComplete(() => RebuildActiveDeltaGlyphs());
        }

        private void RebuildActiveDeltaGlyphs()
        {
            activeDeltaLength = BuildDeltaText(activeDeltaAmount);
            UpdateDeltaGlyphs(activeDeltaLength, activeDeltaAmount > 0L ? deltaIncreaseColor : deltaDecreaseColor, activeDeltaUsesPivotB);
        }

        private void ApplyDeltaShowState(float _time)
        {
            float _glyphDelay = CalculateDeltaGlyphDelay(activeDeltaLength);
            for (int i = 0; i < activeDeltaLength; i++)
            {
                RawImage _glyph = deltaGlyphPool[i];
                if (null == _glyph)
                    continue;

                float _progress = Mathf.Clamp01((_time - (_glyphDelay * i)) / Mathf.Max(0.01f, deltaGlyphShowDuration));
                float _scale = EvaluateOutBack(_progress, deltaGlyphShowOvershoot);
                _glyph.transform.localScale = Vector3.one * _scale;
            }
        }

        private void ApplyDeltaHideState(float _time)
        {
            float _glyphDelay = CalculateDeltaGlyphDelay(activeDeltaLength);
            for (int i = 0; i < activeDeltaLength; i++)
            {
                RawImage _glyph = deltaGlyphPool[i];
                if (null == _glyph)
                    continue;

                float _progress = Mathf.Clamp01((_time - (_glyphDelay * i)) / Mathf.Max(0.01f, deltaGlyphHideDuration));
                float _scale = 1.0f - Mathf.Clamp01(EvaluateInBack(_progress, deltaGlyphHideOvershoot));
                _glyph.transform.localScale = Vector3.one * _scale;
            }
        }

        private void SetDeltaGlyphScales(Vector3 _scale)
        {
            for (int i = 0; i < activeDeltaLength; i++)
            {
                if (null != deltaGlyphPool[i])
                    deltaGlyphPool[i].transform.localScale = _scale;
            }
        }

        private float GetDeltaShowEndTime(int _length)
        {
            return (CalculateDeltaGlyphDelay(_length) * Mathf.Max(0, _length - 1)) + Mathf.Max(0.01f, deltaGlyphShowDuration);
        }

        private float GetDeltaHideEndTime(int _length)
        {
            return (CalculateDeltaGlyphDelay(_length) * Mathf.Max(0, _length - 1)) + Mathf.Max(0.01f, deltaGlyphHideDuration);
        }

        private float EvaluateOutBack(float _progress, float _overshoot)
        {
            float _x = Mathf.Clamp01(_progress) - 1.0f;
            float _c1 = Mathf.Max(0.0f, _overshoot);
            float _c3 = _c1 + 1.0f;
            return 1.0f + (_c3 * _x * _x * _x) + (_c1 * _x * _x);
        }

        private float EvaluateInBack(float _progress, float _overshoot)
        {
            float _x = Mathf.Clamp01(_progress);
            float _c1 = Mathf.Max(0.0f, _overshoot);
            float _c3 = _c1 + 1.0f;
            return (_c3 * _x * _x * _x) - (_c1 * _x * _x);
        }

        private float CalculateDeltaGlyphDelay(int _glyphCount)
        {
            if (_glyphCount <= 0)
                return 0.0f;

            return Mathf.Max(0.0f, deltaGlyphWaveDuration) / _glyphCount;
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
                        SetTweenedValue(_displayedLongValue);
                    },
                    _targetValue,
                    Mathf.Max(0.01f, valueTweenDuration))
                .SetEase(valueTweenEase)
                .OnComplete(() => SetTweenedValue(_targetValue));
        }

        private void SetTweenedValue(long _value)
        {
            if (lastDisplayedValue == _value)
                return;

            SetValue(_value, false);
            TryPlayFontPop();
        }

        private static void TryPlayFontPop()
        {
            if (false == Application.isPlaying)
                return;

            if (MapType.Town != currentGlobalMapType)
                return;

            // 카메라가 충분히 올라가 사실상 들리지 않는 구간에서는 재생 자체를 건너뛴다.
            // 볼륨 0으로 재생하면 소리는 안 나면서 폴리포니 슬롯만 차지한다.
            // (위 맵타입 게이트와 마찬가지로 lastFontPopPlayedTime을 갱신하기 전에 빠져나가므로,
            //  연출이 끝나 볼륨이 돌아온 직후의 첫 팝이 쿨다운에 막히지 않는다)
            if (skyProductionVolumeFactor <= 0.001f)
                return;

            float _currentTime = Time.realtimeSinceStartup;
            if (_currentTime - lastFontPopPlayedTime < FontPopInterval)
                return;

            lastFontPopPlayedTime = _currentTime;
            Sound.PlayUI(SoundID.FontPop, skyProductionVolumeFactor);
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
            KillDeltaSequenceOnly();

            HideDeltaGlyphs();
        }

        private void KillDeltaSequenceOnly()
        {
            if (null != deltaMotionSequence && deltaMotionSequence.IsActive())
                deltaMotionSequence.Kill();

            deltaMotionSequence = null;
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

            float _scaledGlyphSize = GlyphPixelSize * pixelScale;
            mainVisibleLeftEdge = 0.0f;
            mainVisibleRightEdge = 0.0f;

            int _visibleLength = Mathf.Min(_length, _slotCount);
            float _layoutWidth = CalculateMainLayoutWidth(_visibleLength, _slotCount);
            if (alignmentMode == CurrencyFontAlignmentMode.Center)
                _layoutWidth = Mathf.Max(0.0f, centerModeWidth);

            float _cursor = GetMainStartCursor(_visibleLength, _layoutWidth);
            if (_visibleLength > 0)
                mainVisibleLeftEdge = _cursor;

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
                if (_isVisible)
                    mainVisibleRightEdge = _cursor;

                if (i < _slotCount - 1)
                {
                    char _nextChar = i + 1 < _visibleLength ? textBuffer[i + 1] : '0';
                    _cursor += GetSpacing(_char, _nextChar);
                }
            }

            if (null != rectTransform)
            {
                mainLayoutWidth = Mathf.Max(0.0f, _layoutWidth);
                rectTransform.sizeDelta = new Vector2(mainLayoutWidth, _scaledGlyphSize);
            }

            HideRemainingGlyphs(_slotCount);
            NotifyVisibleContentBoundsChanged();
        }

        private void NotifyVisibleContentBoundsChanged()
        {
            float _visibleWidth = VisibleContentWidth;
            if (Mathf.Approximately(notifiedVisibleLeftEdge, mainVisibleLeftEdge) &&
                Mathf.Approximately(notifiedVisibleWidth, _visibleWidth))
            {
                return;
            }

            notifiedVisibleLeftEdge = mainVisibleLeftEdge;
            notifiedVisibleWidth = _visibleWidth;
            VisibleContentBoundsChanged?.Invoke();
        }

        private float GetMainStartCursor(int _visibleLength, float _layoutWidth)
        {
            if (alignmentMode != CurrencyFontAlignmentMode.Center)
                return 0.0f;

            float _textWidth = CalculateTextLayoutWidth(_visibleLength);
            float _centerX = null != centerPivot ? GetChildLocalAnchorPositionX(centerPivot, _layoutWidth) : _layoutWidth * 0.5f;
            return SnapToPixel(_centerX - (_textWidth * 0.5f));
        }

        private float CalculateMainLayoutWidth(int _visibleLength, int _slotCount)
        {
            float _cursor = 0.0f;
            for (int i = 0; i < _slotCount; i++)
            {
                char _char = i < _visibleLength ? textBuffer[i] : '0';
                GlyphMetrics _metrics = GetMetrics(_char);
                _cursor += _metrics.InkWidth * pixelScale;

                if (i < _slotCount - 1)
                {
                    char _nextChar = i + 1 < _visibleLength ? textBuffer[i + 1] : '0';
                    _cursor += GetSpacing(_char, _nextChar);
                }
            }

            return Mathf.Max(0.0f, _cursor);
        }

        private float CalculateTextLayoutWidth(int _visibleLength)
        {
            if (_visibleLength <= 0)
                return 0.0f;

            float _cursor = 0.0f;
            for (int i = 0; i < _visibleLength; i++)
            {
                char _char = textBuffer[i];
                GlyphMetrics _metrics = GetMetrics(_char);
                _cursor += _metrics.InkWidth * pixelScale;

                if (i < _visibleLength - 1)
                    _cursor += GetSpacing(_char, textBuffer[i + 1]);
            }

            return Mathf.Max(0.0f, _cursor);
        }

        private void UpdateDeltaGlyphs(int _length, Color _color, bool _useAmountPivotB)
        {
            EnsureDeltaPoolSize(_length);

            Vector2 _startPosition = GetAmountStartPosition(_useAmountPivotB);
            float _cursor = _startPosition.x;
            float _scaledGlyphSize = GlyphPixelSize * pixelScale;

            for (int i = 0; i < _length; i++)
            {
                char _char = deltaTextBuffer[i];
                GlyphMetrics _metrics = GetMetrics(_char);
                if (0 == i)
                    _cursor += _metrics.LeftPadding * pixelScale;

                RawImage _image = deltaGlyphPool[i];
                RectTransform _imageRect = (RectTransform)_image.transform;

                _image.texture = GetTexture(_char);
                _image.raycastTarget = false;
                _image.color = _color;
                if (false == _image.gameObject.activeSelf)
                    _image.gameObject.SetActive(true);

                _imageRect.sizeDelta = new Vector2(_scaledGlyphSize, _scaledGlyphSize);
                _imageRect.anchoredPosition = new Vector2(
                    _cursor - (_metrics.LeftPadding * pixelScale) + (_scaledGlyphSize * 0.5f),
                    _startPosition.y);

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

        private Vector2 GetAmountStartPosition(bool _useAmountPivotB)
        {
            RectTransform _pivot = _useAmountPivotB ? amountPivotB : amountPivotA;
            if (null != _pivot)
                return _pivot.anchoredPosition;

            return new Vector2(mainVisibleRightEdge + (deltaStartSpacing * pixelScale), 0.0f);
        }

        private void CacheAmountPivots()
        {
            amountPivotA = FindDirectChildRect(AmountPivotAName);
            amountPivotB = FindDirectChildRect(AmountPivotBName);
        }

        private void CacheCenterPivot()
        {
            centerPivot = FindDirectChildRect(CenterPivotName);
        }

        private float GetChildLocalAnchorPositionX(RectTransform _child, float _parentWidth)
        {
            if (null == rectTransform)
                return _child.anchoredPosition.x;

            float _anchorX = Mathf.Lerp(_child.anchorMin.x, _child.anchorMax.x, _child.pivot.x);
            return ((_anchorX - rectTransform.pivot.x) * _parentWidth) + _child.anchoredPosition.x;
        }

        private float SnapToPixel(float _value)
        {
            float _pixelUnit = Mathf.Max(0.0001f, pixelScale);
            return Mathf.Round(_value / _pixelUnit) * _pixelUnit;
        }

        private RectTransform FindDirectChildRect(string _name)
        {
            Transform _child = transform.Find(_name);
            return null != _child ? _child as RectTransform : null;
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
            deltaAmountPhase = DeltaAmountPhase.None;
            activeDeltaAmount = 0L;
            activeDeltaLength = 0;
            currentDeltaShowTime = 0.0f;

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

        private enum DeltaAmountPhase
        {
            None,
            Showing,
            Holding,
            Hiding,
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
