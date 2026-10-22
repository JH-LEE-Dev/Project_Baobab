using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 임계치 바(기획서 v2.6 10-1). 필드 나무 수가 임계치 T를 넘었는지를 폭 120 x 몸통 높이 12 막대 하나로 보여 준다.
/// 스태미너 바의 미니 버전이다(같은 캡슐 프레임 · 채움 광택 · 아래 그림자, 12px 높이). 🌲 아이콘(Icon) 옆에 "나무 수/T"를 작은 숫자(UI_RoguelikeIconNumber: 버프 HUD와 같은 8px 글리프)로 그린다. 글자 라벨은 쓰지 않는다(막대 안에서 한글 12pt가 너무 크다).
///
/// 채움 앞쪽 끝의 젤리 머리는 처치 바와 같은 공용 클래스(RoguelikeJellyHead)가 그린다. 머리는 T까지는 호박색, T를 넘은 구간에 걸리면 진한 적색을 입는다.
/// [막대] 채움 = 나무 수 / 스폰 상한. T 위치에는 막대 위아래로 1칸씩 삐져나오는 크림색 눈금이 선다.
///   T까지는 호박색(BaseFill), T를 넘은 구간은 진한 적색(OverFill). 정수 픽셀 폭이고 반투명이 없다. 값이 바뀌면 지수 감속(시정수 0.1초)으로 따라간다.
///   웨이브가 시작될 때는 SnapNext를 불러, 첫 값을 0에서 차오르지 않고 바로 반영한다(초기 나무가 이미 임계치의 절반이라 빈 바에서 시작하지 않는다).
/// [숫자] T를 넘은 상태(유예·지속 피해)면 숫자가 붉게 물든다. 값이 바뀔 때만 다시 합성한다.
/// [유예] 유예 시간 표현은 이 바가 아니라 바로 아래 UI_RoguelikeGraceTimer가 맡는다.
/// [피해] PlayDamaged는 막대가 4틱 동안 번갈아 하얗게 번쩍이고(24Hz) 정수 픽셀 패턴 { -2, 2, -2, 2, -1, 1, 0 }으로 흔들린다.
///   스태미너 바 경고와 붉은 화면은 UIView_HUD.SetRoguelikePressureWarning이 맡는다. 시간은 unscaled라 ESC로 멈춰도 연출이 이어진다.
/// </summary>
public class UI_RoguelikePressureStrip : MonoBehaviour
{
    private const int InnerWidth = 118;
    private const int SpriteWidth = 120;
    private const int SpriteHeight = 14;
    private const float FillTau = 0.1f;
    private const float TicksPerSecond = 24f;
    private const float ShakeStepSeconds = 0.03f;
    private const int FlashTicks = 4;
    private static readonly int[] ShakePattern = { -2, 2, -2, 2, -1, 1, 0 };

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform stripRoot;
    [SerializeField, Tooltip("T까지의 호박색 채움(스태미너 바 채움을 색조만 돌린 스프라이트, Image Filled).")]
    private Image baseFill;
    [SerializeField, Tooltip("T를 넘은 구간의 진한 적색 채움(Image Filled). 호박색 채움 뒤에 깔려 호박색이 끝나는 곳부터 드러난다.")]
    private Image overFill;
    [SerializeField] private RectTransform tick;
    [SerializeField] private Image flashImage;
    [SerializeField] private UI_RoguelikeIconNumber iconNumber;
    [SerializeField] private RawImage headSurface;
    [SerializeField, Tooltip("호박색 채움 스프라이트의 행별 평균색(아래 행부터 SpriteHeight개). T 안쪽에서 머리가 입는 색.")]
    private Color[] amberRowColors = new Color[0];
    [SerializeField, Tooltip("진한 적색 채움 스프라이트의 행별 평균색. T를 넘은 구간에서 머리가 입는 색.")]
    private Color[] redRowColors = new Color[0];
    [SerializeField, Tooltip("채움 스프라이트의 윤곽 안쪽 여부(SpriteWidth x SpriteHeight, 아래 행부터). 머리는 이 안쪽에만 그려진다.")]
    private bool[] insideMask = new bool[0];
    [SerializeField, Tooltip("머리 바깥 1px 테두리 색(바 프레임 색).")]
    private Color borderColor = new Color(0.23f, 0.16f, 0.12f, 1f);
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color warnColor = new Color(1f, 0.69f, 0.65f, 1f);

    //상태 변수
    private float targetBase;
    private float targetOver;
    private float displayBase;
    private float displayOver;
    private int tickX;
    // 마지막으로 그린 채움 끝 열 수(채움 이미지와 젤리 머리가 같은 값을 기준으로 삼는다).
    private int shownTotalPixels;
    private bool bSnapNext;
    private float clock;
    private Vector2 basePosition;
    private bool bBasePositionCaptured;
    private float shakeTime = -1f;
    private int flashStartTick = -1;
    private bool bShownWarn;
    private RoguelikeJellyHead jelly;
    private float previousFront;
    private bool bJellyOverColor;
    private bool bResetJelly;
    // 퇴장 연출(숫자가 사라지는 동안)이 끝난 뒤 오브젝트를 끈다.
    private bool bHiding;
    private float hideClock;

    /// <summary>임계치 값. _spawnCap이 막대의 끝(스폰 상한)이다.</summary>
    public void SetValues(int _treeCount, int _threshold, int _spawnCap, EPressureState _state)
    {
        bool bWarn = EPressureState.Safe != _state;

        if (null != iconNumber)
        {
            iconNumber.SetPair(_treeCount, _threshold);

            if (bWarn != bShownWarn)
            {
                bShownWarn = bWarn;
                iconNumber.SetTint(true == bWarn ? warnColor : normalColor);
            }
        }

        int cap = Mathf.Max(1, _spawnCap);
        float full = Mathf.Clamp01((float)_treeCount / cap) * InnerWidth;
        float thresholdPixels = Mathf.Clamp01((float)_threshold / cap) * InnerWidth;
        tickX = Mathf.RoundToInt(thresholdPixels);

        targetBase = Mathf.Min(full, thresholdPixels);
        targetOver = Mathf.Max(0f, full - thresholdPixels);

        if (true == bSnapNext)
        {
            bSnapNext = false;
            displayBase = targetBase;
            displayOver = targetOver;
            bResetJelly = true;
            ApplyFill();
        }
    }

    /// <summary>다음 SetValues의 값으로 곧바로 선다(웨이브 시작). 빈 바에서 차오르는 모양을 피한다.</summary>
    public void SnapNext()
    {
        bSnapNext = true;
    }

    /// <summary>임계치 피해 1회: 막대가 번갈아 하얗게 번쩍이고 흔들린다.</summary>
    public void PlayDamaged()
    {
        shakeTime = 0f;
        flashStartTick = CurrentTick();
    }

    /// <summary>켜면 숫자가 등장 연출로 다시 나타난다. 끄면 숫자가 퇴장 연출을 재생한 뒤 오브젝트가 꺼진다.</summary>
    public void SetVisible(bool _bVisible)
    {
        if (true == _bVisible)
        {
            bHiding = false;
            gameObject.SetActive(true);
            if (null != iconNumber) iconNumber.Restore();
            return;
        }

        if (false == gameObject.activeInHierarchy || null == iconNumber)
        {
            gameObject.SetActive(false);
            return;
        }

        iconNumber.PlayExit();
        bHiding = true;
        hideClock = 0f;
    }

    /// <summary>_dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 이 메서드로 시간대별 모습을 만들 수 있다.</summary>
    public void Step(float _dt)
    {
        clock += _dt;

        float follow = 1f - Mathf.Exp(-_dt / FillTau);
        displayBase = Mathf.Abs(targetBase - displayBase) < 0.01f ? targetBase : displayBase + (targetBase - displayBase) * follow;
        displayOver = Mathf.Abs(targetOver - displayOver) < 0.01f ? targetOver : displayOver + (targetOver - displayOver) * follow;

        ApplyFill();
        StepJelly(_dt);
        StepPosition(_dt);
        StepFlash();
    }

    // // 내부

    private int CurrentTick()
    {
        return (int)(clock * TicksPerSecond);
    }

    /// <summary>
    /// 젤리 머리를 진행한다. 채움 앞쪽(호박 + 초과)이 스프라이트 폭 기준 어디인지 넘겨 주고, 앞쪽이 T를 넘은 구간이면 적색 행 색을 입힌다.
    /// 값이 한 번에 서는 경우(웨이브 시작)는 속도로 읽히지 않게 이전 위치를 맞춘다.
    /// </summary>
    private void StepJelly(float _dt)
    {
        if (null == jelly)
        {
            if (null == headSurface) return;

            jelly = new RoguelikeJellyHead(headSurface, SpriteWidth, SpriteHeight, insideMask, borderColor);
            jelly.SetRowColors(amberRowColors);
        }

        // 채움 끝 열(shownTotalPixels)이 없으면 머리도 그리지 않는다. 있으면 채움 끝 바로 다음 열에서 시작한다(채움과 머리 사이에 빈 줄이 생기지 않는다).
        float total = displayBase + displayOver;
        float front = 0 < shownTotalPixels ? total + 1f : 0f;

        bool bOver = 1f <= displayOver;
        if (bOver != bJellyOverColor)
        {
            bJellyOverColor = bOver;
            jelly.SetRowColors(true == bOver ? redRowColors : amberRowColors);
        }

        if (true == bResetJelly)
        {
            bResetJelly = false;
            previousFront = front;
            jelly.Reset();
        }

        jelly.Step(_dt, front, previousFront);
        previousFront = front;
    }

    /// <summary>막대 채움을 정수 픽셀로 반영한다. 적색 채움이 뒤에서 (호박 + 초과)까지 깔리고 호박색이 앞에서 덮어, 넘은 구간이 눈금 오른쪽에서 드러난다.</summary>
    private void ApplyFill()
    {
        // 합계를 먼저 내림해 머리 위치(내림)와 같은 규칙으로 맞춘다. 각각 내림한 값을 더하면 소수 부분이 버려져 채움이 머리보다 1열 모자란다.
        int totalPixels = Mathf.Min(InnerWidth, Mathf.FloorToInt(displayBase + displayOver));
        int basePixels = Mathf.Min(Mathf.FloorToInt(displayBase), totalPixels);
        shownTotalPixels = totalPixels;

        SetFill(baseFill, basePixels);
        SetFill(overFill, totalPixels);

        if (null != tick) tick.anchoredPosition = new Vector2(1 + tickX, tick.anchoredPosition.y);
    }

    /// <summary>안쪽 _pixels칸(0~118)을 스프라이트 폭 기준 채움 비율로. 0이면 끈다.</summary>
    private static void SetFill(Image _image, int _pixels)
    {
        if (null == _image) return;

        _image.enabled = 0 < _pixels;
        _image.fillAmount = 0 < _pixels ? (_pixels + 1) / (float)SpriteWidth : 0f;
    }

    private void StepPosition(float _dt)
    {
        if (null == stripRoot) return;

        if (false == bBasePositionCaptured)
        {
            basePosition = stripRoot.anchoredPosition;
            bBasePositionCaptured = true;
        }

        if (shakeTime < 0f) return;

        shakeTime += _dt;
        int step = (int)(shakeTime / ShakeStepSeconds);
        if (step >= ShakePattern.Length)
        {
            shakeTime = -1f;
            RestorePosition();
            return;
        }

        stripRoot.anchoredPosition = new Vector2(basePosition.x + ShakePattern[step], basePosition.y);
    }

    private void RestorePosition()
    {
        if (null != stripRoot && bBasePositionCaptured) stripRoot.anchoredPosition = basePosition;
    }

    private void StepFlash()
    {
        if (null == flashImage) return;

        if (flashStartTick < 0)
        {
            flashImage.enabled = false;
            return;
        }

        int elapsed = CurrentTick() - flashStartTick;
        if (FlashTicks * 2 <= elapsed)
        {
            flashStartTick = -1;
            flashImage.enabled = false;
            return;
        }

        // 켜짐 · 꺼짐 · 켜짐 · 꺼짐 ... 틱마다 번갈아.
        flashImage.enabled = 0 == elapsed % 2;
    }

    private void Update()
    {
        if (true == bHiding)
        {
            hideClock += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            if (UI_RoguelikeIconNumber.ExitSeconds + 0.02f <= hideClock)
            {
                bHiding = false;
                gameObject.SetActive(false);
            }
        }

        Step(Mathf.Min(Time.unscaledDeltaTime, 1f / 20f));
    }

    private void OnDestroy()
    {
        if (null != jelly) jelly.Dispose();
    }

    private void OnDisable()
    {
        bHiding = false;
        shakeTime = -1f;
        flashStartTick = -1;
        RestorePosition();
        if (null != flashImage) flashImage.enabled = false;
    }
}
