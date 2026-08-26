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
        bool _cancelPressed = (null != Keyboard.current && true == Keyboard.current.escapeKey.wasPressedThisFrame)
            || (null != Gamepad.current && (true == Gamepad.current.buttonEast.wasPressedThisFrame || true == Gamepad.current.startButton.wasPressedThisFrame));

        if (true == _cancelPressed)
        {
            CloseCredit();
            return;
        }

        // 종료 키를 제외한 모든 키보드/마우스/게임패드 입력 감지 시 배속 적용
        if (null != scrollTween && true == scrollTween.IsActive())
        {
            bool _speedUpHeld = IsKeyboardSpeedUpPressed(Keyboard.current)
                || IsMouseSpeedUpPressed(Mouse.current)
                || IsGamepadSpeedUpPressed(Gamepad.current);

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

    private bool IsKeyboardSpeedUpPressed(Keyboard _keyboard)
    {
        if (null == _keyboard) return false;
        return true == _keyboard.anyKey.isPressed && false == _keyboard.escapeKey.isPressed;
    }

    private bool IsMouseSpeedUpPressed(Mouse _mouse)
    {
        if (null == _mouse) return false;
        return true == _mouse.leftButton.isPressed
            || true == _mouse.rightButton.isPressed
            || true == _mouse.middleButton.isPressed;
    }

    private bool IsGamepadSpeedUpPressed(Gamepad _gamepad)
    {
        if (null == _gamepad) return false;

        return true == _gamepad.buttonSouth.isPressed
            || true == _gamepad.buttonWest.isPressed
            || true == _gamepad.buttonNorth.isPressed
            || true == _gamepad.leftShoulder.isPressed
            || true == _gamepad.rightShoulder.isPressed
            || true == _gamepad.leftTrigger.isPressed
            || true == _gamepad.rightTrigger.isPressed
            || true == _gamepad.leftStickButton.isPressed
            || true == _gamepad.rightStickButton.isPressed
            || true == _gamepad.selectButton.isPressed
            || true == _gamepad.dpad.up.isPressed
            || true == _gamepad.dpad.down.isPressed
            || true == _gamepad.dpad.left.isPressed
            || true == _gamepad.dpad.right.isPressed
            || 0.25f <= _gamepad.leftStick.ReadValue().sqrMagnitude
            || 0.25f <= _gamepad.rightStick.ReadValue().sqrMagnitude;
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
