using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 여러 맵 중 현재 선택된 맵의 위치와 전체 맵의 개수를 도트로 표시해주는 인디케이터 클래스입니다.
    /// </summary>
    public class HUD_MapIndicator : MonoBehaviour
    {
        // //외부 의존성
        [SerializeField] private GameObject indicatorDotPrefab;
        [SerializeField] private Transform dotContainer;
        [SerializeField] private Sprite activeSprite;
        [SerializeField] private Sprite inactiveSprite;

        // //내부 의존성
        private readonly List<Image> spawnedDots = new List<Image>(8);

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 전체 맵의 개수만큼 인디케이터 도트를 구성하고 비활성 스프라이트로 초기화합니다.
        /// </summary>
        public void Initialize(int _totalCount)
        {
            if (null == indicatorDotPrefab)
                return;

            if (null == dotContainer)
                return;

            int _currentCount = spawnedDots.Count;
            if (_currentCount < _totalCount)
            {
                int _needed = _totalCount - _currentCount;
                for (int _i = 0; _i < _needed; _i++)
                {
                    GameObject _go = Instantiate(indicatorDotPrefab, dotContainer);
                    if (null != _go)
                    {
                        Image _img = _go.GetComponent<Image>();
                        if (null != _img)
                            spawnedDots.Add(_img);
                    }
                }
            }

            for (int _i = 0; _i < spawnedDots.Count; _i++)
            {
                if (null != spawnedDots[_i])
                {
                    if (_i < _totalCount)
                    {
                        spawnedDots[_i].gameObject.SetActive(true);
                        spawnedDots[_i].sprite = inactiveSprite;
                    }
                    else
                    {
                        spawnedDots[_i].gameObject.SetActive(false);
                    }
                }
            }
        }

        /// <summary>
        /// 선택된 인덱스에 부합하는 도트만 활성 스프라이트로 변경하고 나머지는 비활성 스프라이트로 변경합니다.
        /// </summary>
        public void Refresh(int _selectedIndex)
        {
            for (int _i = 0; _i < spawnedDots.Count; _i++)
            {
                if (null != spawnedDots[_i])
                {
                    if (_i == _selectedIndex)
                        spawnedDots[_i].sprite = activeSprite;
                    else
                        spawnedDots[_i].sprite = inactiveSprite;
                }
            }
        }
    }
}
