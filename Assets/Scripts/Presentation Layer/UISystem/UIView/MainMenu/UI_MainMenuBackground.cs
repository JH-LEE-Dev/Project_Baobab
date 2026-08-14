using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// 메인 메뉴의 배경 화면(Parallax 및 부유 효과)을 담당하는 스크립트입니다.
/// </summary>
public class UI_MainMenuBackground : MonoBehaviour
{
    [Serializable]
    public struct BackgroundLayer
    {
        [Tooltip("움직임을 적용할 대상 (예: 구름, 산, 배경 이미지)")]
        public RectTransform layerTransform;
        
        [Header("Parallax Settings")]
        [Tooltip("마우스 움직임에 따른 반응 강도 (값이 클수록 더 많이 움직임)")]
        public Vector2 parallaxMultiplier;
        
        [Header("Floating Settings")]
        [Tooltip("둥둥 떠다니는 애니메이션 활성화 여부")]
        public bool enableFloating;
        [Tooltip("자식 오브젝트들이 각각 따로 부유하게 할 것인지 여부 (구름 레이어 등에 유용)")]
        public bool independentChildrenFloating;
        [Tooltip("부유 속도")]
        public float floatingSpeed;
        [Tooltip("부유 진폭 (위아래, 좌우로 얼마나 움직일지)")]
        public Vector2 floatingAmplitude;

        // 내부 캐싱 데이터
        [HideInInspector] public Vector2 initialPosition;
        [HideInInspector] public float randomOffset; // 각 구름이 동일하게 움직이지 않도록 하는 랜덤 오프셋
        [HideInInspector] public FloatingChild[] childrenData; // 자식들 개별 이동용 캐시
    }

    public struct FloatingChild
    {
        public RectTransform transform;
        public Vector2 initialPosition;
        public float randomOffset;
    }

    [Header("Settings")]
    [SerializeField, Tooltip("배경 레이어 배열 (0번이 가장 뒤, 마지막이 가장 앞)")] 
    private BackgroundLayer[] backgroundLayers;
    
    [SerializeField, Tooltip("패럴랙스 보간 속도 (부드러운 카메라 무빙 느낌)")] 
    private float parallaxLerpSpeed = 5f;

    // 내부 상태 및 의존성
    private Vector2 currentMousePos;
    private Vector2 targetParallaxOffset;
    private Vector2 currentParallaxOffset;
    private Vector2 screenSize;
    
    private bool isInitialized = false;

    public void Initialize()
    {
        if (true == isInitialized) return;

        screenSize = new Vector2(Screen.width, Screen.height);

        // 각 레이어의 초기 위치를 캐싱하고 부유 효과를 위한 랜덤 오프셋 부여
        for (int i = 0; i < backgroundLayers.Length; i++)
        {
            if (null != backgroundLayers[i].layerTransform)
            {
                backgroundLayers[i].initialPosition = backgroundLayers[i].layerTransform.anchoredPosition;
                backgroundLayers[i].randomOffset = UnityEngine.Random.Range(0f, 100f);

                if (backgroundLayers[i].enableFloating && backgroundLayers[i].independentChildrenFloating)
                {
                    int _childCount = backgroundLayers[i].layerTransform.childCount;
                    backgroundLayers[i].childrenData = new FloatingChild[_childCount];
                    for (int j = 0; j < _childCount; j++)
                    {
                        RectTransform _childRt = backgroundLayers[i].layerTransform.GetChild(j) as RectTransform;
                        if (null != _childRt)
                        {
                            backgroundLayers[i].childrenData[j] = new FloatingChild
                            {
                                transform = _childRt,
                                initialPosition = _childRt.anchoredPosition,
                                randomOffset = UnityEngine.Random.Range(0f, 100f)
                            };
                        }
                    }
                }
            }
        }

        isInitialized = true;
    }

    private void Update()
    {
        if (false == isInitialized) return;

        UpdateMousePosition();
        CalculateParallaxOffset();
        ApplyLayerMovements();
    }

    private void UpdateMousePosition()
    {
        // 화면 크기 변경 대비 (에디터 환경 등)
        screenSize.x = Screen.width;
        screenSize.y = Screen.height;

        // New Input System을 활용한 마우스 위치 획득
        if (null != Mouse.current)
        {
            currentMousePos = Mouse.current.position.ReadValue();
        }
        else
        {
            currentMousePos = screenSize * 0.5f; // 마우스가 없으면 중앙으로 취급
        }
    }

    private void CalculateParallaxOffset()
    {
        // 화면 중앙을 0,0으로 맞추고 해상도에 구애받지 않도록 정규화 (-1.0 ~ 1.0)
        float _normalizedX = (currentMousePos.x / screenSize.x) * 2f - 1f;
        float _normalizedY = (currentMousePos.y / screenSize.y) * 2f - 1f;

        // 마우스가 화면 밖으로 나갔을 때를 대비해 Clamp
        _normalizedX = Mathf.Clamp(_normalizedX, -1f, 1f);
        _normalizedY = Mathf.Clamp(_normalizedY, -1f, 1f);

        // 부호 반전: 마우스가 우측으로 가면 카메라도 우측으로 도는 느낌이므로, 배경은 좌측으로 이동해야 함
        targetParallaxOffset = new Vector2(-_normalizedX, -_normalizedY);

        // 부드러운 움직임을 위한 Lerp 처리
        currentParallaxOffset = Vector2.Lerp(currentParallaxOffset, targetParallaxOffset, Time.deltaTime * parallaxLerpSpeed);
    }

    private void ApplyLayerMovements()
    {
        float _currentTime = Time.time;

        for (int i = 0; i < backgroundLayers.Length; i++)
        {
            if (null == backgroundLayers[i].layerTransform)
            {
                continue;
            }

            Vector2 _layerParallax = new Vector2(
                currentParallaxOffset.x * backgroundLayers[i].parallaxMultiplier.x,
                currentParallaxOffset.y * backgroundLayers[i].parallaxMultiplier.y
            );

            if (backgroundLayers[i].enableFloating)
            {
                ApplyFloatingMovement(backgroundLayers[i], _layerParallax, _currentTime);
            }
            else
            {
                backgroundLayers[i].layerTransform.anchoredPosition = backgroundLayers[i].initialPosition + _layerParallax;
            }
        }
    }

    private void ApplyFloatingMovement(in BackgroundLayer _layer, Vector2 _layerParallax, float _currentTime)
    {
        if (_layer.independentChildrenFloating && null != _layer.childrenData)
        {
            _layer.layerTransform.anchoredPosition = _layer.initialPosition + _layerParallax;
            
            for (int j = 0; j < _layer.childrenData.Length; j++)
            {
                var _child = _layer.childrenData[j];
                if (null == _child.transform)
                {
                    continue;
                }

                float _timeWithOffset = _currentTime * _layer.floatingSpeed + _child.randomOffset;
                Vector2 _childFloating = new Vector2(
                    Mathf.Sin(_timeWithOffset) * _layer.floatingAmplitude.x,
                    Mathf.Cos(_timeWithOffset * 0.8f) * _layer.floatingAmplitude.y
                );

                _child.transform.anchoredPosition = _child.initialPosition + _childFloating;
            }
        }
        else
        {
            float _timeWithOffset = _currentTime * _layer.floatingSpeed + _layer.randomOffset;
            Vector2 _layerFloating = new Vector2(
                Mathf.Sin(_timeWithOffset) * _layer.floatingAmplitude.x,
                Mathf.Cos(_timeWithOffset * 0.8f) * _layer.floatingAmplitude.y
            );
            _layer.layerTransform.anchoredPosition = _layer.initialPosition + _layerParallax + _layerFloating;
        }
    }
}
