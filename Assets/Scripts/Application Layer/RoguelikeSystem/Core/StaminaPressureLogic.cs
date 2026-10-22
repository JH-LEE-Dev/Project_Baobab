/// <summary>
/// 임계치 피해 판정. 기획서 3-2 "피해 판정 흐름"을 그대로 옮겼다.
///
///   안전   : 나무 수 &lt; T. 피해 없음, 타이머 초기 상태.
///   유예 중: 나무 수 ≥ T가 되면 유예 타이머가 돈다. 그 안에 T 미만으로 내리면 초기화되고 안전으로 돌아간다.
///   첫 피해: 유예 타이머가 끝나면 1회 피해.
///   지속   : 첫 피해 이후에도 T 이상이면 쿨다운마다 반복 피해.
///   복귀   : T 미만이 되면 유예·쿨다운 모두 초기화. 다시 T 이상이 되면 이전 쿨다운과 무관하게 유예부터 다시 시작.
/// 기획서 v2.7부터 반복 피해 간격은 유예 시간과 같은 값이다. 런 컨트롤러가 쿨다운에 유예 시간을 그대로 넣는다(ConfigurePressure).
///
/// 게임 시간과 무관한 순수 판정이다. 시간 흐름(dt)과 나무 수는 매 틱 바깥에서 넣어 준다.
/// 런 컨트롤러는 웨이브 전투 중에만 Tick을 부르므로, 카드 선택·연출 대기 중에는 판정이 멈춘다.
/// </summary>
public sealed class StaminaPressureLogic
{
    public EPressureState State { get; private set; } = EPressureState.Safe;

    /// <summary>현재 임계치 T.</summary>
    public int Threshold { get; private set; } = 1;
    public float GraceTime { get; private set; } = 1f;
    public float DamageCooldown { get; private set; } = 1f;

    /// <summary>유예 중 경과 시간(초).</summary>
    public float GraceElapsed { get; private set; }
    /// <summary>지속 피해 중 마지막 피해 이후 경과 시간(초).</summary>
    public float CooldownElapsed { get; private set; }

    /// <summary>유예 진행도 0~1. HUD의 임계치 바가 그린다. 유예 중이 아니면 0, 지속 피해 중이면 1.</summary>
    public float GraceRatio
    {
        get
        {
            if (EPressureState.Damaging == State) return 1f;
            if (EPressureState.Grace != State || GraceTime <= 0f) return 0f;
            float ratio = GraceElapsed / GraceTime;
            return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
        }
    }

    /// <summary>지속 피해 중 다음 피해까지의 진행도 0~1.</summary>
    public float CooldownRatio
    {
        get
        {
            if (EPressureState.Damaging != State || DamageCooldown <= 0f) return 0f;
            float ratio = CooldownElapsed / DamageCooldown;
            return ratio < 0f ? 0f : (ratio > 1f ? 1f : ratio);
        }
    }

    public void Configure(int _threshold, float _graceTime, float _damageCooldown)
    {
        Threshold = _threshold < 1 ? 1 : _threshold;
        GraceTime = _graceTime < 0f ? 0f : _graceTime;
        DamageCooldown = _damageCooldown < 0.01f ? 0.01f : _damageCooldown;
    }

    public void Reset()
    {
        State = EPressureState.Safe;
        GraceElapsed = 0f;
        CooldownElapsed = 0f;
    }

    /// <summary>
    /// _deltaTime만큼 시간을 흘리고, 이번 틱에 발생한 피해 횟수를 돌려준다(보통 0 또는 1).
    /// 프레임이 크게 튀어도 쿨다운 여러 번 분량을 한 번에 돌려줄 수 있다.
    /// </summary>
    public int Tick(float _deltaTime, int _treeCount)
    {
        if (_treeCount < Threshold)
        {
            Reset();
            return 0;
        }

        if (_deltaTime < 0f) _deltaTime = 0f;

        int hits = 0;

        if (EPressureState.Safe == State)
        {
            State = EPressureState.Grace;
            GraceElapsed = 0f;
        }

        if (EPressureState.Grace == State)
        {
            GraceElapsed += _deltaTime;
            if (GraceElapsed < GraceTime) return 0;

            // 유예가 끝난 순간 첫 피해. 남은 시간은 쿨다운으로 넘긴다.
            hits++;
            State = EPressureState.Damaging;
            CooldownElapsed = GraceElapsed - GraceTime;
            GraceElapsed = GraceTime;
            _deltaTime = 0f;
        }
        else
        {
            CooldownElapsed += _deltaTime;
        }

        while (CooldownElapsed >= DamageCooldown)
        {
            CooldownElapsed -= DamageCooldown;
            hits++;
        }

        return hits;
    }
}
