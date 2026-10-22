using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 유예 타이머(기획서 v2.6 10-1). 임계치 바 바로 아래의 폭 120 x 높이 4 얇은 바다. 초 숫자는 임계치 바 오른쪽(UI_RoguelikePressureStrip.SetTimer)이 그린다.
///   대기      나무 수 &lt; T. 채움 없음(비어 있음).
///   유예 중   보라색 채움이 0에서 가득까지 찬다(경과 / 총 유예 시간).
///   쿨다운    장미주황색 채움이 가득에서 줄어든다(다음 피해까지 남은 시간). 첫 피해 이후 T 이상인 동안.
///   피해 발생 PlayDamaged: 바가 4틱 동안 번갈아 하얗게 번쩍이고(24Hz) 정수 픽셀 패턴 { -2, 2, -2, 2, -1, 1, 0 }으로 흔들린다.
/// 나무 수가 T 미만으로 내려가면 상태가 Safe로 오고 채움이 즉시 비워진다(기획서 3-2). 채움 폭은 정수 픽셀이고 반투명이 없다. 시간은 unscaled라 ESC로 멈춰도 연출이 이어진다.
/// </summary>
public class UI_RoguelikeGraceTimer : MonoBehaviour
{
    private const int InnerWidth = 118;
    private const int SpriteWidth = 120;
    private const float TicksPerSecond = 24f;
    private const float ShakeStepSeconds = 0.03f;
    private const int FlashTicks = 4;
    private static readonly int[] ShakePattern = { -2, 2, -2, 2, -1, 1, 0 };

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform timerRoot;
    [SerializeField, Tooltip("프레임 안쪽 모양으로 만든 흰 채움 스프라이트(Image Filled). 프레임의 둥근 끝 밖으로 삐져나오지 않는다.")]
    private Image fillImage;
    [SerializeField] private Image flashImage;
    [SerializeField] private Color graceColor = new Color(0.553f, 0.416f, 0.839f, 1f);
    [SerializeField] private Color cooldownColor = new Color(0.933f, 0.478f, 0.353f, 1f);

    //상태 변수
    private float clock;
    private Vector2 basePosition;
    private bool bBasePositionCaptured;
    private float shakeTime = -1f;
    private int flashStartTick = -1;
    private int shownWidth = -1;
    private EPressureState shownState = EPressureState.Safe;

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
        if (width == shownWidth && _state == shownState) return;

        shownWidth = width;
        shownState = _state;

        if (null == fillImage) return;

        fillImage.color = EPressureState.Damaging == _state ? cooldownColor : graceColor;
        fillImage.enabled = 0 < width;
        fillImage.fillAmount = 0 < width ? (width + 1) / (float)SpriteWidth : 0f;
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

    /// <summary>_dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 이 메서드로 시간대별 모습을 만들 수 있다.</summary>
    public void Step(float _dt)
    {
        clock += _dt;
        StepPosition(_dt);
        StepFlash();
    }

    // // 내부

    private int CurrentTick()
    {
        return (int)(clock * TicksPerSecond);
    }

    private void StepPosition(float _dt)
    {
        if (null == timerRoot) return;

        if (false == bBasePositionCaptured)
        {
            basePosition = timerRoot.anchoredPosition;
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

        timerRoot.anchoredPosition = new Vector2(basePosition.x + ShakePattern[step], basePosition.y);
    }

    private void RestorePosition()
    {
        if (null != timerRoot && bBasePositionCaptured) timerRoot.anchoredPosition = basePosition;
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
