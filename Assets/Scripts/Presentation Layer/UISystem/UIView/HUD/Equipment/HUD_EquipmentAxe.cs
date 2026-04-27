using UnityEngine;
//using PresentationLayer.UISystem.UIView.HUD.Common; // HUD_ProgressBar 등 공통 UI 요소 네임스페이스 가정

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    public class HUD_EquipmentAxe : HUD_EquipmentItem
    {
        // //외부 의존성
        [Header("Axe Specific UI")]
        [SerializeField] private HUD_ProgressBar axeGaugeBar; // 도끼 특수 게이지 바
        [SerializeField] private GameObject gaugeOutline;

        // //내부 의존성

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 초기 설정 및 의존성 구성.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            if (null == axeGaugeBar)
                axeGaugeBar = GetComponentInChildren<HUD_ProgressBar>();

            if (null != axeGaugeBar)
                axeGaugeBar.Initialize();
        }

        protected override void UpdateVisuals()
        {
            base.UpdateVisuals();

            gaugeOutline?.SetActive(isActive);
        }

        /// <summary>
        /// 도끼 게이지 값을 업데이트합니다.
        /// </summary>
        /// <param name="_ratio">0~1 사이의 비율</param>
        public void UpdateGauge(float _ratio)
        {
            if (null == axeGaugeBar)
                return;

            axeGaugeBar.UpdateValue(_ratio);
        }

        // //유니티 이벤트 함수
    }
}
