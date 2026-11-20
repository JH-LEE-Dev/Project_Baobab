using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 카드 선택 화면의 카드 한 칸. 카드 그림(UI_RoguelikeCard) + 마우스·패드 판정(HitBox)을 묶는다(v2.4 - 가격 태그 없음).
///
/// 연출은 이 칸이 스스로 돌린다(등장 뒤집힘 · 흐려짐 · 중앙으로 이동 · 거절 흔들림). 전부 unscaled 시간이라
/// 보관함·ESC 메뉴가 timeScale을 0으로 만들어도 멈추지 않는다. 트윈 라이브러리를 쓰지 않아 호출당 힙 할당이 없다.
/// 한 번에 연출 하나만 돈다(새 연출이 시작되면 이전 연출은 그 자리에서 끝난 것으로 본다).
///
/// [뒤집힘] 폭을 짝수 정수 픽셀 단계로 줄였다 늘린다(3D 회전 금지 - 기획서 10장 픽셀 UI 규칙). 한 장 0.5초:
///   접힘(앞 40%)  폭이 점점 빨라지며 가장 얇은 폭(EdgeWidth)까지 줄고, 그동안 카드가 HopHeight만큼 뜬다.
///   옆면(7%)      가장 얇은 폭으로 두 프레임쯤 머문다(60fps에서 프레임이 이 순간을 건너뛰지 않게). 이때 뒷면 → 앞면으로 바뀌고(앞면이 드러남 = IsRevealed),
///                 카드 두께(옆면)가 앞면 테두리 색으로 보이고 프레임이 약하게 번쩍이며, 희귀 카드는 대각 광택 띠가 지나가며 레어로 바뀐다.
///   펼침(53%)     폭이 목표를 살짝 지나쳤다(최대 +4px) 돌아오고, 카드가 내려와 착지한다.
///   착지          0.08초 동안 1px 눌렸다 돌아오고 먼지·지면 링·금빛 반짝이 퍼진다(PlayLandingDust). 뒤집힘이 끝나면 LandedEvent.
/// 뒷면에서는 구매 요청을 보내지 않는다(IsRevealed).
///
/// [희귀 등장 연출] 희귀 카드(에메랄드 · 프리즘 · 루비)는 PlayIntro(_delay, true)로 시작하면 rareFx(UI_RoguelikeRareRevealFx)가 함께 돈다.
///   두근 0.45초(뒷면이 박자마다 1px 떨림) → 뒤집힘(뜀 10px) → 공개 순간 흰 실루엣(flash · 와이프 없음) → 팡 · 타다다닥 · 유지(후광 · 빛살 · 입자는 rareFx가 슬롯 밖 층에 그린다).
///   슬롯은 매 프레임 자기 위치 · 알파 · 카드 모양을 rareFx에 넘기고, rareFx가 정한 실루엣 색을 카드에 덮는다. rareFx가 비어 있으면 일반 카드와 똑같이 동작한다.
///   두근 시작 ~ 유지 시작 사이에는 RareSpotlightEvent(true)로 화면이 다른 카드를 어둡게 한다.
/// </summary>
public class UI_RoguelikeCardSlot : MonoBehaviour
{
    private enum EAnim
    {
        Idle,
        Intro,
        Fade,
        Move,
        Shake,
        Landing,
    }

    public const float IntroFlipSeconds = 0.5f;
    private const float FoldRatio = 0.4f;
    private const float EdgeHoldRatio = 0.07f;
    private const int EdgeWidth = 4;
    private const int HopHeight = 5;
    private const int RareHopHeight = 10;
    private const float RareBeatSeconds = 0.1f;   // 박자마다 3/30초 동안 떨림
    private static readonly int[] RareBeatShake = { 1, -1, 1 };
    private const int LandingDipPixels = 1;
    private const float LandingSeconds = 0.08f;
    private const float RevealFlashAlpha = 0.35f;
    private const float ShakeStepSeconds = 0.03f;
    private static readonly int[] ShakePattern = { -2, 2, -2, 2, -1, 1, 0 };

    /// <summary>앞면이 된 카드를 눌렀다(구매 요청).</summary>
    public event Action<UI_RoguelikeCardSlot> ClickedEvent;
    /// <summary>뒤집히다 앞면이 드러났다.</summary>
    public event Action<UI_RoguelikeCardSlot> RevealedEvent;
    /// <summary>뒤집힘이 끝났다(커서 박스는 이때 나타난다). 뒤집힘이 다른 연출에 끊겨도 마무리되며 한 번 온다.</summary>
    public event Action<UI_RoguelikeCardSlot> LandedEvent;
    /// <summary>희귀 카드의 두근이 시작됐다(true) / 유지가 시작되었거나 연출이 중단됐다(false). 화면이 다른 카드를 어둡게 하는 구간이다.</summary>
    public event Action<UI_RoguelikeCardSlot, bool> RareSpotlightEvent;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private UI_RoguelikeCard card;
    [SerializeField] private UI_RoguelikeSelectRelay relay;
    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup canvasGroup;
    [Tooltip("희귀 등장 연출(뒤 층 RareFxBack_n에 붙은 컴포넌트). 비어 있으면 희귀 카드도 일반 카드처럼 뒤집힌다.")]
    [SerializeField] private UI_RoguelikeRareRevealFx rareFx;

    //상태 변수
    private RoguelikeCardViewData data;
    private int offerIndex;
    private EAnim anim;
    private float animTime;
    private float animDuration;
    private Vector2 moveFrom;
    private Vector2 moveTo;
    private bool bRevealed;
    private bool bRareIntro;
    private bool bRareFxActive;
    private bool bRareSpotlight;
    private int rareShake;
    private Color32 lastSilhouette;

    public int OfferIndex => offerIndex;
    public RoguelikeCardViewData Data => data;
    public bool IsRevealed => bRevealed;
    public bool IsIntroPlaying => EAnim.Intro == anim;
    public UI_RoguelikeSelectRelay Relay => relay;
    public Button SelectButton => button;
    public UI_RoguelikeCard Card => card;

    /// <summary>이 칸에 카드를 채우고 켠다. 앞면·원래 위치·불투명 상태로 시작한다.</summary>
    public void Bind(int _offerIndex, in RoguelikeCardViewData _data)
    {
        offerIndex = _offerIndex;
        data = _data;

        gameObject.SetActive(true);
        StopRareFx();
        card.SetData(_data);

        anim = EAnim.Idle;
        bRevealed = true;
        canvasGroup.alpha = 1f;
        card.SetFaceDown(false);
        card.SetFlipWidth(card.FrameWidth);
        card.SetShakeOffset(0);
        card.ResetMotion();
    }

    public void Unbind()
    {
        anim = EAnim.Idle;
        StopRareFx();
        gameObject.SetActive(false);
    }

    /// <summary>화면 안 위치(정수 픽셀). 칸의 앵커·피벗은 프리팹에서 가운데 앵커, 왼쪽 아래 피벗이다.</summary>
    public void SetPosition(Vector2 _position)
    {
        ((RectTransform)transform).anchoredPosition = _position;
    }

    public Vector2 GetPosition()
    {
        return ((RectTransform)transform).anchoredPosition;
    }

    public void SetFocused(bool _bFocused)
    {
        card.SetHovered(_bFocused);
    }

    /// <summary>다른 카드가 선택 중일 때 어둡게(밝기 대비).</summary>
    public void SetDimmed(bool _bDimmed)
    {
        card.SetDimmed(_bDimmed);
    }

    /// <summary>선택하는 순간 프레임이 짧게 번쩍인다.</summary>
    public void PlayFlash()
    {
        card.PlayFlash();
    }

    /// <summary>뒷면으로 있다가 _delay초 뒤 뒤집히며 앞면이 된다.</summary>
    public void PlayIntro(float _delay)
    {
        PlayIntro(_delay, false);
    }

    /// <summary>
    /// _bRare가 true이고 rareFx가 연결된 희귀 카드면 _delay초 뒤 두근(0.45초) → 뒤집힘(뜀 10px) → 팡 · 타다다닥 · 유지 연출로 앞면이 된다.
    /// 아니면 일반 카드와 똑같이 뒤집힌다.
    /// </summary>
    public void PlayIntro(float _delay, bool _bRare)
    {
        StopRareFx();

        anim = EAnim.Intro;
        animTime = -_delay;
        bRevealed = false;
        canvasGroup.alpha = 1f;
        card.SetFaceDown(true);
        card.SetFlipWidth(card.FrameWidth);

        bRareIntro = true == _bRare && null != rareFx && 0 <= card.GradeIndex;
        if (true == bRareIntro)
        {
            rareFx.Play(card.GradeIndex, _delay);
            bRareFxActive = true;
        }
    }

    /// <summary>뒤집히는 중이면 그 자리에서 앞면·정상 폭으로 마무리한다(다른 연출이 끼어들 때 폭이 줄어든 채 남지 않게).</summary>
    private void CompleteIntro()
    {
        if (EAnim.Intro != anim) return;

        anim = EAnim.Idle;
        card.SetFaceDown(false);
        card.SetFlipWidth(card.FrameWidth);
        card.SetFlipLift(0);

        if (false == bRevealed)
        {
            bRevealed = true;
            RevealedEvent?.Invoke(this);
        }

        LandedEvent?.Invoke(this);
    }

    /// <summary>다른 연출(흐려짐·이동·흔들림)이 끼어든다. 뒤집히는 중이면 앞면으로 마무리하고, 착지 중이면 눌림을 푼다.</summary>
    private void InterruptFlip()
    {
        // 희귀 연출이 아직 공개 순간에 닿지 않았다면(지연 · 두근 중) 중단한다. 공개 뒤라면 후광 · 빛살은 그대로 따라간다.
        if (EAnim.Intro == anim && true == bRareIntro && false == bRevealed) StopRareFx();

        CompleteIntro();
        card.SetFlipLift(0);
    }

    /// <summary>_duration초 동안 흐려지며 사라진다(고르지 않은 카드).</summary>
    public void PlayFade(float _duration)
    {
        InterruptFlip();
        anim = EAnim.Fade;
        animTime = 0f;
        animDuration = Mathf.Max(0.01f, _duration);
    }

    /// <summary>_to(정수 픽셀)로 _duration초 동안 이동한다(고른 카드가 화면 중앙으로).</summary>
    public void PlayMove(Vector2 _to, float _duration)
    {
        InterruptFlip();
        anim = EAnim.Move;
        animTime = 0f;
        animDuration = Mathf.Max(0.01f, _duration);
        moveFrom = GetPosition();
        moveTo = _to;
    }

    /// <summary>구매·리롤이 거절됐을 때 좌우로 정수 픽셀 흔들린다.</summary>
    public void PlayReject()
    {
        InterruptFlip();
        anim = EAnim.Shake;
        animTime = 0f;
    }

    /// <summary>마우스를 누르는 동안 카드가 눌린다. 뒷면에서는 반응하지 않는다.</summary>
    private void OnPressedChanged(bool _bPressed)
    {
        card.SetPressed(_bPressed && bRevealed);
    }

    /// <summary>패드·키보드 제출 순간 짧게 눌린다.</summary>
    private void OnSubmitted()
    {
        if (bRevealed) card.PlayPressPulse();
    }

    private void OnButtonClicked()
    {
        if (false == bRevealed) return;

        ClickedEvent?.Invoke(this);
    }

    private void UpdateIntro()
    {
        animTime += Time.unscaledDeltaTime;
        EvaluateIntro(animTime);
    }

    /// <summary>뒤집힘의 _time초 시점 모습을 그린다(시간에서 바로 계산하므로 프레임이 튀어도 곡선이 어긋나지 않는다).</summary>
    private void EvaluateIntro(float _time)
    {
        if (_time < 0f) return;

        float flipTime = _time;
        int hopHeight = HopHeight;
        if (true == bRareIntro)
        {
            // 희귀 카드: 두근 0.45초 뒤에 뒤집힘이 시작되고 뜀이 두 배다.
            flipTime = _time - UI_RoguelikeRareRevealFx.TeaseSeconds;
            hopHeight = RareHopHeight;
            if (flipTime < 0f)
            {
                EvaluateRareTease(_time);
                return;
            }

            ApplyRareShake(0);
        }

        float t = flipTime / IntroFlipSeconds;
        int frameWidth = card.FrameWidth;

        if (1f <= t)
        {
            // 프레임이 크게 튀어 중간을 건너뛰어도 앞면은 반드시 드러내고 착지한다.
            CompleteIntro();
            StartLanding();
            return;
        }

        if (t < FoldRatio)
        {
            // 접힘: 점점 빨라지며(ease-in) 가장 얇은 폭까지, 뜀은 빠르게 올라갔다 정점에서 느려진다(ease-out).
            float u = t / FoldRatio;
            card.SetFlipWidth(ToEvenWidth(Mathf.Lerp(frameWidth, EdgeWidth, u * u)));
            card.SetFlipLift(Mathf.RoundToInt(hopHeight * (1f - (1f - u) * (1f - u))));
            return;
        }

        if (false == bRevealed) RevealAtEdge();

        if (t < FoldRatio + EdgeHoldRatio)
        {
            // 옆면: 가장 얇은 폭·가장 높은 곳에서 잠깐 멈춘다.
            card.SetFlipWidth(EdgeWidth);
            card.SetFlipLift(hopHeight);
            return;
        }

        // 펼침: 목표를 살짝 지나쳤다 돌아오고(ease-out-back), 카드는 점점 빨라지며 내려온다(ease-in).
        float v = (t - FoldRatio - EdgeHoldRatio) / (1f - FoldRatio - EdgeHoldRatio);
        card.SetFlipWidth(Mathf.Min(frameWidth + 4, ToEvenWidth(Mathf.LerpUnclamped(EdgeWidth, frameWidth, EaseOutBack(v)))));
        card.SetFlipLift(Mathf.RoundToInt(hopHeight * (1f - v * v)));
    }

    /// <summary>희귀 두근: 박자(0 · 0.2 · 0.34초)마다 3/30초 동안 x 오프셋이 +1, -1, +1px로 떨린다.</summary>
    private void EvaluateRareTease(float _time)
    {
        int shake = 0;
        for (int i = 0; i < UI_RoguelikeRareRevealFx.BeatTimes.Length; i++)
        {
            float d = _time - UI_RoguelikeRareRevealFx.BeatTimes[i];
            if (0f <= d && d < RareBeatSeconds) shake = RareBeatShake[Mathf.Min(RareBeatShake.Length - 1, (int)(d * 30f))];
        }

        // [효과음 지점] 희귀 두근 박자 (0 / 0.2 / 0.34초, 점점 빨라짐)
        ApplyRareShake(shake);
    }

    private void ApplyRareShake(int _shake)
    {
        if (_shake == rareShake) return;

        rareShake = _shake;
        card.SetShakeOffset(_shake);
    }

    /// <summary>가장 얇은 순간: 앞면으로 바뀌고 프레임이 약하게 번쩍인다.</summary>
    private void RevealAtEdge()
    {
        bRevealed = true;
        card.SetFaceDown(false);
        if (true == bRareIntro)
        {
            // 희귀 카드: 반짝 대신 rareFx의 흰 실루엣이 덮고, 프레임은 와이프 없이 곧바로 레어가 된다.
            card.PlayGradeReveal(true);
        }
        else
        {
            card.PlayFlash(RevealFlashAlpha);
            card.PlayGradeReveal();
        }

        RevealedEvent?.Invoke(this);
        // [효과음 지점] 희귀 팡(공개 순간)은 기존 AbilityAppear를 그대로 쓴다. 두근 박자 · 타다다닥 틱 · 만남 차임은 UI_RoguelikeRareRevealFx 주석 지점.
        Sound.PlayUI(SoundID.AbilityAppear);
    }

    /// <summary>희귀 연출을 즉시 멈추고 층을 숨긴다. 카드 위 실루엣도 걷고, 두근 구간이었다면 스포트라이트 해제를 알린다.</summary>
    private void StopRareFx()
    {
        bRareFxActive = false;
        rareShake = 0;
        if (null == rareFx) return;

        rareFx.Stop();
        lastSilhouette = default;
        card.SetSilhouette(default, null);

        if (true == bRareSpotlight)
        {
            bRareSpotlight = false;
            RareSpotlightEvent?.Invoke(this, false);
        }
    }

    /// <summary>매 프레임: 슬롯 위치 · 알파 · 카드 모양을 rareFx에 넘기고, rareFx가 정한 실루엣을 카드에 덮는다.</summary>
    private void UpdateRareFx()
    {
        rareFx.Tick(Time.unscaledDeltaTime, GetPosition(), canvasGroup.alpha, card.FlipWidthPixels, card.FlipLiftPixels, card.VisualOffset, bRevealed);

        Color32 silhouette = rareFx.SilhouetteColor;
        if (silhouette.r != lastSilhouette.r || silhouette.g != lastSilhouette.g || silhouette.b != lastSilhouette.b || silhouette.a != lastSilhouette.a)
        {
            lastSilhouette = silhouette;
            card.SetSilhouette(silhouette, rareFx.SilhouetteMaterial);
        }

        bool bSpotlight = rareFx.IsSpotlight;
        if (bSpotlight != bRareSpotlight)
        {
            bRareSpotlight = bSpotlight;
            RareSpotlightEvent?.Invoke(this, bSpotlight);
        }
    }

    private void StartLanding()
    {
        anim = EAnim.Landing;
        animTime = 0f;
        card.SetFlipLift(-LandingDipPixels);
        card.PlayLandingDust();
        // [효과음 지점] 카드 착지
    }

    private void UpdateLanding()
    {
        animTime += Time.unscaledDeltaTime;
        EvaluateLanding(animTime);
    }

    /// <summary>착지 _time초 시점: 눌림은 LandingSeconds 동안 유지되고 풀린다.</summary>
    private void EvaluateLanding(float _time)
    {
        if (_time < LandingSeconds) return;

        card.SetFlipLift(0);
        anim = EAnim.Idle;
    }

    private static float EaseOutBack(float _t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = _t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private void UpdateFade()
    {
        animTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(animTime / animDuration);
        canvasGroup.alpha = 1f - t;
        if (1f <= t) anim = EAnim.Idle;
    }

    private void UpdateMove()
    {
        animTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(animTime / animDuration);
        float eased = 1f - (1f - t) * (1f - t);
        Vector2 position = Vector2.LerpUnclamped(moveFrom, moveTo, eased);
        SetPosition(new Vector2(Mathf.Round(position.x), Mathf.Round(position.y)));
        if (1f <= t) anim = EAnim.Idle;
    }

    private void UpdateShake()
    {
        animTime += Time.unscaledDeltaTime;
        int step = (int)(animTime / ShakeStepSeconds);

        if (step >= ShakePattern.Length)
        {
            card.SetShakeOffset(0);
            anim = EAnim.Idle;
            return;
        }

        card.SetShakeOffset(ShakePattern[step]);
    }

    /// <summary>폭을 짝수 정수 픽셀로 맞춘다. 가운데 기준 폭이 홀수면 가장자리가 반 픽셀에 걸려 흐려진다.</summary>
    private static int ToEvenWidth(float _width)
    {
        return Mathf.Max(EdgeWidth, Mathf.RoundToInt(_width * 0.5f) * 2);
    }

    private void Awake()
    {
        if (null != button) button.onClick.AddListener(OnButtonClicked);

        relay.PressedEvent += OnPressedChanged;
        relay.SubmittedEvent += OnSubmitted;
    }

    private void Update()
    {
        switch (anim)
        {
            case EAnim.Intro: UpdateIntro(); break;
            case EAnim.Fade: UpdateFade(); break;
            case EAnim.Move: UpdateMove(); break;
            case EAnim.Shake: UpdateShake(); break;
            case EAnim.Landing: UpdateLanding(); break;
        }

        if (true == bRareFxActive) UpdateRareFx();
    }

    private void OnDestroy()
    {
        ClickedEvent = null;
        RevealedEvent = null;
        LandedEvent = null;
        RareSpotlightEvent = null;
        if (null != button) button.onClick.RemoveListener(OnButtonClicked);
        if (null != relay)
        {
            relay.PressedEvent -= OnPressedChanged;
            relay.SubmittedEvent -= OnSubmitted;
        }
    }
}
