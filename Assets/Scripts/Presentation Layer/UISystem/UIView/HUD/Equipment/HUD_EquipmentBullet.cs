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
