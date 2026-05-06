using System;
using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 최대 3개의 HUD_MapSubRegion을 관리하며, 커서 이동 및 선택된 지역 번호를 추적합니다.
    /// </summary>
    public class HUD_MapSubSelector : MonoBehaviour
    {
        // //외부 의존성
        [Header("UI Elements")]
        [SerializeField] private HUD_MapSubRegion[] subRegions; // 최대 3개의 지역 항목
        [SerializeField] private UISelectionCursor selectorCursor; // 선택 표시 커서

        // //내부 의존성
        private int currentSelectedNumber = -1;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (true == isInitialized)
                return;

            if (null == subRegions)
                return;

            for (int _i = 0; _i < subRegions.Length; _i++)
                if (null != subRegions[_i])
                    subRegions[_i].gameObject.SetActive(false);

            if (null != selectorCursor)
                selectorCursor.Initialize(selectorCursor.CursorSize);

            isInitialized = true;
        }

        public void SetSubRegions(System.Collections.Generic.List<ForestEnvironmentInfo> _forestDatas)
        {
            if (false == isInitialized)
                Initialize();

            // 새로운 지역으로 전환되므로 선택 상태와 커서 초기화
            currentSelectedNumber = -1;
            
            if (null != selectorCursor)
                selectorCursor.HideImmediately();

            int _dataCount = _forestDatas.Count;
            Debug.Log($"Region {_dataCount}");

            for (int _i = 0; _i < subRegions.Length; _i++)
                if (null != subRegions[_i])
                    if (_i < _dataCount)
                    {
                        subRegions[_i].gameObject.SetActive(true);
                        subRegions[_i].Setup(_forestDatas[_i], _i + 1, OnRegionHovered, OnRegionSelected);
                    }
                    else
                        subRegions[_i].gameObject.SetActive(false);
        }

        public ForestType GetSelectedForestType()
        {
            if (-1 == currentSelectedNumber || null == subRegions)
                return ForestType.None;

            int _index = currentSelectedNumber - 1;
            if (0 > _index || _index >= subRegions.Length)
                return ForestType.None;

            return subRegions[_index].GetForestType();
        }

        /// <summary>
        /// 현재 선택된 지역의 환경 정보를 반환합니다.
        /// </summary>
        public ForestEnvironmentInfo GetSelectedForestInfo()
        {
            if (-1 == currentSelectedNumber || null == subRegions)
                return default;

            int _index = currentSelectedNumber - 1;
            if (0 > _index || _index >= subRegions.Length)
                return default;

            return subRegions[_index].GetForestInfo();
        }

        /// <summary>
        /// 현재 선택된 지역 번호를 반환합니다.
        /// </summary>
        public int GetSelectedRegionNumber()
        {
            return currentSelectedNumber;
        }

        /// <summary>
        /// 특정 지역을 강제로 선택 상태로 만듭니다. (초기 설정 등)
        /// </summary>
        public void ForceSelectRegion(int _index)
        {
            if (null == subRegions)
                return;

            if (0 > _index || _index >= subRegions.Length)
                return;

            HUD_MapSubRegion _target = subRegions[_index];
            if (null == _target || false == _target.gameObject.activeSelf)
                return;

            OnRegionHovered(_target.GetRectTransform());
            OnRegionSelected(_target.GetNumber());
        }

        /// <summary>
        /// 서브 셀렉터의 표시 여부를 설정합니다.
        /// </summary>
        public void SetVisibility(bool _isVisible)
        {
            this.gameObject.SetActive(_isVisible);

            if (null != selectorCursor)
            {
                if (false == _isVisible)
                    selectorCursor.HideImmediately();
                else
                {
                    // 보이게 할 때는 이미 선택된 번호가 있을 때만 커서를 활성화합니다.
                    if (-1 != currentSelectedNumber)
                    {
                        int _index = currentSelectedNumber - 1;
                        if (_index >= 0 && _index < subRegions.Length && null != subRegions[_index])
                            selectorCursor.Show(subRegions[_index].GetRectTransform());
                    }
                }
            }
        }

        // //내부 로직 (콜백 메서드)

        private void OnRegionHovered(RectTransform _targetRect)
        {
            if (null == selectorCursor || null == _targetRect)
                return;

            selectorCursor.Show(_targetRect);
        }

        private void OnRegionSelected(int _number)
        {
            currentSelectedNumber = _number;
        }
            
        // //유니티 이벤트 함수

        private void Awake()
        {
        }
    }
}
