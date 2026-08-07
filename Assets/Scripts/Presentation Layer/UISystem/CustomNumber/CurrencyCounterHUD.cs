using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.CustomNumber
{
    public enum CurrencyCounterLayoutMode
    {
        Left,
        CenterContent,
    }

    public class CurrencyCounterHUD : MonoBehaviour
    {
        [Serializable]
        private struct CurrencyIconEntry
        {
            public MoneyType moneyType;
            public Sprite icon;
        }

        [Header("UI References")]
        [SerializeField] private Image currencyIcon;
        [SerializeField] private CurrencyFontHUD currencyFontHUD;

        [Header("Currency Icons")]
        [SerializeField] private CurrencyIconEntry[] iconEntries;

        [Header("Layout")]
        [SerializeField] private CurrencyCounterLayoutMode layoutMode = CurrencyCounterLayoutMode.Left;
        [SerializeField] private float contentSpacing = 2.0f;

        [Header("Default")]
        [SerializeField] private MoneyType defaultMoneyType = MoneyType.Coin;
        [SerializeField] private long defaultValue;

        [Header("Debug")]
        [SerializeField] private long debugIncreaseAmount = 100;
        [SerializeField] private long debugDecreaseAmount = 100;

        private MoneyType currentMoneyType = MoneyType.None;
        private long currentValue;
        private bool initialized;
        private bool hasDisplayedValue;
        private RectTransform currencyIconRect;
        private RectTransform currencyFontRect;
        private Vector2 defaultIconPosition;
        private Vector2 defaultFontPosition;
        private bool hasCachedLayout;

        public void Initialize()
        {
            if (initialized)
                return;

            initialized = true;

            if (null == currencyIcon)
                currencyIcon = transform.Find("CurrencyIcon")?.GetComponent<Image>();

            if (null == currencyFontHUD)
                currencyFontHUD = GetComponentInChildren<CurrencyFontHUD>(true);

            CacheLayout();
            SubscribeFontBoundsChanged();
            currencyFontHUD?.Initialize();
            SetMoneyType(defaultMoneyType);
            SetNumber(defaultValue);
            RefreshLayout();
        }

        private void OnDestroy()
        {
            if (null != currencyFontHUD)
                currencyFontHUD.VisibleContentBoundsChanged -= RefreshLayout;
        }

        public void SetMoneyType(MoneyType _moneyType)
        {
            InitializeIfNeeded();

            currentMoneyType = _moneyType;

            Sprite _icon = GetIcon(_moneyType);
            if (null == currencyIcon)
                return;

            currencyIcon.sprite = _icon;
            currencyIcon.gameObject.SetActive(null != _icon);
            RefreshLayout();
        }

        public void SetNumber(long _value)
        {
            InitializeIfNeeded();

            if (hasDisplayedValue && currentValue == _value)
                return;

            currentValue = _value;
            hasDisplayedValue = true;
            currencyFontHUD?.SetNumber(currentValue);
        }

        public void SetNumberAnimated(long _value, bool _useAmountPivotB = false)
        {
            InitializeIfNeeded();

            if (currentValue == _value)
                return;

            long _previousValue = currentValue;
            currentValue = _value;
            hasDisplayedValue = true;
            currencyFontHUD?.SetNumberAnimated(currentValue, currentValue - _previousValue, _useAmountPivotB);
        }

        public void SetMode(CurrencyFontAlignmentMode _mode)
        {
            InitializeIfNeeded();
            currencyFontHUD?.SetMode(_mode);
        }

        public void SetLayoutMode(CurrencyCounterLayoutMode _mode)
        {
            InitializeIfNeeded();

            if (layoutMode == _mode)
                return;

            layoutMode = _mode;
            RefreshLayout();
        }

        public CurrencyCounterLayoutMode GetLayoutMode()
        {
            return layoutMode;
        }

        public MoneyType GetMoneyType()
        {
            return currentMoneyType;
        }

        public long GetNumber()
        {
            return currentValue;
        }

        private void InitializeIfNeeded()
        {
            if (false == initialized)
                Initialize();
        }

        private void CacheLayout()
        {
            if (hasCachedLayout)
                return;

            currencyIconRect = null != currencyIcon ? currencyIcon.rectTransform : null;
            currencyFontRect = null != currencyFontHUD ? currencyFontHUD.GetComponent<RectTransform>() : null;

            if (null != currencyIconRect)
                defaultIconPosition = currencyIconRect.anchoredPosition;

            if (null != currencyFontRect)
                defaultFontPosition = currencyFontRect.anchoredPosition;

            hasCachedLayout = true;
        }

        private void SubscribeFontBoundsChanged()
        {
            if (null == currencyFontHUD)
                return;

            currencyFontHUD.VisibleContentBoundsChanged -= RefreshLayout;
            currencyFontHUD.VisibleContentBoundsChanged += RefreshLayout;
        }

        private void RefreshLayout()
        {
            if (false == hasCachedLayout)
                CacheLayout();

            if (layoutMode == CurrencyCounterLayoutMode.Left)
            {
                RestoreDefaultLayout();
                return;
            }

            bool _hasIcon = null != currencyIconRect && currencyIcon.gameObject.activeSelf;
            bool _hasFont = null != currencyFontRect && currencyFontHUD.gameObject.activeSelf &&
                            currencyFontHUD.VisibleContentWidth > 0.0f;

            float _iconWidth = _hasIcon ? currencyIconRect.rect.width : 0.0f;
            float _fontWidth = _hasFont ? currencyFontHUD.VisibleContentWidth : 0.0f;
            float _spacing = _hasIcon && _hasFont ? Mathf.Max(0.0f, contentSpacing) : 0.0f;
            float _leftEdge = -(_iconWidth + _spacing + _fontWidth) * 0.5f;
            float _pixelUnit = null != currencyFontHUD ? currencyFontHUD.PixelUnit : 1.0f;
            _leftEdge = SnapToPixelGrid(_leftEdge, _pixelUnit);

            if (_hasIcon)
            {
                Vector2 _position = currencyIconRect.anchoredPosition;
                _position.x = SnapToPixelGrid(
                    _leftEdge + (_iconWidth * currencyIconRect.pivot.x),
                    _pixelUnit);
                currencyIconRect.anchoredPosition = _position;
                _leftEdge += _iconWidth + _spacing;
            }

            if (_hasFont)
            {
                Vector2 _position = currencyFontRect.anchoredPosition;
                _position.x = SnapToPixelGrid(
                    _leftEdge - currencyFontHUD.VisibleContentLeftEdge,
                    _pixelUnit);
                currencyFontRect.anchoredPosition = _position;
            }
        }

        private static float SnapToPixelGrid(float _value, float _pixelUnit)
        {
            _pixelUnit = Mathf.Max(0.0001f, _pixelUnit);
            return Mathf.Round(_value / _pixelUnit) * _pixelUnit;
        }

        private void RestoreDefaultLayout()
        {
            if (null != currencyIconRect)
                currencyIconRect.anchoredPosition = defaultIconPosition;

            if (null != currencyFontRect)
                currencyFontRect.anchoredPosition = defaultFontPosition;
        }

        private Sprite GetIcon(MoneyType _moneyType)
        {
            if (null == iconEntries)
                return null;

            for (int i = 0; i < iconEntries.Length; i++)
            {
                if (iconEntries[i].moneyType == _moneyType)
                    return iconEntries[i].icon;
            }

            return null;
        }

        private void PlayPlusMotion()
        {
            currencyFontHUD?.PlayIncreaseMotion();
        }

        private void PlayMinusMotion()
        {
            currencyFontHUD?.PlayDecreaseMotion();
        }

        [Button("SetCurrencyText")]
        private void SetCurrencyText()
        {
            SetNumber(1234);
        }

        [Button("Test CenterPivot")]
        private void Test1()
        {
            currencyFontHUD.SetMode(CurrencyFontAlignmentMode.Center);
        }

        [Button("Test LeftPivot")]
        private void Test2()
        {
            currencyFontHUD.SetMode(CurrencyFontAlignmentMode.Left);
        }



        [Button("Debug Play Plus Motion (Pivot B)")]
        private void DebugPlayPlusMotionB()
        {
            InitializeIfNeeded();
            SetNumberAnimated(currentValue + Math.Max(1L, debugIncreaseAmount), true);
        }



        [Button("Debug Play Plus Motion")]
        private void DebugPlayPlusMotion()
        {
            InitializeIfNeeded();
            SetNumberAnimated(currentValue + Math.Max(1L, debugIncreaseAmount));
        }

        [Button("Debug Play Minus Motion")]
        private void DebugPlayMinusMotion()
        {
            InitializeIfNeeded();
            SetNumberAnimated(Math.Max(0L, currentValue - Math.Max(1L, debugDecreaseAmount)));
        }

        [Button("Debug Replay Increase FX")]
        private void DebugReplayIncreaseFx()
        {
            InitializeIfNeeded();
            PlayPlusMotion();
        }

        [Button("Debug Replay Decrease FX")]
        private void DebugReplayDecreaseFx()
        {
            InitializeIfNeeded();
            PlayMinusMotion();
        }

    }
}
