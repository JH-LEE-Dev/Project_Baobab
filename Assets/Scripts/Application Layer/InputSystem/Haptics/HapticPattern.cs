using UnityEngine;

/// <summary>
/// 진동 파형의 한 구간입니다. 두 모터 세기를 지정한 시간만큼 유지합니다.
/// 세기를 0/0으로 두면 "쉬는 구간"이 되어, 끊었다 다시 거는 느낌(시동 크랭킹 등)을 만들 수 있습니다.
/// </summary>
public readonly struct HapticStep
{
    public readonly float lowFrequency;
    public readonly float highFrequency;
    public readonly float duration;

    public HapticStep(float _lowFrequency, float _highFrequency, float _duration)
    {
        lowFrequency = Mathf.Clamp01(_lowFrequency);
        highFrequency = Mathf.Clamp01(_highFrequency);
        duration = _duration;
    }
}

/// <summary>
/// 여러 구간이 이어진 진동 파형입니다. 한 번 만들어 두고 계속 재사용하는 것을 전제로 합니다.
/// (HapticPresets의 static readonly 필드에만 만들어 두므로 재생할 때마다 할당이 생기지 않습니다)
///
/// class인 이유: 스텝 배열과 피크값을 함께 들고 다니는 "표"에 가까운 데이터라, 값 형식으로 두면
/// 호출할 때마다 배열 참조까지 통째로 복사됩니다. 인스턴스 수가 이벤트 종류만큼으로 고정이라
/// 힙에 남아도 GC 압박이 없습니다.
/// </summary>
public sealed class HapticPattern
{
    /// <summary>파형을 이루는 구간들입니다. 만들어진 뒤에는 바뀌지 않습니다.</summary>
    public readonly HapticStep[] steps;

    /// <summary>이 파형에서 가장 센 지점입니다. 겹침 처리("더 강한 쪽이 이긴다") 비교에 씁니다.</summary>
    public readonly float peak;

    /// <summary>파형 전체 길이(초)입니다. 이벤트별 재발동 간격을 정할 때 참고합니다.</summary>
    public readonly float totalDuration;

    public HapticPattern(params HapticStep[] _steps)
    {
        // 길이가 0 이하인 구간은 재생 중에 무한 루프처럼 보이는 상태를 만들 뿐이라 아예 걸러낸다.
        int _validCount = 0;

        if (null != _steps)
        {
            for (int i = 0; i < _steps.Length; i++)
            {
                if (_steps[i].duration > 0f) _validCount++;
            }
        }

        steps = new HapticStep[_validCount];

        int _index = 0;
        float _peak = 0f;
        float _total = 0f;

        for (int i = 0; i < _validCount; i++)
        {
            // 위에서 센 개수만큼만 채우므로 인덱스 범위를 다시 검사할 필요가 없다.
            while (_steps[_index].duration <= 0f) _index++;

            HapticStep _step = _steps[_index];
            steps[i] = _step;

            float _stepPeak = Mathf.Max(_step.lowFrequency, _step.highFrequency);
            if (_stepPeak > _peak) _peak = _stepPeak;

            _total += _step.duration;
            _index++;
        }

        peak = _peak;
        totalDuration = _total;
    }
}
