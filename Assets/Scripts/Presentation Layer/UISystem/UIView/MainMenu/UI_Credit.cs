using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System;
using DG.Tweening;

/// <summary>
/// 메인 메뉴 크레딧 연출을 담당하는 UI 스크립트입니다.
/// </summary>
public class UI_Credit : MonoBehaviour
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
    private Tween scrollTween;
    private bool isPlaying = false;

    // 델리게이트 캐싱 (GC 차단)
    private TweenCallback onScrollCompleteCallback;
    private TweenCallback onCloseFadeCompleteCallback;
    private UnityEngine.Events.UnityAction onCloseButtonClickedAction;

    public void Initialize(Action _onClose)
    {
        onCloseAction = _onClose;
        
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

        // ESC 키 또는 게임패드 B(buttonEast)/Start 버튼으로 강제 닫기
        bool _cancelPressed = (null != Keyboard.current && Keyboard.current.escapeKey.wasPressedThisFrame)
            || (null != Gamepad.current && (Gamepad.current.buttonEast.wasPressedThisFrame || Gamepad.current.startButton.wasPressedThisFrame));

        if (true == _cancelPressed)
        {
            CloseCredit();
            return;
        }

        // 좌클릭 또는 게임패드 A(buttonSouth) 홀드 시 스피드업
        if (null != scrollTween && scrollTween.IsActive())
        {
            bool _speedUpHeld = (null != Mouse.current && Mouse.current.leftButton.isPressed)
                || (null != Gamepad.current && Gamepad.current.buttonSouth.isPressed);

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
