using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 언어, 화면 모드 등 좌우 버튼으로 단순 선택지를 바꾸는 옵션 항목의 UI입니다.
/// </summary>
public class UI_OptionSelector : MonoBehaviour
{
    // 외부 컴포넌트 참조
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Button leftArrowButton;
    [SerializeField] private Button rightArrowButton;

    // 내부 상태
    private Action onLeftClicked;
    private Action onRightClicked;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(string _title, string _initialValue, Action _onLeft, Action _onRight)
    {
        onLeftClicked = _onLeft;
        onRightClicked = _onRight;

        if (null != titleText)
        {
            titleText.text = _title;
        }

        UpdateValue(_initialValue);
    }

    public void UpdateValue(string _value)
    {
        if (null != valueText)
        {
            valueText.text = _value;
        }
    }

    public void SetInteractable(bool _isInteractable)
    {
        if (null != leftArrowButton) leftArrowButton.interactable = _isInteractable;
        if (null != rightArrowButton) rightArrowButton.interactable = _isInteractable;
        
        // 시각적 피드백 처리 (알파값 조절 등)
        if (null != valueText)
        {
            Color _color = valueText.color;
            _color.a = true == _isInteractable ? 1f : 0.5f;
            valueText.color = _color;
        }
    }

    // 유니티 이벤트 함수
    private void Awake()
    {
        if (null != leftArrowButton)
        {
            leftArrowButton.onClick.AddListener(OnLeftButtonClicked);
        }
        if (null != rightArrowButton)
        {
            rightArrowButton.onClick.AddListener(OnRightButtonClicked);
        }
    }

    private void OnLeftButtonClicked()
    {
        if (null != onLeftClicked)
        {
            onLeftClicked.Invoke();
        }
    }

    private void OnRightButtonClicked()
    {
        if (null != onRightClicked)
        {
            onRightClicked.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (null != leftArrowButton)
        {
            leftArrowButton.onClick.RemoveListener(OnLeftButtonClicked);
        }
        if (null != rightArrowButton)
        {
            rightArrowButton.onClick.RemoveListener(OnRightButtonClicked);
        }
        onLeftClicked = null;
        onRightClicked = null;
    }
}
