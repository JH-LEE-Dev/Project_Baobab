using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.UIView.HUD.DirectionalIndicator
{
    /// <summary>
    /// 화면 밖에 있는 특정 3D 타겟의 방향을 추적하여 UI 경계선에 정밀하게 표시해주는 인디케이터 클래스입니다.
    /// </summary>
    public class HUD_DirIndicator : MonoBehaviour
    {
        // //외부 의존성
        [Header("UI Components")]
        [SerializeField] private Image indicatorImage;         // 화살표 이미지를 렌더링할 Image 컴포넌트
        [SerializeField] private RectTransform rectTransform;  // 인디케이터 자신의 RectTransform

        [Header("Directional Sprites")]
        [SerializeField] private Sprite upArrowSprite;
        [SerializeField] private Sprite downArrowSprite;
        [SerializeField] private Sprite leftArrowSprite;
        [SerializeField] private Sprite rightArrowSprite;

        [Header("Settings")]
        [SerializeField] private float padding = 50f;          // 화면 꼭지점 잘림 방지용 여백 (패딩)
        [SerializeField] private float idleSpeed = 5.0f;       // Idle 왕복 운동 주기 속도

        // //내부 의존성
        private Transform targetTransform;
        private Camera mainCamera;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 추적할 3D 타겟 Transform을 바인딩하고 시스템을 초기화합니다.
        /// </summary>
        public void Initialize(Transform _target = null)
        {
            targetTransform = _target;
            mainCamera = Camera.main;

            if (null == rectTransform)
                rectTransform = GetComponent<RectTransform>();

            isInitialized = true;

            // 부모 게임 오브젝트는 항상 활성화하여 Update()를 돌립니다.
            gameObject.SetActive(true);

            // 초기 상태에서는 인디케이터 이미지를 숨깁니다.
            if (null != indicatorImage)
                indicatorImage.gameObject.SetActive(false);
        }

        /// <summary>
        /// 동적으로 추적 대상을 교체하거나 해제하고 싶을 때 사용합니다.
        /// </summary>
        public void SetTarget(Transform _newTarget)
        {
            targetTransform = _newTarget;

            if (null == _newTarget)
                if (null != indicatorImage)
                    indicatorImage.gameObject.SetActive(false);
        }

        // //유니티 이벤트 함수

        private void Awake()
        {
            if (null == rectTransform)
                rectTransform = GetComponent<RectTransform>();
        }

        private void Update()
        {
            if (false == isInitialized)
                return;

            if (null == targetTransform)
            {
                if (null != indicatorImage)
                    indicatorImage.gameObject.SetActive(false);
                return;
            }

            if (null == mainCamera)
                mainCamera = Camera.main;

            if (null == mainCamera)
                return;

            // 1. 타겟의 스크린 공간 좌표 계산
            Vector3 _screenPos = mainCamera.WorldToScreenPoint(targetTransform.position);
            bool _isOffscreen = false;

            // 카메라의 등 뒤에 있는 경우 보정 처리
            if (0f >= _screenPos.z)
            {
                _isOffscreen = true;

                // 정후방 방향에 맞춰 투영하여 화면 가장자리에 자연스럽게 가리키도록 월드 벡터 보정 투영
                Vector3 _diff = targetTransform.position - mainCamera.transform.position;
                Vector3 _projected = mainCamera.transform.position - _diff.normalized * 10f;
                _screenPos = mainCamera.WorldToScreenPoint(_projected);
            }

            // 화면 가로/세로 범위를 벗어났는지 판별
            if (0f > _screenPos.x || _screenPos.x > Screen.width || 0f > _screenPos.y || _screenPos.y > Screen.height)
                _isOffscreen = true;

            // 2. 화면 밖이 아닐 때 처리
            if (false == _isOffscreen)
            {
                if (null != indicatorImage)
                    indicatorImage.gameObject.SetActive(false);
                return;
            }

            // 화면 밖이면 인디케이터 이미지를 보이게 함
            if (null != indicatorImage)
                indicatorImage.gameObject.SetActive(true);

            // 3. 인디케이터 가야 할 경계선 좌표 연산
            Vector2 _screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 _dir = (Vector2)_screenPos - _screenCenter;

            float _finalX = 0f;
            float _finalY = 0f;
            int _snapIdleOffset = Mathf.RoundToInt(Mathf.Sin(Time.time * idleSpeed));

            // 화면 종횡비(가로/세로 절반 크기) 반영
            float _halfW = Screen.width * 0.5f - padding;
            float _halfH = Screen.height * 0.5f - padding;

            // 실제 화면 경계에 먼저 충돌하는 면(가까운 면) 판정 비율 계산
            float _ratioX = Mathf.Abs(_dir.x) / _halfW;
            float _ratioY = Mathf.Abs(_dir.y) / _halfH;

            if (_ratioY >= _ratioX)
            {
                // 세로 경계에 먼저 도달하거나 더 가까운 경우 ➡️ 위쪽 혹은 아래쪽 면에 고정
                if (0f < _dir.y)
                {
                    // 위(Top) 면 고정
                    _finalY = Screen.height - padding - _snapIdleOffset; // 모서리쪽 왕복 운동 적용
                    _finalX = Mathf.Clamp(_screenCenter.x + _dir.x, padding, Screen.width - padding);
                    
                    if (null != indicatorImage && indicatorImage.sprite != upArrowSprite)
                        indicatorImage.sprite = upArrowSprite;
                }
                else
                {
                    // 아래(Bottom) 면 고정
                    _finalY = padding + _snapIdleOffset; // 모서리쪽 왕복 운동 적용
                    _finalX = Mathf.Clamp(_screenCenter.x + _dir.x, padding, Screen.width - padding);

                    if (null != indicatorImage && indicatorImage.sprite != downArrowSprite)
                        indicatorImage.sprite = downArrowSprite;
                }
            }
            else
            {
                // X축(가로축) 이동량이 더 큰 경우 ➡️ 오른쪽 혹은 왼쪽 면에 고정
                if (0f < _dir.x)
                {
                    // 오른쪽(Right) 면 고정
                    _finalX = Screen.width - padding - _snapIdleOffset; // 모서리쪽 왕복 운동 적용
                    _finalY = Mathf.Clamp(_screenCenter.y + _dir.y, padding, Screen.height - padding);

                    if (null != indicatorImage && indicatorImage.sprite != rightArrowSprite)
                        indicatorImage.sprite = rightArrowSprite;
                }
                else
                {
                    // 왼쪽(Left) 면 고정
                    _finalX = padding + _snapIdleOffset; // 모서리쪽 왕복 운동 적용
                    _finalY = Mathf.Clamp(_screenCenter.y + _dir.y, padding, Screen.height - padding);

                    if (null != indicatorImage && indicatorImage.sprite != leftArrowSprite)
                        indicatorImage.sprite = leftArrowSprite;
                }
            }

            // 4. 픽셀 격자 스냅 및 좌표 대입
            if (null != rectTransform)
                rectTransform.position = new Vector3(Mathf.Round(_finalX), Mathf.Round(_finalY), 0f);
        }
    }
}
