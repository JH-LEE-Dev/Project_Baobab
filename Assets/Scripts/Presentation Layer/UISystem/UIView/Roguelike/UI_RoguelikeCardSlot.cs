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
/// 뒤집힘은 폭을 짝수 정수 픽셀 단계로 줄였다 늘린다(3D 회전 금지 - UI_WorkList 0장). 절반 지점에서 뒷면 → 앞면으로 바뀐다.
/// 뒷면에서는 구매 요청을 보내지 않는다(IsRevealed).
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
    }

    public const float IntroFlipSeconds = 0.5f;
    private const float ShakeStepSeconds = 0.03f;
    private static readonly int[] ShakePattern = { -2, 2, -2, 2, -1, 1, 0 };

    /// <summary>앞면이 된 카드를 눌렀다(구매 요청).</summary>
    public event Action<UI_RoguelikeCardSlot> ClickedEvent;
    /// <summary>뒤집히다 앞면이 드러났다.</summary>
    public event Action<UI_RoguelikeCardSlot> RevealedEvent;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField] private UI_RoguelikeCard card;
    [SerializeField] private UI_RoguelikeSelectRelay relay;
    [SerializeField] private Button button;
    [SerializeField] private CanvasGroup canvasGroup;

    //상태 변수
    private RoguelikeCardViewData data;
    private int offerIndex;
    private EAnim anim;
    private float animTime;
    private float animDuration;
    private Vector2 moveFrom;
    private Vector2 moveTo;
    private bool bRevealed;

    public int OfferIndex => offerIndex;
    public RoguelikeCardViewData Data => data;
    public bool IsRevealed => bRevealed;
    public bool IsAnimating => EAnim.Idle != anim && EAnim.Intro != anim;
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
        card.SetData(_data);

        anim = EAnim.Idle;
        bRevealed = true;
        canvasGroup.alpha = 1f;
        card.SetFaceDown(false);
        card.SetFlipWidth(card.FrameWidth);
        card.SetShakeOffset(0);
        card.SetHovered(false);
    }

    public void Unbind()
    {
        anim = EAnim.Idle;
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

    /// <summary>뒷면으로 있다가 _delay초 뒤 뒤집히며 앞면이 된다.</summary>
    public void PlayIntro(float _delay)
    {
        anim = EAnim.Intro;
        animTime = -_delay;
        bRevealed = false;
        canvasGroup.alpha = 1f;
        card.SetFaceDown(true);
        card.SetFlipWidth(card.FrameWidth);
    }

    /// <summary>뒤집히는 중이면 그 자리에서 앞면·정상 폭으로 마무리한다(다른 연출이 끼어들 때 폭이 줄어든 채 남지 않게).</summary>
    private void CompleteIntro()
    {
        if (EAnim.Intro != anim) return;

        anim = EAnim.Idle;
        card.SetFaceDown(false);
        card.SetFlipWidth(card.FrameWidth);

        if (false == bRevealed)
        {
            bRevealed = true;
            RevealedEvent?.Invoke(this);
        }
    }

    /// <summary>_duration초 동안 흐려지며 사라진다(고르지 않은 카드).</summary>
    public void PlayFade(float _duration)
    {
        CompleteIntro();
        anim = EAnim.Fade;
        animTime = 0f;
        animDuration = Mathf.Max(0.01f, _duration);
    }

    /// <summary>_to(정수 픽셀)로 _duration초 동안 이동한다(고른 카드가 화면 중앙으로).</summary>
    public void PlayMove(Vector2 _to, float _duration)
    {
        CompleteIntro();
        anim = EAnim.Move;
        animTime = 0f;
        animDuration = Mathf.Max(0.01f, _duration);
        moveFrom = GetPosition();
        moveTo = _to;
    }

    /// <summary>구매·리롤이 거절됐을 때 좌우로 정수 픽셀 흔들린다.</summary>
    public void PlayReject()
    {
        CompleteIntro();
        anim = EAnim.Shake;
        animTime = 0f;
    }

    private void OnButtonClicked()
    {
        if (false == bRevealed) return;

        ClickedEvent?.Invoke(this);
    }

    private void UpdateIntro()
    {
        animTime += Time.unscaledDeltaTime;
        if (animTime < 0f) return;

        float t = animTime / IntroFlipSeconds;
        int frameWidth = card.FrameWidth;

        if (1f <= t)
        {
            // 프레임이 크게 튀어 절반 지점을 건너뛰어도 앞면은 반드시 드러낸다.
            CompleteIntro();
            return;
        }

        if (t < 0.5f)
        {
            card.SetFlipWidth(ToEvenWidth(frameWidth * (1f - t * 2f)));
            return;
        }

        if (false == bRevealed)
        {
            bRevealed = true;
            card.SetFaceDown(false);
            RevealedEvent?.Invoke(this);
        }

        card.SetFlipWidth(ToEvenWidth(frameWidth * (t - 0.5f) * 2f));
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
        return Mathf.Max(0, Mathf.RoundToInt(_width * 0.5f) * 2);
    }

    private void Awake()
    {
        if (null != button) button.onClick.AddListener(OnButtonClicked);
    }

    private void Update()
    {
        switch (anim)
        {
            case EAnim.Intro: UpdateIntro(); break;
            case EAnim.Fade: UpdateFade(); break;
            case EAnim.Move: UpdateMove(); break;
            case EAnim.Shake: UpdateShake(); break;
        }
    }

    private void OnDestroy()
    {
        ClickedEvent = null;
        RevealedEvent = null;
        if (null != button) button.onClick.RemoveListener(OnButtonClicked);
    }
}
