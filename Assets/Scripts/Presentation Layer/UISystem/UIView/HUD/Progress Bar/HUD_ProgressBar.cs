using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD에서 진행도를 표시하는 프로그레스 바의 최상위 클래스입니다.
/// </summary>
public class HUD_ProgressBar : MonoBehaviour
{
    // //외부 의존성
    [SerializeField] protected Slider progressSlider;

    // //내부 의존성
    protected float currentValue = 0.0f;

    // //퍼블릭 초기화 및 제어 메서드

    public virtual void Initialize()
    {
        if (null == progressSlider)
            progressSlider = GetComponentInChildren<Slider>();

        if (null == progressSlider)
            return;

        progressSlider.minValue = 0.0f;
        progressSlider.maxValue = 1.0f;
        progressSlider.value = 1.0f;
    }

    public void UpdateValue(float _ratio)
    {
        currentValue = _ratio;

        if (null != progressSlider)
            progressSlider.value = currentValue;
    }

    public void SetActivate(bool _is)
    {
        gameObject.SetActive(_is);
    }
}

