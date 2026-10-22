using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 목표치 바. 현재 웨이브의 "현재 처치 수 / 목표 처치 수"(예: 12/40)를 채움과 숫자로 보여 준다.
/// 스태미너 바의 미니 버전이다: 같은 캡슐 프레임 · 채움 광택 · 아래 그림자, 폭 120 x 높이 14(몸통 12 + 그림자 2). 스태미너 바 바로 위에 놓이고 채움은 분홍적색이다(임계치 바의 호박색과 구분).
/// 막대 왼쪽에 도끼 아이콘(Icon)이 붙고, 숫자는 작은 숫자(UI_RoguelikeIconNumber: 버프 HUD와 같은 8px 글리프)로 "현재/목표"를 그린다. 글자 라벨은 쓰지 않는다.
///   - 채움 폭은 정수 픽셀(Image Filled)이고 반투명이 없다. 값이 바뀌면 지수 감속(시정수 0.12초)으로 따라간다.
///   - 목표를 채우기 전에는 오른쪽 14px(머리 포함)를 항상 비워 둔다(비율 x 106px). 목표를 채웠을 때만 꽉 찬다.
///   - 시간은 unscaled라 ESC로 멈춰도 연출이 이어진다.
/// 채움 앞쪽 끝의 젤리 머리는 임계치 바와 같은 공용 클래스(RoguelikeJellyHead)가 그린다.
/// [숫자] 목표를 넘으면 41/40처럼 그대로 보여 준다(최대 999). 값이 바뀔 때만 다시 합성하고, 등장 · 변경 · 퇴장 연출은 숫자 컴포넌트가 맡는다.
/// [경고] 임계점 경고는 스태미너 바와 붉은 화면(BloodScreen)이 맡는다(UIView_HUD.SetRoguelikePressureWarning).
/// [피해] 임계치 피해의 번쩍임과 흔들림은 임계치 바(UI_RoguelikePressureStrip)와 유예 타이머가 맡는다.
/// </summary>
public class UI_RoguelikeThresholdBar : MonoBehaviour
{
    private const int BarWidth = 120;
    private const int BarHeight = 14;

    // 채움: 목표 미달성 구간은 오른쪽 EndReserve만큼 비워 둔다(머리가 바 끝을 덮어 가득 찬 것처럼 보이지 않게). 목표를 채우면 BarWidth까지.
    private const int EndReserve = 14;
    private const float FillTau = 0.12f;
    private const float MaxStepSeconds = 1f / 20f;

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private Image fillImage;
    [SerializeField] private RawImage headSurface;
    [SerializeField] private UI_RoguelikeIconNumber iconNumber;
    [SerializeField, Tooltip("채움 스프라이트의 행별 평균색(아래 행부터 BarHeight개). 머리가 바 본체의 명암 행에 맞춰 색을 입는 데 쓴다.")]
    private Color[] rowColors = new Color[0];
    [SerializeField, Tooltip("채움 스프라이트의 윤곽 안쪽 여부(BarWidth x BarHeight, 아래 행부터). 머리는 이 안쪽에만 그려진다.")]
    private bool[] insideMask = new bool[0];
    [SerializeField, Tooltip("머리 바깥 1px 테두리 색(바 프레임 색).")]
    private Color borderColor = new Color(0.23f, 0.16f, 0.12f, 1f);

    //상태 변수
    private RoguelikeJellyHead jelly;
    private float targetRatio;
    private float displayPixels;
    private int shownKilled = -1;
    private int shownTarget = -1;
    // 퇴장 연출(숫자가 사라지는 동안)이 끝난 뒤 오브젝트를 끈다.
    private bool bHiding;
    private float hideClock;

    /// <summary>처치 진행도. 채움 = _killed / _target, 숫자 = "_killed/_target".</summary>
    public void SetProgress(int _killed, int _target)
    {
        ApplyValueText(_killed, _target);
        targetRatio = 0 < _target ? Mathf.Clamp01((float)_killed / _target) : 0f;
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

    /// <summary>처음 모습으로 되돌린다(런이 시작·종료될 때).</summary>
    public void ResetBar()
    {
        targetRatio = 0f;
        displayPixels = 0f;
        shownKilled = -1;
        shownTarget = -1;
        if (null != iconNumber) iconNumber.HideImmediate();
        if (null != GetJelly()) jelly.Reset();
        ApplyVisuals();
    }

    /// <summary>
    /// _dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 실제 시간 없이 이 메서드로 시간대별 모습을 만들 수 있다.
    /// </summary>
    public void Step(float _dt)
    {
        float previous = displayPixels;
        float targetPixels = 1f <= targetRatio ? BarWidth : targetRatio * (BarWidth - EndReserve);
        displayPixels = Mathf.Abs(targetPixels - displayPixels) < 0.01f ? targetPixels : displayPixels + (targetPixels - displayPixels) * (1f - Mathf.Exp(-_dt / FillTau));

        if (null != GetJelly()) jelly.Step(_dt, displayPixels, previous);
        ApplyVisuals();
    }

    // // 내부

    private RoguelikeJellyHead GetJelly()
    {
        if (null != jelly) return jelly;
        if (null == headSurface) return null;

        jelly = new RoguelikeJellyHead(headSurface, BarWidth, BarHeight, insideMask, borderColor);
        jelly.SetRowColors(rowColors);
        return jelly;
    }

    /// <summary>"처치 수/목표 처치 수" 숫자를 갱신한다. 값이 그대로면 아무것도 하지 않는다.</summary>
    private void ApplyValueText(int _killed, int _target)
    {
        if (_killed == shownKilled && _target == shownTarget) return;

        shownKilled = _killed;
        shownTarget = _target;

        if (null != iconNumber) iconNumber.SetPair(_killed, _target);
    }

    /// <summary>채움을 정수 픽셀로 반영한다(채움 앞쪽 열은 머리와 같은 Floor 규칙).</summary>
    private void ApplyVisuals()
    {
        int fillPixels = Mathf.Min(BarWidth, Mathf.FloorToInt(displayPixels));

        if (null != fillImage) fillImage.fillAmount = fillPixels / (float)BarWidth;
    }

    private void Update()
    {
        if (true == bHiding)
        {
            hideClock += Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds);
            if (UI_RoguelikeIconNumber.ExitSeconds + 0.02f <= hideClock)
            {
                bHiding = false;
                gameObject.SetActive(false);
            }
        }

        Step(Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds));
    }

    private void OnDisable()
    {
        bHiding = false;
    }

    private void OnDestroy()
    {
        if (null != jelly) jelly.Dispose();
    }
}
