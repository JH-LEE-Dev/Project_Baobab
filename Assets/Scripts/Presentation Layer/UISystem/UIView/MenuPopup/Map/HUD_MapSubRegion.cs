using System;
using UnityEngine;
using UnityEngine.EventSystems;
using PresentationLayer.UISystem.CustomNumber;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 맵 선택 UI에서 세부 지역(Sub-Region) 항목을 관리하는 클래스입니다.
    /// 레이캐스트 상호작용을 통해 외부로 위치 정보와 지역 번호를 전달합니다.
    /// </summary>
    public class HUD_MapSubRegion : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
    {
        // //외부 의존성
        [Header("UI References")]
        [SerializeField] private CustomNumberDisplay numberDisplay; // 숫자 표시 컴포넌트

        // //내부 의존성
        private RectTransform rect;
        private int regionNumber = 0;
        private bool isSelected = false;
        private bool isInitialized = false;

        private Action<RectTransform> onHoverEvent; // 커서 이동용
        private Action<int> onSelectEvent;         // 값 전달용

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 상위 매니저에서 콜백과 데이터를 주입합니다.
        /// </summary>
        public void Setup(int _number, Action<RectTransform> _onHover, Action<int> _onSelect)
        {
            Initialize(_number);

            onHoverEvent = _onHover;
            onSelectEvent = _onSelect;
        }

        /// <summary>
        /// 서브 지역 항목을 초기화합니다.
        /// </summary>
        public void Initialize(int _number)
        {
            if (true == isInitialized)
                return;

            rect = GetComponent<RectTransform>();

            if (null != numberDisplay)
            {
                numberDisplay.Initialize();
                SetNumber(_number);
            }

            SetSelect(false);
            isInitialized = true;
        }

        /// <summary>
        /// 지역 번호를 설정합니다.
        /// </summary>
        public void SetNumber(int _number)
        {
            regionNumber = _number;

            if (null != numberDisplay)
                numberDisplay.SetNumber(regionNumber);
        }

        /// <summary>
        /// 현재 설정된 지역 번호를 반환합니다.
        /// </summary>
        public int GetNumber()
        {
            return regionNumber;
        }

        /// <summary>
        /// 선택 상태를 설정합니다.
        /// </summary>
        public void SetSelect(bool _isSelect)
        {
            isSelected = _isSelect;
        }

        /// <summary>
        /// 현재 선택 여부를 반환합니다.
        /// </summary>
        public bool IsSelected()
        {
            return isSelected;
        }

        /// <summary>
        /// 위치 추적을 위해 RectTransform을 반환합니다.
        /// </summary>
        public RectTransform GetRectTransform()
        {
            if (null == rect)
                rect = GetComponent<RectTransform>();

            return rect;
        }

        // //Event System 구현부

        public void OnPointerEnter(PointerEventData _eventData)
        {
            // 마우스가 진입하면 커서 이동을 위해 자신의 RectTransform을 전달
            onHoverEvent?.Invoke(GetRectTransform());
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            // 클릭 시 최종 선택된 지역 번호를 전달
            onSelectEvent?.Invoke(regionNumber);
        }

        // //유니티 이벤트 함수

        private void Awake()
        {
            if (false == isInitialized)
                Initialize(regionNumber);
        }
    }
}
