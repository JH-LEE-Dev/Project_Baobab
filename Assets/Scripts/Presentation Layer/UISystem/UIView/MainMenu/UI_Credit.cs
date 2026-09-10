using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using DG.Tweening;

/// <summary>
/// 메인 메뉴 크레딧 연출을 담당하는 UI 스크립트입니다.
/// </summary>
public class UI_Credit : MonoBehaviour, IUIDepthCloseable
{
    [Header("Scroll Settings")]
    [SerializeField, Tooltip("크레딧 텍스트/이미지들이 들어있는 부모 렉트 (ContentSizeFitter 필수)")] 
    private RectTransform contentRoot; 
    [SerializeField] private float baseDuration = 20f;
    [SerializeField] private float speedMultiplier = 3f;
    [SerializeField] private Ease scrollEase = Ease.Linear;
    
    [Header("UI Elements")]
    [SerializeField] private Button closeButton;
    [SerializeField] private CanvasGroup canvasGroup;

    private Action onCloseAction;
    private InputManager inputManager;
    private UIDepthController depthController;
    private Tween scrollTween;
    private bool isPlaying = false;

    // IUIDepthCloseable 구현
    public bool IsActive => isPlaying && gameObject.activeInHierarchy;
    public void Hide() => CloseCredit();

    // 델리게이트 캐싱 (GC 차단)
    private TweenCallback onScrollCompleteCallback;
    private TweenCallback onCloseFadeCompleteCallback;
    private UnityEngine.Events.UnityAction onCloseButtonClickedAction;

    public void Initialize(Action _onClose, InputManager _inputManager = null, UIDepthController _depthController = null)
    {
        onCloseAction = _onClose;
        inputManager = _inputManager;
        depthController = _depthController;
        
        onScrollCompleteCallback = OnScrollComplete;
        onCloseFadeCompleteCallback = OnCloseFadeComplete;
        onCloseButtonClickedAction = OnCloseButtonClicked;

        if (null != closeButton)
        {
            closeButton.onClick.AddListener(onCloseButtonClickedAction);
        }
    }

    public void PlayCredit()
    {
        gameObject.SetActive(true);
        depthController?.RegisterView(this);

        if (null != canvasGroup)
        {
            canvasGroup.DOKill();
            canvasGroup.alpha = 0f;
            canvasGroup.DOFade(1f, 0.5f);
        }

        if (null == contentRoot) return;

        // ContentSizeFitter가 사이즈를 즉시 계산하도록 강제 업데이트
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);

        // 스크린 높이 계산 (부모 캔버스 기준)
        float _screenHeight = 1080f; 
        Canvas _canvas = GetComponentInParent<Canvas>();
        if (null != _canvas && null != _canvas.GetComponent<RectTransform>())
        {
            _screenHeight = _canvas.GetComponent<RectTransform>().rect.height;
        }

        float _contentHeight = contentRoot.rect.height;

        // 시작 위치: 화면 아래 바깥 (앵커가 정중앙(0.5, 0.5)이라고 가정)
        // 만약 앵커가 상단이면 위치 계산이 다를 수 있지만, 일반적으로 0.5, 0.5를 씁니다.
        float _startY = -(_screenHeight / 2f) - (_contentHeight / 2f);
        // 종료 위치: 화면 위 바깥
        float _endY = (_screenHeight / 2f) + (_contentHeight / 2f);

        contentRoot.anchoredPosition = new Vector2(contentRoot.anchoredPosition.x, _startY);

        if (null != scrollTween)
        {
            scrollTween.Kill();
        }

        isPlaying = true;

        scrollTween = contentRoot.DOAnchorPosY(_endY, baseDuration)
            .SetEase(scrollEase)
            .OnComplete(onScrollCompleteCallback);
    }

    private void Update()
    {
        if (false == isPlaying) return;

        // 취소 키(ESC 또는 패드 B/Start)로 닫기
        bool _cancelPressed = (null != inputManager && true == inputManager.WasGamepadUICancelPressedThisFrame)
            || (null != Keyboard.current && true == Keyboard.current.escapeKey.wasPressedThisFrame);

        if (true == _cancelPressed)
        {
            CloseCredit();
            return;
        }

        // 취소 키를 제외한 모든 "눌림" 감지 시 스크롤 배속 적용.
        // 마우스 이동과 휠 스크롤은 배속 조건이 아니다. AnyInputThisFrame은 장치 전환 판정용이라
        // 일정 거리 이상의 마우스 이동까지 조작으로 치므로, 그걸 쓰면 마우스를 스치기만 해도
        // 크레딧이 제멋대로 빨라진다. 그래서 눌림만 보는 AnyButtonHeldThisFrame을 쓴다.
        if (null != scrollTween && true == scrollTween.IsActive())
        {
            bool _speedUpHeld = (null != inputManager && true == inputManager.AnyButtonHeldThisFrame && false == inputManager.WasGamepadUICancelPressedThisFrame)
                || (null != Keyboard.current && true == Keyboard.current.anyKey.isPressed && false == Keyboard.current.escapeKey.isPressed)
                || (null != Mouse.current && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed));

            if (true == _speedUpHeld)
            {
                scrollTween.timeScale = speedMultiplier;
            }
            else
            {
                scrollTween.timeScale = 1f;
            }
        }
    }

    private void OnScrollComplete()
    {
        CloseCredit();
    }

    private void OnCloseButtonClicked()
    {
        CloseCredit();
    }

    private void CloseCredit()
    {
        if (false == isPlaying) return;
        isPlaying = false;
        depthController?.UnregisterView(this);

        if (null != scrollTween)
        {
            scrollTween.Kill();
            scrollTween = null;
        }

        if (null != canvasGroup)
        {
            canvasGroup.DOFade(0f, 0.3f).OnComplete(onCloseFadeCompleteCallback);
        }
        else
        {
            OnCloseFadeComplete();
        }
    }

    private void OnCloseFadeComplete()
    {
        gameObject.SetActive(false);
        if (null != onCloseAction)
        {
            onCloseAction();
        }
    }

    private void OnDestroy()
    {
        if (null != closeButton)
        {
            closeButton.onClick.RemoveListener(onCloseButtonClickedAction);
        }
        
        if (null != scrollTween)
        {
            scrollTween.Kill();
        }
        
        if (null != canvasGroup)
        {
            canvasGroup.DOKill();
        }
        
        onCloseAction = null;
    }
}
