using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임패드 진동을 담당합니다. "무엇이 언제 진동할지"는 여기서 정하지 않고, 호출부가 정합니다.
///
/// 반드시 지켜야 하는 것 세 가지 (전부 실제로 자주 터지는 버그입니다):
/// 1. 게임이 포커스를 잃거나 종료되면 즉시 멈춘다. 안 그러면 알트탭하거나 게임을 껐는데도
///    패드가 계속 울린다. 진동은 게임이 아니라 장치 쪽에 남는 상태라서 스스로 꺼지지 않는다.
/// 2. unscaled 시간을 쓴다. Time.timeScale = 0으로 일시정지하면 진동이 영원히 안 끝난다.
/// 3. 세기 설정이 0이면 모터를 아예 건드리지 않는다.
///
/// 겹침 정책은 "더 강한 쪽이 이긴다"입니다. 약하고 긴 진동(예: 엔진 아이들)이 도중에 들어온
/// 강한 타격 진동을 덮어써 버리면 타격감이 사라지기 때문입니다.
///
/// 한 가지 세기를 일정 시간 유지하는 Play와, 여러 구간이 이어진 파형을 재생하는 PlayPattern이
/// 있습니다. 중간에 세기 0인 구간을 넣으면 진동이 끊겼다 다시 걸리므로, 시동을 거는 느낌처럼
/// 단순한 "세게/약하게"로는 안 나오는 감각을 만들 수 있습니다.
/// </summary>
public class GamepadHaptics
{
    // 유저 설정(0~1). 0이면 진동 기능 자체를 끈 것으로 본다.
    private float strengthScale = 1f;

    private float currentLowFrequency = 0f;
    private float currentHighFrequency = 0f;
    private float remainSeconds = 0f;

    private bool bMotorsRunning = false;

    // 재생 중인 파형. null이면 Play로 들어온 단일 세기 진동이다.
    private HapticStep[] patternSteps = null;
    private int patternIndex = 0;
    private float patternPeak = 0f;

    // 포커스를 잃어 일시적으로 멈춘 상태. 남은 시간은 그대로 두고 모터만 멈춘다.
    private bool bSuspended = false;

    /// <summary>현재 진동이 재생 중인지입니다. (포커스 상실로 일시 중단된 상태도 포함)</summary>
    public bool IsPlaying => remainSeconds > 0f;

    /// <summary>진동을 낼 수 있는 상태인지입니다. 패드가 없거나 세기가 0이면 false입니다.</summary>
    public bool CanPlay => strengthScale > 0f && null != Gamepad.current;

    // 겹침 비교용 "지금 재생 중인 것의 세기". 파형은 중간에 약해지는 구간이 있어도 전체 피크로
    // 비교해야 한다. 안 그러면 강한 파형의 잦아드는 꼬리에 약한 진동이 끼어들어 파형이 망가진다.
    private float CurrentPeak => null != patternSteps
        ? patternPeak
        : Mathf.Max(currentLowFrequency, currentHighFrequency);

    /// <summary>
    /// 진동 세기 배율을 설정합니다. (0 = 끔, 1 = 최대)
    /// 0으로 내리면 재생 중인 진동도 즉시 멈춥니다.
    /// </summary>
    public void SetStrengthScale(float _scale01)
    {
        strengthScale = Mathf.Clamp01(_scale01);

        if (0f == strengthScale)
        {
            Stop();
            return;
        }

        // 재생 중이라면 새 세기를 곧바로 반영한다. (옵션 슬라이더를 드래그하는 동안의 미리듣기)
        if (true == bMotorsRunning)
        {
            ApplyMotorSpeeds();
        }
    }

    /// <summary>
    /// 진동을 재생합니다.
    /// </summary>
    /// <param name="_lowFrequency">저주파(굵은) 모터 세기 0~1. 폭발·피격처럼 묵직한 느낌.</param>
    /// <param name="_highFrequency">고주파(가는) 모터 세기 0~1. 잔진동·기계음 같은 느낌.</param>
    /// <param name="_duration">지속 시간(초). 0 이하이면 아무 일도 하지 않습니다.</param>
    public void Play(float _lowFrequency, float _highFrequency, float _duration)
    {
        if (_duration <= 0f) return;
        if (0f == strengthScale) return;

        float _low = Mathf.Clamp01(_lowFrequency);
        float _high = Mathf.Clamp01(_highFrequency);

        if (0f == _low && 0f == _high) return;

        // 겹침 처리: 재생 중인 진동보다 약한 요청은 무시한다.
        // 두 모터 중 큰 값을 세기로 삼아 비교한다.
        if (true == IsPlaying)
        {
            float _incoming = Mathf.Max(_low, _high);

            if (_incoming < CurrentPeak)
            {
                // 약한 요청이라도 더 오래 끌고 가야 한다면 남은 시간만 늘려준다.
                // 단, 파형 재생 중에는 늘리면 안 된다. 여기서 늘어나는 것은 "파형의 현재 구간"이라
                // 구간 하나만 길어져 파형이 뭉개진다.
                if (null == patternSteps && _duration > remainSeconds) remainSeconds = _duration;
                return;
            }
        }

        patternSteps = null;
        currentLowFrequency = _low;
        currentHighFrequency = _high;
        remainSeconds = _duration;

        if (false == bSuspended)
        {
            ApplyMotorSpeeds();
        }
    }

    /// <summary>
    /// 여러 구간이 이어진 파형을 재생합니다. 겹침 규칙은 Play와 같고, 비교 기준은 파형의 피크입니다.
    /// </summary>
    public void PlayPattern(HapticPattern _pattern)
    {
        if (null == _pattern || 0 == _pattern.steps.Length) return;
        if (0f == strengthScale) return;
        if (0f == _pattern.peak) return;

        if (true == IsPlaying && _pattern.peak < CurrentPeak) return;

        patternSteps = _pattern.steps;
        patternIndex = 0;
        patternPeak = _pattern.peak;

        HapticStep _first = patternSteps[0];
        currentLowFrequency = _first.lowFrequency;
        currentHighFrequency = _first.highFrequency;
        remainSeconds = _first.duration;

        if (false == bSuspended)
        {
            ApplyMotorSpeeds();
        }
    }

    /// <summary>진동을 즉시 멈추고 상태를 초기화합니다.</summary>
    public void Stop()
    {
        patternSteps = null;
        patternIndex = 0;
        patternPeak = 0f;

        currentLowFrequency = 0f;
        currentHighFrequency = 0f;
        remainSeconds = 0f;

        StopMotors();
    }

    /// <summary>
    /// 매 프레임 호출합니다. 반드시 unscaled 델타를 넘기세요.
    /// (일시정지 중에 스케일된 델타를 넘기면 진동이 영원히 끝나지 않습니다)
    /// </summary>
    public void Tick(float _unscaledDeltaTime)
    {
        if (false == IsPlaying) return;

        remainSeconds -= _unscaledDeltaTime;

        if (remainSeconds <= 0f)
        {
            if (null == patternSteps)
            {
                Stop();
                return;
            }

            // 넘친 시간은 다음 구간에서 이어서 소모한다. 프레임이 한 번 길게 튀어도 짧은 구간들이
            // 통째로 건너뛰어지지 않고, 파형 전체 길이도 늘어나지 않는다.
            if (false == AdvancePattern(-remainSeconds))
            {
                return;
            }
        }

        // 재생 도중 패드가 교체·재연결되면 새 패드에는 모터 값이 설정되어 있지 않다.
        // 매 프레임 다시 써 주면 그 경우가 자동으로 복구된다.
        if (false == bSuspended)
        {
            ApplyMotorSpeeds();
        }
    }

    /// <summary>
    /// 파형의 다음 구간으로 넘어갑니다. 파형이 끝나 정지했다면 false를 반환합니다.
    /// </summary>
    private bool AdvancePattern(float _overflowSeconds)
    {
        while (true)
        {
            patternIndex++;

            if (patternIndex >= patternSteps.Length)
            {
                Stop();
                return false;
            }

            HapticStep _step = patternSteps[patternIndex];

            if (_step.duration > _overflowSeconds)
            {
                currentLowFrequency = _step.lowFrequency;
                currentHighFrequency = _step.highFrequency;
                remainSeconds = _step.duration - _overflowSeconds;
                return true;
            }

            _overflowSeconds -= _step.duration;
        }
    }

    /// <summary>
    /// 애플리케이션 포커스 변화를 알립니다. InputManager의 OnApplicationFocus에서 호출합니다.
    /// 포커스를 잃으면 모터를 멈추되 남은 시간은 유지하므로, 돌아오면 이어서 재생됩니다.
    /// </summary>
    public void SetApplicationFocus(bool _bFocused)
    {
        bSuspended = false == _bFocused;

        if (true == bSuspended)
        {
            StopMotors();
            return;
        }

        if (true == IsPlaying)
        {
            ApplyMotorSpeeds();
        }
    }

    /// <summary>
    /// 종료·씬 정리 시 호출합니다. 진동이 장치에 남지 않도록 확실히 끕니다.
    /// </summary>
    public void Release()
    {
        Stop();
    }

    private void ApplyMotorSpeeds()
    {
        Gamepad _gamepad = Gamepad.current;
        if (null == _gamepad) return;

        float _low = currentLowFrequency * strengthScale;
        float _high = currentHighFrequency * strengthScale;

        // 파형 중간의 "쉬는 구간". 재생 상태(남은 시간·다음 구간)는 그대로 두고 모터만 멈춘다.
        // 여기서 Stop()을 부르면 파형이 첫 공백에서 끝나 버린다.
        if (0f == _low && 0f == _high)
        {
            if (true == bMotorsRunning) StopMotors();
            return;
        }

        _gamepad.SetMotorSpeeds(_low, _high);
        bMotorsRunning = true;
    }

    private void StopMotors()
    {
        bMotorsRunning = false;

        Gamepad _gamepad = Gamepad.current;
        if (null == _gamepad) return;

        // SetMotorSpeeds(0, 0)이 아니라 ResetHaptics를 쓴다.
        // 전자는 "0의 세기로 계속 진동 중"인 상태로 남아 일부 드라이버에서 모터가 완전히 멎지 않는다.
        _gamepad.ResetHaptics();
    }
}
