/// <summary>
/// 임계치 스태미너 손실 판정. 기획서 v3.3 3-2 "스태미너 손실 판정 흐름".
///
///   안전   : 나무 수 &lt; T. 손실 없음.
///   손실 중: 나무 수 ≥ T. 초당 스태미너 손실(감소 반영)을 프레임마다 깎는다. T 미만이 되면 즉시 안전(초기화할 타이머 없음).
/// 손실 중에는 1초마다 펄스(연출 신호)를 낸다. 들어가는 순간 첫 펄스, 이후 1초마다. 펄스는 손실 계산과 무관하다.
///
/// 게임 시간과 무관한 순수 판정이다. 시간 흐름(dt)과 나무 수는 매 틱 바깥에서 넣어 준다.
/// 런 컨트롤러는 웨이브 전투 중에만 Tick을 부르므로, 카드 선택·연출 대기 중에는 판정이 멈춘다.
/// </summary>
public sealed class StaminaPressureLogic
{
    /// <summary>손실 중 펄스(바 번쩍임 · 효과음) 주기(초).</summary>
    public const float PulseInterval = 1f;

    public EPressureState State { get; private set; } = EPressureState.Safe;

    /// <summary>현재 임계치 T.</summary>
    public int Threshold { get; private set; } = 1;

    /// <summary>감소를 반영한 초당 스태미너 손실. 상태와 무관하게 지금 웨이브 값이다(스태미너 바 경고 판정 · 디버그 창이 읽는다).</summary>
    public float LossPerSecond { get; private set; }

    /// <summary>손실 중 마지막 펄스 이후 경과 시간(초).</summary>
    public float PulseElapsed { get; private set; }

    /// <summary>
    /// 웨이브 기본 초당 손실에 감소(%)를 적용한 값. 감소는 0 ~ _maxReducePercent(밸런스 상한, 70이면 손실이 30% 밑으로 내려가지 않는다)로 자른다.
    /// 공통 패시브 · 인내의 시간 계약의 감소를 합친 값을 _reducePercent로 넣는다(기획서 v3.3 3-2 · 6-4).
    /// </summary>
    public static float ReducedLoss(float _baseLossPerSecond, float _reducePercent, float _maxReducePercent)
    {
        float cap = _maxReducePercent < 0f ? 0f : _maxReducePercent;
        float reduce = _reducePercent < 0f ? 0f : (_reducePercent > cap ? cap : _reducePercent);
        return _baseLossPerSecond * (1f - reduce / 100f);
    }

    public void Configure(int _threshold, float _lossPerSecond)
    {
        Threshold = _threshold < 1 ? 1 : _threshold;
        LossPerSecond = _lossPerSecond < 0f ? 0f : _lossPerSecond;
    }

    public void Reset()
    {
        State = EPressureState.Safe;
        PulseElapsed = 0f;
    }

    /// <summary>
    /// _deltaTime만큼 시간을 흘리고 이번 틱에 깎을 스태미너 양을 돌려준다(안전이면 0). _bPulse는 이번 틱에 펄스가 났는지.
    /// 프레임이 크게 튀어도 펄스는 한 번만 난다.
    /// </summary>
    public float Tick(float _deltaTime, int _treeCount, out bool _bPulse)
    {
        _bPulse = false;

        if (_treeCount < Threshold)
        {
            Reset();
            return 0f;
        }

        if (_deltaTime < 0f) _deltaTime = 0f;

        if (EPressureState.Safe == State)
        {
            // 임계치를 넘는 순간: 바로 손실 중으로 들어가고 첫 펄스를 낸다.
            State = EPressureState.Losing;
            PulseElapsed = 0f;
            _bPulse = true;
        }
        else
        {
            PulseElapsed += _deltaTime;
            if (PulseElapsed >= PulseInterval)
            {
                PulseElapsed -= PulseInterval;
                if (PulseElapsed >= PulseInterval) PulseElapsed %= PulseInterval;
                _bPulse = true;
            }
        }

        return LossPerSecond * _deltaTime;
    }
}
