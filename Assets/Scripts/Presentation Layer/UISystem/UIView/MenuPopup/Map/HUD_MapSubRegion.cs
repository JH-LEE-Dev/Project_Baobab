using System;
using UnityEngine;
using UnityEngine.EventSystems;
using PresentationLayer.UISystem.CustomNumber;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 맵 선택 UI에서 세부 지역(Sub-Region) 항목을 관리하는 클래스입니다.
    /// 레이캐스트 상호작용을 통해 외부로 위치 정보와 지역 번호를 전달합니다.
    /// </summary>
    public class HUD_MapSubRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        // //외부 의존성
        [Header("UI References")]
        [SerializeField] private CustomNumberDisplay numberDisplay; // 숫자 표시 컴포넌트
        [SerializeField] private GameObject lockObject;             // 잠금 시 활성화될 오브젝트
        [SerializeField] private GameObject unlockObject;           // 해제 시 활성화될 오브젝트
        [SerializeField] private HUD_ProgressBar progressBar;       // 진행도 표시 바
        [SerializeField] private ObjectMotionPlayer motionPlayer;   // 애니메이션 플레이어

        // //내부 의존성
        private RectTransform rect;
        private ForestEnvironmentInfo forestInfo;
        private int regionNumber = 0;
        private bool isSelected = false;
        private bool isLocked = false;
        private bool isInitialized = false;

        private Action<RectTransform> onHoverEnterEvent; // 커서 이동 및 표시용
        private Action onHoverExitEvent;                // 커서 숨김용
        private Action<int> onSelectEvent;              // 값 전달용

        private static readonly string hoverMotionKey = "Hover";
        private static readonly string clickMotionKey = "Click";

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 상위 매니저에서 콜백과 데이터를 주입합니다.
        /// </summary>
        public void Setup(ForestEnvironmentInfo _info, int _number, Action<RectTransform> _onHoverEnter, Action _onHoverExit, Action<int> _onSelect)
        {
            forestInfo = _info;
            Initialize(_number);

            // 데이터가 새로 설정되므로 선택 상태 초기화
            SetSelect(false);
            SetNumber(_number);

            onHoverEnterEvent = _onHoverEnter;
            onHoverExitEvent = _onHoverExit;
            onSelectEvent = _onSelect;
        }

        public void Initialize(int _number)
        {
            if (true == isInitialized)
                return;

            rect = GetComponent<RectTransform>();

            if (null == motionPlayer)
                motionPlayer = GetComponentInParent<ObjectMotionPlayer>();

            if (null != numberDisplay)
            {
                numberDisplay.Initialize();
                SetNumber(_number);
            }

            if (null != progressBar)
                progressBar.Initialize();

            SetSelect(false);
            SetLock(false); // 일단 해제 상태로 테스트 (필요 시 로직 추가)
            isInitialized = true;
        }

        public void SetProgress(float _ratio)
        {
            if (null != progressBar)
                progressBar.UpdateValue(_ratio);
        }

        public void SetLock(bool _isLock)
        {
            isLocked = _isLock;

            if (null != lockObject)
                lockObject.SetActive(isLocked);

            if (null != unlockObject)
                unlockObject.SetActive(false == isLocked);
        }

        public void SetNumber(int _number)
        {
            regionNumber = _number;

            if (null != numberDisplay)
                numberDisplay.SetNumber(regionNumber);
        }

        public void SetSelect(bool _isSelect)
        {
            isSelected = _isSelect;
        }

        public ForestType GetForestType()
        {
            return forestInfo.forestType;
        }

        public ForestEnvironmentInfo GetForestInfo()
        {
            return forestInfo;
        }

        public bool IsLocked()
        {
            return isLocked;
        }

        public int GetNumber()
        {
            return regionNumber;
        }

        public bool IsSelected()
        {
            return isSelected;
        }

        public RectTransform GetRectTransform()
        {
            if (null == rect)
                rect = GetComponent<RectTransform>();

            return rect;
        }

        // //Event System 구현부

        public void OnPointerEnter(PointerEventData _eventData)
        {
            if (true == isLocked)
                return;

            // 진입 시 커서 이동 및 애니메이션 재생
            onHoverEnterEvent?.Invoke(GetRectTransform());

            if (null != motionPlayer)
                motionPlayer.Play(hoverMotionKey);
        }

        public void OnPointerExit(PointerEventData _eventData)
        {
            if (true == isLocked)
                return;

            // 퇴장 시 커서 숨김 애니메이션 재생
            onHoverExitEvent?.Invoke();
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            if (true == isLocked)
                return;

            // 최종 선택된 지역 번호를 전달
            onSelectEvent?.Invoke(regionNumber);

            // 클릭 애니메이션 재생
            if (null != motionPlayer)
                motionPlayer.Play(clickMotionKey);
        }

        // //유니티 이벤트 함수

        private void Awake()
        {
            if (false == isInitialized)
                Initialize(regionNumber);
        }
    }
}
