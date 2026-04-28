using UnityEngine;
using TMPro;
using Unity.VisualScripting;
using UnityEngine.Rendering;
using System;
using UnityEngine.Events;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 라이플 장비의 HUD 표시를 관리하는 클래스.
    /// 탄약 시스템 및 총알 시각화 컴포넌트를 포함합니다.
    /// </summary>
    public class HUD_EquipmentRifle : HUD_EquipmentItem
    {
        // //외부 의존성
        [Header("Rifle Specific UI")]
        [SerializeField] private TextMeshProUGUI totalAmmoText;      // 총 보유 탄약 표시 텍스트
        [SerializeField] private HUD_EquipmentBullet bulletDisplay; // 개별 총알 시각화 컴포넌트

        [Header("UI Ref")]
        [SerializeField] private GameObject ammoBox;

        public override void Initialize()
        {
            base.Initialize();

            if (null == totalAmmoText)
                totalAmmoText = GetComponentInChildren<TextMeshProUGUI>();

            if (null == bulletDisplay)
                bulletDisplay = GetComponentInChildren<HUD_EquipmentBullet>();

            bulletDisplay?.Initialize();
        }

        protected override void UpdateVisuals()
        {
            base.UpdateVisuals();

            ammoBox?.SetActive(isActive);
            bulletDisplay?.SetActive(isActive);
            //bulletDisplay?.총알 흩어질지, 모일지 연출
        }

        public void PlayReloadMotion(float _duration, UnityAction _callEvent)
        {
            if (null == bulletDisplay)
                return;

            bulletDisplay.PlayReloadMotion(_duration, _callEvent);
        }

        public void PlayResetMotion(float _duration)
        {
            if (null == bulletDisplay)
                return;

            bulletDisplay.PlayResetMotion(_duration);
        }

        /// <summary>
        /// 탄약 정보를 업데이트합니다.
        /// </summary>
        /// <param name="_currentMag">현재 탄창 내 탄약</param>
        /// <param name="_maxMag">최대 탄창 용량</param>
        /// <param name="_totalAmmo">총 보유 탄약</param>
        public void UpdateAmmo(int _currentMag, int _maxMag, int _totalAmmo)
        {
            UpdateTotalAmmoText(_totalAmmo);

            if (null == bulletDisplay)
                return;

            bulletDisplay.UpdateBulletStatus(_currentMag, _maxMag, _totalAmmo);
        }

        /// <summary>
        /// 총 보유 탄약 텍스트를 업데이트합니다.
        /// </summary>
        private void UpdateTotalAmmoText(int _totalAmmo)
        {
            if (null == totalAmmoText)
                return;

            // GC 최소화를 위해 단순 대입 사용 (필요 시 캐싱된 문자열 또는 StringBuilder 활용 가능)
            totalAmmoText.text = _totalAmmo.ToString();
        }

        // //유니티 이벤트 함수
    }
}
