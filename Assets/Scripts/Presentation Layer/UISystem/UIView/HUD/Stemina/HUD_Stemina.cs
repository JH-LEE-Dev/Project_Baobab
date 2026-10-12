using Coffee.UIEffects;
using DG.Tweening;
using DG.Tweening.Core;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.UI;

public class HUD_Stemina : MonoBehaviour
{
    // 외부 의존성
    [Header("UI Ref")]
    [SerializeField] private HUD_ProgressBar progressBar;
    [SerializeField] private ObjectMotionPlayer motionPlayer;

    [Header("Motions")]
    [SerializeField] private string shakeTag = "DangerShake";
    [SerializeField] private string colorTag = "DangerColor";
    [Range(0f, 100f)][SerializeField] private float startPoint = 100f;

    [Tooltip("경고가 켜진 뒤 피로도가 startPoint + 이 값(%p) 위로 회복되면 경고를 끈다. 경계에서 켜졌다 꺼졌다 하지 않도록 두는 여유폭")]
    [Range(0f, 100f)][SerializeField] private float releaseMargin = 5f;

    [Header("Hit / Recover Effect Ref")]
    [Tooltip("피격 잔상. Fill 아래에 깔려, 깎인 구간이 잠깐 남았다가 따라온다(HUD_HPBar의 고스트 바와 같은 규칙).")]
    [SerializeField] private Slider ghostSlider;
    [Tooltip("회복 구간 표시. 앵커로 [회복 전, 회복 후] 구간을 지정한다. 보통 Fill 위에 둔다.")]
    [SerializeField] private RectTransform gainSegment;
    [SerializeField] private Image gainSegmentImage;
    [Tooltip("게이지 전체를 덮는 플래시. 레이캐스트는 꺼 둔다. 바 모양의 스프라이트를 쓰고, 색은 flashEffect의 색 치환(Replace)이 입힌다.")]
    [SerializeField] private Image flashOverlay;
    [Tooltip("flashOverlay의 UIEffect. 스프라이트의 원래 색이 아니라 지정한 단색(흰색/붉은색)으로 실루엣만 칠한다.")]
    [SerializeField] private UIEffect flashEffect;
    [Tooltip("피격 흔들림을 줄 대상. 비워 두면 이 오브젝트 자신. 저피로도 경고(DangerShake)는 SteminaBar를 흔들므로 그 위 단계를 지정해 합산되게 한다.")]
    [SerializeField] private RectTransform shakeTarget;

    [Header("Hit / Recover Effect")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private Color recoverFlashColor = Color.white;
    [Tooltip("변화량이 fullEffectRatio 이상일 때의 플래시 알파")]
    [Range(0f, 1f)][SerializeField] private float flashMaxAlpha = 0.75f;
    [Tooltip("변화량이 minEffectRatio 근처일 때의 플래시 알파")]
    [Range(0f, 1f)][SerializeField] private float flashMinAlpha = 0.25f;
    [SerializeField] private float damageFlashDuration = 0.12f;
    [SerializeField] private int damageFlashCount = 2;
    [SerializeField] private float recoverFlashDuration = 0.25f;
    [Tooltip("피격 후 잔상이 남아 있다가 따라오기 시작하기까지의 시간(초)")]
    [SerializeField] private float ghostDelay = 0.35f;
    [Tooltip("잔상이 현재 값을 따라잡는 데 걸리는 대략적인 시간(초). 현재 값을 매 프레임 따라가므로 지속 소모 중에도 끊기지 않는다.")]
    [SerializeField] private float ghostFollowDuration = 0.3f;
    [Tooltip("회복 구간이 흰색으로 남아 있는 시간(초)")]
    [SerializeField] private float gainHoldDuration = 0.15f;
    [SerializeField] private float gainFadeDuration = 0.3f;
    [Tooltip("피격 흔들림의 최대 세기(캔버스 픽셀)")]
    [SerializeField] private float shakeStrength = 2f;
    [SerializeField] private float shakeDuration = 0.2f;
    [Tooltip("이 비율(최대치 대비)보다 작은 변화는 연출하지 않는다. 휴식 구역 틱처럼 잦고 작은 회복이 계속 번쩍이는 것을 막는다.")]
    [Range(0f, 1f)][SerializeField] private float minEffectRatio = 0.01f;
    [Tooltip("이 비율 이상의 변화는 연출 세기가 최대가 된다.")]
    [Range(0.01f, 1f)][SerializeField] private float fullEffectRatio = 0.2f;

    // 내부 의존성
    private const int ShakeStepCount = 8;
    // 잔상과 현재 값의 차이가 이보다 작으면(160px 바 기준 반 픽셀 미만) 눈에 보이지 않으므로 바로 맞춘다.
    private const float GhostSnapGap = 0.003f;
    // 지수 감쇠의 세기. 클수록 ghostFollowDuration 안에 더 확실히 따라잡는다.
    private const float GhostFollowSharpness = 5f;
    // 격차가 작아져도 끝이 늘어지지 않도록 하는 최소 속도(비율/초)
    private const float GhostMinFollowSpeed = 0.15f;

    private HUD_ScreenBlood screenBloodComponent;
    // 던전에서만 true. 마을에서는 게이지가 숨겨지고 저피로도 경고(게이지 흔들림/붉은 화면)도 판정하지 않는다.
    private bool bActive = false;
    private bool bWarningGauge = false;
    private MotionEntry shakeMotion;
    private MotionEntry colorMotion;

    // 피격/회복 연출 상태
    private float displayRatio = 1f;
    private float pendingLossRatio = 0f;
    private float pendingGainRatio = 0f;
    private bool bGhostTrailing = false;
    private float ghostHoldRemain = 0f;

    private float flashValue = 0f;
    private Color flashColor = Color.white;
    private float flashPeakAlpha = 0f;
    private float gainValue = 0f;
    private float shakeValue = 0f;
    private float currentShakeStrength = 0f;
    private Vector2 appliedShakeOffset = Vector2.zero;

    private Tween flashTween;
    private Tween gainTween;
    private Tween shakeTween;

    // 트윈마다 델리게이트를 새로 만들지 않도록 한 번만 캐싱한다.
    private DOGetter<float> getFlashValue;
    private DOSetter<float> setFlashValue;
    private DOGetter<float> getGainValue;
    private DOSetter<float> setGainValue;
    private DOGetter<float> getShakeValue;
    private DOSetter<float> setShakeValue;


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(HUD_ScreenBlood _screenBlood)
    {
        progressBar?.Initialize();
        motionPlayer?.Initialize();

        screenBloodComponent = _screenBlood;

        getFlashValue = GetFlashValue;
        setFlashValue = SetFlashValue;
        getGainValue = GetGainValue;
        setGainValue = SetGainValue;
        getShakeValue = GetShakeValue;
        setShakeValue = SetShakeValue;

        if (null == shakeTarget)
            shakeTarget = transform as RectTransform;

        if (null != ghostSlider)
        {
            ghostSlider.minValue = 0.0f;
            ghostSlider.maxValue = 1.0f;
            ghostSlider.value = 1.0f;
        }

        ResetEffects();
    }

    public void UpdateValue(float _ratio)
    {
        // 경고 상태는 현재 비율을 그대로 따라간다(켜짐: startPoint 이하, 꺼짐: startPoint + releaseMargin 초과).
        // 예전엔 한 번 켜지면 던전 진입 때만 풀리는 래치였는데, 그러면 마을에서 켜져 있어도 끌 방법이 없었고
        // 휴식/포션으로 피로도를 회복해도 붉은 화면이 계속 깜빡였다.
        if (true == bActive)
        {
            float _warnRatio = startPoint * 0.01f;

            if (false == bWarningGauge && _warnRatio >= _ratio)
                SetWarning(true);
            else if (true == bWarningGauge && _warnRatio + releaseMargin * 0.01f < _ratio)
                SetWarning(false);
        }

        displayRatio = _ratio;
        progressBar?.UpdateValue(_ratio);

        UpdateGhost(_ratio);
        ApplyPendingGain(_ratio);
    }

    /// <summary>
    /// 스태미나가 한 번에 줄어든 순간의 연출(붉은 플래시, 잔상, 흔들림). _lossRatio는 최대치 대비 줄어든 비율이다.
    /// 지속 소모는 호출하지 않는다.
    /// </summary>
    public void PlayDamaged(float _lossRatio)
    {
        if (false == bActive || minEffectRatio > _lossRatio)
            return;

        float _intensity = GetIntensity(_lossRatio);

        // 잔상의 시작점은 줄어든 값이 HUD 비율에 반영된 뒤(UpdateValue)에 정한다. 이벤트와 비율 갱신의 순서가 프레임마다 달라질 수 있다.
        pendingLossRatio += _lossRatio;

        StartFlash(damageFlashColor, _intensity, damageFlashDuration, Mathf.Max(1, damageFlashCount));
        StartShake(_intensity);
    }

    /// <summary>
    /// 스태미나가 한 번에 회복된 순간의 연출(흰색 플래시, 회복 구간 표시). _gainRatio는 최대치 대비 늘어난 비율이다.
    /// 자연 회복처럼 매 프레임 쌓이는 회복은 호출하지 않는다.
    /// </summary>
    public void PlayRecovered(float _gainRatio)
    {
        if (false == bActive || minEffectRatio > _gainRatio)
            return;

        float _intensity = GetIntensity(_gainRatio);

        pendingGainRatio += _gainRatio;

        StartFlash(recoverFlashColor, _intensity, recoverFlashDuration, 1);
    }

    /// <summary>
    /// 게이지 표시 여부. 던전이면 true, 마을이면 false(UIView_HUD.ChangedActiveStateStemina 참고).
    /// 어느 쪽으로 바뀌든 저피로도 경고는 일단 끈다. 예전엔 던전 진입(true)에서만 끄고 마을 진입(false)에서는
    /// 아무것도 하지 않아, 던전에서 켜진 붉은 화면 깜빡임이 마을까지 이어지면 다음 던전에 들어갈 때까지 남았다.
    /// </summary>
    public void SetActivate(bool _bActive)
    {
        bActive = _bActive;
        progressBar?.SetActivate(_bActive);

        // 숨겨지는 동안 남아 있던 연출이 다음에 나타날 때 이어 보이지 않도록 정리한다.
        ResetEffects();
        SetWarning(false);
    }

    private void SetWarning(bool _bOn)
    {
        bWarningGauge = _bOn;

        if (true == _bOn)
        {
            if (null != motionPlayer)
            {
                shakeMotion = motionPlayer.Play(shakeTag, bReset: true);
                colorMotion = motionPlayer.Play(colorTag, bReset: true);
            }
        }
        else
        {
            if (null != motionPlayer)
            {
                motionPlayer.SettingEntryMotion(shakeMotion, true, true);
                motionPlayer.SettingEntryMotion(colorMotion, true, true);
            }
        }

        // 켜기/끄기 모두 HUD_ScreenBlood 쪽이 중복 호출을 걸러주므로 상태 전이 여부와 무관하게 넘긴다.
        if (null != screenBloodComponent)
            screenBloodComponent.PlayBloodEffect(_bOn);
    }

    // 피격/회복 연출

    private float GetIntensity(float _ratio)
    {
        return Mathf.Clamp01(_ratio / Mathf.Max(0.0001f, fullEffectRatio));
    }

    // 고스트 바: 피격 직후에는 줄어들기 전 값을 잠깐 붙들고(ghostDelay), 이후 "살아 있는 현재 값"을 매 프레임 따라간다.
    // 따라가는 목표를 피격 순간의 값으로 고정하면, 지속 소모가 계속되는 동안 잔상이 옛 값에서 멈춘 뒤 한 번에 맞춰져
    // 붉은 조각이 툭 사라진다. 회복되면 예전처럼 즉시 맞춘다.
    private void UpdateGhost(float _ratio)
    {
        if (null == ghostSlider)
        {
            pendingLossRatio = 0f;
            return;
        }

        if (0f < pendingLossRatio)
        {
            // 줄어들기 전 값(= 지금 값 + 줄어든 양)에서 잔상을 시작한다. 이미 따라오는 중이면 더 높은 쪽을 유지해 누적한다.
            float _start = Mathf.Min(1f, _ratio + pendingLossRatio);
            ghostSlider.value = Mathf.Max(ghostSlider.value, _start);

            pendingLossRatio = 0f;
            ghostHoldRemain = ghostDelay;
            bGhostTrailing = true;
        }

        if (false == bGhostTrailing)
        {
            ghostSlider.value = _ratio;
            return;
        }

        // 현재 값이 잔상 이상이면(회복, 또는 따라잡음) 잔상을 맞추고 끝낸다.
        float _gap = ghostSlider.value - _ratio;
        if (GhostSnapGap >= _gap)
        {
            ghostSlider.value = _ratio;
            bGhostTrailing = false;
            return;
        }

        float _dt = Time.unscaledDeltaTime;

        if (0f < ghostHoldRemain)
        {
            ghostHoldRemain -= _dt;
            return;
        }

        // 지수 감쇠: 격차가 클 때 빠르고 끝에서 느려지는 OutQuad와 비슷한 움직임. 끝이 너무 길게 늘어지지 않도록 최소 속도를 둔다.
        float _follow = 1f - Mathf.Exp(-_dt * GhostFollowSharpness / Mathf.Max(0.01f, ghostFollowDuration));
        float _step = Mathf.Max(_gap * _follow, GhostMinFollowSpeed * _dt);
        ghostSlider.value = Mathf.Max(_ratio, ghostSlider.value - _step);
    }

    // 회복 구간: [회복 전, 회복 후]를 앵커로 지정한 흰색 구간을 잠깐 보여 주고 사라지게 한다.
    private void ApplyPendingGain(float _ratio)
    {
        if (0f >= pendingGainRatio)
            return;

        float _gain = pendingGainRatio;
        pendingGainRatio = 0f;

        if (null == gainSegment || null == gainSegmentImage)
            return;

        float _high = Mathf.Clamp01(_ratio);
        float _low = Mathf.Clamp01(_ratio - _gain);

        gainSegment.anchorMin = new Vector2(_low, 0f);
        gainSegment.anchorMax = new Vector2(_high, 1f);
        gainSegment.offsetMin = Vector2.zero;
        gainSegment.offsetMax = Vector2.zero;

        KillTween(ref gainTween);
        SetGainValue(1f);

        gainTween = DOTween.To(getGainValue, setGainValue, 0f, gainFadeDuration)
            .SetDelay(gainHoldDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true);
    }

    private void StartFlash(Color _color, float _intensity, float _duration, int _count)
    {
        if (null == flashOverlay)
            return;

        KillTween(ref flashTween);

        flashColor = _color;
        flashPeakAlpha = Mathf.Lerp(flashMinAlpha, flashMaxAlpha, _intensity);

        if (null != flashEffect)
        {
            flashEffect.colorFilter = ColorFilter.Replace;
            flashEffect.colorIntensity = 1f;
            flashEffect.color = new Color(_color.r, _color.g, _color.b, 1f);
        }

        SetFlashValue(1f);

        // 같은 감소를 Restart 루프로 반복해 "번쩍, 번쩍"처럼 보이게 한다. 값이 매번 1로 되돌아가므로 경계가 또렷하다.
        flashTween = DOTween.To(getFlashValue, setFlashValue, 0f, _duration)
            .SetEase(Ease.OutQuad)
            .SetLoops(_count, LoopType.Restart)
            .SetUpdate(true);
    }

    private void StartShake(float _intensity)
    {
        if (null == shakeTarget)
            return;

        KillTween(ref shakeTween);

        currentShakeStrength = shakeStrength * Mathf.Lerp(0.5f, 1f, _intensity);
        SetShakeValue(1f);

        shakeTween = DOTween.To(getShakeValue, setShakeValue, 0f, shakeDuration)
            .SetEase(Ease.Linear)
            .SetUpdate(true);
    }

    private float GetFlashValue()
    {
        return flashValue;
    }

    private void SetFlashValue(float _value)
    {
        flashValue = _value;

        if (null == flashOverlay)
            return;

        // 색 치환(UIEffect)을 쓰면 이미지 색은 알파만 담당하고, 쓰지 않으면 이미지 색 자체를 칠한다.
        Color _color = null != flashEffect ? Color.white : flashColor;
        _color.a = flashPeakAlpha * _value;
        flashOverlay.color = _color;
    }

    private float GetGainValue()
    {
        return gainValue;
    }

    private void SetGainValue(float _value)
    {
        gainValue = _value;

        if (null == gainSegmentImage)
            return;

        Color _color = gainSegmentImage.color;
        _color.a = _value;
        gainSegmentImage.color = _color;
    }

    private float GetShakeValue()
    {
        return shakeValue;
    }

    // 값이 줄어드는 동안 좌우를 번갈아 흔든다. 부드러운 보간 대신 정수 픽셀 단위의 계단식으로 움직여 픽셀 느낌을 지킨다.
    // 다른 움직임(HUD 이동 등)과 겹쳐도 틀어지지 않도록 절대 위치가 아니라 "이번에 더한 오프셋"만 갈아 끼운다.
    private void SetShakeValue(float _value)
    {
        shakeValue = _value;

        if (null == shakeTarget)
            return;

        Vector2 _offset = Vector2.zero;

        if (0f < _value)
        {
            int _step = Mathf.FloorToInt(_value * ShakeStepCount);
            float _dir = 0 == (_step & 1) ? 1f : -1f;
            _offset.x = Mathf.Round(_dir * currentShakeStrength * _value);
        }

        shakeTarget.anchoredPosition += _offset - appliedShakeOffset;
        appliedShakeOffset = _offset;
    }

    private static void KillTween(ref Tween _tween)
    {
        if (null != _tween)
        {
            _tween.Kill();
            _tween = null;
        }
    }

    // 진행 중인 연출을 모두 끊고 눈에 보이는 상태를 원래대로 돌려 놓는다.
    private void ResetEffects()
    {
        KillTween(ref flashTween);
        KillTween(ref gainTween);
        KillTween(ref shakeTween);

        bGhostTrailing = false;
        ghostHoldRemain = 0f;
        pendingLossRatio = 0f;
        pendingGainRatio = 0f;

        if (null != ghostSlider)
            ghostSlider.value = displayRatio;

        SetFlashValue(0f);
        SetGainValue(0f);
        SetShakeValue(0f);
    }

    // 유니티 이벤트 함수

    private void OnDestroy()
    {
        KillTween(ref flashTween);
        KillTween(ref gainTween);
        KillTween(ref shakeTween);
    }
}
