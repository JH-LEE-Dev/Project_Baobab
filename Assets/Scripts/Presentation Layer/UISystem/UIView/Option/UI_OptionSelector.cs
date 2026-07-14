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
    [SerializeField] private UI_OptionButton leftArrowButton;
    [SerializeField] private UI_OptionButton rightArrowButton;

    // 내부 상태
    private Action onLeftClicked;
    private Action onRightClicked;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(string _title, string _initialValue, Action _onLeft, Action _onRight)
    {
        onLeftClicked = _onLeft;
        onRightClicked = _onRight;

        if (null != leftArrowButton) leftArrowButton.Initialize(onLeftClicked);
        if (null != rightArrowButton) rightArrowButton.Initialize(onRightClicked);

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
        // UI_OptionButton 내부에 구현된 SetInteractable 호출로 시각적 피드백과 로직 동시 처리
        if (null != leftArrowButton) leftArrowButton.SetInteractable(_isInteractable);
        if (null != rightArrowButton) rightArrowButton.SetInteractable(_isInteractable);
        
        // 시각적 피드백 처리 (알파값 조절 등)
        if (null != valueText)
        {
            Color _color = valueText.color;
            _color.a = true == _isInteractable ? 1f : 0.5f;
            valueText.color = _color;
        }
    }

    private void OnDestroy()
    {
        onLeftClicked = null;
        onRightClicked = null;
    }
}
