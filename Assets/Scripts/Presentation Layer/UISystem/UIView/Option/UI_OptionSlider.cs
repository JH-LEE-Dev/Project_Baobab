using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 볼륨, 밝기 등 연속적인 범위의 값을 좌우 버튼 및 슬라이더 드래그로 조절하는 옵션 항목의 UI입니다.
/// </summary>
public class UI_OptionSlider : MonoBehaviour
{
    // 외부 컴포넌트 참조
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private Slider slider;
    [SerializeField] private UI_OptionButton leftArrowButton;
    [SerializeField] private UI_OptionButton rightArrowButton;

    [Header("Settings")]
    [SerializeField] private float stepValue = 5f; // 좌우 버튼 클릭 시 변하는 양
    [SerializeField] private string valueFormat = "{0}%"; // 값 표기 형식

    // 내부 상태
    private Action<float> onValueChanged;
    private Action onLeftClicked;
    private Action onRightClicked;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(string _title, float _initialValue, float _minValue, float _maxValue, Action<float> _onValueChanged)
    {
        onValueChanged = _onValueChanged;
        
        if (null == onLeftClicked) onLeftClicked = OnLeftButtonClicked;
        if (null == onRightClicked) onRightClicked = OnRightButtonClicked;

        if (null != leftArrowButton) leftArrowButton.Initialize(onLeftClicked);
        if (null != rightArrowButton) rightArrowButton.Initialize(onRightClicked);

        if (null != titleText)
        {
            titleText.text = _title;
        }

        if (null != slider)
        {
            slider.minValue = _minValue;
            slider.maxValue = _maxValue;
            // 슬라이더 값을 설정하면 자동으로 OnSliderValueChanged 이벤트가 발생하므로 UI도 함께 업데이트됨
            slider.value = _initialValue;
        }

        UpdateValueDisplay(_initialValue);
    }

    public void UpdateValue(float _value)
    {
        if (null != slider)
        {
            // 이벤트를 발생시키며 텍스트도 자동 갱신됨
            slider.value = _value;
        }
        else
        {
            UpdateValueDisplay(_value);
        }
    }

    private void UpdateValueDisplay(float _value)
    {
        if (null != valueText)
        {
            valueText.text = string.Format(valueFormat, Mathf.RoundToInt(_value));
        }
    }

    public void SetInteractable(bool _isInteractable)
    {
        if (null != slider) slider.interactable = _isInteractable;
        if (null != leftArrowButton) leftArrowButton.SetInteractable(_isInteractable);
        if (null != rightArrowButton) rightArrowButton.SetInteractable(_isInteractable);

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
        if (null != slider)
        {
            slider.onValueChanged.AddListener(OnSliderValueChanged);
        }
    }

    private void OnSliderValueChanged(float _newValue)
    {
        UpdateValueDisplay(_newValue);

        if (null != onValueChanged)
        {
            onValueChanged.Invoke(_newValue);
        }
    }

    private void OnLeftButtonClicked()
    {
        if (null != slider)
        {
            float _newValue = Mathf.Clamp(slider.value - stepValue, slider.minValue, slider.maxValue);
            slider.value = _newValue;
        }
    }

    private void OnRightButtonClicked()
    {
        if (null != slider)
        {
            float _newValue = Mathf.Clamp(slider.value + stepValue, slider.minValue, slider.maxValue);
            slider.value = _newValue;
        }
    }

    private void OnDestroy()
    {
        if (null != slider)
        {
            slider.onValueChanged.RemoveListener(OnSliderValueChanged);
        }
        onValueChanged = null;
        onLeftClicked = null;
        onRightClicked = null;
    }
}
