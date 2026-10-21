using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 임계점 바. 현재 웨이브의 "현재 처치 수 / 목표 처치 수"(예: 12/40)를 채움과 숫자로 보여 준다.
/// 스태미너 바(MainBar)와 같은 모양에 폭만 120으로 줄이고(오른쪽의 대쉬 게이지와 겹치지 않게), 채움 색은 밝은 파스텔 적색 한 가지다(상태별 색 구분·눈금 없음). 프로젝트의 픽셀 규칙을 따른다.
///   - 채움 폭은 정수 픽셀(Image Filled)이고 반투명이 없다. 값이 바뀌면 초당 140px로 따라가며 줄어들고 늘어난다.
///   - 섬광은 24Hz 틱으로 끊고, 흔들림은 정수 픽셀 패턴(카드 거절 흔들림과 같은 { -2, 2, -2, 2, -1, 1, 0 })이다. 시간은 unscaled라 ESC로 멈춰도 연출이 이어진다.
///
/// [젤리 움직임] 스태미너 바(HUD_Stemina의 젤리 머리)와 같은 규칙의 가벼운 버전이다.
///   머리   채움 앞쪽 끝에 둥근 머리(Head, 9-slice 8x18)가 얹힌다. 채움이 늘어나는 동안은 앞으로 늘어났다가 멈추면 스프링으로 튕기며 안착하고, 줄어드는 동안은 눌린다.
///          가만히 있을 때는 느리게 호흡한다(1px, 24Hz 틱). 머리 폭은 항상 정수 픽셀이고 채움이 거의 꽉 차면(바 끝에 닿으면) 숨긴다 - 채움 이미지의 둥근 끝이 대신 보인다.
///   고스트 값이 줄어들 때(새 웨이브 시작 등) 직전 값이 옅은 색(Ghost)으로 잠깐 남았다가(0.3초) 따라온다. 늘어날 때는 바로 따라간다.
///
/// [숫자] 바 가운데에 "처치 수/목표 처치 수"(현재/최대)를 그린다. 파스텔 적색·갈색 어느 바탕 위에서도 읽히도록 밝은 크림색에 1px 짙은 갈색 외곽선(네 방향 복사본)이고,
///        섬광 층 위에 있어 번쩍이는 동안에도 읽힌다. 목표를 넘으면 41/40처럼 그대로 보여 준다. 값이 바뀔 때만 갱신한다(zero-alloc SetText).
/// [경고] 임계점 경고는 스태미너 바와 붉은 화면(BloodScreen)이 맡는다(UIView_HUD.SetRoguelikePressureWarning).
/// [피해] PlayDamaged는 바가 4틱 동안 번갈아 하얗게 번쩍이고 좌우로 흔들린다(스태미너 바의 피격 연출은 본편 HUD가 이미 재생한다).
/// </summary>
public class UI_RoguelikeThresholdBar : MonoBehaviour
{
    private const int BarWidth = 120;
    private const float FillPixelsPerSecond = 140f;
    private const float TicksPerSecond = 24f;
    private const float ShakeStepSeconds = 0.03f;
    private const int FlashTicks = 4;
    private static readonly int[] ShakePattern = { -2, 2, -2, 2, -1, 1, 0 };

    // 젤리 머리: 평소 폭 8, 늘어나면 +5, 눌리면 -2, 호흡 +1. 스프링은 약간 출렁이게(감쇠비 0.45) 1/120초 단위로 적분한다.
    private const int HeadRestWidth = 8;
    private const int HeadMinWidth = 5;
    private const float HeadStretch = 5f;
    private const float HeadSquash = -2f;
    private const float HeadOmega = 26f;
    private const float HeadDamping = 0.45f;
    private const float SpringStep = 1f / 120f;
    private const float MovingSpeed = 6f;

    // 고스트: 줄어든 직후 0.3초 머물렀다가 초당 70px로 따라온다.
    private const float GhostHoldSeconds = 0.3f;
    private const float GhostFollowPixelsPerSecond = 70f;
    private const float MaxStepSeconds = 1f / 20f;

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform barRoot;
    [SerializeField] private Image ghostImage;
    [SerializeField] private Image fillImage;
    [SerializeField] private RectTransform headRect;
    [SerializeField] private Image headImage;
    [SerializeField] private Image flashImage;
    [SerializeField, Tooltip("숫자 텍스트. 앞의 넷은 1px 외곽선(네 방향), 마지막이 본문. 모두 같은 문구를 받는다.")]
    private TMP_Text[] valueTexts = new TMP_Text[0];

    //상태 변수
    private float targetRatio;
    private float displayPixels;
    private float ghostPixels;
    private float ghostHold;
    private float headSpring;
    private float headVelocity;
    private float clock;
    private Vector2 basePosition;
    private bool bBasePositionCaptured;
    private float shakeTime = -1f;
    private int flashStartTick = -1;
    private int shownKilled = -1;
    private int shownTarget = -1;

    /// <summary>처치 진행도. 채움 = _killed / _target, 숫자 = "_killed/_target".</summary>
    public void SetProgress(int _killed, int _target)
    {
        ApplyValueText(_killed, _target);
        targetRatio = 0 < _target ? Mathf.Clamp01((float)_killed / _target) : 0f;
    }

    /// <summary>임계치 피해 1회: 바가 번갈아 하얗게 번쩍이고 흔들린다.</summary>
    public void PlayDamaged()
    {
        shakeTime = 0f;
        flashStartTick = CurrentTick();
    }

    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }

    /// <summary>처음 모습으로 되돌린다(런이 시작·종료될 때).</summary>
    public void ResetBar()
    {
        targetRatio = 0f;
        displayPixels = 0f;
        ghostPixels = 0f;
        ghostHold = 0f;
        headSpring = 0f;
        headVelocity = 0f;
        shakeTime = -1f;
        flashStartTick = -1;
        shownKilled = -1;
        shownTarget = -1;
        RestorePosition();
        ApplyVisuals();
    }

    /// <summary>
    /// _dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 실제 시간 없이 이 메서드로 시간대별 모습을 만들 수 있다.
    /// </summary>
    public void Step(float _dt)
    {
        clock += _dt;

        float previous = displayPixels;
        displayPixels = Mathf.MoveTowards(displayPixels, targetRatio * BarWidth, FillPixelsPerSecond * _dt);
        float velocity = 0f < _dt ? (displayPixels - previous) / _dt : 0f;

        StepGhost(previous, _dt);
        StepHead(velocity, _dt);
        ApplyVisuals();
        StepPosition(_dt);
        StepFlash();
    }

    // // 내부

    private int CurrentTick()
    {
        return (int)(clock * TicksPerSecond);
    }

    /// <summary>줄어드는 순간 고스트가 직전 값에 남아 0.3초 머문 뒤 따라온다. 늘어날 때는 채움과 함께 간다.</summary>
    private void StepGhost(float _previous, float _dt)
    {
        if (displayPixels < _previous - 0.001f) ghostHold = GhostHoldSeconds;

        if (ghostPixels <= displayPixels)
        {
            ghostPixels = displayPixels;
            return;
        }

        if (0f < ghostHold)
        {
            ghostHold = Mathf.Max(0f, ghostHold - _dt);
            return;
        }

        ghostPixels = Mathf.MoveTowards(ghostPixels, displayPixels, GhostFollowPixelsPerSecond * _dt);
    }

    /// <summary>
    /// 머리 스프링. 목표는 늘어나는 중 +5, 줄어드는 중 -2, 멈춰 있으면 호흡(느린 24Hz 틱 1px). 멈추는 순간 목표가 0으로 돌아가며 스프링이 출렁이다 안착한다.
    /// </summary>
    private void StepHead(float _velocity, float _dt)
    {
        float target;
        if (MovingSpeed < _velocity) target = HeadStretch;
        else if (_velocity < -MovingSpeed) target = HeadSquash;
        else target = 1 == (CurrentTick() / 12) % 4 || 2 == (CurrentTick() / 12) % 4 ? 1f : 0f;

        float remain = _dt;
        while (0f < remain)
        {
            float step = Mathf.Min(remain, SpringStep);
            float acceleration = HeadOmega * HeadOmega * (target - headSpring) - 2f * HeadDamping * HeadOmega * headVelocity;
            headVelocity += acceleration * step;
            headSpring += headVelocity * step;
            remain -= step;
        }
    }

    /// <summary>"처치 수/목표 처치 수" 숫자를 갱신한다. 값이 그대로면 아무것도 하지 않는다.</summary>
    private void ApplyValueText(int _killed, int _target)
    {
        if (_killed == shownKilled && _target == shownTarget) return;

        shownKilled = _killed;
        shownTarget = _target;

        for (int i = 0; i < valueTexts.Length; i++)
        {
            if (null != valueTexts[i]) valueTexts[i].SetText("{0}/{1}", _killed, _target);
        }
    }

    /// <summary>채움·고스트·머리를 정수 픽셀로 반영한다.</summary>
    private void ApplyVisuals()
    {
        int fillPixels = Mathf.RoundToInt(displayPixels);
        int ghostWholePixels = Mathf.RoundToInt(ghostPixels);

        if (null != fillImage) fillImage.fillAmount = fillPixels / (float)BarWidth;

        if (null != ghostImage)
        {
            ghostImage.enabled = fillPixels < ghostWholePixels;
            ghostImage.fillAmount = ghostWholePixels / (float)BarWidth;
        }

        ApplyHead(fillPixels);
    }

    /// <summary>
    /// 젤리 머리: 채움 끝에서 1px 겹쳐 시작해 폭(정수)만큼 뻗는다. 바 끝(BarWidth)을 넘으면 폭을 줄이고, 최소 폭에도 못 들어가면(채움이 거의 꽉 참) 숨긴다.
    /// </summary>
    private void ApplyHead(int _fillPixels)
    {
        if (null == headRect || null == headImage) return;

        int width = Mathf.Clamp(HeadRestWidth + Mathf.RoundToInt(headSpring), HeadMinWidth, HeadRestWidth + (int)HeadStretch);
        int left = _fillPixels - 1;
        int room = BarWidth - left;
        if (room < width) width = room;

        bool bShow = 1 <= _fillPixels && HeadMinWidth <= width && _fillPixels < BarWidth - 1;
        headImage.enabled = bShow;
        if (false == bShow) return;

        headRect.sizeDelta = new Vector2(width, headRect.sizeDelta.y);
        headRect.anchoredPosition = new Vector2(left, headRect.anchoredPosition.y);
    }

    private void StepPosition(float _dt)
    {
        if (null == barRoot) return;

        if (false == bBasePositionCaptured)
        {
            basePosition = barRoot.anchoredPosition;
            bBasePositionCaptured = true;
        }

        if (shakeTime < 0f)
        {
            return;
        }

        shakeTime += _dt;
        int step = (int)(shakeTime / ShakeStepSeconds);
        if (step >= ShakePattern.Length)
        {
            shakeTime = -1f;
            RestorePosition();
            return;
        }

        barRoot.anchoredPosition = new Vector2(basePosition.x + ShakePattern[step], basePosition.y);
    }

    private void RestorePosition()
    {
        if (null != barRoot && bBasePositionCaptured) barRoot.anchoredPosition = basePosition;
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
        Step(Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds));
    }

    private void OnDisable()
    {
        shakeTime = -1f;
        flashStartTick = -1;
        RestorePosition();
        if (null != flashImage) flashImage.enabled = false;
    }
}
