using Coffee.UIEffects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버프 아이콘 한 칸. 프레임/아이콘/게이지/숫자와 등장·퇴장·갱신 연출을 담당한다.
/// 타이머와 입자는 HUD_BuffBar가 매 프레임 Tick으로 구동하고 입자는 바의 오버레이가 그린다.
/// 알파 페이드는 쓰지 않고(외곽선이 겹쳐 보인다) 크기 단계, 색 변경, 점멸로만 연출한다.
/// </summary>
public class HUD_BuffIcon : MonoBehaviour
{
    private enum Phase
    {
        Idle = 0,
        Enter,
        ExitFlash,
        Done,
    }

    // 외부 의존성
    [Header("UI Ref")]
    [Tooltip("크기/흔들림 연출 대상. 피벗은 가운데.")]
    [SerializeField] private RectTransform visualRoot;
    [Tooltip("점멸(만료 임박)에 쓰는 그룹. 알파는 0/1로만 바꾼다.")]
    [SerializeField] private CanvasGroup visualGroup;
    [SerializeField] private Image frameImage;
    [Tooltip("프레임의 흰 외곽선(Outline8). 색은 Replace 필터로 바꾼다.")]
    [SerializeField] private UIEffect outlineEffect;
    [SerializeField] private Image accentImage;
    [Tooltip("아이콘 픽셀을 CPU로 합성해 그리는 RawImage. 임의 스프라이트를 읽어 넣는다.")]
    [SerializeField] private RawImage iconImage;
    [SerializeField] private Image gaugeFillTopImage;
    [SerializeField] private Image gaugeFillBottomImage;
    [SerializeField] private RawImage numberImage;

    [Header("Look")]
    [SerializeField] private Color durationAccent = new Color32(170, 212, 88, 255);
    [SerializeField] private Color hitsAccent = new Color32(232, 178, 58, 255);
    [SerializeField] private Color thresholdAccent = new Color32(120, 224, 224, 255);
    [Tooltip("위 지속형 게이지가 이 비율 아래로 내려가면 경고색(붉은 계열)으로 바뀐다.")]
    [Range(0f, 1f)][SerializeField] private float warnRatio = 0.25f;
    [Tooltip("지속형이 이 비율 아래로 내려가면 만료 임박 점멸을 시작한다.")]
    [Range(0f, 1f)][SerializeField] private float blinkRatio = 0.3f;
    [SerializeField] private float shineInterval = 2.6f;

    [Header("Motion")]
    [Tooltip("등장: 이 시간 동안 enterFromScale에서 1까지 커진다(Ease는 아래). 크기는 정수 도트 폭으로 스냅한다.")]
    [SerializeField] private float enterDuration = 0.4f;
    [Range(0f, 1f)][SerializeField] private float enterFromScale = 0.4f;
    [SerializeField] private Ease enterEase = Ease.OutBack;
    [SerializeField] private float enterOvershoot = 2.2f;
    [Tooltip("퇴장: 먼저 exitSwellScale까지 부풀고(exitSwellDuration), 이어서 0까지 줄어든다(exitShrinkDuration).")]
    [SerializeField] private float exitSwellDuration = 0.12f;
    [SerializeField] private float exitSwellScale = 1.25f;
    [SerializeField] private Ease exitSwellEase = Ease.OutQuad;
    [SerializeField] private float exitShrinkDuration = 0.23f;
    [SerializeField] private Ease exitShrinkEase = Ease.InBack;
    [SerializeField] private float exitShrinkOvershoot = 1.7f;
    [Tooltip("갱신/타격 때 위로 튀는 높이(px)와 시간. 정수 도트로 반올림해 적용한다.")]
    [SerializeField] private float bounceHeight = 3f;
    [SerializeField] private float bounceTime = 0.22f;
    [SerializeField] private Ease bounceEase = Ease.OutQuad;
    [SerializeField] private float numberKickTime = 0.3f;
    [SerializeField] private Ease numberKickEase = Ease.OutBack;

    // 연출 상수
    private const float EnterFlashTime = 0.15f;
    private const float ExitFlashTime = 0.12f;
    private const float FrameWidth = 18f;
    private const float HighlightTime = 0.12f;
    private const float ThresholdSmoothTau = 0.1f;
    private const float ShineSweepPortion = 0.28f;
    private const int GaugeInnerWidth = 14;
    private const int NumberTexWidth = 24;
    private const float CenterX = 9f;
    private const float CenterY = 11f;
    private const float GaugeTipY = 3f;
    private const float NumberBaseY = 4f;
    private const float DotRiseTime = 0.4f;

    private static readonly Color32 FlashColor = new Color32(255, 232, 120, 255);
    private static readonly Color32 HighlightColor = new Color32(255, 244, 190, 255);
    private static readonly Color32 SparkColor = new Color32(255, 232, 130, 255);
    private static readonly Color32 EnterDotColor = new Color32(255, 224, 120, 255);
    private static readonly Color32 DecayDotColor = new Color32(224, 58, 78, 255);
    private static readonly Color32 CrossSparkColor = new Color32(160, 240, 240, 255);
    private static readonly Color32 GoldOutline = new Color32(255, 224, 120, 255);
    private static readonly Color32 DurationTop = new Color32(169, 211, 86, 255);
    private static readonly Color32 DurationBottom = new Color32(116, 165, 48, 255);
    private static readonly Color32 WarnTop = new Color32(236, 96, 76, 255);
    private static readonly Color32 WarnBottom = new Color32(170, 52, 42, 255);
    private static readonly Color32 HitsTop = new Color32(246, 200, 70, 255);
    private static readonly Color32 HitsBottom = new Color32(196, 140, 36, 255);
    private static readonly Color32 ThresholdTop = new Color32(120, 224, 224, 255);
    private static readonly Color32 ThresholdBottom = new Color32(62, 168, 176, 255);

    // 내부 의존성
    private HUD_BuffBar bar;
    private HUD_BuffNumberFont font;
    private Texture2D iconTexture;
    private Texture2D numberTexture;
    private Color32[] iconBase;
    private Color32[] iconWork;
    private Color32[] numberBuffer;
    private RectTransform rectTransformCache;
    private RectTransform numberRect;
    private RectTransform iconRect;
    private Vector2 visualBasePos;

    // 상태 변수
    private BuffDisplayInfo info;
    private Phase phase = Phase.Done;
    private int iconWidth = 0;
    private int iconHeight = 0;
    private float age = 0f;
    private float exitAge = 0f;
    private float remaining = 0f;
    private float value = 0f;
    private float shownValue = 0f;
    private float sinceGain = 0f;
    private float hitDecayTimer = 0f;
    private float bounceAge = -1f;
    private float numberKickAge = -1f;
    private float numberKickDir = 0f;
    private float highlightAge = -1f;
    private float goldAge = -1f;
    private float crossAge = -1f;
    private float decayDotTimer = 0f;
    private float shinePhaseOffset = 0f;
    private float slotX = 0f;
    private float slotY = 0f;
    private float slideFromY = 0f;
    private float slideToY = 0f;
    private float slideFrom = 0f;
    private float slideTo = 0f;
    private float slideAge = 1f;
    private bool bOverflowHidden = false;
    private bool bFlashShown = false;
    private int shineKeyShown = -1;
    private bool bIconDirty = true;
    private int numberShown = -1;
    private int numberKickShown = int.MinValue;
    private bool bBlinkHidden = false;
    private bool bMaxedOnce = false;
    private bool bThresholdAbove = false;
    private bool bDecaying = false;
    private Color tint = Color.white;
    private bool bGoldOutline = false;

    public int Id
    {
        get { return info.id; }
    }

    public bool IsExiting
    {
        get { return Phase.ExitFlash == phase || Phase.Done == phase; }
    }

    public bool IsFinished
    {
        get { return Phase.Done == phase; }
    }

    // 현재 값(스택형은 스택 값, 지속형은 남은 시간)
    public float CurrentValue
    {
        get { return BuffGaugeKind.Duration == info.kind ? remaining : value; }
    }

    public float SlotX
    {
        get { return slotX; }
    }

    public void Setup(HUD_BuffBar _bar, HUD_BuffNumberFont _font)
    {
        bar = _bar;
        font = _font;
        rectTransformCache = (RectTransform)transform;
        numberRect = numberImage.rectTransform;
        iconRect = iconImage.rectTransform;
        visualBasePos = visualRoot.anchoredPosition;

        numberTexture = new Texture2D(NumberTexWidth, HUD_BuffNumberFont.GlyphHeight, TextureFormat.RGBA32, false);
        numberTexture.filterMode = FilterMode.Point;
        numberTexture.wrapMode = TextureWrapMode.Clamp;
        numberTexture.name = "BuffNumber";
        numberBuffer = new Color32[NumberTexWidth * HUD_BuffNumberFont.GlyphHeight];
        numberImage.texture = numberTexture;

        gameObject.SetActive(false);
    }

    // 새 버프로 칸을 연다. _bPlayEnter가 false면 등장 연출 없이 바로 보인다.
    public void Begin(in BuffDisplayInfo _info, Vector2 _slotPos, bool _bPlayEnter)
    {
        info = _info;
        gameObject.SetActive(true);

        SanitizeInfo();

        LoadIcon(info.icon);
        ApplyKindLook();

        remaining = info.duration;
        value = Mathf.Clamp(info.initialValue, 0f, info.maxValue);
        shownValue = value;
        sinceGain = 0f;
        hitDecayTimer = 0f;
        bounceAge = -1f;
        numberKickAge = -1f;
        highlightAge = -1f;
        goldAge = -1f;
        crossAge = -1f;
        decayDotTimer = 0f;
        bMaxedOnce = BuffGaugeKind.StackHits == info.kind && info.maxValue <= value;
        bThresholdAbove = BuffGaugeKind.StackThreshold == info.kind && info.thresholdValue <= value;
        bDecaying = false;
        bGoldOutline = false;
        bBlinkHidden = false;
        bOverflowHidden = false;
        numberShown = -1;
        numberKickShown = int.MinValue;
        bFlashShown = false;
        shineKeyShown = -1;
        bIconDirty = true;
        shinePhaseOffset = Mathf.Abs(info.id * 0.37f) % shineInterval;

        age = 0f;
        exitAge = 0f;
        phase = true == _bPlayEnter ? Phase.Enter : Phase.Idle;

        slotX = _slotPos.x;
        slideFrom = _slotPos.x;
        slideTo = _slotPos.x;
        slotY = _slotPos.y;
        slideFromY = _slotPos.y;
        slideToY = _slotPos.y;
        slideAge = 1f;
        ApplyPosition();

        visualGroup.alpha = 1f;
        visualRoot.localScale = true == _bPlayEnter ? Vector3.one * SnapScale(enterFromScale) : Vector3.one;
        visualRoot.anchoredPosition = visualBasePos;

        ApplyTint();
        RefreshNumber(true);
        RefreshGauge();
        RepaintIcon(false, -1);

        if (true == _bPlayEnter)
        {
            EmitEnterFx();
        }
    }

    // 같은 id가 이미 있을 때의 갱신. 지속형은 가득 차오르고 한 번 튄다.
    public void Refresh(in BuffDisplayInfo _info)
    {
        BuffDisplayInfo _prev = info;
        info = _info;

        SanitizeInfo();

        if (_prev.icon != info.icon)
        {
            LoadIcon(info.icon);
            bIconDirty = true;
        }

        ApplyKindLook();

        if (BuffGaugeKind.Duration == info.kind)
        {
            remaining = info.duration;
            bounceAge = 0f;
            highlightAge = 0f;
            EmitDots(CenterX, GaugeTipY, 3, SparkColor);
        }
    }

    // 스택형의 현재 값을 바꾼다. 증가하면 팝/불꽃, 감소하면 가라앉는 연출이 나온다.
    public void SetValue(float _value)
    {
        float _clamped = Mathf.Clamp(_value, 0f, info.maxValue);

        if (BuffGaugeKind.Duration == info.kind)
        {
            remaining = Mathf.Clamp(_value, 0f, info.duration);
            return;
        }

        if (Mathf.Approximately(_clamped, value))
        {
            return;
        }

        bool _bGain = value < _clamped;
        value = _clamped;

        if (true == _bGain)
        {
            sinceGain = 0f;
            hitDecayTimer = 0f;
            OnGain();
        }
        else
        {
            OnLoss(false);
        }
    }

    // 임의 발동 연출(불꽃 + 번쩍). 임계 돌파/최대 도달은 SetValue가 자동으로 호출한다.
    public void Trigger()
    {
        crossAge = 0f;
        highlightAge = 0f;
        EmitSparks(HUD_BuffBar.KindsCross, CrossSparkColor, 0.6f, 9f, 13f);
    }

    public void BeginExit()
    {
        if (true == IsExiting)
        {
            return;
        }

        phase = Phase.ExitFlash;
        exitAge = 0f;
        bIconDirty = true;
    }

    public void SetLayoutTarget(Vector2 _pos, bool _bSnap)
    {
        if (true == _bSnap)
        {
            slotX = _pos.x;
            slideFrom = _pos.x;
            slideTo = _pos.x;
            slotY = _pos.y;
            slideFromY = _pos.y;
            slideToY = _pos.y;
            slideAge = 1f;
            ApplyPosition();
            return;
        }

        if (Mathf.Approximately(slideTo, _pos.x) && Mathf.Approximately(slideToY, _pos.y))
        {
            return;
        }

        slideFrom = slotX;
        slideTo = _pos.x;
        slideFromY = slotY;
        slideToY = _pos.y;
        slideAge = 0f;
    }

    public void SetOverflowHidden(bool _bHidden)
    {
        if (_bHidden == bOverflowHidden)
        {
            return;
        }

        bOverflowHidden = _bHidden;
        visualRoot.gameObject.SetActive(false == _bHidden);

        if (false == _bHidden)
        {
            bIconDirty = true;
            numberShown = -1;
        }
    }

    public void SetTint(Color _tint)
    {
        if (tint == _tint)
        {
            return;
        }

        tint = _tint;
        ApplyTint();
    }

    public void Tick(float _dt, float _clock)
    {
        if (Phase.Done == phase)
        {
            return;
        }

        age += _dt;

        UpdateSlide(_dt);

        if (true == bOverflowHidden)
        {
            TickLogic(_dt);
            return;
        }

        TickLogic(_dt);

        if (Phase.Done == phase)
        {
            return;
        }

        UpdateVisual(_clock);
        RefreshNumber(false);
        RefreshGauge();
    }

    // ===== 논리 갱신 =====

    private void TickLogic(float _dt)
    {
        if (Phase.ExitFlash == phase)
        {
            exitAge += _dt;

            if (exitSwellDuration + exitShrinkDuration <= exitAge)
            {
                FinishExit();
            }

            return;
        }

        if (Phase.Enter == phase && enterDuration <= age)
        {
            phase = Phase.Idle;
        }

        switch (info.kind)
        {
            case BuffGaugeKind.Duration:
                remaining -= _dt;

                if (remaining <= 0f)
                {
                    remaining = 0f;
                    BeginExit();
                }
                break;

            case BuffGaugeKind.StackHits:
                TickHits(_dt);
                break;

            case BuffGaugeKind.StackThreshold:
                TickThreshold(_dt);
                break;
        }

        if (0f <= bounceAge)
        {
            bounceAge += _dt;

            if (bounceTime <= bounceAge)
            {
                bounceAge = -1f;
            }
        }

        if (0f <= numberKickAge)
        {
            numberKickAge += _dt;

            if (numberKickTime <= numberKickAge)
            {
                numberKickAge = -1f;
            }
        }

        if (0f <= highlightAge)
        {
            highlightAge += _dt;

            if (HighlightTime <= highlightAge)
            {
                highlightAge = -1f;
            }
        }

        if (0f <= goldAge)
        {
            goldAge += _dt;

            if (0.5f <= goldAge)
            {
                goldAge = -1f;
            }
        }

        if (0f <= crossAge)
        {
            crossAge += _dt;

            if (0.2f <= crossAge)
            {
                crossAge = -1f;
            }
        }
    }

    private void TickHits(float _dt)
    {
        if (info.decayInterval <= 0f)
        {
            return;
        }

        sinceGain += _dt;

        if (sinceGain < info.decayDelay)
        {
            return;
        }

        hitDecayTimer += _dt;

        if (hitDecayTimer < info.decayInterval)
        {
            return;
        }

        hitDecayTimer -= info.decayInterval;
        value = Mathf.Max(0f, value - 1f);
        OnLoss(true);

        if (value <= 0f)
        {
            BeginExit();
        }
    }

    private void TickThreshold(float _dt)
    {
        sinceGain += _dt;
        bDecaying = false;

        if (0f < info.decayPerSecond && info.decayDelay <= sinceGain && 0f < value)
        {
            value = Mathf.Max(0f, value - info.decayPerSecond * _dt);
            bDecaying = true;

            decayDotTimer += _dt;

            if (0.3f <= decayDotTimer)
            {
                decayDotTimer = 0f;
                EmitFallDots(2, DecayDotColor);
            }

            if (value < info.thresholdValue)
            {
                bThresholdAbove = false;
            }
        }

        float _k = 1f - Mathf.Exp(-_dt / ThresholdSmoothTau);
        shownValue = Mathf.Lerp(shownValue, value, _k);

        if (Mathf.Abs(shownValue - value) < 0.01f)
        {
            shownValue = value;
        }

        if (true == bDecaying && value <= 0f && shownValue <= 0.5f)
        {
            BeginExit();
        }
    }

    private void OnGain()
    {
        if (BuffGaugeKind.StackHits == info.kind)
        {
            bounceAge = 0f;
            numberKickAge = 0f;
            numberKickDir = 2f;
            highlightAge = 0f;
            EmitSparks(HUD_BuffBar.KindsHit, SparkColor, 0.5f, 2f, 5f);
            EmitRiseDots(3, HitsTop);

            if (false == bMaxedOnce && info.maxValue <= value)
            {
                bMaxedOnce = true;
                goldAge = 0f;
                EmitSparks(HUD_BuffBar.KindsBig6, EnterDotColor, 0.6f, 9f, 14f);
                EmitDots(CenterX, CenterY, 24, EnterDotColor);
            }
            else if (value < info.maxValue)
            {
                bMaxedOnce = false;
            }
        }
        else if (BuffGaugeKind.StackThreshold == info.kind)
        {
            numberKickAge = 0f;
            numberKickDir = 1f;
            EmitRiseDots(2, ThresholdTop);

            if (false == bThresholdAbove && info.thresholdValue <= value)
            {
                bThresholdAbove = true;
                Trigger();
            }
        }
    }

    private void OnLoss(bool _bDecay)
    {
        numberKickAge = 0f;
        numberKickDir = -2f;
        bMaxedOnce = false;
        EmitFallDots(5, DecayDotColor);

        if (BuffGaugeKind.StackThreshold == info.kind && value < info.thresholdValue)
        {
            bThresholdAbove = false;
        }

        if (false == _bDecay && value <= 0f)
        {
            BeginExit();
        }
    }

    private void FinishExit()
    {
        phase = Phase.Done;

        float _baseX = slotX;
        float _baseY = slotY;

        if (null != iconBase && false == bOverflowHidden)
        {
            for (int ty = 0; ty < iconHeight; ty++)
            {
                for (int tx = 0; tx < iconWidth; tx++)
                {
                    Color32 _c = iconBase[ty * iconWidth + tx];

                    if (0 == _c.a)
                    {
                        continue;
                    }

                    bar.FxShard(_baseX, _baseY, tx, iconHeight - 1 - ty, iconWidth, iconHeight, _c);
                }
            }

            bar.FxSparks(_baseX + CenterX, _baseY + CenterY, HUD_BuffBar.KindsBig6, SparkColor, 0.5f, 8f, 14f);
        }

        bar.NotifyIconDone(this);
    }

    // ===== 표시 갱신 =====

    // 칸 폭이 정수 도트가 되도록 배율을 스냅한다(0 이하는 최소값으로 눌러 둔다).
    private static float SnapScale(float _scale)
    {
        float _px = Mathf.Max(1f, Mathf.Round(_scale * FrameWidth));
        return _px / FrameWidth;
    }

    private void UpdateSlide(float _dt)
    {
        if (bar.SlideDuration <= slideAge)
        {
            return;
        }

        slideAge += _dt;
        float _k = Mathf.Clamp01(slideAge / bar.SlideDuration);
        float _e = DOVirtual.EasedValue(0f, 1f, _k, bar.SlideEase, bar.SlideOvershoot);
        slotX = Mathf.LerpUnclamped(slideFrom, slideTo, _e);
        slotY = Mathf.LerpUnclamped(slideFromY, slideToY, _e);
        ApplyPosition();
    }

    private void ApplyPosition()
    {
        rectTransformCache.anchoredPosition = new Vector2(Mathf.Round(slotX), Mathf.Round(slotY));
    }

    private void UpdateVisual(float _clock)
    {
        float _scale = 1f;
        bool _bFlash = false;
        int _shineKey = -1;
        bool _bBlinkHidden = false;
        int _fidx = (int)(_clock * 20f);

        if (Phase.Enter == phase)
        {
            float _et = Mathf.Clamp01(age / enterDuration);
            _scale = SnapScale(DOVirtual.EasedValue(enterFromScale, 1f, _et, enterEase, enterOvershoot));
            _bFlash = age < EnterFlashTime;
        }
        else if (Phase.ExitFlash == phase)
        {
            if (exitAge < exitSwellDuration)
            {
                float _st = Mathf.Clamp01(exitAge / exitSwellDuration);
                _scale = SnapScale(DOVirtual.EasedValue(1f, exitSwellScale, _st, exitSwellEase));
            }
            else
            {
                float _ht = Mathf.Clamp01((exitAge - exitSwellDuration) / exitShrinkDuration);
                _scale = SnapScale(DOVirtual.EasedValue(exitSwellScale, 0f, _ht, exitShrinkEase, exitShrinkOvershoot));
            }

            _bFlash = exitAge < ExitFlashTime;
        }
        else
        {
            float _ph = ((age + shinePhaseOffset) % shineInterval) / shineInterval;

            if (_ph < ShineSweepPortion)
            {
                float _sweep = (_ph / ShineSweepPortion) * 34f - 6f;
                _shineKey = Mathf.RoundToInt(_sweep * 2f) + 100;
            }

            if (BuffGaugeKind.Duration == info.kind && remaining < info.duration * blinkRatio && 3 == (_fidx % 4))
            {
                _bBlinkHidden = true;
            }
        }

        // 최대 스택 도달 직후의 금색 점멸과 유지 중 가끔 반짝
        bool _bGold = false;

        if (0f <= goldAge)
        {
            _bGold = 0 == (_fidx % 2);

            if (goldAge < 0.12f)
            {
                _bFlash = true;
            }
        }
        else if (true == bMaxedOnce && 0 == ((int)(_clock * 2f) % 6))
        {
            _bGold = true;
        }

        if (0f <= crossAge && crossAge < 0.1f)
        {
            _bFlash = true;
        }

        if (0f <= bounceAge && bounceAge < 0.08f && BuffGaugeKind.Duration == info.kind)
        {
            _bFlash = true;
        }

        // 위치 튀기(갱신/타격)
        float _bounce = 0f;

        if (0f <= bounceAge)
        {
            _bounce = Mathf.Round(DOVirtual.EasedValue(bounceHeight, 0f, Mathf.Clamp01(bounceAge / bounceTime), bounceEase));
        }

        visualRoot.localScale = new Vector3(_scale, _scale, 1f);
        visualRoot.anchoredPosition = new Vector2(visualBasePos.x, visualBasePos.y + _bounce);

        if (_bBlinkHidden != bBlinkHidden)
        {
            bBlinkHidden = _bBlinkHidden;
            visualGroup.alpha = true == _bBlinkHidden ? 0f : 1f;
        }

        if (_bGold != bGoldOutline)
        {
            bGoldOutline = _bGold;
            ApplyOutlineColor();
        }

        if (_bFlash != bFlashShown || _shineKey != shineKeyShown || true == bIconDirty)
        {
            RepaintIcon(_bFlash, _shineKey);
        }
    }

    private void RefreshNumber(bool _bForce)
    {
        if (BuffGaugeKind.Duration == info.kind)
        {
            if (true == numberImage.enabled)
            {
                numberImage.enabled = false;
            }

            return;
        }

        int _n = BuffGaugeKind.StackThreshold == info.kind ? Mathf.RoundToInt(shownValue) : Mathf.RoundToInt(value);
        _n = Mathf.Clamp(_n, 0, 999);

        float _dy = 0f;

        if (0f <= numberKickAge)
        {
            float _nt = Mathf.Clamp01(numberKickAge / numberKickTime);
            _dy = DOVirtual.EasedValue(numberKickDir, 0f, _nt, numberKickEase);

            if (BuffGaugeKind.StackThreshold == info.kind && true == bDecaying)
            {
                _dy = -1f;
            }
        }
        else if (BuffGaugeKind.StackThreshold == info.kind && true == bDecaying)
        {
            _dy = -1f;
        }

        int _kick = Mathf.RoundToInt(_dy);

        if (false == _bForce && _n == numberShown && _kick == numberKickShown)
        {
            return;
        }

        if (_n != numberShown)
        {
            int _w = font.Compose(_n, false, numberBuffer, NumberTexWidth);
            numberTexture.SetPixels32(numberBuffer);
            numberTexture.Apply(false, false);
            numberImage.uvRect = new Rect(0f, 0f, (float)_w / NumberTexWidth, 1f);
            numberRect.sizeDelta = new Vector2(_w, HUD_BuffNumberFont.GlyphHeight);
            numberRect.anchoredPosition = new Vector2(17f - _w, NumberBaseY + _kick);
        }
        else
        {
            Vector2 _p = numberRect.anchoredPosition;
            numberRect.anchoredPosition = new Vector2(_p.x, NumberBaseY + _kick);
        }

        numberShown = _n;
        numberKickShown = _kick;
        numberImage.enabled = true;
    }

    private void RefreshGauge()
    {
        float _ratio;
        Color32 _top;
        Color32 _bottom;

        switch (info.kind)
        {
            case BuffGaugeKind.StackHits:
                _ratio = value / info.maxValue;
                _top = HitsTop;
                _bottom = HitsBottom;
                break;

            case BuffGaugeKind.StackThreshold:
                _ratio = shownValue / info.thresholdValue;
                _top = ThresholdTop;
                _bottom = ThresholdBottom;
                break;

            default:
                _ratio = 0f < info.duration ? remaining / info.duration : 0f;
                bool _bWarn = _ratio < warnRatio;
                _top = true == _bWarn ? WarnTop : DurationTop;
                _bottom = true == _bWarn ? WarnBottom : DurationBottom;
                break;
        }

        _ratio = Mathf.Clamp01(_ratio);
        float _px = Mathf.Round(_ratio * GaugeInnerWidth);

        // 값이 남아 있으면 최소 1칸은 보이게 한다(0이 되는 순간만 빈 게이지)
        if (_px < 1f && 0.0001f < _ratio)
        {
            _px = 1f;
        }

        float _fill = _px / GaugeInnerWidth;
        gaugeFillTopImage.fillAmount = _fill;
        gaugeFillBottomImage.fillAmount = _fill;

        if (0f <= highlightAge)
        {
            _top = HighlightColor;
        }

        gaugeFillTopImage.color = _top;
        gaugeFillBottomImage.color = _bottom;
    }

    private void RepaintIcon(bool _bFlash, int _shineKey)
    {
        bFlashShown = _bFlash;
        shineKeyShown = _shineKey;
        bIconDirty = false;

        if (null == iconBase)
        {
            return;
        }

        float _sweep = (_shineKey - 100) * 0.5f;

        for (int i = 0; i < iconBase.Length; i++)
        {
            Color32 _c = iconBase[i];

            if (0 == _c.a)
            {
                iconWork[i] = _c;
                continue;
            }

            if (true == _bFlash)
            {
                iconWork[i] = FlashColor;
                continue;
            }

            if (0 <= _shineKey)
            {
                int _x = i % iconWidth;
                int _yTop = iconHeight - 1 - (i / iconWidth);
                float _d = Mathf.Abs((_x + _yTop * 0.6f) - _sweep);
                float _k = _d < 1.2f ? 0.45f : (_d < 2.2f ? 0.2f : 0f);

                if (0f < _k)
                {
                    _c = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Lerp(_c.r, 255f, _k)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(_c.g, 255f, _k)),
                        (byte)Mathf.RoundToInt(Mathf.Lerp(_c.b, 255f, _k)),
                        255);
                }
            }

            iconWork[i] = _c;
        }

        iconTexture.SetPixels32(iconWork);
        iconTexture.Apply(false, false);
    }

    private void LoadIcon(Sprite _sprite)
    {
        Sprite _use = null != _sprite ? _sprite : HUD_BuffPixelUtil.GetTempIcon(info.id);

        if (false == HUD_BuffPixelUtil.TryGetSpritePixels(_use, out Color32[] _pixels, out int _w, out int _h))
        {
            iconBase = null;
            return;
        }

        iconBase = _pixels;
        iconWidth = _w;
        iconHeight = _h;

        if (null == iconWork || iconWork.Length != _pixels.Length)
        {
            iconWork = new Color32[_pixels.Length];
        }

        if (null == iconTexture || iconTexture.width != _w || iconTexture.height != _h)
        {
            if (null != iconTexture)
            {
                Destroy(iconTexture);
            }

            iconTexture = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            iconTexture.filterMode = FilterMode.Point;
            iconTexture.wrapMode = TextureWrapMode.Clamp;
            iconTexture.name = "BuffIcon";
            iconImage.texture = iconTexture;
            iconRect.sizeDelta = new Vector2(_w, _h);
            iconRect.anchoredPosition = new Vector2(3f + (12 - _w) * 0.5f, 6f + (12 - _h) * 0.5f);
        }
    }

    // 임계형: 게이지가 가득 차는 지점이 곧 임계점이다. 값의 상한(maxValue)은 임계점 이상으로 둔다.
    private void SanitizeInfo()
    {
        if (info.maxValue <= 0f)
        {
            info.maxValue = 1f;
        }

        if (BuffGaugeKind.StackThreshold != info.kind)
        {
            return;
        }

        if (info.thresholdValue <= 0f)
        {
            info.thresholdValue = info.maxValue;
        }

        info.maxValue = Mathf.Max(info.maxValue, info.thresholdValue);
    }

    private void ApplyKindLook()
    {
        Color _accent = info.accentColor;

        if (_accent.a <= 0f)
        {
            switch (info.kind)
            {
                case BuffGaugeKind.StackHits:
                    _accent = hitsAccent;
                    break;
                case BuffGaugeKind.StackThreshold:
                    _accent = thresholdAccent;
                    break;
                default:
                    _accent = durationAccent;
                    break;
            }
        }

        accentImage.color = _accent;
    }

    private void ApplyTint()
    {
        frameImage.color = tint;
        iconImage.color = tint;
        ApplyOutlineColor();
    }

    private void ApplyOutlineColor()
    {
        Color _color = true == bGoldOutline ? (Color)GoldOutline : tint;

        // 프레임 스프라이트에 외곽선을 직접 그리는 경우 UIEffect(Outline8)를 꺼 둔다. 그때는 외곽선 색 대신
        // 프레임 이미지 색을 바꿔 최대 스택의 금색 번쩍임과 위험 틴트를 표현한다.
        if (null != outlineEffect && true == outlineEffect.enabled)
        {
            outlineEffect.shadowColor = _color;
            return;
        }

        frameImage.color = _color;
    }

    // ===== 입자 =====

    private void EmitEnterFx()
    {
        if (true == bOverflowHidden)
        {
            return;
        }

        EmitDots(CenterX, CenterY, 30, EnterDotColor);
        bar.FxSparks(slotX + CenterX, slotY + CenterY, HUD_BuffBar.KindsEnter, SparkColor, 0.5f, 9f, 13f);
    }

    private void EmitDots(float _cx, float _cy, int _count, Color32 _color)
    {
        if (true == bOverflowHidden)
        {
            return;
        }

        bar.FxDots(slotX + _cx, slotY + _cy, _count, _color, 0.5f);
    }

    private void EmitSparks(byte[] _kinds, Color32 _color, float _life, float _radMin, float _radMax)
    {
        if (true == bOverflowHidden)
        {
            return;
        }

        bar.FxSparks(slotX + CenterX, slotY + CenterY, _kinds, _color, _life, _radMin, _radMax);
    }

    // 게이지 끝에서 위로 튀어 오르는 점
    private void EmitRiseDots(int _count, Color32 _color)
    {
        if (true == bOverflowHidden)
        {
            return;
        }

        float _ratio = (BuffGaugeKind.StackThreshold == info.kind ? shownValue / info.thresholdValue : value / info.maxValue);
        float _tipX = slotX + 2f + Mathf.Round(GaugeInnerWidth * Mathf.Clamp01(_ratio));

        for (int i = 0; i < _count; i++)
        {
            bar.FxRise(_tipX + Random.Range(-1f, 2f), slotY + GaugeTipY, _color, DotRiseTime, 0.03f * i);
        }
    }

    // 아이콘 오른쪽 아래에서 아래로 떨어지는 붉은 점
    private void EmitFallDots(int _count, Color32 _color)
    {
        if (true == bOverflowHidden)
        {
            return;
        }

        for (int i = 0; i < _count; i++)
        {
            bar.FxFall(slotX + 14f + Random.Range(-3f, 3f), slotY + 6f, _color, 0.5f, Random.Range(0f, 0.1f));
        }
    }

    // 임시 해제(풀로 반환)
    public void Release()
    {
        phase = Phase.Done;
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (null != iconTexture)
        {
            Destroy(iconTexture);
        }

        if (null != numberTexture)
        {
            Destroy(numberTexture);
        }
    }
}
