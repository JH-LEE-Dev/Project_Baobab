using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 유예 타이머(RoguelikeHUD_Polish_Plan.md). 임계치 바 바로 아래에 붙은 2px 줄이다(바 아래 윤곽을 공유, 안쪽 108 x 2). 초 숫자는 없다.
///   대기      나무 수 &lt; T. 채움 없음(빈 홈).
///   유예 중   보라색 줄이 0에서 가득까지 찬다(경과 / 총 유예 시간).
///   쿨다운    주황색 줄이 가득에서 줄어든다(다음 피해까지 남은 시간). 첫 피해 이후 T 이상인 동안.
///   피해 발생 PlayDamaged: 채움 전체가 2틱 동안 하얗게 번쩍인다(24Hz). 흔들림은 없다(임계치 바에 붙어 있어야 한다).
/// 나무 수가 T 미만으로 내려가면 상태가 Safe로 오고 채움이 즉시 비워진다(기획서 3-2). 채움 끝 위쪽 한 칸이 흰 점이다.
/// 안쪽을 CPU로 그린 RawImage 하나(RoguelikeBarPainter)가 홈 + 채움을 맡고, 윤곽 스프라이트(Background)는 프리팹에 있다. 시간은 unscaled라 ESC로 멈춰도 연출이 이어진다.
/// </summary>
public class UI_RoguelikeGraceTimer : MonoBehaviour
{
    private const int InnerWidth = 108;
    private const int InnerHeight = 2;
    private const int NoTick = -1000000;
    private const int FlashTicks = 2;

    //내부 의존성 (프리팹에서 연결)
    [SerializeField, Tooltip("안쪽 108x2 홈 + 채움을 그리는 RawImage.")]
    private RawImage fillSurface;

    //상태 변수
    private RoguelikeBarPainter painter;
    private float clock;
    private int flashStartTick = NoTick;
    private int shownWidth = -1;
    private EPressureState shownState = EPressureState.Safe;
    private bool bPaintedFlash;
    private bool bPainted;

    /// <summary>상태와 경과 / 총 시간(초). 유예 중은 차오르고, 지속 피해(쿨다운) 중은 줄어든다.</summary>
    public void SetState(EPressureState _state, float _elapsed, float _total)
    {
        float ratio = 0f;

        switch (_state)
        {
            case EPressureState.Grace:
                ratio = 0f < _total ? _elapsed / _total : 1f;
                break;

            case EPressureState.Damaging:
                ratio = 0f < _total ? 1f - _elapsed / _total : 0f;
                break;
        }

        int width = Mathf.RoundToInt(InnerWidth * Mathf.Clamp01(ratio));
        if (width == shownWidth && _state == shownState && true == bPainted) return;

        shownWidth = width;
        shownState = _state;

        Repaint();
    }

    /// <summary>임계치 피해 1회: 채움 전체가 2틱 동안 하얗게 번쩍인다.</summary>
    public void PlayDamaged()
    {
        flashStartTick = CurrentTick();
    }

    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }

    /// <summary>_dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 이 메서드로 시간대별 모습을 만들 수 있다.</summary>
    public void Step(float _dt)
    {
        clock += _dt;

        if (bPaintedFlash != IsFlashing()) Repaint();
    }

    // // 내부

    private int CurrentTick()
    {
        return (int)(clock * RoguelikeBarPainter.TicksPerSecond);
    }

    private bool IsFlashing()
    {
        return CurrentTick() - flashStartTick < FlashTicks;
    }

    private void Repaint()
    {
        if (null == fillSurface) return;
        if (null == painter) painter = new RoguelikeBarPainter(fillSurface, InnerWidth, InnerHeight, "UI_RoguelikeGraceFill");

        bPainted = true;
        bPaintedFlash = IsFlashing();

        painter.Clear();
        painter.Fill(0, 0, InnerWidth, InnerHeight, RoguelikeHudColors.GrooveTop);

        if (EPressureState.Safe != shownState && 0 < shownWidth)
        {
            RoguelikeBarPalette palette = true == bPaintedFlash ? RoguelikeHudColors.Flash : (EPressureState.Damaging == shownState ? RoguelikeHudColors.Cooldown : RoguelikeHudColors.Grace);
            painter.Fill(0, 0, shownWidth, 1, palette.light);
            painter.Fill(0, 1, shownWidth, 2, palette.main);
            painter.SetPixel(shownWidth - 1, 0, RoguelikeHudColors.White);
        }

        painter.Apply();
    }

    private void Awake()
    {
        // 첫 프레임에 텍스처 없는 RawImage(흰 네모)가 보이지 않도록 미리 한 번 그린다.
        Repaint();
    }

    private void Update()
    {
        Step(Mathf.Min(Time.unscaledDeltaTime, 1f / 20f));
    }

    private void OnDisable()
    {
        flashStartTick = NoTick;
        bPainted = false;
    }

    private void OnDestroy()
    {
        if (null != painter) painter.Dispose();
    }
}
