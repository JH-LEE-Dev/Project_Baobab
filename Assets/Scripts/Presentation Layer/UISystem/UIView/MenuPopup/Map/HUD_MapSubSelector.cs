using System;
using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 최대 3개의 HUD_MapSubRegion을 관리하며, 커서 이동 및 선택된 지역 번호를 추적합니다.
    /// </summary>
    public class HUD_MapSubSelector : MonoBehaviour
    {
        // //외부 의존성
        [Header("UI Elements")]
        [SerializeField] private HUD_MapSubRegion[] subRegions; 
        [SerializeField] private UISelectionCursor selectorCursor;

        // //내부 의존성
        private Action onSelectionChanged;
        private int currentSelectedNumber = -1;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize(Action _onSelectionChanged = null)
        {
            if (true == isInitialized)
                return;

            onSelectionChanged = _onSelectionChanged;

            if (null != subRegions)
            {
                for (int _i = 0; _i < subRegions.Length; _i++)
                    if (null != subRegions[_i])
                        subRegions[_i].gameObject.SetActive(false);
            }

            if (null != selectorCursor)
                selectorCursor.Initialize(selectorCursor.CursorSize);

            isInitialized = true;
        }

        public void SetSubRegions(List<ForestEnvironmentInfo> _forestDatas)
        {
            if (false == isInitialized)
                Initialize();

            currentSelectedNumber = -1;
            
            if (null != selectorCursor)
                selectorCursor.HideImmediately();

            int _dataCount = _forestDatas.Count;

            for (int _i = 0; _i < subRegions.Length; _i++)
            {
                if (null == subRegions[_i]) continue;

                if (_i < _dataCount)
                {
                    subRegions[_i].PlayOpenAnimation();
                    subRegions[_i].Setup(_forestDatas[_i], _i + 1, OnRegionHoverEntered, OnRegionHoverExited, OnRegionSelected);
                    subRegions[_i].SetProgress(_forestDatas[_i].currentHiddenGauge / _forestDatas[_i].limitHiddenGauge);
                }
                else
                {
                    subRegions[_i].PlayCloseAnimation();
                }
            }
        }

        public void Close()
        {
            if (null == subRegions)
                return;

            for (int _i = 0; _i < subRegions.Length; _i++)
            {
                if (null != subRegions[_i] && subRegions[_i].gameObject.activeSelf)
                    subRegions[_i].PlayCloseAnimation();
            }
        }

        public void UpdateHiddenGauges(List<ForestEnvironmentInfo> _forestDatas)
        {
            int _dataCount = _forestDatas.Count;

            for (int _i = 0; _i < subRegions.Length; _i++)
            {
                if (null != subRegions[_i] && _i < _dataCount)
                {
                    float _ratio = _forestDatas[_i].currentHiddenGauge / _forestDatas[_i].limitHiddenGauge;
                    subRegions[_i].SetProgress(_ratio);
                }
            }
        }

        public ForestType GetSelectedForestType()
        {
            int _index = currentSelectedNumber - 1;
            if (null == subRegions || _index < 0 || _index >= subRegions.Length)
                return ForestType.None;

            return subRegions[_index].GetForestType();
        }

        public ForestEnvironmentInfo GetSelectedForestInfo()
        {
            int _index = currentSelectedNumber - 1;
            if (null == subRegions || _index < 0 || _index >= subRegions.Length)
                return default;

            return subRegions[_index].GetForestInfo();
        }

        public int GetSelectedRegionNumber() => currentSelectedNumber;

        public void ForceSelectRegion(int _index)
        {
            if (null == subRegions || _index < 0 || _index >= subRegions.Length)
                return;

            HUD_MapSubRegion _target = subRegions[_index];
            if (null == _target || !_target.gameObject.activeSelf)
                return;

            OnRegionSelected(_target.GetNumber());
        }

        public void SetVisibility(bool _isVisible)
        {
            if (gameObject.activeSelf == _isVisible)
                return;

            gameObject.SetActive(_isVisible);

            if (null != selectorCursor && !_isVisible)
                selectorCursor.HideImmediately();
        }

        public void ClearSelection()
        {
            currentSelectedNumber = -1;

            if (null != selectorCursor)
                selectorCursor.HideImmediately();

            if (null != subRegions)
            {
                for (int _i = 0; _i < subRegions.Length; _i++)
                    if (null != subRegions[_i])
                        subRegions[_i].SetSelect(false);
            }

            onSelectionChanged?.Invoke();
        }

        // //내부 로직 (콜백 메서드)

        private void OnRegionHoverEntered(RectTransform _targetRect)
        {
            if (null != selectorCursor && null != _targetRect)
                selectorCursor.Show(_targetRect);
        }

        private void OnRegionHoverExited() => selectorCursor?.Hide();

        private void OnRegionSelected(int _number)
        {
            currentSelectedNumber = _number;

            if (null != subRegions)
            {
                for (int _i = 0; _i < subRegions.Length; _i++)
                    if (null != subRegions[_i])
                        subRegions[_i].SetSelect(subRegions[_i].GetNumber() == _number);
            }

            onSelectionChanged?.Invoke();
        }
    }
}
