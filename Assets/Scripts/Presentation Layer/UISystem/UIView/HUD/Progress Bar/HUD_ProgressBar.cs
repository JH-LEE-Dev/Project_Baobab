using UnityEngine;
using UnityEngine.UI;

public class HUD_ProgressBar : MonoBehaviour
{
    private Slider progressSlider;

    private float currentValue = 0.0f;
    private float maxValue = 100.0f;

    public void Initialize()
    { 
        progressSlider = GetComponentInChildren<Slider>();

        if (null == progressSlider)
            return;

        progressSlider.minValue = 0.0f;
        progressSlider.maxValue = maxValue;
        progressSlider.value = currentValue;
    }

    public void OnDestroy()
    {
        
    }

    public void SetMaxValue(float _maxValue)
    {
        if (0.0f >= _maxValue)
            return;

        maxValue = _maxValue;

        if (null == progressSlider)
            return;

        progressSlider.maxValue = maxValue;
    }

    public void UpdateValue(float _newValue)
    {
        currentValue = _newValue;

        if (null == progressSlider)
            return;

        // TODO :: DOTween
        progressSlider.value = currentValue;
    }

    public void OnShow()
    {
        gameObject.SetActive(true);
    }

    public void OnHide()
    {
        gameObject.SetActive(false);
    }
}
