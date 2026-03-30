using System;
using PresentationLayer.ObjectSystem;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.HUD
{
    /// <summary>
    /// HUD에서 진행도를 표시하는 프로그레스 바입니다.
    /// 지정된 시간 동안만 활성화되고 이후 콜백을 통해 반납될 수 있습니다.
    /// </summary>
    public class HUD_ProgressBar : MonoBehaviour, IPoolable
    {
        // //외부 의존성
        [SerializeField] private Slider progressSlider;

        private RectTransform rect;

        // //내부 의존성
        private float currentValue = 0.0f;
        
        private float activeTimer = 0.0f;
        private float showYOffset = 0f;
        private bool isTimerActive = false;
        
        private Action<HUD_ProgressBar> onHideCallback;

        private GameObject targetObj;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (null == progressSlider)
            {
                progressSlider = GetComponentInChildren<Slider>();
            }

            if (null == progressSlider)
                return;

            rect = progressSlider?.GetComponent<RectTransform>();

            progressSlider.minValue = 0.0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = 1f;
        }

        public void UpdateValue(float _ratio)
        {
            currentValue = _ratio;

            if (null != progressSlider)
                progressSlider.value = currentValue;
        }

        public void UpdateTargetObj(GameObject _target) => targetObj = _target;
        public void UpdateYOffset(float _in) =>  showYOffset = _in;

        /// <summary>
        /// 지정된 시간 동안 활성화하고, 종료 시 실행할 콜백을 등록합니다.
        /// </summary>
        public void TriggerActiveForDuration(float _duration, Action<HUD_ProgressBar> _onHide = null)
        {
            if (0.0f >= _duration)
            {
                onHideCallback = _onHide;
                OnHide();
                return;
            }

            activeTimer = _duration;
            isTimerActive = true;
            onHideCallback = _onHide;
            OnShow();
        }

        public void OnShow()
        {
            if (false == gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void OnHide()
        {
            if (true == gameObject.activeSelf)
            {
                isTimerActive = false;
                activeTimer = 0.0f;
                gameObject.SetActive(false);
                
                // 반납 등을 위한 콜백 실행
                onHideCallback?.Invoke(this);
                onHideCallback = null;
            }
        }

        // //IPoolable 구현부

        public void OnSpawn()
        {
            Initialize();
        }

        public void OnDespawn()
        {
            isTimerActive = false;
            activeTimer = 0.0f;
            onHideCallback = null;
        }

        // //유니티 이벤트 함수

        private void Update()
        {
            if (false == isTimerActive)
            {
                return;
            }

            activeTimer -= Time.deltaTime;

            if (0.0f >= activeTimer)
            {
                OnHide();
            }
        }

        public void LateUpdate()
        {
            if (true == isTimerActive && null != targetObj)
            {
                Vector3 newPos = targetObj.transform.position;
                newPos.y += showYOffset;

                rect.position = newPos;
                //Debug.Log(newPos + ", " + rect.position);
            }
        }

        private void OnDestroy()
        {
        }
    }
}
