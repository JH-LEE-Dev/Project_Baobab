// ──────────────────────────────────────────────────────────────────────────
// [사용 안 함 — 기획 결정]  총(소총 / 리코셰) 계열
//
// 이 계열은 쓰지 않기로 결정된 기능입니다. 배선이 빠진 것이 아니라 "안 쓰기로 한 것"이므로,
// 동작하지 않는다고 해서 버그로 보고 되살리거나 배선을 복구하지 마십시오.
//
// 현재 상태: SkillDataBase_Full에 소총·리코셰 커맨드 8종 항목이 없어 WeaponMode 전환이 열리지 않습니다.
//            공격은 항상 도끼 모드로 고정됩니다.
//
// 코드를 지우지 않고 주석만 남긴 이유: WeaponMode가 AttackComponent(15곳) · ArmComponent(6곳) 같은
//   "지금 돌아가는 공격 경로"에 박혀 있어, 삭제가 곧 전투 경로 리팩터가 됩니다.
// 정리하려면 에디터에서 컴파일이 도는 상태로 독립 커밋으로 진행하십시오.
// ──────────────────────────────────────────────────────────────────────────
using UnityEngine;
using TMPro;
using UnityEngine.Rendering;
using System;
using UnityEngine.Events;
using PresentationLayer.UISystem.CustomNumber;

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
        [SerializeField] private CustomNumberDisplay ammoDisplay;
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

            ammoDisplay?.Initialize();

            bulletDisplay?.Initialize();
        }

        public override void ResetAllMotions()
        {
            base.ResetAllMotions();

            bulletDisplay?.ResetAllMotions();
        }

        protected override void UpdateVisuals()
        {
            base.UpdateVisuals();

            ammoBox?.SetActive(isActive);
            bulletDisplay?.SetActive(isActive);
            //bulletDisplay?.총알 흩어질지, 모일지 연출
        }

        public void PlayReloadMotion(float _duration, UnityAction _onComplete = null)
        {
            if (null == bulletDisplay)
                return;

            bulletDisplay.PlayReloadMotion(_duration, _onComplete);
        }

        public void PlayResetMotion(UnityAction _onStart = null, UnityAction _onComplete = null)
        {
            if (null == bulletDisplay)
                return;

            bulletDisplay.PlayResetMotion(_onStart, _onComplete);
        }

        public void PlayGatherMotion(UnityAction _onStart = null, UnityAction _onComplete = null)
        {
            if (null == bulletDisplay)
                return;

            bulletDisplay.PlayGatherMotion(_onStart, _onComplete);
        }

        public void PlayGatherMotion(int _currentMag, int _maxMag, int _totalAmmo, UnityAction _onStart = null, UnityAction _onComplete = null)
        {
            UpdateAmmo(_currentMag, _maxMag, _totalAmmo);

            if (null == bulletDisplay)
                return;

            bulletDisplay.PlayGatherMotion(_onStart, _onComplete);
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
            ammoDisplay?.SetNumber(_totalAmmo);

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
