using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace PresentationLayer.UISystem.CustomNumber
{
    /// <summary>
    /// 0~9까지의 숫자가 그려진 스프라이트를 사용하여 숫자를 화면에 표시하는 컴포넌트입니다.
    /// 문자열 할당을 피하기 위해 정수 연산을 사용하며 오브젝트 풀링을 적용합니다.
    /// </summary>
    public class CustomNumberDisplay : MonoBehaviour
    {
        // //외부 의존성
        [Header("Resources")]
        [SerializeField] private Sprite[] numberSprites;    // 0~9까지 순서대로 배치된 스프라이트 배열
        [SerializeField] private Transform digitContainer;  // 숫자 이미지들이 자식으로 배치된 부모 컨테이너

        [Header("Settings")]
        [SerializeField] private bool hideLeadingZeros = true; // 앞자리의 0을 숨길지 여부

        // //내부 의존성
        private List<Image> digitPool;
        private int lastDisplayedValue = -1;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 초기 설정 및 컨테이너의 자식 오브젝트들로부터 이미지 컴포넌트를 수집합니다.
        /// </summary>
        public void Initialize()
        {
            if (null == digitContainer)
                digitContainer = this.transform;

            int _childCount = digitContainer.childCount;
            digitPool = new List<Image>(_childCount);

            for (int i = 0; i < _childCount; i++)
            {
                Transform _child = digitContainer.GetChild(i);
                Image _img = _child.GetComponent<Image>();
                
                if (null != _img)
                {
                    _img.gameObject.SetActive(false);
                    digitPool.Add(_img);
                }
            }
        }

        /// <summary>
        /// 표시할 숫자를 설정하고 UI를 갱신합니다.
        /// </summary>
        /// <param name="_value">표시할 정수값</param>
        public void SetNumber(int _value)
        {
            if (lastDisplayedValue == _value)
                return;

            lastDisplayedValue = _value;
            UpdateDisplay(_value);
        }

        /// <summary>
        /// 실제 UI 요소를 갱신하는 로직입니다.
        /// </summary>
        private void UpdateDisplay(int _value)
        {
            if (null == numberSprites || 10 != numberSprites.Length)
                return;

            int _temp = _value;
            int _digitCount = 0;

            // 0 처리
            if (0 == _value)
            {
                DisplaySingleDigit(0, 0);
                HideRemainingDigits(1);
                return;
            }

            // 정수 연산을 통해 낮은 자릿수부터 추출 (GC Alloc 방지)
            while (0 < _temp)
            {
                int _digit = _temp % 10;
                DisplaySingleDigit(_digitCount, _digit);
                
                _temp /= 10;
                _digitCount++;
            }

            // 남은 자릿수 비활성화
            HideRemainingDigits(_digitCount);
            
            // 자릿수 정렬 (낮은 자릿수가 오른쪽으로 가도록 계층 구조 순서 변경)
            SortDigitOrder(_digitCount);
        }

        private void DisplaySingleDigit(int _index, int _number)
        {
            if (_index >= digitPool.Count)
                return;

            Image _img = digitPool[_index];
            if (null == _img)
                return;

            _img.sprite = numberSprites[_number];
            
            if (false == _img.gameObject.activeSelf)
                _img.gameObject.SetActive(true);
        }

        private void HideRemainingDigits(int _activeCount)
        {
            if (null == digitPool)
                return;

            for (int i = _activeCount; i < digitPool.Count; i++)
            {
                if (true == digitPool[i].gameObject.activeSelf)
                    digitPool[i].gameObject.SetActive(false);
            }
        }

        private void SortDigitOrder(int _activeCount)
        {
            // 자릿수 이미지가 생성된 순서대로(0번이 일의 자리) 계층 구조에서 뒤로 보냄
            // LayoutGroup 사용 시 낮은 자릿수가 오른쪽/아래에 오게 됨
            for (int i = 0; i < _activeCount; i++)
            {
                if (null != digitPool[i])
                    digitPool[i].transform.SetAsFirstSibling();
            }
        }

        // //유니티 이벤트 함수
        // 최하단 배치
    }
}
