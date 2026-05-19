using System;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.CustomNumber
{
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

        public void Initialize()
        {
            if (initialized)
                return;

            initialized = true;

            if (null == currencyIcon)
                currencyIcon = transform.Find("CurrencyIcon")?.GetComponent<Image>();

            if (null == currencyFontHUD)
                currencyFontHUD = GetComponentInChildren<CurrencyFontHUD>(true);

            currencyFontHUD?.Initialize();
            SetMoneyType(defaultMoneyType);
            SetNumber(defaultValue);
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
