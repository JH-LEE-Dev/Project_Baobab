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

        // //내부 의존성
        private Action onSelectionChanged;
        private Action<RectTransform, Vector2> onHoverEnterEvent;
        private Action onHoverExitEvent;
        private int currentSelectedNumber = -1;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize(Action _onSelectionChanged = null, Action<RectTransform, Vector2> _onHoverEnter = null, Action _onHoverExit = null)
        {
            if (true == isInitialized)
                return;

            onSelectionChanged = _onSelectionChanged;
            onHoverEnterEvent = _onHoverEnter;
            onHoverExitEvent = _onHoverExit;

            if (null != subRegions)
            {
                for (int _i = 0; _i < subRegions.Length; _i++)
                    if (null != subRegions[_i])
                        subRegions[_i].gameObject.SetActive(false);
            }

            isInitialized = true;
        }

        public void SetSubRegions(List<ForestEnvironmentInfo> _forestDatas)
        {
            if (false == isInitialized)
                Initialize();

            currentSelectedNumber = -1;

            int _dataCount = _forestDatas.Count;

            for (int _i = 0; _i < subRegions.Length; _i++)
            {
                if (null == subRegions[_i]) continue;

                if (_i < _dataCount)
                {
                    subRegions[_i].PlayOpenAnimation();
                    subRegions[_i].Setup(_forestDatas[_i], _i + 1, OnRegionHoverEntered, OnRegionHoverExited, OnRegionSelected);
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
        }

        public void ClearSelection()
        {
            currentSelectedNumber = -1;

            if (null != subRegions)
            {
                for (int _i = 0; _i < subRegions.Length; _i++)
                    if (null != subRegions[_i])
                        subRegions[_i].SetSelect(false);
            }

            onSelectionChanged?.Invoke();
        }

        // //내부 로직 (콜백 메서드)

        private void OnRegionHoverEntered(RectTransform _targetRect, Vector2 _targetSize) => onHoverEnterEvent?.Invoke(_targetRect, _targetSize);

        private void OnRegionHoverExited() => onHoverExitEvent?.Invoke();

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