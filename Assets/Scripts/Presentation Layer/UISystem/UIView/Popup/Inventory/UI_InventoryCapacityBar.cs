using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 인벤토리 최대 용량 대비 현재 용량을 표시하는 게이지 바입니다.
/// 용량의 차오름 정도에 따라 Fill 색상이 녹색 -> 주황 -> 빨강으로 변합니다.
/// 고스트 바 기능과 커스텀 애니메이션을 지원합니다.
/// </summary>
public class UI_InventoryCapacityBar : HUD_ProgressBar
{
    // //외부 의존성
    [Header("Ghost Bar Settings")]
    [SerializeField] private Slider ghostSlider;
    [SerializeField] private float ghostDelay = 0.5f;
    [SerializeField] private float ghostCatchupDuration = 0.3f;

    [Header("Capacity Colors")]
    [SerializeField] private Color lowCapacityColor = Color.green;
    [SerializeField] private Color mediumCapacityColor = new Color(1f, 0.5f, 0f); // Orange
    [SerializeField] private Color highCapacityColor = Color.red;

    [SerializeField] private Image fillImage;

    [Header("Squash & Stretch Settings")]
    [SerializeField] private float stretchX = 1.25f;
    [SerializeField] private float stretchY = 0.8f;
    [SerializeField] private float squashX = 0.85f;
    [SerializeField] private float squashY = 1.15f;
    [SerializeField] private float stepDuration = 0.08f;
    [SerializeField] private float settleOvershoot = 2.5f;

    [Header("Remove Item Animation Settings")]
    [SerializeField] private float removeSquashScale = 0.9f;

    // //내부 의존성
    private Sequence feedbackSequence;
    private Tween catchupTween;

    // //퍼블릭 초기화 및 제어 메서드
    public override void Initialize()
    {
        base.Initialize();

        if (null == fillImage && null != progressSlider)
        {
            RectTransform _fillRect = progressSlider.fillRect;
            if (null != _fillRect)
            {
                fillImage = _fillRect.GetComponent<Image>();
            }
        }

        if (null != ghostSlider)
        {
            ghostSlider.minValue = 0.0f;
            ghostSlider.maxValue = 1.0f;
            ghostSlider.value = 0.0f;
        }
    }

    /// <summary>
    /// 현재 용량과 최대 용량을 받아 게이지 바와 색상을 갱신합니다.
    /// 용량이 증가할 때는 고스트 바가 먼저 오르고 딜레이 후 메인 게이지가 따라옵니다.
    /// </summary>
    public void UpdateCapacity(int _current, int _max)
    {
        if (0 >= _max)
            return;

        float _ratio = (float)_current / _max;
        float _prevRatio = (null != ghostSlider) ? ghostSlider.value : currentValue;

        if (null != ghostSlider)
        {
            if (_ratio > _prevRatio)
            {
                // 증가 시: 고스트 바 즉시 반영, 메인 바는 딜레이 후 따라감 (아이템 계속 먹으면 딜레이 갱신)
                ghostSlider.value = _ratio;

                if (null != catchupTween && catchupTween.IsActive())
                    catchupTween.Kill();

                catchupTween = progressSlider.DOValue(_ratio, ghostCatchupDuration)
                    .SetDelay(ghostDelay)
                    .SetEase(Ease.OutQuad)
                    .OnUpdate(() => { UpdateColor(progressSlider.value); });
            }
            else
            {
                // 감소/초기화 시: 메인 바 즉시 반영, 고스트 바는 딜레이 후 따라감
                UpdateValue(_ratio);
                UpdateColor(_ratio);

                if (null != catchupTween && catchupTween.IsActive())
                    catchupTween.Kill();

                catchupTween = ghostSlider.DOValue(_ratio, ghostCatchupDuration)
                    .SetDelay(ghostDelay)
                    .SetEase(Ease.OutQuad);
            }
        }
        else
        {
            UpdateValue(_ratio);
            UpdateColor(_ratio);
        }
    }

    /// <summary>
    /// 아이템을 획득했을 때 호출되어 양옆으로 비틀어서 쫙쫙 늘어나는 스쿼시 앤 스트레치 모션을 재생합니다.
    /// </summary>
    public void PlayFeedbackAnimation()
    {
        if (null != feedbackSequence && feedbackSequence.IsActive())
            feedbackSequence.Kill(true);

        transform.localScale = Vector3.one;
        
        feedbackSequence = DOTween.Sequence();
        // 1. 가로로 늘어나면서 세로로 수축 (Stretch)
        feedbackSequence.Append(transform.DOScale(new Vector3(stretchX, stretchY, 1f), stepDuration).SetEase(Ease.OutQuad));
        // 2. 가로로 수축되면서 세로로 늘어남 (Squash)
        feedbackSequence.Append(transform.DOScale(new Vector3(squashX, squashY, 1f), stepDuration).SetEase(Ease.InOutQuad));
        // 3. 원래대로 찰지게 돌아오기
        feedbackSequence.Append(transform.DOScale(Vector3.one, stepDuration * 2f).SetEase(Ease.OutBack, settleOvershoot));
    }

    /// <summary>
    /// 아이템이 슬롯에서 빠져나갈 때 호출되어 약간 수축했다가 통통 튀며 돌아오는 모션을 재생합니다.
    /// </summary>
    public void PlayRemoveFeedbackAnimation()
    {
        if (null != feedbackSequence && feedbackSequence.IsActive())
            feedbackSequence.Kill(true);

        transform.localScale = Vector3.one;

        feedbackSequence = DOTween.Sequence();
        // 1. 살짝 작아지면서 눌리는 느낌 (Squash)
        feedbackSequence.Append(transform.DOScale(new Vector3(removeSquashScale, removeSquashScale, 1f), stepDuration).SetEase(Ease.OutQuad));
        // 2. 원래대로 찰지게 돌아오기
        feedbackSequence.Append(transform.DOScale(Vector3.one, stepDuration * 2f).SetEase(Ease.OutBack, settleOvershoot));
    }

    // //내부 로직
    private void UpdateColor(float _ratio)
    {
        if (null == fillImage)
            return;

        if (0.5f > _ratio)
        {
            // 0.0 ~ 0.5 구간: Green -> Orange
            float _t = _ratio / 0.5f;
            fillImage.color = Color.Lerp(lowCapacityColor, mediumCapacityColor, _t);
        }
        else
        {
            // 0.5 ~ 1.0 구간: Orange -> Red
            float _t = (_ratio - 0.5f) / 0.5f;
            fillImage.color = Color.Lerp(mediumCapacityColor, highCapacityColor, _t);
        }
    }
}
