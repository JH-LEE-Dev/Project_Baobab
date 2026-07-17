using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using TMPro;

/// <summary>
/// 메인 메뉴 진입 전 "Press Any Key to Start" 화면을 담당하는 스크립트입니다.
/// </summary>
public class UI_PressAnyKey : MonoBehaviour
{
    [Header("UI Component")]
    [SerializeField, Tooltip("깜빡일 텍스트 컴포넌트")] 
    private TextMeshProUGUI pressAnyKeyText;
    
    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private float minAlpha = 0.2f;

    private UIView_MainMenu parentView;
    private bool isWaitingForInput = false;

    public void Initialize(UIView_MainMenu _parentView)
    {
        parentView = _parentView;
        
        if (null != pressAnyKeyText)
        {
            // 텍스트 깜빡임(Pulse) 애니메이션 무한 반복
            pressAnyKeyText.DOFade(minAlpha, fadeDuration)
                           .SetLoops(-1, LoopType.Yoyo)
                           .SetEase(Ease.InOutSine);
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);
        isWaitingForInput = true;
    }

    public void Hide()
    {
        isWaitingForInput = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 외부(로컬라이징 매니저 등)에서 문구를 변경할 때 호출합니다.
    /// </summary>
    public void SetText(string _localizedText)
    {
        if (null != pressAnyKeyText)
        {
            pressAnyKeyText.text = _localizedText;
        }
    }

    private void Update()
    {
        if (false == isWaitingForInput) return;

        bool _anyInputReceived = false;

        // 키보드 아무 키 입력 감지 (New Input System)
        if (null != Keyboard.current && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            _anyInputReceived = true;
        }
        // 마우스 클릭 감지
        else if (null != Mouse.current && (Mouse.current.leftButton.wasPressedThisFrame || Mouse.current.rightButton.wasPressedThisFrame))
        {
            _anyInputReceived = true;
        }
        // 게임패드 아무 버튼 감지
        else if (null != Gamepad.current)
        {
            var _controls = Gamepad.current.allControls;
            for (int i = 0; i < _controls.Count; i++)
            {
                if (_controls[i] is UnityEngine.InputSystem.Controls.ButtonControl _button && _button.wasPressedThisFrame)
                {
                    _anyInputReceived = true;
                    break;
                }
            }
        }

        // 입력이 감지되면 메인 메뉴로 전환
        if (_anyInputReceived)
        {
            isWaitingForInput = false;
            if (null != parentView)
            {
                parentView.OnPressAnyKeyCompleted();
            }
        }
    }

    private void OnDestroy()
    {
        // 텍스트 애니메이션 메모리 누수 방지
        if (null != pressAnyKeyText)
        {
            pressAnyKeyText.DOKill();
        }
    }
}
