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
    // feedbackSequence가 획득 연출(PlayFeedbackAnimation)인지. 제거 연출과 같은 필드를 쓰므로 재사용 판정에 필요하다.
    private bool bFeedbackIsAdd = false;
    private Tween catchupTween;
    // Slider.DOValue 대신 쓰는 슬라이더별 델리게이트 캐시(SliderValueTweener 참조)
    private SliderValueTweener progressValueTweener;
    private SliderValueTweener ghostValueTweener;
    private TweenCallback cachedOnCapacityUpdate;

    // //퍼블릭 초기화 및 제어 메서드
    public override void Initialize()
    {
        base.Initialize();

        cachedOnCapacityUpdate = OnCapacityTweenUpdate;

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
            if (_prevRatio < _ratio)
            {
                // 증가 시: 고스트 바 즉시 반영, 메인 바는 딜레이 후 따라감 (아이템 계속 먹으면 딜레이 갱신)
                ghostSlider.value = _ratio;

                if (null != catchupTween && true == catchupTween.IsActive())
                    catchupTween.Kill();

                catchupTween = SliderValueTweener.DOValue(ref progressValueTweener, progressSlider, _ratio, ghostCatchupDuration)
                    .SetDelay(ghostDelay)
                    .SetEase(Ease.OutQuad)
                    .SetLink(gameObject)
                    .OnUpdate(cachedOnCapacityUpdate);
            }
            else
            {
                // 감소/초기화 시: 메인 바 즉시 반영, 고스트 바는 딜레이 후 따라감
                UpdateValue(_ratio);
                UpdateColor(_ratio);

                if (null != catchupTween && true == catchupTween.IsActive())
                    catchupTween.Kill();

                catchupTween = SliderValueTweener.DOValue(ref ghostValueTweener, ghostSlider, _ratio, ghostCatchupDuration)
                    .SetDelay(ghostDelay)
                    .SetEase(Ease.OutQuad)
                    .SetLink(gameObject);
            }
        }
        else
        {
            UpdateValue(_ratio);
            UpdateColor(_ratio);
        }
    }

    /// <summary>
    /// UpdateCapacity(_current, _max)를 같은 값으로 두 번 연달아 부른 것과 정확히 같은 결과를 만든다.
    ///
    /// 인벤토리 UI 갱신(UI_Inventory.InventoryShowEvent)은 슬롯 갱신 안에서 한 번, 끝에서 한 번, 같은 값으로 용량바를
    /// 두 번 갱신한다. 그때 첫 호출이 만든 따라잡기 트윈은 한 번도 진행되기 전에 두 번째 호출이 끊는다. 아이템을 하나
    /// 먹을 때마다 버려질 트윈을 하나씩 만드는 셈이라, 여기서는 그 트윈만 만들지 않고 나머지 처리는 두 호출의 순서
    /// 그대로 수행한다(진행 전에 끊긴 트윈은 아무 콜백도 실행하지 않으므로, 만들지 않은 것과 같다).
    /// </summary>
    public void UpdateCapacityTwice(int _current, int _max)
    {
        if (0 >= _max)
            return;

        float _ratio = (float)_current / _max;

        if (null == ghostSlider)
        {
            // 두 호출 모두 같은 값을 그대로 쓰는 경로라 한 번과 같다.
            UpdateValue(_ratio);
            UpdateColor(_ratio);
            return;
        }

        // 첫 번째 호출: 새로 만들 트윈은 두 번째 호출이 곧바로 끊으므로 만들지 않는다.
        if (ghostSlider.value < _ratio)
        {
            ghostSlider.value = _ratio;
        }
        else
        {
            UpdateValue(_ratio);
            UpdateColor(_ratio);
        }

        if (null != catchupTween && true == catchupTween.IsActive())
            catchupTween.Kill();

        // 두 번째 호출: UpdateCapacity와 같은 처리(끊을 트윈은 첫 번째 호출이 만들지 않았으므로 없다).
        if (ghostSlider.value < _ratio)
        {
            ghostSlider.value = _ratio;

            catchupTween = SliderValueTweener.DOValue(ref progressValueTweener, progressSlider, _ratio, ghostCatchupDuration)
                .SetDelay(ghostDelay)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject)
                .OnUpdate(cachedOnCapacityUpdate);
        }
        else
        {
            UpdateValue(_ratio);
            UpdateColor(_ratio);

            catchupTween = SliderValueTweener.DOValue(ref ghostValueTweener, ghostSlider, _ratio, ghostCatchupDuration)
                .SetDelay(ghostDelay)
                .SetEase(Ease.OutQuad)
                .SetLink(gameObject);
        }
    }

    /// <summary>
    /// 아이템을 획득했을 때 호출되어 양옆으로 비틀어서 쫙쫙 늘어나는 스쿼시 앤 스트레치 모션을 재생합니다.
    /// </summary>
    public void PlayFeedbackAnimation()
    {
        // 흡입 반경이 넓으면 한 프레임에 원목 수십 개가 도착해 이 함수가 연달아 불린다. 직전에 만든 획득 연출이
        // 아직 한 번도 진행되지 않았다면(같은 프레임) 그것을 끊고 똑같은 연출을 새로 만드는 것과, 그대로 두는 것은
        // 결과가 같다 - 끊으면 끝값(원래 크기)으로 맞춰진 뒤 원래 크기에서 다시 시작하고, 그대로 두어도 원래 크기에서
        // 시작한다. 그래서 새로 만들지 않고 재사용해 트윈 할당을 아낀다.
        if (true == bFeedbackIsAdd && null != feedbackSequence && true == feedbackSequence.IsActive()
            && true == feedbackSequence.IsPlaying() && 0f >= feedbackSequence.Elapsed(true))
        {
            transform.localScale = Vector3.one;
            return;
        }

        if (null != feedbackSequence && true == feedbackSequence.IsActive())
            feedbackSequence.Kill(true);

        transform.localScale = Vector3.one;

        bFeedbackIsAdd = true;
        feedbackSequence = DOTween.Sequence().SetLink(gameObject);
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
        if (null != feedbackSequence && true == feedbackSequence.IsActive())
            feedbackSequence.Kill(true);

        transform.localScale = Vector3.one;

        bFeedbackIsAdd = false;
        feedbackSequence = DOTween.Sequence().SetLink(gameObject);
        // 1. 살짝 작아지면서 눌리는 느낌 (Squash)
        feedbackSequence.Append(transform.DOScale(new Vector3(removeSquashScale, removeSquashScale, 1f), stepDuration).SetEase(Ease.OutQuad));
        // 2. 원래대로 찰지게 돌아오기
        feedbackSequence.Append(transform.DOScale(Vector3.one, stepDuration * 2f).SetEase(Ease.OutBack, settleOvershoot));
    }

    private void OnCapacityTweenUpdate()
    {
        if (null != progressSlider)
            UpdateColor(progressSlider.value);
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

    private void OnDisable()
    {
        if (null != catchupTween && true == catchupTween.IsActive())
        {
            catchupTween.Kill();
            catchupTween = null;
        }

        if (null != feedbackSequence && true == feedbackSequence.IsActive())
        {
            feedbackSequence.Kill();
            feedbackSequence = null;
        }

        transform.localScale = Vector3.one;
    }
}
