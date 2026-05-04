using UnityEngine;
using PresentationLayer.UISystem.CustomNumber;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 맵 선택 UI에서 세부 지역(Sub-Region) 항목을 관리하는 클래스입니다.
    /// CustomNumberDisplay를 통해 지역 번호를 표시하고 자신의 위치 정보를 제공합니다.
    /// </summary>
    public class HUD_MapSubRegion : MonoBehaviour
    {
        // //외부 의존성
        [Header("UI References")]
        [SerializeField] private CustomNumberDisplay numberDisplay; // 숫자 표시 컴포넌트

        // //내부 의존성
        private RectTransform rect;
        private int regionNumber = 0;
        private bool isSelected = false;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

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

        // //유니티 이벤트 함수

        private void Awake()
        {
            if (false == isInitialized)
                Initialize(regionNumber);
        }
    }
}
