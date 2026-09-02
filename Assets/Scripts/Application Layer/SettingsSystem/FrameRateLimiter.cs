using System.Collections;
using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// 프레임 상한을 엔진 설정과 독립적으로 보장하는 안전장치입니다.
///
/// QualitySettings.vSyncCount와 Application.targetFrameRate는 둘 다 그래픽 드라이버에
/// 덮어써질 수 있습니다. 특히 이 프로젝트처럼 테두리 없는 창 + flip model 조합에서
/// 드라이버가 "수직 동기화: 끄기"를 강제하면 앱이 건 상한이 통째로 무시되어
/// 프레임이 무제한으로 치솟습니다. 이 컴포넌트는 메인 스레드를 직접 대기시키므로
/// 그런 환경에서도 유저가 고른 상한이 지켜집니다.
///
/// 대기 예산을 목표 프레임 간격보다 의도적으로 조금 짧게 잡는 것이 핵심입니다.
/// VSync가 정상 동작하는 환경에서는 예산이 이미 충족되어 있어 리미터가 한 번도
/// 개입하지 않고, 따라서 리미터가 vblank를 밀어내 프레임을 떨어뜨리는 일이 없습니다.
///
/// 상한을 정하는 주체는 SettingsManager 단독입니다. 다른 곳에서 SetLimit을 호출하지 마세요.
/// </summary>
public class FrameRateLimiter : MonoBehaviour
{
    /// <summary>
    /// 목표 프레임 간격 대비 실제 대기 예산의 비율입니다.
    /// 1보다 작아야 VSync가 살아있는 환경에서 리미터가 개입하지 않습니다.
    /// 대신 드라이버가 VSync를 꺼버린 환경에서의 실제 상한은 목표보다 약 5% 높아집니다.
    /// (60 선택 시 약 63fps. 상한이 아예 없어 200+로 치솟던 것에 비하면 무시할 수준입니다)
    /// </summary>
    private const double budgetRatio = 0.95;

    /// <summary>
    /// 남은 대기 시간이 이 값 이하로 줄면 Sleep 대신 스핀으로 전환합니다.
    /// Thread.Sleep의 실제 해상도는 OS 타이머 설정에 따라 수 ms까지 거칠어질 수 있어,
    /// 마지막 구간까지 Sleep에 맡기면 목표 시점을 넘겨서 깨어납니다.
    /// </summary>
    private const double spinThresholdSeconds = 0.004;

    // Time.realtimeSinceStartup은 프레임 경계에서만 갱신되므로 프레임 내부 대기에 쓸 수 없습니다.
    private static readonly Stopwatch clock = Stopwatch.StartNew();

    /// <summary>다음 프레임을 통과시킬 목표 시각(초)입니다. 매 프레임 예산만큼 전진합니다.</summary>
    private double nextFrameTime;

    /// <summary>0 이하이면 제한하지 않습니다.</summary>
    private int limitFps;

    /// <summary>
    /// 프레임 상한을 설정합니다. 0 이하(Unlimited의 -1 포함)를 넘기면 제한을 해제합니다.
    /// </summary>
    public void SetLimit(int _fps)
    {
        limitFps = _fps;

        // 상한이 바뀌면 이전 목표 시각은 의미가 없으므로 기준선을 지금으로 다시 잡습니다.
        nextFrameTime = clock.Elapsed.TotalSeconds;
    }

    /// <summary>
    /// 렌더링과 프레젠트가 끝난 뒤에 대기해야 실제 프레임 간격이 제한됩니다.
    /// Update/LateUpdate에서 대기하면 렌더링 직전에 멈추는 셈이라 입력 지연만 늘어납니다.
    /// </summary>
    private IEnumerator Start()
    {
        WaitForEndOfFrame _endOfFrame = new WaitForEndOfFrame();

        while (true)
        {
            yield return _endOfFrame;
            WaitForBudget();
        }
    }

    private void WaitForBudget()
    {
        if (limitFps <= 0) return;

        nextFrameTime += (1.0 / limitFps) * budgetRatio;

        double _now = clock.Elapsed.TotalSeconds;

        // 이미 예산을 넘겼다면(로딩 스파이크, 창 비활성화 등) 밀린 만큼 따라잡지 않고
        // 기준선을 현재로 리셋합니다. 누적된 빚을 갚으려 들면 이후 수십 프레임이
        // 대기 없이 통과해 오히려 프레임이 폭주합니다.
        if (nextFrameTime <= _now)
        {
            nextFrameTime = _now;
            return;
        }

        double _remain = nextFrameTime - _now;

        // 대부분의 시간은 Sleep으로 넘겨 CPU를 놓아주고, 마지막 구간만 스핀으로 정밀하게 맞춥니다.
        if (_remain > spinThresholdSeconds)
        {
            Thread.Sleep((int)((_remain - spinThresholdSeconds) * 1000.0));
        }

        while (clock.Elapsed.TotalSeconds < nextFrameTime)
        {
            Thread.SpinWait(50);
        }
    }
}
