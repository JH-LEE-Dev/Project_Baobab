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
using System.Collections.Generic;
using System;
using UnityEngine.Events;

namespace PresentationLayer.UISystem.UIView.HUD.Equipment
{
    /// <summary>
    /// 라이플의 탄창 시각화를 담당하며, 첫 번째 총알이 가장 앞에(Layer Top) 오도록 정렬합니다.
    /// </summary>
    public class HUD_EquipmentBullet : MonoBehaviour
    {
        // //외부 의존성
        [Header("Pool Settings")]
        [SerializeField] private GameObject bulletPrefab;      // HUD_BulletIcon 컴포넌트가 포함된 프리발
        [SerializeField] private Transform bulletContainer;    // 아이콘 부모 컨테이너
        [SerializeField] private int defaultPoolSize = 30;     // 초기 생성할 총알 개수

        // //내부 의존성
        private List<HUD_BulletIcon> bulletIcons;
        private int lastKnownMagCount = -1;
        private int lastKnownMaxMag = -1;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 초기 설정 및 오브젝트 풀을 미리 생성합니다.
        /// </summary>
        public void Initialize()
        {
            if (null == bulletContainer)
                bulletContainer = this.transform;

            if (null == bulletIcons)
            {
                bulletIcons = new List<HUD_BulletIcon>(defaultPoolSize);
                PrewarmPool(defaultPoolSize);
            }
        }

        public void SetActive(bool _isActive)
        {
            int _limit = (-1 == lastKnownMaxMag) ? bulletIcons.Count : lastKnownMaxMag;
            for (int i = 0; i < _limit; i++)
            {
                HUD_BulletIcon _icon = bulletIcons[i];
                if (null == _icon)
                    continue;

                _icon.SetActive(_isActive);
            }
        }

        /// <summary>
        /// 모든 총알 아이콘의 모션을 정지하고 초기 상태로 원복합니다.
        /// </summary>
        public void ResetAllMotions()
        {
            if (null == bulletIcons)
                return;

            for (int i = 0; i < bulletIcons.Count; i++)
            {
                if (null != bulletIcons[i])
                    bulletIcons[i].ResetAllMotions();
            }
        }

        /// <summary>
        /// 탄창 정보를 업데이트합니다. 인덱스 0번이 레이어상 가장 위에 위치합니다.
        /// </summary>
        public void UpdateBulletStatus(int _currentMag, int _maxMag, int _totalAmmo)
        {
            if (_maxMag > bulletIcons.Count)
                PrewarmPool(_maxMag);

            bool _isFiring = (-1 != lastKnownMagCount) && (_currentMag < lastKnownMagCount);

            for (int i = 0; i < bulletIcons.Count; i++)
            {
                HUD_BulletIcon _icon = bulletIcons[i];
                if (null == _icon)
                    continue;

                if (i >= _maxMag)
                {
                    if (true == _icon.gameObject.activeSelf)
                        _icon.gameObject.SetActive(false);
                    
                    continue;
                }

                if (false == _icon.gameObject.activeSelf)
                    _icon.gameObject.SetActive(true);
                
                bool _isFilled = i < _currentMag;
                bool _shouldAnimate = _isFiring && (i == _currentMag);
                
                _icon.SetState(_isFilled, _shouldAnimate);
            }

            lastKnownMagCount = _currentMag;
            lastKnownMaxMag = _maxMag;
        }

        public void PlayReloadMotion(float _totalDuration, UnityAction _onComplete)
        {
            if (null == bulletIcons || 0 == bulletIcons.Count)
                return;

            if (0 >= lastKnownMaxMag)
                return;

            float _interval = _totalDuration / lastKnownMaxMag;

            for (int i = 0; i < lastKnownMaxMag; i++)
            {
                HUD_BulletIcon _icon = bulletIcons[i];
                if (null == _icon)
                    continue;

                _icon.PlayReloadMotion(i * _interval, _onComplete);
            }
        }

        public void PlayResetMotion(UnityAction _onStart = null, UnityAction _onComplete = null)
        {
            if (null == bulletIcons || 0 == bulletIcons.Count)
                return;

            int _limit = (-1 == lastKnownMaxMag) ? bulletIcons.Count : lastKnownMaxMag;
            if (0 >= _limit)
                return;

            for (int i = 0; i < _limit; i++)
            {
                HUD_BulletIcon _icon = bulletIcons[i];
                if (null == _icon)
                    continue;

                // 현재 장전된 탄환 수에 따라 활성화 여부 결정
                bool _isFilled = i < lastKnownMagCount;

                // 첫 번째 아이콘 시작 시와 마지막 아이콘 종료 시 콜백 실행 (전체 연출 흐름 제어)
                UnityAction _start = (0 == i) ? _onStart : null;
                UnityAction _complete = (i == _limit - 1) ? _onComplete : null;

                _icon.PlayResetMotion(i * 0.025f, _isFilled, _start, _complete); 
            }
        }

        /// <summary>
        /// 모든 총알이 첫 번째 총알 위치로 촤라락 뭉치는 연출을 재생합니다.
        /// </summary>
        public void PlayGatherMotion(UnityAction _onStart, UnityAction _onComplete)
        {
            if (null == bulletIcons || 0 == bulletIcons.Count)
                return;

            if (0 >= lastKnownMaxMag)
                return;

            for (int i = 0; i < lastKnownMaxMag; i++)
            {
                HUD_BulletIcon _icon = bulletIcons[i];
                if (null == _icon)
                    continue;

                _icon.PlayGatherMotion(i * 0.025f, bulletContainer.position, _onStart, _onComplete);
            }
        }
        /// <summary>
        /// 오브젝트 풀을 생성하고 레이어 순서를 조정합니다.
        /// </summary>
        private void PrewarmPool(int _size)
        {
            if (null == bulletPrefab)
                return;

            while (bulletIcons.Count < _size)
            {
                GameObject _go = Instantiate(bulletPrefab, bulletContainer);
                HUD_BulletIcon _icon = _go.GetComponent<HUD_BulletIcon>();
                
                if (null != _icon)
                {
                    _go.transform.SetAsFirstSibling();
                    
                    _icon.Initialize(true);
                    _icon.gameObject.SetActive(false);
                    bulletIcons.Add(_icon);
                }
            }
        }
    }
}
