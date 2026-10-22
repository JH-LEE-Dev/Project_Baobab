using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 임계치 표시(기획서 v2.5 10-1). 필드 나무 수가 임계치 T를 넘었는지를 얇은 띠와 숫자로 보여 준다. 목표치 바(처치 수) 바로 위에 놓인다.
///
/// [띠] 폭 120 x 높이 4(안쪽 118 x 2). 채움 = 나무 수 / 스폰 상한(T + 20). T 위치에는 6px 높이의 크림색 눈금이 선다.
///   T까지는 파스텔 적색(Base), T를 넘은 구간은 진한 적색(Over). 모두 정수 픽셀 폭이고 반투명이 없다. 값이 바뀌면 지수 감속(시정수 0.1초)으로 따라간다.
///   Grace     진한 구간이 남은 유예만큼 오른쪽부터 줄어든다(채움 = 넘은 길이 x (1 - 유예 진행도)).
///   Damaging  진한 구간이 다음 피해까지의 진행도(쿨다운 진행도)만큼 차오른다.
/// [숫자] 띠 오른쪽 위에 나무 아이콘 + "나무 수/T". T를 넘은 상태(Grace, Damaging)면 숫자와 아이콘이 경고색이다. 값이 바뀔 때만 갱신한다(zero-alloc SetText).
///   글자는 밝은 크림색 본문 1장 + 1px 짙은 갈색 외곽선 4장(네 방향 복사본)이다.
/// [피해] PlayDamaged는 띠가 4틱 동안 번갈아 하얗게 번쩍이고(24Hz), 띠와 숫자 줄이 함께 정수 픽셀 패턴 { -2, 2, -2, 2, -1, 1, 0 }으로 흔들린다.
///   스태미너 바 경고와 붉은 화면은 UIView_HUD.SetRoguelikePressureWarning이 맡는다. 시간은 unscaled라 ESC로 멈춰도 연출이 이어진다.
/// </summary>
public class UI_RoguelikePressureStrip : MonoBehaviour
{
    private const int InnerWidth = 118;
    private const float FillTau = 0.1f;
    private const float TicksPerSecond = 24f;
    private const float ShakeStepSeconds = 0.03f;
    private const int FlashTicks = 4;
    private const float IconGap = 2f;
    private static readonly int[] ShakePattern = { -2, 2, -2, 2, -1, 1, 0 };

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform stripRoot;
    [SerializeField] private RectTransform baseFill;
    [SerializeField] private RectTransform overFill;
    [SerializeField] private RectTransform tick;
    [SerializeField] private Image flashImage;
    [SerializeField] private RectTransform iconRect;
    [SerializeField] private Image iconImage;
    [SerializeField, Tooltip("숫자 텍스트. 앞의 넷은 1px 외곽선(네 방향), 마지막이 본문. 모두 같은 문구를 받는다.")]
    private TMP_Text[] valueTexts = new TMP_Text[0];
    [SerializeField] private Color normalColor = new Color(1f, 0.945f, 0.79f, 1f);
    [SerializeField] private Color warnColor = new Color(1f, 0.435f, 0.435f, 1f);

    //상태 변수
    private float targetBase;
    private float targetOver;
    private float displayBase;
    private float displayOver;
    private int tickX;
    private float clock;
    private Vector2 basePosition;
    private bool bBasePositionCaptured;
    private float shakeTime = -1f;
    private int flashStartTick = -1;
    private int shownTreeCount = -1;
    private int shownThreshold = -1;
    private bool bShownWarn;

    /// <summary>
    /// 임계치 값. _spawnCap이 띠의 끝(T + 20)이다. _graceRatio는 유예 진행도(0~1), _cooldownRatio는 지속 피해 중 다음 피해까지의 진행도(0~1).
    /// </summary>
    public void SetValues(int _treeCount, int _threshold, int _spawnCap, EPressureState _state, float _graceRatio, float _cooldownRatio)
    {
        ApplyValueText(_treeCount, _threshold, EPressureState.Safe != _state);

        int cap = Mathf.Max(1, _spawnCap);
        float full = Mathf.Clamp01((float)_treeCount / cap) * InnerWidth;
        float thresholdPixels = Mathf.Clamp01((float)_threshold / cap) * InnerWidth;
        tickX = Mathf.RoundToInt(thresholdPixels);

        float basePixels = Mathf.Min(full, thresholdPixels);
        float overPixels = Mathf.Max(0f, full - thresholdPixels);

        switch (_state)
        {
            case EPressureState.Grace:
                overPixels *= 1f - Mathf.Clamp01(_graceRatio);
                break;

            case EPressureState.Damaging:
                overPixels *= Mathf.Clamp01(_cooldownRatio);
                break;
        }

        targetBase = basePixels;
        targetOver = overPixels;
    }

    /// <summary>임계치 피해 1회: 띠가 번갈아 하얗게 번쩍이고 띠와 숫자 줄이 함께 흔들린다.</summary>
    public void PlayDamaged()
    {
        shakeTime = 0f;
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

        float follow = 1f - Mathf.Exp(-_dt / FillTau);
        displayBase = Mathf.Abs(targetBase - displayBase) < 0.01f ? targetBase : displayBase + (targetBase - displayBase) * follow;
        displayOver = Mathf.Abs(targetOver - displayOver) < 0.01f ? targetOver : displayOver + (targetOver - displayOver) * follow;

        ApplyFill();
        StepPosition(_dt);
        StepFlash();
    }

    // // 내부

    private int CurrentTick()
    {
        return (int)(clock * TicksPerSecond);
    }

    /// <summary>띠 채움을 정수 픽셀로 반영한다. 넘은 구간은 눈금 오른쪽에서 시작한다.</summary>
    private void ApplyFill()
    {
        int basePixels = Mathf.Min(InnerWidth, Mathf.FloorToInt(displayBase));
        int overPixels = Mathf.Min(InnerWidth - tickX, Mathf.FloorToInt(displayOver));

        SetSegment(baseFill, 1, basePixels);
        SetSegment(overFill, 1 + tickX, overPixels);

        if (null != tick) tick.anchoredPosition = new Vector2(1 + tickX, tick.anchoredPosition.y);
    }

    private static void SetSegment(RectTransform _rect, int _left, int _width)
    {
        if (null == _rect) return;

        _rect.gameObject.SetActive(0 < _width);
        _rect.anchoredPosition = new Vector2(_left, _rect.anchoredPosition.y);
        _rect.sizeDelta = new Vector2(_width, _rect.sizeDelta.y);
    }

    /// <summary>"나무 수/T" 숫자와 경고색을 갱신한다. 값이 그대로면 아무것도 하지 않는다.</summary>
    private void ApplyValueText(int _treeCount, int _threshold, bool _bWarn)
    {
        bool bTextChanged = _treeCount != shownTreeCount || _threshold != shownThreshold;
        if (false == bTextChanged && _bWarn == bShownWarn) return;

        shownTreeCount = _treeCount;
        shownThreshold = _threshold;
        bShownWarn = _bWarn;

        for (int i = 0; i < valueTexts.Length; i++)
        {
            if (null == valueTexts[i]) continue;

            if (true == bTextChanged) valueTexts[i].SetText("{0}/{1}", _treeCount, _threshold);
        }

        if (0 < valueTexts.Length && null != valueTexts[valueTexts.Length - 1]) valueTexts[valueTexts.Length - 1].color = true == _bWarn ? warnColor : normalColor;
        if (null != iconImage) iconImage.color = true == _bWarn ? warnColor : normalColor;

        if (true == bTextChanged) PlaceIcon();
    }

    /// <summary>아이콘을 숫자 왼쪽에 붙인다(숫자는 오른쪽 정렬이라 폭이 바뀌면 아이콘이 따라간다).</summary>
    private void PlaceIcon()
    {
        if (null == iconRect || 0 == valueTexts.Length || null == valueTexts[valueTexts.Length - 1]) return;

        TMP_Text main = valueTexts[valueTexts.Length - 1];
        main.ForceMeshUpdate();

        float right = ((RectTransform)main.transform.parent).rect.width;
        float left = Mathf.Floor(right - main.preferredWidth - iconRect.rect.width - IconGap);
        iconRect.anchoredPosition = new Vector2(left, iconRect.anchoredPosition.y);
    }

    private void StepPosition(float _dt)
    {
        if (null == stripRoot) return;

        if (false == bBasePositionCaptured)
        {
            basePosition = stripRoot.anchoredPosition;
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
        Step(Mathf.Min(Time.unscaledDeltaTime, 1f / 20f));
    }

    private void OnDisable()
    {
        shakeTime = -1f;
        flashStartTick = -1;
        RestorePosition();
        if (null != flashImage) flashImage.enabled = false;
    }
}
