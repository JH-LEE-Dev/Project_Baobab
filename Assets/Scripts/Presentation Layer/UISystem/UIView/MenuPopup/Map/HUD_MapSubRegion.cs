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
        [SerializeField] private CustomNumberDisplay numberDisplay;
        [SerializeField] private GameObject lockObject;
        [SerializeField] private GameObject focusObject;
        [SerializeField] private GameObject focusGauge;
        [SerializeField] private HUD_ProgressBar progressBar;
        [SerializeField] private ObjectMotionPlayer motionPlayer;

        // //내부 의존성
        private RectTransform rect;
        private ForestEnvironmentInfo forestInfo;
        private Action<RectTransform> onHoverEnterEvent;
        private Action onHoverExitEvent;
        private Action<int> onSelectEvent;

        private MotionEntry enterMotion;
        private MotionEntry exitMotion;
        private MotionEntry clickMotion;

        private int regionNumber = 0;
        private bool isSelected = false;
        private bool isLocked = false;
        private bool isInitialized = false;
        private bool isClicked = false;

        private static readonly string hoverMotionKey = "Hover";
        private static readonly string hoverOffMotionKey = "HoverOff";
        private static readonly string clickMotionKey = "Click";

        // //퍼블릭 초기화 및 제어 메서드

        public void Setup(ForestEnvironmentInfo _info, int _number, Action<RectTransform> _onHoverEnter, Action _onHoverExit, Action<int> _onSelect)
        {
            forestInfo = _info;
            Initialize(_number);

            SetSelect(false);
            SetNumber(_number);
            SetLock(!_info.bCanAccess);
            SetProgress(Mathf.Clamp01(_info.currentHiddenGauge / _info.limitHiddenGauge));

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

            if (null != focusObject)
                focusObject.SetActive(false);

            if (null != focusGauge)
                focusGauge.SetActive(false);

            SetSelect(false);
            SetLock(false); 
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
            if (null != lockObject) lockObject.SetActive(isLocked);
            if (null != numberDisplay) numberDisplay.gameObject.SetActive(!isLocked);
        }

        public void SetNumber(int _number)
        {
            regionNumber = _number;
            if (null != numberDisplay) numberDisplay.SetNumber(regionNumber);
        }

        public void PlayOpenAnimation() => gameObject.SetActive(true);
        public void PlayCloseAnimation() => gameObject.SetActive(false);

        public void SetSelect(bool _isSelect)
        {
            isSelected = _isSelect;
            
            if (null != focusObject) 
                focusObject.SetActive(isSelected);

            if (null != focusGauge) 
                focusGauge.SetActive(isSelected);
        }

        public ForestType GetForestType() => forestInfo.forestType;
        public ForestEnvironmentInfo GetForestInfo() => forestInfo;
        public bool IsLocked() => isLocked;
        public int GetNumber() => regionNumber;
        public bool IsSelected() => isSelected;

        public RectTransform GetRectTransform()
        {
            if (null == rect) rect = GetComponent<RectTransform>();
            return rect;
        }

        // //Event System 구현부

        public void OnPointerEnter(PointerEventData _eventData)
        {
            if (true == isLocked) return;
            onHoverEnterEvent?.Invoke(GetRectTransform());

            if (null != motionPlayer && !isClicked)
            {
                motionPlayer.SettingEntryMotion(exitMotion, true, true);
                motionPlayer.SettingEntryMotion(clickMotion, true, true);
                enterMotion = motionPlayer.Play(hoverMotionKey, bReset: true);
            }
        }

        public void OnPointerExit(PointerEventData _eventData)
        {
            if (true == isLocked) return;
            onHoverExitEvent?.Invoke();

            if (null != motionPlayer && !isClicked)
            {
                motionPlayer.SettingEntryMotion(enterMotion, true, true);
                motionPlayer.SettingEntryMotion(clickMotion, true, true);
                exitMotion = motionPlayer.Play(hoverOffMotionKey, bReset: true);
            }
        }

        public void OnPointerClick(PointerEventData _eventData)
        {
            if (true == isLocked) return;
            onSelectEvent?.Invoke(regionNumber);

            isClicked = true;
            if (null != motionPlayer)
            {
                motionPlayer.SettingEntryMotion(enterMotion, true, true);
                motionPlayer.SettingEntryMotion(exitMotion, true, true);
                clickMotion = motionPlayer.Play(clickMotionKey, bReset: true, _onComplete: OnClickAnimationComplete);
            }
        }

        private void OnClickAnimationComplete() => isClicked = false;

        // //유니티 이벤트 함수

        private void Awake()
        {
            if (false == isInitialized)
                Initialize(regionNumber);
        }
    }
}
