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
        [SerializeField] private RectTransform selectorCursor; // 선택 표시 커서

        // //내부 의존성
        private int currentSelectedNumber = -1;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 서브 셀렉터의 일회성 설정을 수행합니다.
        /// </summary>
        public void Initialize()
        {
            if (true == isInitialized)
                return;

            if (null == subRegions)
                return;

            for (int _i = 0; _i < subRegions.Length; _i++)
            {
                if (null == subRegions[_i])
                    continue;

                // 1부터 시작하는 고정 번호를 부여하고 콜백 주입
                subRegions[_i].Setup(_i + 1, OnRegionHovered, OnRegionSelected);
                subRegions[_i].gameObject.SetActive(false);
            }

            if (null != selectorCursor)
                selectorCursor.gameObject.SetActive(false);

            isInitialized = true;
        }

        /// <summary>
        /// 메인 지역 전환 시 호출하여 표시할 서브 지역 개수를 갱신합니다.
        /// </summary>
        /// <param name="_activeCount">표시할 지역 개수 (1~3)</param>
        public void SetSubRegionCount(int _activeCount)
        {
            if (false == isInitialized)
                Initialize();

            // 새로운 지역으로 전환되므로 선택 상태와 커서 초기화
            currentSelectedNumber = -1;
            
            if (null != selectorCursor)
                selectorCursor.gameObject.SetActive(false);

            for (int _i = 0; _i < subRegions.Length; _i++)
            {
                if (null == subRegions[_i])
                    continue;

                // 요청된 개수만큼만 활성화하여 UI 축소/확장 연출
                subRegions[_i].gameObject.SetActive(_i < _activeCount);
            }
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

            if (_index < 0 || _index >= subRegions.Length)
                return;

            HUD_MapSubRegion _target = subRegions[_index];
            if (null == _target || false == _target.gameObject.activeSelf)
                return;

            OnRegionHovered(_target.GetRectTransform());
            OnRegionSelected(_target.GetNumber());
        }

        // //내부 로직 (콜백 메서드)

        private void OnRegionHovered(RectTransform _targetRect)
        {
            if (null == selectorCursor || null == _targetRect)
                return;

            // 특정 구역이 골라지면(마우스 오버 등) 커서 활성화
            if (false == selectorCursor.gameObject.activeSelf)
                selectorCursor.gameObject.SetActive(true);

            // 커서를 해당 항목의 위치로 즉시 이동
            selectorCursor.position = _targetRect.position;
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
