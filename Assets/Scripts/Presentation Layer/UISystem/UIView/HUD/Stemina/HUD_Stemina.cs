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

    [Header("Hit Effect Ref")]
    [Tooltip("피격 잔상. Fill 아래에 깔려, 깎인 구간이 잠깐 남았다가 따라온다(HUD_HPBar의 고스트 바와 같은 규칙).")]
    [SerializeField] private Slider ghostSlider;
    [Tooltip("피격/회복 플래시. 게이지 전체를 덮고 레이캐스트는 꺼 둔다. 바 모양의 스프라이트를 쓰고, 색은 flashEffect의 색 치환(Replace)이 입힌다.")]
    [SerializeField] private Image flashOverlay;
    [Tooltip("flashOverlay의 UIEffect. 스프라이트의 원래 색이 아니라 지정한 단색으로 실루엣만 칠한다.")]
    [SerializeField] private UIEffect flashEffect;
    [Tooltip("피격 흔들림을 줄 대상. 비워 두면 이 오브젝트 자신. 저피로도 경고(DangerShake)는 SteminaBar를 흔들므로 그 위 단계를 지정해 합산되게 한다.")]
    [SerializeField] private RectTransform shakeTarget;

    [Header("Recover Effect Ref")]
    [Tooltip("바의 채움 이미지(Fill). 회복 연출의 몸통 영역(스프라이트 알파에서 테두리/하이라이트/그림자 줄을 뺀 안쪽)과 색(스프라이트 평균색 x 현재 틴트)을 여기서 읽어 항상 바와 같은 색 계열로 그린다.")]
    [SerializeField] private Image barFillImage;
    [Tooltip("\"+\" 입자가 그려질 영역(바 전체와 같은 크기). 입자의 앵커는 (0, 0.5), 피벗은 (0, 0)으로 둔다(왼쪽 아래 모서리를 정수 픽셀에 맞추기 위함). SteminaBar의 자식으로 두어 경고 흔들림을 같이 받는다.")]
    [SerializeField] private RectTransform sparkleContainer;
    [SerializeField] private RectTransform[] sparkleRects;
    [SerializeField] private Image[] sparkleImages;
    [Tooltip("3x3 \"+\" 모양 흰색 픽셀 스프라이트. 비워 두면 \"+\"는 1px 점으로 대신한다.")]
    [SerializeField] private Sprite sparklePlusSprite;

    [Header("Hit Effect")]
    [SerializeField] private Color damageFlashColor = new Color(1f, 0.15f, 0.15f, 1f);
    [Tooltip("변화량이 fullEffectRatio 이상일 때의 플래시 알파")]
    [Range(0f, 1f)][SerializeField] private float flashMaxAlpha = 0.75f;
    [Tooltip("변화량이 minEffectRatio 근처일 때의 플래시 알파")]
    [Range(0f, 1f)][SerializeField] private float flashMinAlpha = 0.25f;
    [SerializeField] private float damageFlashDuration = 0.12f;
    [SerializeField] private int damageFlashCount = 2;
    [Tooltip("피격 후 잔상이 남아 있다가 따라오기 시작하기까지의 시간(초)")]
    [SerializeField] private float ghostDelay = 0.35f;
    [Tooltip("잔상이 현재 값을 따라잡는 데 걸리는 대략적인 시간(초). 현재 값을 매 프레임 따라가므로 지속 소모 중에도 끊기지 않는다.")]
    [SerializeField] private float ghostFollowDuration = 0.3f;
    [Tooltip("피격 흔들림의 최대 세기(캔버스 픽셀)")]
    [SerializeField] private float shakeStrength = 2f;
    [SerializeField] private float shakeDuration = 0.2f;

    [Header("Recover Effect")]
    [Tooltip("게이지가 회복 전 값에서 새 값까지 차오르는 대략적인 시간(초). 현재 값을 매 프레임 따라가므로 지속 소모가 겹쳐도 끊기지 않는다.")]
    [SerializeField] private float fillUpDuration = 0.35f;
    [Tooltip("이 비율(최대치 대비) 이상의 단발 회복에서만 플래시, \"+\"를 띄운다. 작은 회복은 차오름만 보인다.")]
    [Range(0f, 1f)][SerializeField] private float sparkleMinRatio = 0.03f;
    [Tooltip("몸통 영역 위쪽에서 제외하는 줄 수. 스프라이트의 위쪽 하이라이트/어두운 줄(2행)을 피한다.")]
    [SerializeField] private int bodyInsetTop = 2;
    [Tooltip("몸통 영역 아래쪽에서 제외하는 줄 수. 스프라이트의 아래쪽 하이라이트 줄과 그림자(3행)를 피한다.")]
    [SerializeField] private int bodyInsetBottom = 3;
    [Tooltip("몸통 영역 좌우에서 제외하는 열 수. 스프라이트의 왼쪽/오른쪽 윤곽(3열)을 피한다.")]
    [SerializeField] private int bodyInsetSide = 3;

    [Header("Recover Effect - Flash")]
    [Tooltip("회복 플래시 색: 바의 현재 색에서 흰색 쪽으로 밝히는 비율. 1이면 순백이다. 피격 플래시(붉은색)와 같은 길이/횟수/알파 규칙을 쓴다.")]
    [Range(0f, 1f)][SerializeField] private float recoverFlashLighten = 0.75f;
    [SerializeField] private float recoverFlashDuration = 0.12f;
    [SerializeField] private int recoverFlashCount = 2;

    [Header("Recover Effect - Plus Particles")]
    [Tooltip("단발 회복에서 늘어난 구간에서 떠오르는 \"+\"의 개수(x: 회복량 최소, y: 회복량 최대)")]
    [SerializeField] private Vector2Int plusCountRange = new Vector2Int(5, 10);
    [Tooltip("\"+\"의 처음 위쪽 속도(캔버스 픽셀/초)")]
    [SerializeField] private Vector2 plusRiseSpeed = new Vector2(14f, 26f);
    [Tooltip("\"+\"가 좌우로 흔들리는 폭(캔버스 픽셀)")]
    [SerializeField] private float plusSway = 1.2f;
    [Tooltip("\"+\" 하나가 머무는 시간(초). 3x3 \"+\"로 떠오르다 끝 무렵 1px 점으로 줄고 깜빡이며 사라진다.")]
    [SerializeField] private Vector2 plusLifetime = new Vector2(0.7f, 1f);
    [Tooltip("\"+\"가 바의 몸통 위쪽 끝에서 더 올라갈 수 있는 최대 높이(캔버스 픽셀). 이보다 높이는 그리지 않는다.")]
    [SerializeField] private float plusRiseLimit = 16f;
    [Tooltip("\"+\" 색: 바의 현재 색에서 흰색 쪽으로 밝히는 비율")]
    [Range(0f, 1f)][SerializeField] private float plusLighten = 0.8f;
    [Tooltip("휴식 구역 안에서 \"+\" 하나가 떠오르는 간격(초). 바가 충분히 찼을 때의 값이며, 채움이 적으면 비율에 맞춰 늘어난다.")]
    [SerializeField] private float restPlusInterval = 0.15f;

    [Header("Common")]
    [Tooltip("이 비율(최대치 대비)보다 작은 변화는 연출하지 않는다. 휴식 구역 틱처럼 잦고 작은 회복이 계속 번쩍이는 것을 막는다.")]
    [Range(0f, 1f)][SerializeField] private float minEffectRatio = 0.01f;
    [Tooltip("이 비율 이상의 변화는 연출 세기가 최대가 된다.")]
    [Range(0.01f, 1f)][SerializeField] private float fullEffectRatio = 0.2f;

    // 내부 의존성
    private struct PlusState
    {
        public bool bAlive;
        public float x;
        public float y;
        public float vy;
        public float swayPhase;
        public float age;
        public float life;
    }

    private const int ShakeStepCount = 8;
    // 휴식 "+" 간격 보정 기준 채움 비율. 이보다 덜 찼으면 좁은 구간에 "+"가 뭉치지 않도록 간격을 비율만큼 늘린다.
    private const float RestPlusMinFillRatio = 0.35f;
    // 잔상/차오름이 현재 값과의 차이가 이보다 작으면(160px 바 기준 반 픽셀 미만) 눈에 보이지 않으므로 바로 맞춘다.
    private const float SnapGap = 0.003f;
    // 지수 감쇠의 세기. 클수록 정해진 시간 안에 더 확실히 따라잡는다.
    private const float FollowSharpness = 5f;
    // 격차가 작아져도 끝이 늘어지지 않도록 하는 최소 속도(비율/초)
    private const float GhostMinFollowSpeed = 0.15f;
    // 차오름은 일정한 속도로 차오르다가 목표에 가까워질 때만 느려진다. (처음부터 지수 감쇠로 시작하면 첫 프레임에 한꺼번에 튄다)
    private const float FillEaseGap = 0.12f;
    private const float FillEaseMinFactor = 0.35f;
    private const float FillMinSpeed = 0.5f;
    // "+"는 이 간격으로 한 칸씩만 움직인다. (20fps)
    private const float PixelStep = 0.05f;
    private const int MaxStepsPerFrame = 4;
    // "+" 수명의 이 비율을 지나면 깜빡이며 사라진다.
    private const float PlusBlinkStart = 0.7f;
    // "+" 수명의 이 비율까지는 3x3 모양, 이후는 1px 점
    private const float PlusShapeEnd = 0.6f;
    // 시작 위치를 몸통 안에서 찾을 때 시도하는 횟수
    private const int PlacementTries = 6;
    // 한 번에 예약해 둘 수 있는 "+"의 최대 개수
    private const int MaxPlannedPlus = 16;
    // 윤곽(스프라이트 알파)을 안쪽으로 인정하는 기준
    private const byte SilhouetteAlphaCut = 127;
    // 윤곽 표 생성에 실패했을 때 다시 시도하는 간격(초)
    private const float SilhouetteRetryInterval = 0.5f;

    private HUD_ScreenBlood screenBloodComponent;
    // 던전에서만 true. 마을에서는 게이지가 숨겨지고 저피로도 경고(게이지 흔들림/붉은 화면)도 판정하지 않는다.
    private bool bActive = false;
    private bool bWarningGauge = false;
    private MotionEntry shakeMotion;
    private MotionEntry colorMotion;

    // 피격/회복 연출 상태
    // displayRatio: 마지막으로 받은 실제 비율. shownRatio: 게이지에 실제로 그려 주는 값(순간 회복 때만 실제 비율보다 늦게 차오른다).
    private float displayRatio = 1f;
    private float shownRatio = 1f;
    private float pendingLossRatio = 0f;
    private float pendingGainRatio = 0f;
    private bool bGhostTrailing = false;
    private float ghostHoldRemain = 0f;
    private bool bFillingUp = false;
    private bool bFillEvent = false;
    private float fillSegmentLow = 0f;
    private float fillSpeed = 0f;
    private float fillIntensity = 0f;

    private float flashValue = 0f;
    private Color flashColor = Color.white;
    private float flashPeakAlpha = 0f;
    private float shakeValue = 0f;
    private float currentShakeStrength = 0f;
    private Vector2 appliedShakeOffset = Vector2.zero;

    private PlusState[] pluses;
    private int alivePlusCount = 0;
    private float pixelStepTimer = 0f;

    // 차오르는 동안 앞쪽이 지나갈 때 켜도록 예약해 둔 "+"의 가로 위치(캔버스 픽셀)
    private float[] plannedPlusX = new float[MaxPlannedPlus];
    private int plannedPlusCount = 0;

    // 바 윤곽(Fill 스프라이트 알파)과 그 안쪽 몸통 영역, 기본색. 시작할 때 한 번만 읽는다.
    private bool[] barInside;
    private bool[] barBody;
    private int barW = 0;
    private int barH = 0;
    private int bodyTopRow = 0;
    private Color barBaseColor = new Color(0.66f, 0.82f, 0.36f, 1f);
    private bool bSilhouetteReady = false;
    private float silhouetteRetryAt = 0f;
    // 이번 갱신에서 "+"와 플래시가 쓰는 바의 현재 색(스프라이트 평균색 x Fill 틴트)
    private Color barColorNow = new Color(0.66f, 0.82f, 0.36f, 1f);

    private bool bResting = false;
    private float restPlusTimer = 0f;

    private Tween flashTween;
    private Tween shakeTween;

    // 트윈마다 델리게이트를 새로 만들지 않도록 한 번만 캐싱한다.
    private DOGetter<float> getFlashValue;
    private DOSetter<float> setFlashValue;
    private DOGetter<float> getShakeValue;
    private DOSetter<float> setShakeValue;

    // 스태미나 무적 원인별 상태(SetInvincible 참고)
    private bool bEntryInvincible = false;
    private bool bRestAreaInvincible = false;


    // 퍼블릭 초기화 및 제어 메서드

    public void Initialize(HUD_ScreenBlood _screenBlood)
    {
        progressBar?.Initialize();
        motionPlayer?.Initialize();

        screenBloodComponent = _screenBlood;

        getFlashValue = GetFlashValue;
        setFlashValue = SetFlashValue;
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

        pluses = new PlusState[GetPlusTotal()];
        BuildBarSilhouette();

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

        // 이벤트(순간 변화)와 값 변경은 같은 호출 안에서 일어나므로, 이벤트는 항상 HUD가 새 값을 보기 전에 도착한다.
        float _prevShown = shownRatio;
        bool _wasFilling = bFillingUp;

        if (0f < pendingGainRatio)
            BeginFillUp(_ratio);

        UpdateShownRatio(_ratio);
        progressBar?.UpdateValue(shownRatio);

        UpdateGhost(_ratio, _prevShown, _wasFilling);
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

        // 잔상의 시작점은 줄어든 값이 HUD 비율에 반영된 뒤(UpdateValue)에 정한다.
        pendingLossRatio += _lossRatio;

        StartFlash(damageFlashColor, _intensity, damageFlashDuration, Mathf.Max(1, damageFlashCount));
        StartShake(_intensity);
    }

    /// <summary>
    /// 스태미나가 한 번에 회복된 순간의 연출(차오름 + 바 전체 플래시 + "+" 상승). _gainRatio는 최대치 대비 늘어난 비율이다.
    /// 자연 회복처럼 매 프레임 쌓이는 회복은 호출하지 않는다.
    /// </summary>
    public void PlayRecovered(float _gainRatio)
    {
        if (false == bActive || minEffectRatio > _gainRatio)
            return;

        pendingGainRatio += _gainRatio;
    }

    /// <summary>
    /// 최대치에 막혀 회복량이 일부 또는 전부 버려진 단발 회복(벌목, 포션)의 표시. 채워질 구간이 없어도 약한 플래시와 "+" 몇 개를 띄운다.
    /// 휴식 구역 안에서는 조용한 "+"가 이미 떠오르고 있어 따로 재생하지 않는다.
    /// </summary>
    public void PlayRecoverWasted()
    {
        if (false == bActive || true == bResting || false == bSilhouetteReady)
            return;

        RefreshBarColor();

        float _filledPx = GetBarWidth() * Mathf.Clamp01(displayRatio);
        StartRecoverFlash(0.3f);
        SpawnPlusNow(3, 4f, _filledPx - 4f);
    }

    /// <summary>
    /// 휴식 구역 안에서 회복 중인지. true인 동안 피로도가 꽉 차 있어도 "+" 하나가 일정 간격으로 조용히 떠오른다.
    /// 매 프레임 불러도 값이 같으면 아무 일도 하지 않는다.
    /// </summary>
    public void SetResting(bool _bResting)
    {
        bool _bNext = true == _bResting && true == bActive;

        if (_bNext == bResting)
            return;

        bResting = _bNext;

        // 들어오자마자 첫 "+"가 나가도록 타이머를 가득 채워 둔다. 나갈 때는 이미 떠 있는 "+"가 수명대로 사라진다.
        restPlusTimer = true == bResting ? restPlusInterval : 0f;
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

        // 무적 상태도 맵이 바뀔 때마다 비운다. 꺼짐 알림이 어떤 이유로 빠져도 다음 맵까지 연출이 남지 않게 한다.
        // 던전 쪽은 이 호출(DecalreDungeonTypeSignal)이 차량 활성화(ReadyPortal) 직후 같은 프레임에 오므로
        // 휴식 구역 진입 알림(차량의 다음 Update)보다 항상 먼저다. 입장 무적 켜짐은 그보다도 한참 뒤다.
        ResetInvincible();
    }

    /// <summary>
    /// 스태미나가 닳지 않는 무적 구간의 원인별 시작(true) / 종료(false).
    ///   · DungeonEntry: HUD가 올라오기 시작하는 프레임에 켜지고, 실제 소모가 시작되는 프레임에 꺼진다(약 0.7초 간격).
    ///   · RestArea: "휴식" 특성으로 열린 차량 주변 휴식 구역에 들어가고 나갈 때.
    /// 스폰 지점이 휴식 구역 안이라 두 원인은 자주 겹친다. 그래서 원인별로 따로 기억하고, 둘을 합친
    /// 무적 여부가 실제로 바뀔 때만 연출을 켜고 끈다(입장 무적이 끝나도 원 안이면 연출이 이어진다).
    /// </summary>
    public void SetInvincible(EStaminaInvincibleSource _source, bool _bInvincible)
    {
        // 마을에서는 무적 연출을 쓰지 않는다. 마을 차량에도 같은 휴식 구역이 있어 알림은 오지만 기록하지도 않는다.
        if (false == bActive)
            return;

        bool bWasInvincible = IsInvincible;

        if (EStaminaInvincibleSource.DungeonEntry == _source)
            bEntryInvincible = _bInvincible;
        else if (EStaminaInvincibleSource.RestArea == _source)
            bRestAreaInvincible = _bInvincible;

        if (bWasInvincible != IsInvincible)
            PlayInvincibleEffect(IsInvincible);
    }

    private bool IsInvincible => bEntryInvincible || bRestAreaInvincible;

    private void ResetInvincible()
    {
        bool bWasInvincible = IsInvincible;

        bEntryInvincible = false;
        bRestAreaInvincible = false;

        if (true == bWasInvincible)
            PlayInvincibleEffect(false);
    }

    private void PlayInvincibleEffect(bool _bOn)
    {
        // TODO: 무적 연출 (켜짐: 연출 시작 / 꺼짐: 연출 종료)
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

    // 순간 회복이 반영되는 첫 프레임: 게이지가 "회복 전 값"에서 차오르기 시작하도록 보이는 값을 되돌려 놓고 연출을 시작한다.
    private void BeginFillUp(float _ratio)
    {
        float _gain = pendingGainRatio;
        pendingGainRatio = 0f;

        float _high = Mathf.Clamp01(_ratio);

        if (false == bFillingUp)
        {
            shownRatio = Mathf.Clamp01(_ratio - _gain);
            fillSegmentLow = shownRatio;
            bFillingUp = true;
            fillIntensity = 0f;
            plannedPlusCount = 0;
        }

        fillIntensity = Mathf.Max(fillIntensity, GetIntensity(_gain));

        // 작은 회복은 플래시, "+" 없이 차오름만 보여 준다. 차오르는 중 큰 회복이 겹치면 큰 쪽으로 올린다.
        // 플래시는 차오름과 동시에 시작한다(겹친 회복이면 올라간 세기로 다시 시작).
        if (_gain >= sparkleMinRatio)
        {
            bFillEvent = true;
            PlanPluses(fillSegmentLow, _high, _gain);

            RefreshBarColor();
            StartRecoverFlash(fillIntensity);
        }

        // 남은 거리를 fillUpDuration 안에 채우는 속도. 이미 차오르는 중에 또 회복되면 구간의 시작은 그대로 두고 남은 거리로 다시 계산한다.
        fillSpeed = Mathf.Max(FillMinSpeed, (_high - shownRatio) / Mathf.Max(0.05f, fillUpDuration));
    }

    // 보이는 값: 차오르는 동안은 실제 비율을 향해 일정한 속도로 차오르고(지속 소모가 겹쳐도 끊기지 않음), 아니면 실제 비율과 같다.
    private void UpdateShownRatio(float _ratio)
    {
        if (false == bFillingUp)
        {
            shownRatio = _ratio;
            return;
        }

        // 실제 값이 보이는 값 이하로 내려갔다면(피격, 지속 소모) 더 차오를 곳이 없으니 맞추고 끝낸다.
        if (_ratio <= shownRatio)
        {
            shownRatio = _ratio;
            EndFillUp();
            return;
        }

        float _gap = _ratio - shownRatio;
        float _dt = Time.unscaledDeltaTime;
        float _ease = Mathf.Lerp(FillEaseMinFactor, 1f, Mathf.Clamp01(_gap / FillEaseGap));
        float _step = fillSpeed * _ease * _dt;

        shownRatio = Mathf.Min(_ratio, shownRatio + _step);

        if (SnapGap >= _ratio - shownRatio)
        {
            shownRatio = _ratio;
            EndFillUp();
            return;
        }
    }

    // 다 차오르면, 큰 회복은 아직 못 떠오른 "+"가 모두 떠오른다. (플래시는 차오름이 시작될 때 이미 시작했다)
    private void EndFillUp()
    {
        bFillingUp = false;

        if (true == bFillEvent)
        {
            RefreshBarColor();

            FlushPlannedPluses();
        }

        bFillEvent = false;
    }

    // 고스트 바: 피격 직후에는 줄어들기 전 값을 잠깐 붙들고(ghostDelay), 이후 "보이는 값"을 매 프레임 따라간다.
    // 따라가는 목표를 피격 순간의 값으로 고정하면, 지속 소모가 계속되는 동안 잔상이 옛 값에서 멈춘 뒤 한 번에 맞춰져
    // 붉은 조각이 툭 사라진다. 회복되면 예전처럼 즉시 맞춘다.
    private void UpdateGhost(float _ratio, float _prevShown, bool _wasFilling)
    {
        if (null == ghostSlider)
        {
            pendingLossRatio = 0f;
            return;
        }

        if (0f < pendingLossRatio)
        {
            // 줄어들기 전 값에서 잔상을 시작한다. 차오르는 중이었다면 그때 "보이던 값"이 기준이라, 아직 안 채워진 곳이 붉게 보이지 않는다.
            float _start = Mathf.Min(1f, _ratio + pendingLossRatio);
            if (true == _wasFilling)
                _start = Mathf.Min(_start, _prevShown);

            if (_start > shownRatio)
                ghostSlider.value = Mathf.Max(ghostSlider.value, _start);

            pendingLossRatio = 0f;
            ghostHoldRemain = ghostDelay;
            bGhostTrailing = true;
        }

        if (false == bGhostTrailing)
        {
            ghostSlider.value = shownRatio;
            return;
        }

        // 보이는 값이 잔상 이상이면(회복, 또는 따라잡음) 잔상을 맞추고 끝낸다.
        float _gap = ghostSlider.value - shownRatio;
        if (SnapGap >= _gap)
        {
            ghostSlider.value = shownRatio;
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
        float _follow = 1f - Mathf.Exp(-_dt * FollowSharpness / Mathf.Max(0.01f, ghostFollowDuration));
        float _step = Mathf.Max(_gap * _follow, GhostMinFollowSpeed * _dt);
        ghostSlider.value = Mathf.Max(shownRatio, ghostSlider.value - _step);
    }

    // 회복 플래시: 피격 플래시와 같은 경로(길이, 횟수, 알파 규칙)를 쓰고 색만 바 색을 밝힌 라임-흰색으로 바꾼다.
    private void StartRecoverFlash(float _intensity)
    {
        Color _color = Color.Lerp(barColorNow, Color.white, recoverFlashLighten);
        StartFlash(_color, Mathf.Clamp01(_intensity), recoverFlashDuration, Mathf.Max(1, recoverFlashCount));
    }

    // 바 윤곽과 기본색을 Fill 스프라이트에서 한 번 읽는다. 스프라이트가 읽기 불가여도 GPU 복사로 읽는다.
    // 윤곽은 캔버스 픽셀 단위(바 너비 x 바 높이)로 표를 만들고, 그 안쪽에서 위/아래/좌우 윤곽 줄을 뺀 "몸통 영역"을 따로 만든다.
    // 효과는 몸통 영역 안(과 "+"가 떠오르는 바 위쪽 일정 높이)에서만 그린다. 스텐실 마스크는 쓰지 않는다.
    // (렌더 대상에 스텐실이 없으면 마스크가 동작하지 않고, 알파 윤곽만 쓰면 하이라이트/그림자 줄 위에 효과가 올라간다)
    private void BuildBarSilhouette()
    {
        bSilhouetteReady = false;
        silhouetteRetryAt = Time.unscaledTime + SilhouetteRetryInterval;

        if (null == barFillImage || null == barFillImage.sprite || null == sparkleContainer)
        {
            Debug.LogWarning("[HUD_Stemina] 윤곽 표 생성 실패: barFillImage/sprite/sparkleContainer 중 비어 있는 참조가 있습니다. "
                + "barFillImage=" + (null != barFillImage) + " sparkleContainer=" + (null != sparkleContainer));
            return;
        }

        Sprite _sprite = barFillImage.sprite;
        Texture _source = _sprite.texture;
        Rect _rect = _sprite.textureRect;

        int _sw = Mathf.RoundToInt(_rect.width);
        int _sh = Mathf.RoundToInt(_rect.height);

        // 레이아웃이 아직 계산되지 않아 크기가 0이면 스프라이트 원본 크기(캔버스 1픽셀 = 스프라이트 1픽셀)로 대신한다.
        int _w = Mathf.RoundToInt(sparkleContainer.rect.width);
        int _h = Mathf.RoundToInt(sparkleContainer.rect.height);
        if (0 >= _w)
            _w = _sw;
        if (0 >= _h)
            _h = _sh;

        if (null == _source || 0 >= _w || 0 >= _h || 0 >= _sw || 0 >= _sh)
        {
            Debug.LogWarning("[HUD_Stemina] 윤곽 표 생성 실패: 크기가 0입니다. container=" + sparkleContainer.rect.size + " sprite=" + _sw + "x" + _sh);
            return;
        }

        RenderTexture _rt = RenderTexture.GetTemporary(_source.width, _source.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture _prev = RenderTexture.active;

        Graphics.Blit(_source, _rt);
        RenderTexture.active = _rt;

        Texture2D _read = new Texture2D(_sw, _sh, TextureFormat.RGBA32, false);
        _read.ReadPixels(new Rect(_rect.x, _rect.y, _sw, _sh), 0, 0);
        _read.Apply();

        RenderTexture.active = _prev;
        RenderTexture.ReleaseTemporary(_rt);

        Color32[] _pixels = _read.GetPixels32();
        Destroy(_read);

        barW = _w;
        barH = _h;
        barInside = new bool[_w * _h];
        barBody = new bool[_w * _h];

        float _r = 0f;
        float _g = 0f;
        float _b = 0f;
        int _opaque = 0;

        for (int y = 0; y < _h; y++)
        {
            int _sy = Mathf.Clamp(Mathf.FloorToInt((y + 0.5f) / _h * _sh), 0, _sh - 1);

            for (int x = 0; x < _w; x++)
            {
                int _sx = Mathf.Clamp(Mathf.FloorToInt((x + 0.5f) / _w * _sw), 0, _sw - 1);
                Color32 _c = _pixels[_sy * _sw + _sx];

                if (SilhouetteAlphaCut < _c.a)
                {
                    barInside[y * _w + x] = true;
                    _r += _c.r;
                    _g += _c.g;
                    _b += _c.b;
                    _opaque++;
                }
            }
        }

        if (0 < _opaque)
            barBaseColor = new Color(_r / _opaque / 255f, _g / _opaque / 255f, _b / _opaque / 255f, 1f);

        // 몸통 영역: 윤곽 안쪽에서, 위/아래/좌우로 정해진 폭만큼 윤곽 밖과 닿지 않는 픽셀만 남긴다. (상자 모양 침식)
        int _top = Mathf.Max(0, bodyInsetTop);
        int _bottom = Mathf.Max(0, bodyInsetBottom);
        int _side = Mathf.Max(0, bodyInsetSide);

        bodyTopRow = 0;

        for (int y = 0; y < _h; y++)
        {
            for (int x = 0; x < _w; x++)
            {
                if (false == barInside[y * _w + x])
                    continue;

                bool _bBody = true;

                for (int dy = -_bottom; dy <= _top && true == _bBody; dy++)
                {
                    int _yy = y + dy;

                    for (int dx = -_side; dx <= _side; dx++)
                    {
                        int _xx = x + dx;

                        // 바의 좌우 끝(0, 너비-1)을 넘어서는 곳은 윤곽 밖으로 본다. 위/아래도 마찬가지다.
                        if (0 > _xx || _xx >= _w || 0 > _yy || _yy >= _h || false == barInside[_yy * _w + _xx])
                        {
                            _bBody = false;
                            break;
                        }
                    }
                }

                barBody[y * _w + x] = _bBody;

                if (true == _bBody && y > bodyTopRow)
                    bodyTopRow = y;
            }
        }

        barColorNow = barBaseColor;
        bSilhouetteReady = true;
    }

    // 윤곽 표가 아직 없으면(레이아웃 지연 등으로 첫 시도가 실패한 경우) 일정 간격으로 다시 만든다.
    // 표가 없으면 효과가 전부 숨겨지므로, 게임에서 한 번 실패해도 연출이 영구히 사라지지 않게 한다.
    private void EnsureSilhouette()
    {
        if (true == bSilhouetteReady || Time.unscaledTime < silhouetteRetryAt)
            return;

        BuildBarSilhouette();
    }

    // 캔버스 픽셀 (x, y)(바의 왼쪽 아래가 원점)가 몸통 영역인지. 표가 없으면 안쪽이 아닌 것으로 본다.
    private bool IsInsideBody(int _x, int _y)
    {
        if (0 > _x || _x >= barW || 0 > _y || _y >= barH)
            return false;

        if (false == bSilhouetteReady || null == barBody)
            return false;

        return barBody[_y * barW + _x];
    }

    // 바가 지금 화면에 표시되는 색. 스프라이트 평균색에 Fill의 현재 틴트(경고 펄스 포함)를 곱한 값이라 항상 바와 같은 색 계열이다.
    private void RefreshBarColor()
    {
        Color _tint = null != barFillImage ? barFillImage.color : Color.white;

        barColorNow = new Color(barBaseColor.r * _tint.r, barBaseColor.g * _tint.g, barBaseColor.b * _tint.b, 1f);
    }

    // "+" 입자

    private float GetBarWidth()
    {
        // 레이아웃이 아직 계산되지 않아 0이면 윤곽 표를 만들 때 쓴 폭을 대신 쓴다.
        if (null != sparkleContainer && 0f < sparkleContainer.rect.width)
            return sparkleContainer.rect.width;

        return barW;
    }

    private int GetPlusTotal()
    {
        if (null == sparkleRects || null == sparkleImages)
            return 0;

        return Mathf.Min(sparkleRects.Length, sparkleImages.Length);
    }

    private int ClaimPlusSlot(int _from)
    {
        for (int i = _from; i < pluses.Length; i++)
        {
            if (false == pluses[i].bAlive)
                return i;
        }

        return -1;
    }

    // 회복 구간 [_lowRatio, _highRatio]에서 켤 "+"의 가로 위치를 정해 둔다. 차오르는 앞쪽이 그 위치를 지날 때 켜진다.
    private void PlanPluses(float _lowRatio, float _highRatio, float _gainRatio)
    {
        float _width = GetBarWidth();
        float _lo = _width * Mathf.Clamp01(_lowRatio) + 2f;
        float _hi = _width * Mathf.Clamp01(_highRatio) - 2f;

        if (_hi < _lo)
            return;

        int _n = Mathf.RoundToInt(Mathf.Lerp(plusCountRange.x, plusCountRange.y, GetIntensity(_gainRatio)));

        for (int i = 0; i < _n && plannedPlusCount < MaxPlannedPlus; i++)
        {
            plannedPlusX[plannedPlusCount] = Random.Range(_lo, _hi);
            plannedPlusCount++;
        }
    }

    // 차오르는 앞쪽이 지나간 위치의 "+"를 켜고 목록에서 뺀다.
    private void ReleasePlannedPluses(float _frontPx)
    {
        int _write = 0;

        for (int i = 0; i < plannedPlusCount; i++)
        {
            if (plannedPlusX[i] <= _frontPx)
                SpawnPlusAt(plannedPlusX[i]);
            else
                plannedPlusX[_write++] = plannedPlusX[i];
        }

        plannedPlusCount = _write;
    }

    private void FlushPlannedPluses()
    {
        for (int i = 0; i < plannedPlusCount; i++)
            SpawnPlusAt(plannedPlusX[i]);

        plannedPlusCount = 0;
    }

    // 구간 [_fromPx, _toPx] 안에 "+" _count개를 한꺼번에 켠다.
    private void SpawnPlusNow(int _count, float _fromPx, float _toPx)
    {
        if (_toPx < _fromPx)
            return;

        for (int i = 0; i < _count; i++)
            SpawnPlusAt(Random.Range(_fromPx, _toPx));
    }

    // 가로 위치 _xPx의 몸통 안쪽 행 하나에서 "+"를 켠다. 몸통 안에서 자리를 못 찾으면 켜지 않는다.
    private void SpawnPlusAt(float _xPx)
    {
        if (null == pluses || 0 == pluses.Length || false == bSilhouetteReady)
            return;

        int _slot = ClaimPlusSlot(0);
        if (0 > _slot)
            return;

        int _px = Mathf.RoundToInt(_xPx);
        int _py = 0;
        bool _bFound = false;

        for (int t = 0; t < PlacementTries; t++)
        {
            _py = Random.Range(0, barH);

            if (true == IsInsideBody(_px, _py))
            {
                _bFound = true;
                break;
            }
        }

        if (false == _bFound)
            return;

        PlusState _p = pluses[_slot];

        _p.bAlive = true;
        _p.age = 0f;
        _p.life = Random.Range(plusLifetime.x, plusLifetime.y);
        _p.x = _px;
        // y는 바 가운데가 0이라, 아래 기준 행에서 높이의 절반을 뺀다.
        _p.y = _py - barH * 0.5f;
        _p.vy = Random.Range(plusRiseSpeed.x, plusRiseSpeed.y);
        _p.swayPhase = Random.Range(0f, 6.2832f);

        pluses[_slot] = _p;
        ApplyPlusVisual(_slot);
        alivePlusCount = CountAlivePluses();
    }

    private int CountAlivePluses()
    {
        int _alive = 0;

        if (null != pluses)
        {
            for (int i = 0; i < pluses.Length; i++)
            {
                if (true == pluses[i].bAlive)
                    _alive++;
            }
        }

        return _alive;
    }

    // "+" 하나를 지금 나이에 맞춰 그린다. 알파는 쓰지 않고 크기 단계와 깜빡임으로만 사라진다.
    // 네 모서리가 모두 (몸통 영역 안) 또는 (바 위쪽 plusRiseLimit 이내)가 아니면 그리지 않는다.
    private void ApplyPlusVisual(int _index)
    {
        PlusState _p = pluses[_index];
        Image _image = sparkleImages[_index];
        RectTransform _rect = sparkleRects[_index];

        if (null == _image || null == _rect)
            return;

        float _t = Mathf.Clamp01(_p.age / Mathf.Max(0.01f, _p.life));

        // 끝 무렵에는 켜졌다 꺼졌다 하며 사라진다.
        if (_t >= PlusBlinkStart && 0 != (Mathf.FloorToInt(_p.age / PixelStep) & 1))
        {
            _image.enabled = false;
            return;
        }

        bool _bShape = _t < PlusShapeEnd && null != sparklePlusSprite;
        int _size = true == _bShape ? 3 : 1;
        Sprite _sprite = true == _bShape ? sparklePlusSprite : null;

        // 피벗이 (0, 0)이라 위치가 곧 왼쪽 아래 모서리다. 입자 중심에서 크기의 절반(내림)을 빼 홀수 크기도 모서리가 정수 픽셀에 놓이게 한다.
        int _half = _size / 2;
        float _swayX = Mathf.Sin(_p.age * 8f + _p.swayPhase) * plusSway;
        int _left = Mathf.RoundToInt(_p.x + _swayX) - _half;
        int _bottomLocal = Mathf.RoundToInt(_p.y) - _half;
        // 윤곽 표는 바의 왼쪽 아래가 원점이므로 높이의 절반을 더해 맞춘다.
        int _bottom = _bottomLocal + Mathf.RoundToInt(barH * 0.5f);

        if (false == IsPlusInside(_left, _bottom, _size))
        {
            _image.enabled = false;
            return;
        }

        _image.sprite = _sprite;
        _rect.sizeDelta = new Vector2(_size, _size);
        _rect.anchoredPosition = new Vector2(_left, _bottomLocal);
        _image.color = Color.Lerp(barColorNow, Color.white, plusLighten);
        _image.enabled = true;
    }

    // 실제로 그려지는 칸이 모두 허용 영역 안인지. 3x3 "+"는 가운데와 팔 네 칸(모서리 칸은 투명), 1px 점은 그 한 칸만 본다.
    // 허용 영역: 몸통 영역, 또는 몸통 위쪽 끝 위로 plusRiseLimit 이내이면서 바 가로 폭 안(채워진 구간 안).
    private bool IsPlusInside(int _left, int _bottom, int _size)
    {
        int _right = _left + _size - 1;
        int _targetPx = Mathf.Min(barW, Mathf.FloorToInt(GetBarWidth() * Mathf.Clamp01(Mathf.Max(shownRatio, displayRatio))));

        if (0 > _left || _right >= _targetPx)
            return false;

        if (3 > _size)
            return IsPlusCellOk(_left, _bottom);

        return true == IsPlusCellOk(_left + 1, _bottom)
            && true == IsPlusCellOk(_left, _bottom + 1)
            && true == IsPlusCellOk(_left + 1, _bottom + 1)
            && true == IsPlusCellOk(_left + 2, _bottom + 1)
            && true == IsPlusCellOk(_left + 1, _bottom + 2);
    }

    private bool IsPlusCellOk(int _x, int _y)
    {
        if (true == IsInsideBody(_x, _y))
            return true;

        return _y > bodyTopRow && _y <= bodyTopRow + Mathf.RoundToInt(plusRiseLimit) && 0 <= _x && _x < barW;
    }

    // 한 칸(PixelStep)만큼 "+"를 움직이고 새 "+"를 켠다. 정수 픽셀로 반올림해 계단식으로 움직이는 픽셀 아트의 느낌을 지킨다.
    private void StepPluses()
    {
        if (null == pluses)
            return;

        EnsureSilhouette();

        // 바의 현재 색(경고 펄스 포함)에 동기화한다.
        RefreshBarColor();

        // 차오르는 동안은 앞쪽이 예약된 위치를 지날 때 "+"가 켜진다.
        if (true == bFillingUp && 0 < plannedPlusCount)
            ReleasePlannedPluses(GetBarWidth() * Mathf.Clamp01(shownRatio));

        for (int i = 0; i < pluses.Length; i++)
        {
            PlusState _p = pluses[i];
            if (false == _p.bAlive)
                continue;

            _p.age += PixelStep;

            if (_p.age >= _p.life)
            {
                _p.bAlive = false;
                pluses[i] = _p;

                if (null != sparkleImages[i])
                    sparkleImages[i].enabled = false;

                continue;
            }

            // 바 위쪽 한계에 닿으면 더 올라가지 않고 그 자리에서 깜빡이며 사라진다.
            float _maxY = bodyTopRow + plusRiseLimit - barH * 0.5f;
            _p.y = Mathf.Min(_maxY, _p.y + _p.vy * PixelStep);
            pluses[i] = _p;

            ApplyPlusVisual(i);
        }

        alivePlusCount = CountAlivePluses();
    }

    // 피격/회복 플래시, 흔들림

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
        KillTween(ref shakeTween);

        bGhostTrailing = false;
        ghostHoldRemain = 0f;
        bFillingUp = false;
        bFillEvent = false;
        pendingLossRatio = 0f;
        pendingGainRatio = 0f;
        shownRatio = displayRatio;
        plannedPlusCount = 0;
        pixelStepTimer = 0f;

        if (null != ghostSlider)
            ghostSlider.value = displayRatio;

        if (null != pluses)
        {
            for (int i = 0; i < pluses.Length; i++)
            {
                pluses[i].bAlive = false;

                if (null != sparkleImages[i])
                    sparkleImages[i].enabled = false;
            }
        }

        alivePlusCount = 0;

        bResting = false;
        restPlusTimer = 0f;

        SetFlashValue(0f);
        SetShakeValue(0f);
    }

    // 유니티 이벤트 함수

    // "+"는 트윈 없이 정해진 간격(20fps)으로 한 칸씩만 움직인다.
    // 할 일이 없으면 곧바로 돌아간다.
    private void Update()
    {
        if (0 >= alivePlusCount && false == bFillingUp && false == bResting && 0 >= plannedPlusCount)
        {
            pixelStepTimer = 0f;
            return;
        }

        float _dt = Time.unscaledDeltaTime;

        // 휴식 구역 안에서는 일정 간격마다 채워진 구간에서 "+" 하나가 조용히 떠오른다.
        if (true == bResting && false == bFillingUp)
        {
            restPlusTimer += _dt;

            float _interval = restPlusInterval / Mathf.Max(RestPlusMinFillRatio, Mathf.Clamp01(shownRatio));

            if (restPlusTimer >= _interval)
            {
                restPlusTimer = 0f;

                float _filledPx = GetBarWidth() * Mathf.Clamp01(shownRatio);
                if (8f < _filledPx)
                    SpawnPlusAt(Random.Range(4f, _filledPx - 4f));
            }
        }

        pixelStepTimer += _dt;

        int _steps = 0;
        while (PixelStep <= pixelStepTimer && MaxStepsPerFrame > _steps)
        {
            pixelStepTimer -= PixelStep;
            _steps++;

            StepPluses();
        }

        // 프레임이 크게 끊겼다면 밀린 시간은 버린다.
        if (pixelStepTimer > PixelStep)
            pixelStepTimer = 0f;
    }

    private void OnDestroy()
    {
        KillTween(ref flashTween);
        KillTween(ref shakeTween);
    }
}
