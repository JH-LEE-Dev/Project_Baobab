using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
//using PresentationLayer.UISystem.UIView.HUD.Common; // HUD_ProgressBar 등 공통 UI 요소 네임스페이스 가정

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    public class HUD_EquipmentAxe : HUD_EquipmentItem
    {
        private enum AxeMode { DB100, DB75, DB50, DB25, ZERO }

        [Header("Images")]
        [SerializeField] List<Sprite> axeImages;

        // //외부 의존성
        [Header("Axe Specific UI")]
        [SerializeField] private Image axeImage; // 도끼 이미지
        [SerializeField] private HUD_HPBar axeGaugeBar; // 도끼 특수 게이지 바

        // //내부 의존성
        private AxeMode axeMode = AxeMode.DB100;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 초기 설정 및 의존성 구성.
        /// </summary>
        public override void Initialize()
        {
            base.Initialize();

            if (null != axeGaugeBar)
                axeGaugeBar.Initialize();
        }

        protected override void UpdateVisuals()
        {
            base.UpdateVisuals();
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
            UpdateAxeImage(_ratio);
        }

        private void UpdateAxeImage(float _ratio)
        {
            if (null == axeImages || null == axeImage)
                return;

            if (_ratio > 0.75f)
                axeMode = AxeMode.DB100;
            else if (_ratio > 0.5f)
                axeMode = AxeMode.DB75;
            else if (_ratio > 0.25f)
                axeMode = AxeMode.DB50;
            else if (_ratio > 0.0f)
                axeMode = AxeMode.DB25;
            else
                axeMode = AxeMode.ZERO;

            axeImage.sprite = axeImages[(int)axeMode];
        }
        // //유니티 이벤트 함수
    }
}
