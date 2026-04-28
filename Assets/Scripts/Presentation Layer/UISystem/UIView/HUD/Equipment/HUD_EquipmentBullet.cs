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
        [SerializeField] private GameObject bulletPrefab;      // HUD_BulletIcon 컴포넌트가 포함된 프리팹
        [SerializeField] private Transform bulletContainer;    // 아이콘 부모 컨테이너
        [SerializeField] private int defaultPoolSize = 30;     // 초기 생성할 총알 개수

        // //내부 의존성
        private List<HUD_BulletIcon> bulletIcons;
        private int lastKnownMagCount = -1;

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
            for (int i = 0; i < bulletIcons.Count; i++)
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

            bool _shouldAnimate = (-1 != lastKnownMagCount) && (_currentMag < lastKnownMagCount);

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
                _icon.SetState(_isFilled, _shouldAnimate);
            }

            lastKnownMagCount = _currentMag;
        }

        public void PlayReloadMotion(float _totalDuration, UnityAction _callEvent)
        {
            if (null == bulletIcons || 0 == bulletIcons.Count)
                return;

            // 재장전 연출을 위한 총알 간 간격 계산
            // maxCapacity 만큼의 총알만 연출함
            int _targetCount = 0;
            for (int i = 0; i < bulletIcons.Count; i++)
            {
                if (null != bulletIcons[i] && true == bulletIcons[i].gameObject.activeSelf)
                    _targetCount++;
            }

            if (0 == _targetCount)
                return;

            float _bulletDuration = _totalDuration * 0.5f;
            float _interval = (_totalDuration - _bulletDuration) / _targetCount;

            int _activeIndex = 0;
            for (int i = 0; i < bulletIcons.Count; i++)
            {
                HUD_BulletIcon _icon = bulletIcons[i];
                if (null == _icon || false == _icon.gameObject.activeSelf)
                    continue;

                _icon.PlayReloadMotion(_bulletDuration, _activeIndex * _interval, _callEvent);
                _activeIndex++;
            }
        }

        public void PlayResetMotion(float _duration)
        {
            if (null == bulletIcons || 0 == bulletIcons.Count)
                return;

            for (int i = 0; i < bulletIcons.Count; i++)
            {
                HUD_BulletIcon _icon = bulletIcons[i];
                if (null == _icon || false == _icon.gameObject.activeSelf)
                    continue;

                // 재장전 완료는 딜레이 없이 한 번에 복구
                _icon.PlayReloadMotion(_duration, 0f); // "Reload" 태그를 재사용하거나 별도 구현 가능
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
                    // 신규 생성된 오브젝트를 계층 구조의 가장 첫 번째로 보냄으로써
                    // 먼저 생성된 오브젝트(bulletIcons[0])가 항상 마지막 자식이 되어 레이어상 가장 위에 오게 함
                    _go.transform.SetAsFirstSibling();
                    
                    _icon.Initialize(true);
                    _icon.gameObject.SetActive(false);
                    bulletIcons.Add(_icon);
                }
            }
        }

        // //유니티 이벤트 함수
    }
}
